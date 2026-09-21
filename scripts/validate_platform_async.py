from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
UPDATE = ROOT / "UnityProject/Assets/Game/Platform/RuStore/RuStoreUpdateService.cs"
REVIEW = ROOT / "UnityProject/Assets/Game/Platform/RuStore/RuStoreReviewService.cs"
GATE = ROOT / "UnityProject/Assets/Game/Services/PlatformUiLaunchGate.cs"
RUNTIME = ROOT / "UnityProject/Assets/Game/Presentation/PlatformUiLaunchGateRuntime.cs"
META = ROOT / "UnityProject/Assets/Game/Presentation/MetaMenuOverlay.cs"
PLATFORM_RUNTIME = ROOT / "UnityProject/Assets/Game/Presentation/RuntimePlatformCoordinator.cs"

update = UPDATE.read_text(encoding="utf-8")
review = REVIEW.read_text(encoding="utf-8")
gate = GATE.read_text(encoding="utf-8")
runtime = RUNTIME.read_text(encoding="utf-8")
meta = META.read_text(encoding="utf-8")
platform_runtime = PLATFORM_RUNTIME.read_text(encoding="utf-8")

required_gate = [
    "public static bool CanLaunchNow()",
    "if (gate == null) return false;",
    "catch",
    "return false;",
]
for value in required_gate:
    if value not in gate:
        raise SystemExit(f"Platform UI launch gate is not fail-closed: {value}")

required_runtime = [
    "PlatformUiLaunchGate.Configure(null)",
    "PlatformUiLaunchGate.Configure(CanLaunchPlatformUi)",
    "GameBootstrapRuntimeBridge.IsPlainHome(bootstrap)",
    "meta.IsPanelOpen",
    "training.IsOpen",
    "campaign.IsOpen",
    "referral.IsVisible",
    "platform.IsNotificationPromptOpen",
    'GameObject.Find("MandatoryUpdateCanvas")',
]
for value in required_runtime:
    if value not in runtime:
        raise SystemExit(f"Platform UI runtime gate is incomplete: {value}")

for value in [
    "EnforceMandatoryUpdateWhenSafeHome",
    "while (!IsSafeHome())",
    'ShowMandatoryUpdateBlocker("Для продолжения нужна новая версия игры.")',
]:
    if value not in platform_runtime:
        raise SystemExit(f"Runtime platform safe-update transition is incomplete: {value}")

update_gate = update.find("PlatformUiLaunchGate.CanLaunchNow()")
update_launch = update.find("await StartFlexibleAsync()")
if update_gate < 0 or update_launch < 0 or update_gate > update_launch:
    raise SystemExit("Optional RuStore update can launch without a final UI safety check.")
if "await StartImmediateAsync()" not in update:
    raise SystemExit("Mandatory RuStore update flow is no longer launched.")

review_gate = review.find("PlatformUiLaunchGate.CanLaunchNow()")
review_launch = review.find("manager.LaunchReviewFlow")
if review_gate < 0 or review_launch < 0 or review_gate > review_launch:
    raise SystemExit("RuStore review can launch without a final UI safety check.")

# Store operations may finish after the player leaves the panel. Entitlements may still be applied,
# but stale callbacks must not redraw a newer/closed panel.
for value in [
    "private int BeginPanelNavigation()",
    "private bool IsCurrentPanel(int revision)",
    "int revision = BeginPanelNavigation();",
    "int revision = _panelRevision;",
    "if (!IsCurrentPanel(revision)) return;",
    "RenderStore(_lastStoreProducts, feedback);",
]:
    if value not in meta:
        raise SystemExit(f"Store panel async navigation guard missing: {value}")

print("Platform/store async validation passed.")
