using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace DontGetSidetracked.Presentation
{
    /// <summary>
    /// Mobile-only presentation glue: keeps runtime-created canvases inside the device safe area,
    /// provides predictable Android Back behaviour and prevents half-finished rounds from resuming
    /// after the app was backgrounded.
    /// </summary>
    public sealed class MobileUiCoordinator : MonoBehaviour
    {
        private sealed class AnchorSnapshot
        {
            public Vector2 Min;
            public Vector2 Max;
        }

        private readonly Dictionary<RectTransform, AnchorSnapshot> _originalAnchors =
            new Dictionary<RectTransform, AnchorSnapshot>();

        private Rect _lastSafeArea;
        private Vector2Int _lastScreenSize;
        private float _nextCanvasScan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (FindFirstObjectByType<MobileUiCoordinator>() != null) return;
            var root = new GameObject("MobileUiCoordinator");
            DontDestroyOnLoad(root);
            root.AddComponent<MobileUiCoordinator>();
        }

        private void Awake()
        {
            _lastSafeArea = new Rect(-1, -1, -1, -1);
            _lastScreenSize = new Vector2Int(-1, -1);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) HandleBack();

            if (Time.unscaledTime >= _nextCanvasScan)
            {
                _nextCanvasScan = Time.unscaledTime + 0.5f;
                ApplySafeAreaIfNeeded();
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) AbortActiveRoundToHome();
        }

        private void HandleBack()
        {
            RuntimePlatformCoordinator platform = FindFirstObjectByType<RuntimePlatformCoordinator>();
            if (TryDismissNotificationPrompt(platform)) return;

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

            AbortActiveRoundToHome(bootstrap);
        }

        private static void AbortActiveRoundToHome(GameBootstrap bootstrap = null)
        {
            if (bootstrap == null) bootstrap = FindFirstObjectByType<GameBootstrap>();
            if (bootstrap == null) return;

            Type type = typeof(GameBootstrap);
            FieldInfo modeField = type.GetField("_mode", BindingFlags.Instance | BindingFlags.NonPublic);
            object mode = modeField?.GetValue(bootstrap);
            if (mode != null && string.Equals(mode.ToString(), "Home", StringComparison.Ordinal)) return;

            bootstrap.StopAllCoroutines();
            MethodInfo showHome = type.GetMethod("ShowHome", BindingFlags.Instance | BindingFlags.NonPublic);
            showHome?.Invoke(bootstrap, null);
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
            if (meta == null) return false;
            Type type = typeof(MetaMenuOverlay);
            FieldInfo openField = type.GetField("_panelOpen", BindingFlags.Instance | BindingFlags.NonPublic);
            if (!(openField?.GetValue(meta) is bool isOpen) || !isOpen) return false;

            MethodInfo close = type.GetMethod("ClosePanel", BindingFlags.Instance | BindingFlags.NonPublic);
            close?.Invoke(meta, null);
            return true;
        }

        private void ApplySafeAreaIfNeeded()
        {
            int width = Math.Max(1, Screen.width);
            int height = Math.Max(1, Screen.height);
            Rect safe = Screen.safeArea;
            var size = new Vector2Int(width, height);

            bool screenChanged = size != _lastScreenSize;
            bool safeChanged = !Approximately(safe, _lastSafeArea);
            bool canvasSetChanged = CaptureNewCanvases();
            if (!screenChanged && !safeChanged && !canvasSetChanged) return;

            _lastScreenSize = size;
            _lastSafeArea = safe;

            Vector2 safeMin = new Vector2(safe.xMin / width, safe.yMin / height);
            Vector2 safeMax = new Vector2(safe.xMax / width, safe.yMax / height);
            Vector2 safeSize = safeMax - safeMin;

            var missing = new List<RectTransform>();
            foreach (KeyValuePair<RectTransform, AnchorSnapshot> pair in _originalAnchors)
            {
                RectTransform rect = pair.Key;
                if (rect == null)
                {
                    missing.Add(pair.Key);
                    continue;
                }

                AnchorSnapshot original = pair.Value;
                rect.anchorMin = new Vector2(
                    safeMin.x + original.Min.x * safeSize.x,
                    safeMin.y + original.Min.y * safeSize.y);
                rect.anchorMax = new Vector2(
                    safeMin.x + original.Max.x * safeSize.x,
                    safeMin.y + original.Max.y * safeSize.y);
            }

            for (int i = 0; i < missing.Count; i++) _originalAnchors.Remove(missing[i]);
        }

        private bool CaptureNewCanvases()
        {
            bool changed = false;
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            for (int c = 0; c < canvases.Length; c++)
            {
                Canvas canvas = canvases[c];
                if (canvas == null || !ShouldFitCanvas(canvas.name)) continue;

                Transform root = canvas.transform;
                for (int i = 0; i < root.childCount; i++)
                {
                    if (!(root.GetChild(i) is RectTransform rect) || _originalAnchors.ContainsKey(rect)) continue;
                    _originalAnchors.Add(rect, new AnchorSnapshot { Min = rect.anchorMin, Max = rect.anchorMax });
                    changed = true;
                }
            }
            return changed;
        }

        private static bool ShouldFitCanvas(string canvasName) =>
            string.Equals(canvasName, "GameCanvas", StringComparison.Ordinal) ||
            string.Equals(canvasName, "MetaCanvas", StringComparison.Ordinal) ||
            string.Equals(canvasName, "RewardedCanvas", StringComparison.Ordinal) ||
            string.Equals(canvasName, "NotificationValueCanvas", StringComparison.Ordinal) ||
            string.Equals(canvasName, "MandatoryUpdateCanvas", StringComparison.Ordinal);

        private static bool Approximately(Rect a, Rect b) =>
            Mathf.Abs(a.x - b.x) < 0.5f &&
            Mathf.Abs(a.y - b.y) < 0.5f &&
            Mathf.Abs(a.width - b.width) < 0.5f &&
            Mathf.Abs(a.height - b.height) < 0.5f;
    }
}
