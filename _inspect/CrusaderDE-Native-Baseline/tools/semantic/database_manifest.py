#!/usr/bin/env python3
"""Create and validate the reproducible CrusaderDE semantic SQLite manifest."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import sqlite3
from pathlib import Path


CRLF = "\r\n"
COUNT_TABLES = [
    "binaries", "functions", "call_edges", "xrefs", "strings", "globals",
    "managed_methods", "pinvokes", "managed_calls", "managed_native_links",
    "patterns", "data_types", "source_types", "type_fields", "vtable_members",
    "delegates", "rtti_vtables", "xaml_resources", "xaml_links",
    "classifications", "version_matches",
]


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest().upper()


def relative(path: Path, root: Path) -> str:
    return path.resolve().relative_to(root.resolve()).as_posix()


def write_json_atomic(path: Path, value) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + ".tmp")
    text = json.dumps(value, ensure_ascii=False, indent=2).replace("\n", CRLF) + CRLF
    temporary.write_text(text, encoding="utf-8", newline="")
    os.replace(temporary, path)


def database_facts(database: Path):
    uri = database.resolve().as_uri() + "?mode=ro"
    connection = sqlite3.connect(uri, uri=True)
    integrity = connection.execute("PRAGMA integrity_check").fetchone()[0]
    foreign_keys = connection.execute("PRAGMA foreign_key_check").fetchall()
    metadata = dict(connection.execute("SELECT key,value FROM metadata"))
    counts = {table: connection.execute(f"SELECT COUNT(*) FROM {table}").fetchone()[0] for table in COUNT_TABLES}
    fts_hits = connection.execute("SELECT COUNT(*) FROM function_search WHERE function_search MATCH 'operator'").fetchone()[0]
    binaries = [dict(zip(("hash", "role", "sourcePath", "size"), row)) for row in connection.execute("SELECT hash,role,path,size FROM binaries ORDER BY role")]
    connection.close()
    return {
        "sqliteVersion": sqlite3.sqlite_version,
        "integrityCheck": integrity,
        "foreignKeyErrors": len(foreign_keys),
        "metadata": metadata,
        "counts": counts,
        "ftsProbe": {"query": "operator", "hits": fts_hits},
        "binaries": binaries,
    }


def input_paths(args):
    semantic = Path(args.semantic)
    comparison = Path(args.comparison)
    raw = Path(args.raw_root)
    managed = Path(args.managed_dir)
    return [
        semantic / "IDENTITY.json",
        comparison / "IDENTITY.json",
        semantic / "exports" / "semantic-functions.jsonl",
        semantic / "exports" / "semantic-decompiled-functions.c",
        semantic / "exports" / "globals.jsonl",
        semantic / "exports" / "data-types.jsonl",
        semantic / "exports" / "rtti-vtables.jsonl",
        comparison / "exports" / "semantic-functions.jsonl",
        comparison / "version-matches.jsonl",
        raw / "exports" / "xrefs.jsonl",
        raw / "exports" / "strings.jsonl",
        raw / "exports" / "imports.jsonl",
        raw / "exports" / "exports.jsonl",
        managed / "managed-methods.jsonl",
        managed / "pinvokes.jsonl",
        managed / "managed-calls.jsonl",
        managed / "managed-native-links.jsonl",
        semantic / "sources" / "pattern-matches.jsonl",
        semantic / "sources" / "source-types.jsonl",
        semantic / "sources" / "type-fields.jsonl",
        semantic / "sources" / "vtable-members.jsonl",
        semantic / "sources" / "delegates.jsonl",
        semantic / "resources" / "xaml-index.jsonl",
        semantic / "resources" / "xaml-managed-links.jsonl",
    ]


def assert_identities(manifest, semantic: Path, comparison: Path):
    semantic_identity = json.loads((semantic / "IDENTITY.json").read_text(encoding="utf-8"))
    comparison_identity = json.loads((comparison / "IDENTITY.json").read_text(encoding="utf-8"))
    expected = manifest["identities"]
    actual = {
        "currentNativeHash": semantic_identity["currentNativeHash"],
        "managedHash": semantic_identity["managedHash"],
        "oldNativeHash": comparison_identity["oldNativeHash"],
        "scriptExtenderCommit": semantic_identity["scriptExtenderCommit"],
    }
    if actual != expected:
        raise ValueError(f"Identity mismatch: {actual} != {expected}")


def verify_inputs(manifest, baseline_root: Path):
    for item in manifest["inputs"]:
        path = baseline_root / Path(item["path"])
        if not path.is_file():
            raise FileNotFoundError(f"Database input is missing: {path}")
        actual = sha256(path)
        if actual != item["sha256"]:
            raise ValueError(f"Database input hash mismatch for {path}: {actual} != {item['sha256']}")


def create(args):
    baseline = Path(args.baseline_root)
    semantic = Path(args.semantic)
    comparison = Path(args.comparison)
    database = Path(args.database)
    facts = database_facts(database)
    identities = {
        "currentNativeHash": args.current_hash.upper(),
        "managedHash": args.managed_hash.upper(),
        "oldNativeHash": args.old_hash.upper(),
        "scriptExtenderCommit": args.se_commit,
    }
    manifest = {
        "manifestSchemaVersion": 1,
        "database": {
            "path": relative(database, baseline),
            "schemaVersion": int(facts["metadata"]["schema_version"]),
            "referenceSize": database.stat().st_size,
            "referenceSha256": sha256(database),
            "referenceSqliteVersion": facts["sqliteVersion"],
        },
        "identities": identities,
        "binaries": facts["binaries"],
        "validation": {
            "integrityCheck": facts["integrityCheck"],
            "foreignKeyErrors": facts["foreignKeyErrors"],
            "ftsProbe": facts["ftsProbe"],
            "counts": facts["counts"],
        },
        "inputs": [{"path": relative(path, baseline), "sha256": sha256(path), "bytes": path.stat().st_size} for path in input_paths(args)],
    }
    if facts["integrityCheck"] != "ok" or facts["foreignKeyErrors"]:
        raise ValueError(f"Cannot publish invalid database manifest: {facts}")
    if facts["metadata"].get("current_native_hash") != identities["currentNativeHash"] or facts["metadata"].get("managed_hash") != identities["managedHash"]:
        raise ValueError("Database metadata does not match requested identities")
    assert_identities(manifest, semantic, comparison)
    write_json_atomic(Path(args.manifest), manifest)
    current = {
        "schemaVersion": 1,
        "currentNativeHash": identities["currentNativeHash"],
        "semanticDirectory": relative(semantic, baseline),
        "databaseManifest": relative(Path(args.manifest), baseline),
    }
    write_json_atomic(Path(args.current_index), current)
    print(json.dumps({"status": "created", "manifest": str(args.manifest), "databaseSha256": manifest["database"]["referenceSha256"], "databaseSize": manifest["database"]["referenceSize"]}))


def validate(args, inputs_only=False):
    baseline = Path(args.baseline_root)
    semantic = Path(args.semantic)
    comparison = Path(args.comparison)
    manifest = json.loads(Path(args.manifest).read_text(encoding="utf-8"))
    assert_identities(manifest, semantic, comparison)
    verify_inputs(manifest, baseline)
    if inputs_only:
        print(json.dumps({"status": "ok", "inputs": len(manifest["inputs"]), "identities": manifest["identities"]}))
        return
    database = Path(args.database)
    facts = database_facts(database)
    expected = manifest["validation"]
    logical_ok = (
        facts["integrityCheck"] == expected["integrityCheck"] == "ok"
        and facts["foreignKeyErrors"] == expected["foreignKeyErrors"] == 0
        and facts["counts"] == expected["counts"]
        and facts["ftsProbe"] == expected["ftsProbe"]
        and facts["binaries"] == manifest["binaries"]
        and int(facts["metadata"].get("schema_version", -1)) == manifest["database"]["schemaVersion"]
        and facts["metadata"].get("current_native_hash") == manifest["identities"]["currentNativeHash"]
        and facts["metadata"].get("managed_hash") == manifest["identities"]["managedHash"]
    )
    if not logical_ok:
        raise ValueError(f"Logical database validation failed: actual={facts}, expected={expected}")
    actual_sha = sha256(database)
    result = {
        "status": "ok",
        "logicalMatch": True,
        "physicalMatch": actual_sha == manifest["database"]["referenceSha256"] and database.stat().st_size == manifest["database"]["referenceSize"],
        "sqliteVersionMatch": facts["sqliteVersion"] == manifest["database"]["referenceSqliteVersion"],
        "actualSha256": actual_sha,
        "actualSize": database.stat().st_size,
        "sqliteVersion": facts["sqliteVersion"],
    }
    print(json.dumps(result))


def add_common(parser):
    for name in ["baseline-root", "semantic", "comparison", "database", "manifest"]:
        parser.add_argument(f"--{name}", required=True)


def main():
    parser = argparse.ArgumentParser()
    commands = parser.add_subparsers(dest="command", required=True)
    create_parser = commands.add_parser("create")
    add_common(create_parser)
    for name in ["raw-root", "managed-dir", "current-hash", "managed-hash", "old-hash", "se-commit", "current-index"]:
        create_parser.add_argument(f"--{name}", required=True)
    verify_parser = commands.add_parser("verify-inputs")
    add_common(verify_parser)
    validate_parser = commands.add_parser("validate")
    add_common(validate_parser)
    args = parser.parse_args()
    if args.command == "create":
        create(args)
    elif args.command == "verify-inputs":
        validate(args, inputs_only=True)
    else:
        validate(args)


if __name__ == "__main__":
    main()
