using System;
using System.Collections.Generic;
using DontGetSidetracked.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DontGetSidetracked.Presentation
{
    /// <summary>
    /// Decorative Home-only composition. It is intentionally presentation-only and never participates in scoring/input.
    /// </summary>
    public sealed class HomeHeroCoordinator : MonoBehaviour
    {
        private static Sprite _circleSprite;
        private GameObject _hero;
        private Text _title;
        private float _nextResolve;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (FindFirstObjectByType<HomeHeroCoordinator>() != null) return;
            var root = new GameObject("HomeHeroCoordinator");
            DontDestroyOnLoad(root);
            root.AddComponent<HomeHeroCoordinator>();
        }

        private void Update()
        {
            if (_hero == null && Time.unscaledTime >= _nextResolve)
            {
                _nextResolve = Time.unscaledTime + 0.25f;
                TryBuild();
            }

            if (_hero == null || _title == null) return;
            bool home = string.Equals(_title.text, "НЕ СБЕЙСЯ!", StringComparison.Ordinal);
            if (_hero.activeSelf != home) _hero.SetActive(home);
        }

        private void TryBuild()
        {
            GameObject canvas = GameObject.Find("GameCanvas");
            if (canvas == null) return;

            Transform playArea = Find(canvas.transform, "PlayArea");
            Transform titleTransform = Find(canvas.transform, "Title");
            if (playArea == null || titleTransform == null) return;
            _title = titleTransform.GetComponent<Text>();
            if (_title == null) return;

            Transform existing = playArea.Find("HomeHero");
            if (existing != null)
            {
                _hero = existing.gameObject;
                return;
            }

            if (_circleSprite == null) _circleSprite = CreateCircleSprite(64);

            _hero = new GameObject("HomeHero", typeof(RectTransform));
            _hero.transform.SetParent(playArea, false);
            Stretch(_hero.GetComponent<RectTransform>());
            _hero.transform.SetAsFirstSibling();

            Text kicker = CreateText(_hero.transform, "Kicker", "60 УРОВНЕЙ  •  6 ГЛАВ", 28,
                new Vector2(0.08f, 0.82f), new Vector2(0.92f, 0.91f));
            kicker.color = new Color(0.35f, 0.88f, 1f, 0.94f);
            kicker.fontStyle = FontStyle.Bold;

            Text headline = CreateText(_hero.transform, "Headline", "ЗАПОМНИ.\nПОВТОРИ.", 62,
                new Vector2(0.08f, 0.58f), new Vector2(0.92f, 0.80f));
            headline.color = new Color(0.96f, 0.98f, 1f, 1f);
            headline.fontStyle = FontStyle.Bold;
            headline.lineSpacing = 0.84f;
            AddShadow(headline, 0.45f, new Vector2(0f, -3f));

            var routeHost = new GameObject("PreviewRouteHost", typeof(RectTransform));
            routeHost.transform.SetParent(_hero.transform, false);
            RectTransform routeRect = routeHost.GetComponent<RectTransform>();
            routeRect.anchorMin = new Vector2(0.12f, 0.23f);
            routeRect.anchorMax = new Vector2(0.88f, 0.57f);
            routeRect.offsetMin = Vector2.zero;
            routeRect.offsetMax = Vector2.zero;

            var points = new List<FixedPoint2>
            {
                FixedPoint2.FromNormalized(0.08, 0.22),
                FixedPoint2.FromNormalized(0.22, 0.70),
                FixedPoint2.FromNormalized(0.42, 0.42),
                FixedPoint2.FromNormalized(0.62, 0.76),
                FixedPoint2.FromNormalized(0.82, 0.36),
                FixedPoint2.FromNormalized(0.94, 0.64)
            };

            RouteGraphic glow = CreateRoute(routeHost.transform, "PreviewGlow", new Color(0.22f, 0.80f, 1f, 0.14f), 28f);
            glow.SetPoints(points);
            RouteGraphic route = CreateRoute(routeHost.transform, "PreviewRoute", new Color(0.29f, 0.91f, 1f, 0.92f), 12f);
            route.SetPoints(points);

            CreateDot(routeHost.transform, "StartPreview", new Vector2(0.08f, 0.22f), new Color(0.34f, 1f, 0.58f, 1f));
            CreateDot(routeHost.transform, "EndPreview", new Vector2(0.94f, 0.64f), new Color(1f, 0.40f, 0.48f, 1f));

            Text caption = CreateText(_hero.transform, "Caption", "Проходи уровни, собирай звёзды и открывай новые главы.", 26,
                new Vector2(0.08f, 0.07f), new Vector2(0.92f, 0.18f));
            caption.color = new Color(0.66f, 0.74f, 0.84f, 1f);
        }

        private static RouteGraphic CreateRoute(Transform parent, string name, Color color, float thickness)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(RouteGraphic));
            go.transform.SetParent(parent, false);
            Stretch(go.GetComponent<RectTransform>());
            RouteGraphic graphic = go.GetComponent<RouteGraphic>();
            graphic.color = color;
            graphic.Thickness = thickness;
            graphic.raycastTarget = false;
            return graphic;
        }

        private static Text CreateText(Transform parent, string name, string value, int size, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Text text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 16;
            text.resizeTextMaxSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.text = value;
            text.raycastTarget = false;
            return text;
        }

        private static void CreateDot(Transform parent, string name, Vector2 anchor, Color color)
        {
            var glowGo = new GameObject(name + "Glow", typeof(RectTransform), typeof(Image));
            glowGo.transform.SetParent(parent, false);
            RectTransform glowRect = glowGo.GetComponent<RectTransform>();
            glowRect.anchorMin = anchor;
            glowRect.anchorMax = anchor;
            glowRect.sizeDelta = new Vector2(58f, 58f);
            Image glow = glowGo.GetComponent<Image>();
            glow.sprite = _circleSprite;
            glow.color = new Color(color.r, color.g, color.b, 0.18f);
            glow.raycastTarget = false;

            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.sizeDelta = new Vector2(34f, 34f);
            Image image = go.GetComponent<Image>();
            image.sprite = _circleSprite;
            image.color = color;
            image.raycastTarget = false;
        }

        private static Sprite CreateCircleSprite(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "HomeHeroCircle",
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
                    byte alpha = distance <= radius - 1f ? (byte)255 : distance <= radius + 0.75f ? (byte)150 : (byte)0;
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static void AddShadow(Text text, float alpha, Vector2 distance)
        {
            Shadow shadow = text.GetComponent<Shadow>();
            if (shadow == null) shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, alpha);
            shadow.effectDistance = distance;
            shadow.useGraphicAlpha = true;
        }

        private static Transform Find(Transform root, string name)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
                if (string.Equals(all[i].name, name, StringComparison.Ordinal)) return all[i];
            return null;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
