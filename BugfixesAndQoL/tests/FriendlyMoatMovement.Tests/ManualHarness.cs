using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
namespace CommandFixture
{
    internal enum AliveState { IsAlive, Dead }
    [StructLayout(LayoutKind.Sequential, Size = 0x490)]
    internal struct GameUnit
    {
        internal AliveState r_AliveState;
        internal int r_ControllableForPlayerId, r_CurrentTilePositionX, r_CurrentTilePositionY;
        internal uint r_GlobalId, r_CurrentPositionTileId;
        internal bool Digger;
        internal ushort r_IsKilledByProjectile;
    }
    internal struct GameTribe { internal int r_PlayerIdOwner, r_LeaderUnitId; }
    internal unsafe sealed class GameTribeManagerAPI
    {
        internal static GameTribeManagerAPI Instance = new();
        internal GameTribe* Tribe;
        internal bool TryGetTribeById(int id, out GameTribe* tribe) { tribe=Tribe; return id==1 && tribe!=null; }
    }
    internal sealed class GamePlayerManagerAPI
    {
        internal static GamePlayerManagerAPI Instance = new();
        internal bool Ai;
        internal bool IsAIPlayer(int id) => Ai;
    }
    internal sealed class GameTileManagerAPI
    {
        internal static GameTileManagerAPI Instance = new();
        internal int GetTileId(int x,int y)=>y*800+x;
    }
    internal sealed class TribeIssueOrderMoveHereEventArgs
    {
        internal EventHookPhase Phase;
        internal bool SkipOriginalFunction;
        internal int TribeId=1, TileX=20, TileY=20;
    }
    internal enum EventHookPhase { Pre, Post }
    internal sealed class TribeIssueOrderWithTargetEventArgs { internal EventHookPhase Phase; internal bool SkipOriginalFunction; }
    internal sealed class FormationRuntime
    {
        internal Action<TribeIssueOrderMoveHereEventArgs> Observe;
        internal void OnTribeIssueOrderMoveHere(TribeIssueOrderMoveHereEventArgs args) => Observe?.Invoke(args);
    }
    public unsafe partial class UnitCommandPathRuntime
    {
        private FormationRuntime formationRuntime;
        private const int MapWidth=800;
        private IntPtr nativePathManager;
        private IntPtr nativeTribeManager;
        private bool disposed;
        private Func<IntPtr,int,int> originalFirstGroupUnitOnCompletedMoat;
        private Func<IntPtr,int,int,int,int,int> originalRegionReachability;
        private byte* nativeUnitManager;
        private bool ManualCommandsEnabled=true, TraversalEnabled;
        private MaintenanceProvider Traversal;
        private sealed class MaintenanceProvider
        {
            internal int Invalidations, Resets, Clears;
            internal void InvalidateFastMoatData()=>Invalidations++;
            internal void LogAndResetFastMoatMetrics()=>Resets++;
            internal void ClearDeferredFastMoveScope()=>Clears++;
        }
        private MoveCommandScope activeMoveCommand;
        private AttackCommandScope activeAttackCommand;
        private DirectFillCommandScope activeDirectFillCommand;
        private MoatWorkSelectionScope activeMoatWorkSelection;
        private PlanScope activePlan, pendingPlan;
        private UnitMoveFrame unitMoveFrame;
        private AttackApproachDiagnosticScope activeAttackApproachDiagnostic;
        private BuildingApproachPerformanceScope activeBuildingApproachPerformance;
        private BuildingConsumerPerformanceScope activeBuildingConsumerPerformance;
        private ushort[] pathRegionGrid=new ushort[800*800];
        private Func<IntPtr,int,int,int,int> originalCentralMovementPlan;
        private sealed class AttackCommandScope {}
        private sealed class DirectFillCommandScope {}
        private sealed class MoatWorkSelectionScope {}
        private sealed class AttackApproachDiagnosticScope {}
        private sealed class BuildingApproachPerformanceScope {}
        private sealed class BuildingConsumerPerformanceScope {}
        private sealed class PlanScope {}
        private sealed class UnitMoveFrame {}
        private sealed class MoveCommandScope
        {
            internal int TribeId=1, TargetX=20, TargetY=20, UnitsOnMoatAtDispatch=1;
            internal int[] ActiveUnitIdsAtDispatch={1,2};
            internal bool NativeCommonFallback;
        }
        private void ClearUnitMoveFrames()=>unitMoveFrame=null;
        private Action<TribeIssueOrderMoveHereEventArgs> queueMoveEvent;
        private Action<TribeIssueOrderWithTargetEventArgs> queueTargetEvent;
        private readonly List<string> order = new();
        private bool failMovePre;
        private void ObserveTribeMoveOrder(TribeIssueOrderMoveHereEventArgs args)
        {
            order.Add("move-"+args.Phase);
            if(args.Phase==EventHookPhase.Pre) { PushManualCommandContext(); activeMoveCommand=new MoveCommandScope(); if(failMovePre)throw new Exception("pre failure"); }
            else RestoreManualCommandContext();
        }
        private void ObserveTribeTargetOrder(TribeIssueOrderWithTargetEventArgs args)
        { order.Add("target-"+args.Phase); activeAttackCommand=args.Phase==EventHookPhase.Pre?new AttackCommandScope():null; }
        private void EnsureMoveCommandGroupSummary(MoveCommandScope command) {}
        private static bool IsValidTileId(int tile)=>(uint)tile<800*800;
        private readonly HashSet<int> completedMoatTiles = new();
        private bool IsCompletedMoatTile(int tile) => completedMoatTiles.Contains(tile);
        private static bool CanDigMoat(GameUnit* unit) => unit->Digger;
        private static int assertions;
        private static void Check(bool value,string text) { assertions++;if(!value)throw new Exception(text); }
        public static void Run()
        {
            var fixture=new UnitCommandPathRuntime(); fixture.Tests();
            fixture.DispatchTests();
            Console.WriteLine($"PASS: {assertions} actual manual-path and mixed-group assertions.");
        }
        private void DispatchTests()
        {
            Traversal=new MaintenanceProvider(); TraversalEnabled=false; ManualCommandsEnabled=false;
            InvalidateFastMoatData();LogAndResetFastMoatMetrics();ClearDeferredFastMoveScope();
            Check(Traversal.Invalidations==1 && Traversal.Resets==1 && Traversal.Clears==1,
                "disabled traversal policy still receives topology invalidation and context cleanup");
            Traversal=null;InvalidateFastMoatData();ClearDeferredFastMoveScope();
            ManualCommandsEnabled=true;
            var parent=new MoveCommandScope(); activeMoveCommand=parent;
            queueMoveEvent=a=>order.Add("queue-"+a.Phase);
            var move=new TribeIssueOrderMoveHereEventArgs();
            DispatchMoveEvent(move); var outer=activeMoveCommand;
            DispatchMoveEvent(new TribeIssueOrderMoveHereEventArgs());
            DispatchMoveEvent(new TribeIssueOrderMoveHereEventArgs {Phase=EventHookPhase.Post});
            Check(ReferenceEquals(activeMoveCommand,outer),"nested move restores outer command");
            move.Phase=EventHookPhase.Post; DispatchMoveEvent(move);
            Check(ReferenceEquals(activeMoveCommand,parent) && manualCommandContexts.Count==0,"outer move restores original context");
            Check(string.Join(",",order)=="queue-Pre,move-Pre,queue-Pre,move-Pre,move-Post,queue-Post,move-Post,queue-Post","dispatcher queue/command order is explicit and each boundary runs once");
            order.Clear();queueMoveEvent=a=>a.SkipOriginalFunction=true;
            DispatchMoveEvent(new TribeIssueOrderMoveHereEventArgs());
            Check(order.Count==0 && moveEventObservers.Count==0 && moveEventDepths.Count==0 && manualCommandContexts.Count==0,"consumed queue has no command or pending Post frame");
            queueMoveEvent=null;failMovePre=true;
            try{DispatchMoveEvent(new TribeIssueOrderMoveHereEventArgs());}catch(Exception){}
            Check(ReferenceEquals(activeMoveCommand,parent) && manualCommandContexts.Count==0,"failed command Pre restores parent immediately");
            order.Clear();DispatchMoveEvent(new TribeIssueOrderMoveHereEventArgs {Phase=EventHookPhase.Post});
            Check(order.Count==0 && ReferenceEquals(activeMoveCommand,parent) && manualCommandContexts.Count==0,"Post after failed Pre does not consume parent command");
            failMovePre=false;
            order.Clear();
            formationRuntime=new FormationRuntime { Observe=a=> {
                Check(ReferenceEquals(activeMoveCommand,parent),"formation sees parent context before Pre and after completed Post");
                order.Add("formation-"+a.Phase);
            }};
            queueMoveEvent=a=>order.Add("queue-"+a.Phase);
            DispatchMoveEvent(new TribeIssueOrderMoveHereEventArgs());
            DispatchMoveEvent(new TribeIssueOrderMoveHereEventArgs {Phase=EventHookPhase.Post});
            Check(string.Join(",",order)=="formation-Pre,queue-Pre,move-Pre,move-Post,queue-Post,formation-Post",
                "integrated formation runs before queue dispatch and after general command cleanup exactly once");
            formationRuntime=null;
            var attack=new AttackCommandScope();activeAttackCommand=attack;
            queueTargetEvent=a=>order.Add("target-queue");
            DispatchTargetEvent(new TribeIssueOrderWithTargetEventArgs());var outerAttack=activeAttackCommand;
            DispatchTargetEvent(new TribeIssueOrderWithTargetEventArgs());
            DispatchTargetEvent(new TribeIssueOrderWithTargetEventArgs {Phase=EventHookPhase.Post});
            Check(ReferenceEquals(activeAttackCommand,outerAttack),"nested target restores outer attack");
            DispatchTargetEvent(new TribeIssueOrderWithTargetEventArgs {Phase=EventHookPhase.Post});
            Check(ReferenceEquals(activeAttackCommand,attack) && targetCommandParents.Count==0,"target completion restores parent attack");
            queueTargetEvent=a=>a.SkipOriginalFunction=true;
            DispatchTargetEvent(new TribeIssueOrderWithTargetEventArgs());
            Check(ReferenceEquals(activeAttackCommand,attack) && targetEventObservers.Count==0 && targetCommandParents.Count==0,"consumed target retains parent and has no Post frame");
        }
        private void Tests()
        {
            GameUnit* units=(GameUnit*)NativeMemory.AllocZeroed(3*(nuint)sizeof(GameUnit));
            byte* managerAllocation=(byte*)NativeMemory.AllocZeroed(NativeProbeManagerBytes+0x18);
            byte* manager=managerAllocation+0x18;
            GameTribe* tribe=(GameTribe*)NativeMemory.AllocZeroed((nuint)sizeof(GameTribe));
            nativeProbeGrid=(byte*)NativeMemory.AllocZeroed(NativeProbeGridBytes);
            nativeProbeRectangle=(byte*)NativeMemory.AllocZeroed(0x20);
            try
            {
                APIShared.UnitAccess.Units=units; nativeUnitManager=(byte*)units;
                nativePathManager=(IntPtr)manager; GameTribeManagerAPI.Instance.Tribe=tribe;
                nativeTribeManager=(IntPtr)tribe;
                tribe->r_PlayerIdOwner=1; tribe->r_LeaderUnitId=1;
                units[1].r_ControllableForPlayerId=units[2].r_ControllableForPlayerId=1;
                units[1].r_GlobalId=41; units[2].r_GlobalId=42;
                units[1].r_CurrentTilePositionX=10; units[1].r_CurrentTilePositionY=10;
                units[2].r_CurrentTilePositionX=11; units[2].r_CurrentTilePositionY=10;
                for(int i=0;i<NativeProbeManagerBytes;i++)manager[i]=(byte)(i*17);
                *(int*)(manager+4)=100; *(int*)(manager+0xC4)=5;
                byte[] managerBefore=new ReadOnlySpan<byte>(manager,NativeProbeManagerBytes).ToArray();
                byte[] unitBefore=new ReadOnlySpan<byte>(units+1,sizeof(GameUnit)).ToArray();
                var parent=new MoveCommandScope(); activeMoveCommand=parent;
                var plan=new PlanScope(); activePlan=plan; pendingPlan=plan;
                var frame=new UnitMoveFrame(); unitMoveFrame=frame;
                var attack=new AttackCommandScope(); activeAttackCommand=attack;
                int calls=0;
                originalCentralMovementPlan=(m,id,x,y)=>
                {
                    calls++;
                    Check(nativeManualProbe && activeMoveCommand==null && activeAttackCommand==null &&
                        activePlan==null && pendingPlan==null && unitMoveFrame==null,"probe isolated from enclosing command");
                    Check(*(int*)(manager+0x80)==0 && *(int*)(manager+0x84)==0 && *(int*)(manager+0x88)==0,
                        "probe cannot inherit another unit's path modes");
                    Check(!ProbeNativeManualPath(id,x,y),"nested probe rejected without another native call");
                    for(int i=0;i<NativeProbeManagerBytes;i++)manager[i]=0xAB;
                    for(int i=0;i<sizeof(GameUnit);i++)((byte*)(units+id))[i]=0xCD;
                    nativeProbeGrid[NativeProbeGridBytes-1]=71; nativeProbeRectangle[31]=37;
                    manager[-8]=99;
                    *(long*)(manager+0x155F60)=12345;
                    *(int*)(manager+4)=101; *(int*)(manager+0xC4)=6;
                    return 3;
                };
                Check(ProbeNativeManualPath(1,20,20) && calls==1,"positive native path is one invocation");
                Check(new ReadOnlySpan<byte>(units+1,sizeof(GameUnit)).SequenceEqual(unitBefore),"entire portal/unit state restored");
                Check(new ReadOnlySpan<byte>(manager,NativeProbeManagerBytes).SequenceEqual(managerBefore),
                    "entire manager, output buffer pointer, queues and generations restored");
                Check(nativeProbeGrid[NativeProbeGridBytes-1]==0 && nativeProbeRectangle[31]==0,
                    "external distance/stamp grids and rectangle restored");
                Check(manager[-8]==0,"native memory helper preceding path manager restored");
                Check(ReferenceEquals(activeMoveCommand,parent) && ReferenceEquals(activePlan,plan) &&
                    ReferenceEquals(pendingPlan,plan) && ReferenceEquals(unitMoveFrame,frame) &&
                    ReferenceEquals(activeAttackCommand,attack) && !nativeManualProbe,"all parent contexts restored");
                originalCentralMovementPlan=(m,id,x,y)=>throw new InvalidOperationException("native fixture failure");
                bool threw=false; try{ProbeNativeManualPath(1,20,20);}catch(InvalidOperationException){threw=true;}
                Check(threw && !nativeManualProbe && ReferenceEquals(activePlan,plan),"failed probe restores contexts");
                Check(!ProbeNativeManualPath(1,800,20) && !ProbeNativeManualPath(0,20,20),"invalid probes never call native");
                units[1].r_AliveState=AliveState.Dead;
                Check(!ProbeNativeManualPath(1,20,20),"dead unit rejected"); units[1].r_AliveState=AliveState.IsAlive;
                units[1].r_IsKilledByProjectile=1;
                Check(!ProbeNativeManualPath(1,20,20),"dying unit rejected before native probe");
                units[1].r_IsKilledByProjectile=0;
                originalCentralMovementPlan=(m,id,x,y)=>id==2?7:-1;
                var command=new TribeIssueOrderMoveHereEventArgs();
                activeMoveCommand=new MoveCommandScope(); unitMoveFrame=null; activePlan=pendingPlan=null;
                PrepareNativeManualGroup(command);
                Check(activeMoveCommand.NativeCommonFallback,"unreachable leader does not block reachable second member");
                pathRegionGrid[20*800+20]=37;
                Check(IsNativeManualGroupFlood(nativePathManager,1,37,10,10),"fallback bound to actual leader and exact native target component");
                Check(!IsNativeManualGroupFlood(nativePathManager,2,37,10,10) &&
                    !IsNativeManualGroupFlood(nativePathManager,1,38,10,10) &&
                    !IsNativeManualGroupFlood(nativePathManager,1,37,11,10),"unrelated native queries cannot consume group fallback");
                tribe->r_LeaderUnitId=2;
                Check(IsNativeManualGroupFlood(nativePathManager,1,37,11,10),"same command supports opposite leader variant");
                unitMoveFrame=new UnitMoveFrame();
                Check(!IsNativeManualGroupFlood(nativePathManager,1,37,11,10),"unit query retains its own native flood");unitMoveFrame=null;
                foreach(int mode in new[]{0,1,2,3,4})
                {
                    activeMoveCommand=new MoveCommandScope();
                    ManualCommandsEnabled=mode!=1; TraversalEnabled=mode==2;
                    activeMoveCommand.UnitsOnMoatAtDispatch=mode==3?0:1;
                    GamePlayerManagerAPI.Instance.Ai=mode==4;
                    originalCentralMovementPlan=(m,id,x,y)=>mode==0?-1:7;
                    PrepareNativeManualGroup(command);
                    Check(!activeMoveCommand.NativeCommonFallback,"no reachable witness, disabled setting, addon, dry group and AI preserve vanilla");
                }
                GamePlayerManagerAPI.Instance.Ai=false; ManualCommandsEnabled=true; TraversalEnabled=false;
                foreach (int addonMode in new[] { -1, 0, 1, 2 })
                foreach (int moatUnit in new[] { 1, 2 })
                foreach (int leader in new[] { 1, 2 })
                foreach (bool reverse in new[] { false, true })
                foreach (int reachableMask in new[] { 0, 1, 2, 3 })
                {
                    TraversalEnabled=addonMode>=0;
                    tribe->r_LeaderUnitId=leader;
                    completedMoatTiles.Clear(); completedMoatTiles.Add(100+moatUnit);
                    for(int id=1;id<=2;id++)
                    {
                        units[id].r_CurrentPositionTileId=(uint)(100+id);
                        units[id].Digger=id!=moatUnit;
                    }
                    activeMoveCommand=new MoveCommandScope {
                        ActiveUnitIdsAtDispatch=reverse?new[]{2,1}:new[]{1,2} };
                    originalCentralMovementPlan=(m,id,x,y)=>
                        ((reachableMask & (1<<(id-1)))!=0 && x==20 && y==20)?1:0;
                    PrepareNativeManualGroup(command);
                    Check(activeMoveCommand.NativeCommonFallback==(reachableMask!=0),
                        $"native-only moat starter: addon={addonMode}, moat={moatUnit}, leader={leader}, reverse={reverse}, reachable={reachableMask}");
                    Check(units[moatUnit].Digger==false && !nativeManualProbe,
                        "qualification neither grants digging capability nor leaves a probe context");
                    if(reachableMask!=0)
                    {
                        originalFirstGroupUnitOnCompletedMoat=(m,t)=>moatUnit;
                        Check(SelectOwnerSafeGroupMoatMode(nativeTribeManager,1)==leader,
                            "native group mode resolves to the actual leader before addon capability filtering");
                        Check(IsNativeManualGroupFlood(nativePathManager,1,37,
                            units[leader].r_CurrentTilePositionX,units[leader].r_CurrentTilePositionY),
                            "native fallback remains bound to the actual leader in every provider mode");
                        int floods=0;
                        originalRegionReachability=(m,p,r,x,y)=> { floods++; manager[0x18]=91; return 37; };
                        Check(AllowBuilderAfterFailedRegionSearch(nativePathManager,1,37,
                            units[leader].r_CurrentTilePositionX,units[leader].r_CurrentTilePositionY)==0 &&
                            floods==1 && manager[0x18]==91,
                            "group selects native per-unit fallback after exactly one original flood with preserved outputs");
                        unitMoveFrame=new UnitMoveFrame();
                        Check(AllowBuilderAfterFailedRegionSearch(nativePathManager,1,37,
                            units[leader].r_CurrentTilePositionX,units[leader].r_CurrentTilePositionY)==37 && floods==2,
                            "member flood keeps its own native result instead of consuming the group override");
                        unitMoveFrame=null;
                    }
                }
                // A non-digger outside a moat must not change the addon's group mode.
                TraversalEnabled=true; units[1].Digger=false; units[2].Digger=true;
                completedMoatTiles.Clear(); completedMoatTiles.Add(102);
                activeMoveCommand=new MoveCommandScope();
                originalCentralMovementPlan=(m,id,x,y)=>1;
                PrepareNativeManualGroup(command);
                Check(!activeMoveCommand.NativeCommonFallback,"dry non-digger does not alter addon group routing");
                completedMoatTiles.Clear(); completedMoatTiles.Add(101);
                units[1].r_ControllableForPlayerId=2;
                PrepareNativeManualGroup(command);
                Check(!activeMoveCommand.NativeCommonFallback,"foreign non-digger cannot authorize the special group path");
                units[1].r_ControllableForPlayerId=1;
                originalCentralMovementPlan=(m,id,x,y)=>1;
                foreach (int count in new[] { 1, 120, 680 })
                {
                    int probes=count;
                    var watch=System.Diagnostics.Stopwatch.StartNew();
                    for(int i=0;i<probes;i++)
                        Check(ProbeNativeManualPath(1,20,20),"snapshot benchmark restores the actual native probe context");
                    Console.WriteLine($"NATIVE PROBE SNAPSHOTS units={count} probes={probes} ms={watch.Elapsed.TotalMilliseconds:F3}; native search mocked, snapshot buffers real");
                }
                completedMoatTiles.Clear(); TraversalEnabled=false;
                activeMoveCommand=new MoveCommandScope(); command.SkipOriginalFunction=true;
                PrepareNativeManualGroup(command);
                Check(!activeMoveCommand.NativeCommonFallback,"consumed queue command is not executed twice");
                var outerMove=activeMoveCommand; var outerFrame=new UnitMoveFrame(); unitMoveFrame=outerFrame;
                var outerPlan=new PlanScope();activePlan=pendingPlan=outerPlan;
                PushManualCommandContext(); activeMoveCommand=new MoveCommandScope();
                PushManualCommandContext(); activeMoveCommand=new MoveCommandScope();
                RestoreManualCommandContext(); RestoreManualCommandContext();
                Check(ReferenceEquals(activeMoveCommand,outerMove) && ReferenceEquals(unitMoveFrame,outerFrame) &&
                    ReferenceEquals(activePlan,outerPlan) && ReferenceEquals(pendingPlan,outerPlan),"nested commands restore original ownership");
            }
            finally { NativeMemory.Free(units);NativeMemory.Free(managerAllocation);NativeMemory.Free(tribe);NativeMemory.Free(nativeProbeGrid);NativeMemory.Free(nativeProbeRectangle);APIShared.UnitAccess.Units=null; }
        }
    }
}
namespace APIShared
{
    internal static unsafe class UnitAccess
    {
        internal static CommandFixture.GameUnit* Units;
        internal static bool IsReallyAlive(CommandFixture.GameUnit* unit) =>
            unit != null && unit->r_AliveState == CommandFixture.AliveState.IsAlive && unit->r_IsKilledByProjectile == 0;
        internal static bool TryGetById(int id,out CommandFixture.GameUnit* unit,out int failure)
        { failure=0;unit=id>0 && id<3 ? Units+id:null;return unit!=null; }
    }
}
