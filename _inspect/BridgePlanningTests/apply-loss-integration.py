from pathlib import Path
p=Path('Testmods/EnemyBridgePathTest')
changes={
'src/BridgePlanningCapture.cs': [('requiredEntryPhase=4,','requiredEntryPhases=4/5,'),('internal void Begin(long value)','internal static bool IsDecisionCapturePhase(int phase)=>phase==4||phase==5;\n        internal void Begin(long value)')],
'src/BridgeDecisionTrace.cs': [('return value.PrePlan[1]==4;','return BridgePlanningCapture.IsDecisionCapturePhase(value.PrePlan[1]);'),('this.testUnit=testUnit;','this.testUnit=testUnit;\n            lossHistory=new BridgeLossHistory(Read,Observe);buildingIndex.Refreshed=entries=>{lossHistory.Retain(entries);lossHistory.Sample("index-observation");};'),('planningMapIdentity=new object();','lossHistory.Begin();planningMapIdentity=new object();'),('Detail=Summary()+","+DamageStatus','Detail=Summary()+","+lossHistory.Status+","+DamageStatus'),('scope.Rebuilt=true;if(testCapture==null)','scope.Rebuilt=true;lock(captureGate){lossHistory.Retain(buildingIndex.Entries);lossHistory.Sample("completed-topology-rebuild");}if(testCapture==null)')]
}
for name,replacements in changes.items():
 f=p/name;t=f.read_text(encoding='utf-8-sig')
 for old,new in replacements:
  assert old in t,(name,old);t=t.replace(old,new)
 f.write_bytes(t.replace('\r\n','\n').replace('\n','\r\n').encode('utf-8'))
for name in ['EnemyBridgePathTest.csproj','EnemyBridgePathTest.PolicyTests.csproj']:
 f=p/name;t=f.read_text(encoding='utf-8-sig');t=t.replace('</Project>','<ItemGroup><Compile Include="src\\BridgeLossHistory.cs" /></ItemGroup>\n</Project>');f.write_bytes(t.replace('\r\n','\n').replace('\n','\r\n').encode('utf-8'))
