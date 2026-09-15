using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DontGetSidetracked.Services;

namespace DontGetSidetracked.Daily
{
    public sealed class LeaderboardSnapshot
    {
        public string ChallengeId { get; }
        public IReadOnlyList<LeaderboardItemDto> Items { get; }
        public LeaderboardItemDto CurrentPlayer { get; }

        public LeaderboardSnapshot(string challengeId, IReadOnlyList<LeaderboardItemDto> items, LeaderboardItemDto currentPlayer)
        {
            ChallengeId = challengeId;
            Items = items ?? Array.Empty<LeaderboardItemDto>();
            CurrentPlayer = currentPlayer;
        }
    }

    public sealed class LeaderboardService
    {
        private readonly ILeaderboardApi _api;
        private readonly string _playerId;

        public LeaderboardService(ILeaderboardApi api, string playerId)
        {
            _api = api ?? throw new ArgumentNullException(nameof(api));
            _playerId = playerId ?? string.Empty;
        }

        public async Task<LeaderboardSnapshot> LoadDailyAsync(string challengeId, int limit = 100)
        {
            LeaderboardDto dto = await _api.GetDailyLeaderboardAsync(challengeId, limit);
            if (dto == null) throw new InvalidOperationException("Leaderboard response is empty.");

            IReadOnlyList<LeaderboardItemDto> items = dto.Items;
            LeaderboardItemDto current = null;
            for (int i = 0; i < items.Count; i++)
            {
                LeaderboardItemDto item = items[i];
                if (item != null && string.Equals(item.PlayerId, _playerId, StringComparison.Ordinal))
                {
                    current = item;
                    break;
                }
            }
            return new LeaderboardSnapshot(dto.ChallengeId, items, current);
        }
    }
}
