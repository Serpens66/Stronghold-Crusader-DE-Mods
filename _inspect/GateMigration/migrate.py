from pathlib import Path
import re
R=Path('.');src=R/'Testmods/EnemyGatePathfindingTest/src';dest=R/'BugfixesAndQoL/src/GateRouting';api=R/'APIShared/src/Pathfinding/GateRoutes/Native'
def write(p,s):
 p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(s.replace('\r\n','\n').replace('\n','\r\n').encode('utf-8'))
def read(p):return p.read_text(encoding='utf-8-sig')
native={'EnemyGatePathfindingNativeDefinition.cs','DirectCursorCallAdapterEmitter.cs','CapturerComparisonAdapterEmitter.cs','DirectionFilterAdapterEmitter.cs','AiTacticalTargetAdapterEmitter.cs','NativeCallbackAdapterEmitter.cs','AssassinGateDescentAdapter.cs'}
for p in src.glob('*.cs'):
 if p.name=='EnemyGatePathfindingTestPlugin.cs':continue
 s=read(p)
 if p.name in native:
  write(api/p.name,s.replace('namespace EnemyGatePathfindingTest','namespace APIShared.GateRouting.Native'))
 else:
  s=s.replace('namespace EnemyGatePathfindingTest','namespace BugfixesAndQoL.GateRouting')
  s='using APIShared.GateRouting.Native;\n'+s
  if p.name.startswith(('Temporary','Macemen','NativeAssassin','AttackOrder')):s='// TEMP_GATE_MIGRATION_DIAGNOSTICS: retained for old/new acceptance; see removal inventory.\n'+s
  write(dest/p.name,s)
# explicit main compile items, no source link to legacy testmod
p=R/'BugfixesAndQoL/BugfixesAndQoL.csproj';s=read(p);items='\n  <ItemGroup>\n'+''.join('    <Compile Include="src\\GateRouting\\'+p.name+'" />\n' for p in sorted(dest.glob('*.cs')))
for name in ['GameBuildingFootprint','PathDecisionAggregate','DeferredSnapshotRefreshRequest']:
 paths=list((R/'Shared').rglob(name+'.cs'));assert len(paths)==1
 inc='..\\'+str(paths[0]).replace('/','\\')
 if name+'.cs' not in s:items+='    <Compile Include="'+inc+'" />\n'
s=s.replace('</Project>',items+'  </ItemGroup>\n</Project>');write(p,s)
