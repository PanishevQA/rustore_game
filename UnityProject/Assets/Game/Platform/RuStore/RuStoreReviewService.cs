using System;
using System.Threading.Tasks;
using DontGetSidetracked.Services;
using RuStore.Review;

namespace DontGetSidetracked.Platform.RuStore
{
    public sealed class RuStoreReviewService : IReviewService
    {
        public Task RequestReviewAsync()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            var completion = new TaskCompletionSource<bool>();
            try
            {
                RuStoreReviewManager manager = RuStoreReviewManager.Instance;
                if (!manager.IsInitialized) manager.Init();

                manager.RequestReviewFlow(
                    onFailure: error => completion.TrySetException(
                        new InvalidOperationException($"RuStore review prepare failed: {error}")),
                    onSuccess: () => manager.LaunchReviewFlow(
                        onFailure: error => completion.TrySetException(
                            new InvalidOperationException($"RuStore review launch failed: {error}")),
                        onSuccess: () => completion.TrySetResult(true)));
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
