using System.Collections.Generic;
using DontGetSidetracked.Core;
using DontGetSidetracked.Gameplay;
using NUnit.Framework;

namespace DontGetSidetracked.Tests
{
    public sealed class GameplayRulesTests
    {
        [Test]
        public void SameSeedProducesSamePolyline()
        {
            var generator = new RouteGenerator();
            var a = generator.Generate(123456, 1, RouteDifficulty.Medium);
            var b = generator.Generate(123456, 1, RouteDifficulty.Medium);
            Assert.That(a.ReferencePoints.Count, Is.EqualTo(b.ReferencePoints.Count));
            for (int i = 0; i < a.ReferencePoints.Count; i++) Assert.That(a.ReferencePoints[i], Is.EqualTo(b.ReferencePoints[i]));
        }

        [Test]
        public void ThousandRoutesAreValid()
        {
            var generator = new RouteGenerator();
            for (int i = 0; i < 1000; i++)
            {
                var difficulty = (RouteDifficulty)(i % 3);
                Assert.That(RouteGenerator.IsValid(generator.Generate(i + 1, 1, difficulty)), Is.True, $"seed={i + 1}");
            }
        }

        [Test]
        public void ExactReplayScoresOneHundred()
        {
            var generator = new RouteGenerator();
            var route = generator.Generate(42, 1, RouteDifficulty.Hard);
            var replay = new List<RecordedPoint>();
            for (int i = 0; i < route.ReferencePoints.Count; i++) replay.Add(new RecordedPoint(route.ReferencePoints[i], i * 16));
            var result = new ScoreCalculator().Calculate(route, replay);
            Assert.That(result.Score, Is.EqualTo(100.0));
        }
    }
}
