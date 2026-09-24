using DontGetSidetracked.Gameplay;
using NUnit.Framework;

namespace DontGetSidetracked.Tests
{
    public sealed class RouteRuntimeTuningTests
    {
        [Test]
        public void DisplayTuning_ChangesTimingButNotDeterministicGeometry()
        {
            var generator = new RouteGenerator();
            RouteRuntimeTuning.ResetDefaults();
            RouteDefinition baseline = generator.Generate(912345, RouteGenerator.CurrentGeneratorVersion, RouteDifficulty.Easy);

            try
            {
                RouteRuntimeTuning.ConfigureDisplayTimes(4200, 3600, 2900);
                RouteDefinition tuned = generator.Generate(912345, RouteGenerator.CurrentGeneratorVersion, RouteDifficulty.Easy);

                Assert.That(tuned.DisplayTimeMs, Is.EqualTo(4200));
                Assert.That(tuned.ReferencePoints.Count, Is.EqualTo(baseline.ReferencePoints.Count));
                for (int i = 0; i < baseline.ReferencePoints.Count; i++)
                {
                    Assert.That(tuned.ReferencePoints[i].X, Is.EqualTo(baseline.ReferencePoints[i].X));
                    Assert.That(tuned.ReferencePoints[i].Y, Is.EqualTo(baseline.ReferencePoints[i].Y));
                }
            }
            finally
            {
                RouteRuntimeTuning.ResetDefaults();
            }
        }

        [Test]
        public void DisplayTuning_RejectsUnsafeValuesToDefaults()
        {
            try
            {
                RouteRuntimeTuning.ConfigureDisplayTimes(0, 700, 20000);
                Assert.That(RouteRuntimeTuning.GetDisplayTimeMs(RouteDifficulty.Easy), Is.EqualTo(RouteRuntimeTuning.DefaultEasyDisplayTimeMs));
                Assert.That(RouteRuntimeTuning.GetDisplayTimeMs(RouteDifficulty.Medium), Is.EqualTo(RouteRuntimeTuning.DefaultMediumDisplayTimeMs));
                Assert.That(RouteRuntimeTuning.GetDisplayTimeMs(RouteDifficulty.Hard), Is.EqualTo(RouteRuntimeTuning.DefaultHardDisplayTimeMs));
            }
            finally
            {
                RouteRuntimeTuning.ResetDefaults();
            }
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void DailyRouteCount_AcceptsSupportedRange(int count)
        {
            try
            {
                RouteRuntimeTuning.ConfigureDailyRouteCount(count);
                Assert.That(RouteRuntimeTuning.DailyRouteCount, Is.EqualTo(count));
                DailyChallengeDefinition challenge = new DailyChallengeFactory().Create(
                    "daily_test",
                    12345,
                    RouteGenerator.CurrentGeneratorVersion,
                    RouteRuntimeTuning.DailyRouteCount);
                Assert.That(challenge.RouteCount, Is.EqualTo(count));
            }
            finally
            {
                RouteRuntimeTuning.ResetDefaults();
            }
        }

        [TestCase(0)]
        [TestCase(4)]
        [TestCase(99)]
        public void DailyRouteCount_RejectsUnsupportedValuesToDefault(int count)
        {
            try
            {
                RouteRuntimeTuning.ConfigureDailyRouteCount(count);
                Assert.That(RouteRuntimeTuning.DailyRouteCount, Is.EqualTo(RouteRuntimeTuning.DefaultDailyRouteCount));
            }
            finally
            {
                RouteRuntimeTuning.ResetDefaults();
            }
        }
    }
}
