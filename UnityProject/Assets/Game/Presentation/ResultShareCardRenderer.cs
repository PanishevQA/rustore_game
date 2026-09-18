using System;
using System.Collections.Generic;
using DontGetSidetracked.Core;
using DontGetSidetracked.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace DontGetSidetracked.Presentation
{
    public sealed class ResultShareCardModel
    {
        public string ModeLabel = string.Empty;
        public string ChallengeLabel = string.Empty;
        public double Score;
        public ScoreCelebration Celebration;
        public IReadOnlyList<FixedPoint2> ReferencePoints;
        public IReadOnlyList<RecordedPoint> PlayerPoints;
        public double RivalScore;
        public bool HasRivalScore;
    }

    public static class ResultShareCardRenderer
    {
        private const int Width = 1080;
        private const int Height = 1350;
        private const int UiLayer = 31;

        public static byte[] RenderPng(ResultShareCardModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));

            var cameraGo = new GameObject("ShareCardCamera", typeof(Camera));
            cameraGo.layer = UiLayer;
            Camera camera = cameraGo.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.035f, 0.065f, 1f);
            camera.orthographic = true;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 10f;
            camera.cullingMask = 1 << UiLayer;

            var renderTexture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            renderTexture.Create();
            camera.targetTexture = renderTexture;

            var root = new GameObject("ShareCardCanvas", typeof(Canvas), typeof(CanvasScaler));
            root.layer = UiLayer;
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(Width, Height);
            scaler.matchWidthOrHeight = 0.5f;

            Image background = CreateImage(root.transform, "Background", Vector2.zero, Vector2.one,
                new Color(0.010f, 0.016f, 0.040f, 1f));
            background.raycastTarget = false;

            CreateText(root.transform, "BrandKicker", "MEMORY TRACE", 22, TextAnchor.MiddleLeft,
                new Vector2(0.075f, 0.915f), new Vector2(0.45f, 0.955f), new Color(0.20f, 0.86f, 1f, 1f));
            Text brand = CreateText(root.transform, "Brand", "НЕ СБЕЙСЯ!", 68, TextAnchor.MiddleLeft,
                new Vector2(0.075f, 0.845f), new Vector2(0.68f, 0.925f), Color.white);
            brand.fontStyle = FontStyle.Bold;
            AddTextShadow(brand, 0.45f, -3f);

            CreateImage(root.transform, "AccentDash",
                new Vector2(0.075f, 0.833f), new Vector2(0.29f, 0.840f),
                new Color(0.20f, 0.86f, 1f, 1f), true);

            CreateText(root.transform, "Mode", model.ModeLabel, 27, TextAnchor.MiddleRight,
                new Vector2(0.54f, 0.865f), new Vector2(0.925f, 0.925f),
                new Color(0.64f, 0.71f, 0.83f, 1f));

            Text score = CreateText(root.transform, "Score", $"{model.Score:0.0}%", 118, TextAnchor.MiddleLeft,
                new Vector2(0.075f, 0.690f), new Vector2(0.60f, 0.825f), ScoreColor(model.Score));
            score.fontStyle = FontStyle.Bold;
            AddTextShadow(score, 0.50f, -4f);

            string medal = model.Celebration?.Label ?? string.Empty;
            Text medalText = CreateText(root.transform, "Medal", medal, 37, TextAnchor.MiddleRight,
                new Vector2(0.58f, 0.710f), new Vector2(0.925f, 0.790f), Color.white);
            medalText.fontStyle = FontStyle.Bold;

            Image routePanel = CreateImage(root.transform, "RoutePanel",
                new Vector2(0.075f, 0.285f), new Vector2(0.925f, 0.670f),
                new Color(0.030f, 0.047f, 0.088f, 1f), true);
            routePanel.raycastTarget = false;
            Outline routeOutline = routePanel.gameObject.AddComponent<Outline>();
            routeOutline.effectColor = new Color(0.20f, 0.86f, 1f, 0.15f);
            routeOutline.effectDistance = new Vector2(2f, -2f);

            CreateText(routePanel.transform, "ReferenceLabel", "ЭТАЛОН  /  ТВОЯ ЛИНИЯ", 20, TextAnchor.MiddleLeft,
                new Vector2(0.055f, 0.88f), new Vector2(0.68f, 0.97f),
                new Color(0.58f, 0.68f, 0.82f, 1f));

            RouteGraphic reference = CreateRouteGraphic(routePanel.transform, "Reference",
                new Color(0.18f, 0.82f, 1f, 0.48f), 14f);
            reference.SetPoints(model.ReferencePoints);
            RouteGraphic player = CreateRouteGraphic(routePanel.transform, "Player", ScoreColor(model.Score), 11f);
            player.SetPoints(ToPositions(model.PlayerPoints));

            if (model.ReferencePoints != null && model.ReferencePoints.Count >= 2)
            {
                CreateMarker(routePanel.transform, "Start", model.ReferencePoints[0], ReleaseUiKit.Green);
                CreateMarker(routePanel.transform, "End", model.ReferencePoints[model.ReferencePoints.Count - 1], ReleaseUiKit.Danger);
            }

            string comparison = model.HasRivalScore
                ? $"ДРУГ  {model.RivalScore:0.0}%     •     ТЫ  {model.Score:0.0}%"
                : model.ChallengeLabel;

            Image infoCard = CreateImage(root.transform, "InfoCard",
                new Vector2(0.075f, 0.180f), new Vector2(0.925f, 0.265f),
                new Color(0.045f, 0.060f, 0.105f, 0.98f), true);
            CreateText(infoCard.transform, "Challenge", comparison, 27, TextAnchor.MiddleCenter,
                new Vector2(0.05f, 0.12f), new Vector2(0.95f, 0.88f),
                new Color(0.83f, 0.88f, 0.96f, 1f));

            Text cta = CreateText(root.transform, "Cta", "СМОЖЕШЬ ТОЧНЕЕ?", 45, TextAnchor.MiddleCenter,
                new Vector2(0.08f, 0.085f), new Vector2(0.92f, 0.155f),
                new Color(0.72f, 0.54f, 1f, 1f));
            cta.fontStyle = FontStyle.Bold;

            CreateText(root.transform, "Footer", "НЕ СБЕЙСЯ!  •  RuStore", 21, TextAnchor.MiddleCenter,
                new Vector2(0.08f, 0.025f), new Vector2(0.92f, 0.075f),
                new Color(0.48f, 0.56f, 0.69f, 1f));

            Canvas.ForceUpdateCanvases();
            camera.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTexture;
            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
            texture.Apply(false, false);
            byte[] png = texture.EncodeToPNG();
            RenderTexture.active = previous;

            camera.targetTexture = null;
            renderTexture.Release();
            UnityEngine.Object.Destroy(texture);
            UnityEngine.Object.Destroy(renderTexture);
            UnityEngine.Object.Destroy(root);
            UnityEngine.Object.Destroy(cameraGo);
            return png;
        }

        private static void CreateMarker(Transform parent, string name, FixedPoint2 point, Color color)
        {
            float normalizedX = Mathf.Clamp01(point.X / (float)FixedPoint2.Scale);
            float normalizedY = Mathf.Clamp01(point.Y / (float)FixedPoint2.Scale);
            Vector2 anchor = new Vector2(
                Mathf.Lerp(0.05f, 0.95f, normalizedX),
                Mathf.Lerp(0.07f, 0.93f, normalizedY));

            var glowGo = new GameObject(name + "Glow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            glowGo.layer = UiLayer;
            glowGo.transform.SetParent(parent, false);
            RectTransform glowRect = glowGo.GetComponent<RectTransform>();
            glowRect.anchorMin = anchor;
            glowRect.anchorMax = anchor;
            glowRect.sizeDelta = new Vector2(56f, 56f);
            Image glow = glowGo.GetComponent<Image>();
            glow.sprite = ReleaseUiKit.Circle;
            glow.color = new Color(color.r, color.g, color.b, 0.20f);
            glow.raycastTarget = false;

            var dotGo = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            dotGo.layer = UiLayer;
            dotGo.transform.SetParent(parent, false);
            RectTransform dotRect = dotGo.GetComponent<RectTransform>();
            dotRect.anchorMin = anchor;
            dotRect.anchorMax = anchor;
            dotRect.sizeDelta = new Vector2(30f, 30f);
            Image dot = dotGo.GetComponent<Image>();
            dot.sprite = ReleaseUiKit.Circle;
            dot.color = color;
            dot.raycastTarget = false;
        }

        private static RouteGraphic CreateRouteGraphic(Transform parent, string name, Color color, float thickness)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RouteGraphic));
            go.layer = UiLayer;
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            SetAnchors(rect, new Vector2(0.05f, 0.07f), new Vector2(0.95f, 0.93f));
            RouteGraphic graphic = go.GetComponent<RouteGraphic>();
            graphic.color = color;
            graphic.Thickness = thickness;
            graphic.raycastTarget = false;
            return graphic;
        }

        private static Image CreateImage(
            Transform parent,
            string name,
            Vector2 min,
            Vector2 max,
            Color color,
            bool rounded = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = UiLayer;
            go.transform.SetParent(parent, false);
            SetAnchors(go.GetComponent<RectTransform>(), min, max);
            Image image = go.GetComponent<Image>();
            image.color = color;
            if (rounded)
            {
                image.sprite = ReleaseUiKit.Rounded;
                image.type = Image.Type.Sliced;
            }
            return image;
        }

        private static Text CreateText(
            Transform parent,
            string name,
            string value,
            int size,
            TextAnchor alignment,
            Vector2 min,
            Vector2 max,
            Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.layer = UiLayer;
            go.transform.SetParent(parent, false);
            SetAnchors(go.GetComponent<RectTransform>(), min, max);
            Text text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value ?? string.Empty;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 18;
            text.resizeTextMaxSize = size;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static void AddTextShadow(Text text, float alpha, float y)
        {
            if (text == null) return;
            Shadow shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, alpha);
            shadow.effectDistance = new Vector2(0f, y);
            shadow.useGraphicAlpha = true;
        }

        private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static IReadOnlyList<FixedPoint2> ToPositions(IReadOnlyList<RecordedPoint> points)
        {
            if (points == null) return Array.Empty<FixedPoint2>();
            var result = new List<FixedPoint2>(points.Count);
            for (int i = 0; i < points.Count; i++) result.Add(points[i].Position);
            return result;
        }

        private static Color ScoreColor(double score)
        {
            if (score >= 98.0) return new Color(0.48f, 1f, 0.72f, 1f);
            if (score >= 90.0) return new Color(0.25f, 0.95f, 0.55f, 1f);
            if (score >= 80.0) return new Color(1f, 0.77f, 0.24f, 1f);
            return new Color(1f, 0.45f, 0.45f, 1f);
        }
    }
}
