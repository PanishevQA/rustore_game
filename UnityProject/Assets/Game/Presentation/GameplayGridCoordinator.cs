using System;
using UnityEngine;
using UnityEngine.UI;

namespace DontGetSidetracked.Presentation
{
    /// <summary>
    /// Adds a subtle memory-grid to the gameplay surface. The grid is presentation-only,
    /// lives behind both route graphics and never participates in hit testing or scoring.
    /// </summary>
    public sealed class GameplayGridCoordinator : MonoBehaviour
    {
        private static readonly Color GridColor = new Color(0.31f, 0.70f, 0.94f, 0.075f);
        private static readonly Color MajorGridColor = new Color(0.31f, 0.78f, 1f, 0.115f);

        private GameObject _grid;
        private float _nextResolve;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (FindFirstObjectByType<GameplayGridCoordinator>() != null) return;
            var root = new GameObject("GameplayGridCoordinator");
            DontDestroyOnLoad(root);
            root.AddComponent<GameplayGridCoordinator>();
        }

        private void Update()
        {
            if (_grid != null || Time.unscaledTime < _nextResolve) return;
            _nextResolve = Time.unscaledTime + 0.25f;
            TryBuild();
        }

        private void TryBuild()
        {
            GameObject canvas = GameObject.Find("GameCanvas");
            if (canvas == null) return;

            RectTransform playArea = Find<RectTransform>(canvas.transform, "PlayArea");
            if (playArea == null) return;

            Transform existing = playArea.Find("GameplayGrid");
            if (existing != null)
            {
                _grid = existing.gameObject;
                return;
            }

            _grid = new GameObject("GameplayGrid", typeof(RectTransform));
            _grid.transform.SetParent(playArea, false);
            RectTransform root = _grid.GetComponent<RectTransform>();
            root.anchorMin = new Vector2(0.035f, 0.045f);
            root.anchorMax = new Vector2(0.965f, 0.955f);
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            _grid.transform.SetAsFirstSibling();

            // Even divisions put the two major guides exactly through the board centre.
            const int columns = 8;
            const int rows = 12;
            for (int i = 1; i < columns; i++)
            {
                float x = i / (float)columns;
                CreateLine(
                    root,
                    "V" + i,
                    new Vector2(x, 0f),
                    new Vector2(x, 1f),
                    new Vector2(i == columns / 2 ? 2.6f : 1.5f, 0f),
                    i == columns / 2 ? MajorGridColor : GridColor);
            }

            for (int i = 1; i < rows; i++)
            {
                float y = i / (float)rows;
                CreateLine(
                    root,
                    "H" + i,
                    new Vector2(0f, y),
                    new Vector2(1f, y),
                    new Vector2(0f, i == rows / 2 ? 2.6f : 1.5f),
                    i == rows / 2 ? MajorGridColor : GridColor);
            }

            CanvasGroup group = _grid.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        private static void CreateLine(
            Transform parent,
            string name,
            Vector2 min,
            Vector2 max,
            Vector2 size,
            Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;

            Image image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        private static T Find<T>(Transform root, string name) where T : Component
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (!string.Equals(all[i].name, name, StringComparison.Ordinal)) continue;
                return all[i].GetComponent<T>();
            }
            return null;
        }
    }
}
