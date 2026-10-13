using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Collections.Generic;
using EnemyGatePathfindingTest;
using SHCDESE.API;
using SHCDESE.Interop;
using APIShared;
namespace EnemyGatePathfindingTest
{
    internal unsafe struct X64SmartCPUContext { internal ulong RAX,RBX,RCX,RDX,RSI,RDI,RBP,RSP,R8,R9,R10,R11,R12,R13,R14,R15,Rflags; }
    internal class NativeAssassinMovementDiagnostics { internal struct Sample { internal long Epoch,Generation; internal object Snapshot; } internal string[] Drain()=>new string[0]; }
    internal unsafe class NativeAssassinDiagnosticHooks
    {
        internal bool Installed => true;
        internal long Failures, UnscopedReconstructions, AbandonedBuilders;
        internal long CallbackCount, CallbackTicks, MaximumCallbackTicks;
        internal long BuilderEntries, BuilderExits;
        internal readonly NativeAssassinMovementDiagnostics Execution = new NativeAssassinMovementDiagnostics();
        private Action<int,IntPtr,bool> executionCallback; private Action executionBuilder;
        [ThreadStatic] private static NativeAssassinTrace current;
        private readonly ulong module;
        private readonly TemporaryGateRouteAcceptance observer;
        private long sequence;
        internal void SetActive(bool enabled) { }
        internal NativeAssassinDiagnosticHooks(ulong module,TemporaryGateRouteAcceptance observer) { this.module=module;this.observer=observer;current=null; }
        internal void Feed(int site,ref X64SmartCPUContext context) { fixed(X64SmartCPUContext* p=&context) Observe(site,p); }
        /* ACTUAL_NATIVE_OBSERVER */
    }
}
public static unsafe class NativeTracePublicationTests
{
    private static void Check(bool ok,string text) { if(!ok) throw new Exception("Native publication fixture: "+text); }
    public static void Run()
    {
        byte* module=(byte*)Marshal.AllocHGlobal(0x7400000),stack=(byte*)Marshal.AllocHGlobal(512);
        var buildings=GameBuildingManagerAPI.Instance; var tribes=GameTribeManagerAPI.Instance;
        buildings.Building=(GameBuilding*)Marshal.AllocHGlobal(sizeof(GameBuilding));
        buildings.Additional=null;buildings.BuildingId=578;
        *buildings.Building=new GameBuilding{r_GlobalId=99,r_BuildingType=45,r_PlayerIdOwner=2,r_AliveState=2};
        tribes.Tribe=(GameTribe*)Marshal.AllocHGlobal(sizeof(GameTribe));*tribes.Tribe=new GameTribe{r_GlobalId=9,r_PlayerIdOwner=5};
        try {
            var masks=new byte[9][];masks[5]=new byte[320800];Array.Fill(masks[5],(byte)255);masks[5][80100]&=unchecked((byte)~4);
            var owners=new GateEdgeOwnership[9];owners[5]=new GateEdgeOwnership();owners[5].Record(80100,2,578);
            var geometry=new GateEdgeOwnership();geometry.Record(80100,2,578);
            RouteTilePolicySnapshot Make()=>new RouteTilePolicySnapshot(masks,43,edgeOwners:owners,
                gateIdentities:new Dictionary<int,RouteTilePolicySnapshot.GateIdentity>{{578,new RouteTilePolicySnapshot.GateIdentity(99,2,0)}},diagnosticGateEdges:geometry);
            RouteTilePolicySnapshot snapshot=Make();
            GameTileManagerAPI.Instance.Buildings[80101]=578;
            var acceptance=new TemporaryGateRouteAcceptance(new BepInEx.Logging.ManualLogSource(),()=>snapshot);
            var hooks=new NativeAssassinDiagnosticHooks((ulong)module,acceptance);acceptance.NativeDiagnostics=hooks;
            byte* manager=module+0x60AD660;
            *(int*)(manager+0x88)=1;
            *(ulong*)(manager+0x155F60)=(ulong)(module+0x67E8400+0xB4FE78+1000);
            *(int*)(manager+8)=100;*(int*)(manager+12)=100;*(int*)(manager+16)=101;*(int*)(manager+20)=100;
            foreach(string scenario in new[]{"ground","climb","secondary","fallback","snapshot","different-output","nested","failed"}) {
                Shared.DebugLogHelper.Lines.Clear();acceptance.Begin();snapshot=Make();
                var token=acceptance.BeginRoute(5,37,9,1,12,0x49,100,100,101,100,"fixture");
                var c=new X64SmartCPUContext{RCX=(ulong)manager,RSP=(ulong)(stack+256)};hooks.Feed(0,ref c);
                void Reconstruct(bool climb) {
                    c.RCX=(ulong)manager;c.RSP=(ulong)(stack+0x88);*(int*)(stack+0xb8)=climb?3:1;hooks.Feed(4,ref c);c.RSP=(ulong)stack;
                    c.R8=80101;c.RDX=80100;c.R12=6;*(byte*)(stack+0x98)=climb?(byte)0:(byte)64;
                    *(uint*)(module+0x48F71B0+80100*4)=0;*(uint*)(module+0x48F71B0+80101*4)=0x100;
                    hooks.Feed(5,ref c);hooks.Feed(6,ref c);c.R10=80100;c.R13=6;hooks.Feed(7,ref c);hooks.Feed(8,ref c);
                }
                Reconstruct(scenario!="ground");
                if(scenario=="secondary"||scenario=="fallback") { c.RSP=(ulong)(stack+256);c.RBX=(ulong)manager;hooks.Feed(2,ref c);if(scenario=="fallback")hooks.Feed(3,ref c);Reconstruct(false); }
                if(scenario=="nested") {
                    c.RCX=(ulong)manager;c.RSP=(ulong)(stack+128);hooks.Feed(0,ref c);Reconstruct(false);
                    *(int*)(manager+0x155F68)=1;c.RAX=1;c.RBX=(ulong)manager;c.RSP=(ulong)(stack+128);hooks.Feed(1,ref c);
                }
                if(scenario=="failed") { c.RSP=(ulong)stack;hooks.Feed(9,ref c);hooks.Feed(8,ref c); }
                *(int*)(manager+0x155F68)=1;c.RAX=1;c.RBX=(ulong)manager;c.RSP=(ulong)(stack+256);hooks.Feed(1,ref c);
                acceptance.RouteEdge(token,80100,scenario=="different-output"?80102:80101,2);
                if(scenario=="snapshot")snapshot=Make();
                acceptance.EndRoute(token,"decoded|diag=exactWeightedPublished=False,secondaryGroundPassExecution=not-observed",1);acceptance.End();
                string log=string.Join("\n",Shared.DebugLogHelper.Lines);
                if(scenario=="ground"||scenario=="secondary"||scenario=="fallback")Check(log.Contains("confirmedGroundViolations=1"),scenario+" proven ground");
                else if(scenario=="climb")Check(log.Contains("permittedClimbRoutes=1"),scenario+" selected climb");
                else Check(log.Contains("confirmedGroundViolations=0")&&log.Contains("permittedClimbRoutes=0"),scenario+" cannot prove a route");
                if(scenario=="secondary")Check(log.Contains("secondaryGroundPassExecution=executed")&&log.Contains("supersededReconstructions=1"),"actual secondary observation");
                Check(hooks.Failures==0,"actual callback valid context");
            }
            Console.WriteLine("PASS: actual native Observe callback + actual publication observer; ground/climb, secondary overwrite/fallback, nesting, snapshot/failure and different output.");
        } finally { Marshal.FreeHGlobal((IntPtr)module);Marshal.FreeHGlobal((IntPtr)stack);Marshal.FreeHGlobal((IntPtr)buildings.Building);Marshal.FreeHGlobal((IntPtr)tribes.Tribe); }
    }
}
