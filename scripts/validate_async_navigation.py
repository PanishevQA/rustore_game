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

# Completion methods must use captured session identity after await, not mutable current fields.
daily_completion = text.split("private async void CompleteDaily()", 1)[1].split("private async void CompleteDuel()", 1)[0]
duel_completion = text.split("private async void CompleteDuel()", 1)[1].split("private void NextDailyRoute()", 1)[0]

if '"challenge_id", _daily.ChallengeId' in daily_completion:
    raise SystemExit("Daily completion reads mutable _daily identity after await.")
if '"challenge_id", _daily.ChallengeId' in duel_completion:
    raise SystemExit("Duel completion reads mutable _daily identity after await.")
if '"referrer_id", _duelSession.Referral.ReferralId' in duel_completion:
    raise SystemExit("Duel completion reads mutable _duelSession identity after await.")

print("Async navigation validation passed.")
