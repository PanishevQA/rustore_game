using DontGetSidetracked.Core;
using NUnit.Framework;

namespace DontGetSidetracked.Tests
{
    public sealed class GameSettingsServiceTests
    {
        [Test]
        public void NewSave_DefaultsSoundAndHapticsOn()
        {
            var save = SaveData.CreateNew();
            var service = new GameSettingsService(new MemorySaveRepository(save), save);

            Assert.That(service.SoundEnabled, Is.True);
            Assert.That(service.HapticsEnabled, Is.True);
        }

        [Test]
        public void SetSound_PersistsOnlyWhenValueChanges()
        {
            var save = SaveData.CreateNew();
            var repo = new MemorySaveRepository(save);
            var service = new GameSettingsService(repo, save);

            Assert.That(service.SetSound(false), Is.True);
            Assert.That(service.SoundEnabled, Is.False);
            Assert.That(repo.SaveCount, Is.EqualTo(1));
            Assert.That(service.SetSound(false), Is.False);
            Assert.That(repo.SaveCount, Is.EqualTo(1));
        }

        [Test]
        public void SetHaptics_PersistsOnlyWhenValueChanges()
        {
            var save = SaveData.CreateNew();
            var repo = new MemorySaveRepository(save);
            var service = new GameSettingsService(repo, save);

            Assert.That(service.SetHaptics(false), Is.True);
            Assert.That(service.HapticsEnabled, Is.False);
            Assert.That(repo.SaveCount, Is.EqualTo(1));
            Assert.That(service.SetHaptics(false), Is.False);
            Assert.That(repo.SaveCount, Is.EqualTo(1));
        }

        [Test]
        public void ToggleMethods_RoundTripPreferences()
        {
            var save = SaveData.CreateNew();
            var repo = new MemorySaveRepository(save);
            var service = new GameSettingsService(repo, save);

            Assert.That(service.ToggleSound(), Is.True);
            Assert.That(service.SoundEnabled, Is.False);
            Assert.That(service.ToggleSound(), Is.True);
            Assert.That(service.SoundEnabled, Is.True);

            Assert.That(service.ToggleHaptics(), Is.True);
            Assert.That(service.HapticsEnabled, Is.False);
            Assert.That(service.ToggleHaptics(), Is.True);
            Assert.That(service.HapticsEnabled, Is.True);
            Assert.That(repo.SaveCount, Is.EqualTo(4));
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
