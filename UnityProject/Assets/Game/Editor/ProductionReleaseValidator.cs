#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DontGetSidetracked.EditorTools
{
    public sealed class ProductionReleaseValidator : IPreprocessBuildWithReport
    {
        private const string ManifestPath = "Assets/Plugins/Android/AndroidManifest.xml";
        private const string SharePathsPath = "Assets/ResultShare.androidlib/src/main/res/xml/nesbeisya_file_paths.xml";
        private const string PackagesManifestPath = "Packages/manifest.json";
        private const string RuntimeSettingsPath = "Assets/Game/Presentation/GameRuntimeSettings.cs";
        private const string AdsSettingsPath = "Assets/Game/Monetization/YandexMobileAdsService.cs";
        private const string ProjectSettingsAssetPath = "ProjectSettings/ProjectSettings.asset";
        private const string MainGradleTemplatePath = "Assets/Plugins/Android/mainTemplate.gradle";
        private const string GradlePropertiesTemplatePath = "Assets/Plugins/Android/gradleTemplate.properties";
        private const string GradleSettingsTemplatePath = "Assets/Plugins/Android/settingsTemplate.gradle";
        private const string RemoteConfigSettingsPath = "Assets/Game/Platform/RuStore/RuStoreRemoteConfigService.cs";
        private const string InstallReferrerAdapterPath = "Assets/Game/Platform/RuStore/RuStoreInstallReferrerService.cs";
        private const string InstallReferrerDependenciesPath = "Assets/Game/Platform/RuStore/Editor/RuStoreNativeInstallReferrerDependencies.xml";
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

        internal static List<string> CollectErrors()
        {
            var errors = new List<string>();
            string packageName = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            if (!string.Equals(packageName, ProjectConfigurator.ProductionPackageName, StringComparison.Ordinal))
                errors.Add($"Android package name must be exactly '{ProjectConfigurator.ProductionPackageName}' for the RuStore production app.");

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
                ("@xml/nesbeisya_file_paths", "Result-card FileProvider paths resource is missing."));
            ValidateFileProviderAuthority(errors, packageName);
            ValidateForbiddenManifestPermissions(errors);

            ValidateFileContains(SharePathsPath, errors,
                ("cache-path", "Result share paths must expose only the app cache directory."),
                ("path=\"share/\"", "Result share cache subdirectory must match NativeImageShare's share/ directory."));

            ValidateFileContains(PackagesManifestPath, errors,
                ("\"ru.rustore.pay\": \"11.1.0\"", "RuStore Pay must remain pinned to the verified version."),
                ("\"ru.rustore.remoteconfig\": \"10.5.1\"", "RuStore Remote Config must remain pinned to the verified Unity package version."),
                ("\"ru.rustore.update\": \"10.5.1\"", "RuStore Update must remain pinned to the verified version."),
                ("\"ru.rustore.review\": \"10.5.1\"", "RuStore Review must remain pinned to the verified version."),
                ("nexus-external.vkteam.ru/repository/npm-unity-rustore-exposed", "RuStore npm registry must use the current verified vkteam endpoint."),
                ("com.google.external-dependency-manager", "EDM4U must be installed for RuStore/Yandex Android dependency resolution."),
                ("v1.2.188", "EDM4U must stay pinned to the verified 1.2.188 release."));

            ValidateNoDirectRuStoreCorePin(errors);
            ValidateRuStorePackageCompatibility(errors);
            ValidateRuStoreIntegrationPresence(errors);
            ValidateFileContains(SdkVersionsPath, errors,
                ("InstallReferrer = \"10.6.1\"", "Install Referrer release target must be re-verified before production."),
                ("RemoteConfig = \"10.5.1\"", "Remote Config release target must be re-verified before production."));

            ValidateFileContains(RuntimeSettingsPath, errors,
                ("OptionalBackendBaseUrl = \"\"", "Offline-first MVP must ship without a developer-operated backend URL."));

            ValidateRemoteConfig(errors);
            ValidateAds(errors);
            ValidateBranding(errors);

            string allProjectText = ReadSmallTextFiles("Assets/Game/Platform/RuStore");
            if (allProjectText.IndexOf("billingclient", StringComparison.OrdinalIgnoreCase) >= 0)
                errors.Add("Deprecated BillingClient reference detected under Platform/RuStore.");

            return errors;
        }

        private static void ValidateFileProviderAuthority(List<string> errors, string packageName)
        {
            if (!File.Exists(ManifestPath)) return;

            try
            {
                var document = new XmlDocument();
                document.Load(ManifestPath);

                var namespaces = new XmlNamespaceManager(document.NameTable);
                namespaces.AddNamespace("android", "http://schemas.android.com/apk/res/android");

                XmlElement provider = document.SelectSingleNode(
                    "/manifest/application/provider[@android:name='androidx.core.content.FileProvider']",
                    namespaces) as XmlElement;
                if (provider == null) return;

                string authority = provider.GetAttribute(
                    "authorities",
                    "http://schemas.android.com/apk/res/android");
                string placeholder = "${applicationId}.shareprovider";
                string concrete = (packageName ?? string.Empty) + ".shareprovider";

                if (!string.Equals(authority, placeholder, StringComparison.Ordinal) &&
                    !string.Equals(authority, concrete, StringComparison.Ordinal))
                {
                    errors.Add(
                        $"Result-card FileProvider authority must be application-scoped. " +
                        $"Actual='{authority}', expected='{placeholder}' or '{concrete}'.");
                }
            }
            catch (Exception error)
            {
                errors.Add("Could not parse AndroidManifest.xml while validating FileProvider authority: " + error.Message);
            }
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

        private static void ValidateNoDirectRuStoreCorePin(List<string> errors)
        {
            if (!File.Exists(PackagesManifestPath)) return;
            string manifest = File.ReadAllText(PackagesManifestPath);
            if (manifest.Contains("\"ru.rustore.core\"", StringComparison.Ordinal))
                errors.Add("Do not pin ru.rustore.core directly; verified RuStore feature packages must resolve their compatible core transitively.");
            if (manifest.Contains("nexus-external.rustore.ru", StringComparison.Ordinal) ||
                manifest.Contains("artifactory-external.vkpartner.ru", StringComparison.Ordinal))
                errors.Add("Obsolete RuStore repository address detected in Packages/manifest.json.");
        }

        private static void ValidateRuStorePackageCompatibility(List<string> errors)
        {
            if (!File.Exists(PackagesManifestPath)) return;
            string manifest = File.ReadAllText(PackagesManifestPath);

            if (manifest.Contains("\"ru.rustore.installreferrer\"", StringComparison.Ordinal))
            {
                errors.Add(
                    "Do not install the RuStore Install Referrer Unity package beside Remote Config 10.5.1: " +
                    "the official 10.6.1/10.5.1 Unity release artifacts contain duplicate .meta GUIDs. " +
                    "Production must use the native Android Install Referrer bridge instead.");
            }
        }

        private static void ValidateRuStoreIntegrationPresence(List<string> errors)
        {
            if (!HasLoadedRuStoreType("RuStoreRemoteConfigClient"))
                errors.Add("RuStore Remote Config Unity 10.5.1 integration is not loaded.");

            ValidateFileContains(InstallReferrerDependenciesPath, errors,
                ("ru.rustore.sdk:installreferrer:10.6.1", "Native RuStore Install Referrer 10.6.1 Maven dependency is missing."),
                ("https://nexus-external.rustore.ru/repository/maven-rustore-exposed", "Native RuStore Install Referrer must use the current official Maven repository."));

            ValidateFileContains(InstallReferrerAdapterPath, errors,
                ("ru.rustore.sdk.install.referrer.InstallReferrerClient", "Install Referrer adapter must call the official native Android client."),
                ("ru.rustore.sdk.core.tasks.OnSuccessListener", "Install Referrer adapter success listener bridge is missing."),
                ("ru.rustore.sdk.core.tasks.OnFailureListener", "Install Referrer adapter failure listener bridge is missing."),
                ("\"getInstallReferrer\"", "Install Referrer adapter must request the native one-shot referrer."),
                ("\"getReferrerId\"", "Install Referrer adapter must extract referrerId from the native result."));
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

        private static void ValidateBranding(List<string> errors)
        {
            Texture2D[] icons = PlayerSettings.GetIcons(NamedBuildTarget.Android, IconKind.Application);
            if (icons == null || icons.Length == 0)
            {
                errors.Add("Android launcher icon is missing. Run Tools → НЕ СБЕЙСЯ! → Generate Release Brand Assets.");
            }
            else
            {
                for (int i = 0; i < icons.Length; i++)
                {
                    if (icons[i] != null) continue;
                    errors.Add("Every Android launcher icon slot must use the generated НЕ СБЕЙСЯ! brand icon.");
                    break;
                }
            }

            Color splash = PlayerSettings.SplashScreen.backgroundColor;
            Color expected = new Color(0.010f, 0.016f, 0.040f, 1f);
            if (Mathf.Abs(splash.r - expected.r) > 0.02f ||
                Mathf.Abs(splash.g - expected.g) > 0.02f ||
                Mathf.Abs(splash.b - expected.b) > 0.02f)
                errors.Add("Splash background does not match the release brand palette. Re-run Generate Release Brand Assets.");
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
            ValidateFileContains(PackagesManifestPath, errors,
                ("yandexmobile/yandex-ads-unity-plugin.git?path=/mobileads-sdk#8.4.0", "Yandex Mobile Ads 8.4.0 package must stay pinned to the verified official repository tag."));
            ValidateFileContains("Assets/Game/Monetization/Game.Monetization.asmdef", errors,
                ("\"YandexMobileAds\"", "Game.Monetization must reference the YandexMobileAds assembly."),
                ("\"YANDEX_MOBILE_ADS\"", "Game.Monetization must auto-enable the Yandex adapter through an asmdef version define."),
                ("[8.4.0,8.5.0)", "Yandex adapter version define must stay constrained to the verified 8.4.x line."));

            ValidateYandexGradleTemplates(errors);

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

        private static void ValidateYandexGradleTemplates(List<string> errors)
        {
            if (!File.Exists(ProjectSettingsAssetPath))
            {
                errors.Add("ProjectSettings.asset is missing; cannot verify Yandex Mobile Ads Gradle template settings.");
                return;
            }

            string settings = File.ReadAllText(ProjectSettingsAssetPath);
            if (!settings.Contains("useCustomMainGradleTemplate: 1", StringComparison.Ordinal))
                errors.Add("Enable Custom Main Gradle Template for the Yandex Mobile Ads Android production build.");
            if (!settings.Contains("useCustomGradlePropertiesTemplate: 1", StringComparison.Ordinal))
                errors.Add("Enable Custom Gradle Properties Template for the Yandex Mobile Ads Android production build.");
            if (!settings.Contains("useCustomGradleSettingsTemplate: 1", StringComparison.Ordinal))
                errors.Add("Enable Custom Gradle Settings Template so EDM4U can own Android repositories consistently.");

            if (!File.Exists(MainGradleTemplatePath))
                errors.Add("Android production integration requires Assets/Plugins/Android/mainTemplate.gradle.");
            if (!File.Exists(GradlePropertiesTemplatePath))
                errors.Add("Android production integration requires Assets/Plugins/Android/gradleTemplate.properties.");
            if (!File.Exists(GradleSettingsTemplatePath))
                errors.Add("Android production integration requires Assets/Plugins/Android/settingsTemplate.gradle.");

            if (File.Exists(MainGradleTemplatePath))
            {
                string mainGradle = File.ReadAllText(MainGradleTemplatePath);
                if (!mainGradle.Contains("com.yandex.android:mobileads:8.4.0", StringComparison.Ordinal))
                    errors.Add("Yandex Android dependency is not resolved into mainTemplate.gradle. Run EDM4U Android Resolver → Force Resolve.");
            }
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