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
    public sealed class DailyBestData
    {
        public string ChallengeId = string.Empty;
        public double BestScore;
    }

    [Serializable]
    public sealed class LevelProgressData
    {
        public int LevelNumber;
        public double BestScore;
        public int Stars;
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
        public const int CurrentVersion = 14;

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
        public List<DailyBestData> DailyBests = new List<DailyBestData>();
        public int HighestUnlockedLevel = 1;
        public List<LevelProgressData> LevelProgress = new List<LevelProgressData>();
        public string PendingReferralId = string.Empty;
        public bool InstallReferrerConsumed;
        public int SessionNumber;
        public int CompletedDailyCount;
        public int TotalScoredAttempts;
        public double TotalScoreSum;

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
                SelectedSkinId = "default",
                HighestUnlockedLevel = 1
            };
        }
    }

    public static class SaveMigrator
    {
        private const int MinDisplayTimeMs = 750;
        private const int MaxDisplayTimeMs = 10000;
        private const int MaxCampaignLevel = 60;
        private const int MaxDailyBests = 60;

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

            if (data.Version == 11)
            {
                if (data.DailyBests == null) data.DailyBests = new List<DailyBestData>();
                data.Version = 12;
            }

            if (data.Version == 12)
            {
                if (data.LevelProgress == null) data.LevelProgress = new List<LevelProgressData>();
                data.HighestUnlockedLevel = 1;
                data.Version = 13;
            }

            if (data.Version == 13)
            {
                data.TotalScoredAttempts = 0;
                data.TotalScoreSum = 0.0;
                data.Version = 14;
            }

            NormalizeCurrentData(data);
            data.Version = SaveData.CurrentVersion;
            return data;
        }

        private static void NormalizeCurrentData(SaveData data)
        {
            if (string.IsNullOrWhiteSpace(data.AnonymousPlayerId))
                data.AnonymousPlayerId = "anon_" + Guid.NewGuid().ToString("N");

            if (data.Settings == null) data.Settings = new GameSettingsData();
            if (string.IsNullOrWhiteSpace(data.Settings.Language)) data.Settings.Language = "ru";

            data.Inventory = NormalizeStringList(data.Inventory);
            data.Entitlements = NormalizeStringList(data.Entitlements);
            data.ProcessedPurchaseIds = NormalizeStringList(data.ProcessedPurchaseIds);

            if (string.IsNullOrWhiteSpace(data.SelectedSkinId)) data.SelectedSkinId = "default";
            if (data.LastCompletedDailyDateUtc == null) data.LastCompletedDailyDateUtc = string.Empty;
            if (data.PendingReferralId == null) data.PendingReferralId = string.Empty;

            data.Coins = Math.Max(0, data.Coins);
            data.Hints = Math.Max(0, data.Hints);
            data.Streak = Math.Max(0, data.Streak);
            data.SessionNumber = Math.Max(0, data.SessionNumber);
            data.CompletedDailyCount = Math.Max(0, data.CompletedDailyCount);
            data.TotalScoredAttempts = Math.Max(0, data.TotalScoredAttempts);
            if (double.IsNaN(data.TotalScoreSum) || double.IsInfinity(data.TotalScoreSum) || data.TotalScoreSum < 0.0)
                data.TotalScoreSum = 0.0;
            if (data.TotalScoredAttempts == 0)
                data.TotalScoreSum = 0.0;
            else
                data.TotalScoreSum = Math.Min(data.TotalScoreSum, data.TotalScoredAttempts * 100.0);
            data.LastReviewRequestSession = Math.Max(0, data.LastReviewRequestSession);
            data.ReviewRequestCount = Math.Max(0, data.ReviewRequestCount);
            data.PersonalBest = ClampScore(data.PersonalBest);

            if (data.LastDaily == null) data.LastDaily = new DailyCacheData();
            NormalizeDailyCache(data.LastDaily);

            data.DailyBests = NormalizeDailyBests(data.DailyBests);
            data.LevelProgress = NormalizeLevelProgress(data.LevelProgress);

            data.HighestUnlockedLevel = Math.Max(1, Math.Min(MaxCampaignLevel, data.HighestUnlockedLevel));
            for (int i = 0; i < data.LevelProgress.Count; i++)
            {
                LevelProgressData progress = data.LevelProgress[i];
                if (progress.Stars >= 1 && progress.LevelNumber < MaxCampaignLevel)
                    data.HighestUnlockedLevel = Math.Max(data.HighestUnlockedLevel, progress.LevelNumber + 1);
            }

            // The offline-first runtime never submits a background backend queue.
            // Clear stale/corrupt legacy entries even if a current-version file somehow contains them.
            if (data.PendingAttempts == null) data.PendingAttempts = new List<PendingDailyAttemptData>();
            else if (data.PendingAttempts.Count > 0) data.PendingAttempts.Clear();
        }

        private static void NormalizeDailyCache(DailyCacheData cache)
        {
            if (cache.ChallengeId == null) cache.ChallengeId = string.Empty;
            if (cache.ServerTimeUtc == null) cache.ServerTimeUtc = string.Empty;
            if (cache.GeneratorVersion < 1) cache.GeneratorVersion = 1;
            if (cache.RouteCount < 1 || cache.RouteCount > 3) cache.RouteCount = 3;
            cache.DisplayTimeEasyMs = NormalizeDisplayTime(cache.DisplayTimeEasyMs, DailyCacheData.DefaultEasyDisplayTimeMs);
            cache.DisplayTimeMediumMs = NormalizeDisplayTime(cache.DisplayTimeMediumMs, DailyCacheData.DefaultMediumDisplayTimeMs);
            cache.DisplayTimeHardMs = NormalizeDisplayTime(cache.DisplayTimeHardMs, DailyCacheData.DefaultHardDisplayTimeMs);
        }

        private static List<string> NormalizeStringList(List<string> source)
        {
            var result = new List<string>();
            if (source == null) return result;

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < source.Count; i++)
            {
                string value = source[i];
                if (string.IsNullOrWhiteSpace(value)) continue;
                value = value.Trim();
                if (seen.Add(value)) result.Add(value);
            }
            return result;
        }

        private static List<DailyBestData> NormalizeDailyBests(List<DailyBestData> source)
        {
            var result = new List<DailyBestData>();
            var byId = new Dictionary<string, DailyBestData>(StringComparer.Ordinal);
            if (source != null)
            {
                for (int i = 0; i < source.Count; i++)
                {
                    DailyBestData item = source[i];
                    if (item == null || string.IsNullOrWhiteSpace(item.ChallengeId)) continue;
                    string id = item.ChallengeId.Trim();
                    double score = ClampScore(item.BestScore);
                    if (byId.TryGetValue(id, out DailyBestData existing))
                    {
                        if (score > existing.BestScore) existing.BestScore = score;
                        continue;
                    }

                    var normalized = new DailyBestData { ChallengeId = id, BestScore = score };
                    byId.Add(id, normalized);
                    result.Add(normalized);
                }
            }

            if (result.Count > MaxDailyBests)
                result.RemoveRange(0, result.Count - MaxDailyBests);
            return result;
        }

        private static List<LevelProgressData> NormalizeLevelProgress(List<LevelProgressData> source)
        {
            var result = new List<LevelProgressData>();
            var byLevel = new Dictionary<int, LevelProgressData>();
            if (source == null) return result;

            for (int i = 0; i < source.Count; i++)
            {
                LevelProgressData item = source[i];
                if (item == null || item.LevelNumber < 1 || item.LevelNumber > MaxCampaignLevel) continue;

                double score = ClampScore(item.BestScore);
                int stars = Math.Max(0, Math.Min(3, item.Stars));
                if (byLevel.TryGetValue(item.LevelNumber, out LevelProgressData existing))
                {
                    existing.BestScore = Math.Max(existing.BestScore, score);
                    existing.Stars = Math.Max(existing.Stars, stars);
                    continue;
                }

                var normalized = new LevelProgressData
                {
                    LevelNumber = item.LevelNumber,
                    BestScore = score,
                    Stars = stars
                };
                byLevel.Add(item.LevelNumber, normalized);
                result.Add(normalized);
            }

            result.Sort((left, right) => left.LevelNumber.CompareTo(right.LevelNumber));
            return result;
        }

        private static double ClampScore(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return 0.0;
            return Math.Max(0.0, Math.Min(100.0, value));
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
