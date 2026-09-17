#!/usr/bin/env python3
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SERVICE = ROOT / "UnityProject/Assets/Game/Platform/RuStore/RuStoreRemoteConfigService.cs"
ASMDEF = ROOT / "UnityProject/Assets/Game/Platform/RuStore/Game.Platform.RuStore.asmdef"
IDENTITY = ROOT / "UnityProject/Assets/Game/Platform/Android/AndroidBuildIdentity.cs"

service = SERVICE.read_text(encoding="utf-8")
asmdef = ASMDEF.read_text(encoding="utf-8")
identity = IDENTITY.read_text(encoding="utf-8")

errors: list[str] = []

required_service = {
    "using DontGetSidetracked.Platform.Android;": "Remote Config must consume the shared Android build identity adapter.",
    "RuntimeBuildIdentity buildIdentity = AndroidBuildIdentity.Current;": "Remote Config must snapshot build identity before SDK initialization.",
    'SetMember(settings, "appVersion", buildIdentity.Version);': "RuStore appVersion must use the public application version.",
    'SetMember(settings, "appBuild", buildIdentity.VersionCode.ToString(CultureInfo.InvariantCulture));': "RuStore appBuild must use numeric Android versionCode in invariant form.",
}
for needle, message in required_service.items():
    if needle not in service:
        errors.append(message)

if 'SetMember(settings, "appBuild", Application.version);' in service:
    errors.append("RuStore appBuild must never reuse Application.version.")

if '"Game.Platform.Android"' not in asmdef:
    errors.append("Game.Platform.RuStore asmdef must reference Game.Platform.Android for the shared build identity.")

required_identity = {
    'packageInfo.Call<long>("getLongVersionCode")': "AndroidBuildIdentity must read longVersionCode on API 28+.",
    'packageInfo.Get<int>("versionCode")': "AndroidBuildIdentity must retain the API 25-27 versionCode fallback.",
    "RuntimeInitializeLoadType.SubsystemRegistration": "AndroidBuildIdentity cache must reset between Unity runtime sessions.",
}
for needle, message in required_identity.items():
    if needle not in identity:
        errors.append(message)

if errors:
    raise SystemExit("Remote Config build identity validation failed:\n- " + "\n- ".join(errors))

print("Remote Config build identity validation passed.")
