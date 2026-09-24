using UnityEngine;
using UnityEngine.UI;

namespace DontGetSidetracked.Presentation
{
    /// <summary>
    /// Runtime access to the release skin baked by ReleaseSkinBaker.
    /// The player build ships ordinary imported PNG sprites/textures under Resources;
    /// no texture pixels are generated at runtime.
    /// </summary>
    internal static class ReleaseSkinAssets
    {
        private const string Root = "ReleaseSkin/";

        private static Texture2D _background;
        private static Sprite _glass;
        private static Sprite _gameplay;
        private static Sprite _route;
        private static Sprite _navViolet;
        private static Sprite _primary;
        private static Sprite _secondary;
        private static Sprite _scoreRing;

        public static Texture2D Background => _background != null
            ? _background
            : (_background = Resources.Load<Texture2D>(Root + "Backdrop"));

        public static Sprite GlassPanel => Load(ref _glass, "Glass");
        public static Sprite GameplayPanel => Load(ref _gameplay, "Gameplay");
        public static Sprite RoutePanel => Load(ref _route, "Route");
        public static Sprite NavViolet => Load(ref _navViolet, "NavViolet");
        public static Sprite PrimaryButton => Load(ref _primary, "Primary");
        public static Sprite SecondaryButton => Load(ref _secondary, "Secondary");
        public static Sprite ScoreRing => Load(ref _scoreRing, "ScoreRing");

        public static bool HasCoreSkin =>
            Background != null &&
            GlassPanel != null &&
            GameplayPanel != null &&
            PrimaryButton != null &&
            SecondaryButton != null;

        public static bool TryApply(Image image, Sprite sprite, Image.Type type, bool preserveAspect = false)
        {
            if (image == null || sprite == null) return false;
            image.sprite = sprite;
            image.type = type;
            image.color = Color.white;
            image.preserveAspect = preserveAspect;
            return true;
        }

        private static Sprite Load(ref Sprite cache, string name)
        {
            if (cache != null) return cache;
            cache = Resources.Load<Sprite>(Root + name);
            return cache;
        }
    }
}
