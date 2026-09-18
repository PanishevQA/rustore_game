#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace DontGetSidetracked.EditorTools
{
    [InitializeOnLoad]
    public static class RuStorePayProductionConfigurator
    {
        public const string ConsoleApplicationId = "2063758837";
        public const string DeeplinkScheme = "nesbeisyapay";

        static RuStorePayProductionConfigurator()
        {
            EditorApplication.delayCall += ConfigureExistingAsset;
        }

        [MenuItem("Tools/НЕ СБЕЙСЯ!/Configure RuStore Pay Production Settings")]
        public static void ConfigureMenu()
        {
            if (ConfigureExistingAsset()) return;

            bool opened = EditorApplication.ExecuteMenuItem("Window/RuStoreSDK/Settings/PayClient");
            EditorApplication.delayCall += () =>
            {
                if (ConfigureExistingAsset())
                {
                    Debug.Log("RuStore Pay production settings filled. In the PayClient inspector run Patch Manifest, then Verify Manifest.");
                    return;
                }

                Debug.LogWarning(
                    opened
                        ? "PayClient settings window opened, but PayClientSettings.asset was not found yet. Create it there, then run Tools → НЕ СБЕЙСЯ! → Configure RuStore Pay Production Settings."
                        : "RuStore Pay settings menu was not found. Verify ru.rustore.pay 11.1.0 is loaded.");
            };
        }

        public static bool ConfigureExistingAsset()
        {
            string[] guids = AssetDatabase.FindAssets("PayClientSettings t:ScriptableObject");
            if (guids == null || guids.Length == 0) return false;

            string assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            ScriptableObject asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
            if (asset == null) return false;

            var serialized = new SerializedObject(asset);
            SerializedProperty appId = serialized.FindProperty("consoleApplicationId");
            SerializedProperty scheme = serialized.FindProperty("deeplinkScheme");
            if (appId == null || scheme == null)
            {
                Debug.LogWarning("Current PayClientSettings schema does not expose consoleApplicationId/deeplinkScheme. Open the RuStore Pay settings window and run its migration.");
                return false;
            }

            bool changed = false;
            if (!string.Equals(appId.stringValue, ConsoleApplicationId, StringComparison.Ordinal))
            {
                appId.stringValue = ConsoleApplicationId;
                changed = true;
            }
            if (!string.Equals(scheme.stringValue, DeeplinkScheme, StringComparison.Ordinal))
            {
                scheme.stringValue = DeeplinkScheme;
                changed = true;
            }

            if (changed)
            {
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
                Debug.Log($"RuStore Pay production settings applied to {assetPath}: appId={ConsoleApplicationId}, scheme={DeeplinkScheme}.");
            }

            return true;
        }
    }

    internal sealed class RuStorePaySettingsAssetPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (importedAssets == null) return;
            for (int i = 0; i < importedAssets.Length; i++)
            {
                string path = importedAssets[i] ?? string.Empty;
                if (path.IndexOf("PayClientSettings", StringComparison.OrdinalIgnoreCase) < 0) continue;
                EditorApplication.delayCall += RuStorePayProductionConfigurator.ConfigureExistingAsset;
                return;
            }
        }
    }
}
#endif
