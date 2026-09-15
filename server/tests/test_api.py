import os
from pathlib import Path
from uuid import uuid4

os.environ["GAME_DB_PATH"] = str(Path(__file__).parent / "test.db")

from fastapi.testclient import TestClient
import app.main as main_module
from app.game_rules import RecordedPoint, create_daily_routes, score_route
from app.main import app
from app.rustore_pay import VerifiedPurchase

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


def test_challenge_referral_roundtrip_restores_exact_daily():
    daily = client.get("/daily").json()
    created = client.post("/challenge", json={"inviterId": "anon_a", "challengeId": daily["challengeId"], "score": 94.7})
    assert created.status_code == 200
    ref = created.json()["referralId"]
    restored = client.get(f"/referral/{ref}")
    assert restored.status_code == 200
    payload = restored.json()
    assert payload["inviterScore"] == 94.7
    assert payload["challengeId"] == daily["challengeId"]
    assert payload["seed"] == daily["seed"]
    assert payload["generatorVersion"] == daily["generatorVersion"]
    assert payload["serverTimeUtc"]


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


def test_purchase_verification_claim_is_idempotent_and_bound_to_player(monkeypatch):
    invoice_id = str(int(uuid4().hex[:12], 16))
    purchase_id = str(uuid4())

    def fake_verify(invoice, product, expected_purchase):
        assert invoice == invoice_id
        assert product == "remove_ads"
        assert expected_purchase == purchase_id
        return VerifiedPurchase(
            product_id=product,
            purchase_id=purchase_id,
            invoice_id=invoice_id,
            status="CONFIRMED",
            app_id=123,
        )

    monkeypatch.setattr(main_module, "verify_rustore_purchase", fake_verify)
    payload = {
        "playerId": "anon_purchase_a",
        "productId": "remove_ads",
        "invoiceId": invoice_id,
        "purchaseId": purchase_id,
    }

    first = client.post("/purchase/verify", json=payload)
    second = client.post("/purchase/verify", json=payload)
    stolen = client.post("/purchase/verify", json={**payload, "playerId": "anon_purchase_b"})

    assert first.status_code == 200, first.text
    assert first.json()["verified"] is True
    assert first.json()["firstClaim"] is True
    assert second.status_code == 200, second.text
    assert second.json()["firstClaim"] is False
    assert stolen.status_code == 409


def test_purchase_verification_rejects_unknown_product():
    response = client.post("/purchase/verify", json={
        "playerId": "anon_purchase",
        "productId": "unknown_product",
        "invoiceId": "123456",
        "purchaseId": str(uuid4()),
    })
    assert response.status_code == 400
