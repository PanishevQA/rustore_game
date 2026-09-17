using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace DontGetSidetracked.Presentation
{
    /// <summary>
    /// Offline-first crash/error journal. Keeps a small diagnostics file on-device and never uploads it.
    /// Intended for QA/support diagnostics when no remote crash service is configured.
    /// </summary>
    public sealed class LocalCrashLog : MonoBehaviour
    {
        private const long MaxBytes = 256 * 1024;
        private const long KeepBytesAfterTrim = 128 * 1024;
        private static readonly object Gate = new object();
        private static string _path;
        private static bool _subscribed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            // Covers Editor Play Mode without domain reload and avoids duplicate threaded log handlers.
            if (_subscribed)
                Application.logMessageReceivedThreaded -= OnLogMessage;
            _subscribed = false;
            _path = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (_subscribed) return;
            _path = Path.Combine(Application.persistentDataPath, "crash-local.log");
            _subscribed = true;
            Application.logMessageReceivedThreaded += OnLogMessage;

            var root = new GameObject("LocalCrashLog");
            DontDestroyOnLoad(root);
            root.AddComponent<LocalCrashLog>();
        }

        private static void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert) return;
            if (string.IsNullOrWhiteSpace(_path)) return;

            try
            {
                string entry = BuildEntry(condition, stackTrace, type);
                lock (Gate)
                {
                    TrimIfNeeded();
                    File.AppendAllText(_path, entry, Encoding.UTF8);
                }
            }
            catch
            {
                // Never log from inside the log handler: that could recurse indefinitely.
            }
        }

        private static string BuildEntry(string condition, string stackTrace, LogType type)
        {
            var builder = new StringBuilder(1024);
            builder.Append('[')
                .Append(DateTime.UtcNow.ToString("O"))
                .Append("] ")
                .Append(type)
                .AppendLine();
            builder.AppendLine(Clamp(condition, 4096));
            if (!string.IsNullOrWhiteSpace(stackTrace)) builder.AppendLine(Clamp(stackTrace, 12_000));
            builder.AppendLine("---");
            return builder.ToString();
        }

        private static void TrimIfNeeded()
        {
            if (!File.Exists(_path)) return;
            var info = new FileInfo(_path);
            if (info.Length <= MaxBytes) return;

            byte[] bytes = File.ReadAllBytes(_path);
            int keep = (int)Math.Min(KeepBytesAfterTrim, bytes.Length);
            int start = bytes.Length - keep;

            // Advance to the next newline so the retained file starts at a readable entry boundary.
            while (start < bytes.Length && bytes[start] != (byte)'\n') start++;
            if (start < bytes.Length) start++;

            int length = bytes.Length - start;
            var retained = new byte[length];
            Buffer.BlockCopy(bytes, start, retained, 0, length);
            File.WriteAllBytes(_path, retained);
        }

        private static string Clamp(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength) + "…";
        }

        private void OnDestroy()
        {
            if (!_subscribed) return;
            Application.logMessageReceivedThreaded -= OnLogMessage;
            _subscribed = false;
        }
    }
}
