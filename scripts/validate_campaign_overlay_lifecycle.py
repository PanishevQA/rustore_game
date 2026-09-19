#!/usr/bin/env python3
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OVERLAY = ROOT / "UnityProject/Assets/Game/Presentation/CampaignLevelMenuOverlay.cs"
text = OVERLAY.read_text(encoding="utf-8")

required = (
    "private void LateUpdate()",
    "if (ResolveLiveBootstrap() == null) Close();",
    "_bootstrap = ResolveLiveBootstrap(bootstrap);",
    "GameBootstrap owner = ResolveLiveBootstrap();",
    "CampaignRuntimeCoordinator.StartLevel(owner, levelNumber);",
    "CampaignRuntimeCoordinator.ReturnHome(owner);",
    "public void Close()",
    "_bootstrap = null;",
    "private GameBootstrap ResolveLiveBootstrap(GameBootstrap preferred = null)",
    "FindFirstObjectByType<GameBootstrap>()",
)
for needle in required:
    if needle not in text:
        raise SystemExit(f"Campaign overlay lifecycle contract is missing: {needle}")

select = text.find("private void SelectLevel")
resolve = text.find("GameBootstrap owner = ResolveLiveBootstrap();", select)
close = text.find("Close();", resolve)
start = text.find("CampaignRuntimeCoordinator.StartLevel(owner, levelNumber);", close)
if min(select, resolve, close, start) < 0 or not (select < resolve < close < start):
    raise SystemExit("Campaign level selection must capture a live owner before closing the overlay and starting a level.")

go_home = text.find("private void GoHome")
home_resolve = text.find("GameBootstrap owner = ResolveLiveBootstrap();", go_home)
home_close = text.find("Close();", home_resolve)
home_call = text.find("CampaignRuntimeCoordinator.ReturnHome(owner);", home_close)
if min(go_home, home_resolve, home_close, home_call) < 0 or not (go_home < home_resolve < home_close < home_call):
    raise SystemExit("Campaign Home action must capture a live owner before closing the overlay.")

print("Campaign overlay lifecycle validation passed.")
