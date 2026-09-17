#!/usr/bin/env python3
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
CONFIG = ROOT / "UnityProject/Assets/Game/Editor/ProjectConfigurator.cs"
RELEASE = ROOT / "UnityProject/Assets/Game/Editor/ProductionReleaseValidator.cs"
errors: list[str] = []


def require(text: str, needle: str, message: str) -> None:
    if needle not in text:
        errors.append(message)


def main() -> int:
    config = CONFIG.read_text(encoding="utf-8")
    release = RELEASE.read_text(encoding="utf-8")

    require(config, "ShouldAssignDevelopmentPackageName(currentPackage)",
            "Editor bootstrap must preserve explicitly configured production package names.")
    require(config, "ShouldRaiseMinimumSdk(PlayerSettings.Android.minSdkVersion)",
            "Editor bootstrap must not lower an explicitly configured minimum Android SDK.")
    require(config, "ShouldAssignBaselineTargetSdk(PlayerSettings.Android.targetSdkVersion)",
            "Editor bootstrap must not blindly overwrite the Android target SDK.")
    require(config, "currentTarget == AndroidSdkVersions.AndroidApiLevelAuto",
            "Highest-installed Android target (Auto) must survive Editor reloads.")
    require(config, "(int)currentTarget < (int)AndroidSdkVersions.AndroidApiLevel34",
            "Editor bootstrap may only raise an older explicit target to the verified API 34 baseline.")
    require(config, "(int)currentMinimum < (int)AndroidSdkVersions.AndroidApiLevel25",
            "Editor bootstrap may only raise an older min SDK to the Unity 6.3 baseline.")

    # API 34 is a verified floor, not a ceiling. Auto and explicit newer targets must remain valid.
    require(release, "AndroidSdkVersions targetSdk = PlayerSettings.Android.targetSdkVersion",
            "Production preflight must evaluate the configured target SDK as a floor check.")
    require(release, "targetSdk != AndroidSdkVersions.AndroidApiLevelAuto",
            "Production preflight must allow highest-installed target SDK.")
    require(release, "(int)targetSdk < (int)AndroidSdkVersions.AndroidApiLevel34",
            "Production preflight must reject only targets below the verified API 34 floor.")
    if "targetSdkVersion != AndroidSdkVersions.AndroidApiLevel34" in release:
        errors.append("Production preflight must not reject explicit Android targets newer than API 34.")

    require(release, "AndroidApplicationEntry.Activity", "Production preflight must require UnityPlayerActivity entry mode.")
    require(release, "ScriptingImplementation.IL2CPP", "Production preflight must require IL2CPP.")
    require(release, "AndroidArchitecture.ARM64", "Production preflight must require ARM64.")
    require(release, "EditorUserBuildSettings.buildAppBundle", "Production preflight must require AAB output.")
    require(release, "useCustomKeystore", "Production preflight must require custom Android signing.")

    if errors:
        print("Android editor/release configuration guard FAILED:")
        for error in errors:
            print(f"- {error}")
        return 1

    print("Android editor/release configuration guard passed.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
