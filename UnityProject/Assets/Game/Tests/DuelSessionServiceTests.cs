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
        [Test]
        public async Task LoadAsync_RecreatesExactChallengeFromReferralSeed()
        {
            var save = SaveData.CreateNew();
            save.PendingReferralId = "ABC123";
            var api = new FakeApi();
            var service = new DuelSessionService(api, new MemorySaveRepository(save), save);

            DuelSession session = await service.LoadAsync("ABC123");

            Assert.That(session.Referral.InviterScore, Is.EqualTo(94.7));
            Assert.That(session.Challenge.ChallengeId, Is.EqualTo("daily_2026_09_15"));
            Assert.That(session.Challenge.Seed, Is.EqualTo(778899));
            Assert.That(session.Challenge.GeneratorVersion, Is.EqualTo(1));
        }

        [Test]
        public async Task CompleteAsync_QueuesOfflineWithoutChangingStreak_AndClearsReferral()
        {
            var save = SaveData.CreateNew();
            save.PendingReferralId = "ABC123";
            save.Streak = 7;
            save.CompletedDailyCount = 4;
            var repo = new MemorySaveRepository(save);
            var api = new FakeApi { FailSubmit = true };
            var service = new DuelSessionService(api, repo, save);
            DuelSession session = await service.LoadAsync("ABC123");
            List<IReadOnlyList<RecordedPoint>> replays = CreateReplays(session.Challenge);
            var scores = new List<double> { 95.0, 96.0, 97.0 };

            DuelSubmissionResult result = await service.CompleteAsync(session, replays, scores);

            Assert.That(result.SubmittedToServer, Is.False);
            Assert.That(result.Score, Is.EqualTo(96.0));
            Assert.That(result.Won, Is.True);
            Assert.That(save.PendingAttempts.Count, Is.EqualTo(1));
            Assert.That(save.PendingReferralId, Is.Empty);
            Assert.That(save.Streak, Is.EqualTo(7));
            Assert.That(save.CompletedDailyCount, Is.EqualTo(4));
        }

        private static List<IReadOnlyList<RecordedPoint>> CreateReplays(DailyChallengeDefinition challenge)
        {
            var result = new List<IReadOnlyList<RecordedPoint>>(3);
            for (int i = 0; i < 3; i++)
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
                Task.FromResult("https://example.test/c/ABC123");

            public Task<ReferralDto> GetReferralAsync(string referralId) => Task.FromResult(new ReferralDto
            {
                referralId = referralId,
                challengeId = "daily_2026_09_15",
                inviterId = "anon_friend",
                inviterScore = 94.7,
                seed = 778899,
                generatorVersion = 1,
                serverTimeUtc = "2026-09-15T06:00:00Z"
            });
        }
    }
}
