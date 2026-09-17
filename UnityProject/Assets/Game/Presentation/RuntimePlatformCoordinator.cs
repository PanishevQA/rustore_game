using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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
        private FieldInfo _modeField;
        private FieldInfo _stateField;
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
                ShowMandatoryUpdateBlocker("Для продолжения нужна новая версия игры.");
                await TryRunUpdateAsync(true);
            }
            else if (recommendedUpdate)
            {
                await TryRunUpdateAsync(false);
            }
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
            if (_bootstrap == null)
            {
                _modeField = null;
                _stateField = null;
                return;
            }

            Type type = typeof(GameBootstrap);
            _modeField = type.GetField("_mode", BindingFlags.Instance | BindingFlags.NonPublic);
            _stateField = type.GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        private bool IsSafeHome()
        {
            if (_bootstrap == null || _modeField == null || _stateField == null)
            {
                ResolveBootstrap();
                if (_bootstrap == null || _modeField == null || _stateField == null) return false;
            }

            object mode = _modeField.GetValue(_bootstrap);
            object state = _stateField.GetValue(_bootstrap);
            return mode != null && state != null &&
                   string.Equals(mode.ToString(), "Home", StringComparison.Ordinal) &&
                   string.Equals(state.ToString(), "Idle", StringComparison.Ordinal);
        }

        private void ShowNotificationValuePrompt()
        {
            if (_notificationPrompt == null) BuildNotificationValuePrompt();
            _notificationPromptOpen = true;
            _notificationPrompt.SetActive(true);
        }

        private void HideNotificationValuePrompt()
        {
            _notificationPromptOpen = false;
            if (_notificationPrompt != null) _notificationPrompt.SetActive(false);
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

            _notificationPrompt = new GameObject("Prompt", typeof(RectTransform), typeof(Image));
            _notificationPrompt.transform.SetParent(canvasGo.transform, false);
            RectTransform panel = _notificationPrompt.GetComponent<RectTransform>();
            panel.anchorMin = new Vector2(0.08f, 0.30f);
            panel.anchorMax = new Vector2(0.92f, 0.70f);
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
            _notificationPrompt.GetComponent<Image>().color = new Color(0.025f, 0.04f, 0.065f, 0.99f);

            Text title = CreateText(_notificationPrompt.transform, "Title", 52, TextAnchor.MiddleCenter,
                new Vector2(0.08f, 0.68f), new Vector2(0.92f, 0.90f));
            title.text = "НЕ ПРОПУСКАТЬ DAILY?";
            Text body = CreateText(_notificationPrompt.transform, "Body", 32, TextAnchor.MiddleCenter,
                new Vector2(0.10f, 0.38f), new Vector2(0.90f, 0.68f));
            body.text = "Разрешить локальное напоминание, когда появится новый Daily Challenge?";

            CreateButton(_notificationPrompt.transform, "ВКЛЮЧИТЬ", new Vector2(0.10f, 0.12f), new Vector2(0.57f, 0.30f), AcceptNotificationValuePrompt);
            CreateButton(_notificationPrompt.transform, "БЕЗ НАПОМИНАНИЙ", new Vector2(0.60f, 0.12f), new Vector2(0.90f, 0.30f), DeclineNotificationValuePrompt);
            _notificationPrompt.SetActive(false);
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

            _updateBlocker = new GameObject("Blocker", typeof(RectTransform), typeof(Image));
            _updateBlocker.transform.SetParent(canvasGo.transform, false);
            RectTransform panel = _updateBlocker.GetComponent<RectTransform>();
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
            _updateBlocker.GetComponent<Image>().color = new Color(0.02f, 0.03f, 0.05f, 0.99f);

            Text title = CreateText(_updateBlocker.transform, "Title", 66, TextAnchor.MiddleCenter,
                new Vector2(0.08f, 0.62f), new Vector2(0.92f, 0.75f));
            title.text = "ТРЕБУЕТСЯ ОБНОВЛЕНИЕ";
            _updateStatus = CreateText(_updateBlocker.transform, "Status", 36, TextAnchor.MiddleCenter,
                new Vector2(0.10f, 0.43f), new Vector2(0.90f, 0.60f));

            CreateButton(_updateBlocker.transform, "ПРОВЕРИТЬ ЕЩЁ РАЗ",
                new Vector2(0.18f, 0.31f), new Vector2(0.82f, 0.39f), RetryMandatoryUpdate);
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
