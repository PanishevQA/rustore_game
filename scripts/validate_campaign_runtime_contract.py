#!/usr/bin/env python3
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CAMPAIGN = ROOT / "UnityProject/Assets/Game/Presentation/CampaignRuntimeCoordinator.cs"
BRIDGE = ROOT / "UnityProject/Assets/Game/Presentation/GameBootstrapRuntimeBridge.cs"

campaign = CAMPAIGN.read_text(encoding="utf-8")
bridge = BRIDGE.read_text(encoding="utf-8")

for forbidden in (
    "System.Reflection",
    "BindingFlags",
    "FieldInfo",
    "MethodInfo",
    "GetField(",
    "GetMethod(",
):
    if forbidden in campaign:
        raise SystemExit(f"CampaignRuntimeCoordinator must not reflect into GameBootstrap directly: {forbidden}")

campaign_required = (
    "GameBootstrapRuntimeBridge.PrepareCampaign(_bootstrap)",
    "GameBootstrapRuntimeBridge.BeginRoute(_bootstrap, route)",
    "GameBootstrapRuntimeBridge.IsResult(_bootstrap)",
    "GameBootstrapRuntimeBridge.IsActiveRound(_bootstrap)",
    "GameBootstrapRuntimeBridge.LastResultScore(_bootstrap)",
    "GameBootstrapRuntimeBridge.Save(_bootstrap)",
    "GameBootstrapRuntimeBridge.ReplaceSave(_bootstrap, _progress.Save)",
    "GameBootstrapRuntimeBridge.PrimaryButton(_bootstrap)",
    "GameBootstrapRuntimeBridge.SecondaryButton(_bootstrap)",
    "GameBootstrapRuntimeBridge.ShareButton(_bootstrap)",
    "GameBootstrapRuntimeBridge.ShowHome(_bootstrap)",
    "GameBootstrapRuntimeBridge.StartDaily(_bootstrap)",
    "GameBootstrapRuntimeBridge.ShareCurrentResult(_bootstrap)",
)
for needle in campaign_required:
    if needle not in campaign:
        raise SystemExit(f"Campaign runtime bridge usage is missing: {needle}")

bridge_required = (
    'GetField("_dailyCompleted", PrivateInstance)',
    'GetField("_duelSession", PrivateInstance)',
    'GetField("_dailySession", PrivateInstance)',
    'GetField("_daily", PrivateInstance)',
    'GetField("_save", PrivateInstance)',
    'GetField("_lastResultScore", PrivateInstance)',
    'GetMethod("BeginNavigation", PrivateInstance)',
    'GetMethod("BeginRoute", PrivateInstance)',
    'GetMethod("ShowHome", PrivateInstance)',
    'GetMethod("StartDaily", PrivateInstance)',
    'GetMethod("ShareCurrentResult", PrivateInstance)',
    "BeginNavigationMethod.Invoke(bootstrap, null);",
    "public static bool PrepareCampaign",
    "public static bool BeginRoute",
    "public static void ReplaceSave",
)
for needle in bridge_required:
    if needle not in bridge:
        raise SystemExit(f"GameBootstrapRuntimeBridge campaign contract is missing: {needle}")

# If route start fails, Campaign must not remain marked active or emit a successful start event.
failure_index = campaign.find("if (!GameBootstrapRuntimeBridge.BeginRoute(_bootstrap, route))")
analytics_index = campaign.find('AnalyticsLifecycle.Service?.Track("level_start"')
if failure_index < 0 or analytics_index < 0 or failure_index > analytics_index:
    raise SystemExit("Campaign route failure must be handled before level_start analytics.")

print("Campaign runtime contract validation passed.")
