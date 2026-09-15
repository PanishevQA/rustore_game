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
        }

        [Test]
        public void StreakUsesServerDateAndDoesNotDoubleCountDay()
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
