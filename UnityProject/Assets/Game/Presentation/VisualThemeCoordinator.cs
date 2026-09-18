using System;
using UnityEngine;
using UnityEngine.UI;

namespace DontGetSidetracked.Presentation
{
    /// <summary>
    /// Applies the production visual language to the programmatically-created MVP UI.
    /// Presentation-only: it never changes gameplay state, scoring or persistence.
    /// </summary>
    public sealed class VisualThemeCoordinator : MonoBehaviour
    {
        private enum ButtonVariant { Primary, Secondary, Accent, Ghost }

        private static readonly Color BackgroundTop = new Color(0.025f, 0.035f, 0.075f, 1f);
        private static readonly Color BackgroundBottom = new Color(0.008f, 0.012f, 0.035f, 1f);
        private static readonly Color Surface = new Color(0.055f, 0.075f, 0.125f, 0.96f);
        private static readonly Color SurfaceRaised = new Color(0.075f, 0.105f, 0.17f, 0.98f);
        private static readonly Color TextPrimary = new Color(0.96f, 0.98f, 1f, 1f);
        private static readonly Color TextMuted = new Color(0.64f, 0.71f, 0.82f, 1f);
        private static readonly Color Cyan = new Color(0.29f, 0.91f, 1f, 1f);
        private static readonly Color Violet = new Color(0.55f, 0.43f, 1f, 1f);
        private static readonly Color Green = new Color(0.34f, 1f, 0.58f, 1f);

        private static Sprite _roundedSprite;
        private static Sprite _circleSprite;
        private static Texture2D _backgroundTexture;
        private float _nextRefresh;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (FindFirstObjectByType<VisualThemeCoordinator>() != null) return;
            var root = new GameObject("VisualThemeCoordinator");
            DontDestroyOnLoad(root);
            root.AddComponent<VisualThemeCoordinator>();
        }

        private void Awake()
        {
            EnsureGeneratedAssets();
            ApplyTheme();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + 0.35f;
            ApplyTheme();
        }

        private static void ApplyTheme()
        {
            GameObject game = GameObject.Find("GameCanvas");
            if (game != null) StyleGameCanvas(game.transform);

            GameObject meta = GameObject.Find("MetaCanvas");
            if (meta != null) StyleMetaCanvas(meta.transform);

            GameObject result = GameObject.Find("ResultEnhancementCanvas");
            if (result != null) StyleResultCanvas(result.transform);

            StyleAuxiliaryCanvas("HintCanvas");
            StyleAuxiliaryCanvas("RewardedAdCanvas");
            StyleAuxiliaryCanvas("NotificationValueCanvas");
        }

