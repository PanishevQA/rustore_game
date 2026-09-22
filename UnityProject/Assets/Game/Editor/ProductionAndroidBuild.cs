#if UNITY_EDITOR
using System;
using System.Globalization;
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
    /// Single production Android build entrypoint for both the Unity menu and batchmode.
    /// Existing IPreprocessBuildWithReport validators remain the source of truth for release readiness.
    /// </summary>
    public static class ProductionAndroidBuild
    {
        private const string OutputEnvironmentVariable = "NESBEISYA_RELEASE_OUTPUT";
        private const string GitShaEnvironmentVariable = "RELEASE_GIT_SHA";
        [MenuItem("Tools/НЕ СБЕЙСЯ!/Build/Production Android AAB")]
        public static void BuildFromMenu()
        {
            BuildProductionAab();
        }

        /// <summary>
        /// Batchmode entrypoint:
        /// Unity -batchmode -quit -projectPath UnityProject
        ///   -executeMethod DontGetSidetracked.EditorTools.ProductionAndroidBuild.BuildFromCommandLine
        /// </summary>
        public static void BuildFromCommandLine()
        {
            BuildProductionAab();
        }

        private static void BuildProductionAab()
        {
            ProjectConfigurator.Configure();
            ProductionSigningRuntimeValidator.EnsureReady();
            ApplySigningSecretsFromEnvironment();
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
                throw new BuildFailedException("Android dependency resolution failed. Production AAB build is blocked.");

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.SaveAssets();

            EditorUserBuildSettings.buildAppBundle = true;
            EditorUserBuildSettings.development = false;

            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled && !string.IsNullOrWhiteSpace(scene.path))
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length == 0)
                throw new BuildFailedException("No enabled scenes are configured for the production build.");

            string outputPath = ResolveOutputPath();
            string directory = Path.GetDirectoryName(outputPath);
            if (string.IsNullOrWhiteSpace(directory))
                throw new BuildFailedException("Could not resolve the production AAB output directory.");
            Directory.CreateDirectory(directory);

            string metadataPath = Path.ChangeExtension(outputPath, ".release.json");
            string metadataTempPath = metadataPath + ".tmp";
            DeleteStaleReleaseFile(outputPath);
            DeleteStaleReleaseFile(metadataPath);
            DeleteStaleReleaseFile(metadataTempPath);

            try
            {
                var options = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = outputPath,
                    target = BuildTarget.Android,
                    options = BuildOptions.None
                };

                BuildReport report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != BuildResult.Succeeded)
                {
                    throw new BuildFailedException(
                        $"Production Android build failed: {report.summary.result}; " +
                        $"errors={report.summary.totalErrors}; warnings={report.summary.totalWarnings}.");
                }

                if (!File.Exists(outputPath))
                    throw new BuildFailedException("Unity reported success but the production AAB file was not created: " + outputPath);

                WriteReleaseMetadata(metadataPath, metadataTempPath, outputPath, report.summary);
                Debug.Log($"Production Android AAB created: {outputPath}\nRelease metadata: {metadataPath}");
            }
            catch
            {
                TryDeleteFailedReleaseFile(outputPath);
                TryDeleteFailedReleaseFile(metadataPath);
                TryDeleteFailedReleaseFile(metadataTempPath);
                throw;
            }
        }

        internal static void ApplySigningSecretsFromEnvironment()
        {
            string keystorePassword = Environment.GetEnvironmentVariable(
                ProductionSigningRuntimeValidator.KeystorePasswordEnvironmentVariable);
            string keyAliasPassword = Environment.GetEnvironmentVariable(
                ProductionSigningRuntimeValidator.KeyAliasPasswordEnvironmentVariable);

            if (string.IsNullOrWhiteSpace(keystorePassword) ||
                string.IsNullOrWhiteSpace(keyAliasPassword))
            {
                throw new BuildFailedException(
                    "Production signing passwords are not available to the Unity process. " +
                    $"Set user-scoped environment variables {ProductionSigningRuntimeValidator.KeystorePasswordEnvironmentVariable} and " +
                    $"{ProductionSigningRuntimeValidator.KeyAliasPasswordEnvironmentVariable} on the self-hosted runner machine, then restart " +
                    "the runner task. Never store these values in the repository or GitHub repository variables.");
            }

            PlayerSettings.Android.keystorePass = keystorePassword;
            PlayerSettings.Android.keyaliasPass = keyAliasPassword;
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
                path = Path.Combine(projectRoot, "Builds", "Android", $"nesbeisya-{version}-{versionCode}.aab");
            }

            if (!string.Equals(Path.GetExtension(path), ".aab", StringComparison.OrdinalIgnoreCase))
                throw new BuildFailedException($"{OutputEnvironmentVariable} must resolve to an .aab file path.");

            return Path.GetFullPath(path);
        }

        private static void WriteReleaseMetadata(
            string metadataPath,
            string metadataTempPath,
            string outputPath,
            BuildSummary summary)
        {
            string gitCommit = Environment.GetEnvironmentVariable(GitShaEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(gitCommit))
                gitCommit = Environment.GetEnvironmentVariable("GITHUB_SHA");

            var artifact = new FileInfo(outputPath);
            var metadata = new ReleaseMetadata
            {
                packageName = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android) ?? string.Empty,
                version = PlayerSettings.bundleVersion ?? string.Empty,
                versionCode = PlayerSettings.Android.bundleVersionCode,
                unityVersion = Application.unityVersion ?? string.Empty,
                buildGuid = summary.guid.ToString(),
                builtAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                gitCommit = gitCommit ?? string.Empty,
                artifactFile = artifact.Name,
                outputPath = artifact.FullName,
                artifactSizeBytes = artifact.Length,
                artifactSha256 = ComputeSha256(outputPath),
                unityReportedSizeBytes = summary.totalSize
            };

            File.WriteAllText(metadataTempPath, JsonUtility.ToJson(metadata, true));
            File.Move(metadataTempPath, metadataPath);
        }

        private static void DeleteStaleReleaseFile(string path)
        {
            if (!File.Exists(path)) return;
            try
            {
                File.Delete(path);
            }
            catch (Exception error)
            {
                throw new BuildFailedException($"Could not remove stale release artifact '{path}': {error.Message}");
            }
        }

        private static void TryDeleteFailedReleaseFile(string path)
        {
            if (!File.Exists(path)) return;
            try
            {
                File.Delete(path);
            }
            catch (Exception error)
            {
                Debug.LogWarning($"Could not clean failed release artifact '{path}': {error.Message}");
            }
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
            char[] invalid = Path.GetInvalidFileNameChars();
            foreach (char character in invalid)
                candidate = candidate.Replace(character, '_');
            return candidate;
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
            public string gitCommit;
            public string artifactFile;
            public string outputPath;
            public long artifactSizeBytes;
            public string artifactSha256;
            public ulong unityReportedSizeBytes;
        }
    }
}
#endif
