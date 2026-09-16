using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using DontGetSidetracked.Core;
using DontGetSidetracked.Gameplay;
using DontGetSidetracked.Services;

namespace DontGetSidetracked.Social
{
    public sealed class OfflineGameApi : IGameApi
    {
        private sealed class ChallengeIdentity
        {
            public long Seed;
            public int GeneratorVersion;
            public int RouteCount;
            public int[] DisplayTimesMs;
        }

        private readonly ScoreCalculator _scorer = new ScoreCalculator();
        private readonly Dictionary<string, ChallengeIdentity> _knownChallenges =
            new Dictionary<string, ChallengeIdentity>(StringComparer.Ordinal);
        private readonly string _packageName;

        public OfflineGameApi(string packageName = null)
        {
            _packageName = packageName ?? string.Empty;
        }

        public Task<DailyDto> GetDailyAsync()
        {
            DailyDto dto = OfflineDaily.CreateDto(DateTime.UtcNow);
            Remember(dto.ChallengeId, dto.Seed, dto.GeneratorVersion, dto.RouteCount, dto.DisplayTimesMs);
            return Task.FromResult(dto);
        }

        public Task<double> SubmitDailyAttemptAsync(
            string playerId,
            DailyChallengeDefinition challenge,
            IReadOnlyList<IReadOnlyList<RecordedPoint>> replays,
            IReadOnlyList<double> clientScores,
            bool assisted)
        {
            if (challenge == null) throw new ArgumentNullException(nameof(challenge));
            int routeCount = challenge.RouteCount;
            if (replays == null || replays.Count != routeCount)
                throw new ArgumentException($"Challenge requires {routeCount} replay(s).", nameof(replays));

            Remember(challenge.ChallengeId, challenge.Seed, challenge.GeneratorVersion, routeCount, challenge.GetDisplayTimesMs());
            var verified = new List<double>(routeCount);
            for (int i = 0; i < routeCount; i++)
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

            ChallengeIdentity identity;
            if (!_knownChallenges.TryGetValue(challengeId, out identity))
            {
                int version = RouteGenerator.CurrentGeneratorVersion;
                int routeCount = RouteRuntimeTuning.DailyRouteCount;
                identity = new ChallengeIdentity
                {
                    Seed = OfflineDaily.SeedForDate(date, version),
                    GeneratorVersion = version,
                    RouteCount = routeCount,
                    DisplayTimesMs = CopyProfile(RouteRuntimeTuning.GetCurrentDisplayTimes(routeCount), routeCount)
                };
                _knownChallenges[challengeId] = identity;
            }

            string token = OfflineChallengeCodec.Encode(
                date,
                identity.Seed,
                identity.GeneratorVersion,
                identity.RouteCount,
                identity.DisplayTimesMs,
                score);
            string deepLink = OfflineChallengeCodec.BuildDeepLink(token);
            if (string.IsNullOrWhiteSpace(_packageName)) return Task.FromResult(deepLink);

            string installUrl = OfflineChallengeCodec.BuildInstallUrl(_packageName, token);
            return Task.FromResult(deepLink + "\n" + installUrl);
        }

        public Task<ReferralDto> GetReferralAsync(string referralId)
        {
            if (!OfflineChallengeCodec.TryDecode(referralId, out ReferralDto referral))
                throw new ArgumentException("Offline challenge token is invalid.", nameof(referralId));
            Remember(referral.ChallengeId, referral.Seed, referral.GeneratorVersion, referral.RouteCount, referral.DisplayTimesMs);
            return Task.FromResult(referral);
        }

        private void Remember(
            string challengeId,
            long seed,
            int generatorVersion,
            int routeCount,
            IReadOnlyList<int> displayTimesMs)
        {
            if (string.IsNullOrWhiteSpace(challengeId) || generatorVersion <= 0) return;
            int count = routeCount >= 1 && routeCount <= 3 ? routeCount : 3;
            _knownChallenges[challengeId] = new ChallengeIdentity
            {
                Seed = seed,
                GeneratorVersion = generatorVersion,
                RouteCount = count,
                DisplayTimesMs = CopyProfile(displayTimesMs, count)
            };
        }

