#!/usr/bin/env python3
"""Fail-closed validation for the hash-bound CrusaderDE semantic baseline."""

from __future__ import annotations

import argparse
import hashlib
import json
import sqlite3
import struct
import xml.etree.ElementTree as ET
from pathlib import Path


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest().upper()


def jsonl(path: Path):
    if not path.is_file():
        raise FileNotFoundError(path)
    with path.open("r", encoding="utf-8") as handle:
        return [json.loads(line) for line in handle if line.strip()]


def pe_identity(path: Path):
    data = path.read_bytes()
    pe = struct.unpack_from("<I", data, 0x3C)[0]
    optional = pe + 24
    if data[:2] != b"MZ" or data[pe:pe + 4] != b"PE\0\0" or struct.unpack_from("<H", data, optional)[0] != 0x20B:
        raise ValueError(f"Not a PE32+ image: {path}")
    return struct.unpack_from("<Q", data, optional + 24)[0], struct.unpack_from("<I", data, optional + 56)[0]


def snapshot(root: Path, excludes: tuple[str, ...]):
    result = {}
    for path in root.rglob("*"):
        if not path.is_file():
            continue
        relative = path.relative_to(root).as_posix()
        if any(relative == item or relative.startswith(item + "/") for item in excludes):
            continue
        result[relative] = (path.stat().st_size, sha256(path))
    return result


def stored_snapshot(path: Path):
    return {row["path"]: (row["bytes"], row["sha256"]) for row in jsonl(path)}


