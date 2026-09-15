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

        public Task CheckForUpdateAsync()
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
            return completion.Task;
#else
            IsUpdateAvailable = false;
            IsUpdateInProgress = false;
            AvailableVersionCode = 0;
            return Task.CompletedTask;
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

        public Task CompleteFlexibleAsync()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            var completion = new TaskCompletionSource<bool>();
            try
            {
                RuStoreAppUpdateManager.Instance.CompleteUpdate(
                    UpdateType.FLEXIBLE,
                    error => completion.TrySetException(
                        new InvalidOperationException($"RuStore update completion failed: {error}")));
                // CompleteUpdate has no success callback. Reaching this call means it was handed to the SDK.
                completion.TrySetResult(true);
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
