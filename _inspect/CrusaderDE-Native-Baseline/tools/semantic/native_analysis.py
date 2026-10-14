#!/usr/bin/env python3
"""Offline analysis generation, conservative machine contracts and feature inventory."""
from __future__ import annotations
import argparse
import collections
import hashlib
import json
import os
from pathlib import Path
import re
import sqlite3
import subprocess
import shutil

TOOL = Path(__file__).resolve().parent
BASELINE = TOOL.parent.parent
WORKSPACE = BASELINE.parent.parent
FIXES = WORKSPACE.parent / "Fremde Mods" / "shcde-fixes-main"
COMMANDS = {"instructions", "flow", "trace", "global", "feature", "hookcheck", "coverage"}
ANALYSIS_TABLES = ["analysis_functions", "analysis_features", "analysis_sources", "analysis_profiles"]

def sha(path):
    h = hashlib.sha256()
    with Path(path).open("rb") as f:
        for b in iter(lambda: f.read(1024 * 1024), b""): h.update(b)
    return h.hexdigest().upper()

def write(path, value):
    path = Path(path); path.parent.mkdir(parents=True, exist_ok=True)
    tmp = path.with_name(path.name + ".tmp")
    tmp.write_text(json.dumps(value, ensure_ascii=False, indent=2).replace("\n", "\r\n") + "\r\n", encoding="utf-8", newline="")
    os.replace(tmp, path)

def read_jsonl(path):
    with Path(path).open(encoding="utf-8") as f:
        for line in f:
            if line.strip(): yield json.loads(line)

def identity():
    current = json.loads((BASELINE / "CURRENT.json").read_text(encoding="utf-8"))
    manifest = json.loads((BASELINE / current["databaseManifest"]).read_text(encoding="utf-8"))
    semantic = (BASELINE / current["semanticDirectory"]).resolve()
    semantic.relative_to(BASELINE)
    if current["currentNativeHash"] != manifest["identities"]["currentNativeHash"]: raise ValueError("CURRENT/manifest mismatch")
    return current, manifest, semantic

def git(root):
    def invoke(*args):
        return subprocess.check_output(["git", "-C", str(root), *args], text=True, stderr=subprocess.PIPE).strip()
    try:
        return {"path": str(root), "commit": invoke("rev-parse", "HEAD"), "tree": invoke("rev-parse", "HEAD^{tree}"),
                "changes": invoke("status", "--porcelain", "--ignore-submodules"), "status": "observed"}
    except (subprocess.SubprocessError, OSError) as e:
        raise RuntimeError(f"Required source identity unavailable: {root}: {e}") from e

def functions(semantic):
    return list(read_jsonl(semantic / "exports" / "semantic-functions.jsonl"))

def coordinate(value):
    n = int(str(value), 16)
    return n - 0x180000000 if n >= 0x180000000 else n

def hx(value): return f"0x{int(value):X}"

def mentioned(text):
    # Coordinates in prose are evidence candidates, never automatic function/field claims.
    return sorted(set(coordinate(x) for x in re.findall(r"\b0x[0-9a-fA-F]+\b", text)))

