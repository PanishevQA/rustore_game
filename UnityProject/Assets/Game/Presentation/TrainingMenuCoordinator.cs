using System;
using UnityEngine;
using UnityEngine.UI;

namespace DontGetSidetracked.Presentation
{
    /// <summary>
    /// Home-only training selector. Reuses GameBootstrap training loop and only chooses
    /// the next difficulty before the round starts.
    /// </summary>
    [DefaultExecutionOrder(12000)]
    public sealed class TrainingMenuCoordinator : MonoBehaviour
    {
        private GameBootstrap _bootstrap;
        private Button _trainingButton;
        private GameObject _canvas;
        private GameObject _panel;
        private float _nextResolve;
        private bool _wired;

        public bool IsOpen => _panel != null && _panel.activeSelf;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (FindFirstObjectByType<TrainingMenuCoordinator>() != null) return;
            var root = new GameObject("TrainingMenuCoordinator");
            DontDestroyOnLoad(root);
            root.AddComponent<TrainingMenuCoordinator>();
        }

        private void Awake()
        {
            BuildUi();
            SetVisible(false);
        }

        private void LateUpdate()
        {
            if (_bootstrap == null && Time.unscaledTime >= _nextResolve)
            {
                _nextResolve = Time.unscaledTime + 0.25f;
                ResolveBootstrap();
            }
            if (_bootstrap == null) return;

            if (!GameBootstrapRuntimeBridge.IsHome(_bootstrap))
            {
                _wired = false;
                if (IsOpen) SetVisible(false);
                return;
            }

            ResolveTrainingButton();
            if (_trainingButton == null || _wired) return;
            Text label = _trainingButton.GetComponentInChildren<Text>(true);
            if (label == null || label.text.IndexOf("ТРЕНИРОВКА", StringComparison.OrdinalIgnoreCase) < 0) return;

            _trainingButton.onClick.RemoveAllListeners();
            _trainingButton.onClick.AddListener(Open);
            _wired = true;
        }

        private void ResolveBootstrap()
        {
            _bootstrap = FindFirstObjectByType<GameBootstrap>();
        }

        private void ResolveTrainingButton()
        {
            if (_trainingButton != null) return;
            GameObject gameCanvas = GameObject.Find("GameCanvas");
            if (gameCanvas == null) return;
            Button[] buttons = gameCanvas.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Text label = buttons[i].GetComponentInChildren<Text>(true);
                if (label != null && label.text.IndexOf("ТРЕНИРОВКА", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _trainingButton = buttons[i];
                    return;
                }
            }
        }

        private void Open() => SetVisible(true);

        private void StartEasy() => StartDifficulty(0);
        private void StartMedium() => StartDifficulty(1);
        private void StartHard() => StartDifficulty(2);

        private void StartRandom()
        {
            int difficulty = UnityEngine.Random.Range(0, 3);
            StartDifficulty(difficulty);
        }

        private void StartDifficulty(int difficulty)
        {
            if (_bootstrap == null || !GameBootstrapRuntimeBridge.IsHome(_bootstrap)) return;
            if (!GameBootstrapRuntimeBridge.StartTrainingDifficulty(_bootstrap, difficulty)) return;
            SetVisible(false);
            _wired = false;
        }

        public void Close() => SetVisible(false);

        private void SetVisible(bool visible)
        {
            if (_panel != null) _panel.SetActive(visible);
        }

