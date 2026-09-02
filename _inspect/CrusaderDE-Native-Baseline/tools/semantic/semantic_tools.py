#!/usr/bin/env python3
"""Deterministic helpers for the hash-bound CrusaderDE semantic baseline."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import sqlite3
import struct
import sys
import xml.etree.ElementTree as ET
from collections import Counter, defaultdict, deque
from pathlib import Path


CRLF = "\r\n"


def read_jsonl(path: Path):
    if not path.exists():
        raise FileNotFoundError(f"Required JSONL input does not exist: {path}")
    with path.open("r", encoding="utf-8") as handle:
        return [json.loads(line) for line in handle if line.strip()]


def write_jsonl(path: Path, rows):
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="") as handle:
        for row in rows:
            handle.write(json.dumps(row, ensure_ascii=False, separators=(",", ":")) + CRLF)


def write_text(path: Path, value: str):
    path.parent.mkdir(parents=True, exist_ok=True)
    normalized = value.replace("\r\n", "\n").replace("\r", "\n").replace("\n", CRLF)
    with path.open("w", encoding="utf-8", newline="") as handle:
        handle.write(normalized)


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest().upper()


def parse_int(value):
    if value is None:
        return None
    if isinstance(value, int):
        return value
    return int(value, 0)


def load_pe_image(path: Path):
    data = path.read_bytes()
    if data[:2] != b"MZ":
        raise ValueError(f"Not a PE file: {path}")
    pe_offset = struct.unpack_from("<I", data, 0x3C)[0]
    if data[pe_offset:pe_offset + 4] != b"PE\0\0":
        raise ValueError(f"Invalid PE signature: {path}")
    coff = pe_offset + 4
    section_count = struct.unpack_from("<H", data, coff + 2)[0]
    optional_size = struct.unpack_from("<H", data, coff + 16)[0]
    optional = coff + 20
    magic = struct.unpack_from("<H", data, optional)[0]
    if magic != 0x20B:
        raise ValueError(f"Expected PE32+ image: {path}")
    image_base = struct.unpack_from("<Q", data, optional + 24)[0]
    size_of_image = struct.unpack_from("<I", data, optional + 56)[0]
    size_of_headers = struct.unpack_from("<I", data, optional + 60)[0]
    image = bytearray(size_of_image)
    image[:min(size_of_headers, len(data))] = data[:min(size_of_headers, len(data))]
    sections = []
    section_table = optional + optional_size
    for index in range(section_count):
        offset = section_table + index * 40
        name = data[offset:offset + 8].split(b"\0", 1)[0].decode("ascii", "replace")
        virtual_size, virtual_address, raw_size, raw_pointer = struct.unpack_from("<IIII", data, offset + 8)
        copy_size = min(raw_size, max(0, len(data) - raw_pointer), max(0, len(image) - virtual_address))
        if copy_size:
            image[virtual_address:virtual_address + copy_size] = data[raw_pointer:raw_pointer + copy_size]
        sections.append({
            "name": name,
            "virtualAddress": virtual_address,
            "virtualSize": virtual_size,
            "rawSize": raw_size,
            "rawPointer": raw_pointer,
        })
    return bytes(image), image_base, sections


def compile_pattern(pattern: str):
    values = []
    masks = []
    for token in pattern.split():
        if token in {"?", "??"}:
            values.append(0)
            masks.append(False)
        else:
            values.append(int(token, 16))
            masks.append(True)
    return bytes(values), masks


def find_pattern(image: bytes, values: bytes, masks: list[bool]):
    concrete = [index for index, used in enumerate(masks) if used]
    if not concrete:
        return []
    anchor = concrete[0]
    anchor_byte = bytes([values[anchor]])
    matches = []
    start = 0
    limit = len(image) - len(values)
    while start <= limit:
        found = image.find(anchor_byte, start + anchor)
        if found < 0:
            break
        candidate = found - anchor
        if candidate >= start and candidate <= limit and all(image[candidate + i] == values[i] for i in concrete):
            matches.append(candidate)
        start = max(start + 1, candidate + 1)
    return matches


def section_for_rva(sections, rva):
    for section in sections:
        size = max(section["virtualSize"], section["rawSize"])
        if section["virtualAddress"] <= rva < section["virtualAddress"] + size:
            return section["name"]
    return None


def command_scan_aobs(args):
    patterns = read_jsonl(Path(args.patterns))
    rows = []
    label_lines = ["rva\tsymbol\tsourcePath\tsourceLine\tpattern\n"]
    for binary_spec in args.binary:
        binary_hash, binary_path = binary_spec.split("=", 1)
        path = Path(binary_path)
        if sha256_file(path) != binary_hash.upper():
            raise ValueError(f"Hash mismatch for {path}")
        image, image_base, sections = load_pe_image(path)
        for pattern in patterns:
            values, masks = compile_pattern(pattern["pattern"])
            matches = find_pattern(image, values, masks)
            for rva in matches:
                rows.append({
                    **pattern,
                    "binaryHash": binary_hash.upper(),
                    "matchCount": len(matches),
                    "address": f"0x{image_base + rva:X}",
                    "rva": f"0x{rva:X}",
                    "section": section_for_rva(sections, rva),
                    "unique": len(matches) == 1,
                })
            if not matches:
                rows.append({
                    **pattern,
                    "binaryHash": binary_hash.upper(),
                    "matchCount": 0,
                    "address": None,
                    "rva": None,
                    "section": None,
                    "unique": False,
                })
            if binary_hash.upper() == args.current_hash.upper() and pattern.get("directFunction") and len(matches) == 1:
                rva = matches[0]
                clean = re.sub(r"[^A-Za-z0-9_:$@?]", "_", pattern.get("symbol") or "unknown")
                label_lines.append(
                    f"0x{rva:X}\t{clean}\t{pattern['sourcePath']}\t{pattern['sourceLine']}\t{pattern['pattern']}\n")
    write_jsonl(Path(args.output), rows)
    write_text(Path(args.labels), "".join(label_lines))
    counts = Counter((row["binaryHash"], row["matchCount"]) for row in rows)
    print(json.dumps({"records": len(rows), "counts": {str(key): value for key, value in counts.items()}}))


def command_snapshot(args):
    root = Path(args.root).resolve()
    excludes = [part.replace("\\", "/").strip("/") for part in args.exclude]
    rows = []
    for path in sorted((item for item in root.rglob("*") if item.is_file()), key=lambda item: item.as_posix().lower()):
        relative = path.relative_to(root).as_posix()
        if any(relative == excluded or relative.startswith(excluded + "/") for excluded in excludes):
            continue
        rows.append({"path": relative, "bytes": path.stat().st_size, "sha256": sha256_file(path)})
    write_jsonl(Path(args.output), rows)
    print(json.dumps({"root": str(root), "files": len(rows)}))


def command_combine_headers(args):
    source = Path(args.source).resolve()
    destination = Path(args.destination).resolve()
    copied = destination.parent / "headers"
    copied.mkdir(parents=True, exist_ok=True)
    includes = []
    manifest = []
    for path in sorted(source.glob("*.h"), key=lambda item: item.name.lower()):
        target = copied / path.name
        content = path.read_text(encoding="utf-8-sig", errors="strict")
        write_text(target, content)
        manifest.append({"sourcePath": str(path), "copiedPath": target.name, "sha256": sha256_file(path)})
        includes.append(f'#include "headers/{path.name}"')
    preamble = """#ifndef SERPS_SHCDE_SEMANTIC_TYPES_H