def inventory(semantic, binary_hash):
    fs = functions(semantic); by_entry = {coordinate(f["rva"]): f["rva"] for f in fs}
    claims = list(read_jsonl(semantic / "knowledge" / "function-claims.jsonl"))
    spans = list(read_jsonl(semantic / "knowledge" / "hook-spans.jsonl"))
    docs, roots = [], set()
    for path in sorted((semantic / "knowledge").glob("*.md")):
        text = path.read_text(encoding="utf-8")
        nums = mentioned(text)
        if path.stem == "MARKET_PRICE_QUERIES": nums += [0xCEB10,0xCEB90,0x29650,0x3EC90,0x29700,0xD78C0,0xD7AD0]
        entries = sorted({by_entry[n] for n in nums if n in by_entry}, key=coordinate)
        roots.update(entries)
        own_claims = [c for c in claims if coordinate(c["functionRva"]) in nums]
        own_spans = [s for s in spans if coordinate(s["startRva"]) in nums or s["claimId"] in {c["claimId"] for c in own_claims}]
        docs.append({"id": path.stem.lower().replace("_", "-"), "title": text.splitlines()[0].lstrip("# "),
            "binaryHash": binary_hash, "source": path.relative_to(WORKSPACE).as_posix(), "sourceSha256": sha(path),
            "declaredHashes": sorted(set(re.findall(r"\b[0-9A-Fa-f]{64}\b", text))),
            "currentHashMentioned": binary_hash.lower() in text.lower(), "entrypoints": entries,
            "coordinateCandidates": [hx(n) for n in nums], "claims": own_claims, "hookSpans": own_spans,
            "document": text, "status": "indexed-existing-audit", "semanticReview": "not-promoted",
            "openQuestions": ["Document coordinates may be offsets, data, historical addresses or boundaries.",
                              "Reachability and shared globals do not establish feature relevance or runtime execution."]})
        docs[-1]["documentedContracts"]=[{"line":number,"statement":line.strip(),"authority":"existing-audit-text",
            "interpretation":"verbatim evidence; no automatic semantic promotion"} for number,line in enumerate(text.splitlines(),1)
            if line.strip() and re.search(r"hook|span|ABI|register|RFLAGS|parameter|signed|offset|state|continuation|branch|writer|reader|event|publisher|terminal|contract",line,re.I)]
    roots.update(c["functionRva"] for c in claims if c["binaryHash"] == binary_hash)
    roots.update(f["rva"] for f in fs if f.get("name", "").startswith("DLL_"))
    source_rows = []
    for owner, root in [("workspace", WORKSPACE),("script-extender", WORKSPACE / "shcde-script-extender"),("fixes", FIXES),("APIShared", WORKSPACE / "APIShared")]:
        for path in sorted(root.rglob("*.cs")):
            relative = path.relative_to(root)
            if any(p in {"bin","obj",".git","_inspect",".tools",".native-analysis","tests"} for p in relative.parts): continue
            if owner == "workspace" and relative.parts[0] in {"APIShared","shcde-script-extender"}: continue
            text = path.read_text(encoding="utf-8-sig")
            candidates = []
            for line_number,line in enumerate(text.splitlines(),1):
                if re.search(r"AddDetour|InlineHook|ContextHook|HookTarget|HookSize|OnKeyDown|Subscribe\(|event |DllImport|NativePointer|RVA",line):
                    candidates.append({"line":line_number,"text":line.strip(),"coordinates":[hx(n) for n in mentioned(line)],
                                       "classification":"source-discovery-only"})
            if candidates:
                coordinates = set(mentioned(text)); entries = [by_entry[n] for n in coordinates if n in by_entry]
                roots.update(entries)
                source_rows.append({"owner":owner,"path":str(path),"relativePath":relative.as_posix(),"sha256":sha(path),
                                    "entryCandidates":entries,"observations":candidates})
    # Expand native callees to a fixed point. No depth cut masquerading as a feature boundary.
    graph = {f["rva"]: [x for x in f.get("calleeRvas",[]) if x] for f in fs}
    callers = collections.defaultdict(set)
    for a,bs in graph.items():
        for b in bs: callers[b].add(a)
    for doc in docs:
        seeds=set(doc["entrypoints"]); direct_callers=set().union(*(callers[r] for r in seeds)) if seeds else set()
        scope=seeds|direct_callers; queue=list(scope)
        while queue:
            for target in graph.get(queue.pop(),[]):
                if target in graph and target not in scope: scope.add(target);queue.append(target)
        roots.update(scope)
        doc["relatedFunctions"]=sorted(scope,key=coordinate)
        doc["callerFrontier"]=sorted(set().union(*(callers[r] for r in scope))-scope,key=coordinate) if scope else []
        doc["scopeReason"]="Document entry candidates, their immediate callers, and complete resolved callee closure; upstream caller frontier remains open."
    return docs, source_rows, sorted(roots,key=coordinate)

