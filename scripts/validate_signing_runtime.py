#!/usr/bin/env python3
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SIGNING = ROOT / "UnityProject/Assets/Game/Editor/ProductionSigningRuntimeValidator.cs"
BUILD = ROOT / "UnityProject/Assets/Game/Editor/ProductionAndroidBuild.cs"
REPORT = ROOT / "UnityProject/Assets/Game/Editor/ReleaseReadinessReporter.cs"
DOC = ROOT / "docs/ANDROID_RELEASE_BUILD.md"

for path in (SIGNING, BUILD, REPORT, DOC):
    if not path.is_file():
        raise SystemExit(f"Production signing runtime guard is missing required file: {path.relative_to(ROOT)}")

signing = SIGNING.read_text(encoding="utf-8")
build = BUILD.read_text(encoding="utf-8")
report = REPORT.read_text(encoding="utf-8")
doc = DOC.read_text(encoding="utf-8")

errors: list[str] = []

for marker in (
    'KeystorePasswordEnvironmentVariable = "NESBEISYA_KEYSTORE_PASS"',
    'KeyAliasPasswordEnvironmentVariable = "NESBEISYA_KEYALIAS_PASS"',
    'PlayerSettings.Android.useCustomKeystore',
    'PlayerSettings.Android.keystoreName',
    'PlayerSettings.Android.keyaliasName',
    'InProjectPrefix = "{inproject}:"',
    'Path.IsPathRooted(candidate)',
    'File.Exists(resolved)',
    'Environment.GetEnvironmentVariable(KeystorePasswordEnvironmentVariable)',
    'Environment.GetEnvironmentVariable(KeyAliasPasswordEnvironmentVariable)',
    'internal static void EnsureReady()',
    'BuildFailedException',
):
    if marker not in signing:
        errors.append(f"Production signing runtime validator is incomplete: missing {marker!r}.")

if 'ProductionSigningRuntimeValidator.EnsureReady();' not in build:
    errors.append("Production Android build must fail fast through ProductionSigningRuntimeValidator.EnsureReady().")

if 'new Section("Production signing runtime", ProductionSigningRuntimeValidator.CollectErrors())' not in report:
    errors.append("Release readiness report must include machine-local production signing readiness.")

for marker in (
    "NESBEISYA_KEYSTORE_PASS",
    "NESBEISYA_KEYALIAS_PASS",
    "Секреты, keystore и пароли не коммитятся.",
    "Значения нельзя добавлять в репозиторий, документацию или логи.",
):
    if marker not in doc:
        errors.append(f"Android release build documentation is missing signing-runtime guidance: {marker!r}.")

if errors:
    raise SystemExit("Production signing runtime validation failed:\n- " + "\n- ".join(errors))

print("Production signing runtime guard passed.")
