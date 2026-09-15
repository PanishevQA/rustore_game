using System;
using DontGetSidetracked.Core;
using NUnit.Framework;

namespace DontGetSidetracked.Tests
{
    public sealed class DailyReminderPolicyTests
    {
        [TestCase(-5, 0)]
        [TestCase(0, 0)]
        [TestCase(10, 10)]
        [TestCase(23, 23)]
        [TestCase(42, 23)]
        public void ClampHour_StaysInsideDay(int input, int expected)
        {
            Assert.That(DailyReminderPolicy.ClampHour(input), Is.EqualTo(expected));
        }

        [Test]
        public void NextLocalFireTime_BeforeTargetHour_UsesSameDay()
        {
            var now = new DateTime(2026, 9, 15, 8, 30, 0, DateTimeKind.Local);

            DateTime next = DailyReminderPolicy.NextLocalFireTime(now, 10);

            Assert.That(next, Is.EqualTo(new DateTime(2026, 9, 15, 10, 0, 0, DateTimeKind.Local)));
        }

        [Test]
        public void NextLocalFireTime_AtTargetHour_UsesNextDay()
        {
            var now = new DateTime(2026, 9, 15, 10, 0, 0, DateTimeKind.Local);

            DateTime next = DailyReminderPolicy.NextLocalFireTime(now, 10);

            Assert.That(next, Is.EqualTo(new DateTime(2026, 9, 16, 10, 0, 0, DateTimeKind.Local)));
        }

        [Test]
        public void NextLocalFireTime_AfterTargetHour_UsesNextDay()
        {
            var now = new DateTime(2026, 9, 15, 22, 5, 0, DateTimeKind.Local);

            DateTime next = DailyReminderPolicy.NextLocalFireTime(now, 10);

            Assert.That(next, Is.EqualTo(new DateTime(2026, 9, 16, 10, 0, 0, DateTimeKind.Local)));
        }

        [Test]
        public void NextLocalFireTime_UnspecifiedInput_IsTreatedAsLocalClockTime()
        {
            var now = new DateTime(2026, 9, 15, 9, 59, 59, DateTimeKind.Unspecified);

            DateTime next = DailyReminderPolicy.NextLocalFireTime(now, 10);

            Assert.That(next.Kind, Is.EqualTo(DateTimeKind.Local));
            Assert.That(next.Hour, Is.EqualTo(10));
            Assert.That(next.Day, Is.EqualTo(15));
        }
    }
}
