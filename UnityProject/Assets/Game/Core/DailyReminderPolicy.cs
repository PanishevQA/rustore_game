using System;

namespace DontGetSidetracked.Core
{
    public static class DailyReminderPolicy
    {
        public static int ClampHour(int hour) => Math.Max(0, Math.Min(23, hour));

        public static DateTime NextLocalFireTime(DateTime localNow, int localHour)
        {
            int hour = ClampHour(localHour);
            DateTime current = localNow.Kind == DateTimeKind.Local
                ? localNow
                : DateTime.SpecifyKind(localNow, DateTimeKind.Local);

            DateTime candidate = new DateTime(
                current.Year,
                current.Month,
                current.Day,
                hour,
                0,
                0,
                DateTimeKind.Local);

            return candidate <= current ? candidate.AddDays(1) : candidate;
        }
    }
}
