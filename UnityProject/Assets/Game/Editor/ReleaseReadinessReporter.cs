#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DontGetSidetracked.EditorTools
{
    /// <summary>
    /// One-click release preparation + readiness report.
    /// Runs the same deterministic editor preparation as the production AAB entrypoint,
    /// then reuses the exact fail-closed validators without producing an artifact.
    /// </summary>
    public static class ReleaseReadinessReporter
    {
        private const string ReportPath = "Library/NesbeisyaReleaseReadiness.txt";

        [MenuItem("Tools/НЕ СБЕЙСЯ!/Release Readiness Report")]
        public static void Report()
        {
            var preparationErrors = new List<string>();
            PrepareReleaseEnvironment(preparationErrors);

            var sections = new List<Section>
            {
                new Section("Release preparation", preparationErrors),
                new Section("Release version", ProductionReleaseVersionValidator.CollectErrors()),
                new Section("Production configuration", ProductionPlaceholderValidator.CollectErrors()),
                new Section("Production signing runtime", ProductionSigningRuntimeValidator.CollectErrors()),
                new Section("Android / SDK / build contract", ProductionReleaseValidator.CollectErrors()),
                new Section("RuStore Pay", RuStorePayReleaseContractValidator.CollectErrors())
            };

            var unique = new HashSet<string>(StringComparer.Ordinal);
            int totalErrors = 0;
            var builder = new StringBuilder(4096);
            builder.AppendLine("НЕ СБЕЙСЯ! — RELEASE READINESS");
            builder.AppendLine("Generated UTC: " + DateTime.UtcNow.ToString("O"));
            builder.AppendLine("Unity: " + Application.unityVersion);
            builder.AppendLine("Package: " + PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android));
            builder.AppendLine("Version: " + PlayerSettings.bundleVersion + " (" + PlayerSettings.Android.bundleVersionCode + ")");
            builder.AppendLine();

            for (int i = 0; i < sections.Count; i++)
            {
                Section section = sections[i];
                builder.AppendLine("[" + section.Name + "]");

                int sectionErrors = 0;
                for (int e = 0; e < section.Errors.Count; e++)
                {
                    string error = section.Errors[e];
                    if (string.IsNullOrWhiteSpace(error) || !unique.Add(error)) continue;
                    builder.AppendLine("- BLOCKED: " + error);
                    sectionErrors++;
                    totalErrors++;
                }

                if (sectionErrors == 0) builder.AppendLine("- PASS");
                builder.AppendLine();
            }

            builder.AppendLine(totalErrors == 0
                ? "STATUS: READY FOR SIGNED ANDROID DEVICE SMOKE TEST"
                : "STATUS: NOT READY — resolve the blockers above before production AAB.");

            string report = builder.ToString();
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath) ?? "Library");
            File.WriteAllText(ReportPath, report, Encoding.UTF8);

            if (totalErrors == 0)
                Debug.Log(report);
            else
                Debug.LogWarning(report);

            Debug.Log("Release readiness report written to: " + Path.GetFullPath(ReportPath));
        }

        private static void PrepareReleaseEnvironment(List<string> errors)
        {
            try
            {
                ProjectConfigurator.Configure();
                RuStorePayProductionConfigurator.ConfigureExistingAsset();
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
                    {
                        errors.Add("Could not switch active build target to Android.");
                        return;
                    }
                }

                if (!AndroidDependencyConfigurator.ForceResolveAndroidDependencies())
                    errors.Add("EDM4U Android dependency resolution failed.");

                EditorUserBuildSettings.buildAppBundle = true;
                EditorUserBuildSettings.development = false;
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.SaveAssets();
            }
            catch (Exception error)
            {
                errors.Add("Release preparation failed: " + error.Message);
            }
        }

        private readonly struct Section
        {
            public string Name { get; }
            public List<string> Errors { get; }

            public Section(string name, List<string> errors)
            {
                Name = name ?? string.Empty;
                Errors = errors ?? new List<string>();
            }
        }
    }
}
#endif
