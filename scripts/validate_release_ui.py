#!/usr/bin/env python3
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
PRESENTATION = ROOT / "UnityProject/Assets/Game/Presentation"

FILES = {
    "home": PRESENTATION / "HomeDashboardCoordinator.cs",
    "ui_kit": PRESENTATION / "ReleaseUiKit.cs",
    "ui_components": PRESENTATION / "ReleaseUiComponents.cs",
    "daily_intro": PRESENTATION / "DailyIntroCoordinator.cs",
    "gameplay_hud": PRESENTATION / "GameplayHudCoordinator.cs",
    "generated_assets": PRESENTATION / "GeneratedUiAssets.cs",
    "visual_theme": PRESENTATION / "VisualThemeCoordinator.cs",
    "route_graphic": PRESENTATION / "RouteGraphic.cs",
    "game_bootstrap": PRESENTATION / "GameBootstrap.cs",
    "training": PRESENTATION / "TrainingMenuCoordinator.cs",
    "campaign": PRESENTATION / "CampaignLevelMenuOverlay.cs",
    "meta": PRESENTATION / "MetaMenuOverlay.cs",
    "result": PRESENTATION / "ResultEnhancementCoordinator.cs",
    "share_card": PRESENTATION / "ResultShareCardRenderer.cs",
    "rewarded": PRESENTATION / "RewardedHomeOverlay.cs",
    "hint": PRESENTATION / "HintRuntimeCoordinator.cs",
    "platform": PRESENTATION / "RuntimePlatformCoordinator.cs",
    "referral_offer": PRESENTATION / "ReferralOfferCoordinator.cs",
}

errors: list[str] = []


def read(key: str) -> str:
    path = FILES[key]
    if not path.is_file():
        errors.append(f"Missing release UI source: {path.relative_to(ROOT)}")
        return ""
    return path.read_text(encoding="utf-8")


texts = {key: read(key) for key in FILES}

