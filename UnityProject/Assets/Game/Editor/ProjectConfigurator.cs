#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DontGetSidetracked.EditorTools
{
    [InitializeOnLoad]
    public static class ProjectConfigurator
    {
        public const string ProductionPackageName = "ru.release.nesbeisya";
        public const string ProductionKeyAlias = "nesbeysya";
        public const string LegacyDevelopmentPackageName = "ru.panishedqa.nesbeisya.dev";
        public const string DevelopmentVersion = "0.1.0";
        private const string ScenePath = "Assets/Scenes/Main.unity";

        static ProjectConfigurator()
        {
            EditorApplication.delayCall += Configure;
        }

        [MenuItem("Tools/НЕ СБЕЙСЯ!/Apply Android Settings")]
        public static void Configure()
        {
            PlayerSettings.companyName = "PanishevQA";
            PlayerSettings.productName = "НЕ СБЕЙСЯ!";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

            string currentPackage = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            if (ShouldAssignProductionPackageName(currentPackage))
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, ProductionPackageName);

            if (string.IsNullOrWhiteSpace(PlayerSettings.bundleVersion))
                PlayerSettings.bundleVersion = DevelopmentVersion;
            if (PlayerSettings.Android.bundleVersionCode <= 0)
                PlayerSettings.Android.bundleVersionCode = 1;

            if (ShouldRaiseMinimumSdk(PlayerSettings.Android.minSdkVersion))
                PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
            if (ShouldAssignBaselineTargetSdk(PlayerSettings.Android.targetSdkVersion))
                PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel34;

            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;
            if (string.IsNullOrWhiteSpace(PlayerSettings.Android.keyaliasName))
                PlayerSettings.Android.keyaliasName = ProductionKeyAlias;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            // Runtime-created presentation/input components are discovered dynamically.
            // Engine-code stripping removed required Unity classes in the signed Android player
            // and produced a black screen on a physical device ("Could not produce class with ID 115").
            PlayerSettings.stripEngineCode = false;

            EnsureScene();
        }

        private static bool ShouldAssignProductionPackageName(string currentPackage)
        {
            if (string.IsNullOrWhiteSpace(currentPackage)) return true;

            return currentPackage.StartsWith("com.DefaultCompany.", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(currentPackage, "com.DefaultCompany.ProductName", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(currentPackage, LegacyDevelopmentPackageName, StringComparison.OrdinalIgnoreCase);
        }

        private static bool ShouldRaiseMinimumSdk(AndroidSdkVersions currentMinimum)
        {
            return (int)currentMinimum < (int)AndroidSdkVersions.AndroidApiLevel25;
        }

        private static bool ShouldAssignBaselineTargetSdk(AndroidSdkVersions currentTarget)
        {
            // Auto means highest installed and is intentionally release-safe. Explicit targets newer than
            // the verified baseline must also survive Editor reloads; only an actually older target is raised.
            if (currentTarget == AndroidSdkVersions.AndroidApiLevelAuto) return false;
            return (int)currentTarget < (int)AndroidSdkVersions.AndroidApiLevel34;
        }

        private static void EnsureScene()
        {
            if (!Directory.Exists("Assets/Scenes")) Directory.CreateDirectory("Assets/Scenes");
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }
}
#endif
