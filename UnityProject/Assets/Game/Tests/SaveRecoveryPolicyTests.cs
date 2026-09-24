using System;
using DontGetSidetracked.Core;
using NUnit.Framework;

namespace DontGetSidetracked.Tests
{
    public sealed class SaveRecoveryPolicyTests
    {
        private static readonly DateTime Older = new DateTime(2026, 9, 17, 10, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime Newer = Older.AddSeconds(1);

        [Test]
        public void MissingOrInvalidTempNeverReplacesPrimary()
        {
            Assert.That(SaveRecoveryPolicy.ShouldRecoverInterruptedWrite(true, false, Older, Newer), Is.False);
            Assert.That(SaveRecoveryPolicy.ShouldRecoverInterruptedWrite(false, false, Older, Newer), Is.False);
        }

        [Test]
        public void ValidTempRecoversWhenPrimaryIsMissingOrInvalid()
        {
            Assert.That(SaveRecoveryPolicy.ShouldRecoverInterruptedWrite(false, true, Newer, Older), Is.True);
        }

        [Test]
        public void NewerValidTempReplacesStaleValidPrimary()
        {
            Assert.That(SaveRecoveryPolicy.ShouldRecoverInterruptedWrite(true, true, Older, Newer), Is.True);
        }

        [Test]
        public void EqualOrOlderTempDoesNotReplaceValidPrimary()
        {
            Assert.That(SaveRecoveryPolicy.ShouldRecoverInterruptedWrite(true, true, Newer, Older), Is.False);
            Assert.That(SaveRecoveryPolicy.ShouldRecoverInterruptedWrite(true, true, Older, Older), Is.False);
        }
    }
}
