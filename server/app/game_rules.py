from __future__ import annotations

from dataclasses import dataclass
from enum import IntEnum
from math import sqrt
from typing import Iterable, Sequence

SCALE = 1_000_000
MARGIN = 120_000
START_Y = 150_000
END_Y = 850_000
SAMPLES_PER_SEGMENT = 16
MASK64 = (1 << 64) - 1


@dataclass(frozen=True, slots=True)
class Point:
    x: int
    y: int


@dataclass(frozen=True, slots=True)
class RecordedPoint:
    x: int
    y: int
    timestamp_ms: int


class Difficulty(IntEnum):
    EASY = 0
    MEDIUM = 1
    HARD = 2


@dataclass(frozen=True, slots=True)
class Route:
    seed: int
    generator_version: int
    difficulty: Difficulty
    display_time_ms: int
    path_width: int
    points: tuple[Point, ...]


class DeterministicRandom:
    def __init__(self, seed: int, generator_version: int):
        self.state = ((seed & MASK64) ^ ((generator_version & 0xFFFFFFFF) * 0xD1B54A32D192ED03 & MASK64)) & MASK64
        self.state = (self.state + 0x9E3779B97F4A7C15) & MASK64

    def next_u64(self) -> int:
        self.state = (self.state + 0x9E3779B97F4A7C15) & MASK64
        z = self.state
        z = ((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9) & MASK64
        z = ((z ^ (z >> 27)) * 0x94D049BB133111EB) & MASK64
        return (z ^ (z >> 31)) & MASK64

    def next_int(self, min_inclusive: int, max_inclusive: int) -> int:
        if max_inclusive < min_inclusive:
            raise ValueError("invalid range")
        return min_inclusive + self.next_u64() % (max_inclusive - min_inclusive + 1)

    def fork_seed(self, index: int) -> int:
        return (self.next_u64() ^ ((index & 0xFFFFFFFF) * 0x9E3779B97F4A7C15)) & 0x7FFFFFFF


def _clamp(v: int, lo: int, hi: int) -> int:
    return lo if v < lo else hi if v > hi else v


def _bezier(p0: Point, p1: Point, p2: Point, p3: Point, t: int, scale: int = SAMPLES_PER_SEGMENT) -> Point:
    u = scale - t
    den = scale * scale * scale
    w0 = u * u * u
    w1 = 3 * u * u * t
    w2 = 3 * u * t * t
    w3 = t * t * t
    return Point(
        (p0.x * w0 + p1.x * w1 + p2.x * w2 + p3.x * w3) // den,
        (p0.y * w0 + p1.y * w1 + p2.y * w2 + p3.y * w3) // den,
    )


def generate_route(seed: int, generator_version: int, difficulty: Difficulty) -> Route:
    if generator_version != 1:
        raise ValueError(f"unsupported generator_version={generator_version}")
    rng = DeterministicRandom(seed, generator_version)
    anchor_count = {Difficulty.EASY: 4, Difficulty.MEDIUM: 6, Difficulty.HARD: 8}[difficulty]
    bend = {Difficulty.EASY: 150_000, Difficulty.MEDIUM: 220_000, Difficulty.HARD: 280_000}[difficulty]
    width = {Difficulty.EASY: 55_000, Difficulty.MEDIUM: 45_000, Difficulty.HARD: 35_000}[difficulty]
    display = {Difficulty.EASY: 3500, Difficulty.MEDIUM: 3000, Difficulty.HARD: 2500}[difficulty]

    anchors: list[Point] = []
    for i in range(anchor_count):
        y = START_Y + ((END_Y - START_Y) * i) // (anchor_count - 1)
        x = 500_000 + rng.next_int(-80_000, 80_000) if i in (0, anchor_count - 1) else rng.next_int(MARGIN, SCALE - MARGIN)
        anchors.append(Point(x, y))

    points = [anchors[0]]
    for p0, p3 in zip(anchors, anchors[1:]):
        dy = p3.y - p0.y
        c1 = Point(_clamp(p0.x + rng.next_int(-bend, bend), MARGIN, SCALE - MARGIN), p0.y + dy // 3)
        c2 = Point(_clamp(p3.x + rng.next_int(-bend, bend), MARGIN, SCALE - MARGIN), p0.y + (2 * dy) // 3)
        for step in range(1, SAMPLES_PER_SEGMENT + 1):
            points.append(_bezier(p0, c1, c2, p3, step))

    return Route(seed, generator_version, difficulty, display, width, tuple(points))


def validate_route(route: Route) -> bool:
    if len(route.points) < 2:
        return False
    previous_y = -1
    for p in route.points:
        if not (MARGIN <= p.x <= SCALE - MARGIN):
            return False
        if not (START_Y <= p.y <= END_Y):
            return False
        if p.y < previous_y:
            return False
        previous_y = p.y
    a, b = route.points[0], route.points[-1]
    return (a.x - b.x) ** 2 + (a.y - b.y) ** 2 > 250_000**2


def create_daily_routes(seed: int, generator_version: int = 1) -> tuple[Route, Route, Route]:
    seeder = DeterministicRandom(seed, generator_version)
    return (
        generate_route(seeder.fork_seed(0), generator_version, Difficulty.EASY),
        generate_route(seeder.fork_seed(1), generator_version, Difficulty.MEDIUM),
        generate_route(seeder.fork_seed(2), generator_version, Difficulty.HARD),
    )


def _nearest(reference: Sequence[Point], point: RecordedPoint) -> tuple[float, float]:
    best_sq = float("inf")
    best_progress = 0.0
    for i, (a, b) in enumerate(zip(reference, reference[1:])):
        abx, aby = b.x - a.x, b.y - a.y
        apx, apy = point.x - a.x, point.y - a.y
        len_sq = abx * abx + aby * aby
        t = 0.0 if len_sq <= 0 else max(0.0, min(1.0, (apx * abx + apy * aby) / len_sq))
        qx, qy = a.x + abx * t, a.y + aby * t
        dist_sq = (point.x - qx) ** 2 + (point.y - qy) ** 2
        if dist_sq < best_sq:
            best_sq = dist_sq
            best_progress = i + t
    return sqrt(best_sq), best_progress


def score_route(route: Route, user_points: Sequence[RecordedPoint]) -> float:
    if len(user_points) < 2:
        return 0.0
    distances: list[float] = []
    max_progress = 0.0
    gross = 0
    for p in user_points:
        distance, progress = _nearest(route.points, p)
        distances.append(distance)
        max_progress = max(max_progress, progress)
        if distance > route.path_width * 3.0:
            gross += 1
    mean = sum(distances) / len(distances)
    completion = max(0.0, min(1.0, max_progress / (len(route.points) - 1)))
    end = route.points[-1]
    last = user_points[-1]
    end_distance = sqrt((last.x - end.x) ** 2 + (last.y - end.y) ** 2)
    end_accuracy = max(0.0, min(1.0, 1.0 - end_distance / (route.path_width * 2.0)))
    distance_score = max(0.0, min(1.0, 1.0 - mean / (route.path_width * 2.5)))
    gross_score = 1.0 - gross / len(user_points)
    score = 100.0 * (0.70 * distance_score + 0.18 * completion + 0.08 * end_accuracy + 0.04 * gross_score)
    if completion < 0.5:
        score *= completion / 0.5
    return round(max(0.0, min(100.0, score)), 1)


def validate_replay(points: Sequence[RecordedPoint], duration_ms: int) -> tuple[bool, str | None]:
    if len(points) < 2 or len(points) > 6000:
        return False, "invalid_point_count"
    if duration_ms <= 0 or duration_ms > 120_000:
        return False, "invalid_duration"
    previous = -1
    for p in points:
        if not (0 <= p.x <= SCALE and 0 <= p.y <= SCALE):
            return False, "point_out_of_bounds"
        if p.timestamp_ms < previous:
            return False, "non_monotonic_timestamps"
        previous = p.timestamp_ms
    if points[-1].timestamp_ms > duration_ms + 1000:
        return False, "timestamp_exceeds_duration"
    return True, None
