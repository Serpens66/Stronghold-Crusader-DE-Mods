"""Compare observed contracts across explicitly matched functions, without claiming equivalence."""
from collections import Counter
import hashlib
import json


def value_shape(value, operations, visiting=frozenset(), depth=0, budget=None):
    budget=[256] if budget is None else budget
    budget[0]-=1
    if depth>=32 or budget[0]<0:return {'unknown':'expression budget/depth'}
    definition=value.get('definition')
    if definition in visiting:return {'unknown':'cyclic expression'}
    if definition:
        op=operations.get(definition)
        if op is None:return {'unknown':'missing definition'}
        if op['opcode'] in {'CALL','CALLIND'}:
            return {'callResult':True,'width':value.get('size'),'binding':'not assumed'}
        inputs=[value_shape(v,operations,visiting|{definition},depth+1,budget) for v in op.get('inputs',[])]
        shape={'operation':op['opcode'],'width':value.get('size'),
               'inputFingerprints':[hashlib.sha256(json.dumps(v,sort_keys=True).encode()).hexdigest() for v in inputs]}
        unknowns=sorted({u for v in inputs for u in v.get('unknowns',[]) + ([v['unknown']] if 'unknown' in v else [])})
        if unknowns:shape['unknowns']=unknowns
        return shape
    if value.get('parameterOrdinal') is not None:return {'parameter':value['parameterOrdinal'],'width':value.get('size')}
    if value.get('constant'):return {'constant':value.get('offset'),'width':value.get('size')}
    return {'inputSpace':value.get('space'),'inputOffset':value.get('offset'),'width':value.get('size')}


def observed(function, call_identity=None):
    flow=function['flow'];operations={o['id']:o for o in flow.get('operations',[])}
    conditions=[];memory=[];calls=[]
    for op in operations.values():
        code=op['opcode'];inputs=op.get('inputs',[])
        if code=='CBRANCH' and len(inputs)>1:
            conditions.append(value_shape(inputs[1],operations))
        if code in {'LOAD','STORE'} and len(inputs)>1:
            memory.append({'access':'read' if code=='LOAD' else 'write','address':value_shape(inputs[1],operations),
                           'size':(op.get('output') or {}).get('size') if code=='LOAD' else inputs[2].get('size') if len(inputs)>2 else None})
        if code in {'CALL','CALLIND'}:
            target=op.get('callTarget')
            calls.append({'target':call_identity(target) if target and call_identity else target,
                          'indirect':code=='CALLIND','arity':max(0,len(inputs)-1),
                          'arguments':[value_shape(v,operations) for v in inputs[1:]]})
    return {'flowStatus':flow['status'],'parameters':[{'ordinal':p['ordinal'],'type':p.get('type'),
                'size':(p.get('representative') or {}).get('size')} for p in flow.get('parameters',[])],
            'variadic':function.get('variadic'),'callingConvention':function.get('callingConvention'),
            'operationCounts':dict(Counter(o['opcode'] for o in operations.values())),
            'conditions':conditions,'memory':memory,'calls':calls,
            'returns':[[value_shape(v,operations) for v in o.get('inputs',[])[1:]] for o in operations.values() if o['opcode']=='RETURN'],
            'machineCounts':dict(Counter(i.get('mnemonic',i.get('assembly','').split(' ')[0]) for i in function['instructions'])),
            'blockEdges':[(b['id'],b['successors']) for b in flow.get('blocks',[])],
            'machineBytesSha256':hashlib.sha256(b''.join(bytes.fromhex(i['bytes']) for i in function['instructions'])).hexdigest().upper()}


def compare(old,new,match,call_map=None,feature_ids=()):
    call_map=call_map or {}
    # Only separately confirmed unique identities normalize relocated callees.
    before=observed(old,lambda target:call_map.get(target,{'unmatchedOldTarget':target}))
    after=observed(new)
    categories=['parameters','variadic','callingConvention','operationCounts','conditions','memory','calls','returns','machineCounts','blockEdges']
    differences={key:{'old':before[key],'new':after[key]} for key in categories if before[key]!=after[key]}
    complete=before['flowStatus']=='complete' and after['flowStatus']=='complete'
    if not complete:
        for key in ['parameters','operationCounts','conditions','memory','calls','returns','blockEdges']:differences.pop(key,None)
    return {'oldBinaryHash':old['binaryHash'],'newBinaryHash':new['binaryHash'],'oldRva':old['functionRva'],'newRva':new['functionRva'],
            'match':match,'affectedFeatureCandidates':sorted(feature_ids),'differences':differences,
            'machineBytesChanged':before['machineBytesSha256']!=after['machineBytesSha256'],
            'oldMachineBytesSha256':before['machineBytesSha256'],'newMachineBytesSha256':after['machineBytesSha256'],
            'flowCoverage':{'old':before['flowStatus'],'new':after['flowStatus']},
            'status':'observed-differences' if differences or before['machineBytesSha256']!=after['machineBytesSha256'] else 'no-difference-in-compared-observations' if complete else 'incomplete',
            'completeness':'selected-static-observations' if complete else 'partial',
            'limits':['Function correspondence retains existing confidence; this is not semantic equivalence.',
                      'Absolute data constants may differ through relocation; field migration is not inferred.',
                      'CFG identifiers and recovered prototypes can change with decompiler reconstruction.',
                      'Unselected or failed flows withhold data-flow comparisons.']}


def rows(old_packets,new_packets,matches,features):
    old={p['functionRva']:p for p in old_packets};new={p['functionRva']:p for p in new_packets}
    candidates={}
    for m in matches:
        if m['confidence']=='confirmed':candidates.setdefault(m['oldRva'],set()).add(m['newRva'])
    call_map={k:next(iter(v)) for k,v in candidates.items() if len(v)==1}
    membership={}
    for feature in features:
        for rva in feature['relatedFunctions']:membership.setdefault(rva,set()).add(feature['id'])
    for match in matches:
        if match['oldRva'] not in old or match['newRva'] not in new:continue
        yield compare(old[match['oldRva']],new[match['newRva']],match,call_map,membership.get(match['newRva'],()))


def import_exports(connection,generation):
    from pathlib import Path
    paths=sorted(Path(generation).glob('changes-*.jsonl'))
    if not paths:return
    connection.execute('CREATE TABLE IF NOT EXISTS analysis_changes(old_hash TEXT,new_hash TEXT,old_rva TEXT,new_rva TEXT,payload TEXT,PRIMARY KEY(old_hash,new_hash,old_rva,new_rva))')
    for path in paths:
        with path.open(encoding='utf-8') as stream:
            for line in stream:
                row=json.loads(line)
                connection.execute('INSERT INTO analysis_changes VALUES(?,?,?,?,?)',(row['oldBinaryHash'],row['newBinaryHash'],row['oldRva'],row['newRva'],json.dumps(row)))
