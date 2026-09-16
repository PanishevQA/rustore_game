using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DontGetSidetracked.Analytics;
using DontGetSidetracked.Core;
using DontGetSidetracked.Daily;
using DontGetSidetracked.Gameplay;
using DontGetSidetracked.Network;
using DontGetSidetracked.Social;
using UnityEngine;
using UnityEngine.UI;

namespace DontGetSidetracked.Presentation
{
    /// <summary>
    /// Adds result-only presentation without changing the input/scoring loop:
    /// score medals, perfect pulse, per-Daily personal records, PNG share cards and Duel rematch UX.
    /// </summary>
    public sealed class ResultEnhancementCoordinator : MonoBehaviour
    {
        private GameBootstrap _bootstrap;
        private Type _bootstrapType;
        private FieldInfo _stateField;
        private FieldInfo _modeField;
        private FieldInfo _dailyCompletedField;
        private FieldInfo _lastResultScoreField;
        private FieldInfo _lastDailyScoreField;
        private FieldInfo _dailyField;
        private FieldInfo _routeField;
        private FieldInfo _recordingField;
        private FieldInfo _duelSessionField;
        private FieldInfo _apiField;
        private FieldInfo _primaryField;
        private FieldInfo _saveField;

        private readonly DailyBestService _dailyBest = new DailyBestService();
        private JsonFileSaveRepository _saveRepository;
        private GameObject _canvas;
        private Text _badge;
        private Text _recordLabel;
        private Button _cardButton;
        private Text _cardButtonText;
        private Image _flash;
        private bool _wasResult;
        private bool _shareBusy;
        private Coroutine _perfectRoutine;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (FindFirstObjectByType<ResultEnhancementCoordinator>() != null) return;
            var root = new GameObject("ResultEnhancementCoordinator");
            DontDestroyOnLoad(root);
            root.AddComponent<ResultEnhancementCoordinator>();
        }

        private void Awake()
        {
            _saveRepository = new JsonFileSaveRepository();
            BuildUi();
            ResolveBootstrap();
            SetResultUiVisible(false);
        }

        private void Update()
        {
            if (_bootstrap == null) ResolveBootstrap();
            if (_bootstrap == null || _stateField == null) return;

            bool isResult = string.Equals(GetEnumName(_stateField), "Result", StringComparison.Ordinal);
            if (!isResult)
            {
                if (_wasResult)
                {
                    _wasResult = false;
                    SetResultUiVisible(false);
                    if (_perfectRoutine != null)
                    {
                        StopCoroutine(_perfectRoutine);
                        _perfectRoutine = null;
                    }
                    if (_flash != null) _flash.color = new Color(0.45f, 0.8f, 1f, 0f);
                }
                return;
            }

            if (_wasResult) return;
            _wasResult = true;
            HandleEnteredResult();
        }

        private void HandleEnteredResult()
        {
            string mode = GetEnumName(_modeField);
            bool dailyCompleted = GetBool(_dailyCompletedField);
            bool aggregate = dailyCompleted &&
                             (string.Equals(mode, "Daily", StringComparison.Ordinal) ||
                              string.Equals(mode, "Duel", StringComparison.Ordinal));
            double score = aggregate ? GetDouble(_lastDailyScoreField) : GetDouble(_lastResultScoreField);
            ScoreCelebration celebration = ScoreCelebrationPolicy.Evaluate(score);

            _badge.text = string.IsNullOrWhiteSpace(celebration.Icon)
                ? celebration.Label
                : celebration.Icon + "  " + celebration.Label;
            _badge.color = BadgeColor(celebration.Tier);
            _recordLabel.text = string.Empty;
            SetResultUiVisible(true);

            if (celebration.Tier != ScoreMedalTier.None)
            {
                AnalyticsLifecycle.Service?.Track(AnalyticsEventNames.MedalEarned, Params(
                    "tier", celebration.Tier.ToString(),
                    "score", score,
                    "mode", mode));
            }

            if (string.Equals(mode, "Daily", StringComparison.Ordinal) && dailyCompleted)
                ApplyDailyRecord(score);

            bool shareable = string.Equals(mode, "Training", StringComparison.Ordinal) || aggregate;
            _cardButton.gameObject.SetActive(shareable);

            if (string.Equals(mode, "Duel", StringComparison.Ordinal) && dailyCompleted)
                ConfigureRematch(score);

            if (celebration.PlayPerfectEffect)
            {
                if (_perfectRoutine != null) StopCoroutine(_perfectRoutine);
                _perfectRoutine = StartCoroutine(PerfectPulse());
            }
        }