def prepare():
    current, manifest, semantic = identity()
    native = next(b for b in manifest["binaries"] if b["role"]=="current-native")["sourcePath"]
    managed = next(b for b in manifest["binaries"] if b["role"]=="current-managed")["sourcePath"]
    if sha(native)!=current["currentNativeHash"] or sha(managed)!=manifest["identities"]["managedHash"]: raise ValueError("Installed binary mismatch")
    docs,sources,roots=inventory(semantic,current["currentNativeHash"])
    installed=Path(managed).parents[2] / "BepInEx" / "plugins" / "000shcdese"
    backends={name:{"path":str(installed / name),"sha256":sha(installed / name)} for name in ["Iced.dll","RedBird.X64.dll","RedBird.Backends.NativeX64.dll","SHCDESE.dll"]}
    profile={"schemaVersion":1,"binaryHash":current["currentNativeHash"],"managedHash":manifest["identities"]["managedHash"],
        "nativePath":native,"sources":[git(WORKSPACE),git(WORKSPACE/"shcde-script-extender"),git(FIXES),git(WORKSPACE/"APIShared")],
        "installed":backends,"baselineProvenance":manifest["identities"],"ghidraVersion":"12.1.3",
        "exporterSha256":sha(TOOL/"ExportNativeAnalysis.java"),"decoderSourceSha256":sha(TOOL/"NativeFacts"/"Program.cs"),
        "analysisSourceSha256":sha(__file__),"functionExportSha256":sha(semantic/"exports"/"semantic-functions.jsonl"),
        "querySourceSha256":sha(TOOL/"native_queries.py"),"driverSha256":sha(TOOL/"Run-NativeAnalysis.ps1"),
        "selection":roots,"documentHashes":{d["id"]:d["sourceSha256"] for d in docs},"sourceHashes":{s["path"]:s["sha256"] for s in sources}}
    project=semantic/"ghidra"
    profile["projectHashes"]={p.relative_to(BASELINE).as_posix():sha(p) for p in project.rglob('*') if p.is_file() and p.suffix not in {'.lock','.ulock'}}
    # Git worktree status is recorded, not used to invalidate caches for generated outputs.
    committed={k:v for k,v in profile.items() if k!="sources"}
    committed["sourceIdentities"]=[{k:v for k,v in s.items() if k!="changes"} for s in profile["sources"]]
    digest=hashlib.sha256(json.dumps(committed,sort_keys=True).encode()).hexdigest().upper();profile["digest"]=digest
    fact_inputs={k:profile[k] for k in ["binaryHash","ghidraVersion","exporterSha256","decoderSourceSha256","functionExportSha256","projectHashes"]}
    fact_inputs["icedSha256"]=backends["Iced.dll"]["sha256"]
    profile["factsDigest"]=hashlib.sha256(json.dumps(fact_inputs,sort_keys=True).encode()).hexdigest().upper()
    generation=semantic/"analysis"/("g"+digest[:12]); generation.mkdir(parents=True,exist_ok=True)
    if manifest.get("analysis",{}).get("digest")==digest:
        from database_manifest import verify_inputs
        verify_inputs(manifest,BASELINE)
        print(json.dumps({"generation":str(generation),"digest":digest,"features":len(docs),"selectedFunctions":len(roots),"reusedPublished":True}))
        return
    work=generation/"work";work.mkdir(exist_ok=True)
    for previous in sorted((semantic/"analysis").glob("g*/profile.json")):
        old=json.loads(previous.read_text(encoding="utf-8"))
        if previous.parent!=generation and old.get("factsDigest")==profile["factsDigest"]:
            for p in (previous.parent/"work").glob("*.json"):
                if p.name!="COMPLETE.json" and not (work/p.name).exists():shutil.copyfile(p,work/p.name)
                checksum=p.with_name(p.name+'.sha256')
                if p.name!="COMPLETE.json" and checksum.exists() and not (work/checksum.name).exists():shutil.copyfile(checksum,work/checksum.name)
    write(generation/"profile.json",profile);write(generation/"features.json",docs);write(generation/"sources.json",sources)
    (generation/"selection.txt").write_text("\r\n".join(roots)+"\r\n",encoding="utf-8",newline="")
    write(semantic/"analysis"/"pending.json",{"generation":str(generation),"digest":digest})
    print(json.dumps({"generation":str(generation),"digest":digest,"features":len(docs),"selectedFunctions":len(roots)},ensure_ascii=False))

