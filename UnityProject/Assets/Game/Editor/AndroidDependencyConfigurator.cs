#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
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

        [MenuItem("Tools/НЕ СБЕЙСЯ!/Force Resolve Android Dependencies")]
        public static void ForceResolveMenu()
        {
            if (!ForceResolveAndroidDependencies())
                throw new InvalidOperationException("EDM4U Android dependency resolution failed. See the Unity Console for details.");
        }

        public static bool ForceResolveAndroidDependencies()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                Debug.LogError("EDM4U Android resolution requires Android to be the active build target.");
                return false;
            }

            Type resolverType = FindType("GooglePlayServices.PlayServicesResolver");
            if (resolverType == null)
            {
                Debug.LogError("External Dependency Manager Android Resolver is not loaded.");
                return false;
            }

            MethodInfo resolveSync = resolverType.GetMethod(
                "ResolveSync",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(bool) },
                null);
            if (resolveSync == null)
            {
                Debug.LogError("EDM4U PlayServicesResolver.ResolveSync(bool) was not found.");
                return false;
            }

            try
            {
                object result = resolveSync.Invoke(null, new object[] { true });
                bool success = result is bool value && value;
                if (!success)
                    Debug.LogError("EDM4U Force Resolve reported failure.");
                else
                    Debug.Log("EDM4U Force Resolve completed successfully.");
                return success;
            }
            catch (TargetInvocationException error)
            {
                Exception cause = error.InnerException ?? error;
                Debug.LogError("EDM4U Force Resolve threw an exception: " + cause.Message);
                return false;
            }
            catch (Exception error)
            {
                Debug.LogError("EDM4U Force Resolve failed: " + error.Message);
                return false;
            }
        }

        private static Type FindType(string fullName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type type = assemblies[i].GetType(fullName, false);
                if (type != null) return type;
            }
            return null;
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
