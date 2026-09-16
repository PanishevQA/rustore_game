using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace DontGetSidetracked.Presentation
{
    /// <summary>
    /// Home-only training selector. Reuses GameBootstrap training loop and only chooses
    /// the next difficulty before the round starts.
    /// </summary>
    [DefaultExecutionOrder(12000)]
    public sealed class TrainingMenuCoordinator : MonoBehaviour
    {
        private GameBootstrap _bootstrap;
        private FieldInfo _modeField;
        private FieldInfo _trainingIndexField;
        private MethodInfo _startTrainingMethod;
        private Button _trainingButton;
        private GameObject _canvas;
        private GameObject _panel;
        private float _nextResolve;
        private bool _wired;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (FindFirstObjectByType<TrainingMenuCoordinator>() != null) return;
            var root = new GameObject("TrainingMenuCoordinator");
            DontDestroyOnLoad(root);
            root.AddComponent<TrainingMenuCoordinator>();
        }

        private void Awake()
        {
            BuildUi();
            SetVisible(false);
        }

        private void LateUpdate()
        {
            if (_bootstrap == null && Time.unscaledTime >= _nextResolve)
            {
                _nextResolve = Time.unscaledTime + 0.25f;
                ResolveBootstrap();
            }
            if (_bootstrap == null || _modeField == null) return;

            string mode = _modeField.GetValue(_bootstrap)?.ToString() ?? string.Empty;
            if (!string.Equals(mode, "Home", StringComparison.Ordinal))
            {
                _wired = false;
                if (_panel != null && _panel.activeSelf) SetVisible(false);
                return;
            }

            ResolveTrainingButton();
            if (_trainingButton == null || _wired) return;
            Text label = _trainingButton.GetComponentInChildren<Text>(true);
            if (label == null || label.text.IndexOf("ТРЕНИРОВКА", StringComparison.OrdinalIgnoreCase) < 0) return;

            _trainingButton.onClick.RemoveAllListeners();
            _trainingButton.onClick.AddListener(Open);
            _wired = true;
        }

        private void ResolveBootstrap()
        {
            _bootstrap = FindFirstObjectByType<GameBootstrap>();
            if (_bootstrap == null) return;
            BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            Type type = typeof(GameBootstrap);
            _modeField = type.GetField("_mode", flags);
            _trainingIndexField = type.GetField("_trainingIndex", flags);
            _startTrainingMethod = type.GetMethod("StartTraining", flags);
        }

        private void ResolveTrainingButton()
        {
            if (_trainingButton != null) return;
            GameObject gameCanvas = GameObject.Find("GameCanvas");
            if (gameCanvas == null) return;
            Button[] buttons = gameCanvas.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Text label = buttons[i].GetComponentInChildren<Text>(true);
                if (label != null && label.text.IndexOf("ТРЕНИРОВКА", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _trainingButton = buttons[i];
                    return;
                }
            }
        }

        private void Open()
        {
            SetVisible(true);
        }

        private void StartEasy() => StartDifficulty(0);
        private void StartMedium() => StartDifficulty(1);
        private void StartHard() => StartDifficulty(2);

        private void StartRandom()
        {
            int difficulty = UnityEngine.Random.Range(0, 3);
            StartDifficulty(difficulty);
        }

        private void StartDifficulty(int difficulty)
        {
            if (_bootstrap == null || _startTrainingMethod == null || _trainingIndexField == null) return;
            // GameBootstrap increments _trainingIndex before choosing (index % 3).
            int beforeIncrement = difficulty == 0 ? 2 : difficulty - 1;
            _trainingIndexField.SetValue(_bootstrap, beforeIncrement);
            SetVisible(false);
            _wired = false;
            _startTrainingMethod.Invoke(_bootstrap, null);
        }

        private void Close() => SetVisible(false);

        private void SetVisible(bool visible)
        {
            if (_panel != null) _panel.SetActive(visible);
        }

        private void BuildUi()
        {
            _canvas = new GameObject("TrainingSelectCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvas.transform.SetParent(transform, false);
            Canvas canvas = _canvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 75;
            CanvasScaler scaler = _canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            _panel = new GameObject("TrainingPanel", typeof(RectTransform), typeof(Image));
            _panel.transform.SetParent(_canvas.transform, false);
            SetAnchors(_panel.GetComponent<RectTransform>(), new Vector2(0.08f, 0.22f), new Vector2(0.92f, 0.78f));
            _panel.GetComponent<Image>().color = new Color(0.02f, 0.035f, 0.075f, 0.995f);

            Text title = CreateText(_panel.transform, "Title", "ТРЕНИРОВКА", 54, new Vector2(0.08f, 0.82f), new Vector2(0.92f, 0.95f));
            title.fontStyle = FontStyle.Bold;
            Text subtitle = CreateText(_panel.transform, "Subtitle", "Выбери сложность. Результат не влияет на Daily и кампанию.", 27, new Vector2(0.08f, 0.70f), new Vector2(0.92f, 0.82f));
            subtitle.color = new Color(0.65f, 0.74f, 0.86f, 1f);

            CreateButton(_panel.transform, "ЛЁГКАЯ", new Vector2(0.10f, 0.55f), new Vector2(0.90f, 0.66f), StartEasy);
            CreateButton(_panel.transform, "СРЕДНЯЯ", new Vector2(0.10f, 0.41f), new Vector2(0.90f, 0.52f), StartMedium);
            CreateButton(_panel.transform, "СЛОЖНАЯ", new Vector2(0.10f, 0.27f), new Vector2(0.90f, 0.38f), StartHard);
            CreateButton(_panel.transform, "СЛУЧАЙНАЯ", new Vector2(0.10f, 0.13f), new Vector2(0.90f, 0.24f), StartRandom);
            CreateButton(_panel.transform, "ЗАКРЫТЬ", new Vector2(0.32f, 0.025f), new Vector2(0.68f, 0.10f), Close);
        }

        private static Button CreateButton(Transform parent, string label, Vector2 min, Vector2 max, UnityEngine.Events.UnityAction action)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            SetAnchors(go.GetComponent<RectTransform>(), min, max);
            go.GetComponent<Image>().color = new Color(0.08f, 0.14f, 0.24f, 1f);
            Button button = go.GetComponent<Button>();
            button.onClick.AddListener(action);
            Text text = CreateText(go.transform, "Label", label, 32, Vector2.zero, Vector2.one);
            text.fontStyle = FontStyle.Bold;
            text.raycastTarget = false;
            return button;
        }

        private static Text CreateText(Transform parent, string name, string value, int size, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            SetAnchors(go.GetComponent<RectTransform>(), min, max);
            Text text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 16;
            text.resizeTextMaxSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = value;
            return text;
        }

        private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
