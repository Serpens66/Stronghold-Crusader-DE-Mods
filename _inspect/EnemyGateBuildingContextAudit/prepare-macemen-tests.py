from pathlib import Path
root=Path.cwd()
def write(p,s):
 p=root/p; p.parent.mkdir(parents=True,exist_ok=True); b=s.replace('\r\n','\n').replace('\n','\r\n').encode('utf-8'); p.write_bytes(b); assert p.read_bytes()==b
for name in ['EnemyGatePathfindingTest.csproj','EnemyGatePathfindingTest.PolicyTests.csproj']:
 p=Path('Testmods/EnemyGatePathfindingTest')/name
 s=(root/p).read_text(encoding='utf-8-sig')
 item='  <ItemGroup><Compile Include="src\\MacemenDiagnosticAggregate.cs" />'
 if name=='EnemyGatePathfindingTest.csproj': item+='<Compile Include="src\\MacemenCommandDiagnostics.cs" />'
 else: item+='<Compile Include="tests\\MacemenDiagnosticTests.cs" />'
 s=s.replace('</Project>',item+'</ItemGroup>\n</Project>');write(p,s)
p=Path('Testmods/EnemyGatePathfindingTest/tests/Program.cs');s=(root/p).read_text();s=s.replace('assertions += CompactDiagnosticTests.Run();','assertions += CompactDiagnosticTests.Run();\n                assertions += MacemenDiagnosticTests.Run();');write(p,s)
write(Path('Testmods/EnemyGatePathfindingTest/tests/MacemenDiagnosticTests.cs'),'''// TEMP_MACEMEN_GATE_DIAGNOSTICS: aggregate behavior, not a native simulation.
using System;
using System.Diagnostics;
namespace EnemyGatePathfindingTest
{
    internal static class MacemenDiagnosticTests
    {
        internal static int Run()
        {
            int assertions = 0;
            Action<bool> check = b => { assertions++; if (!b) throw new InvalidOperationException("Macemen diagnostic assertion"); };
            check(MacemenDiagnosticAggregate.Classify(9,true,true)==MacemenTarget.GateAttack);
            check(MacemenDiagnosticAggregate.Classify(36,true,true)==MacemenTarget.GateAttack);
            check(MacemenDiagnosticAggregate.Classify(3,true,true)==MacemenTarget.GroundOnGate);
            check(MacemenDiagnosticAggregate.Classify(3,false,true)==MacemenTarget.GroundElsewhere);
            check(MacemenDiagnosticAggregate.Classify(6,false,true)==MacemenTarget.Moat);
            check(MacemenDiagnosticAggregate.Classify(7,false,true)==MacemenTarget.Moat);
            check(MacemenDiagnosticAggregate.Classify(38,true,true)==MacemenTarget.Unknown);
            var aggregate = new MacemenDiagnosticAggregate();
            var unit = new MacemenUnit(10,100,2,5,500,3,1,20,20,2,0,true);
            var reused = new MacemenUnit(10,101,2,5,500,3,1,20,20,2,0,true);
            check(!unit.SameIdentity(reused));
            long start=Stopwatch.GetTimestamp();
            for (int i=0;i<200000;i++)
            {
                int player=i%8+1, gate=i%64+1;
                var before=new MacemenUnit(i+1,(uint)i+1,player,5,500,3,1,20,20,player,0,true);
                var sample=new MacemenSample(i,start,1,1,i,i+1,0,1,1,before,before,
                    new MacemenGate(gate,(uint)gate+1000,1,0,1,0),true);
                aggregate.Record(MacemenStage.Search,MacemenTarget.GroundElsewhere,3,1,sample);
            }
            check(aggregate.Total==200000); check(aggregate.ActiveCombinations==64);
            long total=0,rejected=0,changes=0; int rows=0;
            aggregate.Drain((key,bucket)=> { rows++;total+=bucket.Count;rejected+=bucket.RejectedEdges;
                changes+=bucket.TargetChanges;check(bucket.Unstable==0); });
            check(total==200000);check(rejected==200000);check(rows==64);check(changes==200000-64);
            check(aggregate.ActiveCombinations==0);check(aggregate.Total==200000);
            aggregate.Reset();check(aggregate.Total==0);
            Console.WriteLine("PASS: Macemen aggregate 200000 observations,64 rows; elapsedMs="+
                ((Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency).ToString("F2"));
            return assertions;
        }
    }
}
''')
# Normalize only the explicitly edited text files, with ordinal byte verification.
files=['APIShared/src/Pathfinding/Assassin/AssassinRouteHandoff.cs','APIShared/tests/APISharedTests/AssassinRouteHandoffTests.cs','BugfixesAndQoL/src/UnitCommands/Movement/MovementPathPublication.cs',
'Testmods/EnemyGatePathfindingTest/src/MacemenCommandDiagnostics.cs','Testmods/EnemyGatePathfindingTest/src/MacemenDiagnosticAggregate.cs','Testmods/EnemyGatePathfindingTest/src/EnemyGatePathfindingRuntime.cs','Testmods/EnemyGatePathfindingTest/src/EnemyGatePathfindingTestPlugin.cs','Testmods/EnemyGatePathfindingTest/src/SamePclGateRouteRuntime.cs']
for f in files:write(Path(f),(root/f).read_text(encoding='utf-8-sig'))
print('Updated explicit project links/tests and verified CRLF for edited files.')