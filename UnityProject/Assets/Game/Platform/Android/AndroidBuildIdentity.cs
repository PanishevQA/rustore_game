using UnityEngine;

namespace DontGetSidetracked.Platform.Android
{
    public readonly struct RuntimeBuildIdentity
    {
        public RuntimeBuildIdentity(string packageName, string version, long versionCode, string buildGuid)
        {
            PackageName = packageName ?? string.Empty;
            Version = version ?? string.Empty;
            VersionCode = versionCode;
            BuildGuid = buildGuid ?? string.Empty;
        }

        public string PackageName { get; }
        public string Version { get; }
        public long VersionCode { get; }
        public string BuildGuid { get; }
    }

    /// <summary>
    /// Runtime release identity used by diagnostics/analytics. Android versionCode is read from the
    /// installed package rather than duplicated in gameplay configuration, so the value describes
    /// the artifact that is actually running on the device.
    /// </summary>
    public static class AndroidBuildIdentity
    {
        private static bool _resolved;
        private static RuntimeBuildIdentity _cached;

        public static RuntimeBuildIdentity Current
        {
            get
            {
                if (!_resolved) Resolve();
                return _cached;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            _resolved = false;
            _cached = default;
        }

        private static void Resolve()
        {
            string packageName = Application.identifier ?? string.Empty;
            long versionCode = ResolveVersionCode(packageName);
            _cached = new RuntimeBuildIdentity(packageName, Application.version, versionCode, Application.buildGUID);
            _resolved = true;
        }

        private static long ResolveVersionCode(string packageName)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (string.IsNullOrWhiteSpace(packageName)) return 0;
            try
            {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                if (activity == null) return 0;

                using AndroidJavaObject packageManager = activity.Call<AndroidJavaObject>("getPackageManager");
                using AndroidJavaObject packageInfo = packageManager.Call<AndroidJavaObject>("getPackageInfo", packageName, 0);
                using var version = new AndroidJavaClass("android.os.Build$VERSION");
                int sdk = version.GetStatic<int>("SDK_INT");
                if (sdk >= 28)
                    return packageInfo.Call<long>("getLongVersionCode");
                return packageInfo.Get<int>("versionCode");
            }
            catch (System.Exception error)
            {
                Debug.LogWarning($"Android versionCode unavailable: {error.Message}");
                return 0;
            }
#else
            return 0;
#endif
        }
    }
}
