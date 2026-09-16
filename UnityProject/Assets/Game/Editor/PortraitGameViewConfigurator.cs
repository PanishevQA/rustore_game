#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DontGetSidetracked.EditorTools
{
    /// <summary>
    /// Keeps the Editor Game view aligned with the portrait-only mobile product.
    /// Uses reflection only against UnityEditor internals and fails silently if Unity changes them.
    /// Runtime/build orientation is enforced separately by PlayerSettings and AndroidManifest.
    /// </summary>
    [InitializeOnLoad]
    public static class PortraitGameViewConfigurator
    {
        private const int Width = 1080;
        private const int Height = 1920;
        private const string Label = "НЕ СБЕЙСЯ! 1080x1920";

        static PortraitGameViewConfigurator()
        {
            EditorApplication.delayCall += ApplyPortraitPreset;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem("Tools/НЕ СБЕЙСЯ!/Set Portrait Game View")]
        public static void ApplyPortraitPreset()
        {
            try
            {
                Assembly editorAssembly = typeof(Editor).Assembly;
                Type gameViewType = editorAssembly.GetType("UnityEditor.GameView");
                Type sizesType = editorAssembly.GetType("UnityEditor.GameViewSizes");
                Type groupType = editorAssembly.GetType("UnityEditor.GameViewSizeGroupType");
                Type sizeType = editorAssembly.GetType("UnityEditor.GameViewSize");
                Type sizeKindType = editorAssembly.GetType("UnityEditor.GameViewSizeType");
                if (gameViewType == null || sizesType == null || groupType == null || sizeType == null || sizeKindType == null)
                    return;

                object sizes = sizesType.GetProperty("instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
                if (sizes == null) return;

                string groupName = EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android ? "Android" : "Standalone";
                object groupEnum = Enum.Parse(groupType, groupName);
                object group = sizesType.GetMethod("GetGroup", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(sizes, new[] { groupEnum });
                if (group == null) return;

                Type concreteGroupType = group.GetType();
                MethodInfo getTotalCount = concreteGroupType.GetMethod("GetTotalCount", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                MethodInfo getGameViewSize = concreteGroupType.GetMethod("GetGameViewSize", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                MethodInfo addCustomSize = concreteGroupType.GetMethod("AddCustomSize", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (getTotalCount == null || getGameViewSize == null || addCustomSize == null) return;

                int count = Convert.ToInt32(getTotalCount.Invoke(group, null));
                int selectedIndex = -1;
                for (int i = 0; i < count; i++)
                {
                    object existing = getGameViewSize.Invoke(group, new object[] { i });
                    if (existing == null) continue;
                    PropertyInfo widthProperty = existing.GetType().GetProperty("width", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    PropertyInfo heightProperty = existing.GetType().GetProperty("height", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    int width = widthProperty == null ? 0 : Convert.ToInt32(widthProperty.GetValue(existing));
                    int height = heightProperty == null ? 0 : Convert.ToInt32(heightProperty.GetValue(existing));
                    if (width == Width && height == Height)
                    {
                        selectedIndex = i;
                        break;
                    }
                }

                if (selectedIndex < 0)
                {
                    object fixedResolution = Enum.Parse(sizeKindType, "FixedResolution");
                    ConstructorInfo ctor = sizeType.GetConstructor(
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                        null,
                        new[] { sizeKindType, typeof(int), typeof(int), typeof(string) },
                        null);
                    if (ctor == null) return;
                    object customSize = ctor.Invoke(new object[] { fixedResolution, Width, Height, Label });
                    addCustomSize.Invoke(group, new[] { customSize });
                    count = Convert.ToInt32(getTotalCount.Invoke(group, null));
                    selectedIndex = count - 1;
                }

                EditorWindow gameView = EditorWindow.GetWindow(gameViewType, false, "Game", false);
                PropertyInfo selectedSizeIndex = gameViewType.GetProperty("selectedSizeIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                selectedSizeIndex?.SetValue(gameView, selectedIndex);
                gameView.Repaint();
            }
            catch (Exception error)
            {
                Debug.LogWarning($"Could not switch Game View to portrait automatically: {error.Message}");
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredPlayMode)
                EditorApplication.delayCall += ApplyPortraitPreset;
        }
    }
}
#endif
