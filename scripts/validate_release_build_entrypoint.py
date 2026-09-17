from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
builder_path = ROOT / "UnityProject/Assets/Game/Editor/AndroidReleaseBuilder.cs"
checklist_path = ROOT / "docs/RUSTORE_RELEASE_CHECKLIST.md"

assert builder_path.exists(), "AndroidReleaseBuilder.cs is missing"
text = builder_path.read_text(encoding="utf-8")

required = [
    "ProjectConfigurator.Configure();",
    "EditorUserBuildSettings.buildAppBundle = true;",
    "EditorUserBuildSettings.development = false;",
    "BuildPipeline.BuildPlayer(options)",
    "target = BuildTarget.Android",
    "options = BuildOptions.None",
    "summary.result != BuildResult.Succeeded",
    "release.json",
    "packageName = packageName",
    "version = version",
    "versionCode = versionCode",
    "buildGuid = summary.guid.ToString()",
    "NESBEISYA_RELEASE_DIR",
]
for needle in required:
    assert needle in text, f"Release build entrypoint contract missing: {needle}"

assert "BuildOptions.Development" not in text, "Production entrypoint must never enable Development build"
assert ".apk" not in text, "Production entrypoint must produce AAB, not APK"
assert ".aab" in text, "Production artifact extension must be .aab"

checklist = checklist_path.read_text(encoding="utf-8")
assert "AndroidReleaseBuilder.BuildReleaseAab" in checklist, "Release checklist must document the batchmode entrypoint"
assert "release.json" in checklist, "Release checklist must require release metadata verification"
assert "Remote Config Unity: **10.5.1**" in checklist or "current official Unity target — **10.5.1**" in checklist, (
    "Release checklist Remote Config baseline must match the current verified 10.5.1 target"
)

print("release build entrypoint validation passed")
