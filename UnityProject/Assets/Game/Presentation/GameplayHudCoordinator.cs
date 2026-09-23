using System;
using UnityEngine;
using UnityEngine.UI;

namespace DontGetSidetracked.Presentation
{
    /// <summary>
    /// Target release gameplay chrome. Presentation-only: GameBootstrap keeps ownership
    /// of route generation, round state, input sampling and scoring.
    /// </summary>
    [DefaultExecutionOrder(16000)]
    public sealed class GameplayHudCoordinator : MonoBehaviour
    {
        public static bool IsGameplayPaused { get; private set; }

        private GameBootstrap _bootstrap;
        private Text _legacyTitle;
        private Text _legacyStatus;
        private RectTransform _playArea;
        private CanvasGroup _legacyTitleGroup;
        private CanvasGroup _legacyStatusGroup;

        private CanvasGroup _hudGroup;
        private Text _instruction;
        private Text _hint;
        private Text _countdown;
        private GameObject _progressRoot;
        private Image _progressFill;
        private Text _phaseText;
        private Button _pauseButton;
        private GameObject _pauseOverlay;
        private float _nextResolve;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetPauseState()
        {
            IsGameplayPaused = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (FindFirstObjectByType<GameplayHudCoordinator>() != null) return;
            var root = new GameObject("GameplayHudCoordinator");
            DontDestroyOnLoad(root);
            root.AddComponent<GameplayHudCoordinator>();
        }

        private void LateUpdate()
        {
            if ((_bootstrap == null || _legacyTitle == null || _legacyStatus == null || _hudGroup == null) &&
                Time.unscaledTime >= _nextResolve)
            {
                _nextResolve = Time.unscaledTime + 0.25f;
                ResolveAndBuild();
            }

            if (_bootstrap == null || _legacyTitle == null || _legacyStatus == null || _hudGroup == null) return;

            bool active = GameBootstrapRuntimeBridge.IsActiveRound(_bootstrap);
            bool result = GameBootstrapRuntimeBridge.IsResult(_bootstrap);
            bool plainHome = GameBootstrapRuntimeBridge.IsPlainHome(_bootstrap);

            if (active)
            {
                ApplyGameplayBoardLayout(true);
                SetLegacyVisible(false);
                SetHudVisible(true);
                Refresh();
            }
            else
            {
                if (IsGameplayPaused) ResumeFromPause();
                ApplyRestingBoardLayout(result);
                SetHudVisible(false);
                if (!plainHome && !result) SetLegacyVisible(true);
            }
        }

        private void OnDisable()
        {
            IsGameplayPaused = false;
        }

        private void ResolveAndBuild()
        {
            if (_bootstrap == null) _bootstrap = FindFirstObjectByType<GameBootstrap>();

            GameObject gameCanvas = GameObject.Find("GameCanvas");
            if (gameCanvas == null) return;

            _legacyTitle = Find<Text>(gameCanvas.transform, "Title");
            _legacyStatus = Find<Text>(gameCanvas.transform, "Status");
            _playArea = Find<RectTransform>(gameCanvas.transform, "PlayArea");
            if (_legacyTitle == null || _legacyStatus == null || _playArea == null) return;

            _legacyTitleGroup = EnsureCanvasGroup(_legacyTitle.gameObject);
            _legacyStatusGroup = EnsureCanvasGroup(_legacyStatus.gameObject);

            Transform existing = gameCanvas.transform.Find("ReleaseGameplayHud");
            if (existing != null)
            {
                _hudGroup = existing.GetComponent<CanvasGroup>();
                _instruction = Find<Text>(existing, "Instruction");
                _hint = Find<Text>(existing, "Hint");
                _countdown = Find<Text>(existing, "Countdown");
                _phaseText = Find<Text>(existing, "Phase");
                _progressFill = Find<Image>(existing, "ProgressFill");
                _progressRoot = FindTransform(existing, "ProgressTrack")?.gameObject;
                _pauseButton = Find<Button>(existing, "PauseButton");
                _pauseOverlay = FindTransform(existing, "PauseOverlay")?.gameObject;
                return;
            }

            Build(gameCanvas.transform);
        }

