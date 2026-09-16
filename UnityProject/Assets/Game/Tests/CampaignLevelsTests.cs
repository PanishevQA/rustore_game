using DontGetSidetracked.Core;
using DontGetSidetracked.Economy;
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
        public void ReplayCannotReduceBestScoreStarsOrFarmRewards()
        {
            var save = SaveData.CreateNew();
            var service = new CampaignProgressService(new MemoryRepository(save), save);

            CampaignLevelCompletion first = service.RecordResult(1, 97.2);
            CampaignLevelCompletion replay = service.RecordResult(1, 63.0);

            Assert.That(first.CoinsAwarded, Is.EqualTo(15));
            Assert.That(save.Coins, Is.EqualTo(15));
            Assert.That(replay.BestScore, Is.EqualTo(97.2));
            Assert.That(replay.Stars, Is.EqualTo(3));
            Assert.That(replay.NewBest, Is.False);
            Assert.That(replay.NewStars, Is.False);
            Assert.That(replay.CoinsAwarded, Is.EqualTo(0));
            Assert.That(service.TotalStars(), Is.EqualTo(3));
            Assert.That(service.CompletedLevels(), Is.EqualTo(1));
        }

        [Test]
        public void StarUpgradeAwardsOnlyTheDifference()
        {
            var save = SaveData.CreateNew();
            var service = new CampaignProgressService(new MemoryRepository(save), save);

            CampaignLevelCompletion oneStar = service.RecordResult(1, 65.0);
            CampaignLevelCompletion threeStars = service.RecordResult(1, 96.0);

            Assert.That(oneStar.CoinsAwarded, Is.EqualTo(5));
            Assert.That(threeStars.CoinsAwarded, Is.EqualTo(10));
            Assert.That(save.Coins, Is.EqualTo(15));
        }

        [Test]
        public void FirstChapterCompletionAwardsHintOnlyOnce()
        {
            var save = SaveData.CreateNew { HighestUnlockedLevel = 10 };
            var service = new CampaignProgressService(new MemoryRepository(save), save);

            CampaignLevelCompletion first = service.RecordResult(10, 70.0);
            CampaignLevelCompletion replay = service.RecordResult(10, 99.0);

            Assert.That(first.HintsAwarded, Is.EqualTo(1));
            Assert.That(replay.HintsAwarded, Is.EqualTo(0));
            Assert.That(save.Hints, Is.EqualTo(1));
        }

        [Test]
        public void CoinHintExchangeIsLocalAndAtomic()
        {
            var save = SaveData.CreateNew();
            save.Coins = LocalRewardEconomyService.HintPriceCoins;
            var repository = new MemoryRepository(save);
            var economy = new LocalRewardEconomyService(repository, save);

            Assert.That(economy.TryBuyHint(), Is.True);
            Assert.That(save.Coins, Is.EqualTo(0));
            Assert.That(save.Hints, Is.EqualTo(1));
            Assert.That(economy.TryBuyHint(), Is.False);
            Assert.That(save.Hints, Is.EqualTo(1));
            Assert.That(repository.SaveCount, Is.EqualTo(1));
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
