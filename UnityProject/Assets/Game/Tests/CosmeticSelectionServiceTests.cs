using DontGetSidetracked.Core;
using DontGetSidetracked.Economy;
using NUnit.Framework;

namespace DontGetSidetracked.Tests
{
    public sealed class CosmeticSelectionServiceTests
    {
        [Test]
        public void DefaultSkin_IsAlwaysAvailable()
        {
            var save = SaveData.CreateNew();
            var service = new CosmeticSelectionService(new MemorySaveRepository(save), save);

            Assert.That(service.SelectedSkinId, Is.EqualTo(CosmeticIds.Default));
            Assert.That(service.GetAvailableSkins(), Is.EquivalentTo(new[] { CosmeticIds.Default }));
        }

        [Test]
        public void CannotSelectSkinThatIsNotOwned()
        {
            var save = SaveData.CreateNew();
            var repo = new MemorySaveRepository(save);
            var service = new CosmeticSelectionService(repo, save);

            Assert.That(service.Select(CosmeticIds.Neon), Is.False);
            Assert.That(service.SelectedSkinId, Is.EqualTo(CosmeticIds.Default));
            Assert.That(repo.SaveCount, Is.EqualTo(0));
        }

        [Test]
        public void OwnedSkin_CanBeSelectedAndPersists()
        {
            var save = SaveData.CreateNew();
            save.Inventory.Add(CosmeticIds.Neon);
            var repo = new MemorySaveRepository(save);
            var service = new CosmeticSelectionService(repo, save);

            Assert.That(service.Select(CosmeticIds.Neon), Is.True);
            Assert.That(service.SelectedSkinId, Is.EqualTo(CosmeticIds.Neon));
            Assert.That(repo.SaveCount, Is.EqualTo(1));
            Assert.That(repo.Load().SelectedSkinId, Is.EqualTo(CosmeticIds.Neon));
        }

        [Test]
        public void InvalidSavedSelection_FallsBackToDefault()
        {
            var save = SaveData.CreateNew();
            save.SelectedSkinId = CosmeticIds.Retro;
            var service = new CosmeticSelectionService(new MemorySaveRepository(save), save);

            Assert.That(service.SelectedSkinId, Is.EqualTo(CosmeticIds.Default));
        }

        [Test]
        public void AvailableSkins_ContainOnlyOwnedKnownCosmetics()
        {
            var save = SaveData.CreateNew();
            save.Inventory.Add(CosmeticIds.Neon);
            save.Inventory.Add(CosmeticIds.Gold);
            save.Inventory.Add("unknown_skin");
            var service = new CosmeticSelectionService(new MemorySaveRepository(save), save);

            Assert.That(service.GetAvailableSkins(), Is.EquivalentTo(new[]
            {
                CosmeticIds.Default,
                CosmeticIds.Neon,
                CosmeticIds.Gold
            }));
        }

        private sealed class MemorySaveRepository : ISaveRepository
        {
            private SaveData _data;
            public int SaveCount { get; private set; }

            public MemorySaveRepository(SaveData data) => _data = data;
            public SaveData Load() => _data;
            public void Save(SaveData data)
            {
                _data = data;
                SaveCount++;
            }
        }
    }
}
