#!/usr/bin/env python3
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
PRESENTATION = ROOT / "UnityProject/Assets/Game/Presentation"

FILES = {
    "home": PRESENTATION / "HomeDashboardCoordinator.cs",
    "ui_kit": PRESENTATION / "ReleaseUiKit.cs",
    "gameplay_hud": PRESENTATION / "GameplayHudCoordinator.cs",
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
    "home": (
        "HomeDashboard",
        "BuildDailyCard",
        "BuildCampaignCard",
        "BuildQuickActions",
        "GameBootstrapRuntimeBridge.IsPlainHome(_bootstrap)",
        "_dashboardMotion?.Cancel()",
        "MEMORY TRACE",
    ),
    "gameplay_hud": (
        "ReleaseGameplayHud",
        "GameplayHeader",
        "GameBootstrapRuntimeBridge.IsActiveRound(_bootstrap)",
        "ЗАПОМНИ МАРШРУТ",
        "ТВОЯ ОЧЕРЕДЬ",
    ),
    "training": (
        "ReleaseUiKit.Panel",
        "ЛЁГКАЯ",
        "СРЕДНЯЯ",
        "СЛОЖНАЯ",
        "СЛУЧАЙНАЯ",
    ),
    "campaign": (
        "ChapterProgressTrack",
        "CreateLevelButton(",
        "ReleaseUiKit.SurfaceRaised",
    ),
    "meta": (
        "BodyCard",
        "ActionsCard",
        "AddStoreProductAction",
        "ProductPrice",
        "ReleaseUiKit.Button",
    ),
    "result": (
        "ResultHeader",
        "ResultDetail",
        "ResultActions",
        "SuppressLegacyResultHeader",
        "SuppressLegacyResultControls",
        "RestoreLegacyResultHeader",
        "_legacyTitleGroup.alpha = 1f",
        "_legacyStatusGroup.alpha = 1f",
        "_scoreText",
        "ПОДЕЛИТЬСЯ РЕЗУЛЬТАТОМ",
        "БРОСИТЬ ВЫЗОВ",
    ),
    "share_card": (
        "MEMORY TRACE",
        "ЭТАЛОН  /  ТВОЯ ЛИНИЯ",
        "НЕ СБЕЙСЯ!  •  RuStore",
        "ReleaseUiKit.Rounded",
        'CreateMarker(routePanel.transform, "Start"',
        'CreateMarker(routePanel.transform, "End"',
    ),
    "rewarded": (
        "new Vector2(0.075f, 0.092f)",
        "new Vector2(0.925f, 0.142f)",
        "ПОДСКАЗКА ЗА РЕКЛАМУ",
        "ReleaseUiKit.Button",
    ),
    "hint": (
        "ПОКАЗАТЬ МАРШРУТ ЕЩЁ РАЗ",
        "ReleaseUiKit.Button",
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
        "ПРИНЯТЬ ВЫЗОВ",
        "public bool Dismiss()",
        "ReleaseUiKit.Panel",
        "ReleaseUiKit.Button",
    ),
}

for key, markers in required.items():
    for marker in markers:
        if marker not in texts[key]:
            errors.append(f"{FILES[key].name} release UI contract is missing: {marker!r}")

if "_hudVisible == visible" in texts["gameplay_hud"]:
    errors.append("Gameplay HUD visibility must always write CanvasGroup state; equality short-circuit can leak HUD on initial Home.")

# Avoid emoji glyphs in runtime text rendered through Unity's built-in fallback font.
for key in ("meta", "result", "share_card", "rewarded", "hint", "gameplay_hud", "referral_offer"):
    text = texts[key]
    for char in text:
        if ord(char) > 0xFFFF:
            errors.append(f"{FILES[key].name} contains a supplementary-plane glyph/emoji; keep release copy font-safe.")
            break

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
