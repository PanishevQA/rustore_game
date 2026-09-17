using DontGetSidetracked.Services;
using UnityEngine;

namespace DontGetSidetracked.Presentation
{
    /// <summary>
    /// Supplies the presentation-side safety predicate used by platform SDK adapters immediately
    /// before they launch external/system UI. No gameplay or platform service depends directly on
    /// concrete presentation types.
    /// </summary>
    internal static class PlatformUiLaunchGateRuntime
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetGate() => PlatformUiLaunchGate.Configure(null);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ConfigureGate() => PlatformUiLaunchGate.Configure(CanLaunchPlatformUi);

        private static bool CanLaunchPlatformUi()
        {
            if (!Application.isFocused) return false;

            GameBootstrap bootstrap = Object.FindFirstObjectByType<GameBootstrap>();
            if (bootstrap == null || !GameBootstrapRuntimeBridge.IsHome(bootstrap) ||
                GameBootstrapRuntimeBridge.IsActiveRound(bootstrap))
                return false;

            MetaMenuOverlay meta = Object.FindFirstObjectByType<MetaMenuOverlay>();
            if (meta != null && meta.IsPanelOpen) return false;

            TrainingMenuCoordinator training = Object.FindFirstObjectByType<TrainingMenuCoordinator>();
            if (training != null && training.IsOpen) return false;

            CampaignLevelMenuOverlay campaign = Object.FindFirstObjectByType<CampaignLevelMenuOverlay>();
            if (campaign != null && campaign.IsOpen) return false;

            RuntimePlatformCoordinator platform = Object.FindFirstObjectByType<RuntimePlatformCoordinator>();
            if (platform != null && platform.IsNotificationPromptOpen) return false;

            GameObject mandatoryUpdate = GameObject.Find("MandatoryUpdateCanvas");
            if (mandatoryUpdate != null && mandatoryUpdate.activeInHierarchy) return false;

            return true;
        }
    }
}
