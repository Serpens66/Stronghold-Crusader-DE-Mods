using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

// External managers/publishers only. All decisions, cache keys, footprint ordering,
// exterior tracing and the diagnostic collector are the actual production sources.
namespace BepInEx.Logging { public class ManualLogSource { } }
namespace HarmonyLib
{
    public sealed class Harmony { public Harmony(string id) { } public void Patch(MethodBase method, HarmonyMethod prefix=null, HarmonyMethod postfix=null, HarmonyMethod finalizer=null, object ilmanipulator=null) { } }
    public sealed class HarmonyMethod { public HarmonyMethod(Type type,string name) { } }
    public static class AccessTools { public static MethodInfo Method(Type type,string name,Type[] parameters)=>type.GetMethod(name,parameters); }
}
public static class EngineInterface { public static int run(bool skip)=>0; }
namespace R3 { public static class Extensions { public static IDisposable Subscribe<T>(this Publisher<T> publisher,Action<T> action)=>null; } public sealed class Publisher<T> { } }
namespace SHCDESE.EventAPI { public static class BuildingR3EventHooks { public static readonly Hook OnGatehouseQuery=new Hook(); public sealed class Hook { public readonly R3.Publisher<Buildings.GatehouseQueryEventArgs> Observable=new R3.Publisher<Buildings.GatehouseQueryEventArgs>(); } } }
namespace SHCDESE.EventAPI.Buildings { public sealed class GatehouseQueryEventArgs { public int BuildingId,UnitId; public bool? ShouldClose; } }
namespace SHCDESE.Interop.Enums { public enum AliveState { IsAlive, Dead } public enum PathConnectionQueryMode { Fixture } }
namespace SHCDESE.Interop
{
    public enum eChimps { CHIMP_TYPE_LION, FixtureSoldier }
    public enum eStructs { STRUCT_DRAWBRIDGE, FixtureGate }
    public struct GameUnit { public bool Alive; public eChimps r_UnitChimp; public int r_ControllableForPlayerId,r_CurrentHealth,r_PathConnectionMode; public uint r_CurrentPositionTileId; }
    public struct GameBuilding { public Enums.AliveState r_AliveState; public eStructs r_BuildingType; public uint r_GlobalId; public int r_OccupyTileGridSize,r_TilePositionXBegin,r_TilePositionYBegin; }
    public struct PathConnectionRecord { public uint r_EntryTileId,r_ExitTileId,r_SubjectGlobalId; public int r_EntryTilePositionX,r_EntryTilePositionY,r_ExitTilePositionX,r_ExitTilePositionY,r_BuildingId; }
}
namespace APIShared
{
    public static unsafe class UnitAccess
    {
        public static SHCDESE.Interop.GameUnit* Units;
        public static bool TryGetById(int id,out SHCDESE.Interop.GameUnit* value,out int index) { index=id-1;value=id>0&&id<=4?Units+index:null;return value!=null; }
        public static bool IsReallyAlive(SHCDESE.Interop.GameUnit* value)=>value!=null&&value->Alive;
    }
}
namespace SHCDESE.API
{
    public sealed unsafe class GameUnitManagerAPI { public static readonly GameUnitManagerAPI Instance=new GameUnitManagerAPI(); public Span<SHCDESE.Interop.GameUnit> GetUnitsAsSpan()=>new Span<SHCDESE.Interop.GameUnit>(APIShared.UnitAccess.Units,4); }
    public sealed unsafe class GameBuildingManagerAPI
    {
        public static readonly GameBuildingManagerAPI Instance=new GameBuildingManagerAPI();
        public SHCDESE.Interop.GameBuilding* Buildings;
        public bool TryGetBuildingById(int id,out SHCDESE.Interop.GameBuilding* value) { value=id>0&&id<=3?Buildings+id-1:null;return value!=null; }
    }
    public sealed unsafe class GamePathingManagerAPI
    {
        public static readonly GamePathingManagerAPI Instance=new GamePathingManagerAPI();
        public SHCDESE.Interop.PathConnectionRecord* Record;
        public ushort[] Grid=new ushort[64];public int Calls,Target;public bool Throw;
        public Span<ushort> GetPathComponentGrid()=>Grid;
        public bool TryGetPathConnectionRecordByBuildingId(int id,out SHCDESE.Interop.PathConnectionRecord* value) { value=Record;return id==1; }
        public int FindNextComponentTowardDestination(int p,int from,int to,SHCDESE.Interop.Enums.PathConnectionQueryMode mode) { Calls++;if(Throw)throw new InvalidOperationException("external route failure");return to==Target?to:0; }
    }
    public sealed class GameTileManagerAPI
    {
        public static readonly GameTileManagerAPI Instance=new GameTileManagerAPI();public bool Bridge;public int Reads;
        public readonly Dictionary<int,int> Occupancy=new Dictionary<int,int>(); public Action OnRead;
        public bool IsValidTileId(int id)=>id>=0&&id<64;public bool IsTileInsideMapBounds(int x,int y)=>x>=0&&x<8&&y>=0&&y<8;
        public int GetTileId(int x,int y)=>x+y*8;
        public int GetTileBuildingId(int id) { Reads++;var callback=OnRead;OnRead=null;callback?.Invoke();return Occupancy.TryGetValue(id,out int building)?building:Bridge&&id==28?2:0; }
    }
    public sealed class GameTimeManagerAPI { public static readonly GameTimeManagerAPI Instance=new GameTimeManagerAPI(); public int Tick; public Stamp CaptureTimeStamp()=>new Stamp { CapturedGameTick=Tick };public struct Stamp { public int CapturedGameTick; } }
}
namespace Shared
{
    public static class DebugLogHelper { public static void LogWarningLimited(BepInEx.Logging.ManualLogSource log,string key,string text) { } }
    public static class DevDiag { public static string LastLog; public static void Log(Func<string> text) { LastLog=text(); } }
    public static class DevDiagRuntime { public static string LastSpike;public static void Spike(string name,long ticks,string detail){LastSpike=detail;} }
}
namespace BugfixesAndQoL
{
    public sealed class BugfixesAndQoLViewModel { public bool EnableMod=true,EnableClientFeatures=true,RequireReachableEnemyForAutomaticGateClosing=true,EnableVanillaPerformanceOptimizations=true; }
    internal sealed unsafe partial class ReachableEnemyGatehouseRuntime
    {
        internal void Call(SHCDESE.EventAPI.Buildings.GatehouseQueryEventArgs args)=>OnGatehouseQuery(args);
        internal static IReadOnlyList<SynchronizedDrawbridgeSnapshot> Collect(SHCDESE.Interop.GameBuilding* building,out int signature)
        { return TryCollectSynchronizedDrawbridges(building,SHCDESE.API.GameTileManagerAPI.Instance,out var result,out signature)?result:null; }
    }
    public static unsafe class GatehouseFixtureRunner
    {
        public static string AllocationReport;
        private static void Check(bool value,string message) { if(!value)throw new Exception(message); }
        public static string Run()
        {
            var units=stackalloc SHCDESE.Interop.GameUnit[4];APIShared.UnitAccess.Units=units;
            var buildings=stackalloc SHCDESE.Interop.GameBuilding[3];SHCDESE.API.GameBuildingManagerAPI.Instance.Buildings=buildings;
            var gate=stackalloc SHCDESE.Interop.PathConnectionRecord[1];SHCDESE.API.GamePathingManagerAPI.Instance.Record=gate;
            units[0]=new SHCDESE.Interop.GameUnit { Alive=true,r_UnitChimp=SHCDESE.Interop.eChimps.FixtureSoldier,r_ControllableForPlayerId=1,r_CurrentHealth=10,r_CurrentPositionTileId=1 };
            buildings[0]=new SHCDESE.Interop.GameBuilding { r_GlobalId=10,r_OccupyTileGridSize=1,r_TilePositionXBegin=3,r_TilePositionYBegin=3,r_BuildingType=SHCDESE.Interop.eStructs.FixtureGate };
            buildings[1]=new SHCDESE.Interop.GameBuilding { r_GlobalId=20,r_BuildingType=SHCDESE.Interop.eStructs.STRUCT_DRAWBRIDGE };
            gate[0]=new SHCDESE.Interop.PathConnectionRecord { r_BuildingId=1,r_SubjectGlobalId=10,r_EntryTileId=27,r_ExitTileId=35,r_EntryTilePositionX=3,r_EntryTilePositionY=3,r_ExitTilePositionX=3,r_ExitTilePositionY=4 };
            var paths=SHCDESE.API.GamePathingManagerAPI.Instance;var tiles=SHCDESE.API.GameTileManagerAPI.Instance;
            Array.Fill(paths.Grid,(ushort)3);paths.Grid[1]=1;paths.Grid[27]=paths.Grid[35]=2;
            var digest=new List<string>();
            for(int scenario=0;scenario<4;scenario++)
            {
                paths.Calls=0;paths.Target=scenario==0?2:scenario==1?3:0;paths.Throw=scenario==3;
                tiles.Bridge=scenario==1;tiles.Reads=0;SHCDESE.API.GameTimeManagerAPI.Instance.Tick++;
                var settings=new BugfixesAndQoLViewModel();var runtime=new ReachableEnemyGatehouseRuntime(new BepInEx.Logging.ManualLogSource(),settings);
                runtime.SetNativeCompatibility(true);
#if SERP_DEVDIAG
                GatehouseDiagnosticRuntime.Current.Reset();
#endif
                for(int repeat=0;repeat<2;repeat++)
                {
                    var args=new SHCDESE.EventAPI.Buildings.GatehouseQueryEventArgs { BuildingId=1,UnitId=1 };
                    runtime.Call(args);Check(args.ShouldClose==(scenario!=2),"Production close decision changed: "+scenario);
                }
                int expected=scenario==0?1:scenario==1?3:2;
                Check(paths.Calls==expected,"Original route count changed: "+scenario+" got="+paths.Calls);
#if SERP_DEVDIAG
                var totals=GatehouseDiagnosticRuntime.Current;
                Check(totals.Queries==2&&totals.RouteCalls==expected,"Diagnostic route/callback counters differ");
                if(scenario!=3)Check(totals.DirectMisses==1&&totals.DirectHits==1,"Direct cache decisions differ");
                if(scenario==1)Check(totals.FullMisses==1&&totals.FullHits==1&&totals.Bridges==2&&totals.Approaches==2&&totals.TraceCalls==2,"Bridge/cache stages differ");
#if GEOMETRY_OPTIMIZED
                if(scenario==1)Check(totals.FootprintScans==2&&totals.FootprintCalls+totals.FootprintCacheHits==2&&totals.CollectionTileProbes==tiles.Reads,"Geometry creation/reuse/live probes were conflated");
#else
                if(scenario==1)Check(totals.FootprintCalls==2,"Baseline footprint instrumentation changed");
#endif
                Check(totals.Detail().Contains("lastUnit=1"),"Primitive identity missing");
#endif
                digest.Add(scenario+":"+paths.Calls+":"+tiles.Reads);
                settings.EnableMod=false;runtime.Call(new SHCDESE.EventAPI.Buildings.GatehouseQueryEventArgs { BuildingId=1,UnitId=1 });
                Check(paths.Calls==expected,"Disabled diagnostic path changed gameplay");
            }
            TestLiveChanges(units,buildings,gate,digest);
#if GEOMETRY_OPTIMIZED
            GeometryCacheTests.Run();
#endif
#if SERP_DEVDIAG
            TestCollector();
#else
            Check(typeof(ReachableEnemyGatehouseRuntime).GetMethod("DiagInitialize",BindingFlags.NonPublic|BindingFlags.Instance)==null,
                "Normal compilation retained diagnostic partial method");
            Check(typeof(ReachableEnemyGatehouseRuntime).Assembly.GetType("BugfixesAndQoL.GatehouseDiagnosticRuntime")==null,
                "Normal compilation retained diagnostic types");
#endif
            MeasureAllocations(units,buildings);
            return "PASS: production gatehouse decisions/cache/exception paths; native calls and tile reads="+string.Join(",",digest);
        }
        private static void MeasureAllocations(SHCDESE.Interop.GameUnit* units,SHCDESE.Interop.GameBuilding* buildings)
        {
            const int count=20000;
            var tiles=SHCDESE.API.GameTileManagerAPI.Instance;var paths=SHCDESE.API.GamePathingManagerAPI.Instance;
            tiles.Occupancy.Clear();tiles.Bridge=false;tiles.OnRead=null;paths.Throw=false;paths.Target=0;
            buildings[0].r_TilePositionXBegin=buildings[0].r_TilePositionYBegin=1;buildings[0].r_OccupyTileGridSize=5;
            var runtime=new ReachableEnemyGatehouseRuntime(new BepInEx.Logging.ManualLogSource(),new BugfixesAndQoLViewModel());
            runtime.SetNativeCompatibility(true);
            var args=new SHCDESE.EventAPI.Buildings.GatehouseQueryEventArgs {BuildingId=1,UnitId=1};
            for(int i=0;i<20;i++){args.ShouldClose=null;runtime.Call(args);}
            long before=GC.GetAllocatedBytesForCurrentThread();paths.Calls=tiles.Reads=0;
            for(int i=0;i<count;i++){args.ShouldClose=null;runtime.Call(args);}
            long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(args.ShouldClose==false&&paths.Calls==0&&tiles.Reads==count*20,"Allocation measurement changed decisions or live reads");
            AllocationReport="ALLOCATED_BYTES_PER_QUERY="+(bytes/(double)count).ToString("F2",System.Globalization.CultureInfo.InvariantCulture);
        }
        private static void TestLiveChanges(SHCDESE.Interop.GameUnit* units,SHCDESE.Interop.GameBuilding* buildings,
            SHCDESE.Interop.PathConnectionRecord* gate,List<string> digest)
        {
            var tiles=SHCDESE.API.GameTileManagerAPI.Instance;var paths=SHCDESE.API.GamePathingManagerAPI.Instance;
            tiles.Bridge=false;paths.Throw=false;paths.Target=3;
            buildings[2]=buildings[1];buildings[2].r_GlobalId=30;
            var runtime=new ReachableEnemyGatehouseRuntime(new BepInEx.Logging.ManualLogSource(),new BugfixesAndQoLViewModel());
            runtime.SetNativeCompatibility(true);SHCDESE.API.GameTimeManagerAPI.Instance.Tick++;
            for(int stage=0;stage<=8;stage++)
            {
                switch(stage){
                    case 1:tiles.Occupancy[19]=2;tiles.Occupancy[28]=3;break;
                    case 2:tiles.Occupancy.Remove(19);break;
                    case 3:buildings[2].r_GlobalId++;break;
                    case 4:paths.Grid[29]=9;break;
                    case 5:buildings[2].r_AliveState=SHCDESE.Interop.Enums.AliveState.Dead;break;
                    case 6:buildings[2].r_AliveState=SHCDESE.Interop.Enums.AliveState.IsAlive;buildings[0].r_TilePositionXBegin=4;break;
                    case 7:buildings[0].r_OccupyTileGridSize=2;break;
                    case 8:tiles.Occupancy.Clear();buildings[0].r_TilePositionXBegin=3;buildings[0].r_OccupyTileGridSize=1;
                        buildings[1].r_GlobalId=40;tiles.Occupancy[19]=2;runtime.SetNativeCompatibility(true);break;
                }
                paths.Calls=tiles.Reads=0;
                var result=ReachableEnemyGatehouseRuntime.Collect(buildings,out int signature);
                Check(result!=null,"Live collection became unavailable");
                if(stage==1)Check(result.Count==2&&result[0].BuildingId==2&&result[1].BuildingId==3,"Native first-two order changed");
                var rows=new List<string>();
                foreach(var bridge in result){rows.Add(bridge.BuildingId+"/"+bridge.GlobalId);
                    foreach(var approach in bridge.Approaches)rows.Add(approach.ExteriorTileId+"/"+approach.ExteriorPcl);}
                var args=new SHCDESE.EventAPI.Buildings.GatehouseQueryEventArgs {BuildingId=1,UnitId=1};runtime.Call(args);
                digest.Add("live"+stage+":"+signature+":"+string.Join("/",rows)+":"+args.ShouldClose+":"+paths.Calls+":"+tiles.Reads);
            }
            // A nested collection during a live tile read must leave the outer coordinate sequence intact.
            tiles.Occupancy.Clear();var nested=buildings[0];nested.r_TilePositionXBegin=4;
            IntPtr nestedAddress=(IntPtr)(&nested);int nestedSignature=0;
            tiles.OnRead=()=>ReachableEnemyGatehouseRuntime.Collect((SHCDESE.Interop.GameBuilding*)nestedAddress,out nestedSignature);
            tiles.Reads=0;var outer=ReachableEnemyGatehouseRuntime.Collect(buildings,out int outerSignature);
            Check(outer.Count==0&&outerSignature==17&&nestedSignature==17,"Nested collection changed outer geometry");
            digest.Add("nested:"+outerSignature+":"+nestedSignature+":"+tiles.Reads);
#if GEOMETRY_OPTIMIZED
            var empty=ReachableEnemyGatehouseRuntime.Collect(buildings,out _);
            Check(ReferenceEquals(outer,empty),"Empty snapshots were allocated per query");
#endif
        }
#if SERP_DEVDIAG
        private static void TestCollector()
        {
            GatehouseDiagnosticRuntime.Begin(out var outer);var parent=GatehouseDiagnosticRuntime.Current;
            GatehouseDiagnosticRuntime.Context(11,2,3,4,5,6);var query=GatehouseDiagnosticRuntime.QueryStart();
            GatehouseDiagnosticRuntime.Context(12,3,4,5,6,7);GatehouseDiagnosticRuntime.Query(1,2,query);
            Check(parent.LastTick==11&&parent.Player==2,"Recursive query context was not restored");
            GatehouseDiagnosticRuntime.Begin(out var inner);var child=GatehouseDiagnosticRuntime.Current;
            Check(!ReferenceEquals(parent,child)&&child.Queries==0,"Nested run overwrote parent collector");
            child.Query(2,3,Stopwatch.Frequency/5);inner.Start-=Stopwatch.Frequency/5;
            GatehouseDiagnosticRuntime.Finish(ref inner);
            Check(ReferenceEquals(parent,GatehouseDiagnosticRuntime.Current)&&parent.Queries==1,"Nested collector did not restore parent");
            Check(Shared.DevDiagRuntime.LastSpike.Contains("queries=1")&&Shared.DevDiagRuntime.LastSpike.Contains("startStamp="),"Slow run aggregation/stamps missing");
            GatehouseDiagnosticRuntime.Finish(ref inner);Check(ReferenceEquals(parent,GatehouseDiagnosticRuntime.Current),"Duplicate finally corrupted depth");
            for(int i=2;i<=6;i++)parent.Query(i,i,Stopwatch.Frequency*i/100);
            string detail=parent.Detail();
            Check(detail.IndexOf("6:calls=")<detail.IndexOf("5:calls=")&&detail.Contains("3:calls=")&&!detail.Contains("2:calls="),"Top-four gate ordering incorrect");
            for(int i=7;i<=260;i++)parent.Query(i,i,1);
            Check(parent.Overflow==4,"Gate identity capacity loss was hidden");
            typeof(GatehouseDiagnosticRuntime).GetField("windowStarted",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,Stopwatch.GetTimestamp()-Stopwatch.Frequency*6);
            GatehouseDiagnosticRuntime.Finish(ref outer);
            Check(Shared.DevDiag.LastLog.Contains("gatehouseWindow")&&Shared.DevDiag.LastLog.Contains("queries=261"),"Five-second totals omitted/duplicated nested queries");
            // Unusually deep reentrancy is bounded and visibly marked, not indexed past the buffer.
            var scopes=new GatehouseDiagnosticRuntime.Scope[10];
            for(int i=0;i<scopes.Length;i++)GatehouseDiagnosticRuntime.Begin(out scopes[i]);
            for(int i=scopes.Length-1;i>=0;i--)GatehouseDiagnosticRuntime.Finish(ref scopes[i]);
        }
#endif
    }
}
