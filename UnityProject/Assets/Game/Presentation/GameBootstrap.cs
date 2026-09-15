using System;
using System.Collections;
using System.Collections.Generic;
using DontGetSidetracked.Core;
using DontGetSidetracked.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DontGetSidetracked.Presentation
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        private enum Mode { Home, Daily, Training }
        private enum RoundState { Idle, Showing, Drawing, Result }

        private readonly RouteGenerator _generator = new RouteGenerator();
        private readonly ScoreCalculator _scorer = new ScoreCalculator();
        private readonly List<RecordedPoint> _recording = new List<RecordedPoint>(256);
        private readonly List<double> _dailyScores = new List<double>(3);

        private RouteGraphic _referenceGraphic;
        private RouteGraphic _playerGraphic;
        private Text _title;
        private Text _status;
        private Button _primary;
        private Button _secondary;
        private Button _share;
        private RectTransform _playArea;

        private Mode _mode = Mode.Home;
        private RoundState _state = RoundState.Idle;
        private DailyChallengeDefinition _daily;
        private RouteDefinition _route;
        private int _dailyIndex;
        private long _gestureStartMs;
        private bool _pointerDown;
        private int _trainingIndex;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (FindFirstObjectByType<GameBootstrap>() != null) return;
            var root = new GameObject("GameBootstrap");
            DontDestroyOnLoad(root);
            root.AddComponent<GameBootstrap>();
        }

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.Portrait;
            BuildUi();
            ShowHome();
        }

        private void Update()
        {
            if (_state != RoundState.Drawing) return;
            PollPointer();
        }

        private void ShowHome()
        {
            _mode = Mode.Home;
            _state = RoundState.Idle;
            _referenceGraphic.Clear();
            _playerGraphic.Clear();
            _title.text = "НЕ СБЕЙСЯ!";
            _status.text = "Запомни линию. Проведи по памяти.";
            ConfigureButton(_primary, "DAILY CHALLENGE", StartDaily);
            ConfigureButton(_secondary, "ТРЕНИРОВКА", StartTraining);
            _share.gameObject.SetActive(false);
        }

        private void StartDaily()
        {
            _mode = Mode.Daily;
            _dailyIndex = 0;
            _dailyScores.Clear();
            DateTime utc = DateTime.UtcNow;
            string id = $"daily_{utc:yyyy_MM_dd}";
            long seed = StableDateSeed(utc.Date);
            _daily = new DailyChallengeFactory().Create(id, seed, RouteGenerator.CurrentGeneratorVersion);
            StartCoroutine(BeginRoute(_daily.Routes[0]));
        }

        private void StartTraining()
        {
            _mode = Mode.Training;
            _trainingIndex++;
            long seed = DateTime.UtcNow.Ticks ^ (_trainingIndex * 7919L);
            RouteDifficulty difficulty = (RouteDifficulty)(_trainingIndex % 3);
            StartCoroutine(BeginRoute(_generator.Generate(seed & 0x7FFFFFFF, RouteGenerator.CurrentGeneratorVersion, difficulty)));
        }

        private IEnumerator BeginRoute(RouteDefinition route)
        {
            _route = route;
            _recording.Clear();
            _playerGraphic.Clear();
            _referenceGraphic.color = new Color(0.1f, 0.9f, 1f, 1f);
            _referenceGraphic.Thickness = Mathf.Max(8f, route.PathWidth / (float)FixedPoint2.Scale * _playArea.rect.width);
            _referenceGraphic.SetPoints(route.ReferencePoints);
            _title.text = _mode == Mode.Daily ? $"DAILY {_dailyIndex + 1}/3" : "ТРЕНИРОВКА";
            _status.text = "ЗАПОМНИ ЛИНИЮ";
            _state = RoundState.Showing;
            HideButtons();

            yield return new WaitForSecondsRealtime(route.DisplayTimeMs / 1000f);
            _status.text = "3"; yield return new WaitForSecondsRealtime(0.35f);
            _status.text = "2"; yield return new WaitForSecondsRealtime(0.35f);
            _status.text = "1"; yield return new WaitForSecondsRealtime(0.35f);
            _referenceGraphic.Clear();
            _status.text = "ТЕПЕРЬ ПОВТОРИ";
            _state = RoundState.Drawing;
        }

        private void PollPointer()
        {
            bool pressed;
            bool released;
            Vector2 screenPosition;

            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                screenPosition = touch.position;
                pressed = touch.phase == TouchPhase.Began;
                released = touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled;
                if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary) pressed = false;
            }
            else
            {
                screenPosition = Input.mousePosition;
                pressed = Input.GetMouseButtonDown(0);
                released = Input.GetMouseButtonUp(0);
            }

            if (pressed && TryScreenToFixed(screenPosition, out FixedPoint2 start))
            {
                _pointerDown = true;
                _gestureStartMs = NowMs();
                _recording.Clear();
                AddPoint(start);
            }

            bool held = Input.touchCount > 0 || Input.GetMouseButton(0);
            if (_pointerDown && held && TryScreenToFixed(screenPosition, out FixedPoint2 point)) AddPoint(point);

            if (_pointerDown && released)
            {
                if (TryScreenToFixed(screenPosition, out FixedPoint2 end)) AddPoint(end);
                _pointerDown = false;
                FinishRound();
            }
        }

        private void AddPoint(FixedPoint2 point)
        {
            long ts = NowMs() - _gestureStartMs;
            if (_recording.Count > 0 && _recording[_recording.Count - 1].Position.DistanceSquared(point) < 700L * 700L) return;
            _recording.Add(new RecordedPoint(point, ts));
            var positions = new List<FixedPoint2>(_recording.Count);
            for (int i = 0; i < _recording.Count; i++) positions.Add(_recording[i].Position);
            _playerGraphic.SetPoints(positions);
        }

        private void FinishRound()
        {
            _state = RoundState.Result;
            ScoreBreakdown result = _scorer.Calculate(_route, _recording);
            _referenceGraphic.SetPoints(_route.ReferencePoints);
            _referenceGraphic.color = new Color(0.1f, 0.9f, 1f, 0.65f);
            _playerGraphic.color = result.Score >= 90 ? new Color(0.2f, 1f, 0.45f, 1f) : new Color(1f, 0.75f, 0.15f, 1f);
            _status.text = $"ТОЧНОСТЬ {result.Score:0.0}%";

            if (_mode == Mode.Daily)
            {
                _dailyScores.Add(result.Score);
                if (_dailyIndex < 2)
                    ConfigureButton(_primary, "СЛЕДУЮЩИЙ МАРШРУТ", NextDailyRoute);
                else
                    ConfigureButton(_primary, $"ИТОГ {DailyChallengeFactory.DailyScore(_dailyScores):0.0}%", ShowHome);
            }
            else
            {
                ConfigureButton(_primary, "ЕЩЁ РАЗ", StartTraining);
            }

            ConfigureButton(_secondary, "ДОМОЙ", ShowHome);
            ConfigureButton(_share, "БРОСИТЬ ВЫЗОВ", ShareCurrentResult);
            _share.gameObject.SetActive(true);
        }

        private void NextDailyRoute()
        {
            _dailyIndex++;
            StartCoroutine(BeginRoute(_daily.Routes[_dailyIndex]));
        }

        private void ShareCurrentResult()
        {
            double score = _mode == Mode.Daily && _dailyScores.Count == 3 ? DailyChallengeFactory.DailyScore(_dailyScores) : (_dailyScores.Count > 0 ? _dailyScores[_dailyScores.Count - 1] : 0);
            string text = $"Я прошёл НЕ СБЕЙСЯ! на {score:0.0}%. Сможешь точнее?";
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var intentClass = new AndroidJavaClass("android.content.Intent"))
            using (var intent = new AndroidJavaObject("android.content.Intent"))
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                intent.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_SEND"));
                intent.Call<AndroidJavaObject>("setType", "text/plain");
                intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_TEXT"), text);
                var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                var chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, "Бросить вызов");
                activity.Call("startActivity", chooser);
            }
