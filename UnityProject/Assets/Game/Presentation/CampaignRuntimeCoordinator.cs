using System;
using DontGetSidetracked.Analytics;
using DontGetSidetracked.Core;
using DontGetSidetracked.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace DontGetSidetracked.Presentation
{
    /// <summary>
    /// Campaign presentation adapter. Reuses the proven route input/scoring loop in GameBootstrap
    /// while campaign catalog/progression remain pure C# in Game.Gameplay.
    /// </summary>
    [DefaultExecutionOrder(2600)]
    public sealed class CampaignRuntimeCoordinator : MonoBehaviour
    {
        private static CampaignRuntimeCoordinator _instance;

        public static bool IsCampaignActive => _instance != null && _instance._campaignActive;
        public static int CurrentLevelNumber => _instance?._level?.LevelNumber ?? 0;
        public static int CurrentChapterNumber => _instance?._level?.ChapterNumber ?? 0;

        private GameBootstrap _bootstrap;
        private JsonFileSaveRepository _repository;
        private CampaignProgressService _progress;
        private CampaignLevelDefinition _level;
        private bool _campaignActive;
        private bool _resultHandled;
        private bool _homeWired;
        private RectTransform _statsButton;
        private RectTransform _storeButton;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            EnsureInstance();
        }

        public static void StartLevel(GameBootstrap bootstrap, int levelNumber)
        {
            EnsureInstance();
            _instance.BeginLevel(bootstrap, levelNumber);
        }

        public static void ReturnHome(GameBootstrap bootstrap)
        {
            EnsureInstance();
            _instance.ReturnToHome(bootstrap);
        }

        private static void EnsureInstance()
        {
            if (_instance != null) return;
            _instance = FindFirstObjectByType<CampaignRuntimeCoordinator>();
            if (_instance != null) return;
            var root = new GameObject("CampaignRuntimeCoordinator");
            DontDestroyOnLoad(root);
            _instance = root.AddComponent<CampaignRuntimeCoordinator>();
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
            ResolveBootstrap();
        }

        private void LateUpdate()
        {
            if (_bootstrap == null) ResolveBootstrap();
            if (_bootstrap == null) return;

            if (_campaignActive)
            {
                MaintainCampaignPresentation();
                return;
            }

            MaintainHomeEntry();
        }

        private void BeginLevel(GameBootstrap bootstrap, int levelNumber)
        {
            if (bootstrap != null && bootstrap != _bootstrap) ResolveBootstrap(bootstrap);
            if (_bootstrap == null) return;

            SaveData save = GameBootstrapRuntimeBridge.Save(_bootstrap) ?? _repository.Load();
            _progress = new CampaignProgressService(_repository, save);
            if (!_progress.IsUnlocked(levelNumber)) return;

            CampaignLevelDefinition level = CampaignLevelCatalog.Get(levelNumber);
            if (!GameBootstrapRuntimeBridge.PrepareCampaign(_bootstrap)) return;

            _level = level;
            _campaignActive = true;
            _resultHandled = false;
            _homeWired = false;

            Text title = GameBootstrapRuntimeBridge.Title(_bootstrap);
            Text status = GameBootstrapRuntimeBridge.Status(_bootstrap);
            if (title != null) title.text = LevelTitle();
            if (status != null) status.text = "ГОТОВЬСЯ";

            RouteDefinition route = _level.BuildRoute(new RouteGenerator());
            if (!GameBootstrapRuntimeBridge.BeginRoute(_bootstrap, route))
            {
                _campaignActive = false;
                _resultHandled = false;
                _level = null;
                GameBootstrapRuntimeBridge.ShowHome(_bootstrap);
                return;
            }

            AnalyticsLifecycle.Service?.Track("level_start", Params(
                "level", _level.LevelNumber,
                "chapter", _level.ChapterNumber,
                "difficulty", _level.Difficulty.ToString(),
                "display_time_ms", _level.DisplayTimeMs));
        }

        private void MaintainCampaignPresentation()
        {
            if (GameBootstrapRuntimeBridge.IsResult(_bootstrap))
            {
                if (!_resultHandled) HandleCampaignResult();
                return;
            }

            if (GameBootstrapRuntimeBridge.IsActiveRound(_bootstrap))
            {
                Text title = GameBootstrapRuntimeBridge.Title(_bootstrap);
                if (title != null) title.text = LevelTitle();
            }
        }

        private void HandleCampaignResult()
        {
            _resultHandled = true;
            double score = GameBootstrapRuntimeBridge.LastResultScore(_bootstrap);
            SaveData save = GameBootstrapRuntimeBridge.Save(_bootstrap) ?? _repository.Load();
            _progress = new CampaignProgressService(_repository, save);
            CampaignLevelCompletion completion = _progress.RecordResult(_level.LevelNumber, score);
            GameBootstrapRuntimeBridge.ReplaceSave(_bootstrap, _progress.Save);

            bool campaignFinished = _level.LevelNumber == CampaignLevelCatalog.TotalLevels && completion.Stars >= 1;
            Text status = GameBootstrapRuntimeBridge.Status(_bootstrap);
            if (status != null)
            {
                string best = completion.NewBest ? " • НОВЫЙ РЕКОРД" : string.Empty;
                string rewards = string.Empty;
                if (completion.CoinsAwarded > 0) rewards += $"\n+{completion.CoinsAwarded} МОНЕТ";
                if (completion.HintsAwarded > 0) rewards += $"  •  +{completion.HintsAwarded} ПОДСКАЗКА";
                if (completion.NextLevelUnlocked) rewards += $"\nОТКРЫТ УРОВЕНЬ {completion.HighestUnlockedLevel}";
                if (_level.LevelNumber % CampaignLevelCatalog.LevelsPerChapter == 0 && completion.Stars >= 1)
                    rewards += $"\nГЛАВА {_level.ChapterNumber} ПРОЙДЕНА";
                if (campaignFinished)
                    rewards += $"\nКАМПАНИЯ ЗАВЕРШЕНА • ★ {_progress.TotalStars()}/{CampaignLevelCatalog.TotalLevels * 3}";

                status.text =
                    $"УРОВЕНЬ {_level.LevelNumber}  •  {completion.Score:0.0}%\n" +
                    $"{Stars(completion.Stars)}{best}{rewards}";
            }

            Button primary = GameBootstrapRuntimeBridge.PrimaryButton(_bootstrap);
            Button secondary = GameBootstrapRuntimeBridge.SecondaryButton(_bootstrap);
            Button share = GameBootstrapRuntimeBridge.ShareButton(_bootstrap);

            if (campaignFinished)
                Configure(primary, "К УРОВНЯМ", OpenLevelMenu);
            else if (completion.Stars >= 1 && _level.LevelNumber < CampaignLevelCatalog.TotalLevels)
                Configure(primary, "СЛЕДУЮЩИЙ УРОВЕНЬ", () => BeginLevel(_bootstrap, _level.LevelNumber + 1));
            else
                Configure(primary, "ПОВТОРИТЬ", () => BeginLevel(_bootstrap, _level.LevelNumber));

            Configure(secondary, "ВЫБОР УРОВНЕЙ", OpenLevelMenu);
            if (share != null)
            {
                Configure(share, "ПОДЕЛИТЬСЯ", ShareCurrentResult);
                share.gameObject.SetActive(true);
            }

            AnalyticsLifecycle.Service?.Track("level_complete", Params(
                "level", _level.LevelNumber,
                "chapter", _level.ChapterNumber,
                "score", completion.Score,
                "stars", completion.Stars,
                "new_best", completion.NewBest,
                "new_stars", completion.NewStars,
                "coins_awarded", completion.CoinsAwarded,
                "hints_awarded", completion.HintsAwarded,
                "unlocked_next", completion.NextLevelUnlocked,
                "campaign_finished", campaignFinished));
        }

        private void OpenLevelMenu()
        {
            CampaignLevelMenuOverlay.OpenFor(_bootstrap);
        }

        private void ShareCurrentResult()
        {
            GameBootstrapRuntimeBridge.ShareCurrentResult(_bootstrap);
        }

        private void ReturnToHome(GameBootstrap bootstrap)
        {
            if (bootstrap != null && bootstrap != _bootstrap) ResolveBootstrap(bootstrap);
            _campaignActive = false;
            _resultHandled = false;
            _level = null;
            GameBootstrapRuntimeBridge.ShowHome(_bootstrap);
        }

        private void MaintainHomeEntry()
        {
            Text title = GameBootstrapRuntimeBridge.Title(_bootstrap);
            if (!GameBootstrapRuntimeBridge.IsHome(_bootstrap) ||
                title == null || !string.Equals(title.text, "НЕ СБЕЙСЯ!", StringComparison.Ordinal))
            {
                _homeWired = false;
                return;
            }

            Button primary = GameBootstrapRuntimeBridge.PrimaryButton(_bootstrap);
            Button secondary = GameBootstrapRuntimeBridge.SecondaryButton(_bootstrap);
            Button share = GameBootstrapRuntimeBridge.ShareButton(_bootstrap);
            if (primary == null || secondary == null || share == null) return;

            if (!_homeWired)
            {
                Configure(primary, "УРОВНИ", () => CampaignLevelMenuOverlay.OpenFor(_bootstrap));
                Configure(share, "DAILY", StartDaily);
                _homeWired = true;
            }

            SetAnchors(primary.GetComponent<RectTransform>(), new Vector2(0.075f, 0.205f), new Vector2(0.925f, 0.270f));
            SetAnchors(share.GetComponent<RectTransform>(), new Vector2(0.075f, 0.135f), new Vector2(0.925f, 0.195f));
            share.gameObject.SetActive(true);

            SetAnchors(secondary.GetComponent<RectTransform>(), new Vector2(0.075f, 0.045f), new Vector2(0.350f, 0.105f));
            ResolveMetaButtons();
            if (_statsButton != null) SetAnchors(_statsButton, new Vector2(0.365f, 0.045f), new Vector2(0.635f, 0.105f));
            if (_storeButton != null) SetAnchors(_storeButton, new Vector2(0.650f, 0.045f), new Vector2(0.925f, 0.105f));

            SaveData save = GameBootstrapRuntimeBridge.Save(_bootstrap);
            if (save != null)
            {
                var progress = new CampaignProgressService(_repository, save);
                Text status = GameBootstrapRuntimeBridge.Status(_bootstrap);
                if (status != null)
                    status.text = $"ПРОЙДЕНО {progress.CompletedLevels()}/{CampaignLevelCatalog.TotalLevels}  •  ★ {progress.TotalStars()}\nМОНЕТЫ {save.Coins}  •  DAILY {save.Streak}д";
            }
        }

        private void StartDaily()
        {
            _homeWired = false;
            GameBootstrapRuntimeBridge.StartDaily(_bootstrap);
        }

        private void ResolveMetaButtons()
        {
            if (_statsButton != null && _storeButton != null) return;
            GameObject meta = GameObject.Find("MetaCanvas");
            if (meta == null) return;
            Button[] buttons = meta.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Text label = buttons[i].GetComponentInChildren<Text>(true);
                if (label == null) continue;
                if (label.text.IndexOf("СТАТИСТИКА", StringComparison.OrdinalIgnoreCase) >= 0)
                    _statsButton = buttons[i].GetComponent<RectTransform>();
                else if (label.text.IndexOf("МАГАЗИН", StringComparison.OrdinalIgnoreCase) >= 0)
                    _storeButton = buttons[i].GetComponent<RectTransform>();
            }
        }

        private void ResolveBootstrap(GameBootstrap explicitBootstrap = null)
        {
            _bootstrap = explicitBootstrap != null ? explicitBootstrap : FindFirstObjectByType<GameBootstrap>();
        }

        private static void Configure(Button button, string label, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            button.gameObject.SetActive(true);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
            Text text = button.GetComponentInChildren<Text>(true);
            if (text != null) text.text = label;
        }

        private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
        {
            if (rect == null) return;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private string LevelTitle() => _level == null
            ? "УРОВЕНЬ"
            : $"УРОВЕНЬ {_level.LevelNumber}  •  {CampaignLevelCatalog.ChapterName(_level.ChapterNumber)}";

        private static string Stars(int count)
        {
            if (count >= 3) return "★ ★ ★";
            if (count == 2) return "★ ★ ☆";
            if (count == 1) return "★ ☆ ☆";
            return "☆ ☆ ☆";
        }

        private static System.Collections.Generic.Dictionary<string, object> Params(params object[] pairs)
        {
            var result = new System.Collections.Generic.Dictionary<string, object>();
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                string key = pairs[i]?.ToString();
                if (!string.IsNullOrWhiteSpace(key)) result[key] = pairs[i + 1];
            }
            return result;
        }
    }
}
