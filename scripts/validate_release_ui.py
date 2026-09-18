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
    ),
    "gameplay_hud": (
        "ReleaseGameplayHud",
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
        "ReleaseUiKit.Button",
    ),
    "result": (
        "ResultHeader",
        "_scoreText",
        "ПОДЕЛИТЬСЯ КАРТОЧКОЙ",
    ),
    "share_card": (
        "MEMORY TRACE",
        "ЭТАЛОН  /  ТВОЯ ЛИНИЯ",
        "НЕ СБЕЙСЯ!  •  RuStore",
        "ReleaseUiKit.Rounded",
    ),
    "rewarded": (
        "new Vector2(0.23f, 0.118f)",
        "БЕСПЛАТНАЯ ПОДСКАЗКА",
        "ReleaseUiKit.Button",
    ),
    "hint": (
        "ПОКАЗАТЬ МАРШРУТ ЕЩЁ РАЗ",
        "ReleaseUiKit.Button",
    ),
    "platform": (
        "НЕ ПРОПУСКАТЬ DAILY?",
        "НУЖНО ОБНОВЛЕНИЕ",
        "ReleaseUiKit.Panel",
        "ReleaseUiKit.Button",
    ),
}

for key, markers in required.items():
    for marker in markers:
        if marker not in texts[key]:
            errors.append(f"{FILES[key].name} release UI contract is missing: {marker!r}")

# Avoid emoji glyphs in runtime text rendered through Unity's built-in fallback font.
for key in ("meta", "result", "share_card", "rewarded", "hint", "gameplay_hud"):
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

if errors:
    print("Release UI contract FAILED:")
    for error in errors:
        print(f"- {error}")
    sys.exit(1)

print("Release UI contract passed.")
