using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using DontGetSidetracked.Core;
using DontGetSidetracked.Gameplay;
using DontGetSidetracked.Services;
using DontGetSidetracked.Social;
using NUnit.Framework;

namespace DontGetSidetracked.Tests
{
    public sealed class OfflineModeTests
    {
        [SetUp]
        public void SetUp() => RouteRuntimeTuning.ResetDefaults();

        [TearDown]
        public void TearDown() => RouteRuntimeTuning.ResetDefaults();

        [Test]
        public void SameUtcDate_ProducesSameDailySeedAndProfile()
        {
            RouteRuntimeTuning.ConfigureDisplayTimes(4200, 3600, 2900);
            RouteRuntimeTuning.ConfigureDailyRouteCount(2);
            var a = new DateTime(2026, 9, 15, 0, 1, 0, DateTimeKind.Utc);
            var b = new DateTime(2026, 9, 15, 23, 59, 59, DateTimeKind.Utc);

            DailyDto first = OfflineDaily.CreateDto(a);
            DailyDto second = OfflineDaily.CreateDto(b);

            Assert.That(first.ChallengeId, Is.EqualTo("daily_2026_09_15"));
            Assert.That(first.Seed, Is.EqualTo(970244546L));
            Assert.That(second.ChallengeId, Is.EqualTo(first.ChallengeId));
            Assert.That(second.Seed, Is.EqualTo(first.Seed));
            Assert.That(second.GeneratorVersion, Is.EqualTo(first.GeneratorVersion));
            Assert.That(second.RouteCount, Is.EqualTo(2));
            Assert.That(second.DisplayTimesMs[0], Is.EqualTo(4200));
            Assert.That(second.DisplayTimesMs[1], Is.EqualTo(3600));
        }

        [Test]
        public void DifferentUtcDates_ProduceDifferentDailySeed()
        {
            DailyDto first = OfflineDaily.CreateDto(new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc));
            DailyDto second = OfflineDaily.CreateDto(new DateTime(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc));

            Assert.That(second.ChallengeId, Is.Not.EqualTo(first.ChallengeId));
            Assert.That(second.Seed, Is.Not.EqualTo(first.Seed));
        }

        [Test]
        public void L4ChallengeToken_RoundTripsRouteCountDisplayTimesAndChecksum()
        {
            var date = new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc);
            long seed = OfflineDaily.SeedForDate(date, RouteGenerator.CurrentGeneratorVersion);
            string token = OfflineChallengeCodec.Encode(
                date,
                seed,
                RouteGenerator.CurrentGeneratorVersion,
                2,
                new[] { 4200, 3600 },
                94.7);

            Assert.That(token.Length, Is.EqualTo(36));
            Assert.That(token, Does.StartWith("L4"));
            Assert.That(OfflineChallengeCodec.TryDecode(token, out ReferralDto referral), Is.True);
            Assert.That(referral.ReferralId, Is.EqualTo(token));
            Assert.That(referral.ChallengeId, Is.EqualTo("daily_2026_09_15"));
            Assert.That(referral.Seed, Is.EqualTo(seed));
            Assert.That(referral.GeneratorVersion, Is.EqualTo(RouteGenerator.CurrentGeneratorVersion));
            Assert.That(referral.RouteCount, Is.EqualTo(2));
            Assert.That(referral.DisplayTimesMs[0], Is.EqualTo(4200));
            Assert.That(referral.DisplayTimesMs[1], Is.EqualTo(3600));
            Assert.That(referral.InviterScore, Is.EqualTo(94.7).Within(0.001));

            string deepLink = OfflineChallengeCodec.BuildDeepLink(token);
            Assert.That(OfflineChallengeCodec.TryExtractToken(deepLink, out string restored), Is.True);
            Assert.That(restored, Is.EqualTo(token));

