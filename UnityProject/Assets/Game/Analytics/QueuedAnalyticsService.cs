using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using DontGetSidetracked.Services;
using UnityEngine;
using UnityEngine.Networking;

namespace DontGetSidetracked.Analytics
{
    [Serializable]
    internal sealed class AnalyticsParameterData
    {
        public string key;
        public string value;
    }

    [Serializable]
    internal sealed class AnalyticsEventData
    {
        public string eventId;
        public string playerId;
        public int sessionNumber;
        public string eventName;
        public string occurredAtUtc;
        public AnalyticsParameterData[] parameters;
    }

    [Serializable]
    internal sealed class AnalyticsQueueData
    {
        public List<AnalyticsEventData> events = new List<AnalyticsEventData>();
    }

    [Serializable]
    internal sealed class AnalyticsBatchData
    {
        public AnalyticsEventData[] events;
    }

    public sealed class QueuedAnalyticsService : IAnalyticsService
    {
        private const int MaxQueuedEvents = 500;
        private const int MaxBatchSize = 100;

        private readonly string _playerId;
        private readonly int _sessionNumber;
        private readonly string _baseUrl;
        private readonly string _queuePath;
        private readonly object _gate = new object();
        private AnalyticsQueueData _queue;
        private bool _flushInProgress;

        public int PendingCount
        {
            get
            {
                lock (_gate) return _queue.events.Count;
            }
        }

        public QueuedAnalyticsService(string playerId, int sessionNumber, string baseUrl, string queueFileName = "analytics-queue.json")
        {
            _playerId = string.IsNullOrWhiteSpace(playerId) ? "anonymous" : playerId;
            _sessionNumber = Math.Max(1, sessionNumber);
            _baseUrl = (baseUrl ?? string.Empty).TrimEnd('/');
            _queuePath = Path.Combine(Application.persistentDataPath, queueFileName);
            _queue = LoadQueue();
        }

        public void Track(string eventName, IReadOnlyDictionary<string, object> parameters = null)
        {
            if (!AnalyticsEventNames.IsKnown(eventName))
            {
                Debug.LogWarning($"Ignoring unknown analytics event '{eventName}'.");
                return;
            }

            var data = new AnalyticsEventData
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
                _queue.events.Add(data);
                int overflow = _queue.events.Count - MaxQueuedEvents;
                if (overflow > 0) _queue.events.RemoveRange(0, overflow);
                SaveQueueLocked();
            }
        }

        public async Task<int> FlushAsync()
        {
            if (string.IsNullOrWhiteSpace(_baseUrl)) return 0;

            AnalyticsEventData[] batch;
            lock (_gate)
            {
                if (_flushInProgress || _queue.events.Count == 0) return 0;
                _flushInProgress = true;
                int count = Math.Min(MaxBatchSize, _queue.events.Count);
                batch = _queue.events.GetRange(0, count).ToArray();
            }

            try
            {
                var payload = new AnalyticsBatchData { events = batch };
                string json = JsonUtility.ToJson(payload);
                using var request = new UnityWebRequest(_baseUrl + "/analytics/events", "POST");
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = 8;
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Accept", "application/json");

                UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                var completion = new TaskCompletionSource<bool>();
                operation.completed += _ => completion.TrySetResult(true);
                await completion.Task;

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.Log($"Analytics flush deferred: HTTP {(long)request.responseCode} {request.error}");
                    return 0;
                }

                var sentIds = new HashSet<string>();
                for (int i = 0; i < batch.Length; i++) sentIds.Add(batch[i].eventId);

                lock (_gate)
                {
                    _queue.events.RemoveAll(item => sentIds.Contains(item.eventId));
                    SaveQueueLocked();
                }
                return batch.Length;
            }
            catch (Exception error)
            {
                Debug.Log($"Analytics flush deferred: {error.Message}");
                return 0;
            }
            finally
            {
                lock (_gate) _flushInProgress = false;
            }
        }

        private AnalyticsQueueData LoadQueue()
        {
            if (!File.Exists(_queuePath)) return new AnalyticsQueueData();
            try
            {
                AnalyticsQueueData data = JsonUtility.FromJson<AnalyticsQueueData>(File.ReadAllText(_queuePath));
                if (data == null) data = new AnalyticsQueueData();
                if (data.events == null) data.events = new List<AnalyticsEventData>();
                if (data.events.Count > MaxQueuedEvents)
                    data.events.RemoveRange(0, data.events.Count - MaxQueuedEvents);
                return data;
            }
            catch (Exception error)
            {
                Debug.LogWarning($"Analytics queue load failed: {error.Message}");
                return new AnalyticsQueueData();
            }
        }

        private void SaveQueueLocked()
        {
            try
            {
                string temp = _queuePath + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(_queue));
                if (File.Exists(_queuePath)) File.Delete(_queuePath);
                File.Move(temp, _queuePath);
            }
            catch (Exception error)
            {
                Debug.LogWarning($"Analytics queue save failed: {error.Message}");
            }
        }

        private static AnalyticsParameterData[] ConvertParameters(IReadOnlyDictionary<string, object> parameters)
        {
            if (parameters == null || parameters.Count == 0) return Array.Empty<AnalyticsParameterData>();
            var result = new List<AnalyticsParameterData>(Math.Min(parameters.Count, 64));
            foreach (KeyValuePair<string, object> pair in parameters)
            {
                if (result.Count >= 64) break;
                if (string.IsNullOrWhiteSpace(pair.Key)) continue;
                string key = pair.Key.Length > 64 ? pair.Key.Substring(0, 64) : pair.Key;
                string value = ToInvariantString(pair.Value);
                if (value.Length > 512) value = value.Substring(0, 512);
                result.Add(new AnalyticsParameterData { key = key, value = value });
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
