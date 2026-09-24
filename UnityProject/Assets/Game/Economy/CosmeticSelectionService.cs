using System;
using System.Collections.Generic;
using DontGetSidetracked.Core;

namespace DontGetSidetracked.Economy
{
    public static class CosmeticIds
    {
        public const string Default = "default";
        public const string Neon = ProductIds.SkinNeon;
        public const string Retro = ProductIds.SkinRetro;
        public const string Gold = "skin_gold";
    }

    /// <summary>
    /// Pure local cosmetic selection. Ownership comes from SaveData inventory restored/granted by StoreService.
    /// </summary>
    public sealed class CosmeticSelectionService
    {
        private readonly ISaveRepository _repository;
        private readonly SaveData _save;

        public SaveData Save => _save;

        public CosmeticSelectionService(ISaveRepository repository, SaveData save)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _save = SaveMigrator.Migrate(save ?? SaveData.CreateNew());
            NormalizeSelection();
        }

        public string SelectedSkinId
        {
            get
            {
                NormalizeSelection();
                return _save.SelectedSkinId;
            }
        }

        public IReadOnlyList<string> GetAvailableSkins()
        {
            var result = new List<string> { CosmeticIds.Default };
            AddIfOwned(result, CosmeticIds.Neon);
            AddIfOwned(result, CosmeticIds.Retro);
            AddIfOwned(result, CosmeticIds.Gold);
            return result;
        }

        public bool Select(string skinId)
        {
            if (!IsSelectable(skinId)) return false;
            if (string.Equals(_save.SelectedSkinId, skinId, StringComparison.Ordinal)) return false;
            _save.SelectedSkinId = skinId;
            _repository.Save(_save);
            return true;
        }

        public bool IsSelectable(string skinId)
        {
            if (string.Equals(skinId, CosmeticIds.Default, StringComparison.Ordinal)) return true;
            if (!IsKnownSkin(skinId) || _save.Inventory == null) return false;
            return _save.Inventory.Contains(skinId);
        }

        private void NormalizeSelection()
        {
            if (!IsSelectable(_save.SelectedSkinId)) _save.SelectedSkinId = CosmeticIds.Default;
        }

        private void AddIfOwned(List<string> target, string skinId)
        {
            if (IsSelectable(skinId)) target.Add(skinId);
        }

        private static bool IsKnownSkin(string skinId) =>
            string.Equals(skinId, CosmeticIds.Neon, StringComparison.Ordinal) ||
            string.Equals(skinId, CosmeticIds.Retro, StringComparison.Ordinal) ||
            string.Equals(skinId, CosmeticIds.Gold, StringComparison.Ordinal);
    }
}
