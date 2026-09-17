from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
BOOTSTRAP = ROOT / "UnityProject/Assets/Game/Presentation/GameBootstrap.cs"

text = BOOTSTRAP.read_text(encoding="utf-8")

required = [
    "private int _navigationRevision;",
    "private int BeginNavigation()",
    "private bool IsCurrentNavigation(int revision, Mode mode)",
    "DailyLoadResult loaded = await _dailyService.LoadCurrentAsync();",
    "if (!IsCurrentNavigation(revision, Mode.Daily)) return;",
    "DuelSession session = _duelSession;",
    "if (!IsCurrentNavigation(revision, Mode.Duel)) return;",
    "List<IReadOnlyList<RecordedPoint>> replays = SnapshotReplays(_dailyReplays);",
    "var scores = new List<double>(_dailyScores);",
    "if (!IsCurrentNavigation(revision, mode)) return;",
    "!IsCurrentNavigation(revision, Mode.Home)",
]

missing = [value for value in required if value not in text]
if missing:
    raise SystemExit("Async navigation contract missing: " + ", ".join(missing))

# Async completion code must not read mutable current session identifiers after await.
for forbidden in [
    '"challenge_id", _daily.ChallengeId',
    '"referrer_id", _duelSession.Referral.ReferralId',
]:
    if forbidden in text:
        raise SystemExit(f"Mutable async state leaked into completion callback: {forbidden}")

print("Async navigation validation passed.")
