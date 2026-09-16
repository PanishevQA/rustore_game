using System;
using System.Collections;
using System.Reflection;
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

        private GameBootstrap _bootstrap;
        private Type _bootstrapType;
        private FieldInfo _modeField;
        private FieldInfo _stateField;
        private FieldInfo _dailyCompletedField;
        private FieldInfo _duelSessionField;
        private FieldInfo _dailySessionField;
        private FieldInfo _dailyField;
        private FieldInfo _saveField;
        private FieldInfo _titleField;
        private FieldInfo _statusField;
        private FieldInfo _primaryField;
        private FieldInfo _secondaryField;
        private FieldInfo _shareField;
        private FieldInfo _lastResultScoreField;
        private MethodInfo _beginRouteMethod;
        private MethodInfo _showHomeMethod;
        private MethodInfo _startDailyMethod;
        private MethodInfo _shareResultMethod;

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
            if (_bootstrap == null || _beginRouteMethod == null) return;

            SaveData save = GetSave() ?? _repository.Load();
            _progress = new CampaignProgressService(_repository, save);
            if (!_progress.IsUnlocked(levelNumber)) return;

            _level = CampaignLevelCatalog.Get(levelNumber);
            _campaignActive = true;
            _resultHandled = false;
            _homeWired = false;

            SetEnum(_modeField, "Training");
            SetEnum(_stateField, "Idle");
            _dailyCompletedField?.SetValue(_bootstrap, false);
            _duelSessionField?.SetValue(_bootstrap, null);
            _dailySessionField?.SetValue(_bootstrap, null);
            _dailyField?.SetValue(_bootstrap, null);

            Text title = Get<Text>(_titleField);
            Text status = Get<Text>(_statusField);
            if (title != null) title.text = LevelTitle();
            if (status != null) status.text = "ГОТОВЬСЯ";

            RouteDefinition route = _level.BuildRoute(new RouteGenerator());
            object routineObject = _beginRouteMethod.Invoke(_bootstrap, new object[] { route });
            if (routineObject is IEnumerator routine)
                _bootstrap.StartCoroutine(routine);

            AnalyticsLifecycle.Service?.Track("level_start", Params(
                "level", _level.LevelNumber,
                "chapter", _level.ChapterNumber,
                "difficulty", _level.Difficulty.ToString(),
                "display_time_ms", _level.DisplayTimeMs));
        }

        private void MaintainCampaignPresentation()
        {
            string state = EnumName(_stateField);
            if (string.Equals(state, "Showing", StringComparison.Ordinal) ||
                string.Equals(state, "Drawing", StringComparison.Ordinal))
            {
                Text title = Get<Text>(_titleField);
                if (title != null) title.text = LevelTitle();
                return;
            }

            if (string.Equals(state, "Result", StringComparison.Ordinal) && !_resultHandled)
                HandleCampaignResult();
        }

        private void HandleCampaignResult()
        {
            _resultHandled = true;
            double score = GetDouble(_lastResultScoreField);
            SaveData save = GetSave() ?? _repository.Load();
            _progress = new CampaignProgressService(_repository, save);
            CampaignLevelCompletion completion = _progress.RecordResult(_level.LevelNumber, score);
            if (_saveField != null) _saveField.SetValue(_bootstrap, _progress.Save);

            Text status = Get<Text>(_statusField);
            if (status != null)
            {
                string unlocked = completion.NextLevelUnlocked
                    ? $"\nОТКРЫТ УРОВЕНЬ {completion.HighestUnlockedLevel}"
                    : string.Empty;
                string best = completion.NewBest ? " • НОВЫЙ РЕКОРД" : string.Empty;
                status.text =
                    $"УРОВЕНЬ {_level.LevelNumber}  •  {completion.Score:0.0}%\n" +
                    $"{Stars(completion.Stars)}{best}{unlocked}";
            }

            Button primary = Get<Button>(_primaryField);
            Button secondary = Get<Button>(_secondaryField);
            Button share = Get<Button>(_shareField);

            if (completion.Stars >= 1 && _level.LevelNumber < CampaignLevelCatalog.TotalLevels)
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
                "unlocked_next", completion.NextLevelUnlocked));
        }

        private void OpenLevelMenu()
        {
            CampaignLevelMenuOverlay.OpenFor(_bootstrap);
        }

        private void ShareCurrentResult()
        {
            _shareResultMethod?.Invoke(_bootstrap, null);
        }

        private void ReturnToHome(GameBootstrap bootstrap)
        {
            if (bootstrap != null && bootstrap != _bootstrap) ResolveBootstrap(bootstrap);
            _campaignActive = false;
            _resultHandled = false;
            _showHomeMethod?.Invoke(_bootstrap, null);
        }

        private void MaintainHomeEntry()
        {
            string mode = EnumName(_modeField);
            Text title = Get<Text>(_titleField);
            if (!string.Equals(mode, "Home", StringComparison.Ordinal) ||
                title == null || !string.Equals(title.text, "НЕ СБЕЙСЯ!", StringComparison.Ordinal))
            {
                _homeWired = false;
                return;
            }

            Button primary = Get<Button>(_primaryField);
            Button secondary = Get<Button>(_secondaryField);
            Button share = Get<Button>(_shareField);
            if (primary == null || secondary == null || share == null) return;

            if (!_homeWired)
            {
                Configure(primary, "УРОВНИ", () => CampaignLevelMenuOverlay.OpenFor(_bootstrap));
                Configure(share, "DAILY", StartDaily);
                _homeWired = true;
            }

            // HomePolish owns the general portrait layout. This adapter only makes room for a second core CTA.
            SetAnchors(primary.GetComponent<RectTransform>(), new Vector2(0.075f, 0.205f), new Vector2(0.925f, 0.270f));
            SetAnchors(share.GetComponent<RectTransform>(), new Vector2(0.075f, 0.135f), new Vector2(0.925f, 0.195f));
            share.gameObject.SetActive(true);

            SetAnchors(secondary.GetComponent<RectTransform>(), new Vector2(0.075f, 0.045f), new Vector2(0.350f, 0.105f));
            ResolveMetaButtons();
            if (_statsButton != null) SetAnchors(_statsButton, new Vector2(0.365f, 0.045f), new Vector2(0.635f, 0.105f));
            if (_storeButton != null) SetAnchors(_storeButton, new Vector2(0.650f, 0.045f), new Vector2(0.925f, 0.105f));

            SaveData save = GetSave();
            if (save != null)
            {
                var progress = new CampaignProgressService(_repository, save);
                Text status = Get<Text>(_statusField);
                if (status != null)
                    status.text = $"УРОВЕНЬ {progress.HighestUnlockedLevel}/{CampaignLevelCatalog.TotalLevels}  •  ★ {progress.TotalStars()}  •  DAILY {_saveStreak(save)}д";
            }
        }

        private void StartDaily()
        {
            _homeWired = false;
            _startDailyMethod?.Invoke(_bootstrap, null);
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
            if (_bootstrap == null) return;
            _bootstrapType = typeof(GameBootstrap);
            BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            _modeField = _bootstrapType.GetField("_mode", flags);
            _stateField = _bootstrapType.GetField("_state", flags);
            _dailyCompletedField = _bootstrapType.GetField("_dailyCompleted", flags);
            _duelSessionField = _bootstrapType.GetField("_duelSession", flags);
            _dailySessionField = _bootstrapType.GetField("_dailySession", flags);
            _dailyField = _bootstrapType.GetField("_daily", flags);
            _saveField = _bootstrapType.GetField("_save", flags);
            _titleField = _bootstrapType.GetField("_title", flags);
            _statusField = _bootstrapType.GetField("_status", flags);
            _primaryField = _bootstrapType.GetField("_primary", flags);
            _secondaryField = _bootstrapType.GetField("_secondary", flags);
            _shareField = _bootstrapType.GetField("_share", flags);
            _lastResultScoreField = _bootstrapType.GetField("_lastResultScore", flags);
            _beginRouteMethod = _bootstrapType.GetMethod("BeginRoute", flags);
            _showHomeMethod = _bootstrapType.GetMethod("ShowHome", flags);
            _startDailyMethod = _bootstrapType.GetMethod("StartDaily", flags);
            _shareResultMethod = _bootstrapType.GetMethod("ShareCurrentResult", flags);
        }

        private SaveData GetSave() => _saveField?.GetValue(_bootstrap) as SaveData;

        private T Get<T>(FieldInfo field) where T : class => field?.GetValue(_bootstrap) as T;

        private double GetDouble(FieldInfo field)
        {
            object value = field?.GetValue(_bootstrap);
            return value == null ? 0.0 : Convert.ToDouble(value);
        }

        private string EnumName(FieldInfo field)
        {
            object value = field?.GetValue(_bootstrap);
            return value?.ToString() ?? string.Empty;
        }

        private void SetEnum(FieldInfo field, string name)
        {
            if (field == null) return;
            object value = Enum.Parse(field.FieldType, name);
            field.SetValue(_bootstrap, value);
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
            : $"УРОВЕНЬ {_level.LevelNumber}  •  ГЛАВА {_level.ChapterNumber}";

        private static string Stars(int count)
        {
            if (count >= 3) return "★ ★ ★";
            if (count == 2) return "★ ★ ☆";
            if (count == 1) return "★ ☆ ☆";
            return "☆ ☆ ☆";
        }

        private static int _saveStreak(SaveData save) => save == null ? 0 : save.Streak;

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