        private static void StyleGameCanvas(Transform root)
        {
            EnsureBackground(root);

            Text title = Find<Text>(root, "Title");
            bool resultState = title != null && string.Equals(title.text, "РЕЗУЛЬТАТ", StringComparison.Ordinal);
            if (title != null)
            {
                title.color = resultState ? Cyan : TextPrimary;
                title.fontStyle = FontStyle.Bold;
                title.fontSize = resultState ? 42 : 64;
                title.resizeTextMinSize = resultState ? 24 : 28;
                title.resizeTextMaxSize = resultState ? 42 : 64;
                ApplyTextShadow(title, 0.48f, new Vector2(0f, -3f));
            }

            Text status = Find<Text>(root, "Status");
            if (status != null)
            {
                status.color = resultState ? TextPrimary : TextMuted;
                status.fontStyle = resultState ? FontStyle.Bold : FontStyle.Normal;
                status.fontSize = resultState ? 86 : 50;
                status.resizeTextMinSize = resultState ? 46 : 22;
                status.resizeTextMaxSize = resultState ? 86 : 50;
                status.lineSpacing = 1.12f;
                ApplyTextShadow(status, resultState ? 0.52f : 0.28f, new Vector2(0f, -2f));
            }

            Image playArea = Find<Image>(root, "PlayArea");
            if (playArea != null)
            {
                StylePanel(playArea, Surface, true);
                RectTransform rt = playArea.rectTransform;
                rt.anchorMin = new Vector2(0.065f, 0.26f);
                rt.anchorMax = new Vector2(0.935f, 0.745f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            Button primary = Find<Button>(root, "Primary");
            Button secondary = Find<Button>(root, "Secondary");
            Button share = Find<Button>(root, "Share");
            StyleButton(primary, ButtonVariant.Primary);
            StyleButton(secondary, ButtonVariant.Secondary);
            StyleButton(share, ButtonVariant.Accent);

            Image start = Find<Image>(root, "Start");
            Image end = Find<Image>(root, "End");
            StyleMarker(start, Green);
            StyleMarker(end, new Color(1f, 0.40f, 0.48f, 1f));

            RouteGraphic reference = Find<RouteGraphic>(root, "Reference");
            RouteGraphic player = Find<RouteGraphic>(root, "Player");
            StyleRouteGlow(reference, 0.30f, 3f);
            StyleRouteGlow(player, 0.26f, 3f);
        }

        private static void StyleMetaCanvas(Transform root)
        {
            Transform releaseVisual = FindTransform(root, "ReleaseVisual");
            if (releaseVisual != null) return;

            Transform home = FindTransform(root, "HomeMetaButtons");
            if (home != null)
            {
                Button[] buttons = home.GetComponentsInChildren<Button>(true);
                for (int i = 0; i < buttons.Length; i++)
                {
                    Text label = buttons[i].GetComponentInChildren<Text>(true);
                    bool store = label != null && label.text.IndexOf("МАГАЗИН", StringComparison.OrdinalIgnoreCase) >= 0;
                    StyleButton(buttons[i], store ? ButtonVariant.Accent : ButtonVariant.Secondary, 25);
                }
            }

            Image panel = Find<Image>(root, "MetaPanel");
            if (panel != null) StylePanel(panel, new Color(0.035f, 0.050f, 0.090f, 0.985f), true);

            Transform panelRoot = FindTransform(root, "MetaPanel");
            if (panelRoot != null)
            {
                Text title = Find<Text>(panelRoot, "Title");
                if (title != null)
                {
                    title.color = TextPrimary;
                    title.fontStyle = FontStyle.Bold;
                    ApplyTextShadow(title, 0.35f, new Vector2(0f, -2f));
                }

                Text body = Find<Text>(panelRoot, "Body");
                if (body != null)
                {
                    body.color = TextMuted;
                    body.lineSpacing = 1.16f;
                }

                Transform actions = FindTransform(panelRoot, "Actions");
                if (actions != null)
                {
                    Button[] actionButtons = actions.GetComponentsInChildren<Button>(true);
                    for (int i = 0; i < actionButtons.Length; i++)
                        StyleButton(actionButtons[i], ButtonVariant.Secondary, 28);
                }

                Button[] all = panelRoot.GetComponentsInChildren<Button>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    Text label = all[i].GetComponentInChildren<Text>(true);
                    if (label != null && label.text.IndexOf("ЗАКРЫТЬ", StringComparison.OrdinalIgnoreCase) >= 0)
                        StyleButton(all[i], ButtonVariant.Ghost, 26);
                }
            }
        }

        private static void StyleResultCanvas(Transform root)
        {
            Text medal = Find<Text>(root, "Medal");
            if (medal != null)
            {
                medal.fontStyle = FontStyle.Bold;
                ApplyTextShadow(medal, 0.55f, new Vector2(0f, -3f));
            }

            Text record = Find<Text>(root, "DailyRecord");
            if (record != null)
            {
                record.color = new Color(1f, 0.82f, 0.34f, 1f);
                record.fontStyle = FontStyle.Bold;
            }

            Button card = Find<Button>(root, "ShareCard");
            StyleButton(card, ButtonVariant.Accent, 25);
        }

        private static void StyleAuxiliaryCanvas(string canvasName)
        {
            GameObject go = GameObject.Find(canvasName);
            if (go == null) return;

            Button[] buttons = go.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
                StyleButton(buttons[i], i == 0 ? ButtonVariant.Primary : ButtonVariant.Secondary, 26);

            Text[] texts = go.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i].GetComponentInParent<Button>() != null) continue;
                texts[i].color = TextPrimary;
                ApplyTextShadow(texts[i], 0.30f, new Vector2(0f, -2f));
            }
        }

        private static void EnsureBackground(Transform root)
        {
            Transform existing = root.Find("VisualBackground");
            if (existing != null)
            {
                existing.SetAsFirstSibling();
                return;
            }

            var go = new GameObject("VisualBackground", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(root, false);
            go.transform.SetAsFirstSibling();
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            RawImage image = go.GetComponent<RawImage>();
            image.texture = _backgroundTexture;
            image.color = Color.white;
            image.raycastTarget = false;
        }

        private static void StylePanel(Image image, Color color, bool shadow)
        {
            if (image == null) return;
            image.sprite = _roundedSprite;
            image.type = Image.Type.Sliced;
            image.color = color;

            Outline outline = image.GetComponent<Outline>();
            if (outline == null) outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.11f);
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = true;

            if (shadow)
            {
                Shadow drop = image.GetComponent<Shadow>();
                if (drop == null) drop = image.gameObject.AddComponent<Shadow>();
                drop.effectColor = new Color(0f, 0f, 0f, 0.42f);
                drop.effectDistance = new Vector2(0f, -10f);
                drop.useGraphicAlpha = true;
            }
        }

        private static void StyleMarker(Image marker, Color color)
        {
            if (marker == null) return;
            marker.sprite = _circleSprite;
            marker.type = Image.Type.Simple;
            marker.color = color;
            Shadow shadow = marker.GetComponent<Shadow>();
            if (shadow == null) shadow = marker.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(color.r, color.g, color.b, 0.55f);
            shadow.effectDistance = new Vector2(0f, -2f);
        }

        private static void StyleRouteGlow(RouteGraphic graphic, float alpha, float distance)
        {
            if (graphic == null) return;
            Shadow shadow = graphic.GetComponent<Shadow>();
            if (shadow == null) shadow = graphic.gameObject.AddComponent<Shadow>();
            Color color = graphic.color;
            shadow.effectColor = new Color(color.r, color.g, color.b, Mathf.Clamp01(color.a * alpha));
            shadow.effectDistance = new Vector2(distance, -distance);
            shadow.useGraphicAlpha = true;
        }

        private static void StyleButton(Button button, ButtonVariant variant, int fontSize = 32)
        {
            if (button == null) return;
            Image image = button.GetComponent<Image>();
            if (image == null) return;

            Color normal;
            Color textColor;
            switch (variant)
            {
                case ButtonVariant.Primary:
                    normal = Cyan;
                    textColor = new Color(0.025f, 0.06f, 0.09f, 1f);
                    break;
                case ButtonVariant.Accent:
                    normal = Violet;
                    textColor = Color.white;
                    break;
                case ButtonVariant.Ghost:
                    normal = new Color(0.12f, 0.15f, 0.22f, 0.72f);
                    textColor = TextMuted;
                    break;
                default:
                    normal = SurfaceRaised;
                    textColor = TextPrimary;
                    break;
            }

            image.sprite = _roundedSprite;
            image.type = Image.Type.Sliced;
            image.color = normal;

            ColorBlock colors = button.colors;
            colors.normalColor = normal;
            colors.highlightedColor = Lighten(normal, 0.09f);
            colors.pressedColor = Darken(normal, 0.14f);
            colors.selectedColor = Lighten(normal, 0.05f);
            colors.disabledColor = new Color(normal.r, normal.g, normal.b, 0.35f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.transition = Selectable.Transition.ColorTint;

            Shadow shadow = button.GetComponent<Shadow>();
            if (shadow == null) shadow = button.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, variant == ButtonVariant.Primary ? 0.34f : 0.26f);
            shadow.effectDistance = new Vector2(0f, -7f);
            shadow.useGraphicAlpha = true;

            Text label = button.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.color = textColor;
                label.fontStyle = FontStyle.Bold;
                label.fontSize = fontSize;
                label.resizeTextMinSize = 16;
                label.resizeTextMaxSize = fontSize;
                label.raycastTarget = false;
            }
        }

        private static void ApplyTextShadow(Text text, float alpha, Vector2 distance)
        {
            if (text == null) return;
            Shadow shadow = text.GetComponent<Shadow>();
            if (shadow == null) shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, alpha);
            shadow.effectDistance = distance;
            shadow.useGraphicAlpha = true;
        }

        private static T Find<T>(Transform root, string objectName) where T : Component
        {
            Transform transform = FindTransform(root, objectName);
            return transform == null ? null : transform.GetComponent<T>();
        }

        private static Transform FindTransform(Transform root, string objectName)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
                if (string.Equals(all[i].name, objectName, StringComparison.Ordinal)) return all[i];
            return null;
        }

        private static Color Lighten(Color color, float amount) => new Color(
            Mathf.Clamp01(color.r + amount),
            Mathf.Clamp01(color.g + amount),
            Mathf.Clamp01(color.b + amount),
            color.a);

        private static Color Darken(Color color, float amount) => new Color(
            Mathf.Clamp01(color.r - amount),
            Mathf.Clamp01(color.g - amount),
            Mathf.Clamp01(color.b - amount),
            color.a);

        private static void EnsureGeneratedAssets()
        {
            if (_roundedSprite == null) _roundedSprite = CreateRoundedSprite(96, 24);
            if (_circleSprite == null) _circleSprite = CreateCircleSprite(64);
            if (_backgroundTexture == null) _backgroundTexture = CreateBackgroundTexture(96, 160);
        }

        private static Sprite CreateRoundedSprite(int size, int radius)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "UiRoundedRect",
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
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            sprite.name = "UiRoundedRectSprite";
            return sprite;
        }

        private static Sprite CreateCircleSprite(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "UiCircle",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            Color32[] pixels = new Color32[size * size];
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = size * 0.48f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center);
                    byte alpha = d <= radius - 1f ? (byte)255 : d <= radius + 0.5f ? (byte)150 : (byte)0;
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = "UiCircleSprite";
            return sprite;
        }

        private static Texture2D CreateBackgroundTexture(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "UiBackgroundGradient",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                float ny = y / (float)(height - 1);
                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)(width - 1);
                    Color color = Color.Lerp(BackgroundBottom, BackgroundTop, ny);
                    float cyanGlow = Mathf.Pow(Mathf.Clamp01(1f - Vector2.Distance(new Vector2(nx, ny), new Vector2(0.12f, 0.90f)) / 0.70f), 2.4f);
                    float violetGlow = Mathf.Pow(Mathf.Clamp01(1f - Vector2.Distance(new Vector2(nx, ny), new Vector2(0.92f, 0.18f)) / 0.72f), 2.2f);
                    color.r = Mathf.Clamp01(color.r + Cyan.r * cyanGlow * 0.12f + Violet.r * violetGlow * 0.10f);
                    color.g = Mathf.Clamp01(color.g + Cyan.g * cyanGlow * 0.12f + Violet.g * violetGlow * 0.10f);
                    color.b = Mathf.Clamp01(color.b + Cyan.b * cyanGlow * 0.13f + Violet.b * violetGlow * 0.12f);
                    pixels[y * width + x] = color;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
