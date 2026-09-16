using System;
using UnityEngine;
using UnityEngine.UI;

namespace DontGetSidetracked.Presentation
{
    /// <summary>
    /// Final campaign-aware Home layout. Runs after HomePolishCoordinator so the extra Daily CTA
    /// cannot fight with the temporary Home composition or produce one-frame jumps.
    /// </summary>
    [DefaultExecutionOrder(12000)]
    public sealed class CampaignHomeLayoutCoordinator : MonoBehaviour
    {
        private Text _title;
        private Button _primary;
        private Button _secondary;
        private Button _share;
        private RectTransform _stats;
        private RectTransform _store;
        private float _nextResolve;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (FindFirstObjectByType<CampaignHomeLayoutCoordinator>() != null) return;
            var root = new GameObject("CampaignHomeLayoutCoordinator");
            DontDestroyOnLoad(root);
            root.AddComponent<CampaignHomeLayoutCoordinator>();
        }

        private void LateUpdate()
        {
            if ((_title == null || _stats == null || _store == null) && Time.unscaledTime >= _nextResolve)
            {
                _nextResolve = Time.unscaledTime + 0.25f;
                Resolve();
            }

            if (_title == null || !string.Equals(_title.text, "НЕ СБЕЙСЯ!", StringComparison.Ordinal)) return;
            if (_primary == null || _secondary == null || _share == null) return;

            Text primaryLabel = _primary.GetComponentInChildren<Text>(true);
            Text shareLabel = _share.GetComponentInChildren<Text>(true);
            if (primaryLabel == null || shareLabel == null ||
                !string.Equals(primaryLabel.text, "УРОВНИ", StringComparison.Ordinal) ||
                !string.Equals(shareLabel.text, "DAILY", StringComparison.Ordinal)) return;

            SetAnchors(_primary.GetComponent<RectTransform>(), new Vector2(0.075f, 0.205f), new Vector2(0.925f, 0.270f));
            SetAnchors(_share.GetComponent<RectTransform>(), new Vector2(0.075f, 0.135f), new Vector2(0.925f, 0.195f));
            SetAnchors(_secondary.GetComponent<RectTransform>(), new Vector2(0.075f, 0.045f), new Vector2(0.350f, 0.105f));
            if (_stats != null) SetAnchors(_stats, new Vector2(0.365f, 0.045f), new Vector2(0.635f, 0.105f));
            if (_store != null) SetAnchors(_store, new Vector2(0.650f, 0.045f), new Vector2(0.925f, 0.105f));
        }

        private void Resolve()
        {
            GameObject game = GameObject.Find("GameCanvas");
            if (game != null)
            {
                _title = Find<Text>(game.transform, "Title");
                _primary = Find<Button>(game.transform, "Primary");
                _secondary = Find<Button>(game.transform, "Secondary");
                _share = Find<Button>(game.transform, "Share");
            }

            GameObject meta = GameObject.Find("MetaCanvas");
            if (meta == null) return;
            Button[] buttons = meta.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Text label = buttons[i].GetComponentInChildren<Text>(true);
                if (label == null) continue;
                if (label.text.IndexOf("СТАТИСТИКА", StringComparison.OrdinalIgnoreCase) >= 0)
                    _stats = buttons[i].GetComponent<RectTransform>();
                else if (label.text.IndexOf("МАГАЗИН", StringComparison.OrdinalIgnoreCase) >= 0)
                    _store = buttons[i].GetComponent<RectTransform>();
            }
        }

        private static T Find<T>(Transform root, string name) where T : Component
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
                if (string.Equals(all[i].name, name, StringComparison.Ordinal)) return all[i].GetComponent<T>();
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