        private void ApplyDailyRecord(double score)
        {
            DailyChallengeDefinition daily = _dailyField?.GetValue(_bootstrap) as DailyChallengeDefinition;
            if (daily == null || string.IsNullOrWhiteSpace(daily.ChallengeId)) return;

            SaveData save = _saveField?.GetValue(_bootstrap) as SaveData;
            if (save == null) save = _saveRepository.Load();
            DailyBestUpdate update = _dailyBest.Apply(save, daily.ChallengeId, score);
            _saveRepository.Save(save);

            if (!update.IsNewRecord) return;
            _recordLabel.text = update.PreviousBest > 0.0
                ? $"🏆 НОВЫЙ РЕКОРД  {update.PreviousBest:0.0}% → {update.BestScore:0.0}%"
                : $"🏆 РЕКОРД ДНЯ  {update.BestScore:0.0}%";
            _recordLabel.gameObject.SetActive(true);
            AnalyticsLifecycle.Service?.Track(AnalyticsEventNames.DailyRecord, Params(
                "challenge_id", daily.ChallengeId,
                "previous", update.PreviousBest,
                "score", update.BestScore));
        }

        private void ConfigureRematch(double score)
        {
            Button primary = _primaryField?.GetValue(_bootstrap) as Button;
            if (primary == null) return;
            Text label = primary.GetComponentInChildren<Text>(true);
            if (label != null) label.text = "РЕВАНШ";

            DuelSession duel = _duelSessionField?.GetValue(_bootstrap) as DuelSession;
            string referralId = duel?.Referral?.ReferralId ?? string.Empty;
            double rivalScore = duel?.Referral?.InviterScore ?? 0.0;
            primary.onClick.AddListener(() => AnalyticsLifecycle.Service?.Track(
                AnalyticsEventNames.DuelRematch,
                Params("referrer_id", referralId, "previous_score", score, "rival_score", rivalScore)));
        }

        private async void ShareCard()
        {
            if (_shareBusy || _bootstrap == null) return;
            _shareBusy = true;
            _cardButton.interactable = false;
            _cardButtonText.text = "ГОТОВИМ КАРТОЧКУ…";

            try
            {
                string mode = GetEnumName(_modeField);
                bool dailyCompleted = GetBool(_dailyCompletedField);
                bool aggregate = dailyCompleted &&
                                 (string.Equals(mode, "Daily", StringComparison.Ordinal) ||
                                  string.Equals(mode, "Duel", StringComparison.Ordinal));
                double score = aggregate ? GetDouble(_lastDailyScoreField) : GetDouble(_lastResultScoreField);
                RouteDefinition route = _routeField?.GetValue(_bootstrap) as RouteDefinition;
                var recording = _recordingField?.GetValue(_bootstrap) as List<RecordedPoint>;
                DailyChallengeDefinition daily = _dailyField?.GetValue(_bootstrap) as DailyChallengeDefinition;
                DuelSession duel = _duelSessionField?.GetValue(_bootstrap) as DuelSession;
                UnityGameApi api = _apiField?.GetValue(_bootstrap) as UnityGameApi;

                if (route == null || recording == null || recording.Count < 2)
                    throw new InvalidOperationException("Result trajectory is not available for the share card.");

                string challengeUrl = string.Empty;
                if (aggregate && daily != null && api != null)
                {
                    SaveData save = _saveField?.GetValue(_bootstrap) as SaveData ?? _saveRepository.Load();
                    challengeUrl = await api.CreateChallengeAsync(save.AnonymousPlayerId, daily.ChallengeId, score);
                }

                var model = new ResultShareCardModel
                {
                    ModeLabel = ModeLabel(mode),
                    ChallengeLabel = daily?.ChallengeId ?? "Тренировка",
                    Score = score,
                    Celebration = ScoreCelebrationPolicy.Evaluate(score),
                    ReferencePoints = route.ReferencePoints,
                    PlayerPoints = new List<RecordedPoint>(recording),
                    HasRivalScore = string.Equals(mode, "Duel", StringComparison.Ordinal) && duel != null,
                    RivalScore = duel?.Referral?.InviterScore ?? 0.0
                };

                byte[] png = ResultShareCardRenderer.RenderPng(model);
                string text = aggregate
                    ? $"Я набрал {score:0.0}% в НЕ СБЕЙСЯ! Сможешь точнее?"
                    : $"Мой результат в НЕ СБЕЙСЯ! — {score:0.0}%. Сможешь точнее?";
                if (!string.IsNullOrWhiteSpace(challengeUrl)) text += "\n" + challengeUrl;

                bool imageShared = NativeImageShare.Share(png, text);
                AnalyticsLifecycle.Service?.Track(AnalyticsEventNames.ShareCard, Params(
                    "mode", mode,
                    "score", score,
                    "image_shared", imageShared,
                    "has_challenge_url", !string.IsNullOrWhiteSpace(challengeUrl)));
            }
            catch (Exception error)
            {
                Debug.LogWarning($"Result card unavailable: {error.Message}");
            }
            finally
            {
                _shareBusy = false;
                if (_cardButton != null) _cardButton.interactable = true;
                if (_cardButtonText != null) _cardButtonText.text = "📸 КАРТОЧКА";
            }
        }

