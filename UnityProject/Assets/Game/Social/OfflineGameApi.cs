using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using DontGetSidetracked.Gameplay;
using DontGetSidetracked.Services;

namespace DontGetSidetracked.Social
{
    /// <summary>
    /// Fully local replacement for the MVP backend. Daily content is derived from UTC date + generatorVersion,
    /// replay scores are recalculated locally, and friend challenges carry all data required to restore a duel.
    /// </summary>
    public sealed class OfflineGameApi : IGameApi
    {
        private readonly ScoreCalculator _scorer = new ScoreCalculator();
        private readonly string _packageName;

        public OfflineGameApi(string packageName = null)
        {
            _packageName = packageName ?? string.Empty;
        }

        public Task<DailyDto> GetDailyAsync()
        {
            DateTime now = DateTime.UtcNow;
            return Task.FromResult(OfflineDaily.CreateDto(now));
        }

        public Task<double> SubmitDailyAttemptAsync(
            string playerId,
            DailyChallengeDefinition challenge,
            IReadOnlyList<IReadOnlyList<RecordedPoint>> replays,
            IReadOnlyList<double> clientScores,
            bool assisted)
        {
            if (challenge == null) throw new ArgumentNullException(nameof(challenge));
            if (replays == null || replays.Count != 3) throw new ArgumentException("Daily requires three replays.", nameof(replays));

            var verified = new List<double>(3);
            for (int i = 0; i < 3; i++)
            {
                if (replays[i] == null || replays[i].Count == 0)
                    throw new ArgumentException("Replay cannot be empty.", nameof(replays));
                verified.Add(_scorer.Calculate(challenge.Routes[i], replays[i]).Score);
            }

            return Task.FromResult(DailyChallengeFactory.DailyScore(verified));
        }

        public Task<string> CreateChallengeAsync(string playerId, string challengeId, double score)
        {
            if (!OfflineDaily.TryParseChallengeDate(challengeId, out DateTime date))
                throw new ArgumentException("Only deterministic Daily challenges can be shared offline.", nameof(challengeId));

            int version = RouteGenerator.CurrentGeneratorVersion;
            long seed = OfflineDaily.SeedForDate(date, version);
            string token = OfflineChallengeCodec.Encode(date, seed, version, score);
            string deepLink = OfflineChallengeCodec.BuildDeepLink(token);
            if (string.IsNullOrWhiteSpace(_packageName)) return Task.FromResult(deepLink);

            // Return both destinations as one share fragment. Installed users can open the deeplink;
            // new users can install from RuStore and the same token is recovered via Install Referrer.
            string installUrl = OfflineChallengeCodec.BuildInstallUrl(_packageName, token);
            return Task.FromResult(deepLink + "\n" + installUrl);
        }

        public Task<ReferralDto> GetReferralAsync(string referralId)
        {
            if (!OfflineChallengeCodec.TryDecode(referralId, out ReferralDto referral))
                throw new ArgumentException("Offline challenge token is invalid.", nameof(referralId));
            return Task.FromResult(referral);
        }
    }

    public static class OfflineDaily
    {
        private const uint FnvOffset = 2166136261u;
        private const uint FnvPrime = 16777619u;

        public static DailyDto CreateDto(DateTime utcNow)
        {
            DateTime utc = utcNow.Kind == DateTimeKind.Utc ? utcNow : utcNow.ToUniversalTime();
            DateTime date = utc.Date;
            int version = RouteGenerator.CurrentGeneratorVersion;
            return new DailyDto
            {
                challengeId = ChallengeId(date),
                seed = SeedForDate(date, version),
                generatorVersion = version,
                serverTimeUtc = utc.ToString("O", CultureInfo.InvariantCulture)
            };
        }

        public static string ChallengeId(DateTime date) => date.ToString("'daily_'yyyy_MM_dd", CultureInfo.InvariantCulture);

