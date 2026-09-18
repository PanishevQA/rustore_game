using System;
using System.Collections.Generic;
using DontGetSidetracked.Core;
using DontGetSidetracked.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace DontGetSidetracked.Presentation
{
    /// <summary>
    /// High-fidelity Home composition built on top of the existing runtime-created MVP UI.
    /// It proxies the proven navigation buttons instead of owning gameplay/navigation state.
    /// This keeps the visual layer replaceable while the gameplay loop stays untouched.
    /// </summary>
    [DefaultExecutionOrder(15000)]
    public sealed class HomeDashboardCoordinator : MonoBehaviour
    {
        private static readonly Color Surface = new Color(0.035f, 0.050f, 0.095f, 0.98f);
        private static readonly Color SurfaceRaised = new Color(0.055f, 0.078f, 0.135f, 0.99f);
        private static readonly Color TextPrimary = new Color(0.96f, 0.98f, 1f, 1f);
        private static readonly Color TextMuted = new Color(0.59f, 0.69f, 0.82f, 1f);
        private static readonly Color Cyan = new Color(0.20f, 0.80f, 1f, 1f);
        private static readonly Color CyanBright = new Color(0.22f, 0.91f, 1f, 1f);
        private static readonly Color Violet = new Color(0.36f, 0.22f, 1f, 1f);
        private static readonly Color Green = new Color(0.27f, 0.96f, 0.62f, 1f);
        private static readonly Color Gold = new Color(1f, 0.76f, 0.24f, 1f);

        private static Sprite _rounded;
        private static Sprite _circle;

        private GameBootstrap _bootstrap;
        private JsonFileSaveRepository _repository;

        private CanvasGroup _dashboardGroup;
        private CanvasGroup _titleGroup;
        private CanvasGroup _statusGroup;
        private CanvasGroup _playGroup;
        private CanvasGroup _primaryGroup;
        private CanvasGroup _secondaryGroup;
        private CanvasGroup _shareGroup;
        private CanvasGroup _legacyMetaGroup;

        private Button _legacyPrimary;
        private Button _legacySecondary;
        private Button _legacyShare;
        private Button _legacyStats;
        private Button _legacyStore;
        private Text _legacyTitle;

        private Text _streakValue;
        private Text _starsValue;
        private Text _coinsValue;
        private Text _dailyMeta;
        private Text _dailyBest;
        private Text _campaignMeta;
        private Image _campaignProgressFill;

        private bool _dashboardBuilt;
        private bool _homeVisible;
        private float _nextResolve;
        private float _nextDataRefresh;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (FindFirstObjectByType<HomeDashboardCoordinator>() != null) return;
            var root = new GameObject("HomeDashboardCoordinator");
            DontDestroyOnLoad(root);
            root.AddComponent<HomeDashboardCoordinator>();
        }

        private void Awake()
        {
            _repository = new JsonFileSaveRepository();
            EnsureAssets();
        }

        private void Update()
        {
            if ((!_dashboardBuilt || _bootstrap == null || _legacyStats == null || _legacyStore == null) &&
                Time.unscaledTime >= _nextResolve)
            {
                _nextResolve = Time.unscaledTime + 0.25f;
                ResolveAndBuild();
            }

            if (!_dashboardBuilt || _bootstrap == null || _legacyTitle == null) return;

            bool show = GameBootstrapRuntimeBridge.IsHome(_bootstrap) &&
                        string.Equals(_legacyTitle.text, "НЕ СБЕЙСЯ!", StringComparison.Ordinal);

            if (show != _homeVisible)
            {
                _homeVisible = show;
                SetDashboardVisible(show);
            }

            if (show && Time.unscaledTime >= _nextDataRefresh)
            {
                _nextDataRefresh = Time.unscaledTime + 0.40f;
                RefreshData();
            }
        }

        private void ResolveAndBuild()
        {
            if (_bootstrap == null) _bootstrap = FindFirstObjectByType<GameBootstrap>();

            GameObject gameCanvas = GameObject.Find("GameCanvas");
            if (gameCanvas == null) return;

            _legacyTitle = Find<Text>(gameCanvas.transform, "Title");
            Text legacyStatus = Find<Text>(gameCanvas.transform, "Status");
            RectTransform legacyPlay = Find<RectTransform>(gameCanvas.transform, "PlayArea");
            _legacyPrimary = Find<Button>(gameCanvas.transform, "Primary");
            _legacySecondary = Find<Button>(gameCanvas.transform, "Secondary");
            _legacyShare = Find<Button>(gameCanvas.transform, "Share");
            if (_legacyTitle == null || legacyStatus == null || legacyPlay == null ||
                _legacyPrimary == null || _legacySecondary == null || _legacyShare == null)
                return;

            ResolveMetaButtons();
            if (_legacyStats == null || _legacyStore == null) return;

            if (!_dashboardBuilt)
            {
                _titleGroup = EnsureCanvasGroup(_legacyTitle.gameObject);
                _statusGroup = EnsureCanvasGroup(legacyStatus.gameObject);
                _playGroup = EnsureCanvasGroup(legacyPlay.gameObject);
                _primaryGroup = EnsureCanvasGroup(_legacyPrimary.gameObject);
                _secondaryGroup = EnsureCanvasGroup(_legacySecondary.gameObject);
                _shareGroup = EnsureCanvasGroup(_legacyShare.gameObject);

                BuildDashboard(gameCanvas.transform);
                _dashboardBuilt = true;
                _homeVisible = false;
                SetDashboardVisible(false);
            }

            HideLegacyMetaHomeButtons();
        }

        private void ResolveMetaButtons()
        {
            GameObject meta = GameObject.Find("MetaCanvas");
            if (meta == null) return;

            Transform homeButtons = FindTransform(meta.transform, "HomeMetaButtons");
            if (homeButtons != null)
            {
                _legacyMetaGroup = EnsureCanvasGroup(homeButtons.gameObject);
                _legacyMetaGroup.alpha = 0f;
                _legacyMetaGroup.blocksRaycasts = false;
                _legacyMetaGroup.interactable = false;
            }

            Button[] buttons = meta.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Text label = buttons[i].GetComponentInChildren<Text>(true);
                if (label == null) continue;
                if (string.Equals(label.text, "СТАТИСТИКА", StringComparison.OrdinalIgnoreCase))
                    _legacyStats = buttons[i];
                else if (string.Equals(label.text, "МАГАЗИН", StringComparison.OrdinalIgnoreCase))
                    _legacyStore = buttons[i];
            }
        }

        private void HideLegacyMetaHomeButtons()
        {
            if (_legacyMetaGroup == null) ResolveMetaButtons();
            if (_legacyMetaGroup == null) return;
            _legacyMetaGroup.alpha = 0f;
            _legacyMetaGroup.blocksRaycasts = false;
            _legacyMetaGroup.interactable = false;
        }

        private void BuildDashboard(Transform canvas)
        {
            var root = new GameObject("HomeDashboard", typeof(RectTransform), typeof(CanvasGroup));
            root.transform.SetParent(canvas, false);
            Stretch(root.GetComponent<RectTransform>());
            root.transform.SetAsLastSibling();
            _dashboardGroup = root.GetComponent<CanvasGroup>();

            Text logo = CreateText(root.transform, "Logo", "НЕ СБЕЙСЯ!", 68, TextAnchor.MiddleLeft,
                new Vector2(0.075f, 0.900f), new Vector2(0.70f, 0.965f), TextPrimary);
            logo.fontStyle = FontStyle.Bold;
            AddShadow(logo, 0.42f, new Vector2(0f, -3f));

            Text tagline = CreateText(root.transform, "Tagline", "Запомни. Проведи. Попади точно.", 25, TextAnchor.MiddleLeft,
                new Vector2(0.075f, 0.858f), new Vector2(0.80f, 0.902f), TextMuted);
            tagline.fontStyle = FontStyle.Normal;

            CreateAccentDash(root.transform, new Vector2(0.075f, 0.852f), new Vector2(0.29f, 0.858f));

            _streakValue = CreateMetricChip(root.transform, "StreakChip", "СЕРИЯ", "0 ДН", Green,
                new Vector2(0.075f, 0.785f), new Vector2(0.345f, 0.842f));
            _starsValue = CreateMetricChip(root.transform, "StarsChip", "ЗВЁЗДЫ", "0", Gold,
                new Vector2(0.365f, 0.785f), new Vector2(0.635f, 0.842f));
            _coinsValue = CreateMetricChip(root.transform, "CoinsChip", "МОНЕТЫ", "0", CyanBright,
                new Vector2(0.655f, 0.785f), new Vector2(0.925f, 0.842f));

            BuildDailyCard(root.transform);
            BuildCampaignCard(root.transform);
            BuildQuickActions(root.transform);

            Text footer = CreateText(root.transform, "Footer", "Короткие сессии  •  одной рукой  •  прогресс хранится локально", 20,
                new Vector2(0.075f, 0.075f), new Vector2(0.925f, 0.112f), TextMuted);
            footer.alignment = TextAnchor.MiddleCenter;
        }

        private void BuildDailyCard(Transform parent)
        {
            Transform card = CreateCard(parent, "DailyCard",
                new Vector2(0.075f, 0.545f), new Vector2(0.925f, 0.755f),
                new Color(0.035f, 0.075f, 0.130f, 0.99f), Cyan);

            CreateText(card, "DailyKicker", "ИСПЫТАНИЕ ДНЯ", 24, TextAnchor.MiddleLeft,
                new Vector2(0.055f, 0.79f), new Vector2(0.58f, 0.94f), CyanBright).fontStyle = FontStyle.Bold;
            _dailyMeta = CreateText(card, "DailyMeta", "СЕГОДНЯ", 21, TextAnchor.MiddleRight,
                new Vector2(0.58f, 0.79f), new Vector2(0.94f, 0.94f), TextMuted);

            Text headline = CreateText(card, "DailyHeadline", "ОДИН МАРШРУТ.\nСМОЖЕШЬ ТОЧНЕЕ?", 38, TextAnchor.MiddleLeft,
                new Vector2(0.055f, 0.42f), new Vector2(0.64f, 0.79f), TextPrimary);
            headline.fontStyle = FontStyle.Bold;
            headline.lineSpacing = 0.90f;

            BuildRoutePreview(card);

            _dailyBest = CreateText(card, "DailyBest", "ЛУЧШИЙ  —", 20, TextAnchor.MiddleLeft,
                new Vector2(0.055f, 0.25f), new Vector2(0.55f, 0.40f), TextMuted);

            Button play = CreateActionButton(card, "DailyPlay", "ИГРАТЬ DAILY", CyanBright,
                new Color(0.02f, 0.05f, 0.08f, 1f),
                new Vector2(0.055f, 0.07f), new Vector2(0.945f, 0.25f));
            play.onClick.AddListener(() => InvokeLegacy(_legacyShare));
        }

        private void BuildCampaignCard(Transform parent)
        {
            Transform card = CreateCard(parent, "CampaignCard",
                new Vector2(0.075f, 0.345f), new Vector2(0.925f, 0.515f),
                Surface, Violet);

            CreateText(card, "CampaignKicker", "КАМПАНИЯ", 21, TextAnchor.MiddleLeft,
                new Vector2(0.055f, 0.73f), new Vector2(0.45f, 0.92f), new Color(0.72f, 0.66f, 1f, 1f)).fontStyle = FontStyle.Bold;

            Text title = CreateText(card, "CampaignTitle", "60 УРОВНЕЙ  •  6 ГЛАВ", 31, TextAnchor.MiddleLeft,
                new Vector2(0.055f, 0.45f), new Vector2(0.72f, 0.74f), TextPrimary);
            title.fontStyle = FontStyle.Bold;

            _campaignMeta = CreateText(card, "CampaignMeta", "0/60  •  ★ 0", 20, TextAnchor.MiddleLeft,
                new Vector2(0.055f, 0.27f), new Vector2(0.65f, 0.45f), TextMuted);

            Transform progress = CreateRect(parent: card, name: "ProgressTrack",
                min: new Vector2(0.055f, 0.16f), max: new Vector2(0.66f, 0.23f));
            Image track = progress.gameObject.AddComponent<Image>();
            track.sprite = _rounded;
            track.type = Image.Type.Sliced;
            track.color = new Color(0.12f, 0.14f, 0.22f, 0.88f);
            track.raycastTarget = false;

            Transform fillGo = CreateRect(progress, "ProgressFill", Vector2.zero, Vector2.one);
            _campaignProgressFill = fillGo.gameObject.AddComponent<Image>();
            _campaignProgressFill.sprite = _rounded;
            _campaignProgressFill.type = Image.Type.Sliced;
            _campaignProgressFill.color = new Color(Violet.r, Violet.g, Violet.b, 0.95f);
            _campaignProgressFill.raycastTarget = false;
            _campaignProgressFill.rectTransform.anchorMax = new Vector2(0.02f, 1f);

            Button levels = CreateActionButton(card, "LevelsButton", "УРОВНИ", Violet, TextPrimary,
                new Vector2(0.70f, 0.14f), new Vector2(0.945f, 0.50f));
            levels.onClick.AddListener(() => InvokeLegacy(_legacyPrimary));
        }

        private void BuildQuickActions(Transform parent)
        {
            CreateQuickAction(parent, "TrainingQuick", "ТРЕНИРОВКА", "Без ограничений", CyanBright,
                new Vector2(0.075f, 0.165f), new Vector2(0.345f, 0.315f),
                () => InvokeLegacy(_legacySecondary));

            CreateQuickAction(parent, "StatsQuick", "СТАТИСТИКА", "Рекорды и серия", Gold,
                new Vector2(0.365f, 0.165f), new Vector2(0.635f, 0.315f),
                () => InvokeLegacy(_legacyStats));

            CreateQuickAction(parent, "StoreQuick", "МАГАЗИН", "Скины и подсказки", new Color(0.72f, 0.54f, 1f, 1f),
                new Vector2(0.655f, 0.165f), new Vector2(0.925f, 0.315f),
                () => InvokeLegacy(_legacyStore));
        }

        private void CreateQuickAction(
            Transform parent,
            string name,
            string title,
            string subtitle,
            Color accent,
            Vector2 min,
            Vector2 max,
            UnityEngine.Events.UnityAction action)
        {
            Transform card = CreateCard(parent, name, min, max, SurfaceRaised, accent);

            Image icon = CreateImage(card, "Icon", accent,
                new Vector2(0.10f, 0.60f), new Vector2(0.30f, 0.86f), _circle);
            icon.raycastTarget = false;

            Text glyph = CreateText(icon.transform, "Glyph", QuickGlyph(title), 28, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, new Color(0.02f, 0.04f, 0.07f, 1f));
            glyph.fontStyle = FontStyle.Bold;

            Text heading = CreateText(card, "Title", title, 22, TextAnchor.MiddleLeft,
                new Vector2(0.10f, 0.34f), new Vector2(0.92f, 0.58f), TextPrimary);
            heading.fontStyle = FontStyle.Bold;

            CreateText(card, "Subtitle", subtitle, 17, TextAnchor.UpperLeft,
                new Vector2(0.10f, 0.10f), new Vector2(0.92f, 0.34f), TextMuted);

            Button button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = card.GetComponent<Image>();
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.94f);
            colors.pressedColor = new Color(0.80f, 0.84f, 0.92f, 1f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(action);
        }

        private void RefreshData()
        {
            if (_bootstrap == null) return;
            SaveData save = GameBootstrapRuntimeBridge.Save(_bootstrap) ?? _repository.Load();
            if (save == null) return;

            var progress = new CampaignProgressService(_repository, save);
            int completed = progress.CompletedLevels();
            int totalStars = progress.TotalStars();

            if (_streakValue != null) _streakValue.text = save.Streak + " ДН";
            if (_starsValue != null) _starsValue.text = totalStars.ToString();
            if (_coinsValue != null) _coinsValue.text = save.Coins.ToString();

            if (_dailyMeta != null)
            {
                string date = DateTime.Now.ToString("dd.MM");
                _dailyMeta.text = date + "  •  СЕРИЯ " + save.Streak + "Д";
            }

            if (_dailyBest != null)
                _dailyBest.text = save.PersonalBest > 0.0
                    ? "ЛУЧШИЙ  " + save.PersonalBest.ToString("0.0") + "%"
                    : "ЛУЧШИЙ  —";

            if (_campaignMeta != null)
                _campaignMeta.text = completed + "/" + CampaignLevelCatalog.TotalLevels + "  •  ★ " + totalStars;

            if (_campaignProgressFill != null)
            {
                float ratio = CampaignLevelCatalog.TotalLevels <= 0
                    ? 0f
                    : Mathf.Clamp01(completed / (float)CampaignLevelCatalog.TotalLevels);
                _campaignProgressFill.rectTransform.anchorMax = new Vector2(Mathf.Max(0.02f, ratio), 1f);
            }
        }

        private void SetDashboardVisible(bool visible)
        {
            if (_dashboardGroup != null)
            {
                _dashboardGroup.alpha = visible ? 1f : 0f;
                _dashboardGroup.interactable = visible;
                _dashboardGroup.blocksRaycasts = visible;
            }

            SetLegacyGroup(_titleGroup, !visible, false);
            SetLegacyGroup(_statusGroup, !visible, false);
            SetLegacyGroup(_playGroup, !visible, false);
            SetLegacyGroup(_primaryGroup, !visible, true);
            SetLegacyGroup(_secondaryGroup, !visible, true);
            SetLegacyGroup(_shareGroup, !visible, true);

            HideLegacyMetaHomeButtons();
        }

        private static void SetLegacyGroup(CanvasGroup group, bool visible, bool interactive)
        {
            if (group == null) return;
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible && interactive;
            group.blocksRaycasts = visible && interactive;
        }

        private static void InvokeLegacy(Button button)
        {
            if (button == null || !button.interactable) return;
            button.onClick.Invoke();
        }

        private static Transform CreateCard(
            Transform parent,
            string name,
            Vector2 min,
            Vector2 max,
            Color background,
            Color accent)
        {
            Transform card = CreateRect(parent, name, min, max);
            Image image = card.gameObject.AddComponent<Image>();
            image.sprite = _rounded;
            image.type = Image.Type.Sliced;
            image.color = background;

            Shadow shadow = card.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.42f);
            shadow.effectDistance = new Vector2(0f, -10f);
            shadow.useGraphicAlpha = true;

            Outline outline = card.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(accent.r, accent.g, accent.b, 0.16f);
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = true;

            Image accentBar = CreateImage(card, "Accent", accent,
                new Vector2(0f, 0.10f), new Vector2(0.010f, 0.90f), _rounded);
            accentBar.raycastTarget = false;
            return card;
        }

        private static Text CreateMetricChip(
            Transform parent,
            string name,
            string label,
            string value,
            Color accent,
            Vector2 min,
            Vector2 max)
        {
            Transform chip = CreateCard(parent, name, min, max,
                new Color(0.045f, 0.062f, 0.105f, 0.94f), accent);

            Text labelText = CreateText(chip, "Label", label, 15, TextAnchor.MiddleLeft,
                new Vector2(0.09f, 0.52f), new Vector2(0.60f, 0.88f), TextMuted);
            labelText.fontStyle = FontStyle.Bold;

            Text valueText = CreateText(chip, "Value", value, 24, TextAnchor.MiddleLeft,
                new Vector2(0.09f, 0.08f), new Vector2(0.88f, 0.58f), TextPrimary);
            valueText.fontStyle = FontStyle.Bold;
            return valueText;
        }

        private static Button CreateActionButton(
            Transform parent,
            string name,
            string label,
            Color background,
            Color textColor,
            Vector2 min,
            Vector2 max)
        {
            Transform root = CreateRect(parent, name, min, max);
            Image image = root.gameObject.AddComponent<Image>();
            image.sprite = _rounded;
            image.type = Image.Type.Sliced;
            image.color = background;

            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = background;
            colors.highlightedColor = Lighten(background, 0.07f);
            colors.pressedColor = Darken(background, 0.12f);
            colors.disabledColor = new Color(background.r, background.g, background.b, 0.28f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            Text text = CreateText(root, "Label", label, 27, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, textColor);
            text.fontStyle = FontStyle.Bold;
            text.raycastTarget = false;
            return button;
        }

        private static void BuildRoutePreview(Transform card)
        {
            var host = new GameObject("DailyRoutePreview", typeof(RectTransform));
            host.transform.SetParent(card, false);
            RectTransform rect = host.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.66f, 0.37f);
            rect.anchorMax = new Vector2(0.94f, 0.78f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var points = new List<FixedPoint2>
            {
                FixedPoint2.FromNormalized(0.08, 0.20),
                FixedPoint2.FromNormalized(0.30, 0.68),
                FixedPoint2.FromNormalized(0.52, 0.38),
                FixedPoint2.FromNormalized(0.74, 0.72),
                FixedPoint2.FromNormalized(0.94, 0.42)
            };

            RouteGraphic glow = CreateRoute(host.transform, "Glow", new Color(Cyan.r, Cyan.g, Cyan.b, 0.18f), 24f);
            glow.SetPoints(points);
            RouteGraphic route = CreateRoute(host.transform, "Route", CyanBright, 10f);
            route.SetPoints(points);

            Image start = CreateImage(host.transform, "Start", Green,
                new Vector2(0.02f, 0.14f), new Vector2(0.15f, 0.31f), _circle);
            start.raycastTarget = false;
            Image end = CreateImage(host.transform, "End", new Color(1f, 0.38f, 0.48f, 1f),
                new Vector2(0.87f, 0.34f), new Vector2(1.00f, 0.51f), _circle);
            end.raycastTarget = false;
        }

        private static RouteGraphic CreateRoute(Transform parent, string name, Color color, float thickness)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(RouteGraphic));
            go.transform.SetParent(parent, false);
            Stretch(go.GetComponent<RectTransform>());
            RouteGraphic graphic = go.GetComponent<RouteGraphic>();
            graphic.color = color;
            graphic.Thickness = thickness;
            graphic.raycastTarget = false;
            return graphic;
        }

        private static void CreateAccentDash(Transform parent, Vector2 min, Vector2 max)
        {
            Transform dash = CreateRect(parent, "LogoAccent", min, max);
            Image image = dash.gameObject.AddComponent<Image>();
            image.sprite = _rounded;
            image.type = Image.Type.Sliced;
            image.color = CyanBright;
            image.raycastTarget = false;
        }

        private static string QuickGlyph(string title)
        {
            if (title.StartsWith("ТРЕНИ", StringComparison.Ordinal)) return "▶";
            if (title.StartsWith("СТАТ", StringComparison.Ordinal)) return "★";
            return "◆";
        }

        private static Transform CreateRect(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return go.transform;
        }

        private static Image CreateImage(
            Transform parent,
            string name,
            Color color,
            Vector2 min,
            Vector2 max,
            Sprite sprite)
        {
            Transform root = CreateRect(parent, name, min, max);
            Image image = root.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = sprite == _rounded ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
            return image;
        }

        private static Text CreateText(
            Transform parent,
            string name,
            string value,
            int size,
            TextAnchor alignment,
            Vector2 min,
            Vector2 max,
            Color color)
        {
            Transform root = CreateRect(parent, name, min, max);
            Text text = root.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Math.Max(13, size - 10);
            text.resizeTextMaxSize = size;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static void AddShadow(Text text, float alpha, Vector2 distance)
        {
            Shadow shadow = text.GetComponent<Shadow>();
            if (shadow == null) shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, alpha);
            shadow.effectDistance = distance;
            shadow.useGraphicAlpha = true;
        }

        private static CanvasGroup EnsureCanvasGroup(GameObject go)
        {
            CanvasGroup group = go.GetComponent<CanvasGroup>();
            if (group == null) group = go.AddComponent<CanvasGroup>();
            return group;
        }

        private static T Find<T>(Transform root, string name) where T : Component
        {
            Transform transform = FindTransform(root, name);
            return transform == null ? null : transform.GetComponent<T>();
        }

        private static Transform FindTransform(Transform root, string name)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
                if (string.Equals(all[i].name, name, StringComparison.Ordinal)) return all[i];
            return null;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Color Lighten(Color color, float amount) => new Color(
            Mathf.Clamp01(color.r + amount),
            Mathf.Clamp01(color.g + amount),
            Mathf.Clamp01(color.b + amount),
            color.a);

        private static Color Darken(Color color, float amount) => new Color(
            Mathf.Clamp01(color.r - amount),
            Mathf.Clamp01(color.g - amount),
            Mathf.Clamp01(color.b - amount),
            color.a);

        private static void EnsureAssets()
        {
            if (_rounded == null) _rounded = CreateRoundedSprite(96, 24);
            if (_circle == null) _circle = CreateCircleSprite(64);
        }

        private static Sprite CreateRoundedSprite(int size, int radius)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "HomeDashboardRounded",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color32[] pixels = new Color32[size * size];
            float edge = radius - 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float cx = x < radius ? radius : x >= size - radius ? size - radius - 1 : x;
                    float cy = y < radius ? radius : y >= size - radius ? size - radius - 1 : y;
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                    byte alpha = distance <= edge ? (byte)255 : distance <= radius + 0.75f ? (byte)170 : (byte)0;
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Vector4 border = new Vector4(radius + 2, radius + 2, radius + 2, radius + 2);
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                border);
            sprite.name = "HomeDashboardRoundedSprite";
            return sprite;
        }

        private static Sprite CreateCircleSprite(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "HomeDashboardCircle",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color32[] pixels = new Color32[size * size];
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = size * 0.46f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    byte alpha = distance <= radius - 1f ? (byte)255 :
                                 distance <= radius + 0.75f ? (byte)150 : (byte)0;
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = "HomeDashboardCircleSprite";
            return sprite;
        }
    }
}
