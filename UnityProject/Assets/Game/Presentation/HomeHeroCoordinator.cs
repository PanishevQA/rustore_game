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

            _hero = new GameObject("HomeHero", typeof(RectTransform));
            _hero.transform.SetParent(playArea, false);
            Stretch(_hero.GetComponent<RectTransform>());
            _hero.transform.SetAsFirstSibling();

            Text kicker = CreateText(_hero.transform, "Kicker", "DAILY CHALLENGE", 26,
                new Vector2(0.08f, 0.80f), new Vector2(0.92f, 0.90f));
            kicker.color = new Color(0.35f, 0.88f, 1f, 0.90f);
            kicker.fontStyle = FontStyle.Bold;

            Text headline = CreateText(_hero.transform, "Headline", "ЗАПОМНИ.\nПОВТОРИ.", 52,
                new Vector2(0.08f, 0.55f), new Vector2(0.92f, 0.79f));
            headline.color = new Color(0.96f, 0.98f, 1f, 1f);
            headline.fontStyle = FontStyle.Bold;
            headline.lineSpacing = 0.88f;

            var routeGo = new GameObject("PreviewRoute", typeof(RectTransform), typeof(RouteGraphic));
            routeGo.transform.SetParent(_hero.transform, false);
            RectTransform routeRect = routeGo.GetComponent<RectTransform>();
            routeRect.anchorMin = new Vector2(0.12f, 0.23f);
            routeRect.anchorMax = new Vector2(0.88f, 0.55f);
            routeRect.offsetMin = Vector2.zero;
            routeRect.offsetMax = Vector2.zero;
            RouteGraphic route = routeGo.GetComponent<RouteGraphic>();
            route.color = new Color(0.29f, 0.91f, 1f, 0.78f);
            route.Thickness = 11f;
            route.raycastTarget = false;
            route.SetPoints(new List<FixedPoint2>
            {
                FixedPoint2.FromNormalized(0.08, 0.22),
                FixedPoint2.FromNormalized(0.22, 0.70),
                FixedPoint2.FromNormalized(0.42, 0.42),
                FixedPoint2.FromNormalized(0.62, 0.76),
                FixedPoint2.FromNormalized(0.82, 0.36),
                FixedPoint2.FromNormalized(0.94, 0.64)
            });

            CreateDot(routeGo.transform, "StartPreview", new Vector2(0.08f, 0.22f), new Color(0.34f, 1f, 0.58f, 1f));
            CreateDot(routeGo.transform, "EndPreview", new Vector2(0.94f, 0.64f), new Color(1f, 0.40f, 0.48f, 1f));

            Text caption = CreateText(_hero.transform, "Caption", "Несколько секунд на память. Потом — один жест.", 24,
                new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.20f));
            caption.color = new Color(0.62f, 0.70f, 0.82f, 1f);
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
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.sizeDelta = new Vector2(32f, 32f);
            Image image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
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
