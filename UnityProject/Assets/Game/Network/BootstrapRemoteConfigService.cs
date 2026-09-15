using System;
using System.Threading.Tasks;
using DontGetSidetracked.Platform.RuStore;
using DontGetSidetracked.Services;
using UnityEngine;

namespace DontGetSidetracked.Network
{
    /// <summary>
    /// Compatibility facade kept so presentation code depends on a stable config surface.
    /// All runtime instances share one RuStore provider/snapshot. The offline-first release performs
    /// no requests to a developer-operated backend.
    /// </summary>
    public sealed class BootstrapRemoteConfigService : IRemoteConfigService
    {
        private const string DefaultVersion = "0.1.0";
        private static RuStoreRemoteConfigService _sharedProvider;
        private readonly RuStoreRemoteConfigService _provider;

        public string MinSupportedVersion =>
            string.IsNullOrWhiteSpace(_provider.MinSupportedVersion) ? DefaultVersion : _provider.MinSupportedVersion;

        public string RecommendedVersion =>
            string.IsNullOrWhiteSpace(_provider.RecommendedVersion) ? MinSupportedVersion : _provider.RecommendedVersion;

        public string ServerTimeUtc => string.Empty;

        public BootstrapRemoteConfigService(string optionalBackendBaseUrl)
        {
            // Deliberately ignore the optional backend URL for the release runtime.
            // A future online mode should use a separate provider instead of changing gameplay contracts.
            _provider = GetSharedProvider();
        }

        private static RuStoreRemoteConfigService GetSharedProvider()
        {
            if (_sharedProvider == null)
            {
                _sharedProvider = new RuStoreRemoteConfigService(
                    RuStoreRemoteConfigSettings.AppId,
                    account: string.Empty,
                    cacheFileName: "rustore-remote-config.json");
            }
            return _sharedProvider;
        }

        public async Task<bool> RefreshAsync()
        {
            try
            {
                return await _provider.RefreshAsync();
            }
            catch (Exception error)
            {
                Debug.LogWarning($"RuStore Remote Config refresh failed; using cache/defaults: {error.Message}");
                return false;
            }
        }

        public double GetDouble(string key, double fallback) => _provider.GetDouble(key, fallback);
        public int GetInt(string key, int fallback) => _provider.GetInt(key, fallback);
        public bool GetBool(string key, bool fallback) => _provider.GetBool(key, fallback);
        public string GetString(string key, string fallback) => _provider.GetString(key, fallback);

        public bool RequiresMandatoryUpdate(string currentVersion) =>
            Compare(currentVersion, MinSupportedVersion) < 0;

        public bool ShouldRecommendUpdate(string currentVersion) =>
            Compare(currentVersion, RecommendedVersion) < 0;

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

    public static class VersionPolicy
    {
        public static int Compare(string left, string right) => BootstrapRemoteConfigService.Compare(left, right);
    }
}
