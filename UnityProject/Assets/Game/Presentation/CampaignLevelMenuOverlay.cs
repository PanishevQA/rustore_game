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
    /// </summary>
    public sealed class CampaignLevelMenuOverlay : MonoBehaviour
    {
        private static CampaignLevelMenuOverlay _instance;

        private GameObject _canvas;
        private GameObject _panel;
        private Text _title;
        private Text _summary;
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
            if (bootstrap == null) return;
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

        private void Open(GameBootstrap bootstrap)
        {
            _bootstrap = bootstrap;
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
                $"Пройдено {_progress.CompletedLevels()}/{CampaignLevelCatalog.TotalLevels}  •  " +
                $"★ {_progress.TotalStars()}/{CampaignLevelCatalog.TotalLevels * 3}\n" +
                $"Монеты: {save.Coins}  •  Подсказки: {save.Hints}";

            for (int i = _gridRoot.childCount - 1; i >= 0; i--)
                Destroy(_gridRoot.GetChild(i).gameObject);

            int first = ((_chapter - 1) * CampaignLevelCatalog.LevelsPerChapter) + 1;
            int last = Math.Min(first + CampaignLevelCatalog.LevelsPerChapter - 1, CampaignLevelCatalog.TotalLevels);
            for (int level = first; level <= last; level++)
            {
                int capturedLevel = level;
                bool unlocked = _progress.IsUnlocked(level);
                LevelProgressData record = _progress.GetProgress(level);
                string label;
                if (!unlocked)
                {
                    label = $"{level:00}\nЗАКРЫТ";
                }
                else if (record == null || record.Stars <= 0)
                {
                    label = $"{level:00}\n☆ ☆ ☆";
                }
                else
                {
                    label = $"{level:00}\n{Stars(record.Stars)}\n{record.BestScore:0.0}%";
                }

                Button button = CreateLevelButton(_gridRoot, label, () => SelectLevel(capturedLevel));
                button.interactable = unlocked;
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
            SetVisible(false);
            CampaignRuntimeCoordinator.StartLevel(_bootstrap, levelNumber);
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
            SetVisible(false);
            CampaignRuntimeCoordinator.ReturnHome(_bootstrap);
        }

        public void Close() => SetVisible(false);

        private void SetVisible(bool visible)
        {
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

            _panel = new GameObject("CampaignPanel", typeof(RectTransform), typeof(Image));
            _panel.transform.SetParent(_canvas.transform, false);
            SetAnchors(_panel.GetComponent<RectTransform>(), new Vector2(0.035f, 0.035f), new Vector2(0.965f, 0.965f));
            _panel.GetComponent<Image>().color = new Color(0.018f, 0.028f, 0.060f, 0.995f);

            _title = CreateText(_panel.transform, "Title", 56, TextAnchor.MiddleCenter,
                new Vector2(0.06f, 0.875f), new Vector2(0.94f, 0.965f));
            _title.fontStyle = FontStyle.Bold;

            _summary = CreateText(_panel.transform, "Summary", 27, TextAnchor.MiddleCenter,
                new Vector2(0.06f, 0.800f), new Vector2(0.94f, 0.875f));
            _summary.color = new Color(0.62f, 0.72f, 0.86f, 1f);

            var grid = new GameObject("LevelGrid", typeof(RectTransform), typeof(GridLayoutGroup));
            grid.transform.SetParent(_panel.transform, false);
            RectTransform gridRect = grid.GetComponent<RectTransform>();
            SetAnchors(gridRect, new Vector2(0.07f, 0.245f), new Vector2(0.93f, 0.785f));
            GridLayoutGroup layout = grid.GetComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(400f, 150f);
            layout.spacing = new Vector2(28f, 18f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 2;
            layout.childAlignment = TextAnchor.UpperCenter;
            _gridRoot = grid.transform;

            _coinHint = CreateButton(_panel.transform, "МОНЕТЫ → ПОДСКАЗКА", new Vector2(0.22f, 0.185f), new Vector2(0.78f, 0.235f), BuyHintWithCoins);
            _previous = CreateButton(_panel.transform, "← ГЛАВА", new Vector2(0.07f, 0.105f), new Vector2(0.31f, 0.165f), PreviousChapter);
            CreateButton(_panel.transform, "ДОМОЙ", new Vector2(0.38f, 0.105f), new Vector2(0.62f, 0.165f), GoHome);
            _next = CreateButton(_panel.transform, "ГЛАВА →", new Vector2(0.69f, 0.105f), new Vector2(0.93f, 0.165f), NextChapter);

            Text hint = CreateText(_panel.transform, "Hint", 23, TextAnchor.MiddleCenter,
                new Vector2(0.08f, 0.045f), new Vector2(0.92f, 0.095f));
            hint.text = "Новые звёзды дают монеты • финал главы даёт +1 подсказку";
            hint.color = new Color(0.45f, 0.88f, 1f, 1f);
        }

        private static Button CreateLevelButton(Transform parent, string label, UnityEngine.Events.UnityAction action)
        {
            var go = new GameObject("Level", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = new Color(0.07f, 0.10f, 0.17f, 1f);
            Button button = go.GetComponent<Button>();
            button.onClick.AddListener(action);
            Text text = CreateText(go.transform, "Label", 32, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
            text.text = label;
            text.fontStyle = FontStyle.Bold;
            text.raycastTarget = false;
            return button;
        }

        private static Button CreateButton(Transform parent, string label, Vector2 min, Vector2 max, UnityEngine.Events.UnityAction action)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            SetAnchors(go.GetComponent<RectTransform>(), min, max);
            go.GetComponent<Image>().color = new Color(0.10f, 0.16f, 0.25f, 1f);
            Button button = go.GetComponent<Button>();
            button.onClick.AddListener(action);
            Text text = CreateText(go.transform, "Label", 26, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
            text.text = label;
            text.fontStyle = FontStyle.Bold;
            text.raycastTarget = false;
            return button;
        }

        private static Text CreateText(Transform parent, string name, int size, TextAnchor alignment, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            SetAnchors(go.GetComponent<RectTransform>(), min, max);
            Text text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Color.white;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 16;
            text.resizeTextMaxSize = size;
            return text;
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
