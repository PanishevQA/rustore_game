using System;
using System.Collections;
using System.Reflection;
using DontGetSidetracked.Core;
using DontGetSidetracked.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace DontGetSidetracked.Presentation
{
    /// <summary>
    /// Narrow compatibility boundary for runtime coordinators that still need to observe/control
    /// GameBootstrap's legacy private state. Reflection is resolved once here instead of being
    /// repeated throughout presentation/lifecycle code.
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
        private static readonly FieldInfo DailyCompletedField = typeof(GameBootstrap).GetField("_dailyCompleted", PrivateInstance);
        private static readonly FieldInfo DuelSessionField = typeof(GameBootstrap).GetField("_duelSession", PrivateInstance);
        private static readonly FieldInfo DailySessionField = typeof(GameBootstrap).GetField("_dailySession", PrivateInstance);
        private static readonly FieldInfo DailyField = typeof(GameBootstrap).GetField("_daily", PrivateInstance);
        private static readonly FieldInfo SaveField = typeof(GameBootstrap).GetField("_save", PrivateInstance);
        private static readonly FieldInfo TitleField = typeof(GameBootstrap).GetField("_title", PrivateInstance);
        private static readonly FieldInfo StatusField = typeof(GameBootstrap).GetField("_status", PrivateInstance);
        private static readonly FieldInfo PrimaryField = typeof(GameBootstrap).GetField("_primary", PrivateInstance);
        private static readonly FieldInfo SecondaryField = typeof(GameBootstrap).GetField("_secondary", PrivateInstance);
        private static readonly FieldInfo ShareField = typeof(GameBootstrap).GetField("_share", PrivateInstance);
        private static readonly FieldInfo LastResultScoreField = typeof(GameBootstrap).GetField("_lastResultScore", PrivateInstance);

        private static readonly MethodInfo StartTutorialMethod = typeof(GameBootstrap).GetMethod("StartTutorial", PrivateInstance);
        private static readonly MethodInfo BeginNavigationMethod = typeof(GameBootstrap).GetMethod("BeginNavigation", PrivateInstance);
        private static readonly MethodInfo BeginRouteMethod = typeof(GameBootstrap).GetMethod("BeginRoute", PrivateInstance);
        private static readonly MethodInfo ShowHomeMethod = typeof(GameBootstrap).GetMethod("ShowHome", PrivateInstance);
        private static readonly MethodInfo StartDailyMethod = typeof(GameBootstrap).GetMethod("StartDaily", PrivateInstance);
        private static readonly MethodInfo ShareCurrentResultMethod = typeof(GameBootstrap).GetMethod("ShareCurrentResult", PrivateInstance);

        private static bool _missingContractReported;

        public static bool IsHome(GameBootstrap bootstrap) =>
            string.Equals(ReadEnumName(bootstrap, ModeField), "Home", StringComparison.Ordinal);

        public static bool IsTutorial(GameBootstrap bootstrap) =>
            string.Equals(ReadEnumName(bootstrap, ModeField), "Tutorial", StringComparison.Ordinal);

        public static bool IsIdleHome(GameBootstrap bootstrap)
        {
            if (!IsHome(bootstrap)) return false;
            if (StateField == null)
            {
                ReportMissingContractIfNeeded();
                return false;
            }

            return string.Equals(ReadEnumName(bootstrap, StateField), "Idle", StringComparison.Ordinal);
        }

        public static bool IsResult(GameBootstrap bootstrap)
        {
            if (bootstrap == null || StateField == null)
            {
                ReportMissingContractIfNeeded();
                return false;
            }

            return string.Equals(ReadEnumName(bootstrap, StateField), "Result", StringComparison.Ordinal);
        }

        public static bool IsActiveRound(GameBootstrap bootstrap)
        {
            if (bootstrap == null) return false;
            if (StateField == null)
            {
                ReportMissingContractIfNeeded();
                // Fail safe: if the legacy state contract changed, never allow a non-Home screen
                // to resume as if an interrupted gesture/preview was still valid.
                return !IsHome(bootstrap);
            }

            string state = ReadEnumName(bootstrap, StateField);
            return string.Equals(state, "Showing", StringComparison.Ordinal) ||
                   string.Equals(state, "Drawing", StringComparison.Ordinal);
        }

        public static Text Title(GameBootstrap bootstrap) => ReadField<Text>(bootstrap, TitleField);

        public static Text Status(GameBootstrap bootstrap) => ReadField<Text>(bootstrap, StatusField);

        public static Button PrimaryButton(GameBootstrap bootstrap) => ReadField<Button>(bootstrap, PrimaryField);

        public static Button SecondaryButton(GameBootstrap bootstrap) => ReadField<Button>(bootstrap, SecondaryField);

        public static Button ShareButton(GameBootstrap bootstrap) => ReadField<Button>(bootstrap, ShareField);

        public static SaveData Save(GameBootstrap bootstrap) => ReadField<SaveData>(bootstrap, SaveField);

        public static double LastResultScore(GameBootstrap bootstrap)
        {
            if (bootstrap == null || LastResultScoreField == null)
            {
                ReportMissingContractIfNeeded();
                return 0.0;
            }

            object value = LastResultScoreField.GetValue(bootstrap);
            return value == null ? 0.0 : Convert.ToDouble(value);
        }

        public static bool PrepareCampaign(GameBootstrap bootstrap)
        {
            if (bootstrap == null || !CampaignContractAvailable())
            {
                ReportMissingContractIfNeeded();
                return false;
            }

            try
            {
                BeginNavigationMethod.Invoke(bootstrap, null);
                SetEnum(bootstrap, ModeField, "Training");
                SetEnum(bootstrap, StateField, "Idle");
                DailyCompletedField.SetValue(bootstrap, false);
                DuelSessionField.SetValue(bootstrap, null);
                DailySessionField.SetValue(bootstrap, null);
                DailyField.SetValue(bootstrap, null);
                return true;
            }
            catch (Exception error)
            {
                Debug.LogError($"GameBootstrap runtime bridge failed to prepare Campaign: {error.Message}");
                return false;
            }
        }

        public static bool BeginRoute(GameBootstrap bootstrap, RouteDefinition route)
        {
            if (bootstrap == null || route == null || BeginRouteMethod == null)
            {
                ReportMissingContractIfNeeded();
                return false;
            }

            try
            {
                object routineObject = BeginRouteMethod.Invoke(bootstrap, new object[] { route });
                if (routineObject is not IEnumerator routine)
                {
                    Debug.LogError("GameBootstrap runtime bridge expected BeginRoute to return IEnumerator.");
                    return false;
                }

                bootstrap.StartCoroutine(routine);
                return true;
            }
            catch (Exception error)
            {
                Debug.LogError($"GameBootstrap runtime bridge failed to begin Campaign route: {error.Message}");
                return false;
            }
        }

        public static void ReplaceSave(GameBootstrap bootstrap, SaveData save)
        {
            if (bootstrap == null || save == null || SaveField == null)
            {
                ReportMissingContractIfNeeded();
                return;
            }

            SaveField.SetValue(bootstrap, save);
        }

        public static void ShowHome(GameBootstrap bootstrap) => InvokeNoArgs(bootstrap, ShowHomeMethod, "ShowHome");

        public static void StartDaily(GameBootstrap bootstrap) => InvokeNoArgs(bootstrap, StartDailyMethod, "StartDaily");

        public static void ShareCurrentResult(GameBootstrap bootstrap) =>
            InvokeNoArgs(bootstrap, ShareCurrentResultMethod, "ShareCurrentResult");

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

            try
            {
                StartTutorialMethod.Invoke(bootstrap, null);
            }
            catch (Exception error)
            {
                Debug.LogError($"GameBootstrap runtime bridge failed to restart Tutorial: {error.Message}");
            }
        }

        private static T ReadField<T>(GameBootstrap bootstrap, FieldInfo field) where T : class
        {
            if (bootstrap == null || field == null)
            {
                ReportMissingContractIfNeeded();
                return null;
            }

            return field.GetValue(bootstrap) as T;
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

        private static void SetEnum(GameBootstrap bootstrap, FieldInfo field, string name)
        {
            object value = Enum.Parse(field.FieldType, name);
            field.SetValue(bootstrap, value);
        }

        private static void InvokeNoArgs(GameBootstrap bootstrap, MethodInfo method, string methodName)
        {
            if (bootstrap == null || method == null)
            {
                ReportMissingContractIfNeeded();
                return;
            }

            try
            {
                method.Invoke(bootstrap, null);
            }
            catch (Exception error)
            {
                Debug.LogError($"GameBootstrap runtime bridge failed to invoke {methodName}: {error.Message}");
            }
        }

        private static bool CampaignContractAvailable()
        {
            return ModeField != null && StateField != null && DailyCompletedField != null &&
                   DuelSessionField != null && DailySessionField != null && DailyField != null &&
                   SaveField != null && TitleField != null && StatusField != null &&
                   PrimaryField != null && SecondaryField != null && ShareField != null &&
                   LastResultScoreField != null && BeginNavigationMethod != null && BeginRouteMethod != null &&
                   ShowHomeMethod != null && StartDailyMethod != null && ShareCurrentResultMethod != null;
        }

        private static void ReportMissingContractIfNeeded()
        {
            if (_missingContractReported ||
                (ModeField != null && StateField != null && PointerDownField != null &&
                 StartTutorialMethod != null && CampaignContractAvailable()))
                return;

            _missingContractReported = true;
            Debug.LogError(
                "GameBootstrap private runtime contract changed. Compatibility fallback is active; " +
                "update GameBootstrapRuntimeBridge before release.");
        }
    }
}
