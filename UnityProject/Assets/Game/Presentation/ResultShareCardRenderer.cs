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
                new Color(0.025f, 0.035f, 0.065f, 1f));
            background.raycastTarget = false;

            CreateText(root.transform, "Brand", "НЕ СБЕЙСЯ!", 64, TextAnchor.MiddleCenter,
                new Vector2(0.08f, 0.88f), new Vector2(0.92f, 0.97f), Color.white);
            CreateText(root.transform, "Mode", model.ModeLabel, 30, TextAnchor.MiddleCenter,
                new Vector2(0.08f, 0.83f), new Vector2(0.92f, 0.88f), new Color(0.55f, 0.86f, 1f, 1f));

            string icon = model.Celebration?.Icon ?? string.Empty;
            string medal = model.Celebration?.Label ?? string.Empty;
            CreateText(root.transform, "Score", $"{model.Score:0.0}%", 108, TextAnchor.MiddleCenter,
                new Vector2(0.08f, 0.69f), new Vector2(0.92f, 0.82f), ScoreColor(model.Score));
            CreateText(root.transform, "Medal", string.IsNullOrWhiteSpace(icon) ? medal : icon + "  " + medal, 40,
                TextAnchor.MiddleCenter, new Vector2(0.08f, 0.64f), new Vector2(0.92f, 0.70f), Color.white);

            Image routePanel = CreateImage(root.transform, "RoutePanel", new Vector2(0.08f, 0.24f), new Vector2(0.92f, 0.62f),
                new Color(0.06f, 0.085f, 0.13f, 1f));
            routePanel.raycastTarget = false;

            RouteGraphic reference = CreateRouteGraphic(routePanel.transform, "Reference", new Color(0.18f, 0.82f, 1f, 0.62f), 14f);
            reference.SetPoints(model.ReferencePoints);
            RouteGraphic player = CreateRouteGraphic(routePanel.transform, "Player", ScoreColor(model.Score), 11f);
            player.SetPoints(ToPositions(model.PlayerPoints));

            string comparison = model.HasRivalScore
                ? $"Друг: {model.RivalScore:0.0}%   •   Ты: {model.Score:0.0}%"
                : model.ChallengeLabel;
            CreateText(root.transform, "Challenge", comparison, 31, TextAnchor.MiddleCenter,
                new Vector2(0.08f, 0.16f), new Vector2(0.92f, 0.23f), new Color(0.83f, 0.88f, 0.96f, 1f));
            CreateText(root.transform, "Cta", "СМОЖЕШЬ ТОЧНЕЕ?", 42, TextAnchor.MiddleCenter,
                new Vector2(0.08f, 0.07f), new Vector2(0.92f, 0.15f), new Color(0.72f, 0.48f, 1f, 1f));
            CreateText(root.transform, "Footer", "Daily memory challenge • RuStore", 23, TextAnchor.MiddleCenter,
                new Vector2(0.08f, 0.02f), new Vector2(0.92f, 0.07f), new Color(0.55f, 0.62f, 0.74f, 1f));

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

        private static Image CreateImage(Transform parent, string name, Vector2 min, Vector2 max, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = UiLayer;
            go.transform.SetParent(parent, false);
            SetAnchors(go.GetComponent<RectTransform>(), min, max);
            Image image = go.GetComponent<Image>();
            image.color = color;
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
