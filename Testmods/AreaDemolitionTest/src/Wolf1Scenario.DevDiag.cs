using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Input;
using APIShared;
using APIShared.Automation;
using BepInEx;
using BepInEx.Logging;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using Shared;
using UnityEngine;

namespace AreaDemolitionTest
{
    internal sealed class Wolf1Driver
    {
        internal Action<Vector2,ushort> Begin;
        internal Action<Vector2> Move,Release;
        internal Action Cancel,Reset;
        internal Func<Wolf1State> Read;
        internal Func<bool> Idle;
        internal Func<Wolf1Wall[],int,ushort,Wolf1Expectation> Expected;
    }
    internal sealed class Wolf1State
    {
        internal long SessionId;
        internal int Actor,ProcessedRoots,QuoteRoots;
        internal bool Busy,FinalPending,PendingReceipt,GestureOwned;
        internal string Stage,Status,Timings;
        internal DemolitionTarget[] RedTargets,SubmittedTargets,SentTargets;
        internal ApplyResult[] Results;
    }
    internal sealed class Wolf1Wall
    {
        internal int Tile,X,Y,Owner,Unit,Structure,Height,DefaultHeight,Pending;
        internal uint Flags;
        internal string Describe()=>"tile="+Tile+" xy="+X+","+Y+" owner="+Owner+" unit="+Unit+" structure="+Structure+
            " height="+Height+" defaultHeight="+DefaultHeight+" pending="+Pending+" flags=0x"+Flags.ToString("X8",CultureInfo.InvariantCulture);
    }
    internal sealed class Wolf1Eligibility {internal DemolitionTarget Target;internal bool Allowed;internal string Reason;internal int Tile;}
    internal sealed class Wolf1Expectation
    {
        internal Wolf1Eligibility[] Walls;
        internal DemolitionTarget[] Buildings;
    }

    // Run only through the explicit APIShared .run request. These are local tests of the
    // production gesture/quote/render/transport pipeline, never direct fabricated target submission.
    internal sealed class Wolf1Scenario
    {
        private readonly Guid id;
        private readonly Wolf1Driver driver;
        private readonly bool combined;
        private readonly string label,artifacts;
        private readonly List<string> errors=new List<string>();
        private Wolf1Wall[] baseline;
        private Wolf1Expectation expected;
        private int owner;
        private long session;

        private Wolf1Scenario(Guid id,Wolf1Driver driver,string scenario)
        {
            this.id=id;this.driver=driver;combined=scenario=="wolf1-combined";label=combined?"combined":"walls";
            artifacts=Path.Combine(Paths.GameRootPath,"BepInEx","SerpAutomationArtifacts",id.ToString("N"));
        }
        internal static void Start(ModApiClient api,ManualLogSource log,Guid id,Wolf1Driver driver,Action cleanup,string scenario)
        {
            var fixture=new Wolf1Scenario(id,driver,scenario);
            api.Automation.Start(id,new[]{
                new AutomationStep("wolf1-fresh-castle",TimeSpan.FromSeconds(180),fixture.Prepare),
                new AutomationStep("wolf1-"+fixture.label+"-gesture",TimeSpan.FromSeconds(100),fixture.Exercise,missionBound:true)
            },log,_=>cleanup(),quitWhenDone:true);
        }
        private void Report(string text)
        { string record="wolf1 run="+id.ToString("N")+" case="+label+" "+text;DevDiag.Log(()=>record); }