required = {
    "ui_kit": (
        "CreateRoundedSprite",
        "CreateCircleSprite",
        "public static Button Button",
        "public static Image Panel",
    ),
    "ui_components": (
        "PrimaryButton(",
        "SecondaryButton(",
        "GlassCard(",
        "Backdrop(",
        "StatTile(",
        "CurrencyPill(",
        "Icon(",
        "GeneratedCoin",
        "GeneratedUiAssets.CoinIcon",
        "CreateGradientRoundedSprite",
        "CreateBackdropTexture",
    ),
    "daily_intro": (
        "DailyIntroCanvas",
        "ИСПЫТАНИЕ ДНЯ",
        "GameBootstrapRuntimeBridge.StartDaily",
        "ReleaseUiComponents.PrimaryButton",
        "backdrop.raycastTarget = true",
        "public bool IsOpen",
    ),
    "home": (
        "HomeDashboard",
        "BuildDailyCard",
        "BuildCampaignCard",
        "BuildStatRow",
        "HomeStatsStrip",
        "BuildQuickActions",
        "GeneratedUiAssets.QuickActionIcon",
        "GeneratedUiAssets.TryApply(start, GeneratedUiAssets.StartMarker)",
        "wide ? 28 : 24",
        "wide ? 20 : 18",
        "DailyIntroCoordinator",
        "ReleaseUiComponents.Backdrop",
        "ОДИН ЧЕЛЛЕНДЖ.",
        "GameBootstrapRuntimeBridge.IsPlainHome(_bootstrap)",
        "_dashboardMotion?.Cancel()",
    ),
    "gameplay_hud": (
        "ReleaseGameplayHud",
        "GameplayHeader",
        "GameplayMetrics",
        "GameplayInstruction",
        "ApplyGameplayBoardLayout(true)",
        "new Vector2(0.055f, 0.155f)",
        "ReleaseUiComponents.GlassCard",
        "GameBootstrapRuntimeBridge.IsActiveRound(_bootstrap)",
        "ЗАПОМНИ МАРШРУТ",
        '"Instruction", "ЗАПОМНИ МАРШРУТ", 40',
        '"Hint", "Запомни форму и повороты маршрута", 23',
        "ТВОЯ ОЧЕРЕДЬ",
    ),
    "generated_assets": (
        'Root = "GeneratedUI/"',
        'TrainingIcon = "icon_training"',
        'StatisticsIcon = "icon_stats"',
        'StoreIcon = "icon_store"',
        'CoinIcon = "coin"',
        'StartMarker = "marker_start_glow"',
        'EndMarker = "marker_end_glow"',
        'DailyIcon = "icon_daily"',
        'CampaignIcon = "icon_campaign"',
        'HintIcon = "icon_hint"',
        'EyeIcon = "icon_eye"',
        'ReplayIcon = "icon_replay"',
        'ShareIcon = "icon_share"',
        'ChallengeIcon = "icon_challenge"',
        'AdIcon = "icon_ad"',
        'SettingsIcon = "icon_settings"',
        'CheckIcon = "icon_check"',
        'LockIcon = "icon_lock"',
        'MedalBronze = "medal_bronze"',
        'MedalSilver = "medal_silver"',
        'MedalGold = "medal_gold"',
        'StarFilled = "star_filled"',
        'StarEmpty = "star_empty"',
        "Resources.Load<Sprite>",
    ),
    "visual_theme": (
        "GeneratedUiAssets.StartMarker",
        "GeneratedUiAssets.EndMarker",
        "marker.rectTransform.sizeDelta = new Vector2(72f, 72f)",
    ),
    "route_graphic": (
        "BuildLocalPoints",
        "JoinOffset",
        "AddRoundCap",
        "one continuous strip",
    ),
    "game_bootstrap": (
        "corridorWidth * 0.46f",
        "Mathf.Clamp",
        "ГОЛУБОЙ ТОЧКИ",
    ),
    "training": (
        "ReleaseUiComponents.GlassCard",
        "SelectDifficulty",
        "StartSelected",
        "GeneratedUiAssets.TrainingIcon",
        "GeneratedTrainingIcon",
        "ЛЁГКАЯ",
        "СРЕДНЯЯ",
        "СЛОЖНАЯ",
        "НАЧАТЬ ТРЕНИРОВКУ",
    ),
    "campaign": (
        "ChapterProgressTrack",
        "LevelPath",
        "CreatePathRail",
        "CreateLevelButton(",
        "ТЕКУЩИЙ",
        "StartHighestUnlocked",
        "CampaignReleaseBackdrop",
        '"LevelNumber", levelNumber.ToString(), 44',
        '"State", stars, 22',
        '"Best", best, 19',
    ),
    "meta": (
        "MetaReleaseBackdrop",
        "GeneratedPanelIcon",
        "GeneratedUiAssets.StatisticsIcon",
        "GeneratedUiAssets.StoreIcon",
        "RenderStatisticsRows",
        "AddInfoRow",
        "AddSettingToggleRow",
        "PremiumOffer",
        "AddStoreSectionLabel",
        "СКИНЫ ЛИНИИ",
        "ПОДСКАЗКИ",
        "BodyCard",
        "ActionsCard",
        "AddStoreProductAction",
        "ProductPrice",
        "ScrollRect",
        "RectMask2D",
        "ContentSizeFitter",
        '"ActionsViewport"',
        '"ProductIconWell"',
    ),
    "result": (
        "ResultHeader",
        "ResultDetail",
        "ResultActions",
        "MeanDeviation",
        "EndAccuracy",
        "Completion",
        "GestureTime",
        "LastScoreBreakdown",
        "SetStatTileVisible(_meanValue, visible)",
        "SetStatTileVisible(_timeValue, visible)",
        "_metricsPanel.SetActive(visible)",
        "ReleaseUiComponents.PrimaryButton",
        "SuppressLegacyResultHeader",
        "SuppressLegacyResultControls",
        "RestoreLegacyResultHeader",
        "_legacyTitleGroup.alpha = 1f",
        "_legacyStatusGroup.alpha = 1f",
        "_scoreText",
        '"Score", "0.0%", 96',
        '"ComparisonScope"',
        '"ReferenceLegend"',
        '"PlayerLegend"',
        '"MedalIcon"',
        "MedalAsset",
        '"ShareImage"',
        '"ShareFeedback"',
        "ПОДЕЛИТЬСЯ РЕЗУЛЬТАТОМ",
        "БРОСИТЬ ВЫЗОВ",
    ),
    "share_card": (
        "ИСПЫТАНИЕ ПАМЯТИ",
        '"ReferenceLegendLine"',
        '"ReferenceLegendText"',
        '"PlayerLegendLine"',
        '"PlayerLegendText"',
        "ЭТАЛОН",
        "ТВОЯ ЛИНИЯ",
        "НЕ СБЕЙСЯ!  •  RuStore",
        "ReleaseUiKit.Rounded",
        'CreateMarker(routePanel.transform, "Start"',
        'CreateMarker(routePanel.transform, "End"',
    ),
    "rewarded": (
        '"RewardedHintCard"',
        '"RewardedIconWell"',
        '"RewardedTitle"',
        '"RewardedReward"',
        '"RewardedBalance"',
        "new Vector2(0.07f, 0.072f)",
        "new Vector2(0.93f, 0.150f)",
        "СМОТРЕТЬ РЕКЛАМУ",
        "ПОДСКАЗКА ПОЛУЧЕНА",
        "ReleaseUiComponents.SecondaryButton",
        "dailyIntro.IsOpen",
    ),
    "hint": (
        "ПОКАЗАТЬ МАРШРУТ ЕЩЁ РАЗ",
        '"HintTitle"',
        '"HintCount"',
        '"0 ПОДСК."',
        "RefreshHintLabel",
        "ReleaseUiComponents.SecondaryButton",
    ),
    "platform": (
        "НЕ ПРОПУСКАТЬ DAILY?",
        "БЕЗ НАПОМИНАНИЙ",
        "_notificationBackdrop.SetActive(false)",
        "НУЖНО ОБНОВЛЕНИЕ",
        "ReleaseUiKit.Panel",
        "ReleaseUiKit.Button",
    ),
    "referral_offer": (
        "ЧЕЛЛЕНДЖ ОТ ДРУГА",
        "РЕЗУЛЬТАТ ДРУГА",
        "ПРИНЯТЬ ВЫЗОВ",
        "public bool Dismiss()",
        '"ReferralReleaseBackdrop"',
        "ReleaseUiComponents.GlassCard",
        "ReleaseUiComponents.PrimaryButton",
        "ReleaseUiComponents.SecondaryButton",
    ),
}