#define SERPS_SHCDE_SEMANTIC_TYPES_H
typedef signed char int8_t;
typedef unsigned char uint8_t;
typedef signed short int16_t;
typedef unsigned short uint16_t;
typedef signed int int32_t;
typedef unsigned int uint32_t;
typedef signed long long int64_t;
typedef unsigned long long uint64_t;
"""
    write_text(destination, preamble + "\n" + "\n".join(includes) + "\n#endif\n")
    write_jsonl(destination.parent / "header-manifest.jsonl", manifest)
    print(json.dumps({"headers": len(manifest), "combined": str(destination)}))


def sanitize_header(text: str, source_name: str):
    output = [f"/* Derived from {source_name}; C++ syntax normalized for Ghidra CParser. */"]
    active = None
    active_kind = None
    waiting_for_brace = False
    depth = 0
    for original in text.replace("\r\n", "\n").replace("\r", "\n").split("\n"):
        line = original
        stripped = line.strip()
        if stripped.startswith("#ifndef") or stripped.startswith("#define") or stripped.startswith("#endif"):
            continue
        if stripped in {"public:", "private:", "protected:"}:
            continue
        enum_match = re.match(r"^(\s*)enum\s+([A-Za-z_]\w*)(?:\s*:\s*[A-Za-z_]\w*)?\s*$", line)
        class_match = re.match(r"^(\s*)class\s+([A-Za-z_]\w*)\s*$", line)
        if enum_match:
            active = enum_match.group(2)
            active_kind = "enum"
            waiting_for_brace = True
            depth = 0
            line = f"{enum_match.group(1)}typedef enum {active}"
        elif class_match:
            active = class_match.group(2)
            active_kind = "struct"
            waiting_for_brace = True
            depth = 0
            line = f"{class_match.group(1)}typedef struct {active}"
        elif active and active_kind == "enum" and depth > 0:
            member = re.match(r"^(\s*)([A-Za-z_]\w*)(\s*(?:=|,|$).*)$", line)
            if member and not member.group(2).startswith(active + "_"):
                line = f"{member.group(1)}{active}_{member.group(2)}{member.group(3)}"
        # C permits the typedef name directly; Ghidra's C parser does not
        # understand C++-style `class Name` field declarations.
        line = re.sub(r"\bclass\s+([A-Za-z_]\w*)", r"\1", line)
        if active:
            opened = line.count("{")
            closed = line.count("}")
            if waiting_for_brace and opened:
                waiting_for_brace = False
            depth += opened - closed
            if not waiting_for_brace and depth == 0 and re.match(r"^\s*};\s*$", line):
                line = re.sub(r"};", f"}} {active};", line, count=1)
                active = None
                active_kind = None
        output.append(line)
    return "\n".join(output)


def command_sanitize_headers(args):
    source = Path(args.source)
    # ReClassExports uses the project-local enums declared by Custom.h.
    # Keep all small standalone enum headers ahead of the large ReClass layout.
    order = [
        "Enums.h", "Custom.h", "AILordMessageType.h", "RationsMode.h",
        "TaxesMode.h", "TilePropertyFlags.h", "TileType.h", "TribeAICommand.h",
        "ReClassExports.h", "engineinterface.h",
    ]
    paths = {path.name: path for path in source.glob("*.h")}
    ordered = [paths.pop(name) for name in order if name in paths]
    ordered.extend(sorted(paths.values(), key=lambda item: item.name.lower()))
    preamble = """#ifndef SERPS_SHCDE_GHIDRA_TYPES_H
