using System;
using UnityEngine;
using UnityEngine.UI;

namespace DontGetSidetracked.Presentation
{
    /// <summary>
    /// Shared runtime-only visual primitives for the release UI.
    /// Keeps presentation styling out of gameplay/domain code and avoids asset/package dependencies.
    /// </summary>
    internal static class ReleaseUiKit
    {
        public static readonly Color Background = new Color(0.010f, 0.016f, 0.040f, 1f);
        public static readonly Color Surface = new Color(0.030f, 0.045f, 0.085f, 0.985f);
        public static readonly Color SurfaceRaised = new Color(0.050f, 0.072f, 0.125f, 0.995f);
        public static readonly Color SurfaceSoft = new Color(0.070f, 0.095f, 0.155f, 0.88f);
        public static readonly Color Text = new Color(0.965f, 0.982f, 1f, 1f);
        public static readonly Color Muted = new Color(0.60f, 0.69f, 0.81f, 1f);
        public static readonly Color Cyan = new Color(0.20f, 0.86f, 1f, 1f);
        public static readonly Color Violet = new Color(0.39f, 0.24f, 1f, 1f);
        public static readonly Color Green = new Color(0.29f, 0.96f, 0.62f, 1f);
        public static readonly Color Gold = new Color(1f, 0.77f, 0.26f, 1f);
        public static readonly Color Danger = new Color(1f, 0.39f, 0.49f, 1f);

        private static Sprite _rounded;
        private static Sprite _circle;

        public static Sprite Rounded
        {
            get
            {
                EnsureAssets();
                return _rounded;
            }
        }

        public static Sprite Circle
        {
            get
            {
                EnsureAssets();
                return _circle;
            }
        }

        public static Transform Rect(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            SetAnchors(rect, min, max);
            return go.transform;
        }

        public static Image Panel(
            Transform parent,
            string name,
            Vector2 min,
            Vector2 max,
            Color background,
            Color accent,
            bool shadow = true)
        {
            Transform root = Rect(parent, name, min, max);
            Image image = root.gameObject.AddComponent<Image>();
            image.sprite = Rounded;
            image.type = Image.Type.Sliced;
            image.color = background;

            Outline outline = root.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(accent.r, accent.g, accent.b, 0.16f);
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = true;

            if (shadow)
            {
                Shadow drop = root.gameObject.AddComponent<Shadow>();
                drop.effectColor = new Color(0f, 0f, 0f, 0.44f);
                drop.effectDistance = new Vector2(0f, -10f);
                drop.useGraphicAlpha = true;
            }

            return image;
        }

        public static Text TextBlock(
            Transform parent,
            string name,
            string value,
            int size,
            TextAnchor alignment,
            Vector2 min,
            Vector2 max,
            Color color,
            FontStyle style = FontStyle.Normal)
        {
            Transform root = Rect(parent, name, min, max);
            Text text = root.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value ?? string.Empty;
            text.fontSize = Math.Max(20, size);
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Math.Max(18, size - 4);
            text.resizeTextMaxSize = Math.Max(20, size);
            text.alignment = alignment;
            text.color = color;
            text.fontStyle = style;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        public static Button Button(
            Transform parent,
            string name,
            string label,
            Vector2 min,
            Vector2 max,
            Color background,
            Color textColor,
            int fontSize,
            UnityEngine.Events.UnityAction action)
        {
            Transform root = Rect(parent, name, min, max);
            Image image = root.gameObject.AddComponent<Image>();
            image.sprite = Rounded;
            image.type = Image.Type.Sliced;
            image.color = background;

            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            // Selectable multiplies this tint by Image.color; repeating the surface
            // color here made dark buttons almost black and hid their pressed state.
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
            colors.pressedColor = new Color(0.65f, 0.75f, 0.86f, 1f);
            colors.selectedColor = new Color(1.10f, 1.10f, 1.10f, 1f);
            colors.disabledColor = new Color(0.60f, 0.64f, 0.72f, 0.75f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            if (action != null) button.onClick.AddListener(action);

            Text text = TextBlock(root, "Label", label, fontSize, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, textColor, FontStyle.Bold);
            text.raycastTarget = false;

            Shadow drop = root.gameObject.AddComponent<Shadow>();
            drop.effectColor = new Color(0f, 0f, 0f, 0.28f);
            drop.effectDistance = new Vector2(0f, -6f);
            drop.useGraphicAlpha = true;
            return button;
        }

        public static Image Dot(
            Transform parent,
            string name,
            Color color,
            Vector2 min,
            Vector2 max)
        {
            Transform root = Rect(parent, name, min, max);
            Image image = root.gameObject.AddComponent<Image>();
            image.sprite = Circle;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static void AddTextShadow(Text text, float alpha = 0.40f, float y = -2f)
        {
            if (text == null) return;
            Shadow shadow = text.GetComponent<Shadow>();
            if (shadow == null) shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, alpha);
            shadow.effectDistance = new Vector2(0f, y);
            shadow.useGraphicAlpha = true;
        }

        public static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
        {
            if (rect == null) return;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public static void Stretch(RectTransform rect) => SetAnchors(rect, Vector2.zero, Vector2.one);

        public static Color Lighten(Color c, float amount) => new Color(
            Mathf.Clamp01(c.r + amount),
            Mathf.Clamp01(c.g + amount),
            Mathf.Clamp01(c.b + amount),
            c.a);

        public static Color Darken(Color c, float amount) => new Color(
            Mathf.Clamp01(c.r - amount),
            Mathf.Clamp01(c.g - amount),
            Mathf.Clamp01(c.b - amount),
            c.a);

        private static void EnsureAssets()
        {
            if (_rounded == null) _rounded = CreateRoundedSprite(96, 24);
            if (_circle == null) _circle = CreateCircleSprite(64);
        }

        private static Sprite CreateRoundedSprite(int size, int radius)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "ReleaseUiRounded",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color32[] pixels = new Color32[size * size];
            float edge = radius - 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float cx = x < radius ? radius : x >= size - radius ? size - radius - 1 : x;
                    float cy = y < radius ? radius : y >= size - radius ? size - radius - 1 : y;
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                    byte alpha = distance <= edge ? (byte)255 : distance <= radius + 0.75f ? (byte)170 : (byte)0;
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Vector4 border = new Vector4(radius + 2, radius + 2, radius + 2, radius + 2);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, border);
            sprite.name = "ReleaseUiRoundedSprite";
            return sprite;
        }

        private static Sprite CreateCircleSprite(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "ReleaseUiCircle",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color32[] pixels = new Color32[size * size];
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = size * 0.47f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    byte alpha = distance <= radius - 1f ? (byte)255 :
                                 distance <= radius + 0.75f ? (byte)150 : (byte)0;
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = "ReleaseUiCircleSprite";
            return sprite;
        }
    }
}
