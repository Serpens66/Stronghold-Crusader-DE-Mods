from pathlib import Path
import re,json
root=Path(__file__).resolve().parents[2]
configpath=root/'Shared/Tools/Release/release-projects.json'
config=json.loads(configpath.read_text())
pattern=re.compile(r'^\s*\[BepInDependency\((ApiSharedGuid|"APIShared_Serp"),\s*("[^"]+"|ApiSharedVersion)\)\]\s*$',re.M)
records=[]
for path in root.rglob('*Plugin.cs'):
    if any(x in path.parts for x in ['shcde-script-extender','_inspect','.inspect','BepInEx','bin','obj']):continue
    text=path.read_bytes().decode('utf-8-sig');matches=list(pattern.finditer(text))
    if len(matches)<=1:continue
    def version(match):
        arg=match.group(2)
        if arg.startswith('"'):return arg.strip('"')
        return re.search(r'const\s+string\s+ApiSharedVersion\s*=\s*"([^"]+)"',text).group(1)
    effective=max((version(m) for m in matches),key=lambda v:tuple(map(int,v.split('.'))))
    first=matches[0];owner=first.group(1)
    for match in reversed(matches[1:]):text=text[:match.start()]+text[match.end():]
    # Normalize only this existing declaration, not the plugin's runtime behavior.
    text=pattern.sub('    [BepInDependency('+owner+', "'+effective+'")]',text,count=1)
    text=text.replace('\r\n','\n').replace('\n','\r\n');path.write_bytes(text.encode())
    for name in config['ApiShared']['Consumers']:
        moddir=config.get('ProjectDirectories',{}).get(name,name)
        if path.is_relative_to(root/moddir):config['ApiShared']['Consumers'][name]=effective
    records.append({'plugin':path.relative_to(root).as_posix(),'effectiveMinimum':effective,'previousDeclarations':[version(m) for m in matches]})
# Keep the configuration's original layout; replace only changed consumer values.
original=configpath.read_bytes().decode()
for name,value in config['ApiShared']['Consumers'].items():
    original=re.sub(r'("'+re.escape(name)+r'"\s*:\s*")[^"]+("\s*[,}])',lambda m:m.group(1)+value+m.group(2),original)
configpath.write_bytes(original.encode())
report=json.dumps(records,indent=2).replace('\n','\r\n')+'\r\n'
(Path(__file__).parent/'dependency-cleanup.json').write_bytes(report.encode())
print(report)
