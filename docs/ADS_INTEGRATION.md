# Advertising integration — offline-first MVP

The game does **not** need a developer-operated backend for advertising. The current provider adapter targets **Yandex Mobile Ads Unity 8.4.0** and stays behind `IAdService`.

Release target rechecked against the official Yandex Mobile Ads Unity changelog on **2026-09-23**: plugin **8.4.0** was released on 2026-09-09 and is the current Unity plugin baseline. The target is mirrored by `YandexMobileAdsSettings.VerifiedPluginVersion` / `LastVerifiedUtc` so CI can detect stale release documentation.

## Current code

- `Game.Monetization/YandexMobileAdsService.cs` — provider adapter.
- `AdRuntimeCoordinator` — interstitials only at safe Home transitions.
- `RewardedHomeOverlay` — opt-in rewarded placement: one completed rewarded ad grants one local hint.
- `InterstitialController` — Remote Config/default frequency cap and `remove_ads` entitlement checks.

The official plugin is now repository-managed through `Packages/manifest.json` and pinned to the upstream Git tag `#8.4.0` at `mobileads-sdk`. `Game.Monetization.asmdef` references `YandexMobileAds` and enables `YANDEX_MOBILE_ADS` automatically only for the verified `[8.4.0,8.5.0)` package line. Gameplay still stays isolated behind `IAdService`.

## Production setup

1. Open the project and let Unity Package Manager resolve the pinned Yandex Mobile Ads Unity **8.4.0** package from `Packages/manifest.json`.
2. External Dependency Manager is also pinned (Google upstream tag `v1.2.188`). The production AAB entrypoint runs synchronous forced resolution automatically after Android becomes the active build target. A manual **Force Resolve** remains available only for diagnostics.
3. `AndroidDependencyConfigurator` creates **Custom Main Gradle Template**, **Custom Gradle Properties Template**, and **Custom Gradle Settings Template** from the exact installed Unity editor template set and enables the corresponding Player Settings flags. This avoids committing stale Gradle templates from another Unity patch.
4. Replace empty values in `YandexMobileAdsSettings.RewardedUnitId` and `InterstitialUnitId` with real `R-M-...` IDs from Yandex Advertising Network.
5. Never ship `demo-rewarded-yandex`, `demo-interstitial-yandex`, or any other demo block ID in release.
6. For diagnostics you can run `Tools → НЕ СБЕЙСЯ! → Prepare Android Dependency Templates` and `Force Resolve Android Dependencies`. The normal `Production Android AAB` command performs both automatically and fails if resolution is incomplete.
7. Build a signed Android release and verify initialization/device callbacks with `adb logcat`.

## Behaviour rules

- Rewarded is always user-initiated.
- The reward is granted only after Yandex fires `OnRewarded`.
- Interstitial is never shown while a route is visible, while the player draws, on the result/share surface, or while the store/meta panel is open.
- `remove_ads` and `starter_pack` suppress interstitials.
- Interstitial frequency is controlled by `interstitial_min_rounds` and `interstitial_cooldown_sec` defaults/config.
- Failed loads do not loop aggressively; the adapter retries by normal preload after a completed/failed show path.

## Test checklist

- Rewarded: close before earning → no hint.
- Rewarded: complete ad → exactly +1 hint.
- Rewarded: app background/foreground during ad → no duplicate reward.
- Interstitial: never appears during Drawing/Showing/Result/store/share.
- Interstitial: frequency cap works across multiple completed rounds in one process.
- `remove_ads` blocks interstitials.
- Test on a signed Android build with the exact production package name before RuStore submission.
- After dependency resolution and initialization, verify device logs with `adb logcat | grep 'Yandex Ads' -i` and confirm the SDK reports successful integration/initialization.
