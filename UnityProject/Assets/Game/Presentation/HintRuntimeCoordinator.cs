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
            if (visible) _label.text = $"💡 ПОКАЗАТЬ ЕЩЁ РАЗ   •   {save.Hints}";
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

            _label.text = $"💡 ПОКАЗАТЬ ЕЩЁ РАЗ   •   {save.Hints}";
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

            var go = new GameObject("UseHint", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(_canvas.transform, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.18f, 0.205f);
            rect.anchorMax = new Vector2(0.82f, 0.245f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = new Color(0.10f, 0.18f, 0.27f, 0.96f);
            _button = go.GetComponent<Button>();
            _button.onClick.AddListener(UseHint);

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            RectTransform textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            _label = textGo.GetComponent<Text>();
            _label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _label.fontSize = 28;
            _label.alignment = TextAnchor.MiddleCenter;
            _label.color = Color.white;
            _label.resizeTextForBestFit = true;
            _label.resizeTextMinSize = 16;
            _label.resizeTextMaxSize = 28;
            _label.raycastTarget = false;
        }

        private void SetVisible(bool visible)
        {
            if (_canvas != null && _canvas.activeSelf != visible) _canvas.SetActive(visible);
        }
    }
}
