using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using APIShared;
using APIShared.Automation;
using APIShared.Simulation;
using APIShared.Units;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace TrappedUnitRescueTest
{
    internal sealed partial class Fixture
    {
        private sealed class PerformanceGroup
        {
            internal int Tribe,X,Y,LastX,LastY;
            internal Actor Sample;
            internal bool Progress;
        }
        private sealed class PerformanceProof
        {
            internal Fixture Fixture;
            internal string Path,Fault,RunId;
            internal long PreviousSession,LoadedSession;
            internal bool Active,Armed,Ended,Enabled,Walking,ObserveArmyMoves;
            internal int Loaded,WindowStart,LastCommandTick,Commands,OwnArmyMoves,Population,AmbientReserve,PhysicalCapacity;
            internal readonly List<Actor> IdleActors=new List<Actor>();
            internal readonly List<int[]> KeepAreas=new List<int[]>();
            internal readonly Dictionary<int,uint> TribeGlobals=new Dictionary<int,uint>();
            internal readonly HashSet<int> PlannedAivTiles=new HashSet<int>();
            internal readonly HashSet<int> ActorIds=new HashSet<int>();
            internal readonly List<Actor> Actors=new List<Actor>();
            internal readonly List<PerformanceGroup> Groups=new List<PerformanceGroup>();
        }
        private static volatile PerformanceProof performanceProof;
        private static bool performanceLifecycleRegistered,performanceTickRegistered,performanceMoveRegistered;

        // A single unbound iterator owns all four ordinary save/load transitions.
        private IEnumerable<AutomationStep> PerformanceSteps(int population=1000,int warmupTicks=256,int windowTicks=3600,int speed=40,bool observeArmyMoves=true)
        {
            Require(population==1000 || population==4000 || population==9871,"Unselected performance tier");
            Require(population==1000 || !observeArmyMoves,"Higher tiers require the explicit passive profile");
            Require(warmupTicks>=128 && windowTicks>=3600 && (speed==40 || speed==250),"Invalid performance profile");
            yield return new AutomationStep("performance-"+population+"-idle-1000walking-identical-save-abab",TimeSpan.FromMinutes(45),
                run=>Performance(run,population,warmupTicks,windowTicks,speed,observeArmyMoves),false);
        }
        private void ArmPerformance(PerformanceProof proof)
        {
            if(!performanceLifecycleRegistered)
            {
                Require(ApiShared.ForMod(Plugin.Guid).TryGetMissionLifecycle(out var missions,out var available),"Performance lifecycle unavailable: "+available.Reason);
                Require(missions.TryRegisterObserver("acceptance-performance",PerformanceStarted,PerformanceEnded,null,out var diagnostic),
                    "Performance lifecycle observer rejected: "+diagnostic.Reason);
                performanceLifecycleRegistered=true;
            }
            if(!performanceTickRegistered)
            {
                Require(SimulationTickEvents.TryRegister(Plugin.Guid,"acceptance-performance-walking",PerformanceTick,out string reason,order:100),
                    "Performance walking scheduler rejected: "+reason);
                performanceTickRegistered=true;
            }
            if(proof.ObserveArmyMoves && !performanceMoveRegistered)
            {
                Require(UnitMoveEvents.TryRegister(Plugin.Guid,"acceptance-performance-army",new PerformanceArmyMoveObserver(),out string reason,
                    enabled:()=>performanceProof!=null && performanceProof.Active && performanceProof.ObserveArmyMoves),"Performance army move observer rejected: "+reason);
                performanceMoveRegistered=true;
            }
            Require(performanceProof==null,"Another performance owner is active");performanceProof=proof;
        }
        private sealed class PerformanceArmyMoveObserver : IUnitMoveObserver
        {
            public IUnitMoveInvocation TryBegin(UnitMoveContext context)
            {
                var proof=performanceProof;
                if(proof!=null && proof.Active && proof.ObserveArmyMoves && context!=null && context.SessionId==proof.Fixture.session &&
                    context.OriginOwnerGuid==Plugin.Guid && proof.ActorIds.Contains(context.UnitId))
                    Interlocked.Increment(ref proof.OwnArmyMoves);
                return null;
            }
        }
        private static void PerformanceEnded(MissionLifecycleNotification notification)
        {
            var proof=performanceProof;
            if(proof!=null && proof.Active && notification.Context.SessionId==proof.PreviousSession)proof.Ended=true;
        }
        private static void PerformanceStarted(MissionLifecycleNotification notification)
        {
            var proof=performanceProof;var context=notification.Context;
            if(proof==null || !proof.Active || !proof.Armed || notification.IsReplay || !context.IsSave ||
                context.SessionId==proof.PreviousSession || string.IsNullOrEmpty(context.FilePath))return;
            if(!string.Equals(Path.GetFullPath(context.FilePath),proof.Path,StringComparison.OrdinalIgnoreCase))return;
            try
            {
                proof.Armed=false;Require(proof.Ended,"Performance save loaded before retiring its preceding mission");
                proof.Fixture.session=context.SessionId;proof.Fixture.stopped=false;proof.LoadedSession=context.SessionId;
                Plugin.SetGeneralFixtureSession(proof.Enabled);
                Require(Plugin.GeneralObserver.IsActive==proof.Enabled,"Performance current-settings activation was not restored on start");
            }
            catch(Exception error){proof.Fault=error.ToString();}
            finally {Volatile.Write(ref proof.Loaded,1);}
        }
        private static void PerformanceTick(int tick)
        {
            var proof=performanceProof;
            if(proof==null || !proof.Active || !proof.Walking || proof.Fault!=null)return;
            try
            {
                Require(!proof.Fixture.stopped && proof.Fixture.session==proof.LoadedSession,"Performance walking owner changed");
                if(unchecked((uint)(tick-proof.LastCommandTick))<128)return;
                proof.LastCommandTick=tick;
                proof.Fixture.PerformanceCommands(proof,((unchecked((uint)(tick-proof.WindowStart))/128)&1)==0);
            }
            catch(Exception error){proof.Fault=error.ToString();proof.Walking=false;}
        }
        private unsafe void PerformanceCommands(PerformanceProof proof,bool north)
        {
            foreach(var group in proof.Groups)
            {
                var sample=Resolve(group.Sample);
                if(sample->r_CurrentTilePositionX!=group.LastX || sample->r_CurrentTilePositionY!=group.LastY)group.Progress=true;
                group.LastX=sample->r_CurrentTilePositionX;group.LastY=sample->r_CurrentTilePositionY;
                Require(GameTribeManagerAPI.Instance.IssueMoveHereCommand(group.Tribe,group.X,group.Y+(north?-16:16)),
                    "Performance ordinary shared-tribe command refused");
                Require(sample->r_TargetTilePositionX2!=sample->r_CurrentTilePositionX || sample->r_TargetTilePositionY2!=sample->r_CurrentTilePositionY,
                    "Performance command published no nontrivial sample goal");
                Require(sample->r_AIState==101 && (sample->r_PathPlanStateBitFlags==2 || sample->r_PathPlanStateBitFlags==4) &&
                    Math.Abs(sample->r_TargetTilePositionX2-group.X)<=6 && Math.Abs(sample->r_TargetTilePositionY2-(group.Y+(north?-16:16)))<=6,
                    "Performance shared-tribe move did not publish a qualified ordinary sample route");
                proof.Commands++;
            }
        }
        private unsafe int PerformanceCensus()
        {
            var span=GameUnitManagerAPI.Instance.GetUnitsAsSpan();int count=0;
            for(int i=0;i<span.Length;i++)if(span[i].r_AliveState!=AliveState.None || span[i].r_GlobalId!=0)count++;
            return count;
        }
        private unsafe void PerformanceSpawn(PerformanceProof proof,int population)
        {
            Require(GameTileManagerAPI.Instance.GetCurrentMapSize()==800,"Performance fixture requires an actual 800-coordinate map");
            Plugin.SetGeneralFixtureSession(false);
            int occupied=PerformanceCensus(),capacity=GameUnitManagerAPI.Instance.GetUnitsAsSpan().Length;
            Require(occupied+population+64<=capacity,"Performance population leaves insufficient physical unit-pool headroom");
            proof.Population=population;proof.PhysicalCapacity=capacity;
            // Observed ambient peak60 +64 free slots rounds to128. A larger
            // actual census increases this reservation instead of shrinking the tier.
            proof.AmbientReserve=population==1000?occupied+64:Math.Max(128,checked(((occupied+64+63)/64)*64));
            Require(population+proof.AmbientReserve<=capacity,"Requested complete tier exceeds physical capacity plus actual ambient reserve");
            if(population>1000)PerformanceExtreme(proof,"before-spawn");
            var tiles=GameTileManagerAPI.Instance;int remaining=1000;
            // Disjoint 28x52 lanes. All geometry enumeration is setup-only.
            for(int y=28;y<800-28 && remaining>0;y+=56)
                for(int x=16;x<800-16 && remaining>0;x+=32)
                {
                    int height=-1;bool usable=true;
                    for(int yy=y-25;yy<=y+25 && usable;yy++)for(int xx=x-13;xx<=x+13;xx++)
                    {
                        int tile=runtime.Native.Tile(xx,yy);
                        if(tile<0 || !runtime.Native.OrdinaryGround(tile) || !tiles.IsTileWalkableAndUnoccupied(tile) ||
                            (tiles.GetTilePropertyFlag(tile)&TilePropertyFlag.IsTree)!=0){usable=false;break;}
                        int h=tiles.GetTileHeight(tile);if(height>=0 && height!=h){usable=false;break;}height=h;
                        // Every internal cardinal edge is explicitly connected.
                        byte links=runtime.Native.Connections(tile);
                        if((xx<x+13 && (links&(1<<2))==0) || (yy<y+25 && (links&(1<<4))==0)){usable=false;break;}
                    }
                    if(!usable)continue;
                    int tribe=checked((int)GameTribeManagerAPI.Instance.Create(owner));Require(tribe>0,"Performance shared tribe exhausted");
                    var group=new PerformanceGroup{Tribe=tribe,X=x,Y=y};int count=Math.Min(32,remaining);
                    for(int i=0;i<count;i++)
                    {
                        int sx=x-7+(i%8)*2,sy=y-3+(i/8)*2;
                        int id=checked((int)GameUnitManagerAPI.Instance.CreateUnitLocal(owner,owner,sx,sy,height,eChimps.CHIMP_TYPE_SPEARMAN));
                        GameUnit* unit=null;
                        Require(id>0 && UnitAccess.TryGetById(id,out unit,out _) && unit!=null && unit->r_GlobalId!=0 &&
                            unit->r_UnitChimp==eChimps.CHIMP_TYPE_SPEARMAN && unit->r_CurrentTilePositionX==sx && unit->r_CurrentTilePositionY==sy,
                            "Performance spawn refused/projected or changed type");
                        Require(GameTribeManagerAPI.Instance.AssignUnit(tribe,id) && unit->r_TribeId==tribe,"Performance native shared-tribe assignment failed");
                        var actor=new Actor{Id=id,Global=unit->r_GlobalId,Type=eChimps.CHIMP_TYPE_SPEARMAN,Tribe=tribe,X=sx,Y=sy,SourceX=sx,SourceY=sy};
                        proof.Actors.Add(actor);Require(proof.ActorIds.Add(id),"Performance native spawn reused a fixture ID");if(i==0)group.Sample=actor;
                    }
                    group.LastX=group.Sample.X;group.LastY=group.Sample.Y;proof.Groups.Add(group);remaining-=count;
                }
            Require(remaining==0,"Insufficient genuinely clear connected flat walking lanes for the complete1000 walking cohort; no partial tier accepted");
            if(population>1000)PerformanceSpawnIdle(proof,population-1000);
            Require(proof.Actors.Count==population && PerformanceCensus()==occupied+population,"Partial tier or unexpected native spawn census");
            log("performance-population run="+proof.RunId+" requested="+population+" physicalBefore="+occupied+" physicalAfter="+PerformanceCensus()+
                " physicalCapacity="+capacity+" ambientReservation="+proof.AmbientReserve+" freeMarginRequired=64 walkingPopulation=1000 groups="+proof.Groups.Count+" actors="+proof.Actors.Count);
        }
        private bool IsHigherPerformanceTier()=>scenario=="rescue-perf-4000-idle1000walk-passive-on" || scenario=="rescue-perf-9871-idle1000walk-passive-on";
        private void ConfigurePerformanceLobby(EngineInterface.MultiplayerSetupData setup)
        {
            if(!IsHigherPerformanceTier())return;
            Require(setup!=null,"Performance Extreme lobby setup unavailable");
            setup.extreme_troops=1;
            Require(setup.extreme_troops==1,"Performance Extreme lobby setting rejected");
            log("performance-extreme-lobby scenario="+scenario+" extremeTroops=1 beforeLaunch=true");
        }
        private void PerformanceExtreme(PerformanceProof proof,string boundary)
        {
            var setup=EngineInterface.getMultiplayerStartingData();
            Require(setup!=null && setup.extreme_troops==1,"Higher performance tier requires actual native Extreme mode: "+boundary);
            log("performance-extreme run="+proof.RunId+" scenario="+scenario+" boundary="+boundary+" extremeTroops=1 publicReadback=true");
        }
        private unsafe void PerformanceCaptureIdleExclusions(PerformanceProof proof)
        {
            // Resolve concrete live Keep footprints once. The16-tile spacing is
            // only fixture policy; it is NOT a bound on any future AI building.
            foreach(int player in new[]{deterministicHuman,deterministicAi})
            {
                int id=GamePlayerManagerAPI.Instance.GetPlayerKeepId(player);
                GameBuilding* keep=null;
                Require(id>0 && BuildingAccess.TryGetById(id,out keep,out _) && keep!=null,"Performance Keep exclusion unavailable");
                int left=keep->r_TilePositionXBegin,top=keep->r_TilePositionYBegin;
                int size=checked((int)keep->r_OccupyTileGridSize);
                Require(size>0 && size<=32,"Performance Keep footprint invalid");
                proof.KeepAreas.Add(new[]{left-16,top-16,left+size+16,top+size+16});
            }
            var aiv=GameAIVManagerAPI.Instance;
            Require(aiv.TryGetVillageSlotByPlayerId(deterministicAi,out int slot) &&
                aiv.TryGetVillageBySlot(slot,out var village) && village->OwnerPlayerId==deterministicAi,"Performance actual AI village unavailable");
            var steps=aiv.GetBuildSteps(slot);Require(steps.Length==AivVillageState.BUILD_STEP_CAPACITY,"Performance actual AI build-step view invalid");
            int reads=0;
            for(int i=0;i<steps.Length;i++)
            {
                if(steps[i].State==AivBuildStepState.Inactive)continue;
                var fields=aiv.GetBuildStepMapTiles(slot,i);
                Require(fields.Length>0,"Performance active AI build-step has no auditable target tiles");
                for(int j=0;j<fields.Length;j++)
                {
                    Require(++reads<=20000 && fields[j]>=0 && fields[j]<NativeRelocation.TileCapacity,"Performance AI planned-target budget/identity invalid");
                    proof.PlannedAivTiles.Add(fields[j]);
                }
            }
            log("performance-idle-exclusions keeps="+proof.KeepAreas.Count+" keepSpacingPolicy=16 plannedAivTargets="+proof.PlannedAivTiles.Count+
                " futureConstructionGuarantee=false boundaryOrdinaryGroundRequired=true");
        }
        private unsafe void PerformanceSpawnIdle(PerformanceProof proof,int additional)
        {
            PerformanceCaptureIdleExclusions(proof);
            var tiles=GameTileManagerAPI.Instance;int tribe=0,members=0,remaining=additional;
            // Setup-only bounded map raster; no commands and no permanent census.
            for(int y=16;y<784 && remaining>0;y+=2)for(int x=16;x<784 && remaining>0;x+=2)
            {
                bool usable=true;
                foreach(var area in proof.KeepAreas)
                    if(x>=area[0] && x<=area[2] && y>=area[1] && y<=area[3]){usable=false;break;}
                if(!usable)continue;
                foreach(var lane in proof.Groups)
                    if(x>=lane.X-15 && x<=lane.X+15 && y>=lane.Y-27 && y<=lane.Y+27){usable=false;break;}
                if(!usable)continue;
                int height=-1;
                for(int dy=-1;dy<=1 && usable;dy++)for(int dx=-1;dx<=1;dx++)
                {
                    int tile=runtime.Native.Tile(x+dx,y+dy);
                    if(tile<0 || proof.PlannedAivTiles.Contains(tile) || !runtime.Native.OrdinaryGround(tile) || !tiles.IsTileWalkableAndUnoccupied(tile) ||
                        tiles.GetTileBuildingId(tile)!=0 || (tiles.GetTilePropertyFlag(tile)&TilePropertyFlag.IsTree)!=0){usable=false;break;}
                    int h=tiles.GetTileHeight(tile);if(height>=0 && h!=height){usable=false;break;}height=h;
                }
                if(!usable)continue;
                if(members==0){tribe=checked((int)GameTribeManagerAPI.Instance.Create(owner));Require(tribe>0,"Performance idle shared tribe exhausted");}
                int id=checked((int)GameUnitManagerAPI.Instance.CreateUnitLocal(owner,owner,x,y,height,eChimps.CHIMP_TYPE_SPEARMAN));
                GameUnit* unit=null;
                Require(id>0 && UnitAccess.TryGetById(id,out unit,out _) && unit!=null && unit->r_GlobalId!=0 &&
                    unit->r_UnitChimp==eChimps.CHIMP_TYPE_SPEARMAN && unit->r_CurrentTilePositionX==x && unit->r_CurrentTilePositionY==y,
                    "Performance idle spawn refused/projected/type changed; partial tier rejected");
                Require(GameTribeManagerAPI.Instance.AssignUnit(tribe,id) && unit->r_TribeId==tribe,"Performance idle native tribe assignment failed");
                var actor=new Actor{Id=id,Global=unit->r_GlobalId,Type=eChimps.CHIMP_TYPE_SPEARMAN,Tribe=tribe,X=x,Y=y,SourceX=x,SourceY=y};
                Require(proof.ActorIds.Add(id),"Performance idle native spawn reused an ID");proof.Actors.Add(actor);proof.IdleActors.Add(actor);
                remaining--;members=(members+1)%32;
            }
            Require(remaining==0,"Insufficient genuinely free flat compact idle area; complete requested tier rejected");
        }
        private unsafe ulong PerformanceBoundary(PerformanceProof proof,bool remember)
        {
            Require(proof.Actors.Count==proof.Population && proof.ActorIds.Count==proof.Population,"Performance incomplete full-population boundary");
            if(proof.Population>1000)
            {
                Require(PerformanceCensus()+64<=proof.PhysicalCapacity,"Performance actual ambient growth exhausted the reserved free margin");
                foreach(var actor in proof.IdleActors)PerformanceAtSavedSource(actor);
            }
            ulong hash=14695981039346656037UL;
            foreach(var actor in proof.Actors)
            {
                var unit=Resolve(actor);Require(UnitAccess.IsReallyAlive(unit) && unit->r_CurrentHealth>0 && unit->r_TribeId==actor.Tribe,
                    "Performance actor lifetime/type/tribe changed");
                if(remember)
                {
                    Require(unit->r_PathPlanStateBitFlags==0 && unit->r_MovementSubstep==8 && unit->r_CurrentTilePositionX==unit->r_NextTilePositionX2 &&
                        unit->r_CurrentTilePositionY==unit->r_NextTilePositionY2,"Performance common save requires genuinely initialized idle actors");
                    actor.Health=unit->r_CurrentHealth;
                }
                if(proof.Population>1000)
                {
                    if(remember && !proof.TribeGlobals.ContainsKey(actor.Tribe))
                    {
                        Require(runtime.Native.TryTribeGlobal(actor.Tribe,out uint initialGlobal) && initialGlobal!=0,"Performance tribe lifetime unavailable");
                        proof.TribeGlobals.Add(actor.Tribe,initialGlobal);
                    }
                    Require(proof.TribeGlobals.TryGetValue(actor.Tribe,out uint savedGlobal) &&
                        runtime.Native.TryTribeGlobal(actor.Tribe,out uint liveGlobal) && savedGlobal==liveGlobal &&
                        runtime.Native.JobDword(unit,0x93C)==savedGlobal,"Performance tribe lifetime or actor's native tribe binding changed");
                }
                Require(unit->r_CurrentHealth==actor.Health,"Performance actor health changed");
                CheckActor(actor,true);
                Require(runtime.Native.OrdinaryGround((int)unit->r_CurrentPositionTileId),
                    "Performance army is no longer on ordinary ground: unit="+actor.Id+"/"+actor.Global+" tile="+unit->r_CurrentPositionTileId);
                unchecked {hash=(hash^(uint)actor.Id)*1099511628211UL;hash=(hash^actor.Global)*1099511628211UL;hash=(hash^(uint)actor.Tribe)*1099511628211UL;hash=(hash^actor.Health)*1099511628211UL;}
            }
            if(proof.ObserveArmyMoves)Require(proof.OwnArmyMoves==0,"Ambient world caused an own recovery call on the performance army; this is not an unmutated army sample");
            return hash;
        }
        private unsafe void PerformanceAtSavedSources(PerformanceProof proof)
        {
            foreach(var actor in proof.Actors)PerformanceAtSavedSource(actor);
        }
        private unsafe void PerformanceAtSavedSource(Actor actor)
        {
            var unit=Resolve(actor);
            Require(unit->r_CurrentTilePositionX==actor.SourceX && unit->r_CurrentTilePositionY==actor.SourceY &&
                unit->r_NextTilePositionX2==actor.SourceX && unit->r_NextTilePositionY2==actor.SourceY &&
                unit->r_PathPlanStateBitFlags==0 && unit->r_MovementSubstep==8,
                "Performance common-save/idle actor changed its immutable saved source pose");
            int sourceTile=runtime.Native.Tile(actor.SourceX,actor.SourceY);
            Require(sourceTile>=0 && runtime.Native.OrdinaryGround(sourceTile),
                "Performance saved idle source is no longer ordinary ground: unit="+actor.Id+"/"+actor.Global+" tile="+sourceTile);
        }

        private void PerformanceManifest(PerformanceProof proof,ulong identity)
        {
            // The same initialized lifetimes are restored from the one ordinary
            // save in all four loads. Emit only at setup, never in timing windows.
            Require(proof.Actors.Count==proof.Population && proof.ActorIds.Count==proof.Population,"Performance manifest requires the complete requested initialized tier");
            int batches=(proof.Actors.Count+31)/32;
            log("performance-army-begin schema=1 run="+proof.RunId+" population="+proof.Actors.Count+" batches="+batches+
                " scenario="+scenario+" walkingPopulation=1000 workloadModel=full-idle-1000-walking observeArmyMoves="+(proof.ObserveArmyMoves?"true":"false"));
            for(int batch=0;batch<batches;batch++)
            {
                int first=batch*32,count=Math.Min(32,proof.Actors.Count-first);var units=new string[count];
                for(int i=0;i<count;i++)
                {
                    var actor=proof.Actors[first+i];
                    Require(actor.Id>=1 && actor.Id<=NativeRelocation.MaximumUnitId && actor.Global!=0,"Invalid performance manifest lifetime");
                    units[i]=actor.Id.ToString(CultureInfo.InvariantCulture)+"/"+actor.Global.ToString(CultureInfo.InvariantCulture);
                }
                log("performance-army-manifest schema=1 run="+proof.RunId+" batch="+batch+" batches="+batches+" count="+count+
                    " units="+string.Join(",",units));
            }
            log("performance-army-manifest-end schema=1 run="+proof.RunId+" count="+proof.Actors.Count+" batches="+batches+
                " identity="+identity.ToString(CultureInfo.InvariantCulture));
        }
        private static string PerformanceNumber(float value)=>value.ToString("R",CultureInfo.InvariantCulture);
        private static string PerformanceCameraFacts(int centreX,int centreY,float x,float y,float zoom)=>
            "expectedCentreX="+centreX+" expectedCentreY="+centreY+" expectedCameraX="+PerformanceNumber(x)+
            " expectedCameraY="+PerformanceNumber(y)+" expectedZoom="+PerformanceNumber(zoom);
        private void RequirePerformanceCamera(string boundary,int centreX,int centreY,float x,float y,float zoom)
        {
            int actualX=-1,actualY=-1;GameMap.instance.getRotationCentre(ref actualX,ref actualY);
            float cameraX=CameraControls2D.instance.getCameraXPos(),cameraY=CameraControls2D.instance.getCameraYPos();
            float actualZoom=PerfectPixelWithZoom.instance.getZoom();
            bool rotationPending=GameMap.instance.pendingRotationtriggered,zoomPending=PerfectPixelWithZoom.instance.midZoom;
            if(centreX>=0 && centreY>=0 && actualX==centreX && actualY==centreY && cameraX==x && cameraY==y &&
                actualZoom==zoom && !rotationPending && !zoomPending)return;
            string facts="boundary="+boundary+" "+PerformanceCameraFacts(centreX,centreY,x,y,zoom)+
                " actualCentreX="+actualX+" actualCentreY="+actualY+" actualCameraX="+PerformanceNumber(cameraX)+
                " actualCameraY="+PerformanceNumber(cameraY)+" actualZoom="+PerformanceNumber(actualZoom)+
                " rotationPending="+rotationPending+" zoomPending="+zoomPending;
            log("performance-camera-mismatch "+facts);
            Require(false,"Performance settled measurement camera mismatch: "+facts);
        }
        private static string PerformanceHash(string path)
        {
            using(var stream=File.OpenRead(path))using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","");
        }
        private IEnumerable<AutomationWait> Performance(AutomationRun run,int population,int warmupTicks,int windowTicks,int speed,bool observeArmyMoves)
        {
            Require(Path.IsPathRooted(map) && File.Exists(map),"Performance requires the full existing built-in map path for provenance hashing");
            var probe=new PerfProbeReflection();probe.Validate();
            var proof=new PerformanceProof{Fixture=this,Path=PersistenceSavePath(run),Active=true,PreviousSession=session,ObserveArmyMoves=observeArmyMoves};
            string saveName=Path.GetFileNameWithoutExtension(proof.Path);const string savePrefix="SerpAcceptance-";Guid actualRun=Guid.Empty;
            Require(saveName.StartsWith(savePrefix,StringComparison.Ordinal) &&
                Guid.TryParseExact(saveName.Substring(savePrefix.Length),"N",out actualRun) && actualRun!=Guid.Empty,
                "Performance owned save does not carry its actual AutomationRun ID");
            proof.RunId=actualRun.ToString("N");
            ArmPerformance(proof);string activeLabel=null;ulong identity=0;string saveHash=null;float zoom=PerfectPixelWithZoom.instance.getZoom();
            int referenceCentreX=-1,referenceCentreY=-1;float referenceCameraX=0,referenceCameraY=0,referenceZoom=0;
            try
            {
                run.InSimulation(run.CurrentMission,()=>{PerformanceSpawn(proof,population);return true;});
                foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                yield return run.WaitForTicks(run.CurrentMission,32);
                foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                identity=run.InSimulation(run.CurrentMission,()=>{
                    Require(Plugin.GeneralObserver.PendingCount==0 && !Plugin.GeneralObserver.HasEvidence,"Performance common save must have an empty causal archive");
                    return PerformanceBoundary(proof,true);
                });
                PerformanceManifest(proof,identity);
                yield return AutomationWait.Until("performance common native save safe",()=>Director.instance.SafeToSave());Director.instance.FinishedSaving();
                // Match EditorDirector.SaveSaveGameOrMap exactly: these are screen
                // dimensions in native tile units, not an arbitrary1x1 viewport.
                int saveTilesWide=(int)((float)(UnityEngine.Screen.width/64)/PerfectPixelWithZoom.instance.getZoom());
                int saveTilesHigh=(int)((float)(UnityEngine.Screen.height/16)/PerfectPixelWithZoom.instance.getZoom());
                var screen=CameraControls2D.instance.getScreenXYForSaveCentring(saveTilesWide,saveTilesHigh);int centreX=-1,centreY=-1;
                GameMap.instance.getRotationCentre(ref centreX,ref centreY);
                Require(EngineInterface.SaveSaveGame(proof.Path,screen.x,screen.y,centreX,centreY,false,false,false),"Performance common native save refused");
                Require(File.Exists(proof.Path) && new FileInfo(proof.Path).Length>0,"Performance save absent");saveHash=PerformanceHash(proof.Path);
                log("performance-common-save path="+proof.Path+" sha256="+saveHash+" identity="+identity+" map="+Path.GetFullPath(map)+
                    " mapSha256="+PerformanceHash(map)+" zoom="+PerformanceNumber(zoom)+" centre="+centreX+","+centreY+" screen="+screen.x+","+screen.y+" saveTileViewport="+saveTilesWide+","+saveTilesHigh+" speed="+speed);
                for(int iteration=0;iteration<4;iteration++)
                {
                    proof.Walking=false;proof.PreviousSession=session;proof.Ended=false;
                    foreach(var wait in PersistenceQuitToMenu(run))yield return wait;
                    proof.Enabled=(iteration&1)!=0;enable(proof.Enabled);proof.Armed=true;proof.Loaded=0;proof.Fault=null;
                    Require(PerformanceHash(proof.Path)==saveHash,"Common ABAB save changed");
                    SavegameModSettings.PrepareLoad(proof.Path,true);
                    foreach(var wait in run.LoadSave(proof.Path))yield return wait;
                    yield return run.WaitForPlayState(run.CurrentMission);
                    if(MainViewModel.Instance.Show_HUD_Briefing)MainViewModel.Instance.ButtonBriefingResumeCommand.Execute(null);
                    yield return AutomationWait.Until("performance owned-load briefing closed",()=>!MainViewModel.Instance.Show_HUD_Briefing);
                    // A fresh PlayState may precede the requested rotation frame.
                    // processTestMap clears this flag only after finishPendingRotate
                    // and applies its PlayState camera before publishing lastGameState.
                    yield return AutomationWait.Until("performance loaded rotation and zoom applied",()=>
                        !GameMap.instance.pendingRotationtriggered && !PerfectPixelWithZoom.instance.midZoom);
                    foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                    Require(Volatile.Read(ref proof.Loaded)!=0 && proof.Fault==null && session==proof.LoadedSession,"Performance owned reload failed: "+proof.Fault);
                    Director.instance.SetEngineFrameRate(speed);
                    for(int attempt=0;attempt<32 && Math.Abs(PerfectPixelWithZoom.instance.getZoom()-zoom)>0.001f;attempt++)
                        foreach(var wait in run.ZoomStep(run.CurrentMission,PerfectPixelWithZoom.instance.getZoom()<zoom,actual=>{}))yield return wait;
                    Require(Math.Abs(PerfectPixelWithZoom.instance.getZoom()-zoom)<=0.001f,"Performance zoom could not be restored exactly");
                    int loadedX=-1,loadedY=-1;GameMap.instance.getRotationCentre(ref loadedX,ref loadedY);
                    float loadedCameraX=CameraControls2D.instance.getCameraXPos(),loadedCameraY=CameraControls2D.instance.getCameraYPos();
                    float loadedZoom=PerfectPixelWithZoom.instance.getZoom();
                    Require(loadedX>=0 && loadedY>=0,"Performance loaded camera has no actual map centre");
                    // This is load diagnostics, not the settled measurement camera.
                    // Unity Update may still apply its ordinary bounds clamp.
                    log("performance-loaded-camera iteration="+iteration+" savedRealCentreX="+centreX+" savedRealCentreY="+centreY+
                        " loadedCentreX="+loadedX+" loadedCentreY="+loadedY+" cameraX="+PerformanceNumber(loadedCameraX)+
                        " cameraY="+PerformanceNumber(loadedCameraY)+" zoom="+PerformanceNumber(loadedZoom)+" rotationApplied=true referencePendingWarmup=true");
                    run.InSimulation(run.CurrentMission,()=>{
                        Require(GameTileManagerAPI.Instance.GetCurrentMapSize()==800 && Plugin.GeneralObserver.IsActive==proof.Enabled,"Performance loaded map/settings mismatch");
                        if(population>1000)PerformanceExtreme(proof,"load-"+iteration);
                        Require(PerformanceBoundary(proof,false)==identity,"Performance same-save identity checksum differs");
                        PerformanceAtSavedSources(proof);probe.Validate();return true;
                    });
                    foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                    yield return run.WaitForTicks(run.CurrentMission,warmupTicks);
                    foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                    // The excluded real warmup includes normal post-load camera
                    // clamping; only its settled boundary defines the A/B camera.
                    int settledX=-1,settledY=-1;GameMap.instance.getRotationCentre(ref settledX,ref settledY);
                    if(iteration==0)
                    {
                        referenceCentreX=settledX;referenceCentreY=settledY;
                        referenceCameraX=CameraControls2D.instance.getCameraXPos();referenceCameraY=CameraControls2D.instance.getCameraYPos();
                        referenceZoom=PerfectPixelWithZoom.instance.getZoom();
                    }
                    RequirePerformanceCamera("post-warmup-iteration-"+iteration,referenceCentreX,referenceCentreY,
                        referenceCameraX,referenceCameraY,referenceZoom);
                    log("performance-measurement-camera iteration="+iteration+" reference=A0-post-warmup exactPairMatch=true "+
                        PerformanceCameraFacts(referenceCentreX,referenceCentreY,referenceCameraX,referenceCameraY,referenceZoom));
                    for(int workload=0;workload<2;workload++)
                    {
                        if(workload==1)
                        {
                            run.InSimulation(run.CurrentMission,()=>{
                                Require(SimulationTickEvents.TryGetCurrentTick(out proof.WindowStart),"Performance walking warmup clock unavailable");
                                proof.LastCommandTick=proof.WindowStart;PerformanceCommands(proof,true);proof.Walking=true;return true;
                            });
                            foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                            yield return run.WaitForTicks(run.CurrentMission,warmupTicks);
                            foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                            run.InSimulation(run.CurrentMission,()=>{proof.Walking=false;Require(proof.Fault==null,"Performance walking warmup failed: "+proof.Fault);return true;});
                        }
                        int start=0,end=0,physical=0;long captures=0,attempts=0,rescues=0;
#if SERP_DEVDIAG
                        RescueCaptureDiagnostics.Sample windowAllocation=default;
#endif
                        string label="rescue-p"+population+"-"+iteration+"-"+(workload==0?"idle":"walk");
                        run.InSimulation(run.CurrentMission,()=>{
                            PerformanceBoundary(proof,false);if(workload==0)PerformanceAtSavedSources(proof);physical=PerformanceCensus();
                            RequirePerformanceCamera("before-window-"+label,referenceCentreX,referenceCentreY,referenceCameraX,referenceCameraY,referenceZoom);
                            var observer=Plugin.GeneralObserver;captures=observer.CaptureCount;attempts=observer.AttemptCount;rescues=observer.RescueCount;
                            Require(SimulationTickEvents.TryGetCurrentTick(out start),"Performance native clock unavailable");
                            proof.WindowStart=start;proof.LastCommandTick=start;proof.Commands=0;
                            foreach(var group in proof.Groups){group.Progress=false;group.LastX=group.Sample.X;group.LastY=group.Sample.Y;}
                            if(workload==1)PerformanceCommands(proof,true);
                            probe.Validate();
#if SERP_DEVDIAG
                            windowAllocation=RescueCaptureDiagnostics.Read();
#endif
                            Require(probe.Begin(label),"PerfProbe rejected owned timing window");activeLabel=label;proof.Walking=workload==1;
#if SERP_DEVDIAG
                            log("performance-window-open schema=1 run="+proof.RunId+" iteration="+iteration+" workload="+(workload==0?"idle":"walking")+
                                " enabled="+proof.Enabled+" label="+label+" actualTick="+start+" requestedPopulation="+population+
                                " monotonicStamp="+System.Diagnostics.Stopwatch.GetTimestamp()+" boundaryThread="+Thread.CurrentThread.ManagedThreadId+
                                " pausedEngineBoundary=true");
#endif
                            return true;
                        });
                        foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                        yield return run.WaitForTicks(run.CurrentMission,windowTicks);
                        foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                        string summary=run.InSimulation(run.CurrentMission,()=>{
                            proof.Walking=false;Require(SimulationTickEvents.TryGetCurrentTick(out end),"Performance ending native clock unavailable");
                            string result=probe.End(label);activeLabel=null;Require(result!=null,"PerfProbe lost the owned timing window");
#if SERP_DEVDIAG
                            var windowEnd=RescueCaptureDiagnostics.Read();
                            log("performance-window-close schema=1 run="+proof.RunId+" iteration="+iteration+" workload="+(workload==0?"idle":"walking")+
                                " enabled="+proof.Enabled+" label="+label+" actualTick="+end+" requestedPopulation="+population+
                                " actualAdvancingTicks="+unchecked((uint)(end-start))+" monotonicStamp="+System.Diagnostics.Stopwatch.GetTimestamp()+
                                " pausedEngineBoundary=true "+RescueCaptureDiagnostics.WindowDelta(windowAllocation,windowEnd));
#endif
                            return result;
                        });
                        run.InSimulation(run.CurrentMission,()=>{
                            Require(proof.Fault==null,"Performance walking failed: "+proof.Fault);
                            RequirePerformanceCamera("after-window-"+label,referenceCentreX,referenceCentreY,referenceCameraX,referenceCameraY,referenceZoom);
                            Require(unchecked((uint)(end-start))>=windowTicks,"Insufficient actual advancing ticks");
                            Require(PerformanceBoundary(proof,false)==identity,"Performance identity changed during measured window");
                            if(workload==0)PerformanceAtSavedSources(proof);
                            var observer=Plugin.GeneralObserver;
                            if(proof.ObserveArmyMoves)Require(proof.OwnArmyMoves==0,"Measured performance army received an own recovery call");
                            if(workload==1)foreach(var group in proof.Groups)Require(group.Progress,"A shared walking group's fixed sample made no physical progress");
                            log("performance-window run="+proof.RunId+" iteration="+iteration+" enabled="+proof.Enabled+" workload="+(workload==0?"idle":"walking")+
                                " startTick="+start+" endTick="+end+" advancingTicks="+unchecked((uint)(end-start))+" warmupTicks="+warmupTicks+
                                " requestedPopulation="+population+" scenario="+scenario+" walkingPopulation=1000 workloadModel=full-idle-1000-walking physicalStart="+physical+" physicalEnd="+PerformanceCensus()+" identity="+identity+
                                " ambientAi=true ordinaryGroupCommands="+proof.Commands+" armyObservation="+(proof.ObserveArmyMoves?"final-unitmove":"passive-runtime-log")+
                                " ownArmyRecoveryCalls="+(proof.ObserveArmyMoves?proof.OwnArmyMoves.ToString(CultureInfo.InvariantCulture):"unobserved")+
                                " captureDelta="+(observer.CaptureCount-captures)+" attemptDelta="+(observer.AttemptCount-attempts)+" rescueDelta="+(observer.RescueCount-rescues)+" "+summary);return true;
                        });
                    }
                    // Boundary-only completion proof, outside the measured windows.
                    var endpoints=new List<GroupMemberGoal>();
                    run.InSimulation(run.CurrentMission,()=>{
                        foreach(var group in proof.Groups)
                        {
                            var sample=group.Sample;int targetY=group.Y+(sample.Y>=group.Y?-16:16);
                            Require(GameTribeManagerAPI.Instance.IssueMoveHereCommand(group.Tribe,group.X,targetY),"Performance final endpoint command refused");
                            var goal=CaptureGroupMemberGoal(sample,group.X,targetY);Require(goal.Assigned,"Performance final native sample goal absent");endpoints.Add(goal);
                        }
                        return true;
                    });
                    foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                    yield return run.WaitForTicks(run.CurrentMission,1024);
                    foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                    run.InSimulation(run.CurrentMission,()=>{
                        foreach(var goal in endpoints)Require(GroupMemberArrived(goal),"Performance fixed walking sample did not reach its actual native endpoint");
                        Require(PerformanceBoundary(proof,false)==identity,"Performance endpoint identity changed");
                        log("performance-endpoints run="+proof.RunId+" iteration="+iteration+" nativeSampleGoalsArrived="+endpoints.Count+" outsideMeasuredWindow=true");return true;
                    });
                }
            }
            finally
            {
                proof.Walking=false;proof.Active=false;proof.Armed=false;
                if(activeLabel!=null)log("performance-window-rejected "+probe.End(activeLabel));
                if(ReferenceEquals(performanceProof,proof))performanceProof=null;
                // The external transactional runner owns removal of this exact run-owned save.
                log("performance-cleanup ownedSave="+proof.Path+" keptForRunnerCleanup=true");
            }
        }
        private sealed class PerfProbeReflection
        {
            private readonly ConfigFile config;
            private readonly Type timing,sampler,simulation;
            private readonly Func<string,bool> begin;
            private readonly Func<string,string> end;
            private const BindingFlags Static=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
            internal PerfProbeReflection()
            {
                Require(Chainloader.PluginInfos.TryGetValue("PerfProbe_Serp",out var info) && !ReferenceEquals(info.Instance,null),"A timing-only /devdiag PerfProbe package is required");
                config=info.Instance.Config;var assembly=info.Instance.GetType().Assembly;
                timing=assembly.GetType("PerfProbe.PerfProbeRunTiming",true);sampler=assembly.GetType("PerfProbe.PerfProbeThreadSampler",true);
                simulation=assembly.GetType("PerfProbe.PerfProbeSimSampler",true);var durations=assembly.GetType("PerfProbe.PerfProbeNativeDurations",true);
                begin=(Func<string,bool>)Delegate.CreateDelegate(typeof(Func<string,bool>),durations.GetMethod("Begin",Static));
                end=(Func<string,string>)Delegate.CreateDelegate(typeof(Func<string,string>),durations.GetMethod("End",Static));
            }
            private void Setting(string section,string key,bool expected)
            {
                Require(config.TryGetEntry<bool>(section,key,out ConfigEntry<bool> entry) && entry.Value==expected,
                    "PerfProbe profile mismatch: "+section+"."+key);
            }
            private static object Field(Type type,string name)
            {
                var field=type.GetField(name,Static);Require(field!=null,"PerfProbe reflection field missing: "+type.FullName+"."+name);return field.GetValue(null);
            }
            internal void Validate()
            {
                Setting("Automation","Enabled",false);Setting("Automation","AutomationOnly",false);
                Setting("Sampler","Enabled",false);Setting("Sampler","LongRunFallback",false);
                Setting("Spikes","Enabled",false);Setting("Spikes","RunPhaseTiming",true);Setting("Zoom","RenderSnapshots",false);
                foreach(string key in new[]{"UpdateSweep","EngineMarkers","BatchCensus","GcTracking","RecordBreakdown","EngineCalls","TextureInventory"})Setting("Extras",key,false);
                Require(config.TryGetEntry<int>("Spikes","TickGapMs",out ConfigEntry<int> gap) && gap.Value==0,"PerfProbe tick-gap diagnostics must be disabled");
                Require((bool)Field(timing,"Instrumented"),"PerfProbe native phase instrumentation is absent");
                var enabled=sampler.GetProperty("IsEnabled",Static);Require(enabled!=null && !(bool)enabled.GetValue(null,null),"PerfProbe thread sampler is active");
                Require(Field(sampler,"worker")==null && Field(sampler,"analysisWorker")==null && !(bool)Field(simulation,"LongRunFallback"),
                    "PerfProbe sampling worker/fallback is active or unsupported");
            }
            internal bool Begin(string label)=>begin(label);
            internal string End(string label)=>end(label);
        }
    }
}