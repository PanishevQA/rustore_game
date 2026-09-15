#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace DontGetSidetracked.EditorTools
{
    public sealed class ProductionReleaseValidator : IPreprocessBuildWithReport
    {
        private const string ManifestPath = "Assets/Plugins/Android/AndroidManifest.xml";
        private const string PackagesManifestPath = "Packages/manifest.json";
        private const string RuntimeSettingsPath = "Assets/Game/Presentation/GameRuntimeSettings.cs";
        private const string AdsSettingsPath = "Assets/Game/Monetization/YandexMobileAdsService.cs";
        private const string RemoteConfigSettingsPath = "Assets/Game/Platform/RuStore/RuStoreRemoteConfigService.cs";
        public int callbackOrder => -1000;

        [MenuItem("Tools/НЕ СБЕЙСЯ!/Validate Production Release")]
        public static void ValidateMenu()
        {
            List<string> errors = CollectErrors();
            if (errors.Count == 0)
            {
                UnityEngine.Debug.Log("Production release preflight passed (offline-first build).");
                return;
            }
            throw new BuildFailedException("Production release preflight failed:\n- " + string.Join("\n- ", errors));
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android) return;
            if ((report.summary.options & BuildOptions.Development) != 0) return;

            List<string> errors = CollectErrors();
            if (errors.Count > 0)
                throw new BuildFailedException("Android production build blocked:\n- " + string.Join("\n- ", errors));
        }

        private static List<string> CollectErrors()
        {
            var errors = new List<string>();
            string packageName = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            if (string.IsNullOrWhiteSpace(packageName) || packageName.EndsWith(".dev", StringComparison.OrdinalIgnoreCase))
                errors.Add("Replace the development Android package name with the exact RuStore Console package name.");

            if (PlayerSettings.Android.applicationEntry != AndroidApplicationEntry.Activity)
                errors.Add("Android Application Entry must be Activity (UnityPlayerActivity), never GameActivity for RuStore Pay.");
            if (PlayerSettings.Android.minSdkVersion < AndroidSdkVersions.AndroidApiLevel24)
                errors.Add("Minimum Android API must be at least 24.");
            if (PlayerSettings.Android.targetSdkVersion != AndroidSdkVersions.AndroidApiLevel34 &&
                PlayerSettings.Android.targetSdkVersion != AndroidSdkVersions.AndroidApiLevelAuto)
                errors.Add("Target Android API must match the currently verified RuStore SDK baseline (34) or use highest installed.");

            ValidateFileContains(ManifestPath, errors,
                ("com.unity3d.player.UnityPlayerActivity", "Custom AndroidManifest must use UnityPlayerActivity."),
                ("android:scheme=\"nesbeisya\"", "Challenge deeplink scheme is missing from AndroidManifest."),
                ("android:host=\"challenge\"", "Challenge deeplink host is missing from AndroidManifest."));

            ValidateFileContains(PackagesManifestPath, errors,
                ("nexus-external.rustore.ru/repository/npm-unity-rustore-exposed", "Use the current RuStore npm registry."),
                ("\"ru.rustore.pay\": \"11.1.0\"", "RuStore Pay must remain pinned to the verified version."),
                ("\"ru.rustore.installreferrer\": \"10.6.1\"", "RuStore Install Referrer must remain pinned to the verified version."),
                ("\"ru.rustore.update\": \"10.5.1\"", "RuStore Update must remain pinned to the verified version."),
                ("\"ru.rustore.review\": \"10.5.1\"", "RuStore Review must remain pinned to the verified version."),
                ("\"ru.rustore.remoteconfig\": \"10.5.1\"", "RuStore Remote Config must remain pinned to the verified version."));

            ValidateFileContains(RuntimeSettingsPath, errors,
                ("OptionalBackendBaseUrl = \"\"", "Offline-first MVP must ship without a developer-operated backend URL."));

            ValidateRemoteConfig(errors);
            ValidateAds(errors);

            string allProjectText = ReadSmallTextFiles("Assets/Game/Platform/RuStore");
            if (allProjectText.IndexOf("billingclient", StringComparison.OrdinalIgnoreCase) >= 0)
                errors.Add("Deprecated BillingClient reference detected under Platform/RuStore.");

            return errors;
        }

        private static void ValidateRemoteConfig(List<string> errors)
        {
            if (!File.Exists(RemoteConfigSettingsPath))
            {
                errors.Add("RuStoreRemoteConfigService.cs is missing.");
                return;
            }

            string config = File.ReadAllText(RemoteConfigSettingsPath);
            if (config.Contains("AppId = \"\"", StringComparison.Ordinal))
                errors.Add("Configure the production RuStore Remote Config AppId from RuStore Console before release.");
            if (!config.Contains("RuStoreRemoteConfigClient", StringComparison.Ordinal))
                errors.Add("RuStore Remote Config runtime adapter is not wired.");
        }

        private static void ValidateAds(List<string> errors)
        {
            string symbols = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android) ?? string.Empty;
            bool hasYandexSymbol = false;
            string[] split = symbols.Split(';');
            for (int i = 0; i < split.Length; i++)
            {
                if (string.Equals(split[i].Trim(), "YANDEX_MOBILE_ADS", StringComparison.Ordinal))
                {
                    hasYandexSymbol = true;
                    break;
                }
            }
            if (!hasYandexSymbol)
                errors.Add("Import the official Yandex Mobile Ads Unity plugin and define YANDEX_MOBILE_ADS for Android production builds.");

            if (!File.Exists(AdsSettingsPath))
            {
                errors.Add("YandexMobileAdsService.cs is missing.");
                return;
            }

            string ads = File.ReadAllText(AdsSettingsPath);
            if (ads.Contains("RewardedUnitId = \"\"", StringComparison.Ordinal) ||
                ads.Contains("InterstitialUnitId = \"\"", StringComparison.Ordinal))
                errors.Add("Configure production Yandex rewarded and interstitial ad unit IDs before release.");
            if (ads.IndexOf("= \"demo-", StringComparison.OrdinalIgnoreCase) >= 0)
                errors.Add("Demo Yandex ad unit IDs are forbidden in production builds.");
        }

        private static void ValidateFileContains(string path, List<string> errors, params (string Needle, string Error)[] checks)
        {
            if (!File.Exists(path))
            {
                errors.Add(path + " is missing.");
                return;
            }

            string content = File.ReadAllText(path);
            for (int i = 0; i < checks.Length; i++)
                if (!content.Contains(checks[i].Needle, StringComparison.Ordinal)) errors.Add(checks[i].Error);
        }

        private static string ReadSmallTextFiles(string root)
        {
            if (!Directory.Exists(root)) return string.Empty;
            var text = new System.Text.StringBuilder();
            string[] files = Directory.GetFiles(root, "*.*", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string extension = Path.GetExtension(files[i]);
                if (extension != ".cs" && extension != ".json" && extension != ".xml" && extension != ".md") continue;
                try { text.Append(File.ReadAllText(files[i])); }
                catch (IOException) { }
            }
            return text.ToString();
        }
    }
}
#endif