        private IEnumerable<AutomationWait> Prepare(AutomationRun run)
        {
            string map=Path.Combine(Application.streamingAssetsPath,"Maps","Wide_Open_Plain.map");
            if(!File.Exists(map))throw new FileNotFoundException("Wolf1 fixture map is missing.",map);
            foreach(var wait in run.StartSkirmish(map,new[]{AILords.SK_RAT},60))yield return wait;
            yield return run.WaitForPlayState(run.CurrentMission);
            ResumeBriefing();yield return AutomationWait.NextFrame;
            yield return AutomationWait.Until("CastlePlanner wolf1 preview",()=>
            {
                ResumeBriefing();var model=CastleModel();
                return model!=null && ReadProperty<bool>(model,"PreviewVisible") && ReadProperty<bool>(model,"CanConfirmCastle");
            });
            object blueprint=CastleModel();
            string selected=ReadProperty<string>(blueprint,"SelectedCastle");
            if(!Regex.IsMatch(selected??"",@"\bwolf1(?:\.aivjson)?\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant))
                throw new InvalidOperationException("Select Vanilla wolf1 in CastlePlanner before arming this test; current="+selected);
            long before=run.CurrentMission.SessionId;
            Report("confirm selected="+selected+" initialSession="+before+" map="+map);
            var command=ReadProperty<ICommand>(blueprint,"ConfirmCastleCommand");
            if(command==null || !command.CanExecute(null))throw new InvalidOperationException("CastlePlanner confirmation is unavailable.");
            command.Execute(null); // The same public command as the normal CastlePlanner confirm button.
            yield return AutomationWait.Until("CastlePlanner committed restart",()=>
            { ResumeBriefing();return run.CurrentMission!=null && run.CurrentMission.SessionId!=before; });
            var mission=run.CurrentMission;
            if(mission.IsSave || mission.Mode.IsRealMultiplayer || !mission.Mode.IsSingleplayerSkirmish)
                throw new InvalidOperationException("CastlePlanner did not restart into a fresh local Skirmish.");
            yield return run.WaitForPlayState(mission);
            ResumeBriefing();yield return AutomationWait.Until("restarted briefing closed",()=>{ResumeBriefing();return !MainViewModel.Instance.Show_HUD_Briefing;});
            foreach(var wait in run.SetPause(mission,false))yield return wait;
            yield return run.WaitForTicks(mission,12);
            owner=PlayerPerspectiveAPI.GetInputControlledPlayerId();session=mission.SessionId;
            if(owner<1 || owner>8)throw new InvalidOperationException("No real controlled player.");
            string previous=null;int stable=0;
            for(int attempt=0;attempt<30 && stable<4;attempt++)
            {
                baseline=run.InSimulation(mission,()=>ReadWalls(owner));
                string stamp=string.Join("|",baseline.Select(w=>w.Tile+":"+w.Flags+":"+w.Height+":"+w.DefaultHeight));
                stable=stamp==previous?stable+1:0;previous=stamp;
                if(stable<4)yield return AutomationWait.Delay(TimeSpan.FromMilliseconds(500));
            }
            if(stable<4 || baseline.Length<100)throw new InvalidOperationException("Castle never settled with100+ own wall cells; walls="+baseline.Length);
            int buildings=run.InSimulation(mission,()=>OwnBuildingCount(owner));
            Report("settled session="+session+" owner="+owner+" walls="+baseline.Length+" buildings="+buildings);
            ushort mask=combined?AllFilters():(ushort)(1<<(int)TargetGroup.Walls);
            expected=run.InSimulation(mission,()=>driver.Expected(baseline,owner,mask));
            var baselineRows=baseline.ToDictionary(w=>w.Tile);
            foreach(var entry in expected.Walls)Report("expected eligible="+entry.Allowed+" reason="+entry.Reason+" "+baselineRows[entry.Tile].Describe());
            GamePlayerManagerAPI.Instance.SetScreenCenterToTilePosition(new System.Numerics.Vector2((float)baseline.Average(w=>w.X),(float)baseline.Average(w=>w.Y)));
            yield return AutomationWait.Delay(TimeSpan.FromMilliseconds(500));
            bool fit=false;
            for(int zoom=0;zoom<10;zoom++)
            {
                var rectangle=CastleRectangle();
                fit=rectangle.MinX>Screen.width*.13f && rectangle.MaxX<Screen.width*.83f && rectangle.MinY>Screen.height*.17f && rectangle.MaxY<Screen.height*.88f;
                if(fit)break;
                float beforeZoom=PerfectPixelWithZoom.instance.getZoom(),afterZoom=beforeZoom;
                foreach(var wait in run.ZoomStep(mission,false,value=>afterZoom=value))yield return wait;
                yield return AutomationWait.Delay(TimeSpan.FromMilliseconds(150));
                if(afterZoom==beforeZoom)break;
            }
            if(!fit)throw new InvalidOperationException("The settled castle does not fit the unobstructed test viewport at available zoom.");
            Directory.CreateDirectory(artifacts);
            Report("artifacts="+artifacts+" zoom="+PerfectPixelWithZoom.instance.getZoom());
        }

        private IEnumerable<AutomationWait> Exercise(AutomationRun run)
        {
            var mission=run.CurrentMission;
            if(mission.SessionId!=session)throw new InvalidOperationException("Wrong settled castle session.");
            ushort mask=combined?AllFilters():(ushort)(1<<(int)TargetGroup.Walls);
            driver.Reset();
            var full=CastleRectangle();
            var corners=new[]{new Vector2(full.MinX,full.MinY),new Vector2(full.MinX,full.MaxY),new Vector2(full.MaxX,full.MinY),new Vector2(full.MaxX,full.MaxY)};
            Vector2 anchor=default,end=default;bool began=false;
            try
            {
                foreach(var point in corners)
                {
                    driver.Begin(point,mask);yield return AutomationWait.NextFrame;
                    if(driver.Read().GestureOwned)
                    {anchor=point;end=new Vector2(point.x==full.MinX?full.MaxX:full.MinX,point.y==full.MinY?full.MaxY:full.MinY);began=true;break;}
                    driver.Cancel();yield return AutomationWait.NextFrame;
                }
                if(!began)throw new InvalidOperationException("No valid castle rectangle corner can start the real bulldozer gesture.");
                double maximumFrameMs=0;long last=Stopwatch.GetTimestamp();
                for(int frame=0;frame<18;frame++)
                {
                    float fraction=frame<9?.15f+.85f*frame/8f:frame<12?.32f:1f;
                    driver.Move(Vector2.Lerp(anchor,end,fraction));
                    yield return AutomationWait.NextFrame;
                    long now=Stopwatch.GetTimestamp();maximumFrameMs=Math.Max(maximumFrameMs,(now-last)*1000d/Stopwatch.Frequency);last=now;
                }
                var hold=Stopwatch.StartNew();int samples=0;
                while(hold.Elapsed<TimeSpan.FromSeconds(5.5))
                {
                    driver.Move(end);yield return AutomationWait.NextFrame;
                    long now=Stopwatch.GetTimestamp();maximumFrameMs=Math.Max(maximumFrameMs,(now-last)*1000d/Stopwatch.Frequency);last=now;
                    if(hold.Elapsed.TotalSeconds>=samples*.5)
                    {var state=driver.Read();Report("preview sample="+(samples++)+" stage="+state.Stage+" roots="+state.ProcessedRoots+"/"+state.QuoteRoots+" red="+state.RedTargets.Length);}
                }
                var red=driver.Read();
                var redWalls=red.RedTargets.Where(t=>t.Kind==TargetKind.Wall).ToArray();
                var eligibleIds=new HashSet<int>(expected.Walls.Where(w=>w.Allowed).Select(w=>w.Tile));
                var redIds=new HashSet<int>(redWalls.Select(w=>w.TileId));
                int[] missedRed=eligibleIds.Where(tile=>!redIds.Contains(tile)).OrderBy(tile=>tile).ToArray();
                Report("red count="+red.RedTargets.Length+" walls="+redWalls.Length+" maxFrameMs="+maximumFrameMs.ToString("F3",CultureInfo.InvariantCulture)+" ids="+Ids(redWalls.Select(t=>t.TileId)));
                Report("coverage eligible="+eligibleIds.Count+" protected="+expected.Walls.Count(w=>!w.Allowed)+" missingRed="+Ids(missedRed));
                if(missedRed.Length!=0)errors.Add("Native-eligible expected walls missing from red preview: "+missedRed.Length);
                if(redWalls.Length<100)errors.Add("Stable red preview did not include100+ wall cells: "+redWalls.Length);
                ScreenCapture.CaptureScreenshot(Path.Combine(artifacts,label+"-red.png"));
                yield return AutomationWait.Delay(TimeSpan.FromMilliseconds(600));
                driver.Release(end);yield return AutomationWait.NextFrame;
                yield return AutomationWait.Until("real final quote, render receipt and submitted batches",()=>
                    driver.Idle());
                yield return run.WaitForTicks(mission,12);
                var final=driver.Read();var remaining=run.InSimulation(mission,()=>ReadWalls(owner));
                var remainingIds=new HashSet<int>(remaining.Select(w=>w.Tile));
                var sentWalls=final.SentTargets.Where(t=>t.Kind==TargetKind.Wall).ToArray();
                var leftSent=sentWalls.Where(t=>remainingIds.Contains(t.TileId)).ToArray();
                var leftRed=redWalls.Where(t=>remainingIds.Contains(t.TileId)).ToArray();
                var frozenWalls=final.SubmittedTargets.Where(t=>t.Kind==TargetKind.Wall).ToArray();
                var frozenIds=new HashSet<int>(frozenWalls.Select(t=>t.TileId));
                var missedFinal=eligibleIds.Where(tile=>!frozenIds.Contains(tile)).OrderBy(tile=>tile).ToArray();
                var missingReceived=frozenWalls.Where(t=>!sentWalls.Any(s=>s.Identity==t.Identity)).ToArray();
                Report("result submitted="+final.SubmittedTargets.Length+" received="+final.SentTargets.Length+" receivedWalls="+sentWalls.Length+
                    " results="+string.Join(",",final.Results.GroupBy(r=>r.Status).Select(g=>g.Key+":"+g.Count()))+" status="+final.Status+
                    " redRemaining="+leftRed.Length+" receivedRemaining="+leftSent.Length+" timing=count:totalMs:maxMs "+final.Timings);
                Report("receivedWallIds="+Ids(sentWalls.Select(t=>t.TileId))+" redRemainingIds="+Ids(leftRed.Select(t=>t.TileId)));
                Report("finalFrozenRedWallIds="+Ids(frozenWalls.Select(t=>t.TileId))+" expectedMissingFinal="+Ids(missedFinal)+" frozenNotReceived="+Ids(missingReceived.Select(t=>t.TileId)));
                var inspect=new HashSet<int>(leftRed.Select(t=>t.TileId).Concat(leftSent.Select(t=>t.TileId)));
                foreach(var wall in remaining.Where(w=>inspect.Contains(w.Tile)))Report("remaining "+wall.Describe());
                foreach(var result in final.Results.Where(r=>r.Status!=ApplyStatus.Applied))Report("receipt identity="+result.Identity+" status="+result.Status+" detail="+result.Detail);
                ScreenCapture.CaptureScreenshot(Path.Combine(artifacts,label+"-after.png"));
                yield return AutomationWait.Delay(TimeSpan.FromMilliseconds(600));
                if(sentWalls.Length==0)errors.Add("No wall target reached the real native receiver.");
                if(missedFinal.Length!=0)errors.Add("Native-eligible expected walls missing from final frozen red quote: "+missedFinal.Length);
                if(missingReceived.Length!=0)errors.Add("Final red targets were never received: "+missingReceived.Length);
                if(leftSent.Length!=0)errors.Add("Received wall targets remained: "+leftSent.Length);
                if(leftRed.Length!=0)errors.Add("Red wall targets remained: "+leftRed.Length);
                if(final.Results.Any(r=>r.Status!=ApplyStatus.Applied))errors.Add("Some actual receipts were not Applied.");
                if(errors.Count!=0)throw new InvalidOperationException(string.Join(" | ",errors));
            }
            finally {driver.Cancel();}
        }

        private static ushort AllFilters()
        {ushort value=0;foreach(TargetGroup group in Enum.GetValues(typeof(TargetGroup)))value|=(ushort)(1<<(int)group);return value;}
        private ScreenRect CastleRectangle()
        {
            float minX=float.MaxValue,minY=float.MaxValue,maxX=float.MinValue,maxY=float.MinValue;
            foreach(var wall in baseline)
            {
                GameMap.instance.mapGameTileToTilemapCoord(wall.X,wall.Y,out int tx,out int ty);
                var tile=GameMap.instance.getMapTile(tx,ty);
                if(tile==null)continue;
                Vector3 world=TilemapManager.instance.gameTileMap.GetCellCenterWorld(new Vector3Int(tx,ty,0))+Vector3.up*tile.height;
                Vector3 point=Camera.main.WorldToScreenPoint(world);
                if(point.z<=0)throw new InvalidOperationException("Castle is behind the map camera.");
                minX=Math.Min(minX,point.x);maxX=Math.Max(maxX,point.x);minY=Math.Min(minY,point.y);maxY=Math.Max(maxY,point.y);
            }
            if(combined && expected?.Buildings!=null)
                foreach(int id in expected.Buildings.SelectMany(b=>b.OccupiedTiles).Distinct())
                {
                    var point=GameTileManagerAPI.Instance.GetTileVectorFromId(id);
                    GameMap.instance.mapGameTileToTilemapCoord(point.X,point.Y,out int tx,out int ty);
                    var tile=GameMap.instance.getMapTile(tx,ty);if(tile==null)continue;
                    Vector3 screen=Camera.main.WorldToScreenPoint(TilemapManager.instance.gameTileMap.GetCellCenterWorld(new Vector3Int(tx,ty,0))+Vector3.up*tile.height);
                    if(screen.z<=0)throw new InvalidOperationException("Own building is behind map camera.");
                    minX=Math.Min(minX,screen.x);maxX=Math.Max(maxX,screen.x);minY=Math.Min(minY,screen.y);maxY=Math.Max(maxY,screen.y);
                }
            return new ScreenRect(minX-12,minY-12,maxX+12,maxY+12);
        }
        private static object CastleModel()=>GameXAMLManagerAPI.Instance.FindGlobalElement("CastlePlannerBlueprintHud")?.DataContext;
        private static T ReadProperty<T>(object model,string name)
        {
            var property=model?.GetType().GetProperty(name,BindingFlags.Public|BindingFlags.Instance);
            if(property==null || !typeof(T).IsAssignableFrom(property.PropertyType))throw new MissingMemberException("CastlePlanner HUD contract: "+name);
            return (T)property.GetValue(model,null);
        }
        private static void ResumeBriefing()
        {if(MainViewModel.viewModelLoaded && MainViewModel.Instance.Show_HUD_Briefing && MainViewModel.Instance.ButtonBriefingResumeCommand.CanExecute(null))MainViewModel.Instance.ButtonBriefingResumeCommand.Execute(null);}
        private static string Ids(IEnumerable<int> ids)=>string.Join(",",ids.Distinct().OrderBy(x=>x));
        private static unsafe int OwnBuildingCount(int actor)
        {int count=0;foreach(var building in GameBuildingManagerAPI.Instance.GetBuildingsAsSpan())if(building.r_AliveState==AliveState.IsAlive && building.r_PlayerIdOwner==actor)count++;return count;}
        private static Wolf1Wall[] ReadWalls(int actor)
        {
            var api=GameTileManagerAPI.Instance;var logic=api.GetLogicLayer();var owners=api.GetWallOwnerLayer();var units=api.GetChimpLayer();
            var structures=api.GetStructureLayer();var heights=api.GetHeightLayer();var defaults=api.GetDefaultHeightLayer();var display=api.GetMiscDisplayLayer();
            var result=new List<Wolf1Wall>();
            for(int tile=0;tile<logic.Length;tile++)
            {
                var flags=(TilePropertyFlag)(uint)logic[tile];
                if((flags&TilePropertyFlag.IsWall)==0 || (flags&TilePropertyFlag.GoodsyardRelated)!=0 || structures[tile]!=0 || (owners[tile]&7)+1!=actor)continue;
                var point=api.GetTileVectorFromId(tile);if(!api.IsTileInsideMapBounds(point.X,point.Y))continue;
                result.Add(new Wolf1Wall {Tile=tile,X=point.X,Y=point.Y,Owner=actor,Flags=(uint)flags,Unit=units[tile],Structure=structures[tile],
                    Height=heights[tile],DefaultHeight=defaults[tile],Pending=display[tile]&0x400});
            }
            return result.ToArray();
        }
    }

    // Independent pre-test coverage oracle. It queries actual native eligibility once before
    // profiling, not the production candidate/hull selector being tested.
    internal sealed unsafe partial class NativeDemolition
    {
        internal Wolf1Expectation DiagExpected(Wolf1Wall[] walls,int actor,ushort filters)
        {
            lock(EngineGate)
            {
                var entries=new List<Wolf1Eligibility>();var tiles=GameTileManagerAPI.Instance;var decorations=DecorationsByTile();
                foreach(var wall in walls)
                {
                    var target=WallAt(wall.Tile,wall.X,wall.Y,tiles.GetTilePropertyFlag(wall.Tile),actor);
                    bool matches=target!=null && (filters&(1<<(int)target.Group))!=0;
                    bool permitted=matches && Permitted(target,actor);
                    bool closure=permitted && ClosureOwn(target,actor,out _,decorations);
                    entries.Add(new Wolf1Eligibility {Tile=wall.Tile,Target=target?.Copy(),Allowed=closure,
                        Reason=target==null?"unsupported-or-changed":!matches?"filter-excluded":!permitted?"native-permission-denied":!closure?"protected-closure":"native-eligible"});
                }
                var buildings=new List<DemolitionTarget>();var span=GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
                for(int index=0;index<span.Length;index++)
                    if(span[index].r_PlayerIdOwner==actor && span[index].r_AliveState==AliveState.IsAlive)
                    {var target=Building(index+1);if(target!=null)buildings.Add(target.Copy());}
                return new Wolf1Expectation {Walls=entries.ToArray(),Buildings=buildings.ToArray()};
            }
        }
        internal string[] DiagWallStates(DemolitionTarget[] targets)
        {
            lock(EngineGate)
            {
                var tiles=GameTileManagerAPI.Instance;var records=new List<string>();
                foreach(var target in targets)
                {
                    if(target.Kind!=TargetKind.Wall)continue;
                    var flags=tiles.GetTilePropertyFlag(target.TileId);
                    var live=WallAt(target.TileId,target.X,target.Y,flags);
                    string change=live==null?"missing-wall":target.Owner!=live.Owner?"owner":target.Type!=live.Type?"signature":target.Flags!=live.Flags?"flags":"same";
                    records.Add("identity="+target.Identity+" expectedType="+target.Type+" expectedFlags=0x"+target.Flags.ToString("X8",CultureInfo.InvariantCulture)+
                        " liveFlags=0x"+((uint)flags).ToString("X8",CultureInfo.InvariantCulture)+" owner="+((tiles.GetWallOwnerLayer()[target.TileId]&7)+1)+
                        " xy="+target.X+","+target.Y+" height="+tiles.GetHeightLayer()[target.TileId]+" defaultHeight="+tiles.GetDefaultHeightLayer()[target.TileId]+
                        " unit="+tiles.GetChimpLayer()[target.TileId]+" structure="+tiles.GetStructureLayer()[target.TileId]+" pending="+(tiles.GetMiscDisplayLayer()[target.TileId]&0x400)+" compare="+change);
                }
                return records.ToArray();
            }
        }
    }
}