            string install = OfflineChallengeCodec.BuildInstallUrl("ru.example.game", token);
            Assert.That(install, Does.Contain("rustore.ru/catalog/app/ru.example.game"));
            Assert.That(install, Does.Contain("referrerId=" + token));
        }

        [Test]
        public void L4ChallengeToken_RejectsSingleCharacterPayloadMutation()
        {
            var date = new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc);
            string token = OfflineChallengeCodec.Encode(
                date,
                0x01234567,
                RouteGenerator.CurrentGeneratorVersion,
                2,
                new[] { 4200, 3600 },
                94.7);

            char[] chars = token.ToCharArray();
            const int seedCharacterIndex = 10;
            chars[seedCharacterIndex] = chars[seedCharacterIndex] == 'A' ? 'B' : 'A';
            string corrupted = new string(chars);

            Assert.That(corrupted, Is.Not.EqualTo(token));
            Assert.That(OfflineChallengeCodec.TryDecode(corrupted, out _), Is.False);
        }

        [Test]
        public void LegacyL1L2AndL3Tokens_RemainCompatible()
        {
            var date = new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc);
            long seed = OfflineDaily.SeedForDate(date, RouteGenerator.CurrentGeneratorVersion);
            string common = date.ToString("yyyyMMdd", CultureInfo.InvariantCulture) +
                            ((uint)seed).ToString("X8", CultureInfo.InvariantCulture) +
                            RouteGenerator.CurrentGeneratorVersion.ToString("X2", CultureInfo.InvariantCulture);
            string score = ((int)Math.Round(88.8 * 10)).ToString("X3", CultureInfo.InvariantCulture);
            string l1 = "L1" + common + score;
            string l2 = "L2" + common + "2" + score;
            string l3 = BuildLegacyL3(date, seed, RouteGenerator.CurrentGeneratorVersion, 2, new[] { 4200, 3600 }, 88.8);

            Assert.That(OfflineChallengeCodec.TryDecode(l1, out ReferralDto oldV1), Is.True);
            Assert.That(oldV1.RouteCount, Is.EqualTo(3));
            Assert.That(oldV1.DisplayTimesMs[0], Is.EqualTo(RouteRuntimeTuning.DefaultEasyDisplayTimeMs));
            Assert.That(oldV1.DisplayTimesMs[1], Is.EqualTo(RouteRuntimeTuning.DefaultMediumDisplayTimeMs));
            Assert.That(oldV1.DisplayTimesMs[2], Is.EqualTo(RouteRuntimeTuning.DefaultHardDisplayTimeMs));

            Assert.That(OfflineChallengeCodec.TryDecode(l2, out ReferralDto oldV2), Is.True);
            Assert.That(oldV2.RouteCount, Is.EqualTo(2));
            Assert.That(oldV2.DisplayTimesMs[0], Is.EqualTo(RouteRuntimeTuning.DefaultEasyDisplayTimeMs));
            Assert.That(oldV2.DisplayTimesMs[1], Is.EqualTo(RouteRuntimeTuning.DefaultMediumDisplayTimeMs));

            Assert.That(OfflineChallengeCodec.TryDecode(l3, out ReferralDto oldV3), Is.True);
            Assert.That(oldV3.ReferralId, Is.EqualTo(l3));
            Assert.That(oldV3.RouteCount, Is.EqualTo(2));
            Assert.That(oldV3.DisplayTimesMs[0], Is.EqualTo(4200));
            Assert.That(oldV3.DisplayTimesMs[1], Is.EqualTo(3600));
            Assert.That(oldV3.InviterScore, Is.EqualTo(88.8).Within(0.001));
        }

        [Test]
        public async Task OfflineApi_WithPackageName_SharesL4DeepLinkAndInstallFallback()
        {
            RouteRuntimeTuning.ConfigureDailyRouteCount(2);
            RouteRuntimeTuning.ConfigureDisplayTimes(4300, 3700, 2800);
            var api = new OfflineGameApi("ru.example.game");
            DailyDto daily = await api.GetDailyAsync();
            Assert.That(daily.RouteCount, Is.EqualTo(2));

            string share = await api.CreateChallengeAsync("anon_test", daily.ChallengeId, 91.2);
            string[] lines = share.Split('\n');
            Assert.That(lines.Length, Is.EqualTo(2));
            Assert.That(lines[0], Does.StartWith("nesbeisya://challenge/L4"));
            Assert.That(lines[1], Does.StartWith("https://www.rustore.ru/catalog/app/ru.example.game?referrerId=L4"));

            Assert.That(OfflineChallengeCodec.TryExtractToken(lines[0], out string token), Is.True);
            Assert.That(OfflineChallengeCodec.TryDecode(token, out ReferralDto referral), Is.True);
            Assert.That(referral.RouteCount, Is.EqualTo(2));
            Assert.That(referral.DisplayTimesMs[0], Is.EqualTo(4300));
            Assert.That(referral.DisplayTimesMs[1], Is.EqualTo(3700));
        }

        [Test]
        public async Task ResharedLegacyL3Duel_PreservesIdentityAndUpgradesToL4()
        {
            var api = new OfflineGameApi();
            var date = new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc);
            const long originalSeed = 0x01234567;
            const int originalVersion = 7;
            const int originalRouteCount = 1;
            string incoming = BuildLegacyL3(
                date,
                originalSeed,
                originalVersion,
                originalRouteCount,
                new[] { 4800 },
                80.0);

            ReferralDto loaded = await api.GetReferralAsync(incoming);
            RouteRuntimeTuning.ConfigureDisplayTimes(1000, 1000, 1000); // Must not affect reshare identity.
            string resharedLink = await api.CreateChallengeAsync("anon_test", loaded.ChallengeId, 91.2);

            Assert.That(OfflineChallengeCodec.TryExtractToken(resharedLink, out string resharedToken), Is.True);
            Assert.That(resharedToken, Does.StartWith("L4"));
            Assert.That(OfflineChallengeCodec.TryDecode(resharedToken, out ReferralDto reshared), Is.True);
            Assert.That(reshared.Seed, Is.EqualTo(originalSeed));
            Assert.That(reshared.GeneratorVersion, Is.EqualTo(originalVersion));
            Assert.That(reshared.RouteCount, Is.EqualTo(originalRouteCount));
            Assert.That(reshared.DisplayTimesMs[0], Is.EqualTo(4800));
            Assert.That(reshared.InviterScore, Is.EqualTo(91.2).Within(0.001));
        }

        [Test]
        public async Task OfflineApi_RecalculatesConfiguredRouteCountInsteadOfTrustingClientScore()
        {
            RouteRuntimeTuning.ConfigureDailyRouteCount(2);
            var api = new OfflineGameApi();
            DailyDto dto = OfflineDaily.CreateDto(new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc));
            DailyChallengeDefinition challenge = new DailyChallengeFactory().Create(
                dto.ChallengeId,
                dto.Seed,
                dto.GeneratorVersion,
                dto.RouteCount,
                dto.DisplayTimesMs);
            var replays = new List<IReadOnlyList<RecordedPoint>>(challenge.RouteCount);

            for (int routeIndex = 0; routeIndex < challenge.RouteCount; routeIndex++)
            {
                RouteDefinition route = challenge.Routes[routeIndex];
                var replay = new List<RecordedPoint>(route.ReferencePoints.Count);
                for (int i = 0; i < route.ReferencePoints.Count; i++)
                    replay.Add(new RecordedPoint(route.ReferencePoints[i], i * 16L));
                replays.Add(replay);
            }

            double score = await api.SubmitDailyAttemptAsync(
                "anon_test",
                challenge,
                replays,
                new[] { 0.0, 0.0 },
                false);

            Assert.That(score, Is.EqualTo(100.0));
        }

        private static string BuildLegacyL3(
            DateTime date,
            long seed,
            int generatorVersion,
            int routeCount,
            IReadOnlyList<int> displayTimesMs,
            double score)
        {
            int scoreTenths = Math.Max(0, Math.Min(1000, (int)Math.Round(score * 10.0, MidpointRounding.AwayFromZero)));
            string token = "L3" +
                           date.ToString("yyyyMMdd", CultureInfo.InvariantCulture) +
                           ((uint)seed).ToString("X8", CultureInfo.InvariantCulture) +
                           generatorVersion.ToString("X2", CultureInfo.InvariantCulture) +
                           routeCount.ToString("X1", CultureInfo.InvariantCulture);
            for (int i = 0; i < routeCount; i++)
                token += displayTimesMs[i].ToString("X4", CultureInfo.InvariantCulture);
            return token + scoreTenths.ToString("X3", CultureInfo.InvariantCulture);
        }
    }
}