        private IEnumerator PerfectPulse()
        {
            Vector3 originalScale = _badge.transform.localScale;
            for (int pulse = 0; pulse < 3; pulse++)
            {
                for (int step = 0; step <= 6; step++)
                {
                    float t = step / 6f;
                    float alpha = Mathf.Sin(t * Mathf.PI) * 0.28f;
                    _flash.color = new Color(0.38f, 0.82f, 1f, alpha);
                    _badge.transform.localScale = originalScale * (1f + Mathf.Sin(t * Mathf.PI) * 0.08f);
                    yield return new WaitForSecondsRealtime(0.035f);
                }
            }
            _flash.color = new Color(0.38f, 0.82f, 1f, 0f);
            _badge.transform.localScale = originalScale;
            _perfectRoutine = null;
        }

        private void ResolveBootstrap()
        {
            _bootstrap = FindFirstObjectByType<GameBootstrap>();
            if (_bootstrap == null) return;
            _bootstrapType = typeof(GameBootstrap);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            _stateField = _bootstrapType.GetField("_state", flags);
            _modeField = _bootstrapType.GetField("_mode", flags);
            _dailyCompletedField = _bootstrapType.GetField("_dailyCompleted", flags);
            _lastResultScoreField = _bootstrapType.GetField("_lastResultScore", flags);
            _lastDailyScoreField = _bootstrapType.GetField("_lastDailyScore", flags);
            _dailyField = _bootstrapType.GetField("_daily", flags);
            _routeField = _bootstrapType.GetField("_route", flags);
            _recordingField = _bootstrapType.GetField("_recording", flags);
            _duelSessionField = _bootstrapType.GetField("_duelSession", flags);
            _apiField = _bootstrapType.GetField("_api", flags);
            _primaryField = _bootstrapType.GetField("_primary", flags);
            _saveField = _bootstrapType.GetField("_save", flags);
        }

        private string GetEnumName(FieldInfo field) => field?.GetValue(_bootstrap)?.ToString() ?? string.Empty;
        private bool GetBool(FieldInfo field) => field?.GetValue(_bootstrap) is bool value && value;
        private double GetDouble(FieldInfo field) => field?.GetValue(_bootstrap) is double value ? value : 0.0;

        private void BuildUi()
        {
            _canvas = new GameObject("ResultEnhancementCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvas.transform.SetParent(transform, false);
            Canvas canvas = _canvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 28;
            CanvasScaler scaler = _canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            var flashGo = new GameObject("PerfectFlash", typeof(RectTransform), typeof(Image));
            flashGo.transform.SetParent(_canvas.transform, false);
            SetAnchors(flashGo.GetComponent<RectTransform>(), new Vector2(0.06f, 0.25f), new Vector2(0.94f, 0.74f));
            _flash = flashGo.GetComponent<Image>();
            _flash.color = new Color(0.38f, 0.82f, 1f, 0f);
            _flash.raycastTarget = false;

            _badge = CreateText(_canvas.transform, "Medal", 42, TextAnchor.MiddleCenter,
                new Vector2(0.10f, 0.685f), new Vector2(0.90f, 0.735f));
            _recordLabel = CreateText(_canvas.transform, "DailyRecord", 28, TextAnchor.MiddleCenter,
                new Vector2(0.08f, 0.645f), new Vector2(0.92f, 0.685f));

            _cardButton = CreateButton(_canvas.transform, "ShareCard", new Vector2(0.25f, 0.205f), new Vector2(0.75f, 0.245f));
            _cardButtonText = _cardButton.GetComponentInChildren<Text>();
            _cardButtonText.text = "📸 КАРТОЧКА";
            _cardButton.onClick.AddListener(ShareCard);
        }

        private void SetResultUiVisible(bool visible)
        {
            if (_badge != null) _badge.gameObject.SetActive(visible);
            if (_recordLabel != null) _recordLabel.gameObject.SetActive(visible && !string.IsNullOrWhiteSpace(_recordLabel.text));
            if (_cardButton != null) _cardButton.gameObject.SetActive(visible);
        }

        private static Button CreateButton(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            SetAnchors(go.GetComponent<RectTransform>(), min, max);
            go.GetComponent<Image>().color = new Color(0.14f, 0.20f, 0.34f, 0.96f);
            Button button = go.GetComponent<Button>();
            Text text = CreateText(go.transform, "Label", 25, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
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
            text.raycastTarget = false;
            return text;
        }

        private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Color BadgeColor(ScoreMedalTier tier)
        {
            switch (tier)
            {
                case ScoreMedalTier.Perfect: return new Color(0.58f, 1f, 0.78f, 1f);
                case ScoreMedalTier.Gold: return new Color(1f, 0.78f, 0.25f, 1f);
                case ScoreMedalTier.Silver: return new Color(0.80f, 0.90f, 1f, 1f);
                case ScoreMedalTier.Bronze: return new Color(0.92f, 0.58f, 0.32f, 1f);
                default: return new Color(0.72f, 0.78f, 0.88f, 1f);
            }
        }

        private static string ModeLabel(string mode)
        {
            if (string.Equals(mode, "Daily", StringComparison.Ordinal)) return "DAILY CHALLENGE";
            if (string.Equals(mode, "Duel", StringComparison.Ordinal)) return "ВЫЗОВ ДРУГА";
            if (string.Equals(mode, "Training", StringComparison.Ordinal)) return "ТРЕНИРОВКА";
            return "НЕ СБЕЙСЯ!";
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
