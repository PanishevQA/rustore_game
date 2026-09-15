using System;
using DontGetSidetracked.Core;
using DontGetSidetracked.Daily;
using NUnit.Framework;

namespace DontGetSidetracked.Tests
{
    public sealed class SaveAndStreakTests
    {
        [Test]
        public void MigrationCreatesRequiredCollectionsAndId()
        {
            var data = new SaveData { Version = 1, AnonymousPlayerId = string.Empty, Entitlements = null, LastDaily = null };
            SaveData migrated = SaveMigrator.Migrate(data);
            Assert.That(migrated.Version, Is.EqualTo(SaveData.CurrentVersion));
            Assert.That(migrated.AnonymousPlayerId, Does.StartWith("anon_"));
            Assert.That(migrated.Entitlements, Is.Not.Null);
            Assert.That(migrated.LastDaily, Is.Not.Null);
            Assert.That(migrated.LastDaily.RouteCount, Is.EqualTo(3));
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
