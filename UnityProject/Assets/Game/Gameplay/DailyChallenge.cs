using System;
using System.Collections.Generic;
using DontGetSidetracked.Core;

namespace DontGetSidetracked.Gameplay
{
    public sealed class DailyChallengeDefinition
    {
        public string ChallengeId { get; }
        public long Seed { get; }
        public int GeneratorVersion { get; }
        public IReadOnlyList<RouteDefinition> Routes { get; }
        public int RouteCount => Routes?.Count ?? 0;

        public DailyChallengeDefinition(string challengeId, long seed, int generatorVersion, IReadOnlyList<RouteDefinition> routes)
        {
            ChallengeId = challengeId;
            Seed = seed;
            GeneratorVersion = generatorVersion;
            Routes = routes ?? throw new ArgumentNullException(nameof(routes));
            if (routes.Count < 1 || routes.Count > 3)
                throw new ArgumentOutOfRangeException(nameof(routes), "Daily challenge must contain 1 to 3 routes.");
        }
    }

    public sealed class DailyChallengeFactory
    {
        private readonly RouteGenerator _generator = new RouteGenerator();

        public DailyChallengeDefinition Create(string challengeId, long seed, int generatorVersion, int routeCount = 3)
        {
            if (routeCount < 1 || routeCount > 3)
                throw new ArgumentOutOfRangeException(nameof(routeCount), "Daily route count must be between 1 and 3.");

            var seeder = new DeterministicRandom(seed, generatorVersion);
            var routes = new List<RouteDefinition>(routeCount);
            RouteDifficulty[] difficulties =
            {
                RouteDifficulty.Easy,
                RouteDifficulty.Medium,
                RouteDifficulty.Hard
            };

            for (int i = 0; i < routeCount; i++)
                routes.Add(_generator.Generate(seeder.ForkSeed(i), generatorVersion, difficulties[i]));

            return new DailyChallengeDefinition(challengeId, seed, generatorVersion, routes);
        }

        public static double DailyScore(IReadOnlyList<double> scores)
        {
            if (scores == null || scores.Count == 0) return 0;
            double sum = 0;
            for (int i = 0; i < scores.Count; i++) sum += scores[i];
            return Math.Round(sum / scores.Count, 1, MidpointRounding.ToEven);
        }
    }
}
