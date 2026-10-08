from pathlib import Path
import json, re, subprocess, os
root=Path(__file__).resolve().parents[2]
audit=Path(__file__).resolve().parent
data=json.loads((audit/'migration-map.json').read_text())
def write(p,t):
    expected=t.replace('\r\n','\n').replace('\n','\r\n')
    p.write_bytes(expected.encode('utf-8')); assert p.read_bytes().decode('utf-8')==expected
changed=subprocess.check_output(['git','diff','--name-only','--diff-filter=M'],cwd=root).decode().splitlines()
for rel in changed:
    p=root/rel
    # Generated historical findings retain their original provenance, not new paths.
    if (rel.startswith('_inspect/') and p.suffix in {'.json','.txt'}) or '/before/' in rel or rel=='BugfixesAndQoL/_inspect/workspace-exe-inventory.txt':
        old=subprocess.check_output(['git','show','HEAD:'+rel],cwd=root).decode('utf-8-sig')
        write(p,old)
for base,dirs,names in os.walk(root):
    dirs[:]=[d for d in dirs if d not in {'.git','.tools','.native-analysis','bin','obj','BepInEx','shcde-script-extender','x86_64','.release-output'}]
    for name in names:
        p=Path(base)/name
        if p.suffix not in {'.csproj','.props','.targets','.ps1','.bat','.cs','.md','.json'}: continue
        if p.is_relative_to(audit) or 'CrusaderDE-Native-Baseline' in p.parts: continue
        try: t=p.read_bytes().decode('utf-8-sig')
        except UnicodeDecodeError: continue
        old=t
        for sep in ['/', '\\', '\\\\']:
            doubled=sep.join(['Shared','Adapters','APIShared','Adapters','APIShared'])
            single=sep.join(['Shared','Adapters','APIShared'])
            while doubled in t:t=t.replace(doubled,single)
        if p.is_relative_to(root/'_inspect/APISharedTests') and p.suffix=='.cs':
            t=t.replace('using Shared;','using APIShared.Internal;')
        if t!=old:write(p,t)
project=root/'APIShared/APIShared.csproj'
t=project.read_bytes().decode('utf-8-sig')
t=re.sub(r'(<Compile Include="src\\[^\"]+")><Link>Shared\\[^<]+</Link></Compile>',r'\1 />',t)
write(project,t)
for name,target in data['apiCopies'].items():
    p=root/'APIShared/src'/target
    t=p.read_bytes().decode('utf-8-sig')
    t=re.sub(r'// Initial provenance: [^\r\n]+', '// Initial provenance: a7888900e, Shared/'+name+'.cs. No automatic synchronization.', t)
    write(p,t)
print('Corrected adapter-path boundaries, test namespaces, source provenance and API project display links.')
