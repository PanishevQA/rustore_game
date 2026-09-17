using System;
using System.Collections;
using System.Collections.Generic;
using DontGetSidetracked.Analytics;
using DontGetSidetracked.Core;
using DontGetSidetracked.Daily;
using DontGetSidetracked.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace DontGetSidetracked.Presentation
{
    /// <summary>
    /// Adds result-only presentation without changing the input/scoring loop:
    /// score medals, perfect pulse, per-Daily personal records, PNG share cards and Duel rematch UX.
    /// Campaign shares are owned here too so the internal Training bootstrap mode never leaks to users.
    /// </summary>
    public sealed class ResultEnhancementCoordinator : MonoBehaviour
    {
        private GameBootstrap _bootstrap;

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
            if (_bootstrap == null) return;

            bool isResult = GameBootstrapRuntimeBridge.IsResult(_bootstrap);
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
            if (!GameBootstrapRuntimeBridge.TryCaptureResult(_bootstrap, out GameBootstrapResultSnapshot snapshot))
            {
                SetResultUiVisible(false);
                return;
            }

            bool campaign = CampaignRuntimeCoordinator.IsCampaignActive;
            string mode = snapshot.ModeName;
            bool aggregate = snapshot.DailyCompleted &&
                             (string.Equals(mode, "Daily", StringComparison.Ordinal) ||
                              string.Equals(mode, "Duel", StringComparison.Ordinal));
            double score = aggregate ? snapshot.LastDailyScore : snapshot.LastResultScore;
            ScoreCelebration celebration = ScoreCelebrationPolicy.Evaluate(score);

            _badge.text = string.IsNullOrWhiteSpace(celebration.Icon)
                ? celebration.Label
                : celebration.Icon + "  " + celebration.Label;
            _badge.color = BadgeColor(celebration.Tier);
            _recordLabel.text = string.Empty;
            SetResultUiVisible(true);

            string analyticsMode = campaign ? "Campaign" : mode;
            if (celebration.Tier != ScoreMedalTier.None)
            {
                AnalyticsLifecycle.Service?.Track(AnalyticsEventNames.MedalEarned, Params(
                    "tier", celebration.Tier.ToString(),
                    "score", score,
                    "mode", analyticsMode));
            }

            if (!campaign && string.Equals(mode, "Daily", StringComparison.Ordinal) && snapshot.DailyCompleted)
                ApplyDailyRecord(snapshot, score);

            bool shareable = campaign || string.Equals(mode, "Training", StringComparison.Ordinal) || aggregate;
            _cardButton.gameObject.SetActive(shareable);
            _cardButtonText.text = campaign ? "📸 КАРТОЧКА УРОВНЯ" : "📸 КАРТОЧКА";

            if (!campaign && string.Equals(mode, "Duel", StringComparison.Ordinal) && snapshot.DailyCompleted)
                ConfigureRematch(snapshot, score);

            if (celebration.PlayPerfectEffect)
            {
                if (_perfectRoutine != null) StopCoroutine(_perfectRoutine);
                _perfectRoutine = StartCoroutine(PerfectPulse());
            }
        }

        private void ApplyDailyRecord(GameBootstrapResultSnapshot snapshot, double score)
        {
            DailyChallengeDefinition daily = snapshot.Daily;
            if (daily == null || string.IsNullOrWhiteSpace(daily.ChallengeId)) return;

            SaveData save = snapshot.Save ?? _saveRepository.Load();
            DailyBestUpdate update = _dailyBest.Apply(save, daily.ChallengeId, score);
            _saveRepository.Save(save);
            GameBootstrapRuntimeBridge.ReplaceSave(_bootstrap, save);

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

        private static void ConfigureRematch(GameBootstrapResultSnapshot snapshot, double score)
        {
            Button primary = snapshot.PrimaryButton;
            if (primary == null) return;
            Text label = primary.GetComponentInChildren<Text>(true);
            if (label != null) label.text = "РЕВАНШ";

            string referralId = snapshot.DuelSession?.Referral?.ReferralId ?? string.Empty;
            double rivalScore = snapshot.DuelSession?.Referral?.InviterScore ?? 0.0;
            primary.onClick.AddListener(() => AnalyticsLifecycle.Service?.Track(
                AnalyticsEventNames.DuelRematch,
                Params("referrer_id", referralId, "previous_score", score, "rival_score", rivalScore)));
        }

        private async void ShareCard()
        {
            if (_shareBusy || _bootstrap == null) return;
            if (!GameBootstrapRuntimeBridge.TryCaptureResult(_bootstrap, out GameBootstrapResultSnapshot snapshot)) return;

            int revision = snapshot.NavigationRevision;
            bool campaign = CampaignRuntimeCoordinator.IsCampaignActive;
            int campaignLevel = campaign ? CampaignRuntimeCoordinator.CurrentLevelNumber : 0;
            int campaignChapter = campaign ? CampaignRuntimeCoordinator.CurrentChapterNumber : 0;

            _shareBusy = true;
            _cardButton.interactable = false;
            _cardButtonText.text = "ГОТОВИМ КАРТОЧКУ…";

            try
            {
                string mode = snapshot.ModeName;
                bool aggregate = snapshot.DailyCompleted &&
                                 (string.Equals(mode, "Daily", StringComparison.Ordinal) ||
                                  string.Equals(mode, "Duel", StringComparison.Ordinal));
                double score = aggregate ? snapshot.LastDailyScore : snapshot.LastResultScore;
                RouteDefinition route = snapshot.Route;
                List<RecordedPoint> recording = snapshot.Recording;
                DailyChallengeDefinition daily = snapshot.Daily;

                if (route == null || recording == null || recording.Count < 2)
                    throw new InvalidOperationException("Result trajectory is not available for the share card.");
                if (campaign && (campaignLevel <= 0 || campaignChapter <= 0))
                    throw new InvalidOperationException("Campaign result identity is not available for the share card.");

                string challengeUrl = string.Empty;
                if (!campaign && aggregate && daily != null && snapshot.Api != null)
                {
                    SaveData save = snapshot.Save ?? _saveRepository.Load();
                    challengeUrl = await snapshot.Api.CreateChallengeAsync(save.AnonymousPlayerId, daily.ChallengeId, score);

                    if (!GameBootstrapRuntimeBridge.IsCurrentNavigation(_bootstrap, revision) ||
                        !GameBootstrapRuntimeBridge.IsResult(_bootstrap))
                        return;
                }

                if (campaign &&
                    (!CampaignRuntimeCoordinator.IsCampaignActive ||
                     CampaignRuntimeCoordinator.CurrentLevelNumber != campaignLevel ||
                     CampaignRuntimeCoordinator.CurrentChapterNumber != campaignChapter))
                    return;

                var model = new ResultShareCardModel
                {
                    ModeLabel = campaign ? "КАМПАНИЯ" : ModeLabel(mode),
                    ChallengeLabel = campaign
                        ? $"УРОВЕНЬ {campaignLevel} • {CampaignLevelCatalog.ChapterName(campaignChapter)}"
                        : daily?.ChallengeId ?? "Тренировка",
                    Score = score,
                    Celebration = ScoreCelebrationPolicy.Evaluate(score),
                    ReferencePoints = route.ReferencePoints,
                    PlayerPoints = recording,
                    HasRivalScore = !campaign && string.Equals(mode, "Duel", StringComparison.Ordinal) && snapshot.DuelSession != null,
                    RivalScore = campaign ? 0.0 : snapshot.DuelSession?.Referral?.InviterScore ?? 0.0
                };

                byte[] png = ResultShareCardRenderer.RenderPng(model);
                string text;
                if (campaign)
                    text = $"Уровень {campaignLevel} в НЕ СБЕЙСЯ! — {score:0.0}%. Сможешь точнее?";
                else if (aggregate)
                    text = $"Я набрал {score:0.0}% в НЕ СБЕЙСЯ! Сможешь точнее?";
                else
                    text = $"Мой результат в НЕ СБЕЙСЯ! — {score:0.0}%. Сможешь точнее?";
                if (!string.IsNullOrWhiteSpace(challengeUrl)) text += "\n" + challengeUrl;

                bool imageShared = NativeImageShare.Share(png, text);
                Dictionary<string, object> analytics = Params(
                    "mode", campaign ? "Campaign" : mode,
                    "score", score,
                    "image_shared", imageShared,
                    "has_challenge_url", !string.IsNullOrWhiteSpace(challengeUrl));
                if (campaign)
                {
                    analytics["level"] = campaignLevel;
                    analytics["chapter"] = campaignChapter;
                }
                AnalyticsLifecycle.Service?.Track(AnalyticsEventNames.ShareCard, analytics);
            }
            catch (Exception error)
            {
                Debug.LogWarning($"Result card unavailable: {error.Message}");
            }
            finally
            {
                _shareBusy = false;
                if (_cardButton != null) _cardButton.interactable = true;
                if (_cardButtonText != null)
                    _cardButtonText.text = CampaignRuntimeCoordinator.IsCampaignActive
                        ? "📸 КАРТОЧКА УРОВНЯ"
                        : "📸 КАРТОЧКА";
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
        }

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
