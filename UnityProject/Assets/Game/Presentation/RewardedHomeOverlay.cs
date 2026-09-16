using System;
using System.Collections.Generic;
using System.Reflection;
using DontGetSidetracked.Analytics;
using DontGetSidetracked.Core;
using DontGetSidetracked.Platform.RuStore;
using DontGetSidetracked.Services;
using UnityEngine;
using UnityEngine.UI;

namespace DontGetSidetracked.Presentation
{
    /// <summary>
    /// Small opt-in rewarded surface for the offline MVP. A completed reward grants one local hint.
    /// It is visible only on Home and never interrupts an active game session.
    /// </summary>
    public sealed class RewardedHomeOverlay : MonoBehaviour
    {
        private JsonFileSaveRepository _saveRepository;
        private IRemoteConfigService _config;
        private GameBootstrap _bootstrap;
        private FieldInfo _modeField;
        private FieldInfo _stateField;
        private GameObject _canvas;
        private Button _button;
        private Text _label;
        private bool _inProgress;
        private float _nextPoll;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (FindFirstObjectByType<RewardedHomeOverlay>() != null) return;
            var root = new GameObject("RewardedHomeOverlay");
            DontDestroyOnLoad(root);
            root.AddComponent<RewardedHomeOverlay>();
        }

        private void Awake()
        {
            _saveRepository = new JsonFileSaveRepository();
            _config = RuStoreRemoteConfigRuntime.Service;
            BuildUi();
            ResolveBootstrap();
            SetVisible(false);
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextPoll) return;
            _nextPoll = Time.unscaledTime + 0.25f;
            if (_bootstrap == null) ResolveBootstrap();

            IAdService ads = AdRuntimeCoordinator.Ads;
            bool visible = !_inProgress &&
                           _config.GetBool("rewarded_enabled", true) &&
                           ads != null &&
                           ads.IsRewardedReady &&
                           IsSafeHome() &&
                           !IsMetaPanelOpen();
            SetVisible(visible);
            if (visible) RefreshLabel();
        }

        private async void ClaimRewardedHint()
        {
            if (_inProgress) return;
            IAdService ads = AdRuntimeCoordinator.Ads;
            if (ads == null || !ads.IsRewardedReady || !IsSafeHome()) return;

            _inProgress = true;
            _button.interactable = false;
            AnalyticsLifecycle.Service?.Track(AnalyticsEventNames.RewardedOffer,
                new Dictionary<string, object> { ["placement"] = "free_hint_home" });
            AnalyticsLifecycle.Service?.Track(AnalyticsEventNames.RewardedStart,
                new Dictionary<string, object> { ["placement"] = "free_hint_home" });

            try
            {
                bool rewarded = await ads.ShowRewardedAsync(RewardPlacement.FreeHint);
                if (rewarded)
                {
                    SaveData save = _saveRepository.Load();
                    save.Hints++;
                    _saveRepository.Save(save);
                    AnalyticsLifecycle.Service?.Track(AnalyticsEventNames.RewardedComplete,
                        new Dictionary<string, object>
                        {
                            ["placement"] = "free_hint_home",
                            ["reward"] = "hint",
                            ["hints"] = save.Hints
                        });
                }
            }
            catch (Exception error)
            {
                Debug.LogWarning($"Rewarded ad unavailable: {error.Message}");
            }
            finally
            {
                _inProgress = false;
                if (_button != null) _button.interactable = true;
                RefreshLabel();
            }
        }

        private void RefreshLabel()
        {
            if (_label == null) return;
            SaveData save = _saveRepository.Load();
            _label.text = $"🎁 +1 ПОДСКАЗКА ЗА РЕКЛАМУ   •   {save.Hints}";
        }

        private bool IsSafeHome()
        {
            if (_bootstrap == null || _modeField == null || _stateField == null) return false;
            object mode = _modeField.GetValue(_bootstrap);
            object state = _stateField.GetValue(_bootstrap);
            return mode != null && state != null &&
                   string.Equals(mode.ToString(), "Home", StringComparison.Ordinal) &&
                   string.Equals(state.ToString(), "Idle", StringComparison.Ordinal);
        }

        private static bool IsMetaPanelOpen()
        {
            MetaMenuOverlay meta = FindFirstObjectByType<MetaMenuOverlay>();
            return meta != null && meta.IsPanelOpen;
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

        private void BuildUi()
        {
            _canvas = new GameObject("RewardedCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvas.transform.SetParent(transform, false);
            Canvas canvas = _canvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 35;
            CanvasScaler scaler = _canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            var go = new GameObject("RewardedHint", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(_canvas.transform, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            // Dedicated strip between the Home hero (ends around .305) and the Levels CTA (ends at .270).
            rect.anchorMin = new Vector2(0.20f, 0.276f);
            rect.anchorMax = new Vector2(0.80f, 0.301f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = new Color(0.10f, 0.18f, 0.27f, 0.96f);
            _button = go.GetComponent<Button>();
            _button.onClick.AddListener(ClaimRewardedHint);

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            RectTransform textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            _label = textGo.GetComponent<Text>();
            _label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _label.fontSize = 24;
            _label.alignment = TextAnchor.MiddleCenter;
            _label.color = Color.white;
            _label.resizeTextForBestFit = true;
            _label.resizeTextMinSize = 15;
            _label.resizeTextMaxSize = 24;
            _label.raycastTarget = false;
            RefreshLabel();
        }

        private void SetVisible(bool visible)
        {
            if (_canvas != null && _canvas.activeSelf != visible) _canvas.SetActive(visible);
        }
    }
}
