using DontGetSidetracked.Core;
using DontGetSidetracked.Daily;
using DontGetSidetracked.Gameplay;
using NUnit.Framework;

namespace DontGetSidetracked.Tests
{
    public sealed class ResultEnhancementPolicyTests
    {
        [TestCase(79.9, ScoreMedalTier.None, false)]
        [TestCase(80.0, ScoreMedalTier.Bronze, false)]
        [TestCase(90.0, ScoreMedalTier.Silver, false)]
        [TestCase(95.0, ScoreMedalTier.Gold, false)]
        [TestCase(98.0, ScoreMedalTier.Gold, true)]
        [TestCase(99.0, ScoreMedalTier.Perfect, true)]
        [TestCase(100.0, ScoreMedalTier.Perfect, true)]
        public void ScoreCelebration_UsesStableThresholds(double score, ScoreMedalTier tier, bool perfectEffect)
        {
            ScoreCelebration result = ScoreCelebrationPolicy.Evaluate(score);
            Assert.That(result.Tier, Is.EqualTo(tier));
            Assert.That(result.PlayPerfectEffect, Is.EqualTo(perfectEffect));
            Assert.That(result.Label, Is.Not.Empty);
        }

        [Test]
        public void DailyBest_OnlyImprovesRecord()
        {
            var save = SaveData.CreateNew();
            var service = new DailyBestService();

            DailyBestUpdate first = service.Apply(save, "daily_2026_09_16", 91.24);
            DailyBestUpdate lower = service.Apply(save, "daily_2026_09_16", 88.0);
            DailyBestUpdate better = service.Apply(save, "daily_2026_09_16", 96.76);

            Assert.That(first.IsNewRecord, Is.True);
            Assert.That(first.BestScore, Is.EqualTo(91.2));
            Assert.That(lower.IsNewRecord, Is.False);
            Assert.That(lower.BestScore, Is.EqualTo(91.2));
            Assert.That(better.IsNewRecord, Is.True);
            Assert.That(better.PreviousBest, Is.EqualTo(91.2));
            Assert.That(better.BestScore, Is.EqualTo(96.8));
            Assert.That(service.GetBest(save, "daily_2026_09_16"), Is.EqualTo(96.8));
        }

        [Test]
        public void DailyBest_KeepsBoundedHistory()
        {
            var save = SaveData.CreateNew();
            var service = new DailyBestService();

            for (int i = 0; i < DailyBestService.MaxStoredRecords + 5; i++)
                service.Apply(save, "daily_" + i, 80 + i % 20);

            Assert.That(save.DailyBests.Count, Is.EqualTo(DailyBestService.MaxStoredRecords));
            Assert.That(service.GetBest(save, "daily_0"), Is.EqualTo(0.0));
            Assert.That(service.GetBest(save, "daily_64"), Is.GreaterThan(0.0));
        }

        [Test]
        public void Version11Migration_InitializesDailyBestHistory()
        {
            var save = new SaveData { Version = 11, DailyBests = null };
            SaveData migrated = SaveMigrator.Migrate(save);

            Assert.That(migrated.Version, Is.EqualTo(SaveData.CurrentVersion));
            Assert.That(migrated.DailyBests, Is.Not.Null);
            Assert.That(migrated.DailyBests, Is.Empty);
        }
    }
}
