#!/usr/bin/env python3
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
BUILD = ROOT / "UnityProject/Assets/Game/Platform/Android/AndroidBuildIdentity.cs"
ANALYTICS = ROOT / "UnityProject/Assets/Game/Presentation/AnalyticsLifecycle.cs"
CRASH = ROOT / "UnityProject/Assets/Game/Presentation/LocalCrashLog.cs"
errors: list[str] = []


def require(text: str, needle: str, message: str) -> None:
    if needle not in text:
        errors.append(message)


def main() -> int:
    build = BUILD.read_text(encoding="utf-8")
    analytics = ANALYTICS.read_text(encoding="utf-8")
    crash = CRASH.read_text(encoding="utf-8")

    require(build, 'Application.identifier', "Build identity must use the package that is actually running.")
    require(build, 'Application.version', "Build identity must include the public application version.")
    require(build, 'Application.buildGUID', "Build identity must include Unity's build GUID.")
    require(build, 'getLongVersionCode', "Android API 28+ versionCode must come from PackageInfo.")
    require(build, 'packageInfo.Get<int>("versionCode")', "Pre-API-28 devices must keep a versionCode fallback.")
    require(build, 'RuntimeInitializeLoadType.SubsystemRegistration', "Cached build identity must reset between Unity runtime sessions.")

    require(analytics, 'RuntimeBuildIdentity build = AndroidBuildIdentity.Current',
            "Analytics lifecycle must resolve the shared build identity.")
    for key in ('client_version', 'build_code', 'package_name', 'build_guid'):
        require(analytics, f'["{key}"]', f"app_open analytics must contain {key}.")

    require(crash, '_buildIdentity = AndroidBuildIdentity.Current',
            "Crash diagnostics must cache build identity before threaded logging begins.")
    require(crash, 'Application.logMessageReceivedThreaded += OnLogMessage',
            "Local crash diagnostics must remain subscribed to threaded Unity errors.")
    require(crash, '.Append(_buildIdentity.VersionCode)', "Crash entries must contain Android build code.")
    require(crash, '.AppendLine(_buildIdentity.BuildGuid)', "Crash entries must contain build GUID.")

    identity_pos = crash.find('_buildIdentity = AndroidBuildIdentity.Current')
    subscribe_pos = crash.find('Application.logMessageReceivedThreaded += OnLogMessage')
    if identity_pos < 0 or subscribe_pos < 0 or identity_pos > subscribe_pos:
        errors.append("Build identity must be resolved on the main thread before the threaded crash handler is registered.")

    if errors:
        print("Build observability guard FAILED:")
        for error in errors:
            print(f"- {error}")
        return 1

    print("Build observability guard passed.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
