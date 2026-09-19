using System;
using System.Collections;
using System.Collections.Generic;
using DontGetSidetracked.Analytics;
using DontGetSidetracked.Core;
using DontGetSidetracked.Daily;
using DontGetSidetracked.Network;
using DontGetSidetracked.Platform.Android;
using DontGetSidetracked.Platform.RuStore;
using DontGetSidetracked.Services;
using UnityEngine;
using UnityEngine.UI;

namespace DontGetSidetracked.Presentation
{
    /// <summary>
    /// Coordinates platform flows only at safe UI points. Gameplay rules never depend on platform SDKs.
    /// </summary>
    public sealed class RuntimePlatformCoordinator : MonoBehaviour
    {
        private JsonFileSaveRepository _saveRepository;
        private BootstrapRemoteConfigService _config;
        private RuStoreUpdateService _updateService;
        private RuStoreReviewService _reviewService;
        private INotificationPermissionService _notificationPermission;
        private LocalDailyNotificationScheduler _localNotificationScheduler;
        private ReviewPolicy _reviewPolicy;
        private GameBootstrap _bootstrap;
        private int _observedCompletedDaily;
        private bool _reviewInFlight;
        private bool _mandatoryUpdate;
        private bool _updateInFlight;
        private bool _localReminderEnabled;
        private bool _notificationPromptOpen;
        private bool _notificationPromptDeferredForSession;
        private bool _notificationRequestPending;
        private float _permissionResultEarliestTime;
        private float _nextPolicyPoll;
        private GameObject _updateBlocker;
        private Text _updateStatus;
        private GameObject _notificationBackdrop;
        private GameObject _notificationPrompt;

        public bool IsNotificationPromptOpen => _notificationPromptOpen;

        /// <summary>
        /// Android Back dismisses the explanatory prompt only for the current session. This is different
        /// from the explicit "БЕЗ НАПОМИНАНИЙ" choice, which persists the user's refusal.
        /// </summary>
        public bool DismissNotificationPrompt()
        {
            if (!_notificationPromptOpen) return false;
            _notificationPromptDeferredForSession = true;
            HideNotificationValuePrompt();
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (FindFirstObjectByType<RuntimePlatformCoordinator>() != null) return;
            var root = new GameObject("RuntimePlatformCoordinator");
            DontDestroyOnLoad(root);
            root.AddComponent<RuntimePlatformCoordinator>();
        }

        private void Awake()
        {
            _saveRepository = new JsonFileSaveRepository();
            SaveData save = _saveRepository.Load();
            _observedCompletedDaily = save.CompletedDailyCount;
            _config = new BootstrapRemoteConfigService(GameRuntimeSettings.BackendBaseUrl);
            _updateService = new RuStoreUpdateService();
            _reviewService = new RuStoreReviewService();
            _notificationPermission = new AndroidNotificationPermissionService();
            _localNotificationScheduler = new LocalDailyNotificationScheduler();
            _reviewPolicy = new ReviewPolicy();
            ResolveBootstrap();
            StartCoroutine(InitializeWhenHome());
        }

        private void Update()
        {
            if (_notificationRequestPending &&
                Time.unscaledTime >= _permissionResultEarliestTime &&
                Application.isFocused)
            {
                CompleteNotificationPermissionRequest();
            }

            if (Time.unscaledTime < _nextPolicyPoll) return;
            _nextPolicyPoll = Time.unscaledTime + 1f;
            if (_mandatoryUpdate || _reviewInFlight || _notificationPromptOpen || !IsSafeHome()) return;

            TryOfferNotificationPermission();
            if (!_notificationPromptOpen)
                TryRequestReviewAfterPositiveDaily();
        }

        private IEnumerator InitializeWhenHome()
        {
            while (!IsSafeHome())
            {
                ResolveBootstrap();
                yield return new WaitForSecondsRealtime(0.25f);
            }
            InitializePlatformAsync();
        }

