using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using DontGetSidetracked.Services;
using UnityEngine;

namespace DontGetSidetracked.Analytics
{
    [Serializable]
    internal sealed class LocalAnalyticsParameter
    {
        public string key;
        public string value;
    }

    [Serializable]
    internal sealed class LocalAnalyticsEvent
    {
        public string eventId;
        public string playerId;
        public int sessionNumber;
        public string eventName;
        public string occurredAtUtc;
        public LocalAnalyticsParameter[] parameters;
    }

    [Serializable]
    internal sealed class LocalAnalyticsLog
    {
        public List<LocalAnalyticsEvent> events = new List<LocalAnalyticsEvent>();
    }

    /// <summary>
    /// Device-only analytics log for the offline MVP. It never opens a socket and never uploads data.
    /// The bounded file is useful for local QA/debug builds and can later be replaced by another adapter.
    /// </summary>
    public sealed class LocalAnalyticsService : IAnalyticsService
    {
        private const int MaxEvents = 500;

        private readonly string _playerId;
        private readonly int _sessionNumber;
        private readonly string _path;
        private readonly object _gate = new object();
        private LocalAnalyticsLog _log;

        public int StoredCount
        {
            get
            {
                lock (_gate) return _log.events.Count;
            }
        }

        public LocalAnalyticsService(string playerId, int sessionNumber, string fileName = "analytics-local.json")
        {
            _playerId = string.IsNullOrWhiteSpace(playerId) ? "anonymous" : playerId;
            _sessionNumber = Math.Max(1, sessionNumber);
            _path = Path.Combine(Application.persistentDataPath, fileName);
            _log = Load();
        }

        public void Track(string eventName, IReadOnlyDictionary<string, object> parameters = null)
        {
            if (!AnalyticsEventNames.IsKnown(eventName)) return;

            var item = new LocalAnalyticsEvent
            {
                eventId = "evt_" + Guid.NewGuid().ToString("N"),
                playerId = _playerId,
                sessionNumber = _sessionNumber,
                eventName = eventName,
                occurredAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                parameters = ConvertParameters(parameters)
            };

            lock (_gate)
            {
                _log.events.Add(item);
                int overflow = _log.events.Count - MaxEvents;
                if (overflow > 0) _log.events.RemoveRange(0, overflow);
                SaveLocked();
            }
        }

        // Kept to preserve lifecycle call sites. Offline analytics has nothing to upload.
        public Task<int> FlushAsync() => Task.FromResult(0);

        private LocalAnalyticsLog Load()
        {
            if (!File.Exists(_path)) return new LocalAnalyticsLog();
            try
            {
                LocalAnalyticsLog data = JsonUtility.FromJson<LocalAnalyticsLog>(File.ReadAllText(_path));
                if (data == null) data = new LocalAnalyticsLog();
                if (data.events == null) data.events = new List<LocalAnalyticsEvent>();
                if (data.events.Count > MaxEvents)
                    data.events.RemoveRange(0, data.events.Count - MaxEvents);
                return data;
            }
            catch (Exception error)
            {
                Debug.LogWarning($"Local analytics log ignored: {error.Message}");
                return new LocalAnalyticsLog();
            }
        }

        private void SaveLocked()
        {
            try
            {
                string temp = _path + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(_log));
                if (File.Exists(_path)) File.Delete(_path);
                File.Move(temp, _path);
            }
            catch (Exception error)
            {
                Debug.LogWarning($"Local analytics save failed: {error.Message}");
            }
        }

        private static LocalAnalyticsParameter[] ConvertParameters(IReadOnlyDictionary<string, object> parameters)
        {
            if (parameters == null || parameters.Count == 0) return Array.Empty<LocalAnalyticsParameter>();
            var result = new List<LocalAnalyticsParameter>(Math.Min(parameters.Count, 64));
            foreach (KeyValuePair<string, object> pair in parameters)
            {
                if (result.Count >= 64 || string.IsNullOrWhiteSpace(pair.Key)) continue;
                string key = pair.Key.Length > 64 ? pair.Key.Substring(0, 64) : pair.Key;
                string value = ToInvariantString(pair.Value);
                if (value.Length > 512) value = value.Substring(0, 512);
                result.Add(new LocalAnalyticsParameter { key = key, value = value });
            }
            return result.ToArray();
        }

        private static string ToInvariantString(object value)
        {
            if (value == null) return string.Empty;
            if (value is IFormattable formattable) return formattable.ToString(null, CultureInfo.InvariantCulture);
            return value.ToString() ?? string.Empty;
        }
    }
}
