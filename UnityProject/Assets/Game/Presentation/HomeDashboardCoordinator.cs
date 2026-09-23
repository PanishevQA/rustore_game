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
        private ReleasePanelMotion _dashboardMotion;
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
        private Text _hintsValue;
        private Text _dailyCountdown;
        private Text _bestStatValue;
        private Text _dailyCountValue;
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

            bool show = GameBootstrapRuntimeBridge.IsPlainHome(_bootstrap);

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
            _dashboardMotion = root.AddComponent<ReleasePanelMotion>();

            ReleaseUiComponents.Backdrop(root.transform, "HomeBackdrop");

            BuildTargetTopBar(root.transform);

            Text logo = ReleaseUiKit.TextBlock(root.transform, "Logo", "НЕ СБЕЙСЯ!", 66,
                TextAnchor.MiddleCenter, new Vector2(0.12f, 0.825f), new Vector2(0.88f, 0.895f),
                ReleaseUiComponents.Text, FontStyle.Bold);
            ReleaseUiKit.AddTextShadow(logo, 0.58f, -3f);
            Outline logoGlow = logo.gameObject.AddComponent<Outline>();
            logoGlow.effectColor = new Color(ReleaseUiComponents.Cyan.r, ReleaseUiComponents.Cyan.g,
                ReleaseUiComponents.Cyan.b, 0.28f);
            logoGlow.effectDistance = new Vector2(2f, -2f);

            BuildTargetDailyCard(root.transform);
            BuildTargetStatCards(root.transform);

            CreateTargetNavRow(root.transform, "CampaignRow", "КАМПАНИЯ", "Пройдено: 0/60",
                GeneratedUiAssets.CampaignIcon, ReleaseUiComponents.Blue,
                new Vector2(0.055f, 0.405f), new Vector2(0.945f, 0.472f), OpenCampaignFromHome, out _campaignMeta);

            CreateTargetNavRow(root.transform, "TrainingRow", "ТРЕНИРОВКА", "Без ограничений",
                GeneratedUiAssets.TrainingIcon, ReleaseUiComponents.Cyan,
                new Vector2(0.055f, 0.329f), new Vector2(0.945f, 0.396f), OpenTrainingFromHome, out _);

            CreateTargetNavRow(root.transform, "RatingRow", "РЕЙТИНГ", "Топ игроков",
                GeneratedUiAssets.StatisticsIcon, ReleaseUiComponents.Violet,
                new Vector2(0.055f, 0.253f), new Vector2(0.945f, 0.320f), OpenStatisticsFromHome, out _);

            CreateTargetNavRow(root.transform, "StoreRow", "МАГАЗИН", "Скины, подсказки, без рекламы",
                GeneratedUiAssets.StoreIcon, new Color(0.83f, 0.38f, 1f, 1f),
                new Vector2(0.055f, 0.177f), new Vector2(0.945f, 0.244f), OpenStoreFromHome, out _);
        }

        private void BuildTargetTopBar(Transform parent)
        {
            Image hintPill = ReleaseUiComponents.GlassCard(parent, "HintPill",
                new Vector2(0.055f, 0.922f), new Vector2(0.255f, 0.972f),
                ReleaseUiComponents.Cyan, false);
            hintPill.color = new Color(0.020f, 0.060f, 0.110f, 0.96f);
            BuildLightningIcon(hintPill.transform, new Vector2(0.070f, 0.20f), new Vector2(0.285f, 0.80f));
            _hintsValue = ReleaseUiKit.TextBlock(hintPill.transform, "Value", "0", 25, TextAnchor.MiddleCenter,
                new Vector2(0.30f, 0.08f), new Vector2(0.66f, 0.92f), ReleaseUiComponents.Text, FontStyle.Bold);
            Button hintPlus = ReleaseUiComponents.SecondaryButton(hintPill.transform, "Plus", "+",
                new Vector2(0.70f, 0.14f), new Vector2(0.95f, 0.86f), OpenStoreFromHome, 22);
            Image hintPlusImage = hintPlus.GetComponent<Image>();
            if (hintPlusImage != null)
                hintPlusImage.color = new Color(ReleaseUiComponents.Cyan.r, ReleaseUiComponents.Cyan.g,
                    ReleaseUiComponents.Cyan.b, 0.14f);

            Image coinPill = ReleaseUiComponents.GlassCard(parent, "CoinsPill",
                new Vector2(0.300f, 0.922f), new Vector2(0.725f, 0.972f),
                ReleaseUiComponents.Gold, false);
            coinPill.color = new Color(0.028f, 0.055f, 0.090f, 0.97f);
            Image coinOuter = CreateImage(coinPill.transform, "CoinOuter", ReleaseUiComponents.Gold,
                new Vector2(0.055f, 0.18f), new Vector2(0.245f, 0.82f), _circle);
            coinOuter.raycastTarget = false;
            Image coinInner = CreateImage(coinOuter.transform, "CoinInner", new Color(1f, 0.55f, 0.06f, 1f),
                new Vector2(0.18f, 0.18f), new Vector2(0.82f, 0.82f), _circle);
            coinInner.raycastTarget = false;
            _coinsValue = ReleaseUiKit.TextBlock(coinPill.transform, "Value", "0", 25, TextAnchor.MiddleLeft,
                new Vector2(0.29f, 0.08f), new Vector2(0.70f, 0.92f), ReleaseUiComponents.Text, FontStyle.Bold);
            Button coinPlus = ReleaseUiComponents.SecondaryButton(coinPill.transform, "Plus", "+",
                new Vector2(0.76f, 0.14f), new Vector2(0.95f, 0.86f), OpenStoreFromHome, 22);
            Image coinPlusImage = coinPlus.GetComponent<Image>();
            if (coinPlusImage != null)
                coinPlusImage.color = new Color(ReleaseUiComponents.Gold.r, ReleaseUiComponents.Gold.g,
                    ReleaseUiComponents.Gold.b, 0.14f);

            Image settingsWell = ReleaseUiComponents.GlassCard(parent, "SettingsButton",
                new Vector2(0.825f, 0.918f), new Vector2(0.945f, 0.974f),
                ReleaseUiComponents.Blue, false);
            settingsWell.color = new Color(0.025f, 0.060f, 0.110f, 0.97f);
            ReleaseUiComponents.Icon(settingsWell.transform, "SettingsIcon", GeneratedUiAssets.SettingsIcon,
                new Vector2(0.22f, 0.18f), new Vector2(0.78f, 0.82f));
            Button settingsButton = settingsWell.gameObject.AddComponent<Button>();
            settingsButton.targetGraphic = settingsWell;
            settingsButton.onClick.AddListener(OpenSettingsFromHome);
        }

        private static void BuildLightningIcon(Transform parent, Vector2 min, Vector2 max)
        {
            Transform host = CreateRect(parent, "Lightning", min, max);
            Transform upper = CreateRect(host, "Upper", new Vector2(0.36f, 0.50f), new Vector2(0.58f, 0.98f));
            Image upperImage = upper.gameObject.AddComponent<Image>();
            upperImage.sprite = _rounded;
            upperImage.type = Image.Type.Sliced;
            upperImage.color = ReleaseUiComponents.Cyan;
            upperImage.raycastTarget = false;
            upper.GetComponent<RectTransform>().localRotation = Quaternion.Euler(0f, 0f, -26f);

            Transform middle = CreateRect(host, "Middle", new Vector2(0.28f, 0.42f), new Vector2(0.72f, 0.58f));
            Image middleImage = middle.gameObject.AddComponent<Image>();
            middleImage.sprite = _rounded;
            middleImage.type = Image.Type.Sliced;
            middleImage.color = ReleaseUiComponents.Cyan;
            middleImage.raycastTarget = false;
            middle.GetComponent<RectTransform>().localRotation = Quaternion.Euler(0f, 0f, -10f);

            Transform lower = CreateRect(host, "Lower", new Vector2(0.42f, 0.02f), new Vector2(0.64f, 0.50f));
            Image lowerImage = lower.gameObject.AddComponent<Image>();
            lowerImage.sprite = _rounded;
            lowerImage.type = Image.Type.Sliced;
            lowerImage.color = ReleaseUiComponents.Cyan;
            lowerImage.raycastTarget = false;
            lower.GetComponent<RectTransform>().localRotation = Quaternion.Euler(0f, 0f, -26f);
        }

        private void BuildTargetDailyCard(Transform parent)
        {
            Image card = ReleaseUiComponents.GlassCard(parent, "DailyCard",
                new Vector2(0.055f, 0.620f), new Vector2(0.945f, 0.805f),
                ReleaseUiComponents.Violet, true);
            card.color = new Color(0.018f, 0.052f, 0.105f, 0.985f);

            ReleaseUiComponents.Icon(card.transform, "DailyIcon", GeneratedUiAssets.DailyIcon,
                new Vector2(0.055f, 0.69f), new Vector2(0.155f, 0.91f));

            ReleaseUiKit.TextBlock(card.transform, "DailyTitle", "СЕГОДНЯШНИЙ ВЫЗОВ", 30,
                TextAnchor.MiddleLeft, new Vector2(0.18f, 0.73f), new Vector2(0.90f, 0.93f),
                ReleaseUiComponents.Text, FontStyle.Bold);

            _dailyMeta = ReleaseUiKit.TextBlock(card.transform, "DailyDate", "СЕГОДНЯ", 19,
                TextAnchor.MiddleLeft, new Vector2(0.18f, 0.57f), new Vector2(0.64f, 0.73f),
                ReleaseUiComponents.Muted, FontStyle.Bold);

            _dailyCountdown = ReleaseUiKit.TextBlock(card.transform, "DailyCountdown", "Осталось: --:--:--", 17,
                TextAnchor.MiddleLeft, new Vector2(0.18f, 0.43f), new Vector2(0.78f, 0.59f),
                ReleaseUiComponents.Muted);

            ReleaseUiComponents.PrimaryButton(card.transform, "DailyPlay", "ИГРАТЬ",
                new Vector2(0.055f, 0.085f), new Vector2(0.945f, 0.365f), StartDailyFromHome, 31);
        }

        private void BuildTargetStatCards(Transform parent)
        {
            Image streak = ReleaseUiComponents.GlassCard(parent, "StreakCard",
                new Vector2(0.055f, 0.495f), new Vector2(0.485f, 0.600f),
                ReleaseUiComponents.Gold, false);
            ReleaseUiComponents.Icon(streak.transform, "Icon", GeneratedUiAssets.DailyIcon,
                new Vector2(0.070f, 0.30f), new Vector2(0.255f, 0.82f));
            ReleaseUiKit.TextBlock(streak.transform, "Label", "СЕРИЯ ДНЕЙ", 17, TextAnchor.MiddleLeft,
                new Vector2(0.30f, 0.55f), new Vector2(0.94f, 0.88f),
                ReleaseUiComponents.Muted, FontStyle.Bold);
            _streakValue = ReleaseUiKit.TextBlock(streak.transform, "Value", "0", 38, TextAnchor.MiddleLeft,
                new Vector2(0.30f, 0.12f), new Vector2(0.94f, 0.58f),
                ReleaseUiComponents.Text, FontStyle.Bold);

            Image best = ReleaseUiComponents.GlassCard(parent, "BestCard",
                new Vector2(0.515f, 0.495f), new Vector2(0.945f, 0.600f),
                ReleaseUiComponents.Cyan, false);
            ReleaseUiComponents.Icon(best.transform, "Icon", GeneratedUiAssets.MedalGold,
                new Vector2(0.070f, 0.27f), new Vector2(0.255f, 0.84f));
            ReleaseUiKit.TextBlock(best.transform, "Label", "ЛУЧШИЙ РЕЗУЛЬТАТ", 16, TextAnchor.MiddleLeft,
                new Vector2(0.30f, 0.55f), new Vector2(0.94f, 0.88f),
                ReleaseUiComponents.Muted, FontStyle.Bold);
            _bestStatValue = ReleaseUiKit.TextBlock(best.transform, "Value", "—", 37, TextAnchor.MiddleLeft,
                new Vector2(0.30f, 0.12f), new Vector2(0.94f, 0.58f),
                ReleaseUiComponents.Text, FontStyle.Bold);
        }

        private void CreateTargetNavRow(
            Transform parent,
            string name,
            string title,
            string subtitle,
            string iconAsset,
            Color accent,
            Vector2 min,
            Vector2 max,
            UnityEngine.Events.UnityAction action,
            out Text subtitleText)
        {
            Image card = ReleaseUiComponents.GlassCard(parent, name, min, max, accent, false);
            card.color = new Color(0.018f, 0.050f, 0.095f, 0.975f);

            Image iconWell = ReleaseUiKit.Panel(card.transform, "IconWell",
                new Vector2(0.030f, 0.15f), new Vector2(0.160f, 0.85f),
                new Color(accent.r, accent.g, accent.b, 0.14f), accent, false);
            iconWell.raycastTarget = false;
            ReleaseUiComponents.Icon(iconWell.transform, "Icon", iconAsset,
                new Vector2(0.16f, 0.16f), new Vector2(0.84f, 0.84f));

            ReleaseUiKit.TextBlock(card.transform, "Title", title, 24, TextAnchor.MiddleLeft,
                new Vector2(0.195f, 0.48f), new Vector2(0.80f, 0.86f),
                ReleaseUiComponents.Text, FontStyle.Bold);
            subtitleText = ReleaseUiKit.TextBlock(card.transform, "Subtitle", subtitle, 16,
                TextAnchor.MiddleLeft, new Vector2(0.195f, 0.10f), new Vector2(0.82f, 0.51f),
                ReleaseUiComponents.Muted);

            ReleaseUiKit.TextBlock(card.transform, "Arrow", "›", 36, TextAnchor.MiddleCenter,
                new Vector2(0.87f, 0.16f), new Vector2(0.96f, 0.84f), accent, FontStyle.Bold);

            Button button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = card;
            button.onClick.AddListener(action);
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor = new Color(0.72f, 0.82f, 0.95f, 1f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
        }

        private void OpenStatisticsFromHome()
        {
            MetaMenuOverlay meta = FindFirstObjectByType<MetaMenuOverlay>();
            if (meta != null) meta.OpenStatisticsFromHome();
            else InvokeLegacy(_legacyStats);
        }

        private void OpenStoreFromHome()
        {
            MetaMenuOverlay meta = FindFirstObjectByType<MetaMenuOverlay>();
            if (meta != null) meta.OpenStoreFromHome();
            else InvokeLegacy(_legacyStore);
        }

        private void OpenSettingsFromHome()
        {
            MetaMenuOverlay meta = FindFirstObjectByType<MetaMenuOverlay>();
            if (meta != null) meta.OpenSettingsFromHome();
            else OpenStatisticsFromHome();
        }

        private void BuildDailyCard(Transform parent)
        {
            Transform card = ReleaseUiComponents.GlassCard(parent, "DailyCard",
                new Vector2(0.07f, 0.600f), new Vector2(0.93f, 0.835f),
                ReleaseUiComponents.Cyan, true).transform;

            ReleaseUiKit.TextBlock(card, "DailyKicker", "ИСПЫТАНИЕ ДНЯ", 28, TextAnchor.MiddleLeft,
                new Vector2(0.055f, 0.80f), new Vector2(0.60f, 0.94f), ReleaseUiComponents.Text, FontStyle.Bold);
            _dailyMeta = ReleaseUiKit.TextBlock(card, "DailyMeta", "СЕГОДНЯ", 18, TextAnchor.MiddleRight,
                new Vector2(0.58f, 0.81f), new Vector2(0.94f, 0.94f), ReleaseUiComponents.Muted, FontStyle.Bold);

            Text headline = ReleaseUiKit.TextBlock(card, "DailyHeadline", "ОДИН ЧЕЛЛЕНДЖ.\nВСЕ ИГРОКИ. КТО ТОЧНЕЕ?", 33, TextAnchor.MiddleLeft,
                new Vector2(0.055f, 0.42f), new Vector2(0.64f, 0.77f), ReleaseUiComponents.Text, FontStyle.Bold);
            headline.fontStyle = FontStyle.Bold;
            headline.lineSpacing = 0.90f;

            BuildRoutePreview(card);

            _dailyBest = ReleaseUiKit.TextBlock(card, "DailyBest", "ЛУЧШИЙ  —", 20, TextAnchor.MiddleLeft,
                new Vector2(0.055f, 0.23f), new Vector2(0.55f, 0.38f), ReleaseUiComponents.Gold, FontStyle.Bold);

            ReleaseUiComponents.PrimaryButton(card, "DailyPlay", "▶  ИГРАТЬ",
                new Vector2(0.48f, 0.055f), new Vector2(0.945f, 0.245f), StartDailyFromHome, 31);

            Image reward = ReleaseUiComponents.GlassCard(card, "DailyReward",
                new Vector2(0.055f, 0.055f), new Vector2(0.45f, 0.245f), ReleaseUiComponents.Gold, false);
            ReleaseUiKit.TextBlock(reward.transform, "RewardLabel", "DAILY", 16, TextAnchor.MiddleLeft,
                new Vector2(0.08f, 0.54f), new Vector2(0.92f, 0.87f), ReleaseUiComponents.Muted, FontStyle.Bold);
            ReleaseUiKit.TextBlock(reward.transform, "RewardValue", "ОБЩИЙ ЧЕЛЛЕНДЖ", 22, TextAnchor.MiddleLeft,
                new Vector2(0.08f, 0.12f), new Vector2(0.92f, 0.58f), ReleaseUiComponents.Gold, FontStyle.Bold);
        }

        private void BuildCampaignCard(Transform parent)
        {
            Transform card = ReleaseUiComponents.GlassCard(parent, "CampaignCard",
                new Vector2(0.07f, 0.425f), new Vector2(0.93f, 0.585f),
                ReleaseUiComponents.Violet, false).transform;

            CreateText(card, "CampaignKicker", "КАМПАНИЯ", 24, TextAnchor.MiddleLeft,
                new Vector2(0.055f, 0.73f), new Vector2(0.45f, 0.92f), new Color(0.72f, 0.66f, 1f, 1f)).fontStyle = FontStyle.Bold;

            Text title = CreateText(card, "CampaignTitle", "60 УРОВНЕЙ  •  6 ГЛАВ", 36, TextAnchor.MiddleLeft,
                new Vector2(0.055f, 0.45f), new Vector2(0.72f, 0.74f), TextPrimary);
            title.fontStyle = FontStyle.Bold;

            _campaignMeta = CreateText(card, "CampaignMeta", "0/60  •  ★ 0", 24, TextAnchor.MiddleLeft,
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

            Button levels = ReleaseUiComponents.SecondaryButton(card, "LevelsButton", "ПРОДОЛЖИТЬ  ›",
                new Vector2(0.66f, 0.12f), new Vector2(0.945f, 0.48f), OpenCampaignFromHome, 24);
            Image levelsSurface = levels.GetComponent<Image>();
            if (levelsSurface != null)
                levelsSurface.color = new Color(0.055f, 0.040f, 0.135f, 0.98f);
            Outline levelsOutline = levels.GetComponent<Outline>();
            if (levelsOutline != null)
                levelsOutline.effectColor = new Color(Violet.r, Violet.g, Violet.b, 0.52f);
        }

        private void BuildStatRow(Transform parent)
        {
            // One readable progress strip replaces four narrow dashboard cards. At phone
            // width every value now has room to breathe and the Home hierarchy stays clear.
            Image strip = ReleaseUiComponents.GlassCard(parent, "HomeStatsStrip",
                new Vector2(0.07f, 0.315f), new Vector2(0.93f, 0.410f),
                ReleaseUiComponents.Blue, false);

            _starsValue = CreateCompactMetric(strip.transform, "StarsStat", "ЗВЁЗДЫ", "0",
                ReleaseUiComponents.Gold, new Vector2(0.00f, 0f), new Vector2(0.25f, 1f));
            _streakValue = CreateCompactMetric(strip.transform, "StreakStat", "СЕРИЯ", "0 ДН",
                ReleaseUiComponents.Danger, new Vector2(0.25f, 0f), new Vector2(0.50f, 1f));
            _bestStatValue = CreateCompactMetric(strip.transform, "BestStat", "ЛУЧШИЙ", "—",
                ReleaseUiComponents.Cyan, new Vector2(0.50f, 0f), new Vector2(0.75f, 1f));
            _dailyCountValue = CreateCompactMetric(strip.transform, "DailyCountStat", "DAILY", "0",
                ReleaseUiComponents.Blue, new Vector2(0.75f, 0f), new Vector2(1.00f, 1f));

            CreateDivider(strip.transform, 0.25f);
            CreateDivider(strip.transform, 0.50f);
            CreateDivider(strip.transform, 0.75f);
        }

        private static Text CreateCompactMetric(
            Transform parent,
            string name,
            string label,
            string value,
            Color accent,
            Vector2 min,
            Vector2 max)
        {
            Transform slot = CreateRect(parent, name, min, max);
            ReleaseUiKit.TextBlock(slot, "Label", label, 17, TextAnchor.MiddleCenter,
                new Vector2(0.05f, 0.58f), new Vector2(0.95f, 0.88f), accent, FontStyle.Bold);
            return ReleaseUiKit.TextBlock(slot, "Value", value, 34, TextAnchor.MiddleCenter,
                new Vector2(0.05f, 0.12f), new Vector2(0.95f, 0.61f), TextPrimary, FontStyle.Bold);
        }

        private static void CreateDivider(Transform parent, float x)
        {
            Transform divider = CreateRect(parent, "Divider",
                new Vector2(x - 0.0012f, 0.18f), new Vector2(x + 0.0012f, 0.82f));
            Image image = divider.gameObject.AddComponent<Image>();
            image.color = new Color(0.42f, 0.68f, 0.90f, 0.16f);
            image.raycastTarget = false;
        }

        private void BuildQuickActions(Transform parent)
        {
            // Training is the secondary gameplay loop, so it gets a full-width touch target.
            // Meta destinations stay one row below instead of becoming three tiny columns.
            CreateQuickAction(parent, "TrainingQuick", "ТРЕНИРОВКА", "Бесконечные маршруты", ReleaseUiComponents.Success,
                new Vector2(0.07f, 0.235f), new Vector2(0.93f, 0.300f),
                OpenTrainingFromHome);

            CreateQuickAction(parent, "StatsQuick", "СТАТИСТИКА", "Прогресс и рекорды", ReleaseUiComponents.Violet,
                new Vector2(0.07f, 0.155f), new Vector2(0.49f, 0.220f),
                () => InvokeLegacy(_legacyStats));

            CreateQuickAction(parent, "StoreQuick", "МАГАЗИН", "Скины и подсказки", ReleaseUiComponents.Cyan,
                new Vector2(0.51f, 0.155f), new Vector2(0.93f, 0.220f),
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
            Transform card = ReleaseUiComponents.GlassCard(parent, name, min, max, accent, false).transform;

            bool wide = max.x - min.x > 0.60f;
            float iconLeft = wide ? 0.035f : 0.055f;
            float iconRight = wide ? 0.125f : 0.205f;
            float textLeft = wide ? 0.155f : 0.245f;

            Image icon = CreateImage(card, "Icon", accent,
                new Vector2(iconLeft, 0.18f), new Vector2(iconRight, 0.82f), _circle);
            icon.raycastTarget = false;

            string generatedIcon = GeneratedUiAssets.QuickActionIcon(title);
            bool generatedApplied = GeneratedUiAssets.TryApply(icon, generatedIcon);
            if (!generatedApplied)
            {
                Text glyph = CreateText(icon.transform, "Glyph", QuickGlyph(title), wide ? 27 : 24, TextAnchor.MiddleCenter,
                    Vector2.zero, Vector2.one, new Color(0.02f, 0.04f, 0.07f, 1f));
                glyph.fontStyle = FontStyle.Bold;
            }

            Text heading = CreateText(card, "Title", title, wide ? 28 : 24, TextAnchor.MiddleLeft,
                new Vector2(textLeft, 0.48f), new Vector2(0.94f, 0.82f), TextPrimary);
            heading.fontStyle = FontStyle.Bold;

            CreateText(card, "Subtitle", subtitle, wide ? 20 : 18, TextAnchor.MiddleLeft,
                new Vector2(textLeft, 0.14f), new Vector2(0.94f, 0.50f), TextMuted);

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

            if (_streakValue != null) _streakValue.text = save.Streak.ToString();
            if (_starsValue != null) _starsValue.text = totalStars.ToString();
            if (_coinsValue != null) _coinsValue.text = save.Coins.ToString();
            if (_hintsValue != null) _hintsValue.text = save.Hints.ToString();
            if (_bestStatValue != null)
                _bestStatValue.text = save.PersonalBest > 0.0 ? save.PersonalBest.ToString("0.0") + "%" : "—";
            if (_dailyCountValue != null) _dailyCountValue.text = save.CompletedDailyCount.ToString();

            DateTime now = DateTime.UtcNow;
            if (_dailyMeta != null)
                _dailyMeta.text = FormatRussianDate(now);

            if (_dailyCountdown != null)
            {
                TimeSpan remaining = now.Date.AddDays(1) - now;
                _dailyCountdown.text = $"Осталось: {remaining.Hours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}";
            }

            if (_dailyBest != null)
                _dailyBest.text = save.PersonalBest > 0.0
                    ? "ЛУЧШИЙ  " + save.PersonalBest.ToString("0.0") + "%"
                    : "ЛУЧШИЙ  —";

            if (_campaignMeta != null)
                _campaignMeta.text = "Пройдено: " + completed + "/" + CampaignLevelCatalog.TotalLevels;

            if (_campaignProgressFill != null)
            {
                float ratio = CampaignLevelCatalog.TotalLevels <= 0
                    ? 0f
                    : Mathf.Clamp01(completed / (float)CampaignLevelCatalog.TotalLevels);
                _campaignProgressFill.rectTransform.anchorMax = new Vector2(Mathf.Max(0.02f, ratio), 1f);
            }
        }

        private static string FormatRussianDate(DateTime value)
        {
            string month;
            switch (value.Month)
            {
                case 1: month = "января"; break;
                case 2: month = "февраля"; break;
                case 3: month = "марта"; break;
                case 4: month = "апреля"; break;
                case 5: month = "мая"; break;
                case 6: month = "июня"; break;
                case 7: month = "июля"; break;
                case 8: month = "августа"; break;
                case 9: month = "сентября"; break;
                case 10: month = "октября"; break;
                case 11: month = "ноября"; break;
                default: month = "декабря"; break;
            }
            return value.Day + " " + month;
        }

        private void SetDashboardVisible(bool visible)
        {
            if (_dashboardGroup != null)
            {
                if (!visible) _dashboardMotion?.Cancel();
                _dashboardGroup.alpha = visible ? 1f : 0f;
                _dashboardGroup.interactable = visible;
                _dashboardGroup.blocksRaycasts = visible;
                if (visible) _dashboardMotion?.Play();
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

        private void StartDailyFromHome()
        {
            if (_bootstrap == null || !GameBootstrapRuntimeBridge.IsPlainHome(_bootstrap)) return;
            DailyIntroCoordinator intro = FindFirstObjectByType<DailyIntroCoordinator>();
            if (intro != null) intro.Open(_bootstrap);
            else GameBootstrapRuntimeBridge.StartDaily(_bootstrap);
        }

        private void OpenCampaignFromHome()
        {
            if (_bootstrap == null || !GameBootstrapRuntimeBridge.IsPlainHome(_bootstrap)) return;
            CampaignLevelMenuOverlay.OpenFor(_bootstrap);
        }

        private void OpenTrainingFromHome()
        {
            if (_bootstrap == null || !GameBootstrapRuntimeBridge.IsPlainHome(_bootstrap)) return;
            TrainingMenuCoordinator training = FindFirstObjectByType<TrainingMenuCoordinator>();
            if (training != null) training.OpenFromHome();
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
            rect.anchorMin = new Vector2(0.64f, 0.39f);
            rect.anchorMax = new Vector2(0.95f, 0.79f);
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

            Image start = CreateImage(host.transform, "Start", ReleaseUiComponents.Cyan,
                new Vector2(0.02f, 0.14f), new Vector2(0.15f, 0.31f), _circle);
            start.raycastTarget = false;
            GeneratedUiAssets.TryApply(start, GeneratedUiAssets.StartMarker);
            Image end = CreateImage(host.transform, "End", ReleaseUiComponents.Gold,
                new Vector2(0.87f, 0.34f), new Vector2(1.00f, 0.51f), _circle);
            end.raycastTarget = false;
            GeneratedUiAssets.TryApply(end, GeneratedUiAssets.EndMarker);
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
