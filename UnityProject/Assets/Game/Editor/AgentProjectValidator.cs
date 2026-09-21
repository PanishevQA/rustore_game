#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DontGetSidetracked.EditorTools
{
    /// <summary>
    /// Read-only serialized-project validation for coding-agent automation.
    /// Opens build scenes and prefab contents without saving them, checks missing scripts
    /// and broken Unity object references, then restores the original Editor scene setup.
    /// </summary>
    public static class AgentProjectValidator
    {
        private const string MainScenePath = "Assets/Scenes/Main.unity";
        private const string ReportRelativePath = "artifacts/agent-check/unity-serialized-validation.txt";

        [MenuItem("Tools/НЕ СБЕЙСЯ!/QA/Validate Serialized Project")]
        public static void ValidateFromMenu()
        {
            try
            {
                RunValidation();
            }
            catch (BuildFailedException error)
            {
                Debug.LogError(error.Message);
            }
        }

        /// <summary>
        /// Batchmode entrypoint:
        /// Unity -batchmode -nographics -quit -projectPath UnityProject
        ///   -executeMethod DontGetSidetracked.EditorTools.AgentProjectValidator.ValidateForAutomation
        /// </summary>
        public static void ValidateForAutomation()
        {
            RunValidation();
        }

        private static void RunValidation()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new BuildFailedException("Serialized project validation cannot run while entering or using Play Mode.");

            if (HasDirtyOpenScenes())
                throw new BuildFailedException(
                    "Serialized project validation refused to switch scenes because the Editor has unsaved scene changes.");

            SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
            var errors = new List<string>();
            var stats = new ValidationStats();

            try
            {
                ValidateBuildScenes(errors, stats);
                ValidatePrefabs(errors, stats);
                ValidateScriptableObjects(errors, stats);
                ValidateMaterials(errors, stats);
            }
            finally
            {
                try
                {
                    EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
                }
                catch (Exception error)
                {
                    errors.Add("Could not restore the original Editor scene setup: " + error.Message);
                }
            }

            string reportPath = WriteReport(errors, stats);
            if (errors.Count > 0)
            {
                throw new BuildFailedException(
                    $"Serialized project validation failed with {errors.Count} error(s). Report: {reportPath}");
            }

            Debug.Log(
                $"Serialized project validation passed. " +
                $"Scenes={stats.Scenes}, Prefabs={stats.Prefabs}, GameObjects={stats.GameObjects}, " +
                $"ScriptableObjects={stats.ScriptableObjects}, Materials={stats.Materials}. Report: {reportPath}");
        }

        private static bool HasDirtyOpenScenes()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.IsValid() && scene.isDirty)
                    return true;
            }

            return false;
        }

        private static void ValidateBuildScenes(List<string> errors, ValidationStats stats)
        {
            string[] scenePaths = EditorBuildSettings.scenes
                .Where(scene => scene.enabled && !string.IsNullOrWhiteSpace(scene.path))
                .Select(scene => scene.path)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (scenePaths.Length == 0)
            {
                errors.Add("No enabled scenes exist in EditorBuildSettings.");
                return;
            }

            if (!scenePaths.Contains(MainScenePath, StringComparer.Ordinal))
                errors.Add("Main scene is not enabled in EditorBuildSettings: " + MainScenePath);

            for (int i = 0; i < scenePaths.Length; i++)
            {
                string scenePath = scenePaths[i];
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
                {
                    errors.Add("Enabled build scene is missing or unreadable: " + scenePath);
                    continue;
                }

                Scene scene;
                try
                {
                    scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                }
                catch (Exception error)
                {
                    errors.Add($"Could not open scene '{scenePath}': {error.Message}");
                    continue;
                }

                stats.Scenes++;
                foreach (GameObject root in scene.GetRootGameObjects())
                    ValidateHierarchy(root, scenePath, errors, stats);
            }
        }

        private static void ValidatePrefabs(List<string> errors, ValidationStats stats)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            Array.Sort(guids, StringComparer.Ordinal);

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameObject root = null;
                try
                {
                    root = PrefabUtility.LoadPrefabContents(path);
                    stats.Prefabs++;
                    ValidateHierarchy(root, path, errors, stats);
                }
                catch (Exception error)
                {
                    errors.Add($"Could not validate prefab '{path}': {error.Message}");
                }
                finally
                {
                    if (root != null)
                        PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        private static void ValidateScriptableObjects(List<string> errors, ValidationStats stats)
        {
            string[] guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets" });
            Array.Sort(guids, StringComparer.Ordinal);

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                ScriptableObject asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (asset == null)
                {
                    errors.Add("ScriptableObject asset could not be loaded: " + path);
                    continue;
                }

                stats.ScriptableObjects++;
                ValidateSerializedObject(asset, path, errors, stats);
            }
        }

        private static void ValidateMaterials(List<string> errors, ValidationStats stats)
        {
            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets" });
            Array.Sort(guids, StringComparer.Ordinal);

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    errors.Add("Material asset could not be loaded: " + path);
                    continue;
                }

                stats.Materials++;
                if (material.shader == null || string.Equals(
                        material.shader.name,
                        "Hidden/InternalErrorShader",
                        StringComparison.Ordinal))
                {
                    errors.Add("Material has a missing/error shader: " + path);
                }

                ValidateSerializedObject(material, path, errors, stats);
            }
        }

        private static void ValidateHierarchy(
            GameObject root,
            string assetPath,
            List<string> errors,
            ValidationStats stats)
        {
            if (root == null)
            {
                errors.Add("Serialized hierarchy root is null: " + assetPath);
                return;
            }

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                GameObject gameObject = transforms[i].gameObject;
                stats.GameObjects++;
                string hierarchyPath = BuildHierarchyPath(gameObject.transform);

                int missingScripts = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject);
                if (missingScripts > 0)
                {
                    errors.Add(
                        $"{assetPath} :: {hierarchyPath} has {missingScripts} missing MonoBehaviour script(s).");
                }

                Component[] components = gameObject.GetComponents<Component>();
                for (int c = 0; c < components.Length; c++)
                {
                    Component component = components[c];
                    if (component == null)
                        continue;

                    ValidateSerializedObject(
                        component,
                        $"{assetPath} :: {hierarchyPath} :: {component.GetType().Name}",
                        errors,
                        stats);
                }
            }
        }

        private static void ValidateSerializedObject(
            UnityEngine.Object target,
            string context,
            List<string> errors,
            ValidationStats stats)
        {
            try
            {
                SerializedObject serializedObject = new SerializedObject(target);
                stats.SerializedObjects++;

                SerializedProperty property = serializedObject.GetIterator();
                bool enterChildren = true;
                while (property.NextVisible(enterChildren))
                {
                    enterChildren = false;
                    if (property.propertyType != SerializedPropertyType.ObjectReference)
                        continue;

                    if (property.objectReferenceValue == null && property.objectReferenceInstanceIDValue != 0)
                    {
                        errors.Add(
                            $"{context} has a broken object reference at serialized property '{property.propertyPath}'.");
                    }
                }
            }
            catch (Exception error)
            {
                errors.Add($"{context} could not be inspected through SerializedObject: {error.Message}");
            }
        }

        private static string BuildHierarchyPath(Transform transform)
        {
            if (transform == null)
                return "<null>";

            var names = new Stack<string>();
            Transform current = transform;
            while (current != null)
            {
                names.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", names);
        }

        private static string WriteReport(IReadOnlyList<string> errors, ValidationStats stats)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Directory.GetCurrentDirectory();
            string reportPath = Path.Combine(
                projectRoot,
                ReportRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string directory = Path.GetDirectoryName(reportPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var builder = new StringBuilder(4096);
            builder.AppendLine("НЕ СБЕЙСЯ! — SERIALIZED PROJECT VALIDATION");
            builder.AppendLine("Generated UTC: " + DateTime.UtcNow.ToString("O"));
            builder.AppendLine("Unity: " + Application.unityVersion);
            builder.AppendLine($"Scenes: {stats.Scenes}");
            builder.AppendLine($"Prefabs: {stats.Prefabs}");
            builder.AppendLine($"GameObjects: {stats.GameObjects}");
            builder.AppendLine($"ScriptableObjects: {stats.ScriptableObjects}");
            builder.AppendLine($"Materials: {stats.Materials}");
            builder.AppendLine($"SerializedObjects: {stats.SerializedObjects}");
            builder.AppendLine();

            if (errors.Count == 0)
            {
                builder.AppendLine("STATUS: PASS");
            }
            else
            {
                builder.AppendLine("STATUS: FAILED");
                for (int i = 0; i < errors.Count; i++)
                    builder.AppendLine("- " + errors[i]);
            }

            File.WriteAllText(reportPath, builder.ToString(), Encoding.UTF8);
            return Path.GetFullPath(reportPath);
        }

        private sealed class ValidationStats
        {
            public int Scenes;
            public int Prefabs;
            public int GameObjects;
            public int ScriptableObjects;
            public int Materials;
            public int SerializedObjects;
        }
    }
}
#endif
