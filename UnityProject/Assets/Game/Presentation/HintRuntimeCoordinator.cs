using System;
using System.Collections;
using System.Collections.Generic;
using DontGetSidetracked.Analytics;
using DontGetSidetracked.Core;
using DontGetSidetracked.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace DontGetSidetracked.Presentation
{
    /// <summary>
    /// Turns locally owned hints into an actual gameplay affordance without coupling Gameplay/Daily to Unity UI.
    /// Hints can reveal the current route once more before the gesture starts in Daily, Campaign or Training.
    /// Duel and Tutorial intentionally do not expose hints.
    /// </summary>
    public sealed class HintRuntimeCoordinator : MonoBehaviour
    {
        private const float RevealSeconds = 1.0f;

        private JsonFileSaveRepository _saveRepository;
        private GameBootstrap _bootstrap;
        private GameObject _canvas;
        private Button _button;
        private Text _label;
        private float _nextPoll;
        private int _revealGeneration;
        private RouteDefinition _revealedRoute;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (FindFirstObjectByType<HintRuntimeCoordinator>() != null) return;
            var root = new GameObject("HintRuntimeCoordinator");
            DontDestroyOnLoad(root);
            root.AddComponent<HintRuntimeCoordinator>();
        }

        private void Awake()
        {
            _saveRepository = new JsonFileSaveRepository();
            BuildUi();
            ResolveBootstrap();
            SetVisible(false);
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextPoll) return;
            _nextPoll = Time.unscaledTime + 0.2f;

            if (_bootstrap == null) ResolveBootstrap();
            if (_bootstrap == null)
            {
                SetVisible(false);
                return;
            }

            if (IsPointerDown() && _revealedRoute != null)
                ClearRevealIfStillDrawing();

            bool candidate = CanOfferHint(out _, out RouteDefinition route);
            if (!candidate)
            {
                SetVisible(false);
                return;
            }

            SaveData save = _saveRepository.Load();
            bool visible = save.Hints > 0 && route != null && !IsMetaPanelOpen();
            SetVisible(visible);
            if (visible) _label.text = $"ПОКАЗАТЬ МАРШРУТ ЕЩЁ РАЗ  •  {save.Hints}";
        }

        private void UseHint()
        {
            if (!CanOfferHint(out string mode, out RouteDefinition route) || route == null) return;

            SaveData save = _saveRepository.Load();
            if (save.Hints <= 0) return;

            // Confirm the current route/reference contract before consuming local inventory.
            if (!GameBootstrapRuntimeBridge.ShowHintReference(_bootstrap, route)) return;

            save.Hints--;
            _saveRepository.Save(save);

            if (string.Equals(mode, "Daily", StringComparison.Ordinal) &&
                GameBootstrapRuntimeBridge.TryCaptureHintContext(_bootstrap, out GameBootstrapHintContext context) &&
                context.Daily != null)
            {
                ChallengeAssistanceTracker.MarkAssisted(context.Daily.ChallengeId);
            }

            _revealedRoute = route;
            _revealGeneration++;
            int generation = _revealGeneration;
            StartCoroutine(HideReferenceAfterDelay(route, generation));

            AnalyticsLifecycle.Service?.Track(AnalyticsEventNames.HintUsed,
                new Dictionary<string, object>
                {
                    ["mode"] = CampaignRuntimeCoordinator.IsCampaignActive ? "Campaign" : mode,
                    ["difficulty"] = route.Difficulty.ToString(),
                    ["hints_remaining"] = save.Hints,
                    ["source"] = "local_inventory"
                });

            _label.text = $"ПОКАЗАТЬ МАРШРУТ ЕЩЁ РАЗ  •  {save.Hints}";
            if (save.Hints <= 0) SetVisible(false);
        }

        private IEnumerator HideReferenceAfterDelay(RouteDefinition route, int generation)
        {
            yield return new WaitForSecondsRealtime(RevealSeconds);
            if (generation != _revealGeneration || !ReferenceEquals(route, _revealedRoute)) yield break;
            ClearRevealIfStillDrawing();
        }

        private void ClearRevealIfStillDrawing()
        {
            RouteDefinition revealed = _revealedRoute;
            _revealedRoute = null;
            _revealGeneration++;
            if (revealed == null || _bootstrap == null) return;
            GameBootstrapRuntimeBridge.ClearHintReferenceIfCurrentDrawing(_bootstrap, revealed);
        }

        private bool CanOfferHint(out string mode, out RouteDefinition route)
        {
            mode = string.Empty;
            route = null;
            if (_bootstrap == null ||
                !GameBootstrapRuntimeBridge.TryCaptureHintContext(_bootstrap, out GameBootstrapHintContext context))
                return false;

            mode = context.ModeName;
            if (!context.IsDrawing || context.PointerDown) return false;
            if (!string.Equals(mode, "Daily", StringComparison.Ordinal) &&
                !string.Equals(mode, "Training", StringComparison.Ordinal)) return false;

            route = context.Route;
            return route != null;
        }

        private bool IsPointerDown()
        {
            return _bootstrap != null &&
                   GameBootstrapRuntimeBridge.TryCaptureHintContext(_bootstrap, out GameBootstrapHintContext context) &&
                   context.PointerDown;
        }

        private static bool IsMetaPanelOpen()
        {
            MetaMenuOverlay meta = FindFirstObjectByType<MetaMenuOverlay>();
            return meta != null && meta.IsPanelOpen;
        }

        private void ResolveBootstrap()
        {
            _bootstrap = FindFirstObjectByType<GameBootstrap>();
        }

        private void BuildUi()
        {
            _canvas = new GameObject("HintCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvas.transform.SetParent(transform, false);
            Canvas canvas = _canvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 34;
            CanvasScaler scaler = _canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            _button = ReleaseUiKit.Button(
                _canvas.transform,
                "UseHint",
                "ПОКАЗАТЬ МАРШРУТ ЕЩЁ РАЗ",
                new Vector2(0.18f, 0.195f),
                new Vector2(0.82f, 0.247f),
                new Color(0.055f, 0.080f, 0.140f, 0.98f),
                ReleaseUiKit.Gold,
                23,
                UseHint);

            _label = _button.GetComponentInChildren<Text>(true);
            if (_label != null)
            {
                _label.alignment = TextAnchor.MiddleCenter;
                _label.fontStyle = FontStyle.Bold;
            }

            Outline outline = _button.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(ReleaseUiKit.Gold.r, ReleaseUiKit.Gold.g, ReleaseUiKit.Gold.b, 0.20f);
            outline.effectDistance = new Vector2(2f, -2f);
        }

        private void SetVisible(bool visible)
        {
            if (_canvas != null && _canvas.activeSelf != visible) _canvas.SetActive(visible);
        }
    }
}
