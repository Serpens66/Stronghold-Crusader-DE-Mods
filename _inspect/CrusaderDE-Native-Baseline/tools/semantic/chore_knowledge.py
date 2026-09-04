#!/usr/bin/env python3
"""Validate the standalone, hash-bound Chore knowledge domains."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path


CONFIDENCE = {"confirmed-runtime", "confirmed-static", "probable", "candidate", "contradicted", "retired"}


def jsonl(path: Path):
    return [json.loads(line) for line in path.read_text(encoding="utf-8").splitlines() if line.strip()]


def sha256(path: Path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def unique(rows, field):
    values = [row.get(field) for row in rows]
    if any(not value for value in values) or len(values) != len(set(values)):
        raise ValueError(f"Missing or duplicate {field}")


def validate_layout(owner, variant):
    size = variant.get("size")
    spans = []
    for field in variant.get("fields") or []:
        offset, width = field.get("offset"), field.get("size")
        if not isinstance(offset, int) or offset < 0:
            raise ValueError(f"Invalid payload field in {owner}: {field}")
        if not isinstance(width, int):
            continue
        if width <= 0:
            raise ValueError(f"Invalid payload field in {owner}: {field}")
        if isinstance(size, int) and offset + width > size:
            raise ValueError(f"Payload field exceeds size in {owner}: {field}")
        span = range(offset, offset + width)
        if any(set(span).intersection(previous) for previous in spans):
            raise ValueError(f"Overlapping payload fields in {owner}: {field}")
        spans.append(set(span))


def effective_claim(record):
    """Return a copy with accepted field corrections, preserving unrelated facts."""
    result = json.loads(json.dumps(record))
    for counter in record.get("counterEvidence") or []:
        if counter.get("status") == "accepted" and counter.get("path"):
            target = result
            parts = counter["path"].split(".")
            for part in parts[:-1]:
                target = target[int(part)] if isinstance(target, list) else target[part]
            last = parts[-1]
            if isinstance(target, list):
                target[int(last)] = counter.get("replacement")
            else:
                target[last] = counter.get("replacement")
    return result


def validate(args):
    semantic = Path(args.semantic)
    source_root = Path(args.source_root).resolve()
    knowledge = semantic / "knowledge"
    opcodes = jsonl(knowledge / "chore-opcodes.jsonl")
    contracts = jsonl(knowledge / "chore-contracts.jsonl")
    observations = jsonl(knowledge / "chore-observations.jsonl")
    evidence = jsonl(knowledge / "chore-evidence.jsonl")
    claims = jsonl(knowledge / "function-claims.jsonl")
    functions = jsonl(semantic / "exports" / "semantic-functions.jsonl")

    for rows, field in [(opcodes, "opcodeId"), (contracts, "contractId"), (observations, "observationId"), (evidence, "evidenceId")]:
        unique(rows, field)
    opcode_keys = [(row["opcode"], json.dumps(row.get("applicability") or [], sort_keys=True)) for row in opcodes]
    if len(opcode_keys) != len(set(opcode_keys)):
        raise ValueError("Duplicate opcode/hash applicability combination")
    evidence_ids = {row["evidenceId"] for row in evidence}
    for row in opcodes + contracts + observations:
        if row.get("status") not in CONFIDENCE:
            raise ValueError(f"Invalid Chore status: {row}")
        for value in (row.get("confidence") or {}).values():
            if value not in CONFIDENCE:
                raise ValueError(f"Invalid Chore confidence: {value}")
        missing = set(row.get("evidenceIds") or []) - evidence_ids
        if missing:
            raise ValueError(f"Unknown evidence IDs in {row}: {sorted(missing)}")
    for row in opcodes:
        for variant in row.get("payloadVariants") or []:
            validate_layout(row["opcodeId"], variant)
    for row in contracts:
        validate_layout(row["contractId"], {"size": None, "fields": [field for field in row.get("layout") or [] if isinstance(field.get("offset"), int) and isinstance(field.get("size"), int)]})

    for item in evidence:
        path_text = item.get("sourcePath")
        if not path_text:
            continue
        if "mptest" in [part.lower() for part in Path(path_text).parts]:
            raise ValueError(f"Active evidence path points at removed test project: {path_text}")
        path = source_root / path_text
        if not path.is_file() or sha256(path) != item.get("sourceSha256", "").upper():
            raise ValueError(f"Evidence hash mismatch: {path_text}")

    current_hash = args.current_hash.upper()
    current_rvas = {row["rva"].upper(): row for row in functions if row.get("binaryHash", current_hash).upper() == current_hash}
    target_contract = next(row for row in contracts if row["contractId"] == "chore-current-version-targets")
    for claim in (row for row in claims if row.get("claimId", "").startswith("fn-chore-")):
        function = current_rvas.get(claim["functionRva"].upper())
        if not function or function.get("rawHash", "").upper() != claim.get("functionRawHash", "").upper():
            raise ValueError(f"Chore function fingerprint mismatch: {claim['claimId']}")
        if int(claim["address"], 0) != int(function["address"], 0) or int(claim["functionEndRva"], 0) != int(function["rva"], 0) + int(function["size"]):
            raise ValueError(f"Chore function VA/RVA boundary mismatch: {claim['claimId']}")
        chains = {item.get("chain") for item in claim.get("evidence") or []}
        if claim.get("semanticConfidence") == "probable" and (claim.get("flowReview") != "complete" or len(chains) < 2):
            raise ValueError(f"Probable Chore function lacks two evidence chains: {claim['claimId']}")
    for row in opcodes:
        for application in row.get("applicability") or []:
            if application.get("binaryHash", "").upper() == current_hash and application.get("handlerRva"):
                is_audited_stub = row["opcode"] == 111 and application["handlerRva"].upper() == target_contract["targets"]["opcode111HandlerRva"].upper()
                if application["handlerRva"].upper() not in current_rvas and not is_audited_stub:
                    raise ValueError(f"Current handler is not a function entry: {row['opcodeId']} {application['handlerRva']}")

    comprehensive = next(row for row in observations if row["observationId"] == "chore-probe-comprehensive-500ms")
    measurements = comprehensive["measurements"]
    expected = {"logicalRequests": 30, "heldCommands": 15, "minimumHoldMs": 502, "maximumHoldMs": 524,
                "earlyBarrierCrossings": 0, "errors": 0, "mode0ExecutionsPerPeer": 30}
    if any(measurements.get(key) != value for key, value in expected.items()):
        raise ValueError("Comprehensive runtime aggregate changed")
    runtime_text = "\n".join((source_root / item["sourcePath"]).read_text(encoding="utf-8") for item in evidence if item["evidenceId"].startswith("runtime-peer-"))
    for token in ["fullOriginalLogSha256=", "event=delay-held", "event=delay-barrier-observed", "event=delay-released", "event=delay-injected", "elapsedMs=502", "elapsedMs=524", "crossedBarrier=false", "actualTick=728"]:
        if token not in runtime_text:
            raise ValueError(f"Compact runtime evidence lacks invariant token: {token}")

    result = {"status": "ok", "opcodes": len(opcodes), "contracts": len(contracts), "observations": len(observations),
              "evidence": len(evidence), "functionClaims": sum(row.get("claimId", "").startswith("fn-chore-") for row in claims)}
    print(json.dumps(result))
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--semantic", required=True)
    parser.add_argument("--source-root", required=True)
    parser.add_argument("--current-hash", required=True)
    validate(parser.parse_args())


if __name__ == "__main__":
    main()
