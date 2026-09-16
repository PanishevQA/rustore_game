using System;
using DontGetSidetracked.Core;

namespace DontGetSidetracked.Gameplay
{
    public sealed class CampaignLevelDefinition
    {
        public int LevelNumber { get; }
        public int ChapterNumber { get; }
        public long Seed { get; }
        public int GeneratorVersion { get; }
        public RouteDifficulty Difficulty { get; }
        public int DisplayTimeMs { get; }

        public CampaignLevelDefinition(
            int levelNumber,
            int chapterNumber,
            long seed,
            int generatorVersion,
            RouteDifficulty difficulty,
            int displayTimeMs)
        {
            LevelNumber = levelNumber;
            ChapterNumber = chapterNumber;
            Seed = seed;
            GeneratorVersion = generatorVersion;
            Difficulty = difficulty;
            DisplayTimeMs = displayTimeMs;
        }

        public RouteDefinition BuildRoute(RouteGenerator generator)
        {
            if (generator == null) throw new ArgumentNullException(nameof(generator));
            RouteDefinition generated = generator.Generate(Seed, GeneratorVersion, Difficulty);
            return new RouteDefinition(
                generated.Seed,
                generated.GeneratorVersion,
                generated.Difficulty,
                DisplayTimeMs,
                generated.PathWidth,
                generated.ReferencePoints);
        }
    }

    public static class CampaignLevelCatalog
    {
        public const int TotalLevels = 60;
        public const int LevelsPerChapter = 10;
        public const int TotalChapters = TotalLevels / LevelsPerChapter;

        public static CampaignLevelDefinition Get(int levelNumber)
        {
            if (levelNumber < 1 || levelNumber > TotalLevels)
                throw new ArgumentOutOfRangeException(nameof(levelNumber));

            int chapter = ((levelNumber - 1) / LevelsPerChapter) + 1;
            RouteDifficulty difficulty;
            int displayTime;

            if (levelNumber <= 10)
            {
                difficulty = RouteDifficulty.Easy;
                displayTime = Interpolate(4000, 3400, levelNumber, 1, 10);
            }
            else if (levelNumber <= 20)
            {
                difficulty = RouteDifficulty.Easy;
                displayTime = Interpolate(3300, 2800, levelNumber, 11, 20);
            }
            else if (levelNumber <= 30)
            {
                difficulty = RouteDifficulty.Medium;
                displayTime = Interpolate(3400, 2900, levelNumber, 21, 30);
            }
            else if (levelNumber <= 40)
            {
                difficulty = RouteDifficulty.Medium;
                displayTime = Interpolate(2800, 2300, levelNumber, 31, 40);
            }
            else if (levelNumber <= 50)
            {
                difficulty = RouteDifficulty.Hard;
                displayTime = Interpolate(3200, 2700, levelNumber, 41, 50);
            }
            else
            {
                difficulty = RouteDifficulty.Hard;
                displayTime = Interpolate(2600, 2100, levelNumber, 51, 60);
            }

            return new CampaignLevelDefinition(
                levelNumber,
                chapter,
                SeedFor(levelNumber, RouteGenerator.CurrentGeneratorVersion),
                RouteGenerator.CurrentGeneratorVersion,
                difficulty,
                displayTime);
        }

        public static long SeedFor(int levelNumber, int generatorVersion)
        {
            if (levelNumber < 1 || levelNumber > TotalLevels)
                throw new ArgumentOutOfRangeException(nameof(levelNumber));
            unchecked
            {
                long mixed = levelNumber * 1103515245L + 12345L + generatorVersion * 2654435761L;
                return mixed & 0x7FFFFFFF;
            }
        }

        private static int Interpolate(int from, int to, int value, int min, int max)
        {
            if (max <= min) return from;
            int step = value - min;
            int span = max - min;
            return from + (int)(((long)(to - from) * step) / span);
        }
    }

    public static class CampaignStarPolicy
    {
        public static int StarsFor(double score)
        {
            if (score >= 95.0) return 3;
            if (score >= 80.0) return 2;
            if (score >= 60.0) return 1;
            return 0;
        }
    }

