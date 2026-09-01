#!/usr/bin/env python3
"""Read-only query interface for CrusaderDE-semantic.sqlite."""

from __future__ import annotations

import argparse
import json
import re
import sqlite3
from pathlib import Path


def rows(cursor):
    return [dict(row) for row in cursor.fetchall()]


def print_json(value):
    print(json.dumps(value, ensure_ascii=False, indent=2))


def resolve_function(connection, value):
    normalized = value.upper()
    result = connection.execute(
        "SELECT * FROM functions WHERE upper(name)=? OR upper(rva)=? OR upper(address)=? ORDER BY binary_hash",
        (normalized, normalized, normalized),
    ).fetchall()
    if not result:
        result = connection.execute(
            "SELECT * FROM functions WHERE name LIKE ? ORDER BY binary_hash,rva LIMIT 50", (f"%{value}%",)
        ).fetchall()
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--database", required=True)
    sub = parser.add_subparsers(dest="command", required=True)
    search = sub.add_parser("search"); search.add_argument("text")
    function = sub.add_parser("function"); function.add_argument("value")
    callers = sub.add_parser("callers"); callers.add_argument("value")
    callees = sub.add_parser("callees"); callees.add_argument("value")
    managed = sub.add_parser("managed"); managed.add_argument("value")
    diff = sub.add_parser("diff"); diff.add_argument("old_hash"); diff.add_argument("new_hash")
    sub.add_parser("stats")
    args = parser.parse_args()

    uri = Path(args.database).resolve().as_uri() + "?mode=ro"
    connection = sqlite3.connect(uri, uri=True)
    connection.row_factory = sqlite3.Row

    if args.command == "search":
        query = " AND ".join(f'"{part.replace(chr(34), chr(34) * 2)}"' for part in args.text.split())
        print_json(rows(connection.execute(
            "SELECT binary_hash,rva,name,signature,comment,snippet(function_search,5,'[',']',' … ',20) AS excerpt "
            "FROM function_search WHERE function_search MATCH ? LIMIT 100", (query,))))
    elif args.command == "function":
        found = resolve_function(connection, args.value)
        packets = []
        for function_row in found:
            item = dict(function_row)
            if item.get("pseudocode") and len(item["pseudocode"]) > 40000:
                item["pseudocode"] = item["pseudocode"][:40000] + "\n/* query output truncated; full text remains in SQLite/export */\n"
                item["pseudocodeTruncated"] = True
            key = (item["binary_hash"], item["rva"])
            item["classifications"] = rows(connection.execute("SELECT category,evidence FROM classifications WHERE binary_hash=? AND function_rva=?", key))
            item["strings"] = rows(connection.execute("SELECT value FROM function_strings WHERE binary_hash=? AND function_rva=?", key))
            item["callees"] = rows(connection.execute("SELECT callee_rva,callee_name FROM call_edges WHERE binary_hash=? AND caller_rva=?", key))
            item["callers"] = rows(connection.execute("SELECT caller_rva FROM call_edges WHERE binary_hash=? AND callee_rva=?", key))
            item["managedCallers"] = rows(connection.execute("SELECT managed_method,distance,path FROM managed_native_links WHERE native_rva=? ORDER BY distance,managed_method LIMIT 25", (item["rva"],)))
            item["globals"] = rows(connection.execute(
                "SELECT DISTINCT g.address,g.rva,g.name,g.data_type,g.reference_count FROM globals g "
                "JOIN xrefs x ON x.binary_hash=g.binary_hash AND x.to_rva=g.rva "
                "WHERE x.binary_hash=? AND x.source_function=? ORDER BY g.rva LIMIT 200", (item["binary_hash"], item["name"])))
            signature = item.get("signature") or ""
            type_tokens = sorted(set(re.findall(r"[A-Za-z_][A-Za-z0-9_]*", signature)))
            if type_tokens:
                placeholders = ",".join("?" for _ in type_tokens)
                item["types"] = rows(connection.execute(
                    f"SELECT name,kind,source_path,declaration FROM data_types WHERE name IN ({placeholders}) "
                    f"UNION ALL SELECT name,kind,source_path,declaration FROM source_types WHERE name IN ({placeholders})",
                    tuple(type_tokens + type_tokens)))
            else:
                item["types"] = []
            item["versionMatches"] = rows(connection.execute("SELECT * FROM version_matches WHERE (old_hash=? AND old_rva=?) OR (new_hash=? AND new_rva=?)", (key[0], key[1], key[0], key[1])))
            packets.append(item)
        print_json(packets)
    elif args.command in {"callers", "callees"}:
        found = resolve_function(connection, args.value)
        output = []
        for function_row in found:
            key = (function_row["binary_hash"], function_row["rva"])
            if args.command == "callees":
                related = rows(connection.execute("SELECT callee_rva,callee_name FROM call_edges WHERE binary_hash=? AND caller_rva=?", key))
            else:
                related = rows(connection.execute("SELECT caller_rva FROM call_edges WHERE binary_hash=? AND callee_rva=?", key))
            output.append({"function": dict(function_row), args.command: related})
        print_json(output)
    elif args.command == "managed":
        print_json({
            "methods": rows(connection.execute("SELECT * FROM managed_methods WHERE display LIKE ? OR name LIKE ? LIMIT 100", (f"%{args.value}%", f"%{args.value}%"))),
            "nativeLinks": rows(connection.execute("SELECT * FROM managed_native_links WHERE managed_method LIKE ? OR entry_point LIKE ? ORDER BY distance LIMIT 200", (f"%{args.value}%", f"%{args.value}%"))),
        })
    elif args.command == "diff":
        print_json(rows(connection.execute("SELECT * FROM version_matches WHERE old_hash=? AND new_hash=? ORDER BY confidence,score DESC", (args.old_hash.upper(), args.new_hash.upper()))))
    else:
        table_names = ["binaries", "functions", "call_edges", "xrefs", "strings", "globals", "managed_methods", "pinvokes", "managed_native_links", "patterns", "data_types", "source_types", "type_fields", "vtable_members", "delegates", "rtti_vtables", "xaml_resources", "classifications", "version_matches"]
        print_json({table: connection.execute(f"SELECT COUNT(*) FROM {table}").fetchone()[0] for table in table_names})
    connection.close()


if __name__ == "__main__":
    main()
