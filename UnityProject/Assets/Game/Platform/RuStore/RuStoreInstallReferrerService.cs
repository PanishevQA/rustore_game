using System;
using System.Threading;
using System.Threading.Tasks;
using DontGetSidetracked.Services;
using UnityEngine;

namespace DontGetSidetracked.Platform.RuStore
{
    /// <summary>
    /// Android-native RuStore Install Referrer adapter.
    ///
    /// The official Unity Install Referrer 10.6.1 package cannot coexist with the official
    /// Remote Config 10.5.1 package because their published Unity .meta files contain duplicate
    /// GUIDs. To keep both production features without forking either RuStore Unity package,
    /// this adapter talks directly to the official Android artifact
    /// ru.rustore.sdk:installreferrer:10.6.1 through Unity's AndroidJava bridge.
    ///
    /// Gameplay remains isolated behind IReferrerService and the one-shot persistence policy
    /// remains owned by InstallReferrerCapture.
    /// </summary>
    public sealed class RuStoreInstallReferrerService : IReferrerService
    {
        private const string UnityPlayerClass = "com.unity3d.player.UnityPlayer";
        private const string NativeClientClass = "ru.rustore.sdk.install.referrer.InstallReferrerClient";

        public Task<InstallReferrerResult> ConsumeInstallReferrerAsync()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var unityPlayer = new AndroidJavaClass(UnityPlayerClass))
                using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    if (activity == null)
                    {
                        Debug.LogWarning("RuStore Install Referrer unavailable: Unity currentActivity is null.");
                        return Task.FromResult(new InstallReferrerResult(false, null));
                    }

                    var request = new NativeRequest();
                    return request.Start(activity);
                }
            }
            catch (Exception error)
            {
                Debug.LogWarning($"RuStore Install Referrer unavailable: {error.Message}");
                return Task.FromResult(new InstallReferrerResult(false, null));
            }
#else
            return Task.FromResult(new InstallReferrerResult(false, null));
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private sealed class NativeRequest
        {
            private readonly TaskCompletionSource<InstallReferrerResult> _completion =
                new TaskCompletionSource<InstallReferrerResult>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

            private AndroidJavaObject _client;
            private AndroidJavaObject _task;
            private SuccessListener _successListener;
            private FailureListener _failureListener;
            private int _completed;

            public Task<InstallReferrerResult> Start(AndroidJavaObject activity)
            {
                try
                {
                    _client = new AndroidJavaObject(NativeClientClass, activity);
                    _task = _client.Call<AndroidJavaObject>("getInstallReferrerV2");
                    if (_task == null)
                        throw new InvalidOperationException("InstallReferrerClient.getInstallReferrerV2() returned null Task.");

                    _successListener = new SuccessListener(this);
                    _failureListener = new FailureListener(this);

                    _task.Call("addOnSuccessListener", _successListener);
                    _task.Call("addOnFailureListener", _failureListener);
                    return _completion.Task;
                }
                catch (Exception error)
                {
                    CompleteFailure("RuStore Install Referrer request could not be started: " + error.Message);
                    return _completion.Task;
                }
            }

            internal void CompleteSuccess(AndroidJavaObject result)
            {
                string referrerId = null;
                try
                {
                    if (result != null)
                        referrerId = result.Call<string>("getInstallReferrer");
                }
                catch (Exception error)
                {
                    CompleteFailure("RuStore Install Referrer response could not be read: " + error.Message);
                    return;
                }

                Complete(new InstallReferrerResult(
                    true,
                    string.IsNullOrWhiteSpace(referrerId) ? null : referrerId));
            }

            internal void CompleteFailure(AndroidJavaObject error)
            {
                string detail = "unknown error";
                try
                {
                    if (error != null)
                        detail = error.Call<string>("toString") ?? detail;
                }
                catch
                {
                    // Keep the generic message. A failed referrer request must never crash gameplay.
                }

                CompleteFailure("RuStore Install Referrer request failed: " + detail);
            }

            private void CompleteFailure(string message)
            {
                Debug.LogWarning(message);
                Complete(new InstallReferrerResult(false, null));
            }

            private void Complete(InstallReferrerResult result)
            {
                if (Interlocked.Exchange(ref _completed, 1) != 0) return;

                _completion.TrySetResult(result);

                try { _task?.Dispose(); } catch { }
                try { _client?.Dispose(); } catch { }

                _task = null;
                _client = null;
                _successListener = null;
                _failureListener = null;
            }
        }

        private sealed class SuccessListener : AndroidJavaProxy
        {
            private readonly NativeRequest _request;

            public SuccessListener(NativeRequest request)
                : base("ru.rustore.sdk.core.tasks.OnSuccessListener")
            {
                _request = request;
            }

            // Java interface method name is intentionally lower camel case.
            public void onSuccess(AndroidJavaObject result)
            {
                _request.CompleteSuccess(result);
            }
        }

        private sealed class FailureListener : AndroidJavaProxy
        {
            private readonly NativeRequest _request;

            public FailureListener(NativeRequest request)
                : base("ru.rustore.sdk.core.tasks.OnFailureListener")
            {
                _request = request;
            }

            // Java interface method name is intentionally lower camel case.
            public void onFailure(AndroidJavaObject error)
            {
                _request.CompleteFailure(error);
            }
        }
#endif
    }
}
