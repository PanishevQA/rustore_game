import os
from pathlib import Path

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
