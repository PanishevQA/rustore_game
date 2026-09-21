using System;
using System.Collections;
using System.Collections.Generic;
using DontGetSidetracked.Analytics;
using DontGetSidetracked.Core;
using DontGetSidetracked.Daily;
using DontGetSidetracked.Gameplay;
using DontGetSidetracked.Network;
using DontGetSidetracked.Social;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DontGetSidetracked.Presentation
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        private enum Mode { Tutorial, Home, Daily, Duel, Training }
        private enum RoundState { Idle, Showing, Drawing, Result }

        private readonly RouteGenerator _generator = new RouteGenerator();
        private readonly ScoreCalculator _scorer = new ScoreCalculator();
        private const int MaxRecordingPoints = 2048;
        private const long MaxGestureDurationMs = 20_000;

        private readonly List<RecordedPoint> _recording = new List<RecordedPoint>(256);
        private readonly List<double> _dailyScores = new List<double>(3);
        private readonly List<IReadOnlyList<RecordedPoint>> _dailyReplays = new List<IReadOnlyList<RecordedPoint>>(3);

        private RouteGraphic _referenceGraphic;
        private RouteGraphic _playerGraphic;
        private Text _title;
        private Text _status;
        private Button _primary;
        private Button _secondary;
        private Button _share;
        private RectTransform _playArea;
        private Image _startMarker;
        private Image _endMarker;

        private JsonFileSaveRepository _saveRepository;
        private SaveData _save;
        private UnityGameApi _api;
        private DailySessionService _dailyService;
        private DailyLoadResult _dailySession;
        private DuelSessionService _duelService;
        private DuelSession _duelSession;

        private Mode _mode = Mode.Home;
        private RoundState _state = RoundState.Idle;
        private DailyChallengeDefinition _daily;
        private RouteDefinition _route;
        private int _dailyIndex;
        private long _gestureStartMs;
        private bool _pointerDown;
        private int _trainingIndex;
        private double _lastResultScore;
        private ScoreBreakdown _lastScoreBreakdown;
        private double _lastDailyScore;
        private bool _dailyCompleted;
        private bool _referralOfferLoading;
        private bool _deferReferralForSession;
        private int _navigationRevision;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (FindFirstObjectByType<GameBootstrap>() != null) return;
            var root = new GameObject("GameBootstrap");
            DontDestroyOnLoad(root);
            root.AddComponent<GameBootstrap>();
        }

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.Portrait;

            _saveRepository = new JsonFileSaveRepository();
            _save = _saveRepository.Load();
            _save.SessionNumber++;
            _saveRepository.Save(_save);

            _api = new UnityGameApi(GameRuntimeSettings.BackendBaseUrl);
            _dailyService = new DailySessionService(_api, _saveRepository, _save);
            _duelService = new DuelSessionService(_api, _saveRepository, _save);

            BuildUi();
            if (_save.TutorialCompleted) ShowHome();
            else StartTutorial();
        }

        private void Update()
        {
            if (_state != RoundState.Drawing) return;
            PollPointer();
        }

        private void ShowHome()
        {
            BeginNavigation();
            _mode = Mode.Home;
            _state = RoundState.Idle;
            _referenceGraphic.Clear();
            _playerGraphic.Clear();
            SetMarkersVisible(false);
            _title.text = "НЕ СБЕЙСЯ!";

            string best = _save.PersonalBest > 0 ? $"Лучший: {_save.PersonalBest:0.0}%" : "Лучший: --";
            _status.text = $"Серия: {_save.Streak} дней\n{best}";

            ConfigureButton(_primary, "ИГРАТЬ DAILY", StartDaily);
            ConfigureButton(_secondary, "ТРЕНИРОВКА", StartTraining);
            _share.gameObject.SetActive(false);

            if (!_deferReferralForSession && !string.IsNullOrWhiteSpace(_save.PendingReferralId))
                TryOfferPendingReferral();
        }

        private async void TryOfferPendingReferral()
        {
            if (_referralOfferLoading || _mode != Mode.Home || string.IsNullOrWhiteSpace(_save.PendingReferralId)) return;
            _referralOfferLoading = true;
            int revision = _navigationRevision;
            string referralId = _save.PendingReferralId;
            _status.text = "ГОТОВИМ ВЫЗОВ ДРУГА…";

            try
            {
                DuelSession loaded = await _duelService.LoadAsync(referralId);
                if (!IsCurrentNavigation(revision, Mode.Home) ||
                    !string.Equals(_save.PendingReferralId, referralId, StringComparison.OrdinalIgnoreCase)) return;

                _duelSession = loaded;
                _title.text = "ВЫЗОВ ДРУГА";
                _status.text = $"Друг: {loaded.Referral.InviterScore:0.0}%\nТвоя цель — побить результат";
                ConfigureButton(_primary, "ПРИНЯТЬ ВЫЗОВ", StartDuel);
                ConfigureButton(_secondary, "ПОЗЖЕ", DeferReferralOffer);
                _share.gameObject.SetActive(false);
            }
            catch (Exception error)
            {
                Debug.LogWarning($"Referral challenge unavailable: {error.Message}");
                if (!IsCurrentNavigation(revision, Mode.Home) ||
                    !string.Equals(_save.PendingReferralId, referralId, StringComparison.OrdinalIgnoreCase)) return;

                _deferReferralForSession = true;
                ShowHome();
            }
            finally
            {
                _referralOfferLoading = false;
                if (!IsCurrentNavigation(revision) &&
                    _mode == Mode.Home &&
                    !_deferReferralForSession &&
                    !string.IsNullOrWhiteSpace(_save.PendingReferralId))
                    TryOfferPendingReferral();
            }
        }

        private void DeferReferralOffer()
        {
            _deferReferralForSession = true;
            ShowHome();
        }

        internal void NotifyPendingReferralAvailable(string referralId)
        {
            if (string.IsNullOrWhiteSpace(referralId)) return;

            _save = _saveRepository.Load();
            if (!string.Equals(_save.PendingReferralId, referralId, StringComparison.OrdinalIgnoreCase)) return;

            // A fresh external intent is explicit user intent, so a previous "later" choice
            // must not suppress it. Never interrupt Tutorial/Showing/Drawing/Result; ShowHome
            // will offer the pending challenge at the next safe transition.
            _deferReferralForSession = false;
            if (_mode == Mode.Home && _state == RoundState.Idle)
                TryOfferPendingReferral();
        }

        private void StartTutorial()
        {
            BeginNavigation();
            _mode = Mode.Tutorial;
            _dailyCompleted = false;
            if (!_save.TutorialCompleted)
                AnalyticsLifecycle.Service?.Track(AnalyticsEventNames.TutorialStart);
            RouteDefinition tutorialRoute = _generator.Generate(24051990, RouteGenerator.CurrentGeneratorVersion, RouteDifficulty.Easy);
            StartCoroutine(BeginRoute(tutorialRoute));
        }

        private async void StartDaily()
        {
            if (_state == RoundState.Showing || _state == RoundState.Drawing) return;

            int revision = BeginNavigation();
            _mode = Mode.Daily;
            _state = RoundState.Idle;
            _dailyIndex = 0;
            _dailyScores.Clear();
            _dailyReplays.Clear();
            _dailyCompleted = false;
            _duelSession = null;
            HideButtons();
            SetMarkersVisible(false);
            _title.text = "DAILY CHALLENGE";
            _status.text = "ГОТОВИМ ИСПЫТАНИЕ…";

            try
            {
                DailyLoadResult loaded = await _dailyService.LoadCurrentAsync();
                _save = _dailyService.Save;
                if (!IsCurrentNavigation(revision, Mode.Daily)) return;

                _dailySession = loaded;
                _daily = loaded.Challenge;
                AnalyticsLifecycle.Service?.Track(AnalyticsEventNames.DailyStart, Params(
                    "challenge_id", _daily.ChallengeId,
                    "route_count", _daily.RouteCount,
                    "offline", true));
                StartCoroutine(BeginRoute(_daily.Routes[0]));
            }
            catch (Exception error)
            {
                Debug.LogWarning($"Daily unavailable: {error.Message}");
                if (!IsCurrentNavigation(revision, Mode.Daily)) return;

                _state = RoundState.Idle;
                _status.text = "Не удалось подготовить Daily.\nТренировка доступна локально.";
                ConfigureButton(_primary, "ПОВТОРИТЬ", StartDaily);
                ConfigureButton(_secondary, "ТРЕНИРОВКА", StartTraining);
                _share.gameObject.SetActive(false);
            }
        }

        private void StartDuel()
        {
            if (_duelSession == null || _state == RoundState.Showing || _state == RoundState.Drawing) return;

            BeginNavigation();
            _mode = Mode.Duel;
            _state = RoundState.Idle;
            _dailyIndex = 0;
            _dailyScores.Clear();
            _dailyReplays.Clear();
            _dailyCompleted = false;
            _dailySession = null;
            _daily = _duelSession.Challenge;
            AnalyticsLifecycle.Service?.Track(AnalyticsEventNames.ChallengeOpen, Params(
                "challenge_id", _daily.ChallengeId,
                "referrer_id", _duelSession.Referral.ReferralId,
                "route_count", _daily.RouteCount,
                "source", "duel_offer"));
            StartCoroutine(BeginRoute(_daily.Routes[0]));
        }

        private void StartTraining()
        {
            BeginNavigation();
            _mode = Mode.Training;
            _dailyCompleted = false;
            _duelSession = null;
            _trainingIndex++;
            long seed = (DateTime.UtcNow.Ticks ^ (_trainingIndex * 7919L)) & 0x7FFFFFFF;
            RouteDifficulty difficulty = (RouteDifficulty)(_trainingIndex % 3);
            StartCoroutine(BeginRoute(_generator.Generate(seed, RouteGenerator.CurrentGeneratorVersion, difficulty)));
        }

        private IEnumerator BeginRoute(RouteDefinition route)
        {
            _route = route;
            _recording.Clear();
            _pointerDown = false;
            _playerGraphic.Clear();
            _referenceGraphic.color = new Color(0.1f, 0.9f, 1f, 1f);
            // PathWidth is a gameplay/scoring corridor, not the desired visual stroke width.
            // Keep the reference elegant and readable on phones instead of rendering the
            // full tolerance corridor as a thick ribbon.
            float corridorWidth = route.PathWidth / (float)FixedPoint2.Scale * _playArea.rect.width;
            _referenceGraphic.Thickness = Mathf.Clamp(corridorWidth * 0.46f, 8f, 14f);
            _referenceGraphic.SetPoints(route.ReferencePoints);
            PositionMarkers(route);
            SetMarkersVisible(true);

            if (_mode == Mode.Daily)
            {
                string offline = _dailySession != null && _dailySession.FromCache ? " • КЭШ" : string.Empty;
                _title.text = $"DAILY {_dailyIndex + 1}/{_daily.RouteCount}{offline}";
            }
            else if (_mode == Mode.Duel)
            {
                _title.text = $"ВЫЗОВ {_dailyIndex + 1}/{_daily.RouteCount} • ЦЕЛЬ {_duelSession.Referral.InviterScore:0.0}%";
            }
            else if (_mode == Mode.Tutorial)
            {
                _title.text = "ОБУЧЕНИЕ";
            }
            else
            {
                _title.text = "ТРЕНИРОВКА";
            }

            AnalyticsLifecycle.Service?.Track(AnalyticsEventNames.RoundStart, Params(
                "difficulty", route.Difficulty.ToString(),
                "challenge_id", _daily?.ChallengeId ?? string.Empty,
                "route_index", _dailyIndex,
                "mode", _mode.ToString()));

            _status.text = "ЗАПОМНИ ЛИНИЮ";
            _state = RoundState.Showing;
            HideButtons();

            yield return new WaitForSecondsRealtime(route.DisplayTimeMs / 1000f);
            _status.text = "3"; yield return new WaitForSecondsRealtime(0.35f);
            _status.text = "2"; yield return new WaitForSecondsRealtime(0.35f);
            _status.text = "1"; yield return new WaitForSecondsRealtime(0.35f);
            _referenceGraphic.Clear();
            _status.text = "ТЕПЕРЬ ПОВТОРИ\nНачни с голубой точки";
            _state = RoundState.Drawing;
        }

        private void PollPointer()
        {
            bool pressed;
            bool released;
            Vector2 screenPosition;

            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                screenPosition = touch.position;
                pressed = touch.phase == TouchPhase.Began;
                released = touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled;
                if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary) pressed = false;
            }
            else
            {
                screenPosition = Input.mousePosition;
                pressed = Input.GetMouseButtonDown(0);
                released = Input.GetMouseButtonUp(0);
            }

            if (pressed && TryScreenToFixed(screenPosition, out FixedPoint2 start))
            {
                FixedPoint2 expectedStart = _route.ReferencePoints[0];
                const long startRadius = 75_000;
                if (expectedStart.DistanceSquared(start) > startRadius * startRadius)
                {
                    _status.text = "НАЧНИ С ГОЛУБОЙ ТОЧКИ";
                    return;
                }

                _pointerDown = true;
                _gestureStartMs = NowMs();
                _recording.Clear();
                _playerGraphic.Clear();
                AddPoint(start);
                _status.text = "ВЕДИ ПО ПАМЯТИ";
            }

            bool held = Input.touchCount > 0 || Input.GetMouseButton(0);
            if (_pointerDown && NowMs() - _gestureStartMs >= MaxGestureDurationMs)
            {
                _pointerDown = false;
                FinishRound();
                return;
            }

            if (_pointerDown && held && TryScreenToFixed(screenPosition, out FixedPoint2 point))
            {
                AddPoint(point);
                if (_recording.Count >= MaxRecordingPoints)
                {
                    _pointerDown = false;
                    FinishRound();
                    return;
                }
            }

            if (_pointerDown && released)
            {
                if (TryScreenToFixed(screenPosition, out FixedPoint2 end)) AddPoint(end);
                _pointerDown = false;
                FinishRound();
            }
        }

        private void AddPoint(FixedPoint2 point)
        {
            long ts = NowMs() - _gestureStartMs;
            if (_recording.Count > 0 && _recording[_recording.Count - 1].Position.DistanceSquared(point) < 7_000L * 7_000L) return;
            _recording.Add(new RecordedPoint(point, ts));
            _playerGraphic.AppendPoint(point);
        }

        private void FinishRound()
        {
            _state = RoundState.Result;
            ScoreBreakdown result = _scorer.Calculate(_route, _recording);
            _lastScoreBreakdown = result;
            _lastResultScore = result.Score;
            _referenceGraphic.SetPoints(_route.ReferencePoints);
            _referenceGraphic.color = new Color(0.1f, 0.9f, 1f, 0.65f);
            _playerGraphic.color = result.Score >= 90 ? new Color(0.2f, 1f, 0.45f, 1f) : new Color(1f, 0.75f, 0.15f, 1f);
            _title.text = "РЕЗУЛЬТАТ";
            _status.text = $"{result.Score:0.0}%";

            AnalyticsLifecycle.Service?.Track(AnalyticsEventNames.RoundComplete, Params(
                "difficulty", _route.Difficulty.ToString(),
                "score", result.Score,
                "challenge_id", _daily?.ChallengeId ?? string.Empty,
                "route_index", _dailyIndex,
                "mode", _mode.ToString()));
            AnalyticsLifecycle.Service?.Track(AnalyticsEventNames.ScoreGenerated, Params(
                "score", result.Score,
                "difficulty", _route.Difficulty.ToString()));

            if (_mode != Mode.Tutorial)
                RecordScoredAttempt(result.Score);

            if (_mode == Mode.Tutorial)
            {
                bool firstCompletion = !_save.TutorialCompleted;
                _save.TutorialCompleted = true;
                _saveRepository.Save(_save);
                if (firstCompletion)
                    AnalyticsLifecycle.Service?.Track(AnalyticsEventNames.TutorialComplete, Params("score", result.Score));

                if (result.Score >= 70.0)
                {
                    _status.text = $"{result.Score:0.0}% — ОТЛИЧНО!\nТеперь настоящее испытание.";
                    ConfigureButton(_secondary, "ТРЕНИРОВКА", StartTraining);
                }
                else if (result.Score >= 40.0)
                {
                    _status.text = $"{result.Score:0.0}% — ГОТОВО.\nГлавное — повторить маршрут одним движением.";
                    ConfigureButton(_secondary, "ПОВТОРИТЬ ОБУЧЕНИЕ", StartTutorial);
                }
                else
                {
                    _status.text = $"{result.Score:0.0}% — ПЕРВЫЙ МАРШРУТ ГОТОВ.\nМожно повторить обучение или продолжить.";
                    ConfigureButton(_secondary, "ПОВТОРИТЬ ОБУЧЕНИЕ", StartTutorial);
                }

                ConfigureButton(_primary, "ПРОДОЛЖИТЬ", ShowHome);
                _share.gameObject.SetActive(false);
                return;
            }

            if (_mode == Mode.Daily || _mode == Mode.Duel)
            {
                _dailyScores.Add(result.Score);
                _dailyReplays.Add(new List<RecordedPoint>(_recording));
                if (_dailyIndex < _daily.RouteCount - 1)
                {
                    ConfigureButton(_primary, "СЛЕДУЮЩИЙ МАРШРУТ", NextDailyRoute);
                    ConfigureButton(_secondary, "ДОМОЙ", ShowHome);
                    _share.gameObject.SetActive(false);
                }
                else
                {
                    _lastDailyScore = DailyChallengeFactory.DailyScore(_dailyScores);
                    _dailyCompleted = true;
                    if (_mode == Mode.Daily)
                    {
                        ConfigureButton(_primary, "ПОВТОРИТЬ DAILY", StartDaily);
                        CompleteDaily();
                    }
                    else
                    {
                        ConfigureButton(_primary, "ЕЩЁ РАЗ", StartDuel);
                        CompleteDuel();
                    }
                    ConfigureButton(_secondary, "ДОМОЙ", ShowHome);
                    ConfigureButton(_share, "БРОСИТЬ ВЫЗОВ", ShareCurrentResult);
                    _share.gameObject.SetActive(true);
                }
            }
            else
            {
                ConfigureButton(_primary, "ЕЩЁ РАЗ", StartTraining);
                ConfigureButton(_secondary, "ДОМОЙ", ShowHome);
                ConfigureButton(_share, "ПОДЕЛИТЬСЯ", ShareCurrentResult);
                _share.gameObject.SetActive(true);
            }
        }

        private void RecordScoredAttempt(double score)
        {
            if (_save == null || _save.TotalScoredAttempts == int.MaxValue) return;

            double normalized = Math.Max(0.0, Math.Min(100.0, score));
            _save.TotalScoredAttempts++;
            _save.TotalScoreSum += normalized;
            _saveRepository.Save(_save);
        }

        private async void CompleteDaily()
        {
            DailyLoadResult session = _dailySession;
            int expected = session?.Challenge?.RouteCount ?? 0;
            if (expected < 1 || _dailyReplays.Count != expected || _dailyScores.Count != expected) return;

            int revision = _navigationRevision;
            string challengeId = session.Challenge.ChallengeId;
            double visibleScore = _lastDailyScore;
            double previousBest = _save.PersonalBest;
            List<IReadOnlyList<RecordedPoint>> replays = SnapshotReplays(_dailyReplays);
            var scores = new List<double>(_dailyScores);
            _status.text = $"DAILY {visibleScore:0.0}%\nСохраняем результат…";

            try
            {
                DailyAttemptSubmissionResult submission = await _dailyService.CompleteAndSubmitAsync(
                    session,
                    replays,
                    scores,
                    false);
                _save = _dailyService.Save;
                double acceptedScore = submission.ServerScore;

                AnalyticsLifecycle.Service?.Track(AnalyticsEventNames.DailyComplete, Params(
                    "challenge_id", challengeId,
                    "score", acceptedScore,
                    "route_count", expected,
                    "verified_locally", true));
                if (_save.PersonalBest > previousBest)
                    AnalyticsLifecycle.Service?.Track(AnalyticsEventNames.PersonalBest, Params("score", _save.PersonalBest));

                if (!IsCurrentNavigation(revision, Mode.Daily)) return;
                _lastDailyScore = acceptedScore;
                _status.text = $"DAILY {_lastDailyScore:0.0}%\nСерия: {_save.Streak}";
            }
            catch (Exception error)
            {
                Debug.LogWarning($"Daily completion failed: {error.Message}");
                if (!IsCurrentNavigation(revision, Mode.Daily)) return;
                _status.text = $"DAILY {visibleScore:0.0}%\nРезультат сохранён на устройстве";
            }
        }

        private async void CompleteDuel()
        {
            DuelSession session = _duelSession;
            int expected = session?.Challenge?.RouteCount ?? 0;
            if (expected < 1 || _dailyReplays.Count != expected || _dailyScores.Count != expected) return;

            int revision = _navigationRevision;
            string challengeId = session.Challenge.ChallengeId;
            string referralId = session.Referral.ReferralId;
            double inviterScore = session.Referral.InviterScore;
            double visibleScore = _lastDailyScore;
            List<IReadOnlyList<RecordedPoint>> replays = SnapshotReplays(_dailyReplays);
            var scores = new List<double>(_dailyScores);
            _status.text = $"ТЫ: {visibleScore:0.0}%\nДРУГ: {inviterScore:0.0}%\nСчитаем результат…";

            try
            {
                DuelSubmissionResult submission = await _duelService.CompleteAsync(session, replays, scores);
                _save = _duelService.Save;

                AnalyticsLifecycle.Service?.Track(AnalyticsEventNames.ChallengeComplete, Params(
                    "challenge_id", challengeId,
                    "referrer_id", referralId,
                    "score", submission.Score,
                    "inviter_score", submission.InviterScore,
                    "route_count", expected,
                    "won", submission.Won,
                    "verified_locally", true));

                if (!IsCurrentNavigation(revision, Mode.Duel)) return;
                _lastDailyScore = submission.Score;
                string outcome = submission.Tied ? "НИЧЬЯ" : submission.Won ? "ПОБЕДА" : "ПОКА НЕ ПОБЕДИЛ";
                _status.text = $"ТЫ: {_lastDailyScore:0.0}%\nДРУГ: {submission.InviterScore:0.0}%\n{outcome}";
            }
            catch (Exception error)
            {
                Debug.LogWarning($"Duel completion failed: {error.Message}");
                if (!IsCurrentNavigation(revision, Mode.Duel)) return;
                _status.text = $"ТЫ: {visibleScore:0.0}%\nДРУГ: {inviterScore:0.0}%\nРезультат сохранён на устройстве";
            }
        }

        private void NextDailyRoute()
        {
            _dailyIndex++;
            StartCoroutine(BeginRoute(_daily.Routes[_dailyIndex]));
        }

        private async void ShareCurrentResult()
        {
            int revision = _navigationRevision;
            Mode mode = _mode;
            bool completed = _dailyCompleted;
            DailyChallengeDefinition daily = _daily;
            string challengeId = daily?.ChallengeId ?? string.Empty;
            double score = completed ? _lastDailyScore : _lastResultScore;
            string playerId = _save.AnonymousPlayerId;
            string challengeUrl = string.Empty;

            AnalyticsLifecycle.Service?.Track(AnalyticsEventNames.ShareClick, Params(
                "challenge_id", challengeId,
                "score", score));

            if (completed && daily != null)
            {
                try
                {
                    challengeUrl = await _api.CreateChallengeAsync(playerId, daily.ChallengeId, score);
                }
                catch (Exception error)
                {
                    Debug.Log($"Challenge link unavailable: {error.Message}");
                }
            }

            if (!IsCurrentNavigation(revision, mode)) return;

            string text = completed
                ? $"Я прошёл сегодняшний НЕ СБЕЙСЯ! на {score:0.0}%. Сможешь точнее?"
                : $"Я прошёл НЕ СБЕЙСЯ! на {score:0.0}%. Сможешь точнее?";
            if (!string.IsNullOrWhiteSpace(challengeUrl))
                text += "\n\n" + FormatChallengeLinks(challengeUrl);
            ShareText(text);
            AnalyticsLifecycle.Service?.Track(AnalyticsEventNames.ShareComplete, Params(
                "challenge_id", challengeId,
                "score", score,
                "has_challenge_url", !string.IsNullOrWhiteSpace(challengeUrl)));
        }

        private static string FormatChallengeLinks(string challengeUrl)
        {
            if (string.IsNullOrWhiteSpace(challengeUrl)) return string.Empty;

            string[] links = challengeUrl
                .Replace("\r", string.Empty)
                .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

            if (links.Length >= 2 &&
                links[0].StartsWith("nesbeisya://", StringComparison.OrdinalIgnoreCase) &&
                links[1].StartsWith("https://www.rustore.ru/", StringComparison.OrdinalIgnoreCase))
            {
                return "Игра уже установлена:\n" + links[0].Trim() +
                       "\n\nНет игры — установить через RuStore:\n" + links[1].Trim();
            }

            return challengeUrl.Trim();
        }

        private static void ShareText(string text)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var intentClass = new AndroidJavaClass("android.content.Intent"))
            using (var intent = new AndroidJavaObject("android.content.Intent"))
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                intent.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_SEND"));
                intent.Call<AndroidJavaObject>("setType", "text/plain");
                intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_TEXT"), text);
                var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                var chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, "Бросить вызов");
                activity.Call("startActivity", chooser);
            }
