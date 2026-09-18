using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DontGetSidetracked.Core;
using DontGetSidetracked.Gameplay;
using DontGetSidetracked.Network;
using DontGetSidetracked.Social;
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
        private static readonly FieldInfo LastScoreBreakdownField = typeof(GameBootstrap).GetField("_lastScoreBreakdown", PrivateInstance);
        private static readonly FieldInfo LastDailyScoreField = typeof(GameBootstrap).GetField("_lastDailyScore", PrivateInstance);
        private static readonly FieldInfo RouteField = typeof(GameBootstrap).GetField("_route", PrivateInstance);
        private static readonly FieldInfo RecordingField = typeof(GameBootstrap).GetField("_recording", PrivateInstance);
        private static readonly FieldInfo ApiField = typeof(GameBootstrap).GetField("_api", PrivateInstance);
        private static readonly FieldInfo NavigationRevisionField = typeof(GameBootstrap).GetField("_navigationRevision", PrivateInstance);
        private static readonly FieldInfo TrainingIndexField = typeof(GameBootstrap).GetField("_trainingIndex", PrivateInstance);
        private static readonly FieldInfo ReferenceGraphicField = typeof(GameBootstrap).GetField("_referenceGraphic", PrivateInstance);

        private static readonly MethodInfo StartTutorialMethod = typeof(GameBootstrap).GetMethod("StartTutorial", PrivateInstance);
        private static readonly MethodInfo BeginNavigationMethod = typeof(GameBootstrap).GetMethod("BeginNavigation", PrivateInstance);
        private static readonly MethodInfo BeginRouteMethod = typeof(GameBootstrap).GetMethod("BeginRoute", PrivateInstance);
        private static readonly MethodInfo ShowHomeMethod = typeof(GameBootstrap).GetMethod("ShowHome", PrivateInstance);
        private static readonly MethodInfo StartDailyMethod = typeof(GameBootstrap).GetMethod("StartDaily", PrivateInstance);
        private static readonly MethodInfo StartTrainingMethod = typeof(GameBootstrap).GetMethod("StartTraining", PrivateInstance);
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

        public static bool IsPlainHome(GameBootstrap bootstrap)
        {
            if (!IsIdleHome(bootstrap)) return false;

            Text title = Title(bootstrap);
            Text status = Status(bootstrap);
            if (title == null || !string.Equals(title.text, "НЕ СБЕЙСЯ!", StringComparison.Ordinal))
                return false;

            return status == null ||
                   status.text == null ||
                   status.text.IndexOf("ГОТОВИМ ВЫЗОВ", StringComparison.OrdinalIgnoreCase) < 0;
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

            return ReadDouble(bootstrap, LastResultScoreField);
        }

        public static int NavigationRevision(GameBootstrap bootstrap)
        {
            if (bootstrap == null || NavigationRevisionField == null)
            {
                ReportMissingContractIfNeeded();
                return int.MinValue;
            }

            return NavigationRevisionField.GetValue(bootstrap) is int value ? value : int.MinValue;
        }

        public static bool IsCurrentNavigation(GameBootstrap bootstrap, int revision) =>
            bootstrap != null && revision != int.MinValue && NavigationRevision(bootstrap) == revision;

        public static bool TryCaptureResult(GameBootstrap bootstrap, out GameBootstrapResultSnapshot snapshot)
        {
            snapshot = null;
            if (bootstrap == null || !ResultContractAvailable())
            {
                ReportMissingContractIfNeeded();
                return false;
            }

            try
            {
                List<RecordedPoint> recording = RecordingField.GetValue(bootstrap) as List<RecordedPoint>;
                snapshot = new GameBootstrapResultSnapshot
                {
                    ModeName = ReadEnumName(bootstrap, ModeField),
                    DailyCompleted = ReadBool(bootstrap, DailyCompletedField),
                    LastResultScore = ReadDouble(bootstrap, LastResultScoreField),
                    LastScoreBreakdown = LastScoreBreakdownField != null && LastScoreBreakdownField.GetValue(bootstrap) is ScoreBreakdown breakdown
                        ? breakdown
                        : default,
                    LastDailyScore = ReadDouble(bootstrap, LastDailyScoreField),
                    Daily = DailyField.GetValue(bootstrap) as DailyChallengeDefinition,
                    Route = RouteField.GetValue(bootstrap) as RouteDefinition,
                    Recording = recording == null ? new List<RecordedPoint>() : new List<RecordedPoint>(recording),
                    DuelSession = DuelSessionField.GetValue(bootstrap) as DuelSession,
                    Api = ApiField.GetValue(bootstrap) as UnityGameApi,
                    Save = SaveField.GetValue(bootstrap) as SaveData,
                    PrimaryButton = PrimaryField.GetValue(bootstrap) as Button,
                    NavigationRevision = NavigationRevision(bootstrap)
                };
                return true;
            }
            catch (Exception error)
            {
                Debug.LogError($"GameBootstrap runtime bridge failed to capture result snapshot: {error.Message}");
                snapshot = null;
                return false;
            }
        }

        public static bool StartTrainingDifficulty(GameBootstrap bootstrap, int difficulty)
        {
            if (bootstrap == null || difficulty < 0 || difficulty > 2 ||
                TrainingIndexField == null || StartTrainingMethod == null)
            {
                ReportMissingContractIfNeeded();
                return false;
            }

            try
            {
                // GameBootstrap increments _trainingIndex before selecting (_trainingIndex % 3).
                int beforeIncrement = difficulty == 0 ? 2 : difficulty - 1;
                TrainingIndexField.SetValue(bootstrap, beforeIncrement);
                StartTrainingMethod.Invoke(bootstrap, null);
                return true;
            }
            catch (Exception error)
            {
                Debug.LogError($"GameBootstrap runtime bridge failed to start Training: {error.Message}");
                return false;
            }
        }

        public static bool TryCaptureHintContext(GameBootstrap bootstrap, out GameBootstrapHintContext context)
        {
            context = null;
            if (bootstrap == null || !TrainingHintContractAvailable())
            {
                ReportMissingContractIfNeeded();
                return false;
            }

            context = new GameBootstrapHintContext
            {
                ModeName = ReadEnumName(bootstrap, ModeField),
                IsDrawing = string.Equals(ReadEnumName(bootstrap, StateField), "Drawing", StringComparison.Ordinal),
                PointerDown = ReadBool(bootstrap, PointerDownField),
                Route = RouteField.GetValue(bootstrap) as RouteDefinition,
                Daily = DailyField.GetValue(bootstrap) as DailyChallengeDefinition
            };
            return true;
        }

        public static bool ShowHintReference(GameBootstrap bootstrap, RouteDefinition route)
        {
            if (bootstrap == null || route == null || ReferenceGraphicField == null)
            {
                ReportMissingContractIfNeeded();
                return false;
            }

            RouteDefinition current = RouteField?.GetValue(bootstrap) as RouteDefinition;
            RouteGraphic reference = ReferenceGraphicField.GetValue(bootstrap) as RouteGraphic;
            if (!ReferenceEquals(current, route) || reference == null) return false;

            reference.color = new Color(0.1f, 0.9f, 1f, 0.9f);
            reference.SetPoints(route.ReferencePoints);
            return true;
        }

        public static void ClearHintReferenceIfCurrentDrawing(GameBootstrap bootstrap, RouteDefinition route)
        {
            if (bootstrap == null || route == null || ReferenceGraphicField == null) return;
            string state = ReadEnumName(bootstrap, StateField);
            RouteDefinition current = RouteField?.GetValue(bootstrap) as RouteDefinition;
            if (!string.Equals(state, "Drawing", StringComparison.Ordinal) || !ReferenceEquals(current, route)) return;
            (ReferenceGraphicField.GetValue(bootstrap) as RouteGraphic)?.Clear();
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

        private static bool ReadBool(GameBootstrap bootstrap, FieldInfo field) =>
            field?.GetValue(bootstrap) is bool value && value;

        private static double ReadDouble(GameBootstrap bootstrap, FieldInfo field) =>
            field?.GetValue(bootstrap) is double value ? value : 0.0;

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

        private static bool ResultContractAvailable()
        {
            return ModeField != null && StateField != null && DailyCompletedField != null &&
                   LastResultScoreField != null && LastDailyScoreField != null && DailyField != null &&
                   RouteField != null && RecordingField != null && DuelSessionField != null &&
                   ApiField != null && SaveField != null && PrimaryField != null && NavigationRevisionField != null;
        }

        private static bool TrainingHintContractAvailable()
        {
            return ModeField != null && StateField != null && PointerDownField != null &&
                   RouteField != null && DailyField != null && TrainingIndexField != null &&
                   ReferenceGraphicField != null && StartTrainingMethod != null;
        }

        private static void ReportMissingContractIfNeeded()
        {
            if (_missingContractReported ||
                (ModeField != null && StateField != null && PointerDownField != null &&
                 StartTutorialMethod != null && CampaignContractAvailable() && ResultContractAvailable() &&
                 TrainingHintContractAvailable()))
                return;

            _missingContractReported = true;
            Debug.LogError(
                "GameBootstrap private runtime contract changed. Compatibility fallback is active; " +
                "update GameBootstrapRuntimeBridge before release.");
        }
    }

    internal sealed class GameBootstrapResultSnapshot
    {
        public string ModeName;
        public bool DailyCompleted;
        public double LastResultScore;
        public ScoreBreakdown LastScoreBreakdown;
        public double LastDailyScore;
        public DailyChallengeDefinition Daily;
        public RouteDefinition Route;
        public List<RecordedPoint> Recording;
        public DuelSession DuelSession;
        public UnityGameApi Api;
        public SaveData Save;
        public Button PrimaryButton;
        public int NavigationRevision;
    }

    internal sealed class GameBootstrapHintContext
    {
        public string ModeName;
        public bool IsDrawing;
        public bool PointerDown;
        public RouteDefinition Route;
        public DailyChallengeDefinition Daily;
    }
}
