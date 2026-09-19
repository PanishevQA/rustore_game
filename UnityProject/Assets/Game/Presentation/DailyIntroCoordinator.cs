using System;
using DontGetSidetracked.Core;
using DontGetSidetracked.Daily;
using DontGetSidetracked.Gameplay;
using DontGetSidetracked.Social;
using UnityEngine;
using UnityEngine.UI;

namespace DontGetSidetracked.Presentation
{
    /// <summary>
    /// Release Daily Challenge intro screen. Keeps gameplay start explicit and mirrors the approved
    /// pre-round composition without moving Daily rules into presentation code.
    /// </summary>
    [DefaultExecutionOrder(15500)]
    public sealed class DailyIntroCoordinator : MonoBehaviour
    {
        private GameBootstrap _bootstrap;
        private JsonFileSaveRepository _repository;
        private GameObject _canvas;
        private CanvasGroup _group;
        private Text _date;
        private Text _streak;
        private Text _best;
        private Text _coins;
        private Text _routeCount;

        public bool IsOpen => _canvas != null && _canvas.activeSelf;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (FindFirstObjectByType<DailyIntroCoordinator>() != null) return;
            var root = new GameObject("DailyIntroCoordinator");
            DontDestroyOnLoad(root);
            root.AddComponent<DailyIntroCoordinator>();
        }

        private void Awake()
        {
            _repository = new JsonFileSaveRepository();
            BuildUi();
            Close();
        }

        public void Open(GameBootstrap bootstrap)
        {
            _bootstrap = bootstrap != null ? bootstrap : FindFirstObjectByType<GameBootstrap>();
            if (_bootstrap == null || !GameBootstrapRuntimeBridge.IsPlainHome(_bootstrap)) return;

            Refresh();
            _canvas.SetActive(true);
            _group.alpha = 1f;
            _group.interactable = true;
            _group.blocksRaycasts = true;

            ReleasePanelMotion motion = _canvas.GetComponentInChildren<ReleasePanelMotion>(true);
            motion?.Play();
        }

        public void Close()
        {
            if (_canvas == null) return;
            _group.alpha = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false;
            _canvas.SetActive(false);
        }

        private void Play()
        {
            GameBootstrap owner = _bootstrap != null ? _bootstrap : FindFirstObjectByType<GameBootstrap>();
            if (owner == null || !GameBootstrapRuntimeBridge.IsPlainHome(owner)) return;
            Close();
            GameBootstrapRuntimeBridge.StartDaily(owner);
        }

        private void Refresh()
        {
            SaveData save = GameBootstrapRuntimeBridge.Save(_bootstrap) ?? _repository.Load();
            DateTime today = DateTime.UtcNow;
            if (_date != null) _date.text = today.ToString("dd MMMM", new System.Globalization.CultureInfo("ru-RU"));
            if (_streak != null) _streak.text = save == null ? "0 ДНЕЙ" : save.Streak + " ДН.";
            double dailyBest = new DailyBestService().GetBest(save, OfflineDaily.ChallengeId(today));
            if (_best != null) _best.text = dailyBest > 0
                ? dailyBest.ToString("0.0") + "%"
                : "—";
            if (_coins != null) _coins.text = save == null ? "0" : save.Coins.ToString();
            if (_routeCount != null) _routeCount.text = RouteRuntimeTuning.DailyRouteCount.ToString();
        }

