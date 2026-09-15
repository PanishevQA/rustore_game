using System;
using System.Threading.Tasks;
using DontGetSidetracked.Monetization;
using DontGetSidetracked.Services;
using NUnit.Framework;

namespace DontGetSidetracked.Tests
{
    public sealed class AdControllersTests
    {
        [Test]
        public async Task Interstitial_ShowsOnlyBetweenSessionsAfterCapAndCooldown()
        {
            var ads = new FakeAds { IsInterstitialReadyValue = true };
            var config = new FakeConfig();
            DateTime now = new DateTime(2026, 9, 15, 10, 0, 0, DateTimeKind.Utc);
            var controller = new InterstitialController(ads, config, () => now);

            for (int i = 0; i < 4; i++) controller.NotifyRoundCompleted();
            Assert.That(controller.CanShowBetweenSessions(false, false), Is.False);

            controller.NotifyRoundCompleted();
            Assert.That(controller.CanShowBetweenSessions(false, true), Is.False, "Never show during gameplay.");
            Assert.That(await controller.TryShowBetweenSessionsAsync(false, false), Is.True);
            Assert.That(ads.InterstitialShows, Is.EqualTo(1));

            for (int i = 0; i < 5; i++) controller.NotifyRoundCompleted();
            Assert.That(controller.CanShowBetweenSessions(false, false), Is.False, "Cooldown must still apply.");
            now = now.AddSeconds(181);
            Assert.That(controller.CanShowBetweenSessions(false, false), Is.True);
            Assert.That(controller.CanShowBetweenSessions(true, false), Is.False, "remove_ads must suppress interstitials.");
        }

        [Test]
        public async Task Rewarded_RequiresConfigAndReadyProvider()
        {
            var ads = new FakeAds { IsRewardedReadyValue = true };
            var config = new FakeConfig();
            var controller = new RewardedController(ads, config);

            Assert.That(controller.CanOffer, Is.True);
            Assert.That(await controller.TryShowAsync(RewardPlacement.ExtraLook), Is.True);
            Assert.That(ads.RewardedShows, Is.EqualTo(1));
        }

        private sealed class FakeAds : IAdService
        {
            public bool IsRewardedReadyValue;
            public bool IsInterstitialReadyValue;
            public int RewardedShows;
            public int InterstitialShows;
            public bool IsRewardedReady => IsRewardedReadyValue;
            public bool IsInterstitialReady => IsInterstitialReadyValue;
            public Task<bool> ShowRewardedAsync(RewardPlacement placement)
            {
                RewardedShows++;
                return Task.FromResult(true);
            }
            public Task ShowInterstitialAsync()
            {
                InterstitialShows++;
                return Task.CompletedTask;
            }
        }

        private sealed class FakeConfig : IRemoteConfigService
        {
            public double GetDouble(string key, double fallback) => fallback;
            public int GetInt(string key, int fallback) => key == "interstitial_min_rounds" ? 5 : key == "interstitial_cooldown_sec" ? 180 : fallback;
            public bool GetBool(string key, bool fallback) => true;
            public string GetString(string key, string fallback) => fallback;
        }
    }
}
