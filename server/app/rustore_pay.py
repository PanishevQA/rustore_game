from __future__ import annotations

import os
from dataclasses import dataclass

import httpx


class RuStorePayConfigurationError(RuntimeError):
    pass


class RuStorePayVerificationError(RuntimeError):
    pass


class RuStorePayUpstreamError(RuntimeError):
    pass


@dataclass(frozen=True)
class VerifiedPurchase:
    product_id: str
    purchase_id: str
    invoice_id: str
    status: str
    app_id: int


def _is_truthy(value: str | None) -> bool:
    return (value or "").strip().lower() in {"1", "true", "yes", "on"}


def verify_rustore_purchase(
    invoice_id: str,
    expected_product_id: str,
    expected_purchase_id: str | None = None,
) -> VerifiedPurchase:
    invoice_id = (invoice_id or "").strip()
    expected_product_id = (expected_product_id or "").strip()
    expected_purchase_id = (expected_purchase_id or "").strip() or None

    if not invoice_id or not invoice_id.isdigit():
        raise RuStorePayVerificationError("invoiceId must be numeric")
    if not expected_product_id:
        raise RuStorePayVerificationError("productId is required")

    token = os.getenv("RUSTORE_PUBLIC_TOKEN", "").strip()
    app_id_raw = os.getenv("RUSTORE_APP_ID", "").strip()
    if not token or not app_id_raw:
        raise RuStorePayConfigurationError(
            "RUSTORE_PUBLIC_TOKEN and RUSTORE_APP_ID must be configured"
        )

    try:
        expected_app_id = int(app_id_raw)
    except ValueError as exc:
        raise RuStorePayConfigurationError("RUSTORE_APP_ID must be numeric") from exc

    sandbox = _is_truthy(os.getenv("RUSTORE_PAY_SANDBOX"))
    prefix = "public/sandbox/v2" if sandbox else "public/v2"
    url = f"https://public-api.rustore.ru/{prefix}/invoices/{invoice_id}"

    try:
        response = httpx.get(
            url,
            headers={"Public-Token": token, "Accept": "application/json"},
            timeout=httpx.Timeout(15.0, connect=5.0),
        )
    except httpx.HTTPError as exc:
        raise RuStorePayUpstreamError(f"RuStore API unavailable: {exc}") from exc

    if response.status_code != 200:
        raise RuStorePayUpstreamError(
            f"RuStore API returned HTTP {response.status_code}"
        )

    try:
        payload = response.json()
    except ValueError as exc:
        raise RuStorePayUpstreamError("RuStore API returned invalid JSON") from exc

    if str(payload.get("code", "")).upper() != "OK":
        message = payload.get("message") or "RuStore verification failed"
        raise RuStorePayVerificationError(str(message))

    body = payload.get("body")
    if not isinstance(body, dict):
        raise RuStorePayVerificationError("RuStore invoice body is empty")

    try:
        actual_app_id = int(body.get("appId"))
    except (TypeError, ValueError) as exc:
        raise RuStorePayVerificationError("RuStore invoice has invalid appId") from exc
    if actual_app_id != expected_app_id:
        raise RuStorePayVerificationError("RuStore invoice belongs to another app")

    status = str(body.get("invoiceStatus", "")).upper()
    # ONE_STEP Pay SDK purchases are granted only in the final successful state.
    # PAID is intentionally not accepted: for TWO_STEP purchases it still requires confirmation.
    if status != "CONFIRMED":
        raise RuStorePayVerificationError(f"invoice is not confirmed: {status or 'UNKNOWN'}")

    order = body.get("order")
    if not isinstance(order, dict):
        raise RuStorePayVerificationError("RuStore invoice has no order")
    actual_product_id = str(order.get("itemCode", "")).strip()
    if actual_product_id != expected_product_id:
        raise RuStorePayVerificationError("RuStore invoice productId mismatch")

    purchase_id = str(body.get("purchaseId", "")).strip()
    if not purchase_id:
        raise RuStorePayVerificationError("RuStore invoice has no purchaseId")
    if expected_purchase_id and purchase_id != expected_purchase_id:
        raise RuStorePayVerificationError("RuStore purchaseId mismatch")

    return VerifiedPurchase(
        product_id=actual_product_id,
        purchase_id=purchase_id,
        invoice_id=invoice_id,
        status=status,
        app_id=actual_app_id,
    )