def normalize_reg(reg):
    # Conservative family normalization: every write reads old family too unless proven full-width.
    aliases={"AL":"RAX","AH":"RAX","AX":"RAX","EAX":"RAX","BL":"RBX","BH":"RBX","BX":"RBX","EBX":"RBX",
             "CL":"RCX","CH":"RCX","CX":"RCX","ECX":"RCX","DL":"RDX","DH":"RDX","DX":"RDX","EDX":"RDX",
             "SI":"RSI","SIL":"RSI","ESI":"RSI","DI":"RDI","DIL":"RDI","EDI":"RDI","SP":"RSP","SPL":"RSP","ESP":"RSP",
             "BP":"RBP","BPL":"RBP","EBP":"RBP"}
    if reg in aliases:return aliases[reg]
    m=re.fullmatch(r"(R\d+)[DWB]",reg)
    if m:return m[1]
    m=re.fullmatch(r"[XYZ]MM(\d+)",reg)
    return "ZMM"+m[1] if m else reg

def machine_liveness(packet):
    rows=packet["instructions"]; by={r["rva"]:r for r in rows}
    live_in,live_out={},{}; unknown=False
    for row in rows:
        reads=set();writes=set()
        for reg in row.get("registers",[]):
            name=reg["register"];family=normalize_reg(name);access=reg["access"]
            if "Read" in access:reads.add(family)
            if "Write" in access:
                # Keeping old contents for every subfamily write is deliberately conservative.
                if family!=name or "Cond" in access:reads.add(family)
                else:writes.add(family)
        row["registerUses"]=sorted(reads);row["registerDefs"]=sorted(writes)
        flags=["FLAG:"+str(1<<i) for i in range(32) if row.get("flagsRead",0)&(1<<i)]
        written=row.get("flagsWritten",0)|row.get("flagsCleared",0)|row.get("flagsSet",0)|row.get("flagsUndefined",0)
        reads.update(flags);writes.update("FLAG:"+str(1<<i) for i in range(32) if written&(1<<i))
        if row.get("icedFlow") in {"Call","IndirectCall","IndirectBranch"} or not row.get("icedVerified"):
            reads.add("UNKNOWN_MACHINE_STATE");unknown=True
        row["machineUses"]=sorted(reads);row["machineDefs"]=sorted(writes)
        live_in[row["rva"]]=set();live_out[row["rva"]]=set()
    changed=True
    while changed:
        changed=False
        for row in reversed(rows):
            successors=set(row.get("targets",[]));successors.add(row.get("fallthrough"))
            out=set().union(*(live_in[t] for t in successors if t in by))
            if row.get("icedFlow")=="Return":
                out.update(["RAX","RSP","RBX","RBP","RSI","RDI","R12","R13","R14","R15"]+["ZMM"+str(i) for i in range(6,16)])
                if not packet.get("verifiedAbi"):out.add("RETURN_ABI_UNKNOWN")
            if any(t and t not in by for t in successors) and row.get("icedFlow") not in {"Call","IndirectCall"}:out.add("UNKNOWN_MACHINE_STATE");unknown=True
            incoming=set(row["machineUses"])|(out-set(row["machineDefs"]))
            if incoming!=live_in[row["rva"]] or out!=live_out[row["rva"]]:changed=True;live_in[row["rva"]]=incoming;live_out[row["rva"]]=out
    for row in rows:row["liveIn"]=sorted(live_in[row["rva"]]);row["liveOut"]=sorted(live_out[row["rva"]])
    packet["liveness"]={"status":"conservative","unknownCallsOrEdges":unknown,"limitations":["Family-level overapproximation; subregister writes preserve old family.","Return ABI and unknown calls require manual review; no SIMD/flag preservation guarantee."]}

