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
        private Image _panelTitleIcon;
        private Text _panelBody;
        private Transform _actionsRoot;
        private ScrollRect _scroll;
        private GameBootstrap _bootstrap;
        private JsonFileSaveRepository _saveRepository;
        private SaveData _save;
        private StoreService _store;
        private CosmeticSelectionService _cosmetics;
        private GameSettingsService _settings;
        private IReadOnlyList<StoreProduct> _lastStoreProducts;
        private bool _storeBusy;
        private bool _catalogLoading;
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
            _bootstrap != null && GameBootstrapRuntimeBridge.IsPlainHome(_bootstrap);

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
            ShowPanel("МОЯ СТАТИСТИКА", "Каждый маршрут делает тебя точнее.");
            RenderStatisticsRows();
        }

        private void RenderStatisticsRows()
        {
            var progress = new CampaignProgressService(_saveRepository, _save);
            string best = _save.PersonalBest > 0 ? _save.PersonalBest.ToString("0.0") + "%" : "—";

            Transform hero = AddLayoutCard("BestScoreHero", 224, ReleaseUiComponents.Gold, true);
            ReleaseUiComponents.Icon(hero, "BestIcon", GeneratedUiAssets.StarFilled,
                new Vector2(0.06f, 0.28f), new Vector2(0.24f, 0.83f));
            ReleaseUiKit.TextBlock(hero, "Caption", "ЛУЧШИЙ РЕЗУЛЬТАТ", 28, TextAnchor.MiddleLeft,
                new Vector2(0.29f, 0.61f), new Vector2(0.94f, 0.87f), ReleaseUiComponents.Muted, FontStyle.Bold);
            ReleaseUiKit.TextBlock(hero, "BestScore", best, 76, TextAnchor.MiddleLeft,
                new Vector2(0.28f, 0.12f), new Vector2(0.94f, 0.62f), ReleaseUiComponents.Text, FontStyle.Bold);

            AddMetricPair("DAILY ЗАВЕРШЕНО", _save.CompletedDailyCount.ToString(), "СЕРИЯ ДНЕЙ", _save.Streak.ToString());
            AddMetricPair("МОНЕТЫ", _save.Coins.ToString(), "ПОДСКАЗКИ", _save.Hints.ToString());
            AddInfoRow(GeneratedUiAssets.CheckIcon, "ПРОЙДЕНО УРОВНЕЙ",
                progress.CompletedLevels() + " / " + CampaignLevelCatalog.TotalLevels, ReleaseUiComponents.Violet);
            AddInfoRow(GeneratedUiAssets.StarFilled, "ЗВЁЗДЫ КАМПАНИИ", progress.TotalStars().ToString(), ReleaseUiComponents.Gold);
            AddStoreSectionLabel("Статистика хранится локально на этом устройстве.", ReleaseUiComponents.Muted);
            AddAction("НАСТРОЙКИ", OpenSettings);
            AddAction("КОСМЕТИКА", OpenCosmetics);
        }

        private void OpenSettings()
        {
            BeginPanelNavigation();
            RebuildLocalServices();
            AnalyticsLifecycle.Service?.Track(AnalyticsEventNames.SettingsOpen, Params("surface", "meta"));
            RenderSettings();
        }

        private void RenderSettings(string message = null)
        {
            string body = string.IsNullOrWhiteSpace(message) ? "Изменения сохраняются сразу на устройстве." : message;
            ShowPanel("НАСТРОЙКИ", body);

            AddSettingToggleRow(GeneratedUiAssets.SettingsIcon, "ЗВУК", "Игровые эффекты", _settings.SoundEnabled, ToggleSound);
            AddSettingToggleRow(GeneratedUiAssets.SettingsIcon, "ВИБРООТКЛИК", "Отклик на касания", _settings.HapticsEnabled, ToggleHaptics);
            AddInfoRow(GeneratedUiAssets.DailyIcon, "DAILY НАПОМИНАНИЯ",
                _save.NotificationPermissionGranted ? "ВКЛ" : "ПОКА ВЫКЛ",
                _save.NotificationPermissionGranted ? ReleaseUiComponents.Success : ReleaseUiComponents.Muted);
            AddInfoRow(GeneratedUiAssets.CheckIcon, "ПРОГРЕСС", "СОХРАНЁН", ReleaseUiComponents.Success);
            AddInfoRow(GeneratedUiAssets.SettingsIcon, "ВЕРСИЯ ИГРЫ", Application.version, ReleaseUiComponents.Blue);
            AddAction("ВОССТАНОВИТЬ ПОКУПКИ", RestorePurchasesFromSettings, !_storeBusy);
            AddAction("НАЗАД К СТАТИСТИКЕ", OpenStatistics);
        }

        private async void RestorePurchasesFromSettings()
        {
            if (_storeBusy) return;
            int revision = _panelRevision;
            RebuildLocalServices();
            StoreService store = _store;
            _storeBusy = true;
            _panelBody.text = "Проверяем покупки RuStore…";
            SetActionsInteractable(false);
            try
            {
                int restored = await store.RestoreAsync();
                _storeBusy = false;
                _save = store.Save;
                if (!IsCurrentPanel(revision)) return;
                RenderSettings(restored > 0
                    ? $"Восстановлено покупок: {restored}."
                    : "Новых покупок для восстановления нет.");
            }
            catch (Exception error)
            {
                _storeBusy = false;
                Debug.LogWarning($"Restore purchases from settings failed: {error.Message}");
                if (IsCurrentPanel(revision))
                    RenderSettings("Не удалось восстановить покупки. Попробуй позже.");
            }
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
                string captured = skinId;
                Transform card = AddLayoutCard("CosmeticChoice", 152,
                    active ? ReleaseUiComponents.Cyan : ReleaseUiComponents.Violet, active);
                ReleaseUiComponents.Icon(card, "CosmeticIcon", GeneratedUiAssets.CosmeticIcon,
                    new Vector2(0.04f, 0.19f), new Vector2(0.18f, 0.81f));
                ReleaseUiKit.TextBlock(card, "SkinName", CosmeticLabel(skinId), 32, TextAnchor.MiddleLeft,
                    new Vector2(0.22f, 0.43f), new Vector2(0.86f, 0.83f), ReleaseUiComponents.Text, FontStyle.Bold);
                ReleaseUiKit.TextBlock(card, "Selection", active ? "ВЫБРАН" : "ВЫБРАТЬ", 26, TextAnchor.MiddleLeft,
                    new Vector2(0.22f, 0.12f), new Vector2(0.86f, 0.43f),
                    active ? ReleaseUiComponents.Success : ReleaseUiComponents.Muted);
                Button choice = card.gameObject.AddComponent<Button>();
                ConfigureCardButton(choice, card.GetComponent<Image>(), !active, () => SelectCosmetic(captured));
                if (active) ReleaseUiComponents.Icon(card, "Selected", GeneratedUiAssets.CheckIcon,
                    new Vector2(0.87f, 0.30f), new Vector2(0.96f, 0.70f));
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
            RebuildLocalServices();
            _lastStoreProducts = null;
            _catalogLoading = true;
            ShowPanel("МАГАЗИН", "Загружаем цены…");
            RenderStore(null);
            AnalyticsLifecycle.Service?.Track(AnalyticsEventNames.StoreOpen, Params("surface", "home"));
            StoreService store = _store;

            try
            {
                IReadOnlyList<StoreProduct> products = await store.LoadCatalogAsync();
                if (!IsCurrentPanel(revision)) return;
                _catalogLoading = false;
                _lastStoreProducts = products;
                RenderStore(products);
            }
            catch (Exception error)
            {
                Debug.LogWarning($"Store unavailable: {error.Message}");
                if (!IsCurrentPanel(revision)) return;
                _catalogLoading = false;
                RenderStore(null, "Покупки пока недоступны. Твои скины и подсказки остаются с тобой.");
            }
        }

        private void RenderStore(IReadOnlyList<StoreProduct> products, string message = null)
        {
            ClearActions();
            bool available = products != null && products.Count > 0;
            _panelBody.text = !string.IsNullOrWhiteSpace(message) ? message
                : _catalogLoading ? "Загружаем цены…"
                : !available ? "Для покупки подключись к RuStore. Играть можно без сети."
                : $"Монеты: {_store.Save.Coins}    /    Подсказки: {_store.Save.Hints}";

            AddStoreSectionLabel("БЕЗ ПАУЗ НА РЕКЛАМУ", ReleaseUiComponents.Cyan);
            StoreProduct premium = ProductOrUnavailable(products, ProductIds.RemoveAds);
            AddStoreHeroProduct(premium, IsOwned(premium.Id));
            AddStoreSectionLabel("СКИНЫ ЛИНИИ", ReleaseUiComponents.Violet);
            AddStoreProductIfPresent(products, ProductIds.SkinNeon);
            AddStoreProductIfPresent(products, ProductIds.SkinRetro);
            AddStoreProductIfPresent(products, ProductIds.StarterPack);
            AddStoreSectionLabel("ПОДСКАЗКИ", ReleaseUiComponents.Gold);
            AddStoreProductIfPresent(products, ProductIds.Hints10);
            if (!available && !_catalogLoading) AddAction("ОБНОВИТЬ КАТАЛОГ", OpenStore, !_storeBusy);
            AddAction("ВОССТАНОВИТЬ ПОКУПКИ", RestorePurchases, !_storeBusy && !_catalogLoading);
            AddAction("МОИ СКИНЫ", OpenCosmetics);
        }

        private void AddStoreHeroProduct(StoreProduct product, bool owned) =>
            AddStoreProductCard(product, owned, true);

        private void AddStoreProductAction(StoreProduct product, bool owned) =>
            AddStoreProductCard(product, owned, false);

        private void AddStoreProductCard(StoreProduct product, bool owned, bool hero)
        {
            string productId = product.Id;
            // A missing SDK price is unavailable, never a made-up production offer.
            bool hasPrice = !string.IsNullOrWhiteSpace(product.PriceLabel);
            bool interactable = !_storeBusy && !_catalogLoading && hasPrice && !owned;
            Color accent = owned ? ReleaseUiComponents.Success
                : hero ? ReleaseUiComponents.Cyan : ReleaseUiComponents.Violet;
            Transform card = AddLayoutCard(hero ? "PremiumOffer" : "StoreProduct", hero ? 192 : 176, accent, hero);
            Button button = card.gameObject.AddComponent<Button>();
            ConfigureCardButton(button, card.GetComponent<Image>(), interactable, () => Purchase(productId));

            Image iconWell = ReleaseUiComponents.GlassCard(card, "ProductIconWell",
                new Vector2(0.030f, 0.19f), new Vector2(0.190f, 0.81f), accent, false);
            iconWell.color = new Color(accent.r, accent.g, accent.b, hero ? 0.16f : 0.10f);
            ReleaseUiComponents.Icon(iconWell.transform, "ProductIcon", ProductIcon(productId),
                new Vector2(0.12f, 0.12f), new Vector2(0.88f, 0.88f));
            if (hero)
            {
                Image badge = ReleaseUiComponents.GlassCard(card, "PremiumBadge",
                    new Vector2(0.225f, 0.76f), new Vector2(0.47f, 0.92f),
                    ReleaseUiComponents.Cyan, false);
                badge.color = new Color(ReleaseUiComponents.Cyan.r, ReleaseUiComponents.Cyan.g, ReleaseUiComponents.Cyan.b, 0.12f);
                ReleaseUiKit.TextBlock(badge.transform, "Label", "PREMIUM", 15, TextAnchor.MiddleCenter,
                    Vector2.zero, Vector2.one, ReleaseUiComponents.Cyan, FontStyle.Bold);
            }

            ReleaseUiKit.TextBlock(card, "ProductTitle", ProductLabel(productId), hero ? 34 : 32, TextAnchor.MiddleLeft,
                new Vector2(0.225f, hero ? 0.48f : 0.57f), new Vector2(0.72f, hero ? 0.76f : 0.89f),
                ReleaseUiComponents.Text, FontStyle.Bold);
            Text description = ReleaseUiKit.TextBlock(card, "ProductSubtitle", ProductSubtitle(productId), hero ? 23 : 24, TextAnchor.UpperLeft,
                new Vector2(0.225f, 0.12f), new Vector2(0.70f, hero ? 0.47f : 0.55f), ReleaseUiComponents.Muted);
            description.horizontalOverflow = HorizontalWrapMode.Wrap;

            string price = owned ? "КУПЛЕНО" : _catalogLoading ? "Загрузка…" : !hasPrice ? "Недоступно" : product.PriceLabel;
            Color priceAccent = owned ? ReleaseUiComponents.Success : hasPrice ? ReleaseUiComponents.Gold : ReleaseUiComponents.Muted;
            Image pricePill = ReleaseUiComponents.GlassCard(card, "ProductPricePill",
                new Vector2(0.73f, 0.26f), new Vector2(0.97f, 0.76f), priceAccent, false);
            pricePill.color = new Color(priceAccent.r, priceAccent.g, priceAccent.b, owned ? 0.13f : hasPrice ? 0.10f : 0.06f);
            ReleaseUiKit.TextBlock(pricePill.transform, "ProductPrice", price, owned || !hasPrice ? 22 : 28, TextAnchor.MiddleCenter,
                new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f), priceAccent, FontStyle.Bold);
            if (owned)
            {
                ReleaseUiComponents.Icon(pricePill.transform, "OwnedCheck", GeneratedUiAssets.CheckIcon,
                    new Vector2(0.36f, 0.64f), new Vector2(0.64f, 0.94f));
            }
        }

        private void AddStoreSectionLabel(string label, Color accent)
        {
            Transform root = AddLayoutRoot("StoreSection", 48);
            ReleaseUiKit.TextBlock(root, "Label", label, 26, TextAnchor.MiddleLeft,
                new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.98f), accent, FontStyle.Bold);
        }

        private void AddStoreProductIfPresent(IReadOnlyList<StoreProduct> products, string productId)
        {
            StoreProduct product = ProductOrUnavailable(products, productId);
            AddStoreProductAction(product, IsOwned(product.Id));
        }

        private static StoreProduct ProductOrUnavailable(IReadOnlyList<StoreProduct> products, string productId) =>
            FindProduct(products, productId) ?? new StoreProduct { Id = productId };

        private static StoreProduct FindProduct(IReadOnlyList<StoreProduct> products, string productId)
        {
            if (products == null || string.IsNullOrWhiteSpace(productId)) return null;
            for (int i = 0; i < products.Count; i++)
            {
                StoreProduct product = products[i];
                if (product != null && string.Equals(product.Id, productId, StringComparison.Ordinal)) return product;
            }
            return null;
        }

        private static string ProductIcon(string productId)
        {
            switch (productId)
            {
                case ProductIds.RemoveAds: return GeneratedUiAssets.NoAdsIcon;
                case ProductIds.Hints10: return GeneratedUiAssets.HintIcon;
                case ProductIds.StarterPack: return GeneratedUiAssets.StoreIcon;
                case ProductIds.SkinNeon: return GeneratedUiAssets.CosmeticIcon;
                case ProductIds.SkinRetro: return GeneratedUiAssets.ReplayIcon;
                default: return GeneratedUiAssets.StoreIcon;
            }
        }

        private static string ProductSubtitle(string id)
        {
            switch (id)
            {
                case ProductIds.RemoveAds: return "Без рекламы между раундами. Видео за награду — по желанию.";
                case ProductIds.StarterPack: return "Три скина и отключение рекламы между раундами";
                case ProductIds.SkinNeon: return "Яркий цвет твоего маршрута";
                case ProductIds.SkinRetro: return "Новый цвет для каждого касания";
                case ProductIds.Hints10: return "Ещё 10 показов маршрута";
                default: return "Покупка через RuStore";
            }
        }

        private async void Purchase(string productId)
        {
            StoreProduct product = FindProduct(_lastStoreProducts, productId);
            if (_storeBusy || product == null || string.IsNullOrWhiteSpace(product.PriceLabel) || IsOwned(productId)) return;
            int revision = _panelRevision;
            StoreService store = _store;
            _storeBusy = true;
            string feedback = "Не удалось завершить покупку. Попробуй ещё раз.";
            AnalyticsLifecycle.Service?.Track(AnalyticsEventNames.PurchaseStart, Params("product_id", productId));
            _panelBody.text = $"Открываем покупку: {ProductLabel(productId)}…";
            SetActionsInteractable(false);

            try
            {
                StorePurchaseResult result = await store.PurchaseAsync(productId);
                if (result?.Payment?.Outcome == PurchaseOutcome.Completed && result.Verified)
                {
                    AnalyticsLifecycle.Service?.Track(AnalyticsEventNames.PurchaseSuccess, Params("product_id", productId));
                    feedback = result.GrantApplied ? "Готово! Покупка доступна на этом устройстве." : "Эта покупка уже добавлена.";
                }
                else if (result?.Payment?.Outcome == PurchaseOutcome.Cancelled)
                {
                    AnalyticsLifecycle.Service?.Track(AnalyticsEventNames.PurchaseCancel, Params("product_id", productId));
                    feedback = "Покупка отменена. Можно выбрать что-нибудь позже.";
                }
                else
                {
                    AnalyticsLifecycle.Service?.Track(AnalyticsEventNames.PurchaseError, Params(
                        "product_id", productId, "error", result?.Verification?.ErrorMessage ?? result?.Payment?.ErrorMessage ?? "unknown"));
                    feedback = result?.Payment?.Outcome == PurchaseOutcome.Completed
                        ? "Покупка обрабатывается. Если товар не появился, восстанови покупки."
                        : "Покупка не завершена. Попробуй позже.";
                }
            }
            catch (Exception error)
            {
                AnalyticsLifecycle.Service?.Track(AnalyticsEventNames.PurchaseError, Params("product_id", productId, "error", error.Message));
            }
            finally
            {
                _storeBusy = false;
                if (IsCurrentPanel(revision))
                {
                    _save = store.Save;
                    // Rebuild the cards so owned/unavailable products remain disabled after an async operation.
                    RenderStore(_lastStoreProducts, feedback);
                }
            }
        }

        private async void RestorePurchases()
        {
            if (_storeBusy) return;
            int revision = _panelRevision;
            StoreService store = _store;
            _storeBusy = true;
            string feedback = "Не удалось восстановить покупки. Попробуй позже.";
            _panelBody.text = "Проверяем покупки RuStore…";
            SetActionsInteractable(false);
            try
            {
                int restored = await store.RestoreAsync();
                feedback = restored > 0 ? $"Готово! Восстановлено покупок: {restored}." : "Все доступные покупки уже восстановлены.";
            }
            catch (Exception error)
            {
                Debug.LogWarning($"Restore purchases failed: {error.Message}");
            }
            finally
            {
                _storeBusy = false;
                if (IsCurrentPanel(revision))
                {
                    _save = store.Save;
                    RenderStore(_lastStoreProducts, feedback);
                }
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
            canvas.sortingOrder = 80;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            _homeButtons = new GameObject("HomeMetaButtons", typeof(RectTransform));
            _homeButtons.transform.SetParent(canvasGo.transform, false);
            ReleaseUiKit.Stretch(_homeButtons.GetComponent<RectTransform>());
            CreateButton(_homeButtons.transform, "СТАТИСТИКА", new Vector2(0.52f, 0.035f), new Vector2(0.71f, 0.105f), OpenStatistics);
            CreateButton(_homeButtons.transform, "МАГАЗИН", new Vector2(0.72f, 0.035f), new Vector2(0.92f, 0.105f), OpenStore);

            _panel = new GameObject("MetaPanel", typeof(RectTransform), typeof(Image));
            _panel.transform.SetParent(canvasGo.transform, false);
            ReleaseUiKit.SetAnchors(_panel.GetComponent<RectTransform>(), new Vector2(0.025f, 0.025f), new Vector2(0.975f, 0.975f));
            Image panelImage = _panel.GetComponent<Image>();
            panelImage.sprite = ReleaseUiKit.Rounded;
            panelImage.type = Image.Type.Sliced;
            panelImage.color = new Color(0.006f, 0.018f, 0.045f, 0.997f);
            ReleaseUiComponents.Backdrop(_panel.transform, "MetaReleaseBackdrop");

            Outline outline = _panel.AddComponent<Outline>();
            outline.effectColor = new Color(ReleaseUiKit.Cyan.r, ReleaseUiKit.Cyan.g, ReleaseUiKit.Cyan.b, 0.13f);
            outline.effectDistance = new Vector2(2f, -2f);

            Shadow shadow = _panel.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.52f);
            shadow.effectDistance = new Vector2(0f, -12f);
            _panel.AddComponent<ReleasePanelMotion>();

            var releaseVisual = new GameObject("ReleaseVisual", typeof(RectTransform));
            releaseVisual.transform.SetParent(_panel.transform, false);
            ReleaseUiKit.Stretch(releaseVisual.GetComponent<RectTransform>());

            ReleaseUiKit.TextBlock(_panel.transform, "Kicker", "НЕ СБЕЙСЯ!", 19,
                TextAnchor.MiddleCenter, new Vector2(0.34f, 0.925f), new Vector2(0.66f, 0.962f),
                ReleaseUiComponents.Cyan, FontStyle.Bold);

            Transform titleIconRoot = ReleaseUiKit.Rect(_panel.transform, "GeneratedPanelIcon",
                new Vector2(0.195f, 0.855f), new Vector2(0.285f, 0.925f));
            _panelTitleIcon = titleIconRoot.gameObject.AddComponent<Image>();
            _panelTitleIcon.raycastTarget = false;
            _panelTitleIcon.gameObject.SetActive(false);

            _panelTitle = ReleaseUiKit.TextBlock(_panel.transform, "Title", string.Empty, 46,
                TextAnchor.MiddleCenter, new Vector2(0.15f, 0.845f), new Vector2(0.85f, 0.925f),
                ReleaseUiComponents.Text, FontStyle.Bold);
            ReleaseUiKit.AddTextShadow(_panelTitle, 0.42f, -3f);

            Button close = ReleaseUiComponents.SecondaryButton(_panel.transform, "Close", "←",
                new Vector2(0.055f, 0.865f), new Vector2(0.16f, 0.925f), ClosePanel, 34);
            close.gameObject.name = "ЗАКРЫТЬ";

            Image bodyCard = ReleaseUiKit.Panel(_panel.transform, "BodyCard",
                new Vector2(0.07f, 0.705f), new Vector2(0.93f, 0.820f),
                new Color(0.018f, 0.045f, 0.085f, 0.96f), ReleaseUiKit.Cyan, false);

            _panelBody = ReleaseUiKit.TextBlock(bodyCard.transform, "Body", string.Empty, 23,
                TextAnchor.MiddleLeft, new Vector2(0.055f, 0.10f), new Vector2(0.945f, 0.90f),
                ReleaseUiComponents.Muted);
            _panelBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            _panelBody.verticalOverflow = VerticalWrapMode.Overflow;
            _panelBody.lineSpacing = 1.14f;

            Image actionsCard = ReleaseUiKit.Panel(_panel.transform, "ActionsCard",
                new Vector2(0.07f, 0.075f), new Vector2(0.93f, 0.680f),
                new Color(0.018f, 0.030f, 0.064f, 0.96f), ReleaseUiKit.Violet, false);

            var viewport = new GameObject("ActionsViewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(actionsCard.transform, false);
            RectTransform viewportRect = viewport.GetComponent<RectTransform>();
            ReleaseUiKit.SetAnchors(viewportRect, new Vector2(0.018f, 0.020f), new Vector2(0.982f, 0.980f));

            var actions = new GameObject(
                "Actions",
                typeof(RectTransform),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            actions.transform.SetParent(viewport.transform, false);
            RectTransform actionsRect = actions.GetComponent<RectTransform>();
            actionsRect.anchorMin = new Vector2(0f, 1f);
            actionsRect.anchorMax = new Vector2(1f, 1f);
            actionsRect.pivot = new Vector2(0.5f, 1f);
            actionsRect.anchoredPosition = Vector2.zero;
            actionsRect.sizeDelta = Vector2.zero;

            var layout = actions.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 12, 18);
            layout.spacing = 12;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childAlignment = TextAnchor.UpperCenter;

            ContentSizeFitter fitter = actions.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _scroll = actionsCard.gameObject.AddComponent<ScrollRect>();
            _scroll.viewport = viewportRect;
            _scroll.content = actionsRect;
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.inertia = true;
            _scroll.decelerationRate = 0.12f;
            _scroll.scrollSensitivity = 42f;
            _scroll.movementType = ScrollRect.MovementType.Elastic;
            _scroll.elasticity = 0.10f;
            _actionsRoot = actions.transform;

            _panel.SetActive(false);
        }

        private void ShowPanel(string title, string body)
        {
            ClearActions();
            _panelTitle.text = title;
            ApplyPanelTitleIcon(title);
            _panelBody.text = body;
            _panel.SetActive(true);
            if (_scroll != null)
            {
                Canvas.ForceUpdateCanvases();
                _scroll.verticalNormalizedPosition = 1f;
            }
            SetHomeButtonsVisible(false);
        }

        private void ApplyPanelTitleIcon(string title)
        {
            if (_panelTitleIcon == null) return;

            string asset = string.Equals(title, "МОЯ СТАТИСТИКА", StringComparison.Ordinal)
                ? GeneratedUiAssets.StatisticsIcon
                : string.Equals(title, "МАГАЗИН", StringComparison.Ordinal)
                    ? GeneratedUiAssets.StoreIcon
                    : string.Equals(title, "НАСТРОЙКИ", StringComparison.Ordinal)
                        ? GeneratedUiAssets.SettingsIcon
                        : string.Equals(title, "КОСМЕТИКА", StringComparison.Ordinal)
                            ? GeneratedUiAssets.CosmeticIcon
                            : null;

            bool visible = GeneratedUiAssets.TryApply(_panelTitleIcon, asset);
            _panelTitleIcon.gameObject.SetActive(visible);
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
            if (_scroll != null)
            {
                _scroll.StopMovement();
                _scroll.verticalNormalizedPosition = 1f;
            }
        }

        private void AddSettingToggleRow(
            string glyph,
            string label,
            string description,
            bool enabled,
            UnityEngine.Events.UnityAction action)
        {
            var go = new GameObject("SettingToggle", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(_actionsRoot, false);

            Image image = go.GetComponent<Image>();
            image.sprite = ReleaseUiKit.Rounded;
            image.type = Image.Type.Sliced;
            image.color = new Color(0.020f, 0.055f, 0.100f, 0.97f);

            Outline outline = go.AddComponent<Outline>();
            Color accent = enabled ? ReleaseUiComponents.Cyan : ReleaseUiComponents.Muted;
            outline.effectColor = new Color(accent.r, accent.g, accent.b, enabled ? 0.26f : 0.12f);
            outline.effectDistance = new Vector2(2f, -2f);

            LayoutElement element = go.GetComponent<LayoutElement>();
            element.preferredHeight = 78;
            element.minHeight = 72;

            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;
            if (action != null) button.onClick.AddListener(action);
            ColorBlock colors = button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = ReleaseUiKit.Lighten(image.color, 0.05f);
            colors.pressedColor = ReleaseUiKit.Darken(image.color, 0.06f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            Image settingWell = ReleaseUiComponents.GlassCard(go.transform, "SettingIconWell",
                new Vector2(0.035f, 0.14f), new Vector2(0.175f, 0.86f), accent, false);
            settingWell.color = new Color(accent.r, accent.g, accent.b, 0.09f);
            ReleaseUiComponents.Icon(settingWell.transform, "SettingIcon", glyph,
                new Vector2(0.13f, 0.13f), new Vector2(0.87f, 0.87f));
            ReleaseUiKit.TextBlock(go.transform, "Label", label, 18, TextAnchor.MiddleLeft,
                new Vector2(0.18f, 0.50f), new Vector2(0.67f, 0.88f), ReleaseUiComponents.Text, FontStyle.Bold);
            ReleaseUiKit.TextBlock(go.transform, "Description", description, 14, TextAnchor.MiddleLeft,
                new Vector2(0.18f, 0.12f), new Vector2(0.70f, 0.50f), ReleaseUiComponents.Muted);

            ReleaseUiKit.TextBlock(go.transform, "StateLabel", enabled ? "ВКЛ" : "ВЫКЛ", 13,
                TextAnchor.MiddleRight, new Vector2(0.67f, 0.24f), new Vector2(0.775f, 0.76f),
                enabled ? ReleaseUiComponents.Cyan : ReleaseUiComponents.Muted, FontStyle.Bold);

            Image track = ReleaseUiComponents.GlassCard(go.transform, "Switch",
                new Vector2(0.79f, 0.25f), new Vector2(0.94f, 0.75f),
                enabled ? ReleaseUiComponents.Cyan : ReleaseUiComponents.Muted, false);
            track.color = enabled
                ? new Color(ReleaseUiComponents.Cyan.r, ReleaseUiComponents.Cyan.g, ReleaseUiComponents.Cyan.b, 0.26f)
                : new Color(0.07f, 0.095f, 0.14f, 0.96f);

            Transform thumbRoot = ReleaseUiKit.Rect(track.transform, "Thumb",
                enabled ? new Vector2(0.56f, 0.13f) : new Vector2(0.08f, 0.13f),
                enabled ? new Vector2(0.92f, 0.87f) : new Vector2(0.44f, 0.87f));
            Image thumb = thumbRoot.gameObject.AddComponent<Image>();
            thumb.sprite = ReleaseUiKit.Circle;
            thumb.color = enabled ? ReleaseUiComponents.Cyan : new Color(0.62f, 0.69f, 0.79f, 1f);
            thumb.raycastTarget = false;
        }

        private void AddInfoRow(string glyph, string label, string value, Color accent)
        {
            var go = new GameObject("InfoRow", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(_actionsRoot, false);

            Image image = go.GetComponent<Image>();
            image.sprite = ReleaseUiKit.Rounded;
            image.type = Image.Type.Sliced;
            image.color = new Color(0.020f, 0.055f, 0.100f, 0.96f);

            Outline outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(accent.r, accent.g, accent.b, 0.18f);
            outline.effectDistance = new Vector2(2f, -2f);

            LayoutElement element = go.GetComponent<LayoutElement>();
            element.preferredHeight = 72;
            element.minHeight = 66;

            Image infoWell = ReleaseUiComponents.GlassCard(go.transform, "InfoIconWell",
                new Vector2(0.035f, 0.14f), new Vector2(0.175f, 0.86f), accent, false);
            infoWell.color = new Color(accent.r, accent.g, accent.b, 0.09f);
            ReleaseUiComponents.Icon(infoWell.transform, "InfoIcon", glyph,
                new Vector2(0.13f, 0.13f), new Vector2(0.87f, 0.87f));
            ReleaseUiKit.TextBlock(go.transform, "Label", label, 16, TextAnchor.MiddleLeft,
                new Vector2(0.18f, 0.50f), new Vector2(0.56f, 0.88f), ReleaseUiComponents.Muted, FontStyle.Bold);
            ReleaseUiKit.TextBlock(go.transform, "Value", value, 22, TextAnchor.MiddleRight,
                new Vector2(0.58f, 0.12f), new Vector2(0.94f, 0.78f), ReleaseUiComponents.Text, FontStyle.Bold);
        }

        private void AddAction(string label, UnityEngine.Events.UnityAction action, bool interactable = true)
        {
            Button button = CreateLayoutButton(_actionsRoot, label, action);
            button.interactable = interactable;
        }

        private Transform AddLayoutRoot(string name, float preferredHeight)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(_actionsRoot, false);
            LayoutElement element = go.GetComponent<LayoutElement>();
            element.preferredHeight = preferredHeight;
            element.minHeight = Mathf.Max(48f, preferredHeight - 12f);
            return go.transform;
        }

        private Transform AddLayoutCard(string name, float preferredHeight, Color accent, bool strong)
        {
            Transform root = AddLayoutRoot(name, preferredHeight);
            Image image = root.gameObject.AddComponent<Image>();
            image.sprite = ReleaseUiKit.Rounded;
            image.type = Image.Type.Sliced;
            image.color = strong
                ? new Color(0.035f, 0.085f, 0.145f, 0.99f)
                : new Color(0.020f, 0.055f, 0.100f, 0.97f);

            Outline outline = root.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(accent.r, accent.g, accent.b, strong ? 0.42f : 0.22f);
            outline.effectDistance = strong ? new Vector2(3f, -3f) : new Vector2(2f, -2f);
            return root;
        }

        private void AddMetricPair(string leftLabel, string leftValue, string rightLabel, string rightValue)
        {
            Transform card = AddLayoutCard("MetricPair", 126f, ReleaseUiComponents.Cyan, false);
            ReleaseUiKit.TextBlock(card, "LeftLabel", leftLabel, 17, TextAnchor.MiddleLeft,
                new Vector2(0.055f, 0.54f), new Vector2(0.47f, 0.86f), ReleaseUiComponents.Muted, FontStyle.Bold);
            ReleaseUiKit.TextBlock(card, "LeftValue", leftValue, 30, TextAnchor.MiddleLeft,
                new Vector2(0.055f, 0.13f), new Vector2(0.47f, 0.56f), ReleaseUiComponents.Text, FontStyle.Bold);
            ReleaseUiKit.TextBlock(card, "RightLabel", rightLabel, 17, TextAnchor.MiddleRight,
                new Vector2(0.53f, 0.54f), new Vector2(0.945f, 0.86f), ReleaseUiComponents.Muted, FontStyle.Bold);
            ReleaseUiKit.TextBlock(card, "RightValue", rightValue, 30, TextAnchor.MiddleRight,
                new Vector2(0.53f, 0.13f), new Vector2(0.945f, 0.56f), ReleaseUiComponents.Text, FontStyle.Bold);
        }

        private static void ConfigureCardButton(
            Button button,
            Image image,
            bool interactable,
            UnityEngine.Events.UnityAction action)
        {
            if (button == null || image == null) return;
            button.targetGraphic = image;
            button.interactable = interactable;
            if (interactable && action != null) button.onClick.AddListener(action);

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor = new Color(0.72f, 0.82f, 0.95f, 1f);
            colors.disabledColor = new Color(0.60f, 0.65f, 0.75f, 0.72f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
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

            Image image = go.GetComponent<Image>();
            image.sprite = ReleaseUiKit.Rounded;
            image.type = Image.Type.Sliced;
            image.color = new Color(0.025f, 0.070f, 0.120f, 0.98f);

            LayoutElement element = go.GetComponent<LayoutElement>();
            element.preferredHeight = 72;
            element.minHeight = 64;

            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            ColorBlock colors = button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = ReleaseUiKit.Lighten(image.color, 0.05f);
            colors.pressedColor = ReleaseUiKit.Darken(image.color, 0.06f);
            colors.disabledColor = new Color(0.07f, 0.08f, 0.12f, 0.68f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            Text text = ReleaseUiKit.TextBlock(go.transform, "Label", label, 25,
                TextAnchor.MiddleLeft, new Vector2(0.055f, 0f), new Vector2(0.945f, 1f),
                ReleaseUiKit.Text, FontStyle.Bold);
            text.raycastTarget = false;

            return button;
        }

        private static Button CreateButton(Transform parent, string label, Vector2 min, Vector2 max, UnityEngine.Events.UnityAction action)
        {
            return ReleaseUiKit.Button(parent, label, label, min, max,
                ReleaseUiKit.SurfaceRaised, ReleaseUiKit.Text, 24, action);
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
