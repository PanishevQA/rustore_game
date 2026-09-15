using System;
using System.Threading.Tasks;
using DontGetSidetracked.Services;

namespace DontGetSidetracked.Monetization
{
    public sealed class RewardedController
    {
        private readonly IAdService _ads;
        private readonly IRemoteConfigService _config;

        public RewardedController(IAdService ads, IRemoteConfigService config)
        {
            _ads = ads ?? throw new ArgumentNullException(nameof(ads));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public bool CanOffer => _config.GetBool("rewarded_enabled", true) && _ads.IsRewardedReady;

        public async Task<bool> TryShowAsync(RewardPlacement placement)
        {
            if (!CanOffer) return false;
            return await _ads.ShowRewardedAsync(placement);
        }
    }

    public sealed class InterstitialController
    {
        private readonly IAdService _ads;
        private readonly IRemoteConfigService _config;
        private readonly Func<DateTime> _utcNow;
        private int _completedRoundsSinceAd;
        private DateTime _lastShownUtc = DateTime.MinValue;

        public InterstitialController(IAdService ads, IRemoteConfigService config, Func<DateTime> utcNow = null)
        {
            _ads = ads ?? throw new ArgumentNullException(nameof(ads));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        public void NotifyRoundCompleted()
        {
            if (_completedRoundsSinceAd < int.MaxValue) _completedRoundsSinceAd++;
        }

        public bool CanShowBetweenSessions(bool removeAdsEntitlement, bool gameplayActive)
        {
            if (removeAdsEntitlement || gameplayActive) return false;
            if (!_config.GetBool("interstitial_enabled", true) || !_ads.IsInterstitialReady) return false;

            int minRounds = Math.Max(1, _config.GetInt("interstitial_min_rounds", 5));
            int cooldownSeconds = Math.Max(0, _config.GetInt("interstitial_cooldown_sec", 180));
            if (_completedRoundsSinceAd < minRounds) return false;
            if (_lastShownUtc != DateTime.MinValue && (_utcNow() - _lastShownUtc).TotalSeconds < cooldownSeconds) return false;
            return true;
        }

        public async Task<bool> TryShowBetweenSessionsAsync(bool removeAdsEntitlement, bool gameplayActive)
        {
            if (!CanShowBetweenSessions(removeAdsEntitlement, gameplayActive)) return false;
            await _ads.ShowInterstitialAsync();
            _lastShownUtc = _utcNow();
            _completedRoundsSinceAd = 0;
            return true;
        }
    }

    public sealed class NoopAdService : IAdService
    {
        public bool IsRewardedReady => false;
        public bool IsInterstitialReady => false;
        public Task<bool> ShowRewardedAsync(RewardPlacement placement) => Task.FromResult(false);
        public Task ShowInterstitialAsync() => Task.CompletedTask;
    }
}
