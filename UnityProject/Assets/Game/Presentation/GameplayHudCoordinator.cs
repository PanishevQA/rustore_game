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

            Image header = ReleaseUiComponents.GlassCard(root.transform, "GameplayHeader",
                new Vector2(0.07f, 0.855f), new Vector2(0.93f, 0.955f),
                ReleaseUiComponents.Cyan, true);

            _mode = ReleaseUiKit.TextBlock(header.transform, "Mode", "РЕЖИМ", 18,
                TextAnchor.MiddleCenter, new Vector2(0.16f, 0.62f), new Vector2(0.84f, 0.93f),
                ReleaseUiComponents.Muted, FontStyle.Bold);

            _instruction = ReleaseUiKit.TextBlock(header.transform, "Instruction", "ЗАПОМНИ МАРШРУТ", 33,
                TextAnchor.MiddleCenter, new Vector2(0.05f, 0.12f), new Vector2(0.95f, 0.65f),
                ReleaseUiComponents.Text, FontStyle.Bold);
            ReleaseUiKit.AddTextShadow(_instruction, 0.42f, -2f);

            Image metrics = ReleaseUiComponents.GlassCard(root.transform, "GameplayMetrics",
                new Vector2(0.07f, 0.785f), new Vector2(0.93f, 0.842f),
                ReleaseUiComponents.Violet, false);
            ReleaseUiKit.TextBlock(metrics.transform, "Memory", "◉  ПАМЯТЬ", 15,
                TextAnchor.MiddleCenter, new Vector2(0.02f, 0.08f), new Vector2(0.34f, 0.92f),
                ReleaseUiComponents.Cyan, FontStyle.Bold);
            ReleaseUiKit.TextBlock(metrics.transform, "Gesture", "1  ДВИЖЕНИЕ", 15,
                TextAnchor.MiddleCenter, new Vector2(0.34f, 0.08f), new Vector2(0.66f, 0.92f),
                ReleaseUiComponents.Text, FontStyle.Bold);
            ReleaseUiKit.TextBlock(metrics.transform, "Rule", "БЕЗ ПОДСКАЗКИ", 15,
                TextAnchor.MiddleCenter, new Vector2(0.66f, 0.08f), new Vector2(0.98f, 0.92f),
                ReleaseUiComponents.Muted, FontStyle.Bold);

            Image hintCard = ReleaseUiComponents.GlassCard(root.transform, "GameplayInstruction",
                new Vector2(0.07f, 0.155f), new Vector2(0.93f, 0.235f),
                ReleaseUiComponents.Blue, false);
            ReleaseUiKit.TextBlock(hintCard.transform, "Eye", "◉", 32, TextAnchor.MiddleCenter,
                new Vector2(0.04f, 0.12f), new Vector2(0.20f, 0.88f),
                ReleaseUiComponents.Blue, FontStyle.Bold);
            _hint = ReleaseUiKit.TextBlock(hintCard.transform, "Hint", "Запомни форму и повороты маршрута", 19,
                TextAnchor.MiddleLeft, new Vector2(0.22f, 0.08f), new Vector2(0.94f, 0.92f),
                ReleaseUiComponents.Text, FontStyle.Bold);

            _countdown = ReleaseUiKit.TextBlock(root.transform, "Countdown", string.Empty, 112,
                TextAnchor.MiddleCenter, new Vector2(0.35f, 0.545f), new Vector2(0.65f, 0.690f),
                ReleaseUiComponents.Cyan, FontStyle.Bold);
            ReleaseUiKit.AddTextShadow(_countdown, 0.64f, -5f);

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