        private async void InitializePlatformAsync()
        {
            try
            {
                await _config.RefreshAsync();
            }
            catch (Exception error)
            {
                Debug.Log($"Config refresh skipped; using local defaults/cache: {error.Message}");
            }

            _localReminderEnabled = _config.GetBool("local_daily_reminder_enabled", true);
            _mandatoryUpdate = _config.RequiresMandatoryUpdate(Application.version);
            bool recommendedUpdate = _config.ShouldRecommendUpdate(Application.version);

            SaveData save = _saveRepository.Load();
            if (_notificationPermission.IsGranted && !save.NotificationPermissionGranted)
            {
                save.NotificationPermissionGranted = true;
                _saveRepository.Save(save);
            }

            if (_localReminderEnabled && save.CompletedDailyCount >= 1 && _notificationPermission.IsGranted)
                ScheduleLocalReminder();
            else if (!_localReminderEnabled)
                _localNotificationScheduler.Cancel();

            if (_mandatoryUpdate)
            {
                StartCoroutine(EnforceMandatoryUpdateWhenSafeHome());
            }
            else if (recommendedUpdate)
            {
                await TryRunUpdateAsync(false);
            }
        }

        private IEnumerator EnforceMandatoryUpdateWhenSafeHome()
        {
            while (!IsSafeHome())
                yield return new WaitForSecondsRealtime(0.20f);

            ShowMandatoryUpdateBlocker("Для продолжения нужна новая версия игры.");
            _ = TryRunUpdateAsync(true);
        }

        private async System.Threading.Tasks.Task TryRunUpdateAsync(bool mandatory)
        {
            if (_updateInFlight) return;
            _updateInFlight = true;
            try
            {
                await _updateService.CheckForUpdateAsync(mandatory);
                if (mandatory && !_updateService.IsUpdateAvailable && !_updateService.IsUpdateInProgress)
                    SetUpdateBlockerStatus("Версия больше не поддерживается. Обновление пока недоступно через SDK. Повторите проверку позже.");
            }
            catch (Exception error)
            {
                Debug.LogWarning($"RuStore update flow unavailable: {error.Message}");
                if (mandatory)
                    SetUpdateBlockerStatus("Не удалось проверить обновление. Подключитесь к сети и повторите.");
            }
            finally
            {
                _updateInFlight = false;
            }
        }

        private async void RetryMandatoryUpdate()
        {
            if (!_mandatoryUpdate || _updateInFlight) return;
            SetUpdateBlockerStatus("Проверяем обновление…");
            await TryRunUpdateAsync(true);
        }

        private void TryOfferNotificationPermission()
        {
            if (!_localReminderEnabled || _notificationRequestPending || _notificationPromptDeferredForSession) return;

            SaveData save = _saveRepository.Load();
            if (save.CompletedDailyCount < 1 || save.NotificationValuePromptShown) return;

            if (!_notificationPermission.IsRuntimePermissionRequired || _notificationPermission.IsGranted)
            {
                save.NotificationValuePromptShown = true;
                save.NotificationPermissionGranted = true;
                _saveRepository.Save(save);
                ScheduleLocalReminder();
                return;
            }

            ShowNotificationValuePrompt();
        }

        private void AcceptNotificationValuePrompt()
        {
            SaveData save = _saveRepository.Load();
            save.NotificationValuePromptShown = true;
            _saveRepository.Save(save);
            HideNotificationValuePrompt();

            AnalyticsLifecycle.Service?.Track(AnalyticsEventNames.PushPermissionRequest,
                new Dictionary<string, object>
                {
                    ["completed_daily"] = save.CompletedDailyCount,
                    ["session_number"] = save.SessionNumber,
                    ["purpose"] = "local_daily_reminder"
                });

            if (!_notificationPermission.IsRuntimePermissionRequired)
            {
                save.NotificationPermissionGranted = true;
                _saveRepository.Save(save);
                ScheduleLocalReminder();
                return;
            }

            _notificationRequestPending = true;
            _permissionResultEarliestTime = Time.unscaledTime + 0.75f;
            _notificationPermission.Request();
        }