def import_analysis(connection, semantic, generation=None):
    connection.executescript("""
        CREATE TABLE IF NOT EXISTS analysis_functions(binary_hash TEXT, rva TEXT, payload TEXT, PRIMARY KEY(binary_hash,rva));
        CREATE TABLE IF NOT EXISTS analysis_features(id TEXT PRIMARY KEY, payload TEXT);
        CREATE TABLE IF NOT EXISTS analysis_sources(path TEXT PRIMARY KEY, payload TEXT);
        CREATE TABLE IF NOT EXISTS analysis_profiles(key TEXT PRIMARY KEY, payload TEXT);
    """)
    if generation is None:
        manifest_path=Path(semantic)/"DATABASE_INFO.json"
        manifest=json.loads(manifest_path.read_text(encoding="utf-8"))
        if "analysis" not in manifest:return
        generation=(BASELINE/manifest["analysis"]["generation"]).resolve()
    generation=Path(generation);generation.relative_to(BASELINE)
    profile=json.loads((generation/"profile.json").read_text(encoding="utf-8"))
    connection.execute("INSERT INTO analysis_profiles VALUES(?,?)",("active",json.dumps(profile)))
    backend_path=generation/"backends.json"
    if backend_path.exists():connection.execute("INSERT INTO analysis_profiles VALUES(?,?)",("backends",backend_path.read_text(encoding="utf-8")))
    pending=None
    def flush():
        if pending is None:return
        expected=pending.pop('expectedCounts')
        if len(pending['instructions'])!=expected['instructions'] or len(pending['flow'].get('operations',[]))!=expected['operations']:raise ValueError('Incomplete chunked function export')
        connection.execute('INSERT INTO analysis_functions VALUES(?,?,?)',(pending['binaryHash'],pending['functionRva'],json.dumps(pending,separators=(',',':'))))
    for path in sorted(generation.glob("facts-*.jsonl")):
        for row in read_jsonl(path):
            if row['recordType']=='header':
                flush();pending=row['payload'];pending['instructions']=[]
                if pending['flow']['status']=='complete':pending['flow']['operations']=[]
            else:
                if pending is None or row['functionRva']!=pending['functionRva']:raise ValueError('Chunk has no matching function header')
                target=pending['instructions'] if row['recordType']=='instructions' else pending['flow']['operations']
                if row['offset']!=len(target):raise ValueError('Missing or reordered function chunk')
                target.extend(row['items'])
    flush()
    for name,table,key in [("features.json","analysis_features","id"),("sources.json","analysis_sources","path")]:
        for row in json.loads((generation/name).read_text(encoding="utf-8")):connection.execute(f"INSERT INTO {table} VALUES(?,?)",(row[key],json.dumps(row)))

