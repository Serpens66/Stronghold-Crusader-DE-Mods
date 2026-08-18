from __future__ import annotations

import sys
from pathlib import Path


def find_all(path: Path, needles: list[tuple[str, str, bytes]]) -> list[tuple[int, str, str]]:
    offsets: list[tuple[int, str, str]] = []
    overlap = max((len(needle) - 1 for _, _, needle in needles), default=0)
    previous = b""
    absolute = 0

    with path.open("rb") as stream:
        while chunk := stream.read(8 * 1024 * 1024):
            data = previous + chunk
            for text, encoding, needle in needles:
                start = 0
                while (match := data.find(needle, start)) >= 0:
                    offsets.append((absolute - len(previous) + match, text, encoding))
                    start = match + 1
            previous = data[-overlap:] if overlap else b""
            absolute += len(chunk)

    return offsets


def main() -> int:
    if len(sys.argv) < 3:
        print("usage: scan_binary_text.py <file-or-directory> <text> [text ...]")
        return 2

    root = Path(sys.argv[1])
    paths = [root] if root.is_file() else (path for path in root.rglob("*") if path.is_file())
    encodings = ("utf-8", "utf-16-le", "utf-16-be")
    needles = [
        (text, encoding, text.encode(encoding))
        for text in sys.argv[2:]
        for encoding in encodings
    ]

    for path in paths:
        for offset, text, encoding in find_all(path, needles):
            print(f"{path}\t0x{offset:X}\t{encoding}\t{text}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
