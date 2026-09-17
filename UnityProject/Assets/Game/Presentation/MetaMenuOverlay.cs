using System;
using System.Collections.Generic;
using DontGetSidetracked.Analytics;
using DontGetSidetracked.Core;
using DontGetSidetracked.Economy;
using DontGetSidetracked.Gameplay;
using DontGetSidetracked.Platform.RuStore;
using DontGetSidetracked.Services;
using UnityEngine;
using UnityEngine.UI;

namespace DontGetSidetracked.Presentation
{
    /// <summary>
    /// Home-only meta UI for the offline-first MVP: local statistics, settings, cosmetic selection and RuStore shop.
    /// No developer-operated backend is required.
    /// </summary>
    public sealed class MetaMenuOverlay : MonoBehaviour
    {
        private GameObject _homeButtons;
        private GameObject _panel;
        private Text _panelTitle;
        private Text _panelBody;
        private Transform _actionsRoot;
        private GameBootstrap _bootstrap;
        private JsonFileSaveRepository _saveRepository;
        private SaveData _save;
        private StoreService _store;
        private CosmeticSelectionService _cosmetics;
        private GameSettingsService _settings;
        private IReadOnlyList<StoreProduct> _lastStoreProducts;
        private bool _panelOpen;
        private int _panelRevision;
        private float _nextVisibilityCheck;

