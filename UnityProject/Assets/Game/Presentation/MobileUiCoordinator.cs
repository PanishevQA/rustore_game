using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace DontGetSidetracked.Presentation
{
    /// <summary>
    /// Mobile-only presentation glue: keeps runtime-created content inside the device safe area,
    /// provides predictable Android Back behaviour and prevents half-finished rounds from resuming
    /// after the app was backgrounded.
    ///
    /// Safe area is applied to a dedicated wrapper under each Canvas instead of rewriting the anchors
    /// of gameplay/layout elements themselves. Full-bleed visual backgrounds remain outside the wrapper.
    /// </summary>
    [DefaultExecutionOrder(20000)]
    public sealed class MobileUiCoordinator : MonoBehaviour
    {
        private const string SafeAreaRootName = "SafeAreaRoot";
        private const string FullBleedBackgroundName = "VisualBackground";

        private readonly Dictionary<Canvas, RectTransform> _safeRoots =
            new Dictionary<Canvas, RectTransform>();

        private float _nextCanvasScan;
        private bool _restartTutorialOnResume;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (FindFirstObjectByType<MobileUiCoordinator>() != null) return;
            var root = new GameObject("MobileUiCoordinator");
            DontDestroyOnLoad(root);
            root.AddComponent<MobileUiCoordinator>();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) HandleBack();
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime < _nextCanvasScan) return;
            _nextCanvasScan = Time.unscaledTime + 0.25f;
            ApplySafeArea();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                AbortOnlyActiveGesture();
                return;
            }

            if (_restartTutorialOnResume)
            {
                _restartTutorialOnResume = false;
                RestartTutorial();
            }
        }

        private void HandleBack()
        {
            RuntimePlatformCoordinator platform = FindFirstObjectByType<RuntimePlatformCoordinator>();
            if (TryDismissNotificationPrompt(platform)) return;

            TrainingMenuCoordinator training = FindFirstObjectByType<TrainingMenuCoordinator>();
            if (training != null && training.IsOpen)
            {
                training.Close();
                return;
            }

            CampaignLevelMenuOverlay campaign = FindFirstObjectByType<CampaignLevelMenuOverlay>();
            if (campaign != null && campaign.IsOpen)
            {
                campaign.Close();
                return;
            }

            MetaMenuOverlay meta = FindFirstObjectByType<MetaMenuOverlay>();
            if (TryCloseMetaPanel(meta)) return;

            GameBootstrap bootstrap = FindFirstObjectByType<GameBootstrap>();
            if (bootstrap == null)
            {
                Application.Quit();
                return;
            }

            Type type = typeof(GameBootstrap);
            FieldInfo modeField = type.GetField("_mode", BindingFlags.Instance | BindingFlags.NonPublic);
            object mode = modeField?.GetValue(bootstrap);
            if (mode != null && string.Equals(mode.ToString(), "Home", StringComparison.Ordinal))
            {
                Application.Quit();
                return;
            }

            AbortToHome(bootstrap);
        }

        private void AbortOnlyActiveGesture()
        {
            GameBootstrap bootstrap = FindFirstObjectByType<GameBootstrap>();
            if (bootstrap == null) return;

            Type type = typeof(GameBootstrap);
            FieldInfo stateField = type.GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo modeField = type.GetField("_mode", BindingFlags.Instance | BindingFlags.NonPublic);
            string state = stateField?.GetValue(bootstrap)?.ToString() ?? string.Empty;
            string mode = modeField?.GetValue(bootstrap)?.ToString() ?? string.Empty;

            if (!string.Equals(state, "Showing", StringComparison.Ordinal) &&
                !string.Equals(state, "Drawing", StringComparison.Ordinal)) return;

            if (string.Equals(mode, "Tutorial", StringComparison.Ordinal))
                _restartTutorialOnResume = true;

            AbortToHome(bootstrap);
        }

        private static void AbortToHome(GameBootstrap bootstrap = null)
        {
            if (bootstrap == null) bootstrap = FindFirstObjectByType<GameBootstrap>();
            if (bootstrap == null) return;

            Type type = typeof(GameBootstrap);
            FieldInfo modeField = type.GetField("_mode", BindingFlags.Instance | BindingFlags.NonPublic);
            object mode = modeField?.GetValue(bootstrap);
            if (mode != null && string.Equals(mode.ToString(), "Home", StringComparison.Ordinal)) return;

            FieldInfo pointerField = type.GetField("_pointerDown", BindingFlags.Instance | BindingFlags.NonPublic);
            pointerField?.SetValue(bootstrap, false);
            bootstrap.StopAllCoroutines();
            CampaignRuntimeCoordinator.ReturnHome(bootstrap);
        }

        private static void RestartTutorial()
        {
            GameBootstrap bootstrap = FindFirstObjectByType<GameBootstrap>();
            if (bootstrap == null) return;
            Type type = typeof(GameBootstrap);
            MethodInfo startTutorial = type.GetMethod("StartTutorial", BindingFlags.Instance | BindingFlags.NonPublic);
            startTutorial?.Invoke(bootstrap, null);
        }

        private static bool TryDismissNotificationPrompt(RuntimePlatformCoordinator platform)
        {
            if (platform == null) return false;
            Type type = typeof(RuntimePlatformCoordinator);
            FieldInfo openField = type.GetField("_notificationPromptOpen", BindingFlags.Instance | BindingFlags.NonPublic);
            if (!(openField?.GetValue(platform) is bool isOpen) || !isOpen) return false;

            MethodInfo decline = type.GetMethod("DeclineNotificationValuePrompt", BindingFlags.Instance | BindingFlags.NonPublic);
            decline?.Invoke(platform, null);
            return true;
        }

        private static bool TryCloseMetaPanel(MetaMenuOverlay meta)
        {
            if (meta == null || !meta.IsPanelOpen) return false;
            meta.ClosePanel();
            return true;
        }

        private void ApplySafeArea()
        {
            int width = Math.Max(1, Screen.width);
            int height = Math.Max(1, Screen.height);
            Rect safe = Screen.safeArea;

            Vector2 safeMin = new Vector2(safe.xMin / width, safe.yMin / height);
            Vector2 safeMax = new Vector2(safe.xMax / width, safe.yMax / height);

            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas == null || !ShouldFitCanvas(canvas.name)) continue;

                RectTransform safeRoot = EnsureSafeAreaRoot(canvas);
                ReparentDirectUiChildren(canvas, safeRoot);
                safeRoot.anchorMin = safeMin;
                safeRoot.anchorMax = safeMax;
                safeRoot.offsetMin = Vector2.zero;
                safeRoot.offsetMax = Vector2.zero;
            }

            CleanupDestroyedCanvases();
        }

        private RectTransform EnsureSafeAreaRoot(Canvas canvas)
        {
            if (_safeRoots.TryGetValue(canvas, out RectTransform existing) && existing != null)
                return existing;

            Transform found = canvas.transform.Find(SafeAreaRootName);
            RectTransform rect = found as RectTransform;
            if (rect == null)
            {
                var root = new GameObject(SafeAreaRootName, typeof(RectTransform));
                rect = root.GetComponent<RectTransform>();
                rect.SetParent(canvas.transform, false);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }

            _safeRoots[canvas] = rect;
            return rect;
        }

        private static void ReparentDirectUiChildren(Canvas canvas, RectTransform safeRoot)
        {
            Transform canvasTransform = canvas.transform;
            var toMove = new List<RectTransform>();
            for (int i = 0; i < canvasTransform.childCount; i++)
            {
                Transform child = canvasTransform.GetChild(i);
                if (child == safeRoot || string.Equals(child.name, FullBleedBackgroundName, StringComparison.Ordinal))
                    continue;
                if (child is RectTransform rect) toMove.Add(rect);
            }

            for (int i = 0; i < toMove.Count; i++)
                toMove[i].SetParent(safeRoot, false);
        }

        private void CleanupDestroyedCanvases()
        {
            if (_safeRoots.Count == 0) return;
            var stale = new List<Canvas>();
            foreach (KeyValuePair<Canvas, RectTransform> pair in _safeRoots)
                if (pair.Key == null || pair.Value == null) stale.Add(pair.Key);
            for (int i = 0; i < stale.Count; i++) _safeRoots.Remove(stale[i]);
        }

        private static bool ShouldFitCanvas(string canvasName) =>
            string.Equals(canvasName, "GameCanvas", StringComparison.Ordinal) ||
            string.Equals(canvasName, "MetaCanvas", StringComparison.Ordinal) ||
            string.Equals(canvasName, "CampaignCanvas", StringComparison.Ordinal) ||
            string.Equals(canvasName, "TrainingSelectCanvas", StringComparison.Ordinal) ||
            string.Equals(canvasName, "RewardedCanvas", StringComparison.Ordinal) ||
            string.Equals(canvasName, "HintCanvas", StringComparison.Ordinal) ||
            string.Equals(canvasName, "ResultEnhancementCanvas", StringComparison.Ordinal) ||
            string.Equals(canvasName, "NotificationValueCanvas", StringComparison.Ordinal) ||
            string.Equals(canvasName, "MandatoryUpdateCanvas", StringComparison.Ordinal);
    }
}
