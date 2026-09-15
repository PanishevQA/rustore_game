from __future__ import annotations

import hashlib
import os
import secrets
from datetime import date, datetime, timezone
from html import escape
from typing import Annotated

from fastapi import FastAPI, HTTPException, Query
from fastapi.responses import HTMLResponse
from pydantic import BaseModel, Field

from .game_rules import RecordedPoint, create_daily_routes, score_route, validate_replay
from .store import Store

app = FastAPI(title="НЕ СБЕЙСЯ! API", version="0.1.0")
DB_PATH = os.getenv("GAME_DB_PATH", os.path.join(os.path.dirname(__file__), "..", "data", "game.db"))
PUBLIC_BASE_URL = os.getenv("PUBLIC_BASE_URL", "http://localhost:8000").rstrip("/")
RUSTORE_PACKAGE_NAME = os.getenv("RUSTORE_PACKAGE_NAME", "ru.panishedqa.nesbeisya.dev")
DAILY_SECRET = os.getenv("DAILY_SECRET", "dev-only-change-me")
GENERATOR_VERSION = 1
store = Store(DB_PATH)

ANALYTICS_EVENT_NAMES = {
    "app_open",
    "session_start",
    "tutorial_start",
    "tutorial_complete",
    "daily_start",
    "daily_complete",
    "round_start",
    "round_complete",
    "round_failed",
    "score_generated",
    "personal_best",
    "share_click",
    "share_complete",
    "challenge_open",
    "challenge_complete",
    "rewarded_offer",
    "rewarded_start",
    "rewarded_complete",
    "interstitial_show",
    "store_open",
    "purchase_start",
    "purchase_success",
    "purchase_cancel",
    "purchase_error",
    "review_flow_request",
    "push_permission_request",
    "push_permission_result",
}


class PointDto(BaseModel):
    x: int
    y: int
    timestampMs: int = Field(ge=0)


class RouteAttemptDto(BaseModel):
    routeIndex: int = Field(ge=0, le=2)
    durationMs: int = Field(gt=0, le=120_000)
    clientScore: float = Field(ge=0, le=100)
    points: list[PointDto]


class AttemptRequest(BaseModel):
    playerId: str = Field(min_length=4, max_length=128)
    challengeId: str
    generatorVersion: int
    assisted: bool = False
    routes: list[RouteAttemptDto] = Field(min_length=3, max_length=3)


class ChallengeRequest(BaseModel):
    inviterId: str = Field(min_length=4, max_length=128)
    challengeId: str
    score: float = Field(ge=0, le=100)


class AnalyticsParameterDto(BaseModel):
    key: str = Field(min_length=1, max_length=64)
    value: str = Field(max_length=512)


class AnalyticsEventDto(BaseModel):
    eventId: str = Field(min_length=8, max_length=64)
    playerId: str = Field(min_length=4, max_length=128)
    sessionNumber: int = Field(ge=1)
    eventName: str = Field(min_length=1, max_length=64)
    occurredAtUtc: datetime
    parameters: list[AnalyticsParameterDto] = Field(default_factory=list, max_length=64)


class AnalyticsBatchRequest(BaseModel):
    events: list[AnalyticsEventDto] = Field(min_length=1, max_length=100)


def _seed_for(day: date) -> int:
    digest = hashlib.sha256(f"{DAILY_SECRET}|{day.isoformat()}".encode()).digest()
    return int.from_bytes(digest[:4], "big") & 0x7FFFFFFF


def _parse_challenge_id(challenge_id: str) -> date:
    try:
        return datetime.strptime(challenge_id, "daily_%Y_%m_%d").date()
    except ValueError as exc:
        raise HTTPException(400, "invalid challengeId") from exc


def _daily_payload(day: date) -> dict:
    return {
        "challengeId": f"daily_{day:%Y_%m_%d}",
        "seed": _seed_for(day),
        "generatorVersion": GENERATOR_VERSION,
        "routeCount": 3,
        "serverTimeUtc": datetime.now(timezone.utc).isoformat(),
    }


def _referral_payload(data: dict) -> dict:
    day = _parse_challenge_id(data["challengeId"])
    daily = _daily_payload(day)
    return {
        **data,
        "seed": daily["seed"],
        "generatorVersion": daily["generatorVersion"],
        "serverTimeUtc": daily["serverTimeUtc"],
    }


@app.get("/health")
def health() -> dict:
    return {"ok": True, "generatorVersion": GENERATOR_VERSION}


@app.get("/config/bootstrap")
def bootstrap() -> dict:
    return {
        "serverTimeUtc": datetime.now(timezone.utc).isoformat(),
        "minSupportedVersion": "0.1.0",
        "recommendedVersion": "0.1.0",
        "config": {
            "route_display_time_easy_ms": 3500,
            "route_display_time_medium_ms": 3000,
            "route_display_time_hard_ms": 2500,
            "daily_route_count": 3,
            "rewarded_enabled": True,
            "interstitial_enabled": True,
            "interstitial_min_rounds": 5,
            "interstitial_cooldown_sec": 180,
            "share_copy_variant": "A",
            "review_min_sessions": 5,
        },
    }


@app.get("/daily")
def daily() -> dict:
    return _daily_payload(datetime.now(timezone.utc).date())


