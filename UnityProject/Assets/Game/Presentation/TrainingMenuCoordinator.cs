using System;
using System.Collections.Generic;
using DontGetSidetracked.Core;
using DontGetSidetracked.Gameplay;
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
        private readonly Text[] _difficultyTimings = new Text[3];
        private readonly Text[] _difficultyStates = new Text[3];
        private readonly Image[] _difficultyChecks = new Image[3];
        private readonly Text[] _difficultyLabels = new Text[3];
        private Text _selectionSummary;
        private Text _rewardedLabel;
        private Button _rewardedButton;

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
            dimImage.color = new Color(0.001f, 0.005f, 0.016f, 0.93f);
            dimImage.raycastTarget = true;

            _panel = new GameObject("TrainingPanel", typeof(RectTransform));
            _panel.transform.SetParent(_canvas.transform, false);
            ReleaseUiKit.SetAnchors(_panel.GetComponent<RectTransform>(), new Vector2(0.035f, 0.025f), new Vector2(0.965f, 0.975f));
            ReleaseUiComponents.Backdrop(_panel.transform, "TrainingReleaseBackdrop");
            _panel.AddComponent<ReleasePanelMotion>();

            Image backSurface = ReleaseUiKit.Panel(
                _panel.transform,
                "Close",
                new Vector2(0.045f, 0.900f),
                new Vector2(0.125f, 0.952f),
                new Color(0.018f, 0.055f, 0.105f, 0.98f),
                ReleaseUiComponents.Cyan,
                false);
            Button backButton = backSurface.gameObject.AddComponent<Button>();
            backButton.targetGraphic = backSurface;
            backButton.onClick.AddListener(Close);
            BuildBackChevron(backSurface.transform);

            Text title = ReleaseUiKit.TextBlock(
                _panel.transform,
                "Title",
                "ТРЕНИРОВКА",
                42,
                TextAnchor.MiddleCenter,
                new Vector2(0.22f, 0.895f),
                new Vector2(0.78f, 0.955f),
                ReleaseUiComponents.Text,
                FontStyle.Bold);
            ReleaseUiKit.AddTextShadow(title, 0.50f, -3f);

            ReleaseUiKit.TextBlock(
                _panel.transform,
                "Subtitle",
                "Без ограничений",
                20,
                TextAnchor.MiddleCenter,
                new Vector2(0.22f, 0.862f),
                new Vector2(0.78f, 0.900f),
                ReleaseUiComponents.Muted);

            Image preview = ReleaseUiKit.Panel(
                _panel.transform,
                "TrainingRoutePreview",
                new Vector2(0.070f, 0.565f),
                new Vector2(0.930f, 0.840f),
                new Color(0.006f, 0.024f, 0.060f, 0.99f),
                ReleaseUiComponents.Cyan,
                true);
            if (ReleaseSkinAssets.RoutePanel != null)
            {
                preview.sprite = ReleaseSkinAssets.RoutePanel;
                preview.type = Image.Type.Sliced;
                preview.color = Color.white;
                Outline po = preview.GetComponent<Outline>();
                if (po != null) po.enabled = false;
            }
            if (ReleaseSkinAssets.RoutePanel == null)
                BuildPreviewRoute(preview.transform);

            ReleaseUiKit.TextBlock(
                _panel.transform,
                "DifficultyCaption",
                "СЛОЖНОСТЬ",
                18,
                TextAnchor.MiddleLeft,
                new Vector2(0.070f, 0.505f),
                new Vector2(0.360f, 0.548f),
                ReleaseUiComponents.Muted,
                FontStyle.Bold);

            CreateDifficultyChip(0, "EasyChip", "ЛЁГКАЯ",
                new Vector2(0.070f, 0.430f), new Vector2(0.345f, 0.500f));
            CreateDifficultyChip(1, "MediumChip", "СРЕДНЯЯ",
                new Vector2(0.365f, 0.430f), new Vector2(0.640f, 0.500f));
            CreateDifficultyChip(2, "HardChip", "СЛОЖНАЯ",
                new Vector2(0.660f, 0.430f), new Vector2(0.930f, 0.500f));

            _selectionSummary = null;

            ReleaseUiComponents.PrimaryButton(
                _panel.transform,
                "StartTraining",
                "ИГРАТЬ",
                new Vector2(0.070f, 0.285f),
                new Vector2(0.930f, 0.370f),
                StartSelected,
                31);

            Image rewarded = ReleaseUiComponents.GlassCard(
                _panel.transform,
                "TrainingRewarded",
                new Vector2(0.070f, 0.095f),
                new Vector2(0.930f, 0.220f),
                ReleaseUiComponents.Violet,
                true);
            if (ReleaseSkinAssets.NavViolet != null)
            {
                rewarded.sprite = ReleaseSkinAssets.NavViolet;
                rewarded.type = Image.Type.Sliced;
                rewarded.color = Color.white;
            }

            Image adIcon = ReleaseUiComponents.Icon(
                rewarded.transform,
                "RewardedIcon",
                GeneratedUiAssets.EyeIcon,
                new Vector2(0.045f, 0.18f),
                new Vector2(0.170f, 0.82f));
            adIcon.color = Color.white;

            _rewardedLabel = ReleaseUiKit.TextBlock(
                rewarded.transform,
                "RewardedCopy",
                "СМОТРИ РЕКЛАМУ →\nЕЩЁ ОДИН ПРОСМОТР МАРШРУТА",
                20,
                TextAnchor.MiddleLeft,
                new Vector2(0.205f, 0.12f),
                new Vector2(0.82f, 0.88f),
                ReleaseUiComponents.Text,
                FontStyle.Bold);
            ReleaseUiKit.TextBlock(
                rewarded.transform,
                "RewardedArrow",
                "›",
                38,
                TextAnchor.MiddleCenter,
                new Vector2(0.855f, 0.10f),
                new Vector2(0.955f, 0.90f),
                new Color(0.86f, 0.55f, 1f, 1f),
                FontStyle.Bold);

            _rewardedButton = rewarded.gameObject.AddComponent<Button>();
            _rewardedButton.targetGraphic = rewarded;
            _rewardedButton.onClick.AddListener(ClaimTrainingReward);

            RefreshDifficultySelection();
        }

        private void CreateDifficultyChip(int difficulty, string name, string title, Vector2 min, Vector2 max)
        {
            Color accent = difficulty == 0
                ? ReleaseUiComponents.Cyan
                : difficulty == 1
                    ? ReleaseUiComponents.Blue
                    : ReleaseUiComponents.Violet;

            Image chip = ReleaseUiComponents.GlassCard(_panel.transform, name, min, max, accent, false);
            _difficultyCards[difficulty] = chip;

            Button button = chip.gameObject.AddComponent<Button>();
            button.targetGraphic = chip;
            button.onClick.AddListener(() => SelectDifficulty(difficulty));

            Text label = ReleaseUiKit.TextBlock(
                chip.transform,
                "Label",
                title,
                20,
                TextAnchor.MiddleCenter,
                new Vector2(0.06f, 0.20f),
                new Vector2(0.94f, 0.80f),
                ReleaseUiComponents.Text,
                FontStyle.Bold);
            label.raycastTarget = false;
            _difficultyLabels[difficulty] = label;

            _difficultyTimings[difficulty] = ReleaseUiKit.TextBlock(
                chip.transform,
                "Timing",
                string.Empty,
                12,
                TextAnchor.UpperCenter,
                new Vector2(0.05f, 0.00f),
                new Vector2(0.95f, 0.24f),
                Color.clear);
            _difficultyStates[difficulty] = ReleaseUiKit.TextBlock(
                chip.transform,
                "SelectionState",
                string.Empty,
                12,
                TextAnchor.MiddleCenter,
                new Vector2(0.05f, 0.00f),
                new Vector2(0.95f, 0.20f),
                Color.clear);
            _difficultyChecks[difficulty] = null;
        }

        private static void BuildBackChevron(Transform parent)
        {
            Transform upper = ReleaseUiKit.Rect(parent, "ChevronUpper",
                new Vector2(0.38f, 0.46f), new Vector2(0.66f, 0.56f));
            Image upperImage = upper.gameObject.AddComponent<Image>();
            upperImage.sprite = ReleaseUiKit.Rounded;
            upperImage.type = Image.Type.Sliced;
            upperImage.color = ReleaseUiComponents.Cyan;
            upperImage.raycastTarget = false;
            upper.GetComponent<RectTransform>().localRotation = Quaternion.Euler(0f, 0f, -42f);

            Transform lower = ReleaseUiKit.Rect(parent, "ChevronLower",
                new Vector2(0.38f, 0.36f), new Vector2(0.66f, 0.46f));
            Image lowerImage = lower.gameObject.AddComponent<Image>();
            lowerImage.sprite = ReleaseUiKit.Rounded;
            lowerImage.type = Image.Type.Sliced;
            lowerImage.color = ReleaseUiComponents.Cyan;
            lowerImage.raycastTarget = false;
            lower.GetComponent<RectTransform>().localRotation = Quaternion.Euler(0f, 0f, 42f);
        }

        private static void BuildPreviewRoute(Transform parent)
        {
            Transform host = ReleaseUiKit.Rect(parent, "PreviewRouteHost",
                new Vector2(0.07f, 0.09f), new Vector2(0.93f, 0.91f));

            var points = new List<FixedPoint2>
            {
                FixedPoint2.FromNormalized(0.07, 0.22),
                FixedPoint2.FromNormalized(0.16, 0.48),
                FixedPoint2.FromNormalized(0.28, 0.67),
                FixedPoint2.FromNormalized(0.42, 0.70),
                FixedPoint2.FromNormalized(0.55, 0.58),
                FixedPoint2.FromNormalized(0.66, 0.42),
                FixedPoint2.FromNormalized(0.77, 0.45),
                FixedPoint2.FromNormalized(0.88, 0.70),
                FixedPoint2.FromNormalized(0.95, 0.82)
            };

            var glowGo = new GameObject("PreviewGlow", typeof(RectTransform), typeof(RouteGraphic));
            glowGo.transform.SetParent(host, false);
            ReleaseUiKit.Stretch(glowGo.GetComponent<RectTransform>());
            RouteGraphic glow = glowGo.GetComponent<RouteGraphic>();
            glow.color = new Color(0.05f, 0.70f, 1f, 0.28f);
            glow.Thickness = 30f;
            glow.raycastTarget = false;
            glow.SetPoints(points);

            var lineGo = new GameObject("PreviewLine", typeof(RectTransform), typeof(RouteGraphic));
            lineGo.transform.SetParent(host, false);
            ReleaseUiKit.Stretch(lineGo.GetComponent<RectTransform>());
            RouteGraphic line = lineGo.GetComponent<RouteGraphic>();
            line.color = new Color(0.16f, 0.82f, 1f, 1f);
            line.Thickness = 10f;
            line.raycastTarget = false;
            line.SetPoints(points);

            Image start = ReleaseUiKit.Dot(host, "PreviewStart", Color.white,
                new Vector2(0.015f, 0.145f), new Vector2(0.105f, 0.285f));
            Outline startGlow = start.gameObject.AddComponent<Outline>();
            startGlow.effectColor = new Color(0.08f, 0.72f, 1f, 0.72f);
            startGlow.effectDistance = new Vector2(3f, -3f);

            Image end = ReleaseUiKit.Dot(host, "PreviewEnd", ReleaseUiComponents.Cyan,
                new Vector2(0.895f, 0.745f), new Vector2(0.985f, 0.885f));
            Image inner = ReleaseUiKit.Dot(end.transform, "PreviewEndInner",
                new Color(0.008f, 0.025f, 0.060f, 1f),
                new Vector2(0.27f, 0.27f), new Vector2(0.73f, 0.73f));
            inner.raycastTarget = false;
        }

        private async void ClaimTrainingReward()
        {
            if (_rewardedButton == null || !_rewardedButton.interactable) return;

            var ads = AdRuntimeCoordinator.Ads;
            if (ads == null || !ads.IsRewardedReady)
            {
                if (_rewardedLabel != null) _rewardedLabel.text = "РЕКЛАМА ПОКА НЕДОСТУПНА";
                return;
            }

            _rewardedButton.interactable = false;
            try
            {
                bool rewarded = await ads.ShowRewardedAsync(DontGetSidetracked.Services.RewardPlacement.FreeHint);
                if (rewarded)
                {
                    var repository = new JsonFileSaveRepository();
                    SaveData save = repository.Load();
                    save.Hints++;
                    repository.Save(save);
                    if (_rewardedLabel != null) _rewardedLabel.text = "ДОП. ПРОСМОТР ДОСТУПЕН";
                }
            }
            catch (Exception error)
            {
                Debug.LogWarning("Training rewarded unavailable: " + error.Message);
                if (_rewardedLabel != null) _rewardedLabel.text = "РЕКЛАМА ПОКА НЕДОСТУПНА";
            }
            finally
            {
                _rewardedButton.interactable = true;
            }
        }

        private void RefreshDifficultySelection()
        {
            for (int i = 0; i < _difficultyCards.Length; i++)
            {
                Image card = _difficultyCards[i];
                if (card == null) continue;

                bool selected = i == _selectedDifficulty;
                Color accent = i == 0
                    ? ReleaseUiComponents.Cyan
                    : i == 1
                        ? ReleaseUiComponents.Blue
                        : ReleaseUiComponents.Violet;

                if (selected && ReleaseSkinAssets.PrimaryButton != null)
                {
                    card.sprite = ReleaseSkinAssets.PrimaryButton;
                    card.type = Image.Type.Simple;
                    card.color = Color.white;
                }
                else if (!selected && ReleaseSkinAssets.SecondaryButton != null)
                {
                    card.sprite = ReleaseSkinAssets.SecondaryButton;
                    card.type = Image.Type.Sliced;
                    card.color = new Color(0.68f, 0.75f, 0.86f, 0.92f);
                }
                else
                {
                    card.color = selected ? Color.white : new Color(0.30f, 0.38f, 0.50f, 0.88f);
                }

                if (_difficultyLabels[i] != null)
                    _difficultyLabels[i].color = selected
                        ? new Color(0.010f, 0.050f, 0.100f, 1f)
                        : ReleaseUiComponents.Text;

                Shadow shadow = card.GetComponent<Shadow>();
                if (shadow != null)
                {
                    shadow.effectColor = selected
                        ? new Color(accent.r, accent.g, accent.b, 0.38f)
                        : new Color(0f, 0f, 0f, 0.20f);
                    shadow.effectDistance = selected ? new Vector2(0f, -5f) : new Vector2(0f, -2f);
                }

                if (_difficultyTimings[i] != null)
                    _difficultyTimings[i].text = string.Empty;
                if (_difficultyStates[i] != null)
                    _difficultyStates[i].text = string.Empty;
                if (_difficultyChecks[i] != null)
                    _difficultyChecks[i].gameObject.SetActive(false);
            }

            string selectedName = _selectedDifficulty == 0
                ? "ЛЁГКАЯ"
                : _selectedDifficulty == 1
                    ? "СРЕДНЯЯ"
                    : "СЛОЖНАЯ";

            float seconds = RouteRuntimeTuning.GetDisplayTimeMs((RouteDifficulty)_selectedDifficulty) / 1000f;
            if (_selectionSummary != null)
                _selectionSummary.text = selectedName + "  •  ПОКАЗ " + seconds.ToString("0.0") + " С";
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