    public sealed class CampaignLevelCompletion
    {
        public int LevelNumber { get; }
        public double Score { get; }
        public double BestScore { get; }
        public int Stars { get; }
        public bool NewBest { get; }
        public bool NewStars { get; }
        public bool NextLevelUnlocked { get; }
        public int HighestUnlockedLevel { get; }

        public CampaignLevelCompletion(
            int levelNumber,
            double score,
            double bestScore,
            int stars,
            bool newBest,
            bool newStars,
            bool nextLevelUnlocked,
            int highestUnlockedLevel)
        {
            LevelNumber = levelNumber;
            Score = score;
            BestScore = bestScore;
            Stars = stars;
            NewBest = newBest;
            NewStars = newStars;
            NextLevelUnlocked = nextLevelUnlocked;
            HighestUnlockedLevel = highestUnlockedLevel;
        }
    }

    public sealed class CampaignProgressService
    {
        private readonly ISaveRepository _repository;
        private readonly SaveData _save;

        public CampaignProgressService(ISaveRepository repository, SaveData save)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _save = save ?? throw new ArgumentNullException(nameof(save));
            Normalize();
        }

        public SaveData Save => _save;
        public int HighestUnlockedLevel => _save.HighestUnlockedLevel;

        public bool IsUnlocked(int levelNumber) =>
            levelNumber >= 1 && levelNumber <= CampaignLevelCatalog.TotalLevels && levelNumber <= _save.HighestUnlockedLevel;

        public LevelProgressData GetProgress(int levelNumber)
        {
            for (int i = 0; i < _save.LevelProgress.Count; i++)
            {
                if (_save.LevelProgress[i] != null && _save.LevelProgress[i].LevelNumber == levelNumber)
                    return _save.LevelProgress[i];
            }
            return null;
        }

        public int TotalStars()
        {
            int total = 0;
            for (int i = 0; i < _save.LevelProgress.Count; i++)
                if (_save.LevelProgress[i] != null) total += Math.Max(0, Math.Min(3, _save.LevelProgress[i].Stars));
            return total;
        }

        public CampaignLevelCompletion RecordResult(int levelNumber, double score)
        {
            if (!IsUnlocked(levelNumber))
                throw new InvalidOperationException($"Level {levelNumber} is locked.");

            double normalizedScore = Math.Max(0.0, Math.Min(100.0, score));
            LevelProgressData progress = GetProgress(levelNumber);
            if (progress == null)
            {
                progress = new LevelProgressData { LevelNumber = levelNumber };
                _save.LevelProgress.Add(progress);
            }

            bool newBest = normalizedScore > progress.BestScore;
            if (newBest) progress.BestScore = normalizedScore;

            int earnedStars = CampaignStarPolicy.StarsFor(normalizedScore);
            bool newStars = earnedStars > progress.Stars;
            if (newStars) progress.Stars = earnedStars;

            int previousHighest = _save.HighestUnlockedLevel;
            if (progress.Stars >= 1 && levelNumber < CampaignLevelCatalog.TotalLevels)
                _save.HighestUnlockedLevel = Math.Max(_save.HighestUnlockedLevel, levelNumber + 1);

            Normalize();
            _repository.Save(_save);

            return new CampaignLevelCompletion(
                levelNumber,
                normalizedScore,
                progress.BestScore,
                progress.Stars,
                newBest,
                newStars,
                _save.HighestUnlockedLevel > previousHighest,
                _save.HighestUnlockedLevel);
        }

        private void Normalize()
        {
            if (_save.LevelProgress == null) _save.LevelProgress = new System.Collections.Generic.List<LevelProgressData>();
            if (_save.HighestUnlockedLevel < 1) _save.HighestUnlockedLevel = 1;
            if (_save.HighestUnlockedLevel > CampaignLevelCatalog.TotalLevels)
                _save.HighestUnlockedLevel = CampaignLevelCatalog.TotalLevels;
        }
    }
}
