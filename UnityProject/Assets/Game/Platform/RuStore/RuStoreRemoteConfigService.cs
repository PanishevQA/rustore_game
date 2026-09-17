using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using DontGetSidetracked.Core;
using DontGetSidetracked.Platform.Android;
using DontGetSidetracked.Services;
using UnityEngine;

namespace DontGetSidetracked.Platform.RuStore
{
    /// <summary>
    /// RuStore Remote Config adapter with an app-local validated snapshot.
    /// Gameplay reads only IRemoteConfigService and remains usable when RuStore/network is unavailable.
    /// </summary>
    public sealed class RuStoreRemoteConfigService : IRemoteConfigService
    {
        private readonly string _appId;
        private readonly string _account;
        private readonly string _cachePath;
        private readonly string _cacheBackupPath;
        private readonly string _cacheTempPath;
        private readonly object _cacheGate = new object();
        private Snapshot _snapshot;

        public string MinSupportedVersion => _snapshot.minSupportedVersion;
        public string RecommendedVersion => _snapshot.recommendedVersion;

        public RuStoreRemoteConfigService(string appId, string account, string cacheFileName = "rustore-remote-config.json")
        {
            _appId = appId ?? string.Empty;
            _account = account ?? string.Empty;
            _cachePath = Path.Combine(Application.persistentDataPath, cacheFileName);
            _cacheBackupPath = _cachePath + ".bak";
            _cacheTempPath = _cachePath + ".tmp";
            _snapshot = LoadCache() ?? Snapshot.CreateDefaults();
            Normalize(_snapshot);
        }

        public async Task<bool> RefreshAsync()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (string.IsNullOrWhiteSpace(_appId)) return false;

            try
            {
                object client = GetClient(out Type clientType);
                EnsureInitialized(client, clientType);
                object remoteConfig = await GetRemoteConfigAsync(client, clientType);
                if (remoteConfig == null) return false;

                Snapshot next = Clone(_snapshot);
                ApplyRemoteConfig(remoteConfig, next);
                Normalize(next);
                _snapshot = next;
                SaveCache();
                return true;
            }
            catch (Exception error)
            {
                Debug.LogWarning($"RuStore Remote Config unavailable; using cache/defaults: {Unwrap(error).Message}");
                return false;
            }
#else
            await Task.CompletedTask;
            return false;
#endif
        }

        public double GetDouble(string key, double fallback)
        {
            switch (key)
            {
                case "route_display_time_easy_ms": return _snapshot.routeDisplayTimeEasyMs;
                case "route_display_time_medium_ms": return _snapshot.routeDisplayTimeMediumMs;
                case "route_display_time_hard_ms": return _snapshot.routeDisplayTimeHardMs;
                default: return fallback;
            }
        }

        public int GetInt(string key, int fallback)
        {
            switch (key)
            {
                case "route_display_time_easy_ms": return _snapshot.routeDisplayTimeEasyMs;
                case "route_display_time_medium_ms": return _snapshot.routeDisplayTimeMediumMs;
                case "route_display_time_hard_ms": return _snapshot.routeDisplayTimeHardMs;
                case "daily_route_count": return _snapshot.dailyRouteCount;
                case "interstitial_min_rounds": return _snapshot.interstitialMinRounds;
                case "interstitial_cooldown_sec": return _snapshot.interstitialCooldownSec;
                case "review_min_sessions": return _snapshot.reviewMinSessions;
                case "daily_reminder_hour": return _snapshot.dailyReminderHour;
                default: return fallback;
            }
        }

        public bool GetBool(string key, bool fallback)
        {
            switch (key)
            {
                case "rewarded_enabled": return _snapshot.rewardedEnabled;
                case "interstitial_enabled": return _snapshot.interstitialEnabled;
                case "local_daily_reminder_enabled": return _snapshot.localDailyReminderEnabled;
                default: return fallback;
            }
        }

