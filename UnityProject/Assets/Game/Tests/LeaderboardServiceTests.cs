using System.Threading.Tasks;
using DontGetSidetracked.Network;
using DontGetSidetracked.Services;
using NUnit.Framework;

namespace DontGetSidetracked.Tests
{
    public sealed class LeaderboardServiceTests
    {
        [Test]
        public async Task LoadAsync_FindsCurrentPlayerAndPreservesServerRanks()
        {
            var api = new FakeLeaderboardApi
            {
                Response = new LeaderboardDto
                {
                    challengeId = "daily_2026_09_15",
                    items = new[]
                    {
                        new LeaderboardItemDto { rank = 1, playerId = "anon_a", score = 99.1 },
                        new LeaderboardItemDto { rank = 2, playerId = "anon_me", score = 96.4 },
                        new LeaderboardItemDto { rank = 3, playerId = "anon_b", score = 94.0 }
                    }
                }
            };
            var service = new LeaderboardService(api, "anon_me");

            LeaderboardSnapshot snapshot = await service.LoadAsync("daily_2026_09_15", 50);

            Assert.That(snapshot.ChallengeId, Is.EqualTo("daily_2026_09_15"));
            Assert.That(snapshot.Items.Count, Is.EqualTo(3));
            Assert.That(snapshot.CurrentPlayer, Is.Not.Null);
            Assert.That(snapshot.CurrentPlayer.Rank, Is.EqualTo(2));
            Assert.That(snapshot.CurrentPlayer.Score, Is.EqualTo(96.4).Within(0.001));
            Assert.That(api.RequestedLimit, Is.EqualTo(50));
        }

        [Test]
        public async Task LoadAsync_ClampsLimitAndAllowsMissingCurrentPlayer()
        {
            var api = new FakeLeaderboardApi
            {
                Response = new LeaderboardDto
                {
                    challengeId = "daily_2026_09_15",
                    items = new[]
                    {
                        new LeaderboardItemDto { rank = 1, playerId = "anon_a", score = 100 }
                    }
                }
            };
            var service = new LeaderboardService(api, "anon_missing");

            LeaderboardSnapshot snapshot = await service.LoadAsync("daily_2026_09_15", 500);

            Assert.That(api.RequestedLimit, Is.EqualTo(100));
            Assert.That(snapshot.CurrentPlayer, Is.Null);
        }

        private sealed class FakeLeaderboardApi : ILeaderboardApi
        {
            public LeaderboardDto Response;
            public int RequestedLimit;

            public Task<LeaderboardDto> GetDailyLeaderboardAsync(string challengeId, int limit = 100)
            {
                RequestedLimit = limit;
                return Task.FromResult(Response);
            }
        }
    }
}
