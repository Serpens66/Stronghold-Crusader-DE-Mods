from pathlib import Path
import re,json,hashlib,xml.etree.ElementTree as ET
ROOT=Path(__file__).resolve().parents[2]
GAME=Path(r'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition')
OUT=Path(__file__).parent
def digest(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
lines=(GAME/'BepInEx/LogOutput.log').read_text(encoding='utf-8-sig',errors='replace').splitlines()
starts=[i for i,l in enumerate(lines) if re.search(r'^\[Message:\s+BepInEx\] BepInEx .* - Stronghold Crusader Definitive Edition',l)]
start=starts[-1];latest=lines[start:]
loaded=[m.groups() for l in latest if (m:=re.search(r'Loading \[(.+) ([\d.]+)\]',l))]
errors=[{'line':start+i+1,'text':l} for i,l in enumerate(latest) if 'Tried to access unit index' in l]
registry=json.loads((ROOT/'Shared/ScriptExtenderUpdate/mods.json').read_text(encoding='utf-8-sig'))
installed=list((GAME/'BepInEx/plugins').rglob('*.dll'))
rows=[]
for entry in registry:
    project=ROOT/entry['Project']
    if not project.exists():continue
    xml=ET.parse(project).getroot()
    asm=next((e.text for e in xml.iter() if e.tag.split('}')[-1]=='AssemblyName'),project.stem)
    plugin=ROOT/entry['Plugin'] if entry.get('Plugin') else None
    text=plugin.read_text(encoding='utf-8-sig') if plugin and plugin.exists() else ''
    name=re.search(r'PluginName\s*=\s*"([^"]+)"',text)
    if not name or not any(n==name.group(1) for n,v in loaded):continue
    local=ROOT/entry['Package']/(asm+'.dll')
    candidates=[p for p in installed if p.name==asm+'.dll']
    rows.append({'name':name.group(1),'project':entry['Project'],'local':str(local),'localSha256':digest(local) if local.exists() else None,
                 'installed':[{'path':str(p),'sha256':digest(p),'matchesLocalOutput':local.exists() and digest(p)==digest(local)} for p in candidates]})
current=json.loads((ROOT/'_inspect/CrusaderDE-Native-Baseline/CURRENT.json').read_text(encoding='utf-8-sig'))
native=GAME/'Stronghold Crusader Definitive Edition_Data/Plugins/x86_64/CrusaderDE.dll'
assert digest(native)==current['currentNativeHash']
data={'log':str(GAME/'BepInEx/LogOutput.log'),'firstLine':start+1,'logSha256':digest(GAME/'BepInEx/LogOutput.log'),
      'loaded':loaded,'errors':errors,'ownInstalledAssemblies':rows,'nativeHash':digest(native),
      'extenderHash':digest(GAME/'BepInEx/plugins/000shcdese/SHCDESE.dll')}
(OUT/'latest-run.json').write_bytes(json.dumps(data,indent=2,ensure_ascii=False).replace('\n','\r\n').encode('utf-8'))
excerpt=[l for l in latest if 'Loading [' in l or 'Tried to access unit index' in l or 'Failed to get capturing unit' in l]
(OUT/'latest-run.txt').write_bytes(('\r\n'.join(excerpt)+'\r\n').encode('utf-8'))
print(f'{len(loaded)} loaded plugins; {len(errors)} unit-index errors; {len(rows)} own assembly comparisons recorded.')
for row in rows:print(row['name'], 'installed match='+str(any(x['matchesLocalOutput'] for x in row['installed'])))
