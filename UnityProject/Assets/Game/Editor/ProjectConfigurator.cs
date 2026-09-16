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
        // Development fallback only. Once a real package name is configured, Editor startup must never overwrite it.
        public const string DevelopmentPackageName = "ru.panishedqa.nesbeisya.dev";
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
            if (ShouldAssignDevelopmentPackageName(currentPackage))
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, DevelopmentPackageName);

            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel34;
            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            EnsureScene();
        }

        private static bool ShouldAssignDevelopmentPackageName(string currentPackage)
        {
            if (string.IsNullOrWhiteSpace(currentPackage)) return true;

            // Fresh Unity projects commonly start with a generated DefaultCompany identifier.
            // Replace only that bootstrap value. Any explicit identifier, including a production RuStore one,
            // belongs to the developer and must survive Editor reloads and this menu command.
            return currentPackage.StartsWith("com.DefaultCompany.", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(currentPackage, "com.DefaultCompany.ProductName", StringComparison.OrdinalIgnoreCase);
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
