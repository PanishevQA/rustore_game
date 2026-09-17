#!/usr/bin/env python3
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
FEEDBACK = ROOT / "UnityProject/Assets/Game/Presentation/FeedbackRuntimeCoordinator.cs"
COSMETIC = ROOT / "UnityProject/Assets/Game/Presentation/CosmeticRuntimeCoordinator.cs"

feedback = FEEDBACK.read_text(encoding="utf-8")
cosmetic = COSMETIC.read_text(encoding="utf-8")

for path, text in ((FEEDBACK, feedback), (COSMETIC, cosmetic)):
    for forbidden in ("System.Reflection", "BindingFlags", "FieldInfo", "MethodInfo", "GetField(", "GetMethod("):
        if forbidden in text:
            raise SystemExit(f"{path.name} must not reflect into GameBootstrap directly: {forbidden}")

for needle in (
    "GameBootstrapRuntimeBridge.IsResult(_bootstrap)",
    "GameBootstrapRuntimeBridge.LastResultScore(_bootstrap)",
    "if (!_wasResult && isResult)",
):
    if needle not in feedback:
        raise SystemExit(f"Feedback runtime bridge contract is missing: {needle}")

for needle in (
    "GameBootstrapRuntimeBridge.IsActiveRound(_bootstrap)",
    'GameObject.Find("GameCanvas")',
    'string.Equals(graphics[i].name, "Player", StringComparison.Ordinal)',
    "_playerGraphic.color = desired",
):
    if needle not in cosmetic:
        raise SystemExit(f"Cosmetic visual/runtime contract is missing: {needle}")

# Result colors must remain owned by GameBootstrap; cosmetic code may act only while an active route exists.
active_index = cosmetic.find("if (!GameBootstrapRuntimeBridge.IsActiveRound(_bootstrap)) return;")
color_index = cosmetic.find("_playerGraphic.color = desired")
if active_index < 0 or color_index < 0 or active_index > color_index:
    raise SystemExit("Cosmetic trail color must be gated by active-round state before mutating the Player graphic.")

print("Feedback and cosmetic runtime contract validation passed.")
