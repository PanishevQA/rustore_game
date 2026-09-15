using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DontGetSidetracked.Core;
using DontGetSidetracked.Daily;
using DontGetSidetracked.Gameplay;
using DontGetSidetracked.Services;
using NUnit.Framework;

namespace DontGetSidetracked.Tests
{
    public sealed class DailySessionServiceTests
    {
        [SetUp]
        public void SetUp() => ChallengeAssistanceTracker.ResetAllForTests();

        [TearDown]
        public void TearDown() => ChallengeAssistanceTracker.ResetAllForTests();

        [Test]
        public async Task LoadCurrentAsync_UsesCachedDailyRouteCountAndTiming_WhenProviderFails()
        {
            var save = SaveData.CreateNew();
            save.LastDaily = new DailyCacheData
            {
                ChallengeId = "daily_2026_09_15",
                Seed = 123456,
                GeneratorVersion = 1,
                RouteCount = 2,
                DisplayTimeEasyMs = 4800,
                DisplayTimeMediumMs = 3900,
                DisplayTimeHardMs = 2800,
                ServerTimeUtc = "2026-09-15T06:30:00Z"
            };
            var repo = new MemorySaveRepository(save);
            var api = new FakeGameApi { FailGetDaily = true };
            var service = new DailySessionService(api, repo, save);

            DailyLoadResult result = await service.LoadCurrentAsync();

            Assert.That(result.FromCache, Is.True);
            Assert.That(result.Challenge.ChallengeId, Is.EqualTo("daily_2026_09_15"));
            Assert.That(result.Challenge.RouteCount, Is.EqualTo(2));
            Assert.That(result.Challenge.Routes[0].DisplayTimeMs, Is.EqualTo(4800));
            Assert.That(result.Challenge.Routes[1].DisplayTimeMs, Is.EqualTo(3900));
            Assert.That(result.ServerTimeUtc, Is.EqualTo(new DateTime(2026, 9, 15, 6, 30, 0, DateTimeKind.Utc)));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public async Task LoadCurrentAsync_PreservesProviderRouteCountAndTiming(int routeCount)
        {
            var save = SaveData.CreateNew();
            var api = new FakeGameApi { RouteCount = routeCount };
            var service = new DailySessionService(api, new MemorySaveRepository(save), save);

            DailyLoadResult result = await service.LoadCurrentAsync();

            Assert.That(result.Challenge.RouteCount, Is.EqualTo(routeCount));
            Assert.That(result.Challenge.Routes[0].DisplayTimeMs, Is.EqualTo(api.EasyMs));
            if (routeCount > 1) Assert.That(result.Challenge.Routes[1].DisplayTimeMs, Is.EqualTo(api.MediumMs));
            if (routeCount > 2) Assert.That(result.Challenge.Routes[2].DisplayTimeMs, Is.EqualTo(api.HardMs));
            Assert.That(save.LastDaily.RouteCount, Is.EqualTo(routeCount));
            Assert.That(save.LastDaily.DisplayTimeEasyMs, Is.EqualTo(api.EasyMs));
            Assert.That(save.LastDaily.DisplayTimeMediumMs, Is.EqualTo(routeCount > 1 ? api.MediumMs : DailyCacheData.DefaultMediumDisplayTimeMs));
            Assert.That(save.LastDaily.DisplayTimeHardMs, Is.EqualTo(routeCount > 2 ? api.HardMs : DailyCacheData.DefaultHardDisplayTimeMs));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void CompleteAndSubmitAsync_PersistsLocalProgressForConfiguredRouteCount_BeforeProviderFailure(int routeCount)
        {
            var save = SaveData.CreateNew();
            save.Streak = 2;
            save.LastCompletedDailyDateUtc = "2026-09-14T00:00:00.0000000Z";
            var repo = new MemorySaveRepository(save);
            var api = new FakeGameApi { FailSubmit = true };
            var service = new DailySessionService(api, repo, save);
            var challenge = new DailyChallengeFactory().Create("daily_2026_09_15", 77, 1, routeCount);
            var session = new DailyLoadResult(challenge, new DateTime(2026, 9, 15, 23, 59, 0, DateTimeKind.Utc), false);
            List<IReadOnlyList<RecordedPoint>> replays = CreateReplays(challenge);
            List<double> scores = CreateScores(routeCount);

            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await service.CompleteAndSubmitAsync(session, replays, scores, false));

            Assert.That(save.PendingAttempts, Is.Empty);
            Assert.That(save.Streak, Is.EqualTo(3));
            Assert.That(save.CompletedDailyCount, Is.EqualTo(1));
            Assert.That(save.LastCompletedDailyDateUtc.StartsWith("2026-09-15"), Is.True);
            Assert.That(save.PersonalBest, Is.EqualTo(DailyChallengeFactory.DailyScore(scores)).Within(0.001));
        }

        [Test]
        public async Task CompleteAndSubmitAsync_PropagatesHintAssistanceAndClearsTracker()
        {
            var save = SaveData.CreateNew();
            var api = new FakeGameApi();
            var service = new DailySessionService(api, new MemorySaveRepository(save), save);
            var challenge = new DailyChallengeFactory().Create("daily_2026_09_15", 77, 1, 1);
            var session = new DailyLoadResult(challenge, new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc), false);
            List<IReadOnlyList<RecordedPoint>> replays = CreateReplays(challenge);
            List<double> scores = CreateScores(1);

            ChallengeAssistanceTracker.MarkAssisted(challenge.ChallengeId);
            Assert.That(ChallengeAssistanceTracker.IsAssisted(challenge.ChallengeId), Is.True);

            await service.CompleteAndSubmitAsync(session, replays, scores, false);

            Assert.That(api.LastAssisted, Is.True);
            Assert.That(ChallengeAssistanceTracker.IsAssisted(challenge.ChallengeId), Is.False);
        }

