#!/usr/bin/env python3
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
BUILD = ROOT / "UnityProject/Assets/Game/Editor/ProductionAndroidBuild.cs"
DOC = ROOT / "docs/ANDROID_RELEASE_BUILD.md"
DUPLICATE = ROOT / "UnityProject/Assets/Game/Editor/AndroidReleaseBuilder.cs"

if not BUILD.is_file():
    raise SystemExit("Android release build entrypoint is missing.")
if DUPLICATE.exists():
    raise SystemExit("Duplicate Android release build entrypoint detected; keep ProductionAndroidBuild as the single source of truth.")
if not DOC.is_file():
    raise SystemExit("Production Android build documentation is missing.")

text = BUILD.read_text(encoding="utf-8")
doc = DOC.read_text(encoding="utf-8")

required = {
    '[MenuItem("Tools/НЕ СБЕЙСЯ!/Build/Production Android AAB")]': "Production AAB build must be available from the Unity menu.",
    'public static void BuildFromCommandLine()': "Production AAB build must expose a stable batchmode -executeMethod entrypoint.",
    'ProjectConfigurator.Configure();': "Production build must prepare Android settings and the generated bootstrap scene first.",
    'EditorUserBuildSettings.SwitchActiveBuildTarget': "Production build must explicitly switch to Android when needed.",
    'EditorUserBuildSettings.buildAppBundle = true;': "Production build must force Android App Bundle output.",
    'EditorUserBuildSettings.development = false;': "Production build must explicitly disable Unity Development build mode.",
    'target = BuildTarget.Android': "Production build must target Android.",
    'options = BuildOptions.None': "Production build must remain non-development so release prebuild validators execute fail-closed.",
    'BuildPipeline.BuildPlayer(options)': "Production build must use Unity BuildPipeline so IPreprocessBuildWithReport validators run.",
    'report.summary.result != BuildResult.Succeeded': "Production build must fail when Unity does not report success.",
    'NESBEISYA_RELEASE_OUTPUT': "Production build must support an explicit output path for automation.",
    'Path.GetExtension(path), ".aab"': "Production build output must be restricted to AAB files.",
    'Path.ChangeExtension(outputPath, ".release.json")': "Production build must emit release metadata next to the AAB.",
    'packageName = PlayerSettings.GetApplicationIdentifier': "Release metadata must record the Android package name.",
    'version = PlayerSettings.bundleVersion': "Release metadata must record the public version.",
    'versionCode = PlayerSettings.Android.bundleVersionCode': "Release metadata must record the Android versionCode.",
    'unityVersion = Application.unityVersion': "Release metadata must record the Unity version.",
    'buildGuid = summary.guid.ToString()': "Release metadata must record the Unity build GUID.",
    'totalSizeBytes = summary.totalSize': "Release metadata must record the built artifact size.",
    'builtAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)': "Release metadata must record an unambiguous UTC build timestamp.",
    'GITHUB_SHA': "Release metadata must support the GitHub commit SHA.",
    'RELEASE_GIT_SHA': "Local/batch builds must be able to provide an explicit Git SHA.",
    'JsonUtility.ToJson(metadata, true)': "Release metadata must be serialized as readable JSON.",
}

errors = [message for needle, message in required.items() if needle not in text]

for forbidden in (
    'BuildOptions.Development',
    'buildAppBundle = false',
    '.apk"',
):
    if forbidden in text:
        errors.append(f"Forbidden release build behavior detected: {forbidden}")

for required_doc_text in (
    "ProductionAndroidBuild.BuildFromCommandLine",
    "NESBEISYA_RELEASE_OUTPUT",
    ".release.json",
    "RUSTORE_RELEASE_CHECKLIST.md",
):
    if required_doc_text not in doc:
        errors.append(f"Release build documentation is missing: {required_doc_text}")

if errors:
    raise SystemExit("Android release build entrypoint validation failed:\n- " + "\n- ".join(errors))

print("Android release build entrypoint validation passed.")
