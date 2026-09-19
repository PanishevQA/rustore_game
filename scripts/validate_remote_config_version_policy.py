#!/usr/bin/env python3
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
POLICY = ROOT / "UnityProject/Assets/Game/Services/AppVersionPolicy.cs"
RUSTORE = ROOT / "UnityProject/Assets/Game/Platform/RuStore/RuStoreRemoteConfigService.cs"
BOOTSTRAP = ROOT / "UnityProject/Assets/Game/Network/BootstrapRemoteConfigService.cs"
TESTS = ROOT / "UnityProject/Assets/Game/Tests/AppVersionPolicyTests.cs"
PURE = ROOT / "pure-tests/PureRules.Tests.csproj"

policy = POLICY.read_text(encoding="utf-8")
rustore = RUSTORE.read_text(encoding="utf-8")
bootstrap = BOOTSTRAP.read_text(encoding="utf-8")
tests = TESTS.read_text(encoding="utf-8")
pure = PURE.read_text(encoding="utf-8")

errors: list[str] = []

for needle, message in {
    "public static bool TryNormalize": "AppVersionPolicy must validate remote version syntax.",
    "public static AppVersionRange NormalizeRange": "AppVersionPolicy must normalize mandatory/recommended version pairs.",
    "if (Compare(recommended, min) < 0) recommended = min;": "Recommended version must never normalize below the mandatory floor.",
    "NumberStyles.None": "Version numeric components must be parsed strictly rather than accepting culture-specific formatting.",
}.items():
    if needle not in policy:
        errors.append(message)

for text, name in ((rustore, "RuStoreRemoteConfigService"), (bootstrap, "BootstrapRemoteConfigService")):
    if "AppVersionPolicy" not in text:
        errors.append(f"{name} must delegate update version handling to AppVersionPolicy.")

if "AppVersionPolicy.NormalizeRange" not in rustore:
    errors.append("RuStore Remote Config snapshot normalization must sanitize min/recommended versions before use or caching.")

for forbidden in ("private static int CompareVersions", "private static int[] ParseVersion"):
    if forbidden in rustore:
        errors.append("RuStoreRemoteConfigService must not keep an independent version parser/comparator.")

if "private static int[] Parse(" in bootstrap:
    errors.append("BootstrapRemoteConfigService must not keep an independent version parser.")

if "MalformedVersions_AreRejected" not in tests or "NormalizeRange_InvalidValuesCannotCreateAccidentalUpdateLockout" not in tests:
    errors.append("Pure tests must cover malformed remote versions and accidental lockout prevention.")

if "AppVersionPolicyTests.cs" not in pure:
    errors.append("AppVersionPolicyTests must run in the pure C# test project.")

if errors:
    raise SystemExit("Remote Config version policy validation failed:\n- " + "\n- ".join(errors))

print("Remote Config version policy validation passed.")
