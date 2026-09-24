using System;

namespace DontGetSidetracked.Core
{
    /// <summary>
    /// Pure C# settings facade over versioned SaveData. Presentation can toggle preferences
    /// without depending on PlayerPrefs or platform APIs.
    /// </summary>
    public sealed class GameSettingsService
    {
        private readonly ISaveRepository _repository;
        private readonly SaveData _save;

        public bool SoundEnabled => _save.Settings.Sound;
        public bool HapticsEnabled => _save.Settings.Haptics;
        public SaveData Save => _save;

        public GameSettingsService(ISaveRepository repository, SaveData save)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _save = SaveMigrator.Migrate(save ?? SaveData.CreateNew());
        }

        public bool SetSound(bool enabled)
        {
            if (_save.Settings.Sound == enabled) return false;
            _save.Settings.Sound = enabled;
            _repository.Save(_save);
            return true;
        }

        public bool SetHaptics(bool enabled)
        {
            if (_save.Settings.Haptics == enabled) return false;
            _save.Settings.Haptics = enabled;
            _repository.Save(_save);
            return true;
        }

        public bool ToggleSound() => SetSound(!SoundEnabled);
        public bool ToggleHaptics() => SetHaptics(!HapticsEnabled);
    }
}
