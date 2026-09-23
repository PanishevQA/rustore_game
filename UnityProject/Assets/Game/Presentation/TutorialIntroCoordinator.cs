using System.Collections.Generic;
using DontGetSidetracked.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace DontGetSidetracked.Presentation
{
    /// <summary>
    /// First-launch release intro matching the approved concept board.
    /// Owns presentation only; GameBootstrap still owns the actual tutorial round.
    /// </summary>
    public sealed class TutorialIntroCoordinator : MonoBehaviour
    {
        private static TutorialIntroCoordinator _instance;

        private GameBootstrap _owner;
        private GameObject _canvas;

        public bool IsOpen => _canvas != null && _canvas.activeSelf;

        public static void OpenFor(GameBootstrap owner)
        {
            if (_instance == null)
            {
                var root = new GameObject("TutorialIntroCoordinator");
                DontDestroyOnLoad(root);
                _instance = root.AddComponent<TutorialIntroCoordinator>();
            }

            _instance._owner = owner;
            _instance.SetVisible(true);
        }

        public static void CloseIfOpen()
        {
            if (_instance != null) _instance.SetVisible(false);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            BuildUi();
            SetVisible(false);
        }

        private void BuildUi()
        {
            _canvas = new GameObject("TutorialIntroCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvas.transform.SetParent(transform, false);

            Canvas canvas = _canvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 92;

            CanvasScaler scaler = _canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            ReleaseUiComponents.Backdrop(_canvas.transform, "TutorialIntroBackdrop");

            Button back = ReleaseUiComponents.SecondaryButton(_canvas.transform, "Back", "←",
                new Vector2(0.055f, 0.900f), new Vector2(0.145f, 0.955f), BeginTutorial, 30);
            Image backSurface = back.GetComponent<Image>();
            if (backSurface != null)
                backSurface.color = new Color(0.020f, 0.060f, 0.110f, 0.94f);

            Text title = ReleaseUiKit.TextBlock(_canvas.transform, "Title", "Давай попробуем!", 46,
                TextAnchor.MiddleCenter, new Vector2(0.12f, 0.805f), new Vector2(0.88f, 0.875f),
                ReleaseUiComponents.Text, FontStyle.Bold);
            ReleaseUiKit.AddTextShadow(title, 0.50f, -3f);

            ReleaseUiKit.TextBlock(_canvas.transform, "IntroCopy",
                "Сначала ты увидишь маршрут\nнесколько секунд…", 24,
                TextAnchor.MiddleCenter, new Vector2(0.12f, 0.735f), new Vector2(0.88f, 0.805f),
                ReleaseUiComponents.Muted);

            Image preview = ReleaseUiComponents.GlassCard(_canvas.transform, "TutorialPreview",
                new Vector2(0.080f, 0.425f), new Vector2(0.920f, 0.705f),
                ReleaseUiComponents.Cyan, true);
            preview.color = new Color(0.010f, 0.038f, 0.082f, 0.995f);
            BuildGrid(preview.transform);
            BuildPreviewRoute(preview.transform);

            ReleaseUiKit.TextBlock(_canvas.transform, "DisappearCopy", "…затем он исчезнет.", 20,
                TextAnchor.MiddleCenter, new Vector2(0.12f, 0.355f), new Vector2(0.88f, 0.405f),
                ReleaseUiComponents.Muted, FontStyle.Bold);
            ReleaseUiKit.TextBlock(_canvas.transform, "RepeatCopy",
                "Повтори его по памяти\nодним движением пальца!", 25,
                TextAnchor.MiddleCenter, new Vector2(0.12f, 0.275f), new Vector2(0.88f, 0.355f),
                ReleaseUiComponents.Text, FontStyle.Bold);

            BuildDots(_canvas.transform);

            ReleaseUiComponents.PrimaryButton(_canvas.transform, "Start", "НАЧАТЬ",
                new Vector2(0.080f, 0.075f), new Vector2(0.920f, 0.145f), BeginTutorial, 30);
        }

        private static void BuildGrid(Transform parent)
        {
            for (int i = 1; i < 8; i++)
            {
                float x = i / 8f;
                Transform vertical = ReleaseUiKit.Rect(parent, "GridV" + i,
                    new Vector2(x - 0.001f, 0.04f), new Vector2(x + 0.001f, 0.96f));
                Image vi = vertical.gameObject.AddComponent<Image>();
                vi.color = new Color(0.16f, 0.48f, 0.72f, 0.10f);
                vi.raycastTarget = false;

                float y = i / 8f;
                Transform horizontal = ReleaseUiKit.Rect(parent, "GridH" + i,
                    new Vector2(0.04f, y - 0.001f), new Vector2(0.96f, y + 0.001f));
                Image hi = horizontal.gameObject.AddComponent<Image>();
                hi.color = new Color(0.16f, 0.48f, 0.72f, 0.10f);
                hi.raycastTarget = false;
            }
        }

        private static void BuildPreviewRoute(Transform parent)
        {
            var host = new GameObject("TutorialPreviewRoute", typeof(RectTransform));
            host.transform.SetParent(parent, false);
            ReleaseUiKit.SetAnchors(host.GetComponent<RectTransform>(),
                new Vector2(0.08f, 0.14f), new Vector2(0.92f, 0.86f));

            var points = new List<FixedPoint2>
            {
                FixedPoint2.FromNormalized(0.08, 0.22),
                FixedPoint2.FromNormalized(0.26, 0.58),
                FixedPoint2.FromNormalized(0.45, 0.54),
                FixedPoint2.FromNormalized(0.62, 0.34),
                FixedPoint2.FromNormalized(0.80, 0.44),
                FixedPoint2.FromNormalized(0.92, 0.72),
            };

            RouteGraphic glow = CreateRoute(host.transform, "Glow",
                new Color(ReleaseUiComponents.Cyan.r, ReleaseUiComponents.Cyan.g, ReleaseUiComponents.Cyan.b, 0.22f), 22f);
            glow.SetPoints(points);
            RouteGraphic route = CreateRoute(host.transform, "Route", new Color(0.26f, 0.66f, 1f, 1f), 9f);
            route.SetPoints(points);

            Image start = ReleaseUiKit.Panel(host.transform, "Start",
                new Vector2(0.020f, 0.145f), new Vector2(0.145f, 0.305f),
                ReleaseUiComponents.Text, ReleaseUiComponents.Cyan, false);
            start.sprite = ReleaseUiKit.Circle;
            start.type = Image.Type.Simple;
            start.raycastTarget = false;

            Image end = ReleaseUiKit.Panel(host.transform, "End",
                new Vector2(0.855f, 0.635f), new Vector2(0.980f, 0.795f),
                new Color(0.04f, 0.16f, 0.30f, 1f), ReleaseUiComponents.Cyan, false);
            end.sprite = ReleaseUiKit.Circle;
            end.type = Image.Type.Simple;
            end.raycastTarget = false;
        }

        private static RouteGraphic CreateRoute(Transform parent, string name, Color color, float thickness)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(RouteGraphic));
            go.transform.SetParent(parent, false);
            ReleaseUiKit.Stretch(go.GetComponent<RectTransform>());
            RouteGraphic graphic = go.GetComponent<RouteGraphic>();
            graphic.color = color;
            graphic.Thickness = thickness;
            graphic.raycastTarget = false;
            return graphic;
        }

        private static void BuildDots(Transform parent)
        {
            for (int i = 0; i < 4; i++)
            {
                float left = 0.43f + i * 0.047f;
                Image dot = ReleaseUiKit.Panel(parent, "StepDot" + i,
                    new Vector2(left, 0.215f), new Vector2(left + 0.022f, 0.228f),
                    i == 0 ? ReleaseUiComponents.Cyan : new Color(0.27f, 0.36f, 0.50f, 0.78f),
                    i == 0 ? ReleaseUiComponents.Cyan : ReleaseUiComponents.Muted,
                    false);
                dot.sprite = ReleaseUiKit.Circle;
                dot.type = Image.Type.Simple;
                dot.raycastTarget = false;
            }
        }

        private void BeginTutorial()
        {
            SetVisible(false);
            _owner?.BeginTutorialFromIntro();
        }

        private void SetVisible(bool visible)
        {
            if (_canvas != null && _canvas.activeSelf != visible)
                _canvas.SetActive(visible);
        }
    }
}
