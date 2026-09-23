using System;
using System.Collections.Generic;
using UnityEngine;

namespace DontGetSidetracked.Presentation
{
    /// <summary>
    /// Central access point for the authored release texture skin.
    /// Images are stored as base64 TextAssets so GitHub transport remains deterministic;
    /// Unity only decodes and caches the already-authored pixels at runtime.
    /// </summary>
    internal static class ReleaseSkinAssets
    {
        private const string Root = "ReleaseSkin/";
        private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();

        public static Texture2D Background => LoadTexture("Backdrop");
        public static Sprite GlassPanel => LoadSliced("Glass", 24f);
        public static Sprite GameplayPanel => LoadSliced("Gameplay", 24f);
        public static Sprite RoutePanel => LoadSimple("Route");
        public static Sprite NavViolet => LoadSliced("NavViolet", 24f);
        public static Sprite PrimaryButton => LoadSliced("Primary", 24f);
        public static Sprite SecondaryButton => LoadSliced("Secondary", 24f);
        public static Sprite ScoreRing => LoadSimple("ScoreRing");

        public static bool HasCoreSkin =>
            Background != null &&
            GlassPanel != null &&
            PrimaryButton != null &&
            SecondaryButton != null;

        private static Sprite LoadSliced(string name, float borderPixels)
        {
            string key = name + ":sliced";
            if (Sprites.TryGetValue(key, out Sprite cached)) return cached;

            Texture2D texture = LoadTexture(name);
            if (texture == null)
            {
                Sprites[key] = null;
                return null;
            }

            float border = Mathf.Min(borderPixels, Mathf.Min(texture.width, texture.height) * 0.45f);
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
            sprite.name = "ReleaseSkin_" + name;
            Sprites[key] = sprite;
            return sprite;
        }

        private static Sprite LoadSimple(string name)
        {
            string key = name + ":simple";
            if (Sprites.TryGetValue(key, out Sprite cached)) return cached;

            Texture2D texture = LoadTexture(name);
            if (texture == null)
            {
                Sprites[key] = null;
                return null;
            }

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect);
            sprite.name = "ReleaseSkin_" + name;
            Sprites[key] = sprite;
            return sprite;
        }

        private static Texture2D LoadTexture(string name)
        {
            if (Textures.TryGetValue(name, out Texture2D cached)) return cached;

            TextAsset encoded = Resources.Load<TextAsset>(Root + name);
            if (encoded == null || string.IsNullOrWhiteSpace(encoded.text))
            {
                Textures[name] = null;
                return null;
            }

            try
            {
                byte[] bytes = Convert.FromBase64String(encoded.text.Trim());
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    name = "ReleaseSkin_" + name,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };

                if (!texture.LoadImage(bytes, true))
                {
                    UnityEngine.Object.Destroy(texture);
                    Textures[name] = null;
                    return null;
                }

                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Bilinear;
                Textures[name] = texture;
                return texture;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Release skin decode failed for " + name + ": " + ex.Message);
                Textures[name] = null;
                return null;
            }
        }
    }
}
