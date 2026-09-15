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

        [TestCase("")]
        [TestCase("nesbeisya://settings")]
        [TestCase("nesbeisya://challenge/../bad")]
        [TestCase("javascript:alert(1)")]
        public void RejectsUnsupportedLinks(string input)
        {
            Assert.That(ReferralLinkParser.TryParse(input, out _), Is.False);
        }
    }
}
