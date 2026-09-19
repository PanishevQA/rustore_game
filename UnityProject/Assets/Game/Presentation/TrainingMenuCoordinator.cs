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
        private GameObject _backdrop;
        private GameObject _panel;
        private float _nextResolve;
        private bool _wired;
        private int _selectedDifficulty;
        private readonly Image[] _difficultyCards = new Image[3];

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

            if (!GameBootstrapRuntimeBridge.IsPlainHome(_bootstrap))
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

        private void Open() => OpenFromHome();

        public void OpenFromHome()
        {
            if (_bootstrap == null) ResolveBootstrap();
            if (_bootstrap == null || !GameBootstrapRuntimeBridge.IsPlainHome(_bootstrap)) return;
            _selectedDifficulty = 0;
            RefreshDifficultySelection();
            SetVisible(true);
        }

        private void SelectDifficulty(int difficulty)
        {
            _selectedDifficulty = Mathf.Clamp(difficulty, 0, 2);
            RefreshDifficultySelection();
        }

        private void StartSelected() => StartDifficulty(_selectedDifficulty);

        private void StartDifficulty(int difficulty)
        {
            if (_bootstrap == null || !GameBootstrapRuntimeBridge.IsPlainHome(_bootstrap)) return;
            if (!GameBootstrapRuntimeBridge.StartTrainingDifficulty(_bootstrap, difficulty)) return;
            SetVisible(false);
            _wired = false;
        }

        public void Close() => SetVisible(false);

        private void SetVisible(bool visible)
        {
            if (_backdrop != null) _backdrop.SetActive(visible);
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
            _backdrop = dim;
            ReleaseUiKit.Stretch(dim.GetComponent<RectTransform>());
            Image dimImage = dim.GetComponent<Image>();
            dimImage.color = new Color(0.005f, 0.010f, 0.028f, 0.86f);
            dimImage.raycastTarget = true;

            _panel = new GameObject("TrainingPanel", typeof(RectTransform), typeof(Image));
            _panel.transform.SetParent(_canvas.transform, false);
            ReleaseUiKit.SetAnchors(_panel.GetComponent<RectTransform>(), new Vector2(0.035f, 0.035f), new Vector2(0.965f, 0.965f));
            Image panelImage = _panel.GetComponent<Image>();
            panelImage.sprite = ReleaseUiKit.Rounded;
            panelImage.type = Image.Type.Sliced;
            panelImage.color = new Color(0.008f, 0.020f, 0.050f, 0.995f);
            ReleaseUiComponents.Backdrop(_panel.transform, "TrainingReleaseBackdrop");

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

            ReleaseUiComponents.SecondaryButton(_panel.transform, "Close", "‹",
                new Vector2(0.055f, 0.875f), new Vector2(0.16f, 0.935f), Close, 40);

            Transform trainingIconRoot = ReleaseUiKit.Rect(_panel.transform, "GeneratedTrainingIcon",
                new Vector2(0.43f, 0.855f), new Vector2(0.57f, 0.935f));
            Image trainingIcon = trainingIconRoot.gameObject.AddComponent<Image>();
            if (!GeneratedUiAssets.TryApply(trainingIcon, GeneratedUiAssets.TrainingIcon))
            {
                trainingIconRoot.gameObject.SetActive(false);
                ReleaseUiKit.TextBlock(_panel.transform, "Infinity", "∞", 70, TextAnchor.MiddleCenter,
                    new Vector2(0.37f, 0.855f), new Vector2(0.63f, 0.935f),
                    ReleaseUiComponents.Violet, FontStyle.Bold);
            }

            Text title = ReleaseUiKit.TextBlock(_panel.transform, "Title", "ТРЕНИРОВКА", 50,
                TextAnchor.MiddleCenter, new Vector2(0.16f, 0.795f), new Vector2(0.84f, 0.855f),
                ReleaseUiComponents.Text, FontStyle.Bold);
            ReleaseUiKit.AddTextShadow(title, 0.50f, -3f);

            ReleaseUiKit.TextBlock(_panel.transform, "Subtitle",
                "Бесконечные маршруты для твоего прогресса", 21,
                TextAnchor.MiddleCenter, new Vector2(0.13f, 0.755f), new Vector2(0.87f, 0.800f),
                ReleaseUiComponents.Muted);

            ReleaseUiComponents.StatTile(_panel.transform, "MemoryBenefit", "|||", "ПАМЯТЬ", "Развивай\nпамять",
                new Vector2(0.07f, 0.665f), new Vector2(0.34f, 0.745f), ReleaseUiComponents.Violet);
            ReleaseUiComponents.StatTile(_panel.transform, "FocusBenefit", "◆", "ФОКУС", "Тренируй\nвнимание",
                new Vector2(0.365f, 0.665f), new Vector2(0.635f, 0.745f), ReleaseUiComponents.Cyan);
            ReleaseUiComponents.StatTile(_panel.transform, "AccuracyBenefit", "◎", "ТОЧНОСТЬ", "Становись\nточнее",
                new Vector2(0.66f, 0.665f), new Vector2(0.93f, 0.745f), ReleaseUiComponents.Blue);

            ReleaseUiComponents.SectionHeader(_panel.transform, "ВЫБЕРИ СЛОЖНОСТЬ",
                new Vector2(0.07f, 0.615f), new Vector2(0.62f, 0.650f));

            CreateDifficultyCard(0, "EasyCard", "ЛЁГКАЯ", "ПОКАЗ 3.5 С", "Короткие и простые маршруты",
                ReleaseUiComponents.Success, new Vector2(0.07f, 0.505f), new Vector2(0.93f, 0.605f));

            CreateDifficultyCard(1, "MediumCard", "СРЕДНЯЯ", "ПОКАЗ 3.0 С", "Больше поворотов и меньше времени",
                ReleaseUiComponents.Cyan, new Vector2(0.07f, 0.390f), new Vector2(0.93f, 0.490f));

            CreateDifficultyCard(2, "HardCard", "СЛОЖНАЯ", "ПОКАЗ 2.5 С", "Для настоящих мастеров памяти",
                ReleaseUiComponents.Violet, new Vector2(0.07f, 0.275f), new Vector2(0.93f, 0.375f));

            Image progress = ReleaseUiComponents.GlassCard(_panel.transform, "TrainingProgress",
                new Vector2(0.07f, 0.185f), new Vector2(0.93f, 0.255f), ReleaseUiComponents.Blue, false);
            ReleaseUiKit.TextBlock(progress.transform, "ProgressCopy", "ТВОЙ ПРОГРЕСС  •  ТОЧНОСТЬ ВАЖНЕЕ СКОРОСТИ", 17,
                TextAnchor.MiddleCenter, new Vector2(0.05f, 0.10f), new Vector2(0.95f, 0.90f),
                ReleaseUiComponents.Text, FontStyle.Bold);

            ReleaseUiComponents.PrimaryButton(_panel.transform, "StartTraining", "▶  НАЧАТЬ ТРЕНИРОВКУ",
                new Vector2(0.07f, 0.085f), new Vector2(0.93f, 0.165f), StartSelected, 28);

            RefreshDifficultySelection();
        }

        private void CreateDifficultyCard(
            int difficulty,
            string name,
            string title,
            string timing,
            string description,
            Color accent,
            Vector2 min,
            Vector2 max)
        {
            Image card = ReleaseUiComponents.GlassCard(_panel.transform, name, min, max, accent, false);
            _difficultyCards[difficulty] = card;

            Button button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = card;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = card.color;
            colors.highlightedColor = ReleaseUiKit.Lighten(card.color, 0.04f);
            colors.pressedColor = ReleaseUiKit.Darken(card.color, 0.05f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(() => SelectDifficulty(difficulty));

            ReleaseUiKit.TextBlock(card.transform, "Glyph", difficulty == 0 ? "◆" : difficulty == 1 ? "◆◆" : "◆◆◆", 24,
                TextAnchor.MiddleCenter, new Vector2(0.04f, 0.18f), new Vector2(0.19f, 0.82f),
                accent, FontStyle.Bold);

            ReleaseUiKit.TextBlock(card.transform, "Label", title, 27,
                TextAnchor.MiddleLeft, new Vector2(0.21f, 0.48f), new Vector2(0.62f, 0.88f),
                ReleaseUiComponents.Text, FontStyle.Bold);

            ReleaseUiKit.TextBlock(card.transform, "Description", description, 17,
                TextAnchor.MiddleLeft, new Vector2(0.21f, 0.10f), new Vector2(0.75f, 0.50f),
                ReleaseUiComponents.Muted);

            ReleaseUiKit.TextBlock(card.transform, "Timing", timing, 16,
                TextAnchor.MiddleRight, new Vector2(0.72f, 0.18f), new Vector2(0.93f, 0.82f),
                accent, FontStyle.Bold);
        }

        private void RefreshDifficultySelection()
        {
            for (int i = 0; i < _difficultyCards.Length; i++)
            {
                Image card = _difficultyCards[i];
                if (card == null) continue;
                Outline outline = card.GetComponent<Outline>();
                if (outline == null) continue;
                bool selected = i == _selectedDifficulty;
                Color accent = i == 0 ? ReleaseUiComponents.Success :
                               i == 1 ? ReleaseUiComponents.Cyan :
                                        ReleaseUiComponents.Violet;
                outline.effectColor = new Color(accent.r, accent.g, accent.b, selected ? 0.72f : 0.20f);
                outline.effectDistance = selected ? new Vector2(4f, -4f) : new Vector2(2f, -2f);
                card.color = selected
                    ? new Color(0.035f, 0.110f, 0.145f, 0.995f)
                    : new Color(0.020f, 0.055f, 0.100f, 0.955f);
            }
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
