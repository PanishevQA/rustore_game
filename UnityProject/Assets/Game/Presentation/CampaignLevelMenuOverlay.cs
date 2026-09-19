using System;
using System.Collections.Generic;
using DontGetSidetracked.Analytics;
using DontGetSidetracked.Core;
using DontGetSidetracked.Economy;
using DontGetSidetracked.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace DontGetSidetracked.Presentation
{
    /// <summary>
    /// Local-only campaign browser. Shows one chapter (10 levels) at a time and never touches network state.
    /// Runtime ownership is resolved when opened/actions run so the persistent overlay never acts on a destroyed bootstrap.
    /// </summary>
    public sealed class CampaignLevelMenuOverlay : MonoBehaviour
    {
        private static CampaignLevelMenuOverlay _instance;

        private GameObject _canvas;
        private GameObject _backdrop;
        private GameObject _panel;
        private Text _title;
        private Text _summary;
        private Text _chapterProgressText;
        private Image _chapterProgressFill;
        private Transform _gridRoot;
        private Button _previous;
        private Button _next;
        private Button _coinHint;
        private GameBootstrap _bootstrap;
        private JsonFileSaveRepository _repository;
        private CampaignProgressService _progress;
        private int _chapter = 1;

        public bool IsOpen => _panel != null && _panel.activeSelf;

        public static void OpenFor(GameBootstrap bootstrap)
        {
            if (_instance == null)
            {
                var root = new GameObject("CampaignLevelMenuOverlay");
                DontDestroyOnLoad(root);
                _instance = root.AddComponent<CampaignLevelMenuOverlay>();
            }
            _instance.Open(bootstrap);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            _repository = new JsonFileSaveRepository();
            BuildUi();
            SetVisible(false);
        }

        private void LateUpdate()
        {
            if (!IsOpen) return;
            if (ResolveLiveBootstrap() == null) Close();
        }

        private void Open(GameBootstrap bootstrap)
        {
            _bootstrap = ResolveLiveBootstrap(bootstrap);
            if (_bootstrap == null || !GameBootstrapRuntimeBridge.IsPlainHome(_bootstrap))
            {
                Close();
                return;
            }

            ReloadProgress();
            _chapter = Mathf.Clamp(
                ((_progress.HighestUnlockedLevel - 1) / CampaignLevelCatalog.LevelsPerChapter) + 1,
                1,
                CampaignLevelCatalog.TotalChapters);
            Render();
            SetVisible(true);
        }

        private void ReloadProgress()
        {
            SaveData save = _repository.Load();
            _progress = new CampaignProgressService(_repository, save);
        }

        private void Render()
        {
            ReloadProgress();
            SaveData save = _progress.Save;
            _title.text = $"ГЛАВА {_chapter} / {CampaignLevelCatalog.TotalChapters}\n{CampaignLevelCatalog.ChapterName(_chapter)}";
            _summary.text =
                $"ПРОЙДЕНО {_progress.CompletedLevels()}/{CampaignLevelCatalog.TotalLevels}  •  " +
                $"★ {_progress.TotalStars()}/{CampaignLevelCatalog.TotalLevels * 3}  •  " +
                $"{save.Coins} МОНЕТ  •  {save.Hints} ПОДСКАЗОК";

            for (int i = _gridRoot.childCount - 1; i >= 0; i--)
                Destroy(_gridRoot.GetChild(i).gameObject);
            CreatePathRail(_gridRoot);

            int first = ((_chapter - 1) * CampaignLevelCatalog.LevelsPerChapter) + 1;
            int last = Math.Min(first + CampaignLevelCatalog.LevelsPerChapter - 1, CampaignLevelCatalog.TotalLevels);
            int chapterCompleted = 0;
            for (int level = first; level <= last; level++)
            {
                int capturedLevel = level;
                bool unlocked = _progress.IsUnlocked(level);
                LevelProgressData record = _progress.GetProgress(level);
                if (record != null && record.Stars > 0) chapterCompleted++;
                CreatePathConnector(_gridRoot, level - first, unlocked, record);
                CreateLevelButton(_gridRoot, level - first, level, unlocked, record, () => SelectLevel(capturedLevel));
            }

            if (_chapterProgressText != null)
                _chapterProgressText.text = $"ПРОГРЕСС ГЛАВЫ  {chapterCompleted}/{CampaignLevelCatalog.LevelsPerChapter}";
            if (_chapterProgressFill != null)
            {
                float chapterRatio = Mathf.Clamp01(chapterCompleted / (float)CampaignLevelCatalog.LevelsPerChapter);
                _chapterProgressFill.rectTransform.anchorMax = new Vector2(Mathf.Max(0.02f, chapterRatio), 1f);
            }

            _previous.interactable = _chapter > 1;
            _next.interactable = _chapter < CampaignLevelCatalog.TotalChapters;
            if (_coinHint != null)
            {
                _coinHint.interactable = save.Coins >= LocalRewardEconomyService.HintPriceCoins;
                Text label = _coinHint.GetComponentInChildren<Text>(true);
                if (label != null)
                    label.text = $"{LocalRewardEconomyService.HintPriceCoins} МОНЕТ → +1 ПОДСКАЗКА";
            }
        }

        private void SelectLevel(int levelNumber)
        {
            ReloadProgress();
            if (!_progress.IsUnlocked(levelNumber)) return;

            GameBootstrap owner = ResolveLiveBootstrap();
            if (owner == null) return;

            Close();
            CampaignRuntimeCoordinator.StartLevel(owner, levelNumber);
        }

        private void PreviousChapter()
        {
            if (_chapter <= 1) return;
            _chapter--;
            Render();
        }

        private void NextChapter()
        {
            if (_chapter >= CampaignLevelCatalog.TotalChapters) return;
            _chapter++;
            Render();
        }

        private void BuyHintWithCoins()
        {
            SaveData save = _repository.Load();
            var economy = new LocalRewardEconomyService(_repository, save);
            if (economy.TryBuyHint())
            {
                AnalyticsLifecycle.Service?.Track(
                    AnalyticsEventNames.CoinSpend,
                    Params(
                        "item", "hint",
                        "coins_spent", LocalRewardEconomyService.HintPriceCoins,
                        "coins_left", economy.Save.Coins,
                        "hints", economy.Save.Hints));
            }
            Render();
        }

        private void GoHome()
        {
            GameBootstrap owner = ResolveLiveBootstrap();
            if (owner == null)
            {
                Close();
                return;
            }

            Close();
            CampaignRuntimeCoordinator.ReturnHome(owner);
        }

        public void Close()
        {
            SetVisible(false);
            _bootstrap = null;
        }

        private GameBootstrap ResolveLiveBootstrap(GameBootstrap preferred = null)
        {
            if (preferred != null)
            {
                _bootstrap = preferred;
                return _bootstrap;
            }

            if (_bootstrap != null) return _bootstrap;
            _bootstrap = FindFirstObjectByType<GameBootstrap>();
            return _bootstrap;
        }

        private void SetVisible(bool visible)
        {
            if (_backdrop != null) _backdrop.SetActive(visible);
            if (_panel != null) _panel.SetActive(visible);
        }

        private void BuildUi()
        {
            _canvas = new GameObject("CampaignCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvas.transform.SetParent(transform, false);
            Canvas canvas = _canvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 70;
            CanvasScaler scaler = _canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            var dim = new GameObject("CampaignBackdrop", typeof(RectTransform), typeof(Image));
            dim.transform.SetParent(_canvas.transform, false);
            _backdrop = dim;
            ReleaseUiKit.Stretch(dim.GetComponent<RectTransform>());
            Image dimImage = dim.GetComponent<Image>();
            dimImage.color = new Color(0.004f, 0.009f, 0.025f, 0.90f);
            dimImage.raycastTarget = true;

            _panel = new GameObject("CampaignPanel", typeof(RectTransform), typeof(Image));
            _panel.transform.SetParent(_canvas.transform, false);
            ReleaseUiKit.SetAnchors(_panel.GetComponent<RectTransform>(), new Vector2(0.035f, 0.035f), new Vector2(0.965f, 0.965f));
            Image panelImage = _panel.GetComponent<Image>();
            panelImage.sprite = ReleaseUiKit.Rounded;
            panelImage.type = Image.Type.Sliced;
            panelImage.color = new Color(0.006f, 0.018f, 0.045f, 0.995f);
            ReleaseUiComponents.Backdrop(_panel.transform, "CampaignReleaseBackdrop");
            _panel.AddComponent<ReleasePanelMotion>();

            var releaseVisual = new GameObject("ReleaseVisual", typeof(RectTransform));
            releaseVisual.transform.SetParent(_panel.transform, false);
            ReleaseUiKit.Stretch(releaseVisual.GetComponent<RectTransform>());

            ReleaseUiComponents.SecondaryButton(_panel.transform, "Back", "‹",
                new Vector2(0.055f, 0.885f), new Vector2(0.16f, 0.945f), GoHome, 40);

            Text kicker = ReleaseUiKit.TextBlock(_panel.transform, "Kicker", "КАМПАНИЯ", 18,
                TextAnchor.MiddleCenter, new Vector2(0.30f, 0.925f), new Vector2(0.70f, 0.962f),
                ReleaseUiComponents.Cyan, FontStyle.Bold);

            _title = ReleaseUiKit.TextBlock(_panel.transform, "Title", string.Empty, 46,
                TextAnchor.MiddleCenter, new Vector2(0.17f, 0.835f), new Vector2(0.83f, 0.925f),
                ReleaseUiComponents.Text, FontStyle.Bold);
            _title.lineSpacing = 0.88f;
            ReleaseUiKit.AddTextShadow(_title, 0.42f, -3f);

            _summary = ReleaseUiKit.TextBlock(_panel.transform, "Summary", string.Empty, 20,
                TextAnchor.MiddleCenter, new Vector2(0.11f, 0.775f), new Vector2(0.89f, 0.835f),
                ReleaseUiComponents.Muted);

            Image progressTrack = ReleaseUiKit.Panel(_panel.transform, "ChapterProgressTrack",
                new Vector2(0.065f, 0.730f), new Vector2(0.935f, 0.748f),
                new Color(0.09f, 0.11f, 0.18f, 0.95f), ReleaseUiKit.Violet, false);
            _chapterProgressFill = ReleaseUiKit.Panel(progressTrack.transform, "ChapterProgressFill",
                Vector2.zero, new Vector2(0.02f, 1f), ReleaseUiKit.Violet, ReleaseUiKit.Violet, false);
            _chapterProgressText = ReleaseUiKit.TextBlock(_panel.transform, "ChapterProgressText", "ПРОГРЕСС ГЛАВЫ  0/10", 20,
                TextAnchor.MiddleRight, new Vector2(0.57f, 0.748f), new Vector2(0.935f, 0.775f),
                new Color(0.72f, 0.66f, 1f, 1f), FontStyle.Bold);

            Image pathSurface = ReleaseUiKit.Panel(_panel.transform, "PathSurface",
                new Vector2(0.045f, 0.220f), new Vector2(0.955f, 0.720f),
                new Color(0.004f, 0.012f, 0.030f, 0.74f), ReleaseUiComponents.Cyan, false);
            pathSurface.raycastTarget = false;

            var grid = new GameObject("LevelPath", typeof(RectTransform));
            grid.transform.SetParent(_panel.transform, false);
            RectTransform gridRect = grid.GetComponent<RectTransform>();
            ReleaseUiKit.SetAnchors(gridRect, new Vector2(0.055f, 0.235f), new Vector2(0.945f, 0.715f));
            _gridRoot = grid.transform;

            _coinHint = ReleaseUiComponents.SecondaryButton(_panel.transform, "CoinHint", "30 МОНЕТ  →  +1 ПОДСКАЗКА",
                new Vector2(0.20f, 0.170f), new Vector2(0.80f, 0.220f),
                BuyHintWithCoins, 22);

            _previous = ReleaseUiKit.Button(_panel.transform, "PreviousChapter", "← ГЛАВА",
                new Vector2(0.055f, 0.085f), new Vector2(0.295f, 0.150f),
                ReleaseUiKit.SurfaceRaised, ReleaseUiKit.Text, 25, PreviousChapter);

            ReleaseUiComponents.PrimaryButton(_panel.transform, "Home", "▶  ИГРАТЬ ТЕКУЩИЙ",
                new Vector2(0.32f, 0.080f), new Vector2(0.68f, 0.155f),
                StartHighestUnlocked, 23);

            _next = ReleaseUiKit.Button(_panel.transform, "NextChapter", "ГЛАВА →",
                new Vector2(0.705f, 0.085f), new Vector2(0.945f, 0.150f),
                ReleaseUiKit.SurfaceRaised, ReleaseUiKit.Text, 25, NextChapter);

            Text hint = ReleaseUiKit.TextBlock(_panel.transform, "Hint",
                "Новые звёзды дают монеты  •  финал главы даёт +1 подсказку", 20,
                TextAnchor.MiddleCenter, new Vector2(0.07f, 0.025f), new Vector2(0.93f, 0.065f),
                ReleaseUiKit.Muted);
            hint.raycastTarget = false;
        }

        private static void CreatePathConnector(
            Transform parent,
            int slot,
            bool unlocked,
            LevelProgressData record)
        {
            if (slot >= CampaignLevelCatalog.LevelsPerChapter - 1) return;

            bool completed = record != null && record.Stars > 0;
            Color accent = !unlocked
                ? new Color(0.26f, 0.34f, 0.44f, 0.24f)
                : completed
                    ? new Color(ReleaseUiComponents.Success.r, ReleaseUiComponents.Success.g, ReleaseUiComponents.Success.b, 0.72f)
                    : new Color(ReleaseUiComponents.Cyan.r, ReleaseUiComponents.Cyan.g, ReleaseUiComponents.Cyan.b, 0.78f);

            CreatePathSegment(parent, "PathProgress", LevelCenter(slot), LevelCenter(slot + 1), accent, 0.010f);
        }

        private static Button CreateLevelButton(
            Transform parent,
            int slot,
            int levelNumber,
            bool unlocked,
            LevelProgressData record,
            UnityEngine.Events.UnityAction action)
        {
            bool completed = record != null && record.Stars > 0;
            bool current = unlocked && !completed;
            Color accent = !unlocked
                ? new Color(0.30f, 0.38f, 0.50f, 0.82f)
                : completed
                    ? ReleaseUiComponents.Success
                    : ReleaseUiComponents.Cyan;

            Vector2 center = LevelCenter(slot);
            var go = new GameObject("LevelNode", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(center.x - 0.205f, center.y - 0.066f);
            rect.anchorMax = new Vector2(center.x + 0.205f, center.y + 0.066f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image image = go.GetComponent<Image>();
            image.sprite = ReleaseUiKit.Rounded;
            image.type = Image.Type.Sliced;
            image.color = !unlocked
                ? new Color(0.016f, 0.027f, 0.050f, 0.93f)
                : current
                    ? new Color(0.020f, 0.110f, 0.170f, 0.995f)
                    : new Color(0.022f, 0.070f, 0.115f, 0.985f);

            Outline outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(accent.r, accent.g, accent.b, current ? 0.58f : unlocked ? 0.28f : 0.10f);
            outline.effectDistance = new Vector2(current ? 3f : 2f, current ? -3f : -2f);

            if (current)
            {
                Shadow glow = go.AddComponent<Shadow>();
                glow.effectColor = new Color(accent.r, accent.g, accent.b, 0.26f);
                glow.effectDistance = new Vector2(0f, -7f);
                glow.useGraphicAlpha = true;
            }

            Button button = go.GetComponent<Button>();
            button.interactable = unlocked;
            button.onClick.AddListener(action);
            button.targetGraphic = image;

            ColorBlock block = button.colors;
            block.normalColor = image.color;
            block.highlightedColor = unlocked ? ReleaseUiKit.Lighten(image.color, 0.05f) : image.color;
            block.pressedColor = unlocked ? ReleaseUiKit.Darken(image.color, 0.08f) : image.color;
            block.disabledColor = image.color;
            block.fadeDuration = 0.08f;
            button.colors = block;

            ReleaseUiKit.TextBlock(go.transform, "LevelNumber", levelNumber.ToString(), 40,
                TextAnchor.MiddleCenter, new Vector2(0.035f, 0.12f), new Vector2(0.28f, 0.88f),
                unlocked ? ReleaseUiComponents.Text : new Color(0.45f, 0.50f, 0.60f, 0.88f), FontStyle.Bold);

            string stars = !unlocked ? "ЗАКРЫТ" : current ? "ТЕКУЩИЙ" : Stars(record.Stars);
            ReleaseUiKit.TextBlock(go.transform, "State", stars, 20,
                TextAnchor.MiddleLeft, new Vector2(0.31f, 0.50f), new Vector2(0.95f, 0.88f),
                accent, FontStyle.Bold);

            string best = !unlocked ? "Нужен предыдущий уровень" :
                current ? "Твой следующий маршрут" : $"ЛУЧШИЙ  {record.BestScore:0.0}%";
            ReleaseUiKit.TextBlock(go.transform, "Best", best, 17,
                TextAnchor.MiddleLeft, new Vector2(0.31f, 0.11f), new Vector2(0.95f, 0.50f),
                unlocked ? ReleaseUiComponents.Muted : new Color(0.40f, 0.45f, 0.54f, 0.88f));

            return button;
        }

        private static void CreatePathRail(Transform parent)
        {
            for (int slot = 0; slot < CampaignLevelCatalog.LevelsPerChapter - 1; slot++)
            {
                CreatePathSegment(
                    parent,
                    "PathRail",
                    LevelCenter(slot),
                    LevelCenter(slot + 1),
                    new Color(ReleaseUiComponents.Cyan.r, ReleaseUiComponents.Cyan.g, ReleaseUiComponents.Cyan.b, 0.12f),
                    0.016f);
            }
        }

        private static Vector2 LevelCenter(int slot)
        {
            int row = slot / 2;
            bool firstInRow = slot % 2 == 0;
            bool left = row % 2 == 0 ? firstInRow : !firstInRow;
            float x = left ? 0.24f : 0.76f;
            float y = 0.885f - row * 0.192f;
            return new Vector2(x, y);
        }

        private static void CreatePathSegment(
            Transform parent,
            string name,
            Vector2 a,
            Vector2 b,
            Color color,
            float thickness)
        {
            Vector2 min;
            Vector2 max;

            if (Mathf.Abs(a.y - b.y) < 0.001f)
            {
                min = new Vector2(Mathf.Min(a.x, b.x), a.y - thickness * 0.5f);
                max = new Vector2(Mathf.Max(a.x, b.x), a.y + thickness * 0.5f);
            }
            else
            {
                min = new Vector2(a.x - thickness * 0.5f, Mathf.Min(a.y, b.y));
                max = new Vector2(a.x + thickness * 0.5f, Mathf.Max(a.y, b.y));
            }

            Transform segment = ReleaseUiKit.Rect(parent, name, min, max);
            Image line = segment.gameObject.AddComponent<Image>();
            line.sprite = ReleaseUiKit.Rounded;
            line.type = Image.Type.Sliced;
            line.color = color;
            line.raycastTarget = false;
            segment.SetAsFirstSibling();
        }

        private void StartHighestUnlocked()
        {
            ReloadProgress();
            int level = Mathf.Clamp(_progress.HighestUnlockedLevel, 1, CampaignLevelCatalog.TotalLevels);
            SelectLevel(level);
        }

        private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static string Stars(int count)
        {
            if (count >= 3) return "★ ★ ★";
            if (count == 2) return "★ ★ ☆";
            if (count == 1) return "★ ☆ ☆";
            return "☆ ☆ ☆";
        }

        private static Dictionary<string, object> Params(params object[] values)
        {
            var result = new Dictionary<string, object>();
            for (int i = 0; i + 1 < values.Length; i += 2)
            {
                string key = values[i]?.ToString();
                if (!string.IsNullOrWhiteSpace(key)) result[key] = values[i + 1];
            }
            return result;
        }
    }
}
