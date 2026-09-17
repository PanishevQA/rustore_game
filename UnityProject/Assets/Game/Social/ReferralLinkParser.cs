using System;

namespace DontGetSidetracked.Social
{
    public static class ReferralLinkParser
    {
        public static bool TryParse(string value, out string referralId)
        {
            referralId = null;
            if (string.IsNullOrWhiteSpace(value)) return false;

            string raw = value.Trim();
            if (raw.IndexOf("..", StringComparison.Ordinal) >= 0 || raw.IndexOf('\\') >= 0) return false;
            if (!Uri.TryCreate(raw, UriKind.Absolute, out Uri uri)) return false;

            string candidate = null;
            if (string.Equals(uri.Scheme, "nesbeisya", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(uri.Host, "challenge", StringComparison.OrdinalIgnoreCase))
            {
                candidate = uri.AbsolutePath.Trim('/');
            }
            else if ((string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)))
            {
                string[] segments = uri.AbsolutePath.Trim('/').Split('/');
                if (segments.Length == 2 && string.Equals(segments[0], "c", StringComparison.OrdinalIgnoreCase))
                    candidate = segments[1];
            }

            if (!IsValidReferralId(candidate)) return false;
            referralId = candidate.ToUpperInvariant();
            return true;
        }

        public static bool IsValidReferralId(string value)
        {
            // Current checksummed L4 tokens are 40 chars for a 3-route Daily. Keep some headroom
            // for generic/legacy referral IDs while reserving L1/L2/L3/L4 prefixes for our codec.
            if (string.IsNullOrWhiteSpace(value) || value.Length < 4 || value.Length > 48) return false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (!char.IsLetterOrDigit(c) && c != '-' && c != '_') return false;
            }

            string normalized = value.ToUpperInvariant();
            if (HasReservedChallengePrefix(normalized))
                return OfflineChallengeCodec.TryDecode(normalized, out _);

            return true;
        }

        private static bool HasReservedChallengePrefix(string value) =>
            value.StartsWith("L1", StringComparison.Ordinal) ||
            value.StartsWith("L2", StringComparison.Ordinal) ||
            value.StartsWith("L3", StringComparison.Ordinal) ||
            value.StartsWith("L4", StringComparison.Ordinal);
    }
}
