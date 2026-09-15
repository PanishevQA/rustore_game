using System;
using UnityEngine;
using Unity.Notifications.Android;

namespace DontGetSidetracked.Platform.Android
{
    /// <summary>
    /// Schedules the next Daily reminder entirely on-device. No push server or developer backend is required.
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

        public DateTime CalculateNextFireTime(DateTime localNow, int localHour)
        {
            int hour = Math.Max(0, Math.Min(23, localHour));
            DateTime next = new DateTime(
                localNow.Year,
                localNow.Month,
                localNow.Day,
                hour,
                0,
                0,
                DateTimeKind.Local);
            if (next <= localNow) next = next.AddDays(1);
            return next;
        }

        public bool ScheduleNext(int localHour)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            EnsureChannel();
            AndroidNotificationCenter.CancelScheduledNotification(NotificationId);

            var notification = new AndroidNotification
            {
                Title = "НЕ СБЕЙСЯ!",
                Text = "Новое Daily Challenge уже ждёт. Сможешь повторить маршрут точнее?",
                FireTime = CalculateNextFireTime(DateTime.Now, localHour),
                ShouldAutoCancel = true
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
