#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace DontGetSidetracked.EditorTools
{
    /// <summary>
    /// Generates a deterministic launcher icon matching the in-game neon route identity.
    /// The PNG is generated locally from source code so the repository does not need opaque binary art.
    /// </summary>
    [InitializeOnLoad]
    public static class BrandAssetConfigurator
    {
        private const string BrandDirectory = "Assets/GeneratedBrand";
        private const string IconPath = BrandDirectory + "/AppIcon.png";
        private const int IconSize = 512;

        static BrandAssetConfigurator()
        {
            EditorApplication.delayCall += Configure;
        }

        [MenuItem("Tools/НЕ СБЕЙСЯ!/Generate Release Brand Assets")]
        public static void Configure()
        {
            try
            {
                Directory.CreateDirectory(BrandDirectory);

                if (!File.Exists(IconPath))
                {
                    Texture2D generated = GenerateIcon(IconSize);
                    File.WriteAllBytes(IconPath, generated.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(generated);
                    AssetDatabase.ImportAsset(IconPath, ImportAssetOptions.ForceSynchronousImport);
                }

                ConfigureImporter();
                Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
                if (icon == null)
                {
                    Debug.LogWarning("Generated launcher icon could not be imported.");
                    return;
                }

                int[] sizes = PlayerSettings.GetIconSizes(NamedBuildTarget.Android, IconKind.Application);
                if (sizes != null && sizes.Length > 0)
                {
                    var icons = new Texture2D[sizes.Length];
                    for (int i = 0; i < icons.Length; i++) icons[i] = icon;
                    PlayerSettings.SetIcons(NamedBuildTarget.Android, icons, IconKind.Application);
                }

                PlayerSettings.SplashScreen.backgroundColor = new Color(0.010f, 0.016f, 0.040f, 1f);
                AssetDatabase.SaveAssets();
            }
            catch (Exception error)
            {
                Debug.LogWarning("Brand asset generation failed: " + error.Message);
            }
        }

        private static void ConfigureImporter()
        {
            AssetImporter raw = AssetImporter.GetAtPath(IconPath);
            if (!(raw is TextureImporter importer)) return;

            bool dirty = false;
            if (importer.textureType != TextureImporterType.Default)
            {
                importer.textureType = TextureImporterType.Default;
                dirty = true;
            }
            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                dirty = true;
            }
            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                dirty = true;
            }
            if (importer.maxTextureSize < IconSize)
            {
                importer.maxTextureSize = IconSize;
                dirty = true;
            }

            if (dirty) importer.SaveAndReimport();
        }

        private static Texture2D GenerateIcon(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "GeneratedNesbeisyaAppIcon",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                float ny = y / (float)(size - 1);
                for (int x = 0; x < size; x++)
                {
                    float nx = x / (float)(size - 1);
                    float cyanGlow = Glow(nx, ny, 0.18f, 0.82f, 0.72f);
                    float violetGlow = Glow(nx, ny, 0.84f, 0.18f, 0.68f);

                    float r = 0.010f + cyanGlow * 0.035f + violetGlow * 0.085f;
                    float g = 0.018f + cyanGlow * 0.105f + violetGlow * 0.035f;
                    float b = 0.050f + cyanGlow * 0.145f + violetGlow * 0.130f;
                    pixels[y * size + x] = ToColor32(new Color(r, g, b, 1f));
                }
            }

            texture.SetPixels32(pixels);

            Color grid = new Color(0.20f, 0.62f, 0.90f, 0.07f);
            for (int i = 1; i < 8; i++)
            {
                int v = Mathf.RoundToInt(i * size / 8f);
                DrawLine(texture, new Vector2Int(v, 72), new Vector2Int(v, size - 72), 1, grid);
                DrawLine(texture, new Vector2Int(72, v), new Vector2Int(size - 72, v), 1, grid);
            }

            Vector2Int[] route =
            {
                Point(size, 0.20f, 0.34f),
                Point(size, 0.34f, 0.59f),
                Point(size, 0.52f, 0.46f),
                Point(size, 0.68f, 0.67f),
                Point(size, 0.81f, 0.49f),
            };

            Color cyanSoft = new Color(0.12f, 0.74f, 1f, 0.18f);
            Color cyan = new Color(0.23f, 0.91f, 1f, 1f);
            Color highlight = new Color(0.76f, 0.98f, 1f, 0.92f);
            for (int i = 0; i < route.Length - 1; i++)
            {
                DrawLine(texture, route[i], route[i + 1], 28, cyanSoft);
                DrawLine(texture, route[i], route[i + 1], 12, cyan);
                DrawLine(texture, route[i], route[i + 1], 3, highlight);
            }

            DrawCircle(texture, route[0], 22, new Color(0.20f, 1f, 0.58f, 0.24f));
            DrawCircle(texture, route[0], 13, new Color(0.29f, 1f, 0.62f, 1f));
            DrawCircle(texture, route[route.Length - 1], 22, new Color(1f, 0.34f, 0.45f, 0.24f));
            DrawCircle(texture, route[route.Length - 1], 13, new Color(1f, 0.38f, 0.48f, 1f));

            DrawRoundedDash(texture,
                Mathf.RoundToInt(size * 0.20f),
                Mathf.RoundToInt(size * 0.79f),
                Mathf.RoundToInt(size * 0.31f),
                Mathf.Max(5, size / 70),
                cyan);

            texture.Apply(false, false);
            return texture;
        }

        private static float Glow(float x, float y, float cx, float cy, float radius)
        {
            float distance = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
            float t = Mathf.Clamp01(1f - distance / radius);
            return t * t;
        }

        private static Vector2Int Point(int size, float x, float y) =>
            new Vector2Int(Mathf.RoundToInt(size * x), Mathf.RoundToInt(size * y));

        private static void DrawLine(Texture2D texture, Vector2Int a, Vector2Int b, int width, Color color)
        {
            float distance = Vector2.Distance(a, b);
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance));
            int radius = Mathf.Max(1, width / 2);
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                int x = Mathf.RoundToInt(Mathf.Lerp(a.x, b.x, t));
                int y = Mathf.RoundToInt(Mathf.Lerp(a.y, b.y, t));
                DrawCircle(texture, new Vector2Int(x, y), radius, color);
            }
        }

        private static void DrawCircle(Texture2D texture, Vector2Int center, int radius, Color color)
        {
            int xMin = Mathf.Max(0, center.x - radius);
            int xMax = Mathf.Min(texture.width - 1, center.x + radius);
            int yMin = Mathf.Max(0, center.y - radius);
            int yMax = Mathf.Min(texture.height - 1, center.y + radius);
            float r2 = radius * radius;

            for (int y = yMin; y <= yMax; y++)
            {
                for (int x = xMin; x <= xMax; x++)
                {
                    float dx = x - center.x;
                    float dy = y - center.y;
                    if (dx * dx + dy * dy > r2) continue;

                    Color dst = texture.GetPixel(x, y);
                    texture.SetPixel(x, y, AlphaBlend(dst, color));
                }
            }
        }

        private static void DrawRoundedDash(Texture2D texture, int x, int y, int width, int height, Color color)
        {
            int radius = Mathf.Max(1, height / 2);
            DrawLine(texture,
                new Vector2Int(x + radius, y),
                new Vector2Int(x + width - radius, y),
                height,
                color);
        }

        private static Color AlphaBlend(Color dst, Color src)
        {
            float a = Mathf.Clamp01(src.a);
            return new Color(
                Mathf.Lerp(dst.r, src.r, a),
                Mathf.Lerp(dst.g, src.g, a),
                Mathf.Lerp(dst.b, src.b, a),
                1f);
        }

        private static Color32 ToColor32(Color color) =>
            new Color32(
                (byte)Mathf.RoundToInt(Mathf.Clamp01(color.r) * 255f),
                (byte)Mathf.RoundToInt(Mathf.Clamp01(color.g) * 255f),
                (byte)Mathf.RoundToInt(Mathf.Clamp01(color.b) * 255f),
                255);
    }
}
#endif
