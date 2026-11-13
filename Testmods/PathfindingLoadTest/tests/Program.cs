using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;

// The old acceptance probe sampled at most twelve targets. This verifies that this
// fixture's actual production methods inspect the final identity/member in a 5000-unit set.
string source = File.ReadAllText(Path.Combine(args[0], "Testmods/PathfindingLoadTest/src/Fixture.cs"));
var syntax = CSharpSyntaxTree.ParseText(source).GetRoot();
var names = new[] { "AllLive", "SelectionMatches", "Observe", "RecordFailure", "RecordLookupFailure", "Require", "CheckRegion", "Free", "RejectRegion", "TryNextSpawnSite", "ValidateSpawnPosition", "RequireTargetFree", "ObserveKeepGuide", "WallWithinKeepGuide" };
string methods = string.Join("\n", names.Select(n => syntax.DescendantNodes().OfType<MethodDeclarationSyntax>()
    .Single(m => m.Identifier.ValueText == n).ToFullString()));
string wallSource = File.ReadAllText(Path.Combine(args[0], "Testmods/PathfindingLoadTest/src/Fixture.Wall.cs"));
methods += CSharpSyntaxTree.ParseText(wallSource).GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
    .Single(m => m.Identifier.ValueText == "LogWallOutcome").ToFullString();
string lobbySource = File.ReadAllText(Path.Combine(args[0], "Testmods/PathfindingLoadTest/src/Fixture.Lobby.cs"));
var lobbySyntax = CSharpSyntaxTree.ParseText(lobbySource).GetRoot();
methods += string.Join("\n", new[] { "ChooseKeepExtremes", "PlaceLobbyPlayer", "VerifyPinnedKeeps", "ValidWorldKeep" }.Select(name =>
    lobbySyntax.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.ValueText == name).ToFullString()));
string types = string.Join("\n", syntax.DescendantNodes().OfType<StructDeclarationSyntax>()
    .Where(t => t.Identifier.ValueText is "Identity" or "Outcome").Select(t => t.ToFullString()));
