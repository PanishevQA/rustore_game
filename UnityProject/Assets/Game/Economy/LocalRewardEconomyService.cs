using System;
using DontGetSidetracked.Core;

namespace DontGetSidetracked.Economy
{
    /// <summary>
    /// Small offline campaign economy. It never replaces RuStore products/prices:
    /// earned campaign coins can only be exchanged for gameplay hints.
    /// </summary>
    public sealed class LocalRewardEconomyService
    {
        public const int HintPriceCoins = 30;

        private readonly ISaveRepository _repository;
        private readonly SaveData _save;

        public LocalRewardEconomyService(ISaveRepository repository, SaveData save)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _save = SaveMigrator.Migrate(save ?? SaveData.CreateNew());
        }

        public SaveData Save => _save;
        public bool CanBuyHint => _save.Coins >= HintPriceCoins;

        public bool TryBuyHint()
        {
            if (!CanBuyHint) return false;
            _save.Coins -= HintPriceCoins;
            _save.Hints += 1;
            _repository.Save(_save);
            return true;
        }
    }
}