        private void BuildUi()
        {
            _canvas = new GameObject("TrainingSelectCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvas.transform.SetParent(transform, false);
            Canvas canvas = _canvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 75;
            CanvasScaler scaler = _canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            var dim = new GameObject("TrainingBackdrop", typeof(RectTransform), typeof(Image));
            dim.transform.SetParent(_canvas.transform, false);
            ReleaseUiKit.Stretch(dim.GetComponent<RectTransform>());
            Image dimImage = dim.GetComponent<Image>();
            dimImage.color = new Color(0.005f, 0.010f, 0.028f, 0.86f);
            dimImage.raycastTarget = true;

            _panel = new GameObject("TrainingPanel", typeof(RectTransform), typeof(Image));
            _panel.transform.SetParent(_canvas.transform, false);
            ReleaseUiKit.SetAnchors(_panel.GetComponent<RectTransform>(), new Vector2(0.055f, 0.105f), new Vector2(0.945f, 0.895f));
            Image panelImage = _panel.GetComponent<Image>();
            panelImage.sprite = ReleaseUiKit.Rounded;
            panelImage.type = Image.Type.Sliced;
            panelImage.color = ReleaseUiKit.Surface;

            Outline outline = _panel.AddComponent<Outline>();
            outline.effectColor = new Color(ReleaseUiKit.Cyan.r, ReleaseUiKit.Cyan.g, ReleaseUiKit.Cyan.b, 0.14f);
            outline.effectDistance = new Vector2(2f, -2f);

            Shadow shadow = _panel.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.52f);
            shadow.effectDistance = new Vector2(0f, -12f);
            _panel.AddComponent<ReleasePanelMotion>();

            var releaseVisual = new GameObject("ReleaseVisual", typeof(RectTransform));
            releaseVisual.transform.SetParent(_panel.transform, false);
            ReleaseUiKit.Stretch(releaseVisual.GetComponent<RectTransform>());

            Text kicker = ReleaseUiKit.TextBlock(_panel.transform, "Kicker", "СВОБОДНЫЙ РЕЖИМ", 20,
                TextAnchor.MiddleLeft, new Vector2(0.075f, 0.875f), new Vector2(0.60f, 0.925f),
                ReleaseUiKit.Cyan, FontStyle.Bold);

            Text title = ReleaseUiKit.TextBlock(_panel.transform, "Title", "ТРЕНИРОВКА", 58,
                TextAnchor.MiddleLeft, new Vector2(0.075f, 0.790f), new Vector2(0.78f, 0.875f),
                ReleaseUiKit.Text, FontStyle.Bold);
            ReleaseUiKit.AddTextShadow(title, 0.45f, -3f);

            Text subtitle = ReleaseUiKit.TextBlock(_panel.transform, "Subtitle",
                "Выбери темп. Здесь можно набить руку — результат не влияет на Daily и кампанию.", 25,
                TextAnchor.UpperLeft, new Vector2(0.075f, 0.705f), new Vector2(0.88f, 0.790f),
                ReleaseUiKit.Muted);

            CreateDifficultyCard(
                "EasyCard", "ЛЁГКАЯ", "3.5 СЕК", "Мягкие углы\nи спокойный темп",
                ReleaseUiKit.Green, new Vector2(0.075f, 0.445f), new Vector2(0.485f, 0.680f), StartEasy);

            CreateDifficultyCard(
                "MediumCard", "СРЕДНЯЯ", "3.0 СЕК", "Больше поворотов\nи меньше времени",
                ReleaseUiKit.Cyan, new Vector2(0.515f, 0.445f), new Vector2(0.925f, 0.680f), StartMedium);

            CreateDifficultyCard(
                "HardCard", "СЛОЖНАЯ", "2.5 СЕК", "Плотная траектория\nи сложный ритм",
                ReleaseUiKit.Danger, new Vector2(0.075f, 0.185f), new Vector2(0.485f, 0.420f), StartHard);

            CreateDifficultyCard(
                "RandomCard", "СЛУЧАЙНАЯ", "∞", "Каждый раунд\nновая сложность",
                new Color(0.72f, 0.54f, 1f, 1f), new Vector2(0.515f, 0.185f), new Vector2(0.925f, 0.420f), StartRandom);

            Button close = ReleaseUiKit.Button(_panel.transform, "Close", "ЗАКРЫТЬ",
                new Vector2(0.315f, 0.055f), new Vector2(0.685f, 0.125f),
                new Color(0.09f, 0.12f, 0.19f, 0.94f), ReleaseUiKit.Muted, 23, Close);
            close.gameObject.name = "ЗАКРЫТЬ";
        }

        private void CreateDifficultyCard(
            string name,
            string title,
            string timing,
            string description,
            Color accent,
            Vector2 min,
            Vector2 max,
            UnityEngine.Events.UnityAction action)
        {
            Image card = ReleaseUiKit.Panel(_panel.transform, name, min, max, ReleaseUiKit.SurfaceRaised, accent);
            Button button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = card;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = ReleaseUiKit.SurfaceRaised;
            colors.highlightedColor = ReleaseUiKit.Lighten(ReleaseUiKit.SurfaceRaised, 0.05f);
            colors.pressedColor = ReleaseUiKit.Darken(ReleaseUiKit.SurfaceRaised, 0.07f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(action);

            ReleaseUiKit.Dot(card.transform, "Icon", accent,
                new Vector2(0.075f, 0.68f), new Vector2(0.19f, 0.89f));

            Text timingText = ReleaseUiKit.TextBlock(card.transform, "Timing", timing, 18,
                TextAnchor.MiddleRight, new Vector2(0.57f, 0.70f), new Vector2(0.90f, 0.88f),
                accent, FontStyle.Bold);

            Text heading = ReleaseUiKit.TextBlock(card.transform, "Label", title, 31,
                TextAnchor.MiddleLeft, new Vector2(0.075f, 0.40f), new Vector2(0.90f, 0.68f),
                ReleaseUiKit.Text, FontStyle.Bold);
            heading.raycastTarget = false;

            Text body = ReleaseUiKit.TextBlock(card.transform, "Description", description, 20,
                TextAnchor.UpperLeft, new Vector2(0.075f, 0.10f), new Vector2(0.90f, 0.41f),
                ReleaseUiKit.Muted);
            body.raycastTarget = false;
        }

        private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
