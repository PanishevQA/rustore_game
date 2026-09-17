#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DontGetSidetracked.EditorTools
{
    /// <summary>
    /// Single release entrypoint for local Editor builds and Unity -batchmode.
    /// All existing IPreprocessBuildWithReport validators still run through BuildPipeline.BuildPlayer.
    /// </summary>
    public static class AndroidReleaseBuilder
    {
        private const string DefaultReleaseDirectory = "Builds/Release";
        private const string ReleaseDirectoryEnvironmentVariable = "NESBEISYA_RELEASE_DIR";

        [MenuItem("Tools/НЕ СБЕЙСЯ!/Build/Production AAB")]
        public static void BuildReleaseAab()
        {
            ProjectConfigurator.Configure();

            string packageName = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            string version = PlayerSettings.bundleVersion;
            int versionCode = PlayerSettings.Android.bundleVersionCode;

            string releaseDirectory = ResolveReleaseDirectory();
            Directory.CreateDirectory(releaseDirectory);

            string safePackage = SanitizeFileName(packageName);
            string safeVersion = SanitizeFileName(version);
            string outputPath = Path.Combine(
                releaseDirectory,
                $"nesbeisya-{safePackage}-{safeVersion}-{versionCode}.aab");

            EditorUserBuildSettings.buildAppBundle = true;
            EditorUserBuildSettings.development = false;
            EditorUserBuildSettings.allowDebugging = false;
            EditorUserBuildSettings.connectProfiler = false;

            string[] scenes = GetEnabledScenes();
            if (scenes.Length == 0)
                throw new BuildFailedException("No enabled scenes are configured for the production build.");

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException(
                    $"Production Android build failed: {summary.result}. Errors: {summary.totalErrors}, warnings: {summary.totalWarnings}.");
            }

            WriteReleaseMetadata(summary, packageName, version, versionCode, outputPath);
            Debug.Log($"Production AAB built successfully: {outputPath}");
        }

        private static string[] GetEnabledScenes()
        {
            EditorBuildSettingsScene[] configured = EditorBuildSettings.scenes;
            int count = 0;
            for (int i = 0; i < configured.Length; i++)
                if (configured[i].enabled) count++;

            var result = new string[count];
            int index = 0;
            for (int i = 0; i < configured.Length; i++)
            {
                if (!configured[i].enabled) continue;
                result[index++] = configured[i].path;
            }
            return result;
        }

        private static string ResolveReleaseDirectory()
        {
            string configured = Environment.GetEnvironmentVariable(ReleaseDirectoryEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(configured))
                return Path.GetFullPath(configured);

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(projectRoot, DefaultReleaseDirectory.Replace('/', Path.DirectorySeparatorChar));
        }

        private static void WriteReleaseMetadata(
            BuildSummary summary,
            string packageName,
            string version,
            int versionCode,
            string outputPath)
        {
            var metadata = new ReleaseMetadata
            {
                packageName = packageName,
                version = version,
                versionCode = versionCode,
                unityVersion = Application.unityVersion,
                buildGuid = summary.guid.ToString(),
                builtAtUtc = DateTime.UtcNow.ToString("O"),
                artifact = Path.GetFileName(outputPath),
                totalSizeBytes = summary.totalSize
            };

            string metadataPath = Path.Combine(Path.GetDirectoryName(outputPath) ?? ResolveReleaseDirectory(), "release.json");
            File.WriteAllText(metadataPath, JsonUtility.ToJson(metadata, true));
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "unset";
            char[] invalid = Path.GetInvalidFileNameChars();
            var chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                for (int j = 0; j < invalid.Length; j++)
                {
                    if (chars[i] != invalid[j]) continue;
                    chars[i] = '-';
                    break;
                }
            }
            return new string(chars);
        }

        [Serializable]
        private sealed class ReleaseMetadata
        {
            public string packageName;
            public string version;
            public int versionCode;
            public string unityVersion;
            public string buildGuid;
            public string builtAtUtc;
            public string artifact;
            public ulong totalSizeBytes;
        }
    }
}
#endif