string fixture = """
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
enum eGoods { STORED_STONE_BLOCKS }
struct Point {public int X,Y;}
sealed class GameData {
 public static readonly GameData Instance=new();public int[] start_keep_location_order=new int[8];public int[,] Keep_Locations=new int[8,2];
}
sealed class GamePlayerManagerAPI {
 public const int MAX_PLAYERS=8;public static readonly GamePlayerManagerAPI Instance=new();public int KeepId=1,X=130,Y=70,RequestedOwner;
 public readonly Dictionary<int,Point> Positions=new();public readonly Dictionary<int,int> KeepIds=new();
 public int GetPlayerKeepId(int owner) {RequestedOwner=owner;return KeepIds.TryGetValue(owner,out int id)?id:KeepId;}
 public Point GetPlayerKeepPosition(int owner)=>Positions.TryGetValue(owner,out var position)?position:new Point{X=X,Y=Y};
 public int GetGoodAmount(int owner,eGoods good)=>80;
}
sealed class ManagedRange {public int Value=-1;public int GetValue()=>Value;}
sealed class GameBuildingManagerAPI {
 public static readonly GameBuildingManagerAPI Instance=new();public int TableRange=100,RequestedMap;
 public readonly ManagedRange KeepProximityOverride=new();
 public int GetKeepProximityRange(int mapSize) {RequestedMap=mapSize;return TableRange;}
}
[Flags] enum TilePropertyFlag { None=0,IsWall=1,IsBuilding=2 }
sealed class GameTileManagerAPI {
 public const int MAX_WIDTH=800,MAX_HEIGHT=800;public static readonly GameTileManagerAPI Instance=new();
 public ushort[] Occupied=new ushort[640000];public HashSet<int> Blocked=new(),Walls=new();public int[] Heights=new int[640000];
 public IntPtr Manager;public IntPtr GetTileManager()=>Manager;public int GetCurrentMapSize()=>500;
 public bool IsTileInsideMapBounds(int x,int y)=>x>=0&&y>=0&&x<800&&y<800;
 public int GetTileId(int x,int y)=>x+y*800;
 public bool IsValidTileId(int id)=>id>=0&&id<640000;
 public bool IsTileWalkableAndUnoccupied(int id)=>!Blocked.Contains(id);
 public ushort[] GetChimpLayer()=>Occupied;
 public TilePropertyFlag GetTilePropertyFlag(int id)=>Walls.Contains(id)?TilePropertyFlag.IsWall:Blocked.Contains(id)?TilePropertyFlag.IsBuilding:TilePropertyFlag.None;
 public int GetTileHeight(int id)=>Heights[id];
 public void Reset() {Array.Clear(Occupied);Blocked.Clear();Walls.Clear();Array.Fill(Heights,7);}
}
enum AliveState { None,NeedsInit,IsAlive }
enum eChimps { CHIMP_TYPE_SPEARMAN,Other }
unsafe struct GameUnit { public uint r_GlobalId;public int r_ControllableForPlayerId,r_TribeId; public AliveState r_AliveState;
 public eChimps r_UnitChimp;public int r_CurrentTilePositionX,r_CurrentTilePositionY,r_TargetTilePositionX,r_TargetTilePositionY,r_PathPlanLength,r_IsKilledByProjectile; }
unsafe struct GameTribe { public AliveState r_AliveState;public int r_PlayerIdOwner,r_UnitsInGroup; }
unsafe static class UnitAccess {
 public static GameUnit* Units;public static int Missing;public static bool TryGetById(int id,out GameUnit* unit,out int failure) { failure=0;unit=id>0&&id<=5000&&id!=Missing?Units+id-1:null;return unit!=null; }
 public static bool IsReallyAlive(GameUnit* unit)=>unit->r_AliveState==AliveState.IsAlive;
}
unsafe sealed class GameUnitManagerAPI {
 public static readonly GameUnitManagerAPI Instance=new GameUnitManagerAPI();public bool RawAvailable=true;
 public Span<GameUnit> GetUnitsAsSpan()=>RawAvailable?new Span<GameUnit>(UnitAccess.Units,5000):Span<GameUnit>.Empty;
}
struct Selected { public int UnitId; }
sealed class LocalSelectionSnapshot {
 public Selected[] Units;public int Count=>Units.Length;public Selected this[int index]=>Units[index];
}
static class LocalSelectionAPI {
 public static Selected[] Selection;public static bool Available=true;public static int RequestedOwner;
 public static bool TryCapture(int owner,out LocalSelectionSnapshot snapshot) {
  RequestedOwner=owner;snapshot=Available&&owner==1?new LocalSelectionSnapshot{Units=Selection}:null;return snapshot!=null;
 }
}
unsafe sealed class GameTribeManagerAPI { public static readonly GameTribeManagerAPI Instance=new GameTribeManagerAPI();
 public GameTribe* Tribe;public List<int> Members;public bool Known=true;
 public bool TryGetTribeById(int id,out GameTribe* record) { record=id==42&&Known?Tribe:null;return record!=null; }
 public bool GetUnits(int tribe,List<int> target) { target.AddRange(Members);return true; }
}
public unsafe class Probe {
 private int count=5000,owner=1,tribe,targetX=130,targetY=30;
 private int width=100,height=60,originX=20,originY=40,elevation=7,maxFreeCandidate;
 private int keepId=1,keepX=130,keepY=70,keepRadius=100;
 private bool lobbyPinned;private int pinnedOwner,pinnedRat,pinnedEast,pinnedWest,pinnedHumanRadarX,pinnedHumanRadarY,pinnedRatRadarX,pinnedRatRadarY;
 private readonly List<string> logs=new();private void log(string message)=>logs.Add(message);
 private readonly Dictionary<string,int> regionRejections=new();
 private readonly List<Identity> units=new List<Identity>();
 /* TYPES */
 /* METHODS */
 private static void Check(bool okay,string message) { if(!okay)throw new Exception(message); }
 private static void Throws(Action action,string reason) { bool caught=false;try{action();}catch(InvalidOperationException){caught=true;}Check(caught,reason); }
 public static void Geometry() {
  var api=GameTileManagerAPI.Instance;api.Reset();var f=new Probe();
  // Occupancy is a separate boundary from legal flat terrain. These 1000 blocked
  // positions leave exactly 5000 distinct legal candidates, rather than projecting spawns.
  for(int i=0;i<1000;i++)api.Occupied[api.GetTileId(20+i%100,40+i/100)]=17;
  api.Occupied[api.GetTileId(20,28)]=18; // occupied outer corner is not a spawn/wall/target
  var before=(ushort[])api.Occupied.Clone();
  Check(f.CheckRegion(20,40)&&f.maxFreeCandidate==5000,"exact 5000 with occupied outer corner rejected");
  int cursor=0,seen=0;var coords=new HashSet<int>();
  while(f.TryNextSpawnSite(ref cursor,out int x,out int y,out int h)) {
   Check(api.Occupied[api.GetTileId(x,y)]==0&&h==7&&coords.Add(api.GetTileId(x,y)),"occupied/duplicate spawn site");seen++;
  }
  Check(seen==5000&&cursor==6000,"bounded cursor did not visit exactly all available sites");
  Check(!f.TryNextSpawnSite(ref cursor,out _,out _,out _)&&cursor==6000,"exhausted cursor advanced/admitted");
  Check(before.SequenceEqual(api.Occupied),"geometry changed existing occupancy");
  api.Occupied[api.GetTileId(20,50)]=19;f=new Probe();
  Check(!f.CheckRegion(20,40)&&f.maxFreeCandidate==4999&&f.regionRejections.ContainsKey("spawn-capacity"),"4999 sites admitted or capacity diagnostic lost");
  api.Reset();f=new Probe();api.Occupied[api.GetTileId(130,50)]=17;
  Check(!f.CheckRegion(20,40)&&f.regionRejections.ContainsKey("occupied-wall-column"),"occupied wall column admitted");
  api.Reset();f=new Probe();api.Occupied[api.GetTileId(144,70)]=17;
  Check(!f.CheckRegion(20,40)&&f.regionRejections.ContainsKey("occupied-target"),"occupied destination admitted");
  api.Reset();f=new Probe();api.Blocked.Add(api.GetTileId(25,45));
  Check(!f.CheckRegion(20,40)&&f.regionRejections.ContainsKey("terrain-or-bounds"),"blocked terrain admitted");
  cursor=505;Throws(()=>f.TryNextSpawnSite(ref cursor,out _,out _,out _),"spawn silently tolerated changed terrain");
  api.Reset();f=new Probe();api.Heights[api.GetTileId(25,45)]=8;
  Check(!f.CheckRegion(20,40)&&f.regionRejections.ContainsKey("height"),"nonflat terrain admitted");
  api.Reset();f=new Probe{keepRadius=1000};Check(!f.CheckRegion(799,799)&&f.regionRejections.ContainsKey("corner-terrain-or-bounds"),"out of bounds region admitted");
  // The 1000-member shape keeps 1200 candidates with the same reservation rules.
  api.Reset();f=new Probe{count=1000,width=40,height=30};
  for(int i=0;i<200;i++)api.Occupied[api.GetTileId(20+i%40,40+i/40)]=17;
  Check(f.CheckRegion(20,40)&&f.maxFreeCandidate==1000,"1000 of1200 sites rejected");
  api.Reset();f=new Probe();f.RequireTargetFree();api.Occupied[api.GetTileId(f.targetX,f.targetY)]=17;
  Throws(()=>f.RequireTargetFree(),"new target occupancy admitted after preparation");
  api.Reset();api.Heights[api.GetTileId(f.targetX,f.targetY)]=8;Throws(()=>f.RequireTargetFree(),"changed target elevation admitted");
  var positions=new HashSet<int>();var identity=new Identity{Id=3,X=20,Y=40};
  ValidateSpawnPosition(identity,20,40,positions);
  Throws(()=>ValidateSpawnPosition(identity,20,40,positions),"actual coordinate duplicate admitted");
  Throws(()=>ValidateSpawnPosition(identity,21,40,new HashSet<int>()),"projected creation admitted");
 }
 public static void KeepGuide() {
  var players=GamePlayerManagerAPI.Instance;var buildings=GameBuildingManagerAPI.Instance;
  players.KeepId=1;players.X=130;players.Y=70;buildings.TableRange=100;
  foreach(int value in new[]{-1,0,60,150}) {
   buildings.KeepProximityOverride.Value=value;var f=new Probe();f.ObserveKeepGuide();
   Check(f.keepRadius==(value>0?Math.Min(100,value):100)&&f.keepX==130&&f.keepY==70&&players.RequestedOwner==1&&buildings.RequestedMap==500,"observed conservative guide mismatch");
  }
  players.KeepId=0;Throws(()=>new Probe().ObserveKeepGuide(),"missing own keep admitted");players.KeepId=1;
  buildings.TableRange=0;Throws(()=>new Probe().ObserveKeepGuide(),"nonpositive conservative guide admitted");buildings.TableRange=100;
  var a=new Probe{keepX=31};Check(a.WallWithinKeepGuide(20,40),"distance radius-1 rejected");
  a.keepX=30;Check(!a.WallWithinKeepGuide(20,40),"strict horizontal equality admitted");
  a=new Probe{keepY=127};Check(a.WallWithinKeepGuide(20,40),"top endpoint radius-1 rejected");
  a.keepY=128;Check(!a.WallWithinKeepGuide(20,40),"top endpoint equality admitted");
  a=new Probe{keepY=13};Check(a.WallWithinKeepGuide(20,40),"bottom endpoint radius-1 rejected");
  a.keepY=12;Check(!a.CheckRegion(20,40)&&a.regionRejections.ContainsKey("keep-range"),"bottom endpoint equality admitted/no reason");
  a=new Probe{keepId=0};Check(!a.WallWithinKeepGuide(20,40),"unobserved keep admitted");
  // Actual diagnostic method reads all three current status fields without resetting them.
  var api=GameTileManagerAPI.Instance;api.Reset();api.Manager=Marshal.AllocHGlobal(0x204E708);
  try {
   Marshal.WriteInt32(api.Manager,0x204E6E4,1);Marshal.WriteInt32(api.Manager,0x204E6FC,0);Marshal.WriteInt32(api.Manager,0x204E704,18);
   api.Walls.Add(api.GetTileId(130,40));api.Walls.Add(api.GetTileId(130,42));
   a=new Probe();a.LogWallOutcome("test",130,40,42);string entry=a.logs.Single();
   Check(entry.Contains("invalid=1")&&entry.Contains("queryBlocked=0")&&entry.Contains("reason=18")&&entry.Contains("reasonMayBeStale=true")&&entry.Contains("stoneAfter=80")&&entry.Contains("expectedCells=3")&&entry.Contains("wallFlagCells=2")&&entry.Contains("firstHeight=7"),"bounded wall outcome observations lost");
   Check(Marshal.ReadInt32(api.Manager,0x204E6E4)==1&&Marshal.ReadInt32(api.Manager,0x204E704)==18,"diagnostic reset native state");
  } finally {Marshal.FreeHGlobal(api.Manager);api.Manager=IntPtr.Zero;}
 }
 public static void LobbyPlacement() {
  int[,] locations=new int[8,2];for(int i=0;i<8;i++)locations[i,0]=-1;
  locations[0,0]=231;locations[0,1]=433;locations[3,0]=629;locations[3,1]=404;locations[7,0]=629;locations[7,1]=405;
  ChooseKeepExtremes(locations,out int east,out int west);Check(east==7&&west==0,"coordinate extrema/tie order mismatch");
  locations[6,0]=800;locations[6,1]=-1;ChooseKeepExtremes(locations,out east,out west);Check(east==7,"negative radar coordinate admitted");
  var duplicate=new int[,]{{12,34},{12,34}};Throws(()=>ChooseKeepExtremes(duplicate,out _,out _),"same-coordinate keeps admitted");
  Throws(()=>ChooseKeepExtremes(new int[,]{{-1,0},{800,-1}},out _,out _),"invalid keep set admitted");
  // The callback is an abstract swap boundary, not a copied Vanilla UI implementation.
  // These actual helpers must recompute the Rat source after the human displaces it.
  int[] order=Enumerable.Repeat(-10,8).ToArray();order[0]=2;order[7]=7;int swaps=0;
  Action<int,int> swap=(a,b)=>{swaps++;(order[a],order[b])=(order[b],order[a]);};
  PlaceLobbyPlayer(order,3,7,swap);PlaceLobbyPlayer(order,8,0,swap);
  Check(swaps==1&&order[7]==2&&order[0]==7,"first swap displacement not respected");
  PlaceLobbyPlayer(order,3,7,swap);PlaceLobbyPlayer(order,8,0,swap);Check(swaps==1,"idempotent pin generated swaps");
  order=Enumerable.Repeat(-10,8).ToArray();order[3]=2;order[5]=7;swaps=0;
  PlaceLobbyPlayer(order,3,7,swap);PlaceLobbyPlayer(order,8,0,swap);
  Check(swaps==2&&order[7]==2&&order[0]==7&&order[3]==-10&&order[5]==-10,"empty-target swap mapping incorrect");
  Throws(()=>PlaceLobbyPlayer(order,3,1,(a,b)=>{}),"ignored UI swap admitted");
  order[2]=2;Throws(()=>PlaceLobbyPlayer(order,3,1,swap),"duplicate participant admitted");
  Throws(()=>PlaceLobbyPlayer(order,0,1,swap),"invalid participant id admitted");
  var players=GamePlayerManagerAPI.Instance;players.Positions.Clear();players.KeepIds.Clear();players.KeepId=1;
  players.Positions[3]=new Point{X=629,Y=405};players.Positions[8]=new Point{X=242,Y=429};
  players.KeepIds[3]=101;players.KeepIds[8]=202;
  var data=GameData.Instance;data.start_keep_location_order=Enumerable.Repeat(255,8).ToArray();
  data.start_keep_location_order[7]=2;data.start_keep_location_order[0]=7;
  data.Keep_Locations[7,0]=174;data.Keep_Locations[7,1]=175;data.Keep_Locations[0,0]=32;data.Keep_Locations[0,1]=52;
  var f=new Probe{owner=3,lobbyPinned=true,pinnedOwner=3,pinnedRat=8,pinnedEast=7,pinnedWest=0,
   pinnedHumanRadarX=174,pinnedHumanRadarY=175,pinnedRatRadarX=32,pinnedRatRadarY=52};
  // Native order is authoritative for participants; radar pixels and world tiles are independent.
  f.VerifyPinnedKeeps();players.Positions[3]=new Point{X=628,Y=405};f.VerifyPinnedKeeps();
  data.start_keep_location_order[7]=7;Throws(()=>f.VerifyPinnedKeeps(),"wrong returned player mapping admitted");data.start_keep_location_order[7]=2;
  data.start_keep_location_order[4]=2;Throws(()=>f.VerifyPinnedKeeps(),"duplicate returned participant admitted");data.start_keep_location_order[4]=255;
  players.Positions[3]=new Point{X=800,Y=405};Throws(()=>f.VerifyPinnedKeeps(),"out-of-map world keep admitted");
  players.Positions[3]=players.Positions[8];Throws(()=>f.VerifyPinnedKeeps(),"coincident world keeps admitted");
  players.Positions[3]=new Point{X=629,Y=405};players.KeepIds[3]=0;Throws(()=>f.VerifyPinnedKeeps(),"missing world keep admitted");players.KeepIds[3]=101;
  f.owner=1;Throws(()=>f.VerifyPinnedKeeps(),"different local owner admitted");
  players.Positions.Clear();players.KeepIds.Clear();
 }
 public static void Run() {
  LobbyPlacement();KeepGuide();Geometry();
  var f=new Probe();var array=new GameUnit[5000];
  fixed(GameUnit* ptr=array) {
   UnitAccess.Units=ptr;var record=new GameTribe{r_AliveState=AliveState.IsAlive,r_PlayerIdOwner=1,r_UnitsInGroup=5000};
   GameTribeManagerAPI.Instance.Tribe=&record;GameTribeManagerAPI.Instance.Members=Enumerable.Range(1,5000).ToList();
   LocalSelectionAPI.Selection=Enumerable.Range(1,5000).Select(id=>new Selected{UnitId=id}).ToArray();
   for(int i=0;i<5000;i++) {
    array[i]=new GameUnit{r_GlobalId=(uint)i+101,r_ControllableForPlayerId=1,r_TribeId=42,r_AliveState=AliveState.IsAlive,r_UnitChimp=eChimps.CHIMP_TYPE_SPEARMAN,
     r_CurrentTilePositionX=10,r_CurrentTilePositionY=20,r_TargetTilePositionX=10,r_TargetTilePositionY=20};
    f.units.Add(new Identity{Id=i+1,Global=(uint)i+101,X=10,Y=20,BeforeTargetX=10,BeforeTargetY=20});
   }
   Check(f.SelectionMatches()&&f.tribe==42&&LocalSelectionAPI.RequestedOwner==1,"full local-owner selection was rejected");
   LocalSelectionAPI.Available=false;Check(!f.SelectionMatches(),"unavailable completed local selection admitted");LocalSelectionAPI.Available=true;
   LocalSelectionAPI.Selection[4999].UnitId=9999;Check(!f.SelectionMatches(),"foreign last selection admitted");
   LocalSelectionAPI.Selection[4999].UnitId=5000;
   var complete=LocalSelectionAPI.Selection;LocalSelectionAPI.Selection=complete.Take(4999).ToArray();
   Check(!f.SelectionMatches(),"short completed selection admitted");LocalSelectionAPI.Selection=complete;
   GameTribeManagerAPI.Instance.Members[4999]=9999;Check(!f.SelectionMatches(),"foreign last tribe member admitted");GameTribeManagerAPI.Instance.Members[4999]=5000;
   array[4999].r_TribeId=41;Check(!f.SelectionMatches(),"last unit in different tribe admitted");array[4999].r_TribeId=42;
   record.r_UnitsInGroup=4999;Check(!f.SelectionMatches(),"short group admitted");record.r_UnitsInGroup=5000;
   GameTribeManagerAPI.Instance.Known=false;Check(!f.SelectionMatches(),"unknown group admitted");GameTribeManagerAPI.Instance.Known=true;
   array[4999].r_AliveState=AliveState.NeedsInit;Check(!f.AllLive(),"uninitialized last unit admitted");array[4999].r_AliveState=AliveState.IsAlive;
   array[4999].r_GlobalId++;Throws(()=>f.AllLive(),"recycled last identity admitted");array[4999].r_GlobalId--;
   array[4999].r_ControllableForPlayerId=2;Throws(()=>f.AllLive(),"foreign last owner admitted");array[4999].r_ControllableForPlayerId=1;
   array[4999].r_UnitChimp=eChimps.Other;Throws(()=>f.AllLive(),"wrong last type admitted");array[4999].r_UnitChimp=eChimps.CHIMP_TYPE_SPEARMAN;
   var state=f.Observe();Check(state.Live==5000&&state.Moved==0&&state.TargetChanged==0&&state.HasPath==0,"stationary baseline misreported");
   array[4999].r_CurrentTilePositionX=130;array[4999].r_CurrentTilePositionY=30;
   array[4999].r_TargetTilePositionX=130;array[4999].r_PathPlanLength=7;
   state=f.Observe();Check(state.Moved==1&&state.TargetChanged==1&&state.HasPath==1&&state.Arrived==1,"last member outcome omitted");
   array[4999].r_ControllableForPlayerId=2;state=f.Observe();Check(state.Invalid==1&&state.Live==4999&&state.OwnerMismatch==1&&state.GlobalMismatch==0&&state.Details.Contains("actualOwner=2"),"foreign outcome cause lost");array[4999].r_ControllableForPlayerId=1;
   array[4999].r_UnitChimp=eChimps.Other;state=f.Observe();Check(state.Invalid==1&&state.Live==4999&&state.TypeMismatch==1&&state.Details.Contains("actualType=Other"),"changed type outcome cause lost");array[4999].r_UnitChimp=eChimps.CHIMP_TYPE_SPEARMAN;
   array[4999].r_GlobalId++;state=f.Observe();Check(state.Invalid==1&&state.Live==4999&&state.GlobalMismatch==1&&state.Details.Contains("id=5000")&&state.Details.Contains("pos=130,30")&&state.Details.Contains("target=130,20"),"recycled outcome cause/coordinates lost");array[4999].r_GlobalId--;
   UnitAccess.Missing=5000;state=f.Observe();Check(state.Invalid==1&&state.LookupFailures==1&&state.Details.Contains("raw-slot")&&state.Details.Contains("actualGlobal=5100"),"lookup raw-slot cause lost");
   GameUnitManagerAPI.Instance.RawAvailable=false;state=f.Observe();Check(state.Invalid==1&&state.LookupFailures==1&&state.Details.Contains("actual=unavailable"),"unavailable lookup cause lost");GameUnitManagerAPI.Instance.RawAvailable=true;UnitAccess.Missing=0;
   array[4999].r_AliveState=AliveState.None;state=f.Observe();Check(state.Dead==1&&state.Live==4999&&state.Details.Contains("alive=None"),"dead outcome omitted");
   for(int i=0;i<12;i++){array[i].r_GlobalId++;array[i].r_ControllableForPlayerId=2;}
   state=f.Observe();Check(state.Invalid==12&&state.GlobalMismatch==12&&state.OwnerMismatch==12&&state.DetailCount==8&&state.Details.Contains("id=8 ")&&!state.Details.Contains("id=9 "),"diagnostic limit changed acceptance or lost overlapping causes");
  }
 }
}
""";
fixture = fixture.Replace("/* TYPES */", types).Replace("/* METHODS */", methods);
var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
    .Select(p => MetadataReference.CreateFromFile(p));
var compilation = CSharpCompilation.Create("PathfindingLoadAcceptance", new[] { CSharpSyntaxTree.ParseText(fixture) }, references,
    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
using var stream = new MemoryStream();
var result = compilation.Emit(stream);
if (!result.Success) throw new Exception(string.Join("\n", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
Assembly.Load(stream.ToArray()).GetType("Probe")!.GetMethod("Run")!.Invoke(null, null);
Console.WriteLine("PASS: actual geometry5000/capacity/skip/exhaustion/terrain/wall/target/coordinate guards; actual fixture admission and all-5000 member outcome methods, including final identity/type/owner/member failures.");
