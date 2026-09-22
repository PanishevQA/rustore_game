#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DontGetSidetracked.EditorTools
{
    /// <summary>
    /// Builds a signed, non-Development APK only for physical-device release smoke testing.
    /// The publication artifact remains the production AAB built by ProductionAndroidBuild.
    /// </summary>
    public static class SignedDeviceSmokeBuild
    {
        private const string OutputEnvironmentVariable = "NESBEISYA_DEVICE_SMOKE_OUTPUT";

        internal static bool IsBuildingSignedSmokeApk { get; private set; }

        [MenuItem("Tools/НЕ СБЕЙСЯ!/Build/Signed Device Smoke APK")]
        public static void BuildFromMenu()
        {
            BuildSignedApk();
        }

        public static void BuildFromCommandLine()
        {
            BuildSignedApk();
        }

        private static void BuildSignedApk()
        {
            ProjectConfigurator.Configure();
            ProductionSigningRuntimeValidator.EnsureReady();
            ProductionAndroidBuild.ApplySigningSecretsFromEnvironment();
            AndroidDependencyConfigurator.Configure();
            BrandAssetConfigurator.Configure();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.SaveAssets();

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                bool switched = EditorUserBuildSettings.SwitchActiveBuildTarget(
                    BuildTargetGroup.Android,
                    BuildTarget.Android);
                if (!switched)
                    throw new BuildFailedException("Could not switch the active Unity build target to Android.");
            }

            if (!AndroidDependencyConfigurator.ForceResolveAndroidDependencies())
                throw new BuildFailedException("Android dependency resolution failed. Signed device-smoke APK is blocked.");

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.SaveAssets();

            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.development = false;

            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled && !string.IsNullOrWhiteSpace(scene.path))
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length == 0)
                throw new BuildFailedException("No enabled scenes are configured for the device-smoke build.");

            string outputPath = ResolveOutputPath();
            string directory = Path.GetDirectoryName(outputPath);
            if (string.IsNullOrWhiteSpace(directory))
                throw new BuildFailedException("Could not resolve the signed device-smoke APK output directory.");
            Directory.CreateDirectory(directory);

            string checksumPath = outputPath + ".sha256";
            DeleteStale(outputPath);
            DeleteStale(checksumPath);

            try
            {
                var options = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = outputPath,
                    target = BuildTarget.Android,
                    options = BuildOptions.None
                };

                BuildReport report;
                IsBuildingSignedSmokeApk = true;
                try
                {
                    report = BuildPipeline.BuildPlayer(options);
                }
                finally
                {
                    IsBuildingSignedSmokeApk = false;
                }

                if (report.summary.result != BuildResult.Succeeded)
                {
                    throw new BuildFailedException(
                        $"Signed device-smoke APK build failed: {report.summary.result}; " +
                        $"errors={report.summary.totalErrors}; warnings={report.summary.totalWarnings}.");
                }

                if (!File.Exists(outputPath))
                    throw new BuildFailedException("Unity reported success but the signed device-smoke APK was not created: " + outputPath);

                string sha256 = ComputeSha256(outputPath);
                File.WriteAllText(checksumPath, sha256 + "  " + Path.GetFileName(outputPath) + Environment.NewLine);
                Debug.Log($"Signed device-smoke APK created: {outputPath}\nSHA-256: {sha256}");
            }
            catch
            {
                TryDelete(outputPath);
                TryDelete(checksumPath);
                throw;
            }
        }

        private static string ResolveOutputPath()
        {
            string configured = Environment.GetEnvironmentVariable(OutputEnvironmentVariable);
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Directory.GetCurrentDirectory();
            string path;

            if (!string.IsNullOrWhiteSpace(configured))
            {
                path = configured.Trim();
                if (!Path.IsPathRooted(path))
                    path = Path.GetFullPath(Path.Combine(projectRoot, path));
            }
            else
            {
                string version = SanitizeFileName(PlayerSettings.bundleVersion);
                int versionCode = PlayerSettings.Android.bundleVersionCode;
                path = Path.Combine(projectRoot, "Builds", "Android", $"nesbeisya-device-smoke-{version}-{versionCode}.apk");
            }

            if (!string.Equals(Path.GetExtension(path), ".apk", StringComparison.OrdinalIgnoreCase))
                throw new BuildFailedException($"{OutputEnvironmentVariable} must resolve to an .apk file path.");

            return Path.GetFullPath(path);
        }

        private static string ComputeSha256(string path)
        {
            using var stream = File.OpenRead(path);
            using SHA256 sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static string SanitizeFileName(string value)
        {
            string candidate = string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim();
            foreach (char character in Path.GetInvalidFileNameChars())
                candidate = candidate.Replace(character, '_');
            return candidate;
        }

        private static void DeleteStale(string path)
        {
            if (!File.Exists(path)) return;
            File.Delete(path);
        }

        private static void TryDelete(string path)
        {
            if (!File.Exists(path)) return;
            try { File.Delete(path); }
            catch (Exception error) { Debug.LogWarning($"Could not clean failed device-smoke artifact '{path}': {error.Message}"); }
        }
    }
}
#endif
