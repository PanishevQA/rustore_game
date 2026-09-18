using System;
using DontGetSidetracked.Core;
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
            if (_best != null) _best.text = save != null && save.PersonalBest > 0
                ? save.PersonalBest.ToString("0.0") + "%"
                : "—";
            if (_coins != null) _coins.text = save == null ? "0" : save.Coins.ToString();
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
            ReleaseUiComponents.Backdrop(_canvas.transform);

            var releaseVisual = new GameObject("ReleaseVisual", typeof(RectTransform));
            releaseVisual.transform.SetParent(_canvas.transform, false);
            ReleaseUiKit.Stretch(releaseVisual.GetComponent<RectTransform>());

            Button back = ReleaseUiComponents.SecondaryButton(_canvas.transform, "Back", "‹",
                new Vector2(0.06f, 0.865f), new Vector2(0.18f, 0.925f), Close, 40);

            Image calendar = ReleaseUiComponents.GlassCard(_canvas.transform, "DailyIcon",
                new Vector2(0.42f, 0.855f), new Vector2(0.58f, 0.935f), ReleaseUiComponents.Violet, true);
            ReleaseUiKit.TextBlock(calendar.transform, "Glyph", "★", 38, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, ReleaseUiComponents.Text, FontStyle.Bold);

            Text title = ReleaseUiKit.TextBlock(_canvas.transform, "Title", "ИСПЫТАНИЕ ДНЯ", 49,
                TextAnchor.MiddleCenter, new Vector2(0.10f, 0.790f), new Vector2(0.90f, 0.855f),
                ReleaseUiComponents.Text, FontStyle.Bold);
            ReleaseUiKit.AddTextShadow(title, 0.50f, -3f);

            _date = ReleaseUiKit.TextBlock(_canvas.transform, "Date", string.Empty, 24,
                TextAnchor.MiddleCenter, new Vector2(0.20f, 0.755f), new Vector2(0.80f, 0.795f),
                ReleaseUiComponents.Muted);

            Image hero = ReleaseUiComponents.GlassCard(_canvas.transform, "DailyHero",
                new Vector2(0.07f, 0.470f), new Vector2(0.93f, 0.735f), ReleaseUiComponents.Cyan, true);

            ReleaseUiKit.TextBlock(hero.transform, "Headline", "ОДИН МАРШРУТ.\nВСЕ ИГРОКИ.\nКТО ТОЧНЕЕ?", 35,
                TextAnchor.MiddleLeft, new Vector2(0.055f, 0.49f), new Vector2(0.68f, 0.91f),
                ReleaseUiComponents.Text, FontStyle.Bold);

            _streak = ReleaseUiComponents.StatTile(hero.transform, "Streak", "★", "0 ДН.", "Текущая серия",
                new Vector2(0.055f, 0.08f), new Vector2(0.345f, 0.39f), ReleaseUiComponents.Gold);
            _best = ReleaseUiComponents.StatTile(hero.transform, "Best", "◆", "—", "Лучший результат",
                new Vector2(0.355f, 0.08f), new Vector2(0.645f, 0.39f), ReleaseUiComponents.Cyan);
            ReleaseUiComponents.StatTile(hero.transform, "Global", "|||", "1", "Маршрут сегодня",
                new Vector2(0.655f, 0.08f), new Vector2(0.945f, 0.39f), ReleaseUiComponents.Violet);

            ReleaseUiComponents.SectionHeader(_canvas.transform, "ВАША ЦЕЛЬ",
                new Vector2(0.07f, 0.418f), new Vector2(0.60f, 0.455f));

            Image goal = ReleaseUiComponents.GlassCard(_canvas.transform, "Goal",
                new Vector2(0.07f, 0.300f), new Vector2(0.93f, 0.410f), ReleaseUiComponents.Gold, false);
            ReleaseUiKit.TextBlock(goal.transform, "Star", "★", 52, TextAnchor.MiddleCenter,
                new Vector2(0.04f, 0.10f), new Vector2(0.24f, 0.90f), ReleaseUiComponents.Gold, FontStyle.Bold);
            ReleaseUiKit.TextBlock(goal.transform, "GoalCopy",
                "Запомните маршрут и повторите его по памяти.\nЧем точнее — тем выше результат!", 23,
                TextAnchor.MiddleLeft, new Vector2(0.27f, 0.13f), new Vector2(0.94f, 0.88f),
                ReleaseUiComponents.Text);

            ReleaseUiComponents.PrimaryButton(_canvas.transform, "Play", "▶  ИГРАТЬ",
                new Vector2(0.07f, 0.200f), new Vector2(0.93f, 0.275f), Play, 32);

            Image reward = ReleaseUiComponents.GlassCard(_canvas.transform, "RewardInfo",
                new Vector2(0.07f, 0.105f), new Vector2(0.93f, 0.175f), ReleaseUiComponents.Violet, false);
            ReleaseUiKit.TextBlock(reward.transform, "Copy", "ЕЖЕДНЕВНЫЙ РЕЗУЛЬТАТ  •  ОБЩИЙ ЧЕЛЛЕНДЖ  •  SHARE", 16,
                TextAnchor.MiddleCenter, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.92f),
                ReleaseUiComponents.Muted, FontStyle.Bold);

            _coins = ReleaseUiComponents.CurrencyPill(_canvas.transform, "Coins", "●", "0",
                new Vector2(0.74f, 0.895f), new Vector2(0.94f, 0.94f), ReleaseUiComponents.Gold);

            var motionRoot = new GameObject("DailyIntroMotion", typeof(RectTransform));
            motionRoot.transform.SetParent(_canvas.transform, false);
            ReleaseUiKit.SetAnchors(motionRoot.GetComponent<RectTransform>(), new Vector2(0.07f, 0.105f), new Vector2(0.93f, 0.855f));
            motionRoot.AddComponent<ReleasePanelMotion>();
        }
    }
}
