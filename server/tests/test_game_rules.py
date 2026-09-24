from app.game_rules import Difficulty, RecordedPoint, create_daily_routes, generate_route, score_route, validate_route


def test_same_seed_is_deterministic():
    assert generate_route(123456, 1, Difficulty.MEDIUM) == generate_route(123456, 1, Difficulty.MEDIUM)


def test_1000_routes_are_valid():
    for seed in range(1, 1001):
        route = generate_route(seed, 1, Difficulty(seed % 3))
        assert validate_route(route), seed


def test_exact_replay_is_100_percent():
    route = generate_route(42, 1, Difficulty.HARD)
    replay = [RecordedPoint(p.x, p.y, i * 16) for i, p in enumerate(route.points)]
    assert score_route(route, replay) == 100.0


def test_daily_has_three_difficulties():
    routes = create_daily_routes(999, 1)
    assert [r.difficulty for r in routes] == [Difficulty.EASY, Difficulty.MEDIUM, Difficulty.HARD]
