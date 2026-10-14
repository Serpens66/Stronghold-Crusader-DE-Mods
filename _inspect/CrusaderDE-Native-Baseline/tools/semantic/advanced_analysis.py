"""Reproducible address/contract observations derived from immutable machine packets."""
from __future__ import annotations
import collections
import hashlib
import json
from pathlib import Path
from memory_accesses import accesses

TABLES = ['analysis_memory', 'analysis_contracts', 'analysis_layouts']


def create_tables(connection):
    connection.executescript('''
        CREATE TABLE IF NOT EXISTS analysis_memory(binary_hash TEXT, function_rva TEXT, operation TEXT, model TEXT, field TEXT, payload TEXT);
        CREATE INDEX IF NOT EXISTS analysis_memory_model ON analysis_memory(binary_hash,model,field);
        CREATE INDEX IF NOT EXISTS analysis_memory_function ON analysis_memory(binary_hash,function_rva);
        CREATE TABLE IF NOT EXISTS analysis_contracts(binary_hash TEXT,rva TEXT,payload TEXT,PRIMARY KEY(binary_hash,rva));
        CREATE TABLE IF NOT EXISTS analysis_layouts(path TEXT PRIMARY KEY,payload TEXT);
    ''')


def import_exports(connection, generation):
    generation = Path(generation)
    path = generation/'advanced.json'
    if not path.exists(): return
    create_tables(connection)
    metadata = json.loads(path.read_text(encoding='utf-8'))
    for layout in metadata['layouts']:
        connection.execute('INSERT INTO analysis_layouts VALUES(?,?)',(layout['path'],json.dumps(layout)))
    for contract in metadata['contracts']:
        connection.execute('INSERT INTO analysis_contracts VALUES(?,?,?)',(contract['binaryHash'],contract['functionRva'],json.dumps(contract)))
    connection.execute('INSERT INTO analysis_profiles VALUES(?,?)',('advanced',json.dumps({k:v for k,v in metadata.items() if k not in {'layouts','contracts'}})))
    counts = collections.Counter();seen=set()
    for path in sorted(generation.glob('memory-*.jsonl')):
        with path.open(encoding='utf-8') as stream:
            for line in stream:
                row = json.loads(line)
                key=(row['function'],row['operation'])
                if row['binaryHash']!=metadata['binaryHash'] or key in seen:
                    raise ValueError('Memory export identity mismatch or duplicate operation')
                seen.add(key)
                counts[row['function']] += 1
                associations = row.get('modelAssociations') or [None]
                for match in associations:
                    connection.execute('INSERT INTO analysis_memory VALUES(?,?,?,?,?,?)',(
                        row['binaryHash'],row['function'],row['operation'],match['model'] if match else None,
                        match['field'] if match else None,json.dumps(row,separators=(',',':'))))
    expected = {r['function']: r['count'] for r in metadata['memoryCoverage'] if r['count']}
    if dict(counts) != expected:
        raise ValueError('Advanced memory exports do not match captured coverage')
    connection.execute("UPDATE metadata SET value='6' WHERE key='schema_version'")


def summarize(function, memory, documented=()):
    instructions = function['instructions']; flow = function['flow']
    writes = sorted({r['register'] for i in instructions for r in i.get('registers',[]) if 'Write' in r['access']})
    flags = 0
    for i in instructions:
        flags |= i.get('flagsWritten',0) | i.get('flagsUndefined',0) | i.get('flagsSet',0) | i.get('flagsCleared',0)
    calls = [i['rva'] for i in instructions if i.get('icedFlow') in {'Call','IndirectCall'}]
    result = {'binaryHash':function['binaryHash'],'functionRva':function['functionRva'],
              'authority':'machine/decompiler observations; no automatic semantic confirmation',
              'parameters':flow.get('parameters',[]),'returns':[o for o in flow.get('operations',[]) if o['opcode']=='RETURN'],
              'machineWrites':writes,'machineFlagsWrittenOrUndefined':flags,'machineCallSites':calls,
              'memoryAccessCounts':dict(collections.Counter(a['access'] for a in memory['accesses'])),
              'memoryCoverage':memory['status'],'documentedContracts':[],
              'decompilerDiagnostics':flow.get('diagnostics',[]),
              'limitations':['Writes are possible decoded writes, not a preservation guarantee.',
                             'Call side effects and aliased memory remain open.']}
    body = b''.join(bytes.fromhex(i['bytes']) for i in instructions)
    digest = hashlib.sha256(body).hexdigest().upper()
    ranges = function['ranges']
    contiguous = len(ranges)==1 and len(body)==int(ranges[0]['endInclusive'],16)-int(ranges[0]['start'],16)+1
    for declaration in documented:
        if declaration['binaryHash']!=function['binaryHash'] or declaration['functionRva']!=function['functionRva']: continue
        matches = contiguous and digest==declaration['functionBytesSha256'] and declaration.get('evidenceStatus')=='current' and \
                  bool(instructions) and all(i.get('icedVerified') for i in instructions)
        no_writes = all(a['access']!='write' for a in memory['accesses'])
        compatible = matches and flow['status']=='complete' and not flow.get('diagnostics') and not calls and no_writes
        result['documentedContracts'].append({**declaration,'validation':'matched' if compatible else 'requires-review',
                                               'machineBytesSha256':digest,'flowComplete':flow['status']=='complete',
                                               'observedWritesAbsent':no_writes,'observedCallsAbsent':not calls})
    return result


