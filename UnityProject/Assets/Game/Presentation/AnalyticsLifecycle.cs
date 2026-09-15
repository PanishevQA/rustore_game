using System.Collections.Generic;
using DontGetSidetracked.Analytics;
using DontGetSidetracked.Core;
using UnityEngine;

namespace DontGetSidetracked.Presentation
{
    public sealed class AnalyticsLifecycle : MonoBehaviour
    {
        public static QueuedAnalyticsService Service { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (Service != null) return;

            var saveRepository = new JsonFileSaveRepository();
            SaveData save = saveRepository.Load();
            int sessionNumber = save.SessionNumber + 1;
            Service = new QueuedAnalyticsService(
                save.AnonymousPlayerId,
                sessionNumber,
                GameRuntimeSettings.BackendBaseUrl);

            Service.Track(AnalyticsEventNames.AppOpen, new Dictionary<string, object>
            {
                ["client_version"] = Application.version,
                ["platform"] = Application.platform.ToString()
            });
            Service.Track(AnalyticsEventNames.SessionStart, new Dictionary<string, object>
            {
                ["session_number"] = sessionNumber
            });

            var root = new GameObject("AnalyticsLifecycle");
            DontDestroyOnLoad(root);
            root.AddComponent<AnalyticsLifecycle>();
            _ = Service.FlushAsync();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && Service != null) _ = Service.FlushAsync();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused && Service != null) _ = Service.FlushAsync();
        }

        private void OnApplicationQuit()
        {
            if (Service != null) _ = Service.FlushAsync();
        }
    }
}
