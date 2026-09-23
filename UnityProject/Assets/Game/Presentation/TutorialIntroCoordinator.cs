using System.Collections.Generic;
using DontGetSidetracked.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DontGetSidetracked.Presentation
{
    /// <summary>
    /// First-launch tutorial introduction matching the approved release concept.
    /// Presentation-only: the existing GameBootstrap tutorial remains the source of
    /// truth for route generation, input, scoring and tutorial completion.
    /// </summary>
    [DefaultExecutionOrder(12000)]
    public sealed class TutorialIntroCoordinator : MonoBehaviour
    {
        private GameBootstrap _bootstrap;
        private JsonFileSaveRepository _repository;
        private GameObject _canvas;
        private GameObject _content;

        public bool IsOpen => _content != null && _content.activeSelf;

        public static void OpenFor(GameBootstrap bootstrap)
        {
            if (bootstrap == null) return;

            TutorialIntroCoordinator coordinator = FindFirstObjectByType<TutorialIntroCoordinator>();
            if (coordinator == null)
            {
                var root = new GameObject("TutorialIntroCoordinator");
                DontDestroyOnLoad(root);
                coordinator = root.AddComponent<TutorialIntroCoordinator>();
            }

            coordinator.Open(bootstrap);
        }

        private void Awake()
        {
            _repository = new JsonFileSaveRepository();
        }

        public void Open(GameBootstrap bootstrap)
        {
            _bootstrap = bootstrap;
            if (_canvas == null) BuildUi();
            _canvas.SetActive(true);
            _content.SetActive(true);
        }

        public void HandleBack()
        {
            if (!IsOpen) return;

            SaveData save = _repository.Load();
            if (save != null && save.TutorialCompleted)
            {
                Close();
                GameBootstrapRuntimeBridge.AbortToHome(_bootstrap);
                return;
            }

            Application.Quit();
        }

        private void BeginTutorial()
        {
            if (_bootstrap == null) return;
            Close();
            GameBootstrapRuntimeBridge.RestartTutorial(_bootstrap);
        }

        private void Close()
        {
            if (_content != null) _content.SetActive(false);
            if (_canvas != null) _canvas.SetActive(false);
        }

        private void BuildUi()
        {
            _canvas = new GameObject("TutorialIntroCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvas.transform.SetParent(transform, false);

            Canvas canvas = _canvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;

            CanvasScaler scaler = _canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            ReleaseUiComponents.Backdrop(_canvas.transform, "VisualBackground");

            _content = new GameObject("TutorialIntroContent", typeof(RectTransform));
            _content.transform.SetParent(_canvas.transform, false);
            ReleaseUiKit.Stretch(_content.GetComponent<RectTransform>());

            Button back = ReleaseUiComponents.SecondaryButton(
                _content.transform,
                "TutorialBack",
                "←",
                new Vector2(0.055f, 0.900f),
                new Vector2(0.145f, 0.958f),
                HandleBack,
                32);
            Image backImage = back.GetComponent<Image>();
            if (backImage != null)
                backImage.color = new Color(0.020f, 0.060f, 0.110f, 0.94f);

            Text title = ReleaseUiKit.TextBlock(
                _content.transform,
                "TutorialTitle",
                "Давай попробуем!",
                42,
                TextAnchor.MiddleCenter,
                new Vector2(0.16f, 0.790f),
                new Vector2(0.84f, 0.865f),
                ReleaseUiComponents.Text,
                FontStyle.Bold);
            ReleaseUiKit.AddTextShadow(title, 0.52f, -3f);

            Text intro = ReleaseUiKit.TextBlock(
                _content.transform,
                "TutorialIntro",
                "Сначала ты увидишь маршрут\nнесколько секунд…",
                24,
                TextAnchor.MiddleCenter,
                new Vector2(0.12f, 0.715f),
                new Vector2(0.88f, 0.790f),
                ReleaseUiComponents.Muted);
            intro.lineSpacing = 0.94f;

            Image board = ReleaseUiComponents.GlassCard(
                _content.transform,
                "TutorialRouteCard",
                new Vector2(0.080f, 0.405f),
                new Vector2(0.920f, 0.700f),
                ReleaseUiComponents.Cyan,
                true);
            board.color = new Color(0.010f, 0.038f, 0.078f, 0.985f);
            BuildGrid(board.transform);
            BuildPreviewRoute(board.transform);

            Text disappear = ReleaseUiKit.TextBlock(
                _content.transform,
                "TutorialDisappear",
                "…затем он исчезнет.",
                21,
                TextAnchor.MiddleCenter,
                new Vector2(0.14f, 0.350f),
                new Vector2(0.86f, 0.395f),
                ReleaseUiComponents.Muted);

            Text instruction = ReleaseUiKit.TextBlock(
                _content.transform,
                "TutorialInstruction",
                "Повтори его по памяти\nодним движением пальца!",
                27,
                TextAnchor.MiddleCenter,
                new Vector2(0.10f, 0.265f),
                new Vector2(0.90f, 0.350f),
                ReleaseUiComponents.Text,
                FontStyle.Bold);
            instruction.lineSpacing = 0.94f;

            BuildDots(_content.transform);

            ReleaseUiComponents.PrimaryButton(
                _content.transform,
                "TutorialStart",
                "НАЧАТЬ",
                new Vector2(0.080f, 0.070f),
                new Vector2(0.920f, 0.155f),
                BeginTutorial,
                31);
        }

        private static void BuildGrid(Transform parent)
        {
            Color grid = new Color(
                ReleaseUiComponents.Blue.r,
                ReleaseUiComponents.Blue.g,
                ReleaseUiComponents.Blue.b,
                0.085f);

            for (int i = 1; i < 8; i++)
            {
                float x = i / 8f;
                Transform line = ReleaseUiKit.Rect(parent, "GridV" + i,
                    new Vector2(x - 0.001f, 0.055f),
                    new Vector2(x + 0.001f, 0.945f));
                Image image = line.gameObject.AddComponent<Image>();
                image.color = grid;
                image.raycastTarget = false;
            }

            for (int i = 1; i < 6; i++)
            {
                float y = i / 6f;
                Transform line = ReleaseUiKit.Rect(parent, "GridH" + i,
                    new Vector2(0.045f, y - 0.001f),
                    new Vector2(0.955f, y + 0.001f));
                Image image = line.gameObject.AddComponent<Image>();
                image.color = grid;
                image.raycastTarget = false;
            }
        }

        private static void BuildPreviewRoute(Transform parent)
        {
            Transform host = ReleaseUiKit.Rect(parent, "TutorialRoute",
                new Vector2(0.10f, 0.14f),
                new Vector2(0.90f, 0.86f));

            var points = new List<FixedPoint2>
            {
                FixedPoint2.FromNormalized(0.06, 0.22),
                FixedPoint2.FromNormalized(0.15, 0.40),
                FixedPoint2.FromNormalized(0.27, 0.56),
                FixedPoint2.FromNormalized(0.40, 0.59),
                FixedPoint2.FromNormalized(0.52, 0.48),
                FixedPoint2.FromNormalized(0.63, 0.36),
                FixedPoint2.FromNormalized(0.73, 0.40),
                FixedPoint2.FromNormalized(0.82, 0.55),
                FixedPoint2.FromNormalized(0.91, 0.74)
            };

            var glowGo = new GameObject("TutorialRouteGlow", typeof(RectTransform), typeof(RouteGraphic));
            glowGo.transform.SetParent(host, false);
            ReleaseUiKit.Stretch(glowGo.GetComponent<RectTransform>());
            RouteGraphic glow = glowGo.GetComponent<RouteGraphic>();
            glow.color = new Color(0.12f, 0.55f, 1f, 0.24f);
            glow.Thickness = 28f;
            glow.raycastTarget = false;
            glow.SetPoints(points);

            var routeGo = new GameObject("TutorialRouteLine", typeof(RectTransform), typeof(RouteGraphic));
            routeGo.transform.SetParent(host, false);
            ReleaseUiKit.Stretch(routeGo.GetComponent<RectTransform>());
            RouteGraphic route = routeGo.GetComponent<RouteGraphic>();
            route.color = new Color(0.18f, 0.52f, 1f, 1f);
            route.Thickness = 11f;
            route.raycastTarget = false;
            route.SetPoints(points);

            Image start = ReleaseUiKit.Dot(host, "TutorialStartMarker",
                Color.white,
                new Vector2(0.010f, 0.145f),
                new Vector2(0.105f, 0.285f));
            Outline startGlow = start.gameObject.AddComponent<Outline>();
            startGlow.effectColor = new Color(0.15f, 0.70f, 1f, 0.72f);
            startGlow.effectDistance = new Vector2(3f, -3f);

            Image end = ReleaseUiKit.Dot(host, "TutorialEndMarker",
                new Color(0.10f, 0.50f, 1f, 1f),
                new Vector2(0.855f, 0.665f),
                new Vector2(0.960f, 0.815f));
            Outline endGlow = end.gameObject.AddComponent<Outline>();
            endGlow.effectColor = new Color(0.18f, 0.76f, 1f, 0.76f);
            endGlow.effectDistance = new Vector2(4f, -4f);

            Image inner = ReleaseUiKit.Dot(end.transform, "TutorialEndInner",
                new Color(0.010f, 0.040f, 0.080f, 1f),
                new Vector2(0.28f, 0.28f),
                new Vector2(0.72f, 0.72f));
            inner.raycastTarget = false;
        }

        private static void BuildDots(Transform parent)
        {
            const float width = 0.018f;
            const float gap = 0.016f;
            float startX = 0.455f;

            for (int i = 0; i < 4; i++)
            {
                float x = startX + i * (width + gap);
                Color color = i == 0
                    ? ReleaseUiComponents.Cyan
                    : new Color(0.25f, 0.34f, 0.48f, 0.90f);

                ReleaseUiKit.Dot(
                    parent,
                    "TutorialDot" + i,
                    color,
                    new Vector2(x, 0.205f),
                    new Vector2(x + width, 0.223f));
            }
        }
    }
}
