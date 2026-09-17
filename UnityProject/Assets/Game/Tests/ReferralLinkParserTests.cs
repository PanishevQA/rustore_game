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
        public void ProductionShareLinks_UseChallengeDeepLinkAndRuStoreReferrerId()
        {
            var date = new DateTime(2026, 9, 17, 0, 0, 0, DateTimeKind.Utc);
            string token = OfflineChallengeCodec.Encode(
                date,
                123456,
                RouteGenerator.CurrentGeneratorVersion,
                3,
                new[] { 4200, 3600, 2900 },
                91.2);

            string deepLink = OfflineChallengeCodec.BuildDeepLink(token);
            string installUrl = OfflineChallengeCodec.BuildInstallUrl("com.example.nesbeisya", token);

            Assert.That(deepLink, Is.EqualTo("nesbeisya://challenge/" + token));
            Assert.That(
                installUrl,
                Is.EqualTo("https://www.rustore.ru/catalog/app/com.example.nesbeisya?referrerId=" + token));
            Assert.That(ReferralLinkParser.TryParse(deepLink, out string parsed), Is.True);
            Assert.That(parsed, Is.EqualTo(token));

            // The RuStore catalog URL is an installation/referrer transport, not an in-app gameplay deeplink.
            Assert.That(ReferralLinkParser.TryParse(installUrl, out _), Is.False);
        }

        [Test]
        public void ParsesMaximumCurrentL4ChallengeToken()
        {
            var date = new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc);
            string token = OfflineChallengeCodec.Encode(
                date,
                123456,
                RouteGenerator.CurrentGeneratorVersion,
                3,
                new[] { 4200, 3600, 2900 },
                91.2);

            // L4 = L3 payload (36 chars for three routes) + 4 hex checksum chars.
            Assert.That(token.Length, Is.EqualTo(40));
            Assert.That(token.StartsWith("L4", StringComparison.Ordinal), Is.True);
            Assert.That(ReferralLinkParser.TryParse("nesbeisya://challenge/" + token, out string referralId), Is.True);
            Assert.That(referralId, Is.EqualTo(token));
        }

        [Test]
        public void RejectsCorruptedCurrentL4Checksum()
        {
            var date = new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc);
            string token = OfflineChallengeCodec.Encode(
                date,
                123456,
                RouteGenerator.CurrentGeneratorVersion,
                3,
                new[] { 4200, 3600, 2900 },
                91.2);

            char replacement = token[token.Length - 1] == '0' ? '1' : '0';
            string corrupted = token.Substring(0, token.Length - 1) + replacement;

            Assert.That(OfflineChallengeCodec.TryDecode(corrupted, out _), Is.False);
            Assert.That(ReferralLinkParser.TryParse("nesbeisya://challenge/" + corrupted, out _), Is.False);
        }

        [TestCase("nesbeisya://challenge/L5ABCDEF")]
        [TestCase("nesbeisya://challenge/L9_NOT_A_REAL_VERSION")]
        public void RejectsUnknownVersionedChallengeNamespace(string input)
        {
            Assert.That(ReferralLinkParser.TryParse(input, out _), Is.False);
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
        [TestCase("L4")]
        [TestCase("L420260915")]
        [TestCase("L42026091500000001013FFFF3E8ABCD")]
        public void CodecRejectsMalformedChallengeTokensWithoutThrowing(string token)
        {
            Assert.DoesNotThrow(() =>
            {
                bool decoded = OfflineChallengeCodec.TryDecode(token, out _);
                Assert.That(decoded, Is.False);
            });
        }
    }
}
