using System.Collections.Generic;

namespace DontGetSidetracked.Analytics
{
    public static class AnalyticsEventNames
    {
        public const string AppOpen = "app_open";
        public const string SessionStart = "session_start";
        public const string TutorialStart = "tutorial_start";
        public const string TutorialComplete = "tutorial_complete";
        public const string DailyStart = "daily_start";
        public const string DailyComplete = "daily_complete";
        public const string RoundStart = "round_start";
        public const string RoundComplete = "round_complete";
        public const string RoundFailed = "round_failed";
        public const string ScoreGenerated = "score_generated";
        public const string PersonalBest = "personal_best";
        public const string ShareClick = "share_click";
        public const string ShareComplete = "share_complete";
        public const string ChallengeOpen = "challenge_open";
        public const string ChallengeComplete = "challenge_complete";
        public const string HintUsed = "hint_used";
        public const string CosmeticSelect = "cosmetic_select";
        public const string SettingsOpen = "settings_open";
        public const string SettingsChange = "settings_change";
        public const string RewardedOffer = "rewarded_offer";
        public const string RewardedStart = "rewarded_start";
        public const string RewardedComplete = "rewarded_complete";
        public const string InterstitialShow = "interstitial_show";
        public const string StoreOpen = "store_open";
        public const string PurchaseStart = "purchase_start";
        public const string PurchaseSuccess = "purchase_success";
        public const string PurchaseCancel = "purchase_cancel";
        public const string PurchaseError = "purchase_error";
        public const string ReviewFlowRequest = "review_flow_request";
        public const string PushPermissionRequest = "push_permission_request";
        public const string PushPermissionResult = "push_permission_result";

        private static readonly HashSet<string> Known = new HashSet<string>
        {
            AppOpen, SessionStart, TutorialStart, TutorialComplete, DailyStart, DailyComplete,
            RoundStart, RoundComplete, RoundFailed, ScoreGenerated, PersonalBest, ShareClick,
            ShareComplete, ChallengeOpen, ChallengeComplete, HintUsed, CosmeticSelect,
            SettingsOpen, SettingsChange, RewardedOffer, RewardedStart, RewardedComplete,
            InterstitialShow, StoreOpen, PurchaseStart, PurchaseSuccess, PurchaseCancel,
            PurchaseError, ReviewFlowRequest, PushPermissionRequest, PushPermissionResult
        };

        public static bool IsKnown(string eventName) => eventName != null && Known.Contains(eventName);
    }
}
