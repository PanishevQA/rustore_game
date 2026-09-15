using System;
using System.Collections.Generic;

namespace DontGetSidetracked.Core
{
    [Serializable]
    public sealed class GameSettingsData
    {
        public bool Sound = true;
        public bool Haptics = true;
        public string Language = "ru";
    }

    [Serializable]
    public sealed class DailyCacheData
    {
        public const int DefaultEasyDisplayTimeMs = 3500;
        public const int DefaultMediumDisplayTimeMs = 3000;
        public const int DefaultHardDisplayTimeMs = 2500;

        public string ChallengeId = string.Empty;
        public long Seed;
        public int GeneratorVersion = 1;
        public int RouteCount = 3;
        public int DisplayTimeEasyMs = DefaultEasyDisplayTimeMs;
        public int DisplayTimeMediumMs = DefaultMediumDisplayTimeMs;
        public int DisplayTimeHardMs = DefaultHardDisplayTimeMs;
        public string ServerTimeUtc = string.Empty;
    }

    [Serializable]
    public sealed class ReplayPointData
    {
        public int X;
        public int Y;
        public long TimestampMs;
    }

    [Serializable]
    public sealed class PendingRouteAttemptData
    {
        public int RouteIndex;
        public double ClientScore;
        public List<ReplayPointData> Points = new List<ReplayPointData>();
    }

    [Serializable]
    public sealed class PendingDailyAttemptData
    {
        public string ChallengeId = string.Empty;
        public long Seed;
        public int GeneratorVersion = 1;
        public bool Assisted;
        public List<PendingRouteAttemptData> Routes = new List<PendingRouteAttemptData>();
    }

    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentVersion = 11;

        public int Version = CurrentVersion;
        public string AnonymousPlayerId = string.Empty;
        public int Coins;
        public int Hints;
        public List<string> Inventory = new List<string>();
        public List<string> Entitlements = new List<string>();
        public List<string> ProcessedPurchaseIds = new List<string>();
        public string SelectedSkinId = "default";
        public GameSettingsData Settings = new GameSettingsData();
        public int Streak;
        public double PersonalBest;
        public bool TutorialCompleted;
        public string LastCompletedDailyDateUtc = string.Empty;
        public DailyCacheData LastDaily = new DailyCacheData();
        public string PendingReferralId = string.Empty;
        public bool InstallReferrerConsumed;
        public int SessionNumber;
        public int CompletedDailyCount;

        // Kept only for backward-compatible deserialization of pre-offline saves.
        // Version 8 migration clears this obsolete developer-backend sync queue.
        public List<PendingDailyAttemptData> PendingAttempts = new List<PendingDailyAttemptData>();

        public int LastReviewRequestSession;
        public int ReviewRequestCount;
        public bool NotificationValuePromptShown;
        public bool NotificationPermissionGranted;

        public static SaveData CreateNew()
        {
            return new SaveData
            {
                Version = CurrentVersion,
                AnonymousPlayerId = "anon_" + Guid.NewGuid().ToString("N"),
                SelectedSkinId = "default"
            };
        }
    }

    public static class SaveMigrator
    {
        private const int MinDisplayTimeMs = 750;
        private const int MaxDisplayTimeMs = 10000;

        public static SaveData Migrate(SaveData data)
        {
            if (data == null) return SaveData.CreateNew();

            if (data.Version <= 0)
            {
                if (data.Settings == null) data.Settings = new GameSettingsData();
                if (data.Inventory == null) data.Inventory = new List<string>();
                data.Version = 1;
            }

            if (data.Version == 1)
            {
                if (data.Entitlements == null) data.Entitlements = new List<string>();
                if (data.LastDaily == null) data.LastDaily = new DailyCacheData();
                data.Version = 2;
            }

            if (data.Version == 2)
            {
                if (data.PendingAttempts == null) data.PendingAttempts = new List<PendingDailyAttemptData>();
                data.Version = 3;
            }

            if (data.Version == 3)
            {
                data.LastReviewRequestSession = 0;
                data.ReviewRequestCount = 0;
                data.Version = 4;
            }

            if (data.Version == 4)
            {
                data.InstallReferrerConsumed = false;
                data.Version = 5;
            }

            if (data.Version == 5)
            {
                data.Hints = 0;
                if (data.ProcessedPurchaseIds == null) data.ProcessedPurchaseIds = new List<string>();
                data.Version = 6;
            }

            if (data.Version == 6)
            {
                data.NotificationValuePromptShown = false;
                data.NotificationPermissionGranted = false;
                data.Version = 7;
            }

            if (data.Version == 7)
            {
                data.PendingAttempts = new List<PendingDailyAttemptData>();
                data.Version = 8;
            }

            if (data.Version == 8)
            {
                if (data.LastDaily == null) data.LastDaily = new DailyCacheData();
                if (data.LastDaily.RouteCount < 1 || data.LastDaily.RouteCount > 3)
                    data.LastDaily.RouteCount = 3;
                data.Version = 9;
            }

            if (data.Version == 9)
            {
                if (data.LastDaily == null) data.LastDaily = new DailyCacheData();
                NormalizeDailyCache(data.LastDaily);
                data.Version = 10;
            }

            if (data.Version == 10)
            {
                if (string.IsNullOrWhiteSpace(data.SelectedSkinId)) data.SelectedSkinId = "default";
                data.Version = 11;
            }

            if (string.IsNullOrWhiteSpace(data.AnonymousPlayerId))
                data.AnonymousPlayerId = "anon_" + Guid.NewGuid().ToString("N");
            if (data.Settings == null) data.Settings = new GameSettingsData();
            if (data.Inventory == null) data.Inventory = new List<string>();
            if (data.Entitlements == null) data.Entitlements = new List<string>();
            if (data.ProcessedPurchaseIds == null) data.ProcessedPurchaseIds = new List<string>();
            if (string.IsNullOrWhiteSpace(data.SelectedSkinId)) data.SelectedSkinId = "default";
            if (data.LastDaily == null) data.LastDaily = new DailyCacheData();
            NormalizeDailyCache(data.LastDaily);
            if (data.PendingAttempts == null) data.PendingAttempts = new List<PendingDailyAttemptData>();

            data.Version = SaveData.CurrentVersion;
            return data;
        }

        private static void NormalizeDailyCache(DailyCacheData cache)
        {
            if (cache.RouteCount < 1 || cache.RouteCount > 3) cache.RouteCount = 3;
            cache.DisplayTimeEasyMs = NormalizeDisplayTime(cache.DisplayTimeEasyMs, DailyCacheData.DefaultEasyDisplayTimeMs);
            cache.DisplayTimeMediumMs = NormalizeDisplayTime(cache.DisplayTimeMediumMs, DailyCacheData.DefaultMediumDisplayTimeMs);
            cache.DisplayTimeHardMs = NormalizeDisplayTime(cache.DisplayTimeHardMs, DailyCacheData.DefaultHardDisplayTimeMs);
        }

        private static int NormalizeDisplayTime(int value, int fallback) =>
            value >= MinDisplayTimeMs && value <= MaxDisplayTimeMs ? value : fallback;
    }

    public interface ISaveRepository
    {
        SaveData Load();
        void Save(SaveData data);
    }
}
