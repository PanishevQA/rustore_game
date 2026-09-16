using System;
using DontGetSidetracked.Core;

namespace DontGetSidetracked.Daily
{
    public sealed class DailyBestUpdate
    {
        public string ChallengeId { get; }
        public double PreviousBest { get; }
        public double BestScore { get; }
        public bool IsNewRecord { get; }

        public DailyBestUpdate(string challengeId, double previousBest, double bestScore, bool isNewRecord)
        {
            ChallengeId = challengeId ?? string.Empty;
            PreviousBest = previousBest;
            BestScore = bestScore;
            IsNewRecord = isNewRecord;
        }
    }

    public sealed class DailyBestService
    {
        public const int MaxStoredRecords = 60;

        public DailyBestUpdate Apply(SaveData save, string challengeId, double score)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            if (string.IsNullOrWhiteSpace(challengeId)) throw new ArgumentException("Challenge id is required.", nameof(challengeId));
            if (save.DailyBests == null) save.DailyBests = new System.Collections.Generic.List<DailyBestData>();

            double normalized = Math.Max(0.0, Math.Min(100.0, Math.Round(score, 1, MidpointRounding.AwayFromZero)));
            DailyBestData record = null;
            for (int i = 0; i < save.DailyBests.Count; i++)
            {
                DailyBestData candidate = save.DailyBests[i];
                if (candidate != null && string.Equals(candidate.ChallengeId, challengeId, StringComparison.Ordinal))
                {
                    record = candidate;
                    break;
                }
            }

            double previous = record?.BestScore ?? 0.0;
            bool isNew = record == null || normalized > previous + 0.0001;
            if (record == null)
            {
                record = new DailyBestData { ChallengeId = challengeId, BestScore = normalized };
                save.DailyBests.Add(record);
            }
            else if (isNew)
            {
                record.BestScore = normalized;
            }

            Trim(save);
            return new DailyBestUpdate(challengeId, previous, record.BestScore, isNew);
        }

        public double GetBest(SaveData save, string challengeId)
        {
            if (save?.DailyBests == null || string.IsNullOrWhiteSpace(challengeId)) return 0.0;
            for (int i = 0; i < save.DailyBests.Count; i++)
            {
                DailyBestData record = save.DailyBests[i];
                if (record != null && string.Equals(record.ChallengeId, challengeId, StringComparison.Ordinal))
                    return record.BestScore;
            }
            return 0.0;
        }

        private static void Trim(SaveData save)
        {
            if (save.DailyBests == null) return;
            while (save.DailyBests.Count > MaxStoredRecords)
                save.DailyBests.RemoveAt(0);
        }
    }
}
