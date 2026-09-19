using System.Collections.Generic;
using System.Threading.Tasks;
using DontGetSidetracked.Core;
using DontGetSidetracked.Economy;
using DontGetSidetracked.Services;
using NUnit.Framework;

namespace DontGetSidetracked.Tests
{
    public sealed class StoreServiceTests
    {
        [Test]
        public async Task HintsPurchase_WithBackendVerification_IsGrantedOncePerPurchaseId()
        {
            var save = SaveData.CreateNew();
            var repo = new MemorySaveRepository(save);
            var payments = new FakePayments
            {
                PurchaseResult = new PaymentPurchaseResult(
                    PurchaseOutcome.Completed,
                    ProductIds.Hints10,
                    "purchase_1",
                    "100001")
            };
            var verification = new FakeVerification();
            var store = new StoreService(payments, repo, save, verification, save.AnonymousPlayerId);

            StorePurchaseResult first = await store.PurchaseAsync(ProductIds.Hints10);
            StorePurchaseResult second = await store.PurchaseAsync(ProductIds.Hints10);

            Assert.That(first.Verified, Is.True);
            Assert.That(first.GrantApplied, Is.True);
            Assert.That(second.Verified, Is.True);
            Assert.That(second.GrantApplied, Is.False);
            Assert.That(save.Hints, Is.EqualTo(10));
            Assert.That(verification.CallCount, Is.EqualTo(2));
        }

        [Test]
        public async Task OfflineConsumable_SuccessfulRuStoreResult_IsGrantedOnce()
        {
            var save = SaveData.CreateNew();
            var payments = new FakePayments
            {
                PurchaseResult = new PaymentPurchaseResult(
                    PurchaseOutcome.Completed,
                    ProductIds.Hints10,
                    "purchase_local_1",
                    "200001")
            };
            var store = new StoreService(payments, new MemorySaveRepository(save), save);

            StorePurchaseResult first = await store.PurchaseAsync(ProductIds.Hints10);
            StorePurchaseResult second = await store.PurchaseAsync(ProductIds.Hints10);

            Assert.That(first.Verified, Is.True);
            Assert.That(first.Verification.Status, Is.EqualTo("RUSTORE_PURCHASE_RESULT"));
            Assert.That(first.GrantApplied, Is.True);
            Assert.That(second.GrantApplied, Is.False);
            Assert.That(save.Hints, Is.EqualTo(10));
        }

        [Test]
        public async Task OfflineNonConsumable_WhenRuStoreConfirmsOwnership_GrantsEntitlement()
        {
            var save = SaveData.CreateNew();
            var payments = new FakePayments
            {
                PurchaseResult = new PaymentPurchaseResult(
                    PurchaseOutcome.Completed,
                    ProductIds.RemoveAds,
                    "purchase_2",
                    "200002"),
                Restored = new[] { ProductIds.RemoveAds }
            };
            var store = new StoreService(payments, new MemorySaveRepository(save), save);

            StorePurchaseResult result = await store.PurchaseAsync(ProductIds.RemoveAds);

            Assert.That(result.Payment.IsSuccess, Is.True);
            Assert.That(result.Verified, Is.True);
            Assert.That(result.Verification.Status, Is.EqualTo("RUSTORE_CONFIRMED_OWNERSHIP"));
            Assert.That(result.GrantApplied, Is.True);
            Assert.That(store.InterstitialsRemoved, Is.True);
        }

        [Test]
        public async Task OfflineNonConsumable_WhenRuStoreDoesNotConfirmOwnership_DoesNotGrant()
        {
            var save = SaveData.CreateNew();
            var payments = new FakePayments
            {
                PurchaseResult = new PaymentPurchaseResult(
                    PurchaseOutcome.Completed,
                    ProductIds.RemoveAds,
                    "purchase_3",
                    "200003"),
                Restored = new string[0]
            };
            var store = new StoreService(payments, new MemorySaveRepository(save), save);

            StorePurchaseResult result = await store.PurchaseAsync(ProductIds.RemoveAds);

            Assert.That(result.Payment.IsSuccess, Is.True);
            Assert.That(result.Verified, Is.False);
            Assert.That(result.GrantApplied, Is.False);
            Assert.That(store.InterstitialsRemoved, Is.False);
        }