def validate_packets(generation):
    profile=json.loads((generation/"profile.json").read_text(encoding="utf-8"));packets=[]
    complete=json.loads((generation/"work"/"COMPLETE.json").read_text(encoding="utf-8"))
    if complete['profile']!=profile['factsDigest'] or complete['binaryHash']!=profile['binaryHash']:raise ValueError('Completion profile mismatch')
    for path in sorted((generation/"work").glob("*.json")):
        if path.name=="COMPLETE.json":continue
        row=json.loads(path.read_text(encoding="utf-8"))
        if path.with_name(path.name+'.sha256').read_text(encoding='utf-8').strip()!=sha(path):raise ValueError('Checkpoint integrity mismatch')
        if row["binaryHash"]!=profile["binaryHash"] or row["profile"]!=profile["factsDigest"]:raise ValueError("Stale packet")
        if row.get("icedStatus")!="complete":raise ValueError("Missing decoder verification")
        if row.get('icedAssemblySha256')!=profile['installed']['Iced.dll']['sha256']:raise ValueError('Actual decoder assembly differs from captured installed Iced')
        if row['functionRva'] in profile['selection'] and row['flow']['status']=='not-selected':raise ValueError('Requested flow missing after interrupted export')
        for ins in row["instructions"]:
            if len(ins["bytes"])!=ins["length"]*2:raise ValueError("Invalid instruction byte count")
            n=coordinate(ins["rva"])
            if not any(coordinate(r["start"])<=n<=coordinate(r["endInclusive"]) for r in row["ranges"]):raise ValueError("Instruction outside function body")
        ids={op["id"] for op in row["flow"].get("operations",[])}
        for op in row["flow"].get("operations",[]):
            for v in op["inputs"]:
                if v.get("definition") and v["definition"] not in ids:raise ValueError("Dangling def-use edge")
        packets.append(row)
    if len(packets)!=complete["functions"]:raise ValueError("Interrupted machine export")
    _,_,semantic=identity()
    if {p["functionRva"] for p in packets}!={f["rva"] for f in functions(semantic)}:raise ValueError("Function coverage mismatch")
    return profile,packets

def finalize(generation):
    profile,packets=validate_packets(generation)
    _,_,semantic=identity()
    claims=list(read_jsonl(semantic/"knowledge"/"function-claims.jsonl"))
    verified={c["functionRva"] for c in claims if c.get("abiConfidence")=="confirmed" and c["binaryHash"]==profile["binaryHash"]}
    writer=None;shard=-1;shard_bytes=0
    try:
        for packet in packets:
            packet["verifiedAbi"]=packet["functionRva"] in verified
            machine_liveness(packet)
            header=dict(packet);header.pop('instructions');header['flow']=dict(packet['flow']);operations=header['flow'].pop('operations',[])
            header['expectedCounts']={'instructions':len(packet['instructions']),'operations':len(operations)}
            records=[{'recordType':'header','payload':header}]
            for kind,items in [('instructions',packet['instructions']),('operations',operations)]:
                records.extend({'recordType':kind,'functionRva':packet['functionRva'],'offset':offset,'items':items[offset:offset+256]} for offset in range(0,len(items),256))
            for record in records:
                line=json.dumps(record,ensure_ascii=False,separators=(",",":"))+'\r\n';size=len(line.encode('utf-8'))
                if size>32*1024*1024:raise ValueError('Single bounded record exceeds export size ceiling')
                if writer is None or shard_bytes+size>32*1024*1024:
                    if writer:writer.close()
                    shard+=1;shard_bytes=0;writer=(generation/f'facts-{shard:03X}.jsonl').open('w',encoding='utf-8',newline='')
                writer.write(line);shard_bytes+=size
    finally:
        if writer:writer.close()
    coverage={"binaryHash":profile["binaryHash"],"functions":len(packets),"selected":len(profile["selection"]),
        "flowStatuses":dict(collections.Counter(p["flow"]["status"] for p in packets)),
        "decoderDisagreements":sum(not i.get("icedVerified") for p in packets for i in p["instructions"]),
        "instructions":sum(len(p["instructions"]) for p in packets),"features":len(json.loads((generation/"features.json").read_text())),
        "limitations":["Static analysis is not runtime evidence.","Feature roots and source coordinates are discovery evidence unless separately audited."]}
    write(generation/"coverage.json",coverage)
    enrich_features(generation,packets,semantic)
    print(json.dumps(coverage))

