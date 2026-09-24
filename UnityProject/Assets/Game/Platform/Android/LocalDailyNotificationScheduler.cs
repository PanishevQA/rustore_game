using System;
using DontGetSidetracked.Core;
using UnityEngine;
using Unity.Notifications.Android;

namespace DontGetSidetracked.Platform.Android
{
    /// <summary>
    /// Schedules the Daily reminder entirely on-device. The first fire time is aligned to the configured
    /// local hour and then repeats every 24 hours, so reminders continue even if the player does not reopen
    /// the game the next day. Every later app launch re-schedules it from local time, correcting clock/DST drift.
    /// No push server or developer backend is required.
    /// </summary>
    public sealed class LocalDailyNotificationScheduler
    {
        private const string ChannelId = "daily-reminders";
        private const string ChannelName = "Daily Challenge";
        private const string ChannelDescription = "Напоминание о новом ежедневном испытании";
        private const int NotificationId = 41001;

        public void EnsureChannel()
        {
#if UNITY_ANDROID || UNITY_EDITOR
            AndroidNotificationCenter.RegisterNotificationChannel(new AndroidNotificationChannel
            {
                Id = ChannelId,
                Name = ChannelName,
                Importance = Importance.Default,
                Description = ChannelDescription
            });
#endif
        }

        public DateTime CalculateNextFireTime(DateTime localNow, int localHour) =>
            DailyReminderPolicy.NextLocalFireTime(localNow, localHour);

        public bool ScheduleNext(int localHour)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            EnsureChannel();
            AndroidNotificationCenter.CancelScheduledNotification(NotificationId);

            var notification = new AndroidNotification
            {
                Title = "НЕ СБЕЙСЯ!",
                Text = "Новое Daily Challenge уже ждёт. Сможешь повторить маршрут точнее?",
                FireTime = DailyReminderPolicy.NextLocalFireTime(DateTime.Now, localHour),
                RepeatInterval = TimeSpan.FromDays(1),
                ShouldAutoCancel = true,
                ShowInForeground = false
            };
            AndroidNotificationCenter.SendNotificationWithExplicitID(notification, ChannelId, NotificationId);
            return true;
#else
            return false;
#endif
        }

        public void Cancel()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            AndroidNotificationCenter.CancelScheduledNotification(NotificationId);
#endif
        }
    }
}
