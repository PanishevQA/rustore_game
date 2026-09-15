using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DontGetSidetracked.Services;

namespace DontGetSidetracked.Platform.RuStore
{
    // Production SDK calls live only in this assembly. The foundation intentionally keeps
    // credentials and generated PayClientSettings assets out of git. These adapters fail
    // closed until the corresponding SDK client is wired/configured in the Unity Editor.
    public sealed class UnconfiguredRuStorePaymentService : IPaymentService
    {
        public Task<IReadOnlyList<StoreProduct>> GetProductsAsync(IReadOnlyList<string> productIds) =>
            Task.FromResult<IReadOnlyList<StoreProduct>>(Array.Empty<StoreProduct>());

        public Task<bool> PurchaseAsync(string productId) => Task.FromResult(false);
        public Task RestoreEntitlementsAsync() => Task.CompletedTask;
    }

    public sealed class UnconfiguredRuStoreReferrerService : IReferrerService
    {
        public Task<string> ConsumeInstallReferrerAsync() => Task.FromResult<string>(null);
    }
}