        private void BuildUi()
        {
            _canvas = new GameObject("DailyIntroCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            _canvas.transform.SetParent(transform, false);
            Canvas canvas = _canvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 65;

            CanvasScaler scaler = _canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            _group = _canvas.GetComponent<CanvasGroup>();
            RawImage backdrop = ReleaseUiComponents.Backdrop(_canvas.transform);
            backdrop.raycastTarget = true;

            var releaseVisual = new GameObject("ReleaseVisual", typeof(RectTransform));
            releaseVisual.transform.SetParent(_canvas.transform, false);
            ReleaseUiKit.Stretch(releaseVisual.GetComponent<RectTransform>());

            ReleaseUiComponents.SecondaryButton(_canvas.transform, "Back", "НАЗАД",
                new Vector2(0.06f, 0.880f), new Vector2(0.25f, 0.935f), Close, 24);

            ReleaseUiComponents.Icon(_canvas.transform, "DailyIcon", GeneratedUiAssets.DailyIcon,
                new Vector2(0.42f, 0.855f), new Vector2(0.58f, 0.945f));

            Text title = ReleaseUiKit.TextBlock(_canvas.transform, "Title", "ИСПЫТАНИЕ ДНЯ", 49,
                TextAnchor.MiddleCenter, new Vector2(0.10f, 0.790f), new Vector2(0.90f, 0.855f),
                ReleaseUiComponents.Text, FontStyle.Bold);
            ReleaseUiKit.AddTextShadow(title, 0.50f, -3f);

            _date = ReleaseUiKit.TextBlock(_canvas.transform, "Date", string.Empty, 24,
                TextAnchor.MiddleCenter, new Vector2(0.20f, 0.755f), new Vector2(0.80f, 0.795f),
                ReleaseUiComponents.Muted);

            Image hero = ReleaseUiComponents.GlassCard(_canvas.transform, "DailyHero",
                new Vector2(0.07f, 0.470f), new Vector2(0.93f, 0.735f), ReleaseUiComponents.Cyan, true);
            hero.gameObject.AddComponent<ReleasePanelMotion>();

            ReleaseUiKit.TextBlock(hero.transform, "Headline", "НОВЫЙ ДЕНЬ.\nНОВЫЙ МАРШРУТ.", 42,
                TextAnchor.MiddleLeft, new Vector2(0.055f, 0.55f), new Vector2(0.945f, 0.91f),
                ReleaseUiComponents.Text, FontStyle.Bold);
            ReleaseUiKit.TextBlock(hero.transform, "Description", "Одно испытание для всех. Превзойди свой рекорд.", 27,
                TextAnchor.MiddleLeft, new Vector2(0.055f, 0.41f), new Vector2(0.945f, 0.55f),
                ReleaseUiComponents.Muted);
            _streak = CreateMetric(hero.transform, "Streak", "0 ДН.", "Серия дней", GeneratedUiAssets.StarFilled,
                new Vector2(0.055f, 0.08f), new Vector2(0.335f, 0.36f), ReleaseUiComponents.Gold);
            _best = CreateMetric(hero.transform, "Best", "—", "Лучший за день", GeneratedUiAssets.MedalGold,
                new Vector2(0.365f, 0.08f), new Vector2(0.665f, 0.36f), ReleaseUiComponents.Cyan);
            _routeCount = CreateMetric(hero.transform, "Routes", "3", "Маршрутов", GeneratedUiAssets.DailyIcon,
                new Vector2(0.695f, 0.08f), new Vector2(0.945f, 0.36f), ReleaseUiComponents.Violet);

            ReleaseUiComponents.SectionHeader(_canvas.transform, "КАК ИГРАТЬ",
                new Vector2(0.07f, 0.418f), new Vector2(0.60f, 0.455f));

            Image goal = ReleaseUiComponents.GlassCard(_canvas.transform, "Goal",
                new Vector2(0.07f, 0.300f), new Vector2(0.93f, 0.410f), ReleaseUiComponents.Gold, false);
            ReleaseUiComponents.Icon(goal.transform, "Eye", GeneratedUiAssets.EyeIcon,
                new Vector2(0.04f, 0.15f), new Vector2(0.20f, 0.85f));
            ReleaseUiKit.TextBlock(goal.transform, "GoalCopy",
                "Запомни линию. Повтори одним движением.\nОт голубой точки до золотой — без отрыва.", 28,
                TextAnchor.MiddleLeft, new Vector2(0.24f, 0.13f), new Vector2(0.94f, 0.88f),
                ReleaseUiComponents.Text);

            ReleaseUiComponents.PrimaryButton(_canvas.transform, "Play", "ИГРАТЬ",
                new Vector2(0.07f, 0.200f), new Vector2(0.93f, 0.275f), Play, 32);

            Image reward = ReleaseUiComponents.GlassCard(_canvas.transform, "RewardInfo",
                new Vector2(0.07f, 0.105f), new Vector2(0.93f, 0.175f), ReleaseUiComponents.Violet, false);
            ReleaseUiKit.TextBlock(reward.transform, "Copy", "Сравни точность и брось вызов другу после игры", 23,
                TextAnchor.MiddleCenter, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.92f),
                ReleaseUiComponents.Muted, FontStyle.Bold);

            _coins = ReleaseUiComponents.CurrencyPill(_canvas.transform, "Coins", string.Empty, "0",
                new Vector2(0.74f, 0.895f), new Vector2(0.94f, 0.94f), ReleaseUiComponents.Gold);

        }

        private static Text CreateMetric(Transform parent, string name, string value, string caption,
            string asset, Vector2 min, Vector2 max, Color accent)
        {
            Transform root = ReleaseUiKit.Rect(parent, name, min, max);
            Image iconWell = ReleaseUiComponents.GlassCard(root, "IconWell",
                new Vector2(0.00f, 0.37f), new Vector2(0.28f, 0.97f), accent, false);
            iconWell.color = new Color(accent.r, accent.g, accent.b, 0.10f);
            ReleaseUiComponents.Icon(iconWell.transform, "Icon", asset,
                new Vector2(0.14f, 0.14f), new Vector2(0.86f, 0.86f));
            Text metric = ReleaseUiKit.TextBlock(root, "Value", value, 34, TextAnchor.MiddleLeft,
                new Vector2(0.34f, 0.40f), new Vector2(1f, 0.97f), ReleaseUiComponents.Text, FontStyle.Bold);
            ReleaseUiKit.TextBlock(root, "Caption", caption, 22, TextAnchor.MiddleLeft,
                new Vector2(0f, 0.01f), new Vector2(1f, 0.35f), ReleaseUiComponents.Muted, FontStyle.Bold);
            return metric;
        }
    }
}
