using DontGetSidetracked.Core;
using DontGetSidetracked.Gameplay;
using NUnit.Framework;

namespace DontGetSidetracked.Tests
{
    public sealed class CampaignLevelsTests
    {
        [Test]
        public void CatalogIsDeterministicAndContainsSixtyLevels()
        {
            Assert.That(CampaignLevelCatalog.TotalLevels, Is.EqualTo(60));
            var generator = new RouteGenerator();

            for (int level = 1; level <= CampaignLevelCatalog.TotalLevels; level++)
            {
                CampaignLevelDefinition a = CampaignLevelCatalog.Get(level);
                CampaignLevelDefinition b = CampaignLevelCatalog.Get(level);
                Assert.That(a.Seed, Is.EqualTo(b.Seed));
                Assert.That(a.DisplayTimeMs, Is.EqualTo(b.DisplayTimeMs));
                Assert.That(a.Difficulty, Is.EqualTo(b.Difficulty));

                RouteDefinition routeA = a.BuildRoute(generator);
                RouteDefinition routeB = b.BuildRoute(generator);
                Assert.That(routeA.DisplayTimeMs, Is.EqualTo(a.DisplayTimeMs));
                Assert.That(routeA.ReferencePoints.Count, Is.EqualTo(routeB.ReferencePoints.Count));
                for (int i = 0; i < routeA.ReferencePoints.Count; i++)
                    Assert.That(routeA.ReferencePoints[i], Is.EqualTo(routeB.ReferencePoints[i]));
            }
        }

        [TestCase(59.9, 0)]
        [TestCase(60.0, 1)]
        [TestCase(79.9, 1)]
        [TestCase(80.0, 2)]
        [TestCase(94.9, 2)]
        [TestCase(95.0, 3)]
        [TestCase(100.0, 3)]
        public void StarThresholdsAreStable(double score, int expected)
        {
            Assert.That(CampaignStarPolicy.StarsFor(score), Is.EqualTo(expected));
        }

        [Test]
        public void CompletingLevelUnlocksOnlyTheNextLevel()
        {
            var save = SaveData.CreateNew();
            var repository = new MemoryRepository(save);
            var service = new CampaignProgressService(repository, save);

            Assert.That(service.IsUnlocked(1), Is.True);
            Assert.That(service.IsUnlocked(2), Is.False);

            CampaignLevelCompletion failed = service.RecordResult(1, 59.9);
            Assert.That(failed.Stars, Is.EqualTo(0));
            Assert.That(service.IsUnlocked(2), Is.False);

            CampaignLevelCompletion passed = service.RecordResult(1, 82.4);
            Assert.That(passed.Stars, Is.EqualTo(2));
            Assert.That(passed.NextLevelUnlocked, Is.True);
            Assert.That(service.IsUnlocked(2), Is.True);
            Assert.That(service.IsUnlocked(3), Is.False);
            Assert.That(repository.SaveCount, Is.EqualTo(2));
        }

        [Test]
        public void ReplayCannotReduceBestScoreOrStars()
        {
            var save = SaveData.CreateNew();
            var service = new CampaignProgressService(new MemoryRepository(save), save);

            service.RecordResult(1, 97.2);
            CampaignLevelCompletion replay = service.RecordResult(1, 63.0);

            Assert.That(replay.BestScore, Is.EqualTo(97.2));
            Assert.That(replay.Stars, Is.EqualTo(3));
            Assert.That(replay.NewBest, Is.False);
            Assert.That(replay.NewStars, Is.False);
            Assert.That(service.TotalStars(), Is.EqualTo(3));
        }

        [Test]
        public void LockedLevelCannotBeRecorded()
        {
            var save = SaveData.CreateNew();
            var service = new CampaignProgressService(new MemoryRepository(save), save);
            Assert.Throws<System.InvalidOperationException>(() => service.RecordResult(2, 100.0));
        }

        private sealed class MemoryRepository : ISaveRepository
        {
            private SaveData _save;
            public int SaveCount { get; private set; }

            public MemoryRepository(SaveData save)
            {
                _save = save;
            }

            public SaveData Load() => _save;

            public void Save(SaveData data)
            {
                _save = data;
                SaveCount++;
            }
        }
    }
}
