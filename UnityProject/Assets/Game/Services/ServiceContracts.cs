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

    public sealed class InstallReferrerResult
    {
        public bool RequestSucceeded { get; }
        public string ReferrerId { get; }

        public InstallReferrerResult(bool requestSucceeded, string referrerId)
        {
            RequestSucceeded = requestSucceeded;
            ReferrerId = referrerId;
        }
    }

    public interface IReferrerService
    {
        Task<InstallReferrerResult> ConsumeInstallReferrerAsync();
    }

    public interface IShareService { void ShareText(string text); }

    [Serializable]
    public sealed class DailyDto
    {
        public string challengeId;
        public long seed;
        public int generatorVersion;
        public string serverTimeUtc;

        public string ChallengeId => challengeId;
        public long Seed => seed;
        public int GeneratorVersion => generatorVersion;
        public string ServerTimeUtc => serverTimeUtc;
    }

    [Serializable]
    public sealed class ReferralDto
    {
        public string referralId;
        public string challengeId;
        public string inviterId;
        public double inviterScore;
        public long seed;
        public int generatorVersion;
        public string serverTimeUtc;

        public string ReferralId => referralId;
        public string ChallengeId => challengeId;
        public string InviterId => inviterId;
        public double InviterScore => inviterScore;
        public long Seed => seed;
        public int GeneratorVersion => generatorVersion;
        public string ServerTimeUtc => serverTimeUtc;
    }

    public interface IGameApi
    {
        Task<DailyDto> GetDailyAsync();
        Task<double> SubmitDailyAttemptAsync(string playerId, DailyChallengeDefinition challenge, IReadOnlyList<IReadOnlyList<RecordedPoint>> replays, IReadOnlyList<double> clientScores, bool assisted);
        Task<string> CreateChallengeAsync(string playerId, string challengeId, double score);
        Task<ReferralDto> GetReferralAsync(string referralId);
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
