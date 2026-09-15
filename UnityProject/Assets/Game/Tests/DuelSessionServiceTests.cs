using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DontGetSidetracked.Core;
using DontGetSidetracked.Gameplay;
using DontGetSidetracked.Services;
using DontGetSidetracked.Social;
using NUnit.Framework;

namespace DontGetSidetracked.Tests
{
    public sealed class DuelSessionServiceTests
    {
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public async Task LoadAsync_RecreatesExactChallengeFromReferral(int routeCount)
        {
            var save = SaveData.CreateNew();
            save.PendingReferralId = "ABC123";
            var api = new FakeApi { RouteCount = routeCount };
            var service = new DuelSessionService(api, new MemorySaveRepository(save), save);

            RouteRuntimeTuning.ConfigureDisplayTimes(1000, 1000, 1000); // Recipient config must not override referral timing.
            DuelSession session = await service.LoadAsync("ABC123");

            Assert.That(session.Referral.InviterScore, Is.EqualTo(94.7));
            Assert.That(session.Challenge.ChallengeId, Is.EqualTo("daily_2026_09_15"));
            Assert.That(session.Challenge.Seed, Is.EqualTo(778899));
            Assert.That(session.Challenge.GeneratorVersion, Is.EqualTo(1));
            Assert.That(session.Challenge.RouteCount, Is.EqualTo(routeCount));
            Assert.That(session.Challenge.Routes[0].DisplayTimeMs, Is.EqualTo(api.EasyMs));
            if (routeCount > 1) Assert.That(session.Challenge.Routes[1].DisplayTimeMs, Is.EqualTo(api.MediumMs));
            if (routeCount > 2) Assert.That(session.Challenge.Routes[2].DisplayTimeMs, Is.EqualTo(api.HardMs));
            RouteRuntimeTuning.ResetDefaults();
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void CompleteAsync_PersistsLocalProgressBeforeProviderFailure_WithoutQueue(int routeCount)
        {
            var save = SaveData.CreateNew();
            save.PendingReferralId = "ABC123";
            save.Streak = 7;
            save.CompletedDailyCount = 4;
            var repo = new MemorySaveRepository(save);
            var api = new FakeApi { FailSubmit = true, RouteCount = routeCount };
            var service = new DuelSessionService(api, repo, save);
            DuelSession session = service.LoadAsync("ABC123").GetAwaiter().GetResult();
            List<IReadOnlyList<RecordedPoint>> replays = CreateReplays(session.Challenge);
            List<double> scores = CreateScores(routeCount);

            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await service.CompleteAsync(session, replays, scores));

            Assert.That(save.PendingAttempts, Is.Empty);
            Assert.That(save.PendingReferralId, Is.Empty);
            Assert.That(save.PersonalBest, Is.EqualTo(DailyChallengeFactory.DailyScore(scores)).Within(0.001));
            Assert.That(save.Streak, Is.EqualTo(7));
            Assert.That(save.CompletedDailyCount, Is.EqualTo(4));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public async Task CompleteAsync_WithAcceptedProvider_ReturnsComparisonResult(int routeCount)
        {
            var save = SaveData.CreateNew();
            save.PendingReferralId = "ABC123";
            var api = new FakeApi { RouteCount = routeCount };
            var service = new DuelSessionService(api, new MemorySaveRepository(save), save);
            DuelSession session = await service.LoadAsync("ABC123");
            List<IReadOnlyList<RecordedPoint>> replays = CreateReplays(session.Challenge);
            List<double> scores = CreateScores(routeCount, 96.0);

            DuelSubmissionResult result = await service.CompleteAsync(session, replays, scores);

            Assert.That(result.SubmittedToServer, Is.True);
            Assert.That(result.Score, Is.EqualTo(DailyChallengeFactory.DailyScore(scores)).Within(0.001));
            Assert.That(result.Won, Is.True);
        }

        private static List<IReadOnlyList<RecordedPoint>> CreateReplays(DailyChallengeDefinition challenge)
        {
            var result = new List<IReadOnlyList<RecordedPoint>>(challenge.RouteCount);
            for (int i = 0; i < challenge.RouteCount; i++)
            {
                IReadOnlyList<FixedPoint2> route = challenge.Routes[i].ReferencePoints;
                result.Add(new List<RecordedPoint>
                {
                    new RecordedPoint(route[0], 0),
                    new RecordedPoint(route[route.Count - 1], 800)
                });
            }
            return result;
        }

        private static List<double> CreateScores(int routeCount, double first = 95.0)
        {
            var scores = new List<double>(routeCount);
            for (int i = 0; i < routeCount; i++) scores.Add(first + i);
            return scores;
        }

        private sealed class MemorySaveRepository : ISaveRepository
        {
            private SaveData _data;
            public MemorySaveRepository(SaveData data) => _data = data;
            public SaveData Load() => _data;
            public void Save(SaveData data) => _data = data;
        }

        private sealed class FakeApi : IGameApi
        {
            public bool FailSubmit;
            public int RouteCount = 3;
            public int EasyMs = 4200;
            public int MediumMs = 3600;
            public int HardMs = 2900;

            public Task<DailyDto> GetDailyAsync() => throw new NotSupportedException();

            public Task<double> SubmitDailyAttemptAsync(
                string playerId,
                DailyChallengeDefinition challenge,
                IReadOnlyList<IReadOnlyList<RecordedPoint>> replays,
                IReadOnlyList<double> clientScores,
                bool assisted)
            {
                if (FailSubmit) throw new InvalidOperationException("offline");
                return Task.FromResult(DailyChallengeFactory.DailyScore(clientScores));
            }

            public Task<string> CreateChallengeAsync(string playerId, string challengeId, double score) =>
                Task.FromResult("nesbeisya://challenge/ABC123");

            public Task<ReferralDto> GetReferralAsync(string referralId) => Task.FromResult(new ReferralDto
            {
                referralId = referralId,
                challengeId = "daily_2026_09_15",
                inviterId = "anon_friend",
                inviterScore = 94.7,
                seed = 778899,
                generatorVersion = 1,
                routeCount = RouteCount,
                displayTimeEasyMs = EasyMs,
                displayTimeMediumMs = MediumMs,
                displayTimeHardMs = HardMs,
                serverTimeUtc = "2026-09-15T06:00:00Z"
            });
        }
    }
}
