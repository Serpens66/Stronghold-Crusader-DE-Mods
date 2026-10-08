using System;
using System.Collections.Generic;
using System.Diagnostics;
using APIShared;
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
namespace AssassinAttackControlTest
{
    internal sealed unsafe class AttackControlRuntime
    {
        private readonly ManualLogSource log;
        private readonly AssassinAttackControlTestSettings settings;
        private readonly ushort* buildings;
        private readonly uint* surfaces;
        private readonly AlternativeSearch search=new AlternativeSearch();
        private readonly SortedDictionary<int,PendingMove> pending=new SortedDictionary<int,PendingMove>();
        private readonly SortedDictionary<int,Request> requests=new SortedDictionary<int,Request>();
        private Request working;
        private bool active, tickLogged, alternativeFaulted;
        private int tick;
        private long decisions, protectedCount, redirected, fallback, unknown, expanded, searchTicks;
        internal AttackControlRuntime(ManualLogSource log,AssassinAttackControlTestSettings settings,IntPtr module)
        { this.log=log; this.settings=settings; buildings=(ushort*)((byte*)module+0x4B6AA50); surfaces=(uint*)((byte*)module+0x48F71B0); }
        internal void BeginMap(Shared.GameplaySessionStartedContext context)
        { EndMap(); active=true; alternativeFaulted=false; log.LogInfo("ASSASSIN_ATTACK_CONTROL map="+context.SessionId+" started; human commands excluded."); }
        internal void EndMap() { Flush(); active=false; pending.Clear(); requests.Clear(); working=null; }
        private static bool IsGate(eStructs type) => type==eStructs.STRUCT_GATE_MAIN || type==eStructs.STRUCT_GATE_INNER;
        private static bool IsObstacle(eStructs type) => IsGate(type) || type==eStructs.STRUCT_TOWER ||
            type==eStructs.STRUCT_TOWER1 || type==eStructs.STRUCT_TOWER2 || type==eStructs.STRUCT_TOWER3 ||
            type==eStructs.STRUCT_TOWER4 || type==eStructs.STRUCT_TOWER5 || type==eStructs.STRUCT_GATE_WOOD || type==eStructs.STRUCT_GATE_POSTERN;
        private static bool Live(GameBuilding* b) => b!=null && b->r_AliveState!=AliveState.None && b->r_AliveState!=AliveState.MarkedForDeletion;
        private static bool Allied(int player,int other) => other>=1 && other<=8 &&
            (player==other || GamePlayerManagerAPI.Instance.IsPlayerAlliedTo(player,other));
        internal bool BeforeUpdate(int unitId)
        {
            if(!active || !settings.EnableMod || !UnitAccess.TryGetById(unitId,out GameUnit* unit,out _) || !UnitAccess.IsReallyAlive(in *unit)) return false;
            int player=unit->r_ControllableForPlayerId | (unit->N00000569<<8);
            if(player<1 || player>8 || !GamePlayerManagerAPI.Instance.IsAIPlayer(player) ||
                unit->r_UnitChimp!=eChimps.CHIMP_TYPE_ARAB_ASSASIN || (unit->r_AIState!=101 && unit->r_AIState!=107)) return false;
            int tile=(int)unit->r_AI_ContextTargetBuildingTileId;
            if(tile<=0 || tile>=320800) return false;
            int obstacle=buildings[tile]; GameBuilding* building=null;
            if(obstacle>0 && !GameBuildingManagerAPI.Instance.TryGetBuildingById(obstacle,out building)) return false;
            bool wall=(surfaces[tile]&0x100)!=0;
            if(!wall && (!Live(building) || !IsObstacle(building->r_BuildingType))) return false;
            // State 101 still in transit must keep its original movement plan.
            if(unit->r_AIState==101 && (unit->r_PathPlanStateBitFlags!=0 ||
                unit->r_CurrentTilePositionX!=unit->r_ContextTargetTileX || unit->r_CurrentTilePositionY!=unit->r_ContextTargetTileY)) return false;
            decisions++;
            bool protect=settings.ProtectCapturedGates && Live(building) && IsGate(building->r_BuildingType) && Allied(player,building->r_CapturedByPlayerId);
            if(protect) protectedCount++;
            if(alternativeFaulted || !settings.PreferCastleAccess || !AssassinAttackControlAPI.IsTraversalAvailable) return protect;
            int enemy=ResolveEnemy(unit,building,player);
            if(enemy==0) { unknown++; return true; }
            int start=unit->r_CurrentTilePositionY*800+unit->r_CurrentTilePositionX;
            if(requests.TryGetValue(unitId,out Request existing))
            {
                if(existing.Global!=unit->r_GlobalId || existing.Source!=start || existing.Tile!=tile || existing.Enemy!=enemy ||
                    existing.Tribe!=unit->r_TribeId) { requests.Remove(unitId); if(working==existing) working=null; }
                else if(existing.NoAlternative && existing.View.ValidateTopology() && GoalsCurrent(existing))
                { fallback++; return protect; }
                else if(existing.NoAlternative) { requests.Remove(unitId); if(working==existing) working=null; }
                else return true;
            }
            requests[unitId]=new Request { UnitId=unitId,Global=unit->r_GlobalId,Player=player,Enemy=enemy,
                Source=start,Tile=tile,Tribe=unit->r_TribeId,Speed=unit->r_CurrentSpeed,
                ContextX=unit->r_ContextTargetTileX,ContextY=unit->r_ContextTargetTileY,Protected=protect };
            unknown++;
            log.LogInfo($"ASSASSIN_ATTACK_CONTROL queued tick={tick} unit={unitId}/{unit->r_GlobalId} P{player} obstacle={obstacle} tile={tile} capturer={(Live(building)?building->r_CapturedByPlayerId:0)} enemy=P{enemy} protected={protect}");
            return true;
        }
        private static int ResolveEnemy(GameUnit* unit,GameBuilding* building,int player)
        {
            var players=GamePlayerManagerAPI.Instance;
            if(GameTribeManagerAPI.Instance.TryGetTribeById(unit->r_TribeId,out GameTribe* tribe) && tribe!=null && tribe->r_PlayerIdOwner==player)
            {
                int target=(int)tribe->r_AttackTargetOwnerPlayerId;
                if(target>=1 && target<=8 && !Allied(player,target)) return target;
            }
            if(players.TryGetPlayerResourcesById(player,out GamePlayerResources* resources) && resources!=null)
            {
                int target=(int)resources->r_SiegeAttackTargetPlayerId;
                if(target>=1 && target<=8 && !Allied(player,target)) return target;
            }
            if(Live(building) && building->r_PlayerIdOwner>=1 && building->r_PlayerIdOwner<=8 && !Allied(player,building->r_PlayerIdOwner)) return building->r_PlayerIdOwner;
            return 0;
        }
        private static void BuildGoals(int player,int enemy,out Dictionary<int,int> gates,out HashSet<int> inside)
        {
            gates=new Dictionary<int,int>(); inside=new HashSet<int>();
            var span=GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            for(int spanIndex=0;spanIndex<span.Length;spanIndex++)
            {
                ref GameBuilding b=ref span[spanIndex];
                if(b.r_AliveState==AliveState.None || b.r_AliveState==AliveState.MarkedForDeletion || !IsGate(b.r_BuildingType) || b.r_OnFireTicks!=0 ||
                    b.r_PlayerIdOwner!=enemy || Allied(player,b.r_CapturedByPlayerId) || b.r_EnterCenterTilePositionX>=800 || b.r_EnterCenterTilePositionY>=800) continue;
                int node=b.r_EnterCenterTilePositionY*800+b.r_EnterCenterTilePositionX;
                if(!gates.ContainsKey(node)) gates[node]=spanIndex+1;
            }
            int lord=GamePlayerManagerAPI.Instance.GetLordUnitId(enemy);
            if(!UnitAccess.TryGetById(lord,out GameUnit* target,out _) || !UnitAccess.IsReallyAlive(in *target)) return;
            int x=target->r_CurrentTilePositionX,y=target->r_CurrentTilePositionY;
            for(int dy=-1;dy<=1;dy++) for(int dx=-1;dx<=1;dx++)
                if(x+dx>=0 && x+dx<800 && y+dy>=0 && y+dy<800) inside.Add((y+dy)*800+x+dx);
        }
        private static bool GoalsCurrent(Request request)
        {
            BuildGoals(request.Player,request.Enemy,out Dictionary<int,int> gates,out HashSet<int> inside);
            if(gates.Count!=request.Gates.Count || !inside.SetEquals(request.Inside)) return false;
            foreach(var gate in gates) if(!request.Gates.TryGetValue(gate.Key,out int id) || id!=gate.Value) return false;
            return true;
        }
        private bool RequestCurrent(Request request,out GameUnit* unit)
        {
            if(!UnitAccess.TryGetById(request.UnitId,out unit,out _) || !UnitAccess.IsReallyAlive(in *unit) ||
                unit->r_GlobalId!=request.Global || unit->r_TribeId!=request.Tribe ||
                (unit->r_ControllableForPlayerId | (unit->N00000569<<8))!=request.Player ||
                unit->r_CurrentTilePositionY*800+unit->r_CurrentTilePositionX!=request.Source ||
                (unit->r_AIState!=0 && unit->r_AIState!=1) || unit->r_AI_ContextTargetBuildingTileId!=0 ||
                unit->r_ContextTargetTileX!=request.ContextX || unit->r_ContextTargetTileY!=request.ContextY) return false;
            GameBuilding* building=null;
            int id=buildings[request.Tile];
            if(id>0) GameBuildingManagerAPI.Instance.TryGetBuildingById(id,out building);
            return ResolveEnemy(unit,building,request.Player)==request.Enemy;
        }
        internal void OnTick(int gameTick)
        {
            tick=gameTick;
            if(active && !tickLogged) { tickLogged=true; log.LogInfo("ASSASSIN_ATTACK_CONTROL persistent game-tick callback executed after startup cleanup."); }
            if(alternativeFaulted || !active || !settings.EnableMod || !settings.PreferCastleAccess || !AssassinAttackControlAPI.IsTraversalAvailable)
            {
                if(active) foreach(var request in requests.Values)
                    if(!request.NoAlternative && RequestCurrent(request,out GameUnit* unit))
                    { unit->r_AI_ContextTargetBuildingTileId=(uint)request.Tile; unit->r_AIState=101; }
                pending.Clear(); requests.Clear(); working=null; return;
            }
            try { PruneRequests(); AdvanceSearch(); IssuePendingMoves(); }
            catch(Exception ex)
            {
                alternativeFaulted=true;
                try
                {
                    foreach(var request in requests.Values)
                        if(!request.NoAlternative && RequestCurrent(request,out GameUnit* unit))
                        { unit->r_AI_ContextTargetBuildingTileId=(uint)request.Tile; unit->r_AIState=101; }
                }
                catch(Exception restoreError) { log.LogError("ASSASSIN_ATTACK_CONTROL recovery failed: "+restoreError); }
                working=null; requests.Clear(); pending.Clear();
                log.LogError("ASSASSIN_ATTACK_CONTROL isolated search failure; alternatives disabled until next map, captured-gate guard retained: "+ex);
            }
            if(tick%200==0) Flush();
        }
        private void PruneRequests()
        {
            var obsolete=new List<int>();
            foreach(var pair in requests)
            {
                Request request=pair.Value;
                if(!UnitAccess.TryGetById(request.UnitId,out GameUnit* unit,out _) || !UnitAccess.IsReallyAlive(in *unit) ||
                    unit->r_GlobalId!=request.Global || unit->r_TribeId!=request.Tribe ||
                    unit->r_CurrentTilePositionY*800+unit->r_CurrentTilePositionX!=request.Source ||
                    (request.NoAlternative && !request.Protected && unit->r_AI_ContextTargetBuildingTileId!=request.Tile)) obsolete.Add(pair.Key);
            }
            foreach(int id in obsolete) { if(working!=null && working.UnitId==id) working=null; requests.Remove(id); }
        }
        private void AdvanceSearch()
        {
            foreach(var request in requests.Values)
            {
                if(!request.NoAlternative || !request.Protected) continue;
                bool protectedNow=settings.ProtectCapturedGates && buildings[request.Tile]>0 &&
                    GameBuildingManagerAPI.Instance.TryGetBuildingById(buildings[request.Tile],out GameBuilding* barrier) &&
                    Live(barrier) && IsGate(barrier->r_BuildingType) && Allied(request.Player,barrier->r_CapturedByPlayerId);
                if(!request.View.ValidateTopology() || !GoalsCurrent(request)) { request.NoAlternative=false; request.Protected=protectedNow; }
                else if(!protectedNow && RequestCurrent(request,out GameUnit* unit))
                { request.Protected=false; unit->r_AI_ContextTargetBuildingTileId=(uint)request.Tile; unit->r_AIState=101; }
            }
            if(working==null)
            {
                foreach(var entry in requests)
                    if(!entry.Value.NoAlternative) { working=entry.Value; break; }
                if(working==null) return;
                if(!RequestCurrent(working,out _)) { requests.Remove(working.UnitId); working=null; return; }
                working.View=AssassinAttackControlAPI.CaptureTraversal(working.Player,working.Speed);
                BuildGoals(working.Player,working.Enemy,out working.Gates,out working.Inside);
                search.Begin(working.View,working.Source,working.Gates,working.Inside);
            }
            Request job=working;
            if(!RequestCurrent(job,out _)) { requests.Remove(job.UnitId); working=null; return; }
            if(!GoalsCurrent(job))
            {
                if(RequestCurrent(job,out GameUnit* retry)) { retry->r_AI_ContextTargetBuildingTileId=(uint)job.Tile; retry->r_AIState=101; }
                requests.Remove(job.UnitId); working=null; return;
            }
            long began=Stopwatch.GetTimestamp();
            SearchResult result=search.Step(4096);
            searchTicks+=Stopwatch.GetTimestamp()-began;
            if(result.Outcome==SearchOutcome.Unknown && search.HasPendingNodes) return;
            expanded+=result.Expanded;
            working=null;
            if(result.Outcome==SearchOutcome.Unknown)
            {
                requests.Remove(job.UnitId); unknown++;
                if(RequestCurrent(job,out GameUnit* retry)) { retry->r_AI_ContextTargetBuildingTileId=(uint)job.Tile; retry->r_AIState=101; }
                return;
            }
            if(result.Outcome==SearchOutcome.NoAlternative)
            {
                if(!RequestCurrent(job,out GameUnit* unit) || !job.View.ValidateTopology() || !GoalsCurrent(job)) { requests.Remove(job.UnitId); return; }
                bool protectedNow=settings.ProtectCapturedGates && buildings[job.Tile]>0 &&
                    GameBuildingManagerAPI.Instance.TryGetBuildingById(buildings[job.Tile],out GameBuilding* barrier) &&
                    Live(barrier) && IsGate(barrier->r_BuildingType) && Allied(job.Player,barrier->r_CapturedByPlayerId);
                if(protectedNow) { job.Protected=true; job.NoAlternative=true; return; }
                job.NoAlternative=true;
                unit->r_AI_ContextTargetBuildingTileId=(uint)job.Tile;
                unit->r_AIState=101; // Re-enter Vanilla's adjacency/facing checks before destruction.
                fallback++;
            }
            else if(AlternativeSearch.Validate(job.View,result.Route))
            {
                uint goalGlobal=0;
                if(result.BuildingId>0 && GameBuildingManagerAPI.Instance.TryGetBuildingById(result.BuildingId,out GameBuilding* goal)) goalGlobal=goal->r_GlobalId;
                pending[job.UnitId]=new PendingMove { Global=job.Global,Player=job.Player,Enemy=job.Enemy,Tribe=job.Tribe,
                    Source=job.Source,Goal=result.Goal,Building=result.BuildingId,BuildingGlobal=goalGlobal,View=job.View,Route=result.Route };
                requests.Remove(job.UnitId); redirected++;
            }
            log.LogInfo($"ASSASSIN_ATTACK_CONTROL completed tick={tick} unit={job.UnitId}/{job.Global} enemy=P{job.Enemy} tile={job.Tile} outcome={result.Outcome} gate={result.BuildingId} goal={result.Goal} nodes={result.Expanded} finalStepMs={(Stopwatch.GetTimestamp()-began)*1000.0/Stopwatch.Frequency:F3}");
        }
        private void IssuePendingMoves()
        {
            var orders=new List<KeyValuePair<int,PendingMove>>(pending); pending.Clear();
            foreach(var order in orders)
            {
                int id=order.Key; PendingMove move=order.Value;
                if(!UnitAccess.TryGetById(id,out GameUnit* unit,out _) || !UnitAccess.IsReallyAlive(in *unit) || unit->r_GlobalId!=move.Global ||
                    unit->r_TribeId!=move.Tribe || (unit->r_AIState!=0 && unit->r_AIState!=1) || unit->r_AI_ContextTargetBuildingTileId!=0 ||
                    unit->r_CurrentTilePositionY*800+unit->r_CurrentTilePositionX!=move.Source || !move.View.ValidateTopology() ||
                    !AlternativeSearch.Validate(move.View,move.Route)) continue;
                if(move.Building>0 && (!GameBuildingManagerAPI.Instance.TryGetBuildingById(move.Building,out GameBuilding* goal) || !Live(goal) ||
                    goal->r_GlobalId!=move.BuildingGlobal || goal->r_PlayerIdOwner!=move.Enemy || Allied(move.Player,goal->r_CapturedByPlayerId))) continue;
                if(APIShared.UnitAccess.TryGetById(id,out unit,out _))
                    GameUnitManagerAPI.Instance.MoveToTile(id,move.Goal%800,move.Goal/800);
                else continue;
                bool published=unit->r_TargetTilePositionX==move.Goal%800 && unit->r_TargetTilePositionY==move.Goal/800 && unit->r_PathPlanLength>0 && unit->r_PathPlanStateBitFlags!=0;
                if(published) unit->r_AIState=101;
                log.LogInfo($"ASSASSIN_ATTACK_CONTROL replacement tick={tick} unit={id}/{move.Global} enemy=P{move.Enemy} gate={move.Building}/{move.BuildingGlobal} goal={move.Goal} issued=True pathPublished={published}");
            }
        }
        private void Flush()
        {
            if(decisions==0) return;
            log.LogInfo($"ASSASSIN_ATTACK_CONTROL aggregate decisions={decisions} protected={protectedCount} alternatives={redirected} noAlternative={fallback} unknown={unknown} nodes={expanded} searchMs={searchTicks*1000.0/Stopwatch.Frequency:F3}");
            decisions=protectedCount=redirected=fallback=unknown=expanded=searchTicks=0;
        }
        private sealed class Request
        {
            internal int UnitId,Player,Enemy,Source,Tile,Tribe,Speed,ContextX,ContextY;
            internal uint Global;
            internal bool Protected,NoAlternative;
            internal IAssassinTraversalView View;
            internal Dictionary<int,int> Gates;
            internal HashSet<int> Inside;
        }
        private sealed class PendingMove { internal uint Global,BuildingGlobal; internal int Player,Enemy,Tribe,Source,Goal,Building; internal IAssassinTraversalView View; internal int[] Route; }
    }
}
