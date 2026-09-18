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
    [DefaultExecutionOrder(17000)]
    public sealed class ResultEnhancementCoordinator : MonoBehaviour
    {
        private GameBootstrap _bootstrap;

        private readonly DailyBestService _dailyBest = new DailyBestService();
        private JsonFileSaveRepository _saveRepository;
        private GameObject _canvas;
        private GameObject _resultHeader;
        private Text _modeLabel;
        private Text _scoreText;
        private Text _badge;
        private Text _recordLabel;
        private GameObject _detailCard;
        private Text _detailText;
        private CanvasGroup _legacyTitleGroup;
        private CanvasGroup _legacyStatusGroup;
        private CanvasGroup _legacyPrimaryGroup;
        private CanvasGroup _legacySecondaryGroup;
        private CanvasGroup _legacyShareGroup;
        private Button _legacyPrimary;
        private Button _legacySecondary;
        private Button _legacyShare;
        private GameObject _actionPanel;
        private Button _primaryAction;
        private Button _secondaryAction;
        private Text _primaryActionText;
        private Text _secondaryActionText;
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
                    RestoreLegacyResultHeader();
                    RestoreLegacyResultControls();
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

            SuppressLegacyResultHeader();
            SuppressLegacyResultControls();
            RefreshResultDetail();
            RefreshResultActions();

            if (_wasResult) return;
            _wasResult = true;
            HandleEnteredResult();
            RefreshResultDetail();
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

            _scoreText.text = $"{score:0.0}%";
            _scoreText.color = score >= 90.0
                ? ReleaseUiKit.Green
                : score >= 70.0
                    ? ReleaseUiKit.Cyan
                    : score >= 50.0
                        ? ReleaseUiKit.Gold
                        : ReleaseUiKit.Danger;

            _modeLabel.text = campaign
                ? $"КАМПАНИЯ  •  УРОВЕНЬ {CampaignRuntimeCoordinator.CurrentLevelNumber}"
                : ModeLabel(mode);

            _badge.text = celebration.Label;
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
            _cardButtonText.text = campaign
                ? "ПОДЕЛИТЬСЯ УРОВНЕМ"
                : aggregate
                    ? "БРОСИТЬ ВЫЗОВ"
                    : "ПОДЕЛИТЬСЯ РЕЗУЛЬТАТОМ";
            RefreshResultActions();

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
                ? $"НОВЫЙ РЕКОРД  {update.PreviousBest:0.0}% → {update.BestScore:0.0}%"
                : $"РЕКОРД ДНЯ  {update.BestScore:0.0}%";
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
                if (_cardButtonText != null && GameBootstrapRuntimeBridge.TryCaptureResult(_bootstrap, out GameBootstrapResultSnapshot current))
                {
                    bool campaignNow = CampaignRuntimeCoordinator.IsCampaignActive;
                    bool aggregateNow = current.DailyCompleted &&
                                        (string.Equals(current.ModeName, "Daily", StringComparison.Ordinal) ||
                                         string.Equals(current.ModeName, "Duel", StringComparison.Ordinal));
                    _cardButtonText.text = campaignNow
                        ? "ПОДЕЛИТЬСЯ УРОВНЕМ"
                        : aggregateNow
                            ? "БРОСИТЬ ВЫЗОВ"
                            : "ПОДЕЛИТЬСЯ РЕЗУЛЬТАТОМ";
                }
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

            var releaseVisual = new GameObject("ReleaseVisual", typeof(RectTransform));
            releaseVisual.transform.SetParent(_canvas.transform, false);
            ReleaseUiKit.Stretch(releaseVisual.GetComponent<RectTransform>());

            var flashGo = new GameObject("PerfectFlash", typeof(RectTransform), typeof(Image));
            flashGo.transform.SetParent(_canvas.transform, false);
            ReleaseUiKit.SetAnchors(flashGo.GetComponent<RectTransform>(), new Vector2(0.06f, 0.25f), new Vector2(0.94f, 0.74f));
            _flash = flashGo.GetComponent<Image>();
            _flash.color = new Color(0.38f, 0.82f, 1f, 0f);
            _flash.raycastTarget = false;

            Image header = ReleaseUiKit.Panel(_canvas.transform, "ResultHeader",
                new Vector2(0.075f, 0.765f), new Vector2(0.925f, 0.935f),
                new Color(0.025f, 0.045f, 0.085f, 0.985f), ReleaseUiKit.Cyan, true);
            _resultHeader = header.gameObject;
            _resultHeader.AddComponent<ReleasePanelMotion>();

            _modeLabel = ReleaseUiKit.TextBlock(header.transform, "Mode", "РЕЗУЛЬТАТ", 19,
                TextAnchor.MiddleLeft, new Vector2(0.055f, 0.72f), new Vector2(0.58f, 0.91f),
                ReleaseUiKit.Muted, FontStyle.Bold);

            _scoreText = ReleaseUiKit.TextBlock(header.transform, "Score", "0.0%", 66,
                TextAnchor.MiddleLeft, new Vector2(0.055f, 0.22f), new Vector2(0.52f, 0.72f),
                ReleaseUiKit.Cyan, FontStyle.Bold);
            ReleaseUiKit.AddTextShadow(_scoreText, 0.48f, -3f);

            _badge = ReleaseUiKit.TextBlock(header.transform, "Medal", string.Empty, 29,
                TextAnchor.MiddleRight, new Vector2(0.52f, 0.36f), new Vector2(0.945f, 0.76f),
                ReleaseUiKit.Text, FontStyle.Bold);

            _recordLabel = ReleaseUiKit.TextBlock(header.transform, "DailyRecord", string.Empty, 18,
                TextAnchor.MiddleRight, new Vector2(0.48f, 0.10f), new Vector2(0.945f, 0.38f),
                ReleaseUiKit.Gold, FontStyle.Bold);

            Image detail = ReleaseUiKit.Panel(_canvas.transform, "ResultDetail",
                new Vector2(0.075f, 0.675f), new Vector2(0.925f, 0.755f),
                new Color(0.030f, 0.045f, 0.085f, 0.97f), ReleaseUiKit.Violet, false);
            _detailCard = detail.gameObject;
            _detailText = ReleaseUiKit.TextBlock(detail.transform, "Detail", string.Empty, 21,
                TextAnchor.MiddleCenter, new Vector2(0.035f, 0.08f), new Vector2(0.965f, 0.92f),
                ReleaseUiKit.Muted, FontStyle.Bold);
            _detailText.lineSpacing = 1.05f;

            Image actions = ReleaseUiKit.Panel(_canvas.transform, "ResultActions",
                new Vector2(0.075f, 0.055f), new Vector2(0.925f, 0.205f),
                new Color(0.022f, 0.034f, 0.070f, 0.985f), ReleaseUiKit.Violet, true);
            _actionPanel = actions.gameObject;

            _primaryAction = ReleaseUiKit.Button(actions.transform, "PrimaryAction", "ЕЩЁ РАЗ",
                new Vector2(0.04f, 0.52f), new Vector2(0.63f, 0.92f),
                ReleaseUiKit.Cyan, new Color(0.02f, 0.05f, 0.08f, 1f), 22, InvokePrimary);
            _primaryActionText = _primaryAction.GetComponentInChildren<Text>(true);

            _secondaryAction = ReleaseUiKit.Button(actions.transform, "SecondaryAction", "ДОМОЙ",
                new Vector2(0.66f, 0.52f), new Vector2(0.96f, 0.92f),
                ReleaseUiKit.SurfaceRaised, ReleaseUiKit.Text, 20, InvokeSecondary);
            _secondaryActionText = _secondaryAction.GetComponentInChildren<Text>(true);

            _cardButton = ReleaseUiKit.Button(actions.transform, "ShareCard", "ПОДЕЛИТЬСЯ РЕЗУЛЬТАТОМ",
                new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.43f),
                ReleaseUiKit.Violet, ReleaseUiKit.Text, 19, ShareCard);
            _cardButtonText = _cardButton.GetComponentInChildren<Text>(true);
        }

        private void ResolveLegacyResultControls()
        {
            if (_bootstrap == null) return;

            _legacyPrimary = GameBootstrapRuntimeBridge.PrimaryButton(_bootstrap);
            _legacySecondary = GameBootstrapRuntimeBridge.SecondaryButton(_bootstrap);
            _legacyShare = GameBootstrapRuntimeBridge.ShareButton(_bootstrap);

            if (_legacyPrimary != null && _legacyPrimaryGroup == null)
                _legacyPrimaryGroup = EnsureCanvasGroup(_legacyPrimary.gameObject);
            if (_legacySecondary != null && _legacySecondaryGroup == null)
                _legacySecondaryGroup = EnsureCanvasGroup(_legacySecondary.gameObject);
            if (_legacyShare != null && _legacyShareGroup == null)
                _legacyShareGroup = EnsureCanvasGroup(_legacyShare.gameObject);
        }

        private void SuppressLegacyResultControls()
        {
            ResolveLegacyResultControls();
            SetLegacyControlGroup(_legacyPrimaryGroup, false);
            SetLegacyControlGroup(_legacySecondaryGroup, false);
            SetLegacyControlGroup(_legacyShareGroup, false);
        }

        private void RestoreLegacyResultControls()
        {
            SetLegacyControlGroup(_legacyPrimaryGroup, true);
            SetLegacyControlGroup(_legacySecondaryGroup, true);
            SetLegacyControlGroup(_legacyShareGroup, true);
        }

        private static void SetLegacyControlGroup(CanvasGroup group, bool visible)
        {
            if (group == null) return;
            group.alpha = visible ? 1f : 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        private void RefreshResultActions()
        {
            ResolveLegacyResultControls();
            SyncProxyAction(_legacyPrimary, _primaryAction, _primaryActionText, "ПРОДОЛЖИТЬ");
            SyncProxyAction(_legacySecondary, _secondaryAction, _secondaryActionText, "ДОМОЙ");
        }

        private static void SyncProxyAction(Button legacy, Button proxy, Text proxyText, string fallback)
        {
            if (proxy == null || proxyText == null) return;
            bool visible = legacy != null && legacy.gameObject.activeSelf;
            proxy.gameObject.SetActive(visible);
            proxy.interactable = visible && legacy.interactable;

            Text legacyText = legacy == null ? null : legacy.GetComponentInChildren<Text>(true);
            proxyText.text = legacyText == null || string.IsNullOrWhiteSpace(legacyText.text)
                ? fallback
                : legacyText.text;
        }

        private void InvokePrimary()
        {
            if (_legacyPrimary != null && _legacyPrimary.interactable)
                _legacyPrimary.onClick.Invoke();
        }

        private void InvokeSecondary()
        {
            if (_legacySecondary != null && _legacySecondary.interactable)
                _legacySecondary.onClick.Invoke();
        }

        private void SuppressLegacyResultHeader()
        {
            Text title = GameBootstrapRuntimeBridge.Title(_bootstrap);
            Text status = GameBootstrapRuntimeBridge.Status(_bootstrap);

            if (title != null)
            {
                if (_legacyTitleGroup == null) _legacyTitleGroup = EnsureCanvasGroup(title.gameObject);
                _legacyTitleGroup.alpha = 0f;
                _legacyTitleGroup.interactable = false;
                _legacyTitleGroup.blocksRaycasts = false;
            }

            if (status != null)
            {
                if (_legacyStatusGroup == null) _legacyStatusGroup = EnsureCanvasGroup(status.gameObject);
                _legacyStatusGroup.alpha = 0f;
                _legacyStatusGroup.interactable = false;
                _legacyStatusGroup.blocksRaycasts = false;
            }
        }

        private void RestoreLegacyResultHeader()
        {
            if (_legacyTitleGroup != null)
            {
                _legacyTitleGroup.alpha = 1f;
                _legacyTitleGroup.interactable = false;
                _legacyTitleGroup.blocksRaycasts = false;
            }

            if (_legacyStatusGroup != null)
            {
                _legacyStatusGroup.alpha = 1f;
                _legacyStatusGroup.interactable = false;
                _legacyStatusGroup.blocksRaycasts = false;
            }
        }

        private void RefreshResultDetail()
        {
            if (_detailText == null || _detailCard == null || _bootstrap == null) return;

            Text status = GameBootstrapRuntimeBridge.Status(_bootstrap);
            string value = status?.text?.Replace("\r", string.Empty).Trim() ?? string.Empty;
            bool numericOnly = IsScoreOnly(value);

            _detailText.text = numericOnly ? string.Empty : value;
            bool show = !string.IsNullOrWhiteSpace(_detailText.text);
            if (_detailCard.activeSelf != show) _detailCard.SetActive(show);
        }

        private static bool IsScoreOnly(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return true;
            string trimmed = value.Trim();
            if (!trimmed.EndsWith("%", StringComparison.Ordinal)) return false;

            string number = trimmed.Substring(0, trimmed.Length - 1).Trim().Replace(',', '.');
            return double.TryParse(
                number,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out _);
        }

        private static CanvasGroup EnsureCanvasGroup(GameObject target)
        {
            CanvasGroup group = target.GetComponent<CanvasGroup>();
            if (group == null) group = target.AddComponent<CanvasGroup>();
            return group;
        }

        private void SetResultUiVisible(bool visible)
        {
            if (_resultHeader != null) _resultHeader.SetActive(visible);
            if (_modeLabel != null) _modeLabel.gameObject.SetActive(visible);
            if (_scoreText != null) _scoreText.gameObject.SetActive(visible);
            if (_badge != null) _badge.gameObject.SetActive(visible);
            if (_recordLabel != null) _recordLabel.gameObject.SetActive(visible && !string.IsNullOrWhiteSpace(_recordLabel.text));
            if (_detailCard != null) _detailCard.SetActive(visible && _detailText != null && !string.IsNullOrWhiteSpace(_detailText.text));
            if (_actionPanel != null) _actionPanel.SetActive(visible);
            if (!visible && _cardButton != null) _cardButton.gameObject.SetActive(false);
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
