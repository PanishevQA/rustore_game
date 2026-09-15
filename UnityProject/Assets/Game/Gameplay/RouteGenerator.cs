using System;
using System.Collections.Generic;
using DontGetSidetracked.Core;

namespace DontGetSidetracked.Gameplay
{
    public enum RouteDifficulty { Easy = 0, Medium = 1, Hard = 2 }

    [Serializable]
    public sealed class RouteDefinition
    {
        public long Seed { get; }
        public int GeneratorVersion { get; }
        public RouteDifficulty Difficulty { get; }
        public int DisplayTimeMs { get; }
        public int PathWidth { get; }
        public IReadOnlyList<FixedPoint2> ReferencePoints { get; }

        public RouteDefinition(long seed, int generatorVersion, RouteDifficulty difficulty, int displayTimeMs, int pathWidth, IReadOnlyList<FixedPoint2> referencePoints)
        {
            Seed = seed;
            GeneratorVersion = generatorVersion;
            Difficulty = difficulty;
            DisplayTimeMs = displayTimeMs;
            PathWidth = pathWidth;
            ReferencePoints = referencePoints;
        }
    }

    public sealed class RouteGenerator
    {
        public const int CurrentGeneratorVersion = 1;
        private const int Margin = 120_000;
        private const int StartY = 150_000;
        private const int EndY = 850_000;
        private const int SamplesPerSegment = 16;

        public RouteDefinition Generate(long seed, int generatorVersion, RouteDifficulty difficulty)
        {
            if (generatorVersion != CurrentGeneratorVersion)
                throw new NotSupportedException($"Unsupported generatorVersion={generatorVersion}");

            var rng = new DeterministicRandom(seed, generatorVersion);
            int anchorCount = difficulty == RouteDifficulty.Easy ? 4 : difficulty == RouteDifficulty.Medium ? 6 : 8;
            int bend = difficulty == RouteDifficulty.Easy ? 150_000 : difficulty == RouteDifficulty.Medium ? 220_000 : 280_000;
            int pathWidth = difficulty == RouteDifficulty.Easy ? 55_000 : difficulty == RouteDifficulty.Medium ? 45_000 : 35_000;
            int displayTime = difficulty == RouteDifficulty.Easy ? 3500 : difficulty == RouteDifficulty.Medium ? 3000 : 2500;

            var anchors = new List<FixedPoint2>(anchorCount);
            for (int i = 0; i < anchorCount; i++)
            {
                int y = StartY + (int)(((long)(EndY - StartY) * i) / (anchorCount - 1));
                int x;
                if (i == 0 || i == anchorCount - 1)
                    x = 500_000 + rng.NextInt(-80_000, 80_000);
                else
                    x = rng.NextInt(Margin, FixedPoint2.Scale - Margin);
                anchors.Add(new FixedPoint2(x, y));
            }

            var points = new List<FixedPoint2>((anchorCount - 1) * SamplesPerSegment + 1) { anchors[0] };
            for (int i = 0; i < anchors.Count - 1; i++)
            {
                var p0 = anchors[i];
                var p3 = anchors[i + 1];
                int dy = p3.Y - p0.Y;
                var c1 = new FixedPoint2(Clamp(p0.X + rng.NextInt(-bend, bend), Margin, FixedPoint2.Scale - Margin), p0.Y + dy / 3);
                var c2 = new FixedPoint2(Clamp(p3.X + rng.NextInt(-bend, bend), Margin, FixedPoint2.Scale - Margin), p0.Y + (2 * dy) / 3);

                for (int step = 1; step <= SamplesPerSegment; step++)
                    points.Add(EvaluateBezier(p0, c1, c2, p3, step, SamplesPerSegment));
            }

            return new RouteDefinition(seed, generatorVersion, difficulty, displayTime, pathWidth, points);
        }

        public static bool IsValid(RouteDefinition route)
        {
            if (route.ReferencePoints == null || route.ReferencePoints.Count < 2) return false;
            int previousY = -1;
            foreach (var p in route.ReferencePoints)
            {
                if (p.X < Margin || p.X > FixedPoint2.Scale - Margin) return false;
                if (p.Y < StartY || p.Y > EndY) return false;
                if (p.Y < previousY) return false;
                previousY = p.Y;
            }

            return route.ReferencePoints[0].DistanceSquared(route.ReferencePoints[route.ReferencePoints.Count - 1]) > 250_000L * 250_000L;
        }

        private static FixedPoint2 EvaluateBezier(FixedPoint2 p0, FixedPoint2 p1, FixedPoint2 p2, FixedPoint2 p3, int t, int scale)
        {
            long u = scale - t;
            long denominator = (long)scale * scale * scale;
            long w0 = u * u * u;
            long w1 = 3L * u * u * t;
            long w2 = 3L * u * t * t;
            long w3 = (long)t * t * t;
            int x = (int)((p0.X * w0 + p1.X * w1 + p2.X * w2 + p3.X * w3) / denominator);
            int y = (int)((p0.Y * w0 + p1.Y * w1 + p2.Y * w2 + p3.Y * w3) / denominator);
            return new FixedPoint2(x, y);
        }

        private static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
    }
}
