using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DontGetSidetracked.Presentation
{
    /// <summary>
    /// Runtime access to the generated release UI sprite pack.
    /// Assets stay presentation-only and are loaded by Resources path so gameplay,
    /// persistence and platform integrations never depend on Unity object references.
    /// </summary>
    internal static class GeneratedUiAssets
    {
        private const string Root = "GeneratedUI/";
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public const string TrainingIcon = "icon_training";
        public const string StatisticsIcon = "icon_stats";
        public const string StoreIcon = "icon_store";
        public const string CoinIcon = "coin";
        public const string StartMarker = "marker_start_glow";
        public const string EndMarker = "marker_end_glow";

        public static Sprite Get(string assetName)
        {
            if (string.IsNullOrWhiteSpace(assetName)) return null;
            if (Cache.TryGetValue(assetName, out Sprite cached)) return cached;

            Texture2D texture = Resources.Load<Texture2D>(Root + assetName);
            if (texture == null)
            {
                Cache[assetName] = null;
                return null;
            }

            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect);
            sprite.name = "Generated_" + assetName;
            Cache[assetName] = sprite;
            return sprite;
        }

        public static bool TryApply(Image image, string assetName, bool preserveAspect = true)
        {
            if (image == null) return false;
            Sprite sprite = Get(assetName);
            if (sprite == null) return false;

            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.color = Color.white;
            image.preserveAspect = preserveAspect;
            image.raycastTarget = false;
            return true;
        }

        public static string QuickActionIcon(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return null;
            if (title == "ТРЕНИРОВКА") return TrainingIcon;
            if (title == "СТАТИСТИКА") return StatisticsIcon;
            if (title == "МАГАЗИН") return StoreIcon;
            return null;
        }
    }
}
