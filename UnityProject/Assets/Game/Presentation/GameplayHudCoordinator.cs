using System;
using UnityEngine;
using UnityEngine.UI;

namespace DontGetSidetracked.Presentation
{
    /// <summary>
    /// Release gameplay HUD. Mirrors the bootstrap title/status into a cleaner hierarchy
    /// without owning round state or input.
    /// </summary>
    [DefaultExecutionOrder(16000)]
    public sealed class GameplayHudCoordinator : MonoBehaviour
    {
        private GameBootstrap _bootstrap;
        private Text _legacyTitle;
        private Text _legacyStatus;
        private CanvasGroup _legacyTitleGroup;
        private CanvasGroup _legacyStatusGroup;

        private CanvasGroup _hudGroup;
        private Text _mode;
        private Text _instruction;
        private Text _hint;
        private Text _countdown;
        private bool _hudVisible;
        private float _nextResolve;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (FindFirstObjectByType<GameplayHudCoordinator>() != null) return;
            var root = new GameObject("GameplayHudCoordinator");
            DontDestroyOnLoad(root);
            root.AddComponent<GameplayHudCoordinator>();
        }

        private void LateUpdate()
        {
            if ((_bootstrap == null || _legacyTitle == null || _legacyStatus == null || _hudGroup == null) &&
                Time.unscaledTime >= _nextResolve)
            {
                _nextResolve = Time.unscaledTime + 0.25f;
                ResolveAndBuild();
            }

            if (_bootstrap == null || _legacyTitle == null || _legacyStatus == null || _hudGroup == null) return;

            bool active = GameBootstrapRuntimeBridge.IsActiveRound(_bootstrap);
            bool result = GameBootstrapRuntimeBridge.IsResult(_bootstrap);
            bool plainHome = GameBootstrapRuntimeBridge.IsPlainHome(_bootstrap);

            if (active)
            {
                SetLegacyVisible(false);
                SetHudVisible(true);
                Refresh();
            }
            else
            {
                SetHudVisible(false);
                if (!plainHome && !result) SetLegacyVisible(true);
            }
        }

        private void ResolveAndBuild()
        {
            if (_bootstrap == null) _bootstrap = FindFirstObjectByType<GameBootstrap>();

            GameObject gameCanvas = GameObject.Find("GameCanvas");
            if (gameCanvas == null) return;

            _legacyTitle = Find<Text>(gameCanvas.transform, "Title");
            _legacyStatus = Find<Text>(gameCanvas.transform, "Status");
            if (_legacyTitle == null || _legacyStatus == null) return;

            _legacyTitleGroup = EnsureCanvasGroup(_legacyTitle.gameObject);
            _legacyStatusGroup = EnsureCanvasGroup(_legacyStatus.gameObject);

            Transform existing = gameCanvas.transform.Find("ReleaseGameplayHud");
            if (existing != null)
            {
                _hudGroup = existing.GetComponent<CanvasGroup>();
                _mode = Find<Text>(existing, "Mode");
                _instruction = Find<Text>(existing, "Instruction");
                _hint = Find<Text>(existing, "Hint");
                _countdown = Find<Text>(existing, "Countdown");
                return;
            }

            Build(gameCanvas.transform);
        }

        private void Build(Transform canvas)
        {
            var root = new GameObject("ReleaseGameplayHud", typeof(RectTransform), typeof(CanvasGroup));
            root.transform.SetParent(canvas, false);
            ReleaseUiKit.Stretch(root.GetComponent<RectTransform>());
            root.transform.SetAsLastSibling();
            _hudGroup = root.GetComponent<CanvasGroup>();

            Image header = ReleaseUiKit.Panel(root.transform, "GameplayHeader",
                new Vector2(0.075f, 0.785f), new Vector2(0.925f, 0.955f),
                new Color(0.024f, 0.042f, 0.082f, 0.985f), ReleaseUiKit.Cyan, true);

            Image modeChip = ReleaseUiKit.Panel(header.transform, "ModeChip",
                new Vector2(0.30f, 0.70f), new Vector2(0.70f, 0.93f),
                new Color(0.045f, 0.075f, 0.120f, 0.96f), ReleaseUiKit.Cyan, false);
            _mode = ReleaseUiKit.TextBlock(modeChip.transform, "Mode", "РЕЖИМ", 17,
                TextAnchor.MiddleCenter, new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.95f),
                ReleaseUiKit.Cyan, FontStyle.Bold);

