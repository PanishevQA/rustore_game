using DontGetSidetracked.Network;
using NUnit.Framework;

namespace DontGetSidetracked.Tests
{
    public sealed class VersionPolicyTests
    {
        [TestCase("1.1.0", "1.0.9", 1)]
        [TestCase("1.2", "1.2.0", 0)]
        [TestCase("0.9.9", "1.0.0", -1)]
        [TestCase("1.4.0-beta", "1.4.0", 0)]
        public void Compare_UsesNumericVersionSegments(string left, string right, int expectedSign)
        {
            int result = VersionPolicy.Compare(left, right);
            Assert.That(System.Math.Sign(result), Is.EqualTo(expectedSign));
        }
    }
}
