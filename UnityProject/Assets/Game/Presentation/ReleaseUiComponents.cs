using System;
using UnityEngine;
using UnityEngine.UI;

namespace DontGetSidetracked.Presentation
{
    /// <summary>
    /// Reusable release components derived from the approved visual mockups.
    /// Presentation-only: no gameplay/economy state lives here.
    /// </summary>
    internal static class ReleaseUiComponents
    {
        private static Sprite _primaryGradient;
        private static Sprite _secondaryGradient;
        private static Texture2D _backdropTexture;

        public static readonly Color Navy = new Color(0.012f, 0.030f, 0.070f, 1f);
        public static readonly Color NavyRaised = new Color(0.025f, 0.075f, 0.125f, 0.98f);
        public static readonly Color Cyan = new Color32(0x00, 0xE5, 0xFF, 0xFF);
        public static readonly Color Blue = new Color32(0x38, 0xB2, 0xF6, 0xFF);
        public static readonly Color Violet = new Color32(0x8B, 0x5C, 0xF6, 0xFF);
        public static readonly Color Pink = new Color32(0xFF, 0x4D, 0x9E, 0xFF);
        public static readonly Color Gold = new Color32(0xFA, 0xCC, 0x15, 0xFF);
        public static readonly Color Success = new Color32(0x42, 0xF5, 0xC5, 0xFF);
        public static readonly Color Danger = new Color32(0xFF, 0x5C, 0x7A, 0xFF);
        public static readonly Color Text = new Color(0.965f, 0.982f, 1f, 1f);
        public static readonly Color Muted = new Color(0.64f, 0.72f, 0.84f, 1f);

        public static RawImage Backdrop(Transform parent, string name = "ReleaseBackdrop")
        {
            EnsureAssets();
            Transform root = ReleaseUiKit.Rect(parent, name, Vector2.zero, Vector2.one);
            root.SetAsFirstSibling();
            RawImage image = root.gameObject.AddComponent<RawImage>();
            image.texture = _backdropTexture;
            image.color = Color.white;
            image.raycastTarget = false;
            return image;
        }

        public static Image GlassCard(
            Transform parent,
            string name,
            Vector2 min,
            Vector2 max,
            Color accent,
            bool strong = false)
        {
            Color fill = strong
                ? new Color(0.025f, 0.080f, 0.135f, 0.985f)
                : new Color(0.020f, 0.055f, 0.100f, 0.955f);
            Image card = ReleaseUiKit.Panel(parent, name, min, max, fill, accent, true);
            Outline outline = card.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = new Color(accent.r, accent.g, accent.b, strong ? 0.34f : 0.20f);
                outline.effectDistance = new Vector2(2f, -2f);
            }
            return card;
        }

        public static Button PrimaryButton(
            Transform parent,
            string name,
            string label,
            Vector2 min,
            Vector2 max,
            UnityEngine.Events.UnityAction action,
            int fontSize = 28)
        {
            EnsureAssets();
            Transform root = ReleaseUiKit.Rect(parent, name, min, max);
            Image image = root.gameObject.AddComponent<Image>();
            image.sprite = _primaryGradient;
            image.type = Image.Type.Sliced;
            image.color = Color.white;

            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            if (action != null) button.onClick.AddListener(action);
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
            colors.pressedColor = new Color(0.84f, 0.90f, 1f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.35f, 0.42f, 0.58f, 0.55f);
            colors.fadeDuration = 0.07f;
            button.colors = colors;

            Shadow shadow = root.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.30f);
            shadow.effectDistance = new Vector2(0f, -7f);