def main():
    parser = argparse.ArgumentParser()
    for name in ["semantic", "comparison", "baseline-root", "database", "native", "managed", "old-native", "raw-root", "raw-before", "se-root", "se-before", "current-hash", "managed-hash", "old-hash"]:
        parser.add_argument(f"--{name}", required=True)
    parser.add_argument("--output", required=True)
    args = parser.parse_args()

    semantic = Path(args.semantic)
    comparison = Path(args.comparison)
    path_limit = 240
    measured_paths = [path.resolve() for path in Path(args.baseline_root).rglob("*") if path.is_file()]
    longest_path = max(measured_paths, key=lambda path: len(str(path)))
    over_limit = [path for path in measured_paths if len(str(path)) > path_limit]
    if over_limit:
        details = ", ".join(f"{len(str(path))}:{path}" for path in sorted(over_limit, key=lambda path: len(str(path)), reverse=True)[:10])
        raise ValueError(f"Generated path limit exceeded ({path_limit}): {details}")
    expected = {
        Path(args.native): args.current_hash.upper(),
        Path(args.managed): args.managed_hash.upper(),
        Path(args.old_native): args.old_hash.upper(),
    }
    actual_hashes = {str(path): sha256(path) for path in expected}
    for path, wanted in expected.items():
        if actual_hashes[str(path)] != wanted:
            raise ValueError(f"Hash mismatch for {path}: {actual_hashes[str(path)]} != {wanted}")

    raw_unchanged = snapshot(Path(args.raw_root), ("semantic",)) == stored_snapshot(Path(args.raw_before))
    se_unchanged = snapshot(Path(args.se_root), (".git",)) == stored_snapshot(Path(args.se_before))
    if not raw_unchanged or not se_unchanged:
        raise ValueError(f"Immutable input changed: rawBaseline={raw_unchanged}, scriptExtender={se_unchanged}")

    parsed_json = 0
    parsed_jsonl_records = 0
    for root in (semantic, comparison):
        for path in root.rglob("*.json"):
            json.loads(path.read_text(encoding="utf-8")); parsed_json += 1
        for path in root.rglob("*.jsonl"):
            parsed_jsonl_records += len(jsonl(path))

    pinvokes = jsonl(next(semantic.glob("managed/*/pinvokes.jsonl")))
    crusader_pinvokes = [row for row in pinvokes if str(row.get("module", "")).lower() in {"crusaderde", "crusaderde.dll"}]
    if len(crusader_pinvokes) != 77 or any(not row.get("resolved") for row in crusader_pinvokes):
        raise ValueError(f"Expected 77 resolved CrusaderDE P/Invokes, got {len(crusader_pinvokes)}")

    xaml_rows = jsonl(semantic / "resources" / "xaml-index.jsonl")
    if not xaml_rows or any(not row.get("valid") for row in xaml_rows):
        raise ValueError("One or more extracted XAML documents are invalid")
    for path in (semantic / "resources" / "xaml").rglob("*.xaml"):
        ET.parse(path)

    matches = jsonl(comparison / "version-matches.jsonl")
    old_keys = [(row["oldBinaryHash"], row["oldRva"]) for row in matches]
    new_keys = [(row["newBinaryHash"], row["newRva"]) for row in matches]
    if len(old_keys) != len(set(old_keys)) or len(new_keys) != len(set(new_keys)):
        raise ValueError("Version matching is not one-to-one")
    for row in matches:
        if row["confidence"] == "probable" and float(row["score"]) < 0.92:
            raise ValueError(f"Probable match below threshold: {row}")

    address_records = 0
    identities = {
        args.current_hash.upper(): pe_identity(Path(args.native)),
        args.old_hash.upper(): pe_identity(Path(args.old_native)),
    }
    for path in [semantic / "exports" / "semantic-functions.jsonl", comparison / "exports" / "semantic-functions.jsonl", semantic / "sources" / "pattern-matches.jsonl"]:
        for row in jsonl(path):
            binary_hash, address, rva = row.get("binaryHash"), row.get("address"), row.get("rva")
            if not binary_hash or not address or not rva:
                continue
            base, image_size = identities[binary_hash.upper()]
            va_value, rva_value = int(address, 0), int(rva, 0)
            if va_value != base + rva_value or not 0 <= rva_value < image_size:
                raise ValueError(f"Address mismatch in {path}: {row}")
            address_records += 1

    uri = Path(args.database).resolve().as_uri() + "?mode=ro"
    connection = sqlite3.connect(uri, uri=True)
    integrity = connection.execute("PRAGMA integrity_check").fetchone()[0]
    foreign_keys = connection.execute("PRAGMA foreign_key_check").fetchall()
    fts_hits = connection.execute("SELECT COUNT(*) FROM function_search WHERE function_search MATCH 'operator'").fetchone()[0]
    counts = {name: connection.execute(f"SELECT COUNT(*) FROM {name}").fetchone()[0] for name in [
        "functions", "xrefs", "managed_methods", "pinvokes", "managed_native_links", "patterns",
        "source_types", "type_fields", "vtable_members", "xaml_resources", "version_matches"]}
    connection.close()
    if integrity != "ok" or foreign_keys or fts_hits == 0 or counts["functions"] != 8954:
        raise ValueError(f"SQLite validation failed: integrity={integrity}, foreignKeys={foreign_keys}, ftsHits={fts_hits}, counts={counts}")

    report = {
        "status": "ok", "hashes": actual_hashes, "rawBaselineUnchanged": raw_unchanged,
        "scriptExtenderUnchanged": se_unchanged, "parsedJsonFiles": parsed_json,
        "parsedJsonlRecords": parsed_jsonl_records, "validatedAddressRecords": address_records,
        "resolvedCrusaderPInvokes": len(crusader_pinvokes), "validXamlFiles": len(xaml_rows),
        "versionMatches": len(matches), "sqliteIntegrity": integrity, "foreignKeyErrors": len(foreign_keys),
        "ftsOperatorHits": fts_hits, "pathLimit": path_limit, "maxPathLength": len(str(longest_path)),
        "longestPath": str(longest_path), "filesOverPathLimit": len(over_limit), "counts": counts,
    }
    output = Path(args.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, ensure_ascii=False, indent=2).replace("\n", "\r\n") + "\r\n", encoding="utf-8", newline="")
    print(json.dumps(report, ensure_ascii=False))


if __name__ == "__main__":
    main()
