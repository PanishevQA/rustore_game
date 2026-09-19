using System;
using System.Collections.Generic;

namespace DontGetSidetracked.Gameplay
{
    /// <summary>
    /// Pure C# runtime tuning for presentation/session parameters that may come from Remote Config.
    /// Route geometry remains fully determined by seed + generatorVersion.
    /// </summary>
    public static class RouteRuntimeTuning
    {
        public const int DefaultEasyDisplayTimeMs = 3500;
        public const int DefaultMediumDisplayTimeMs = 3000;
        public const int DefaultHardDisplayTimeMs = 2500;
        public const int DefaultDailyRouteCount = 3;
        public const int MinDisplayTimeMs = 750;
        public const int MaxDisplayTimeMs = 10000;

        private const int MinDailyRouteCount = 1;
        private const int MaxDailyRouteCount = 3;

        private static int _easyDisplayTimeMs = DefaultEasyDisplayTimeMs;
        private static int _mediumDisplayTimeMs = DefaultMediumDisplayTimeMs;
        private static int _hardDisplayTimeMs = DefaultHardDisplayTimeMs;
        private static int _dailyRouteCount = DefaultDailyRouteCount;

        public static int DailyRouteCount => _dailyRouteCount;

        public static void ConfigureDisplayTimes(int easyMs, int mediumMs, int hardMs)
        {
            _easyDisplayTimeMs = NormalizeDisplayTimeMs(RouteDifficulty.Easy, easyMs);
            _mediumDisplayTimeMs = NormalizeDisplayTimeMs(RouteDifficulty.Medium, mediumMs);
            _hardDisplayTimeMs = NormalizeDisplayTimeMs(RouteDifficulty.Hard, hardMs);
        }

        public static void ConfigureDailyRouteCount(int routeCount)
        {
            _dailyRouteCount = routeCount >= MinDailyRouteCount && routeCount <= MaxDailyRouteCount
                ? routeCount
                : DefaultDailyRouteCount;
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

        public static int GetDefaultDisplayTimeMs(RouteDifficulty difficulty)
        {
            switch (difficulty)
            {
                case RouteDifficulty.Easy: return DefaultEasyDisplayTimeMs;
                case RouteDifficulty.Medium: return DefaultMediumDisplayTimeMs;
                case RouteDifficulty.Hard: return DefaultHardDisplayTimeMs;
                default: throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, null);
            }
        }

        public static int NormalizeDisplayTimeMs(RouteDifficulty difficulty, int value)
        {
            int fallback = GetDefaultDisplayTimeMs(difficulty);
            return value >= MinDisplayTimeMs && value <= MaxDisplayTimeMs ? value : fallback;
        }

        public static IReadOnlyList<int> GetCurrentDisplayTimes(int routeCount)
        {
            if (routeCount < 1 || routeCount > 3)
                throw new ArgumentOutOfRangeException(nameof(routeCount));

            var result = new int[routeCount];
            for (int i = 0; i < routeCount; i++) result[i] = GetDisplayTimeMs((RouteDifficulty)i);
            return result;
        }

        public static IReadOnlyList<int> GetDefaultDisplayTimes(int routeCount)
        {
            if (routeCount < 1 || routeCount > 3)
                throw new ArgumentOutOfRangeException(nameof(routeCount));

            var result = new int[routeCount];
            for (int i = 0; i < routeCount; i++) result[i] = GetDefaultDisplayTimeMs((RouteDifficulty)i);
            return result;
        }

        public static void ResetDefaults()
        {
            _easyDisplayTimeMs = DefaultEasyDisplayTimeMs;
            _mediumDisplayTimeMs = DefaultMediumDisplayTimeMs;
            _hardDisplayTimeMs = DefaultHardDisplayTimeMs;
            _dailyRouteCount = DefaultDailyRouteCount;
        }
    }
}
