#!/usr/bin/env python3
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
BUILD = ROOT / "UnityProject/Assets/Game/Editor/SignedDeviceSmokeBuild.cs"
PRODUCTION_VALIDATOR = ROOT / "UnityProject/Assets/Game/Editor/ProductionReleaseValidator.cs"
RUNNER = ROOT / "scripts/run_release_candidate_checks.ps1"
AGENT = ROOT / "scripts/agent_check.ps1"
WORKFLOW = ROOT / ".github/workflows/unity-self-hosted.yml"
INSTALLER = ROOT / "scripts/install_signed_device_smoke_apk.ps1"
DOC = ROOT / "docs/ANDROID_RELEASE_BUILD.md"

errors: list[str] = []

for path in (BUILD, PRODUCTION_VALIDATOR, RUNNER, AGENT, WORKFLOW, INSTALLER, DOC):
    if not path.is_file():
        errors.append(f"Missing signed device-smoke file: {path.relative_to(ROOT)}")

if errors:
    raise SystemExit("Signed device-smoke build validation failed:\n- " + "\n- ".join(errors))

build = BUILD.read_text(encoding="utf-8")
production_validator = PRODUCTION_VALIDATOR.read_text(encoding="utf-8")
runner = RUNNER.read_text(encoding="utf-8")
agent = AGENT.read_text(encoding="utf-8")
workflow = WORKFLOW.read_text(encoding="utf-8")
installer = INSTALLER.read_text(encoding="utf-8")
doc = DOC.read_text(encoding="utf-8")

required_build = (
    '[MenuItem("Tools/НЕ СБЕЙСЯ!/Build/Signed Device Smoke APK")]',
    "ProductionSigningRuntimeValidator.EnsureReady();",
    "ProductionAndroidBuild.ApplySigningSecretsFromEnvironment();",
    "AndroidDependencyConfigurator.ForceResolveAndroidDependencies()",
    "EditorUserBuildSettings.buildAppBundle = false;",
    "EditorUserBuildSettings.development = false;",
    "options = BuildOptions.None",
    "NESBEISYA_DEVICE_SMOKE_OUTPUT",
    'Path.GetExtension(path), ".apk"',
    "BuildPipeline.BuildPlayer(options)",
    "ComputeSha256(outputPath)",
    "internal static bool IsBuildingSignedSmokeApk { get; private set; }",
    "IsBuildingSignedSmokeApk = true;",
    "IsBuildingSignedSmokeApk = false;",
)
for marker in required_build:
    if marker not in build:
        errors.append(f"Signed device-smoke build entrypoint is incomplete: missing {marker!r}.")

for marker in (
    "!EditorUserBuildSettings.buildAppBundle && !SignedDeviceSmokeBuild.IsBuildingSignedSmokeApk",
    "Only the scoped SignedDeviceSmokeBuild path may emit a signed release APK",
):
    if marker not in production_validator:
        errors.append(f"Production release validator is missing the scoped signed-smoke APK exception: {marker!r}.")

for forbidden in (
    "BuildOptions.Development",
    "useCustomKeystore = false",
):
    if forbidden in build:
        errors.append(f"Signed device-smoke build contains forbidden behavior: {forbidden!r}.")

for marker in (
    "[switch]$BuildSmokeApk",
    "SignedDeviceSmokeBuild.BuildFromCommandLine",
    "NESBEISYA_DEVICE_SMOKE_OUTPUT",
    "device-smoke-build.log",
    "$BuildAab -and $BuildSmokeApk",
):
    if marker not in runner:
        errors.append(f"Release runner is missing signed device-smoke support: {marker!r}.")

for marker in (
    "- DeviceSmokeApk",
    "- ReleaseCandidate",
    "agent/release-candidate/",
):
    if marker not in workflow:
        errors.append(f"Self-hosted workflow is missing signed device-smoke support: {marker!r}.")

for marker in (
    '[ValidateSet("Fast", "Unity", "Full", "Build", "ReleaseCandidate")]',
    'elseif ($Mode -eq "ReleaseCandidate")',
    'Invoke-UnityChecks -IncludeReadiness -BuildAab -BuildSmokeApk',
    'Invoke-SmokeArtifactVerification -SmokeOutput $smokeOutput',
    'Invoke-ReleaseArtifactVerification -ReleaseOutput $releaseOutput -ExpectedGitSha $gitSha',
):
    if marker not in agent:
        errors.append(f"Agent release-candidate mode is incomplete: missing {marker!r}.")

for marker in (
    "Get-FileHash -Path $apk -Algorithm SHA256",
    "adb devices",
    "install -r $apk",
    "multiple Android devices are connected",
    "ru.release.nesbeisya",
    "getprop ro.product.model",
    "getprop ro.build.version.sdk",
    "dumpsys package $packageName",
    "monkey -p $packageName -c android.intent.category.LAUNCHER 1",
    "pidof $packageName",
    "connected-device-preflight.txt",
    "ChallengeUri",
):
    if marker not in installer:
        errors.append(f"Signed device-smoke installer is incomplete: missing {marker!r}.")

for marker in (
    "Signed Device Smoke APK",
    "signed, non-Development APK",
    "production AAB",
    "install_signed_device_smoke_apk.ps1",
):
    if marker not in doc:
        errors.append(f"Android release documentation is missing device-smoke guidance: {marker!r}.")

if errors:
    raise SystemExit("Signed device-smoke build validation failed:\n- " + "\n- ".join(errors))

print("Signed device-smoke APK build contract passed.")
