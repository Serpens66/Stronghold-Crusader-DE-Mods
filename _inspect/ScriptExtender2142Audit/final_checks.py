from pathlib import Path
import hashlib, json, subprocess

ROOT=Path(__file__).resolve().parents[2]
OUT=Path(__file__).resolve().parent
BASE=ROOT/'_inspect/CrusaderDE-Native-Baseline'
def read(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
    with p.open('rb') as f: return hashlib.file_digest(f,'sha256').hexdigest().upper()
facts=read(OUT/'preflight.json')
pointer=read(BASE/'CURRENT.json')
sem=BASE/pointer['semanticDirectory']
manifest=read(BASE/pointer['databaseManifest'])
identity=read(sem/'IDENTITY.json')
before=read(OUT/'backup'/pointer['databaseManifest'])
for item in [identity,manifest['identities']]:
    assert item['scriptExtenderCommit']==facts['targetCommit']
    assert item['scriptExtenderTree']==facts['targetTree']
    assert item['currentNativeHash']==facts['nativeHash']
assert manifest['analysis']==before['analysis'], 'Analysis generation changed'
assert manifest['database']['schemaVersion']==before['database']['schemaVersion']
for key,value in before['validation']['counts'].items():
    if key.startswith('analysis_'):
        assert manifest['validation']['counts'][key]==value,(key,value)
decision=read(sem/'validation/script-extender-update.json')
assert decision['scriptExtenderCommit']==facts['targetCommit']
assert not decision['ghidraRequired'] and not decision['changedInputs'] and not decision['missingInputs']
contracts=[]
for name in ['delegates.jsonl','patterns.jsonl','source-types.jsonl','type-fields.jsonl','vtable-members.jsonl']:
    def rows(path):
        result=[]
        for line in path.read_text().splitlines():
            row=json.loads(line)
            for key in ('gitCommit','sourceFileHash','sourceLine'): row.pop(key,None)
            result.append(row)
        return result
    assert rows(sem/'sources'/name)==rows(OUT/'backup'/pointer['semanticDirectory']/'sources'/name), name
    contracts.append(name)
asset=next(json.loads(line) for line in (sem/'sources/source-files.jsonl').read_text().splitlines() if json.loads(line)['path']=='src/SHCDESE.BepInEx/API/GameAssetManagerAPI.cs')
assert asset['gitCommit']==facts['targetCommit']
assert asset['sha256']==sha(ROOT/'shcde-script-extender'/asset['path'])
protected=read(OUT/'protected-before.json')
differences=[item['path'] for item in protected if not Path(item['path']).is_file() or sha(Path(item['path']))!=item['sha256']]
assert not differences, differences
tracked=subprocess.check_output(['git','diff','--name-only','--','_inspect/CrusaderDE-Native-Baseline'],cwd=ROOT,text=True).splitlines()
new=['Shared/Tools/ScriptExtenderUpdate/2.14.1-2.14.2.compatibility.json','Shared/Tools/ScriptExtenderUpdate/2.14.1-2.14.2.review.md']
checked=[]
for relative in tracked+new:
    p=ROOT/relative
    data=p.read_bytes()
    if p.suffix in ['.json','.jsonl','.md','.ps1','.tsv','.h']:
        assert b'\n' not in data.replace(b'\r\n',b''),relative
        if p.suffix=='.json': read(p)
        if p.suffix=='.jsonl':
            for line in data.decode('utf-8-sig').splitlines():
                if line.strip(): json.loads(line)
        checked.append(relative)
result={'status':'passed','sourceCommit':facts['targetCommit'],'sourceTree':facts['targetTree'],'protectedFilesUnchanged':len(protected),'analysisGenerationPreserved':True,'schemaVersion':manifest['database']['schemaVersion'],'ghidraRun':False,'sourceContractsUnchangedExceptProvenance':contracts,'assetSourceHashConfirmed':True,'jsonAndCrlfChecked':checked,'runtimeAcceptance':'Not performed; installed SE remains 2.14.1.0.'}
(OUT/'final-checks.json').write_bytes((json.dumps(result,indent=2)+'\n').replace('\n','\r\n').encode())
print(json.dumps(result,indent=2))
