using System;
using DontGetSidetracked.Gameplay;
using DontGetSidetracked.Social;
using NUnit.Framework;

namespace DontGetSidetracked.Tests
{
    public sealed class ReferralLinkParserTests
    {
        [TestCase("nesbeisya://challenge/X7AB3F", "X7AB3F")]
        [TestCase("https://game.example/c/x7ab3f", "X7AB3F")]
        public void ParsesSupportedReferralLinks(string input, string expected)
        {
            Assert.That(ReferralLinkParser.TryParse(input, out string referralId), Is.True);
            Assert.That(referralId, Is.EqualTo(expected));
        }

        [Test]
        public void ParsesMaximumCurrentL3ChallengeToken()
        {
            var date = new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc);
            string token = OfflineChallengeCodec.Encode(
                date,
                123456,
                RouteGenerator.CurrentGeneratorVersion,
                3,
                new[] { 4200, 3600, 2900 },
                91.2);

            Assert.That(token.Length, Is.EqualTo(36));
            Assert.That(ReferralLinkParser.TryParse("nesbeisya://challenge/" + token, out string referralId), Is.True);
            Assert.That(referralId, Is.EqualTo(token));
        }

        [TestCase("")]
        [TestCase("nesbeisya://settings")]
        [TestCase("nesbeisya://challenge/../bad")]
        [TestCase("javascript:alert(1)")]
        public void RejectsUnsupportedLinks(string input)
        {
            Assert.That(ReferralLinkParser.TryParse(input, out _), Is.False);
        }

        [TestCase("L3")]
        [TestCase("L320260915")]
        [TestCase("L32026091500000001013FFFF3E8")]
        public void CodecRejectsMalformedL3WithoutThrowing(string token)
        {
            Assert.DoesNotThrow(() =>
            {
                bool decoded = OfflineChallengeCodec.TryDecode(token, out _);
                Assert.That(decoded, Is.False);
            });
        }
    }
}
