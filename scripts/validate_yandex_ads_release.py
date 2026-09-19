#!/usr/bin/env python3
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
SERVICE = ROOT / "UnityProject/Assets/Game/Monetization/YandexMobileAdsService.cs"
VALIDATOR = ROOT / "UnityProject/Assets/Game/Editor/ProductionReleaseValidator.cs"
ASMDEF = ROOT / "UnityProject/Assets/Game/Monetization/Game.Monetization.asmdef"
MANIFEST = ROOT / "UnityProject/Packages/manifest.json"
PLACEHOLDER_VALIDATOR = ROOT / "UnityProject/Assets/Game/Editor/ProductionPlaceholderValidator.cs"
DOC = ROOT / "docs/ADS_INTEGRATION.md"
CHECKLIST = ROOT / "docs/RUSTORE_RELEASE_CHECKLIST.md"

EXPECTED_VERSION = "8.4.0"
EXPECTED_VERIFIED_DATE = "2026-09-18"
errors: list[str] = []


def read(path: Path) -> str:
    try:
        return path.read_text(encoding="utf-8")
    except FileNotFoundError:
        errors.append(f"Missing required file: {path.relative_to(ROOT)}")
        return ""


def require(text: str, needle: str, message: str) -> None:
    if needle not in text:
        errors.append(message)


def const(text: str, name: str) -> str | None:
    match = re.search(rf'public const string {re.escape(name)}\s*=\s*"([^"]+)";', text)
    return match.group(1) if match else None


def main() -> int:
    service = read(SERVICE)
    validator = read(VALIDATOR)
    asmdef = read(ASMDEF)
    manifest = read(MANIFEST)
    placeholder_validator = read(PLACEHOLDER_VALIDATOR)
    doc = read(DOC)
    checklist = read(CHECKLIST)

    if const(service, "VerifiedPluginVersion") != EXPECTED_VERSION:
        errors.append(f"Yandex verified plugin target must stay pinned to {EXPECTED_VERSION}.")
    if const(service, "LastVerifiedUtc") != EXPECTED_VERIFIED_DATE:
        errors.append(f"Yandex plugin verification date must be {EXPECTED_VERIFIED_DATE}.")

    require(service, "RewardedAdLoader", "Rewarded integration must use the SDK 8 loader API.")
    require(service, "InterstitialAdLoader", "Interstitial integration must use the SDK 8 loader API.")
    require(service, "new AdRequest(_rewardedUnitId)", "Rewarded integration must use the SDK 8 AdRequest constructor.")
    require(service, "new AdRequest(_interstitialUnitId)", "Interstitial integration must use the SDK 8 AdRequest constructor.")
    require(service, "ad.OnRewarded += OnRewarded", "Rewarded grant must remain tied to the provider reward callback.")

    require(validator, "ValidateYandexGradleTemplates(errors);", "Production preflight must validate Android Gradle templates.")
    require(validator, "useCustomMainGradleTemplate: 1", "Production preflight must require Custom Main Gradle Template.")
    require(validator, "useCustomGradlePropertiesTemplate: 1", "Production preflight must require Custom Gradle Properties Template.")
    require(validator, "useCustomGradleSettingsTemplate: 1", "Production preflight must require Custom Gradle Settings Template.")
    require(validator, "Assets/Plugins/Android/mainTemplate.gradle", "Production preflight must require mainTemplate.gradle.")
    require(validator, "Assets/Plugins/Android/gradleTemplate.properties", "Production preflight must require gradleTemplate.properties.")
    require(validator, "Assets/Plugins/Android/settingsTemplate.gradle", "Production preflight must require settingsTemplate.gradle.")

    require(asmdef, '"YandexMobileAds"', "Game.Monetization must reference the YandexMobileAds assembly.")
    require(asmdef, '"YANDEX_MOBILE_ADS"', "Yandex adapter must be enabled by an asmdef version define.")
    require(asmdef, '"[8.4.0,8.5.0)"', "Yandex version define must stay constrained to the verified 8.4.x line.")

    require(
        manifest,
        "yandexmobile/yandex-ads-unity-plugin.git?path=/mobileads-sdk#8.4.0",
        "Yandex Mobile Ads must be pinned to the official 8.4.0 tag.",
    )
    require(manifest, "com.google.external-dependency-manager", "EDM4U must be pinned in Packages/manifest.json.")
    require(manifest, "v1.2.188", "EDM4U must stay pinned to verified release 1.2.188.")

    require(placeholder_validator, 'value.StartsWith("R-M-"', "Production Yandex ad unit IDs must require the official R-M- prefix.")

    require(doc, f"Yandex Mobile Ads Unity {EXPECTED_VERSION}", "ADS_INTEGRATION.md must document the verified Yandex version.")
    require(doc, f"**{EXPECTED_VERIFIED_DATE}**", "ADS_INTEGRATION.md must document the Yandex verification date.")
    require(doc, "Packages/manifest.json", "ADS_INTEGRATION.md must document the repository-managed package.")
    require(doc, f"#{EXPECTED_VERSION}", "ADS_INTEGRATION.md must document the verified Yandex Git tag.")
    require(doc, "Custom Main Gradle Template", "ADS_INTEGRATION.md must document the required main Gradle template.")
    require(doc, "Custom Gradle Properties Template", "ADS_INTEGRATION.md must document the Gradle properties template.")
    require(doc, "adb logcat", "ADS_INTEGRATION.md must retain the physical-device SDK integration check.")

    require(checklist, f"Yandex Mobile Ads Unity plugin **{EXPECTED_VERSION}**", "Release checklist must match the verified Yandex plugin version.")
    require(checklist, "Custom Main Gradle Template", "Release checklist must include the Yandex main Gradle template gate.")
    require(checklist, "Custom Gradle Properties Template", "Release checklist must include the Yandex Gradle properties gate.")

    if errors:
        print("Yandex Mobile Ads release contract FAILED:")
        for error in errors:
            print(f"- {error}")
        return 1

    print("Yandex Mobile Ads release contract passed.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
