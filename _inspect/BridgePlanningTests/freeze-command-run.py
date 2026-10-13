from pathlib import Path
import hashlib,json,shutil,importlib.util,re
out=Path('_inspect/BridgePlanningTests/historical-20261013-164010');out.mkdir(exist_ok=True)
game=Path('E:/ProgrammeE/Steam/steamapps/common/Stronghold Crusader Definitive Edition/BepInEx')
raw=(game/'logs/Log_314.log').read_bytes()
marker=b'[Message:   BepInEx] BepInEx '
starts=[m.start() for m in re.finditer(re.escape(marker),raw)]+[len(raw)]
parts=[raw[starts[i]:starts[i+1]] for i in range(len(starts)-1)]
selected=[p for p in parts if b'2026-10-13 16:40:40.026' in p]
assert len(selected)==1
part=selected[0]
files={'LogOutput-original.log':raw,'process-original.log':part}
src=game/'diagnostics/EnemyBridgePathTest/20261013-143935-1640952'
expected={'bridge-1-0.bin':'348FF47C8DCCD43D91B98EB06CE255F7AF15A1640F2135EBB130610BF95AC831','bridge-2-0.bin':'A54211D866E6EE4444B2B8F27FE41ABA795ED92BC7DF9AFEFCDA48F7738FA091'}
for name,want in expected.items():
    data=(src/name).read_bytes();assert hashlib.sha256(data).hexdigest().upper()==want;files[name]=data
manifest={}
for name,data in files.items():
    dest=out/name
    if dest.exists():assert dest.read_bytes()==data
    else:dest.write_bytes(data)
    manifest[name]={'sha256':hashlib.sha256(data).hexdigest().upper(),'bytes':len(data)}
spec=importlib.util.spec_from_file_location('bridge_analysis','_inspect/EnemyGateBuildingContextAudit/bridge-log-analysis.py');a=importlib.util.module_from_spec(spec);spec.loader.exec_module(a)
result=a.analyze(part)
args=[]
for _,record in result['records']:
    if record.get('kind')=='command-pre' and str(record.get('op')) in ('62036','468120'):
        args.append(record)
assert len(args)==2,args
manifest['recoveredOriginalPre']=args
manifest['sessionSummary']=result['sessionSummary']
(out/'manifest.json').write_bytes(json.dumps(manifest,indent=2,ensure_ascii=False).replace('\n','\r\n').encode('utf-8'))
print(json.dumps({'sessions':result['sessionSummary'],'originalPre':args},ensure_ascii=False))
