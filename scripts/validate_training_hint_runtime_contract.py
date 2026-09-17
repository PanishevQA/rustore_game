#!/usr/bin/env python3
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
TRAINING = ROOT / "UnityProject/Assets/Game/Presentation/TrainingMenuCoordinator.cs"
HINT = ROOT / "UnityProject/Assets/Game/Presentation/HintRuntimeCoordinator.cs"
BRIDGE = ROOT / "UnityProject/Assets/Game/Presentation/GameBootstrapRuntimeBridge.cs"

training = TRAINING.read_text(encoding="utf-8")
hint = HINT.read_text(encoding="utf-8")
bridge = BRIDGE.read_text(encoding="utf-8")

for path, text in ((TRAINING, training), (HINT, hint)):
    for forbidden in ("System.Reflection", "BindingFlags", "FieldInfo", "MethodInfo", "GetField(", "GetMethod("):
        if forbidden in text:
            raise SystemExit(f"{path.name} must not reflect into runtime state directly: {forbidden}")

for needle in (
    "GameBootstrapRuntimeBridge.IsHome(_bootstrap)",
    "GameBootstrapRuntimeBridge.StartTrainingDifficulty(_bootstrap, difficulty)",
):
    if needle not in training:
        raise SystemExit(f"Training runtime bridge contract is missing: {needle}")

for needle in (
    "GameBootstrapRuntimeBridge.TryCaptureHintContext",
    "GameBootstrapRuntimeBridge.ShowHintReference(_bootstrap, route)",
    "GameBootstrapRuntimeBridge.ClearHintReferenceIfCurrentDrawing",
    "meta.IsPanelOpen",
    "CampaignRuntimeCoordinator.IsCampaignActive ? \"Campaign\" : mode",
):
    if needle not in hint:
        raise SystemExit(f"Hint runtime contract is missing: {needle}")

show_index = hint.find("GameBootstrapRuntimeBridge.ShowHintReference(_bootstrap, route)")
consume_index = hint.find("save.Hints--")
if show_index < 0 or consume_index < 0 or show_index > consume_index:
    raise SystemExit("Hint reference availability must be confirmed before local inventory is consumed.")

for needle in (
    'GetField("_trainingIndex", PrivateInstance)',
    'GetField("_referenceGraphic", PrivateInstance)',
    'GetMethod("StartTraining", PrivateInstance)',
    "public static bool StartTrainingDifficulty",
    "public static bool TryCaptureHintContext",
    "public static bool ShowHintReference",
    "public static void ClearHintReferenceIfCurrentDrawing",
    "internal sealed class GameBootstrapHintContext",
    "private static bool TrainingHintContractAvailable()",
):
    if needle not in bridge:
        raise SystemExit(f"GameBootstrap training/hint bridge contract is missing: {needle}")

print("Training and hint runtime contract validation passed.")
