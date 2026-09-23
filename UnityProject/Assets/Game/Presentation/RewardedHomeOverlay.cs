using System;
using System.Collections.Generic;
using DontGetSidetracked.Analytics;
using DontGetSidetracked.Core;
using DontGetSidetracked.Platform.RuStore;
using DontGetSidetracked.Services;
using UnityEngine;
using UnityEngine.UI;

namespace DontGetSidetracked.Presentation
{
    /// <summary>
    /// Small opt-in rewarded surface for the offline MVP. A completed reward grants one local hint.
    /// It is visible only on unobstructed Home and never interrupts an active game session or menu.
    /// </summary>
    public sealed class RewardedHomeOverlay : MonoBehaviour
    {
        private JsonFileSaveRepository _saveRepository;
        private IRemoteConfigService _config;
        private GameBootstrap _bootstrap;
        private GameObject _canvas;
        private Button _button;
        private Text _label;
        private Text _rewardLabel;
        private Text _balanceLabel;
        private Text _iconLabel;
        private bool _inProgress;
        private float _nextPoll;
        private float _feedbackUntil;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (FindFirstObjectByType<RewardedHomeOverlay>() != null) return;
            var root = new GameObject("RewardedHomeOverlay");
            DontDestroyOnLoad(root);
            root.AddComponent<RewardedHomeOverlay>();
        }

        private void Awake()
        {
            _saveRepository = new JsonFileSaveRepository();
            _config = RuStoreRemoteConfigRuntime.Service;
            BuildUi();
            ResolveBootstrap();
            SetVisible(false);
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextPoll) return;
            _nextPoll = Time.unscaledTime + 0.25f;
            if (_bootstrap == null) ResolveBootstrap();

