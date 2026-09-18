using System;
using System.Collections.Generic;
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
    /// Interstitials are considered only after a completed round and only on an unobstructed idle Home screen.
    /// </summary>
    public sealed class AdRuntimeCoordinator : MonoBehaviour
    {
        public static IAdService Ads { get; private set; }

        private JsonFileSaveRepository _saveRepository;
        private IRemoteConfigService _config;
        private YandexMobileAdsService _yandex;
        private InterstitialController _interstitial;
        private GameBootstrap _bootstrap;
        private bool _wasResult;
        private bool _showInProgress;
        private float _nextPoll;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            // Unity Editor can enter Play Mode without a domain reload. Never expose a provider
            // instance that belongs to the previous runtime session.
            Ads = null;
        }

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
            if (_bootstrap == null) return;

            bool isResult = GameBootstrapRuntimeBridge.IsResult(_bootstrap);
            if (isResult && !_wasResult)
                _interstitial.NotifyRoundCompleted();
            _wasResult = isResult;

            if (_showInProgress || !IsSafeHome() || IsAnyHomeOverlayOpen()) return;

            SaveData save = _saveRepository.Load();
            bool removeAds = save.Entitlements != null &&
                             (save.Entitlements.Contains(ProductIds.RemoveAds) ||
                              save.Entitlements.Contains(ProductIds.StarterPack));
            if (!_interstitial.CanShowBetweenSessions(removeAds, gameplayActive: false)) return;

            ShowInterstitialSafeAsync();
        }

        private async void ShowInterstitialSafeAsync()
        {
            if (_showInProgress || IsAnyHomeOverlayOpen()) return;
            _showInProgress = true;
            try
            {
                bool shown = await _interstitial.TryShowBetweenSessionsAsync(
                    removeAdsEntitlement: HasRemoveAds(),
                    gameplayActive: !IsSafeHome() || IsAnyHomeOverlayOpen());
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

        private bool IsSafeHome() =>
            _bootstrap != null && GameBootstrapRuntimeBridge.IsPlainHome(_bootstrap);

        private static bool IsAnyHomeOverlayOpen()
        {
            MetaMenuOverlay meta = FindFirstObjectByType<MetaMenuOverlay>();
            if (meta != null && meta.IsPanelOpen) return true;

            TrainingMenuCoordinator training = FindFirstObjectByType<TrainingMenuCoordinator>();
            if (training != null && training.IsOpen) return true;

            CampaignLevelMenuOverlay campaign = FindFirstObjectByType<CampaignLevelMenuOverlay>();
            if (campaign != null && campaign.IsOpen) return true;

            ReferralOfferCoordinator referral = FindFirstObjectByType<ReferralOfferCoordinator>();
            if (referral != null && referral.IsVisible) return true;

            RuntimePlatformCoordinator platform = FindFirstObjectByType<RuntimePlatformCoordinator>();
            if (platform != null && platform.IsNotificationPromptOpen) return true;

            // MandatoryUpdateCanvas exists only while the blocking update UI is active.
            return GameObject.Find("MandatoryUpdateCanvas") != null;
        }

        private void ResolveBootstrap()
        {
            _bootstrap = FindFirstObjectByType<GameBootstrap>();
            _wasResult = _bootstrap != null && GameBootstrapRuntimeBridge.IsResult(_bootstrap);
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(Ads, _yandex)) Ads = null;
            _yandex?.Dispose();
        }
    }
}
