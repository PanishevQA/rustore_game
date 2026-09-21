#!/usr/bin/env python3
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
ERRORS: list[str] = []


def require_file(relative: str) -> str:
    path = ROOT / relative
    if not path.is_file():
        ERRORS.append(f"Missing agent contract file: {relative}")
        return ""
    return path.read_text(encoding="utf-8")


def require_markers(relative: str, text: str, markers: tuple[str, ...]) -> None:
    for marker in markers:
        if marker not in text:
            ERRORS.append(f"{relative} is missing required marker: {marker!r}")


def main() -> int:
    agents = require_file("AGENTS.md")
    check = require_file("scripts/agent_check.ps1")
    release = require_file("scripts/run_release_candidate_checks.ps1")
    gitignore = require_file(".gitignore")

    require_markers(
        "AGENTS.md",
        agents,
        (
            "UnityProject/",
            "6000.3.24f1",
            "DontGetSidetracked",
            "agent_check.ps1",
            "run_release_candidate_checks.ps1",
            "Definition of Done",
            "UnityProject/Library/",
            "git reset --hard",
            "codex-local-ui-20260919",
            "GameBootstrapRuntimeBridge",
        ),
    )

    require_markers(
        "scripts/agent_check.ps1",
        check,
        (
            '[ValidateSet("Fast", "Unity", "Full")]',
            'Filter "validate_*.py"',
            "PureRules.Tests.csproj",
            "run_release_candidate_checks.ps1",
            "-SkipReadiness",
            "artifacts\\agent-check",
        ),
    )

    require_markers(
        "scripts/run_release_candidate_checks.ps1",
        release,
        (
            "-runTests",
            '"-testPlatform", "EditMode"',
            "ReleaseReadinessReporter.Report",
            "editmode.log",
            "editmode-results.xml",
        ),
    )

    require_markers(
        ".gitignore",
        gitignore,
        (
            "[Ll]ibrary/",
            "[Tt]emp/",
            "[Ll]ogs/",
            "*.csproj",
            "*.sln",
            "/artifacts/",
        ),
    )

    if ERRORS:
        print("AI agent contract validation FAILED:")
        for error in ERRORS:
            print(f"- {error}")
        return 1

    print("AI agent contract validation passed.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