        public bool IsPanelOpen => _panelOpen;

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
            RebuildLocalServices();
            BuildUi();
            ResolveBootstrap();
            SetHomeButtonsVisible(false);
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextVisibilityCheck) return;
            _nextVisibilityCheck = Time.unscaledTime + 0.2f;
            if (_bootstrap == null) ResolveBootstrap();
            SetHomeButtonsVisible(!_panelOpen && IsHomeMode());
        }

        private void RebuildLocalServices()
        {
            _save = _saveRepository.Load();
            _store = new StoreService(
                new RuStorePaymentService(),
                _saveRepository,
                _save);
            _cosmetics = new CosmeticSelectionService(_saveRepository, _save);
            _settings = new GameSettingsService(_saveRepository, _save);
        }

        private void ResolveBootstrap()
        {
            _bootstrap = FindFirstObjectByType<GameBootstrap>();
        }

        private bool IsHomeMode() =>
            _bootstrap != null && GameBootstrapRuntimeBridge.IsHome(_bootstrap);

        private int BeginPanelNavigation()
        {
            _panelOpen = true;
            _panelRevision++;
            return _panelRevision;
        }

        private bool IsCurrentPanel(int revision) => _panelOpen && revision == _panelRevision;

        private void OpenStatistics()
        {
            BeginPanelNavigation();
            RebuildLocalServices();
            ShowPanel("СТАТИСТИКА", BuildStatisticsText());
            AddAction("НАСТРОЙКИ", OpenSettings);
            AddAction("КОСМЕТИКА", OpenCosmetics);
        }

        private string BuildStatisticsText()
        {
            var progress = new CampaignProgressService(_saveRepository, _save);
            string best = _save.PersonalBest > 0 ? _save.PersonalBest.ToString("0.0") + "%" : "—";
            int cosmetics = _save.Inventory?.Count ?? 0;
            int purchases = _save.ProcessedPurchaseIds?.Count ?? 0;
            string lastDaily = _save.LastCompletedDailyDateUtc;
            if (string.IsNullOrWhiteSpace(lastDaily)) lastDaily = "—";

            return
                "КАМПАНИЯ\n" +
                $"Пройдено: {progress.CompletedLevels()}/{CampaignLevelCatalog.TotalLevels}\n" +
                $"Звёзды: {progress.TotalStars()}/{CampaignLevelCatalog.TotalLevels * 3}\n" +
                $"Открыт уровень: {progress.HighestUnlockedLevel}\n" +
                $"Монеты: {_save.Coins}    Подсказки: {_save.Hints}\n\n" +
                "DAILY\n" +
                $"🔥 Серия: {_save.Streak} дней\n" +
                $"Лучший: {best}\n" +
                $"Завершено: {_save.CompletedDailyCount}\n" +
                $"Последний: {lastDaily}\n\n" +
                $"Косметика: {cosmetics}\n" +
                $"Выбран след: {CosmeticLabel(_cosmetics.SelectedSkinId)}\n" +
                $"Покупок применено: {purchases}\n\n" +
                "Прогресс хранится локально на этом устройстве.";
        }

        private void OpenSettings()
        {
            BeginPanelNavigation();
            RebuildLocalServices();
            AnalyticsLifecycle.Service?.Track(AnalyticsEventNames.SettingsOpen, Params("surface", "meta"));
            RenderSettings();
        }

        private void RenderSettings()
        {
            ShowPanel(
                "НАСТРОЙКИ",
                "Настройки сохраняются локально. Тактильная отдача также учитывает системную настройку Android.");

            AddAction($"ЗВУК: {OnOff(_settings.SoundEnabled)}", ToggleSound);
            AddAction($"ВИБРООТКЛИК: {OnOff(_settings.HapticsEnabled)}", ToggleHaptics);
            AddAction("НАЗАД К СТАТИСТИКЕ", OpenStatistics);
        }

        private void ToggleSound()
        {
            RebuildLocalServices();
            bool next = !_settings.SoundEnabled;
            if (_settings.SetSound(next))
            {
                AnalyticsLifecycle.Service?.Track(
                    AnalyticsEventNames.SettingsChange,
                    Params("setting", "sound", "enabled", next));
            }
            if (next) FeedbackRuntimeCoordinator.PreviewSound();
            RebuildLocalServices();
            RenderSettings();
        }

        private void ToggleHaptics()
        {
            RebuildLocalServices();
            bool next = !_settings.HapticsEnabled;
            if (_settings.SetHaptics(next))
            {
                AnalyticsLifecycle.Service?.Track(
                    AnalyticsEventNames.SettingsChange,
                    Params("setting", "haptics", "enabled", next));
            }
            if (next) FeedbackRuntimeCoordinator.PreviewHaptic();
            RebuildLocalServices();
            RenderSettings();
        }

        private void OpenCosmetics()
        {
            BeginPanelNavigation();
            RebuildLocalServices();
            RenderCosmetics();
        }

        private void RenderCosmetics()
        {
            IReadOnlyList<string> available = _cosmetics.GetAvailableSkins();
            string selected = _cosmetics.SelectedSkinId;
            string body = available.Count > 1
                ? "Выберите цвет своего следа. На экране результата цвет снова показывает качество прохождения."
                : "Пока доступен стандартный след. Дополнительные варианты можно получить в магазине.";
            ShowPanel("КОСМЕТИКА", body);

            for (int i = 0; i < available.Count; i++)
            {
                string skinId = available[i];
                bool active = string.Equals(selected, skinId, StringComparison.Ordinal);
                string label = active
                    ? $"✓ {CosmeticLabel(skinId)}"
                    : CosmeticLabel(skinId);
                string captured = skinId;
                AddAction(label, () => SelectCosmetic(captured), !active);
            }

            AddAction("В МАГАЗИН", OpenStore);
            AddAction("НАЗАД К СТАТИСТИКЕ", OpenStatistics);
        }

        private void SelectCosmetic(string skinId)
        {
            RebuildLocalServices();
            if (!_cosmetics.IsSelectable(skinId))
            {
                RenderCosmetics();
                return;
            }

            bool changed = _cosmetics.Select(skinId);
            _save = _cosmetics.Save;
            if (changed)
            {
                AnalyticsLifecycle.Service?.Track(
                    AnalyticsEventNames.CosmeticSelect,
                    Params("skin_id", skinId));
            }
            RenderCosmetics();
        }

        private async void OpenStore()
        {
            int revision = BeginPanelNavigation();
            ShowPanel("МАГАЗИН", "Загружаем каталог RuStore…");
            AnalyticsLifecycle.Service?.Track(AnalyticsEventNames.StoreOpen, Params("surface", "home"));

            try
            {
                RebuildLocalServices();
                IReadOnlyList<StoreProduct> products = await _store.LoadCatalogAsync();
                if (!IsCurrentPanel(revision)) return;
                _lastStoreProducts = products;
                RenderStore(products);
            }
            catch (Exception error)
            {
                Debug.LogWarning($"Store unavailable: {error.Message}");
                if (!IsCurrentPanel(revision)) return;
                _panelBody.text = "Магазин сейчас недоступен. Игра продолжает работать полностью локально.";
                ClearActions();
                AddAction("ВОССТАНОВИТЬ ПОКУПКИ", RestorePurchases);
                AddAction("КОСМЕТИКА", OpenCosmetics);
            }
        }

        private void RenderStore(IReadOnlyList<StoreProduct> products, string message = null)
        {
            ClearActions();
            string prefix = string.IsNullOrWhiteSpace(message) ? string.Empty : message + "\n\n";
            _panelBody.text = prefix + $"Подсказки: {_store.Save.Hints}\nЦена всегда приходит из RuStore.";

            if (products == null || products.Count == 0)
            {
                _panelBody.text += "\n\nКаталог RuStore сейчас недоступен.";
                AddAction("ВОССТАНОВИТЬ ПОКУПКИ", RestorePurchases);
                AddAction("КОСМЕТИКА", OpenCosmetics);
                return;
            }

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
            AddAction("КОСМЕТИКА", OpenCosmetics);
        }

        private async void Purchase(string productId)
        {
            int revision = _panelRevision;
            AnalyticsLifecycle.Service?.Track(AnalyticsEventNames.PurchaseStart, Params("product_id", productId));
            _panelBody.text = $"Покупка {ProductLabel(productId)}…";
            SetActionsInteractable(false);

            try
            {
                StorePurchaseResult result = await _store.PurchaseAsync(productId);
                _save = _store.Save;
                if (!IsCurrentPanel(revision)) return;

                if (result?.Payment?.Outcome == PurchaseOutcome.Completed && result.Verified)
                {
                    AnalyticsLifecycle.Service?.Track(AnalyticsEventNames.PurchaseSuccess, Params("product_id", productId));
                    string message = result.GrantApplied
                        ? "Покупка подтверждена RuStore и применена на этом устройстве."
                        : "Покупка уже была применена ранее.";
                    RenderStore(_lastStoreProducts, message);
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
                        "error", result?.Verification?.ErrorMessage ?? result?.Payment?.ErrorMessage ?? "unknown"));

                    _panelBody.text = result?.Payment?.Outcome == PurchaseOutcome.Completed
                        ? "RuStore принял покупку, но товар ещё не появился среди подтверждённых. Нажмите «Восстановить покупки»."
                        : "Покупка не завершена. Товар не выдан.";
                }
            }
            catch (Exception error)
            {
                AnalyticsLifecycle.Service?.Track(AnalyticsEventNames.PurchaseError, Params(
                    "product_id", productId,
                    "error", error.Message));
                if (IsCurrentPanel(revision))
                    _panelBody.text = "Ошибка покупки. Товар не выдан.";
            }
            finally
            {
                if (IsCurrentPanel(revision)) SetActionsInteractable(true);
            }
        }

        private async void RestorePurchases()
        {
            int revision = _panelRevision;
            _panelBody.text = "Восстанавливаем подтверждённые покупки RuStore…";
            SetActionsInteractable(false);
            try
            {
                int restored = await _store.RestoreAsync();
                _save = _store.Save;
                if (!IsCurrentPanel(revision)) return;
                string message = restored > 0
                    ? $"Восстановлено: {restored}."
                    : "Новых покупок для восстановления нет.";
                RenderStore(_lastStoreProducts, message);
            }
            catch (Exception error)
            {
                Debug.LogWarning($"Restore purchases failed: {error.Message}");
                if (IsCurrentPanel(revision))
                    _panelBody.text = "Не удалось восстановить покупки. Попробуйте позже.";
            }
            finally
            {
                if (IsCurrentPanel(revision)) SetActionsInteractable(true);
            }
        }

        private bool IsOwned(string productId) =>
            productId == ProductIds.Hints10 ? false : _store.HasEntitlement(productId) || _store.OwnsSkin(productId);

        private void BuildUi()
        {
            var canvasGo = new GameObject("MetaCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            _homeButtons = new GameObject("HomeMetaButtons", typeof(RectTransform));
            _homeButtons.transform.SetParent(canvasGo.transform, false);
            SetAnchors(_homeButtons.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
            CreateButton(_homeButtons.transform, "СТАТИСТИКА", new Vector2(0.52f, 0.035f), new Vector2(0.71f, 0.105f), OpenStatistics);
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
            SetAnchors(actions.GetComponent<RectTransform>(), new Vector2(0.08f, 0.12f), new Vector2(0.92f, 0.47f));
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

        public void ClosePanel()
        {
            _panelRevision++;
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
            go.GetComponent<LayoutElement>().preferredHeight = 84;
            var button = go.GetComponent<Button>();
            button.onClick.AddListener(action);
            var text = CreateText(go.transform, "Label", 30, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
            text.text = label;
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

        private static string CosmeticLabel(string id)
        {
            switch (id)
            {
                case CosmeticIds.Default: return "Стандартный след";
                case CosmeticIds.Neon: return "Неоновый след";
                case CosmeticIds.Retro: return "Ретро-след";
                case CosmeticIds.Gold: return "Золотой след";
                default: return id;
            }
        }

        private static string OnOff(bool value) => value ? "ВКЛ" : "ВЫКЛ";

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
