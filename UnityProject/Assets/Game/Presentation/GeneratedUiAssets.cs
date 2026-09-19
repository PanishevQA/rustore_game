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
        public const string DailyIcon = "icon_daily";
        public const string CampaignIcon = "icon_campaign";
        public const string HintIcon = "icon_hint";
        public const string EyeIcon = "icon_eye";
        public const string ReplayIcon = "icon_replay";
        public const string ShareIcon = "icon_share";
        public const string ChallengeIcon = "icon_challenge";
        public const string AdIcon = "icon_ad";
        public const string SettingsIcon = "icon_settings";
        public const string CheckIcon = "icon_check";
        public const string LockIcon = "icon_lock";
        public const string NoAdsIcon = "item_no_ads";
        public const string CosmeticIcon = "item_cosmetic";
        public const string MedalBronze = "medal_bronze";
        public const string MedalSilver = "medal_silver";
        public const string MedalGold = "medal_gold";
        public const string StarFilled = "star_filled";
        public const string StarEmpty = "star_empty";

        public static Sprite Get(string assetName)
        {
            if (string.IsNullOrWhiteSpace(assetName)) return null;
            if (Cache.TryGetValue(assetName, out Sprite cached)) return cached;

            Sprite sprite = Resources.Load<Sprite>(Root + assetName);
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
