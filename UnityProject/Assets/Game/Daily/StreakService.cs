using System;
using DontGetSidetracked.Core;

namespace DontGetSidetracked.Daily
{
    public sealed class StreakService
    {
        public bool ApplyCompletedDaily(SaveData save, DateTime serverDateUtc)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            DateTime day = serverDateUtc.Date;

            if (DateTime.TryParse(save.LastCompletedDailyDateUtc, out DateTime previous))
            {
                previous = previous.ToUniversalTime().Date;
                if (previous == day) return false;
                save.Streak = previous == day.AddDays(-1) ? save.Streak + 1 : 1;
            }
            else
            {
                save.Streak = 1;
            }

            save.LastCompletedDailyDateUtc = day.ToString("O");
            save.CompletedDailyCount++;
            return true;
        }
    }
}
