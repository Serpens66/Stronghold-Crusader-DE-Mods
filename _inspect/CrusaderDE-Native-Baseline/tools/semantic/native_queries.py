"""Read-only, bounded queries. Missing evidence is never a negative observation."""
from __future__ import annotations
import argparse
import collections
import json
from pathlib import Path
from native_analysis import COMMANDS, coordinate, hx, identity, sha

def parser():
    p=argparse.ArgumentParser();p.add_argument("--database",required=True)
    sub=p.add_subparsers(dest="command",required=True)
    for command in sorted(COMMANDS):
        s=sub.add_parser(command)
        if command!="coverage":s.add_argument("value")
        if command in {"instructions","flow","global","feature"}:s.add_argument("--offset",type=int,default=0);s.add_argument("--limit",type=int,default=256)
        if command=="feature":s.add_argument("--include-document",action="store_true")
        if command=="trace":s.add_argument("--max-functions",type=int,default=128);s.add_argument("--max-operations",type=int,default=10000)
        if command=="hookcheck":
            s.add_argument("--type",required=True);s.add_argument("--backend",required=True);s.add_argument("--scheme")
            s.add_argument("--minimum",required=True,type=int);s.add_argument("--owner")
    return p

def packet(connection, value):
    current_hash=dict(connection.execute("SELECT key,value FROM metadata"))["current_native_hash"]
    try:rva=hx(coordinate(value))
    except ValueError:rva=None
    rows=connection.execute("SELECT payload FROM analysis_functions WHERE binary_hash=? AND rva=?",(current_hash,rva)).fetchall() if rva else []
    if not rows:
        entries=connection.execute("SELECT rva FROM functions WHERE binary_hash=? AND name=?",(current_hash,value)).fetchall()
        if len(entries)!=1:raise ValueError("Exact current function name or entry RVA required; missing/ambiguous selection")
        rows=connection.execute("SELECT payload FROM analysis_functions WHERE binary_hash=? AND rva=?",(current_hash,entries[0][0])).fetchall()
    if not rows:raise ValueError("Function analysis unavailable")
    return json.loads(rows[0][0])

def page(items, offset, limit):
    if offset<0 or not 1<=limit<=10000:raise ValueError("offset >= 0 and 1 <= limit <= 10000 required")
    return {"items":items[offset:offset+limit],"total":len(items),"offset":offset,"returned":len(items[offset:offset+limit]),
            "truncated":offset+limit<len(items),"nextOffset":offset+limit if offset+limit<len(items) else None}

def profiles(connection):
    return {k:json.loads(v) for k,v in connection.execute("SELECT key,payload FROM analysis_profiles")}

def freshness(connection):
    current,manifest,_=identity();ps=profiles(connection);p=ps.get("active")
    if not p:raise ValueError("Analysis unavailable; run Build-SemanticBaseline.ps1 Analysis")
    failures=[]
    if p["binaryHash"]!=current["currentNativeHash"]:failures.append("analysis/CURRENT hash mismatch")
    if p["digest"]!=manifest.get("analysis",{}).get("digest"):failures.append("analysis/manifest generation mismatch")
    if sha(p["nativePath"])!=p["binaryHash"]:failures.append("installed native changed")
    for name,assembly in p["installed"].items():
        if sha(assembly["path"])!=assembly["sha256"]:failures.append(name+" changed")
    for path,digest in p["sourceHashes"].items():
        if sha(path)!=digest:failures.append("source changed: "+path)
    return p,failures

