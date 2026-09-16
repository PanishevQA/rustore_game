using UnityEngine;

namespace DontGetSidetracked.Platform.RuStore
{
    /// <summary>
    /// Owns the single process-wide Remote Config instance used by the offline-first runtime.
    /// The instance is reset at subsystem registration so Unity Play sessions without Domain Reload
    /// start from the persisted cache/default snapshot instead of stale static state.
    /// </summary>
    public static class RuStoreRemoteConfigRuntime
    {
        private static RuStoreRemoteConfigService _service;

        public static RuStoreRemoteConfigService Service
        {
            get
            {
                if (_service == null)
                {
                    // Account is an optional RuStore Remote Config targeting parameter.
                    _service = new RuStoreRemoteConfigService(
                        RuStoreRemoteConfigSettings.AppId,
                        account: string.Empty,
                        cacheFileName: "rustore-remote-config.json");
                }
                return _service;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            _service = null;
        }
    }
}
