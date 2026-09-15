import os
from pathlib import Path
from uuid import uuid4

os.environ["GAME_DB_PATH"] = str(Path(__file__).parent / "test.db")

from fastapi.testclient import TestClient
from app.game_rules import RecordedPoint, create_daily_routes, score_route
from app.main import app

client = TestClient(app)


def test_daily_and_verified_attempt():
    daily = client.get("/daily").json()
    routes = create_daily_routes(daily["seed"], daily["generatorVersion"])
    attempts = []
    for i, route in enumerate(routes):
        replay = [RecordedPoint(p.x, p.y, n * 16) for n, p in enumerate(route.points)]
        attempts.append({
            "routeIndex": i,
            "durationMs": replay[-1].timestamp_ms + 1,
            "clientScore": score_route(route, replay),
            "points": [{"x": p.x, "y": p.y, "timestampMs": p.timestamp_ms} for p in replay],
        })
    response = client.post("/attempt", json={
        "playerId": "anon_test",
        "challengeId": daily["challengeId"],
        "generatorVersion": daily["generatorVersion"],
        "assisted": False,
        "routes": attempts,
    })
    assert response.status_code == 200, response.text
    assert response.json()["score"] == 100.0


def test_challenge_referral_roundtrip():
    daily = client.get("/daily").json()
    created = client.post("/challenge", json={"inviterId": "anon_a", "challengeId": daily["challengeId"], "score": 94.7})
    assert created.status_code == 200
    ref = created.json()["referralId"]
    restored = client.get(f"/referral/{ref}")
    assert restored.status_code == 200
    assert restored.json()["inviterScore"] == 94.7


def test_analytics_batch_is_idempotent():
    event_id = "evt_" + uuid4().hex
    payload = {
        "events": [{
            "eventId": event_id,
            "playerId": "anon_analytics",
            "sessionNumber": 3,
            "eventName": "daily_complete",
            "occurredAtUtc": "2026-09-15T06:30:00Z",
            "parameters": [
                {"key": "challenge_id", "value": "daily_2026_09_15"},
                {"key": "score", "value": "94.7"},
            ],
        }]
    }

    first = client.post("/analytics/events", json=payload)
    second = client.post("/analytics/events", json=payload)

    assert first.status_code == 200, first.text
    assert first.json() == {"accepted": 1, "inserted": 1}
    assert second.status_code == 200, second.text
    assert second.json() == {"accepted": 1, "inserted": 0}


def test_analytics_rejects_unknown_event():
    response = client.post("/analytics/events", json={
        "events": [{
            "eventId": "evt_" + uuid4().hex,
            "playerId": "anon_analytics",
            "sessionNumber": 1,
            "eventName": "made_up_event",
            "occurredAtUtc": "2026-09-15T06:30:00Z",
            "parameters": [],
        }]
    })

    assert response.status_code == 400
