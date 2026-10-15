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
            "SELECT f.* FROM functions f JOIN function_claims c "
            "ON c.binary_hash=f.binary_hash AND c.function_rva=f.rva "
            "WHERE upper(c.claim_id)=? OR upper(c.canonical_name)=? OR upper(c.function_rva)=? "
            "ORDER BY f.binary_hash",
            (normalized, normalized, normalized),
        ).fetchall()
    if not result:
        result = connection.execute(
            "SELECT * FROM functions WHERE name LIKE ? ORDER BY binary_hash,rva LIMIT 50", (f"%{value}%",)
        ).fetchall()
    return result


def decode_json_fields(item, fields):
    for field in fields:
        if item.get(field) is not None:
            item[field] = json.loads(item[field])
    return item


def claim_packet(connection, row):
    item = decode_json_fields(dict(row), [
        "return_contract", "parameters", "field_accesses", "side_effects",
        "caller_observations", "counter_evidence", "verified_by", "payload",
    ])
    item["evidence"] = rows(connection.execute(
        "SELECT evidence_id,chain_id,kind,strength,source_path,source_file_hash,rva,address,summary,counter "
        "FROM claim_evidence WHERE claim_id=? ORDER BY counter,evidence_id", (item["claim_id"],)))
    item["hookSpans"] = [decode_json_fields(dict(span), [
        "instructions", "external_entries", "live_in", "live_out", "clobbers", "evidence", "payload",
    ]) for span in connection.execute("SELECT * FROM hook_spans WHERE claim_id=? ORDER BY start_rva", (item["claim_id"],)).fetchall()]
    return item


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
    claim = sub.add_parser("claim"); claim.add_argument("value")
    hook = sub.add_parser("hook"); hook.add_argument("value")
    contract = sub.add_parser("contract"); contract.add_argument("value")
    chore = sub.add_parser("chore"); chore.add_argument("kind", choices=["list", "opcode", "function", "contract", "evidence", "gaps"]); chore.add_argument("value", nargs="?")
    sub.add_parser("gaps")
    sub.add_parser("stats")
    args = parser.parse_args()

    uri = Path(args.database).resolve().as_uri() + "?mode=ro"
    connection = sqlite3.connect(uri, uri=True)
    connection.row_factory = sqlite3.Row

    if args.command == "search":
        query = " AND ".join(f'"{part.replace(chr(34), chr(34) * 2)}"' for part in args.text.split())
        print_json({
            "functions": rows(connection.execute(
                "SELECT binary_hash,rva,name,signature,comment,snippet(function_search,5,'[',']',' … ',20) AS excerpt "
                "FROM function_search WHERE function_search MATCH ? LIMIT 100", (query,))),
            "chores": rows(connection.execute(
                "SELECT domain,record_id,binary_hashes,status,snippet(chore_search,4,'[',']',' … ',20) AS excerpt "
                "FROM chore_search WHERE chore_search MATCH ? LIMIT 100", (query,))),
        })
    elif args.command == "function":
        found = resolve_function(connection, args.value)
        packets = []
        for function_row in found:
            item = dict(function_row)
            if item.get("pseudocode") and len(item["pseudocode"]) > 40000:
                item["pseudocode"] = item["pseudocode"][:40000] + "\n/* query output truncated; full text remains in SQLite/export */\n"
                item["pseudocodeTruncated"] = True
            key = (item["binary_hash"], item["rva"])
            item["semanticClaims"] = [claim_packet(connection, row) for row in connection.execute(
                "SELECT * FROM function_claims WHERE binary_hash=? AND function_rva=?", key).fetchall()]
            item["classifications"] = rows(connection.execute("SELECT category,evidence FROM classifications WHERE binary_hash=? AND function_rva=?", key))
            item["strings"] = rows(connection.execute("SELECT value FROM function_strings WHERE binary_hash=? AND function_rva=?", key))
            item["dataReferences"] = rows(connection.execute("SELECT target_rva FROM function_data_references WHERE binary_hash=? AND function_rva=? ORDER BY target_rva", key))
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
    elif args.command == "claim":
        value = f"%{args.value}%"
        found = connection.execute(
            "SELECT * FROM function_claims WHERE claim_id LIKE ? OR canonical_name LIKE ? OR function_rva LIKE ? "
            "ORDER BY function_rva", (value, value, value)).fetchall()
        print_json([claim_packet(connection, row) for row in found])
    elif args.command == "hook":
        value = args.value.upper()
        found = connection.execute("SELECT * FROM hook_spans WHERE upper(span_id)=? OR upper(start_rva)=?", (value, value)).fetchall()
        if not found:
            try:
                target = int(args.value, 0)
                found = [row for row in connection.execute("SELECT * FROM hook_spans").fetchall()
                    if int(row["start_rva"], 0) <= target < int(row["end_rva"], 0)]
            except ValueError:
                found = connection.execute("SELECT * FROM hook_spans WHERE span_id LIKE ?", (f"%{args.value}%",)).fetchall()
        print_json([decode_json_fields(dict(row), [
            "instructions", "external_entries", "live_in", "live_out", "clobbers", "evidence", "payload",
        ]) for row in found])
    elif args.command == "contract":
        value = f"%{args.value}%"
        found = connection.execute(
            "SELECT * FROM api_contracts WHERE contract_id LIKE ? OR producer LIKE ? OR field_name LIKE ? OR consumer LIKE ? OR payload LIKE ? "
            "ORDER BY contract_id", (value, value, value, value, value)).fetchall()
        print_json([decode_json_fields(dict(row), ["evidence", "payload"]) for row in found])
    elif args.command == "chore":
        if args.kind in {"opcode", "function", "contract", "evidence"} and not args.value:
            parser.error(f"chore {args.kind} requires a value")
        if args.kind == "list":
            found = connection.execute("SELECT payload FROM chore_opcodes ORDER BY opcode").fetchall()
            print_json([json.loads(row["payload"]) for row in found])
        elif args.kind == "opcode":
            try:
                opcode = int(args.value, 0)
            except ValueError:
                print_json([])
            else:
                found = connection.execute("SELECT payload FROM chore_opcodes WHERE opcode=?", (opcode,)).fetchall()
                print_json([json.loads(row["payload"]) for row in found])
        elif args.kind == "function":
            found = resolve_function(connection, args.value)
            print_json([claim_packet(connection, claim) for function in found for claim in connection.execute(
                "SELECT * FROM function_claims WHERE binary_hash=? AND function_rva=? AND claim_id LIKE 'fn-chore-%'",
                (function["binary_hash"], function["rva"])).fetchall()])
        elif args.kind == "contract":
            value = f"%{args.value}%"
            found = connection.execute("SELECT payload FROM chore_contracts WHERE contract_id LIKE ? OR title LIKE ? ORDER BY contract_id", (value, value)).fetchall()
            print_json([json.loads(row["payload"]) for row in found])
        elif args.kind == "evidence":
            value = f"%{args.value}%"
            found = connection.execute("SELECT payload FROM chore_evidence WHERE evidence_id LIKE ? OR title LIKE ? ORDER BY evidence_id", (value, value)).fetchall()
            print_json([json.loads(row["payload"]) for row in found])
        else:
            output = []
            for table, identifier in [("chore_opcodes", "opcode_id"), ("chore_contracts", "contract_id")]:
                for row in connection.execute(f"SELECT {identifier},payload FROM {table}").fetchall():
                    payload = json.loads(row["payload"])
                    if payload.get("openQuestions") or payload.get("counterEvidence"):
                        output.append({"domain": table, "id": row[identifier], "binaryHashes": payload.get("binaryHashes") or payload.get("applicability"), "status": payload.get("status"), "confidence": payload.get("confidence"), "evidenceIds": payload.get("evidenceIds"), "counterEvidence": payload.get("counterEvidence"), "openQuestions": payload.get("openQuestions")})
            print_json(output)
    elif args.command == "gaps":
        print_json(rows(connection.execute(
            "SELECT c.claim_id,c.function_rva,c.canonical_name,c.semantic_confidence,"
            "v.confidence AS version_match_confidence,v.old_rva,v.reason,v.score "
            "FROM function_claims c JOIN version_matches v ON v.new_hash=c.binary_hash AND v.new_rva=c.function_rva "
            "WHERE CASE v.confidence WHEN 'confirmed' THEN 2 WHEN 'probable' THEN 1 ELSE 0 END > "
            "CASE c.semantic_confidence WHEN 'confirmed' THEN 2 WHEN 'probable' THEN 1 ELSE 0 END "
            "ORDER BY c.function_rva")))
    else:
        table_names = ["binaries", "functions", "function_claims", "claim_evidence", "hook_spans", "api_contracts", "chore_opcodes", "chore_contracts", "chore_observations", "chore_evidence", "function_data_references", "call_edges", "xrefs", "strings", "globals", "managed_methods", "pinvokes", "managed_native_links", "patterns", "data_types", "source_types", "type_fields", "vtable_members", "delegates", "rtti_vtables", "xaml_resources", "classifications", "version_matches"]
        print_json({table: connection.execute(f"SELECT COUNT(*) FROM {table}").fetchone()[0] for table in table_names})
    connection.close()


if __name__ == "__main__":
    main()
