"""Integration acceptance, read-only CLI checks, and restoration to a separate index."""
import argparse
import json
from pathlib import Path
import sqlite3
import subprocess
from native_analysis import BASELINE, WORKSPACE, TOOL, identity, sha, write
from database_manifest import database_facts, verify_inputs

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--restore',action='store_true');args=parser.parse_args()
    current,manifest,semantic=identity();verify_inputs(manifest,BASELINE)
    generation=BASELINE/manifest['analysis']['generation'];database=BASELINE/manifest['database']['path']
    profile=json.loads((generation/'profile.json').read_text(encoding='utf-8'))
    database_before=sha(database)
    for path,digest in profile['projectHashes'].items():
        if sha(BASELINE/path)!=digest:raise ValueError('Reference project changed: '+path)
    query=TOOL/'query.ps1'
    advanced_path=generation/'advanced.json'
    advanced=json.loads(advanced_path.read_text(encoding='utf-8')) if advanced_path.exists() else None
    cases=[['instructions','0xCEB10'],['flow','0xCEB10'],['trace','0x29650','--max-functions','8','--max-operations','10000'],
           ['global','0x67E8400','--limit','10'],['feature','market-price-queries'],['feature','workshop-idle-delay'],['coverage'],
           ['hookcheck','0xCEB10','--type','NativeDetour','--backend','RedBird.Backends.NativeX64','--scheme','Indirect','--minimum','6','--owner','hook-ai-buy-price-entry'],
           ['hookcheck','0x13AC85','--type','X64InlineHook','--backend','RedBird.X64','--minimum','7','--owner','workshop-0x13AC85']]
    if advanced:
        type_path=next(t['path'] for t in advanced['layouts'] if t.get('fields'))
        cases.extend([['slice','0xCEB10','--return','--direction','backward'],['slice','0xCEB10','--parameter','2','--direction','forward'],
            ['memory','0xCEB10'],['memory','all','--model','unit-manager-records','--field','ai-state','--access','write','--limit','10'],
            ['switch','0xD7AD0'],['summary','0xCEB10'],['type',type_path,'--limit','10'],['changes','0xCEB10'],
            ['changes','all','--feature','workshop-idle-delay','--changed-only','--limit','5']])
    outputs={};slice_results=[]
    for command in cases:
        result=subprocess.run(['powershell.exe','-NoProfile','-ExecutionPolicy','Bypass','-File',str(query),*command],cwd=WORKSPACE,capture_output=True,encoding='utf-8')
        if result.returncode:raise RuntimeError('CLI failed: '+str(command)+'\n'+result.stdout+'\n'+result.stderr)
        payload=json.loads(result.stdout);outputs[' '.join(command[:2])]=payload
        if command[0]=='slice':slice_results.append(payload)
        if payload.get('status') in {'stale','unavailable'}:raise ValueError(payload)
        print('QUERY_OK '+ ' '.join(command[:2]),flush=True)
    buy=outputs['hookcheck 0xCEB10'];workshop=outputs['hookcheck 0x13AC85']
    assert buy['displacedByteCount']==10 and not buy['conflicts'],buy
    assert workshop['displacedByteCount']==17 and not workshop['conflicts'],workshop
    assert outputs['flow 0xCEB10']['flow']['status']=='complete'
    assert outputs['instructions 0xCEB10']['instructions']['total']==10
    coverage=outputs['coverage'];assert coverage['functions']==4478 and coverage['decoderDisagreements']==0
    assert coverage['flowStatuses']['failed']==3
    docs={p.stem.lower().replace('_','-') for p in (semantic/'knowledge').glob('*.md')}
    assert docs=={f['id'] for f in coverage['featureIndex']},'Missing audit catalog entries'
    for feature in [outputs['feature market-price-queries'],outputs['feature workshop-idle-delay']]:
        assert feature['contractPage']['total']>0
        assert all(c['status']=='matched' for c in feature['contractChecks'] if c['kind']=='span-bytes')
    if advanced:
        forward=next(r for r in slice_results if r['direction']=='forward')
        backward=next(r for r in slice_results if r['direction']=='backward')
        parameters={n['parameter']['ordinal'] for n in backward['nodes']['items'] if 'parameter' in n}
        assert parameters=={0,2,3},parameters
        assert any(n.get('operation',{}).get('opcode')=='RETURN' for n in forward['nodes']['items'])
        assert any(a['modelAssociations'][0]['model']=='buy-price' for a in outputs['memory 0xCEB10']['accesses']['items'])
        assert outputs['memory all']['accesses']['total']>0
        assert outputs['summary 0xCEB10']['documentedContracts'][0]['validation']=='matched'
        switch=outputs['switch 0xD7AD0']['switches']['items'][0]
        assert len(switch['mappings'])==10 and len(switch['rawMappings'])==19
        assert switch['defaultTargets']==['0xD7C38'] and len(switch['dataSources'])==2
        # Independent file-byte cross-check of the observed two-level dispatch, not only JSON agreement.
        import struct
        native=Path(profile['nativePath']).read_bytes();header=struct.unpack_from('<I',native,0x3c)[0]
        section_table=header+24+struct.unpack_from('<H',native,header+20)[0]
        def read_rva(rva,count):
            for index in range(struct.unpack_from('<H',native,header+6)[0]):
                section=section_table+index*40;va,raw_size,raw_offset=struct.unpack_from('<III',native,section+12)
                if va<=rva and rva+count<=va+raw_size:return native[raw_offset+rva-va:raw_offset+rva-va+count]
            raise ValueError('Switch table has no file backing')
        selector_bytes=read_rva(0xD7D28,19);targets=struct.unpack('<III',read_rva(0xD7D1C,12))
        observed={(int(r['encodedLabel32'],16),int(r['target'],16)) for r in switch['rawMappings']}
        assert observed=={(good+6,targets[selector]) for good,selector in enumerate(selector_bytes)}
        assert outputs['changes 0xCEB10']['comparisons']['total']>0
    negative=subprocess.run(['powershell.exe','-NoProfile','-ExecutionPolicy','Bypass','-File',str(query),'hookcheck','0xCEB11','--type','NativeDetour','--backend','RedBird.Backends.NativeX64','--scheme','Indirect','--minimum','6'],cwd=WORKSPACE,capture_output=True,encoding='utf-8')
    assert negative.returncode!=0 and json.loads(negative.stdout)['status']=='unavailable'
    assert sha(database)==database_before,'Read-only queries mutated SQLite'
    result={'status':'ok','queryCases':len(cases),'negativeCliCases':1,'databaseUnchanged':True,'referenceProjectUnchanged':True,
            'featureCount':len(docs),'marketDisplacement':10,'workshopDisplacement':17,'restoration':'not-requested'}
    if args.restore:
        scratch=generation/'restore-check.sqlite'
        roles={b['role']:b for b in manifest['binaries']};ids=manifest['identities']
        comparison=BASELINE/'diff'/(ids['oldNativeHash'][:8]+'-'+ids['currentNativeHash'][:8])
        raw=BASELINE/current['currentNativeHash'];managed=semantic/'managed'/ids['managedHash'][:8]
        values={'semantic-dir':semantic,'database':scratch,'current-hash':ids['currentNativeHash'],'old-hash':ids['oldNativeHash'],'managed-hash':ids['managedHash'],
            'current-native':roles['current-native']['sourcePath'],'old-native':roles['historical-native']['sourcePath'],'managed-assembly':roles['current-managed']['sourcePath'],
            'current-native-size':roles['current-native']['size'],'old-native-size':roles['historical-native']['size'],'managed-size':roles['current-managed']['size'],
            'old-exports':comparison/'exports','raw-exports':raw/'exports','managed-dir':managed,
            'patterns':semantic/'sources/pattern-matches.jsonl','source-types':semantic/'sources/source-types.jsonl','type-fields':semantic/'sources/type-fields.jsonl',
            'vtable-members':semantic/'sources/vtable-members.jsonl','delegates':semantic/'sources/delegates.jsonl','rtti-vtables':semantic/'exports/rtti-vtables.jsonl',
            'xaml':semantic/'resources/xaml-index.jsonl','xaml-links':semantic/'resources/xaml-managed-links.jsonl','version-matches':comparison/'version-matches.jsonl'}
        for name in ['function-claims','hook-spans','api-contracts','chore-opcodes','chore-contracts','chore-observations','chore-evidence']:values[name]=semantic/'knowledge'/(name+'.jsonl')
        command=['build-index']
        for key,value in values.items():command.extend(['--'+key,str(value)])
        import semantic_tools
        parsed=semantic_tools.build_parser().parse_args(command);parsed.func(parsed)
        restored=database_facts(scratch)
        assert restored['counts']==manifest['validation']['counts'],(restored['counts'],manifest['validation']['counts'])
        assert restored['integrityCheck']=='ok' and int(restored['metadata']['schema_version'])==manifest['database']['schemaVersion']
        # Check the reconstructed large machine/p-code values, not only row counts.
        old=sqlite3.connect(database.resolve().as_uri()+'?mode=ro',uri=True);new=sqlite3.connect(scratch.resolve().as_uri()+'?mode=ro',uri=True)
        try:
            tables=['analysis_functions','analysis_features','analysis_sources','analysis_profiles']
            if advanced:tables+=['analysis_memory','analysis_contracts','analysis_layouts','analysis_changes']
            for table in tables:
                left=old.execute('SELECT * FROM '+table+' ORDER BY 1,2');right=new.execute('SELECT * FROM '+table+' ORDER BY 1,2')
                sentinel=object()
                while True:
                    a=next(left,sentinel);b=next(right,sentinel)
                    if a is sentinel or b is sentinel:assert a is sentinel and b is sentinel;break
                    assert a==b,'Restored analysis differs: '+table
        finally:old.close();new.close()
        assert sha(database)==database_before,'Restoration changed active index'
        result['restoration']='current schema tables and full analysis payloads reproduced in separate database'
    write(generation/'acceptance.json',result);print(json.dumps(result))

if __name__=='__main__':main()
