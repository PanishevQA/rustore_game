#!/usr/bin/env python3
import argparse
import json
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

COMPILER_RE = re.compile(
    r"(?P<file>[^\r\n:]+\.cs)\((?P<line>\d+),(?P<column>\d+)\):\s*error\s+"
    r"(?P<code>CS\d+):\s*(?P<message>.+)"
)

EXCEPTION_NAMES = (
    "NullReferenceException",
    "MissingReferenceException",
    "TypeLoadException",
    "ReflectionTypeLoadException",
    "InvalidOperationException",
    "ArgumentException",
    "BuildFailedException",
)

GENERIC_ERROR_MARKERS = (
    "Scripts have compiler errors",
    "Compilation failed",
    "Player build failed",
    "executeMethod method",
)


def add_unique(items: list[dict], seen: set[tuple], item: dict) -> None:
    key = (
        item.get("kind"),
        item.get("file"),
        item.get("line"),
        item.get("code"),
        item.get("name"),
        item.get("message"),
    )
    if key in seen:
        return
    seen.add(key)
    items.append(item)


def parse_log(path: Path, items: list[dict], seen: set[tuple]) -> None:
    if not path.is_file():
        return

    for raw in path.read_text(encoding="utf-8", errors="replace").splitlines():
        line = raw.strip()
        if not line:
            continue

        match = COMPILER_RE.search(line)
        if match:
            add_unique(
                items,
                seen,
                {
                    "kind": "compiler",
                    "source": str(path),
                    "file": match.group("file").strip(),
                    "line": int(match.group("line")),
                    "column": int(match.group("column")),
                    "code": match.group("code"),
                    "message": match.group("message").strip(),
                },
            )
            continue

        exception_name = next((name for name in EXCEPTION_NAMES if name + ":" in line), None)
        if exception_name:
            add_unique(
                items,
                seen,
                {
                    "kind": "exception",
                    "source": str(path),
                    "code": exception_name,
                    "message": line,
                },
            )
            continue

        if any(marker in line for marker in GENERIC_ERROR_MARKERS):
            add_unique(
                items,
                seen,
                {
                    "kind": "unity",
                    "source": str(path),
                    "message": line,
                },
            )


def parse_test_results(path: Path, items: list[dict], seen: set[tuple]) -> None:
    if not path.is_file():
        return

    try:
        root = ET.parse(path).getroot()
    except ET.ParseError as exc:
        add_unique(
            items,
            seen,
            {
                "kind": "test-results",
                "source": str(path),
                "message": f"Could not parse Unity test results XML: {exc}",
            },
        )
        return

    for case in root.iter("test-case"):
        result = (case.attrib.get("result") or "").lower()
        if result not in {"failed", "failure"}:
            continue

        name = case.attrib.get("fullname") or case.attrib.get("name") or "<unknown test>"
        message = ""
        failure = case.find("failure")
        if failure is not None:
            message_node = failure.find("message")
            if message_node is not None and message_node.text:
                message = " ".join(message_node.text.split())
        add_unique(
            items,
            seen,
            {
                "kind": "test",
                "source": str(path),
                "name": name,
                "message": message or "Unity test failed.",
            },
        )


def build_summary(logs: list[Path], test_results: list[Path]) -> dict:
    diagnostics: list[dict] = []
    seen: set[tuple] = set()

    for log in logs:
        parse_log(log, diagnostics, seen)

    for result_path in test_results:
        parse_test_results(result_path, diagnostics, seen)

    counts: dict[str, int] = {}
    for diagnostic in diagnostics:
        kind = diagnostic["kind"]
        counts[kind] = counts.get(kind, 0) + 1

    return {
        "logs": [str(path) for path in logs],
        "testResults": [str(path) for path in test_results],
        "counts": counts,
        "diagnostics": diagnostics,
    }


def print_summary(summary: dict) -> None:
    diagnostics = summary["diagnostics"]
    if not diagnostics:
        print("Unity diagnostics: no recognized compiler errors, exceptions, or failed tests found.")
        return

    counts = summary["counts"]
    rendered = ", ".join(f"{key}={value}" for key, value in sorted(counts.items()))
    print(f"Unity diagnostics: {rendered}")

    for item in diagnostics[:100]:
        kind = item["kind"]
        if kind == "compiler":
            print(
                f"[compiler] {item['file']}:{item['line']}:{item['column']} "
                f"{item['code']} {item['message']}"
            )
        elif kind == "test":
            print(f"[test] {item['name']}: {item['message']}")
        else:
            print(f"[{kind}] {item.get('message', '')}")

    if len(diagnostics) > 100:
        print(f"... {len(diagnostics) - 100} additional diagnostics omitted from console output.")


def main() -> int:
    parser = argparse.ArgumentParser(description="Summarize Unity batchmode logs for an AI coding agent.")
    parser.add_argument("--log", action="append", default=[], help="Unity log file. May be supplied multiple times.")
    parser.add_argument("--test-results", action="append", default=[], help="Unity Test Framework XML results. May be supplied multiple times.")
    parser.add_argument("--json-out", help="Optional structured JSON output path.")
    parser.add_argument("--fail-on-errors", action="store_true", help="Return exit 1 when diagnostics are found.")
    args = parser.parse_args()

    logs = [Path(value) for value in args.log]
    test_results = [Path(value) for value in args.test_results]

    summary = build_summary(logs, test_results)
    print_summary(summary)

    if args.json_out:
        output = Path(args.json_out)
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")
        print(f"Structured diagnostics: {output}")

    if args.fail_on_errors and summary["diagnostics"]:
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