        [Test]
        public async Task LoadCurrentAsync_ClearsStaleAssistanceForNewAttempt()
        {
            ChallengeAssistanceTracker.MarkAssisted("daily_2026_09_15");
            var save = SaveData.CreateNew();
            var api = new FakeGameApi();
            var service = new DailySessionService(api, new MemorySaveRepository(save), save);

            DailyLoadResult loaded = await service.LoadCurrentAsync();

            Assert.That(loaded.Challenge.ChallengeId, Is.EqualTo("daily_2026_09_15"));
            Assert.That(ChallengeAssistanceTracker.IsAssisted(loaded.Challenge.ChallengeId), Is.False);
        }

        [Test]
        public async Task FlushPendingAsync_OnlyRemovesLegacyQueue()
        {
            var save = SaveData.CreateNew();
            save.PendingAttempts.Add(new PendingDailyAttemptData { ChallengeId = "legacy" });
            var api = new FakeGameApi();
            var service = new DailySessionService(api, new MemorySaveRepository(save), save);

            int removed = await service.FlushPendingAsync();

            Assert.That(removed, Is.EqualTo(1));
            Assert.That(save.PendingAttempts, Is.Empty);
            Assert.That(api.SubmitCount, Is.EqualTo(0));
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

        private static List<double> CreateScores(int routeCount)
        {
            var scores = new List<double>(routeCount);
            for (int i = 0; i < routeCount; i++) scores.Add(90.1 + i * 1.1);
            return scores;
        }

        private sealed class MemorySaveRepository : ISaveRepository
        {
            private SaveData _data;
            public MemorySaveRepository(SaveData data) => _data = data;
            public SaveData Load() => _data;
            public void Save(SaveData data) => _data = data;
        }

        private sealed class FakeGameApi : IGameApi
        {
            public bool FailGetDaily;
            public bool FailSubmit;
            public bool LastAssisted;
            public int SubmitCount;
            public int RouteCount = 3;
            public int EasyMs = 4100;
            public int MediumMs = 3500;
            public int HardMs = 2700;

            public Task<DailyDto> GetDailyAsync()
            {
                if (FailGetDaily) throw new InvalidOperationException("offline");
                return Task.FromResult(new DailyDto
                {
                    challengeId = "daily_2026_09_15",
                    seed = 123,
                    generatorVersion = 1,
                    routeCount = RouteCount,
                    displayTimeEasyMs = EasyMs,
                    displayTimeMediumMs = MediumMs,
                    displayTimeHardMs = HardMs,
                    serverTimeUtc = "2026-09-15T06:00:00Z"
                });
            }

            public Task<double> SubmitDailyAttemptAsync(string playerId, DailyChallengeDefinition challenge, IReadOnlyList<IReadOnlyList<RecordedPoint>> replays, IReadOnlyList<double> clientScores, bool assisted)
            {
                SubmitCount++;
                LastAssisted = assisted;
                if (FailSubmit) throw new InvalidOperationException("offline");
                return Task.FromResult(DailyChallengeFactory.DailyScore(clientScores));
            }

            public Task<string> CreateChallengeAsync(string playerId, string challengeId, double score) => Task.FromResult("nesbeisya://challenge/test");
            public Task<ReferralDto> GetReferralAsync(string referralId) => Task.FromResult(new ReferralDto { referralId = referralId });
        }
    }
}
