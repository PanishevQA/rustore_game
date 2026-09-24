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
        private static Sprite _glassSheen;
        private static Sprite _glassPanel;
        private static Sprite _navViolet;
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

        public static Image Icon(Transform parent, string name, string assetName, Vector2 min, Vector2 max)
        {
            Image icon = ReleaseUiKit.Rect(parent, name, min, max).gameObject.AddComponent<Image>();
            GeneratedUiAssets.TryApply(icon, assetName);
            icon.raycastTarget = false;
            return icon;
        }

        public static Button GhostButton(Transform parent, string name, string label,
            Vector2 min, Vector2 max, UnityEngine.Events.UnityAction action, int fontSize = 24)
        {
            Button button = SecondaryButton(parent, name, label, min, max, action, fontSize);
            button.GetComponent<Image>().color = new Color(0.08f, 0.15f, 0.24f, 0.65f);
            button.GetComponent<Outline>().effectColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.16f);
            return button;
        }

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
                ? new Color(0.94f, 0.98f, 1f, 1f)
                : new Color(0.88f, 0.94f, 1f, 1f);
            Image card = ReleaseUiKit.Panel(parent, name, min, max, fill, accent, true);

            Sprite authoredPanel = IsVioletAccent(accent) && _navViolet != null ? _navViolet : _glassPanel;
            if (authoredPanel != null)
            {
                card.sprite = authoredPanel;
                card.type = Image.Type.Sliced;
                card.color = Color.white;

                Outline outline = card.GetComponent<Outline>();
                if (outline != null) outline.enabled = false;
                Shadow shadow = card.GetComponent<Shadow>();
                if (shadow != null)
                {
                    shadow.effectColor = new Color(0f, 0f, 0f, 0.34f);
                    shadow.effectDistance = new Vector2(0f, -8f);
                }
                return card;
            }

            Transform sheenRoot = ReleaseUiKit.Rect(card.transform, "GlassSheen",
                new Vector2(0.018f, 0.57f), new Vector2(0.982f, 0.985f));
            Image sheen = sheenRoot.gameObject.AddComponent<Image>();
            sheen.sprite = _glassSheen;
            sheen.type = Image.Type.Sliced;
            sheen.color = new Color(accent.r, accent.g, accent.b, strong ? 0.14f : 0.075f);
            sheen.raycastTarget = false;

            Outline fallbackOutline = card.GetComponent<Outline>();
            if (fallbackOutline != null)
            {
                fallbackOutline.effectColor = new Color(accent.r, accent.g, accent.b, strong ? 0.34f : 0.20f);
                fallbackOutline.effectDistance = new Vector2(2f, -2f);
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
            bool authoredPrimary = _primaryGradient == ReleaseSkinAssets.PrimaryButton;
            image.type = authoredPrimary ? Image.Type.Simple : Image.Type.Sliced;
            image.color = Color.white;
            image.preserveAspect = false;

            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            if (action != null) button.onClick.AddListener(action);
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
            colors.pressedColor = new Color(0.64f, 0.76f, 0.94f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.36f, 0.45f, 0.60f, 0.80f);
            colors.fadeDuration = 0.07f;
            button.colors = colors;

            Shadow shadow = root.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(Cyan.r, Cyan.g, Cyan.b, authoredPrimary ? 0.18f : 0.30f);
            shadow.effectDistance = new Vector2(0f, -7f);

            if (!authoredPrimary)
            {
                Outline glow = root.gameObject.AddComponent<Outline>();
                glow.effectColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.38f);
                glow.effectDistance = new Vector2(2f, -2f);

                Transform sheenRoot = ReleaseUiKit.Rect(root, "ButtonSheen",
                    new Vector2(0.025f, 0.54f), new Vector2(0.975f, 0.955f));
                Image sheen = sheenRoot.gameObject.AddComponent<Image>();
                sheen.sprite = _glassSheen;
                sheen.type = Image.Type.Sliced;
                sheen.color = new Color(1f, 1f, 1f, 0.16f);
                sheen.raycastTarget = false;
            }

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
            image.sprite = _secondaryGradient ?? ReleaseUiKit.Rounded;
            image.type = Image.Type.Sliced;
            image.color = Color.white;

            Outline outline = root.gameObject.AddComponent<Outline>();
            bool authoredSecondary = _secondaryGradient == ReleaseSkinAssets.SecondaryButton;
            outline.effectColor = authoredSecondary
                ? new Color(Cyan.r, Cyan.g, Cyan.b, 0.06f)
                : new Color(Cyan.r, Cyan.g, Cyan.b, 0.42f);
            outline.effectDistance = new Vector2(2f, -2f);

            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            if (action != null) button.onClick.AddListener(action);
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
            colors.selectedColor = new Color(1.10f, 1.10f, 1.10f, 1f);
            colors.pressedColor = new Color(0.60f, 0.72f, 0.88f, 1f);
            colors.disabledColor = new Color(0.62f, 0.68f, 0.78f, 0.80f);
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

        private static bool IsVioletAccent(Color accent)
        {
            return accent.b > 0.72f && accent.r > 0.32f && accent.g < 0.58f;
        }

        private static void EnsureAssets()
        {
            if (_primaryGradient == null)
                _primaryGradient = ReleaseSkinAssets.PrimaryButton ??
                    CreateGradientRoundedSprite(256, 80, 24, Cyan, Violet);
            if (_secondaryGradient == null)
                _secondaryGradient = ReleaseSkinAssets.SecondaryButton ??
                    CreateGradientRoundedSprite(256, 80, 24,
                        new Color(0.02f, 0.08f, 0.16f, 1f),
                        new Color(0.05f, 0.04f, 0.16f, 1f));
            if (_glassPanel == null)
                _glassPanel = ReleaseSkinAssets.GlassPanel;
            if (_navViolet == null)
                _navViolet = ReleaseSkinAssets.NavViolet;
            if (_glassSheen == null)
                _glassSheen = CreateVerticalSheenSprite(96, 64, 20);
            if (_backdropTexture == null)
                _backdropTexture = ReleaseSkinAssets.Background ?? CreateBackdropTexture(216, 384);
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

        private static Sprite CreateVerticalSheenSprite(int width, int height, int radius)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "ReleaseGlassSheen",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color32[] pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float ny = y / (float)Math.Max(1, height - 1);
                float alpha = Mathf.SmoothStep(0f, 1f, ny) * 0.30f;
                for (int x = 0; x < width; x++)
                {
                    float dx = x < radius ? radius - x : x >= width - radius ? x - (width - radius - 1) : 0f;
                    float dy = y < radius ? radius - y : y >= height - radius ? y - (height - radius - 1) : 0f;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float mask = distance <= radius ? 1f : 0f;
                    pixels[y * width + x] = new Color(1f, 1f, 1f, alpha * mask);
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

            Color top = new Color(0.026f, 0.100f, 0.205f, 1f);
            Color middle = new Color(0.014f, 0.050f, 0.115f, 1f);
            Color bottom = new Color(0.004f, 0.016f, 0.045f, 1f);
            Color[] pixels = new Color[width * height];

            for (int y = 0; y < height; y++)
            {
                float ny = y / (float)Math.Max(1, height - 1);
                Color baseColor = ny > 0.50f
                    ? Color.Lerp(middle, top, (ny - 0.50f) / 0.50f)
                    : Color.Lerp(bottom, middle, ny / 0.50f);

                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)Math.Max(1, width - 1);
                    Color color = baseColor;

                    float cyanGlow = Mathf.Pow(Mathf.Clamp01(1f - Vector2.Distance(
                        new Vector2(nx, ny), new Vector2(0.18f, 0.84f)) / 0.48f), 1.55f);
                    float violetGlow = Mathf.Pow(Mathf.Clamp01(1f - Vector2.Distance(
                        new Vector2(nx, ny), new Vector2(0.86f, 0.60f)) / 0.50f), 1.65f);
                    color += new Color(0.026f, 0.095f, 0.145f, 0f) * cyanGlow;
                    color += new Color(0.080f, 0.040f, 0.155f, 0f) * violetGlow;

                    int starHash = (x * 43 + y * 79 + x * y * 5) % 431;
                    if (ny > 0.22f && starHash == 0)
                    {
                        float sparkle = ((x + y) % 3 == 0) ? 0.78f : 0.52f;
                        color = Color.Lerp(color, new Color(0.46f, 0.82f, 1f, 1f), sparkle);
                    }

                    float horizon = 0.095f + 0.026f * Mathf.Sin(nx * 10.5f)
                        + 0.016f * Mathf.Sin(nx * 27.0f + 0.9f);
                    if (ny < horizon)
                        color = Color.Lerp(color, new Color(0.002f, 0.015f, 0.038f, 1f), 0.92f);

                    float horizontalEdge = Mathf.Min(nx, 1f - nx);
                    float vignette = Mathf.SmoothStep(0f, 0.085f, horizontalEdge);
                    float bottomDark = Mathf.SmoothStep(0.10f, 0.44f, ny);
                    float mask = Mathf.Clamp01(vignette * (0.78f + 0.22f * bottomDark));
                    color = Color.Lerp(new Color(0.001f, 0.004f, 0.014f, 1f), color, mask);

                    pixels[y * width + x] = color;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
