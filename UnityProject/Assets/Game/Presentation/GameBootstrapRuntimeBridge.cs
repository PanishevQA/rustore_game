using System;
using System.Reflection;
using UnityEngine;

namespace DontGetSidetracked.Presentation
{
    /// <summary>
    /// Narrow compatibility boundary for runtime coordinators that still need to observe/control
    /// GameBootstrap's legacy private state. Reflection is resolved once here instead of being
    /// repeated throughout Android lifecycle/navigation code.
    ///
    /// Keep new presentation code off private GameBootstrap members; this adapter can disappear
    /// once GameBootstrap exposes a dedicated runtime navigation contract.
    /// </summary>
    internal static class GameBootstrapRuntimeBridge
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        private static readonly FieldInfo ModeField = typeof(GameBootstrap).GetField("_mode", PrivateInstance);
        private static readonly FieldInfo StateField = typeof(GameBootstrap).GetField("_state", PrivateInstance);
        private static readonly FieldInfo PointerDownField = typeof(GameBootstrap).GetField("_pointerDown", PrivateInstance);
        private static readonly MethodInfo StartTutorialMethod = typeof(GameBootstrap).GetMethod("StartTutorial", PrivateInstance);

        private static bool _missingContractReported;

        public static bool IsHome(GameBootstrap bootstrap) =>
            string.Equals(ReadEnumName(bootstrap, ModeField), "Home", StringComparison.Ordinal);

        public static bool IsTutorial(GameBootstrap bootstrap) =>
            string.Equals(ReadEnumName(bootstrap, ModeField), "Tutorial", StringComparison.Ordinal);

        public static bool IsActiveRound(GameBootstrap bootstrap)
        {
            string state = ReadEnumName(bootstrap, StateField);
            return string.Equals(state, "Showing", StringComparison.Ordinal) ||
                   string.Equals(state, "Drawing", StringComparison.Ordinal);
        }

        public static void AbortToHome(GameBootstrap bootstrap)
        {
            if (bootstrap == null || IsHome(bootstrap)) return;
            ReportMissingContractIfNeeded();

            PointerDownField?.SetValue(bootstrap, false);
            bootstrap.StopAllCoroutines();
            CampaignRuntimeCoordinator.ReturnHome(bootstrap);
        }

        public static void RestartTutorial(GameBootstrap bootstrap)
        {
            if (bootstrap == null) return;
            ReportMissingContractIfNeeded();

            if (StartTutorialMethod == null)
            {
                Debug.LogError("GameBootstrap runtime bridge cannot restart Tutorial: StartTutorial was not found.");
                return;
            }

            StartTutorialMethod.Invoke(bootstrap, null);
        }

        private static string ReadEnumName(GameBootstrap bootstrap, FieldInfo field)
        {
            if (bootstrap == null || field == null)
            {
                ReportMissingContractIfNeeded();
                return string.Empty;
            }

            return field.GetValue(bootstrap)?.ToString() ?? string.Empty;
        }

        private static void ReportMissingContractIfNeeded()
        {
            if (_missingContractReported ||
                (ModeField != null && StateField != null && PointerDownField != null && StartTutorialMethod != null))
                return;

            _missingContractReported = true;
            Debug.LogError(
                "GameBootstrap private runtime contract changed. Mobile lifecycle fallback may be degraded; " +
                "update GameBootstrapRuntimeBridge before release.");
        }
    }
}