        private void Build(Transform canvas)
        {
            var root = new GameObject("ReleaseGameplayHud", typeof(RectTransform), typeof(CanvasGroup));
            root.transform.SetParent(canvas, false);
            ReleaseUiKit.Stretch(root.GetComponent<RectTransform>());
            root.transform.SetAsLastSibling();
            _hudGroup = root.GetComponent<CanvasGroup>();

            BuildProgress(root.transform);
            BuildPauseButton(root.transform);

            _instruction = ReleaseUiKit.TextBlock(
                root.transform,
                "Instruction",
                "ЗАПОМНИ МАРШРУТ!",
                42,
                TextAnchor.MiddleCenter,
                new Vector2(0.12f, 0.790f),
                new Vector2(0.88f, 0.855f),
                ReleaseUiComponents.Text,
                FontStyle.Bold);
            ReleaseUiKit.AddTextShadow(_instruction, 0.50f, -3f);

            _countdown = ReleaseUiKit.TextBlock(
                root.transform,
                "Countdown",
                string.Empty,
                94,
                TextAnchor.MiddleCenter,
                new Vector2(0.35f, 0.710f),
                new Vector2(0.65f, 0.790f),
                ReleaseUiComponents.Cyan,
                FontStyle.Bold);
            ReleaseUiKit.AddTextShadow(_countdown, 0.64f, -5f);

            _hint = ReleaseUiKit.TextBlock(
                root.transform,
                "Hint",
                "Скоро он исчезнет…",
                21,
                TextAnchor.MiddleCenter,
                new Vector2(0.10f, 0.105f),
                new Vector2(0.90f, 0.155f),
                ReleaseUiComponents.Muted,
                FontStyle.Normal);

            BuildPauseOverlay(root.transform);
            SetHudVisible(false);
        }

        private void BuildProgress(Transform parent)
        {
            Transform trackRoot = ReleaseUiKit.Rect(parent, "ProgressTrack",
                new Vector2(0.30f, 0.925f), new Vector2(0.70f, 0.934f));
            Image track = trackRoot.gameObject.AddComponent<Image>();
            track.sprite = ReleaseUiKit.Rounded;
            track.type = Image.Type.Sliced;
            track.color = new Color(0.12f, 0.18f, 0.28f, 0.92f);
            track.raycastTarget = false;
            _progressRoot = trackRoot.gameObject;

            Transform fillRoot = ReleaseUiKit.Rect(trackRoot, "ProgressFill", Vector2.zero, Vector2.one);
            _progressFill = fillRoot.gameObject.AddComponent<Image>();
            _progressFill.sprite = ReleaseUiKit.Rounded;
            _progressFill.type = Image.Type.Sliced;
            _progressFill.color = ReleaseUiComponents.Blue;
            _progressFill.raycastTarget = false;
            _progressFill.rectTransform.anchorMax = new Vector2(0.34f, 1f);

            _phaseText = ReleaseUiKit.TextBlock(parent, "Phase", "1 / 3", 18,
                TextAnchor.MiddleCenter, new Vector2(0.39f, 0.885f), new Vector2(0.61f, 0.920f),
                ReleaseUiComponents.Muted, FontStyle.Bold);
        }

        private void BuildPauseButton(Transform parent)
        {
            _pauseButton = ReleaseUiComponents.SecondaryButton(
                parent,
                "PauseButton",
                string.Empty,
                new Vector2(0.845f, 0.875f),
                new Vector2(0.935f, 0.945f),
                PauseGameplay,
                24);

            Text label = _pauseButton.GetComponentInChildren<Text>(true);
            if (label != null) label.gameObject.SetActive(false);

            Image surface = _pauseButton.GetComponent<Image>();
            if (surface != null)
                surface.color = new Color(0.030f, 0.065f, 0.115f, 0.96f);

            AddPauseBar(_pauseButton.transform, "PauseLeft", 0.31f, 0.43f);
            AddPauseBar(_pauseButton.transform, "PauseRight", 0.57f, 0.69f);
            _pauseButton.gameObject.SetActive(false);
        }