def hookcheck(connection, start, hook_type, backend, scheme, minimum, owner=None):
    if minimum<1:raise ValueError("Positive requested minimum required")
    ps=profiles(connection);contract=ps.get("backends",{}).get(hook_type)
    if not contract or contract["backend"]!=backend:raise ValueError("Unknown/unverified hook backend profile")
    if sha(contract["assembly"]["path"])!=contract["assembly"]["sha256"]:raise ValueError("Installed hook backend changed")
    n=coordinate(start);candidates=[]
    for row in connection.execute("SELECT payload FROM analysis_functions"):
        f=json.loads(row[0])
        if any(coordinate(r["start"])<=n<=coordinate(r["endInclusive"]) for r in f["ranges"]):candidates.append(f)
    if len(candidates)!=1:raise ValueError("Hook has no unique containing function")
    f=candidates[0];by={coordinate(i["rva"]):i for i in f["instructions"]}
    if n not in by:raise ValueError("Hook begins inside an instruction or at non-code data")
    required=max(minimum,contract.get("minimum",0))
    if hook_type=="NativeDetour":
        if scheme not in contract["schemes"]:raise ValueError("Explicit supported detour scheme required")
        if minimum>contract["schemes"][scheme]:raise ValueError("NativeDetour patch minimum is determined by its schema; a larger requested minimum is not an installed backend option")
        if n!=coordinate(f["functionRva"]):raise ValueError("NativeDetour requires a function entry, not a call-site")
        required=max(required,contract["schemes"][scheme])
    elif scheme:raise ValueError("Inline hook has no NativeDetour scheme")
    displaced=[];end=n;issues=[];gaps=[]
    for iteration in range(16):
        displaced=[];end=n
        while end-n<required:
            if end not in by:raise ValueError("Displacement crosses an unknown instruction/function gap")
            ins=by[end]
            if not ins.get("icedVerified"):raise ValueError("Unverified decoder bytes")
            displaced.append(ins);end+=ins["length"]
            if end-n<required and ins.get("icedFlow") in {"Return","UnconditionalBranch"}:gaps.append("Backend function-end/padding selection requires a runtime backend check")
        if hook_type!="NativeDetour":break
        widened=end
        for ins in f["instructions"]:
            if ins.get('icedFlow') not in {'Call','ConditionalBranch','UnconditionalBranch'}:continue
            for t in ins.get("targets",[]):
                if ins.get('icedFlow')=='Call' and t and coordinate(t)==n:continue
                if t and n<=coordinate(t)<end:widened=max(widened,coordinate(ins["rva"])+ins["length"])
        if widened==end:break
        required=widened-n
    else:gaps.append("Backend self-branch widening did not converge")
    current_hash=f["binaryHash"]
    incoming=[]
    for row in connection.execute("SELECT from_rva,to_rva,type FROM xrefs WHERE binary_hash=?",(current_hash,)):
        source,target,kind=row
        if target and n<coordinate(target)<end and (not source or not n<=coordinate(source)<end):incoming.append({"source":source,"target":target,"type":kind})
    if incoming:issues.append("Known incoming reference enters displaced interior")
    spans=[]
    for row in connection.execute("SELECT payload FROM hook_spans WHERE binary_hash=?",(current_hash,)):
        s=json.loads(row[0])
        if coordinate(s["startRva"])<end and n<coordinate(s["endRva"]):spans.append(s)
    for row in connection.execute("SELECT payload FROM analysis_features"):
        feature=json.loads(row[0])
        spans.extend(s for s in feature.get("hookSpans",[]) if coordinate(s["startRva"])<end and n<coordinate(s["endRva"]))
    unique={s["spanId"]:s for s in spans};overlaps=list(unique.values())
    if overlaps:
        for s in overlaps:
            if not owner or owner!=s["spanId"]:issues.append("Existing documented hook span overlaps; ownership must be resolved")
    unresolved=[i["rva"] for i in f["instructions"] if i.get("icedFlow") in {"IndirectBranch","IndirectCall"}]
    if unresolved:gaps.append("Indirect control-flow targets are not exhaustively established")
    if end not in by:gaps.append("Continuation is not a decoded instruction in the containing function")
    if any("UNKNOWN" in x for i in displaced for x in i.get("liveIn",[])+i.get("liveOut",[])):gaps.append("Liveness contains unknown ABI/control-flow state")
    if any(i.get("icedFlow")=="ConditionalBranch" for i in displaced):gaps.append("Branch in displaced block: callback ordering and relocation require explicit audit")
    if hook_type=="NativeDetour" and any(i.get("icedFlow") in {"Call","IndirectCall","IndirectBranch","UnconditionalBranch"} for i in displaced):gaps.append("Relocation/backend entry behavior requires runtime validation")
    return {"status":"conflict" if issues else "incomplete" if gaps else "statically-passed", "function":f["functionRva"],
        "startRva":hx(n),"endRva":hx(end),"displacedByteCount":end-n,"type":hook_type,"backend":backend,"scheme":scheme,
        "instructions":displaced,"incoming":incoming,"overlaps":overlaps,"conflicts":sorted(set(issues)),"gaps":sorted(set(gaps)),
        "runtimeRequirements":["Validate actual Scheme/DisplacedByteCount and live original bytes before publication.",
            "Validate generated callback, trampoline, pointer slot and continuation with this exact backend.",
            "Audit complete feature semantics and source-discovered hooks/events; static results are not installation approval."]}

