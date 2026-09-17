using System;
using System.Threading.Tasks;
using DontGetSidetracked.Services;
using RuStore.AppUpdate;

namespace DontGetSidetracked.Platform.RuStore
{
    public sealed class RuStoreUpdateService : IUpdateService
    {
        public bool IsUpdateAvailable { get; private set; }
        public bool IsUpdateInProgress { get; private set; }
        public long AvailableVersionCode { get; private set; }

        /// <summary>
        /// Refreshes RuStore update state only. The presentation coordinator decides when it is safe
        /// to launch an update flow after this await completes, so a slow SDK response cannot open
        /// a flexible update on top of an active gameplay round.
        /// </summary>
        public async Task CheckForUpdateAsync(bool mandatory)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            var completion = new TaskCompletionSource<bool>();
            try
            {
                RuStoreAppUpdateManager manager = RuStoreAppUpdateManager.Instance;
                if (!manager.IsInitialized) manager.Init();

                manager.GetAppUpdateInfo(
                    onFailure: error => completion.TrySetException(
                        new InvalidOperationException($"RuStore update check failed: {error}")),
                    onSuccess: info =>
                    {
                        IsUpdateAvailable = info != null &&
                            info.updateAvailability == AppUpdateInfo.UpdateAvailability.UPDATE_AVAILABLE;
                        IsUpdateInProgress = info != null &&
                            info.updateAvailability == AppUpdateInfo.UpdateAvailability.DEVELOPER_TRIGGERED_UPDATE_IN_PROGRESS;
                        AvailableVersionCode = info?.availableVersionCode ?? 0;
                        completion.TrySetResult(true);
                    });
            }
            catch (Exception error)
            {
                completion.TrySetException(error);
            }

            await completion.Task;
#else
            IsUpdateAvailable = false;
            IsUpdateInProgress = false;
            AvailableVersionCode = 0;
            await Task.CompletedTask;
#endif
        }

        public Task StartFlexibleAsync() => StartFlowAsync(UpdateType.FLEXIBLE);

        public Task StartImmediateAsync()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!RuStoreAppUpdateManager.Instance.IsImmediateUpdateAllowed())
                return Task.FromException(new InvalidOperationException("Immediate RuStore update is not allowed."));
#endif
            return StartFlowAsync(UpdateType.IMMEDIATE);
        }

        public void CompleteFlexibleUpdate()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            RuStoreAppUpdateManager.Instance.CompleteUpdate(
                UpdateType.FLEXIBLE,
                error => UnityEngine.Debug.LogWarning($"RuStore update completion failed: {error}"));
#endif
        }

        private static Task StartFlowAsync(UpdateType updateType)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            var completion = new TaskCompletionSource<bool>();
            try
            {
                RuStoreAppUpdateManager manager = RuStoreAppUpdateManager.Instance;
                if (!manager.IsInitialized) manager.Init();
                manager.StartUpdateFlow(
                    updateType,
                    error => completion.TrySetException(
                        new InvalidOperationException($"RuStore update flow failed: {error}")),
                    _ => completion.TrySetResult(true));
            }
            catch (Exception error)
            {
                completion.TrySetException(error);
            }
            return completion.Task;
#else
            return Task.CompletedTask;
#endif
        }
    }
}