        private static void AddPauseBar(Transform parent, string name, float minX, float maxX)
        {
            Transform bar = ReleaseUiKit.Rect(parent, name,
                new Vector2(minX, 0.28f), new Vector2(maxX, 0.72f));
            Image image = bar.gameObject.AddComponent<Image>();
            image.sprite = ReleaseUiKit.Rounded;
            image.type = Image.Type.Sliced;
            image.color = ReleaseUiComponents.Text;
            image.raycastTarget = false;
        }

        private void BuildPauseOverlay(Transform parent)
        {
            var overlay = new GameObject("PauseOverlay", typeof(RectTransform), typeof(Image));
            overlay.transform.SetParent(parent, false);
            ReleaseUiKit.Stretch(overlay.GetComponent<RectTransform>());
            Image dim = overlay.GetComponent<Image>();
            dim.color = new Color(0.002f, 0.008f, 0.025f, 0.88f);
            dim.raycastTarget = true;

            Image card = ReleaseUiComponents.GlassCard(
                overlay.transform,
                "PauseCard",
                new Vector2(0.14f, 0.335f),
                new Vector2(0.86f, 0.665f),
                ReleaseUiComponents.Cyan,
                true);

            ReleaseUiKit.TextBlock(card.transform, "PauseTitle", "ПАУЗА", 44,
                TextAnchor.MiddleCenter, new Vector2(0.10f, 0.68f), new Vector2(0.90f, 0.88f),
                ReleaseUiComponents.Text, FontStyle.Bold);

            ReleaseUiKit.TextBlock(card.transform, "PauseHint", "Маршрут подождёт.", 21,
                TextAnchor.MiddleCenter, new Vector2(0.10f, 0.53f), new Vector2(0.90f, 0.68f),
                ReleaseUiComponents.Muted);

            ReleaseUiComponents.PrimaryButton(card.transform, "ResumeButton", "ПРОДОЛЖИТЬ",
                new Vector2(0.08f, 0.27f), new Vector2(0.92f, 0.47f), ResumeFromPause, 27);

            ReleaseUiComponents.SecondaryButton(card.transform, "ExitButton", "ВЫЙТИ",
                new Vector2(0.08f, 0.07f), new Vector2(0.92f, 0.22f), ExitPausedRound, 24);

            _pauseOverlay = overlay;
            _pauseOverlay.SetActive(false);
        }

        private void PauseGameplay()
        {
            if (_bootstrap == null || !GameBootstrapRuntimeBridge.IsActiveRound(_bootstrap)) return;
            IsGameplayPaused = true;
            if (_pauseOverlay != null) _pauseOverlay.SetActive(true);
            if (_pauseButton != null) _pauseButton.gameObject.SetActive(false);
        }

        private void ResumeFromPause()
        {
            IsGameplayPaused = false;
            if (_pauseOverlay != null) _pauseOverlay.SetActive(false);
        }

        private void ExitPausedRound()
        {
            ResumeFromPause();
            if (_bootstrap != null) GameBootstrapRuntimeBridge.AbortToHome(_bootstrap);
        }

        private void ApplyGameplayBoardLayout(bool active)
        {
            if (!active || _playArea == null) return;

            ReleaseUiKit.SetAnchors(
                _playArea,
                new Vector2(0.080f, 0.205f),
                new Vector2(0.920f, 0.705f));
        }

        private void ApplyRestingBoardLayout(bool result)
        {
            if (_playArea == null) return;

            ReleaseUiKit.SetAnchors(
                _playArea,
                result ? new Vector2(0.07f, 0.330f) : new Vector2(0.07f, 0.245f),
                result ? new Vector2(0.93f, 0.665f) : new Vector2(0.93f, 0.760f));
        }