def export(generation, packets, baseline, models_file, sha, write):
    generation, baseline = Path(generation), Path(baseline)
    types_path = generation/'work/TYPES.json'
    types = json.loads(types_path.read_text(encoding='utf-8'))
    profile = json.loads((generation/'profile.json').read_text(encoding='utf-8'))
    if types['binaryHash']!=profile['binaryHash'] or types['profile']!=profile['factsDigest'] or \
       sha(types_path)!=types_path.with_name(types_path.name+'.sha256').read_text().strip():
        raise ValueError('Ghidra type export identity/integrity mismatch')
    models = json.loads(Path(models_file).read_text(encoding='utf-8'))
    for record in models['models']+models['functionContracts']:
        evidence=record['evidence']; source=baseline/evidence['source']
        record['evidenceStatus']='current' if source.exists() and sha(source)==evidence['sha256'] else 'stale'
    eligible = [m for m in models['models'] if m['evidenceStatus']=='current']
    layouts={t['path']:t for t in types['types']}
    coverage, contracts = [], []; shard=-1; writer=None; shard_bytes=0
    try:
        for index, function in enumerate(packets):
            memory=accesses(function,eligible,layouts)
            coverage.append({'function':function['functionRva'],'status':memory['status'],'count':len(memory['accesses']),
                             'associated':sum(bool(a['modelAssociations']) for a in memory['accesses']),
                             'unknownExpressions':sum(any(e['unknowns'] for e in a['expressions']) for a in memory['accesses'])})
            contracts.append(summarize(function,memory,models['functionContracts']))
            for row in memory['accesses']:
                row['binaryHash']=function['binaryHash']
                line=json.dumps(row,ensure_ascii=False,separators=(',',':'))+'\r\n';size=len(line.encode('utf-8'))
                if size>32*1024*1024:raise ValueError('Memory observation exceeds export ceiling')
                if writer is None or shard_bytes+size>32*1024*1024:
                    if writer:writer.close()
                    shard+=1;shard_bytes=0;writer=(generation/f'memory-{shard:03X}.jsonl').open('w',encoding='utf-8',newline='')
                writer.write(line);shard_bytes+=size
            if index%100==0:print(f'ADDRESS_PROGRESS functions={index+1}/{len(packets)}',flush=True)
    finally:
        if writer:writer.close()
    # Return operations are bounded individually by the existing packet validator.
    metadata={'schemaVersion':1,'binaryHash':profile['binaryHash'],'layouts':types['types'],'models':models['models'],
              'contracts':contracts,'memoryCoverage':coverage,
              'limitations':['Only selected High P-code supplies memory expressions.',
                             'Existing audited contracts retain their original confidence; new summaries are observations.']}
    write(generation/'advanced.json',metadata)
    if (generation/'advanced.json').stat().st_size>=50*1024*1024:
        raise ValueError('Advanced metadata too large for Git; split before publication')
    return {'layouts':len(layouts),'memoryAccesses':sum(c['count'] for c in coverage),
            'associatedMemoryAccesses':sum(c['associated'] for c in coverage),'contracts':len(contracts),
            'staleModels':[m['id'] for m in models['models'] if m['evidenceStatus']!='current']}
