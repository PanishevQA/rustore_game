using DontGetSidetracked.Analytics;
using DontGetSidetracked.Core;
using DontGetSidetracked.Platform.RuStore;
using DontGetSidetracked.Services;
using UnityEngine;

namespace DontGetSidetracked.Presentation
{
    /// <summary>
    /// Captures RuStore's one-shot install referrer as early as possible and persists it
    /// before any network request. A failed SDK request is retried on a later launch;
    /// a successful null/invalid result is considered consumed, matching one-shot semantics.
    /// </summary>
    public sealed class InstallReferrerCapture : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (FindFirstObjectByType<InstallReferrerCapture>() != null) return;
            var root = new GameObject("InstallReferrerCapture");
            DontDestroyOnLoad(root);
            root.AddComponent<InstallReferrerCapture>();
        }

        private async void Start()
        {
            var repository = new JsonFileSaveRepository();
            SaveData save = repository.Load();
            if (save.InstallReferrerConsumed) return;

            IReferrerService service = new RuStoreInstallReferrerService();
            InstallReferrerResult result = await service.ConsumeInstallReferrerAsync();
            if (!result.RequestSucceeded) return;

            save = repository.Load();
            save.InstallReferrerConsumed = true;

            if (string.IsNullOrWhiteSpace(save.PendingReferralId) &&
                ReferralRuntimePolicy.TryNormalizeForCurrentRuntime(result.ReferrerId, out string normalized))
            {
                save.PendingReferralId = normalized;
                AnalyticsLifecycle.Service?.Track(
                    AnalyticsEventNames.ChallengeOpen,
                    new System.Collections.Generic.Dictionary<string, object>
                    {
                        ["referrer_id"] = save.PendingReferralId,
                        ["source"] = "install_referrer"
                    });
            }

            repository.Save(save);
            if (AnalyticsLifecycle.Service != null) _ = AnalyticsLifecycle.Service.FlushAsync();
        }
    }
}
