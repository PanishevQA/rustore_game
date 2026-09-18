using System.Threading.Tasks;
using DontGetSidetracked.Services;
using UnityEngine;

namespace DontGetSidetracked.Platform.RuStore
{
    /// <summary>
    /// Offline/editor-safe review adapter.
    /// The official RuStore Review SDK is intentionally not part of the local Editor baseline.
    /// Production release validation remains responsible for requiring the verified SDK set.
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
