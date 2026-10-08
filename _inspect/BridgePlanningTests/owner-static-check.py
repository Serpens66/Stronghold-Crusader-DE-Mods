from pathlib import Path
import re,hashlib,json
root=Path.cwd()
for name in ['APIShared','Testmods/EnemyBridgePathTest']:
 for p in (root/name/'src').rglob('*.cs'):
  s=p.read_text();assert not re.search(r'JavaScriptSerializer|System\.Text\.Json|Newtonsoft|DataContractJsonSerializer|JsonUtility|System\.Web\.Extensions',s),p
  assert not re.search(r'\b(?:void|IEnumerator)\s+(?:OnDestroy|OnDisable|OnApplicationQuit|Update|LateUpdate|FixedUpdate|OnApplicationPause)\s*\(|\bStartCoroutine\s*\(',s),p
  raw=p.read_bytes();assert b'\n' not in raw.replace(b'\r\n',b''),p
sites={int(a,16) for a in re.findall(r'new Site\(0x([0-9A-Fa-f]+)',(root/'Testmods/EnemyBridgePathTest/src/BridgeNativeDefinition.cs').read_text())}
owned=sites-{0xe49d0,0x111c00};assert len(owned)==30
for name in ['APIShared','BugfixesAndQoL','shcde-script-extender']:
 for p in (root/name).rglob('*.cs'):
  if any(q in p.parts for q in ['obj','bin','BepInEx']):continue
  s=p.read_text()
  for a in re.findall(r'(?:AddDetour|InstallConnectivityObserver)\([^;]*?(?:libraryBase\s*\+\s*|libraryBase\s*,\s*)0x([0-9A-Fa-f]+)',s,re.S):assert int(a,16) not in owned,(p,a)
fixes=Path('D:/CDesktopLink/Unterlagen/Mods/Stronghold Crusader DE/Fremde Mods/shcde-fixes-main/src/shcde-fixes/Detours/AIDetours.cs').read_text()
assert 'c_game_ai_ladder_objective_rank_hook' in fixes and 'SelectLadderObjective' in fixes and 'returnAddress + 0x160' in fixes
api=(root/'APIShared/src/UnitCommands/ManualProbeRepairGuards.cs').read_text()
assert api.count('libraryBase + 0xE49D0')==1 and 'pcl = ExecuteProbePclRebuild' in api and 'originalProbePclRebuild(manager, force)' in api
assert 'runOriginal = !nativeManualProbe' in api and 'completed && called ? (int?)result : null' in api
print('PASS recursive runtime JSON/lifecycle/CRLF;30 private owners;APIShared/Main/Extender direct ownership inventory;Fixes ladder entry excluded;owner suppression/result contract')
