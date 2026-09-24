import pytest

import app.rustore_pay as rustore_pay
from app.rustore_pay import RuStorePayVerificationError, verify_rustore_purchase


class FakeResponse:
    def __init__(self, payload: dict, status_code: int = 200):
        self._payload = payload
        self.status_code = status_code

    def json(self):
        return self._payload


def invoice_payload(*, status="CONFIRMED", app_id=12345, product_id="remove_ads", purchase_id="purchase-1"):
    return {
        "code": "OK",
        "message": None,
        "body": {
            "appId": app_id,
            "invoiceStatus": status,
            "purchaseId": purchase_id,
            "order": {
                "itemCode": product_id,
            },
        },
    }


def configure(monkeypatch):
    monkeypatch.setenv("RUSTORE_PUBLIC_TOKEN", "test-public-token")
    monkeypatch.setenv("RUSTORE_APP_ID", "12345")
    monkeypatch.delenv("RUSTORE_PAY_SANDBOX", raising=False)


def test_confirmed_invoice_is_verified(monkeypatch):
    configure(monkeypatch)
    captured = {}

    def fake_get(url, headers, timeout):
        captured["url"] = url
        captured["token"] = headers.get("Public-Token")
        return FakeResponse(invoice_payload())

    monkeypatch.setattr(rustore_pay.httpx, "get", fake_get)

    result = verify_rustore_purchase("10001", "remove_ads", "purchase-1")

    assert result.product_id == "remove_ads"
    assert result.purchase_id == "purchase-1"
    assert result.status == "CONFIRMED"
    assert captured["url"].endswith("/public/v2/invoices/10001")
    assert captured["token"] == "test-public-token"


def test_sandbox_uses_sandbox_endpoint(monkeypatch):
    configure(monkeypatch)
    monkeypatch.setenv("RUSTORE_PAY_SANDBOX", "true")
    captured = {}

    def fake_get(url, headers, timeout):
        captured["url"] = url
        return FakeResponse(invoice_payload())

    monkeypatch.setattr(rustore_pay.httpx, "get", fake_get)

    verify_rustore_purchase("10002", "remove_ads", "purchase-1")

    assert "/public/sandbox/v2/invoices/10002" in captured["url"]


@pytest.mark.parametrize(
    "payload,error_part",
    [
        (invoice_payload(status="PAID"), "not confirmed"),
        (invoice_payload(app_id=99999), "another app"),
        (invoice_payload(product_id="skin_neon"), "productId mismatch"),
        (invoice_payload(purchase_id="purchase-other"), "purchaseId mismatch"),
    ],
)
def test_invoice_mismatch_is_rejected(monkeypatch, payload, error_part):
    configure(monkeypatch)
    monkeypatch.setattr(
        rustore_pay.httpx,
        "get",
        lambda url, headers, timeout: FakeResponse(payload),
    )

    with pytest.raises(RuStorePayVerificationError, match=error_part):
        verify_rustore_purchase("10003", "remove_ads", "purchase-1")
