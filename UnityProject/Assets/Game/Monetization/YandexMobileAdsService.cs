using System;
using System.Threading.Tasks;
using DontGetSidetracked.Services;

#if YANDEX_MOBILE_ADS
using YandexMobileAds;
using YandexMobileAds.Base;
#endif

namespace DontGetSidetracked.Monetization
{
    /// <summary>
    /// Optional Yandex Mobile Ads Unity 8 adapter.
    /// Import the official Yandex Mobile Ads plugin and define YANDEX_MOBILE_ADS to enable it.
    /// Without the plugin the same type safely behaves as an unavailable ad provider.
    /// </summary>
    public sealed class YandexMobileAdsService : IAdService, IDisposable
    {
        private readonly string _rewardedUnitId;
        private readonly string _interstitialUnitId;

#if YANDEX_MOBILE_ADS
        private readonly RewardedAdLoader _rewardedLoader = new RewardedAdLoader();
        private readonly InterstitialAdLoader _interstitialLoader = new InterstitialAdLoader();
        private RewardedAd _rewardedAd;
        private Interstitial _interstitial;
        private bool _rewardedLoading;
        private bool _interstitialLoading;
#endif

        public YandexMobileAdsService(string rewardedUnitId, string interstitialUnitId)
        {
            _rewardedUnitId = rewardedUnitId ?? string.Empty;
            _interstitialUnitId = interstitialUnitId ?? string.Empty;
#if YANDEX_MOBILE_ADS
            _ = PreloadRewardedAsync();
            _ = PreloadInterstitialAsync();
#endif
        }

        public bool IsRewardedReady
        {
            get
            {
#if YANDEX_MOBILE_ADS
                return _rewardedAd != null;
#else
                return false;
#endif
            }
        }

        public bool IsInterstitialReady
        {
            get
            {
#if YANDEX_MOBILE_ADS
                return _interstitial != null;
#else
                return false;
#endif
            }
        }

        public async Task<bool> ShowRewardedAsync(RewardPlacement placement)
        {
#if YANDEX_MOBILE_ADS
            if (_rewardedAd == null && !await PreloadRewardedAsync()) return false;
            if (_rewardedAd == null) return false;

            RewardedAd ad = _rewardedAd;
            _rewardedAd = null;
            var completion = new TaskCompletionSource<bool>();
            bool rewarded = false;

            void OnRewarded(object sender, Reward reward)
            {
                rewarded = true;
            }

            void OnDismissed(object sender, EventArgs args)
            {
                completion.TrySetResult(rewarded);
            }

            void OnFailed(object sender, AdFailureEventArgs args)
            {
                completion.TrySetResult(false);
            }

            ad.OnRewarded += OnRewarded;
            ad.OnAdDismissed += OnDismissed;
            ad.OnAdFailedToShow += OnFailed;

            try
            {
                ad.Show();
                return await completion.Task;
            }
            finally
            {
                ad.OnRewarded -= OnRewarded;
                ad.OnAdDismissed -= OnDismissed;
                ad.OnAdFailedToShow -= OnFailed;
                ad.Destroy();
                _ = PreloadRewardedAsync();
            }
#else
            await Task.CompletedTask;
            return false;
#endif
        }

        public async Task<bool> ShowInterstitialAsync()
        {
#if YANDEX_MOBILE_ADS
            if (_interstitial == null && !await PreloadInterstitialAsync()) return false;
            if (_interstitial == null) return false;

            Interstitial ad = _interstitial;
            _interstitial = null;
            var completion = new TaskCompletionSource<bool>();

            void OnDismissed(object sender, EventArgs args) => completion.TrySetResult(true);
            void OnFailed(object sender, AdFailureEventArgs args) => completion.TrySetResult(false);

            ad.OnAdDismissed += OnDismissed;
            ad.OnAdFailedToShow += OnFailed;
            try
            {
                ad.Show();
                return await completion.Task;
            }
            finally
            {
                ad.OnAdDismissed -= OnDismissed;
                ad.OnAdFailedToShow -= OnFailed;
                ad.Destroy();
                _ = PreloadInterstitialAsync();
            }
#else
            await Task.CompletedTask;
            return false;
#endif
        }

#if YANDEX_MOBILE_ADS
        private async Task<bool> PreloadRewardedAsync()
        {
            if (_rewardedAd != null) return true;
            if (_rewardedLoading || string.IsNullOrWhiteSpace(_rewardedUnitId)) return false;
            _rewardedLoading = true;
            try
            {
                _rewardedAd = await _rewardedLoader.LoadAd(new AdRequest(_rewardedUnitId));
                return _rewardedAd != null;
            }
            catch (AdLoadingException)
            {
                return false;
            }
            finally
            {
                _rewardedLoading = false;
            }
        }

        private async Task<bool> PreloadInterstitialAsync()
        {
            if (_interstitial != null) return true;
            if (_interstitialLoading || string.IsNullOrWhiteSpace(_interstitialUnitId)) return false;
            _interstitialLoading = true;
            try
            {
                _interstitial = await _interstitialLoader.LoadAd(new AdRequest(_interstitialUnitId));
                return _interstitial != null;
            }
            catch (AdLoadingException)
            {
                return false;
            }
            finally
            {
                _interstitialLoading = false;
            }
        }
#endif

        public void Dispose()
        {
#if YANDEX_MOBILE_ADS
            if (_rewardedAd != null)
            {
                _rewardedAd.Destroy();
                _rewardedAd = null;
            }
            if (_interstitial != null)
            {
                _interstitial.Destroy();
                _interstitial = null;
            }
#endif
        }
    }

    public static class YandexMobileAdsSettings
    {
        // Fill these with production block IDs from the Yandex Advertising Network console.
        // Never ship demo-* IDs in a production build.
        public const string RewardedUnitId = "";
        public const string InterstitialUnitId = "";

        public static bool IsConfigured =>
            !string.IsNullOrWhiteSpace(RewardedUnitId) &&
            !string.IsNullOrWhiteSpace(InterstitialUnitId) &&
            !RewardedUnitId.StartsWith("demo-", StringComparison.OrdinalIgnoreCase) &&
            !InterstitialUnitId.StartsWith("demo-", StringComparison.OrdinalIgnoreCase);
    }
}