        private static int[] CopyProfile(IReadOnlyList<int> source, int routeCount)
        {
            var result = new int[routeCount];
            for (int i = 0; i < routeCount; i++)
            {
                RouteDifficulty difficulty = (RouteDifficulty)i;
                int value = source != null && i < source.Count
                    ? source[i]
                    : RouteRuntimeTuning.GetDefaultDisplayTimeMs(difficulty);
                result[i] = RouteRuntimeTuning.NormalizeDisplayTimeMs(difficulty, value);
            }
            return result;
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
                routeCount = RouteRuntimeTuning.DailyRouteCount,
                displayTimeEasyMs = RouteRuntimeTuning.GetDisplayTimeMs(RouteDifficulty.Easy),
                displayTimeMediumMs = RouteRuntimeTuning.GetDisplayTimeMs(RouteDifficulty.Medium),
                displayTimeHardMs = RouteRuntimeTuning.GetDisplayTimeMs(RouteDifficulty.Hard),
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
        private const string LegacyV1Prefix = "L1";
        private const string LegacyV2Prefix = "L2";
        private const string LegacyV3Prefix = "L3";
        private const string Prefix = "L4";
        private const int LegacyV1TokenLength = 23;
        private const int LegacyV2TokenLength = 24;
        private const int MinLegacyV3TokenLength = 28;
        private const int ChecksumHexLength = 4;
        private const int MinL4TokenLength = MinLegacyV3TokenLength + ChecksumHexLength;
        private const uint ChecksumFnvOffset = 2166136261u;
        private const uint ChecksumFnvPrime = 16777619u;

        public static string Encode(DateTime date, long seed, int generatorVersion, double score) =>
            Encode(
                date,
                seed,
                generatorVersion,
                RouteRuntimeTuning.DailyRouteCount,
                RouteRuntimeTuning.GetCurrentDisplayTimes(RouteRuntimeTuning.DailyRouteCount),
                score);

        public static string Encode(DateTime date, long seed, int generatorVersion, int routeCount, double score) =>
            Encode(date, seed, generatorVersion, routeCount, RouteRuntimeTuning.GetCurrentDisplayTimes(routeCount), score);

        public static string Encode(
            DateTime date,
            long seed,
            int generatorVersion,
            int routeCount,
            IReadOnlyList<int> displayTimesMs,
            double score)
        {
            if (seed < 0 || seed > uint.MaxValue) throw new ArgumentOutOfRangeException(nameof(seed));
            if (generatorVersion <= 0 || generatorVersion > 255) throw new ArgumentOutOfRangeException(nameof(generatorVersion));
            if (routeCount < 1 || routeCount > 3) throw new ArgumentOutOfRangeException(nameof(routeCount));
            if (displayTimesMs == null || displayTimesMs.Count < routeCount)
                throw new ArgumentException("Display-time profile must contain one value per route.", nameof(displayTimesMs));

            int scoreTenths = Math.Max(0, Math.Min(1000, (int)Math.Round(score * 10.0, MidpointRounding.AwayFromZero)));
            string payload = Prefix +
                             date.ToString("yyyyMMdd", CultureInfo.InvariantCulture) +
                             ((uint)seed).ToString("X8", CultureInfo.InvariantCulture) +
                             generatorVersion.ToString("X2", CultureInfo.InvariantCulture) +
                             routeCount.ToString("X1", CultureInfo.InvariantCulture);

            for (int i = 0; i < routeCount; i++)
            {
                int displayTime = displayTimesMs[i];
                if (displayTime < RouteRuntimeTuning.MinDisplayTimeMs || displayTime > RouteRuntimeTuning.MaxDisplayTimeMs)
                    throw new ArgumentOutOfRangeException(nameof(displayTimesMs), "Display time is outside the safe range.");
                payload += displayTime.ToString("X4", CultureInfo.InvariantCulture);
            }

            payload += scoreTenths.ToString("X3", CultureInfo.InvariantCulture);
            return payload + CalculateChecksum(payload).ToString("X4", CultureInfo.InvariantCulture);
        }

        public static bool TryDecode(string token, out ReferralDto referral)
        {
            referral = null;
            if (string.IsNullOrWhiteSpace(token)) return false;
            string value = token.Trim().ToUpperInvariant();

            bool v1 = value.Length == LegacyV1TokenLength && value.StartsWith(LegacyV1Prefix, StringComparison.Ordinal);
            bool v2 = value.Length == LegacyV2TokenLength && value.StartsWith(LegacyV2Prefix, StringComparison.Ordinal);
            bool v3 = value.StartsWith(LegacyV3Prefix, StringComparison.Ordinal);
            bool v4 = value.StartsWith(Prefix, StringComparison.Ordinal);
            if (!v1 && !v2 && !v3 && !v4) return false;
            if (v3 && value.Length < MinLegacyV3TokenLength) return false;
            if (v4 && value.Length < MinL4TokenLength) return false;

            string payload = value;
            if (v4)
            {
                int checksumOffset = value.Length - ChecksumHexLength;
                if (checksumOffset <= 0) return false;
                if (!ushort.TryParse(
                        value.Substring(checksumOffset, ChecksumHexLength),
                        NumberStyles.HexNumber,
                        CultureInfo.InvariantCulture,
                        out ushort checksum)) return false;

                payload = value.Substring(0, checksumOffset);
                if (CalculateChecksum(payload) != checksum) return false;
            }

            if (!DateTime.TryParseExact(payload.Substring(2, 8), "yyyyMMdd", CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime date)) return false;
            if (!uint.TryParse(payload.Substring(10, 8), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint seed)) return false;
            if (!byte.TryParse(payload.Substring(18, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte version) || version == 0) return false;

            int routeCount;
            int scoreOffset;
            IReadOnlyList<int> displayTimes;

            if (v1)
            {
                routeCount = 3;
                scoreOffset = 20;
                displayTimes = RouteRuntimeTuning.GetDefaultDisplayTimes(routeCount);
            }
            else
            {
                if (!int.TryParse(payload.Substring(20, 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out routeCount) ||
                    routeCount < 1 || routeCount > 3) return false;

                if (v2)
                {
                    scoreOffset = 21;
                    displayTimes = RouteRuntimeTuning.GetDefaultDisplayTimes(routeCount);
                }
                else
                {
                    int expectedPayloadLength = 24 + routeCount * 4;
                    if (payload.Length != expectedPayloadLength) return false;
                    var parsedTimes = new int[routeCount];
                    int offset = 21;
                    for (int i = 0; i < routeCount; i++)
                    {
                        if (!int.TryParse(payload.Substring(offset + i * 4, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int displayTime) ||
                            displayTime < RouteRuntimeTuning.MinDisplayTimeMs ||
                            displayTime > RouteRuntimeTuning.MaxDisplayTimeMs)
                            return false;
                        parsedTimes[i] = displayTime;
                    }
                    displayTimes = parsedTimes;
                    scoreOffset = offset + routeCount * 4;
                }
            }

            if (scoreOffset < 0 || scoreOffset + 3 != payload.Length) return false;
            if (!int.TryParse(payload.Substring(scoreOffset, 3), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int scoreTenths) ||
                scoreTenths > 1000) return false;

            int easy = RouteRuntimeTuning.DefaultEasyDisplayTimeMs;
            int medium = RouteRuntimeTuning.DefaultMediumDisplayTimeMs;
            int hard = RouteRuntimeTuning.DefaultHardDisplayTimeMs;
            if (displayTimes.Count > 0) easy = displayTimes[0];
            if (displayTimes.Count > 1) medium = displayTimes[1];
            if (displayTimes.Count > 2) hard = displayTimes[2];

            referral = new ReferralDto
            {
                referralId = value,
                challengeId = OfflineDaily.ChallengeId(date),
                inviterId = "offline_friend",
                inviterScore = scoreTenths / 10.0,
                seed = seed,
                generatorVersion = version,
                routeCount = routeCount,
                displayTimeEasyMs = easy,
                displayTimeMediumMs = medium,
                displayTimeHardMs = hard,
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

        private static ushort CalculateChecksum(string payload)
        {
            uint hash = ChecksumFnvOffset;
            for (int i = 0; i < payload.Length; i++)
            {
                char c = char.ToUpperInvariant(payload[i]);
                hash ^= (byte)(c & 0xFF);
                hash *= ChecksumFnvPrime;
            }
            return (ushort)(((hash >> 16) ^ hash) & 0xFFFFu);
        }
    }
}