def enrich_features(generation,packets,semantic):
    by={p["functionRva"]:p for p in packets};docs=json.loads((generation/"features.json").read_text(encoding="utf-8"))
    edges=collections.defaultdict(set)
    for p in packets:
        for i in p["instructions"]:
            if i.get("icedFlow")=="Call":
                for t in i.get("targets",[]):
                    if t:edges[t].add(p["functionRva"])
    for doc in docs:
        checks=[]
        for claim in doc["claims"]:
            observed=edges[claim["functionRva"]]
            for c in claim.get("callerObservations",[]):
                if c.get("callerRva") not in observed:checks.append({"kind":"caller-owner-mismatch","claim":claim["claimId"],"documentedCaller":c.get("callerRva"),"observedCallers":sorted(observed),"status":"requires-review"})
        if doc["id"]=="workshop-idle-delay":
            for start,end in [(0x13AC85,0x13AC96),(0x13E39F,0x13E3B0)]:
                doc["hookSpans"].append({"spanId":"workshop-"+hx(start),"binaryHash":doc["binaryHash"],"startRva":hx(start),"endRva":hx(end),
                    "originalBytes":"85FF0F8422110000420FB78423D8090000" if start==0x13AC85 else "85ED0F841B030000420FB78423D8090000",
                    "authority":"existing-complete-feature-audit", "source":doc["source"],"sourceSha256":doc["sourceSha256"],"backend":"RedBird.X64","type":"X64InlineHook"})
        for span in doc["hookSpans"]:
            matching=[i for p in packets for i in p["instructions"] if coordinate(span["startRva"])<=coordinate(i["rva"])<coordinate(span["endRva"])]
            actual="".join(i["bytes"] for i in sorted(matching,key=lambda i:coordinate(i["rva"])))
            checks.append({"kind":"span-bytes","spanId":span["spanId"],"status":"matched" if actual==span["originalBytes"] else "mismatch","actual":actual})
        doc["contractChecks"]=checks
        doc["analysisCoverage"]={"functions":len(doc["relatedFunctions"]),"flows":dict(collections.Counter(by[r]["flow"]["status"] if r in by else "missing" for r in doc["relatedFunctions"]))}
        doc["integrationCandidates"]=[{"owner":s["owner"],"path":s["relativePath"],"observations":[o for o in s["observations"] if any(coordinate(r) in mentioned(o["text"]) for r in doc["entrypoints"])]} for s in json.loads((generation/"sources.json").read_text(encoding="utf-8")) if set(s["entryCandidates"])&set(doc["entrypoints"])]
    write(generation/"features.json",docs)

def backend_profiles(generation, profile):
    inline=(generation/"backend"/"inline.cs").read_text(encoding="utf-8")
    jumps=(generation/"backend"/"jumps.cs").read_text(encoding="utf-8")
    prologue=(generation/"backend"/"prologue.cs").read_text(encoding="utf-8")
    if "Math.Max(minHookSize, 14)" not in inline or "DecodeDisplacedInstructions" not in inline:raise ValueError("Inline backend contract changed")
    sizes={name:int(value) for name,value in re.findall(r"DetourScheme\.(\w+)\s*=>\s*(\d+)",jumps)}
    if sizes!={"Indirect":6,"Absolute":14,"PushRet":12,"PushRetRedZoneSafe":23}:raise ValueError("Detour patch schemas changed")
    if not all(s in prologue for s in ["WidenForSelfBranches", "TryTakeAtLeast", "IsFunctionEnd", "num2 <= num"]):raise ValueError("Prologue selection changed")
    return {"X64InlineHook":{"backend":"RedBird.X64","minimum":14,"assembly":profile["installed"]["RedBird.X64.dll"],
               "evidence":sha(generation/"backend"/"inline.cs"),"selection":"instruction-rounded"},
            "NativeDetour":{"backend":"RedBird.Backends.NativeX64","schemes":sizes,"assembly":profile["installed"]["RedBird.Backends.NativeX64.dll"],
               "evidence":[sha(generation/"backend"/"jumps.cs"),sha(generation/"backend"/"prologue.cs")],
               "selection":"instruction-rounded-self-branch-widening","followJumps":False}}

