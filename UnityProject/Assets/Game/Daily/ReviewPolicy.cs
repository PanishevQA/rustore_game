using System;
using DontGetSidetracked.Core;

namespace DontGetSidetracked.Daily
{
    public sealed class ReviewPolicy
    {
        private readonly int _minCompletedDaily;
        private readonly int _minStreak;
        private readonly double _minScore;
        private readonly int _cooldownSessions;

        public ReviewPolicy(int minCompletedDaily = 5, int minStreak = 3, double minScore = 95.0, int cooldownSessions = 10)
        {
            _minCompletedDaily = Math.Max(1, minCompletedDaily);
            _minStreak = Math.Max(1, minStreak);
            _minScore = Math.Max(0, Math.Min(100, minScore));
            _cooldownSessions = Math.Max(1, cooldownSessions);
        }

        public bool ShouldRequest(SaveData save, double dailyScore)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            bool positiveEvent = dailyScore >= _minScore || save.Streak >= _minStreak || save.CompletedDailyCount >= _minCompletedDaily;
            if (!positiveEvent) return false;
            if (save.LastReviewRequestSession <= 0) return true;
            return save.SessionNumber - save.LastReviewRequestSession >= _cooldownSessions;
        }

        public void MarkRequested(SaveData save)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            save.LastReviewRequestSession = Math.Max(1, save.SessionNumber);
            save.ReviewRequestCount++;
        }
    }
}
