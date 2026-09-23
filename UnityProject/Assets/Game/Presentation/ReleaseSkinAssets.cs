using UnityEngine;

namespace DontGetSidetracked.Presentation
{
    /// <summary>
    /// Central access point for committed release texture skin assets.
    /// All lookups are presentation-only and have safe fallbacks in ReleaseUiKit/Components.
    /// </summary>
    internal static class ReleaseSkinAssets
    {
        private const string Root = "ReleaseSkin/";

        private static Texture2D _background;
        private static Sprite _glassPanel;
        private static Sprite _gameplayPanel;
        private static Sprite _routePanel;
        private static Sprite _navViolet;
        private static Sprite _primaryButton;
        private static Sprite _secondaryButton;
        private static Sprite _scoreRing;

        public static Texture2D Background => _background ??= Resources.Load<Texture2D>(Root + "bg_release");
        public static Sprite GlassPanel => _glassPanel ??= Resources.Load<Sprite>(Root + "panel_glass");
        public static Sprite GameplayPanel => _gameplayPanel ??= Resources.Load<Sprite>(Root + "panel_gameplay");
        public static Sprite RoutePanel => _routePanel ??= Resources.Load<Sprite>(Root + "panel_route");
        public static Sprite NavViolet => _navViolet ??= Resources.Load<Sprite>(Root + "panel_nav_violet");
        public static Sprite PrimaryButton => _primaryButton ??= Resources.Load<Sprite>(Root + "button_primary");
        public static Sprite SecondaryButton => _secondaryButton ??= Resources.Load<Sprite>(Root + "button_secondary");
        public static Sprite ScoreRing => _scoreRing ??= Resources.Load<Sprite>(Root + "score_ring");

        public static bool HasCoreSkin =>
            Background != null &&
            GlassPanel != null &&
            PrimaryButton != null &&
            SecondaryButton != null;
    }
}
