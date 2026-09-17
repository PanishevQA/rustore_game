using System;
using System.Globalization;

namespace DontGetSidetracked.Services
{
    public readonly struct AppVersionRange
    {
        public AppVersionRange(string minSupportedVersion, string recommendedVersion)
        {
            MinSupportedVersion = minSupportedVersion ?? string.Empty;
            RecommendedVersion = recommendedVersion ?? string.Empty;
        }

        public string MinSupportedVersion { get; }
        public string RecommendedVersion { get; }
    }

    /// <summary>
    /// Shared fail-safe policy for public application versions used by Remote Config and update gates.
    /// Numeric core components are compared; optional prerelease/build suffixes are ignored for ordering.
    /// Malformed remote values fall back instead of silently being interpreted as zero components.
    /// </summary>
    public static class AppVersionPolicy
    {
        public const string SafeDefaultVersion = "0.1.0";

        public static bool IsValid(string value) => TryNormalize(value, out _);

        public static bool TryNormalize(string value, out string normalized)
        {
            normalized = string.Empty;
            if (string.IsNullOrWhiteSpace(value)) return false;

            string trimmed = value.Trim();
            int suffixIndex = FindSuffixIndex(trimmed);
            string core = suffixIndex >= 0 ? trimmed.Substring(0, suffixIndex) : trimmed;
            if (string.IsNullOrWhiteSpace(core)) return false;

            string[] parts = core.Split('.');
            if (parts.Length == 0) return false;

            for (int i = 0; i < parts.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(parts[i])) return false;
                if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out int parsed) || parsed < 0)
                    return false;
            }

            if (suffixIndex >= 0)
            {
                if (suffixIndex == trimmed.Length - 1) return false;
                for (int i = suffixIndex + 1; i < trimmed.Length; i++)
                    if (char.IsWhiteSpace(trimmed[i])) return false;
            }

            normalized = trimmed;
            return true;
        }

        public static string NormalizeOrFallback(string value, string fallback = SafeDefaultVersion)
        {
            if (TryNormalize(value, out string normalized)) return normalized;
            if (TryNormalize(fallback, out string normalizedFallback)) return normalizedFallback;
            return SafeDefaultVersion;
        }

        public static AppVersionRange NormalizeRange(
            string minSupportedVersion,
            string recommendedVersion,
            string fallback = SafeDefaultVersion)
        {
            string min = NormalizeOrFallback(minSupportedVersion, fallback);
            string recommended = NormalizeOrFallback(recommendedVersion, min);
            if (Compare(recommended, min) < 0) recommended = min;
            return new AppVersionRange(min, recommended);
        }

        public static int Compare(string left, string right)
        {
            int[] a = ParseCore(NormalizeOrFallback(left));
            int[] b = ParseCore(NormalizeOrFallback(right));
            int count = Math.Max(a.Length, b.Length);
            for (int i = 0; i < count; i++)
            {
                int av = i < a.Length ? a[i] : 0;
                int bv = i < b.Length ? b[i] : 0;
                if (av != bv) return av.CompareTo(bv);
            }
            return 0;
        }

        private static int[] ParseCore(string value)
        {
            int suffixIndex = FindSuffixIndex(value);
            string core = suffixIndex >= 0 ? value.Substring(0, suffixIndex) : value;
            string[] parts = core.Split('.');
            var result = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                result[i] = int.Parse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture);
            return result;
        }

        private static int FindSuffixIndex(string value)
        {
            int prerelease = value.IndexOf('-');
            int build = value.IndexOf('+');
            if (prerelease < 0) return build;
            if (build < 0) return prerelease;
            return Math.Min(prerelease, build);
        }
    }
}
