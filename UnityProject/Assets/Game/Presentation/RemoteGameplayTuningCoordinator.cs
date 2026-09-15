using DontGetSidetracked.Gameplay;
using DontGetSidetracked.Network;
using UnityEngine;

namespace DontGetSidetracked.Presentation
{
    /// <summary>
    /// Bridges validated Remote Config values into pure gameplay presentation tuning.
    /// No RuStore SDK type crosses into Gameplay.
    /// </summary>
    public sealed class RemoteGameplayTuningCoordinator : MonoBehaviour
    {
        private BootstrapRemoteConfigService _config;
        private float _nextPoll;
        private int _easy;
        private int _medium;
        private int _hard;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (FindFirstObjectByType<RemoteGameplayTuningCoordinator>() != null) return;
            var root = new GameObject("RemoteGameplayTuningCoordinator");
            DontDestroyOnLoad(root);
            root.AddComponent<RemoteGameplayTuningCoordinator>();
        }

        private void Awake()
        {
            _config = new BootstrapRemoteConfigService(GameRuntimeSettings.BackendBaseUrl);
            ApplyIfChanged(force: true);
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextPoll) return;
            _nextPoll = Time.unscaledTime + 0.5f;
            ApplyIfChanged(force: false);
        }

        private void ApplyIfChanged(bool force)
        {
            int easy = _config.GetInt("route_display_time_easy_ms", RouteRuntimeTuning.DefaultEasyDisplayTimeMs);
            int medium = _config.GetInt("route_display_time_medium_ms", RouteRuntimeTuning.DefaultMediumDisplayTimeMs);
            int hard = _config.GetInt("route_display_time_hard_ms", RouteRuntimeTuning.DefaultHardDisplayTimeMs);

            if (!force && easy == _easy && medium == _medium && hard == _hard) return;
            _easy = easy;
            _medium = medium;
            _hard = hard;
            RouteRuntimeTuning.ConfigureDisplayTimes(easy, medium, hard);
        }
    }
}
