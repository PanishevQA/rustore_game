using DontGetSidetracked.Core;
using DontGetSidetracked.Daily;
using NUnit.Framework;

namespace DontGetSidetracked.Tests
{
    public sealed class ReviewPolicyTests
    {
        [Test]
        public void HighScore_IsPositiveEvent()
        {
            var save = SaveData.CreateNew();
            save.SessionNumber = 2;
            var policy = new ReviewPolicy();

            Assert.That(policy.ShouldRequest(save, 95.0), Is.True);
        }

        [Test]
        public void RecentRequest_RespectsSessionCooldown()
        {
            var save = SaveData.CreateNew();
            save.SessionNumber = 20;
            save.Streak = 5;
            save.LastReviewRequestSession = 15;
            var policy = new ReviewPolicy(cooldownSessions: 10);

            Assert.That(policy.ShouldRequest(save, 99), Is.False);
            save.SessionNumber = 25;
            Assert.That(policy.ShouldRequest(save, 99), Is.True);
        }

        [Test]
        public void MarkRequested_PersistsSessionAndCount()
        {
            var save = SaveData.CreateNew();
            save.SessionNumber = 7;
            var policy = new ReviewPolicy();

            policy.MarkRequested(save);

            Assert.That(save.LastReviewRequestSession, Is.EqualTo(7));
            Assert.That(save.ReviewRequestCount, Is.EqualTo(1));
        }
    }
}
