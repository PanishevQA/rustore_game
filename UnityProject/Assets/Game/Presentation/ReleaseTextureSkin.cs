using System;
using System.Collections.Generic;
using UnityEngine;

namespace DontGetSidetracked.Presentation
{
    /// <summary>
    /// Loads the pre-rendered release skin from embedded Resource TextAssets.
    /// The images are authored assets; runtime code only decodes/caches them.
    /// Missing or invalid assets safely fall back to the procedural release UI.
    /// </summary>
    internal static class ReleaseTextureSkin
    {
        private const string Root = "ReleaseSkin/";
        private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();

        public static Texture2D BackdropTexture => LoadTexture("Backdrop");
        public static Sprite Primary => LoadSliced("Primary", 24f);
        public static Sprite Secondary => LoadSliced("Secondary", 24f);
        public static Sprite Glass => LoadSliced("Glass", 24f);
        public static Sprite Gameplay => LoadSliced("Gameplay", 24f);
        public static Sprite NavViolet => LoadSliced("NavViolet", 24f);
        public static Sprite Route => LoadSimple("Route");
        public static Sprite ScoreRing => LoadSimple("ScoreRing");

        public static bool HasCoreSkin =>
            BackdropTexture != null &&
            Primary != null &&
            Secondary != null &&
            Glass != null;

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

            float maxBorder = Mathf.Max(0f, Mathf.Min(texture.width, texture.height) * 0.45f);
            float border = Mathf.Min(borderPixels, maxBorder);
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
                Debug.LogWarning("Release texture skin failed to decode " + name + ": " + ex.Message);
                Textures[name] = null;
                return null;
            }
        }
    }
}
