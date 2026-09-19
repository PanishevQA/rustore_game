#!/usr/bin/env python3
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
VALIDATOR = ROOT / "UnityProject/Assets/Game/Editor/ProductionPlaceholderValidator.cs"
errors: list[str] = []


def require(text: str, needle: str, message: str) -> None:
    if needle not in text:
        errors.append(message)


def main() -> int:
    if not VALIDATOR.exists():
        print("Release placeholder guard FAILED:\n- ProductionPlaceholderValidator.cs is missing.")
        return 1

    text = VALIDATOR.read_text(encoding="utf-8")

    require(text, "IPreprocessBuildWithReport", "Placeholder validation must run as a Unity prebuild hook.")
    require(text, "BuildOptions.Development", "Development builds must remain exempt from production placeholder validation.")
    require(text, "GetApplicationIdentifier(NamedBuildTarget.Android)", "Production package name must be checked for placeholder values.")
    require(text, 'ValidateConstString(RemoteConfigSettingsPath, "AppId"', "RuStore Remote Config AppId must be validated.")
    require(text, 'ValidateConstString(AdsSettingsPath, "RewardedUnitId"', "Rewarded ad unit ID must be validated.")
    require(text, 'ValidateConstString(AdsSettingsPath, "InterstitialUnitId"', "Interstitial ad unit ID must be validated.")
    require(text, "PlayerSettings.Android.keystoreName", "Production keystore path/name must be placeholder-checked.")
    require(text, "PlayerSettings.Android.keyaliasName", "Production key alias must be placeholder-checked.")
    require(text, "Regex.Escape(constantName)", "Settings constants must be resolved by exact constant name rather than broad substring matching.")
    require(text, 'value.StartsWith("demo-"', "Demo ad unit IDs must be rejected explicitly.")
    require(text, 'value.StartsWith("R-M-"', "Production Yandex ad unit IDs must require the R-M- prefix.")

    for marker in (
        "placeholder",
        "change_me",
        "replace_me",
        "your_app",
        "your_id",
        "example",
        "dummy",
        "defaultcompany",
        "todo",
    ):
        require(text, f'"{marker}"', f"Missing release placeholder marker: {marker}")

    if errors:
        print("Release placeholder guard FAILED:")
        for error in errors:
            print(f"- {error}")
        return 1

    print("Release placeholder guard passed.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
