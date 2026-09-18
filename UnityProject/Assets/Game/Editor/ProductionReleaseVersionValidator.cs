#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace DontGetSidetracked.EditorTools
{
    /// <summary>
    /// Keeps the Editor bootstrap version useful for local builds without allowing that fallback
    /// to escape into a non-development Android artifact.
    /// </summary>
    public sealed class ProductionReleaseVersionValidator : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1100;

        [MenuItem("Tools/НЕ СБЕЙСЯ!/Validate Release Version")]
        public static void ValidateMenu()
        {
            ValidateOrThrow("Production release version validation failed");
            UnityEngine.Debug.Log("Production release version validation passed.");
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android) return;
            if ((report.summary.options & BuildOptions.Development) != 0) return;
            ValidateOrThrow("Android production build blocked");
        }

        internal static System.Collections.Generic.List<string> CollectErrors()
        {
            var errors = new System.Collections.Generic.List<string>();
            string publicVersion = (PlayerSettings.bundleVersion ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(publicVersion))
                errors.Add("Set a public application version before release.");
            else if (string.Equals(publicVersion, ProjectConfigurator.DevelopmentVersion, StringComparison.OrdinalIgnoreCase))
                errors.Add($"Replace Editor development fallback version '{ProjectConfigurator.DevelopmentVersion}' with the intended RuStore release version.");
            return errors;
        }

        private static void ValidateOrThrow(string prefix)
        {
            System.Collections.Generic.List<string> errors = CollectErrors();
            if (errors.Count > 0)
                throw new BuildFailedException(prefix + ": " + string.Join(" ", errors));
        }
    }
}
#endif
