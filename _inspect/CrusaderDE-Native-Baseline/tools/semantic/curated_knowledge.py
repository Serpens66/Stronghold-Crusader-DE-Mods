#!/usr/bin/env python3
"""Validate curated semantic claims and produce deterministic baseline inputs."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import struct
from pathlib import Path


CRLF = "\r\n"
CONFIDENCE = {"candidate": 0, "probable": 1, "confirmed": 2}
STRONG_KINDS = {"data-flow", "callgraph", "managed-bridge", "export-bridge", "runtime", "version", "structure"}


def read_jsonl(path: Path):
    if not path.is_file():
        raise FileNotFoundError(path)
    with path.open("r", encoding="utf-8") as handle:
        return [json.loads(line) for line in handle if line.strip()]


def write_json(path: Path, value) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    text = json.dumps(value, ensure_ascii=False, indent=2).replace("\n", CRLF) + CRLF
    path.write_text(text, encoding="utf-8", newline="")


def write_tsv(path: Path, rows) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    text = "rva\tsymbol\tclaimId\tconfidence\tsource\n"
    for row in rows:
        text += "\t".join(row) + "\n"
    path.write_text(text.replace("\n", CRLF), encoding="utf-8", newline="")


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest().upper()


def number(value) -> int:
    if isinstance(value, int):
        return value
    return int(value, 0)


def load_pe_image(path: Path):
    data = path.read_bytes()
    if data[:2] != b"MZ":
        raise ValueError(f"Not a PE image: {path}")
    pe = struct.unpack_from("<I", data, 0x3C)[0]
    if data[pe:pe + 4] != b"PE\0\0":
        raise ValueError(f"Invalid PE signature: {path}")
    coff = pe + 4
    section_count = struct.unpack_from("<H", data, coff + 2)[0]
    optional_size = struct.unpack_from("<H", data, coff + 16)[0]
    optional = coff + 20
    if struct.unpack_from("<H", data, optional)[0] != 0x20B:
        raise ValueError(f"Expected PE32+: {path}")
    image_base = struct.unpack_from("<Q", data, optional + 24)[0]
    image_size = struct.unpack_from("<I", data, optional + 56)[0]
    header_size = struct.unpack_from("<I", data, optional + 60)[0]
    image = bytearray(image_size)
    image[:min(header_size, len(data))] = data[:min(header_size, len(data))]
    table = optional + optional_size
    for index in range(section_count):
        offset = table + index * 40
        virtual_size, rva, raw_size, raw_offset = struct.unpack_from("<IIII", data, offset + 8)
        length = min(raw_size, max(0, len(data) - raw_offset), max(0, image_size - rva))
        if length:
            image[rva:rva + length] = data[raw_offset:raw_offset + length]
    return bytes(image), image_base


def assert_confidence(value, context):
    if value not in CONFIDENCE:
        raise ValueError(f"Invalid confidence {value!r} in {context}")


def validate_evidence(claim, source_root: Path):
    evidence = claim.get("evidence") or []
    identifiers = set()
    chains = set()
    strong_chains = set()
    kinds = set()
    for item in evidence:
        evidence_id = item.get("id")
        chain = item.get("chain")
        kind = item.get("kind")
        if not evidence_id or evidence_id in identifiers:
            raise ValueError(f"Duplicate or empty evidence id in {claim['claimId']}: {evidence_id}")
        if not chain or not kind or not item.get("summary"):
            raise ValueError(f"Incomplete evidence in {claim['claimId']}: {item}")
        identifiers.add(evidence_id)
        chains.add(chain)
        kinds.add(kind)
        if item.get("strength") == "strong" and kind in STRONG_KINDS:
            strong_chains.add(chain)
        source = item.get("source")
        source_hash = item.get("sourceFileHash")
        if source and source_hash:
            source_path = source_root / Path(source)
            if not source_path.is_file() or sha256(source_path) != source_hash.upper():
                raise ValueError(f"Evidence source hash mismatch in {claim['claimId']}: {source}")
    semantic = claim["semanticConfidence"]
    unresolved = [item for item in claim.get("counterEvidence", []) if item.get("status", "open") == "open"]
    if semantic == "probable":
        if len(chains) < 2 or claim.get("flowReview") != "complete" or unresolved:
            raise ValueError(f"Probable claim lacks two chains, complete flow review, or has open counter-evidence: {claim['claimId']}")
        if not ({"data-flow", "callgraph", "managed-bridge", "export-bridge", "structure"} & kinds):
            raise ValueError(f"Probable claim lacks non-pattern semantic evidence: {claim['claimId']}")
    if semantic == "confirmed":
        direct_bridge = bool({"managed-bridge", "export-bridge"} & kinds)
        reconstructed = (
            len(strong_chains) >= 3
            and "data-flow" in kinds
            and "callgraph" in kinds
            and bool({"runtime", "version"} & kinds)
        )
        if claim.get("flowReview") != "complete" or unresolved or not (direct_bridge or reconstructed):
            raise ValueError(f"Confirmed semantic claim does not satisfy the evidence contract: {claim['claimId']}")


def validate_claims(claims, functions, current_hash, image_base, source_root):
    by_rva = {number(row["rva"]): row for row in functions if row.get("binaryHash", "").upper() == current_hash}
    ids = set()
    labels = []
    for claim in claims:
        claim_id = claim.get("claimId")
        if not claim_id or claim_id in ids:
            raise ValueError(f"Duplicate or empty claimId: {claim_id}")
        ids.add(claim_id)
        if claim.get("binaryHash", "").upper() != current_hash:
            raise ValueError(f"Claim hash mismatch: {claim_id}")
        for field in ("identityConfidence", "semanticConfidence", "abiConfidence"):
            assert_confidence(claim.get(field), f"{claim_id}.{field}")
        rva = f"0x{number(claim['functionRva']):X}"
        function = by_rva.get(number(rva))
        if function is None:
            raise ValueError(f"Claim does not identify a function entry: {claim_id} {rva}")
        start = number(rva)
        expected_end = start + int(function["size"])
        if number(claim["functionEndRva"]) != expected_end:
            raise ValueError(f"Function end mismatch in {claim_id}: expected 0x{expected_end:X}")
        if number(claim["address"]) != image_base + start:
            raise ValueError(f"VA/RVA mismatch in {claim_id}")
        if claim.get("functionRawHash", "").upper() != function.get("rawHash", "").upper():
            raise ValueError(f"Function raw hash mismatch in {claim_id}")
        validate_evidence(claim, source_root)
        if CONFIDENCE[claim["abiConfidence"]] >= CONFIDENCE["probable"]:
            if not claim.get("signature") or not claim.get("parameters") or not claim.get("return"):
                raise ValueError(f"ABI claim is incomplete: {claim_id}")
        if claim["semanticConfidence"] == "confirmed":
            name = claim.get("canonicalName")
            if not name or not re.fullmatch(r"[A-Za-z_][A-Za-z0-9_:$@?]*", name):
                raise ValueError(f"Confirmed claim has invalid canonical name: {claim_id}")
            labels.append((rva, name, claim_id, "0", "curated:confirmed:" + claim.get("nameOrigin", "reconstruction")))
    return labels


def validate_hook_spans(spans, claims, current_hash, image):
    claim_by_id = {row["claimId"]: row for row in claims}
    ids = set()
    for span in spans:
        span_id = span.get("spanId")
        if not span_id or span_id in ids:
            raise ValueError(f"Duplicate or empty spanId: {span_id}")
        ids.add(span_id)
        if span.get("binaryHash", "").upper() != current_hash:
            raise ValueError(f"Hook span hash mismatch: {span_id}")
        claim = claim_by_id.get(span.get("claimId"))
        if claim is None:
            raise ValueError(f"Hook span references unknown claim: {span_id}")
        start = number(span["startRva"])
        end = number(span["endRva"])
        function_start = number(claim["functionRva"])
        function_end = number(claim["functionEndRva"])
        if not function_start <= start < end <= function_end:
            raise ValueError(f"Hook span lies outside its function: {span_id}")
        expected = bytes.fromhex(span["originalBytes"])
        if len(expected) != end - start or image[start:end] != expected:
            raise ValueError(f"Original bytes mismatch: {span_id}")
        instructions = span.get("instructions") or []
        cursor = start
        for instruction in instructions:
            if number(instruction["rva"]) != cursor:
                raise ValueError(f"Instruction gap in {span_id} at 0x{cursor:X}")
            encoded = bytes.fromhex(instruction["bytes"])
            if image[cursor:cursor + len(encoded)] != encoded:
                raise ValueError(f"Instruction bytes mismatch in {span_id} at 0x{cursor:X}")
            cursor += len(encoded)
        if cursor != end:
            raise ValueError(f"Instructions do not cover the complete span: {span_id}")
        required = ["externalEntries", "liveIn", "liveOut", "clobbers", "stackDelta", "flags", "continuation"]
        if any(field not in span for field in required):
            raise ValueError(f"Hook machine contract is incomplete: {span_id}")
        interior = [number(item) for item in span["externalEntries"] if start < number(item) < end]
        if interior:
            raise ValueError(f"Hook span has external interior entries: {span_id} {interior}")


def validate_contracts(contracts, current_hash):
    identifiers = set()
    for contract in contracts:
        contract_id = contract.get("contractId")
        if not contract_id or contract_id in identifiers:
            raise ValueError(f"Duplicate or empty contractId: {contract_id}")
        identifiers.add(contract_id)
        if contract.get("status") not in {"active", "future-fixed", "retired"}:
            raise ValueError(f"Invalid contract status: {contract_id}")
        if contract.get("binaryHash") and contract["binaryHash"].upper() != current_hash:
            raise ValueError(f"Contract hash mismatch: {contract_id}")
        if contract.get("status") == "active" and not contract.get("evidence"):
            raise ValueError(f"Active contract lacks evidence: {contract_id}")


def build_reports(claims, spans, contracts, version_matches, functions):
    match_by_rva = {row["newRva"].upper(): row for row in version_matches}
    gaps = []
    abi = []
    field_versions = []
    for claim in claims:
        match = match_by_rva.get(claim["functionRva"].upper())
        if match and CONFIDENCE.get(match["confidence"], 0) > CONFIDENCE[claim["semanticConfidence"]]:
            gaps.append({
                "claimId": claim["claimId"], "functionRva": claim["functionRva"],
                "canonicalName": claim.get("canonicalName"), "versionMatchConfidence": match["confidence"],
                "semanticConfidence": claim["semanticConfidence"],
            })
        observations = claim.get("callerObservations") or []
        signatures = sorted({item.get("signature") for item in observations if item.get("signature")})
        abi.append({
            "claimId": claim["claimId"], "abiConfidence": claim["abiConfidence"],
            "callerObservations": len(observations), "observedSignatures": signatures,
            "compatible": len(signatures) <= 1,
        })
        if CONFIDENCE[claim["abiConfidence"]] >= CONFIDENCE["probable"] and len(signatures) > 1:
            raise ValueError(f"Caller ABI conflict: {claim['claimId']} {signatures}")
        if claim.get("fieldAccesses"):
            field_versions.append({
                "claimId": claim["claimId"], "currentRva": claim["functionRva"],
                "historicalRva": match.get("oldRva") if match else None,
                "versionMatchConfidence": match.get("confidence") if match else None,
                "functionChanged": bool(match.get("changed")) if match else None,
                "fieldAccesses": claim["fieldAccesses"],
            })
    return {
        "confidenceGaps": gaps,
        "callerAbi": abi,
        "fieldAccessVersions": field_versions,
        "hookSpans": [{"spanId": row["spanId"], "status": "validated", "startRva": row["startRva"], "endRva": row["endRva"]} for row in spans],
        "apiContracts": [{"contractId": row["contractId"], "status": row["status"]} for row in contracts],
        "functionCount": len(functions),
    }


def main():
    parser = argparse.ArgumentParser()
    for name in ["function-claims", "hook-spans", "api-contracts", "functions", "version-matches", "current-hash", "native", "source-root", "labels", "report-dir"]:
        parser.add_argument(f"--{name}", required=True)
    args = parser.parse_args()
    current_hash = args.current_hash.upper()
    native = Path(args.native)
    if sha256(native) != current_hash:
        raise ValueError(f"Native hash mismatch: {native}")
    image, image_base = load_pe_image(native)
    claims = read_jsonl(Path(args.function_claims))
    spans = read_jsonl(Path(args.hook_spans))
    contracts = read_jsonl(Path(args.api_contracts))
    functions = read_jsonl(Path(args.functions))
    version_matches = read_jsonl(Path(args.version_matches))
    labels = validate_claims(claims, functions, current_hash, image_base, Path(args.source_root))
    validate_hook_spans(spans, claims, current_hash, image)
    validate_contracts(contracts, current_hash)
    reports = build_reports(claims, spans, contracts, version_matches, functions)
    write_tsv(Path(args.labels), labels)
    report_dir = Path(args.report_dir)
    write_json(report_dir / "confidence-gaps.json", reports["confidenceGaps"])
    write_json(report_dir / "caller-abi-report.json", reports["callerAbi"])
    write_json(report_dir / "field-access-version-report.json", reports["fieldAccessVersions"])
    write_json(report_dir / "hookspan-report.json", reports["hookSpans"])
    write_json(report_dir / "api-contract-report.json", reports["apiContracts"])
    print(json.dumps({
        "status": "ok", "claims": len(claims), "hookSpans": len(spans),
        "contracts": len(contracts), "confirmedLabels": len(labels),
        "confidenceGaps": len(reports["confidenceGaps"]),
    }))


if __name__ == "__main__":
    main()