        public string GetString(string key, string fallback)
        {
            string value;
            switch (key)
            {
                case "share_copy_variant": value = _snapshot.shareCopyVariant; break;
                case "store_offer_variant": value = _snapshot.storeOfferVariant; break;
                case "min_supported_version": value = _snapshot.minSupportedVersion; break;
                case "recommended_version": value = _snapshot.recommendedVersion; break;
                default: return fallback;
            }
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        public bool RequiresMandatoryUpdate(string currentVersion) =>
            AppVersionPolicy.Compare(currentVersion, MinSupportedVersion) < 0;

        public bool ShouldRecommendUpdate(string currentVersion) =>
            AppVersionPolicy.Compare(currentVersion, RecommendedVersion) < 0;

#if UNITY_ANDROID && !UNITY_EDITOR
        private object GetClient(out Type clientType)
        {
            clientType = FindRuStoreType("RuStoreRemoteConfigClient") ??
                         throw new InvalidOperationException("RuStoreRemoteConfigClient type not found. Verify ru.rustore.remoteconfig package resolution.");
            PropertyInfo instance = clientType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static) ??
                                    clientType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static);
            object client = instance?.GetValue(null);
            if (client == null) throw new InvalidOperationException("RuStoreRemoteConfigClient.Instance is unavailable.");
            return client;
        }

        private void EnsureInitialized(object client, Type clientType)
        {
            PropertyInfo initialized = clientType.GetProperty("IsInitialized", BindingFlags.Public | BindingFlags.Instance) ??
                                       clientType.GetProperty("isInitialized", BindingFlags.Public | BindingFlags.Instance);
            if (initialized != null && initialized.PropertyType == typeof(bool) && (bool)initialized.GetValue(client)) return;

            Type settingsType = FindRuStoreType("RuStoreRemoteConfigClientSettings") ??
                                throw new InvalidOperationException("RuStoreRemoteConfigClientSettings type not found.");
            object settings = Activator.CreateInstance(settingsType) ??
                              throw new InvalidOperationException("Could not create RuStore Remote Config settings.");

            RuntimeBuildIdentity buildIdentity = AndroidBuildIdentity.Current;
            SetMember(settings, "appId", _appId);
            SetMember(settings, "account", _account);
            SetMember(settings, "language", Application.systemLanguage.ToString().ToLowerInvariant());
            SetMember(settings, "osVersion", SystemInfo.operatingSystem);
            SetMember(settings, "deviceModel", SystemInfo.deviceModel);
            SetMember(settings, "appVersion", buildIdentity.Version);
            SetMember(settings, "appBuild", buildIdentity.VersionCode.ToString(CultureInfo.InvariantCulture));
            SetEnumMember(settings, "environment", "Release");

            MethodInfo init = null;
            MethodInfo[] methods = clientType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < methods.Length; i++)
            {
                if (!string.Equals(methods[i].Name, "Init", StringComparison.Ordinal)) continue;
                ParameterInfo[] parameters = methods[i].GetParameters();
                if (parameters.Length < 1 || parameters[0].ParameterType != settingsType) continue;
                if (init == null || parameters.Length < init.GetParameters().Length) init = methods[i];
            }
            if (init == null) throw new MissingMethodException(clientType.FullName, "Init");

            ParameterInfo[] initParameters = init.GetParameters();
            var arguments = new object[initParameters.Length];
            arguments[0] = settings;
            for (int i = 1; i < arguments.Length; i++) arguments[i] = null;
            init.Invoke(client, arguments);
        }

        private static Task<object> GetRemoteConfigAsync(object client, Type clientType)
        {
            MethodInfo target = null;
            MethodInfo[] methods = clientType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < methods.Length; i++)
            {
                if (!string.Equals(methods[i].Name, "GetRemoteConfig", StringComparison.Ordinal)) continue;
                ParameterInfo[] parameters = methods[i].GetParameters();
                if (parameters.Length == 2 &&
                    typeof(Delegate).IsAssignableFrom(parameters[0].ParameterType) &&
                    typeof(Delegate).IsAssignableFrom(parameters[1].ParameterType))
                {
                    target = methods[i];
                    break;
                }
            }
            if (target == null) throw new MissingMethodException(clientType.FullName, "GetRemoteConfig");

            var box = new RemoteConfigCallbackBox();
            ParameterInfo[] callbackParameters = target.GetParameters();
            var args = new object[2];
            bool successBound = false;
            bool failureBound = false;
            for (int i = 0; i < callbackParameters.Length; i++)
            {
                string name = callbackParameters[i].Name ?? string.Empty;
                bool failure = name.IndexOf("failure", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               name.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0;
                if (failure)
                {
                    args[i] = CreateCallback(callbackParameters[i].ParameterType, box, nameof(RemoteConfigCallbackBox.OnFailure));
                    failureBound = true;
                }
                else
                {
                    args[i] = CreateCallback(callbackParameters[i].ParameterType, box, nameof(RemoteConfigCallbackBox.OnSuccess));
                    successBound = true;
                }
            }

            if (!successBound || !failureBound)
            {
                args[0] = CreateCallback(callbackParameters[0].ParameterType, box, nameof(RemoteConfigCallbackBox.OnSuccess));
                args[1] = CreateCallback(callbackParameters[1].ParameterType, box, nameof(RemoteConfigCallbackBox.OnFailure));
            }

            target.Invoke(client, args);
            return box.Task;
        }

        private static Delegate CreateCallback(Type delegateType, RemoteConfigCallbackBox target, string methodName)
        {
            Type[] genericArguments = delegateType.IsGenericType ? delegateType.GetGenericArguments() : Type.EmptyTypes;
            if (genericArguments.Length != 1)
                throw new InvalidOperationException($"Unexpected RuStore Remote Config callback type: {delegateType.FullName}");

            MethodInfo open = typeof(RemoteConfigCallbackBox).GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public) ??
                              throw new MissingMethodException(typeof(RemoteConfigCallbackBox).FullName, methodName);
            MethodInfo closed = open.MakeGenericMethod(genericArguments[0]);
            return Delegate.CreateDelegate(delegateType, target, closed);
        }

        private static void ApplyRemoteConfig(object remoteConfig, Snapshot target)
        {
            target.routeDisplayTimeEasyMs = ReadInt(remoteConfig, "route_display_time_easy_ms", target.routeDisplayTimeEasyMs);
            target.routeDisplayTimeMediumMs = ReadInt(remoteConfig, "route_display_time_medium_ms", target.routeDisplayTimeMediumMs);
            target.routeDisplayTimeHardMs = ReadInt(remoteConfig, "route_display_time_hard_ms", target.routeDisplayTimeHardMs);
            target.dailyRouteCount = ReadInt(remoteConfig, "daily_route_count", target.dailyRouteCount);
            target.rewardedEnabled = ReadBool(remoteConfig, "rewarded_enabled", target.rewardedEnabled);
            target.interstitialEnabled = ReadBool(remoteConfig, "interstitial_enabled", target.interstitialEnabled);
            target.interstitialMinRounds = ReadInt(remoteConfig, "interstitial_min_rounds", target.interstitialMinRounds);
            target.interstitialCooldownSec = ReadInt(remoteConfig, "interstitial_cooldown_sec", target.interstitialCooldownSec);
            target.shareCopyVariant = ReadString(remoteConfig, "share_copy_variant", target.shareCopyVariant);
            target.reviewMinSessions = ReadInt(remoteConfig, "review_min_sessions", target.reviewMinSessions);
            target.localDailyReminderEnabled = ReadBool(remoteConfig, "local_daily_reminder_enabled", target.localDailyReminderEnabled);
            target.dailyReminderHour = ReadInt(remoteConfig, "daily_reminder_hour", target.dailyReminderHour);
            target.storeOfferVariant = ReadString(remoteConfig, "store_offer_variant", target.storeOfferVariant);
            target.minSupportedVersion = ReadString(remoteConfig, "min_supported_version", target.minSupportedVersion);
            target.recommendedVersion = ReadString(remoteConfig, "recommended_version", target.recommendedVersion);
        }

        private static bool ContainsKey(object remoteConfig, string key)
        {
            MethodInfo method = remoteConfig.GetType().GetMethod("ContainsKey", new[] { typeof(string) });
            return method != null && (bool)method.Invoke(remoteConfig, new object[] { key });
        }

        private static int ReadInt(object remoteConfig, string key, int fallback)
        {
            if (!ContainsKey(remoteConfig, key)) return fallback;
            try
            {
                MethodInfo method = remoteConfig.GetType().GetMethod("GetInt", new[] { typeof(string) });
                return method == null ? fallback : Convert.ToInt32(method.Invoke(remoteConfig, new object[] { key }));
            }
            catch { return fallback; }
        }

        private static bool ReadBool(object remoteConfig, string key, bool fallback)
        {
            if (!ContainsKey(remoteConfig, key)) return fallback;
            try
            {
                MethodInfo method = remoteConfig.GetType().GetMethod("GetBool", new[] { typeof(string) });
                return method == null ? fallback : Convert.ToBoolean(method.Invoke(remoteConfig, new object[] { key }));
            }
            catch { return fallback; }
        }

        private static string ReadString(object remoteConfig, string key, string fallback)
        {
            if (!ContainsKey(remoteConfig, key)) return fallback;
            try
            {
                MethodInfo method = remoteConfig.GetType().GetMethod("GetString", new[] { typeof(string) });
                string value = method?.Invoke(remoteConfig, new object[] { key })?.ToString();
                return string.IsNullOrWhiteSpace(value) ? fallback : value;
            }
            catch { return fallback; }
        }

        private static Type FindRuStoreType(string shortName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int a = 0; a < assemblies.Length; a++)
            {
                Type[] types;
                try { types = assemblies[a].GetTypes(); }
                catch (ReflectionTypeLoadException error) { types = error.Types; }
                if (types == null) continue;
                for (int i = 0; i < types.Length; i++)
                {
                    Type type = types[i];
                    if (type == null || !string.Equals(type.Name, shortName, StringComparison.Ordinal)) continue;
                    if (type.Namespace != null && type.Namespace.StartsWith("RuStore", StringComparison.Ordinal)) return type;
                }
            }
            return null;
        }

        private static void SetMember(object target, string name, object value)
        {
            Type type = target.GetType();
            FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (field != null)
            {
                field.SetValue(target, value);
                return;
            }
            PropertyInfo property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (property != null && property.CanWrite) property.SetValue(target, value);
        }

        private static void SetEnumMember(object target, string name, string preferred)
        {
            Type type = target.GetType();
            FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            Type enumType = field?.FieldType;
            PropertyInfo property = null;
            if (enumType == null)
            {
                property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                enumType = property?.PropertyType;
            }
            if (enumType == null || !enumType.IsEnum) return;

            object value;
            try { value = Enum.Parse(enumType, preferred, true); }
            catch { value = Enum.ToObject(enumType, 0); }
            if (field != null) field.SetValue(target, value);
            else if (property != null && property.CanWrite) property.SetValue(target, value);
        }

        private sealed class RemoteConfigCallbackBox
        {
            private readonly TaskCompletionSource<object> _completion = new TaskCompletionSource<object>();
            public Task<object> Task => _completion.Task;

            public void OnSuccess<T>(T response) => _completion.TrySetResult(response);

            public void OnFailure<T>(T error) => _completion.TrySetException(
                new InvalidOperationException($"RuStore Remote Config request failed: {error}"));
        }
