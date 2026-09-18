#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace DontGetSidetracked.EditorTools
{
    /// <summary>
    /// Keeps Android dependency-resolution prerequisites deterministic on a clean checkout.
    /// Custom Gradle templates are copied from the exact installed Unity editor so the repo
    /// never carries a stale template from another Unity patch.
    /// </summary>
    [InitializeOnLoad]
    public static class AndroidDependencyConfigurator
    {
        private const string AndroidPluginsPath = "Assets/Plugins/Android";

        static AndroidDependencyConfigurator()
        {
            EditorApplication.delayCall += Configure;
        }

        [MenuItem("Tools/НЕ СБЕЙСЯ!/Prepare Android Dependency Templates")]
        public static void Configure()
        {
            try
            {
                Directory.CreateDirectory(AndroidPluginsPath);
                bool changed = false;
                changed |= EnsureUnityTemplate("mainTemplate.gradle");
                changed |= EnsureUnityTemplate("gradleTemplate.properties");
                changed |= EnsureUnityTemplate("settingsTemplate.gradle");
                changed |= EnsurePublishingFlags();

                if (changed)
                {
                    AssetDatabase.Refresh();
                    AssetDatabase.SaveAssets();
                    Debug.Log("Android dependency templates/settings prepared from the current Unity editor.");
                }
            }
            catch (Exception error)
            {
                Debug.LogWarning("Android dependency template preparation failed: " + error.Message);
            }
        }

        private static bool EnsureUnityTemplate(string fileName)
        {
            string destination = Path.Combine(AndroidPluginsPath, fileName).Replace('\\', '/');
            if (File.Exists(destination)) return false;

            string templatesRoot = Path.Combine(
                EditorApplication.applicationContentsPath,
                "PlaybackEngines",
                "AndroidPlayer",
                "Tools",
                "GradleTemplates");

            string source = Path.Combine(templatesRoot, fileName);
            if (!File.Exists(source))
                source = FindTemplate(EditorApplication.applicationContentsPath, fileName);

            if (string.IsNullOrWhiteSpace(source) || !File.Exists(source))
            {
                Debug.LogWarning($"Unity Android template not found for {fileName}. Enable it once in Player Settings if this Unity patch moved the template.");
                return false;
            }

            File.Copy(source, destination, false);
            return true;
        }

        private static string FindTemplate(string root, string fileName)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return null;
            try
            {
                string[] files = Directory.GetFiles(root, fileName, SearchOption.AllDirectories);
                return files.Length > 0 ? files[0] : null;
            }
            catch
            {
                return null;
            }
        }

        private static bool EnsurePublishingFlags()
        {
            const string projectSettingsPath = "ProjectSettings/ProjectSettings.asset";
            if (!File.Exists(projectSettingsPath)) return false;

            UnityEngine.Object[] objects = InternalEditorUtility.LoadSerializedFileAndForget(projectSettingsPath);
            if (objects == null || objects.Length == 0 || objects[0] == null) return false;

            var serialized = new SerializedObject(objects[0]);
            bool changed = false;
            changed |= SetBool(serialized, "useCustomMainManifest", true);
            changed |= SetBool(serialized, "useCustomMainGradleTemplate", true);
            changed |= SetBool(serialized, "useCustomGradlePropertiesTemplate", true);
            changed |= SetBool(serialized, "useCustomGradleSettingsTemplate", true);

            if (!changed) return false;

            serialized.ApplyModifiedPropertiesWithoutUndo();
            InternalEditorUtility.SaveToSerializedFileAndForget(objects, projectSettingsPath, true);
            return true;
        }

        private static bool SetBool(SerializedObject serialized, string propertyName, bool value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || property.propertyType != SerializedPropertyType.Boolean) return false;
            if (property.boolValue == value) return false;
            property.boolValue = value;
            return true;
        }
    }
}
#endif
