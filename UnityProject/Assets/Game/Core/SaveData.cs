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
        public string ChallengeId = string.Empty;
        public long Seed;
        public int GeneratorVersion = 1;
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
        public const int CurrentVersion = 4;

        public int Version = CurrentVersion;
        public string AnonymousPlayerId = string.Empty;
        public int Coins;
        public List<string> Inventory = new List<string>();
        public List<string> Entitlements = new List<string>();
        public GameSettingsData Settings = new GameSettingsData();
        public int Streak;
        public double PersonalBest;
        public bool TutorialCompleted;
        public string LastCompletedDailyDateUtc = string.Empty;
        public DailyCacheData LastDaily = new DailyCacheData();
        public string PendingReferralId = string.Empty;
        public int SessionNumber;
        public int CompletedDailyCount;
        public List<PendingDailyAttemptData> PendingAttempts = new List<PendingDailyAttemptData>();
        public int LastReviewRequestSession;
        public int ReviewRequestCount;

        public static SaveData CreateNew()
        {
            return new SaveData
            {
                Version = CurrentVersion,
                AnonymousPlayerId = "anon_" + Guid.NewGuid().ToString("N")
            };
        }
    }

    public static class SaveMigrator
    {
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

            if (string.IsNullOrWhiteSpace(data.AnonymousPlayerId))
                data.AnonymousPlayerId = "anon_" + Guid.NewGuid().ToString("N");
            if (data.Settings == null) data.Settings = new GameSettingsData();
            if (data.Inventory == null) data.Inventory = new List<string>();
            if (data.Entitlements == null) data.Entitlements = new List<string>();
            if (data.LastDaily == null) data.LastDaily = new DailyCacheData();
            if (data.PendingAttempts == null) data.PendingAttempts = new List<PendingDailyAttemptData>();

            data.Version = SaveData.CurrentVersion;
            return data;
        }
    }

    public interface ISaveRepository
    {
        SaveData Load();
        void Save(SaveData data);
    }
}