        private void DeclineNotificationValuePrompt()
        {
            SaveData save = _saveRepository.Load();
            save.NotificationValuePromptShown = true;
            save.NotificationPermissionGranted = false;
            _saveRepository.Save(save);
            HideNotificationValuePrompt();
            _localNotificationScheduler.Cancel();

            AnalyticsLifecycle.Service?.Track(AnalyticsEventNames.PushPermissionResult,
                new Dictionary<string, object>
                {
                    ["granted"] = false,
                    ["stage"] = "value_prompt",
                    ["purpose"] = "local_daily_reminder"
                });
        }

        private void CompleteNotificationPermissionRequest()
        {
            _notificationRequestPending = false;
            bool granted = _notificationPermission.IsGranted;
            SaveData save = _saveRepository.Load();
            save.NotificationPermissionGranted = granted;
            _saveRepository.Save(save);

            if (granted) ScheduleLocalReminder();
            else _localNotificationScheduler.Cancel();

            AnalyticsLifecycle.Service?.Track(AnalyticsEventNames.PushPermissionResult,
                new Dictionary<string, object>
                {
                    ["granted"] = granted,
                    ["stage"] = "android_runtime",
                    ["purpose"] = "local_daily_reminder"
                });
        }

        private void ScheduleLocalReminder()
        {
            if (!_localReminderEnabled || !_notificationPermission.IsGranted) return;
            int hour = _config.GetInt("daily_reminder_hour", 10);
            try
            {
                _localNotificationScheduler.ScheduleNext(hour);
            }
            catch (Exception error)
            {
                Debug.LogWarning($"Local Daily reminder was not scheduled: {error.Message}");
            }
        }

        private async void TryRequestReviewAfterPositiveDaily()
        {
            SaveData save = _saveRepository.Load();
            if (save.CompletedDailyCount <= _observedCompletedDaily) return;

            _observedCompletedDaily = save.CompletedDailyCount;
            if (_localReminderEnabled && save.NotificationPermissionGranted)
                ScheduleLocalReminder();

            int minSessions = _config.GetInt("review_min_sessions", 5);
            if (save.SessionNumber < minSessions) return;
            if (!_reviewPolicy.ShouldRequest(save, save.PersonalBest)) return;

            _reviewInFlight = true;
            AnalyticsLifecycle.Service?.Track(AnalyticsEventNames.ReviewFlowRequest,
                new Dictionary<string, object>
                {
                    ["session_number"] = save.SessionNumber,
                    ["completed_daily"] = save.CompletedDailyCount,
                    ["streak"] = save.Streak,
                    ["personal_best"] = save.PersonalBest
                });

            try
            {
                await _reviewService.RequestReviewAsync();
                _reviewPolicy.MarkRequested(save);
                _saveRepository.Save(save);
            }
            catch (Exception error)
            {
                Debug.LogWarning($"RuStore review flow unavailable: {error.Message}");
            }
            finally
            {
                _reviewInFlight = false;
            }
        }

        private void ResolveBootstrap()
        {
            _bootstrap = FindFirstObjectByType<GameBootstrap>();
        }

        private bool IsSafeHome()
        {
            if (_bootstrap == null) ResolveBootstrap();
            if (_bootstrap == null || !GameBootstrapRuntimeBridge.IsPlainHome(_bootstrap)) return false;

            DailyIntroCoordinator dailyIntro = FindFirstObjectByType<DailyIntroCoordinator>();
            if (dailyIntro != null && dailyIntro.IsOpen) return false;

            MetaMenuOverlay meta = FindFirstObjectByType<MetaMenuOverlay>();
            if (meta != null && meta.IsPanelOpen) return false;

            TrainingMenuCoordinator training = FindFirstObjectByType<TrainingMenuCoordinator>();
            if (training != null && training.IsOpen) return false;

            CampaignLevelMenuOverlay campaign = FindFirstObjectByType<CampaignLevelMenuOverlay>();
            if (campaign != null && campaign.IsOpen) return false;

            ReferralOfferCoordinator referral = FindFirstObjectByType<ReferralOfferCoordinator>();
            return referral == null || !referral.IsVisible;
        }