            Outline glow = root.gameObject.AddComponent<Outline>();
            glow.effectColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.42f);
            glow.effectDistance = new Vector2(2f, -2f);

            Text text = ReleaseUiKit.TextBlock(root, "Label", label, fontSize, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, Text, FontStyle.Bold);
            text.raycastTarget = false;
            return button;
        }

        public static Button SecondaryButton(
            Transform parent,
            string name,
            string label,
            Vector2 min,
            Vector2 max,
            UnityEngine.Events.UnityAction action,
            int fontSize = 23)
        {
            EnsureAssets();
            Transform root = ReleaseUiKit.Rect(parent, name, min, max);
            Image image = root.gameObject.AddComponent<Image>();
            image.sprite = ReleaseUiKit.Rounded;
            image.type = Image.Type.Sliced;
            image.color = new Color(0.025f, 0.065f, 0.115f, 0.96f);

            Outline outline = root.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.42f);
            outline.effectDistance = new Vector2(2f, -2f);

            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            if (action != null) button.onClick.AddListener(action);
            ColorBlock colors = button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = ReleaseUiKit.Lighten(image.color, 0.06f);
            colors.pressedColor = ReleaseUiKit.Darken(image.color, 0.06f);
            colors.disabledColor = new Color(image.color.r, image.color.g, image.color.b, 0.45f);
            colors.fadeDuration = 0.07f;
            button.colors = colors;

            Text text = ReleaseUiKit.TextBlock(root, "Label", label, fontSize, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, Text, FontStyle.Bold);
            text.raycastTarget = false;
            return button;
        }

        public static Text StatTile(
            Transform parent,
            string name,
            string glyph,
            string value,
            string caption,
            Vector2 min,
            Vector2 max,
            Color accent)
        {
            Image card = GlassCard(parent, name, min, max, accent, false);
            Text icon = ReleaseUiKit.TextBlock(card.transform, "Icon", glyph, 28, TextAnchor.MiddleCenter,
                new Vector2(0.06f, 0.56f), new Vector2(0.34f, 0.90f), accent, FontStyle.Bold);
            icon.raycastTarget = false;

            Text valueText = ReleaseUiKit.TextBlock(card.transform, "Value", value, 27, TextAnchor.MiddleLeft,
                new Vector2(0.34f, 0.48f), new Vector2(0.92f, 0.90f), Text, FontStyle.Bold);
            Text captionText = ReleaseUiKit.TextBlock(card.transform, "Caption", caption, 14, TextAnchor.UpperLeft,
                new Vector2(0.10f, 0.08f), new Vector2(0.92f, 0.46f), Muted);
            captionText.lineSpacing = 0.95f;
            return valueText;
        }

        public static Text CurrencyPill(
            Transform parent,
            string name,
            string glyph,
            string value,
            Vector2 min,
            Vector2 max,
            Color accent,
            UnityEngine.Events.UnityAction action = null)
        {
            Image card = GlassCard(parent, name, min, max, accent, false);

            Transform iconRoot = ReleaseUiKit.Rect(card.transform, "GeneratedCoin",
                new Vector2(0.04f, 0.08f), new Vector2(0.27f, 0.92f));
            Image iconImage = iconRoot.gameObject.AddComponent<Image>();
            bool generatedCoin = GeneratedUiAssets.TryApply(iconImage, GeneratedUiAssets.CoinIcon);
            if (!generatedCoin)
            {
                UnityEngine.Object.Destroy(iconRoot.gameObject);
                Text fallbackIcon = ReleaseUiKit.TextBlock(card.transform, "Icon", glyph, 24, TextAnchor.MiddleCenter,
                    new Vector2(0.05f, 0.08f), new Vector2(0.27f, 0.92f), accent, FontStyle.Bold);
                fallbackIcon.raycastTarget = false;
            }

            Text valueText = ReleaseUiKit.TextBlock(card.transform, "Value", value, 24, TextAnchor.MiddleLeft,
                new Vector2(0.28f, 0.08f), new Vector2(action == null ? 0.93f : 0.72f, 0.92f), Text, FontStyle.Bold);
            if (action != null)
            {
                Button plus = SecondaryButton(card.transform, "Plus", "+",
                    new Vector2(0.74f, 0.14f), new Vector2(0.94f, 0.86f), action, 22);
                Image plusImage = plus.GetComponent<Image>();
                if (plusImage != null) plusImage.color = new Color(accent.r, accent.g, accent.b, 0.16f);
            }
            return valueText;
        }

        public static Text SectionHeader(
            Transform parent,
            string title,
            Vector2 min,
            Vector2 max,
            Color color = default)
        {
            Color use = color == default ? Text : color;
            return ReleaseUiKit.TextBlock(parent, "Section_" + title, title, 23, TextAnchor.MiddleLeft,
                min, max, use, FontStyle.Bold);
        }

        private static void EnsureAssets()
        {
            if (_primaryGradient == null)
                _primaryGradient = CreateGradientRoundedSprite(256, 80, 24, Cyan, Violet);
            if (_secondaryGradient == null)
                _secondaryGradient = CreateGradientRoundedSprite(256, 80, 24,
                    new Color(0.02f, 0.08f, 0.16f, 1f),
                    new Color(0.05f, 0.04f, 0.16f, 1f));
            if (_backdropTexture == null)
                _backdropTexture = CreateBackdropTexture(144, 256);
        }

        private static Sprite CreateGradientRoundedSprite(int width, int height, int radius, Color left, Color right)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "ReleaseGradientRounded",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color32[] pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)Math.Max(1, width - 1);
                    Color color = Color.Lerp(left, right, nx);
                    float dx = x < radius ? radius - x : x >= width - radius ? x - (width - radius - 1) : 0f;
                    float dy = y < radius ? radius - y : y >= height - radius ? y - (height - radius - 1) : 0f;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = distance <= radius ? 1f : 0f;
                    color.a *= alpha;
                    pixels[y * width + x] = color;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Vector4 border = new Vector4(radius + 2, radius + 2, radius + 2, radius + 2);
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, border);
        }

        private static Texture2D CreateBackdropTexture(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "ReleaseBackdropTexture",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color top = new Color(0.015f, 0.055f, 0.13f, 1f);
            Color middle = new Color(0.015f, 0.035f, 0.085f, 1f);
            Color bottom = new Color(0.004f, 0.012f, 0.035f, 1f);
            Color[] pixels = new Color[width * height];

            for (int y = 0; y < height; y++)
            {
                float ny = y / (float)Math.Max(1, height - 1);
                Color baseColor = ny > 0.48f
                    ? Color.Lerp(middle, top, (ny - 0.48f) / 0.52f)
                    : Color.Lerp(bottom, middle, ny / 0.48f);

                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)Math.Max(1, width - 1);
                    Color color = baseColor;

                    float glow = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(nx, ny), new Vector2(0.72f, 0.72f)) / 0.42f);
                    color += new Color(0.035f, 0.020f, 0.10f, 0f) * glow;

                    int starHash = (x * 37 + y * 71 + x * y * 3) % 233;
                    if (ny > 0.34f && starHash == 0)
                        color = Color.Lerp(color, new Color(0.48f, 0.80f, 1f, 1f), 0.72f);

                    float ridge1 = 0.12f + 0.045f * Mathf.Sin(nx * 10.8f) + 0.028f * Mathf.Sin(nx * 23.0f + 0.7f);
                    float ridge2 = 0.075f + 0.028f * Mathf.Sin(nx * 15.4f + 1.2f);
                    if (ny < ridge1)
                        color = Color.Lerp(color, new Color(0.005f, 0.024f, 0.055f, 1f), 0.86f);
                    if (ny < ridge2)
                        color = new Color(0.003f, 0.012f, 0.030f, 1f);

                    pixels[y * width + x] = color;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
