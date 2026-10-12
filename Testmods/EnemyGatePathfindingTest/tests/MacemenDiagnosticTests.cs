// TEMP_MACEMEN_GATE_DIAGNOSTICS: aggregate behavior, not a native simulation.
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
            check(MacemenDiagnosticAggregate.Classify(38,true,true)==MacemenTarget.GateApproach);
            check(MacemenDiagnosticAggregate.Classify(38,false,true)==MacemenTarget.OtherBuildingApproach);
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