            IAdService ads = AdRuntimeCoordinator.Ads;
            bool visible = !_inProgress &&
                           _config.GetBool("rewarded_enabled", true) &&
                           ads != null &&
                           ads.IsRewardedReady &&
                           IsSafeHome() &&
                           !IsAnyHomeOverlayOpen();
            SetVisible(visible);
            if (visible) RefreshLabel();
        }

        private async void ClaimRewardedHint()
        {
            if (_inProgress || IsAnyHomeOverlayOpen()) return;
            IAdService ads = AdRuntimeCoordinator.Ads;
            if (ads == null || !ads.IsRewardedReady || !IsSafeHome()) return;

            _inProgress = true;
            _button.interactable = false;
            AnalyticsLifecycle.Service?.Track(AnalyticsEventNames.RewardedOffer,
                new Dictionary<string, object> { ["placement"] = "free_hint_home" });
            AnalyticsLifecycle.Service?.Track(AnalyticsEventNames.RewardedStart,
                new Dictionary<string, object> { ["placement"] = "free_hint_home" });

            try
            {
                bool rewarded = await ads.ShowRewardedAsync(RewardPlacement.FreeHint);
                if (rewarded)
                {
                    SaveData save = _saveRepository.Load();
                    save.Hints++;
                    _saveRepository.Save(save);
                    _feedbackUntil = Time.unscaledTime + 1.4f;
                    AnalyticsLifecycle.Service?.Track(AnalyticsEventNames.RewardedComplete,
                        new Dictionary<string, object>
                        {
                            ["placement"] = "free_hint_home",
                            ["reward"] = "hint",
                            ["hints"] = save.Hints
                        });
                }
            }
            catch (Exception error)
            {
                Debug.LogWarning($"Rewarded ad unavailable: {error.Message}");
            }
            finally
            {
                _inProgress = false;
                if (_button != null) _button.interactable = true;
                RefreshLabel();
            }
        }

        private void RefreshLabel()
        {
            if (_label == null || _rewardLabel == null || _balanceLabel == null) return;
            SaveData save = _saveRepository.Load();
            bool feedback = Time.unscaledTime < _feedbackUntil;
            _label.text = feedback ? "ПОДСКАЗКА ПОЛУЧЕНА" : "СМОТРИ РЕКЛАМУ →";
            _rewardLabel.text = feedback ? "Награда добавлена" : "ПОЛУЧИ ПОДСКАЗКУ!";
            _balanceLabel.text = "›";
            if (_iconLabel != null)
            {
                _iconLabel.text = feedback ? "✓" : string.Empty;
                _iconLabel.color = feedback ? ReleaseUiKit.Green : ReleaseUiKit.Violet;
            }
        }

        private bool IsSafeHome() =>
            _bootstrap != null && GameBootstrapRuntimeBridge.IsPlainHome(_bootstrap);

        private static bool IsAnyHomeOverlayOpen()
        {
            DailyIntroCoordinator dailyIntro = FindFirstObjectByType<DailyIntroCoordinator>();
            if (dailyIntro != null && dailyIntro.IsOpen) return true;

            MetaMenuOverlay meta = FindFirstObjectByType<MetaMenuOverlay>();
            if (meta != null && meta.IsPanelOpen) return true;

            TrainingMenuCoordinator training = FindFirstObjectByType<TrainingMenuCoordinator>();
            if (training != null && training.IsOpen) return true;

            CampaignLevelMenuOverlay campaign = FindFirstObjectByType<CampaignLevelMenuOverlay>();
            if (campaign != null && campaign.IsOpen) return true;

            ReferralOfferCoordinator referral = FindFirstObjectByType<ReferralOfferCoordinator>();
            if (referral != null && referral.IsVisible) return true;

            RuntimePlatformCoordinator platform = FindFirstObjectByType<RuntimePlatformCoordinator>();
            if (platform != null && platform.IsNotificationPromptOpen) return true;

            return GameObject.Find("MandatoryUpdateCanvas") != null;
        }

        private void ResolveBootstrap()
        {
            _bootstrap = FindFirstObjectByType<GameBootstrap>();
        }

        private void BuildUi()
        {
            _canvas = new GameObject("RewardedCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvas.transform.SetParent(transform, false);
            Canvas canvas = _canvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 35;
            CanvasScaler scaler = _canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            var releaseVisual = new GameObject("ReleaseVisual", typeof(RectTransform));
            releaseVisual.transform.SetParent(_canvas.transform, false);
            ReleaseUiKit.Stretch(releaseVisual.GetComponent<RectTransform>());

            Image surface = ReleaseUiKit.Panel(
                _canvas.transform,
                "RewardedHintCard",
                new Vector2(0.055f, 0.062f),
                new Vector2(0.945f, 0.145f),
                new Color(0.070f, 0.030f, 0.145f, 0.985f),
                ReleaseUiKit.Violet,
                true);
            _button = surface.gameObject.AddComponent<Button>();
            _button.targetGraphic = surface;
            _button.onClick.AddListener(ClaimRewardedHint);

            Shadow cardGlow = surface.gameObject.AddComponent<Shadow>();
            cardGlow.effectColor = new Color(ReleaseUiKit.Violet.r, ReleaseUiKit.Violet.g, ReleaseUiKit.Violet.b, 0.28f);
            cardGlow.effectDistance = new Vector2(0f, -5f);
            cardGlow.useGraphicAlpha = true;

            Image iconWell = ReleaseUiKit.Panel(
                surface.transform,
                "RewardedIconWell",
                new Vector2(0.030f, 0.14f),
                new Vector2(0.165f, 0.86f),
                new Color(0.33f, 0.16f, 0.62f, 0.62f),
                ReleaseUiKit.Violet,
                true);
            iconWell.raycastTarget = false;
            BuildGiftIcon(iconWell.transform);
            _iconLabel = ReleaseUiKit.TextBlock(iconWell.transform, "RewardedFeedback", string.Empty, 20,
                TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, ReleaseUiKit.Green, FontStyle.Bold);

            _label = ReleaseUiKit.TextBlock(surface.transform, "RewardedTitle", "СМОТРИ РЕКЛАМУ →", 20,
                TextAnchor.LowerLeft, new Vector2(0.195f, 0.48f), new Vector2(0.73f, 0.86f),
                ReleaseUiKit.Text, FontStyle.Bold);
            _rewardLabel = ReleaseUiKit.TextBlock(surface.transform, "RewardedReward", "ПОЛУЧИ ПОДСКАЗКУ!", 17,
                TextAnchor.UpperLeft, new Vector2(0.195f, 0.14f), new Vector2(0.73f, 0.52f),
                ReleaseUiKit.Green, FontStyle.Bold);
            _balanceLabel = ReleaseUiKit.TextBlock(surface.transform, "RewardedArrow", "›", 38,
                TextAnchor.MiddleCenter, new Vector2(0.84f, 0.12f), new Vector2(0.95f, 0.88f),
                new Color(0.86f, 0.55f, 1f, 1f), FontStyle.Bold);

            RefreshLabel();
        }

        private static void BuildGiftIcon(Transform parent)
        {
            Image box = ReleaseUiKit.Panel(parent, "GiftBox",
                new Vector2(0.22f, 0.20f), new Vector2(0.78f, 0.62f),
                new Color(0.48f, 0.20f, 0.86f, 1f), ReleaseUiKit.Violet, false);
            box.raycastTarget = false;

            Transform lidRoot = ReleaseUiKit.Rect(parent, "GiftLid",
                new Vector2(0.17f, 0.57f), new Vector2(0.83f, 0.73f));
            Image lid = lidRoot.gameObject.AddComponent<Image>();
            lid.sprite = ReleaseUiKit.Rounded;
            lid.type = Image.Type.Sliced;
            lid.color = new Color(0.66f, 0.31f, 1f, 1f);
            lid.raycastTarget = false;

            Transform ribbonVRoot = ReleaseUiKit.Rect(parent, "GiftRibbonV",
                new Vector2(0.46f, 0.20f), new Vector2(0.54f, 0.73f));
            Image ribbonV = ribbonVRoot.gameObject.AddComponent<Image>();
            ribbonV.color = new Color(0.18f, 0.90f, 1f, 1f);
            ribbonV.raycastTarget = false;

            Transform ribbonHRoot = ReleaseUiKit.Rect(parent, "GiftRibbonH",
                new Vector2(0.22f, 0.43f), new Vector2(0.78f, 0.51f));
            Image ribbonH = ribbonHRoot.gameObject.AddComponent<Image>();
            ribbonH.color = new Color(0.18f, 0.90f, 1f, 1f);
            ribbonH.raycastTarget = false;

            Image bowLeft = ReleaseUiKit.Panel(parent, "GiftBowLeft",
                new Vector2(0.30f, 0.70f), new Vector2(0.49f, 0.88f),
                new Color(0.66f, 0.31f, 1f, 1f), ReleaseUiKit.Violet, false);
            bowLeft.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 28f);
            bowLeft.raycastTarget = false;

            Image bowRight = ReleaseUiKit.Panel(parent, "GiftBowRight",
                new Vector2(0.51f, 0.70f), new Vector2(0.70f, 0.88f),
                new Color(0.66f, 0.31f, 1f, 1f), ReleaseUiKit.Violet, false);
            bowRight.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -28f);
            bowRight.raycastTarget = false;
        }

        private void SetVisible(bool visible)
        {
            if (_canvas != null && _canvas.activeSelf != visible) _canvas.SetActive(visible);
        }
    }
}
