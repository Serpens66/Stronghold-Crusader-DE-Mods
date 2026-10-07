// TEMP_GATE_ROUTE_ACCEPTANCE: fixtures model memory/API contracts only; productive sources are compiled unmodified.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using APIShared;
using SHCDESE.API;
using SHCDESE.Interop;
using EnemyGatePathfindingTest;
namespace BepInEx.Logging { public class ManualLogSource { } }
namespace Shared { internal static class DebugLogHelper {
    internal static readonly List<string> Lines = new List<string>();
    internal static void LogInfo(BepInEx.Logging.ManualLogSource log, string text) => Lines.Add(text);
} }
namespace SHCDESE.Interop.Enums {
    public enum eChimps { CHIMP_TYPE_ARAB_ASSASIN = 0x49 }
    public enum AITribeStorageRole16 { HarassmentCombat0 = 180 }
}
namespace SHCDESE.Interop {
    public struct GameUnit { public uint r_GlobalId; public ushort r_TribeId; public byte r_ControllableForPlayerId, N00000569; public ushort r_UnitChimp; public int X,Y; }
    public struct GameTribe { public uint r_GlobalId; public ushort r_PlayerIdOwner; }
}
namespace SHCDESE.API {
    public unsafe class GameUnitManagerAPI {
        public static readonly GameUnitManagerAPI Instance = new GameUnitManagerAPI();
        public GameUnit* Unit; public int Reads;
        public bool IsValidId(int id) => id == 1;
        public bool TryGetUnitById(int id, out GameUnit* unit) { Reads++; unit = IsValidId(id) ? Unit : null; return unit != null; }
    }
    public unsafe class GameTribeManagerAPI {
        public static readonly GameTribeManagerAPI Instance = new GameTribeManagerAPI();
        public GameTribe* Tribe; public uint StorageGlobal = 9; public int Reads, InvalidReads;
        public bool IsValidId(int id) => id > 0 && id < 4500;
        public bool TryGetTribeById(int id, out GameTribe* tribe) {
            Reads++; if (!IsValidId(id)) { InvalidReads++; throw new Exception("Invalid SDK lookup"); }
            tribe = id == 37 ? Tribe : null; return tribe != null;
        }
        public bool TryGetAITribeStorageRole(int player, SHCDESE.Interop.Enums.AITribeStorageRole16 role, out ushort id, out uint global) {
            id=37; global=StorageGlobal; return player==5 && (int)role==183;
        }
        public bool TryResolveAITribeStorageRole(int player, SHCDESE.Interop.Enums.AITribeStorageRole16 role, out GameTribe* tribe) {
            tribe=Tribe; return player==5 && (int)role==183 && tribe != null;
        }
    }
    public class GameTileManagerAPI {
        public static readonly GameTileManagerAPI Instance = new GameTileManagerAPI();
        public int GetTileId(int x,int y) => y*800+x;
    }
}
namespace EnemyGatePathfindingTest { internal static class EnemyGatePathfindingNativeDefinition { internal const uint MaximumTileIdExclusive = 320800; } }
namespace BugfixesAndQoL {
    internal static class WeightedMoatRoutePlanner {
        internal const int MaximumRouteEdges=2000;
        internal static readonly int[] DirectionX={0,1,1,1,0,-1,-1,-1}, DirectionY={-1,-1,0,1,1,1,0,-1};
    }
    internal unsafe sealed partial class FriendlyMoatMovementRuntime {
        private const int NativeUnitPathBufferOffset=0xB4FE78, NativeUnitPathBufferStride=1000, MaximumUnitCount=10000, MapWidth=800;
        private const int PathManagerOutputBufferOffset=0x155F60, PathManagerOutputLengthOffset=0x155F68;
        private IntPtr nativePathManager; private byte* nativeUnitManager;
        private int mapEpoch=1; private object activeMoveCommand; private Attack activeAttackCommand;
        private UnitMoveFrame unitMoveFrame;
        private class Attack { internal int TribeId, Sequence, Command, TargetValue1, TargetValue2; }
        private class Args { internal int UnitId=1; internal bool SkipOriginalFunction; }
        private class UnitMoveFrame { internal Args Args=new Args(); internal int MapEpoch=1, Tick=3; internal object Command; }
        private int CaptureCurrentGameTick() => 3;
        private static void GetNativeMovementStart(GameUnit* unit,out int x,out int y) { x=unit->X; y=unit->Y; }
        private bool IsValidTileId(int tile) => tile>=0 && tile<320800;
        internal int Calls;
        internal static FriendlyMoatMovementRuntime Create() {
            var runtime = new FriendlyMoatMovementRuntime();
            runtime.nativePathManager=Marshal.AllocHGlobal(0x156000);
            runtime.nativeUnitManager=(byte*)Marshal.AllocHGlobal(NativeUnitPathBufferOffset+3000);
            runtime.Reset(); return runtime;
        }
        internal void Reset() {
            byte* context=(byte*)nativePathManager;
            *(byte**)(context+PathManagerOutputBufferOffset)=nativeUnitManager+NativeUnitPathBufferOffset+1000;
            *(int*)(context+8)=100; *(int*)(context+12)=100; *(int*)(context+16)=103; *(int*)(context+20)=100;
            unitMoveFrame=null;
            var u=GameUnitManagerAPI.Instance.Unit; *u=new GameUnit {r_GlobalId=12,r_UnitChimp=0x49,r_ControllableForPlayerId=5,X=100,Y=100};
        }
        internal int Invoke(int result=3, Action change=null) {
            object token=BeginTemporaryRouteReport(nativePathManager);
            Calls++;
            byte* context=(byte*)nativePathManager;
            byte* path=*(byte**)(context+PathManagerOutputBufferOffset);
            path[0]=0x22; path[1]=2; *(int*)(context+PathManagerOutputLengthOffset)=result;
            change?.Invoke(); EndTemporaryRouteReport(nativePathManager,token,true,result); return result;
        }
        internal void WithFrame(bool skip=false) { unitMoveFrame=new UnitMoveFrame(); unitMoveFrame.Args.SkipOriginalFunction=skip; }
        internal void UnknownBuffer() { *(byte**)((byte*)nativePathManager+PathManagerOutputBufferOffset)=nativeUnitManager+NativeUnitPathBufferOffset+999; }
        internal void PartialTarget() { *(int*)((byte*)nativePathManager+16)=104; }
        internal void DisposeFixture() { Marshal.FreeHGlobal(nativePathManager); Marshal.FreeHGlobal((IntPtr)nativeUnitManager); }
    }
    internal class WeightedProducer {
        private const int MapWidth=800;
        private int[] route;
        private static readonly int[] DirectionX=WeightedMoatRoutePlanner.DirectionX, DirectionY=WeightedMoatRoutePlanner.DirectionY;
        private readonly byte[] directionMasks={1,2,4,8,16,32,64,128}, occupancyLayer=new byte[320800];
        private AssassinObservation activeObservation;
        private class AssassinObservation { internal object Token, BridgeToken; internal Sink Observer, BridgeObserver; internal int RouteLength; internal string Error; }
        private class Sink {
            internal TemporaryGateRouteAcceptance Acceptance;
            internal void ObserveAssassinEdge(object token,int player,int from,int to,int direction,bool climb) => Acceptance.AssassinEdge(token,player,from,to,direction,climb);
        }
        private int GetTileId(int x,int y) => y*800+x;
        private bool IsNativeTile(int tile) => tile>=0 && tile<320800;
        private void LogWarning(string message) => throw new Exception(message);
        /* ACTUAL_WEIGHTED_PRODUCER */
        internal void Emit(TemporaryGateRouteAcceptance acceptance,int nodes,bool cache=false,bool climb=false,bool flood=false,int continuation=0) {
            route=new int[Math.Max(nodes,1)];
            for(int i=0;i<nodes;i++) route[i]=80100+nodes-1-i;
            Array.Fill(occupancyLayer,(byte)255); if(climb) occupancyLayer[80100]=0;
            int tx=flood?-1:100+Math.Max(nodes-1,0), ty=flood?-1:100;
            var token=acceptance.BeginAssassin(5,0,100,100,tx,ty);
            activeObservation=new AssassinObservation {Token=token,Observer=new Sink {Acceptance=acceptance}};
            if(!flood) ObservePreparedAssassinRoute(5,nodes);
            acceptance.EndAssassin(token,5,1,1,"weighted-published",cache,nodes,continuation);
        }
    }
}
public static unsafe class RuntimeAcceptanceTests {
    private static int assertions;
    private static void Check(bool value,string why) { assertions++; if(!value) throw new Exception(why); }
    private static bool Contains(string text) => Shared.DebugLogHelper.Lines.Exists(s=>s.Contains(text));
    public static void Run() {
        GameUnitManagerAPI.Instance.Unit=(GameUnit*)Marshal.AllocHGlobal(sizeof(GameUnit));
        GameTribeManagerAPI.Instance.Tribe=(GameTribe*)Marshal.AllocHGlobal(sizeof(GameTribe));
        *GameTribeManagerAPI.Instance.Tribe=new GameTribe {r_GlobalId=9,r_PlayerIdOwner=5};
        var runtime=BugfixesAndQoL.FriendlyMoatMovementRuntime.Create();
        try {
            Check(runtime.Invoke()==3 && runtime.Calls==1 && GameUnitManagerAPI.Instance.Reads==0,"no observer: unchanged result and no SDK reads");
            RouteTilePolicySnapshot snapshot=new RouteTilePolicySnapshot(new byte[9][],42);
            bool fail=false;
            var acceptance=new TemporaryGateRouteAcceptance(new BepInEx.Logging.ManualLogSource(),()=>fail?throw new Exception("fixture"):snapshot);
            TemporaryGateRouteAcceptanceBridge.Register(acceptance);
            var producer=new BugfixesAndQoL.WeightedProducer();
            foreach(int nodes in new[]{1,2,8}) { Shared.DebugLogHelper.Lines.Clear(); acceptance.Begin(); producer.Emit(acceptance,nodes); acceptance.End(); Check(Contains("checked=1,violated=0,unclear=0"),"actual weighted producer: nodes -> edges including stationary"); }
            Shared.DebugLogHelper.Lines.Clear(); acceptance.Begin(); producer.Emit(acceptance,3,true,true); acceptance.End(); Check(Contains("kind=assassin-weighted-cache,result=checked") && Contains("climbEdges=1"),"cache and climb retained");
            var masks=new byte[9][]; masks[5]=new byte[320800]; Array.Fill(masks[5],(byte)255); masks[5][80100]&=unchecked((byte)~4);
            var owners=new GateEdgeOwnership[9]; owners[5]=new GateEdgeOwnership(); owners[5].Record(80100,2,578);
            snapshot=new RouteTilePolicySnapshot(masks,43,edgeOwners:owners);
            Shared.DebugLogHelper.Lines.Clear(); acceptance.Begin(); producer.Emit(acceptance,3); acceptance.End(); Check(Contains("violated=1") && Contains("gate=578/attribution=exact"),"blocked passage exact gate");
            snapshot=new RouteTilePolicySnapshot(new byte[9][],44);
            Shared.DebugLogHelper.Lines.Clear(); acceptance.Begin(); producer.Emit(acceptance,0); producer.Emit(acceptance,0,flood:true,continuation:1); acceptance.End(); Check(Contains("unclear=1") && Contains("positive-without-materialized-route"),"positive without path unclear; flood not a route failure");
            acceptance.Begin(); var old=acceptance.BeginAssassin(5,0,100,100,101,100); acceptance.AssassinEdge(old,5,80100,80101,2,false); snapshot=new RouteTilePolicySnapshot(new byte[9][],45);
            acceptance.EndAssassin(old,5,1,1,"fixture",false,2,0); acceptance.End(); Check(Contains("unclear:snapshot-changed"),"snapshot change");
            Shared.DebugLogHelper.Lines.Clear(); acceptance.Begin(); old=acceptance.BeginAssassin(5,0,100,100,101,100); acceptance.End(); acceptance.Begin(); acceptance.AssassinEdge(old,5,80100,80101,2,false); acceptance.EndAssassin(old,5,1,1,"fixture",false,2,0); acceptance.End(); Check(Contains("unclear:map-epoch-changed"),"map epoch despite same snapshot reference");
            Shared.DebugLogHelper.Lines.Clear(); acceptance.Begin(); foreach(int tribe in new[]{0,-1,4500}) acceptance.RaidMove(5,tribe,true,100,100,1); acceptance.End(); Check(GameTribeManagerAPI.Instance.InvalidReads==0,"no invalid Tribe lookup");
            Shared.DebugLogHelper.Lines.Clear(); runtime.Reset(); acceptance.Begin(); runtime.WithFrame(); Check(runtime.Invoke()==3,"frame without moat plan"); acceptance.End(); Check(Contains("kind=assassin-published,result=checked"),"packed publication independent of moat plan");
            Shared.DebugLogHelper.Lines.Clear(); runtime.Reset(); acceptance.Begin(); runtime.Invoke(); acceptance.End(); Check(Contains("kind=assassin-published,result=checked"),"audited buffer identity without frame");
            Shared.DebugLogHelper.Lines.Clear(); runtime.Reset(); GameUnitManagerAPI.Instance.Unit->r_TribeId=37; GameUnitManagerAPI.Instance.Unit->r_UnitChimp=1; acceptance.Begin(); runtime.Invoke(); acceptance.End(); Check(Contains("role=3,kind=raid-published,result=checked"),"raid route classified by exact generation");
            Shared.DebugLogHelper.Lines.Clear(); runtime.Reset(); GameUnitManagerAPI.Instance.Unit->r_TribeId=37; acceptance.Begin(); runtime.Invoke(change:()=>GameTribeManagerAPI.Instance.Tribe->r_GlobalId=10); acceptance.End(); Check(Contains("tribe-identity-changed"),"reused Tribe identity"); GameTribeManagerAPI.Instance.Tribe->r_GlobalId=9;
            Shared.DebugLogHelper.Lines.Clear(); runtime.Reset(); acceptance.Begin(); runtime.Invoke(change:()=>GameUnitManagerAPI.Instance.Unit->r_GlobalId=13); acceptance.End(); Check(Contains("unit-or-call-identity-changed"),"reused unit identity");
            Shared.DebugLogHelper.Lines.Clear(); runtime.Reset(); runtime.PartialTarget(); acceptance.Begin(); runtime.Invoke(); acceptance.End(); Check(Contains("partial-endpoint"),"partial native path not passed");
            Shared.DebugLogHelper.Lines.Clear(); runtime.Reset(); runtime.UnknownBuffer(); acceptance.Begin(); runtime.Invoke(); acceptance.End(); Check(Contains("unit-buffer-mismatch"),"unknown buffer rejected before copying");
            Shared.DebugLogHelper.Lines.Clear(); runtime.Reset(); runtime.WithFrame(true); acceptance.Begin(); runtime.Invoke(); acceptance.End(); Check(Contains("stale-or-skipped-unit-frame"),"skipped frame not observed or mutated");
            Shared.DebugLogHelper.Lines.Clear(); runtime.Reset(); acceptance.Begin(); runtime.Invoke(result:0); acceptance.End(); Check(Contains("negativeSearches=1") && Contains("unclear=0"),"known negative separate from unknown route");
            int before=runtime.Calls; runtime.Reset(); acceptance.Begin(); fail=true; Check(runtime.Invoke()==3 && runtime.Calls==before+1,"diagnostic exception leaves native call exactly once"); fail=false; acceptance.End(); Check(TemporaryGateRouteAcceptanceBridge.Failures>0,"diagnostic exception counted");
            Check(GameTribeManagerAPI.Instance.InvalidReads==0,"all paths avoid invalid Tribe reads");
            Shared.DebugLogHelper.Lines.Clear(); runtime.Reset(); acceptance.Begin(); before=runtime.Calls; runtime.Invoke(change:()=>runtime.Invoke()); acceptance.End(); Check(runtime.Calls==before+2 && Contains("checked=2"),"nested publications exactly match native calls");
            Shared.DebugLogHelper.Lines.Clear(); runtime.Reset(); acceptance.Begin(); runtime.Invoke(change:()=>GameUnitManagerAPI.Instance.Unit->N00000569=1); acceptance.End(); Check(Contains("unit-or-call-identity-changed"),"full control WORD, not only low byte");
            Shared.DebugLogHelper.Lines.Clear(); acceptance.Begin(); old=acceptance.BeginAssassin(5,37,100,100,101,100); acceptance.AssassinEdge(old,5,80100,80101,2,false); GameTribeManagerAPI.Instance.Tribe->r_GlobalId=10; acceptance.EndAssassin(old,5,1,1,"fixture",false,2,0); acceptance.End(); Check(Contains("tribe-identity-changed") && Contains("checked=0"),"weighted Tribe generation change is not a pass"); GameTribeManagerAPI.Instance.Tribe->r_GlobalId=9;
            Console.WriteLine("PASS: "+assertions+" actual diagnostic path assertions; no moat-plan helper present in fixture.");
        } finally { runtime.DisposeFixture(); Marshal.FreeHGlobal((IntPtr)GameUnitManagerAPI.Instance.Unit); Marshal.FreeHGlobal((IntPtr)GameTribeManagerAPI.Instance.Tribe); }
    }
}
