using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DontGetSidetracked.Core;
using DontGetSidetracked.Gameplay;

namespace DontGetSidetracked.Services
{
    public enum RewardPlacement { ExtraLook, RetryAttempt, DoubleCoins }

    public interface IAdService
    {
        bool IsRewardedReady { get; }
        bool IsInterstitialReady { get; }
        Task<bool> ShowRewardedAsync(RewardPlacement placement);
        Task ShowInterstitialAsync();
    }

    public sealed class StoreProduct
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string PriceLabel { get; set; }
    }

    public interface IPaymentService
    {
        Task<IReadOnlyList<StoreProduct>> GetProductsAsync(IReadOnlyList<string> productIds);
        Task<bool> PurchaseAsync(string productId);
        Task RestoreEntitlementsAsync();
    }

    public interface IAnalyticsService
    {
        void Track(string eventName, IReadOnlyDictionary<string, object> parameters = null);
    }

    public interface IRemoteConfigService
    {
        double GetDouble(string key, double fallback);
        int GetInt(string key, int fallback);
        bool GetBool(string key, bool fallback);
        string GetString(string key, string fallback);
    }

    public interface IReviewService { Task RequestReviewAsync(); }
    public interface IUpdateService { Task CheckForUpdateAsync(bool mandatory); }
    public interface IReferrerService { Task<string> ConsumeInstallReferrerAsync(); }
    public interface IShareService { void ShareText(string text); }

    public sealed class DailyDto
    {
        public string ChallengeId;
        public long Seed;
        public int GeneratorVersion;
        public string ServerTimeUtc;
    }

    public interface IGameApi
    {
        Task<DailyDto> GetDailyAsync();
        Task<double> SubmitDailyAttemptAsync(string playerId, DailyChallengeDefinition challenge, IReadOnlyList<IReadOnlyList<RecordedPoint>> replays, IReadOnlyList<double> clientScores, bool assisted);
        Task<string> CreateChallengeAsync(string playerId, string challengeId, double score);
    }

    public sealed class SafeRemoteConfig : IRemoteConfigService
    {
        public double GetDouble(string key, double fallback) => fallback;
        public int GetInt(string key, int fallback) => fallback;
        public bool GetBool(string key, bool fallback) => fallback;
        public string GetString(string key, string fallback) => fallback;
    }

    public sealed class NoopAnalytics : IAnalyticsService
    {
        public void Track(string eventName, IReadOnlyDictionary<string, object> parameters = null) { }
    }
}
