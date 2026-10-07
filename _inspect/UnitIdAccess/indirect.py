from pathlib import Path
import re, json
ROOT=Path(__file__).resolve().parents[2]
source=(ROOT/'shcde-script-extender/src/SHCDESE.BepInEx/API/GameUnitManagerAPI.cs').read_text(encoding='utf-8-sig')
methods=list(re.finditer(r'\bpublic\s+(?:unsafe\s+)?[\w<>.*]+\s+(\w+)\s*\(([^)]*)\)\s*(?:\{|=>)',source))
lookup_methods={}
for i,m in enumerate(methods):
    body=source[m.end():methods[i+1].start() if i+1<len(methods) else len(source)]
    if 'unitId' in m.group(2) or 'UnitId' in m.group(2):
        if m.group(1) not in ('TryGetUnitById','TryGetUnitByIdEx','IsValidId'):
            lookup_methods[m.group(1)]={'args':m.group(2),'lookup': 'TryGetUnitById(' in body}
excluded={'.git','bin','obj','BepInEx','tests','packages','.inspect','_inspect','.tools','.native-analysis','.release-output','shcde-script-extender','UCP','x86_64'}
rows=[]
for p in ROOT.rglob('*.cs'):
    parts=p.relative_to(ROOT).parts
    if any(x in excluded or x.endswith('.Tests') for x in parts): continue
    text=p.read_text(encoding='utf-8-sig')
    for m in re.finditer(r'\b(GameUnitManagerAPI\.Instance|api|unitApi|units)\.(\w+)\s*\(',text):
        if m.group(2) not in lookup_methods: continue
        line=text[:m.start()].count('\n')+1
        rows.append({'path':str(p.relative_to(ROOT)),'line':line,'method':m.group(2),'contract':lookup_methods[m.group(2)],'code':text.splitlines()[line-1].strip()})
dest=Path(__file__).parent/'indirect.json'
dest.write_text(json.dumps(rows,indent=2).replace('\n','\r\n'),encoding='utf-8',newline='')
print('Indirect unit-ID methods:', ','.join(lookup_methods))
for row in rows: print(f'{row["path"]}:{row["line"]}: {row["code"]}')