#endif

        private Snapshot LoadCache()
        {
            lock (_cacheGate)
            {
                Snapshot primary = TryLoadCacheFile(_cachePath);
                Snapshot interruptedWrite = TryLoadCacheFile(_cacheTempPath);
                Snapshot loaded = primary;

                if (ShouldRecoverInterruptedCache(primary, interruptedWrite))
                {
                    loaded = interruptedWrite;
                    PromoteInterruptedCache(preservePrimaryAsBackup: primary != null);
                }
                else if (File.Exists(_cacheTempPath))
                {
                    TryDelete(_cacheTempPath);
                }

                if (loaded != null) return loaded;

                Snapshot backup = TryLoadCacheFile(_cacheBackupPath);
                if (backup != null)
                {
                    RestoreCacheFromBackup(overwriteExisting: true);
                    return backup;
                }

                TryDelete(_cachePath);
                TryDelete(_cacheBackupPath);
                TryDelete(_cacheTempPath);
                return null;
            }
        }

        private void SaveCache()
        {
            lock (_cacheGate)
            {
                string tempPath = _cacheTempPath;
                try
                {
                    File.WriteAllText(tempPath, JsonUtility.ToJson(_snapshot));
                    if (File.Exists(_cachePath))
                    {
                        File.Copy(_cachePath, _cacheBackupPath, true);
                        File.Delete(_cachePath);
                    }
                    File.Move(tempPath, _cachePath);
                }
                catch (Exception error)
                {
                    RestoreCacheFromBackup(overwriteExisting: false);
                    TryDelete(tempPath);
                    Debug.LogWarning($"RuStore Remote Config cache save failed: {error.Message}");
                }
            }
        }

        private bool ShouldRecoverInterruptedCache(Snapshot primary, Snapshot interruptedWrite)
        {
            bool primaryValid = primary != null;
            bool tempValid = interruptedWrite != null;
            if (!tempValid || !primaryValid)
                return SaveRecoveryPolicy.ShouldRecoverInterruptedWrite(primaryValid, tempValid, default, default);
            if (!File.Exists(_cachePath) || !File.Exists(_cacheTempPath)) return false;

            try
            {
                DateTime primaryWriteUtc = File.GetLastWriteTimeUtc(_cachePath);
                DateTime tempWriteUtc = File.GetLastWriteTimeUtc(_cacheTempPath);
                return SaveRecoveryPolicy.ShouldRecoverInterruptedWrite(true, true, primaryWriteUtc, tempWriteUtc);
            }
            catch (Exception error)
            {
                Debug.LogWarning($"RuStore Remote Config cache timestamp check failed: {error.Message}");
                return false;
            }
        }

        private void PromoteInterruptedCache(bool preservePrimaryAsBackup)
        {
            try
            {
                if (preservePrimaryAsBackup && File.Exists(_cachePath))
                    File.Copy(_cachePath, _cacheBackupPath, true);

                File.Copy(_cacheTempPath, _cachePath, true);
                TryDelete(_cacheTempPath);
            }
            catch (Exception error)
            {
                Debug.LogWarning($"RuStore Remote Config interrupted cache recovery failed: {error.Message}");
            }
        }

        private void RestoreCacheFromBackup(bool overwriteExisting)
        {
            if (!File.Exists(_cacheBackupPath)) return;
            if (!overwriteExisting && File.Exists(_cachePath)) return;
            try
            {
                File.Copy(_cacheBackupPath, _cachePath, true);
            }
            catch (Exception error)
            {
                Debug.LogWarning($"RuStore Remote Config backup restore failed: {error.Message}");
            }
        }

        private static Snapshot TryLoadCacheFile(string path)
        {
            if (!File.Exists(path)) return null;
            try
            {
                return JsonUtility.FromJson<Snapshot>(File.ReadAllText(path));
            }
            catch (Exception error)
            {
                Debug.LogWarning($"RuStore Remote Config cache ignored ({Path.GetFileName(path)}): {error.Message}");
                return null;
            }
        }

        private static void TryDelete(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
            try
            {
                File.Delete(path);
            }
            catch (Exception error)
            {
                Debug.LogWarning($"RuStore Remote Config stale cache file could not be removed ({Path.GetFileName(path)}): {error.Message}");
            }
        }

        private static Snapshot Clone(Snapshot source) => JsonUtility.FromJson<Snapshot>(JsonUtility.ToJson(source));

        private static void Normalize(Snapshot s)
        {
            if (s == null) return;
            s.routeDisplayTimeEasyMs = Clamp(s.routeDisplayTimeEasyMs, 750, 10000, 3500);
            s.routeDisplayTimeMediumMs = Clamp(s.routeDisplayTimeMediumMs, 750, 10000, 3000);
            s.routeDisplayTimeHardMs = Clamp(s.routeDisplayTimeHardMs, 750, 10000, 2500);
            s.dailyRouteCount = Clamp(s.dailyRouteCount, 1, 3, 3);
            s.interstitialMinRounds = Clamp(s.interstitialMinRounds, 1, 100, 5);
            s.interstitialCooldownSec = Clamp(s.interstitialCooldownSec, 0, 86400, 180);
            s.reviewMinSessions = Clamp(s.reviewMinSessions, 1, 1000, 5);
            s.dailyReminderHour = Clamp(s.dailyReminderHour, 0, 23, 10);
            if (string.IsNullOrWhiteSpace(s.shareCopyVariant)) s.shareCopyVariant = "A";
            if (string.IsNullOrWhiteSpace(s.storeOfferVariant)) s.storeOfferVariant = "A";

            AppVersionRange versions = AppVersionPolicy.NormalizeRange(
                s.minSupportedVersion,
                s.recommendedVersion,
                AppVersionPolicy.SafeDefaultVersion);
            s.minSupportedVersion = versions.MinSupportedVersion;
            s.recommendedVersion = versions.RecommendedVersion;
        }

        private static int Clamp(int value, int min, int max, int fallback) =>
            value < min || value > max ? fallback : value;

        private static Exception Unwrap(Exception error) =>
            error is TargetInvocationException invocation && invocation.InnerException != null
                ? invocation.InnerException
                : error;

        [Serializable]
        private sealed class Snapshot
        {
            public int routeDisplayTimeEasyMs = 3500;
            public int routeDisplayTimeMediumMs = 3000;
            public int routeDisplayTimeHardMs = 2500;
            public int dailyRouteCount = 3;
            public bool rewardedEnabled = true;
            public bool interstitialEnabled = true;
            public int interstitialMinRounds = 5;
            public int interstitialCooldownSec = 180;
            public string shareCopyVariant = "A";
            public int reviewMinSessions = 5;
            public bool localDailyReminderEnabled = true;
            public int dailyReminderHour = 10;
            public string storeOfferVariant = "A";
            public string minSupportedVersion = AppVersionPolicy.SafeDefaultVersion;
            public string recommendedVersion = AppVersionPolicy.SafeDefaultVersion;

            public static Snapshot CreateDefaults() => new Snapshot();
        }
    }

    public static class RuStoreRemoteConfigSettings
    {
        // Remote Config application/tool ID from RuStore Console. Required for production release.
        public const string AppId = "";
    }
}
