using System;
using System.Collections.Generic;
using System.Reflection;
using DontGetSidetracked.Analytics;
using DontGetSidetracked.Core;
using DontGetSidetracked.Economy;
using DontGetSidetracked.Monetization;
using DontGetSidetracked.Platform.RuStore;
using DontGetSidetracked.Services;
using UnityEngine;

namespace DontGetSidetracked.Presentation
{
    /// <summary>
    /// Runtime bridge for ads. It observes presentation state without leaking the ad SDK into gameplay.
    /// Interstitials are considered only after a completed round and only after returning to an idle Home screen.
    /// </summary>
    public sealed class AdRuntimeCoordinator : MonoBehaviour
    {
        public static IAdService Ads { get; private set; }

        private JsonFileSaveRepository _saveRepository;
        private IRemoteConfigService _config;
        private YandexMobileAdsService _yandex;
        private InterstitialController _interstitial;
        private GameBootstrap _bootstrap;
        private FieldInfo _modeField;
        private FieldInfo _stateField;
        private string _lastState = string.Empty;
        private bool _showInProgress;
        private float _nextPoll;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (FindFirstObjectByType<AdRuntimeCoordinator>() != null) return;
            var root = new GameObject("AdRuntimeCoordinator");
            DontDestroyOnLoad(root);
            root.AddComponent<AdRuntimeCoordinator>();
        }

        private void Awake()
        {
            _saveRepository = new JsonFileSaveRepository();
            _config = RuStoreRemoteConfigRuntime.Service;

            _yandex = new YandexMobileAdsService(
                YandexMobileAdsSettings.RewardedUnitId,
                YandexMobileAdsSettings.InterstitialUnitId);
            Ads = _yandex;
            _interstitial = new InterstitialController(_yandex, _config);
            ResolveBootstrap();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextPoll) return;
            _nextPoll = Time.unscaledTime + 0.25f;
            if (_bootstrap == null) ResolveBootstrap();
            if (_bootstrap == null || _modeField == null || _stateField == null) return;

            string state = _stateField.GetValue(_bootstrap)?.ToString() ?? string.Empty;
            if (!string.Equals(state, _lastState, StringComparison.Ordinal))
            {
                if (string.Equals(state, "Result", StringComparison.Ordinal))
                    _interstitial.NotifyRoundCompleted();
                _lastState = state;
            }

            if (_showInProgress || !IsSafeHome()) return;
            if (IsMetaPanelOpen()) return;

            SaveData save = _saveRepository.Load();
            bool removeAds = save.Entitlements != null &&
                             (save.Entitlements.Contains(ProductIds.RemoveAds) ||
                              save.Entitlements.Contains(ProductIds.StarterPack));
            if (!_interstitial.CanShowBetweenSessions(removeAds, gameplayActive: false)) return;

            ShowInterstitialSafeAsync();
        }

        private async void ShowInterstitialSafeAsync()
        {
            if (_showInProgress) return;
            _showInProgress = true;
            try
            {
                bool shown = await _interstitial.TryShowBetweenSessionsAsync(
                    removeAdsEntitlement: HasRemoveAds(),
                    gameplayActive: !IsSafeHome());
                if (shown)
                {
                    AnalyticsLifecycle.Service?.Track(AnalyticsEventNames.InterstitialShow,
                        new Dictionary<string, object>
                        {
                            ["surface"] = "home_between_sessions"
                        });
                }
            }
            catch (Exception error)
            {
                Debug.LogWarning($"Interstitial unavailable: {error.Message}");
            }
            finally
            {
                _showInProgress = false;
            }
        }

        private bool HasRemoveAds()
        {
            SaveData save = _saveRepository.Load();
            return save.Entitlements != null &&
                   (save.Entitlements.Contains(ProductIds.RemoveAds) ||
                    save.Entitlements.Contains(ProductIds.StarterPack));
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
            _lastState = _stateField?.GetValue(_bootstrap)?.ToString() ?? string.Empty;
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(Ads, _yandex)) Ads = null;
            _yandex?.Dispose();
        }
    }
}
