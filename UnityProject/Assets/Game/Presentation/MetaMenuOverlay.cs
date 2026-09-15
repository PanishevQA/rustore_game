using System;
using System.Collections.Generic;
using System.Reflection;
using DontGetSidetracked.Analytics;
using DontGetSidetracked.Core;
using DontGetSidetracked.Economy;
using DontGetSidetracked.Network;
using DontGetSidetracked.Platform.RuStore;
using DontGetSidetracked.Services;
using UnityEngine;
using UnityEngine.UI;

namespace DontGetSidetracked.Presentation
{
    /// <summary>
    /// Lightweight meta UI kept outside GameBootstrap so gameplay input/rendering remains isolated.
    /// The bridge only observes GameBootstrap's presentation mode and never touches gameplay rules.
    /// </summary>
    public sealed class MetaMenuOverlay : MonoBehaviour
    {
        private Canvas _canvas;
        private GameObject _homeButtons;
        private GameObject _panel;
        private Text _panelTitle;
        private Text _panelBody;
        private Transform _actionsRoot;
        private GameBootstrap _bootstrap;
        private FieldInfo _modeField;
        private JsonFileSaveRepository _saveRepository;
        private SaveData _save;
        private UnityGameApi _api;
        private StoreService _store;
        private LeaderboardService _leaderboard;
        private bool _panelOpen;
        private float _nextVisibilityCheck;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (FindFirstObjectByType<MetaMenuOverlay>() != null) return;
            var root = new GameObject("MetaMenuOverlay");
            DontDestroyOnLoad(root);
            root.AddComponent<MetaMenuOverlay>();
        }

