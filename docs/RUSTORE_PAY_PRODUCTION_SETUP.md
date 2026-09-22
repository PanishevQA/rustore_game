# RuStore Pay production setup

Last checked against the official RuStore Unity Pay documentation: 2026-09-21.

The repository contains the production RuStore application id in the official `PayClientSettings.asset` (`consoleApplicationId = 2063758837`) and keeps the dedicated Pay deeplink scheme there. The Android manifest references the resources generated from that asset; it does not hardcode those generated values. Development builds remain local/offline-first.

## Before the first production build

1. Open the project in the pinned Unity version.
2. Open `Window → RuStoreSDK → Settings → PayClient`.
3. Create or migrate `PayClientSettings.asset` when prompted by the current RuStore Pay plugin.
4. Set `consoleApplicationId` to the numeric application id from the RuStore Console.
5. Set a dedicated Pay `deeplinkScheme`. Do not reuse `nesbeisya`, which belongs to the gameplay friend-challenge deeplink.
6. Keep Android Application Entry Point set to `Activity / com.unity3d.player.UnityPlayerActivity`. Do not switch to GameActivity.
7. In Pay Client settings select the current official deeplink activity (Default unless a deliberately tested custom activity is required).
8. `AndroidManifest.xml` is kept in the same form produced by the current Pay 11.1.0 manifest contract; before the final production build, run `Patch Manifest`, then `Verify Manifest` once in the Pay Client settings window as a physical release-machine cross-check.
9. Confirm the resulting manifest contains the Pay deeplink activity and the generated-resource meta-data references:
   - `console_app_id_value`
   - `internal_config_key`
   - `sdk_pay_scheme_value`
   - `@string/rustore_PayClientSettings_deeplinkScheme`
10. Run `Tools → НЕ СБЕЙСЯ! → Validate RuStore Pay Release Contract`.
11. Run `Tools → НЕ СБЕЙСЯ! → Validate Production Release`.

The gameplay challenge deeplink `nesbeisya://challenge` must remain present independently of the RuStore Pay deeplink configuration.

## Why this is enforced

RuStore Pay needs `UnityPlayerActivity` as the Unity application entry point and uses a dedicated deeplink activity/configuration to process payment return intents. The Pay Client settings asset is the source for the generated Android resources; production identifiers must not be hardcoded into the repository manifest.

Official reference: `https://www.rustore.ru/help/sdk/pay/unity`
