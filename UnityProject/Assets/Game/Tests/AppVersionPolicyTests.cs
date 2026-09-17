using DontGetSidetracked.Services;
using NUnit.Framework;

namespace DontGetSidetracked.Tests
{
    public sealed class AppVersionPolicyTests
    {
        [TestCase("1.0.0")]
        [TestCase("10.5.1")]
        [TestCase("1.2.3-beta")]
        [TestCase("1.2.3+42")]
        [TestCase("1.2.3-beta+42")]
        public void ValidVersions_AreAccepted(string value)
        {
            Assert.That(AppVersionPolicy.IsValid(value), Is.True);
        }

        [TestCase("")]
        [TestCase("1..2")]
        [TestCase("1.a.2")]
        [TestCase(".1.2")]
        [TestCase("1.2.")]
        [TestCase("1.2.3-")]
        [TestCase("1.2.3 beta")]
        public void MalformedVersions_AreRejected(string value)
        {
            Assert.That(AppVersionPolicy.IsValid(value), Is.False);
        }

        [Test]
        public void Compare_UsesNumericCoreAndMissingSegmentsAsZero()
        {
            Assert.That(AppVersionPolicy.Compare("1.2", "1.2.0"), Is.EqualTo(0));
            Assert.That(AppVersionPolicy.Compare("1.2.10", "1.2.9"), Is.GreaterThan(0));
            Assert.That(AppVersionPolicy.Compare("2.0.0-beta", "1.9.9"), Is.GreaterThan(0));
        }

        [Test]
        public void NormalizeOrFallback_UsesSafeFallbackForMalformedRemoteValue()
        {
            Assert.That(AppVersionPolicy.NormalizeOrFallback("1..2", "3.4.5"), Is.EqualTo("3.4.5"));
        }

        [Test]
        public void NormalizeRange_ClampsRecommendedVersionToMandatoryFloor()
        {
            AppVersionRange range = AppVersionPolicy.NormalizeRange("2.0.0", "1.5.0");

            Assert.That(range.MinSupportedVersion, Is.EqualTo("2.0.0"));
            Assert.That(range.RecommendedVersion, Is.EqualTo("2.0.0"));
        }

        [Test]
        public void NormalizeRange_InvalidValuesCannotCreateAccidentalUpdateLockout()
        {
            AppVersionRange range = AppVersionPolicy.NormalizeRange("999..0", "bad-value");

            Assert.That(range.MinSupportedVersion, Is.EqualTo(AppVersionPolicy.SafeDefaultVersion));
            Assert.That(range.RecommendedVersion, Is.EqualTo(AppVersionPolicy.SafeDefaultVersion));
        }
    }
}
