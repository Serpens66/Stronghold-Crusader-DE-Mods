#!/usr/bin/env python3
"""Normalize and audit generated semantic-baseline text files."""

from __future__ import annotations

import argparse
import json
from pathlib import Path


EXTENSIONS = {
    ".c", ".cs", ".csproj", ".h", ".java", ".json", ".jsonl", ".log",
    ".md", ".props", ".ps1", ".py", ".targets", ".tsv", ".txt", ".xaml",
}
EXCLUDED_PARTS = {"bin", "obj", "ghidra", "rizin", "xaml-raw", "xaml-raw-serial"}


def candidates(items):
    for item in items:
        path = Path(item)
        paths = [path] if path.is_file() else path.rglob("*")
        for candidate in paths:
            if not candidate.is_file() or candidate.suffix.lower() not in EXTENSIONS:
                continue
            if any(part.lower() in EXCLUDED_PARTS for part in candidate.parts):
                continue
            yield candidate


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", action="append", required=True)
    parser.add_argument("--normalize", action="store_true")
    parser.add_argument("--output", required=True)
    args = parser.parse_args()

    rows = []
    for path in sorted(set(candidates(args.root)), key=lambda value: str(value).lower()):
        data = path.read_bytes()
        text = data.decode("utf-8-sig")
        if args.normalize:
            normalized = text.replace("\r\n", "\n").replace("\r", "\n").replace("\n", "\r\n")
            path.write_text(normalized, encoding="utf-8", newline="")
            data = path.read_bytes()
        naked_lf = sum(1 for index, value in enumerate(data) if value == 10 and (index == 0 or data[index - 1] != 13))
        rows.append({
            "path": str(path), "bytes": len(data), "crlf": data.count(b"\r\n"),
            "nakedLf": naked_lf, "literalBackslashRBackslashN": data.count(b"\\r\\n"),
        })

    bad = [row for row in rows if row["nakedLf"]]
    report = {
        "files": len(rows), "nakedLfFiles": len(bad),
        "literalEscapeFiles": sum(bool(row["literalBackslashRBackslashN"]) for row in rows),
        "literalEscapeOccurrences": sum(row["literalBackslashRBackslashN"] for row in rows),
        "details": rows,
    }
    output = Path(args.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, ensure_ascii=False, indent=2).replace("\n", "\r\n") + "\r\n", encoding="utf-8", newline="")
    if bad:
        raise ValueError(f"Naked LF remains in {len(bad)} files")
    print(json.dumps({key: value for key, value in report.items() if key != "details"}))


if __name__ == "__main__":
    main()
