using System;

namespace DontGetSidetracked.Gameplay
{
    public enum ScoreMedalTier
    {
        None = 0,
        Bronze = 1,
        Silver = 2,
        Gold = 3,
        Perfect = 4
    }

    public sealed class ScoreCelebration
    {
        public ScoreMedalTier Tier { get; }
        public string Label { get; }
        public string Icon { get; }
        public bool PlayPerfectEffect { get; }

        public ScoreCelebration(ScoreMedalTier tier, string label, string icon, bool playPerfectEffect)
        {
            Tier = tier;
            Label = label ?? string.Empty;
            Icon = icon ?? string.Empty;
            PlayPerfectEffect = playPerfectEffect;
        }
    }

    public static class ScoreCelebrationPolicy
    {
        public const double BronzeThreshold = 80.0;
        public const double SilverThreshold = 90.0;
        public const double GoldThreshold = 95.0;
        public const double PerfectThreshold = 99.0;
        public const double PerfectEffectThreshold = 98.0;

        public static ScoreCelebration Evaluate(double score)
        {
            double value = Math.Max(0.0, Math.Min(100.0, score));
            bool perfectEffect = value >= PerfectEffectThreshold;

            if (value >= PerfectThreshold)
                return new ScoreCelebration(ScoreMedalTier.Perfect, "ИДЕАЛЬНО!", "✨", true);
            if (value >= GoldThreshold)
                return new ScoreCelebration(ScoreMedalTier.Gold, "МАСТЕР!", "🥇", perfectEffect);
            if (value >= SilverThreshold)
                return new ScoreCelebration(ScoreMedalTier.Silver, "ОТЛИЧНО!", "🥈", false);
            if (value >= BronzeThreshold)
                return new ScoreCelebration(ScoreMedalTier.Bronze, "ТОЧНО!", "🥉", false);
            return new ScoreCelebration(ScoreMedalTier.None, "ЕЩЁ ОДНА ПОПЫТКА", string.Empty, false);
        }
    }
}
