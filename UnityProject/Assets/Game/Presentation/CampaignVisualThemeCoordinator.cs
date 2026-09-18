using System;
using UnityEngine;
using UnityEngine.UI;

namespace DontGetSidetracked.Presentation
{
    /// <summary>
    /// Visual-only skin for CampaignCanvas. It never changes level state or progression.
    /// </summary>
    public sealed class CampaignVisualThemeCoordinator : MonoBehaviour
    {
        private static readonly Color Surface = new Color(0.045f, 0.065f, 0.115f, 0.99f);
        private static readonly Color SurfaceRaised = new Color(0.075f, 0.105f, 0.17f, 1f);
        private static readonly Color Cyan = new Color(0.29f, 0.91f, 1f, 1f);
        private static readonly Color Violet = new Color(0.34f, 0.20f, 1f, 1f);
        private static readonly Color Muted = new Color(0.38f, 0.43f, 0.52f, 0.72f);
        private static Sprite _rounded;
        private float _nextApply;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (FindFirstObjectByType<CampaignVisualThemeCoordinator>() != null) return;
            var root = new GameObject("CampaignVisualThemeCoordinator");
            DontDestroyOnLoad(root);
            root.AddComponent<CampaignVisualThemeCoordinator>();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextApply) return;
            _nextApply = Time.unscaledTime + 0.3f;
            Apply();
        }

        private static void Apply()
        {
            GameObject canvas = GameObject.Find("CampaignCanvas");
            if (canvas == null) return;
            if (_rounded == null) _rounded = CreateRoundedSprite(96, 22);

            Transform releaseVisual = FindTransform(canvas.transform, "ReleaseVisual");
            if (releaseVisual != null) return;

            Image panel = Find<Image>(canvas.transform, "CampaignPanel");
            if (panel != null)
            {
                panel.sprite = _rounded;
                panel.type = Image.Type.Sliced;
                panel.color = new Color(0.025f, 0.038f, 0.075f, 0.995f);
                Outline outline = panel.GetComponent<Outline>();
                if (outline == null) outline = panel.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.12f);
                outline.effectDistance = new Vector2(2f, -2f);
            }

            Text title = Find<Text>(canvas.transform, "Title");
            if (title != null)
            {
                title.color = Color.white;
                title.fontStyle = FontStyle.Bold;
            }

            Transform grid = FindTransform(canvas.transform, "LevelGrid");
            if (grid != null)
            {
                Button[] levels = grid.GetComponentsInChildren<Button>(true);
                for (int i = 0; i < levels.Length; i++) StyleLevel(levels[i]);
            }

            Button[] all = canvas.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (grid != null && all[i].transform.IsChildOf(grid)) continue;
                Text label = all[i].GetComponentInChildren<Text>(true);
                bool home = label != null && label.text.IndexOf("ДОМОЙ", StringComparison.OrdinalIgnoreCase) >= 0;
                StyleAction(all[i], home ? Violet : SurfaceRaised);
            }
        }

        private static void StyleLevel(Button button)
        {
            if (button == null) return;
            Image image = button.GetComponent<Image>();
            if (image == null) return;
            image.sprite = _rounded;
            image.type = Image.Type.Sliced;
            image.color = button.interactable ? SurfaceRaised : new Color(0.035f, 0.045f, 0.070f, 0.78f);

            ColorBlock block = button.colors;
            block.normalColor = image.color;
            block.highlightedColor = button.interactable ? new Color(0.10f, 0.18f, 0.27f, 1f) : image.color;
            block.pressedColor = button.interactable ? new Color(0.055f, 0.12f, 0.18f, 1f) : image.color;
            block.disabledColor = Muted;
            block.colorMultiplier = 1f;
            block.fadeDuration = 0.08f;
            button.colors = block;

            Text text = button.GetComponentInChildren<Text>(true);
            if (text != null)
            {
                text.color = button.interactable ? Color.white : new Color(0.48f, 0.53f, 0.62f, 0.85f);
                text.fontStyle = FontStyle.Bold;
            }

            Shadow shadow = button.GetComponent<Shadow>();
            if (shadow == null) shadow = button.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, button.interactable ? 0.28f : 0.14f);
            shadow.effectDistance = new Vector2(0f, -6f);
        }

        private static void StyleAction(Button button, Color normal)
        {
            if (button == null) return;
            Image image = button.GetComponent<Image>();
            if (image == null) return;
            image.sprite = _rounded;
            image.type = Image.Type.Sliced;
            image.color = normal;

            ColorBlock block = button.colors;
            block.normalColor = normal;
            block.highlightedColor = new Color(
                Mathf.Clamp01(normal.r + 0.08f),
                Mathf.Clamp01(normal.g + 0.08f),
                Mathf.Clamp01(normal.b + 0.08f), normal.a);
            block.pressedColor = new Color(normal.r * 0.82f, normal.g * 0.82f, normal.b * 0.82f, normal.a);
            block.disabledColor = new Color(normal.r, normal.g, normal.b, 0.28f);
            block.colorMultiplier = 1f;
            block.fadeDuration = 0.08f;
            button.colors = block;

            Text text = button.GetComponentInChildren<Text>(true);
            if (text != null)
            {
                text.color = Color.white;
                text.fontStyle = FontStyle.Bold;
            }
        }

        private static T Find<T>(Transform root, string name) where T : Component
        {
            Transform t = FindTransform(root, name);
            return t == null ? null : t.GetComponent<T>();
        }

        private static Transform FindTransform(Transform root, string name)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
                if (string.Equals(all[i].name, name, StringComparison.Ordinal)) return all[i];
            return null;
        }

        private static Sprite CreateRoundedSprite(int size, int radius)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "CampaignRoundedRect",
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
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }
    }
}
