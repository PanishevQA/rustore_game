using System;

namespace DontGetSidetracked.Services
{
    /// <summary>
    /// Presentation-owned runtime gate for platform SDKs that may launch external/system UI only
    /// while the game is still at a safe navigation point. The default is deny so a missing
    /// presentation coordinator cannot accidentally open SDK UI over active gameplay.
    /// </summary>
    public static class PlatformUiLaunchGate
    {
        private static Func<bool> _canLaunch;

        public static void Configure(Func<bool> canLaunch) => _canLaunch = canLaunch;

        public static bool CanLaunchNow()
        {
            Func<bool> gate = _canLaunch;
            if (gate == null) return false;

            try
            {
                return gate();
            }
            catch
            {
                return false;
            }
        }
    }
}
