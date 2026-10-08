"""Finish-scope and provenance review for this accepted update."""
from pathlib import Path
import json,subprocess
ROOT=Path(__file__).resolve().parents[2];OUT=Path(__file__).resolve().parent
def git(*args):return subprocess.check_output(['git','-C',str(ROOT),*args]).decode('utf-8')
changed=git('diff','--name-only').splitlines()
assert all(p.startswith('_inspect/CrusaderDE-Native-Baseline/') or p=='Shared/ScriptExtenderUpdate/Invoke-ScriptExtenderUpdate.ps1' for p in changed),changed
assert not any(Path(p).name.lower()=='readme.md' for p in changed)
records={}
for name in ['source-types','type-fields','vtable-members','delegates','patterns','pattern-matches','source-files']:
    path=f'_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/sources/{name}.jsonl'
    before=[json.loads(row) for row in git('show',f'HEAD:{path}').splitlines()]
    after=[json.loads(row) for row in (ROOT/path).read_text().splitlines()]
    assert len(before)==len(after)
    changes=0
    for old,new in zip(before,after):
        old.pop('gitCommit',None);new.pop('gitCommit',None)
        if old!=new:
            changes+=1
            source=new.get('sourcePath',new.get('path',''))
            assert source in ['src/SHCDESE.BepInEx/Detours/BulkUnitDetours.cs','CHANGELOG.md'],(name,source)
    records[name]=dict(rows=len(after),changedBeyondCommit=changes)
state=json.loads((ROOT/'.inspect/ScriptExtenderUpdates/2.14.0-2.14.1/state.json').read_text())
assert state['BaselineValidated'] and not state['CompletedBuilds'] and not state['ExtenderBuilt']
decision=json.loads((ROOT/'_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/validation/script-extender-update.json').read_text())
assert not decision['ghidraRequired'] and not decision['changedInputs']
result=dict(modSourcesAndMetadataUnchanged=True,readmesUnchanged=True,sourceRecords=records,
    builds=state['CompletedBuilds'],ghidraRequired=False,
    toolRepairs=['Exclude APIShared/examples from productive plugin inventory','Skip deletion commits while retaining exact historical source-hash validation'])
(OUT/'final-review.json').write_bytes((json.dumps(result,indent=2)+'\n').replace('\n','\r\n').encode())
print('PASS: final scope, zero builds, unchanged API/type records apart from provenance; only changed upstream files alter source facts.')
