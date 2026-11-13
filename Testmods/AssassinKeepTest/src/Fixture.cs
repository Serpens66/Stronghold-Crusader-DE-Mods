using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using APIShared;
using APIShared.Automation;
using APIShared.Simulation;
using CrusaderDE;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace AssassinKeepTest
{
    // The static Plugin.active and permanent event publishers keep this observer alive after
    // startup cleanup. Native reads/mutations stay within the simulation monitor or tick.
    internal sealed partial class Fixture
    {
        private const string NativeHash = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        private readonly string map, scenario;
        private readonly Action<string> log;
        private readonly List<string> pending = new List<string>();
        private readonly List<OriginalActor> originalActors = new List<OriginalActor>();
        private sealed class OriginalActor { internal int Id, StartX, StartY, LastState=-1, LastTile=-1, LastIndex=-1; internal uint Global; internal eChimps Type=eChimps.CHIMP_TYPE_ARAB_ASSASIN; internal bool RoofArrived, GroundArrived; }
        private IMissionLifecycleCapability missions;
        private IDisposable moves;
        private int owner, keepOwner, keepId, minX, maxX, minY, maxY, tx, ty, sx, sy, actor, tribe, lastTick;
        private uint actorGlobal, keepGlobal;
        private eChimps actorType=eChimps.CHIMP_TYPE_ARAB_ASSASIN;
        private long session;
        private bool fixedPlaces, returning, connectedControl;
        private volatile bool capture, stopped, commandDone;
        private volatile string failure;
        private volatile Action command;
        private int startTick, hookTicks, ascentTicks, descentTicks, climbStarts, arrivals, lastState = -1, lastTile = -1, lastIndex = -1;
        private int descentStartTicks,descentBodyTicks,descentPositiveHeightTicks;
        private int lastMoveResult = -1, moveCalls;
        internal Fixture(string map, string scenario, Action<string> log) { this.map = map; this.scenario = scenario; this.log = log; }
        internal static bool Owns(string value) => value == "gate-main-improved" || value == "gate-inner-improved" || value == "gate-main-vanilla" || value == "gate-inner-vanilla" || value == "exact-editor-accept-improved" || value == "climb-controls-improved" || value == "climb-controls-vanilla" || value == "wall-keep-improved" || value == "wall-keep-vanilla" || value == "exact-accept-improved" || value == "exact-single-accept-improved" || value == "exact-improved" || value == "exact-vanilla" || value == "keep-improved" || value == "keep-vanilla" || value == "keep-enemy-improved" || value == "keep-enemy-vanilla" || value == "keep-coreoff-improved" || value == "keep-coreoff-vanilla" || value == "keep-enemy-coreoff-improved" || value == "keep-enemy-coreoff-vanilla" || value == "keep-enemy-attack-improved" || value == "keep-enemy-attack-vanilla" || value == "keep-enemy-attack-coreoff-improved" || value == "keep-enemy-attack-coreoff-vanilla";
        private bool Improved => scenario.EndsWith("-improved", StringComparison.Ordinal);
        private bool Enemy => scenario.StartsWith("keep-enemy-", StringComparison.Ordinal);
        private bool Exact => scenario.StartsWith("exact-", StringComparison.Ordinal);
        private bool Editor => scenario.StartsWith("exact-editor-",StringComparison.Ordinal);
        private bool Controls => scenario.StartsWith("climb-controls-",StringComparison.Ordinal) || scenario.StartsWith("wall-keep-",StringComparison.Ordinal) || scenario.StartsWith("gate-main-",StringComparison.Ordinal) || scenario.StartsWith("gate-inner-",StringComparison.Ordinal);
        private bool CoreOff => Exact || Controls || scenario.Contains("-coreoff-");
        private bool Attack => scenario.Contains("-attack-");
        private bool Accept => scenario.Contains("-accept-");
        private bool Single => scenario.StartsWith("exact-single-", StringComparison.Ordinal);
        internal void Stop() { stopped = true; capture = false; command = null; }
        internal IEnumerable<AutomationStep> Steps()
        {
            yield return new AutomationStep("skirmish", TimeSpan.FromSeconds(240), Start);
            yield return new AutomationStep("fresh-keep", TimeSpan.FromSeconds(60), Prepare, true);
            if(Controls)
            {
                if(scenario.StartsWith("gate-main-",StringComparison.Ordinal) || scenario.StartsWith("gate-inner-",StringComparison.Ordinal))
                { string kind=scenario.StartsWith("gate-main-",StringComparison.Ordinal)?"gate-main":"gate-inner";yield return new AutomationStep("climb-control-"+kind,TimeSpan.FromSeconds(150),run=>Control(run,kind),true);yield break; }
                if(!scenario.StartsWith("wall-keep-",StringComparison.Ordinal))
                    foreach(string kind in new[]{"wall","gate-main","gate-inner"})
                    { string current=kind;yield return new AutomationStep("climb-control-"+current,TimeSpan.FromSeconds(150),run=>Control(run,current),true); }
                yield return new AutomationStep("wall-connected-keep",TimeSpan.FromSeconds(150),WallConnectedKeep,true);
                yield break;
            }
            yield return new AutomationStep("opposite-door-single", TimeSpan.FromSeconds(100), Exercise, true);
            if(Accept) yield return new AutomationStep("ordinary-return-behind-keep", TimeSpan.FromSeconds(100), ReturnBehindKeep, true);
        }
        private IEnumerable<AutomationWait> Start(AutomationRun run)
        {
            string native = Path.Combine(BepInEx.Paths.GameRootPath, "Stronghold Crusader Definitive Edition_Data", "Plugins", "x86_64", "CrusaderDE.dll");
            using (var file = File.OpenRead(native)) using (var sha = SHA256.Create())
                Require(BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "") == NativeHash, "Unsupported native raw-read layout");
            object settings = Settings();
            Set(settings, "EnableMod", true);
            Set(settings, "EnableImprovedAssassinPathfinding", Improved);
            Set(settings, "EnableImprovedAssassinPathfindingAI", Improved);
            Set(settings, "EnablePathfindingCore", !CoreOff);
            Set(settings, "EnableMoveFormationEnhancements", false);
            if(Controls)Set(settings,"EnableAssassinGatehouseClimbFix",true);
            log("settings scenario=" + scenario + " improved=" + Get(settings, "EnableImprovedAssassinPathfinding") +
                " gateClimb=" + Get(settings, "EnableAssassinGatehouseClimbFix") + " core=" + Get(settings, "EnablePathfindingCore"));
            if(Editor){foreach(var wait in StartEditor(run))yield return wait;yield break;}
            string selectedMap = Exact || Controls ? "test_assassinkeep.map" : map;
            foreach (var wait in run.StartSkirmish(selectedMap, new[] { AILords.SK_RAT }, peaceTimeMinutes: Attack ? 0 : 60))
            { yield return wait; FixStartPlaces(); }
        }
        private void FixStartPlaces()
        {
            if (fixedPlaces || !MainViewModel.viewModelLoaded || !MainViewModel.Instance.Show_MPGameCreation) return;
            var lobby = MainViewModel.Instance.FRONTMultiplayer;
            if (lobby?.currentLobby == null || lobby.currentLobby.members.Count < 2) return;
            // Same array/refresh path as Vanilla's lobby shield swap. Reflection avoids publicized access.
            var field = typeof(FRONT_Multiplayer).GetField("MPsetupData", BindingFlags.Instance | BindingFlags.NonPublic);
            var setup = (EngineInterface.MultiplayerSetupData)field.GetValue(lobby);
            int[] order = setup.start_keep_location_order;
            int[,] places = GameData.Instance.Keep_Locations;
            var available = Enumerable.Range(0, order.Length).Where(i => places[i, 0] >= 0).Take(2).ToArray();
            Require(available.Length == 2, "Two usable start places required");
            for (int i = 0; i < order.Length; i++) order[i] = -10;
            for (int i = 0; i < available.Length; i++) order[available[i]] = i;
            typeof(FRONT_Multiplayer).GetMethod("UpdateHostInfo", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(lobby, new object[] { false });
            fixedPlaces = true;
            log("start-places=" + string.Join(";", available.Select(i => i + ":" + places[i, 0] + "," + places[i, 1])));
        }
        private IEnumerable<AutomationWait> Prepare(AutomationRun run)
        {
            yield return run.WaitForPlayState(run.CurrentMission);
            if (MainViewModel.Instance.Show_HUD_Briefing) MainViewModel.Instance.ButtonBriefingResumeCommand.Execute(null);
            yield return AutomationWait.Until("briefing closed", () => !MainViewModel.Instance.Show_HUD_Briefing);
            foreach (var wait in Pause(run, false)) yield return wait;
            yield return run.WaitForTicks(run.CurrentMission, 20);
            foreach (var wait in Pause(run, true)) yield return wait;
            object currentSettings = Settings();
            log("effective-settings after-load " + string.Join(" ", new[] { "EnableMod", "EnableImprovedAssassinPathfinding", "EnableImprovedAssassinPathfindingAI", "EnableAssassinGatehouseClimbFix", "EnablePathfindingCore", "AnchorSingleMoves", "EnableMoveFormationEnhancements" }.Select(n => n + "=" + Get(currentSettings, n))));
            Require((bool)Get(currentSettings, "EnableMod") && (bool)Get(currentSettings, "EnableImprovedAssassinPathfinding") == Improved,
                "Scenario feature settings were changed by mission presets; refusing an ambiguous A/B run");
            Require((bool)Get(currentSettings, "EnablePathfindingCore") == !CoreOff, "Scenario core setting changed during load");
            Require((bool)Get(currentSettings, "EnableImprovedAssassinPathfindingAI") == Improved,"Scenario AI weighted setting changed during load");
            session = run.CurrentMission.SessionId;
            Require(ApiShared.ForMod(Plugin.Guid).TryGetMissionLifecycle(out missions, out var diagnostic), diagnostic.Reason);
            Director.instance.SetEngineFrameRate(250);
            Simulation(run, () => { if (Controls)owner=GamePlayerManagerAPI.Instance.GetLocalPlayerId();else if (Exact) FindOriginalActorsAndKeep(); else FindKeepAndSpawn(); return 0; });
            moves = UnitR3EventHooks.OnUnitMoveHere.Observable.Subscribe(MoveObserved);
            Require(SimulationTickEvents.TryRegister(Plugin.Guid, "keep-trace", Tick, out string reason, 750), reason);
            Drain(run);
            log("READY session=" + session + " map=" + (Exact || Controls ? "test_assassinkeep.map" : map) + " scenario=" + scenario);
        }
        private unsafe void FindKeepAndSpawn()
        {
            var players = GamePlayerManagerAPI.Instance; var tiles = GameTileManagerAPI.Instance;
            owner = players.GetLocalPlayerId(); keepOwner = Enemy ? Enumerable.Range(1, 8).FirstOrDefault(p => p != owner && players.GetPlayerKeepId(p) > 0) : owner;
            Require(keepOwner > 0, "Target Keep owner is unavailable"); keepId = players.GetPlayerKeepId(keepOwner);
            Require(BuildingAccess.TryGetById(keepId, out GameBuilding* keep, out _) && keep != null && keep->r_GlobalId != 0, "Local Keep missing");
            keepGlobal = keep->r_GlobalId;
            minX = keep->r_TilePositionXBegin; minY = keep->r_TilePositionYBegin;
            maxX = minX + (int)keep->r_OccupyTileGridSize - 1; maxY = minY + (int)keep->r_OccupyTileGridSize - 1;
            Require(maxX >= minX && maxX - minX < 20, "Keep footprint is invalid");
            tx = (minX + maxX) / 2; ty = (minY + maxY) / 2;
            Require(tiles.GetTileBuildingId(tiles.GetTileId(tx, ty)) == keepId, "Roof center does not belong to Keep");
            var door = players.GetPlayerKeepDoorPosition(keepOwner);
            int dx = door.X - tx, dy = door.Y - ty;
            int backX = Math.Abs(dx) > Math.Abs(dy) ? -Math.Sign(dx) : 0;
            int backY = backX == 0 ? -Math.Sign(dy) : 0;
            Require(backX != 0 || backY != 0, "Keep door coincides with roof center");
            bool free = false; int start = -1;
            for (int distance = 3; distance <= 12 && !free; distance++)
            {
                sx = backX < 0 ? minX - distance : backX > 0 ? maxX + distance : tx;
                sy = backY < 0 ? minY - distance : backY > 0 ? maxY + distance : ty;
                if (!tiles.IsTileInsideMapBounds(sx, sy)) continue;
                start = tiles.GetTileId(sx, sy);
                free = tiles.IsTileWalkableAndUnoccupied(start) && tiles.GetTileBuildingId(start) == 0 &&
                    (tiles.GetTilePropertyFlag(start) & (TilePropertyFlag.IsWall | TilePropertyFlag.IsElevated | TilePropertyFlag.IsBuilding)) == 0;
            }
            Require(free, "No verified free ground3..12 tiles opposite the Keep door");
            long id = GameUnitManagerAPI.Instance.CreateUnitLocal(owner, owner, sx, sy, tiles.GetTileHeight(start), eChimps.CHIMP_TYPE_ARAB_ASSASIN);
            GameUnit* unit = null;
            Require(id > 0 && id <= int.MaxValue && UnitAccess.TryGetById((int)id, out unit, out _), "Assassin spawn missing");
            actor = (int)id; actorGlobal = unit->r_GlobalId;
            originalActors.Add(new OriginalActor {Id=actor,Global=actorGlobal,StartX=sx,StartY=sy});
            Require(unit->r_CurrentTilePositionX == sx && unit->r_CurrentTilePositionY == sy, "Assassin spawn projected");
            pending.Add("fixture owner=" + owner + " keepOwner=" + keepOwner + " keepId=" + keepId + " keepGlobal=" + keepGlobal + " type=" + keep->r_BuildingType +
                " footprint=" + minX + "," + minY + ".." + maxX + "," + maxY + " door=" + door.X + "," + door.Y +
                " actor=" + actor + ":" + actorGlobal + " start=" + sx + "," + sy + " target=" + tx + "," + ty);
            for (int y = minY - 1; y <= maxY + 1; y++) for (int x = minX - 1; x <= maxX + 1; x++) pending.Add("fixture-tile " + Tile(x, y));
        }
        private unsafe void FindOriginalActorsAndKeep()
        {
            owner = GamePlayerManagerAPI.Instance.GetLocalPlayerId();
            var units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
            for (int i=0;i<units.Length;i++)
            {
                ref GameUnit unit = ref units[i];
                if (!UnitAccess.IsReallyAlive(in unit) || unit.r_UnitChimp != eChimps.CHIMP_TYPE_ARAB_ASSASIN) continue;
                pending.Add("original-assassin id="+(i+1)+" global="+unit.r_GlobalId+" owner="+unit.r_ControllableForPlayerId+" tile="+unit.r_CurrentTilePositionX+","+unit.r_CurrentTilePositionY);
                if (unit.r_ControllableForPlayerId==owner) originalActors.Add(new OriginalActor {Id=i+1,Global=unit.r_GlobalId,StartX=unit.r_CurrentTilePositionX,StartY=unit.r_CurrentTilePositionY});
            }
            Require(originalActors.Count>0,"Skirmish did not preserve any authored own Assassins; no units were spawned");
            if(Single && originalActors.Count>1) originalActors.RemoveRange(1,originalActors.Count-1);
            actor=originalActors[0].Id; actorGlobal=originalActors[0].Global;
            GameUnit* first=Actor(); sx=first->r_CurrentTilePositionX;sy=first->r_CurrentTilePositionY;
            int distance=int.MaxValue; var buildings=GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            for(int i=0;i<buildings.Length;i++)
            {
                ref GameBuilding b=ref buildings[i];
                if(b.r_AliveState!=AliveState.IsAlive || !IsKeep(b.r_BuildingType))continue;
                int x=b.r_TilePositionXBegin+(int)b.r_OccupyTileGridSize/2,y=b.r_TilePositionYBegin+(int)b.r_OccupyTileGridSize/2;
                pending.Add("original-keep id="+(i+1)+" global="+b.r_GlobalId+" owner="+b.r_PlayerIdOwner+" type="+b.r_BuildingType+" center="+x+","+y);
                int d=Math.Abs(x-sx)+Math.Abs(y-sy);
                if(d>=distance)continue; distance=d;keepId=i+1;keepGlobal=b.r_GlobalId;keepOwner=b.r_PlayerIdOwner;
                minX=b.r_TilePositionXBegin;minY=b.r_TilePositionYBegin;maxX=minX+(int)b.r_OccupyTileGridSize-1;maxY=minY+(int)b.r_OccupyTileGridSize-1;tx=x;ty=y;
            }
            Require(keepId>0,"No authored Keep survived Skirmish load");
            Require(GameTileManagerAPI.Instance.GetTileBuildingId(GameTileManagerAPI.Instance.GetTileId(tx,ty))==keepId,"Target center is not the selected Keep roof");
            pending.Add("EXACT fixture originalActors="+originalActors.Count+" actorOwner="+owner+" keepOwner="+keepOwner+" keep="+keepId+":"+keepGlobal+" target="+tx+","+ty+" noSpawns=true noGeometryWrites=true");
            for(int y=minY-2;y<=maxY+2;y++)for(int x=minX-2;x<=maxX+2;x++)pending.Add("fixture-tile "+Tile(x,y));
        }
        private static bool IsKeep(eStructs type) => type==eStructs.STRUCT_KEEP_ONE || type==eStructs.STRUCT_KEEP_TWO || type==eStructs.STRUCT_KEEP_THREE || type==eStructs.STRUCT_KEEP_FOUR || type==eStructs.STRUCT_KEEP_FIVE;
        private IEnumerable<AutomationWait> Exercise(AutomationRun run)
        {
            foreach (var wait in Pause(run, false)) yield return wait;
            yield return run.WaitForTicks(run.CurrentMission, 10);
            Simulation(run, () => { SelectActor(); return 0; });
            yield return run.WaitForTicks(run.CurrentMission, 8);
            yield return AutomationWait.Until("exact selected Assassin tribe", () => Simulation(run, GroupReady));
            commandDone = false;
            command = () => { capture = true; startTick = lastTick; pending.Add("COMMAND kind=" + (Attack ? "AttackBuilding" : "MoveHere") + " tribe=" + tribe + " target=" + tx + "," + ty + " keep=" + keepId + ":" + keepGlobal);
                Require(Attack ? GameTribeManagerAPI.Instance.AttackBuildingEx(tribe, keepId, checked((int)keepGlobal)) :
                    GameTribeManagerAPI.Instance.IssueMoveHereCommand(tribe, tx, ty), "Command wrapper rejected"); };
            yield return AutomationWait.Until("simulation move submitted", () => commandDone);
            Require(failure == null, failure);
            for (int i = 0; i < 80; i++)
            {
                yield return run.WaitForTicks(run.CurrentMission, 24);
                Drain(run); Require(failure == null, failure);
                if (Simulation(run, () => Accept || Controls || connectedControl ? AllAtDestination(false) : arrivals > 0)) break;
            }
            foreach (var wait in Pause(run, true)) yield return wait;
            Simulation(run, () => { SampleAll(true); capture = false;
                pending.Add("RESULT ticks=" + (lastTick - startTick) + " moveCalls=" + moveCalls + " lastMoveResult=" + lastMoveResult +
                    " hookTicks=" + hookTicks + " ascentTicks=" + ascentTicks + " descentTicks=" + descentTicks + " climbStarts=" + climbStarts + " arrivals=" + arrivals);
                Require(moveCalls > 0, "No native unit move event observed"); return 0; });
            Drain(run);
            if(Accept || connectedControl)
            {
                Simulation(run, () => { Require(AllAtDestination(false),"Not every commanded Assassin reached and stopped on the Keep roof");RequireNoClimb();return 0; });
                log("ACCEPT_ROOF commanded="+originalActors.Count+" allArrived=true climbStates=0 scenario="+scenario);
            }
            // Diagnostic success means the real command and bounded trace ran; arrival/rejection is evidence.
            log("TRACE_COMPLETE scenario=" + scenario + " observedCommand=true arrived=" + (arrivals > 0));
        }
        private IEnumerable<AutomationWait> ReturnBehindKeep(AutomationRun run)
        {
            int previousDescentStart=descentStartTicks,previousDescentBody=descentBodyTicks,previousPositiveHeight=descentPositiveHeightTicks;
            Simulation(run, () => { SelectActor();return 0; });
            foreach(var wait in Pause(run,false))yield return wait;
            yield return run.WaitForTicks(run.CurrentMission,8);
            yield return AutomationWait.Until("selected return group",()=>Simulation(run,GroupReady));
            commandDone=false;
            command=()=>{returning=true;capture=true;startTick=lastTick;
                pending.Add("RETURN_COMMAND tribe="+tribe+" target="+sx+","+sy+" commanded="+originalActors.Count);
                Require(GameTribeManagerAPI.Instance.IssueMoveHereCommand(tribe,sx,sy),"Return command rejected");};
            yield return AutomationWait.Until("return submitted",()=>commandDone);
            for(int i=0;i<80;i++)
            {
                yield return run.WaitForTicks(run.CurrentMission,24);Drain(run);Require(failure==null,failure);
                if(Simulation(run,()=>AllAtDestination(true)))break;
            }
            foreach(var wait in Pause(run,true))yield return wait;
            Simulation(run,()=>{SampleAll(true);capture=false;Require(AllAtDestination(true),"Not every commanded Assassin returned to the requested clear ground");
                if(Accept)RequireNoClimb();
                else Require(descentStartTicks>previousDescentStart && descentBodyTicks>previousDescentBody && descentPositiveHeightTicks>previousPositiveHeight,
                    "Control return did not observe both descent states128/129 with positive native climb height");return 0;});
            Drain(run);
            if(Accept)log("ACCEPT_RETURN commanded="+originalActors.Count+" allArrived=true climbStates=0 scenario="+scenario);
            else log("CONTROL_DESCENT_PASS scenario="+scenario+" id="+keepId+":"+keepGlobal+" startTicks="+(descentStartTicks-previousDescentStart)+
                " bodyTicks="+(descentBodyTicks-previousDescentBody)+" positiveHeightTicks="+(descentPositiveHeightTicks-previousPositiveHeight)+" allArrived=true");
        }
        private void RequireNoClimb()=>Require(hookTicks==0 && ascentTicks==0 && descentTicks==0 && climbStarts==0,
            "Unwanted physical climb state observed: hook="+hookTicks+" ascent="+ascentTicks+" descent="+descentTicks);
        private unsafe bool AllAtDestination(bool ground)
        {
            foreach(var current in originalActors)
            {
                Require(UnitAccess.TryGetById(current.Id,out GameUnit* unit,out _) && unit!=null && unit->r_GlobalId==current.Global && UnitAccess.IsReallyAlive(unit),"Acceptance actor identity/life changed");
                bool roof=unit->r_CurrentTilePositionX>=minX && unit->r_CurrentTilePositionX<=maxX && unit->r_CurrentTilePositionY>=minY && unit->r_CurrentTilePositionY<=maxY;
                bool moving=unit->r_PathPlanStateBitFlags!=0 && (unit->r_CurrentPathPlanIndex<unit->r_PathPlanLength || unit->r_NextTilePositionX2!=unit->r_CurrentTilePositionX || unit->r_NextTilePositionY2!=unit->r_CurrentTilePositionY);
                bool arrived=!moving && (int)unit->r_AIState<126 && (ground ? !roof && Math.Abs(unit->r_CurrentTilePositionX-sx)<=3 && Math.Abs(unit->r_CurrentTilePositionY-sy)<=3 : roof);
                if(!arrived)return false;
            }
            return originalActors.Count>0;
        }
        private unsafe bool GroupReady()
        {
            tribe = Actor()->r_TribeId;
            return tribe > 0 && GameTribeManagerAPI.Instance.TryGetTribeById(tribe, out GameTribe* group) && group->r_UnitsInGroup == originalActors.Count;
        }
        private unsafe void SelectActor()
        { Require(UnitAccess.IsReallyAlive(Actor()), "Assassin not alive"); GamePlayerManagerAPI.Instance.SetSelectedChimps(originalActors.Select(a=>a.Id).ToList()); }
        private void MoveObserved(UnitMoveHereEventArgs args)
        {
            if (!capture || stopped || args.Phase != EventHookPhase.Post) return;
            var observed=originalActors.FirstOrDefault(a=>a.Id==args.UnitId);if(observed==null)return;
            moveCalls++; lastMoveResult = (int)args.ReturnValue;
            pending.Add("MOVE_POST actor="+args.UnitId+" tick=" + lastTick + " result=" + args.ReturnValue);
            int previousActor=actor;uint previousGlobal=actorGlobal;eChimps previousType=actorType;actor=observed.Id;actorGlobal=observed.Global;actorType=observed.Type;
            try{Route();}finally{actor=previousActor;actorGlobal=previousGlobal;actorType=previousType;}
        }
        private void Tick(int tick)
        {
            if (stopped || failure != null || (!capture && command == null)) return;
            if (missions?.Current?.SessionId != session) { failure = "Mission replaced"; command = null; commandDone = true; return; }
            lastTick = tick;
            try { var next = command; command = null; if (next != null) { try { next(); } finally { commandDone = true; } } if (capture) SampleAll(false); }
            catch (Exception ex) { failure = ex.ToString(); commandDone = true; }
        }
        private void SampleAll(bool force)
        {
            foreach(var current in originalActors)
            {
                actor=current.Id;actorGlobal=current.Global;actorType=current.Type;lastState=current.LastState;lastTile=current.LastTile;lastIndex=current.LastIndex;
                Sample(force);current.LastState=lastState;current.LastTile=lastTile;current.LastIndex=lastIndex;
                RecordArrival(current);
            }
            actor=originalActors[0].Id;actorGlobal=originalActors[0].Global;actorType=originalActors[0].Type;
        }
        private unsafe void RecordArrival(OriginalActor current)
        {
            GameUnit* unit=Actor();
            bool roof=unit->r_CurrentTilePositionX>=minX && unit->r_CurrentTilePositionX<=maxX && unit->r_CurrentTilePositionY>=minY && unit->r_CurrentTilePositionY<=maxY;
            bool moving=unit->r_PathPlanStateBitFlags!=0 && (unit->r_CurrentPathPlanIndex<unit->r_PathPlanLength || unit->r_NextTilePositionX2!=unit->r_CurrentTilePositionX || unit->r_NextTilePositionY2!=unit->r_CurrentTilePositionY);
            if(moving || (int)unit->r_AIState>=126)return;
            if(!returning && roof && !current.RoofArrived){current.RoofArrived=true;pending.Add("ARRIVED_ROOF actor="+actor+":"+actorGlobal+" tick="+lastTick);}
            if(returning && !roof && Math.Abs(unit->r_CurrentTilePositionX-sx)<=3 && Math.Abs(unit->r_CurrentTilePositionY-sy)<=3 && !current.GroundArrived)
            {current.GroundArrived=true;pending.Add("ARRIVED_GROUND actor="+actor+":"+actorGlobal+" tick="+lastTick);}
        }
        private unsafe GameUnit* Actor()
        {
            Require(UnitAccess.TryGetById(actor, out GameUnit* unit, out _) && unit != null && unit->r_GlobalId == actorGlobal &&
                unit->r_UnitChimp == actorType && unit->r_ControllableForPlayerId == owner, "Actor identity lost");
            return unit;
        }
        private unsafe void Sample(bool force)
        {
            GameUnit* unit = Actor(); int state = (int)unit->r_AIState, tile = (int)unit->r_CurrentPositionTileId;
            byte* raw = (byte*)unit;
            if (state == 126) hookTicks++; if (state == 127) ascentTicks++; if (state == 128 || state == 129) descentTicks++;
            if(state==128)descentStartTicks++;if(state==129)descentBodyTicks++;
            if((state==128 || state==129) && *(short*)(raw+0x410)>0)descentPositiveHeightTicks++;
            if (state == 126 && lastState != 126) climbStarts++;
            bool changed = state != lastState || tile != lastTile || unit->r_CurrentPathPlanIndex != lastIndex;
            if (force || changed || state >= 126 && state <= 129 || (lastTick - startTick) % 24 == 0)
            {
                pending.Add("SAMPLE actor="+actor+":"+actorGlobal+" type="+actorType+" tick=" + lastTick + " dt=" + (lastTick - startTick) + " state=" + state + " pos=" + unit->r_CurrentTilePositionX + "," + unit->r_CurrentTilePositionY +
                    " next=" + unit->r_NextTilePositionX2 + "," + unit->r_NextTilePositionY2 + " base=" + unit->r_HeightElevation + " render=" + *(short*)(raw + 0xB8) +
                    " climbHeight=" + *(short*)(raw + 0x410) + " progress=" + *(short*)(raw + 0x414) + " flags=" + unit->r_PathPlanStateBitFlags + " substep=" + unit->r_MovementSubstep +
                    " route=" + unit->r_CurrentPathPlanIndex + "/" + unit->r_PathPlanLength + " from={" + Tile(unit->r_CurrentTilePositionX, unit->r_CurrentTilePositionY) +
                    "} to={" + Tile(unit->r_NextTilePositionX2, unit->r_NextTilePositionY2) + "}");
            }
            if (changed) Route();
            lastState = state; lastTile = tile; lastIndex = unit->r_CurrentPathPlanIndex;
            if (unit->r_CurrentTilePositionX >= minX && unit->r_CurrentTilePositionX <= maxX && unit->r_CurrentTilePositionY >= minY && unit->r_CurrentTilePositionY <= maxY && state < 126)
                arrivals++;
        }
        private unsafe string Tile(int x, int y)
        {
            var tiles = GameTileManagerAPI.Instance;
            if (!tiles.IsTileInsideMapBounds(x, y)) return x + "," + y + ":outside";
            int tile = tiles.GetTileId(x, y), id = tiles.GetTileBuildingId(tile); string identity = "none";
            if (id != 0 && BuildingAccess.TryGetById(id, out GameBuilding* b, out _) && b != null) identity = b->r_BuildingType + ":" + b->r_GlobalId;
            return x + "," + y + ":id=" + tile + ":f=" + ((uint)tiles.GetTilePropertyFlag(tile)).ToString("X") + ":h=" + tiles.GetTileHeight(tile) +
                ":edge=" + GamePathingManagerAPI.Instance.GetPathEdgeMaskGrid()[tile].ToString("X2") + ":was=" + tiles.GetStructureWasLayer()[tile] + ":b=" + id + ":identity=" + identity;
        }
        private void Route()
        {
            if (!GamePathingManagerAPI.Instance.TryGetUnitPathPlanView(actor, out GameUnitPathPlanView view) || !view.IsLengthValid) { pending.Add("ROUTE unavailable"); return; }
            string directions = "";
            for (int i = view.CurrentIndex; i < view.Length && i < view.CurrentIndex + 256; i++)
            { if (!view.TryGetDirection(i, out PackedPathDirection dir)) break; directions += ((int)dir).ToString("X"); }
            pending.Add("ROUTE actor="+actor+":"+actorGlobal+" tick=" + lastTick + " cursor=" + view.CurrentIndex + " length=" + view.Length + " lowNibbleFirst=" + directions);
        }
        private void Drain(AutomationRun run)
        {
            string[] copy = Simulation(run, () => { var rows = pending.ToArray(); pending.Clear(); return rows; });
            foreach (string row in copy) log(row);
        }
        private static object Get(object settings, string name) => settings.GetType().GetProperty(name)?.GetValue(settings) ?? "unavailable";
        private static object Settings()
        { var plugin = BepInEx.Bootstrap.Chainloader.PluginInfos["Pathfinding_Serp"].Instance; return plugin.GetType().GetProperty("Settings").GetValue(plugin); }
        private static void Set(object settings, string name, bool value)
        { var property = settings.GetType().GetProperty(name) ?? throw new MissingMemberException(name); property.SetValue(settings, value); Require((bool)property.GetValue(settings) == value, "Setting rejected " + name); }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message ?? "Fixture failed"); }
    }
}
