from pathlib import Path
import hashlib,json,re,struct,pefile
root=Path(__file__).resolve().parents[2]
folder=root/'Testmods/FixesSiegeTentPlacementTest/Offline'
prov=json.loads((folder/'callback-provenance.json').read_text())
source=Path(prov['externalSource']).read_bytes()
assert hashlib.sha256(source).hexdigest()==prov['sourceSha256'], 'Fixes source changed; re-audit callback'
a=(folder/'OriginalFixesCallback.cs').read_text()
b=(folder/'CounterfactualFixesCallback.cs').read_text().replace('CounterfactualFixesCallback','OriginalFixesCallback')
body=a[a.index('playerId) {')+len('playerId) {'):a.rfind('} }')].strip()
assert body in source.decode('utf-8').replace('\r\n','\n'), 'Original callback differs from external source'
assert b.replace('!playerApi.IsPlayerIdValid(playerId)','playerApi.IsPlayerIdValid(playerId)').replace('!playerApi.IsPlayerIdValid(targetId)','playerApi.IsPlayerIdValid(targetId)')==a, 'Counterfactual changed more than the two guards'
native=Path(r'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll')
assert hashlib.sha256(native.read_bytes()).hexdigest().upper()=='FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2', 'Native hash changed'
pe=pefile.PE(str(native))
y=[struct.unpack('<i',pe.get_data(0x2d2e54+i*8,4))[0] for i in range(8)]
bits=list(pe.get_data(0x312620,8))
constants=(folder/'NativeDirections.cs').read_text()
arrays=re.findall(r'new (?:int|byte)\[\] \{([^}]+)\}',constants)
assert [int(v) for v in arrays[0].split(',')]==y
assert [int(v) for v in arrays[1].split(',')]==bits
for name in ['Popularity.lordjson','Popularity.aivjson']:
 base=root/'Testmods/FixesBadThingPopularityTest/Fixtures'
 files=list((base/'A').rglob(name))
 assert len(files)==1
 assert files[0].read_bytes()==(base/'B'/files[0].relative_to(base/'A')).read_bytes()
print('PASS: reference native hash, eight-byte-stride directions, edge masks and two-guard-only counterfactual')
