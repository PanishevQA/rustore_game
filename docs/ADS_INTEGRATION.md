# Advertising integration — offline-first MVP

The game does **not** need a developer-operated backend for advertising. The current provider adapter targets **Yandex Mobile Ads Unity 8.4.0** and stays behind `IAdService`.

Release target rechecked against the official Yandex Mobile Ads Unity changelog on **2026-09-18**: plugin **8.4.0** was released on 2026-09-09 and is the current Unity plugin baseline. The target is mirrored by `YandexMobileAdsSettings.VerifiedPluginVersion` / `LastVerifiedUtc` so CI can detect stale release documentation.

## Current code

- `Game.Monetization/YandexMobileAdsService.cs` — provider adapter.
- `AdRuntimeCoordinator` — interstitials only at safe Home transitions.
- `RewardedHomeOverlay` — opt-in rewarded placement: one completed rewarded ad grants one local hint.
- `InterstitialController` — Remote Config/default frequency cap and `remove_ads` entitlement checks.

If the Yandex plugin or production block IDs are missing, ads simply remain unavailable. Gameplay still works.

## Import before production

1. Download the official `yandex-mobileads-lite-8.4.0.unitypackage` from Yandex Mobile Ads documentation/repository.
2. Import it into the Unity project.
3. Let External Dependency Manager resolve Android dependencies.
4. Keep **Custom Main Gradle Template** and **Custom Gradle Properties Template** enabled. The production preflight verifies both serialized Player Settings flags and requires `Assets/Plugins/Android/mainTemplate.gradle` plus `gradleTemplate.properties` to exist.
5. Add Android scripting define symbol `YANDEX_MOBILE_ADS`.
6. Replace empty values in `YandexMobileAdsSettings.RewardedUnitId` and `InterstitialUnitId` with real `R-M-...` IDs from Yandex Advertising Network.
7. Never ship `demo-rewarded-yandex`, `demo-interstitial-yandex`, or any other demo block ID in release.
8. Run `Tools → НЕ СБЕЙСЯ! → Validate Production Release`.

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