#else
            Debug.Log(text);
#endif
        }

        private bool TryScreenToFixed(Vector2 screen, out FixedPoint2 point)
        {
            point = default;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_playArea, screen, null, out Vector2 local)) return false;
            Rect rect = _playArea.rect;
            double nx = (local.x - rect.xMin) / rect.width;
            double ny = (local.y - rect.yMin) / rect.height;
            if (nx < 0 || nx > 1 || ny < 0 || ny > 1) return false;
            point = FixedPoint2.FromNormalized(nx, ny);
            return true;
        }

        private void BuildUi()
        {
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                DontDestroyOnLoad(es);
            }

            var canvasGo = new GameObject("GameCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            _title = CreateText(canvasGo.transform, "Title", 64, TextAnchor.MiddleCenter, new Vector2(0.05f, 0.88f), new Vector2(0.95f, 0.98f));
            _status = CreateText(canvasGo.transform, "Status", 50, TextAnchor.MiddleCenter, new Vector2(0.05f, 0.75f), new Vector2(0.95f, 0.87f));

            var play = new GameObject("PlayArea", typeof(RectTransform), typeof(Image));
            play.transform.SetParent(canvasGo.transform, false);
            _playArea = play.GetComponent<RectTransform>();
            SetAnchors(_playArea, new Vector2(0.06f, 0.25f), new Vector2(0.94f, 0.74f));
            play.GetComponent<Image>().color = new Color(0.035f, 0.045f, 0.07f, 1f);

            _referenceGraphic = CreateRouteGraphic(play.transform, "Reference", new Color(0.1f, 0.9f, 1f, 1f));
            _playerGraphic = CreateRouteGraphic(play.transform, "Player", new Color(1f, 0.75f, 0.15f, 1f));
            _startMarker = CreateMarker(play.transform, "Start", new Color(0.2f, 1f, 0.45f, 1f));
            _endMarker = CreateMarker(play.transform, "End", new Color(1f, 0.35f, 0.35f, 1f));

            _primary = CreateButton(canvasGo.transform, "Primary", new Vector2(0.08f, 0.12f), new Vector2(0.92f, 0.20f));
            _secondary = CreateButton(canvasGo.transform, "Secondary", new Vector2(0.08f, 0.035f), new Vector2(0.48f, 0.105f));
            _share = CreateButton(canvasGo.transform, "Share", new Vector2(0.52f, 0.035f), new Vector2(0.92f, 0.105f));
        }

        private void PositionMarkers(RouteDefinition route)
        {
            PositionMarker(_startMarker.rectTransform, route.ReferencePoints[0]);
            PositionMarker(_endMarker.rectTransform, route.ReferencePoints[route.ReferencePoints.Count - 1]);
        }

        private static void PositionMarker(RectTransform marker, FixedPoint2 point)
        {
            Vector2 anchor = new Vector2((float)point.NormalizedX, (float)point.NormalizedY);
            marker.anchorMin = anchor;
            marker.anchorMax = anchor;
            marker.anchoredPosition = Vector2.zero;
        }

        private void SetMarkersVisible(bool visible)
        {
            if (_startMarker != null) _startMarker.gameObject.SetActive(visible);
            if (_endMarker != null) _endMarker.gameObject.SetActive(visible);
        }

        private void HideButtons()
        {
            _primary.gameObject.SetActive(false);
            _secondary.gameObject.SetActive(false);
            _share.gameObject.SetActive(false);
        }

        private static RouteGraphic CreateRouteGraphic(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(RouteGraphic));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            SetAnchors(rt, Vector2.zero, Vector2.one);
            var graphic = go.GetComponent<RouteGraphic>();
            graphic.color = color;
            graphic.raycastTarget = false;
            return graphic;
        }

        private static Image CreateMarker(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(46, 46);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text CreateText(Transform parent, string name, int size, TextAnchor anchor, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            SetAnchors(go.GetComponent<RectTransform>(), min, max);
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.alignment = anchor;
            text.color = Color.white;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 18;
            text.resizeTextMaxSize = size;
            return text;
        }

        private static Button CreateButton(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            SetAnchors(go.GetComponent<RectTransform>(), min, max);
            go.GetComponent<Image>().color = new Color(0.12f, 0.22f, 0.32f, 1f);
            var label = CreateText(go.transform, "Label", 36, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
            label.raycastTarget = false;
            return go.GetComponent<Button>();
        }

        private static void ConfigureButton(Button button, string label, UnityEngine.Events.UnityAction action)
        {
            button.gameObject.SetActive(true);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
            button.GetComponentInChildren<Text>().text = label;
        }

        private static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private int BeginNavigation()
        {
            unchecked { _navigationRevision++; }
            return _navigationRevision;
        }

        private bool IsCurrentNavigation(int revision) => revision == _navigationRevision;

        private bool IsCurrentNavigation(int revision, Mode mode) =>
            revision == _navigationRevision && _mode == mode;

        private static List<IReadOnlyList<RecordedPoint>> SnapshotReplays(
            IReadOnlyList<IReadOnlyList<RecordedPoint>> source)
        {
            var result = new List<IReadOnlyList<RecordedPoint>>(source?.Count ?? 0);
            if (source == null) return result;
            for (int i = 0; i < source.Count; i++)
                result.Add(source[i] == null ? null : new List<RecordedPoint>(source[i]));
            return result;
        }

        private static Dictionary<string, object> Params(params object[] pairs)
        {
            var result = new Dictionary<string, object>();
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                string key = pairs[i]?.ToString();
                if (!string.IsNullOrWhiteSpace(key)) result[key] = pairs[i + 1];
            }
            return result;
        }

        private static long NowMs() => (long)(Time.realtimeSinceStartupAsDouble * 1000.0);
    }
}