        private void Refresh()
        {
            string title = _legacyTitle.text ?? string.Empty;
            string compact = (_legacyStatus.text ?? string.Empty).Replace("\r", string.Empty).Trim();

            bool countdown = compact == "3" || compact == "2" || compact == "1";
            bool drawing =
                compact.IndexOf("ПОВТОРИ", StringComparison.OrdinalIgnoreCase) >= 0 ||
                compact.IndexOf("ВЕДИ", StringComparison.OrdinalIgnoreCase) >= 0 ||
                compact.IndexOf("НАЧНИ", StringComparison.OrdinalIgnoreCase) >= 0;

            if (!drawing)
            {
                _instruction.text = "ЗАПОМНИ МАРШРУТ!";
                _hint.text = "Скоро он исчезнет…";
                _countdown.text = countdown ? compact : string.Empty;
                _countdown.gameObject.SetActive(countdown);
                SetDrawingChrome(false, title);
                return;
            }

            _instruction.text = "ПОВТОРИ МАРШРУТ";
            _countdown.text = string.Empty;
            _countdown.gameObject.SetActive(false);

            if (compact.IndexOf("НАЧНИ", StringComparison.OrdinalIgnoreCase) >= 0)
                _hint.text = "Начни с голубой точки";
            else
                _hint.text = string.Empty;

            SetDrawingChrome(true, title);
        }

        private void SetDrawingChrome(bool drawing, string title)
        {
            bool hasProgress = drawing && UpdateProgress(title);
            if (_progressRoot != null) _progressRoot.SetActive(hasProgress);
            if (_phaseText != null) _phaseText.gameObject.SetActive(hasProgress);

            if (_pauseButton != null && !IsGameplayPaused)
                _pauseButton.gameObject.SetActive(drawing);
        }

        private bool UpdateProgress(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return false;

            int slash = title.IndexOf('/');
            if (slash <= 0 || slash >= title.Length - 1) return false;

            int left = slash - 1;
            while (left >= 0 && char.IsDigit(title[left])) left--;
            left++;

            int right = slash + 1;
            while (right < title.Length && char.IsDigit(title[right])) right++;

            if (!int.TryParse(title.Substring(left, slash - left), out int current)) return false;
            if (!int.TryParse(title.Substring(slash + 1, right - slash - 1), out int total)) return false;
            if (total <= 0) return false;

            current = Mathf.Clamp(current, 1, total);
            float ratio = Mathf.Clamp01(current / (float)total);
            _progressFill.rectTransform.anchorMax = new Vector2(Mathf.Max(0.08f, ratio), 1f);
            _phaseText.text = current + " / " + total;
            return true;
        }

        private void SetHudVisible(bool visible)
        {
            if (_hudGroup == null) return;
            _hudGroup.alpha = visible ? 1f : 0f;
            _hudGroup.interactable = visible;
            _hudGroup.blocksRaycasts = visible;
        }

        private void SetLegacyVisible(bool visible)
        {
            SetGroup(_legacyTitleGroup, visible);
            SetGroup(_legacyStatusGroup, visible);
        }

        private static void SetGroup(CanvasGroup group, bool visible)
        {
            if (group == null) return;
            group.alpha = visible ? 1f : 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        private static CanvasGroup EnsureCanvasGroup(GameObject go)
        {
            CanvasGroup group = go.GetComponent<CanvasGroup>();
            if (group == null) group = go.AddComponent<CanvasGroup>();
            return group;
        }

        private static Transform FindTransform(Transform root, string name)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
                if (string.Equals(all[i].name, name, StringComparison.Ordinal)) return all[i];
            return null;
        }

        private static T Find<T>(Transform root, string name) where T : Component
        {
            Transform transform = FindTransform(root, name);
            return transform == null ? null : transform.GetComponent<T>();
        }
    }
}
