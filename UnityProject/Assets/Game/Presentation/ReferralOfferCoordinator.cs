using System;
using UnityEngine;
using UnityEngine.UI;

namespace DontGetSidetracked.Presentation
{
    /// <summary>
    /// Release-quality presentation for an incoming friend challenge.
    /// The proven GameBootstrap referral flow remains the owner of challenge state;
    /// this surface only mirrors its copy and proxies the existing actions.
    /// </summary>
    [DefaultExecutionOrder(15800)]
    public sealed class ReferralOfferCoordinator : MonoBehaviour
    {
        private GameBootstrap _bootstrap;
        private Text _legacyTitle;
        private Text _legacyStatus;
        private Button _legacyPrimary;
        private Button _legacySecondary;
        private Button _legacyShare;
        private RectTransform _legacyPlayArea;

        private CanvasGroup _titleGroup;
        private CanvasGroup _statusGroup;
        private CanvasGroup _primaryGroup;
        private CanvasGroup _secondaryGroup;
        private CanvasGroup _shareGroup;
        private CanvasGroup _playGroup;

        private CanvasGroup _overlayGroup;
        private Text _target;
        private Text _detail;
        private Button _accept;
        private Button _later;
        private ReleasePanelMotion _offerMotion;

        private bool _visible;
        private float _nextResolve;

        public bool IsVisible => _visible;

        public bool Dismiss()
        {
            if (!_visible) return false;
            Later();
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (FindFirstObjectByType<ReferralOfferCoordinator>() != null) return;
            var root = new GameObject("ReferralOfferCoordinator");
            DontDestroyOnLoad(root);
            root.AddComponent<ReferralOfferCoordinator>();
        }

        private void Update()
        {
            if ((_bootstrap == null || _legacyTitle == null || _overlayGroup == null) &&
                Time.unscaledTime >= _nextResolve)
            {
                _nextResolve = Time.unscaledTime + 0.25f;
                ResolveAndBuild();
            }

            if (_bootstrap == null || _legacyTitle == null || _legacyStatus == null || _overlayGroup == null) return;

            bool show = GameBootstrapRuntimeBridge.IsHome(_bootstrap) &&
                        string.Equals(_legacyTitle.text, "ВЫЗОВ ДРУГА", StringComparison.Ordinal);

            if (show)
            {
                RefreshCopy();
                if (!_visible) SetVisible(true);
            }
            else if (_visible)
            {
                SetVisible(false);
            }
        }

        private void ResolveAndBuild()
        {
            if (_bootstrap == null) _bootstrap = FindFirstObjectByType<GameBootstrap>();

            GameObject gameCanvas = GameObject.Find("GameCanvas");
            if (gameCanvas == null) return;

            _legacyTitle = Find<Text>(gameCanvas.transform, "Title");
            _legacyStatus = Find<Text>(gameCanvas.transform, "Status");
            _legacyPrimary = Find<Button>(gameCanvas.transform, "Primary");
            _legacySecondary = Find<Button>(gameCanvas.transform, "Secondary");
            _legacyShare = Find<Button>(gameCanvas.transform, "Share");
            _legacyPlayArea = Find<RectTransform>(gameCanvas.transform, "PlayArea");

            if (_legacyTitle == null || _legacyStatus == null || _legacyPrimary == null ||
                _legacySecondary == null || _legacyShare == null || _legacyPlayArea == null)
                return;

            _titleGroup = EnsureGroup(_legacyTitle.gameObject);
            _statusGroup = EnsureGroup(_legacyStatus.gameObject);
            _primaryGroup = EnsureGroup(_legacyPrimary.gameObject);
            _secondaryGroup = EnsureGroup(_legacySecondary.gameObject);
            _shareGroup = EnsureGroup(_legacyShare.gameObject);
            _playGroup = EnsureGroup(_legacyPlayArea.gameObject);

            Transform existing = gameCanvas.transform.Find("ReferralOffer");
            if (existing != null)
            {
                _overlayGroup = existing.GetComponent<CanvasGroup>();
                _target = Find<Text>(existing, "Target");
                _detail = Find<Text>(existing, "Detail");
                _accept = Find<Button>(existing, "Accept");
                _later = Find<Button>(existing, "Later");
                return;
            }

            Build(gameCanvas.transform);
        }