@app.post("/attempt")
def submit_attempt(request: AttemptRequest) -> dict:
    day = _parse_challenge_id(request.challengeId)
    daily = _daily_payload(day)
    if request.generatorVersion != daily["generatorVersion"]:
        raise HTTPException(409, "generatorVersion mismatch")

    routes = create_daily_routes(daily["seed"], request.generatorVersion)
    seen: set[int] = set()
    verified_scores: list[float] = []
    replay_dump: dict = {"routes": []}

    for attempt in sorted(request.routes, key=lambda r: r.routeIndex):
        if attempt.routeIndex in seen:
            raise HTTPException(400, "duplicate routeIndex")
        seen.add(attempt.routeIndex)
        points = [RecordedPoint(p.x, p.y, p.timestampMs) for p in attempt.points]
        valid, reason = validate_replay(points, attempt.durationMs)
        if not valid:
            raise HTTPException(400, reason)
        server_score = score_route(routes[attempt.routeIndex], points)
        if abs(server_score - attempt.clientScore) > 1.0:
            raise HTTPException(422, f"score mismatch route={attempt.routeIndex}")
        verified_scores.append(server_score)
        replay_dump["routes"].append(attempt.model_dump())

    if seen != {0, 1, 2}:
        raise HTTPException(400, "all three routes are required")

    daily_score = round(sum(verified_scores) / 3.0, 1)
    store.save_attempt(request.challengeId, request.playerId, daily_score, request.assisted, replay_dump)
    return {
        "accepted": True,
        "score": daily_score,
        "routeScores": verified_scores,
        "leaderboardEligible": not request.assisted,
    }


@app.get("/leaderboard/daily")
def leaderboard(challengeId: str, limit: Annotated[int, Query(ge=1, le=100)] = 100) -> dict:
    _parse_challenge_id(challengeId)
    return {"challengeId": challengeId, "items": store.leaderboard(challengeId, limit)}


@app.post("/challenge")
def create_challenge(request: ChallengeRequest) -> dict:
    _parse_challenge_id(request.challengeId)
    referral_id = secrets.token_urlsafe(5).replace("-", "").replace("_", "")[:7].upper()
    store.create_referral(referral_id, request.challengeId, request.inviterId, request.score)
    install_url = f"https://www.rustore.ru/catalog/app/{RUSTORE_PACKAGE_NAME}?referrerId={referral_id}"
    return {
        "referralId": referral_id,
        "shareUrl": f"{PUBLIC_BASE_URL}/c/{referral_id}",
        "installUrl": install_url,
    }


@app.get("/challenge/{referral_id}")
def get_challenge(referral_id: str) -> dict:
    data = store.get_referral(referral_id)
    if not data:
        raise HTTPException(404, "challenge not found")
    return _referral_payload(data)


@app.get("/referral/{referral_id}")
def get_referral(referral_id: str) -> dict:
    data = store.get_referral(referral_id, increment_open=True)
    if not data:
        raise HTTPException(404, "referral not found")
    return _referral_payload(data)


@app.post("/referral")
def consume_referral(payload: dict) -> dict:
    referral_id = str(payload.get("referralId", ""))
    data = store.get_referral(referral_id, increment_open=True)
    if not data:
        raise HTTPException(404, "referral not found")
    return {"accepted": True, **_referral_payload(data)}


@app.post("/analytics/events")
def analytics_events(request: AnalyticsBatchRequest) -> dict:
    serialized: list[dict] = []
    for event in request.events:
        if event.eventName not in ANALYTICS_EVENT_NAMES:
            raise HTTPException(400, f"unsupported analytics event: {event.eventName}")
        if event.occurredAtUtc.tzinfo is None:
            raise HTTPException(400, "occurredAtUtc must include timezone")
        serialized.append(
            {
                "eventId": event.eventId,
                "playerId": event.playerId,
                "sessionNumber": event.sessionNumber,
                "eventName": event.eventName,
                "occurredAtUtc": event.occurredAtUtc.astimezone(timezone.utc).isoformat(),
                "parameters": [parameter.model_dump() for parameter in event.parameters],
            }
        )

    inserted = store.save_analytics_events(serialized)
    return {"accepted": len(serialized), "inserted": inserted}


@app.post("/purchase/verify")
def verify_purchase() -> dict:
    # Requires RuStore server-side credentials and the concrete verification API contract.
    # Fail closed until configured: never grant entitlement from a client-only claim.
    raise HTTPException(501, "RuStore server-side purchase verification is not configured")


@app.get("/c/{referral_id}", response_class=HTMLResponse)
def landing(referral_id: str) -> str:
    data = store.get_referral(referral_id, increment_open=True)
    if not data:
        raise HTTPException(404, "challenge not found")
    safe_ref = escape(referral_id)
    safe_score = escape(f"{data['inviterScore']:.1f}")
    install = f"https://www.rustore.ru/catalog/app/{RUSTORE_PACKAGE_NAME}?referrerId={safe_ref}"
    deeplink = f"nesbeisya://challenge/{safe_ref}"
    return f"""<!doctype html><html lang='ru'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'>
<title>НЕ СБЕЙСЯ!</title><style>body{{font-family:system-ui;background:#07111c;color:white;display:grid;place-items:center;min-height:100vh;margin:0}}main{{max-width:520px;text-align:center;padding:32px}}a{{display:block;background:#13c8e8;color:#00131a;padding:16px;border-radius:16px;text-decoration:none;font-weight:700;margin:12px}}</style></head>
<body><main><h1>НЕ СБЕЙСЯ!</h1><p>Друг набрал {safe_score}%. Сможешь точнее?</p><a href='{deeplink}'>Открыть испытание</a><a href='{install}'>Установить из RuStore</a></main></body></html>"""
