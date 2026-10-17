from pathlib import Path
import hashlib, json, shutil, subprocess, datetime

ROOT = Path(__file__).resolve().parents[2]
OUT = Path(__file__).resolve().parent
BASE = ROOT / '_inspect/CrusaderDE-Native-Baseline'
SE = ROOT / 'shcde-script-extender'
GAME = Path('E:/ProgrammeE/Steam/steamapps/common/Stronghold Crusader Definitive Edition')

def sha(p):
    with p.open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest().upper()

def write(p, data):
    p.write_bytes((json.dumps(data, indent=2) + '\n').replace('\n', '\r\n').encode())

def git(*args):
    return subprocess.check_output(['git', '-C', str(SE), *args], text=True).strip()

current = json.loads((BASE / 'CURRENT.json').read_text())
sem = BASE / current['semanticDirectory']
manifest = json.loads((BASE / current['databaseManifest']).read_text())
identity = json.loads((sem / 'IDENTITY.json').read_text())
target = git('rev-parse', 'v2.14.2')
assert target == git('rev-parse', 'HEAD')
assert not git('status', '--porcelain', '--untracked-files=no')
assert identity['scriptExtenderCommit'] == '5908e1f12437deb7ef5f5b1dc2d64e301178fba3'
for item in manifest['binaries']:
    assert sha(Path(item['sourcePath'])) == item['hash']
diff = git('diff', identity['scriptExtenderCommit'], target)
(OUT / 'upstream.diff').write_bytes(diff.replace('\r\n','\n').replace('\n','\r\n').encode())
changed = git('diff', '--name-only', identity['scriptExtenderCommit'], target).splitlines()
assert set(changed) == {'CHANGELOG.md','src/SHCDESE.BepInEx/API/GameAssetManagerAPI.cs','src/SHCDESE.BepInEx/Bootstrap/Plugin.cs','src/SHCDESE.BepInEx/ManagedHooks/MyAudioManager_Hooks.cs'}
print('Identities and complete diff confirmed.', flush=True)
backup = OUT / 'backup'
assert not backup.exists(), 'Existing backup must never be overwritten.'
backup.mkdir()
paths = [BASE/'CURRENT.json', BASE/'CURRENT.md']
paths += [p for p in sem.iterdir() if p.is_file()]
for name in ['sources','knowledge','validation','resources']:
    paths += [p for p in (sem/name).rglob('*') if p.is_file()]
comparison = BASE/'diff'/(manifest['identities']['oldNativeHash'][:8]+'-'+current['currentNativeHash'][:8])
paths += [p for p in comparison.iterdir() if p.is_file()]
paths += [BASE/manifest['database']['path']]
backed = []
for p in paths:
    relative = p.relative_to(BASE)
    dest = backup/relative
    dest.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(p,dest)
    backed.append({'path':str(relative),'sha256':sha(p)})
write(OUT/'backup-files.json',backed)
print(f'Backed up {len(backed)} baseline files including active database.',flush=True)
protected = []
for directory in [BASE/current['currentNativeHash'], sem/'exports',sem/'ghidra', comparison/'exports',comparison/'ghidra', GAME/'BepInEx/plugins']:
    protected += [p for p in directory.rglob('*') if p.is_file()]
protected += [SE/'src/SHCDESE.BepInEx/bin/net481/SHCDESE.dll',SE/'mod_output/000shcdese/SHCDESE.dll']
write(OUT/'protected-before.json',[{'path':str(p),'sha256':sha(p)} for p in protected])
write(OUT/'preflight.json',{'date':'2026-10-17','previousCommit':identity['scriptExtenderCommit'],'targetCommit':target,'targetTree':git('rev-parse','HEAD^{tree}'),'nativeHash':current['currentNativeHash'],'managedHash':identity['managedHash'],'changedFiles':changed,'protectedFileCount':len(protected),'installedExtenderSha256':sha(GAME/'BepInEx/plugins/000shcdese/SHCDESE.dll'),'runtimeAcceptance':'Not performed; no extender or mod build/install.'})
print(f'Protected-file snapshot complete: {len(protected)} files.',flush=True)
