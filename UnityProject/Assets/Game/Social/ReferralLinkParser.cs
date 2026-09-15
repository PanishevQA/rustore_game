using System;

namespace DontGetSidetracked.Social
{
    public static class ReferralLinkParser
    {
        public static bool TryParse(string value, out string referralId)
        {
            referralId = null;
            if (string.IsNullOrWhiteSpace(value)) return false;
            if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out Uri uri)) return false;

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
            if (string.IsNullOrWhiteSpace(value) || value.Length < 4 || value.Length > 32) return false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (!char.IsLetterOrDigit(c) && c != '-' && c != '_') return false;
            }
            return true;
        }
    }
}
