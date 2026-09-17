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

        private static void ValidateOrThrow(string prefix)
        {
            string publicVersion = (PlayerSettings.bundleVersion ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(publicVersion))
                throw new BuildFailedException(prefix + ": set a public application version before release.");

            if (string.Equals(publicVersion, ProjectConfigurator.DevelopmentVersion, StringComparison.OrdinalIgnoreCase))
            {
                throw new BuildFailedException(
                    prefix + $": replace Editor development fallback version '{ProjectConfigurator.DevelopmentVersion}' with the intended RuStore release version.");
            }
        }
    }
}
#endif
