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
        private RectTransform _playArea;
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
                ApplyGameplayBoardLayout(true);
                SetLegacyVisible(false);
                SetHudVisible(true);
                Refresh();
            }
            else
            {
                ApplyRestingBoardLayout(result);
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
            _playArea = Find<RectTransform>(gameCanvas.transform, "PlayArea");
            if (_legacyTitle == null || _legacyStatus == null || _playArea == null) return;

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

            // One compact header instead of two stacked dashboard cards. The board is the
            // hero of this screen; chrome should explain the phase without competing with it.
            Image header = ReleaseUiComponents.GlassCard(root.transform, "GameplayHeader",
                new Vector2(0.055f, 0.845f), new Vector2(0.945f, 0.955f),
                ReleaseUiComponents.Cyan, true);

            _mode = ReleaseUiKit.TextBlock(header.transform, "Mode", "РЕЖИМ", 17,
                TextAnchor.MiddleCenter, new Vector2(0.08f, 0.72f), new Vector2(0.92f, 0.94f),
                ReleaseUiComponents.Cyan, FontStyle.Bold);

            _instruction = ReleaseUiKit.TextBlock(header.transform, "Instruction", "ЗАПОМНИ МАРШРУТ", 36,
                TextAnchor.MiddleCenter, new Vector2(0.05f, 0.30f), new Vector2(0.95f, 0.74f),
                ReleaseUiComponents.Text, FontStyle.Bold);
            ReleaseUiKit.AddTextShadow(_instruction, 0.42f, -2f);

            Transform metrics = ReleaseUiKit.Rect(header.transform, "GameplayMetrics",
                new Vector2(0.06f, 0.055f), new Vector2(0.94f, 0.30f));
            ReleaseUiKit.TextBlock(metrics, "Memory", "ПАМЯТЬ", 14,
                TextAnchor.MiddleLeft, new Vector2(0.00f, 0f), new Vector2(0.30f, 1f),
                ReleaseUiComponents.Cyan, FontStyle.Bold);
            ReleaseUiKit.TextBlock(metrics, "Gesture", "1 ДВИЖЕНИЕ", 14,
                TextAnchor.MiddleCenter, new Vector2(0.30f, 0f), new Vector2(0.70f, 1f),
                ReleaseUiComponents.Muted, FontStyle.Bold);
            ReleaseUiKit.TextBlock(metrics, "Rule", "БЕЗ ПОДСКАЗКИ", 14,
                TextAnchor.MiddleRight, new Vector2(0.70f, 0f), new Vector2(1.00f, 1f),
                ReleaseUiComponents.Muted, FontStyle.Bold);

            // Instruction lives below the board, never over it.
            Image hintCard = ReleaseUiComponents.GlassCard(root.transform, "GameplayInstruction",
                new Vector2(0.055f, 0.050f), new Vector2(0.945f, 0.135f),
                ReleaseUiComponents.Blue, false);
            ReleaseUiKit.TextBlock(hintCard.transform, "Eye", "◎", 30, TextAnchor.MiddleCenter,
                new Vector2(0.035f, 0.12f), new Vector2(0.16f, 0.88f),
                ReleaseUiComponents.Blue, FontStyle.Bold);
            _hint = ReleaseUiKit.TextBlock(hintCard.transform, "Hint", "Запомни форму и повороты маршрута", 20,
                TextAnchor.MiddleLeft, new Vector2(0.17f, 0.08f), new Vector2(0.95f, 0.92f),
                ReleaseUiComponents.Text, FontStyle.Bold);

            _countdown = ReleaseUiKit.TextBlock(root.transform, "Countdown", string.Empty, 122,
                TextAnchor.MiddleCenter, new Vector2(0.34f, 0.49f), new Vector2(0.66f, 0.64f),
                ReleaseUiComponents.Cyan, FontStyle.Bold);
            ReleaseUiKit.AddTextShadow(_countdown, 0.64f, -5f);

            SetHudVisible(false);
        }

        private void ApplyGameplayBoardLayout(bool active)
        {
            if (!active || _playArea == null) return;

            // Give the memory gesture most of the portrait screen. This also creates a
            // clean visual gap between the board, compact header and instruction card.
            ReleaseUiKit.SetAnchors(_playArea,
                new Vector2(0.055f, 0.155f),
                new Vector2(0.945f, 0.825f));
        }

        private void ApplyRestingBoardLayout(bool result)
        {
            if (_playArea == null) return;

            // Match VisualThemeCoordinator exactly outside active gameplay so the two
            // presentation layers do not fight over the same RectTransform each frame.
            ReleaseUiKit.SetAnchors(
                _playArea,
                result ? new Vector2(0.07f, 0.330f) : new Vector2(0.07f, 0.245f),
                result ? new Vector2(0.93f, 0.665f) : new Vector2(0.93f, 0.760f));
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
                _hint.text = "Маршрут исчезнет. Приготовься рисовать по памяти.";
                return;
            }

            _countdown.gameObject.SetActive(false);
            _countdown.text = string.Empty;

            if (compact.IndexOf("ЗАПОМНИ", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _instruction.text = "ЗАПОМНИ МАРШРУТ";
                _hint.text = "Смотри внимательно. Запомни форму, повороты и конечную точку.";
                return;
            }

            if (compact.IndexOf("ПОВТОРИ", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _instruction.text = "ТВОЯ ОЧЕРЕДЬ";
                _hint.text = "Проведи маршрут одним непрерывным движением от старта до звезды.";
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
            if (_hudGroup == null) return;
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
