using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using DontGetSidetracked.Services;
using UnityEngine;
using UnityEngine.Networking;

namespace DontGetSidetracked.Network
{
    [Serializable]
    internal sealed class BootstrapConfigValuesDto
    {
        public int route_display_time_easy_ms = 3500;
        public int route_display_time_medium_ms = 3000;
        public int route_display_time_hard_ms = 2500;
        public int daily_route_count = 3;
        public bool rewarded_enabled = true;
        public bool interstitial_enabled = true;
        public int interstitial_min_rounds = 5;
        public int interstitial_cooldown_sec = 180;
        public string share_copy_variant = "A";
        public int review_min_sessions = 5;
        public int daily_push_hour = 10;
        public string store_offer_variant = "A";
    }

    [Serializable]
    internal sealed class BootstrapConfigDto
    {
        public string serverTimeUtc;
        public string minSupportedVersion = "0.1.0";
        public string recommendedVersion = "0.1.0";
        public BootstrapConfigValuesDto config = new BootstrapConfigValuesDto();
    }

    public sealed class BootstrapRemoteConfigService : IRemoteConfigService
    {
        private readonly string _baseUrl;
        private readonly string _cachePath;
        private BootstrapConfigDto _snapshot;

        public string MinSupportedVersion => _snapshot.minSupportedVersion;
        public string RecommendedVersion => _snapshot.recommendedVersion;
        public string ServerTimeUtc => _snapshot.serverTimeUtc;

        public BootstrapRemoteConfigService(string baseUrl, string cacheFileName = "bootstrap-config.json")
        {
            _baseUrl = (baseUrl ?? string.Empty).TrimEnd('/');
            _cachePath = Path.Combine(Application.persistentDataPath, cacheFileName);
            _snapshot = LoadCache() ?? CreateDefaults();
            Normalize(_snapshot);
        }

        public async Task<bool> RefreshAsync()
        {
            if (string.IsNullOrWhiteSpace(_baseUrl)) return false;

            try
            {
                using var request = UnityWebRequest.Get(_baseUrl + "/config/bootstrap");
                request.timeout = 8;
                request.SetRequestHeader("Accept", "application/json");
                UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                var completion = new TaskCompletionSource<bool>();
                operation.completed += _ => completion.TrySetResult(true);
                await completion.Task;

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.Log($"Bootstrap config uses cache/defaults: HTTP {(long)request.responseCode} {request.error}");
                    return false;
                }

                BootstrapConfigDto response = JsonUtility.FromJson<BootstrapConfigDto>(request.downloadHandler.text);
                if (response == null) return false;
                Normalize(response);
                _snapshot = response;
                SaveCache();
                return true;
            }
            catch (Exception error)
            {
                Debug.Log($"Bootstrap config uses cache/defaults: {error.Message}");
                return false;
            }
        }

        public double GetDouble(string key, double fallback) => GetInt(key, (int)Math.Round(fallback));

        public int GetInt(string key, int fallback)
        {
            BootstrapConfigValuesDto c = _snapshot.config;
            switch (key)
            {
                case "route_display_time_easy_ms": return Positive(c.route_display_time_easy_ms, fallback);
                case "route_display_time_medium_ms": return Positive(c.route_display_time_medium_ms, fallback);
                case "route_display_time_hard_ms": return Positive(c.route_display_time_hard_ms, fallback);
                case "daily_route_count": return Positive(c.daily_route_count, fallback);
                case "interstitial_min_rounds": return Positive(c.interstitial_min_rounds, fallback);
                case "interstitial_cooldown_sec": return Positive(c.interstitial_cooldown_sec, fallback);
                case "review_min_sessions": return Positive(c.review_min_sessions, fallback);
                case "daily_push_hour": return c.daily_push_hour >= 0 && c.daily_push_hour <= 23 ? c.daily_push_hour : fallback;
                default: return fallback;
            }
        }

        public bool GetBool(string key, bool fallback)
        {
            switch (key)
            {
                case "rewarded_enabled": return _snapshot.config.rewarded_enabled;
                case "interstitial_enabled": return _snapshot.config.interstitial_enabled;
                default: return fallback;
            }
        }

        public string GetString(string key, string fallback)
        {
            string value;
            switch (key)
            {
                case "share_copy_variant": value = _snapshot.config.share_copy_variant; break;
                case "store_offer_variant": value = _snapshot.config.store_offer_variant; break;
                default: return fallback;
            }
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        public bool RequiresMandatoryUpdate(string currentVersion) =>
            VersionPolicy.Compare(currentVersion, MinSupportedVersion) < 0;

        public bool ShouldRecommendUpdate(string currentVersion) =>
            VersionPolicy.Compare(currentVersion, RecommendedVersion) < 0;

        private BootstrapConfigDto LoadCache()
        {
            try
            {
                if (!File.Exists(_cachePath)) return null;
                return JsonUtility.FromJson<BootstrapConfigDto>(File.ReadAllText(_cachePath));
            }
            catch (Exception error)
            {
                Debug.LogWarning($"Bootstrap config cache ignored: {error.Message}");
                return null;
            }
        }

        private void SaveCache()
        {
            try
            {
                string temp = _cachePath + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(_snapshot));
                if (File.Exists(_cachePath)) File.Delete(_cachePath);
                File.Move(temp, _cachePath);
            }
            catch (Exception error)
            {
                Debug.LogWarning($"Bootstrap config cache save failed: {error.Message}");
            }
        }

        private static BootstrapConfigDto CreateDefaults() => new BootstrapConfigDto
        {
            serverTimeUtc = string.Empty,
            minSupportedVersion = "0.1.0",
            recommendedVersion = "0.1.0",
            config = new BootstrapConfigValuesDto()
        };

        private static void Normalize(BootstrapConfigDto snapshot)
        {
            if (snapshot.config == null) snapshot.config = new BootstrapConfigValuesDto();
            if (string.IsNullOrWhiteSpace(snapshot.minSupportedVersion)) snapshot.minSupportedVersion = "0.1.0";
            if (string.IsNullOrWhiteSpace(snapshot.recommendedVersion)) snapshot.recommendedVersion = snapshot.minSupportedVersion;
        }

        private static int Positive(int value, int fallback) => value > 0 ? value : fallback;
    }

    public static class VersionPolicy
    {
        public static int Compare(string left, string right)
        {
            int[] a = Parse(left);
            int[] b = Parse(right);
            int count = Math.Max(a.Length, b.Length);
            for (int i = 0; i < count; i++)
            {
                int av = i < a.Length ? a[i] : 0;
                int bv = i < b.Length ? b[i] : 0;
                if (av != bv) return av.CompareTo(bv);
            }
            return 0;
        }

        private static int[] Parse(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return new[] { 0 };
            string core = value.Trim().Split('-', '+')[0];
            string[] parts = core.Split('.');
            var result = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                result[i] = int.TryParse(parts[i], out int parsed) && parsed >= 0 ? parsed : 0;
            return result;
        }
    }
}
