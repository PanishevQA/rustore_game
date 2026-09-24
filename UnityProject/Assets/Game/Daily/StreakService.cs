using System;
using System.Globalization;
using DontGetSidetracked.Core;

namespace DontGetSidetracked.Daily
{
    public sealed class StreakService
    {
        public bool ApplyCompletedDaily(SaveData save, DateTime serverDateUtc)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            DateTime utc = serverDateUtc.Kind == DateTimeKind.Utc ? serverDateUtc : serverDateUtc.ToUniversalTime();
            DateTime day = utc.Date;

            if (DateTime.TryParse(
                    save.LastCompletedDailyDateUtc,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out DateTime previous))
            {
                previous = previous.Date;
                if (previous == day) return false;
                save.Streak = previous == day.AddDays(-1) ? save.Streak + 1 : 1;
            }
            else
            {
                save.Streak = 1;
            }

            save.LastCompletedDailyDateUtc = day.ToString("O", CultureInfo.InvariantCulture);
            if (save.CompletedDailyCount < int.MaxValue) save.CompletedDailyCount++;
            return true;
        }
    }
}
