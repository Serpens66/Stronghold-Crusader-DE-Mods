from pathlib import Path
import hashlib,json
out=Path('_inspect/BridgePlanningTests/historical-20261013-173119');out.mkdir(exist_ok=True)
base=Path('E:/ProgrammeE/Steam/steamapps/common/Stronghold Crusader Definitive Edition/BepInEx')
inputs={
 'Log_317.log':(base/'logs/Log_317.log','71A57AD65FFFD55F92792B6D8B96E9DCF63A33D9D1671C3A0C110468344EB97F'),
 'command.bin':(base/'diagnostics/EnemyBridgePathTest/20261013-153059-1121777/bridge-1-0.bin','3E471B66DEB3CC4237450EAF1406EC7E951E2E3A45AA15EB9F85D52FC030DC61'),
 'planning.bin':(base/'diagnostics/EnemyBridgePathTest/20261013-153059-1121777/bridge-1-5.bin','B82464117A2AC2E8DB26A73583AD84FF5108B9346944738924A5B1F60BA9F780')}
manifest={}
for name,(source,want) in inputs.items():
 data=source.read_bytes();actual=hashlib.sha256(data).hexdigest().upper();assert actual==want
 target=out/name
 if target.exists():assert target.read_bytes()==data
 else:target.write_bytes(data)
 manifest[name]={'source':str(source),'bytes':len(data),'sha256':actual}
(out/'manifest.json').write_bytes(json.dumps(manifest,indent=2).replace('\n','\r\n').encode())
print('PASS three frozen original files with complete hashes; no source modifications')
