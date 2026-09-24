using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DontGetSidetracked.Services;

namespace DontGetSidetracked.Platform.RuStore
{
    // Production SDK calls live only in this assembly. Credentials and generated
    // PayClientSettings assets stay out of git. These fallbacks keep gameplay usable
    // when a platform service is unavailable or has not been configured yet.
    public sealed class UnconfiguredRuStorePaymentService : IPaymentService
    {
        public Task<IReadOnlyList<StoreProduct>> GetProductsAsync(IReadOnlyList<string> productIds) =>
            Task.FromResult<IReadOnlyList<StoreProduct>>(Array.Empty<StoreProduct>());

        public Task<PaymentPurchaseResult> PurchaseAsync(string productId) =>
            Task.FromResult(new PaymentPurchaseResult(PurchaseOutcome.Failed, productId, errorMessage: "RuStore Pay is not configured."));

        public Task<IReadOnlyList<string>> RestoreEntitlementsAsync() =>
            Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }

    public sealed class UnconfiguredRuStoreReferrerService : IReferrerService
    {
        public Task<InstallReferrerResult> ConsumeInstallReferrerAsync() =>
            Task.FromResult(new InstallReferrerResult(false, null));
    }
}