        private void ShowNotificationValuePrompt()
        {
            if (_notificationPrompt == null) BuildNotificationValuePrompt();
            _notificationPromptOpen = true;
            if (_notificationBackdrop != null) _notificationBackdrop.SetActive(true);
            _notificationPrompt.SetActive(true);
        }

        private void HideNotificationValuePrompt()
        {
            _notificationPromptOpen = false;
            if (_notificationPrompt != null) _notificationPrompt.SetActive(false);
            if (_notificationBackdrop != null) _notificationBackdrop.SetActive(false);
        }

        private void BuildNotificationValuePrompt()
        {
            var canvasGo = new GameObject("NotificationValueCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            var releaseVisual = new GameObject("ReleaseVisual", typeof(RectTransform));
            releaseVisual.transform.SetParent(canvasGo.transform, false);
            ReleaseUiKit.Stretch(releaseVisual.GetComponent<RectTransform>());

            var backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
            backdrop.transform.SetParent(canvasGo.transform, false);
            _notificationBackdrop = backdrop;
            ReleaseUiKit.Stretch(backdrop.GetComponent<RectTransform>());
            Image backdropImage = backdrop.GetComponent<Image>();
            backdropImage.color = new Color(0.003f, 0.008f, 0.023f, 0.86f);
            backdropImage.raycastTarget = true;

            Image panel = ReleaseUiKit.Panel(
                canvasGo.transform,
                "Prompt",
                new Vector2(0.075f, 0.285f),
                new Vector2(0.925f, 0.715f),
                ReleaseUiKit.Surface,
                ReleaseUiKit.Cyan,
                true);
            _notificationPrompt = panel.gameObject;
            _notificationPrompt.AddComponent<ReleasePanelMotion>();

            ReleaseUiKit.TextBlock(panel.transform, "Kicker", "DAILY CHALLENGE", 19,
                TextAnchor.MiddleCenter, new Vector2(0.10f, 0.79f), new Vector2(0.90f, 0.90f),
                ReleaseUiKit.Cyan, FontStyle.Bold);

            Text title = ReleaseUiKit.TextBlock(panel.transform, "Title", "НЕ ПРОПУСКАТЬ DAILY?", 43,
                TextAnchor.MiddleCenter, new Vector2(0.08f, 0.61f), new Vector2(0.92f, 0.79f),
                ReleaseUiKit.Text, FontStyle.Bold);
            ReleaseUiKit.AddTextShadow(title, 0.38f, -3f);

            ReleaseUiKit.TextBlock(panel.transform, "Body",
                "После нового Daily игра сможет напомнить о нём локальным уведомлением. Никакие контакты или геолокация не нужны.",
                25, TextAnchor.MiddleCenter, new Vector2(0.09f, 0.36f), new Vector2(0.91f, 0.61f),
                ReleaseUiKit.Muted);

            ReleaseUiKit.Button(panel.transform, "Enable", "ВКЛЮЧИТЬ НАПОМИНАНИЯ",
                new Vector2(0.09f, 0.18f), new Vector2(0.91f, 0.31f),
                ReleaseUiKit.Cyan, new Color(0.01f, 0.03f, 0.05f, 1f), 24, AcceptNotificationValuePrompt);

            ReleaseUiKit.Button(panel.transform, "Decline", "БЕЗ НАПОМИНАНИЙ",
                new Vector2(0.29f, 0.065f), new Vector2(0.71f, 0.145f),
                new Color(0.075f, 0.095f, 0.145f, 0.98f), ReleaseUiKit.Muted, 20, DeclineNotificationValuePrompt);

            _notificationPrompt.SetActive(false);
            _notificationBackdrop.SetActive(false);
        }

        private void ShowMandatoryUpdateBlocker(string message)
        {
            if (_updateBlocker == null) BuildUpdateBlocker();
            _updateBlocker.SetActive(true);
            SetUpdateBlockerStatus(message);
        }

        private void SetUpdateBlockerStatus(string message)
        {
            if (_updateStatus != null) _updateStatus.text = message;
        }

        private void BuildUpdateBlocker()
        {
            var canvasGo = new GameObject("MandatoryUpdateCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            _updateBlocker = new GameObject("Blocker", typeof(RectTransform), typeof(Image));
            _updateBlocker.transform.SetParent(canvasGo.transform, false);
            ReleaseUiKit.Stretch(_updateBlocker.GetComponent<RectTransform>());
            _updateBlocker.AddComponent<ReleasePanelMotion>();
            Image background = _updateBlocker.GetComponent<Image>();
            background.color = ReleaseUiKit.Background;
            background.raycastTarget = true;

            ReleaseUiKit.Dot(_updateBlocker.transform, "Signal", ReleaseUiKit.Danger,
                new Vector2(0.44f, 0.735f), new Vector2(0.56f, 0.805f));

            ReleaseUiKit.TextBlock(_updateBlocker.transform, "Kicker", "ВЕРСИЯ УСТАРЕЛА", 20,
                TextAnchor.MiddleCenter, new Vector2(0.20f, 0.68f), new Vector2(0.80f, 0.73f),
                ReleaseUiKit.Danger, FontStyle.Bold);

            Text title = ReleaseUiKit.TextBlock(_updateBlocker.transform, "Title", "НУЖНО ОБНОВЛЕНИЕ", 52,
                TextAnchor.MiddleCenter, new Vector2(0.08f, 0.575f), new Vector2(0.92f, 0.68f),
                ReleaseUiKit.Text, FontStyle.Bold);
            ReleaseUiKit.AddTextShadow(title, 0.45f, -3f);

            Image statusCard = ReleaseUiKit.Panel(_updateBlocker.transform, "StatusCard",
                new Vector2(0.10f, 0.405f), new Vector2(0.90f, 0.555f),
                ReleaseUiKit.Surface, ReleaseUiKit.Danger, false);

            _updateStatus = ReleaseUiKit.TextBlock(statusCard.transform, "Status", string.Empty, 25,
                TextAnchor.MiddleCenter, new Vector2(0.06f, 0.12f), new Vector2(0.94f, 0.88f),
                ReleaseUiKit.Muted);

            ReleaseUiKit.Button(_updateBlocker.transform, "Retry", "ПРОВЕРИТЬ ОБНОВЛЕНИЕ",
                new Vector2(0.19f, 0.300f), new Vector2(0.81f, 0.365f),
                ReleaseUiKit.Violet, ReleaseUiKit.Text, 24, RetryMandatoryUpdate);

            ReleaseUiKit.TextBlock(_updateBlocker.transform, "Footnote",
                "Прогресс хранится на устройстве и останется после обновления.", 19,
                TextAnchor.MiddleCenter, new Vector2(0.12f, 0.235f), new Vector2(0.88f, 0.285f),
                ReleaseUiKit.Muted);
        }

        private static Button CreateButton(
            Transform parent,
            string label,
            Vector2 min,
            Vector2 max,
            UnityEngine.Events.UnityAction action)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = new Color(0.12f, 0.22f, 0.32f, 1f);
            Button button = go.GetComponent<Button>();
            button.onClick.AddListener(action);
            Text text = CreateText(go.transform, "Label", 30, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
            text.text = label;
            text.raycastTarget = false;
            return button;
        }

        private static Text CreateText(Transform parent, string name, int size, TextAnchor anchor, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            Text text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.alignment = anchor;
            text.color = Color.white;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 16;
            text.resizeTextMaxSize = size;
            return text;
        }
    }
}