        private void Build(Transform canvas)
        {
            var root = new GameObject("ReferralOffer", typeof(RectTransform), typeof(CanvasGroup));
            root.transform.SetParent(canvas, false);
            ReleaseUiKit.Stretch(root.GetComponent<RectTransform>());
            root.transform.SetAsLastSibling();
            _overlayGroup = root.GetComponent<CanvasGroup>();

            ReleaseUiKit.TextBlock(root.transform, "Kicker", "ЧЕЛЛЕНДЖ ОТ ДРУГА", 20,
                TextAnchor.MiddleLeft, new Vector2(0.075f, 0.900f), new Vector2(0.68f, 0.948f),
                ReleaseUiKit.Cyan, FontStyle.Bold);

            Text title = ReleaseUiKit.TextBlock(root.transform, "Heading", "НЕ СБЕЙСЯ.", 60,
                TextAnchor.MiddleLeft, new Vector2(0.075f, 0.828f), new Vector2(0.78f, 0.900f),
                ReleaseUiKit.Text, FontStyle.Bold);
            ReleaseUiKit.AddTextShadow(title, 0.45f, -3f);

            ReleaseUiKit.TextBlock(root.transform, "Subheading",
                "Тот же маршрут. Те же условия. Один шанс доказать, что ты точнее.", 24,
                TextAnchor.UpperLeft, new Vector2(0.075f, 0.745f), new Vector2(0.91f, 0.825f),
                ReleaseUiKit.Muted);

            Image card = ReleaseUiKit.Panel(root.transform, "ChallengeCard",
                new Vector2(0.075f, 0.375f), new Vector2(0.925f, 0.710f),
                ReleaseUiKit.Surface, ReleaseUiKit.Violet, true);
            _offerMotion = card.gameObject.AddComponent<ReleasePanelMotion>();

            ReleaseUiKit.TextBlock(card.transform, "TargetLabel", "ЦЕЛЬ", 19,
                TextAnchor.MiddleCenter, new Vector2(0.10f, 0.76f), new Vector2(0.90f, 0.90f),
                new Color(0.72f, 0.66f, 1f, 1f), FontStyle.Bold);

            _target = ReleaseUiKit.TextBlock(card.transform, "Target", "—", 78,
                TextAnchor.MiddleCenter, new Vector2(0.10f, 0.39f), new Vector2(0.90f, 0.76f),
                ReleaseUiKit.Text, FontStyle.Bold);
            ReleaseUiKit.AddTextShadow(_target, 0.50f, -4f);

            _detail = ReleaseUiKit.TextBlock(card.transform, "Detail",
                "Побей результат друга и отправь ответный вызов.", 22,
                TextAnchor.MiddleCenter, new Vector2(0.08f, 0.12f), new Vector2(0.92f, 0.39f),
                ReleaseUiKit.Muted);

            _accept = ReleaseUiKit.Button(root.transform, "Accept", "ПРИНЯТЬ ВЫЗОВ",
                new Vector2(0.075f, 0.255f), new Vector2(0.925f, 0.330f),
                ReleaseUiKit.Cyan, new Color(0.01f, 0.03f, 0.05f, 1f), 28, Accept);

            _later = ReleaseUiKit.Button(root.transform, "Later", "ПОЗЖЕ",
                new Vector2(0.275f, 0.170f), new Vector2(0.725f, 0.225f),
                new Color(0.065f, 0.085f, 0.135f, 0.98f), ReleaseUiKit.Muted, 22, Later);

            ReleaseUiKit.TextBlock(root.transform, "Footnote",
                "Вызов хранится локально и не требует аккаунта.", 18,
                TextAnchor.MiddleCenter, new Vector2(0.10f, 0.105f), new Vector2(0.90f, 0.145f),
                ReleaseUiKit.Muted);

            SetVisible(false);
        }

        private void RefreshCopy()
        {
            string status = _legacyStatus.text ?? string.Empty;
            _target.text = ExtractScore(status);

            int newline = status.IndexOf('\n');
            string secondLine = newline >= 0 && newline + 1 < status.Length
                ? status.Substring(newline + 1).Trim()
                : string.Empty;
            _detail.text = string.IsNullOrWhiteSpace(secondLine)
                ? "Побей результат друга и отправь ответный вызов."
                : secondLine;
        }

        private static string ExtractScore(string status)
        {
            if (string.IsNullOrWhiteSpace(status)) return "—";
            int percent = status.IndexOf('%');
            if (percent < 0) return "—";

            int start = percent - 1;
            while (start >= 0)
            {
                char ch = status[start];
                if (!(char.IsDigit(ch) || ch == '.' || ch == ',')) break;
                start--;
            }

            string score = status.Substring(start + 1, percent - start - 1).Trim();
            return string.IsNullOrWhiteSpace(score) ? "—" : score.Replace(',', '.') + "%";
        }

        private void Accept() => Invoke(_legacyPrimary);

        private void Later() => Invoke(_legacySecondary);

        private static void Invoke(Button button)
        {
            if (button == null || !button.interactable) return;
            button.onClick.Invoke();
        }

        private void SetVisible(bool visible)
        {
            _visible = visible;
            if (visible) _offerMotion?.Play();
            if (_overlayGroup != null)
            {
                _overlayGroup.alpha = visible ? 1f : 0f;
                _overlayGroup.interactable = visible;
                _overlayGroup.blocksRaycasts = visible;
            }

            SetLegacy(_titleGroup, !visible, false);
            SetLegacy(_statusGroup, !visible, false);
            SetLegacy(_playGroup, !visible, false);
            SetLegacy(_primaryGroup, !visible, true);
            SetLegacy(_secondaryGroup, !visible, true);
            SetLegacy(_shareGroup, !visible, true);
        }

        private static void SetLegacy(CanvasGroup group, bool visible, bool interactive)
        {
            if (group == null) return;
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible && interactive;
            group.blocksRaycasts = visible && interactive;
        }

        private static CanvasGroup EnsureGroup(GameObject go)
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
