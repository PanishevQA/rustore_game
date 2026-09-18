using System.Threading.Tasks;
using DontGetSidetracked.Services;
using UnityEngine;

namespace DontGetSidetracked.Platform.RuStore
{
    /// <summary>
    /// Offline/editor-safe update adapter.
    /// The official RuStore Update SDK is intentionally not loaded in the local Editor baseline.
    /// ProductionReleaseValidator blocks a production Android build until the verified SDK set is restored.
    /// </summary>
    public sealed class RuStoreUpdateService : IUpdateService
    {
        public bool IsUpdateAvailable { get; private set; }
        public bool IsUpdateInProgress { get; private set; }
        public long AvailableVersionCode { get; private set; }

        public Task CheckForUpdateAsync(bool mandatory)
        {
            ResetState();
#if UNITY_ANDROID && !UNITY_EDITOR
            Debug.LogWarning("RuStore Update SDK is not present in the local baseline; update check skipped.");
#endif
            return Task.CompletedTask;
        }

        public Task StartFlexibleAsync()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            Debug.LogWarning("RuStore Update SDK is not present in the local baseline; flexible update skipped.");
#endif
            return Task.CompletedTask;
        }

        public Task StartImmediateAsync()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            Debug.LogWarning("RuStore Update SDK is not present in the local baseline; immediate update skipped.");
#endif
            return Task.CompletedTask;
        }

        public void CompleteFlexibleUpdate()
        {
            ResetState();
        }

        private void ResetState()
        {
            IsUpdateAvailable = false;
            IsUpdateInProgress = false;
            AvailableVersionCode = 0;
        }
    }
}
