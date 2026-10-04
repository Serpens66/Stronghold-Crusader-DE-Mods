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
        private static int Main()
        {
            try
            {
                count += DrawbridgeClosureTests.Run();
                count += NativeDecisionTests.Run();
                count += LeanTraceTests.Run();
                Check(EnemyGatePathPolicyBridge.Current == null, "separate provider starts empty");
                Check(Invoke(3) == 3 && calls == 1, "without observer native once");
                var observer = new Observer();
                Check(EnemyBridgeDiagnosticBridge.TryRegister(observer), "passive registration");
                Check(EnemyGatePathPolicyBridge.Current == null, "observer cannot claim gate policy");
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
        private sealed class Observer : IEnemyBridgePathObserver
        {
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
