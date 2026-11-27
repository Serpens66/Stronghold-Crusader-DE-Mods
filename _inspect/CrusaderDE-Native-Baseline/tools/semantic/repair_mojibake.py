#!/usr/bin/env python3
"""Find and reverse UTF-8 text that was decoded as Windows-1252 and re-encoded, possibly many times."""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

CP1252_HIGH = {bytes([b]).decode("cp1252"): b for b in range(0x80, 0x100) if b not in (0x81, 0x8D, 0x8F, 0x90, 0x9D)}
RUN = re.compile("[" + "".join(re.escape(c) for c in CP1252_HIGH) + "\u0081\u008d\u008f\u0090\u009d]{2,}")


def to_bytes(text: str) -> bytes | None:
    out = bytearray()
    for ch in text:
        code = ord(ch)
        if code < 0x80 or 0x80 <= code <= 0xFF and ch not in CP1252_HIGH:
            out.append(code)
        elif ch in CP1252_HIGH:
            out.append(CP1252_HIGH[ch])
        else:
            return None
    return bytes(out)


def unwind(run: str) -> str:
    """Decode one run until it stops changing; any step that is not valid UTF-8 ends the unwinding."""
    while True:
        raw = to_bytes(run)
        if raw is None:
            return run
        try:
            decoded = raw.decode("utf-8")
        except UnicodeDecodeError:
            return run
        if decoded == run:
            return run
        run = decoded


def repair_text(text: str) -> tuple[str, int]:
    count = 0

    def fix(match: re.Match) -> str:
        nonlocal count
        fixed = unwind(match.group(0))
        if fixed != match.group(0):
            count += 1
        return fixed

    previous = None
    while previous != text:
        previous = text
        text = RUN.sub(fix, text)
    return text, count


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=["check", "repair"])
    parser.add_argument("paths", nargs="+")
    args = parser.parse_args()
    files = [p for item in args.paths for p in ([Path(item)] if Path(item).is_file() else sorted(Path(item).rglob("*.md")))]
    dirty = 0
    for path in files:
        raw = path.read_bytes()
        text = raw.decode("utf-8")
        fixed, count = repair_text(text)
        if not count:
            continue
        dirty += 1
        changed_lines = [i + 1 for i, (a, b) in enumerate(zip(text.split("\n"), fixed.split("\n"))) if a != b]
        if fixed.count("\n") != text.count("\n"):
            raise SystemExit(f"{path}: repair would change the line count")
        print(f"{path}: {count} runs, {len(raw)} -> {len(fixed.encode('utf-8'))} bytes, lines {changed_lines[:20]}")
        if args.mode == "repair":
            path.write_bytes(fixed.encode("utf-8"))
    if args.mode == "check" and dirty:
        print(f"Mojibake found in {dirty} file(s); run: repair_mojibake.py repair <paths>", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
