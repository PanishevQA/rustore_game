#!/usr/bin/env python3
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
WORKFLOW = ROOT / ".github/workflows/unity-self-hosted.yml"
OLD_WORKFLOW = ROOT / ".github/workflows/unity-agent-gate.yml"
PREFLIGHT = ROOT / "scripts/verify_unity_runner_environment.ps1"
RELEASE_CHECKS = ROOT / "scripts/run_release_candidate_checks.ps1"
LOGON_TASK = ROOT / "scripts/install_unity_runner_logon_task.ps1"
SIGNING_SETUP = ROOT / "scripts/configure_release_signing_runner.ps1"
PORTABLE_PYTHON = ROOT / "scripts/bootstrap_portable_python.ps1"
errors: list[str] = []


def read(path: Path) -> str:
    if not path.is_file():
        errors.append(f"Missing required file: {path.relative_to(ROOT)}")
        return ""
    return path.read_text(encoding="utf-8")


workflow = read(WORKFLOW)
preflight = read(PREFLIGHT)
release_checks = read(RELEASE_CHECKS)
logon_task = read(LOGON_TASK)
signing_setup = read(SIGNING_SETUP)
portable_python = read(PORTABLE_PYTHON)

if OLD_WORKFLOW.exists():
    errors.append("Unsafe legacy self-hosted pull-request workflow returned: .github/workflows/unity-agent-gate.yml")

required_workflow = (
    "branches:",
    "- 'agent/**'",
    "workflow_dispatch:",
    "Agent validation mode",
    "- Build",
    "- DeviceSmokeApk",
    "- AndroidIntegrationProbe",
    "permissions:",
    "contents: read",
    "vars.UNITY_SELF_HOSTED_ENABLED == 'true'",
    "github.repository == 'PanishevQA/rustore_game'",
    "runs-on: [self-hosted, windows, x64, unity]",
    "timeout-minutes: 60",
    "persist-credentials: false",
    "clean: false",
    "bootstrap_portable_python.ps1",
    "verify_unity_runner_environment.ps1",
    "AGENT_CHECK_MODE",
    "github.event_name == 'workflow_dispatch' && inputs.mode || 'Full'",
    "agent_check.ps1 -Mode $env:AGENT_CHECK_MODE -SkipFast",
    "run_release_candidate_checks.ps1 -BuildSmokeApk",
    "run_android_integration_probe.ps1",
    "cancel-in-progress: true",
    "actions/upload-artifact@v4",
    "artifacts/agent-check/**",
    "artifacts/release-candidate/**",
    "artifacts/android-integration-probe/**",
)

for marker in required_workflow:
    if marker not in workflow:
        errors.append(f"Self-hosted Unity workflow is missing: {marker!r}")

for forbidden in ("pull_request:", "pull_request_target:", "secrets.", "actions/setup-python@", "actions/setup-dotnet@"):
    if forbidden in workflow:
        errors.append(f"Self-hosted Unity workflow must not contain unsafe trigger/secret marker: {forbidden!r}")

required_preflight = (
    'if ($env:OS -ne "Windows_NT")',
    "6000.3.24f1",
    'Resolve-RequiredCommand "git"',
    "Python 3",
    "UNITY_EXE",
    "10GB",
    "agent_check.ps1",
)

required_release_checks = (
    "UnityStepTimeoutSeconds",
    "BuildTimeoutSeconds",
    "BuildSmokeApk",
    "SignedDeviceSmokeBuild.BuildFromCommandLine",
    "taskkill.exe /PID",
    "WaitForExit",
    "timed out after",
)

required_logon_task = (
    "Get-Content $serviceFile -Raw",
    "Set-Service -Name $serviceName -StartupType Disabled",
    "New-ScheduledTaskTrigger -AtLogOn -User $userName",
    "New-ScheduledTaskPrincipal -UserId $userName -LogonType Interactive",
    "Start-ScheduledTask -TaskName $TaskName",
    "run-unity-runner-forever.ps1",
    "unity-runner-watchdog.log",
    "while (`$true)",
    "restarting in 10 seconds",
    "run.cmd",
)
for marker in required_logon_task:
    if marker not in logon_task:
        errors.append(f"Unity licensed-user logon task helper is missing: {marker!r}")

required_signing_setup = (
    '[switch]$CreateIfMissing',
    '[string]$KeyAlias = "nesbeysya"',
    'Documents\\NesbeisyaReleaseSigning\\user.keystore',
    'Read-Host $Prompt -AsSecureString',
    '"-storepass:env", "NESBEISYA_KEYTOOL_STOREPASS"',
    '"-keypass:env", "NESBEISYA_KEYTOOL_KEYPASS"',
    '"-storetype", "JKS"',
    '"-keyalg", "RSA"',
    '"-keysize", "4096"',
    'Remove-Item Env:NESBEISYA_KEYTOOL_STOREPASS',
    'Remove-Item Env:NESBEISYA_KEYTOOL_KEYPASS',
    '[Environment]::SetEnvironmentVariable("NESBEISYA_KEYSTORE_PASS"',
    '[Environment]::SetEnvironmentVariable("NESBEISYA_KEYALIAS_PASS"',
    '"User"',
    'Join-Path $unityProject "user.keystore"',
    'Copy-Item',
    'Get-ScheduledTask -TaskName $TaskName',
    'Start-ScheduledTask -TaskName $TaskName',
    'Never paste signing passwords',
    'keep at least two offline backups',
)
for marker in required_signing_setup:
    if marker not in signing_setup:
        errors.append(f"Release signing setup helper is incomplete: {marker!r}")

for marker in required_preflight:
    if marker not in preflight:
        errors.append(f"Unity runner preflight is missing: {marker!r}")

for marker in required_release_checks:
    if marker not in release_checks:
        errors.append(f"Unity release-check runner is missing: {marker!r}")

required_portable_python = (
    "3.13.15",
    "python-$Version-embed-amd64.zip",
    "d1f04d990aee1253d8569e8e5104e30fa9f5fa830899f14843448872d936a2cf",
    "Get-FileHash",
    "GITHUB_PATH",
)
for marker in required_portable_python:
    if marker not in portable_python:
        errors.append(f"Portable Python bootstrap is incomplete: {marker!r}")

if errors:
    print("Self-hosted Unity runner contract FAILED:")
    for error in errors:
        print(f"- {error}")
    sys.exit(1)

print("Self-hosted Unity runner contract passed.")
