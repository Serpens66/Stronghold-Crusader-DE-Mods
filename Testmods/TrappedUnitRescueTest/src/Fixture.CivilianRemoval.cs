using System;
using System.Collections.Generic;
using R3;
using SHCDESE.EventAPI;
using APIShared;
using APIShared.Automation;
using APIShared.Simulation;
using APIShared.WorldMutations;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace TrappedUnitRescueTest
{
    internal sealed partial class Fixture
    {
        private sealed class CivilianRemovalProof
        {
            internal long Session,World,BeforeRemovals;
            internal bool Active=true,Written,NextOnly,OriginalGone,SawLife3,Reused,FirstTick,FirstCurrentSafe;
            internal int Farm,SourceX,SourceY,Width,ObservedTicks,ReplacementId;internal uint FarmGlobal,ReplacementGlobal;
            internal bool FarmSlotFreed,Replacement,ChainsLogged,BoundaryRecorded,BoundaryOriginalAlive;
            internal int FirstTickNumber,ReplanX,ReplanY,ReplanTile=-1;internal long BoundaryRemovals;
            internal Actor Actor;internal UnitSnapshot Before;internal string Fault;
            internal readonly List<Actor> FreePeasants=new List<Actor>();
            internal IDisposable PeasantCreated;
            internal readonly Dictionary<int,string> RejectStates=new Dictionary<int,string>();
            internal int RejectLogs,CreatedCount,RetiredCount;
            internal bool CensusCaptured,NativeStaged,ExternalMarkerWon;
        }
        private CivilianRemovalProof civilianRemoval;
        private static volatile Fixture civilianRemovalFixture;
        private static bool civilianRemovalTickRegistered;
        private IEnumerable<AutomationStep> CivilianRemovalSteps()
        {
            yield return new AutomationStep("natural-civilian-causal-removal-and-current-safe-control",TimeSpan.FromSeconds(1200),CivilianRemovalCase,true);
        }
        private void CivilianRemovalWorld()
        {
            var p=civilianRemoval;
            Require(p!=null && !stopped && session==p.Session && WorldMutationEvents.TryGetCurrentWorld(out long sid,out long world) &&
                sid==p.Session && world==p.World,"Civilian removal fixture lost its exact session/native world");
        }
        private static unsafe void ObserveCivilianRemoval(int tick)
        {
            var f=civilianRemovalFixture;var p=f?.civilianRemoval;
            if(f==null || f.stopped || p==null || !p.Active || !p.Written || p.Actor==null || p.Fault!=null)return;
            try
            {
                f.CivilianRemovalWorld();p.ObservedTicks++;
                bool same=UnitAccess.TryGetById(p.Actor.Id,out GameUnit* u,out _) && u!=null && u->r_GlobalId==p.Actor.Global;
                if(!p.FirstTick)
                {
                    p.FirstTick=true;p.FirstTickNumber=tick;
                    p.FirstCurrentSafe=same && (int)u->r_CurrentPositionTileId==p.Before.TileId &&
                        u->r_CurrentTilePositionX==p.Before.X && u->r_CurrentTilePositionY==p.Before.Y && f.runtime.Native.OrdinaryGround(p.Before.TileId);
                    f.log("civilian-removal-first-tick tick="+tick+" unit="+p.Actor.Id+"/"+p.Actor.Global+" nextOnly="+p.NextOnly+
                        " sameLife="+same+" currentSafe="+p.FirstCurrentSafe+" life="+(same?u->r_AliveState.ToString():"different-lifetime"));
                }
                if(same && u->r_AliveState==AliveState.MarkedForDeletion)p.SawLife3=true;
                // Life3 is only a request. Require the later actual native clearing/replacement too.
                if(!same || u->r_AliveState==AliveState.None)
                {
                    p.OriginalGone=true;
                    if(!same && u!=null && u->r_GlobalId!=0)
                    {
                        if(!p.Reused)f.log("civilian-removal-slot-reused tick="+tick+" unit="+p.Actor.Id+" oldGlobal="+p.Actor.Global+
                            " newGlobal="+u->r_GlobalId+" life="+u->r_AliveState);
                        p.Reused=true;
                    }
                    // ID-only chains can legitimately contain a NEW lifetime at a reused slot.
                    f.ValidateRemovedCivilianChains();
                }
                if(p.OriginalGone && p.Farm>0)f.ObserveCivilianFarmReplacement();
            }
            catch(Exception e){p.Fault=e.Message;f.log("civilian-removal-first-failure "+e.Message);}
        }
        private static unsafe void ObserveCivilianRemovalBoundary(int tick)
        {
            var f=civilianRemovalFixture;var p=f?.civilianRemoval;
            if(f==null || f.stopped || p==null || !p.Active || !p.Written || !p.NextOnly || p.BoundaryRecorded || p.Fault!=null)return;
            try
            {
                f.CivilianRemovalWorld();Require(p.FirstTick && tick==p.FirstTickNumber,"Next-only pre/post recovery boundary lost callback order");
                p.BoundaryRemovals=Plugin.GeneralObserver.RemovalCount-p.BeforeRemovals;
                p.BoundaryOriginalAlive=UnitAccess.TryGetById(p.Actor.Id,out GameUnit* u,out _) && u!=null && u->r_GlobalId==p.Actor.Global &&
                    UnitAccess.IsReallyAlive(u) && u->r_CurrentHealth==p.Actor.Health && (int)u->r_CurrentPositionTileId==p.Before.TileId &&
                    u->r_CurrentTilePositionX==p.Before.X && u->r_CurrentTilePositionY==p.Before.Y && f.runtime.Native.OrdinaryGround(p.Before.TileId);
                Require(p.FirstCurrentSafe && p.BoundaryOriginalAlive && p.BoundaryRemovals==0,"Current-safe first coordinator boundary removed/lost original civilian");
                p.BoundaryRecorded=true;
                f.log("civilian-removal-next-boundary tick="+tick+" unit="+p.Actor.Id+"/"+p.Actor.Global+" removalRequests="+p.BoundaryRemovals+
                    " preCurrentSafe=true postCurrentSafe=true originalAlive=true phase=after-default-order0-recovery-before-native-unit-step");
            }
            catch(Exception e){p.Fault=e.Message;f.log("civilian-removal-first-failure "+e.Message);}
        }
        private unsafe void ValidateRemovedCivilianChains()
        {
            CivilianRemovalWorld();var p=civilianRemoval;
            int oldEntries=0,newEntries=0,newCurrent=-1;uint currentGlobal=0;
            foreach(int tile in p.Before.TileId==p.Before.NextTileId?new[]{p.Before.TileId}:new[]{p.Before.TileId,p.Before.NextTileId})
            {
                int id=GameTileManagerAPI.Instance.GetTileUnitId(tile);var seen=new HashSet<int>();
                while(id!=0)
                {
                    Require(id>0 && id<=NativeRelocation.MaximumUnitId && seen.Add(id) && seen.Count<=NativeRelocation.MaximumUnitId,
                        "Removed civilian source has an invalid/cyclic complete tile chain");
                    Require(UnitAccess.TryGetById(id,out GameUnit* member,out _) && member!=null,"Removed civilian source has an unreadable chain member");
                    if(id==p.Actor.Id)
                    {
                        currentGlobal=member->r_GlobalId;newCurrent=checked((int)member->r_CurrentPositionTileId);
                        if(currentGlobal==p.Actor.Global)oldEntries++;
                        else
                        {
                            newEntries++;
                            Require(currentGlobal!=0 && UnitAccess.IsReallyAlive(member) && newCurrent==tile &&
                                runtime.Native.Tile(member->r_CurrentTilePositionX,member->r_CurrentTilePositionY)==tile && runtime.Native.OrdinaryGround(tile),
                                "Reused civilian ID is a stale old-source link or a new body standing on completed Moat");
                        }
                    }
                    id=member->r_InterestingTodo4MaybeRandomPathing;
                }
            }
            if(!p.ChainsLogged || newEntries>0)
            {
                log("civilian-removal-chain-check unit="+p.Actor.Id+"/"+p.Actor.Global+" oldCurrent="+p.Before.TileId+" oldNext="+p.Before.NextTileId+
                    " originalEntries="+oldEntries+" replacementEntries="+newEntries+" replacementGlobal="+currentGlobal+" replacementCurrent="+newCurrent+
                    " completeChainsValidated=true");p.ChainsLogged=true;
            }
            Require(oldEntries==0,"Removed original lifetime remains in its captured Current/Next chains");
        }
        private unsafe void ObserveCivilianFarmReplacement()
        {
            CivilianRemovalWorld();var p=civilianRemoval;bool previously=p.Replacement;p.Replacement=false;
            Require(BuildingAccess.TryGetById(p.Farm,out GameBuilding* farm,out _) && farm!=null && farm->r_GlobalId==p.FarmGlobal &&
                farm->r_AliveState==AliveState.IsAlive && farm->r_PlayerIdOwner==owner && farm->r_BuildingType==eStructs.STRUCT_APPLEFARM,
                "Original employee's Farm lifetime changed before natural replacement");
            int id=farm->r_WorkerId_1;uint global=farm->N0000176A;
            if(id==p.Actor.Id && global==p.Actor.Global)return;
            if(!p.FarmSlotFreed)
            {
                p.FarmSlotFreed=true;
                log("civilian-removal-farm-slot-released farm="+p.Farm+"/"+p.FarmGlobal+" original="+p.Actor.Id+"/"+p.Actor.Global+
                    " actualEmployee="+id+"/"+global);
            }
            if(id<=0 || global==0 || global==p.Actor.Global || !UnitAccess.TryGetById(id,out GameUnit* u,out _) || u==null ||
                u->r_GlobalId!=global || !UnitAccess.IsReallyAlive(u) || u->r_UnitChimp!=eChimps.CHIMP_TYPE_FARMER_APPLE ||
                u->r_ControllableForPlayerId!=owner || u->r_LinkedProductionBuildingId!=p.Farm || u->r_LinkedProductionBuildingGlobalId!=p.FarmGlobal)return;
            if(!previously || p.ReplacementId!=id || p.ReplacementGlobal!=global)log("civilian-removal-farm-naturally-reemployed farm="+p.Farm+"/"+p.FarmGlobal+" original="+p.Actor.Id+"/"+p.Actor.Global+
                " replacement="+id+"/"+global+" type="+u->r_UnitChimp+" life="+u->r_AliveState+" originalJobNotReplayed=true");
            p.Replacement=true;p.ReplacementId=id;p.ReplacementGlobal=global;
        }
        private IEnumerable<AutomationWait> AwaitCivilianFarmReplacement(AutomationRun run)
        {
            var p=civilianRemoval;
            // CEF00 is scheduled once per200-tick cycle. Prove actual reassignment and
            // completed Peasant->Farmer conversion, rather than assuming a timer creates it.
            for(int window=0;window<8 && !p.Replacement;window++)
            {
                foreach(var w in run.SetPause(run.CurrentMission,false))yield return w;
                yield return run.WaitForTicks(run.CurrentMission,200);
                foreach(var w in run.SetPause(run.CurrentMission,true))yield return w;
                run.InSimulation(run.CurrentMission,()=>
                {
                    CivilianRemovalWorld();Require(p.Fault==null,p.Fault);ObserveCivilianFarmReplacement();
                    int ignored=0;CivilianPopulationReady(out ignored);
                    log("civilian-removal-reemployment window="+window+" slotFreed="+p.FarmSlotFreed+" replacement="+p.Replacement);return true;
                });
            }
            Require(p.FarmSlotFreed && p.Replacement && p.ReplacementGlobal!=p.Actor.Global,
                "Controlled natural population did not produce a new original-Farm employee lifetime within eight genuine hiring windows");
        }
        private void ValidateCivilianMigrationLayout()
        {
            // runtime.Native only exists after NativeRelocation.Create's canonical NativeHash gate.
            Require(runtime.Native!=null && GamePlayerManagerAPI.Instance.IsPlayerIdValid(owner) &&
                System.Runtime.InteropServices.Marshal.SizeOf(typeof(GamePlayerResources))==0x583C,"Civilian migration player layout/domain unavailable");
            foreach(var field in new[]{
                new KeyValuePair<string,int>(nameof(GamePlayerResources.r_CurrentPopularity),0x60),
                new KeyValuePair<string,int>(nameof(GamePlayerResources.r_PeasantSpawnProgressRequired),0x6C),
                new KeyValuePair<string,int>(nameof(GamePlayerResources.r_PeasantSpawnProgress),0x70),
                new KeyValuePair<string,int>(nameof(GamePlayerResources.r_CivilianHousingSpace),0x74),
                new KeyValuePair<string,int>(nameof(GamePlayerResources.r_ReadyPeasants),0x88),
                new KeyValuePair<string,int>(nameof(GamePlayerResources.r_ExistingPeasants),0x8C),
                new KeyValuePair<string,int>(nameof(GamePlayerResources.r_KeepId),0x94),
                new KeyValuePair<string,int>(nameof(GamePlayerResources.r_KeepDoorId),0x1D4),
                new KeyValuePair<string,int>(nameof(GamePlayerResources.r_KeepDoorTilePositionX),0x1D8),
                new KeyValuePair<string,int>(nameof(GamePlayerResources.r_KeepDoorTilePositionY),0x1DC),
                new KeyValuePair<string,int>(nameof(GamePlayerResources.r_TotalPopulation),0x2180),
                new KeyValuePair<string,int>(nameof(GamePlayerResources.r_IsPaused),0x2210),
                new KeyValuePair<string,int>(nameof(GamePlayerResources.r_WinLossState),0x2244)})
                Require(System.Runtime.InteropServices.Marshal.OffsetOf(typeof(GamePlayerResources),field.Key).ToInt32()==field.Value,
                    "Civilian migration public field layout changed: "+field.Key);
        }
        private unsafe void SetupCivilianRemoval(bool worker)
        {
            CivilianRemovalWorld();ValidateCivilianMigrationLayout();Plugin.SetGeneralFixtureSession(false);
            if(!worker)ArmCivilianPeasantArrivals();
            if(worker)
            {
                appleWorld=ObserveAppleWorld();civilianRemoval.Farm=SetupNaturalAppleFarm(scenario.StartsWith("rescue-removal-ai-worker-",StringComparison.Ordinal));civilianRemoval.FarmGlobal=appleWorld.FarmGlobal;
            }
            else
            {
                FindSite();CreateBakerPrefab(cx-10,cy+6,eMappers.MAPPER_HOVEL,eStructs.STRUCT_HOVEL);
            }
            if(!civilianRemovalTickRegistered)
            {
                Require(SimulationTickEvents.TryRegister(Plugin.Guid,"acceptance-civilian-removal",ObserveCivilianRemoval,out string reason,order:-100),reason);
                Require(SimulationTickEvents.TryRegister(Plugin.Guid,"acceptance-civilian-removal-after-recovery",ObserveCivilianRemovalBoundary,out reason,order:100),reason);
                civilianRemovalTickRegistered=true;
            }
            civilianRemovalFixture=this;
        }
        private unsafe void ArmCivilianPeasantArrivals()
        {
            var p=civilianRemoval;Require(p.PeasantCreated==null,"Peasant arrival observer already armed");
            p.PeasantCreated=UnitR3EventHooks.OnUnitCreate.Observable.Subscribe(args=>{
                if(!p.Active || p.Written || stopped || args.Phase!=EventHookPhase.Post || args.ReturnValue<=0 || args.ReturnValue>NativeRelocation.MaximumUnitId)return;
                try
                {
                    CivilianRemovalWorld();int id=checked((int)args.ReturnValue);
                    // Read the actual original result: other Pre subscribers may have changed input arguments.
                    if(!UnitAccess.TryGetById(id,out GameUnit* u,out _) || u==null || u->r_GlobalId==0 ||
                        u->r_UnitChimp!=eChimps.CHIMP_TYPE_PEASANT || u->r_ControllableForPlayerId!=owner)return;
                    if(u->r_AliveState!=AliveState.NeedsInit && u->r_AliveState!=AliveState.IsAlive)return;
                    CacheCivilianPeasant(u,id,"native-create-post");p.CreatedCount++;
                }
                catch(Exception e){if(p.Fault==null)p.Fault="Peasant arrival observer: "+e.Message;}
            });
        }
        private unsafe void CacheCivilianPeasant(GameUnit* u,int id,string origin)
        {
            var p=civilianRemoval;
            for(int i=p.FreePeasants.Count-1;i>=0;i--)
            {
                var a=p.FreePeasants[i];
                if(a.Id==id && a.Global==u->r_GlobalId)return;
                if(a.Id==id || !UnitAccess.TryGetById(a.Id,out GameUnit* old,out _) || old==null || old->r_GlobalId!=a.Global || old->r_AliveState==AliveState.None)
                {
                    log("civilian-removal-cohort-retired unit="+a.Id+"/"+a.Global+" reason=observed-original-life-gone");
                    p.FreePeasants.RemoveAt(i);p.RejectStates.Remove(a.Id);p.RetiredCount++;
                }
            }
            Require(p.FreePeasants.Count<128,"Natural peasant live cache exceeds explicit128 bound");
            p.FreePeasants.Add(CivilianActor(u,id));
            log("civilian-removal-peasant-arrival origin="+origin+" unit="+id+"/"+u->r_GlobalId+" life="+u->r_AliveState+
                " ai="+u->r_AIState+" transition="+runtime.Native.Word(u,0x9CA)+" current="+u->r_CurrentTilePositionX+","+u->r_CurrentTilePositionY);
        }
        private unsafe bool RejectCivilian(Actor actor,GameUnit* u,string reason,UnitSnapshot b=null)
        {
            var p=civilianRemoval;
            string state=actor.Global+":"+reason+":"+(u==null?"unreadable":u->r_GlobalId+":"+u->r_AliveState+":"+u->r_AIState);
            if(!p.RejectStates.TryGetValue(actor.Id,out string previous) || previous!=state)
            {
                p.RejectStates[actor.Id]=state;
                if(p.RejectLogs<256)
                {
                    p.RejectLogs++;log("civilian-removal-acquire-reject unit="+actor.Id+"/"+actor.Global+" reason="+reason+
                        " actualGlobal="+(u==null?0:u->r_GlobalId)+" life="+(u==null?"unreadable":u->r_AliveState.ToString())+
                        " ai="+(u==null?-1:u->r_AIState)+" employer="+(u==null?-1:runtime.Native.Word(u,0x9F0))+
                        " pending="+(u==null?-1:runtime.Native.Word(u,0x708))+" transition="+(u==null?-1:runtime.Native.Word(u,0x9CA))+
                        " current="+(u==null?"none":u->r_CurrentTilePositionX+","+u->r_CurrentTilePositionY)+
                        " flags="+b?.Flags+" ss="+b?.Substep+" unsupported="+b?.Unsupported+
                        " currentGround="+(u==null?"none":AppleTreeTileFacts(runtime.Native.Tile(u->r_CurrentTilePositionX,u->r_CurrentTilePositionY)))+
                        " nextGround="+(b==null?"none":AppleTreeTileFacts(b.NextTileId)));
                }
                else if(p.RejectLogs++==256)log("civilian-removal-acquire-reject-log-limit count=256 furtherChangesSuppressed=true");
            }
            return false;
        }
        private unsafe bool StageFreeCivilian(Actor actor,GameUnit* u)
        {
            var p=civilianRemoval;if(p.NextOnly || p.NativeStaged)return false;
            Require(!Plugin.GeneralObserver.IsActive,"Peasant native setup placement requires General OFF");
            int ai=u->r_AIState,tribe=u->r_TribeId;uint hp=u->r_CurrentHealth;
            for(int dy=-10;dy<=10;dy++)for(int dx=-10;dx<=10;dx++)
            {
                int x=cx+dx,y=cy+dy,tile=runtime.Native.Tile(x,y);
                if(!MoatWorkerSourceEligible(tile) || GameTileManagerAPI.Instance.GetTileUnitId(tile)!=0)continue;
                bool clear=true;
                for(int oy=-1;oy<=1;oy++)for(int ox=-1;ox<=1;ox++)
                {
                    int neighbour=runtime.Native.Tile(x+ox,y+oy);
                    if(!MoatWorkerSourceEligible(neighbour) || GameTileManagerAPI.Instance.GetTileUnitId(neighbour)!=0)clear=false;
                }
                if(!clear)continue;
                log("civilian-removal-native-setup unit="+actor.Id+"/"+actor.Global+" originalAI="+ai+
                    " originalCurrent="+u->r_CurrentTilePositionX+","+u->r_CurrentTilePositionY+" target="+x+","+y+" original="+AppleTreeTileFacts((int)u->r_CurrentPositionTileId));
                p.NativeStaged=true;Require(runtime.Native.Relocate(actor.Id,x,y),"Audited native free-Peasant setup placement failed");
                Require(UnitAccess.TryGetById(actor.Id,out GameUnit* after,out _) && after!=null && after->r_GlobalId==actor.Global &&
                    UnitAccess.IsReallyAlive(after) && after->r_UnitChimp==actor.Type && after->r_ControllableForPlayerId==owner &&
                    after->r_CurrentHealth==hp && after->r_AIState==ai && after->r_TribeId==tribe && CivilianIsNaturallyFreePeasant(after),
                    "Native Peasant setup changed identity/health/AI/owner/tribe/free-role");
                return true;
            }
            return false;
        }
        private unsafe void CaptureNaturalFreePeasants()
        {
            var p=civilianRemoval;CivilianRemovalWorld();Require(!p.CensusCaptured,"Natural peasant census already captured");p.CensusCaptured=true;
            int own=0,life=0,employed=0,pending=0,phase=0,free=0;
            // One SETUP census. Retain real Peasant lifetimes even while their ordinary
            // migration fade settles; source admission still requires the full free guard.
            for(int id=1;id<=NativeRelocation.MaximumUnitId;id++)
            {
                if(!UnitAccess.TryGetById(id,out GameUnit* u,out _) || u==null || u->r_UnitChimp!=eChimps.CHIMP_TYPE_PEASANT || u->r_ControllableForPlayerId!=owner)continue;
                own++;if(!UnitAccess.IsReallyAlive(u)){life++;continue;}
                Require(p.FreePeasants.Count<128,"Natural peasant setup cohort exceeds explicit128 bound");
                CacheCivilianPeasant(u,id,"setup-census");
                if(runtime.Native.Word(u,0x9F0)!=0)employed++;
                else if(runtime.Native.Word(u,0x708)!=0)pending++;
                else if(!CivilianIsNaturallyFreePeasant(u))phase++;
                else free++;
                if(p.FreePeasants.Count<=4)log("civilian-removal-peasant-member unit="+id+"/"+u->r_GlobalId+" life="+u->r_AliveState+
                    " ai="+u->r_AIState+" employer="+runtime.Native.Word(u,0x9F0)+" pendingType="+runtime.Native.Word(u,0x708)+
                    " transition="+runtime.Native.Word(u,0x9CA)+" current="+u->r_CurrentTilePositionX+","+u->r_CurrentTilePositionY);
            }
            log("civilian-removal-natural-peasant-census own="+own+" notAlive="+life+" cohort="+p.FreePeasants.Count+
                " employed="+employed+" pending="+pending+" excludedPhase="+phase+" freeNow="+free+" censusOnce=true rawSpawn=false");
            Require(p.FreePeasants.Count>0,"Positive native peasant population had no live natural Peasant identity; see complete census guards");
        }
        private unsafe bool CivilianPopulationReady(out int waitBudget)
        {
            CivilianRemovalWorld();waitBudget=0;
            Require(GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(owner,out GamePlayerResources* r) && r!=null,"Civilian population resources unavailable");
            // D2C40: actual cumulative native6C and signed increment70. The SE names are historical aliases.
            uint progress=r->r_PeasantSpawnProgressRequired;int increment=r->r_PeasantSpawnProgress;
            log("civilian-removal-migration progress6C="+progress+" increment70="+increment+" popularity="+r->r_CurrentPopularity+
                " population="+r->r_TotalPopulation+" housing="+r->r_CivilianHousingSpace+" existing="+r->r_ExistingPeasants+
                " ready="+r->r_ReadyPeasants+" keep="+r->r_KeepId+" door="+r->r_KeepDoorId+" paused="+r->r_IsPaused+" winLoss="+r->r_WinLossState);
            Require(r->r_IsPaused==0 && r->r_WinLossState==WinLossState.None,"Native player is ineligible for ordinary migration");
            Require(r->r_KeepId>0 && r->r_KeepDoorId>0 && BuildingAccess.TryGetById(checked((int)r->r_KeepId),out GameBuilding* keep,out _) &&
                keep!=null && keep->r_AliveState==AliveState.IsAlive && keep->r_PlayerIdOwner==owner,"Natural migration Keep is not initialized/owned");
            Require(BuildingAccess.TryGetById(checked((int)r->r_KeepDoorId),out GameBuilding* door,out _) && door!=null &&
                door->r_AliveState==AliveState.IsAlive && door->r_PlayerIdOwner==owner &&
                runtime.Native.Tile(checked((int)r->r_KeepDoorTilePositionX),checked((int)r->r_KeepDoorTilePositionY))>=0,"Natural migration KeepDoor is invalid");
            if(r->r_ExistingPeasants>0)return true;
            Require(r->r_TotalPopulation<r->r_CivilianHousingSpace,"No population vacancy for a first natural Peasant");
            Require(progress<=4000 && increment>0,"No positive ordinary migration progress; setup must be corrected rather than retried");
            // First arrival requires4000, not an arbitrary64-tick delay. One extra native
            // 200-tick scheduling window covers post-spawn initialization/census settlement.
            waitBudget=checked((int)((4000-progress+(uint)increment-1)/(uint)increment)+200);
            Require(waitBudget<=4200,"Migration speed exceeds the explicit controlled setup budget");return false;
        }
        private IEnumerable<AutomationWait> AwaitCivilianPopulation(AutomationRun run)
        {
            foreach(var w in run.SetPause(run.CurrentMission,false))yield return w;
            yield return run.WaitForTicks(run.CurrentMission,16);
            foreach(var w in run.SetPause(run.CurrentMission,true))yield return w;
            int budget=0;bool ready=run.InSimulation(run.CurrentMission,()=>CivilianPopulationReady(out budget));
            for(int consumed=0;!ready && consumed<budget;consumed+=16)
            {
                foreach(var w in run.SetPause(run.CurrentMission,false))yield return w;
                yield return run.WaitForTicks(run.CurrentMission,16);
                foreach(var w in run.SetPause(run.CurrentMission,true))yield return w;
                int ignored=0;ready=run.InSimulation(run.CurrentMission,()=>CivilianPopulationReady(out ignored));
            }
            Require(ready,"Native positive migration failed within its progress-derived arrival/settlement bound");
        }
        private unsafe bool CivilianIsNaturallyFreePeasant(GameUnit* u)=>
            u->r_UnitChimp==eChimps.CHIMP_TYPE_PEASANT && runtime.Native.Word(u,0x9F0)==0 && runtime.Native.Word(u,0x708)==0 &&
            u->r_AIState!=4 && u->r_AIState!=5 && u->r_AIState!=109 && u->r_AIState!=106;
        //190B30 uses employer9F0, not Home990, when selecting a genuinely free Peasant.
        private static unsafe Actor CivilianActor(GameUnit* u,int id)=>new Actor{Id=id,Global=u->r_GlobalId,Health=u->r_CurrentHealth,
            Type=u->r_UnitChimp,Tribe=u->r_TribeId,X=u->r_CurrentTilePositionX,Y=u->r_CurrentTilePositionY,
            SourceX=u->r_CurrentTilePositionX,SourceY=u->r_CurrentTilePositionY};
        private unsafe bool CivilianFootprint(UnitSnapshot b,out int x,out int y,out int width)
        {
            x=b.X;y=b.Y;width=1;var p=civilianRemoval;
            bool moving=b.OrdinaryStep && b.Substep>=0 && b.Substep<8 && (b.Flags==2 || b.Flags==4) &&
                Math.Max(Math.Abs(b.NextX-b.X),Math.Abs(b.NextY-b.Y))==1;
            if(p.NextOnly)
            {
                // A first-tick Current-safe control, not a guessed post-midpoint acceptance.
                if(!moving || b.Substep>1)return false;
                x=b.NextX;y=b.NextY;
            }
            else if(moving)
            {x=Math.Max(b.X,b.NextX);y=Math.Max(b.Y,b.NextY);width=2;}
            else if(b.Flags!=0 || b.Substep!=8 || b.X!=b.NextX || b.Y!=b.NextY)return false;
            var tiles=GameTileManagerAPI.Instance;
            for(int yy=y-width/2;yy<=y;yy++)for(int xx=x-width/2;xx<=x;xx++)
            {
                int tile=runtime.Native.Tile(xx,yy);
                if(!MoatWorkerSourceEligible(tile))return false;
                int id=tiles.GetTileUnitId(tile);if(id!=0 && id!=b.Id)return false;
                if(appleWorld!=null && CivilianAppleAccessInBrush(xx,yy))return false;
            }
            return true;
        }
        private unsafe bool CivilianAppleAccessInBrush(int x,int y)
        {
            foreach(int id in new[]{appleWorld.Farm,appleWorld.Granary})
            {
                Require(BuildingAccess.TryGetById(id,out GameBuilding* b,out _) && b!=null,"Civilian apple access building disappeared");
                if(Math.Abs(x-b->r_TileAccessPositionX)<=1 && Math.Abs(y-b->r_TileAccessPositionY)<=1)return true;
            }
            return false;
        }
        private unsafe bool TryCivilianRemovalActor(Actor actor,bool worker)
        {
            var p=civilianRemoval;
            if(!UnitAccess.TryGetById(actor.Id,out GameUnit* u,out _) || u==null || u->r_GlobalId!=actor.Global || !UnitAccess.IsReallyAlive(u) ||
                u->r_ControllableForPlayerId!=owner || u->r_UnitChimp!=actor.Type)return RejectCivilian(actor,u,"identity-life-type-owner");
            if(worker)
            {
                if(u->r_UnitChimp!=eChimps.CHIMP_TYPE_FARMER_APPLE || u->r_LinkedProductionBuildingId!=p.Farm ||
                    u->r_LinkedProductionBuildingGlobalId!=p.FarmGlobal)return RejectCivilian(actor,u,"worker-binding");
            }
            else if(!CivilianIsNaturallyFreePeasant(u))return RejectCivilian(actor,u,"natural-phase-employer-or-pending");
            var b=runtime.SnapshotUnit(actor.Id,true,false);
            if(b==null || !b.CivilianRemoval || b.Unsupported!=null || b.GlobalId!=actor.Global)
                return RejectCivilian(actor,u,"runtime-admission",b);
            int x=0,y=0,width=0;
            bool geometry=runtime.Native.OrdinaryGround(b.TileId) && (!b.OrdinaryStep || runtime.Native.OrdinaryGround(b.NextTileId)) &&
                (b.CurrentHadExit || b.OrdinaryStep && b.NextHadExit) && CountInTileChain(b.TileId,b.Id)==1 && CivilianFootprint(b,out x,out y,out width);
            if(!geometry && !worker && !p.NextOnly && StageFreeCivilian(actor,u))
            {
                b=runtime.SnapshotUnit(actor.Id,true,false);
                geometry=b!=null && b.CivilianRemoval && b.Unsupported==null && b.GlobalId==actor.Global && b.CurrentHadExit &&
                    CountInTileChain(b.TileId,b.Id)==1 && CivilianFootprint(b,out x,out y,out width);
                Require(geometry,"Native Peasant setup did not produce the fully qualified ordinary source pose");
            }
            if(!geometry)return RejectCivilian(actor,u,"source-geometry",b);
            Require(b.Job==null && !b.HasCapturedVegetation && b.Candidates.Count==0,"Civilian admission still performed old job/vegetation/BFS capture");
            actor.Health=b.Health;actor.X=actor.SourceX=b.X;actor.Y=actor.SourceY=b.Y;
            p.Actor=actor;p.Before=b;p.SourceX=x;p.SourceY=y;p.Width=width;return true;
        }
        private unsafe bool AcquireCivilianRemoval(bool worker)
        {
            var p=civilianRemoval;CivilianRemovalWorld();Require(p.Fault==null,p.Fault);
            if(worker)
            {
                ValidateAppleWorld();
                if(!BuildingAccess.TryGetById(p.Farm,out GameBuilding* b,out _) || b==null || b->r_GlobalId!=p.FarmGlobal || b->r_AliveState!=AliveState.IsAlive)return false;
                int id=b->r_WorkerId_1;
                if(id==0 || !UnitAccess.TryGetById(id,out GameUnit* u,out _) || u==null || u->r_GlobalId!=b->N0000176A)return false;
                return TryCivilianRemovalActor(CivilianActor(u,id),true);
            }
            foreach(var a in p.FreePeasants)if(TryCivilianRemovalActor(a,false))return true;
            return false;
        }
        private unsafe void ApplyCivilianRemovalMoat()
        {
            var p=civilianRemoval;CivilianRemovalWorld();var b=runtime.SnapshotUnit(p.Actor.Id,true,false);int x=0,y=0,width=0;
            Require(b!=null && b.CivilianRemoval && b.Unsupported==null && b.GlobalId==p.Before.GlobalId && b.X==p.Before.X && b.Y==p.Before.Y &&
                b.NextX==p.Before.NextX && b.NextY==p.Before.NextY && b.Flags==p.Before.Flags && b.Substep==p.Before.Substep &&
                CivilianFootprint(b,out x,out y,out width) && x==p.SourceX && y==p.SourceY && width==p.Width,
                "Natural civilian pose/geometry changed before actual source");
            Require(b.Job==null && b.Candidates.Count==0 && !b.HasCapturedVegetation,"Civilian source depends on obsolete continuation capture");
            p.Before=b;p.BeforeRemovals=Plugin.GeneralObserver.RemovalCount;
            if(scenario=="rescue-removal-worker-replan-on")SelectCivilianReplanGoal();
            Plugin.SetGeneralFixtureSession(expectedEnabled);
            Require(Plugin.GeneralObserver.IsActive==expectedEnabled,"Civilian source activation differs");
            long captures=Plugin.GeneralObserver.CaptureCount;
            var tiles=GameTileManagerAPI.Instance;
            EditorBrush.Call(tiles.GetTileManager(),owner,tiles.GetTileId(x,y),y,width,unchecked((int)(uint)TilePropertyFlag.IsMoat),2,0);
            Require(Plugin.GeneralObserver.RemovalCount==p.BeforeRemovals,"Civilian removal ran inside the native construction transaction");
            Require(UnitAccess.TryGetById(p.Actor.Id,out GameUnit* u,out _) && u!=null && u->r_GlobalId==p.Actor.Global && UnitAccess.IsReallyAlive(u) &&
                u->r_CurrentTilePositionX==b.X && u->r_CurrentTilePositionY==b.Y,"Native writer already changed/lost source actor before deferred recovery");
            if(b.OrdinaryStep)Require((tiles.GetTilePropertyFlag(b.NextTileId)&TilePropertyFlag.IsMoat)!=0,"Actual brush did not invalidate accepted Next");
            if(p.NextOnly)
                Require(runtime.Native.OrdinaryGround(b.TileId) && HasStructuralExit(p.Actor),"Next-only control lacks an ordinary Current and genuine legal exit");
            else
                Require((tiles.GetTilePropertyFlag(b.TileId)&TilePropertyFlag.IsMoat)!=0 && !HasStructuralExit(p.Actor),"Both-endpoint source is not a genuine completed trap");
            if(expectedEnabled)Require(Plugin.GeneralObserver.CaptureCount>captures,"Enabled real writer was not observed");
            else Require(Plugin.GeneralObserver.CaptureCount==captures,"Disabled real writer unexpectedly captured a candidate");
            p.Written=true;
            log("civilian-removal-source unit="+p.Actor.Id+"/"+p.Actor.Global+" type="+p.Actor.Type+" enabled="+expectedEnabled+
                " nextOnly="+p.NextOnly+" flags="+b.Flags+" substep="+b.Substep+" current="+b.X+","+b.Y+" next="+b.NextX+","+b.NextY+
                " brush="+x+","+y+" width="+width+" actualUnsuppressedMoat=true noJob=true noVegetation=true noBfs=true deferred=true");
        }
        private void SelectCivilianReplanGoal()
        {
            var p=civilianRemoval;var tiles=GameTileManagerAPI.Instance;
            for(int direction=0;direction<Dx.Length;direction++)
            {
                int x=p.Before.X+Dx[direction],y=p.Before.Y+Dy[direction];
                if(x>=p.SourceX-p.Width/2 && x<=p.SourceX && y>=p.SourceY-p.Width/2 && y<=p.SourceY)continue;
                int tile=runtime.Native.Tile(x,y);
                if(tile<0 || !MoatWorkerSourceEligible(tile) || tiles.GetTileUnitId(tile)!=0)continue;
                p.ReplanX=x;p.ReplanY=y;p.ReplanTile=tile;
                log("civilian-removal-replan-goal-before-source chosen="+x+","+y+" oldRequested="+p.Before.RequestedX+","+p.Before.RequestedY+
                    " outsideFootprint=true facts="+AppleTreeTileFacts(tile));return;
            }
            Require(false,"No different ordinary empty replan goal outside the proven mutation footprint; no native0 attempt made");
        }
        private unsafe void ReplanTrappedCivilianBeforeRecovery()
        {
            CivilianRemovalWorld();var p=civilianRemoval;var observer=Plugin.GeneralObserver;
            Require(expectedEnabled && p.Written && !p.NextOnly && p.Farm>0 && !observer.IsRecovering && observer.PendingCount>0,
                "Civilian replan regression lacks a deferred worker source outside recovery");
            Require(UnitAccess.TryGetById(p.Actor.Id,out GameUnit* before,out _) && before!=null && before->r_GlobalId==p.Actor.Global &&
                UnitAccess.IsReallyAlive(before),"Civilian replan original life unavailable");
            int ai=before->r_AIState,tribe=before->r_TribeId;long removals=observer.RemovalCount;
            int flags=before->r_PathPlanStateBitFlags,substep=before->r_MovementSubstep;
            Require(p.ReplanTile>=0 && (p.ReplanX!=p.Before.X || p.ReplanY!=p.Before.Y) && runtime.Native.OrdinaryGround(p.ReplanTile) &&
                GameTileManagerAPI.Instance.GetTileUnitId(p.ReplanTile)==0,"Preselected different replan goal became unsafe or occupied");
            runtime.Native.TryReissue(p.Actor.Id,p.ReplanX,p.ReplanY,
                ()=>!stopped && p.Active && !observer.IsRecovering && WorldMutationEvents.TryGetCurrentWorld(out long sid,out long world) &&
                    sid==p.Session && world==p.World,out var receipt);
            bool readAfter=UnitAccess.TryGetById(p.Actor.Id,out GameUnit* observed,out _);
            log("civilian-removal-replan-after-raw readSuccess="+readAfter+" pointerNonNull="+(observed!=null)+
                " expected="+p.Actor.Id+"/"+p.Actor.Global+
                (observed==null?" unavailable=true":" global="+observed->r_GlobalId+" type="+observed->r_UnitChimp+
                    " life="+observed->r_AliveState+" hp="+observed->r_CurrentHealth+" owner="+observed->r_ControllableForPlayerId+
                    " tribe="+observed->r_TribeId+" tribeStamp="+runtime.Native.JobDword(observed,0x93C)+" ai="+observed->r_AIState+
                    " current="+observed->r_CurrentTilePositionX+","+observed->r_CurrentTilePositionY+" currentTile="+observed->r_CurrentPositionTileId+
                    " flags="+observed->r_PathPlanStateBitFlags+" substep="+observed->r_MovementSubstep+
                    " next="+observed->r_NextTilePositionX2+","+observed->r_NextTilePositionY2+" nextTile="+observed->r_NextPositionTileId2+
                    " storedMoat9C8="+runtime.Native.Word(observed,0x9C8)+" hidden9B0="+runtime.Native.Word(observed,0x9B0)+
                    " dying8F8="+runtime.Native.Word(observed,0x8F8)));
            var observedAdmission=runtime.SnapshotUnit(p.Actor.Id,true,false);
            log("civilian-removal-replan-after-admission snapshot="+(observedAdmission!=null)+" global="+observedAdmission?.GlobalId+
                " civilian="+observedAdmission?.CivilianRemoval+" unsupported="+observedAdmission?.Unsupported+
                " ai="+observedAdmission?.AIState+" current="+observedAdmission?.X+","+observedAdmission?.Y);
            log("civilian-removal-external-replan unit="+p.Actor.Id+"/"+p.Actor.Global+" chosenGoal="+p.ReplanX+","+p.ReplanY+" goalFacts="+AppleTreeTileFacts(p.ReplanTile)+" originalAI="+ai+
                " recovering="+observer.IsRecovering+" nativeInvoked="+receipt?.NativeInvoked+" nativeReturned="+receipt?.NativeReturned+
                " nativeResult="+receipt?.NativeResult+" status="+receipt?.Status+" pending="+observer.PendingCount+
                " failure="+receipt?.Failure?.GetType().Name+" wrapperFailure="+receipt?.WrapperFailure?.GetType().Name);
            // Native1, veto and exceptions are distinct outcomes, never substitutes for this regression.
            Require(receipt!=null && receipt.IsCurrentWorld && receipt.NativeInvoked && receipt.NativeReturned && receipt.NativeResult==0 &&
                receipt.Failure==null && receipt.WrapperReturned && receipt.WrapperFailure==null,
                "Civilian replan regression did not obtain an actual normal native0; no refusal claim is made");
            Require(!observer.IsRecovering && observer.PendingCount>0 && observer.RemovalCount==removals,
                "External actual-native0 retired the deferred civilian removal proof or recovered synchronously");
            Require(UnitAccess.TryGetById(p.Actor.Id,out GameUnit* after,out _) && after!=null && after->r_GlobalId==p.Actor.Global &&
                (after->r_AliveState==AliveState.IsAlive || after->r_AliveState==AliveState.MarkedForDeletion) &&
                after->r_UnitChimp==p.Actor.Type && after->r_ControllableForPlayerId==owner &&
                after->r_CurrentHealth==p.Actor.Health && after->r_TribeId==tribe &&
                after->r_CurrentTilePositionX==p.Before.X && after->r_CurrentTilePositionY==p.Before.Y &&
                (int)after->r_CurrentPositionTileId==p.Before.TileId &&
                (GameTileManagerAPI.Instance.GetTilePropertyFlag(p.Before.TileId)&TilePropertyFlag.IsMoat)!=0,
                "Actual-native0 changed the trapped civilian original identity/pose");
            if(after->r_AliveState==AliveState.MarkedForDeletion)
            {
                // This is an observed external native-call marker, not completed teardown
                // and not a claim that an arbitrary IdentityChanged receipt was safe.
                Require(after->r_AIState<108 && after->r_PathPlanStateBitFlags==flags && after->r_MovementSubstep==substep &&
                    after->r_NextTilePositionX2==p.Before.NextX && after->r_NextTilePositionY2==p.Before.NextY &&
                    (int)after->r_NextPositionTileId2==p.Before.NextTileId && runtime.Native.Word(after,0x8F8)==0 &&
                    runtime.Native.Word(after,0x6EC)==0 && runtime.Native.Word(after,0x9C8)==0 && runtime.Native.Word(after,0x9B0)==0 &&
                    runtime.Native.Word(after,0x8EC)==0 && runtime.Native.Word(after,0x8F6)==0 && runtime.Native.Word(after,0x708)==0,
                    "External Life3 marker changed the original ordinary civilian mode/pose or entered recruitment/transport/death");
                p.ExternalMarkerWon=true;p.SawLife3=true;
                log("civilian-removal-external-marker-won externalMarkerWon=true unit="+p.Actor.Id+"/"+p.Actor.Global+
                    " nativeResult=0 life="+after->r_AliveState+" ai="+after->r_AIState+" expectedOwnRemoval=0 nativeTeardownStillRequired=true");
                return;
            }
            Require(HasStructuralExit(p.Actor)==false,"Live native0 no longer has the genuine current trap");
            var live=runtime.SnapshotUnit(p.Actor.Id,true,false);
            log("civilian-removal-post-replan-admission ai="+live?.AIState+" flags="+live?.Flags+" substep="+live?.Substep+
                " civilian="+live?.CivilianRemoval+" unsupported="+live?.Unsupported);
            Require(live!=null && live.GlobalId==p.Actor.Global && live.CivilianRemoval && live.Unsupported==null,
                "Actual-native0 no longer has an admissible live civilian role/mode/recruitment state");
        }
        private uint[] CivilianRemovalGeometry()
        {
            var p=civilianRemoval;var t=GameTileManagerAPI.Instance;var values=new uint[24];int at=0;
            for(int yy=p.SourceY-p.Width/2;yy<=p.SourceY;yy++)for(int xx=p.SourceX-p.Width/2;xx<=p.SourceX;xx++)
            {
                int tile=runtime.Native.Tile(xx,yy);Require(tile>=0,"Civilian no-op footprint outside map");
                values[at++]=runtime.Native.Flags(tile);values[at++]=t.GetTileHeight(tile);values[at++]=(uint)t.GetTileType(tile);
                values[at++]=runtime.Native.Connections(tile);values[at++]=unchecked((uint)runtime.Native.Building(tile));values[at++]=unchecked((uint)t.GetTileVegetationId(tile));
            }
            return values;
        }
        private void RepeatCivilianRemovalNoop()
        {
            CivilianRemovalWorld();var p=civilianRemoval;var before=CivilianRemovalGeometry();long count=Plugin.GeneralObserver.RemovalCount;
            var tiles=GameTileManagerAPI.Instance;
            EditorBrush.Call(tiles.GetTileManager(),owner,tiles.GetTileId(p.SourceX,p.SourceY),p.SourceY,p.Width,
                unchecked((int)(uint)TilePropertyFlag.IsMoat),2,0);
            var after=CivilianRemovalGeometry();for(int i=0;i<before.Length;i++)Require(before[i]==after[i],"Repeated civilian writer changed settled geometry");
            Require(Plugin.GeneralObserver.RemovalCount==count,"No-op performed synchronous civilian removal");
        }
        private unsafe void CheckCivilianRemovalOutcome(bool final)
        {
            CivilianRemovalWorld();var p=civilianRemoval;
            Require(p.Fault==null,p.Fault);
            long delta=Plugin.GeneralObserver.RemovalCount-p.BeforeRemovals;
            if(!p.NextOnly || !expectedEnabled)Require(delta==(expectedEnabled && !p.ExternalMarkerWon?1:0),"Civilian removal action count differs: actual="+delta);
            if(p.NextOnly)
            {
                Require(p.BoundaryRecorded && p.FirstCurrentSafe && p.BoundaryOriginalAlive && p.BoundaryRemovals==0,
                    "Next-only lacks exact before/after first recovery boundary evidence");
                log("civilian-removal-next-after-boundary totalRemovals="+delta+" qualifiedBoundaryRemovals="+p.BoundaryRemovals+
                    " laterNaturalStepsOutsideNegativeClaim=true originalGoneLater="+p.OriginalGone);
            }
            else if(expectedEnabled && final)
                Require(p.OriginalGone,
                    "Life3 removal request did not reach actual original-life teardown/chain cleanup");
            if(p.OriginalGone)ValidateRemovedCivilianChains();
            if(final && expectedEnabled && !p.NextOnly && p.Farm>0)
            {
                ObserveCivilianFarmReplacement();Require(p.FarmSlotFreed && p.Replacement && p.ReplacementGlobal!=p.Actor.Global,
                    "Original Farm has no currently verified natural replacement employee lifetime");
            }
            if(final)log("civilian-removal-result unit="+p.Actor.Id+"/"+p.Actor.Global+" type="+p.Actor.Type+" enabled="+expectedEnabled+
                " nextOnly="+p.NextOnly+" externalMarkerWon="+p.ExternalMarkerWon+" removalRequests="+delta+" sawLife3="+p.SawLife3+" originalGone="+p.OriginalGone+
                " reused="+p.Reused+" observedTicks="+p.ObservedTicks+" farmSlotFreed="+p.FarmSlotFreed+" replacement="+p.ReplacementId+"/"+p.ReplacementGlobal+
                " replacementVerified="+p.Replacement+" noNativeContinuationRequired=true");
        }
        private IEnumerable<AutomationWait> CivilianRemovalCase(AutomationRun run)
        {
            bool worker=scenario.Contains("-worker-");
            bool aiOwner=scenario.StartsWith("rescue-removal-ai-worker-",StringComparison.Ordinal);int previousOwner=owner;
            civilianRemoval=new CivilianRemovalProof{NextOnly=scenario.Contains("-next-")};var p=civilianRemoval;
            Require(WorldMutationEvents.TryGetCurrentWorld(out p.Session,out p.World) && p.Session==session,"Civilian fixture has no initial native world");
            try
            {
                Execute(run,()=>{
                    if(aiOwner){SelectMountedAiOwner();Require(owner==deterministicAi,"Civilian fixture did not select the pinned actual AI player");}
                    Require(GamePlayerManagerAPI.Instance.IsAIPlayer(owner)==aiOwner,"Civilian fixture requested owner kind differs from native player state");
                    log("civilian-removal-owner localOwner="+previousOwner+" sourceOwner="+owner+" actualAI="+aiOwner+" originalNativeEmploymentRequired=true");
                    SetupCivilianRemoval(worker);
                });
                Director.instance.SetEngineFrameRate(250);
                if(worker)
                {
                    foreach(var w in run.SetPause(run.CurrentMission,false))yield return w;
                    yield return run.WaitForTicks(run.CurrentMission,1024);
                    foreach(var w in run.SetPause(run.CurrentMission,true))yield return w;
                }
                foreach(var w in AwaitCivilianPopulation(run))yield return w;
                if(!worker)run.InSimulation(run.CurrentMission,()=>{CaptureNaturalFreePeasants();return true;});
                for(int sample=0;sample<6000 && p.Before==null;sample++)
                {
                    run.InSimulation(run.CurrentMission,()=>{CivilianRemovalWorld();AcquireCivilianRemoval(worker);return true;});
                    if(p.Before!=null)break;
                    foreach(var w in run.SetPause(run.CurrentMission,false))yield return w;
                    yield return run.WaitForTicks(run.CurrentMission,2);
                    foreach(var w in run.SetPause(run.CurrentMission,true))yield return w;
                    if(sample%256==0)log("civilian-removal-acquire sample="+sample+" naturalWorker="+worker+" nextOnly="+p.NextOnly+" knownFree="+p.FreePeasants.Count);
                }
                Require(p.Before!=null,"No natural qualified civilian source acquired; no eligible natural identity/phase or controlled native source pose was acquired");
                run.InSimulation(run.CurrentMission,()=>{
                    ApplyCivilianRemovalMoat();
                    if(scenario=="rescue-removal-worker-replan-on")ReplanTrappedCivilianBeforeRecovery();
                    return true;
                });
                foreach(var w in run.SetPause(run.CurrentMission,false))yield return w;
                // Current-safe control ends at its first actual recovery boundary. Later natural steps are not reclassified as that control.
                yield return run.WaitForTicks(run.CurrentMission,p.NextOnly?1:64);
                foreach(var w in run.SetPause(run.CurrentMission,true))yield return w;
                if(worker && expectedEnabled && !p.NextOnly)foreach(var w in AwaitCivilianFarmReplacement(run))yield return w;
                run.InSimulation(run.CurrentMission,()=>{CheckCivilianRemovalOutcome(true);return true;});
                if(!p.NextOnly)
                {
                    run.InSimulation(run.CurrentMission,()=>{RepeatCivilianRemovalNoop();return true;});
                    foreach(var w in run.SetPause(run.CurrentMission,false))yield return w;
                    yield return run.WaitForTicks(run.CurrentMission,8);
                    foreach(var w in run.SetPause(run.CurrentMission,true))yield return w;
                    run.InSimulation(run.CurrentMission,()=>{CheckCivilianRemovalOutcome(true);log("civilian-removal-noop advancedTicks=8 additionalRemovals=0");return true;});
                }
                run.InSimulation(run.CurrentMission,()=>{CivilianRemovalWorld();ValidateCivilianMigrationLayout();Plugin.SetGeneralFixtureSession(false);Director.instance.SetEngineFrameRate(250);return true;});
            }
            finally
            {
                p.Active=false;p.PeasantCreated?.Dispose();p.PeasantCreated=null;if(ReferenceEquals(civilianRemovalFixture,this))civilianRemovalFixture=null;
                // Local observations only: SDK can Dispose this iterator after Finish invalidated its run.
                appleWorld?.Dispose();owner=previousOwner;
            }
        }
    }
}
