#!/usr/bin/env python3
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
WORKFLOW = ROOT / ".github/workflows/unity-self-hosted.yml"
PREFLIGHT = ROOT / "scripts/verify_unity_runner_environment.ps1"
errors: list[str] = []


def read(path: Path) -> str:
    if not path.is_file():
        errors.append(f"Missing required file: {path.relative_to(ROOT)}")
        return ""
    return path.read_text(encoding="utf-8")


workflow = read(WORKFLOW)
preflight = read(PREFLIGHT)

required_workflow = (
    "branches:",
    "- 'agent/**'",
    "workflow_dispatch:",
    "permissions:",
    "contents: read",
    "vars.UNITY_SELF_HOSTED_ENABLED == 'true'",
    "github.repository == 'PanishevQA/rustore_game'",
    "runs-on: [self-hosted, windows, x64, unity]",
    "timeout-minutes: 60",
    "persist-credentials: false",
    "verify_unity_runner_environment.ps1",
    "agent_check.ps1 -Mode Unity",
    "actions/upload-artifact@v4",
    "artifacts/agent-check/**",
    "artifacts/release-candidate/**",
)

for marker in required_workflow:
    if marker not in workflow:
        errors.append(f"Self-hosted Unity workflow is missing: {marker!r}")

for forbidden in ("pull_request:", "pull_request_target:", "secrets."):
    if forbidden in workflow:
        errors.append(f"Self-hosted Unity workflow must not contain unsafe trigger/secret marker: {forbidden!r}")

required_preflight = (
    'if ($env:OS -ne "Windows_NT")',
    "6000.3.24f1",
    'Resolve-RequiredCommand "git"',
    'Resolve-RequiredCommand "dotnet"',
    "Python 3",
    "UNITY_EXE",
    "10GB",
    "agent_check.ps1",
)

for marker in required_preflight:
    if marker not in preflight:
        errors.append(f"Unity runner preflight is missing: {marker!r}")

if errors:
    print("Self-hosted Unity runner contract FAILED:")
    for error in errors:
        print(f"- {error}")
    sys.exit(1)

print("Self-hosted Unity runner contract passed.")
