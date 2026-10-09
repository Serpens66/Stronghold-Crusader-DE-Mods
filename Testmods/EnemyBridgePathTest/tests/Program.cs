using APIShared;
using System;
using System.IO;
using EnemyGatePathfindingTest;

namespace EnemyBridgePathTest
{
    internal static class Program
    {
        private static int count;
        private static void Check(bool value, string text) { count++; if (!value) throw new Exception(text); }
        private static int Invoke(int result)
        {
            object token = EnemyBridgeDiagnosticBridge.BeginSearch("test", 5);
            try { calls++; return EnemyBridgeDiagnosticBridge.NativeResult(result); }
            finally { EnemyBridgeDiagnosticBridge.EndSearch(token, true, result); }
        }
        private static int calls;
        private static int Main(string[] args)
        {
            try
            {
                if(args.Length==2&&args[0]=="--replay")return ShadowControlTests.Replay(args[1]);
                if(args.Length==2&&args[0]=="--replay-alternate-mode")return ShadowControlTests.Replay(args[1],true);
                if(args.Length==2&&args[0]=="--planning-replay")
                {
                    BridgePlanningImporter.Artifact artifact;string reason;
                    if(!BridgePlanningImporter.TryRead(args[1],out artifact,out reason)){Console.WriteLine(reason);return 2;}
                    bool matched=CopiedPlanningReplay.TryBaseline(artifact,out reason);Console.WriteLine("planningBaselineMatched="+matched+",reason="+reason+",artifactSHA256="+artifact.Hash+",behaviorFix=disabled");return matched?0:2;
                }
                if(args.Length==2&&args[0]=="--consumer-closure")
                {
                    BridgePlanningImporter.Artifact artifact;string reason;if(!BridgePlanningImporter.TryRead(args[1],out artifact,out reason)){Console.WriteLine(reason);return 2;}bool known=CopiedPlanningReplay.TryConsumerInputClosure(artifact,out reason);Console.WriteLine(reason);return known?0:2;
                }
                if(args.Length==2&&args[0]=="--consumer-replay")
                {
                    BridgePlanningImporter.Artifact artifact;string report;
                    if(!BridgePlanningImporter.TryRead(args[1],out artifact,out report)){Console.WriteLine(report);return 2;}
                    bool passed=CopiedPlanningReplay.TryConsumerStages(artifact,out report);Console.WriteLine(report);return passed?0:2;
                }
                if(args.Length==2&&args[0]=="--planning-variants")
                {
                    BridgePlanningImporter.Artifact artifact;string report;
                    if(!BridgePlanningImporter.TryRead(args[1],out artifact,out report)){Console.WriteLine(report);return 2;}
                    bool passed=CopiedPlanningReplay.TryVariants(artifact,out report);Console.WriteLine(report);return passed?0:2;
                }
                if(args.Length==3&&args[0]=="--planning-group-check")
                {
                    BridgePlanningImporter.Artifact planning,group;string reason;
                    if(!BridgePlanningImporter.TryRead(args[1],out planning,out reason)||!BridgePlanningImporter.TryRead(args[2],out group,out reason)){Console.WriteLine(reason);return 2;}
                    bool linked=BridgePlanningImporter.TryLinkGroup(planning,group,out reason);Console.WriteLine("planningGroupLinked="+linked+",reason="+reason+",behaviorFix=disabled");return linked?0:2;
                }
                count += ShadowControlTests.Run();
                count += VirtualBridgeTests.Run();
                count += SharedTests.CaptureRefreshTests.Run();
                count += RouteTraceTests.Run();
                count += DrawbridgeClosureTests.Run();
                count += NativeDecisionTests.Run();
                ConsumerOwnerTests();
                count += LeanTraceTests.Run();
                Check(EnemyGatePathPolicyBridge.Current == null, "separate provider starts empty");
                Check(Invoke(3) == 3 && calls == 1, "without observer native once");
                var observer = new Observer();
                Check(EnemyBridgeDiagnosticBridge.TryRegister(observer), "passive registration");
                Check(EnemyGatePathPolicyBridge.Current == null, "observer cannot claim gate policy");
                Check(BridgeNativeDefinition.OwnedCount==30&&Array.FindAll(BridgeNativeDefinition.Sites,BridgeNativeDefinition.OwnsEntry).Length==30,"exact thirty own detours");
                Check(!BridgeNativeDefinition.OwnsEntry(Array.Find(BridgeNativeDefinition.Sites,x=>x.Rva==0xE49D0))&&!BridgeNativeDefinition.OwnsEntry(Array.Find(BridgeNativeDefinition.Sites,x=>x.Rva==0x111C00)),"external owners excluded unconditionally");
                var owner=new BugfixesAndQoL.UnitCommands.UnitCommandPathRuntime();
                foreach(int value in new[]{0,1}) {owner.ReturnValue=value;Check(owner.Invoke(false)==value,"production topology owner preserves result");Check(observer.TopologyCalled&&observer.TopologyNative==value,"native topology result observed");}
                int ownerCalls=owner.Calls;Check(owner.Invoke(true)==0&&owner.Calls==ownerCalls&&!observer.TopologyCalled&&observer.TopologyNative==null,"suppressed production repair does not call original");
                observer.TopologyThrow=true;long topologyFailures=EnemyBridgeDiagnosticBridge.FailureCount;owner.ReturnValue=1;
                Check(owner.Invoke(false)==1&&owner.Calls==ownerCalls+1,"both observer exceptions cannot alter production original");
                Check(EnemyBridgeDiagnosticBridge.FailureCount==topologyFailures+2,"both topology observer errors counted");observer.TopologyThrow=false;
                owner.ThrowOriginal=true;bool originalFailed=false;try{owner.Invoke(false);}catch(InvalidOperationException){originalFailed=true;}
                Check(originalFailed&&!observer.TopologyCompleted&&observer.TopologyCalled&&observer.TopologyNative==null,"original exception preserved with incomplete observation");
                object outerTopology=EnemyBridgeDiagnosticBridge.BeginTopology(1,true,true),innerTopology=EnemyBridgeDiagnosticBridge.BeginTopology(0,true,true);
                EnemyBridgeDiagnosticBridge.EndTopology(innerTopology,true,true,0,0);EnemyBridgeDiagnosticBridge.EndTopology(outerTopology,true,true,1,1);
                Check(observer.TopologyNative==1,"nested topology tokens remain independent");
                var before=new BridgeDecisionTrace.TableImage {Rva=0x2EAAF90,Slot=8,Count=2,Rows=new[]{10,11,0,0,20,21,0,0}};
                var after=new BridgeDecisionTrace.TableImage {Rva=0x2EAAF90,Slot=8,Count=2,Rows=new[]{10,11,0,5,20,21,0,0}};
                var changes=BridgeDecisionTrace.LadderChanges(before,after);Check(changes.Count==1&&changes[0].Unit==5&&changes[0].Task==10&&changes[0].Unique,"effective assignment from consumer pre/post");
                after.Rows[7]=5;changes=BridgeDecisionTrace.LadderChanges(before,after);Check(changes.Count==2&&!changes[0].Unique&&!changes[1].Unique,"ambiguous reservations never claim unique choice");
                before.Rows[7]=5;changes=BridgeDecisionTrace.LadderChanges(before,after);Check(changes.Count==1&&!changes[0].Unique,"old competing reservation remains ambiguous");before.Rows[7]=0;
                Check(BridgeDecisionTrace.LadderChanges(before,before).Count==0,"no effective selection or native fallback without mutation");

                for (int i = 0; i < 80; i++) Check(Invoke(i) == i, "observations cannot alter returns");
                Check(calls == 81 && observer.Begins == 80 && observer.Ends == 80, "all calls exactly paired beyond 32");
                object parent = EnemyBridgeDiagnosticBridge.BeginSearch("parent", 5);
                EnemyBridgeDiagnosticBridge.NativeResult(12);
                Invoke(8);
                EnemyBridgeDiagnosticBridge.EndSearch(parent, true, 12);
                Check(observer.LastNative == 12, "nested call cannot replace parent native result");
                observer.Throw = true;
                long failures = EnemyBridgeDiagnosticBridge.FailureCount;
                Check(Invoke(9) == 9, "begin/end exceptions preserve native return");
                EnemyBridgeDiagnosticBridge.ObserveRegion(5, 1, 2, 0, 0, 1);
                Check(EnemyBridgeDiagnosticBridge.FailureCount == failures + 3, "each observer exception counted");
                observer.Throw = false;
                Check(Invoke(7) == 7 && observer.LastNative == 7, "exception restores nesting");
                var policy = new Policy();
                Check(EnemyGatePathPolicyBridge.TryRegister(policy), "independent gate provider co-loads");
                Check(Invoke(11) == 11 && ReferenceEquals(EnemyGatePathPolicyBridge.Current, policy), "observer preserves independent policy");
                var aggregate = new AiGateDecisionAggregate(); aggregate.Reset();
                for (int i=0; i<80; i++) for (int repeat=0; repeat<3; repeat++)
                    aggregate.RecordGateState(5,i,"state","owner=1,captured="+i,7,i,i,i,"bridge="+i);
                var rows = aggregate.Drain(out var definitions);
                long sum=0; foreach (var row in rows) sum += row.Count;
                Check(sum == 240 && definitions.Length == 80 && rows.Length == 80, "lossless compact states");
                aggregate.Reset(); aggregate.RecordGateState(5,1,"state","new-map",7,0,0,0,"new");
                rows = aggregate.Drain(out definitions);
                Check(definitions.Length == 1 && definitions[0].Epoch == 2 && rows[0].Count == 1, "map state reset");
                string runtime = File.ReadAllText("src/BridgeDiagnostics.cs");
                string snapshot = File.ReadAllText("src/BridgeSnapshot.cs");
                Check(runtime.Contains("same-pcl-with-no-region-call") && runtime.Contains("region-query-executed"), "same PCL bypass is explicit");
                Check(runtime.Contains("WorkBefore") && runtime.Contains("command-or-context-changed") && runtime.Contains("execution=not-proven"), "work call lifetime evidence");
                Check(snapshot.Contains("moat[tile] == 0") && snapshot.Contains("mask=hypothetical-only"), "record prerequisite and passive mask");
                Check(snapshot.Contains("grid > 6") && !snapshot.Contains("grid > 10"), "native occupied array bound is 36 cells");
                string plugin = File.ReadAllText("src/EnemyBridgePathTestPlugin.cs");
                Check(!plugin.Contains("EnemyGatePathfindingTest_Serp") && plugin.Contains("BugfixesAndQoL_Serp"), "standalone mainmod dependency");
                Console.WriteLine("PASS: " + count + " bridge geometry/observer/aggregate assertions");
                return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        private static unsafe void ConsumerOwnerTests()
        {
            var before=new BridgeDecisionTrace.TableImage {Rva=0x2EAAF90,Slot=8,Count=1,Rows=new[]{10,11,0,0}};
            int table=0x2EAAF90+8*0x177bc;
            var unit=default(SHCDESE.Interop.GameUnit);unit.r_GlobalId=77;unit.r_ControllableForPlayerId=8;unit.r_AIState=0x65;unit.r_AI_ContextTargetBuildingTileId=10;
            IntPtr storage=System.Runtime.InteropServices.Marshal.AllocHGlobal(0x490);
            try
            {
                var trace=new BridgeDecisionTrace(null,_=>default,()=>1,r=>r==table?1:r==table+8?10:r==table+12?11:r==table+20?5:0,id=>storage);trace.StartSession(77);trace.SetNative(new IntPtr(1),0x9000000);
                var scope=new BridgeDecisionTrace.Scope {Id=700,LadderBefore=before};
                var complete=typeof(BridgeDecisionTrace).GetMethod("CompleteLadderAssignments",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                Action apply=()=>{scope.Selections.Clear();System.Runtime.InteropServices.Marshal.StructureToPtr(unit,storage,false);complete.Invoke(trace,new object[]{scope});};
                scope.LadderCommands.Add(new BridgeDecisionTrace.SelectionEvidence {Unit=5,Global=77,Player=8,Movement=701});apply();
                Check(scope.Selections.Count==1&&scope.Selections[0].Movement==701,"actual consumer completes same-identity effective assignment");
                unit.r_GlobalId=78;apply();Check(scope.Selections.Count==0,"reused unit cannot inherit command identity");
                unit.r_GlobalId=77;unit.r_ControllableForPlayerId=5;apply();Check(scope.Selections.Count==0,"role change cannot inherit prior player binding");unit.r_ControllableForPlayerId=8;
                unit.r_GlobalId=77;unit.r_AI_ContextTargetBuildingTileId=20;apply();Check(scope.Selections.Count==0,"replaced task cannot inherit reservation link");
                unit.r_AI_ContextTargetBuildingTileId=10;scope.LadderCommands.Clear();apply();Check(scope.Selections.Count==0,"missing movement is explicit without guessed linkage");
                scope.LadderCommands.Add(new BridgeDecisionTrace.SelectionEvidence {Unit=5,Global=77,Player=8,Movement=701});scope.LadderCommands.Add(new BridgeDecisionTrace.SelectionEvidence {Unit=5,Global=77,Player=8,Movement=702});apply();Check(scope.Selections.Count==0,"multiple commands cannot claim unique consumer binding");
                var topo=new BridgeDecisionTrace(null,_=>default,()=>1,_=>0);topo.StartSession(1);topo.SetNative(new IntPtr(1),100);
                Check(topo.BeginTopology(IntPtr.Zero,0,false)==null&&topo.Entered==0,"suppressed repair is not a native call");
                var frame=topo.BeginTopology(IntPtr.Zero,1,true);topo.EndTopology(frame,true,true,1,1);
                Check(topo.Entered==1&&topo.Exited==1&&topo.Summary().Contains("topologyGeneration=1"),"shared original return1 triggers exactly one observed rebuild");
                frame=topo.BeginTopology(IntPtr.Zero,1,true);topo.StartSession(2);topo.EndTopology(frame,true,true,1,1);
                Check(topo.Entered==2&&topo.Exited==2,"map switch inside owner balances original frames");
            }
            finally{System.Runtime.InteropServices.Marshal.FreeHGlobal(storage);}
        }
        private sealed class Observer : IEnemyBridgePathObserver,IEnemyBridgeTopologyObserver
        {
            internal bool TopologyThrow,TopologyCalled,TopologyCompleted;
            internal int? TopologyNative;
            public object BeginTopology(int force,bool run,bool knownPathingContext){if(TopologyThrow)throw new Exception("topology-pre");return force;}
            public void EndTopology(object token,bool completed,bool called,int? native,int effective){TopologyCalled=called;TopologyCompleted=completed;TopologyNative=native;if(TopologyThrow)throw new Exception("topology-post");}
            internal long Begins, Ends;
            internal int? LastNative;
            internal bool Throw;
            public object BeginSearch(string source, int player) { Begins++; if (Throw) throw new Exception("begin"); return source; }
            public void EndSearch(object token, bool completed, int? native, int effective, long nativeCalls) { Ends++; LastNative=native; Check(nativeCalls == 1, "native call counter exact"); if (Throw) throw new Exception("end"); }
            public void ObserveRegion(int player, int source, int target, int mode, int native, int effective) { if (Throw) throw new Exception("region"); }
            public object BeginAssassinSearch(int a,int b,int c,int d,int e,int f,string state) => null;
            public void ObserveAssassinEdge(object token,int p,int a,int b,int d,bool c) { }
            public void ObserveAssassinPolicyFiltering(object token,int p,long g,long c) { }
            public void ObserveAssassinBuildingSearch(int t,int b,int s,int p) { }
            public void EndAssassinSearch(object token,int p,int n,int e,string o,bool c,int l) { }
        }
        private sealed class Policy : IEnemyGatePathPolicy
        {
            public bool HasPublishedMask => true;
            public bool IsDirectionAllowed(int p,int t,int d) => false;
            public int ResolveTribePlayer(int t) => 5;
            public int ResolveBuildingPlayer(int p,int t) => 5;
            public int ResolveCursorPlayer(int t) => 5;
            public object EnterNativeSearch(int p,EnemyGateSearchKind kind) => null;
            public void ExitNativeSearch(object scope,EnemyGateSearchKind kind,bool completed,bool success) { }
        }
    }
}
