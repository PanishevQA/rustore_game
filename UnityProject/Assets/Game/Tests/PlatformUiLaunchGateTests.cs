using System;
using DontGetSidetracked.Services;
using NUnit.Framework;

namespace DontGetSidetracked.Tests
{
    [NonParallelizable]
    public sealed class PlatformUiLaunchGateTests
    {
        [SetUp]
        public void SetUp() => PlatformUiLaunchGate.Configure(null);

        [TearDown]
        public void TearDown() => PlatformUiLaunchGate.Configure(null);

        [Test]
        public void MissingGate_DeniesLaunch()
        {
            Assert.That(PlatformUiLaunchGate.CanLaunchNow(), Is.False);
        }

        [Test]
        public void ConfiguredSafeGate_AllowsLaunch()
        {
            PlatformUiLaunchGate.Configure(() => true);
            Assert.That(PlatformUiLaunchGate.CanLaunchNow(), Is.True);
        }

        [Test]
        public void ConfiguredUnsafeGate_DeniesLaunch()
        {
            PlatformUiLaunchGate.Configure(() => false);
            Assert.That(PlatformUiLaunchGate.CanLaunchNow(), Is.False);
        }

        [Test]
        public void ThrowingGate_FailsClosed()
        {
            PlatformUiLaunchGate.Configure(() => throw new InvalidOperationException("stale scene state"));
            Assert.That(PlatformUiLaunchGate.CanLaunchNow(), Is.False);
        }
    }
}
