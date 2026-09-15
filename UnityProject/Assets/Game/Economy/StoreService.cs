using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DontGetSidetracked.Core;
using DontGetSidetracked.Services;

namespace DontGetSidetracked.Economy
{
    public static class ProductIds
    {
        public const string RemoveAds = "remove_ads";
        public const string StarterPack = "starter_pack";
        public const string SkinNeon = "skin_neon";
        public const string SkinRetro = "skin_retro";
        public const string Hints10 = "hints_10";

        public static readonly string[] Mvp =
        {
            RemoveAds,
            StarterPack,
            SkinNeon,
            SkinRetro,
            Hints10
        };
    }

    public sealed class StorePurchaseResult
    {
        public PaymentPurchaseResult Payment { get; }
        public bool GrantApplied { get; }
        public StorePurchaseResult(PaymentPurchaseResult payment, bool grantApplied)
        {
            Payment = payment;
            GrantApplied = grantApplied;
        }
    }

    public sealed class StoreService
    {
        private readonly IPaymentService _payments;
        private readonly ISaveRepository _saveRepository;
        private readonly SaveData _save;

        public SaveData Save => _save;
        public bool InterstitialsRemoved => HasEntitlement(ProductIds.RemoveAds) || HasEntitlement(ProductIds.StarterPack);

        public StoreService(IPaymentService payments, ISaveRepository saveRepository, SaveData save)
        {
            _payments = payments ?? throw new ArgumentNullException(nameof(payments));
            _saveRepository = saveRepository ?? throw new ArgumentNullException(nameof(saveRepository));
            _save = SaveMigrator.Migrate(save ?? SaveData.CreateNew());
        }

        public Task<IReadOnlyList<StoreProduct>> LoadCatalogAsync() =>
            _payments.GetProductsAsync(ProductIds.Mvp);

        public async Task<StorePurchaseResult> PurchaseAsync(string productId)
        {
            if (!IsKnownProduct(productId))
                throw new ArgumentException("Unknown store product.", nameof(productId));

            PaymentPurchaseResult payment = await _payments.PurchaseAsync(productId);
            if (payment == null || !payment.IsSuccess)
                return new StorePurchaseResult(payment, false);

            bool granted = ApplyPurchase(payment);
            if (granted) _saveRepository.Save(_save);
            return new StorePurchaseResult(payment, granted);
        }

        public async Task<int> RestoreAsync()
        {
            IReadOnlyList<string> owned = await _payments.RestoreEntitlementsAsync();
            int changed = 0;
            if (owned != null)
            {
                for (int i = 0; i < owned.Count; i++)
                    if (ApplyNonConsumableEntitlement(owned[i])) changed++;
            }

            if (changed > 0) _saveRepository.Save(_save);
            return changed;
        }

        public bool HasEntitlement(string id) =>
            _save.Entitlements != null && _save.Entitlements.Contains(id);

        public bool OwnsSkin(string skinId) =>
            _save.Inventory != null && _save.Inventory.Contains(skinId);

        private bool ApplyPurchase(PaymentPurchaseResult purchase)
        {
            if (!string.IsNullOrWhiteSpace(purchase.PurchaseId))
            {
                if (_save.ProcessedPurchaseIds.Contains(purchase.PurchaseId)) return false;
                _save.ProcessedPurchaseIds.Add(purchase.PurchaseId);
            }

            switch (purchase.ProductId)
            {
                case ProductIds.RemoveAds:
                    AddUnique(_save.Entitlements, ProductIds.RemoveAds);
                    return true;

                case ProductIds.StarterPack:
                    AddUnique(_save.Entitlements, ProductIds.StarterPack);
                    AddUnique(_save.Entitlements, ProductIds.RemoveAds);
                    AddUnique(_save.Inventory, ProductIds.SkinNeon);
                    AddUnique(_save.Inventory, ProductIds.SkinRetro);
                    AddUnique(_save.Inventory, "skin_gold");
                    return true;

                case ProductIds.SkinNeon:
                    AddUnique(_save.Entitlements, ProductIds.SkinNeon);
                    AddUnique(_save.Inventory, ProductIds.SkinNeon);
                    return true;

                case ProductIds.SkinRetro:
                    AddUnique(_save.Entitlements, ProductIds.SkinRetro);
                    AddUnique(_save.Inventory, ProductIds.SkinRetro);
                    return true;

                case ProductIds.Hints10:
                    _save.Hints += 10;
                    return true;

                default:
                    return false;
            }
        }

        private bool ApplyNonConsumableEntitlement(string productId)
        {
            if (!IsKnownProduct(productId) || productId == ProductIds.Hints10) return false;
            bool changed = false;

            if (productId == ProductIds.RemoveAds)
            {
                changed |= AddUnique(_save.Entitlements, ProductIds.RemoveAds);
            }
            else if (productId == ProductIds.StarterPack)
            {
                changed |= AddUnique(_save.Entitlements, ProductIds.StarterPack);
                changed |= AddUnique(_save.Entitlements, ProductIds.RemoveAds);
                changed |= AddUnique(_save.Inventory, ProductIds.SkinNeon);
                changed |= AddUnique(_save.Inventory, ProductIds.SkinRetro);
                changed |= AddUnique(_save.Inventory, "skin_gold");
            }
            else if (productId == ProductIds.SkinNeon || productId == ProductIds.SkinRetro)
            {
                changed |= AddUnique(_save.Entitlements, productId);
                changed |= AddUnique(_save.Inventory, productId);
            }

            return changed;
        }

        private static bool IsKnownProduct(string productId)
        {
            if (string.IsNullOrWhiteSpace(productId)) return false;
            for (int i = 0; i < ProductIds.Mvp.Length; i++)
                if (string.Equals(ProductIds.Mvp[i], productId, StringComparison.Ordinal)) return true;
            return false;
        }

        private static bool AddUnique(List<string> target, string value)
        {
            if (target.Contains(value)) return false;
            target.Add(value);
            return true;
        }
    }
}