#define SERPS_SHCDE_GHIDRA_TYPES_H
typedef signed char int8_t;
typedef unsigned char uint8_t;
typedef signed short int16_t;
typedef unsigned short uint16_t;
typedef signed int int32_t;
typedef unsigned int uint32_t;
typedef signed long long int64_t;
typedef unsigned long long uint64_t;
"""
    pieces = [preamble]
    manifest = []
    for path in ordered:
        content = path.read_text(encoding="utf-8-sig")
        pieces.append(sanitize_header(content, path.name))
        manifest.append({"path": path.name, "sha256": sha256_file(path)})
    pieces.append("#endif\n")
    write_text(Path(args.output), "\n\n".join(pieces))
    write_jsonl(Path(args.manifest), manifest)
    print(json.dumps({"headers": len(ordered), "output": args.output}))


def command_managed_links(args):
    calls = read_jsonl(Path(args.calls))
    pinvokes = read_jsonl(Path(args.pinvokes))
    reverse = defaultdict(set)
    for call in calls:
        reverse[call["target"]].add(call["caller"])
    links = []
    for pinvoke in pinvokes:
        start = pinvoke["display"]
        queue = deque([(start, 0, [start])])
        seen = {start}
        while queue:
            target, distance, path = queue.popleft()
            if distance > 0:
                links.append({
                    "binaryHash": pinvoke["binaryHash"],
                    "managedMethod": target,
                    "pinvoke": start,
                    "entryPoint": pinvoke["entryPoint"],
                    "nativeAddress": pinvoke.get("nativeAddress"),
                    "nativeRva": pinvoke.get("nativeRva"),
                    "distance": distance,
                    "path": list(reversed(path)),
                    "confirmed": pinvoke.get("resolved", False),
                })
            for caller in sorted(reverse.get(target, ())):
                if caller not in seen:
                    seen.add(caller)
                    queue.append((caller, distance + 1, path + [caller]))
    write_jsonl(Path(args.output), links)
    prototype_lines = ["rva\tname\treturnType\tparameterTypes\tparameterNames\tsignature\n"]
    for pinvoke in pinvokes:
        if not pinvoke.get("resolved"):
            continue
        prototype_lines.append("\t".join([
            pinvoke["nativeRva"],
            pinvoke["entryPoint"],
            pinvoke["returnType"],
            "|".join(pinvoke["parameterTypes"]),
            "|".join(pinvoke["parameterNames"]),
            pinvoke["signature"].replace("\t", " "),
        ]) + "\n")
    write_text(Path(args.prototypes), "".join(prototype_lines))
    print(json.dumps({"transitiveLinks": len(links), "prototypes": len(pinvokes)}))


def command_xaml(args):
    managed = read_jsonl(Path(args.managed_methods))
    exact_names = defaultdict(list)
    for method in managed:
        exact_names[method["name"]].append(method)
    rows = []
    links = []
    root = Path(args.xaml_root)
    for path in sorted(root.rglob("*.xaml")):
        relative = path.relative_to(root).as_posix()
        try:
            tree = ET.parse(path)
            parse_error = None
        except Exception as exc:
            rows.append({"path": relative, "sha256": sha256_file(path), "valid": False, "error": str(exc)})
            continue
        bindings = set()
        controls = Counter()
        locale_keys = set()
        for element in tree.iter():
            controls[element.tag.split("}")[-1]] += 1
            for attr, value in element.attrib.items():
                for match in re.finditer(r"\{Binding\s+(?:Path=)?([A-Za-z_][A-Za-z0-9_.]*)", value):
                    bindings.add(match.group(1).split(".")[0])
                if "Command" in attr or attr.endswith("Name"):
                    candidate = value.strip()
                    if re.fullmatch(r"[A-Za-z_][A-Za-z0-9_]*", candidate):
                        bindings.add(candidate)
                locale_keys.update(re.findall(r"\b(?:TEXT|LOC|STR)_[A-Z0-9_]+\b", value))
        rows.append({
            "path": relative,
            "sha256": sha256_file(path),
            "valid": True,
            "error": parse_error,
            "rootElement": tree.getroot().tag.split("}")[-1],
            "controls": dict(sorted(controls.items())),
            "bindings": sorted(bindings),
            "localeKeys": sorted(locale_keys),
        })
        for binding in sorted(bindings):
            candidates = exact_names.get(binding, [])
            links.append({
                "xamlPath": relative,
                "binding": binding,
                "resolved": len(candidates) > 0,
                "ambiguous": len(candidates) > 1,
                "managedMethods": [candidate["display"] for candidate in candidates],
            })
    write_jsonl(Path(args.output), rows)
    write_jsonl(Path(args.links), links)
    print(json.dumps({"xamlFiles": len(rows), "valid": sum(row["valid"] for row in rows), "bindings": len(links)}))


def jaccard(left, right):
    left, right = set(left or []), set(right or [])
    if not left and not right:
        return 1.0
    if not left or not right:
        return 0.0
    return len(left & right) / len(left | right)


def similarity(old, new):
    size_old, size_new = max(1, old.get("size", 1)), max(1, new.get("size", 1))
    size_score = min(size_old, size_new) / max(size_old, size_new)
    block_old, block_new = max(1, old.get("blockCount", 1)), max(1, new.get("blockCount", 1))
    block_score = min(block_old, block_new) / max(block_old, block_new)
    mnemonic = 1.0 if old.get("mnemonicHash") == new.get("mnemonicHash") else 0.0
    return 0.35 * size_score + 0.20 * block_score + 0.20 * jaccard(old.get("strings"), new.get("strings")) + 0.15 * jaccard(old.get("imports"), new.get("imports")) + 0.10 * mnemonic


def command_compare(args):
    old_rows = read_jsonl(Path(args.old))
    new_rows = read_jsonl(Path(args.new))
    old_by_rva = {row["rva"]: row for row in old_rows}
    new_by_rva = {row["rva"]: row for row in new_rows}
    matches = []
    used_old, used_new = set(), set()

    def add_match(old, new, confidence, reason, score):
        used_old.add(old["rva"])
        used_new.add(new["rva"])
        matches.append({
            "oldBinaryHash": old.get("binaryHash"), "newBinaryHash": new.get("binaryHash"),
            "oldAddress": old["address"], "oldRva": old["rva"], "oldName": old["name"],
            "newAddress": new["address"], "newRva": new["rva"], "newName": new["name"],
            "confidence": confidence, "reason": reason, "score": round(score, 6),
            "changed": old.get("rawHash") != new.get("rawHash"),
        })

    old_exports = {row["name"]: row for row in old_rows if row["name"].startswith("DLL_")}
    new_exports = {row["name"]: row for row in new_rows if row["name"].startswith("DLL_")}
    for name in sorted(old_exports.keys() & new_exports.keys()):
        add_match(old_exports[name], new_exports[name], "confirmed", "same-export-name", 1.0)

    for field, reason in [("rawHash", "unique-raw-hash"), ("normalizedHash", "unique-normalized-hash-and-cfg")]:
        old_groups, new_groups = defaultdict(list), defaultdict(list)
        for row in old_rows:
            if row["rva"] not in used_old and row.get(field): old_groups[row[field]].append(row)
        for row in new_rows:
            if row["rva"] not in used_new and row.get(field): new_groups[row[field]].append(row)
        for value in sorted(old_groups.keys() & new_groups.keys()):
            if len(old_groups[value]) != 1 or len(new_groups[value]) != 1:
                continue
            old, new = old_groups[value][0], new_groups[value][0]
            if field == "normalizedHash" and old.get("blockCount") != new.get("blockCount"):
                continue
            add_match(old, new, "confirmed", reason, 1.0)

    remaining_old = [row for row in old_rows if row["rva"] not in used_old]
    remaining_new = [row for row in new_rows if row["rva"] not in used_new]
    old_best, new_best = {}, {}
    for old in remaining_old:
        candidates = sorted(((similarity(old, new), new) for new in remaining_new), key=lambda item: item[0], reverse=True)[:2]
        old_best[old["rva"]] = candidates
    for new in remaining_new:
        candidates = sorted(((similarity(old, new), old) for old in remaining_old), key=lambda item: item[0], reverse=True)[:2]
        new_best[new["rva"]] = candidates
    for old in remaining_old:
        candidates = old_best[old["rva"]]
        if not candidates:
            continue
        score, new = candidates[0]
        runner = candidates[1][0] if len(candidates) > 1 else 0.0
        reverse = new_best.get(new["rva"], [])
        mutual = reverse and reverse[0][1]["rva"] == old["rva"]
        corroborators = sum([
            bool(set(old.get("strings") or []) & set(new.get("strings") or [])),
            bool(set(old.get("imports") or []) & set(new.get("imports") or [])),
            min(old.get("size", 0), new.get("size", 0)) / max(1, max(old.get("size", 0), new.get("size", 0))) >= 0.95,
            old.get("blockCount") == new.get("blockCount"),
        ])
        if mutual and score >= 0.92 and score - runner >= 0.10 and corroborators >= 2:
            add_match(old, new, "probable", "mutual-structural-similarity", score)

    unmatched_old = [row for row in old_rows if row["rva"] not in used_old]
    unmatched_new = [row for row in new_rows if row["rva"] not in used_new]
    write_jsonl(Path(args.output) / "version-matches.jsonl", matches)
    write_jsonl(Path(args.output) / "removed-functions.jsonl", unmatched_old)
    write_jsonl(Path(args.output) / "added-functions.jsonl", unmatched_new)
    changed = [row for row in matches if row["changed"]]
    write_jsonl(Path(args.output) / "changed-functions.jsonl", changed)
    reference_diffs = []
    for match in matches:
        old = old_by_rva[match["oldRva"]]
        new = new_by_rva[match["newRva"]]
        old_data = sorted(set(old.get("dataRvas") or []))
        new_data = sorted(set(new.get("dataRvas") or []))
        old_strings = sorted(set(old.get("strings") or []))
        new_strings = sorted(set(new.get("strings") or []))
        if old_data != new_data or old_strings != new_strings:
            reference_diffs.append({
                "oldBinaryHash": match["oldBinaryHash"], "newBinaryHash": match["newBinaryHash"],
                "oldRva": match["oldRva"], "newRva": match["newRva"],
                "matchConfidence": match["confidence"],
                "addedDataRvas": sorted(set(new_data) - set(old_data)),
                "removedDataRvas": sorted(set(old_data) - set(new_data)),
                "addedStrings": sorted(set(new_strings) - set(old_strings)),
                "removedStrings": sorted(set(old_strings) - set(new_strings)),
            })
    write_jsonl(Path(args.output) / "semantic-reference-diff.jsonl", reference_diffs)
    summary = {
        "confirmed": sum(row["confidence"] == "confirmed" for row in matches),
        "probable": sum(row["confidence"] == "probable" for row in matches),
        "unchanged": len(matches) - len(changed), "changed": len(changed),
        "removed": len(unmatched_old), "added": len(unmatched_new),
    }
    report = """# CrusaderDE Version Comparison

