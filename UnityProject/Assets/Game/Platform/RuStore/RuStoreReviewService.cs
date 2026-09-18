using System.Threading.Tasks;
using DontGetSidetracked.Services;
using UnityEngine;

namespace DontGetSidetracked.Platform.RuStore
{
    /// <summary>
    /// Offline/editor-safe review adapter.
    /// The official RuStore Review SDK is intentionally not loaded in the local Editor baseline.
    /// ProductionReleaseValidator blocks a production Android build until the verified SDK set is restored.
    /// </summary>
    public sealed class RuStoreReviewService : IReviewService
    {
        public Task RequestReviewAsync()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            Debug.LogWarning("RuStore Review SDK is not present in the local baseline; review request skipped.");
#endif
            return Task.CompletedTask;
        }
    }
}
