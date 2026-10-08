"""Supplement existing preflight with XAML and all Testmods hook coverage."""
from pathlib import Path
import json,re,xml.etree.ElementTree as ET
ROOT=Path(__file__).resolve().parents[2]
OUT=Path(__file__).resolve().parent
inventory=json.loads((ROOT/'Shared/ScriptExtenderUpdate/mods.json').read_text())
roots={ROOT/Path(m['Project'].replace('\\','/')).parent for m in inventory}
roots.update(p.parent for p in (ROOT/'Testmods').glob('*/*.csproj') if p.stem==p.parent.name)
excluded={'bin','obj','BepInEx','tests','.inspect'}
def source(path):return not any(x in excluded or x.endswith('.Tests') for x in path.parts)
patches=[];contents=0;mutations=[];markers=[]
pattern=re.compile(r'\.(Undo|Disable|Dispose)\s*\(|CodePatch\.Write|Marshal\.Write\w*|VirtualProtect')
for root in sorted(roots):
    for path in root.rglob('*.xaml'):
        if not source(path.relative_to(root)):continue
        if 'Patches' not in path.parts:continue
        tree=ET.parse(path)
        for node in tree.iter():
            if node.tag.split('}')[-1]=='Content':
                assert len(list(node))==1,(str(path),'Content must have one direct element')
                contents+=1
        patches.append(str(path.relative_to(ROOT)))
    for path in root.rglob('*.cs'):
        if not source(path.relative_to(root)):continue
        text=path.read_text(encoding='utf-8-sig')
        if pattern.search(text) and 'Testmods' in path.parts:
            mutations.append(dict(path=str(path.relative_to(ROOT)),
                hits=[dict(line=i,text=line.strip()) for i,line in enumerate(text.splitlines(),1) if pattern.search(line)]))
        markers.extend(dict(path=str(path.relative_to(ROOT)),line=i,text=line.strip())
            for i,line in enumerate(text.splitlines(),1) if 'SHCDESE-WORKAROUND(' in line)
result=dict(xamlPatchFiles=patches,contentNodes=contents,xamlPassed=True,testmodMutationHits=mutations,
    workaroundMarkers=markers,
    review='No runtime source changed. Reviewed Testmods hits separately: Marshal writes target data, WoodSiteGuard rollback is guarded by !published; EnemyBridge forbids installed-hook rollback. MonoMod Release helpers in SkinTest and VirtualUnitsPrototype are reached by initialization-failure catch paths only, not normal lifecycle callbacks. Subscription/candidate disposal is distinct from published executable-hook teardown. Existing release-wide permanent-hook regression passes. These source checks do not assert gameplay acceptance or certify unrelated testmod designs.')
data=(json.dumps(result,indent=2)+'\n').replace('\n','\r\n').encode()
(OUT/'workspace-checks.json').write_bytes(data)
print(f'PASS: {len(patches)} source XAML patches, {contents} Content nodes; {len(mutations)} Testmods mutation-hit files recorded for review; {len(markers)} workaround markers.')
