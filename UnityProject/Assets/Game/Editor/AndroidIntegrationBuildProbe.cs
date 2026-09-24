#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DontGetSidetracked.EditorTools
{
    /// <summary>
    /// Probe-only Android build entrypoint. It deliberately uses Unity's debug signing and a
    /// Development build so Gradle/IL2CPP and native dependency compatibility can be verified
    /// without requiring production signing secrets.
    /// </summary>
    public static class AndroidIntegrationBuildProbe
    {
        private const string OutputEnvironmentVariable = "NESBEISYA_INTEGRATION_OUTPUT";

        public static void BuildFromCommandLine()
        {
            ProjectConfigurator.Configure();
            AndroidDependencyConfigurator.Configure();
            BrandAssetConfigurator.Configure();

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                bool switched = EditorUserBuildSettings.SwitchActiveBuildTarget(
                    BuildTargetGroup.Android,
                    BuildTarget.Android);
                if (!switched)
                    throw new BuildFailedException("Could not switch the integration probe to Android.");
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.SaveAssets();

            if (!AndroidDependencyConfigurator.ForceResolveAndroidDependencies())
                throw new BuildFailedException("EDM4U Android dependency resolution failed during integration probe.");

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.SaveAssets();

            bool originalCustomKeystore = PlayerSettings.Android.useCustomKeystore;
            bool originalBuildAppBundle = EditorUserBuildSettings.buildAppBundle;
            bool originalDevelopment = EditorUserBuildSettings.development;

            try
            {
                PlayerSettings.Android.useCustomKeystore = false;
                EditorUserBuildSettings.buildAppBundle = false;
                EditorUserBuildSettings.development = true;

                string[] scenes = EditorBuildSettings.scenes
                    .Where(scene => scene.enabled && !string.IsNullOrWhiteSpace(scene.path))
                    .Select(scene => scene.path)
                    .ToArray();
                if (scenes.Length == 0)
                    throw new BuildFailedException("No enabled scenes are configured for the Android integration probe.");

                string output = ResolveOutputPath();
                string directory = Path.GetDirectoryName(output);
                if (string.IsNullOrWhiteSpace(directory))
                    throw new BuildFailedException("Could not resolve Android integration probe output directory.");

                Directory.CreateDirectory(directory);
                if (File.Exists(output)) File.Delete(output);

                var options = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = output,
                    target = BuildTarget.Android,
                    options = BuildOptions.Development
                };

                BuildReport report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != BuildResult.Succeeded)
                {
                    throw new BuildFailedException(
                        $"Android integration probe failed: {report.summary.result}; " +
                        $"errors={report.summary.totalErrors}; warnings={report.summary.totalWarnings}.");
                }

                if (!File.Exists(output))
                    throw new BuildFailedException("Unity reported success but the integration APK was not created: " + output);

                var file = new FileInfo(output);
                string summaryPath = Path.Combine(directory, "integration-build-summary.txt");
                File.WriteAllLines(summaryPath, new[]
                {
                    "Android integration probe: PASS",
                    "Unity=" + Application.unityVersion,
                    "Package=" + PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android),
                    "APK=" + file.FullName,
                    "SizeBytes=" + file.Length,
                    "BuildGuid=" + report.summary.guid
                });

                Debug.Log("Android integration probe PASSED: " + output);
            }
            finally
            {
                PlayerSettings.Android.useCustomKeystore = originalCustomKeystore;
                EditorUserBuildSettings.buildAppBundle = originalBuildAppBundle;
                EditorUserBuildSettings.development = originalDevelopment;
            }
        }

        private static string ResolveOutputPath()
        {
            string configured = Environment.GetEnvironmentVariable(OutputEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(configured))
                return Path.GetFullPath(configured.Trim());

            string unityProjectRoot = Directory.GetParent(Application.dataPath)?.FullName
                ?? throw new BuildFailedException("Could not resolve Unity project root.");
            string repoRoot = Directory.GetParent(unityProjectRoot)?.FullName
                ?? throw new BuildFailedException("Could not resolve repository root.");

            return Path.Combine(
                repoRoot,
                "artifacts",
                "android-integration-probe",
                "nesbeisya-integration.apk");
        }
    }
}
#endif