        public static long SeedForDate(DateTime date, int generatorVersion)
        {
            string material = "nesbeisya|" + date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "|v" + generatorVersion;
            uint hash = FnvOffset;
            for (int i = 0; i < material.Length; i++)
            {
                char c = material[i];
                hash ^= (byte)(c & 0xFF);
                hash *= FnvPrime;
            }
            return hash & 0x7FFFFFFF;
        }

        public static bool TryParseChallengeDate(string challengeId, out DateTime date) =>
            DateTime.TryParseExact(
                challengeId,
                "'daily_'yyyy_MM_dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out date);
    }

    public static class OfflineChallengeCodec
    {
        private const string Prefix = "L1";
        private const int TokenLength = 23; // L1 + yyyyMMdd + seed(8 hex) + version(2 hex) + score*10(3 hex)

        public static string Encode(DateTime date, long seed, int generatorVersion, double score)
        {
            if (seed < 0 || seed > uint.MaxValue) throw new ArgumentOutOfRangeException(nameof(seed));
            if (generatorVersion <= 0 || generatorVersion > 255) throw new ArgumentOutOfRangeException(nameof(generatorVersion));
            int scoreTenths = Math.Max(0, Math.Min(1000, (int)Math.Round(score * 10.0, MidpointRounding.AwayFromZero)));
            return Prefix +
                   date.ToString("yyyyMMdd", CultureInfo.InvariantCulture) +
                   ((uint)seed).ToString("X8", CultureInfo.InvariantCulture) +
                   generatorVersion.ToString("X2", CultureInfo.InvariantCulture) +
                   scoreTenths.ToString("X3", CultureInfo.InvariantCulture);
        }

        public static bool TryDecode(string token, out ReferralDto referral)
        {
            referral = null;
            if (string.IsNullOrWhiteSpace(token)) return false;
            string value = token.Trim().ToUpperInvariant();
            if (value.Length != TokenLength || !value.StartsWith(Prefix, StringComparison.Ordinal)) return false;

            if (!DateTime.TryParseExact(value.Substring(2, 8), "yyyyMMdd", CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime date)) return false;
            if (!uint.TryParse(value.Substring(10, 8), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint seed)) return false;
            if (!byte.TryParse(value.Substring(18, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte version) || version == 0) return false;
            if (!int.TryParse(value.Substring(20, 3), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int scoreTenths) || scoreTenths > 1000) return false;

            referral = new ReferralDto
            {
                referralId = value,
                challengeId = OfflineDaily.ChallengeId(date),
                inviterId = "offline_friend",
                inviterScore = scoreTenths / 10.0,
                seed = seed,
                generatorVersion = version,
                serverTimeUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
            };
            return true;
        }

        public static string BuildDeepLink(string token)
        {
            if (!TryDecode(token, out _)) throw new ArgumentException("Invalid offline challenge token.", nameof(token));
            return "nesbeisya://challenge/" + token.ToUpperInvariant();
        }

        public static string BuildInstallUrl(string packageName, string token)
        {
            if (string.IsNullOrWhiteSpace(packageName)) throw new ArgumentException("Package name is required.", nameof(packageName));
            if (!TryDecode(token, out _)) throw new ArgumentException("Invalid offline challenge token.", nameof(token));
            return "https://www.rustore.ru/catalog/app/" + Uri.EscapeDataString(packageName) + "?referrerId=" + Uri.EscapeDataString(token.ToUpperInvariant());
        }

        public static bool TryExtractToken(string value, out string token)
        {
            token = null;
            if (string.IsNullOrWhiteSpace(value)) return false;
            string raw = value.Trim();
            if (TryDecode(raw, out ReferralDto direct))
            {
                token = direct.ReferralId;
                return true;
            }

            if (!Uri.TryCreate(raw, UriKind.Absolute, out Uri uri)) return false;
            if (!string.Equals(uri.Scheme, "nesbeisya", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(uri.Host, "challenge", StringComparison.OrdinalIgnoreCase)) return false;
            string candidate = uri.AbsolutePath.Trim('/');
            if (!TryDecode(candidate, out ReferralDto decoded)) return false;
            token = decoded.ReferralId;
            return true;
        }
    }
}
