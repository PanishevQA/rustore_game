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

        public DailyChallengeDefinition(string challengeId, long seed, int generatorVersion, IReadOnlyList<RouteDefinition> routes)
        {
            ChallengeId = challengeId;
            Seed = seed;
            GeneratorVersion = generatorVersion;
            Routes = routes;
        }
    }

    public sealed class DailyChallengeFactory
    {
        private readonly RouteGenerator _generator = new RouteGenerator();

        public DailyChallengeDefinition Create(string challengeId, long seed, int generatorVersion)
        {
            var seeder = new DeterministicRandom(seed, generatorVersion);
            var routes = new List<RouteDefinition>(3)
            {
                _generator.Generate(seeder.ForkSeed(0), generatorVersion, RouteDifficulty.Easy),
                _generator.Generate(seeder.ForkSeed(1), generatorVersion, RouteDifficulty.Medium),
                _generator.Generate(seeder.ForkSeed(2), generatorVersion, RouteDifficulty.Hard)
            };
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