## Result

""" + "\n".join(f"- {key}: {value}" for key, value in summary.items()) + """

`unchanged` and `changed` refer to safely matched function pairs. `removed` and `added` are deliberately not forced into a mapping.

## Confidence rules

- `confirmed`: same export name, a unique identical raw-byte hash, or a unique identical normalized-instruction hash with the same CFG.
- `probable`: mutual best match, similarity at least 0.92, at least 0.10 separation from the runner-up, plus at least two corroborators from strings, imports, CFG or at most 5 percent size difference.
- Everything else remains unmatched/candidate.

## Machine-readable files

- `version-matches.jsonl`: confirmed and probable one-to-one mappings
- `changed-functions.jsonl`: safely mapped functions whose raw hashes differ
- `removed-functions.jsonl`: historical functions without a safe mapping
- `added-functions.jsonl`: current functions without a safe mapping
- `semantic-reference-diff.jsonl`: changed global/data references and strings for safely mapped functions
"""
    write_text(Path(args.output) / "VERSION_DIFF.md", report)
    print(json.dumps(summary))


CLASSIFIERS = {
    "units": ("unit", "troop", "chimp", "soldier", "archer", "damage", "health"),
    "buildings": ("building", "castle", "wall", "gatehouse", "tower", "construction"),
    "trade": ("trade", "market", "price", "buy", "sell", "good"),
    "ai": ("ai", "lord", "aiv", "aic", "siege", "chore"),
    "map": ("map", "tile", "terrain", "pathfinding", "scenario", "trail"),
    "network": ("network", "multiplayer", "steam", "mp", "packet", "sync"),
    "sound": ("sound", "audio", "music", "speech", "sfx"),
    "ui": ("xaml", "hud", "frontend", "viewmodel", "menu", "cursor"),
}


def classify_function(row):
    text = " ".join([
        row.get("name") or "", row.get("comment") or "",
        " ".join(row.get("strings") or []), " ".join(row.get("callees") or []),
    ])
    return classify_text(text)


def classify_text(text):
    # Preserve deterministic matches inside CamelCase identifiers and paths
    # without resorting to fuzzy similarity.
    text = re.sub(r"(?<=[a-z0-9])(?=[A-Z])", " ", text).lower()
    result = []
    for category, words in CLASSIFIERS.items():
        evidence = sorted({word for word in words if re.search(rf"(?<![a-z0-9]){re.escape(word)}(?![a-z0-9])", text)})
        if evidence:
            result.append({"category": category, "evidence": evidence})
    return result


def command_build_index(args):
    semantic_dir = Path(args.semantic_dir)
    database = Path(args.database)
    database.parent.mkdir(parents=True, exist_ok=True)
    working_database = database.with_name(database.name + "-building")
    if working_database.exists():
        working_database.unlink()
    connection = sqlite3.connect(working_database)
    connection.execute("PRAGMA foreign_keys=ON")
    connection.executescript("""
        CREATE TABLE metadata(key TEXT PRIMARY KEY, value TEXT NOT NULL);
        CREATE TABLE binaries(hash TEXT PRIMARY KEY, role TEXT, path TEXT, size INTEGER);
        CREATE TABLE functions(binary_hash TEXT, address TEXT, rva TEXT, name TEXT, confidence TEXT, size INTEGER,
            signature TEXT, comment TEXT, raw_hash TEXT, normalized_hash TEXT, block_count INTEGER,
            pseudocode TEXT, PRIMARY KEY(binary_hash,rva));
        CREATE TABLE function_claims(claim_id TEXT PRIMARY KEY, binary_hash TEXT, function_rva TEXT,
            canonical_name TEXT, name_origin TEXT, identity_confidence TEXT, semantic_confidence TEXT,
            abi_confidence TEXT, signature TEXT, return_contract TEXT, flow_review TEXT,
            parameters TEXT, field_accesses TEXT, side_effects TEXT, caller_observations TEXT,
            counter_evidence TEXT, verified_by TEXT, payload TEXT);
        CREATE TABLE claim_evidence(claim_id TEXT, evidence_id TEXT, chain_id TEXT, kind TEXT,
            strength TEXT, source_path TEXT, source_file_hash TEXT, rva TEXT, address TEXT,
            summary TEXT, counter INTEGER, PRIMARY KEY(claim_id,evidence_id,counter));
        CREATE TABLE hook_spans(span_id TEXT PRIMARY KEY, claim_id TEXT, binary_hash TEXT,
            span_kind TEXT, start_rva TEXT, end_rva TEXT, original_bytes TEXT, instructions TEXT,
            external_entries TEXT, live_in TEXT, live_out TEXT, clobbers TEXT, stack_delta INTEGER,
            flags_contract TEXT, continuation TEXT, evidence TEXT, payload TEXT);
        CREATE TABLE api_contracts(contract_id TEXT PRIMARY KEY, binary_hash TEXT, status TEXT,
            producer TEXT, field_name TEXT, representation TEXT, consumer TEXT, conversion TEXT,
            script_extender_commit TEXT, evidence TEXT, payload TEXT);
        CREATE TABLE call_edges(binary_hash TEXT, caller_rva TEXT, callee_rva TEXT, callee_name TEXT);
        CREATE TABLE function_data_references(binary_hash TEXT, function_rva TEXT, target_rva TEXT);
        CREATE TABLE function_strings(binary_hash TEXT, function_rva TEXT, value TEXT);
        CREATE TABLE xrefs(binary_hash TEXT, from_rva TEXT, to_rva TEXT, type TEXT, source_function TEXT);
        CREATE TABLE strings(binary_hash TEXT, address TEXT, rva TEXT, value TEXT, encoding TEXT, xref_count INTEGER);
        CREATE TABLE imports(binary_hash TEXT, address TEXT, rva TEXT, name TEXT, namespace TEXT);
        CREATE TABLE exports(binary_hash TEXT, address TEXT, rva TEXT, name TEXT);
        CREATE TABLE globals(binary_hash TEXT, address TEXT, rva TEXT, name TEXT, data_type TEXT, reference_count INTEGER);
        CREATE TABLE managed_methods(binary_hash TEXT, token TEXT, declaring_type TEXT, name TEXT, display TEXT, signature TEXT, pinvoke INTEGER);
        CREATE TABLE pinvokes(binary_hash TEXT, token TEXT, display TEXT, entry_point TEXT, signature TEXT, native_address TEXT, native_rva TEXT, resolved INTEGER);
        CREATE TABLE managed_calls(binary_hash TEXT, caller_token TEXT, caller TEXT, target_token TEXT, target TEXT, il_offset TEXT, opcode TEXT);
        CREATE TABLE managed_native_links(binary_hash TEXT, managed_method TEXT, pinvoke TEXT, entry_point TEXT, native_rva TEXT, distance INTEGER, path TEXT, confirmed INTEGER);
        CREATE TABLE patterns(binary_hash TEXT, pattern TEXT, symbol TEXT, source_path TEXT, source_file_hash TEXT, source_line INTEGER, git_commit TEXT, context TEXT, resolution_kind TEXT, direct_function INTEGER, match_count INTEGER, address TEXT, rva TEXT, section TEXT, unique_match INTEGER);
        CREATE TABLE data_types(binary_hash TEXT, name TEXT, kind TEXT, length INTEGER, category TEXT, declaration TEXT, source_path TEXT);
        CREATE TABLE source_types(git_commit TEXT, source_path TEXT, source_file_hash TEXT, source_line INTEGER, kind TEXT, name TEXT, declaration TEXT);
        CREATE TABLE type_fields(git_commit TEXT, source_path TEXT, source_file_hash TEXT, source_line INTEGER, type_name TEXT, field_name TEXT, field_type TEXT, ordinal INTEGER, slot_span INTEGER, offset_evidence TEXT, declaration TEXT);
        CREATE TABLE vtable_members(git_commit TEXT, source_path TEXT, source_file_hash TEXT, source_line INTEGER, type_name TEXT, member_name TEXT, member_type TEXT, slot INTEGER, slot_span INTEGER, declaration TEXT);
        CREATE TABLE delegates(git_commit TEXT, source_path TEXT, source_file_hash TEXT, source_line INTEGER, name TEXT, return_type TEXT, parameters TEXT, signature TEXT);
        CREATE TABLE rtti_vtables(binary_hash TEXT, address TEXT, rva TEXT, name TEXT);
        CREATE TABLE xaml_resources(path TEXT PRIMARY KEY, sha256 TEXT, valid INTEGER, root_element TEXT, controls TEXT, bindings TEXT, locale_keys TEXT, error TEXT);
        CREATE TABLE xaml_links(xaml_path TEXT, binding TEXT, resolved INTEGER, ambiguous INTEGER, managed_methods TEXT);
        CREATE TABLE classifications(binary_hash TEXT, function_rva TEXT, category TEXT, evidence TEXT);
        CREATE TABLE version_matches(old_hash TEXT, new_hash TEXT, old_rva TEXT, new_rva TEXT, confidence TEXT, reason TEXT, score REAL, changed INTEGER);
        CREATE VIRTUAL TABLE function_search USING fts5(binary_hash, rva, name, signature, comment, pseudocode, strings, categories);
    """)
    current_hash = args.current_hash.upper()
    old_hash = args.old_hash.upper()
    connection.executemany("INSERT INTO binaries VALUES(?,?,?,?)", [
        (current_hash, "current-native", args.current_native, int(args.current_native_size)),
        (old_hash, "historical-native", args.old_native, int(args.old_native_size)),
        (args.managed_hash.upper(), "current-managed", args.managed_assembly, int(args.managed_size)),
    ])
    connection.executemany("INSERT INTO metadata VALUES(?,?)", [("schema_version", "2"), ("current_native_hash", current_hash), ("managed_hash", args.managed_hash.upper())])

    decomp_map = {}
    decomp_path = semantic_dir / "exports" / "semantic-decompiled-functions.c"
    if decomp_path.exists():
        text = decomp_path.read_text(encoding="utf-8")
        parts = re.split(r"(?=/\* FUNCTION )", text)
        for part in parts:
            match = re.match(r"/\* FUNCTION .*? RVA=(0x[0-9A-Fa-f]+).*?\*/", part)
            if match: decomp_map[match.group(1).upper()] = part

    all_functions = []
    for path, binary_hash in [
        (semantic_dir / "exports" / "semantic-functions.jsonl", current_hash),
        (Path(args.old_exports) / "semantic-functions.jsonl", old_hash),
    ]:
        for row in read_jsonl(path):
            row["binaryHash"] = binary_hash
            all_functions.append(row)
            classifications = classify_function(row)
            connection.execute("INSERT INTO functions VALUES(?,?,?,?,?,?,?,?,?,?,?,?)", (
                binary_hash, row["address"], row["rva"], row["name"], row.get("confidence", "candidate"), row.get("size"),
                row.get("signature"), row.get("comment"), row.get("rawHash"), row.get("normalizedHash"), row.get("blockCount"),
                decomp_map.get(row["rva"].upper()) if binary_hash == current_hash else None,
            ))
            for item in classifications:
                connection.execute("INSERT INTO classifications VALUES(?,?,?,?)", (binary_hash, row["rva"], item["category"], json.dumps(item["evidence"])))
            callee_rvas = row.get("calleeRvas") or []
            callees = row.get("callees") or []
            for index, callee in enumerate(callees):
                callee_rva = callee_rvas[index] if index < len(callee_rvas) else None
                connection.execute("INSERT INTO call_edges VALUES(?,?,?,?)", (binary_hash, row["rva"], callee_rva, callee))
            for value in row.get("strings") or []:
                connection.execute("INSERT INTO function_strings VALUES(?,?,?)", (binary_hash, row["rva"], value))
            for target_rva in row.get("dataRvas") or []:
                connection.execute("INSERT INTO function_data_references VALUES(?,?,?)", (binary_hash, row["rva"], target_rva))
            connection.execute("INSERT INTO function_search VALUES(?,?,?,?,?,?,?,?)", (
                binary_hash, row["rva"], row["name"], row.get("signature"), row.get("comment"),
                decomp_map.get(row["rva"].upper(), ""), "\n".join(row.get("strings") or []),
                " ".join(item["category"] for item in classifications),
            ))

    for claim in read_jsonl(Path(args.function_claims)):
        connection.execute("INSERT INTO function_claims VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)", (
            claim.get("claimId"), claim.get("binaryHash"), claim.get("functionRva"),
            claim.get("canonicalName"), claim.get("nameOrigin"), claim.get("identityConfidence"),
            claim.get("semanticConfidence"), claim.get("abiConfidence"), claim.get("signature"),
            json.dumps(claim.get("return"), ensure_ascii=False), claim.get("flowReview"),
            json.dumps(claim.get("parameters") or [], ensure_ascii=False),
            json.dumps(claim.get("fieldAccesses") or [], ensure_ascii=False),
            json.dumps(claim.get("sideEffects") or [], ensure_ascii=False),
            json.dumps(claim.get("callerObservations") or [], ensure_ascii=False),
            json.dumps(claim.get("counterEvidence") or [], ensure_ascii=False),
            json.dumps(claim.get("verifiedBy") or [], ensure_ascii=False),
            json.dumps(claim, ensure_ascii=False),
        ))
        for evidence in claim.get("evidence") or []:
            connection.execute("INSERT INTO claim_evidence VALUES(?,?,?,?,?,?,?,?,?,?,?)", (
                claim.get("claimId"), evidence.get("id"), evidence.get("chain"), evidence.get("kind"),
                evidence.get("strength"), evidence.get("source"), evidence.get("sourceFileHash"),
                evidence.get("rva"), evidence.get("address"), evidence.get("summary"), 0,
            ))
        for index, evidence in enumerate(claim.get("counterEvidence") or []):
            connection.execute("INSERT INTO claim_evidence VALUES(?,?,?,?,?,?,?,?,?,?,?)", (
                claim.get("claimId"), evidence.get("id") or f"counter-{index + 1}",
                evidence.get("chain"), evidence.get("kind"), evidence.get("strength"),
                evidence.get("source"), evidence.get("sourceFileHash"), evidence.get("rva"),
                evidence.get("address"), evidence.get("summary"), 1,
            ))
    for span in read_jsonl(Path(args.hook_spans)):
        connection.execute("INSERT INTO hook_spans VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)", (
            span.get("spanId"), span.get("claimId"), span.get("binaryHash"), span.get("spanKind"),
            span.get("startRva"), span.get("endRva"), span.get("originalBytes"),
            json.dumps(span.get("instructions") or [], ensure_ascii=False),
            json.dumps(span.get("externalEntries") or [], ensure_ascii=False),
            json.dumps(span.get("liveIn") or [], ensure_ascii=False),
            json.dumps(span.get("liveOut") or [], ensure_ascii=False),
            json.dumps(span.get("clobbers") or [], ensure_ascii=False), span.get("stackDelta"),
            span.get("flags"), span.get("continuation"),
            json.dumps(span.get("evidence") or [], ensure_ascii=False),
            json.dumps(span, ensure_ascii=False),
        ))
    for contract in read_jsonl(Path(args.api_contracts)):
        connection.execute("INSERT INTO api_contracts VALUES(?,?,?,?,?,?,?,?,?,?,?)", (
            contract.get("contractId"), contract.get("binaryHash"), contract.get("status"),
            contract.get("producer"), contract.get("field"), contract.get("representation"),
            contract.get("consumer"), contract.get("conversion"), contract.get("scriptExtenderCommit"),
            json.dumps(contract.get("evidence") or [], ensure_ascii=False),
            json.dumps(contract, ensure_ascii=False),
        ))

    raw = Path(args.raw_exports)
    for row in read_jsonl(raw / "xrefs.jsonl"):
        connection.execute("INSERT INTO xrefs VALUES(?,?,?,?,?)", (current_hash, row.get("fromRva"), row.get("toRva"), row.get("type"), row.get("sourceFunction")))
    for row in read_jsonl(raw / "strings.jsonl"):
        connection.execute("INSERT INTO strings VALUES(?,?,?,?,?,?)", (current_hash, row.get("address"), row.get("rva"), row.get("value"), row.get("encoding"), row.get("xrefCount")))
    for table, filename in [("imports", "imports.jsonl"), ("exports", "exports.jsonl")]:
        for row in read_jsonl(raw / filename):
            if table == "imports":
                connection.execute("INSERT INTO imports VALUES(?,?,?,?,?)", (current_hash, row.get("address"), row.get("rva"), row.get("name"), row.get("namespace")))
            else:
                connection.execute("INSERT INTO exports VALUES(?,?,?,?)", (current_hash, row.get("address"), row.get("rva"), row.get("name")))
    for row in read_jsonl(semantic_dir / "exports" / "globals.jsonl"):
        connection.execute("INSERT INTO globals VALUES(?,?,?,?,?,?)", (current_hash, row.get("address"), row.get("rva"), row.get("name"), row.get("dataType"), row.get("referenceCount")))
    for row in read_jsonl(Path(args.managed_dir) / "managed-methods.jsonl"):
        connection.execute("INSERT INTO managed_methods VALUES(?,?,?,?,?,?,?)", (row.get("binaryHash"), row.get("token"), row.get("declaringType"), row.get("name"), row.get("display"), row.get("signature"), int(row.get("pinvoke", False))))
    for row in read_jsonl(Path(args.managed_dir) / "pinvokes.jsonl"):
        connection.execute("INSERT INTO pinvokes VALUES(?,?,?,?,?,?,?,?)", (row.get("binaryHash"), row.get("token"), row.get("display"), row.get("entryPoint"), row.get("signature"), row.get("nativeAddress"), row.get("nativeRva"), int(row.get("resolved", False))))
    for row in read_jsonl(Path(args.managed_dir) / "managed-calls.jsonl"):
        connection.execute("INSERT INTO managed_calls VALUES(?,?,?,?,?,?,?)", (row.get("binaryHash"), row.get("callerToken"), row.get("caller"), row.get("targetToken"), row.get("target"), row.get("ilOffset"), row.get("opcode")))
    for row in read_jsonl(Path(args.managed_dir) / "managed-native-links.jsonl"):
        connection.execute("INSERT INTO managed_native_links VALUES(?,?,?,?,?,?,?,?)", (row.get("binaryHash"), row.get("managedMethod"), row.get("pinvoke"), row.get("entryPoint"), row.get("nativeRva"), row.get("distance"), json.dumps(row.get("path")), int(row.get("confirmed", False))))
    for row in read_jsonl(Path(args.patterns)):
        connection.execute("INSERT INTO patterns VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)", (row.get("binaryHash"), row.get("pattern"), row.get("symbol"), row.get("sourcePath"), row.get("sourceFileHash"), row.get("sourceLine"), row.get("gitCommit"), row.get("context"), row.get("resolutionKind"), int(row.get("directFunction", False)), row.get("matchCount"), row.get("address"), row.get("rva"), row.get("section"), int(row.get("unique", False))))
    for row in read_jsonl(semantic_dir / "exports" / "data-types.jsonl"):
        connection.execute("INSERT INTO data_types VALUES(?,?,?,?,?,?,?)", (current_hash, row.get("name"), row.get("kind"), row.get("length"), row.get("category"), row.get("declaration"), row.get("sourcePath")))
    for row in read_jsonl(Path(args.source_types)):
        connection.execute("INSERT INTO source_types VALUES(?,?,?,?,?,?,?)", (row.get("gitCommit"), row.get("sourcePath"), row.get("sourceFileHash"), row.get("sourceLine"), row.get("kind"), row.get("name"), row.get("declaration")))
    for row in read_jsonl(Path(args.type_fields)):
        connection.execute("INSERT INTO type_fields VALUES(?,?,?,?,?,?,?,?,?,?,?)", (row.get("gitCommit"), row.get("sourcePath"), row.get("sourceFileHash"), row.get("sourceLine"), row.get("typeName"), row.get("fieldName"), row.get("fieldType"), row.get("ordinal"), row.get("slotSpan"), row.get("offsetEvidence"), row.get("declaration")))
    for row in read_jsonl(Path(args.vtable_members)):
        connection.execute("INSERT INTO vtable_members VALUES(?,?,?,?,?,?,?,?,?,?)", (row.get("gitCommit"), row.get("sourcePath"), row.get("sourceFileHash"), row.get("sourceLine"), row.get("typeName"), row.get("fieldName"), row.get("fieldType"), row.get("ordinal"), row.get("slotSpan"), row.get("declaration")))
    for row in read_jsonl(Path(args.delegates)):
        connection.execute("INSERT INTO delegates VALUES(?,?,?,?,?,?,?,?)", (row.get("gitCommit"), row.get("sourcePath"), row.get("sourceFileHash"), row.get("sourceLine"), row.get("name"), row.get("returnType"), json.dumps(row.get("parameters")), row.get("signature")))
    for row in read_jsonl(Path(args.rtti_vtables)):
        connection.execute("INSERT INTO rtti_vtables VALUES(?,?,?,?)", (current_hash, row.get("address"), row.get("rva"), row.get("name")))
    for row in read_jsonl(Path(args.xaml)):
        connection.execute("INSERT INTO xaml_resources VALUES(?,?,?,?,?,?,?,?)", (row.get("path"), row.get("sha256"), int(row.get("valid", False)), row.get("rootElement"), json.dumps(row.get("controls")), json.dumps(row.get("bindings")), json.dumps(row.get("localeKeys")), row.get("error")))
    for row in read_jsonl(Path(args.xaml_links)):
        connection.execute("INSERT INTO xaml_links VALUES(?,?,?,?,?)", (row.get("xamlPath"), row.get("binding"), int(row.get("resolved", False)), int(row.get("ambiguous", False)), json.dumps(row.get("managedMethods"))))
    for row in read_jsonl(Path(args.version_matches)):
        connection.execute("INSERT INTO version_matches VALUES(?,?,?,?,?,?,?,?)", (row.get("oldBinaryHash"), row.get("newBinaryHash"), row.get("oldRva"), row.get("newRva"), row.get("confidence"), row.get("reason"), row.get("score"), int(row.get("changed", False))))
    # Enrich deterministic native subsystem tags with exact managed call-chain
    # names. This never renames a native function and retains its evidence.
    for native_rva, managed_text in connection.execute(
        "SELECT native_rva,group_concat(managed_method || ' ' || pinvoke,' ') "
        "FROM managed_native_links GROUP BY native_rva"):
        for item in classify_text(managed_text or ""):
            exists = connection.execute(
                "SELECT 1 FROM classifications WHERE binary_hash=? AND function_rva=? AND category=?",
                (current_hash, native_rva, item["category"])).fetchone()
            if not exists:
                evidence = {"source": "managed-callchains", "terms": item["evidence"]}
                connection.execute("INSERT INTO classifications VALUES(?,?,?,?)", (current_hash, native_rva, item["category"], json.dumps(evidence)))
    connection.commit()
    integrity = connection.execute("PRAGMA integrity_check").fetchone()[0]
    counts = {table: connection.execute(f"SELECT COUNT(*) FROM {table}").fetchone()[0] for table in ["functions", "function_claims", "claim_evidence", "hook_spans", "api_contracts", "function_data_references", "xrefs", "strings", "managed_methods", "pinvokes", "managed_native_links", "patterns", "source_types", "type_fields", "vtable_members", "xaml_resources", "version_matches"]}
    connection.close()
    os.replace(working_database, database)
    print(json.dumps({"integrity": integrity, "counts": counts}))


def build_parser():
    parser = argparse.ArgumentParser()
    sub = parser.add_subparsers(dest="command", required=True)
    scan = sub.add_parser("scan-aobs")
    scan.add_argument("--patterns", required=True); scan.add_argument("--binary", action="append", required=True)
    scan.add_argument("--current-hash", required=True); scan.add_argument("--output", required=True); scan.add_argument("--labels", required=True)
    scan.set_defaults(func=command_scan_aobs)
    snapshot = sub.add_parser("snapshot")
    snapshot.add_argument("--root", required=True); snapshot.add_argument("--exclude", action="append", default=[]); snapshot.add_argument("--output", required=True)
    snapshot.set_defaults(func=command_snapshot)
    headers = sub.add_parser("combine-headers")
    headers.add_argument("--source", required=True); headers.add_argument("--destination", required=True)
    headers.set_defaults(func=command_combine_headers)
    sanitize = sub.add_parser("sanitize-headers")
    sanitize.add_argument("--source", required=True); sanitize.add_argument("--output", required=True); sanitize.add_argument("--manifest", required=True)
    sanitize.set_defaults(func=command_sanitize_headers)
    links = sub.add_parser("managed-links")
    links.add_argument("--calls", required=True); links.add_argument("--pinvokes", required=True); links.add_argument("--output", required=True); links.add_argument("--prototypes", required=True)
    links.set_defaults(func=command_managed_links)
    xaml = sub.add_parser("xaml")
    xaml.add_argument("--xaml-root", required=True); xaml.add_argument("--managed-methods", required=True); xaml.add_argument("--output", required=True); xaml.add_argument("--links", required=True)
    xaml.set_defaults(func=command_xaml)
    compare = sub.add_parser("compare")
    compare.add_argument("--old", required=True); compare.add_argument("--new", required=True); compare.add_argument("--output", required=True)
    compare.set_defaults(func=command_compare)
    index = sub.add_parser("build-index")
    for name in ["semantic-dir", "database", "current-hash", "old-hash", "managed-hash", "current-native", "old-native", "managed-assembly", "current-native-size", "old-native-size", "managed-size", "old-exports", "raw-exports", "managed-dir", "patterns", "source-types", "type-fields", "vtable-members", "delegates", "rtti-vtables", "xaml", "xaml-links", "version-matches", "function-claims", "hook-spans", "api-contracts"]:
        index.add_argument(f"--{name}", required=True)
    index.set_defaults(func=command_build_index)
    return parser


def main():
    args = build_parser().parse_args()
    args.func(args)


if __name__ == "__main__":
    main()
