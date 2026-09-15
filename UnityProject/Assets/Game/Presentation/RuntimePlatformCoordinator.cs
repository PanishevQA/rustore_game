using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DontGetSidetracked.Analytics;
using DontGetSidetracked.Core;
using DontGetSidetracked.Daily;
using DontGetSidetracked.Network;
using DontGetSidetracked.Platform.RuStore;
using UnityEngine;
using UnityEngine.UI;

namespace DontGetSidetracked.Presentation
{
    /// <summary>
    /// Coordinates platform flows only at safe UI points. Gameplay rules never depend on RuStore SDKs.
    /// </summary>
    public sealed class RuntimePlatformCoordinator : MonoBehaviour
    {
        private JsonFileSaveRepository _saveRepository;
        private BootstrapRemoteConfigService _config;
        private RuStoreUpdateService _updateService;
        private RuStoreReviewService _reviewService;
        private ReviewPolicy _reviewPolicy;
        private GameBootstrap _bootstrap;
        private FieldInfo _modeField;
        private FieldInfo _stateField;
        private int _observedCompletedDaily;
        private bool _reviewInFlight;
        private bool _mandatoryUpdate;
        private bool _updateInFlight;
        private float _nextReviewPoll;
        private GameObject _updateBlocker;
        private Text _updateStatus;

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
            _reviewPolicy = new ReviewPolicy();
            ResolveBootstrap();
            StartCoroutine(InitializeWhenHome());
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextReviewPoll) return;
            _nextReviewPoll = Time.unscaledTime + 1f;
            if (_mandatoryUpdate || _reviewInFlight || !IsSafeHome()) return;
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
                Debug.Log($"Remote config refresh failed; using cache/defaults: {error.Message}");
            }

            _mandatoryUpdate = _config.RequiresMandatoryUpdate(Application.version);
            bool recommendedUpdate = _config.ShouldRecommendUpdate(Application.version);

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

        private async void TryRequestReviewAfterPositiveDaily()
        {
            SaveData save = _saveRepository.Load();
            if (save.CompletedDailyCount <= _observedCompletedDaily) return;

            // Consume the completion edge only at Home. A failed review can be attempted after a future positive Daily,
            // but never repeatedly in the same result screen/session tick.
            _observedCompletedDaily = save.CompletedDailyCount;
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
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
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

            var buttonGo = new GameObject("Retry", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonGo.transform.SetParent(_updateBlocker.transform, false);
            RectTransform buttonRt = buttonGo.GetComponent<RectTransform>();
            buttonRt.anchorMin = new Vector2(0.18f, 0.31f);
            buttonRt.anchorMax = new Vector2(0.82f, 0.39f);
            buttonRt.offsetMin = Vector2.zero;
            buttonRt.offsetMax = Vector2.zero;
            buttonGo.GetComponent<Image>().color = new Color(0.12f, 0.22f, 0.32f, 1f);
            buttonGo.GetComponent<Button>().onClick.AddListener(RetryMandatoryUpdate);
            Text label = CreateText(buttonGo.transform, "Label", 34, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
            label.text = "ПРОВЕРИТЬ ЕЩЁ РАЗ";
            label.raycastTarget = false;
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
