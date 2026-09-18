#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DontGetSidetracked.EditorTools
{
    /// <summary>
    /// Keeps the Editor Game view aligned with the portrait-only mobile product.
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
            if (Application.isBatchMode) return;
            EditorApplication.delayCall += ApplyPortraitPreset;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem("Tools/НЕ СБЕЙСЯ!/Set Portrait Game View")]
        public static void ApplyPortraitPreset()
        {
            if (Application.isBatchMode) return;

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

                Type singletonType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
                PropertyInfo instanceProperty = singletonType.GetProperty(
                    "instance",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                object sizes = instanceProperty?.GetValue(null);
                if (sizes == null) return;

                PropertyInfo currentGroupProperty = sizesType.GetProperty(
                    "currentGroupType",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                object groupEnum = currentGroupProperty?.GetValue(sizes);
                if (groupEnum == null)
                {
                    string fallbackGroup = EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android ? "Android" : "Standalone";
                    groupEnum = Enum.Parse(groupType, fallbackGroup);
                }

                MethodInfo getGroup = sizesType.GetMethod(
                    "GetGroup",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                object group = getGroup?.Invoke(sizes, new[] { groupEnum });
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

                    MethodInfo saveToHdd = sizesType.GetMethod("SaveToHDD", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    saveToHdd?.Invoke(sizes, null);
                }

                EditorWindow gameView = EditorWindow.GetWindow(gameViewType, false, "Game", true);

                // Unity 6.x updates the toolbar reliably through SizeSelectionCallback.
                MethodInfo selectionCallback = gameViewType.GetMethod(
                    "SizeSelectionCallback",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (selectionCallback != null)
                {
                    ParameterInfo[] parameters = selectionCallback.GetParameters();
                    object[] args = parameters.Length == 2
                        ? new object[] { selectedIndex, null }
                        : new object[] { selectedIndex };
                    selectionCallback.Invoke(gameView, args);
                }
                else
                {
                    PropertyInfo selectedSizeIndex = gameViewType.GetProperty(
                        "selectedSizeIndex",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    selectedSizeIndex?.SetValue(gameView, selectedIndex);
                }

                gameView.Repaint();
            }
            catch (Exception error)
            {
                Debug.LogWarning($"Could not switch Game View to portrait automatically: {error.Message}");
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (Application.isBatchMode) return;
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredPlayMode)
                EditorApplication.delayCall += ApplyPortraitPreset;
        }
    }
}
#endif