def trace(connection, root, max_functions, max_operations):
    if max_functions<1 or max_operations<1:raise ValueError("Positive trace budgets required")
    visited={};queue=[root];frontier=[];nodes=[];edges=[];unknown=[]
    while queue:
        value=queue.pop(0)
        if value in visited:continue
        if len(visited)>=max_functions:frontier.extend([value]+queue);break
        try:f=packet(connection,value)
        except ValueError:unknown.append({"function":value,"reason":"no exported function"});continue
        value=f["functionRva"]
        if value in visited:continue
        visited[value]=f
        if f["flow"]["status"]!="complete":unknown.append({"function":value,"reason":f["flow"]["status"]});continue
        ops=f["flow"]["operations"]
        params=f["flow"].get("parameters",[])
        if len(nodes)+len(ops)+len(params)>max_operations:frontier.extend([value]+queue);break
        for param in params:nodes.append({"function":value,"id":value+"/parameter/"+str(param["ordinal"]),"parameter":param})
        for op in ops:
            prefix=value+"/";node={"function":value,"id":prefix+op["id"],"operation":op};nodes.append(node)
            for index,input_value in enumerate(op["inputs"]):
                if input_value.get("definition"):edges.append({"from":prefix+input_value["definition"],"to":node["id"],"input":index,"kind":"def-use"})
                elif input_value.get("parameterOrdinal") is not None:edges.append({"from":value+"/parameter/"+str(input_value["parameterOrdinal"]),"to":node["id"],"input":index,"kind":"parameter-use"})
            if op["opcode"]=="CALLIND":unknown.append({"site":op["site"],"reason":"indirect call"})
            if op["opcode"]=="BRANCHIND":unknown.append({"site":op["site"],"reason":"recovered jump-table destinations are not a proof of all indirect edges"})
            if op["opcode"]=="CALL" and not op.get("callTarget"):unknown.append({"site":op["site"],"reason":"call destination unavailable"})
            if op["opcode"]!="CALL" or not op.get("callTarget"):continue
            target=op["callTarget"];queue.append(target)
            try:callee=packet(connection,target)
            except ValueError:unknown.append({"site":op["site"],"reason":"unavailable callee"});continue
            params=callee["flow"].get("parameters",[])
            claim=connection.execute("SELECT abi_confidence FROM function_claims WHERE binary_hash=? AND function_rva=? AND abi_confidence='confirmed'",(callee["binaryHash"],target)).fetchone()
            trusted=bool(claim) or callee["signatureSource"] in {"USER_DEFINED","IMPORTED"}
            if not trusted or len(params)!=len(op["inputs"])-1:
                unknown.append({"site":op["site"],"target":target,"reason":"unverified or mismatched recovered ABI; argument bindings withheld"});continue
            for param,arg in zip(params,op["inputs"][1:]):
                edges.append({"from":node["id"],"to":target+"/parameter/"+str(param["ordinal"]),"kind":"call-argument",
                              "argument":arg,"ordinal":param["ordinal"],"authority":"decompiler-derived with separately supported callee ABI"})
            for ret in callee["flow"].get("operations",[]):
                if ret["opcode"]=="RETURN" and len(ret["inputs"])>1 and op.get("output"):
                    edges.append({"from":target+"/"+ret["id"],"to":node["id"],"kind":"return-binding","values":ret["inputs"][1:],"authority":"decompiler-derived"})
    present={n["id"] for n in nodes}
    external_endpoints=sorted({e[x] for e in edges for x in ["from","to"] if e[x] not in present})
    return {"nodes":nodes,"edges":edges,"functions":sorted(visited),"frontier":sorted(set(frontier)),"unknowns":unknown,"endpointsOutsideReturnedNodes":external_endpoints,
            "status":"partial" if frontier or unknown else "static-graph-complete",
            "budgets":{"functions":max_functions,"operations":max_operations},
            "limitations":["Memory aliasing, persistent state and execution are not inferred.","Graph completeness is not a complete feature audit."]}

