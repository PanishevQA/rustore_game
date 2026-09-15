using DontGetSidetracked.Services;
using UnityEngine;

namespace DontGetSidetracked.Platform.Android
{
    public sealed class AndroidNotificationPermissionService : INotificationPermissionService
    {
        private const string Permission = "android.permission.POST_NOTIFICATIONS";
        private const int PermissionGranted = 0;
        private const int RequestCode = 4317;

        public bool IsRuntimePermissionRequired
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                using var version = new AndroidJavaClass("android.os.Build$VERSION");
                return version.GetStatic<int>("SDK_INT") >= 33;
#else
                return false;
#endif
            }
        }

        public bool IsGranted
        {
            get
            {
                if (!IsRuntimePermissionRequired) return true;
#if UNITY_ANDROID && !UNITY_EDITOR
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                return activity != null && activity.Call<int>("checkSelfPermission", Permission) == PermissionGranted;
#else
                return true;
#endif
            }
        }

        public void Request()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!IsRuntimePermissionRequired || IsGranted) return;
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            if (activity == null) return;
            activity.Call("requestPermissions", new[] { Permission }, RequestCode);
#endif
        }
    }
}