#else
            Debug.Log(text);
#endif
        }

        private bool TryScreenToFixed(Vector2 screen, out FixedPoint2 point)
        {
            point = default;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_playArea, screen, null, out Vector2 local)) return false;
            Rect rect = _playArea.rect;
            double nx = (local.x - rect.xMin) / rect.width;
            double ny = (local.y - rect.yMin) / rect.height;
            if (nx < 0 || nx > 1 || ny < 0 || ny > 1) return false;
            point = FixedPoint2.FromNormalized(nx, ny);
            return true;
        }

        private void BuildUi()
        {
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                DontDestroyOnLoad(es);
            }

            var canvasGo = new GameObject("GameCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            _title = CreateText(canvasGo.transform, "Title", 64, TextAnchor.MiddleCenter, new Vector2(0.05f, 0.88f), new Vector2(0.95f, 0.98f));
            _status = CreateText(canvasGo.transform, "Status", 50, TextAnchor.MiddleCenter, new Vector2(0.05f, 0.76f), new Vector2(0.95f, 0.86f));

            var play = new GameObject("PlayArea", typeof(RectTransform), typeof(Image));
            play.transform.SetParent(canvasGo.transform, false);
            _playArea = play.GetComponent<RectTransform>();
            SetAnchors(_playArea, new Vector2(0.06f, 0.25f), new Vector2(0.94f, 0.75f));
            play.GetComponent<Image>().color = new Color(0.035f, 0.045f, 0.07f, 1f);

            _referenceGraphic = CreateRouteGraphic(play.transform, "Reference", new Color(0.1f, 0.9f, 1f, 1f));
            _playerGraphic = CreateRouteGraphic(play.transform, "Player", new Color(1f, 0.75f, 0.15f, 1f));

            _primary = CreateButton(canvasGo.transform, "Primary", new Vector2(0.08f, 0.12f), new Vector2(0.92f, 0.20f));
            _secondary = CreateButton(canvasGo.transform, "Secondary", new Vector2(0.08f, 0.035f), new Vector2(0.48f, 0.105f));
            _share = CreateButton(canvasGo.transform, "Share", new Vector2(0.52f, 0.035f), new Vector2(0.92f, 0.105f));
        }

        private void HideButtons()
        {
            _primary.gameObject.SetActive(false);
            _secondary.gameObject.SetActive(false);
            _share.gameObject.SetActive(false);
        }

        private static RouteGraphic CreateRouteGraphic(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(RouteGraphic));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            SetAnchors(rt, Vector2.zero, Vector2.one);
            var graphic = go.GetComponent<RouteGraphic>();
            graphic.color = color;
            graphic.raycastTarget = false;
            return graphic;
        }

        private static Text CreateText(Transform parent, string name, int size, TextAnchor anchor, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            SetAnchors(go.GetComponent<RectTransform>(), min, max);
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.alignment = anchor;
            text.color = Color.white;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 18;
            text.resizeTextMaxSize = size;
            return text;
        }

        private static Button CreateButton(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            SetAnchors(go.GetComponent<RectTransform>(), min, max);
            go.GetComponent<Image>().color = new Color(0.12f, 0.22f, 0.32f, 1f);
            var label = CreateText(go.transform, "Label", 36, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
            label.raycastTarget = false;
            return go.GetComponent<Button>();
        }

        private static void ConfigureButton(Button button, string label, UnityEngine.Events.UnityAction action)
        {
            button.gameObject.SetActive(true);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
            button.GetComponentInChildren<Text>().text = label;
        }

        private static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static long StableDateSeed(DateTime date)
        {
            unchecked
            {
                int value = date.Year * 10000 + date.Month * 100 + date.Day;
                return (value * 1103515245L + 12345L) & 0x7FFFFFFF;
            }
        }

        private static long NowMs() => (long)(Time.realtimeSinceStartupAsDouble * 1000.0);
    }
}
