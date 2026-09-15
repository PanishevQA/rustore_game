using System;
using System.Reflection;
using System.Threading.Tasks;
using DontGetSidetracked.Services;
using UnityEngine;

namespace DontGetSidetracked.Network
{
    /// <summary>
    /// Compatibility facade kept to avoid coupling Presentation to a concrete config provider.
    /// The offline-first release delegates to Platform/RuStore/RuStoreRemoteConfigService via reflection,
    /// so Network has no compile-time dependency on the platform assembly and performs no HTTP requests.
    /// </summary>
    public sealed class BootstrapRemoteConfigService : IRemoteConfigService
    {
        private const string DefaultVersion = "0.1.0";
        private readonly IRemoteConfigService _provider;
        private readonly object _providerObject;
        private readonly Type _providerType;

        public string MinSupportedVersion => ReadStringProperty("MinSupportedVersion", DefaultVersion);
        public string RecommendedVersion => ReadStringProperty("RecommendedVersion", MinSupportedVersion);
        public string ServerTimeUtc => string.Empty;

        public BootstrapRemoteConfigService(string optionalBackendBaseUrl)
        {
            // Deliberately ignore the optional backend URL for the release runtime.
            // Future online mode can introduce a separate provider without changing gameplay contracts.
            object providerObject = null;
            Type providerType = null;
            IRemoteConfigService provider = null;
            try
            {
                providerObject = CreateRuStoreProvider(out providerType);
                provider = providerObject as IRemoteConfigService;
            }
            catch (Exception error)
            {
                Debug.LogWarning($"RuStore Remote Config provider unavailable; using safe defaults: {error.Message}");
            }

            _providerObject = providerObject;
            _providerType = providerType;
            _provider = provider ?? new SafeRemoteConfig();
        }

        public async Task<bool> RefreshAsync()
        {
            if (_providerObject == null || _providerType == null) return false;
            try
            {
                MethodInfo method = _providerType.GetMethod("RefreshAsync", BindingFlags.Public | BindingFlags.Instance);
                if (method == null) return false;
                object result = method.Invoke(_providerObject, null);
                if (result is Task<bool> booleanTask) return await booleanTask;
                if (result is Task task)
                {
                    await task;
                    return true;
                }
                return false;
            }
            catch (Exception error)
            {
                Debug.LogWarning($"RuStore Remote Config refresh failed; using cache/defaults: {Unwrap(error).Message}");
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

        private static object CreateRuStoreProvider(out Type providerType)
        {
            providerType = FindType("DontGetSidetracked.Platform.RuStore.RuStoreRemoteConfigService") ??
                           throw new InvalidOperationException("RuStoreRemoteConfigService type not found.");
            Type settingsType = FindType("DontGetSidetracked.Platform.RuStore.RuStoreRemoteConfigSettings") ??
                                throw new InvalidOperationException("RuStoreRemoteConfigSettings type not found.");

            FieldInfo appIdField = settingsType.GetField("AppId", BindingFlags.Public | BindingFlags.Static);
            string appId = appIdField?.GetRawConstantValue()?.ToString() ?? string.Empty;

            ConstructorInfo constructor = providerType.GetConstructor(new[] { typeof(string), typeof(string), typeof(string) });
            if (constructor != null)
                return constructor.Invoke(new object[] { appId, string.Empty, "rustore-remote-config.json" });

            constructor = providerType.GetConstructor(new[] { typeof(string), typeof(string) });
            if (constructor != null)
                return constructor.Invoke(new object[] { appId, string.Empty });

            throw new MissingMethodException(providerType.FullName, ".ctor(string,string)");
        }

        private string ReadStringProperty(string name, string fallback)
        {
            if (_providerObject == null || _providerType == null) return fallback;
            try
            {
                PropertyInfo property = _providerType.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                string value = property?.GetValue(_providerObject)?.ToString();
                return string.IsNullOrWhiteSpace(value) ? fallback : value;
            }
            catch
            {
                return fallback;
            }
        }

        private static Type FindType(string fullName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type type = assemblies[i].GetType(fullName, false);
                if (type != null) return type;
            }
            return null;
        }

        private static Exception Unwrap(Exception error) =>
            error is TargetInvocationException invocation && invocation.InnerException != null
                ? invocation.InnerException
                : error;

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

    // Kept for existing tests and optional online-mode code that references VersionPolicy.
    public static class VersionPolicy
    {
        public static int Compare(string left, string right) => BootstrapRemoteConfigService.Compare(left, right);
    }
}
