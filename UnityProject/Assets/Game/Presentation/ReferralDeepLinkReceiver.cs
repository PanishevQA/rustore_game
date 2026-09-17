using System;
using DontGetSidetracked.Analytics;
using DontGetSidetracked.Core;
using DontGetSidetracked.Social;
using UnityEngine;

namespace DontGetSidetracked.Presentation
{
    public sealed class ReferralDeepLinkReceiver : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (FindFirstObjectByType<ReferralDeepLinkReceiver>() != null) return;
            var root = new GameObject("ReferralDeepLinkReceiver");
            DontDestroyOnLoad(root);
            root.AddComponent<ReferralDeepLinkReceiver>();
        }

        private void Awake()
        {
            Application.deepLinkActivated += OnDeepLinkActivated;
            if (!string.IsNullOrWhiteSpace(Application.absoluteURL))
                OnDeepLinkActivated(Application.absoluteURL);
        }

        private void OnDestroy()
        {
            Application.deepLinkActivated -= OnDeepLinkActivated;
        }

        private static void OnDeepLinkActivated(string url)
        {
            if (!ReferralLinkParser.TryParse(url, out string referralId)) return;
            if (!ReferralRuntimePolicy.TryNormalizeForCurrentRuntime(referralId, out string normalized)) return;

            var repository = new JsonFileSaveRepository();
            SaveData save = repository.Load();
            if (string.Equals(save.PendingReferralId, normalized, StringComparison.Ordinal)) return;

            save.PendingReferralId = normalized;
            repository.Save(save);

            AnalyticsLifecycle.Service?.Track(AnalyticsEventNames.ChallengeOpen,
                new System.Collections.Generic.Dictionary<string, object>
                {
                    ["referrer_id"] = normalized,
                    ["source"] = "deeplink"
                });
            if (AnalyticsLifecycle.Service != null) _ = AnalyticsLifecycle.Service.FlushAsync();
        }
    }
}
