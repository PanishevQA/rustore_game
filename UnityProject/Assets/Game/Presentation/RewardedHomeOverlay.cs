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
            if (_label == null || _rewardLabel == null || _balanceLabel == null || _iconLabel == null) return;
            SaveData save = _saveRepository.Load();
            bool feedback = Time.unscaledTime < _feedbackUntil;
            _label.text = feedback ? "ПОДСКАЗКА ПОЛУЧЕНА" : "СМОТРЕТЬ РЕКЛАМУ";
            _rewardLabel.text = feedback ? "Награда добавлена" : "+1 ПОДСКАЗКА";
            _balanceLabel.text = $"У ВАС {save.Hints}";
            _iconLabel.text = feedback ? "OK" : "AD";
            _iconLabel.color = feedback ? ReleaseUiKit.Green : ReleaseUiKit.Cyan;
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

            _button = ReleaseUiComponents.SecondaryButton(
                _canvas.transform,
                "RewardedHintCard",
                string.Empty,
                new Vector2(0.07f, 0.072f),
                new Vector2(0.93f, 0.150f),
                ClaimRewardedHint,
                18);

            Text placeholder = _button.GetComponentInChildren<Text>(true);
            if (placeholder != null) placeholder.gameObject.SetActive(false);

            Image iconWell = ReleaseUiKit.Panel(
                _button.transform,
                "RewardedIconWell",
                new Vector2(0.035f, 0.16f),
                new Vector2(0.155f, 0.84f),
                new Color(ReleaseUiKit.Cyan.r, ReleaseUiKit.Cyan.g, ReleaseUiKit.Cyan.b, 0.12f),
                ReleaseUiKit.Cyan,
                true);
            iconWell.raycastTarget = false;
            _iconLabel = ReleaseUiKit.TextBlock(iconWell.transform, "RewardedIcon", "AD", 20,
                TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, ReleaseUiKit.Cyan, FontStyle.Bold);

            _label = ReleaseUiKit.TextBlock(_button.transform, "RewardedTitle", "СМОТРЕТЬ РЕКЛАМУ", 19,
                TextAnchor.LowerLeft, new Vector2(0.19f, 0.46f), new Vector2(0.66f, 0.86f),
                ReleaseUiKit.Text, FontStyle.Bold);
            _rewardLabel = ReleaseUiKit.TextBlock(_button.transform, "RewardedReward", "+1 ПОДСКАЗКА", 16,
                TextAnchor.UpperLeft, new Vector2(0.19f, 0.14f), new Vector2(0.66f, 0.50f),
                ReleaseUiKit.Green, FontStyle.Bold);
            _balanceLabel = ReleaseUiKit.TextBlock(_button.transform, "RewardedBalance", "У ВАС 0", 16,
                TextAnchor.MiddleRight, new Vector2(0.67f, 0.18f), new Vector2(0.95f, 0.82f),
                ReleaseUiComponents.Muted, FontStyle.Bold);

            Outline outline = _button.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(ReleaseUiKit.Green.r, ReleaseUiKit.Green.g, ReleaseUiKit.Green.b, 0.22f);
            outline.effectDistance = new Vector2(2f, -2f);
            RefreshLabel();
        }

        private void SetVisible(bool visible)
        {
            if (_canvas != null && _canvas.activeSelf != visible) _canvas.SetActive(visible);
        }
    }
}
