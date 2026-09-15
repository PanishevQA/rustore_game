using System;

namespace DontGetSidetracked.Gameplay
{
    /// <summary>
    /// Pure C# runtime tuning for presentation-only route parameters.
    /// Geometry remains fully determined by seed + generatorVersion.
    /// </summary>
    public static class RouteRuntimeTuning
    {
        public const int DefaultEasyDisplayTimeMs = 3500;
        public const int DefaultMediumDisplayTimeMs = 3000;
        public const int DefaultHardDisplayTimeMs = 2500;
        private const int MinDisplayTimeMs = 750;
        private const int MaxDisplayTimeMs = 10000;

        private static int _easyDisplayTimeMs = DefaultEasyDisplayTimeMs;
        private static int _mediumDisplayTimeMs = DefaultMediumDisplayTimeMs;
        private static int _hardDisplayTimeMs = DefaultHardDisplayTimeMs;

        public static void ConfigureDisplayTimes(int easyMs, int mediumMs, int hardMs)
        {
            _easyDisplayTimeMs = Validate(easyMs, DefaultEasyDisplayTimeMs);
            _mediumDisplayTimeMs = Validate(mediumMs, DefaultMediumDisplayTimeMs);
            _hardDisplayTimeMs = Validate(hardMs, DefaultHardDisplayTimeMs);
        }

        public static int GetDisplayTimeMs(RouteDifficulty difficulty)
        {
            switch (difficulty)
            {
                case RouteDifficulty.Easy: return _easyDisplayTimeMs;
                case RouteDifficulty.Medium: return _mediumDisplayTimeMs;
                case RouteDifficulty.Hard: return _hardDisplayTimeMs;
                default: throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, null);
            }
        }

        public static void ResetDefaults()
        {
            _easyDisplayTimeMs = DefaultEasyDisplayTimeMs;
            _mediumDisplayTimeMs = DefaultMediumDisplayTimeMs;
            _hardDisplayTimeMs = DefaultHardDisplayTimeMs;
        }

        private static int Validate(int value, int fallback) =>
            value >= MinDisplayTimeMs && value <= MaxDisplayTimeMs ? value : fallback;
    }
}
