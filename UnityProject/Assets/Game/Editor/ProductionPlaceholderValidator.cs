#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace DontGetSidetracked.EditorTools
{
    /// <summary>
    /// Fail-closed release guard for configuration values that are syntactically non-empty
    /// but still clearly look like development/example placeholders.
    /// </summary>
    public sealed class ProductionPlaceholderValidator : IPreprocessBuildWithReport
    {
        private const string RemoteConfigSettingsPath = "Assets/Game/Platform/RuStore/RuStoreRemoteConfigService.cs";
        private const string AdsSettingsPath = "Assets/Game/Monetization/YandexMobileAdsService.cs";

        private static readonly string[] PlaceholderMarkers =
        {
            "placeholder",
            "change_me",
            "change-me",
            "changeme",
            "replace_me",
            "replace-me",
            "replaceme",
            "your_app",
            "your-app",
            "your.app",
            "your_id",
            "your-id",
            "your.id",
            "example",
            "dummy",
            "defaultcompany",
            "todo"
        };

        public int callbackOrder => -900;

        [MenuItem("Tools/НЕ СБЕЙСЯ!/Validate Production Placeholders")]
        public static void ValidateMenu()
        {
            List<string> errors = CollectErrors();
            if (errors.Count == 0)
            {
                UnityEngine.Debug.Log("Production placeholder validation passed.");
                return;
            }

            throw new BuildFailedException("Production placeholder validation failed:\n- " + string.Join("\n- ", errors));
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android) return;
            if ((report.summary.options & BuildOptions.Development) != 0) return;

            List<string> errors = CollectErrors();
            if (errors.Count > 0)
                throw new BuildFailedException("Android production build contains placeholder configuration:\n- " + string.Join("\n- ", errors));
        }

        internal static List<string> CollectErrors()
        {
            var errors = new List<string>();

            string packageName = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            if (LooksLikePlaceholder(packageName) || packageName.EndsWith(".dev", StringComparison.OrdinalIgnoreCase))
                errors.Add("Android package name still looks like a development/example value. Use the exact RuStore Console package name.");

            ValidateConstString(RemoteConfigSettingsPath, "AppId", "RuStore Remote Config AppId", errors, rejectDemoPrefix: false);
            ValidateConstString(AdsSettingsPath, "RewardedUnitId", "Yandex rewarded ad unit ID", errors, rejectDemoPrefix: true);
            ValidateConstString(AdsSettingsPath, "InterstitialUnitId", "Yandex interstitial ad unit ID", errors, rejectDemoPrefix: true);

            if (LooksLikePlaceholder(PlayerSettings.Android.keystoreName))
                errors.Add("Production keystore path/name still looks like a placeholder.");
            if (LooksLikePlaceholder(PlayerSettings.Android.keyaliasName))
                errors.Add("Production key alias still looks like a placeholder.");

            return errors;
        }

        private static void ValidateConstString(
            string path,
            string constantName,
            string displayName,
            List<string> errors,
            bool rejectDemoPrefix)
        {
            if (!TryReadConstString(path, constantName, out string value))
            {
                errors.Add($"Could not resolve {displayName} from {path}.");
                return;
            }

            if (LooksLikePlaceholder(value))
            {
                errors.Add($"{displayName} is empty or still looks like a placeholder.");
                return;
            }

            if (rejectDemoPrefix)
            {
                if (value.StartsWith("demo-", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add($"{displayName} uses a demo unit and is forbidden in production.");
                    return;
                }

                if (!value.StartsWith("R-M-", StringComparison.OrdinalIgnoreCase))
                    errors.Add($"{displayName} must use a production Yandex unit ID starting with R-M-.");
            }
        }

        private static bool TryReadConstString(string path, string constantName, out string value)
        {
            value = string.Empty;
            if (!File.Exists(path)) return false;

            string text = File.ReadAllText(path);
            string pattern = @"\bconst\s+string\s+" + Regex.Escape(constantName) + "\\s*=\\s*\"([^\"]*)\"\\s*;";
            Match match = Regex.Match(text, pattern, RegexOptions.CultureInvariant);
            if (!match.Success) return false;

            value = match.Groups[1].Value.Trim();
            return true;
        }

        private static bool LooksLikePlaceholder(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return true;

            string normalized = value.Trim().ToLowerInvariant();
            for (int i = 0; i < PlaceholderMarkers.Length; i++)
            {
                if (normalized.Contains(PlaceholderMarkers[i], StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
    }
}
#endif