        private void Awake()
        {
            _saveRepository = new JsonFileSaveRepository();
            _save = _saveRepository.Load();
            _api = new UnityGameApi(GameRuntimeSettings.BackendBaseUrl);
            _store = new StoreService(new RuStorePaymentService(), _saveRepository, _save);
            _leaderboard = new LeaderboardService(_api, _save.AnonymousPlayerId);
            BuildUi();
            ResolveBootstrap();
            SetHomeButtonsVisible(false);
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextVisibilityCheck) return;
            _nextVisibilityCheck = Time.unscaledTime + 0.2f;
            if (_bootstrap == null) ResolveBootstrap();
            if (_panelOpen)
            {
                SetHomeButtonsVisible(false);
                return;
            }
            SetHomeButtonsVisible(IsHomeMode());
        }

        private void ResolveBootstrap()
        {
            _bootstrap = FindFirstObjectByType<GameBootstrap>();
            _modeField = _bootstrap == null
                ? null
                : typeof(GameBootstrap).GetField("_mode", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        private bool IsHomeMode()
        {
            if (_bootstrap == null || _modeField == null) return false;
            object mode = _modeField.GetValue(_bootstrap);
            return mode != null && string.Equals(mode.ToString(), "Home", StringComparison.Ordinal);
        }

        private async void OpenLeaderboard()
        {
            _panelOpen = true;
            ShowPanel("РЕЙТИНГ", "Загружаем подтверждённые результаты…");
            AnalyticsLifecycle.Service?.Track(AnalyticsEventNames.StoreOpen, Params("surface", "leaderboard"));

            try
            {
                _save = _saveRepository.Load();
                string challengeId = _save.LastDaily?.ChallengeId;
                if (string.IsNullOrWhiteSpace(challengeId))
                {
                    _panelBody.text = "Сначала открой Daily Challenge, чтобы узнать рейтинг дня.";
                    return;
                }

                _leaderboard = new LeaderboardService(_api, _save.AnonymousPlayerId);
                LeaderboardSnapshot snapshot = await _leaderboard.LoadAsync(challengeId, 50);
                var lines = new List<string>();
                int visible = Math.Min(10, snapshot.Items.Count);
                for (int i = 0; i < visible; i++)
                {
                    LeaderboardItemDto item = snapshot.Items[i];
                    string me = item.PlayerId == _save.AnonymousPlayerId ? "  ← ВЫ" : string.Empty;
                    lines.Add($"#{item.Rank}   {ShortPlayer(item.PlayerId)}   {item.Score:0.0}%{me}");
                }

                if (snapshot.CurrentPlayer != null && snapshot.CurrentPlayer.Rank > visible)
                    lines.Add($"\nВАША ПОЗИЦИЯ: #{snapshot.CurrentPlayer.Rank}   {snapshot.CurrentPlayer.Score:0.0}%");
                else if (snapshot.CurrentPlayer == null)
                    lines.Add("\nВаш подтверждённый результат пока не попал в топ-50.");

                _panelBody.text = lines.Count == 0 ? "Пока нет результатов." : string.Join("\n", lines);
            }
            catch (Exception error)
            {
                Debug.LogWarning($"Leaderboard unavailable: {error.Message}");
                _panelBody.text = "Рейтинг сейчас недоступен. Попробуйте позже.";
            }
        }

        private async void OpenStore()
        {
            _panelOpen = true;
            ShowPanel("МАГАЗИН", "Загружаем каталог RuStore…");
            AnalyticsLifecycle.Service?.Track(AnalyticsEventNames.StoreOpen, Params("surface", "home"));
            ClearActions();

            try
            {
                _save = _saveRepository.Load();
                _store = new StoreService(new RuStorePaymentService(), _saveRepository, _save);
                IReadOnlyList<StoreProduct> products = await _store.LoadCatalogAsync();
                if (products == null || products.Count == 0)
                {
                    _panelBody.text = "Каталог RuStore недоступен. Игра продолжает работать без магазина.";
                    AddAction("ВОССТАНОВИТЬ ПОКУПКИ", RestorePurchases);
                    return;
                }

                _panelBody.text = $"Подсказки: {_store.Save.Hints}\nЦена всегда приходит из RuStore.";
                for (int i = 0; i < products.Count; i++)
                {
                    StoreProduct product = products[i];
                    if (product == null || string.IsNullOrWhiteSpace(product.Id)) continue;
                    bool owned = IsOwned(product.Id);
                    string title = string.IsNullOrWhiteSpace(product.Title) ? ProductLabel(product.Id) : product.Title;
                    string price = string.IsNullOrWhiteSpace(product.PriceLabel) ? "—" : product.PriceLabel;
                    string label = owned && !product.IsConsumable
                        ? $"{title}  •  КУПЛЕНО"
                        : $"{title}  •  {price}";
                    string productId = product.Id;
                    AddAction(label, () => Purchase(productId), !owned || product.IsConsumable);
                }
                AddAction("ВОССТАНОВИТЬ ПОКУПКИ", RestorePurchases);
            }
            catch (Exception error)
            {
                Debug.LogWarning($"Store unavailable: {error.Message}");
                _panelBody.text = "Магазин сейчас недоступен. Никакая покупка не была списана.";
            }
        }

        private async void Purchase(string productId)
        {
            AnalyticsLifecycle.Service?.Track(AnalyticsEventNames.PurchaseStart, Params("product_id", productId));
            _panelBody.text = $"Покупка {ProductLabel(productId)}…";
            SetActionsInteractable(false);
            try
            {
                StorePurchaseResult result = await _store.PurchaseAsync(productId);
                _save = _store.Save;
                if (result?.Payment?.Outcome == PurchaseOutcome.Completed)
                {
                    AnalyticsLifecycle.Service?.Track(AnalyticsEventNames.PurchaseSuccess, Params("product_id", productId));
                    _panelBody.text = result.GrantApplied
                        ? "Покупка подтверждена и применена."
                        : "Покупка уже была применена ранее.";
                }
                else if (result?.Payment?.Outcome == PurchaseOutcome.Cancelled)
                {
                    AnalyticsLifecycle.Service?.Track(AnalyticsEventNames.PurchaseCancel, Params("product_id", productId));
                    _panelBody.text = "Покупка отменена.";
                }
                else
                {
                    AnalyticsLifecycle.Service?.Track(AnalyticsEventNames.PurchaseError, Params(
                        "product_id", productId,
                        "error", result?.Payment?.ErrorMessage ?? "unknown"));
                    _panelBody.text = "Покупка не завершена.";
                }
            }
            catch (Exception error)
            {
                AnalyticsLifecycle.Service?.Track(AnalyticsEventNames.PurchaseError, Params("product_id", productId, "error", error.Message));
                _panelBody.text = "Ошибка покупки. Товар не выдан.";
            }
            finally
            {
                SetActionsInteractable(true);
            }
        }

        private async void RestorePurchases()
        {
            _panelBody.text = "Восстанавливаем подтверждённые покупки RuStore…";
            SetActionsInteractable(false);
            try
            {
                int restored = await _store.RestoreAsync();
                _save = _store.Save;
                _panelBody.text = restored > 0
                    ? $"Восстановлено: {restored}."
                    : "Новых покупок для восстановления нет.";
            }
            catch (Exception error)
            {
                Debug.LogWarning($"Restore purchases failed: {error.Message}");
                _panelBody.text = "Не удалось восстановить покупки. Попробуйте позже.";
            }
            finally
            {
                SetActionsInteractable(true);
            }
        }

        private bool IsOwned(string productId) =>
            productId == ProductIds.Hints10 ? false : _store.HasEntitlement(productId) || _store.OwnsSkin(productId);

        private void BuildUi()
        {
            var canvasGo = new GameObject("MetaCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 40;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            _homeButtons = new GameObject("HomeMetaButtons", typeof(RectTransform));
            _homeButtons.transform.SetParent(canvasGo.transform, false);
            SetAnchors(_homeButtons.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
            CreateButton(_homeButtons.transform, "РЕЙТИНГ", new Vector2(0.52f, 0.035f), new Vector2(0.71f, 0.105f), OpenLeaderboard);
            CreateButton(_homeButtons.transform, "МАГАЗИН", new Vector2(0.72f, 0.035f), new Vector2(0.92f, 0.105f), OpenStore);

            _panel = new GameObject("MetaPanel", typeof(RectTransform), typeof(Image));
            _panel.transform.SetParent(canvasGo.transform, false);
            SetAnchors(_panel.GetComponent<RectTransform>(), new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.94f));
            _panel.GetComponent<Image>().color = new Color(0.025f, 0.04f, 0.065f, 0.98f);

            _panelTitle = CreateText(_panel.transform, "Title", 58, TextAnchor.MiddleCenter, new Vector2(0.06f, 0.87f), new Vector2(0.94f, 0.98f));
            _panelBody = CreateText(_panel.transform, "Body", 34, TextAnchor.UpperLeft, new Vector2(0.08f, 0.48f), new Vector2(0.92f, 0.86f));
            _panelBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            _panelBody.verticalOverflow = VerticalWrapMode.Overflow;

            var actions = new GameObject("Actions", typeof(RectTransform), typeof(VerticalLayoutGroup));
            actions.transform.SetParent(_panel.transform, false);
            var art = actions.GetComponent<RectTransform>();
            SetAnchors(art, new Vector2(0.08f, 0.12f), new Vector2(0.92f, 0.47f));
            var layout = actions.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 12;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = true;
            _actionsRoot = actions.transform;

            CreateButton(_panel.transform, "ЗАКРЫТЬ", new Vector2(0.30f, 0.025f), new Vector2(0.70f, 0.095f), ClosePanel);
            _panel.SetActive(false);
        }

        private void ShowPanel(string title, string body)
        {
            ClearActions();
            _panelTitle.text = title;
            _panelBody.text = body;
            _panel.SetActive(true);
            SetHomeButtonsVisible(false);
        }

        private void ClosePanel()
        {
            _panelOpen = false;
            _panel.SetActive(false);
        }

        private void ClearActions()
        {
            if (_actionsRoot == null) return;
            for (int i = _actionsRoot.childCount - 1; i >= 0; i--)
                Destroy(_actionsRoot.GetChild(i).gameObject);
        }

        private void AddAction(string label, UnityEngine.Events.UnityAction action, bool interactable = true)
        {
            Button button = CreateLayoutButton(_actionsRoot, label, action);
            button.interactable = interactable;
        }

        private void SetActionsInteractable(bool value)
        {
            if (_actionsRoot == null) return;
            for (int i = 0; i < _actionsRoot.childCount; i++)
            {
                Button button = _actionsRoot.GetChild(i).GetComponent<Button>();
                if (button != null) button.interactable = value;
            }
        }

        private void SetHomeButtonsVisible(bool visible)
        {
            if (_homeButtons != null && _homeButtons.activeSelf != visible)
                _homeButtons.SetActive(visible);
        }

        private static Button CreateLayoutButton(Transform parent, string label, UnityEngine.Events.UnityAction action)
        {
            var go = new GameObject("Action", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.12f, 0.22f, 0.32f, 1f);
            go.GetComponent<LayoutElement>().preferredHeight = 92;
            var button = go.GetComponent<Button>();
            button.onClick.AddListener(action);
            var text = CreateText(go.transform, "Label", 30, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
            text.raycastTarget = false;
            return button;
        }

        private static Button CreateButton(Transform parent, string label, Vector2 min, Vector2 max, UnityEngine.Events.UnityAction action)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            SetAnchors(go.GetComponent<RectTransform>(), min, max);
            go.GetComponent<Image>().color = new Color(0.12f, 0.22f, 0.32f, 1f);
            var button = go.GetComponent<Button>();
            button.onClick.AddListener(action);
            var text = CreateText(go.transform, "Label", 28, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
            text.text = label;
            text.raycastTarget = false;
            return button;
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
            text.resizeTextMinSize = 16;
            text.resizeTextMaxSize = size;
            return text;
        }

        private static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static string ShortPlayer(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return "ANON";
            return id.Length <= 10 ? id : id.Substring(0, 6) + "…" + id.Substring(id.Length - 3);
        }

        private static string ProductLabel(string id)
        {
            switch (id)
            {
                case ProductIds.RemoveAds: return "Без рекламы";
                case ProductIds.StarterPack: return "Стартовый набор";
                case ProductIds.SkinNeon: return "Неоновый след";
                case ProductIds.SkinRetro: return "Ретро-след";
                case ProductIds.Hints10: return "10 подсказок";
                default: return id;
            }
        }

        private static Dictionary<string, object> Params(params object[] pairs)
        {
            var result = new Dictionary<string, object>();
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                string key = pairs[i]?.ToString();
                if (!string.IsNullOrWhiteSpace(key)) result[key] = pairs[i + 1];
            }
            return result;
        }
    }
}
