#!/usr/bin/env python3
import json
import subprocess
import sys
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SCRIPT = ROOT / "scripts/analyze_unity_log.py"


def run(*args: str) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        [sys.executable, str(SCRIPT), *args],
        check=False,
        capture_output=True,
        text=True,
        encoding="utf-8",
    )


def main() -> int:
    with tempfile.TemporaryDirectory() as temp:
        root = Path(temp)
        log = root / "editmode.log"
        xml = root / "editmode-results.xml"
        play_xml = root / "playmode-results.xml"
        output = root / "diagnostics.json"

        log.write_text(
            "Assets/Game/Foo.cs(12,7): error CS0103: The name 'missing' does not exist\n"
            "NullReferenceException: object reference not set\n",
            encoding="utf-8",
        )
        xml.write_text(
            '<?xml version="1.0" encoding="utf-8"?>'
            '<test-run>'
            '<test-suite>'
            '<test-case name="BrokenTest" fullname="Game.Tests.BrokenTest" result="Failed">'
            '<failure><message>Expected true but was false</message></failure>'
            '</test-case>'
            '</test-suite>'
            '</test-run>',
            encoding="utf-8",
        )

        play_xml.write_text(
            '<?xml version="1.0" encoding="utf-8"?>'
            '<test-run>'
            '<test-suite>'
            '<test-case name="BrokenPlayModeTest" fullname="Game.PlayModeTests.BrokenPlayModeTest" result="Failed">'
            '<failure><message>Canvas was missing</message></failure>'
            '</test-case>'
            '</test-suite>'
            '</test-run>',
            encoding="utf-8",
        )

        result = run(
            "--log", str(log),
            "--test-results", str(xml),
            "--test-results", str(play_xml),
            "--json-out", str(output),
            "--fail-on-errors",
        )
        if result.returncode != 1:
            print(result.stdout)
            print(result.stderr)
            raise AssertionError("Analyzer must fail with --fail-on-errors when diagnostics exist.")

        data = json.loads(output.read_text(encoding="utf-8"))
        kinds = {item["kind"] for item in data["diagnostics"]}
        if not {"compiler", "exception", "test"}.issubset(kinds):
            raise AssertionError(f"Expected compiler/exception/test diagnostics, got: {kinds}")
        test_names = {item.get("name") for item in data["diagnostics"] if item["kind"] == "test"}
        if "Game.Tests.BrokenTest" not in test_names or "Game.PlayModeTests.BrokenPlayModeTest" not in test_names:
            raise AssertionError(f"Expected EditMode and PlayMode test failures, got: {test_names}")

        clean = root / "clean.log"
        clean.write_text("All tests passed.\n", encoding="utf-8")
        clean_result = run("--log", str(clean), "--fail-on-errors")
        if clean_result.returncode != 0:
            raise AssertionError("Clean log must not fail diagnostics analysis.")

    print("Unity log analyzer tests passed.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
