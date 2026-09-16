using System;
using System.Collections.Generic;
using System.Reflection;
using DontGetSidetracked.Analytics;
using DontGetSidetracked.Core;
using DontGetSidetracked.Gameplay;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace DontGetSidetracked.Presentation
{
    /// <summary>
    /// Campaign reuses GameBootstrap's Training enum internally, but user-facing share cards must not say Training.
    /// This adapter temporarily replaces only the result-card button listener while a campaign result is active.
    /// </summary>
    public sealed class CampaignShareCardCoordinator : MonoBehaviour
    {
        private GameBootstrap _bootstrap;
        private ResultEnhancementCoordinator _result;
        private FieldInfo _stateField;
        private FieldInfo _scoreField;
        private FieldInfo _routeField;
        private FieldInfo _recordingField;
        private FieldInfo _cardButtonField;
        private MethodInfo _defaultShareMethod;
        private bool _campaignListenerInstalled;
        private bool _busy;
        private float _nextResolve;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (FindFirstObjectByType<CampaignShareCardCoordinator>() != null) return;
            var root = new GameObject("CampaignShareCardCoordinator");
            DontDestroyOnLoad(root);
            root.AddComponent<CampaignShareCardCoordinator>();
        }

        private void Update()
        {
            if ((_bootstrap == null || _result == null) && Time.unscaledTime >= _nextResolve)
            {
                _nextResolve = Time.unscaledTime + 0.25f;
                Resolve();
            }
            if (_bootstrap == null || _result == null || _cardButtonField == null) return;

            Button card = _cardButtonField.GetValue(_result) as Button;
            if (card == null) return;

            bool campaignResult = CampaignRuntimeCoordinator.IsCampaignActive &&
                                  string.Equals(_stateField?.GetValue(_bootstrap)?.ToString(), "Result", StringComparison.Ordinal);

            if (campaignResult && !_campaignListenerInstalled)
            {
                card.onClick.RemoveAllListeners();
                card.onClick.AddListener(ShareCampaignCard);
                Text label = card.GetComponentInChildren<Text>(true);
                if (label != null) label.text = "📸 КАРТОЧКА УРОВНЯ";
                _campaignListenerInstalled = true;
            }
            else if (!CampaignRuntimeCoordinator.IsCampaignActive && _campaignListenerInstalled)
            {
                RestoreDefaultListener(card);
                _campaignListenerInstalled = false;
            }
        }

        private void Resolve()
        {
            _bootstrap = FindFirstObjectByType<GameBootstrap>();
            _result = FindFirstObjectByType<ResultEnhancementCoordinator>();
            if (_bootstrap != null)
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                Type type = typeof(GameBootstrap);
                _stateField = type.GetField("_state", flags);
                _scoreField = type.GetField("_lastResultScore", flags);
                _routeField = type.GetField("_route", flags);
                _recordingField = type.GetField("_recording", flags);
            }
            if (_result != null)
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                Type type = typeof(ResultEnhancementCoordinator);
                _cardButtonField = type.GetField("_cardButton", flags);
                _defaultShareMethod = type.GetMethod("ShareCard", flags);
            }
        }

        private void RestoreDefaultListener(Button card)
        {
            card.onClick.RemoveAllListeners();
            if (_defaultShareMethod != null)
            {
                UnityAction action = () => _defaultShareMethod.Invoke(_result, null);
                card.onClick.AddListener(action);
            }
            Text label = card.GetComponentInChildren<Text>(true);
            if (label != null) label.text = "📸 КАРТОЧКА";
        }

        private void ShareCampaignCard()
        {
            if (_busy || _bootstrap == null || !CampaignRuntimeCoordinator.IsCampaignActive) return;
            _busy = true;
            Button card = _cardButtonField?.GetValue(_result) as Button;
            if (card != null) card.interactable = false;

            try
            {
                RouteDefinition route = _routeField?.GetValue(_bootstrap) as RouteDefinition;
                var recording = _recordingField?.GetValue(_bootstrap) as List<RecordedPoint>;
                double score = _scoreField?.GetValue(_bootstrap) is double value ? value : 0.0;
                int level = CampaignRuntimeCoordinator.CurrentLevelNumber;
                int chapter = CampaignRuntimeCoordinator.CurrentChapterNumber;
                if (route == null || recording == null || recording.Count < 2 || level <= 0)
                    throw new InvalidOperationException("Campaign result trajectory is unavailable.");

                var model = new ResultShareCardModel
                {
                    ModeLabel = "КАМПАНИЯ",
                    ChallengeLabel = $"УРОВЕНЬ {level} • {CampaignLevelCatalog.ChapterName(chapter)}",
                    Score = score,
                    Celebration = ScoreCelebrationPolicy.Evaluate(score),
                    ReferencePoints = route.ReferencePoints,
                    PlayerPoints = new List<RecordedPoint>(recording),
                    HasRivalScore = false,
                    RivalScore = 0.0
                };

                byte[] png = ResultShareCardRenderer.RenderPng(model);
                string text = $"Уровень {level} в НЕ СБЕЙСЯ! — {score:0.0}%. Сможешь точнее?";
                bool shared = NativeImageShare.Share(png, text);
                AnalyticsLifecycle.Service?.Track(AnalyticsEventNames.ShareCard, new Dictionary<string, object>
                {
                    ["mode"] = "Campaign",
                    ["level"] = level,
                    ["chapter"] = chapter,
                    ["score"] = score,
                    ["image_shared"] = shared
                });
            }
            catch (Exception error)
            {
                Debug.LogWarning($"Campaign share card unavailable: {error.Message}");
            }
            finally
            {
                _busy = false;
                if (card != null) card.interactable = true;
            }
        }
    }
}
