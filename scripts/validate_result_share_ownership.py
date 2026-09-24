#!/usr/bin/env python3
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
RESULT = ROOT / "UnityProject/Assets/Game/Presentation/ResultEnhancementCoordinator.cs"
BRIDGE = ROOT / "UnityProject/Assets/Game/Presentation/GameBootstrapRuntimeBridge.cs"
LEGACY_CAMPAIGN_SHARE = ROOT / "UnityProject/Assets/Game/Presentation/CampaignShareCardCoordinator.cs"

result = RESULT.read_text(encoding="utf-8")
bridge = BRIDGE.read_text(encoding="utf-8")

if LEGACY_CAMPAIGN_SHARE.exists():
    raise SystemExit("CampaignShareCardCoordinator must stay removed; ResultEnhancementCoordinator owns Campaign result cards.")

for forbidden in (
    "System.Reflection",
    "BindingFlags",
    "FieldInfo",
    "MethodInfo",
    "GetField(",
    "GetMethod(",
):
    if forbidden in result:
        raise SystemExit(f"ResultEnhancementCoordinator must not reflect into GameBootstrap directly: {forbidden}")

result_required = (
    "GameBootstrapRuntimeBridge.IsResult(_bootstrap)",
    "GameBootstrapRuntimeBridge.TryCaptureResult(_bootstrap, out GameBootstrapResultSnapshot snapshot)",
    "GameBootstrapRuntimeBridge.IsCurrentNavigation(_bootstrap, revision)",
    "CampaignRuntimeCoordinator.IsCampaignActive",
    'ModeLabel = campaign ? "КАМПАНИЯ" : ModeLabel(mode)',
    'ChallengeLabel = campaign',
    '"ПОДЕЛИТЬСЯ УРОВНЕМ"',
    '"БРОСИТЬ ВЫЗОВ"',
    '"ResultActions"',
    "SuppressLegacyResultControls()",
    'analytics["level"] = campaignLevel',
    'analytics["chapter"] = campaignChapter',
)
for needle in result_required:
    if needle not in result:
        raise SystemExit(f"Result/Campaign share ownership contract is missing: {needle}")

await_index = result.find("await snapshot.Api.CreateChallengeAsync")
revision_index = result.find("GameBootstrapRuntimeBridge.IsCurrentNavigation(_bootstrap, revision)")
share_index = result.find("NativeImageShare.Share(png, text)")
if await_index < 0 or revision_index < 0 or share_index < 0 or not (await_index < revision_index < share_index):
    raise SystemExit("Async challenge-link creation must validate navigation revision before opening the native share sheet.")

bridge_required = (
    'GetField("_lastDailyScore", PrivateInstance)',
    'GetField("_route", PrivateInstance)',
    'GetField("_recording", PrivateInstance)',
    'GetField("_api", PrivateInstance)',
    'GetField("_navigationRevision", PrivateInstance)',
    "public static bool TryCaptureResult",
    "public static bool IsCurrentNavigation",
    "internal sealed class GameBootstrapResultSnapshot",
    "Recording = recording == null ? new List<RecordedPoint>() : new List<RecordedPoint>(recording)",
    "private static bool ResultContractAvailable()",
)
for needle in bridge_required:
    if needle not in bridge:
        raise SystemExit(f"GameBootstrap result snapshot contract is missing: {needle}")

print("Result share ownership validation passed.")
