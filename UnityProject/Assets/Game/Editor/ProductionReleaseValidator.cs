#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace DontGetSidetracked.EditorTools
{
    public sealed class ProductionReleaseValidator : IPreprocessBuildWithReport
    {
        private const string ManifestPath = "Assets/Plugins/Android/AndroidManifest.xml";
        private const string SharePathsPath = "Assets/ResultShare.androidlib/src/main/res/xml/nesbeisya_file_paths.xml";
        private const string PackagesManifestPath = "Packages/manifest.json";
        private const string RuntimeSettingsPath = "Assets/Game/Presentation/GameRuntimeSettings.cs";
        private const string AdsSettingsPath = "Assets/Game/Monetization/YandexMobileAdsService.cs";
        private const string RemoteConfigSettingsPath = "Assets/Game/Platform/RuStore/RuStoreRemoteConfigService.cs";
        private const string SdkVersionsPath = "Assets/Game/Platform/RuStore/RuStoreSdkVersions.cs";
        public int callbackOrder => -1000;

        [MenuItem("Tools/НЕ СБЕЙСЯ!/Validate Production Release")]
        public static void ValidateMenu()
        {
            List<string> errors = CollectErrors();
            if (errors.Count == 0)
            {
                UnityEngine.Debug.Log("Production release preflight passed (offline-first signed AAB build).");
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

            if (string.IsNullOrWhiteSpace(PlayerSettings.bundleVersion))
                errors.Add("Set a non-empty public application version before release.");
            if (PlayerSettings.Android.bundleVersionCode <= 0)
                errors.Add("Android bundleVersionCode must be greater than zero for release.");

            if (PlayerSettings.Android.applicationEntry != AndroidApplicationEntry.Activity)
                errors.Add("Android Application Entry must be Activity (UnityPlayerActivity), never GameActivity for RuStore Pay.");
            if (PlayerSettings.Android.minSdkVersion < AndroidSdkVersions.AndroidApiLevel25)
                errors.Add("Minimum Android API must be at least 25 for Unity 6.3.");

            AndroidSdkVersions targetSdk = PlayerSettings.Android.targetSdkVersion;
            if (targetSdk != AndroidSdkVersions.AndroidApiLevelAuto &&
                (int)targetSdk < (int)AndroidSdkVersions.AndroidApiLevel34)
                errors.Add("Target Android API must be at least the currently verified RuStore SDK baseline (34), or use highest installed.");

            if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) != ScriptingImplementation.IL2CPP)
                errors.Add("Android production build must use IL2CPP.");
            if ((PlayerSettings.Android.targetArchitectures & AndroidArchitecture.ARM64) == 0)
                errors.Add("Android production build must include ARM64.");

            ValidateSigning(errors);
            if (!EditorUserBuildSettings.buildAppBundle)
                errors.Add("Production release profile requires Android App Bundle (AAB). Enable Build App Bundle before release.");

            ValidateFileContains(ManifestPath, errors,
                ("android.permission.POST_NOTIFICATIONS", "Daily reminder permission is missing from AndroidManifest."),
                ("com.unity3d.player.UnityPlayerActivity", "Custom AndroidManifest must use UnityPlayerActivity."),
                ("android:scheme=\"nesbeisya\"", "Challenge deeplink scheme is missing from AndroidManifest."),
                ("android:host=\"challenge\"", "Challenge deeplink host is missing from AndroidManifest."),
                ("androidx.core.content.FileProvider", "Result-card FileProvider is missing from AndroidManifest."),
                ("${applicationId}.shareprovider", "Result-card FileProvider authority must be application-scoped."),
                ("@xml/nesbeisya_file_paths", "Result-card FileProvider paths resource is missing."));
            ValidateForbiddenManifestPermissions(errors);

            ValidateFileContains(SharePathsPath, errors,
                ("cache-path", "Result share paths must expose only the app cache directory."),
                ("path=\"share/\"", "Result share cache subdirectory must match NativeImageShare's share/ directory."));

            ValidateFileContains(PackagesManifestPath, errors,
                ("\"ru.rustore.pay\": \"11.1.0\"", "RuStore Pay must remain pinned to the verified version."),
                ("\"ru.rustore.installreferrer\": \"10.6.1\"", "RuStore Install Referrer must remain pinned to the verified version."),
                ("\"ru.rustore.remoteconfig\": \"10.5.1\"", "RuStore Remote Config must remain pinned to the verified version."),
                ("\"ru.rustore.update\": \"10.5.1\"", "RuStore Update must remain pinned to the verified version."),
                ("\"ru.rustore.review\": \"10.5.1\"", "RuStore Review must remain pinned to the verified version."));

            ValidateRuStoreIntegrationPresence(errors);
            ValidateFileContains(SdkVersionsPath, errors,
                ("InstallReferrer = \"10.6.1\"", "Install Referrer release target must be re-verified before production."),
                ("RemoteConfig = \"10.5.1\"", "Remote Config release target must be re-verified before production."));

            ValidateFileContains(RuntimeSettingsPath, errors,
                ("OptionalBackendBaseUrl = \"\"", "Offline-first MVP must ship without a developer-operated backend URL."));

            ValidateRemoteConfig(errors);
            ValidateAds(errors);

            string allProjectText = ReadSmallTextFiles("Assets/Game/Platform/RuStore");
            if (allProjectText.IndexOf("billingclient", StringComparison.OrdinalIgnoreCase) >= 0)
                errors.Add("Deprecated BillingClient reference detected under Platform/RuStore.");

            return errors;
        }

        private static void ValidateSigning(List<string> errors)
        {
            if (!PlayerSettings.Android.useCustomKeystore)
            {
                errors.Add("Enable a custom production Android keystore before release.");
                return;
            }

            if (string.IsNullOrWhiteSpace(PlayerSettings.Android.keystoreName))
                errors.Add("Production Android keystore path/name is empty.");
            if (string.IsNullOrWhiteSpace(PlayerSettings.Android.keyaliasName))
                errors.Add("Production Android key alias is empty.");
        }

        private static void ValidateRuStoreIntegrationPresence(List<string> errors)
        {
            if (!HasLoadedRuStoreType("InstallReferrerClient"))
                errors.Add("Install Referrer Unity integration is not loaded. Restore the verified official SDK before production release.");
            if (!HasLoadedRuStoreType("RuStoreRemoteConfigClient"))
                errors.Add("RuStore Remote Config Unity integration is not loaded. Restore the verified official SDK before production release.");
        }

        private static bool HasLoadedRuStoreType(string simpleName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int a = 0; a < assemblies.Length; a++)
            {
                Type[] types;
                try
                {
                    types = assemblies[a].GetTypes();
                }
                catch (ReflectionTypeLoadException error)
                {
                    types = error.Types;
                }
                catch
                {
                    continue;
                }

                if (types == null) continue;
                for (int i = 0; i < types.Length; i++)
                {
                    Type type = types[i];
                    if (type == null || !string.Equals(type.Name, simpleName, StringComparison.Ordinal)) continue;
                    if (type.Namespace != null && type.Namespace.StartsWith("RuStore", StringComparison.Ordinal))
                        return true;
                }
            }
            return false;
        }

        private static void ValidateForbiddenManifestPermissions(List<string> errors)
        {
            if (!File.Exists(ManifestPath)) return;
            string manifest = File.ReadAllText(ManifestPath);
            string[] forbidden =
            {
                "android.permission.ACCESS_FINE_LOCATION",
                "android.permission.ACCESS_COARSE_LOCATION",
                "android.permission.READ_CONTACTS",
                "android.permission.WRITE_CONTACTS",
                "android.permission.RECORD_AUDIO",
                "android.permission.CAMERA",
                "android.permission.READ_SMS",
                "android.permission.SEND_SMS",
                "android.permission.READ_EXTERNAL_STORAGE",
                "android.permission.WRITE_EXTERNAL_STORAGE",
                "android.permission.MANAGE_EXTERNAL_STORAGE"
            };

            for (int i = 0; i < forbidden.Length; i++)
                if (manifest.IndexOf(forbidden[i], StringComparison.Ordinal) >= 0)
                    errors.Add("Unnecessary sensitive Android permission declared: " + forbidden[i]);
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