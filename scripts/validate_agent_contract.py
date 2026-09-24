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
    analyzer = require_file("scripts/analyze_unity_log.py")
    serialized_validator = require_file("UnityProject/Assets/Game/Editor/AgentProjectValidator.cs")
    self_hosted_workflow = require_file(".github/workflows/unity-self-hosted.yml")
    self_hosted_preflight = require_file("scripts/verify_unity_runner_environment.ps1")
    self_hosted_docs = require_file("docs/ai-agent-self-hosted-unity.md")
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
            "unity-diagnostics.json",
            "unity-self-hosted.yml",
            "ai-agent-self-hosted-unity.md",
        ),
    )

    require_markers(
        "scripts/agent_check.ps1",
        check,
        (
            '[ValidateSet("Fast", "Unity", "Full", "Build", "ReleaseCandidate")]',
            'Filter "validate_*.py"',
            'Filter "test_*.py"',
            "analyze_unity_log.py",
            "playmode.log",
            "playmode-results.xml",
            "serialized-validation.log",
            "verify_release_artifact.py",
            "PureRules.Tests.csproj",
            "run_release_candidate_checks.ps1",
            "-SkipReadiness",
            "artifacts\\agent-check",
        ),
    )

    require_markers(
        "scripts/analyze_unity_log.py",
        analyzer,
        (
            "COMPILER_RE",
            "parse_test_results",
            "--json-out",
            "--fail-on-errors",
        ),
    )

    require_markers(
        "UnityProject/Assets/Game/Editor/AgentProjectValidator.cs",
        serialized_validator,
        (
            "ValidateForAutomation",
            "EditorSceneManager.GetSceneManagerSetup",
            "EditorSceneManager.RestoreSceneManagerSetup",
            "GameObjectUtility.GetMonoBehavioursWithMissingScriptCount",
            "PrefabUtility.LoadPrefabContents",
            "objectReferenceInstanceIDValue",
            "unity-serialized-validation.txt",
        ),
    )

    require_markers(
        ".github/workflows/unity-self-hosted.yml",
        self_hosted_workflow,
        (
            "agent/**",
            "UNITY_SELF_HOSTED_ENABLED",
            "runs-on: [self-hosted, windows, x64, unity]",
            "AGENT_CHECK_MODE",
            "- Build",
            "- ReleaseCandidate",
            "agent/release-candidate/",
        ),
    )

    require_markers(
        "scripts/verify_unity_runner_environment.ps1",
        self_hosted_preflight,
        (
            "6000.3.24f1",
            "UNITY_EXE",
            "Python 3",
        ),
    )

    require_markers(
        "docs/ai-agent-self-hosted-unity.md",
        self_hosted_docs,
        (
            "public",
            "Settings -> Actions -> Runners -> New self-hosted runner",
            "UNITY_SELF_HOSTED_ENABLED",
            "unity",
        ),
    )

    require_markers(
        "scripts/run_release_candidate_checks.ps1",
        release,
        (
            "-runTests",
            '"-testPlatform", "EditMode"',
            '"-testPlatform", "PlayMode"',
            "playmode-results.xml",
            "AgentProjectValidator.ValidateForAutomation",
            "serialized-validation.log",
            "ReleaseReadinessReporter.Report",
            "ProductionAndroidBuild.BuildFromCommandLine",
            "[switch]$BuildAab",
            "[switch]$BuildSmokeApk",
            "$BuildAab -and $BuildSmokeApk",
            "production-build.log",
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
