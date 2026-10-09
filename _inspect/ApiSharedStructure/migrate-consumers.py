import pathlib,json,re
root=pathlib.Path.cwd(); moves=json.loads((root/'_inspect/ApiSharedStructure/moves.json').read_text())
excluded={'APIShared','shcde-script-extender','.git','.tools','.native-analysis','.release-output','bin','obj','BepInEx','before','ApiSharedStructure','ApiSharedSubmodule'}
active=[root/p.split('/')[0] for p in []]
dirs=[p for p in root.iterdir() if p.is_dir() and p.name not in excluded and p.name!='_inspect']
inventory=(root/'Shared/Tools/Validation/source-check-projects.txt').read_text().splitlines()
dirs += list({(root/p).parent for p in inventory if p.startswith('_inspect/')})
for base in dirs:
 for path in base.rglob('*'):
  if not path.is_file() or path.suffix not in {'.cs','.csproj','.ps1','.md','.json'} or any(p in excluded for p in path.relative_to(base).parts):continue
  text=path.read_text(encoding='utf-8-sig'); updated=text
  for old,new in moves.items():
   updated=updated.replace(old,new).replace(old.replace('/','\\'),new.replace('/','\\'))
   pattern=r'\s*,\s*'.join(re.escape('"'+p+'"') for p in old.split('/'))
   replacement=', '.join('"'+p+'"' for p in new.split('/'))
   updated=re.sub(pattern,lambda m,r=replacement:r,updated)
  if updated!=text:path.write_bytes(updated.replace('\r\n','\n').replace('\n','\r\n').encode())