            _instruction = ReleaseUiKit.TextBlock(header.transform, "Instruction", "ЗАПОМНИ МАРШРУТ", 38,
                TextAnchor.MiddleCenter, new Vector2(0.055f, 0.30f), new Vector2(0.945f, 0.70f),
                ReleaseUiKit.Text, FontStyle.Bold);
            ReleaseUiKit.AddTextShadow(_instruction, 0.40f, -2f);

            _hint = ReleaseUiKit.TextBlock(header.transform, "Hint", "Через несколько секунд линия исчезнет", 18,
                TextAnchor.MiddleCenter, new Vector2(0.055f, 0.08f), new Vector2(0.945f, 0.31f),
                ReleaseUiKit.Muted);

            _countdown = ReleaseUiKit.TextBlock(root.transform, "Countdown", string.Empty, 104,
                TextAnchor.MiddleCenter, new Vector2(0.36f, 0.575f), new Vector2(0.64f, 0.705f),
                ReleaseUiKit.Cyan, FontStyle.Bold);
            ReleaseUiKit.AddTextShadow(_countdown, 0.55f, -4f);

            SetHudVisible(false);
        }

        private void Refresh()
        {
            string title = _legacyTitle.text ?? string.Empty;
            string status = _legacyStatus.text ?? string.Empty;
            _mode.text = CleanMode(title);

            string compact = status.Replace("\r", string.Empty).Trim();
            if (compact == "3" || compact == "2" || compact == "1")
            {
                _countdown.text = compact;
                _countdown.gameObject.SetActive(true);
                _instruction.text = "МАРШРУТ ИСЧЕЗАЕТ";
                _hint.text = "Приготовься повторить одним движением";
                return;
            }

            _countdown.gameObject.SetActive(false);
            _countdown.text = string.Empty;

            if (compact.IndexOf("ЗАПОМНИ", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _instruction.text = "ЗАПОМНИ МАРШРУТ";
                _hint.text = "Следи за формой от зелёной точки к красной";
                return;
            }

            if (compact.IndexOf("ПОВТОРИ", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _instruction.text = "ТВОЯ ОЧЕРЕДЬ";
                _hint.text = "Начни с зелёной точки и проведи линию одним движением";
                return;
            }

            string[] lines = compact.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            _instruction.text = lines.Length > 0 ? lines[0].Trim() : "НЕ СБЕЙСЯ";
            _hint.text = lines.Length > 1 ? lines[1].Trim() : string.Empty;
        }

        private static string CleanMode(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "НЕ СБЕЙСЯ";
            string mode = value.Trim();
            if (mode.StartsWith("DAILY", StringComparison.OrdinalIgnoreCase)) return mode;
            if (mode.StartsWith("ВЫЗОВ", StringComparison.OrdinalIgnoreCase)) return mode;
            if (mode.StartsWith("УРОВЕНЬ", StringComparison.OrdinalIgnoreCase)) return mode;
            if (mode.StartsWith("ОБУЧЕНИЕ", StringComparison.OrdinalIgnoreCase)) return "ОБУЧЕНИЕ";
            if (mode.StartsWith("ТРЕНИРОВКА", StringComparison.OrdinalIgnoreCase)) return "ТРЕНИРОВКА";
            return mode;
        }

        private void SetHudVisible(bool visible)
        {
            if (_hudGroup == null || _hudVisible == visible) return;
            _hudVisible = visible;
            _hudGroup.alpha = visible ? 1f : 0f;
            _hudGroup.interactable = false;
            _hudGroup.blocksRaycasts = false;
        }

        private void SetLegacyVisible(bool visible)
        {
            SetGroup(_legacyTitleGroup, visible);
            SetGroup(_legacyStatusGroup, visible);
        }

        private static void SetGroup(CanvasGroup group, bool visible)
        {
            if (group == null) return;
            group.alpha = visible ? 1f : 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        private static CanvasGroup EnsureCanvasGroup(GameObject go)
        {
            CanvasGroup group = go.GetComponent<CanvasGroup>();
            if (group == null) group = go.AddComponent<CanvasGroup>();
            return group;
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