        [Test]
        public async Task CancelledPurchase_DoesNotGrantEntitlement()
        {
            var save = SaveData.CreateNew();
            var store = new StoreService(
                new FakePayments
                {
                    PurchaseResult = new PaymentPurchaseResult(PurchaseOutcome.Cancelled, ProductIds.RemoveAds)
                },
                new MemorySaveRepository(save),
                save,
                new FakeVerification(),
                save.AnonymousPlayerId);

            StorePurchaseResult result = await store.PurchaseAsync(ProductIds.RemoveAds);

            Assert.That(result.GrantApplied, Is.False);
            Assert.That(store.InterstitialsRemoved, Is.False);
        }

        [Test]
        public async Task RestoreStarterPack_GrantsNoAdsAndThreeSkins()
        {
            var save = SaveData.CreateNew();
            var payments = new FakePayments
            {
                Restored = new[] { ProductIds.StarterPack }
            };
            var store = new StoreService(payments, new MemorySaveRepository(save), save);

            int changed = await store.RestoreAsync();

            Assert.That(changed, Is.EqualTo(1));
            Assert.That(store.InterstitialsRemoved, Is.True);
            Assert.That(save.Inventory, Does.Contain(ProductIds.SkinNeon));
            Assert.That(save.Inventory, Does.Contain(ProductIds.SkinRetro));
            Assert.That(save.Inventory, Does.Contain("skin_gold"));
        }

        [Test]
        public async Task RestoreNonConsumables_IsIdempotentEvenWithDuplicateOwnershipRows()
        {
            var save = SaveData.CreateNew();
            var payments = new FakePayments
            {
                Restored = new[]
                {
                    ProductIds.StarterPack,
                    ProductIds.StarterPack,
                    ProductIds.RemoveAds,
                    ProductIds.SkinNeon,
                    ProductIds.SkinRetro,
                    ProductIds.Hints10
                }
            };
            var store = new StoreService(payments, new MemorySaveRepository(save), save);

            int firstChanged = await store.RestoreAsync();
            int secondChanged = await store.RestoreAsync();

            Assert.That(firstChanged, Is.GreaterThan(0));
            Assert.That(secondChanged, Is.EqualTo(0));
            Assert.That(save.Hints, Is.EqualTo(0), "Consumables must never be restored as durable ownership.");
            Assert.That(save.Entitlements.FindAll(x => x == ProductIds.StarterPack).Count, Is.EqualTo(1));
            Assert.That(save.Entitlements.FindAll(x => x == ProductIds.RemoveAds).Count, Is.EqualTo(1));
            Assert.That(save.Inventory.FindAll(x => x == ProductIds.SkinNeon).Count, Is.EqualTo(1));
            Assert.That(save.Inventory.FindAll(x => x == ProductIds.SkinRetro).Count, Is.EqualTo(1));
            Assert.That(save.Inventory.FindAll(x => x == "skin_gold").Count, Is.EqualTo(1));
        }

        private sealed class MemorySaveRepository : ISaveRepository
        {
            private SaveData _save;
            public MemorySaveRepository(SaveData save) => _save = save;
            public SaveData Load() => _save;
            public void Save(SaveData data) => _save = data;
        }

        private sealed class FakePayments : IPaymentService
        {
            public PaymentPurchaseResult PurchaseResult =
                new PaymentPurchaseResult(PurchaseOutcome.Failed, "none");
            public IReadOnlyList<string> Restored = new string[0];

            public Task<IReadOnlyList<StoreProduct>> GetProductsAsync(IReadOnlyList<string> productIds) =>
                Task.FromResult<IReadOnlyList<StoreProduct>>(new StoreProduct[0]);

            public Task<PaymentPurchaseResult> PurchaseAsync(string productId) =>
                Task.FromResult(PurchaseResult);

            public Task<IReadOnlyList<string>> RestoreEntitlementsAsync() =>
                Task.FromResult(Restored);
        }

        private sealed class FakeVerification : IPurchaseVerificationApi
        {
            public int CallCount;

            public Task<PurchaseVerificationResult> VerifyPurchaseAsync(
                string playerId,
                string productId,
                string invoiceId,
                string purchaseId)
            {
                CallCount++;
                return Task.FromResult(new PurchaseVerificationResult(
                    true,
                    productId,
                    purchaseId,
                    invoiceId,
                    "CONFIRMED"));
            }
        }
    }
}