for key, markers in required.items():
    for marker in markers:
        if marker not in texts[key]:
            errors.append(f"{FILES[key].name} release UI contract is missing: {marker!r}")

if "_hudVisible == visible" in texts["gameplay_hud"]:
    errors.append("Gameplay HUD visibility must always write CanvasGroup state; equality short-circuit can leak HUD on initial Home.")

if "RectTransform rt = playArea.rectTransform;" in texts["visual_theme"]:
    errors.append("VisualThemeCoordinator must not own PlayArea geometry; GameplayHudCoordinator is the single board layout owner.")

GENERATED_UI = ROOT / "UnityProject/Assets/Resources/GeneratedUI"
for asset in (
    "icon_training.png",
    "icon_stats.png",
    "icon_store.png",
    "coin.png",
    "marker_start_glow.png",
    "marker_end_glow.png",
    "icon_daily.png",
    "icon_campaign.png",
    "icon_hint.png",
    "icon_eye.png",
    "icon_replay.png",
    "icon_share.png",
    "icon_challenge.png",
    "icon_ad.png",
    "icon_settings.png",
    "icon_check.png",
    "icon_lock.png",
    "item_no_ads.png",
    "item_cosmetic.png",
    "medal_bronze.png",
    "medal_silver.png",
    "medal_gold.png",
    "star_empty.png",
    "star_filled.png",
):
    if not (GENERATED_UI / asset).is_file():
        errors.append(f"Missing generated UI runtime asset: {asset}")

# Avoid emoji glyphs in runtime text rendered through Unity's built-in fallback font.
for key in ("ui_components", "daily_intro", "meta", "result", "share_card", "rewarded", "hint", "gameplay_hud", "referral_offer"):
    text = texts[key]
    for char in text:
        if ord(char) > 0xFFFF:
            errors.append(f"{FILES[key].name} contains a supplementary-plane glyph/emoji; keep release copy font-safe.")
            break

result = texts["result"]
if "group.interactable = visible;" not in result or "group.blocksRaycasts = visible;" not in result:
    errors.append("Result release action sheet must restore legacy fallback controls after leaving Result.")

if "SetStatTileVisible(_meanValue, visible)" not in texts["result"]:
    errors.append("Result metrics must be hidden outside Result; leaked stat cards obstruct gameplay input.")

if "+50 МОНЕТ" in texts["home"]:
    errors.append("Home must not advertise an unimplemented fixed Daily coin reward.")

if 'ReleaseUiComponents.Backdrop(_canvas.transform, "ResultBackdrop")' in texts["result"]:
    errors.append("Result release chrome must not hide the underlying route comparison with an opaque backdrop.")

# The share card should use the textual celebration label, not provider emoji/icon glyphs.
if "Celebration?.Icon" in texts["share_card"]:
    errors.append("Share card must not render ScoreCelebration.Icon; use the font-safe celebration label only.")

# Rewarded placement must stay below Home quick-action cards rather than covering them.
if "new Vector2(0.20f, 0.276f)" in texts["rewarded"]:
    errors.append("Rewarded hint regressed into the Home quick-action card area.")


motion = (PRESENTATION / "ReleasePanelMotion.cs").read_text(encoding="utf-8")
for marker in ("public void Cancel()", "_animating = false;"):
    if marker not in motion:
        errors.append(f"ReleasePanelMotion cancel contract is missing: {marker}")

cancel_start = motion.find("public void Cancel()")
cancel_end = motion.find("private void Update()", cancel_start)
if cancel_start >= 0 and cancel_end > cancel_start:
    cancel_body = motion[cancel_start:cancel_end]
    if "_group.alpha = 1f" in cancel_body:
        errors.append("ReleasePanelMotion.Cancel must not reveal a CanvasGroup that its owner is hiding.")

# Superseded Home composition layers must stay removed so one surface has one visual owner.
for obsolete in (
    PRESENTATION / "HomeHeroCoordinator.cs",
    PRESENTATION / "HomePolishCoordinator.cs",
    PRESENTATION / "CampaignHomeLayoutCoordinator.cs",
    PRESENTATION / "ExtendedUiThemeCoordinator.cs",
    PRESENTATION / "CampaignVisualThemeCoordinator.cs",
):
    if obsolete.exists():
        errors.append(f"Superseded Home presentation layer returned: {obsolete.name}")

if errors:
    print("Release UI contract FAILED:")
    for error in errors:
        print(f"- {error}")
    sys.exit(1)

print("Release UI contract passed.")
