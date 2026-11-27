#!/usr/bin/env python3
"""Check the curated knowledge layer (vanilla/, mods/) against the audit documents it summarizes.

Errors: mojibake, documents without exactly one topic, missing topic files, broken links or anchors,
topic files that do not link every audit document of their topic, naked LF in curated files.
Report (--coverage): hex coordinates from a topic's audit documents that the topic file never mentions.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from repair_mojibake import repair_text  # noqa: E402

BASELINE = Path(__file__).resolve().parents[2]
CURRENT_JSON = BASELINE / "CURRENT.json"
LINK = re.compile(r"\]\(([^)\s]+)\)")
HEX = re.compile(r"\b0x([0-9A-Fa-f]{4,9})\b|\b([0-9A-F]{5,6})\b")


def knowledge_dir() -> Path:
    current = json.loads(CURRENT_JSON.read_text(encoding="utf-8"))
    semantic = next((v for k, v in current.items() if "semantic" in k.lower() and isinstance(v, str)), None)
    candidate = BASELINE / semantic / "knowledge" if semantic else None
    if candidate is None or not candidate.is_dir():
        candidate = next(BASELINE.glob("sem/*/knowledge"))
    return candidate


def slug(heading: str) -> str:
    text = heading.strip().lower()
    text = re.sub(r"[^\w\- ]", "", text, flags=re.UNICODE)
    return text.replace(" ", "-")


def anchors(path: Path) -> set[str]:
    seen: dict[str, int] = {}
    result = set()
    for line in path.read_text(encoding="utf-8").splitlines():
        if not line.startswith("#"):
            continue
        base = slug(line.lstrip("#"))
        n = seen.get(base, 0)
        seen[base] = n + 1
        result.add(base if n == 0 else f"{base}-{n}")
    return result


def coordinates(text: str) -> set[int]:
    found = set()
    for a, b in HEX.findall(text):
        value = int(a or b, 16)
        if value >= 0x180000000:
            value -= 0x180000000
        if 0x1000 <= value < 0x1000000:
            found.add(value)
    return found


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--coverage", action="store_true", help="list uncovered coordinates per topic")
    args = parser.parse_args()
    knowledge = knowledge_dir()
    errors: list[str] = []

    docs = sorted(p.name for p in knowledge.glob("*.md"))
    topics = json.loads((knowledge / "topics.json").read_text(encoding="utf-8"))["topics"]
    scopes = {d["file"] for d in json.loads((knowledge / "document-scopes.json").read_text(encoding="utf-8"))["documents"]}
    owner: dict[str, list[str]] = {}
    for topic in topics:
        for doc in topic["docs"]:
            owner.setdefault(doc, []).append(topic["id"])
    for doc in docs:
        if len(owner.get(doc, [])) != 1:
            errors.append(f"{doc}: needs exactly one topic in topics.json, has {owner.get(doc, [])}")
        if doc not in scopes:
            errors.append(f"{doc}: missing from document-scopes.json")
    for doc in owner:
        if doc not in docs:
            errors.append(f"topics.json lists missing document {doc}")

    curated = [knowledge / "vanilla" / "INDEX.md", knowledge / "mods" / "INDEX.md"]
    curated += [knowledge / side / f"{t['id']}.md" for t in topics for side in ("vanilla", "mods")]
    for path in curated:
        if not path.is_file():
            errors.append(f"missing curated file {path.relative_to(BASELINE)}")
    curated = [p for p in curated if p.is_file()] + [BASELINE / "CURRENT.md"]

    for path in [*curated, *(knowledge / d for d in docs)]:
        text = path.read_text(encoding="utf-8")
        if repair_text(text)[1]:
            errors.append(f"{path.relative_to(BASELINE)}: mojibake (run repair_mojibake.py)")
    for path in curated:
        raw = path.read_bytes()
        if raw.count(b"\n") != raw.count(b"\r\n"):
            errors.append(f"{path.relative_to(BASELINE)}: naked LF")
        for target in LINK.findall(path.read_text(encoding="utf-8")):
            if re.match(r"[a-z]+:", target):
                continue
            file_part, _, anchor = target.partition("#")
            resolved = (path.parent / file_part).resolve() if file_part else path
            if not resolved.exists():
                errors.append(f"{path.relative_to(BASELINE)}: broken link {target}")
            elif anchor and resolved.suffix == ".md" and anchor not in anchors(resolved):
                errors.append(f"{path.relative_to(BASELINE)}: unknown anchor {target}")

    for topic in topics:
        path = knowledge / "vanilla" / f"{topic['id']}.md"
        if not path.is_file():
            continue
        text = path.read_text(encoding="utf-8")
        for doc in topic["docs"]:
            if f"../{doc}" not in text:
                errors.append(f"vanilla/{topic['id']}.md: no link to its audit document {doc}")
        if args.coverage:
            covered = coordinates(text)
            source = set()
            for doc in topic["docs"]:
                source |= coordinates((knowledge / doc).read_text(encoding="utf-8"))
            missing = sorted(source - covered)
            print(f"{topic['id']}: {len(source) - len(missing)}/{len(source)} coordinates mentioned; missing {' '.join(hex(v) for v in missing)}")

    for message in errors:
        print("ERROR", message)
    print(f"{len(docs)} audit documents, {len(topics)} topics, {len(errors)} errors")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
