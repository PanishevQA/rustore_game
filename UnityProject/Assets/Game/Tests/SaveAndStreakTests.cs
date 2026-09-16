using System;
using DontGetSidetracked.Core;
using DontGetSidetracked.Daily;
using NUnit.Framework;

namespace DontGetSidetracked.Tests
{
    public sealed class SaveAndStreakTests
    {
        [Test]
        public void MigrationCreatesRequiredCollectionsIdDailyProfileAndCosmeticSelection()
        {
            var data = new SaveData { Version = 1, AnonymousPlayerId = string.Empty, Entitlements = null, LastDaily = null };
            SaveData migrated = SaveMigrator.Migrate(data);
            Assert.That(migrated.Version, Is.EqualTo(SaveData.CurrentVersion));
            Assert.That(migrated.AnonymousPlayerId, Does.StartWith("anon_"));
            Assert.That(migrated.Entitlements, Is.Not.Null);
            Assert.That(migrated.LastDaily, Is.Not.Null);
            Assert.That(migrated.LastDaily.RouteCount, Is.EqualTo(3));
            Assert.That(migrated.LastDaily.DisplayTimeEasyMs, Is.EqualTo(DailyCacheData.DefaultEasyDisplayTimeMs));
            Assert.That(migrated.LastDaily.DisplayTimeMediumMs, Is.EqualTo(DailyCacheData.DefaultMediumDisplayTimeMs));
            Assert.That(migrated.LastDaily.DisplayTimeHardMs, Is.EqualTo(DailyCacheData.DefaultHardDisplayTimeMs));
            Assert.That(migrated.SelectedSkinId, Is.EqualTo("default"));
            Assert.That(migrated.LevelProgress, Is.Not.Null);
            Assert.That(migrated.HighestUnlockedLevel, Is.EqualTo(1));
        }

        [Test]
        public void Version7MigrationClearsObsoletePendingBackendAttemptsAndReachesCurrentVersion()
        {
            var data = new SaveData { Version = 7 };
            data.PendingAttempts.Add(new PendingDailyAttemptData { ChallengeId = "legacy" });

            SaveData migrated = SaveMigrator.Migrate(data);

            Assert.That(migrated.Version, Is.EqualTo(SaveData.CurrentVersion));
            Assert.That(migrated.PendingAttempts, Is.Empty);
            Assert.That(migrated.LastDaily.RouteCount, Is.EqualTo(3));
            Assert.That(migrated.SelectedSkinId, Is.EqualTo("default"));
            Assert.That(migrated.HighestUnlockedLevel, Is.EqualTo(1));
        }

        [Test]
        public void Version8MigrationNormalizesInvalidCachedRouteCount()
        {
            var data = new SaveData
            {
                Version = 8,
                LastDaily = new DailyCacheData { RouteCount = 0 }
            };

            SaveData migrated = SaveMigrator.Migrate(data);

            Assert.That(migrated.Version, Is.EqualTo(SaveData.CurrentVersion));
            Assert.That(migrated.LastDaily.RouteCount, Is.EqualTo(3));
        }

        [Test]
        public void Version9MigrationNormalizesInvalidCachedDisplayTimes()
        {
            var data = new SaveData
            {
                Version = 9,
                LastDaily = new DailyCacheData
                {
                    RouteCount = 2,
                    DisplayTimeEasyMs = 0,
                    DisplayTimeMediumMs = 50000,
                    DisplayTimeHardMs = 900
                }
            };

            SaveData migrated = SaveMigrator.Migrate(data);

            Assert.That(migrated.Version, Is.EqualTo(SaveData.CurrentVersion));
            Assert.That(migrated.LastDaily.RouteCount, Is.EqualTo(2));
            Assert.That(migrated.LastDaily.DisplayTimeEasyMs, Is.EqualTo(DailyCacheData.DefaultEasyDisplayTimeMs));
            Assert.That(migrated.LastDaily.DisplayTimeMediumMs, Is.EqualTo(DailyCacheData.DefaultMediumDisplayTimeMs));
            Assert.That(migrated.LastDaily.DisplayTimeHardMs, Is.EqualTo(900));
        }

        [Test]
        public void Version10MigrationAddsDefaultCosmeticSelection()
        {
            var data = new SaveData
            {
                Version = 10,
                SelectedSkinId = string.Empty
            };

            SaveData migrated = SaveMigrator.Migrate(data);

            Assert.That(migrated.Version, Is.EqualTo(SaveData.CurrentVersion));
            Assert.That(migrated.SelectedSkinId, Is.EqualTo("default"));
        }

        [Test]
        public void Version12MigrationCreatesCampaignProgress()
        {
            var data = new SaveData
            {
                Version = 12,
                HighestUnlockedLevel = 0,
                LevelProgress = null
            };

            SaveData migrated = SaveMigrator.Migrate(data);

            Assert.That(migrated.Version, Is.EqualTo(SaveData.CurrentVersion));
            Assert.That(migrated.HighestUnlockedLevel, Is.EqualTo(1));
            Assert.That(migrated.LevelProgress, Is.Not.Null.And.Empty);
        }

