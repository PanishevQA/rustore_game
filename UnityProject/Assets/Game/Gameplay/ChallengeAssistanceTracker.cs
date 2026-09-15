using System;
using System.Collections.Generic;

namespace DontGetSidetracked.Gameplay
{
    /// <summary>
    /// Pure C# session marker for gameplay assistance such as revealing the reference route again.
    /// Keeps optional online submission honest without coupling Gameplay/Daily to Unity UI.
    /// </summary>
    public static class ChallengeAssistanceTracker
    {
        private static readonly HashSet<string> Assisted = new HashSet<string>(StringComparer.Ordinal);

        public static void MarkAssisted(string challengeId)
        {
            if (!string.IsNullOrWhiteSpace(challengeId)) Assisted.Add(challengeId);
        }

        public static bool IsAssisted(string challengeId) =>
            !string.IsNullOrWhiteSpace(challengeId) && Assisted.Contains(challengeId);

        public static void Clear(string challengeId)
        {
            if (!string.IsNullOrWhiteSpace(challengeId)) Assisted.Remove(challengeId);
        }

        public static void ResetAllForTests() => Assisted.Clear();
    }
}
