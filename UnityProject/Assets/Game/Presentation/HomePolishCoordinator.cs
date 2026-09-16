using System;
using UnityEngine;
using UnityEngine.UI;

namespace DontGetSidetracked.Presentation
{
    /// <summary>
    /// Final portrait composition pass for Home. Keeps the gameplay/result layout untouched.
    /// </summary>
    public sealed class HomePolishCoordinator : MonoBehaviour
    {
        private Text _title;
        private Text _status;
        private RectTransform _playArea;
        private Button _primary;
        private Button _secondary;
        private Button _share;
        private RectTransform _statsButton;
        private RectTransform _storeButton;
        private bool _homeApplied;
        private float _nextResolve;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (FindFirstObjectByType<HomePolishCoordinator>() != null) return;
            var root = new GameObject("HomePolishCoordinator");
            DontDestroyOnLoad(root);
            root.AddComponent<HomePolishCoordinator>();
        }

        private void Update()
        {
            if (_title == null && Time.unscaledTime >= _nextResolve)
            {
                _nextResolve = Time.unscaledTime + 0.25f;
                Resolve();
            }

            if (_title == null) return;
            bool home = string.Equals(_title.text, "НЕ СБЕЙСЯ!", StringComparison.Ordinal);
            if (home)
            {
                ApplyHome();
                _homeApplied = true;
            }
            else if (_homeApplied)
            {
                RestoreGameplayLayout();
                _homeApplied = false;
            }
        }

        private void Resolve()
        {
            GameObject game = GameObject.Find("GameCanvas");
            if (game != null)
            {
                _title = Find<Text>(game.transform, "Title");
                _status = Find<Text>(game.transform, "Status");
                _playArea = Find<RectTransform>(game.transform, "PlayArea");
                _primary = Find<Button>(game.transform, "Primary");
                _secondary = Find<Button>(game.transform, "Secondary");
                _share = Find<Button>(game.transform, "Share");
            }

            GameObject meta = GameObject.Find("MetaCanvas");
            if (meta != null)
            {
                Button[] buttons = meta.GetComponentsInChildren<Button>(true);
                for (int i = 0; i < buttons.Length; i++)
                {
                    Text label = buttons[i].GetComponentInChildren<Text>(true);
                    if (label == null) continue;
                    if (label.text.IndexOf("СТАТИСТИКА", StringComparison.OrdinalIgnoreCase) >= 0)
                        _statsButton = buttons[i].GetComponent<RectTransform>();
                    else if (label.text.IndexOf("МАГАЗИН", StringComparison.OrdinalIgnoreCase) >= 0)
                        _storeButton = buttons[i].GetComponent<RectTransform>();
                }
            }
        }

        private void ApplyHome()
        {
            if (_title != null)
            {
                SetAnchors(_title.rectTransform, new Vector2(0.08f, 0.905f), new Vector2(0.92f, 0.965f));
                _title.fontSize = 72;
                _title.resizeTextMinSize = 38;
                _title.resizeTextMaxSize = 72;
            }

            if (_status != null)
            {
                _status.gameObject.SetActive(true);
                _status.text = CompactStatus(_status.text);
                SetAnchors(_status.rectTransform, new Vector2(0.08f, 0.845f), new Vector2(0.92f, 0.892f));
                _status.fontSize = 30;
                _status.resizeTextMinSize = 21;
                _status.resizeTextMaxSize = 30;
                _status.alignment = TextAnchor.MiddleCenter;
            }

            if (_playArea != null)
                SetAnchors(_playArea, new Vector2(0.075f, 0.305f), new Vector2(0.925f, 0.815f));

            if (_primary != null)
                SetAnchors(_primary.GetComponent<RectTransform>(), new Vector2(0.075f, 0.195f), new Vector2(0.925f, 0.265f));

            if (_secondary != null)
                SetAnchors(_secondary.GetComponent<RectTransform>(), new Vector2(0.075f, 0.100f), new Vector2(0.350f, 0.165f));

            if (_statsButton != null)
                SetAnchors(_statsButton, new Vector2(0.365f, 0.100f), new Vector2(0.635f, 0.165f));

            if (_storeButton != null)
                SetAnchors(_storeButton, new Vector2(0.650f, 0.100f), new Vector2(0.925f, 0.165f));
        }

        private void RestoreGameplayLayout()
        {
            if (_title != null)
                SetAnchors(_title.rectTransform, new Vector2(0.05f, 0.88f), new Vector2(0.95f, 0.98f));

            if (_status != null)
            {
                _status.gameObject.SetActive(true);
                SetAnchors(_status.rectTransform, new Vector2(0.05f, 0.75f), new Vector2(0.95f, 0.87f));
            }

            if (_playArea != null)
                SetAnchors(_playArea, new Vector2(0.065f, 0.26f), new Vector2(0.935f, 0.745f));

            if (_primary != null)
                SetAnchors(_primary.GetComponent<RectTransform>(), new Vector2(0.08f, 0.12f), new Vector2(0.92f, 0.20f));

            if (_secondary != null)
                SetAnchors(_secondary.GetComponent<RectTransform>(), new Vector2(0.08f, 0.035f), new Vector2(0.48f, 0.105f));

            if (_share != null)
                SetAnchors(_share.GetComponent<RectTransform>(), new Vector2(0.52f, 0.035f), new Vector2(0.92f, 0.105f));
        }

        private static string CompactStatus(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return value;
            string compact = value.Replace("\r", string.Empty).Replace("\n", "     •     ");
            compact = compact.Replace("Серия:", "СЕРИЯ").Replace("Лучший:", "ЛУЧШИЙ");
            return compact;
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

        private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
        {
            if (rect == null) return;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
