using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DontGetSidetracked.Gameplay;
using DontGetSidetracked.Services;
using DontGetSidetracked.Social;
using NUnit.Framework;

namespace DontGetSidetracked.Tests
{
    public sealed class OfflineModeTests
    {
        [Test]
        public void SameUtcDate_ProducesSameDailySeed()
        {
            var a = new DateTime(2026, 9, 15, 0, 1, 0, DateTimeKind.Utc);
            var b = new DateTime(2026, 9, 15, 23, 59, 59, DateTimeKind.Utc);

            DailyDto first = OfflineDaily.CreateDto(a);
            DailyDto second = OfflineDaily.CreateDto(b);

            Assert.That(first.ChallengeId, Is.EqualTo("daily_2026_09_15"));
            Assert.That(first.Seed, Is.EqualTo(970244546L)); // Golden seed for generatorVersion 1.
            Assert.That(second.ChallengeId, Is.EqualTo(first.ChallengeId));
            Assert.That(second.Seed, Is.EqualTo(first.Seed));
            Assert.That(second.GeneratorVersion, Is.EqualTo(first.GeneratorVersion));
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
        public void ChallengeToken_RoundTripsWithoutBackend()
        {
            var date = new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc);
            long seed = OfflineDaily.SeedForDate(date, RouteGenerator.CurrentGeneratorVersion);
            string token = OfflineChallengeCodec.Encode(date, seed, RouteGenerator.CurrentGeneratorVersion, 94.7);

            Assert.That(token.Length, Is.EqualTo(23));
            Assert.That(OfflineChallengeCodec.TryDecode(token, out ReferralDto referral), Is.True);
            Assert.That(referral.ChallengeId, Is.EqualTo("daily_2026_09_15"));
            Assert.That(referral.Seed, Is.EqualTo(seed));
            Assert.That(referral.GeneratorVersion, Is.EqualTo(RouteGenerator.CurrentGeneratorVersion));
            Assert.That(referral.InviterScore, Is.EqualTo(94.7).Within(0.001));

            string deepLink = OfflineChallengeCodec.BuildDeepLink(token);
            Assert.That(OfflineChallengeCodec.TryExtractToken(deepLink, out string restored), Is.True);
            Assert.That(restored, Is.EqualTo(token));

            string install = OfflineChallengeCodec.BuildInstallUrl("ru.example.game", token);
            Assert.That(install, Does.Contain("rustore.ru/catalog/app/ru.example.game"));
            Assert.That(install, Does.Contain("referrerId=" + token));
        }

        [Test]
        public async Task OfflineApi_WithPackageName_SharesDeepLinkAndInstallFallback()
        {
            var api = new OfflineGameApi("ru.example.game");
            string share = await api.CreateChallengeAsync("anon_test", "daily_2026_09_15", 91.2);

            string[] lines = share.Split('\n');
            Assert.That(lines.Length, Is.EqualTo(2));
            Assert.That(lines[0], Does.StartWith("nesbeisya://challenge/L1"));
            Assert.That(lines[1], Does.StartWith("https://www.rustore.ru/catalog/app/ru.example.game?referrerId=L1"));
        }

        [Test]
        public async Task OfflineApi_RecalculatesReplayInsteadOfTrustingClientScore()
        {
            var api = new OfflineGameApi();
            DailyDto dto = OfflineDaily.CreateDto(new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc));
            DailyChallengeDefinition challenge = new DailyChallengeFactory().Create(dto.ChallengeId, dto.Seed, dto.GeneratorVersion);
            var replays = new List<IReadOnlyList<RecordedPoint>>(3);

            for (int routeIndex = 0; routeIndex < 3; routeIndex++)
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
                new[] { 0.0, 0.0, 0.0 },
                false);

            Assert.That(score, Is.EqualTo(100.0));
        }
    }
}
