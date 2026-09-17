using System;
using System.Threading.Tasks;
using DontGetSidetracked.Platform.RuStore;
using DontGetSidetracked.Services;
using UnityEngine;

namespace DontGetSidetracked.Network
{
    /// <summary>
    /// Compatibility facade kept for existing presentation/tests while the release runtime uses
    /// the single RuStoreRemoteConfigRuntime provider. The optional backend URL is deliberately ignored.
    /// </summary>
    public sealed class BootstrapRemoteConfigService : IRemoteConfigService
    {
        private readonly RuStoreRemoteConfigService _provider;

        public string MinSupportedVersion =>
            AppVersionPolicy.NormalizeOrFallback(_provider.MinSupportedVersion);

        public string RecommendedVersion =>
            AppVersionPolicy.NormalizeRange(MinSupportedVersion, _provider.RecommendedVersion).RecommendedVersion;

        public string ServerTimeUtc => string.Empty;

        public BootstrapRemoteConfigService(string optionalBackendBaseUrl)
        {
            _provider = RuStoreRemoteConfigRuntime.Service;
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
            AppVersionPolicy.Compare(currentVersion, MinSupportedVersion) < 0;

        public bool ShouldRecommendUpdate(string currentVersion) =>
            AppVersionPolicy.Compare(currentVersion, RecommendedVersion) < 0;

        public static int Compare(string left, string right) => AppVersionPolicy.Compare(left, right);
    }

    public static class VersionPolicy
    {
        public static int Compare(string left, string right) => AppVersionPolicy.Compare(left, right);
    }
}
