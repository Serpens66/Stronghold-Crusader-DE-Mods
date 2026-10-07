from pathlib import Path
import re, json, hashlib

ROOT = Path(__file__).resolve().parents[2]
EXCLUDED = {'.git', 'bin', 'obj', 'BepInEx', 'tests', 'packages', '.inspect', '_inspect', '.tools', '.native-analysis', '.release-output', 'shcde-script-extender', 'UCP', 'x86_64'}
def sources():
    for p in ROOT.rglob('*.cs'):
        parts = p.relative_to(ROOT).parts
        if any(x in EXCLUDED or x.endswith('.Tests') for x in parts): continue
        yield p

files = []
projects = {}
for p in sources():
    text = p.read_text(encoding='utf-8-sig')
    if not re.search(r'\.TryGetUnitById(?:Ex)?\s*\(', text): continue
    folder = p.parent
    while folder != ROOT and not list(folder.glob('*.csproj')): folder = folder.parent
    if folder == ROOT: raise RuntimeError(f'No project: {p}')
    project = next(folder.glob('*.csproj'))
    key = str(project.relative_to(ROOT))
    projects.setdefault(key, {'files': [], 'json': [], 'lifecycle': [], 'callbacks': []})
    rel = str(p.relative_to(ROOT))
    files.append({'path': rel, 'project': key, 'sha256': hashlib.sha256(p.read_bytes()).hexdigest(),
                  'calls': len(re.findall(r'\.TryGetUnitById(?:Ex)?\s*\(', text))})
    projects[key]['files'].append(rel)

for key, info in projects.items():
    folder = (ROOT / key).parent
    for p in list(folder.rglob('*.cs')) + [ROOT / key]:
        if any(x in EXCLUDED or x.endswith('.Tests') for x in p.relative_to(ROOT).parts): continue
        text = p.read_text(encoding='utf-8-sig')
        for category, regex in [('json', r'System\.Web\.Extensions|JavaScriptSerializer|System\.Text\.Json|Newtonsoft\.Json|DataContractJsonSerializer|JsonUtility|System\.Runtime\.Serialization\.Json'),
                                ('lifecycle', r'\b(?:OnDestroy|OnDisable|OnApplicationQuit|OnApplicationPause)\s*\('),
                                ('callbacks', r'\b(?:Update|LateUpdate|FixedUpdate|StartCoroutine|InvokeRepeating)\s*\(')]:
            for m in re.finditer(regex, text):
                info[category].append(f'{p.relative_to(ROOT)}:{text[:m.start()].count(chr(10))+1}: {text[m.start():m.start()+70].splitlines()[0]}')

dest = Path(__file__).parent / 'inventory.json'
dest.write_text(json.dumps({'files': files, 'projects': projects}, indent=2).replace('\n','\r\n'), encoding='utf-8', newline='')
print(f'{len(files)} source files, {sum(f["calls"] for f in files)} lookups, {len(projects)} projects')
for key, info in projects.items():
    print(key, 'JSON='+str(len(info['json'])), 'lifecycle='+str(len(info['lifecycle'])), 'callbacks='+str(len(info['callbacks'])))
    for category in ('json','lifecycle'):
        for hit in info[category]: print(' ',category,hit)
