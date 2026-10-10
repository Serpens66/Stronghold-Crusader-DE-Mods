from pathlib import Path
import re,hashlib,json
root=Path.cwd()
for name in ['APIShared','Testmods/EnemyBridgePathTest']:
 for p in (root/name/'src').rglob('*.cs'):
  s=p.read_text(encoding='utf-8');assert not re.search(r'JavaScriptSerializer|System\.Text\.Json|Newtonsoft|DataContractJsonSerializer|JsonUtility|System\.Web\.Extensions',s),p
  lifecycle_source=s
  if p.relative_to(root).as_posix()=='APIShared/src/GameModes/GameplayModeGate.cs':
   # Explicit reviewed ordinary data object, not a Unity component. Only these
   # two argument-bearing methods are lifecycle-notification consumers.
   assert re.search(r'public sealed class GameplayModeGate\s*\{',s)
   assert not re.search(r'\bclass\s+GameplayModeGate\s*:',s)
   lifecycle_source=re.sub(r'public void Update\((?:MissionLifecycleNotification notification|GameModeSnapshot snapshot)\)', 'public void ReviewedNotificationConsumer()',s)
  assert not re.search(r'\b(?:void|IEnumerator)\s+(?:OnDestroy|OnDisable|OnApplicationQuit|Update|LateUpdate|FixedUpdate|OnApplicationPause)\s*\(|\bStartCoroutine\s*\(',lifecycle_source),p
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
api=(root/'BugfixesAndQoL/src/UnitCommands/Attack/ManualProbeRepairGuards.cs').read_text(encoding='utf-8')
assert api.count('libraryBase + 0xE49D0')==1 and 'pcl = ExecuteProbePclRebuild' in api and 'originalProbePclRebuild(manager, force)' in api
assert 'runOriginal = !nativeManualProbe' in api and 'completed && called ? (int?)result : null' in api
broker=(root/'APIShared/src/Pathfinding/GateRoutes/EnemyBridgeDiagnosticBridge.cs').read_text(encoding='utf-8')
assert 'never owns hooks' in broker and 'BeginTopology' in broker and 'EndTopology' in broker
print('PASS recursive runtime JSON/lifecycle/CRLF;30 private owners;APIShared/Main/Extender direct ownership inventory;Fixes ladder entry excluded;Main topology owner/APIShared observation broker suppression/result contract')