        [Test]
        public void CurrentVersionRepairNormalizesCorruptEconomyProgressAndLegacyQueue()
        {
            var data = new SaveData
            {
                Version = SaveData.CurrentVersion,
                Coins = -10,
                Hints = -2,
                Streak = -4,
                SessionNumber = -1,
                CompletedDailyCount = -8,
                PersonalBest = 250.0,
                Inventory = new System.Collections.Generic.List<string> { "skin_neon", "skin_neon", " ", null },
                Entitlements = new System.Collections.Generic.List<string> { "remove_ads", "remove_ads" },
                ProcessedPurchaseIds = new System.Collections.Generic.List<string> { "p1", "p1", string.Empty },
                DailyBests = new System.Collections.Generic.List<DailyBestData>
                {
                    new DailyBestData { ChallengeId = "daily_a", BestScore = 70.0 },
                    new DailyBestData { ChallengeId = "daily_a", BestScore = 150.0 },
                    new DailyBestData { ChallengeId = " ", BestScore = 99.0 },
                    null
                },
                LevelProgress = new System.Collections.Generic.List<LevelProgressData>
                {
                    new LevelProgressData { LevelNumber = 3, BestScore = 75.0, Stars = 1 },
                    new LevelProgressData { LevelNumber = 3, BestScore = 120.0, Stars = 9 },
                    new LevelProgressData { LevelNumber = 61, BestScore = 90.0, Stars = 3 },
                    null
                },
                HighestUnlockedLevel = 1,
                LastDaily = new DailyCacheData
                {
                    ChallengeId = null,
                    ServerTimeUtc = null,
                    GeneratorVersion = 0,
                    RouteCount = 99,
                    DisplayTimeEasyMs = 0,
                    DisplayTimeMediumMs = 99999,
                    DisplayTimeHardMs = 1000
                },
                PendingAttempts = new System.Collections.Generic.List<PendingDailyAttemptData>
                {
                    new PendingDailyAttemptData { ChallengeId = "stale" }
                }
            };

            SaveData repaired = SaveMigrator.Migrate(data);

            Assert.That(repaired.Coins, Is.Zero);
            Assert.That(repaired.Hints, Is.Zero);
            Assert.That(repaired.Streak, Is.Zero);
            Assert.That(repaired.SessionNumber, Is.Zero);
            Assert.That(repaired.CompletedDailyCount, Is.Zero);
            Assert.That(repaired.PersonalBest, Is.EqualTo(100.0));
            Assert.That(repaired.Inventory, Is.EqualTo(new[] { "skin_neon" }));
            Assert.That(repaired.Entitlements, Is.EqualTo(new[] { "remove_ads" }));
            Assert.That(repaired.ProcessedPurchaseIds, Is.EqualTo(new[] { "p1" }));
            Assert.That(repaired.DailyBests, Has.Count.EqualTo(1));
            Assert.That(repaired.DailyBests[0].ChallengeId, Is.EqualTo("daily_a"));
            Assert.That(repaired.DailyBests[0].BestScore, Is.EqualTo(100.0));
            Assert.That(repaired.LevelProgress, Has.Count.EqualTo(1));
            Assert.That(repaired.LevelProgress[0].LevelNumber, Is.EqualTo(3));
            Assert.That(repaired.LevelProgress[0].BestScore, Is.EqualTo(100.0));
            Assert.That(repaired.LevelProgress[0].Stars, Is.EqualTo(3));
            Assert.That(repaired.HighestUnlockedLevel, Is.EqualTo(4));
            Assert.That(repaired.LastDaily.ChallengeId, Is.Empty);
            Assert.That(repaired.LastDaily.ServerTimeUtc, Is.Empty);
            Assert.That(repaired.LastDaily.GeneratorVersion, Is.EqualTo(1));
            Assert.That(repaired.LastDaily.RouteCount, Is.EqualTo(3));
            Assert.That(repaired.LastDaily.DisplayTimeEasyMs, Is.EqualTo(DailyCacheData.DefaultEasyDisplayTimeMs));
            Assert.That(repaired.LastDaily.DisplayTimeMediumMs, Is.EqualTo(DailyCacheData.DefaultMediumDisplayTimeMs));
            Assert.That(repaired.LastDaily.DisplayTimeHardMs, Is.EqualTo(1000));
            Assert.That(repaired.PendingAttempts, Is.Empty);
        }

        [Test]
        public void StreakUsesUtcChallengeDateAndDoesNotDoubleCountDay()
        {
            var save = SaveData.CreateNew();
            var streak = new StreakService();
            Assert.That(streak.ApplyCompletedDaily(save, new DateTime(2026, 9, 15, 23, 0, 0, DateTimeKind.Utc)), Is.True);
            Assert.That(streak.ApplyCompletedDaily(save, new DateTime(2026, 9, 15, 1, 0, 0, DateTimeKind.Utc)), Is.False);
            Assert.That(streak.ApplyCompletedDaily(save, new DateTime(2026, 9, 16, 1, 0, 0, DateTimeKind.Utc)), Is.True);
            Assert.That(save.Streak, Is.EqualTo(2));
        }
    }
}
