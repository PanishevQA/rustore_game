#!/usr/bin/env python3
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
SERVICE = ROOT / "UnityProject/Assets/Game/Monetization/YandexMobileAdsService.cs"
VALIDATOR = ROOT / "UnityProject/Assets/Game/Editor/ProductionReleaseValidator.cs"
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

    require(validator, "ValidateYandexGradleTemplates(errors);", "Production preflight must validate Yandex Gradle templates.")
    require(validator, "useCustomMainGradleTemplate: 1", "Production preflight must require Custom Main Gradle Template.")
    require(validator, "useCustomGradlePropertiesTemplate: 1", "Production preflight must require Custom Gradle Properties Template.")
    require(validator, "Assets/Plugins/Android/mainTemplate.gradle", "Production preflight must require mainTemplate.gradle.")
    require(validator, "Assets/Plugins/Android/gradleTemplate.properties", "Production preflight must require gradleTemplate.properties.")
    require(validator, "YANDEX_MOBILE_ADS", "Production preflight must require the Yandex Android scripting symbol.")
    require(placeholder_validator, 'value.StartsWith("R-M-"', "Production Yandex ad unit IDs must require the official R-M- prefix.")

    require(doc, f"Yandex Mobile Ads Unity {EXPECTED_VERSION}", "ADS_INTEGRATION.md must document the verified Yandex version.")
    require(doc, f"**{EXPECTED_VERIFIED_DATE}**", "ADS_INTEGRATION.md must document the Yandex verification date.")
    require(doc, f"yandex-mobileads-lite-{EXPECTED_VERSION}.unitypackage", "ADS_INTEGRATION.md must use the verified Unity package.")
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
