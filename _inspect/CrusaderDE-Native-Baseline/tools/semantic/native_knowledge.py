"""Prepare reproducible Ghidra knowledge views from an exported production C# catalog.

This is metadata transfer, not a new native audit or a read-permission promotion.
Captured evidence hashes are retained even when a later document was extended.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import re
import subprocess
from pathlib import Path


WIDTH = {"UInt8": 1, "Int8": 1, "UInt16": 2, "Int16": 2,
         "UInt32": 4, "Int32": 4, "UInt64": 8, "Int64": 8, "Float32": 4, "Float64": 8}
MARKER = "NativeKnowledge owned schema=1"


def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2).replace("\n", "\r\n") + "\r\n",
                    encoding="utf-8", newline="")


def sha(data):
    return hashlib.sha256(data).hexdigest().upper()


def evidence_status(evidence, workspace):
    source = evidence["source"]
    prefix, relative = source.split("/", 1)
    if prefix not in {"baseline", "extender"}:
        raise ValueError("Unknown evidence root")
    root = workspace / ("_inspect/CrusaderDE-Native-Baseline" if prefix == "baseline" else "shcde-script-extender")
    path = (root / relative).resolve()
    if not path.is_relative_to(root.resolve()):
        raise ValueError("Evidence path escapes source root")
    captured = evidence["sourceSha256"]
    data = path.read_bytes()
    state = {"source": source, "capturedSha256": captured, "currentSha256": sha(data)}
    if sha(data) == captured:
        state["status"] = "current-bytes"
    else:
        repo = workspace if prefix == "baseline" else root
        git_path = path.relative_to(repo).as_posix()
        commits = subprocess.check_output(["git", "-C", str(repo), "log", "--format=%H", "--", git_path], text=True).splitlines()
        for commit in commits:
            old = subprocess.check_output(["git", "-C", str(repo), "show", f"{commit}:{git_path}"])
            normalized = old.decode("utf-8-sig").replace("\r\n", "\n").replace("\n", "\r\n").encode()
            if captured in {sha(old), sha(normalized)}:
                data = old if sha(old) == captured else normalized
                state.update(status="historical-bytes", commit=commit)
                break
        else:
            raise ValueError(f"Captured evidence unavailable: {source} {captured}")
    lines = len(data.decode("utf-8-sig").splitlines())
    if evidence["firstLine"] < 1 or evidence["lastLine"] < evidence["firstLine"]:
        raise ValueError("Invalid evidence range")
    if evidence["lastLine"] > lines:
        # Preserve this known capture defect; never silently clamp the recorded range.
        state["rangeWarning"] = f"Captured last line {evidence['lastLine']} exceeds {lines} source lines."
    return state


def field_name(field):
    preferred = field["meanings"][0]
    prefix = {"Confirmed": "", "Probable": "prob_", "Candidate": "cand_", "Rejected": "rejected_"}[preferred["confidence"]]
    if field["name"].startswith("Former"):
        prefix = "historical_"
    return prefix + re.sub(r"[^A-Za-z0-9_]", "_", field["name"])


def validate_function_claims(catalog, claims):
    """Portable APIShared mirrors must agree with the independently validated baseline claims."""
    by_id = {c['claimId']: c for c in claims}
    for function in catalog.get('functions', []):
        original = {k:v for k,v in function.items() if k not in {'id','knowledgeNote'}}
        if original != by_id.get(function['claimId']):
            raise ValueError('APIShared function metadata differs from its audited baseline claim: ' + function['claimId'])


def prepare(catalog, native_hash):
    if catalog["schemaVersion"] != 1 or catalog["nativeHash"] != native_hash:
        raise ValueError("NativeKnowledge/native baseline hash mismatch")
    category = f"/NativeKnowledge/{native_hash[:8]}"
    types = []
    records = {}
    seen = set()
    evidence = catalog.get("evidenceValidation", [])
    warnings = "\n".join(json.dumps(e, ensure_ascii=False) for e in evidence if e["status"] != "current-bytes" or "rangeWarning" in e)
    header = f"{MARKER}\nNative SHA-256: {native_hash}\nKnowledge view only; no pointer validity or runtime acceptance implied.\n"
    def add(name, kind, length, description, components, folder=""):
        path = category + folder + "/" + name
        types.append(dict(path=path, kind=kind, length=length, description=header + description, components=components))
        return path
    def component(name, offset, type_name, length, comment="", count=1):
        return dict(name=name, offset=offset, type=type_name, length=length, count=count, comment=comment)
    for field in catalog["fields"]:
        if field["id"] in seen:
            raise ValueError("Duplicate field identity")
        seen.add(field["id"])
        bindings = [b for b in field["bindings"] if b["nativeHash"] == native_hash]
        if len(bindings) != 1:
            raise ValueError("Missing or ambiguous hash binding")
        binding = bindings[0]
        width = WIDTH.get(binding["scalarType"], 1)
        count = binding["elementCount"] or 1
        stride = binding["elementStride"] or width
        if binding["offset"] < 0 or stride < width:
            raise ValueError("Invalid field extent")
        length = (count - 1) * stride + width
        if binding["recordSize"] and binding["offset"] + length > binding["recordSize"]:
            raise ValueError("Field exceeds declared record")
        # Record identity is authoritative. Version-qualified historical aliases can
        # contain dots (e.g. 2.14.2...) and are never a record-origin definition.
        owner = catalog["records"].get(field["recordTypeId"])
        if not owner:
            raise ValueError("No qualified record origin")
        # Keep every binding, meaning and evidence item, not merely the preferred name.
        comment = json.dumps(field, ensure_ascii=False, indent=2) + "\nEvidence verification notes:\n" + warnings
        name = field_name(field)
        raw = binding["scalarType"] if binding["readVerified"] else "Undefined"
        elems = [component(name, i * stride, raw, width, comment if i == 0 else "", 1) for i in range(count)]
        if stride == width:
            elems = [component(name, 0, raw, width, comment, count)]
        ref = add(field["id"].replace("nkf-", "F") + "_" + name, "structure", length, comment, elems, "/Fields")
        records.setdefault((field["recordTypeId"], owner), []).append((binding["offset"], length, ref, name, comment, binding["recordSize"]))
    for (record_id, owner), fields in records.items():
        fields.sort()
        groups = []
        for field in fields:
            if not groups or field[0] >= max(x[0] + x[1] for x in groups[-1]):
                groups.append([field])
            else:
                groups[-1].append(field)
        members = []
        for group in groups:
            start = group[0][0]
            end = max(f[0] + f[1] for f in group)
            if len(group) == 1:
                f = group[0]
                members.append(component(f[3], start, f[2], f[1], f[4]))
                continue
            alternatives = []
            for f in group:
                view = add(f"{owner}_{f[2].split('/')[-1]}_at_{start:X}", "structure", end - start,
                           f"Alternative for {record_id} +0x{start:X}; preserve overlapping identities.",
                           [component(f[3], f[0] - start, f[2], f[1], f[4])], "/Alternatives")
                alternatives.append(component(f[3], 0, view, end - start, f[4]))
            union = add(f"{owner}_overlap_{start:X}", "union", end - start,
                        "Alternative knowledge views of the same bytes; not equivalent meanings.", alternatives, "/Alternatives")
            members.append(component(f"overlap_{start:X}", start, union, end - start))
        sizes = {f[5] for f in fields if f[5]}
        if len(sizes) > 1:
            raise ValueError("Conflicting record sizes")
        size = next(iter(sizes), max(f[0] + f[1] for f in fields))
        suffix = "KnowledgeView" if sizes else "UnknownExtent_KnowledgeView"
        add(owner + "_" + suffix, "structure", size,
            f"{record_id}: beginning of the named public interop view (manager origin only for GameUnitManager).\n"
            + (f"Captured record extent: {size}." if sizes else "FULL RECORD EXTENT UNKNOWN; displayed size only covers described fields.")
            + "\nNo automatic application to pointers. Unverified storage uses undefined bytes; captured scalar interpretation remains in comments.", members)
    # Preserve the initial published guide paths. Later documents use their own
    # stable names, so inserting a document never renames existing Ghidra types.
    legacy_guides = {"FELDKATALOG.md": "Guide_1_FELDKATALOG_md", "INTEGRATION.md": "Guide_2_INTEGRATION_md", "API contracts": "Guide_3_API_contracts"}
    for document in catalog["documents"]:
        guide_name = legacy_guides.get(document['name'], "Guide_" + re.sub('[^A-Za-z0-9_]', '_', document['name']))
        add(guide_name, "structure", 1,
            "Documentation carrier only; never apply to memory.\n" + document["text"], [], "/Guides")
    add("START_HERE", "structure", 1,
        "Open record KnowledgeView types or Fields/Fxxxxxx types to inspect fields and comments.\n"
        "Confirmed names have no prefix; prob_/cand_/historical_ retain their limitations.\n"
        "Source and Reader documentation is in Guides. No code or pointer type was changed.\n"
        + json.dumps({k: v for k, v in catalog.items() if k not in {"fields", "documents"}}, ensure_ascii=False, indent=2), [])
    functions = []
    function_ids = set()
    for function in catalog.get('functions', []):
        if not re.fullmatch(r'nkfn-[0-9]{6}', function['id']):
            raise ValueError('Invalid permanent function identity')
        if function['binaryHash'] != native_hash or function['id'] in function_ids:
            raise ValueError('Wrong-image or duplicate function identity')
        function_ids.add(function['id'])
        if function['semanticConfidence'] != 'confirmed' or function['flowReview'] != 'complete':
            raise ValueError('Only independently reviewed confirmed function names may be applied')
        if not re.fullmatch(r'[A-Za-z_][A-Za-z0-9_]*', function['canonicalName']):
            raise ValueError('Invalid function name')
        if not re.fullmatch(r'[A-F0-9]{64}', function['functionRawHash']):
            raise ValueError('Missing exact function-body hash')
        start, end = int(function['functionRva'], 16), int(function['functionEndRva'], 16)
        if start < 0 or end <= start:
            raise ValueError('Invalid function boundaries')
        functions.append(dict(id=function['id'], rva=function['functionRva'], name=function['canonicalName'],
                              size=end-start, rawHash=function['functionRawHash'], claimId=function['claimId'],
                              nameOrigin=function['nameOrigin'], comment=json.dumps(function, ensure_ascii=False, indent=2)))
    return dict(schemaVersion=1, nativeHash=native_hash, category=category, functions=functions,
                catalogSha256=sha(json.dumps(catalog, sort_keys=True, ensure_ascii=False).encode()),
                fieldIds=sorted(seen), types=types)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("catalog", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--native-hash", required=True)
    parser.add_argument("--workspace", type=Path)
    args = parser.parse_args()
    catalog = json.loads(args.catalog.read_text(encoding="utf-8-sig"))
    baseline = Path(__file__).resolve().parents[2]
    current = json.loads((baseline / 'CURRENT.json').read_text(encoding='utf-8'))
    claims_path = baseline / current['semanticDirectory'] / 'knowledge/function-claims.jsonl'
    validate_function_claims(catalog, [json.loads(line) for line in claims_path.read_text(encoding='utf-8').splitlines() if line.strip()])
    if args.workspace:
        all_evidence = {json.dumps(e, sort_keys=True): e for f in catalog["fields"]
                        for e in [*(e for b in f["bindings"] for e in b["evidence"]), *(e for m in f["meanings"] for e in m["evidence"])]}
        catalog["evidenceValidation"] = [evidence_status(e, args.workspace) for _, e in sorted(all_evidence.items())]
        write(args.catalog, catalog)
    write(args.output, prepare(catalog, args.native_hash))
    print(json.dumps({"status": "prepared", "fields": len(catalog["fields"])}))


if __name__ == "__main__":
    main()
