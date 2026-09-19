using System;
using DontGetSidetracked.Core;
using DontGetSidetracked.Social;
using UnityEngine;

namespace DontGetSidetracked.Presentation
{
    /// <summary>
    /// Runtime boundary for referrals. The offline release only accepts self-contained challenge tokens
    /// that the local codec can decode. Generic referral IDs remain available to a future online build.
    /// </summary>
    internal static class ReferralRuntimePolicy
    {
        public static bool TryNormalizeForCurrentRuntime(string referralId, out string normalized)
        {
            normalized = null;
            if (!ReferralLinkParser.IsValidReferralId(referralId)) return false;

            string candidate = referralId.Trim().ToUpperInvariant();
            if (!GameRuntimeSettings.UsesDeveloperBackend &&
                !OfflineChallengeCodec.TryDecode(candidate, out _))
                return false;

            normalized = candidate;
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void SanitizePersistedPendingReferral()
        {
            var repository = new JsonFileSaveRepository();
            SaveData save = repository.Load();
            if (string.IsNullOrWhiteSpace(save.PendingReferralId)) return;

            if (TryNormalizeForCurrentRuntime(save.PendingReferralId, out string normalized))
            {
                if (!string.Equals(save.PendingReferralId, normalized, StringComparison.Ordinal))
                {
                    save.PendingReferralId = normalized;
                    repository.Save(save);
                }
                return;
            }

            // A permanently invalid/offline-incompatible token must not reopen and fail on every launch.
            save.PendingReferralId = string.Empty;
            repository.Save(save);
        }
    }
}