def publish(generation):
    from database_manifest import database_facts, verify_inputs
    current,manifest,semantic=identity()
    verify_inputs(manifest,BASELINE)
    profile=json.loads((generation/"profile.json").read_text(encoding="utf-8"))
    if manifest.get("analysis",{}).get("digest")==profile["digest"]:
        print(json.dumps({"status":"reused-published","digest":profile["digest"]}));return
    for key,path in [("exporterSha256",TOOL/"ExportNativeAnalysis.java"),("decoderSourceSha256",TOOL/"NativeFacts/Program.cs"),("analysisSourceSha256",Path(__file__)),("querySourceSha256",TOOL/"native_queries.py"),("driverSha256",TOOL/"Run-NativeAnalysis.ps1")]:
        if sha(path)!=profile[key]:raise ValueError("Analysis implementation changed during run; rerun Analysis to reuse unchanged machine facts")
    if sha(profile["nativePath"])!=profile["binaryHash"]:raise ValueError("Native changed during analysis")
    for assembly in profile["installed"].values():
        if sha(assembly["path"])!=assembly["sha256"]:raise ValueError("Backend changed during analysis")
    for path,digest in profile["projectHashes"].items():
        if sha(BASELINE/path)!=digest:raise ValueError("Reference Ghidra project changed")
    for source in json.loads((generation/"sources.json").read_text(encoding="utf-8")):
        if sha(source["path"])!=source["sha256"]:raise ValueError("Source changed during analysis")
    for feature in json.loads((generation/"features.json").read_text(encoding="utf-8")):
        if sha(WORKSPACE/feature["source"])!=feature["sourceSha256"]:raise ValueError("Audit changed during analysis")
    profiles=backend_profiles(generation,profile);write(generation/"backends.json",profiles)
    original=BASELINE/manifest["database"]["path"]
    target=generation/"index.sqlite";temporary=generation/"index.sqlite-building"
    source=sqlite3.connect(original.resolve().as_uri()+"?mode=ro",uri=True);connection=sqlite3.connect(temporary)
    try:
        source.backup(connection)
        for table in ANALYSIS_TABLES:connection.execute(f"DROP TABLE IF EXISTS {table}")
        connection.commit()
        import_analysis(connection,semantic,generation)
        connection.execute("UPDATE metadata SET value='5' WHERE key='schema_version'")
        connection.commit()
        if connection.execute("PRAGMA integrity_check").fetchone()[0]!="ok":raise ValueError("Index integrity failure")
    finally:source.close();connection.close()
    os.replace(temporary,target)
    facts=database_facts(target)
    updated=json.loads(json.dumps(manifest));updated["manifestSchemaVersion"]=3
    updated["database"].update(path=target.relative_to(BASELINE).as_posix(),schemaVersion=5,referenceSize=target.stat().st_size,
        referenceSha256=sha(target),referenceSqliteVersion=facts["sqliteVersion"])
    updated["validation"].update(counts=facts["counts"])
    updated["analysis"]={"generation":generation.relative_to(BASELINE).as_posix(),"digest":profile["digest"],"profileSchemaVersion":1}
    previous_generation=manifest.get("analysis",{}).get("generation")
    if previous_generation:updated["inputs"]=[x for x in updated["inputs"] if not x["path"].startswith(previous_generation+"/")]
    paths=list(generation.glob("facts-*.jsonl"))+[generation/x for x in ["profile.json","features.json","sources.json","coverage.json","backends.json"]]+list((generation/"backend").glob("*.cs"))
    updated["inputs"].extend({"path":p.relative_to(BASELINE).as_posix(),"sha256":sha(p),"bytes":p.stat().st_size} for p in paths)
    verify_inputs(updated,BASELINE)
    # Single manifest replacement is the publication point. New database is immutable before this.
    write(generation/"previous-manifest.json",manifest)
    write(BASELINE/current["databaseManifest"],updated)
    print(json.dumps({"status":"published","schemaVersion":5,"database":str(target),"counts":facts["counts"]}))

def main():
    parser=argparse.ArgumentParser();parser.add_argument("command",choices=["prepare","finalize","publish"]);parser.add_argument("generation",nargs="?")
    args=parser.parse_args()
    if args.command=="prepare":prepare()
    elif args.command=="finalize":finalize(Path(args.generation))
    else:publish(Path(args.generation))

if __name__=="__main__":main()
