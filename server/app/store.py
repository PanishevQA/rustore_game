from __future__ import annotations

import json
import sqlite3
from pathlib import Path
from threading import Lock


class Store:
    def __init__(self, path: str):
        Path(path).parent.mkdir(parents=True, exist_ok=True)
        self.path = path
        self.lock = Lock()
        with self._connect() as db:
            db.executescript(
                """
                CREATE TABLE IF NOT EXISTS attempts (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    challenge_id TEXT NOT NULL,
                    player_id TEXT NOT NULL,
                    score REAL NOT NULL,
                    assisted INTEGER NOT NULL,
                    replay_json TEXT NOT NULL,
                    created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
                );
                CREATE INDEX IF NOT EXISTS idx_attempts_challenge ON attempts(challenge_id, score DESC);
                CREATE TABLE IF NOT EXISTS referrals (
                    referral_id TEXT PRIMARY KEY,
                    challenge_id TEXT NOT NULL,
                    inviter_id TEXT NOT NULL,
                    inviter_score REAL NOT NULL,
                    opened_count INTEGER NOT NULL DEFAULT 0,
                    created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
                );
                CREATE TABLE IF NOT EXISTS analytics_events (
                    event_id TEXT PRIMARY KEY,
                    player_id TEXT NOT NULL,
                    session_number INTEGER NOT NULL,
                    event_name TEXT NOT NULL,
                    occurred_at_utc TEXT NOT NULL,
                    parameters_json TEXT NOT NULL,
                    received_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
                );
                CREATE INDEX IF NOT EXISTS idx_analytics_event_name ON analytics_events(event_name, received_at);
                CREATE INDEX IF NOT EXISTS idx_analytics_player ON analytics_events(player_id, session_number);
                """
            )

    def _connect(self):
        db = sqlite3.connect(self.path, check_same_thread=False)
        db.row_factory = sqlite3.Row
        return db

    def save_attempt(self, challenge_id: str, player_id: str, score: float, assisted: bool, replay: dict) -> None:
        with self.lock, self._connect() as db:
            db.execute(
                "INSERT INTO attempts(challenge_id, player_id, score, assisted, replay_json) VALUES(?,?,?,?,?)",
                (challenge_id, player_id, score, int(assisted), json.dumps(replay, separators=(",", ":"))),
            )

    def leaderboard(self, challenge_id: str, limit: int = 100) -> list[dict]:
        with self._connect() as db:
            rows = db.execute(
                """
                SELECT player_id, MAX(score) AS score
                FROM attempts
                WHERE challenge_id=? AND assisted=0
                GROUP BY player_id
                ORDER BY score DESC, player_id ASC
                LIMIT ?
                """,
                (challenge_id, limit),
            ).fetchall()
        return [{"rank": i + 1, "playerId": row["player_id"], "score": row["score"]} for i, row in enumerate(rows)]

    def create_referral(self, referral_id: str, challenge_id: str, inviter_id: str, inviter_score: float) -> None:
        with self.lock, self._connect() as db:
            db.execute(
                "INSERT INTO referrals(referral_id, challenge_id, inviter_id, inviter_score) VALUES(?,?,?,?)",
                (referral_id, challenge_id, inviter_id, inviter_score),
            )

    def get_referral(self, referral_id: str, increment_open: bool = False) -> dict | None:
        with self.lock, self._connect() as db:
            row = db.execute("SELECT * FROM referrals WHERE referral_id=?", (referral_id,)).fetchone()
            if not row:
                return None
            if increment_open:
                db.execute("UPDATE referrals SET opened_count=opened_count+1 WHERE referral_id=?", (referral_id,))
            return {
                "referralId": row["referral_id"],
                "challengeId": row["challenge_id"],
                "inviterId": row["inviter_id"],
                "inviterScore": row["inviter_score"],
            }

    def save_analytics_events(self, events: list[dict]) -> int:
        if not events:
            return 0
        inserted = 0
        with self.lock, self._connect() as db:
            for event in events:
                cursor = db.execute(
                    """
                    INSERT OR IGNORE INTO analytics_events(
                        event_id, player_id, session_number, event_name, occurred_at_utc, parameters_json
                    ) VALUES(?,?,?,?,?,?)
                    """,
                    (
                        event["eventId"],
                        event["playerId"],
                        event["sessionNumber"],
                        event["eventName"],
                        event["occurredAtUtc"],
                        json.dumps(event.get("parameters", []), separators=(",", ":"), ensure_ascii=False),
                    ),
                )
                inserted += cursor.rowcount
        return inserted