def run(connection,argv):
    args=parser().parse_args(argv);p,stale=freshness(connection)
    if stale:return {"status":"stale","reasons":stale,"remedy":"Build-SemanticBaseline.ps1 Analysis"}
    command=args.command
    if command=="coverage":
        statuses=collections.Counter();count=0;disagreements=0;failures=[]
        for row in connection.execute("SELECT payload FROM analysis_functions"):
            f=json.loads(row[0]);count+=1;statuses[f["flow"]["status"]]+=1;disagreements+=sum(not i.get("icedVerified") for i in f["instructions"])
            if f["flow"]["status"]=="failed":failures.append({"function":f["functionRva"],"error":f["flow"].get("error")})
        features=[json.loads(r[0]) for r in connection.execute("SELECT payload FROM analysis_features")]
        return {"status":"current", "binaryHash":p["binaryHash"],"functions":count,"flowStatuses":dict(statuses),"decoderDisagreements":disagreements,
                "features":len(features),"featuresWithoutEntryCandidates":[f["id"] for f in features if not f["entrypoints"]],
                "featureIndex":[{"id":f["id"],"title":f["title"],"coverage":f.get("analysisCoverage"),"contractChecks":f.get("contractChecks")} for f in features],
                "failedFlows":failures,
                "auditReview":"Indexed existing contracts; no confidence promotion or runtime verification", "sourceProvenance":p["sources"],
                "baselineProvenance":p["baselineProvenance"]}
    if command=="hookcheck":return hookcheck(connection,args.value,args.type,args.backend,args.scheme,args.minimum,args.owner)
    if command=="trace":return trace(connection,args.value,args.max_functions,args.max_operations)
    if command=="feature":
        rows=connection.execute("SELECT payload FROM analysis_features WHERE id=?",(args.value,)).fetchall()
        if not rows:raise ValueError("Exact feature ID required; use coverage to inspect availability")
        f=json.loads(rows[0][0]);f["functionPage"]=page(f.pop("relatedFunctions"),args.offset,args.limit)
        f["contractPage"]=page(f.pop("documentedContracts",[]),args.offset,args.limit)
        if not args.include_document:f.pop("document",None)
        return f
    if command=="global":
        n=coordinate(args.value);matches=[]
        for row in connection.execute("SELECT payload FROM analysis_functions"):
            f=json.loads(row[0])
            for i in f["instructions"]:
                refs=[r for r in i["references"] if r.get("target") and coordinate(r["target"])==n]
                if refs:matches.append({"function":f["functionRva"],"instruction":i["rva"],"references":refs,"memory":i.get("memory",[]),
                                        "accesses":sorted({r["type"] for r in refs if "READ" in r["type"] or "WRITE" in r["type"]}),
                                        "classification":"known static reference; indexed alias accesses may be absent"})
        return {"address":hx(n),"references":page(matches,args.offset,args.limit),"completeness":"known references only; absence is not proof of no writer"}
    f=packet(connection,args.value)
    if command=="instructions":return {"function":f["functionRva"],"ranges":f["ranges"],"instructions":page(f["instructions"],args.offset,args.limit),"blocks":page(f["blocks"],args.offset,args.limit),"liveness":f["liveness"]}
    flow=dict(f["flow"])
    if "operations" in flow:flow["operations"]=page(flow["operations"],args.offset,args.limit)
    return {"function":f["functionRva"],"signatureSource":f["signatureSource"],"flow":flow}
