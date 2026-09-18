using System;
using UnityEngine;
using UnityEngine.UI;

namespace DontGetSidetracked.Presentation
{
    /// <summary>
    /// Styles product surfaces added after the base visual theme.
    /// CampaignCanvas has its own CampaignVisualThemeCoordinator so there is one visual owner per surface.
    /// </summary>
    public sealed class ExtendedUiThemeCoordinator : MonoBehaviour
    {
        private static Sprite _rounded;
        private float _nextRefresh;

        private static readonly Color Surface = new Color(0.045f, 0.065f, 0.115f, 0.985f);
        private static readonly Color SurfaceRaised = new Color(0.075f, 0.105f, 0.17f, 1f);
        private static readonly Color Cyan = new Color(0.29f, 0.91f, 1f, 1f);
        private static readonly Color Violet = new Color(0.55f, 0.43f, 1f, 1f);
        private static readonly Color Text = new Color(0.96f, 0.98f, 1f, 1f);
        private static readonly Color Muted = new Color(0.62f, 0.72f, 0.84f, 1f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (FindFirstObjectByType<ExtendedUiThemeCoordinator>() != null) return;
            var root = new GameObject("ExtendedUiThemeCoordinator");
            DontDestroyOnLoad(root);
            root.AddComponent<ExtendedUiThemeCoordinator>();
        }

        private void Awake()
        {
            EnsureAssets();
            Apply();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + 0.4f;
            Apply();
        }

        private static void Apply()
        {
            StyleTraining();
            StyleRewarded();
        }

        private static void StyleTraining()
        {
            GameObject canvas = GameObject.Find("TrainingSelectCanvas");
            if (canvas == null) return;
            Transform panel = Find(canvas.transform, "TrainingPanel");
            if (panel == null) return;
            if (Find(panel, "ReleaseVisual") != null) return;
            StylePanel(panel.GetComponent<Image>());

            Text title = Find<Text>(panel, "Title");
            if (title != null)
            {
                title.color = Text;
                title.fontStyle = FontStyle.Bold;
            }
            Text subtitle = Find<Text>(panel, "Subtitle");
            if (subtitle != null) subtitle.color = Muted;

            Button[] buttons = panel.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Text label = buttons[i].GetComponentInChildren<Text>(true);
                string text = label?.text ?? string.Empty;
                Color background = text.IndexOf("СЛУЧАЙНАЯ", StringComparison.OrdinalIgnoreCase) >= 0 ? Violet : SurfaceRaised;
                StyleButton(buttons[i], background, Text);
            }
        }

        private static void StyleRewarded()
        {
            GameObject canvas = GameObject.Find("RewardedCanvas");
            if (canvas == null) return;
            Button button = canvas.GetComponentInChildren<Button>(true);
            if (button == null) return;
            StyleButton(button, new Color(Cyan.r, Cyan.g, Cyan.b, 0.90f), new Color(0.02f, 0.05f, 0.08f, 1f));
            Shadow shadow = button.GetComponent<Shadow>();
            if (shadow == null) shadow = button.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.20f);
            shadow.effectDistance = new Vector2(0f, -3f);
        }

        private static void StylePanel(Image image)
        {
            if (image == null) return;
            image.sprite = _rounded;
            image.type = Image.Type.Sliced;
            image.color = Surface;
            Shadow shadow = image.GetComponent<Shadow>();
            if (shadow == null) shadow = image.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
            shadow.effectDistance = new Vector2(0f, -10f);
        }

        private static void StyleButton(Button button, Color background, Color textColor)
        {
            if (button == null) return;
            Image image = button.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = _rounded;
                image.type = Image.Type.Sliced;
                image.color = background;
            }

            ColorBlock colors = button.colors;
            colors.normalColor = background;
            colors.highlightedColor = Lighten(background, 0.08f);
            colors.pressedColor = Darken(background, 0.12f);
            colors.disabledColor = new Color(background.r, background.g, background.b, 0.28f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            Text label = button.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.color = button.interactable ? textColor : Muted;
                label.fontStyle = FontStyle.Bold;
            }
        }

        private static T Find<T>(Transform root, string name) where T : Component
        {
            Transform transform = Find(root, name);
            return transform == null ? null : transform.GetComponent<T>();
        }

        private static Transform Find(Transform root, string name)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
                if (string.Equals(all[i].name, name, StringComparison.Ordinal)) return all[i];
            return null;
        }

        private static Color Lighten(Color c, float amount) => new Color(
            Mathf.Clamp01(c.r + amount), Mathf.Clamp01(c.g + amount), Mathf.Clamp01(c.b + amount), c.a);

        private static Color Darken(Color c, float amount) => new Color(
            Mathf.Clamp01(c.r - amount), Mathf.Clamp01(c.g - amount), Mathf.Clamp01(c.b - amount), c.a);

        private static void EnsureAssets()
        {
            if (_rounded != null) return;
            const int size = 96;
            const int radius = 24;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "TrainingUiRounded",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float cx = x < radius ? radius : x >= size - radius ? size - radius - 1 : x;
                    float cy = y < radius ? radius : y >= size - radius ? size - radius - 1 : y;
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                    byte alpha = distance <= radius - 0.5f ? (byte)255 : distance <= radius + 0.75f ? (byte)170 : (byte)0;
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Vector4 border = new Vector4(radius + 2, radius + 2, radius + 2, radius + 2);
            _rounded = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }
    }
}
