using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace AreaDemolitionTest
{
    internal sealed unsafe class NativeDemolition
    {
        private readonly NativeBindings native;
        private volatile bool available = true;
        private readonly int wallPolicy;
        internal object EngineGate => native.EngineGate;
        internal bool Available => available;
        internal string Diagnostic { get; private set; }
        internal string Fingerprint { get; }
        internal event Action<string> DiagnosticProduced;
        private const uint WallIdentityMask = (uint)(TilePropertyFlag.IsWall | TilePropertyFlag.IsLowWall | TilePropertyFlag.IsStairs | TilePropertyFlag.CrenelatedLow);

        private NativeDemolition(NativeBindings native, ManualLogSource log)
        {
            this.native = native; wallPolicy = native.WallPolicy;
            Diagnostic = wallPolicy < 0 ? "Native demolition available; unknown wall refund patch: walls disabled." : "Native demolition available.";
            Fingerprint = NativeBindings.SupportedHash + ":demolition-v6:frozen-components-v1:storage-goods-granary-armoury-v1:proof-attempt-v1:dispatch-ticket-v1:registered-footprints,buildings,stockpiles,walls-vs-stairs,pitch,dug-moat,decorations10-15:wall-stable-identity-v1:wall=" + wallPolicy;
        }

        internal static bool TryCreate(CrusaderLibraryLoadContext context, ManualLogSource log, out NativeDemolition result)
        {
            result = null;
            try { result = new NativeDemolition(new NativeBindings(context, log), log); return true; }
            catch (Exception error)
            { log?.LogError(DateTime.Now.ToString("HH:mm:ss.fff") + " DEMOLITION_NATIVE_UNAVAILABLE " + error); return false; }
        }

        internal bool IsValidTile(int x, int y) => available && GameTileManagerAPI.Instance.IsTileInsideMapBounds(x, y);

        private int[] Neighbours(int tile) => TileNeighbours(tile,false);
        private int[] CardinalNeighbours(int tile) => TileNeighbours(tile,true);
        private int[] TileNeighbours(int tile,bool cardinal)
        {
            var result=new int[cardinal?4:8];int count=FillTileNeighbours(tile,cardinal,result);
            if(count!=result.Length)Array.Resize(ref result,count);return result;
        }
        private int FillTileNeighbours(int tile,bool cardinal,int[] result)
        {
            var tiles=GameTileManagerAPI.Instance;
            if (!tiles.IsValidTileId(tile)) return 0;
            var point=tiles.GetTileVectorFromId(tile);
            // DF9D0 validates the initial coordinates, but intermediate nodes use packed bounds/border bits only.
            if (cardinal ? (uint)tile>=320000 || point.Y>=800 : !IsValidTile(point.X,point.Y)) return 0;
            var deltas=tiles.TileManager.PackedNeighborTileDeltas;
            int count=0;
            for (int direction=0;direction<8;direction+=cardinal?2:1)
            {
                int neighbour=tile+deltas[point.Y*8+direction];
                if (!tiles.IsValidTileId(neighbour) || cardinal && (uint)neighbour>=320000) continue;
                var next=tiles.GetTileVectorFromId(neighbour);
                if (cardinal || IsValidTile(next.X,next.Y)) result[count++]=neighbour;
            }
            return count;
        }

        private bool IsBorder(int tile) => (GameTileManagerAPI.Instance.GetTilePropertyFlag(tile)&(TilePropertyFlag.RealityEdge|TilePropertyFlag.MapBorder))!=0;

        // Conservative one-neighbour inverse covers every 459D0 view direction and flat-view path.
        internal static int[] ExpandNeighbourRequests(IEnumerable<int> occupied,Func<int,int[]> neighbours)
        {
            var result=new HashSet<int>();
            foreach (int tile in occupied)
            { result.Add(tile);foreach (int neighbour in neighbours(tile)) result.Add(neighbour); }
            return result.OrderBy(t=>t).ToArray();
        }

        // 90830 refreshes StructureGrid[requested] before 79B90. A neighbouring ordinary
        // building can therefore never be reached from plain grass; only stockpile connectors
        // have the explicit structure-free neighbour fallback in 79B90/64F80.
        internal static bool BuildingRequestMatches(PreviewIndex index,DemolitionTarget root,int requestedSlot,bool stockpileRelated,IEnumerable<int> neighbourSlots)
        {
            if (requestedSlot!=0)
            {
                var requested=index.Canonical(index.Target(requestedSlot));
                return SameRootIdentity(requested,root);
            }
            if (!stockpileRelated || (eStructs)root.Type!=eStructs.STRUCT_GOODS_YARD) return false;
            foreach (int slot in neighbourSlots)
            {
                var candidate=index.Canonical(index.Target(slot));
                if (candidate!=null && candidate.Type==(int)eStructs.STRUCT_GOODS_YARD && SameRootIdentity(candidate,root)) return true;
            }
            return false;
        }

        private static bool SameRootIdentity(DemolitionTarget left,DemolitionTarget right) => left!=null && right!=null && left.Kind==right.Kind &&
            left.Slot==right.Slot && left.GlobalId==right.GlobalId && left.TileId==right.TileId && left.TaskIndex==right.TaskIndex;

        // Native DF9D0 starts depth1 and inspects at most depth4: three cardinal edges.
        // Reverse traversal tests the current node's border bit (the forward edge's destination).
        internal static int[] DecorationRequestTiles(int tile,Func<int,int[]> neighbours,Func<int,bool> border) =>
            DecorationTiles(tile,neighbours,border,true);
        internal static int[] DecorationSearchTiles(int tile,Func<int,int[]> neighbours,Func<int,bool> border) =>
            DecorationTiles(tile,neighbours,border,false);
        private static int[] DecorationTiles(int tile,Func<int,int[]> neighbours,Func<int,bool> border,bool reverse)
        {
            var result=new List<int>(25) { tile };var depths=new List<int>(25) { 0 };var seen=new HashSet<int> { tile };
            for (int at=0;at<result.Count;at++)
            {
                if (depths[at]==3 || reverse && border(result[at])) continue;
                foreach (int neighbour in neighbours(result[at]))
                {
                    if ((!reverse && border(neighbour)) || !seen.Add(neighbour)) continue;
                    result.Add(neighbour);depths.Add(depths[at]+1);
                }
            }
            return result.ToArray();
        }

        internal static int FirstOwnDecorationSlot(int head,GameProjectile* projectiles,int count,int actor)
        {
            int slot=head;
            for (int visited=0;visited<9 && slot!=0;visited++)
            {
                if (slot<0 || slot>=count) return 0;
                var record=projectiles+slot;
                if (DecorationOwner(record)==actor && IsDecorationType(record->r_ProjectileType)) return slot;
                int next=(short)record->N000053FA_2;
                if (next==slot) break;
                slot=next;
            }
            return 0;
        }

        private int FirstOwnDecorationAt(int tile,int actor)
        {
            var projectiles=GameProjectileManagerAPI.Instance.GetProjectilesAsSpan();
            fixed (GameProjectile* records=projectiles)
                return FirstOwnDecorationSlot(GameTileManagerAPI.Instance.GetFlyLayer()[tile],records,projectiles.Length,actor);
        }

        private DemolitionTarget GroundTargetAt(int tile,TargetKind kind,int actor)
        {
            var tiles=GameTileManagerAPI.Instance;
            if (!tiles.IsValidTileId(tile)) return null;
            var point=tiles.GetTileVectorFromId(tile);
            if (!IsValidTile(point.X,point.Y)) return null;
            var flags=tiles.GetTilePropertyFlag(tile);
            return kind==TargetKind.Wall?WallAt(tile,point.X,point.Y,flags,actor):MoatAt(tile,point.X,point.Y,flags,actor);
        }

        private NativeBindings.RequestState BuildRequestState(int tile,int x,int y)
        {
            var tiles=GameTileManagerAPI.Instance;var flags=tiles.GetTilePropertyFlag(tile);
            int building=tiles.GetStructureLayer()[tile],wall=((uint)flags&0x102)==0x100?tile:0,effective=tile;
            if (tiles.GetPathConnectionLayer()[tile]==0 || (flags&TilePropertyFlag.CrenelatedLow)!=0)
            {
                int direction=(native.ViewDirection+4)&7;
                int neighbour=tile+tiles.TileManager.PackedNeighborTileDeltas[y*8+direction];
                var type=global::APIShared.BuildingAccess.TryGetById(building,out var b, out _)?b->r_BuildingType:default;
                effective=EffectiveRequestTile(tile,flags,tiles.GetPathConnectionLayer()[tile],neighbour,
                    tiles.IsValidTileId(neighbour)?tiles.GetTilePropertyFlag(neighbour):0,type);
            }
            int ex=0,ey=0,pitch=0;uint moat=0;bool flat=native.Flattened;
            if (effective>0 && tiles.IsValidTileId(effective))
            {
                var point=tiles.GetTileVectorFromId(effective);ex=point.X;ey=point.Y;
                int selected=flat?tile:effective;
                var selectedFlags=tiles.GetTilePropertyFlag(selected);
                var selectedPoint=tiles.GetTileVectorFromId(selected);
                if ((selectedFlags&TilePropertyFlag.IsMoat)!=0 && tiles.TryGetMoatWorkTaskAtTile(selectedPoint.X,selectedPoint.Y,out _)) moat=(uint)TilePropertyFlag.IsMoat;
                if ((selectedFlags&TilePropertyFlag.PitchTrap)!=0)
                    foreach (var item in PitchByTile()) if (item.Key==selected) { pitch=item.Value.Slot;break; }
            }
            else effective=0;
            // 8AAF0 resets +68 to requested after 459D0 prepared the other selectors.
            return new NativeBindings.RequestState(x,y,tile,building,wall,tile,ex,ey,moat,pitch,flat);
        }

        internal static int EffectiveRequestTile(int tile,TilePropertyFlag flags,int pathConnection,int neighbour,TilePropertyFlag neighbourFlags,eStructs type)
        {
            if (pathConnection!=0 && (flags&TilePropertyFlag.CrenelatedLow)==0 || (flags&TilePropertyFlag.IsMoat)!=0) return tile;
            if ((flags&TilePropertyFlag.CrenelatedLow)!=0) return (neighbourFlags&TilePropertyFlag.IsWall)!=0?neighbour:tile;
            if ((flags&TilePropertyFlag.IsBuilding)!=0 && CursorTowerType(type)) return (neighbourFlags&TilePropertyFlag.IsElevated)!=0?neighbour:tile;
            return 0;
        }

        private static bool CursorTowerType(eStructs type)
        {
            switch (type)
            {
                case eStructs.STRUCT_KEEP_ONE:case eStructs.STRUCT_KEEP_TWO:case eStructs.STRUCT_KEEP_THREE:
                case eStructs.STRUCT_KEEP_FOUR:case eStructs.STRUCT_KEEP_FIVE:
                case eStructs.STRUCT_TOWER:case eStructs.STRUCT_TOWER1:case eStructs.STRUCT_TOWER2:
                case eStructs.STRUCT_TOWER3:case eStructs.STRUCT_TOWER4:case eStructs.STRUCT_TOWER5:return true;
                default:return false;
            }
        }

        internal DemolitionTarget AtTile(int x, int y, int actor)
        {
            if (!available || actor < 1 || actor > 8) return null;
            lock (EngineGate)
            {
                if (!IsValidTile(x,y) || (uint)native.ViewDirection>=8) return null;
                var tiles=GameTileManagerAPI.Instance;
                int tile=tiles.GetTileId(x,y);
                var decorations=DecorationsByTile();
                // Same BFS/linked-list priority as 90830 -> 7A340, without touching pathfinder scratch.
                foreach (int nearby in DecorationSearchTiles(tile,CardinalNeighbours,IsBorder))
                {
                    int slot=FirstOwnDecorationAt(nearby,actor);
                    if (slot==0) continue;
                    var decoration=Decoration(slot);
                    return decoration!=null && Permitted(decoration,actor) && ClosureOwn(decoration,actor,out _,decorations)?decoration:null;
                }
                var request=BuildRequestState(tile,x,y);
                if (!native.QueryRequested(request,actor,out int selected,out bool wall,out bool moat,out int wallTile,out int moatTile)) return null;
                DemolitionTarget target=null;
                if (moat) target=GroundTargetAt(moatTile,TargetKind.DugMoat,actor);
                else if (wall) target=GroundTargetAt(wallTile,TargetKind.Wall,actor);
                else if (selected<0) target=Pitch(-selected);
                else if (selected>0) target=CanonicalBuilding(Building(selected,false));
                return target!=null && target.Owner==actor && Permitted(target,actor) && ClosureOwn(target,actor,out _,decorations)?target:null;
            }
        }

        internal static int SelectUnitHover(int firstUnit,int buildingDepth,int troopDepth,bool selectionOn) =>
            firstUnit>0 && (buildingDepth < -32766 || selectionOn || buildingDepth<=troopDepth)?firstUnit:0;

        internal bool VanillaSingle(int x,int y,int actor,int firstUnit,int buildingDepth,int troopDepth,bool selectionOn,Action delete)
        {
            if (!available || delete==null || actor<1 || actor>8 || firstUnit<0) return false;
            lock (EngineGate)
            {
                if (!IsValidTile(x,y) || (uint)native.ViewDirection>=8) return false;
                int unit=SelectUnitHover(firstUnit,buildingDepth,troopDepth,selectionOn);
                if (unit>0 && !GameUnitManagerAPI.Instance.IsValidId(unit)) return false;
                int tile=GameTileManagerAPI.Instance.GetTileId(x,y);
                native.ScopeVanillaSingle(BuildRequestState(tile,x,y),unit,delete);
                return true;
            }
        }

        // One copied metadata/compound/decor pass per preview snapshot, shared by projection and quoting.
        // This is advisory preview data; MakeBatches/Apply never use it as mutation authority.
        internal sealed class PreviewSnapshot
        {
            internal int Actor;
            internal SelectionSpec Selection;
            internal PreviewIndex Index;
            internal PreviewHitCandidate[] HitCandidates=Array.Empty<PreviewHitCandidate>();
            internal readonly HashSet<DemolitionTarget> Roots=new HashSet<DemolitionTarget>();
            internal readonly Dictionary<int,DemolitionTarget> Geometry=new Dictionary<int,DemolitionTarget>();
            internal Dictionary<int,DemolitionTarget> Pitch;
            internal Dictionary<int,List<DemolitionTarget>> Decorations;
        }

        internal readonly struct BuildingMember
        {
            internal BuildingMember(int slot,int owner,AliveState state,uint compound,DemolitionTarget target)
            { Slot=slot;Owner=owner;State=state;Compound=compound;Target=target; }
            internal readonly int Slot,Owner;
            internal readonly AliveState State;
            internal readonly uint Compound;
            internal readonly DemolitionTarget Target;
        }

        // Pure managed index is also used by offline equivalence/performance fixtures.
        internal sealed class PreviewIndex
        {
            private readonly Dictionary<int,BuildingMember> members=new Dictionary<int,BuildingMember>();
            private readonly Dictionary<uint,List<BuildingMember>> compounds=new Dictionary<uint,List<BuildingMember>>();
            internal PreviewIndex(IEnumerable<BuildingMember> source)
            {
                foreach (var member in source) Add(member);
                foreach (var group in compounds.Values) group.Sort((left,right)=>left.Slot.CompareTo(right.Slot));
            }
            internal void Add(BuildingMember member)
            {
                members[member.Slot]=member;
                if(member.Compound==0)return;
                if(!compounds.TryGetValue(member.Compound,out var group))compounds.Add(member.Compound,group=new List<BuildingMember>());
                group.Add(member);
            }
            internal IEnumerable<BuildingMember> Members => members.Values;
            internal DemolitionTarget Target(int slot) => members.TryGetValue(slot,out var member)?member.Target:null;
            internal DemolitionTarget Canonical(DemolitionTarget target)
            {
                if (target==null || target.Kind!=TargetKind.Building) return target;
                if (IsReserved((eStructs)target.Type))
                {
                    var parent=Target(target.TaskIndex&0xFFFF);
                    return parent!=null && parent.Flags!=0 && parent.Flags==target.Flags && parent.Owner==target.Owner && !IsReserved((eStructs)parent.Type) ? parent:null;
                }
                if ((eStructs)target.Type!=eStructs.STRUCT_GOODS_YARD && (eStructs)target.Type!=eStructs.STRUCT_QUARRYPILE || target.Flags==0) return target;
                if (!compounds.TryGetValue(target.Flags,out var group)) return target;
                eStructs wanted=(eStructs)target.Type==eStructs.STRUCT_GOODS_YARD?eStructs.STRUCT_GOODS_YARD:eStructs.STRUCT_QUARRY;
                foreach (var member in group)
                    if (member.State==AliveState.IsAlive && member.Owner==target.Owner && member.Target!=null && member.Target.Type==(int)wanted) return member.Target;
                return target;
            }
            internal bool TryCompound(int slot,int actor,out int[] slots)
            {
                slots=Array.Empty<int>();
                if (!members.TryGetValue(slot,out var root) || root.Owner!=actor || root.Target==null) return false;
                if (root.Compound==0) { slots=new[]{slot};return true; }
                if (!compounds.TryGetValue(root.Compound,out var group)) return false;
                var result=new List<int>(group.Count);
                foreach (var member in group)
                {
                    if (member.Owner!=actor) return false;
                    if (member.State==AliveState.MarkedForDeletion) continue;
                    if (member.State!=AliveState.IsAlive || member.Target==null) return false;
                    result.Add(member.Slot);
                }
                slots=result.ToArray();return true;
            }
        }

        private static bool WantsBuildings(SelectionSpec selection) => selection.SameType ? selection.Start?.Kind==TargetKind.Building :
            (selection.Filters&(((1<<(int)TargetGroup.Pitch)-1)|(1<<(int)TargetGroup.Stockpiles)|(1<<(int)TargetGroup.Walls)))!=0;

        internal sealed class PreviewPreparation
        {
            internal readonly PreviewSnapshot Snapshot;
            internal readonly List<BuildingMember> Members=new List<BuildingMember>();
            internal readonly Dictionary<string,CandidatePreparation> Candidates=new Dictionary<string,CandidatePreparation>(StringComparer.Ordinal);
            internal readonly List<CandidatePreparation> Ordered=new List<CandidatePreparation>();
            internal readonly Dictionary<int,int[]> Neighbours=new Dictionary<int,int[]>();
            internal int Phase,At,Limit,CellAt,TargetAt;
            internal CandidatePreparation Active;
            internal int[] Cells;
            internal List<DemolitionTarget>[] DecorationGroups;
            internal PreviewHitCandidate[] Result;
            internal PreviewPreparation(int actor,SelectionSpec selection)
            { Snapshot=new PreviewSnapshot { Actor=actor,Selection=selection,Index=new PreviewIndex(Array.Empty<BuildingMember>()),Pitch=new Dictionary<int,DemolitionTarget>(),Decorations=new Dictionary<int,List<DemolitionTarget>>() }; }
        }
        internal sealed class CandidatePreparation
        {
            internal DemolitionTarget Root;
            internal readonly HashSet<int> Requests=new HashSet<int>(),Occupied=new HashSet<int>();
        }

        internal PreviewPreparation BeginPreviewPreparation(int actor,SelectionSpec selection) => new PreviewPreparation(actor,selection);

        internal bool TryStepPreviewPreparation(PreviewPreparation job,long deadlineStopwatch,out bool complete,out PreviewSnapshot snapshot)
        {
            complete=false;snapshot=null;
            if(job==null)throw new ArgumentNullException(nameof(job));
            if(job.Phase==8){complete=true;snapshot=job.Snapshot;return true;}
            if(!System.Threading.Monitor.TryEnter(EngineGate))return false;
            try
            {
                if(!available || job.Snapshot.Actor<1 || job.Snapshot.Actor>8)job.Phase=8;
                while(job.Phase!=8 && System.Diagnostics.Stopwatch.GetTimestamp()<deadlineStopwatch) StepPreparationUnit(job);
                complete=job.Phase==8;if(complete)snapshot=job.Snapshot;return true;
            }
            finally {System.Threading.Monitor.Exit(EngineGate);}
        }

        private CandidatePreparation PreparationCandidate(PreviewPreparation job,DemolitionTarget root)
        {
            string identity=root.Identity;
            if(!job.Candidates.TryGetValue(identity,out var candidate))
            { candidate=new CandidatePreparation {Root=root.Copy()};job.Candidates.Add(identity,candidate);job.Ordered.Add(candidate); }
            return candidate;
        }
        private int[] PreparationNeighbours(PreviewPreparation job,int tile)
        { if(!job.Neighbours.TryGetValue(tile,out var result))job.Neighbours.Add(tile,result=Neighbours(tile));return result; }
        private void PreparationRequest(PreviewPreparation job,CandidatePreparation candidate,int tile)
        {
            if(candidate.Root.Kind==TargetKind.Building)
            {
                var tiles=GameTileManagerAPI.Instance;int slot=tiles.GetStructureLayer()[tile];
                bool related=(tiles.GetTilePropertyFlag(tile)&TilePropertyFlag.GoodsyardRelated)!=0;
                if(!BuildingRequestMatches(job.Snapshot.Index,candidate.Root,slot,related,
                    slot==0 && related && (eStructs)candidate.Root.Type==eStructs.STRUCT_GOODS_YARD?
                    PreparationNeighbours(job,tile).Select(neighbour=>(int)tiles.GetStructureLayer()[neighbour]):Array.Empty<int>()))return;
            }
            candidate.Requests.Add(tile);
        }
        // One unit is a metadata slot, one footprint cell (at most8neighbours), or one root finalization.
        // No native span/pointer or monitor survives this call's frame boundary.
        private void StepPreparationUnit(PreviewPreparation job)
        {
            var snapshot=job.Snapshot;int actor=snapshot.Actor;var selection=snapshot.Selection;
            switch(job.Phase)
            {
                case 0:
                    job.Limit=WantsBuildings(selection)?GameBuildingManagerAPI.Instance.GetBuildingsAsSpan().Length:0;
                    job.At=0;job.Phase=1;break;
                case 1:
                    if(job.At>=job.Limit){job.At=0;job.Phase=2;break;}
                    var span=GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();int index=job.At++;
                    if(index>=span.Length)break;
                    ref var building=ref span[index];
                    if(building.r_AliveState==AliveState.None)break;
                    var member=new BuildingMember(index+1,building.r_PlayerIdOwner,building.r_AliveState,building.N0000178A,Building(index+1,false));
                    job.Members.Add(member);snapshot.Index.Add(member);break;
                case 2:
                    if(job.Active!=null)
                    {
                        if(job.CellAt<job.Cells.Length)
                        {
                            int tile=job.Cells[job.CellAt++];job.Active.Occupied.Add(tile);PreparationRequest(job,job.Active,tile);
                            foreach(int neighbour in PreparationNeighbours(job,tile))PreparationRequest(job,job.Active,neighbour);
                            break;
                        }
                        job.Active=null;job.Cells=null;
                    }
                    if(job.At>=job.Members.Count)
                    {job.At=1;job.Limit=WantsGround(selection,TargetKind.Pitch,TargetGroup.Pitch)?GamePitchManagerAPI.Instance.GetPitchArrayAsSpan().Length:1;job.Phase=3;break;}
                    var next=job.Members[job.At++];
                    if(next.Owner!=actor || next.Target==null)break;
                    var root=snapshot.Index.Canonical(next.Target);
                    if(root==null || !selection.Matches(root))break;
                    var geometry=Building(next.Slot);
                    if(geometry==null)break;
                    snapshot.Geometry[next.Slot]=geometry;job.Active=PreparationCandidate(job,root);job.Cells=geometry.OccupiedTiles;job.CellAt=0;break;
                case 3:
                    if(job.At>=job.Limit)
                    {job.At=1;job.Limit=GameProjectileManagerAPI.Instance.GetProjectilesAsSpan().Length;job.Phase=4;break;}
                    var pitch=Pitch(job.At++);
                    if(pitch==null)break;
                    snapshot.Pitch[pitch.TileId]=pitch;
                    if(pitch.Owner==actor && selection.Matches(pitch))
                    {var candidate=PreparationCandidate(job,pitch);foreach(int tile in pitch.OccupiedTiles){candidate.Requests.Add(tile);foreach(int neighbour in PreparationNeighbours(job,tile))candidate.Requests.Add(neighbour);}}
                    break;
                case 4:
                    if(job.At>=job.Limit)
                    {job.DecorationGroups=snapshot.Decorations.Values.ToArray();job.At=0;job.TargetAt=0;job.Cells=null;job.Phase=5;break;}
                    var decoration=DecorationSnapshotSlot(job.At++);
                    if(decoration==null)break;
                    if(!snapshot.Decorations.TryGetValue(decoration.TileId,out var group))snapshot.Decorations.Add(decoration.TileId,group=new List<DemolitionTarget>());
                    group.Add(decoration);break;
                case 5:
                    if(!WantsGround(selection,TargetKind.Decoration,TargetGroup.Decorations) || job.At>=job.DecorationGroups.Length)
                    {job.At=0;job.Result=new PreviewHitCandidate[job.Ordered.Count];job.Phase=6;break;}
                    var decorations=job.DecorationGroups[job.At];
                    if(job.Cells==null)
                    {
                        if(decorations.Count==0 || FirstOwnDecorationAt(decorations[0].TileId,actor)==0){job.At++;break;}
                        job.Cells=DecorationRequestTiles(decorations[0].TileId,CardinalNeighbours,IsBorder);job.TargetAt=0;
                    }
                    if(job.TargetAt>=decorations.Count){job.At++;job.Cells=null;break;}
                    var target=decorations[job.TargetAt++];
                    if(target.Owner==actor && selection.Matches(target))PreparationCandidate(job,target).Requests.UnionWith(job.Cells);
                    break;
                case 6:
                    if(job.At>=job.Ordered.Count){snapshot.HitCandidates=job.Result;job.Phase=8;break;}
                    var prepared=job.Ordered[job.At];
                    if(prepared.Occupied.Count>0)prepared.Root.OccupiedTiles=prepared.Occupied.OrderBy(t=>t).ToArray();
                    snapshot.Roots.Add(prepared.Root);
                    job.Result[job.At++]=new PreviewHitCandidate {Root=prepared.Root,RequestTiles=prepared.Requests.OrderBy(t=>t).ToArray()};break;
            }
        }
        internal sealed class PreviewQuote
        {
            internal PreviewSnapshot Snapshot;
            internal int Actor,NextRoot;
            internal SelectionSpec Selection;
            internal readonly Dictionary<string,DemolitionTarget> Expanded=new Dictionary<string,DemolitionTarget>(StringComparer.Ordinal);
            internal readonly List<PreviewRootResult> CompletedRoots=new List<PreviewRootResult>();
            internal DemolitionTarget[] OrderedRoots;
            internal DemolitionTarget[] Result;
        }

        internal PreviewQuote BeginQuote(PreviewSnapshot snapshot,DemolitionTarget[] hitRoots,int actor,SelectionSpec selection)
        {
            // Geometry supplies an immutable unique-root array. Validate membership in each
            // budgeted root step instead of constructing another full identity/group index.
            return new PreviewQuote {Snapshot=snapshot,Actor=actor,Selection=selection,OrderedRoots=hitRoots??Array.Empty<DemolitionTarget>()};
        }

        internal bool TryStepQuote(PreviewQuote quote,int maximumRoots,long deadlineStopwatch,out bool complete,out DemolitionTarget[] targets)
        {
            complete=false;targets=Array.Empty<DemolitionTarget>();
            if (quote==null || !available || quote.Snapshot?.Index==null || quote.Actor!=quote.Snapshot.Actor || quote.Actor<1 || quote.Actor>8)
            { complete=true;return true; }
            if (quote.Result!=null) { complete=true;targets=quote.Result;return true; }
            if (maximumRoots<1 || !System.Threading.Monitor.TryEnter(EngineGate)) return false;
            try
            {
                complete=StepPreviewRoots(quote,maximumRoots,deadlineStopwatch,expected=>EvaluatePreviewRoot(quote.Snapshot,expected,quote.Actor));
                if (complete) targets=quote.Result;
                return true;
            }
            finally { System.Threading.Monitor.Exit(EngineGate); }
        }

        private IEnumerable<DemolitionTarget> EvaluatePreviewRoot(PreviewSnapshot snapshot,DemolitionTarget expected,int actor)
        {
            if(expected==null || !snapshot.Roots.Contains(expected) || expected.Owner!=actor || !snapshot.Selection.Matches(expected))return null;
            var current=Resolve(expected);
            if (current==null || !SameState(expected,current) || !Permitted(current,actor)) return null;
            return PreviewClosure(snapshot,expected,actor,out var closure)?closure:null;
        }

        // Actual production root-step boundary; deterministic clock injection is only for offline fixtures.
        private static readonly Func<long> PreviewClock=System.Diagnostics.Stopwatch.GetTimestamp;
        internal static bool StepPreviewRoots(PreviewQuote quote,int maximumRoots,long deadlineStopwatch,Func<DemolitionTarget,IEnumerable<DemolitionTarget>> evaluate,Func<long> clock=null)
        {
            clock=clock??PreviewClock;
            int processed=0;
            while (quote.NextRoot<quote.OrderedRoots.Length && processed<maximumRoots && clock()<deadlineStopwatch)
            {
                var expected=quote.OrderedRoots[quote.NextRoot++];processed++;
                var closure=evaluate(expected);
                if (closure!=null)
                {
                    var frozen=closure.ToArray();
                    quote.CompletedRoots.Add(new PreviewRootResult { Root=expected,Closure=frozen });
                    foreach (var target in frozen) quote.Expanded[target.Identity]=target;
                }
            }
            if (quote.NextRoot!=quote.OrderedRoots.Length) return false;
            quote.Result=DemolitionPolicy.Canonicalize(quote.Expanded.Values);return true;
        }

        internal sealed class GroundPreparation
        {
            internal PreviewSnapshot Snapshot;
            internal int[] Requests;
            internal int Actor,Phase,At;
            internal SelectionSpec Selection;
            internal readonly HashSet<int> Discovered=new HashSet<int>();
            internal readonly Dictionary<string,PreviewHitCandidate> Result=new Dictionary<string,PreviewHitCandidate>(StringComparer.Ordinal);
            internal readonly int[] Scratch=new int[8];
        }
        internal GroundPreparation BeginGroundPreparation(PreviewSnapshot snapshot,int[] broadPhaseTileIds,int actor,SelectionSpec selection) =>
            new GroundPreparation {Snapshot=snapshot,Requests=broadPhaseTileIds??Array.Empty<int>(),Actor=actor,Selection=selection};

        internal bool TryStepGroundPreparation(GroundPreparation job,long deadlineStopwatch,out bool complete)
        {
            complete=false;if(job==null)throw new ArgumentNullException(nameof(job));
            if(job.Phase==3){complete=true;return true;}
            if(!System.Threading.Monitor.TryEnter(EngineGate))return false;
            try
            {
                bool walls=WantsWalls(job.Selection),moats=WantsGround(job.Selection,TargetKind.DugMoat,TargetGroup.DugMoats);
                if(!available || job.Snapshot==null || job.Actor<1 || job.Actor>8 || job.Snapshot.Actor!=job.Actor || !walls && !moats)job.Phase=3;
                while(job.Phase!=3 && System.Diagnostics.Stopwatch.GetTimestamp()<deadlineStopwatch)
                {
                    if(job.Phase==0)
                    {
                        if(job.At<job.Snapshot.HitCandidates.Length)
                        {var candidate=job.Snapshot.HitCandidates[job.At++];job.Result[candidate.Root.Identity]=candidate;continue;}
                        job.At=0;job.Phase=1;continue;
                    }
                    if(job.Phase==1)
                    {
                        if(job.At>=job.Requests.Length){job.Phase=2;continue;}
                        int request=job.Requests[job.At++];var tiles=GameTileManagerAPI.Instance;
                        if(!tiles.IsValidTileId(request))continue;
                        var point=tiles.GetTileVectorFromId(request);if(!IsValidTile(point.X,point.Y))continue;
                        ConsiderGround(job,request,walls,moats);
                        int count=FillTileNeighbours(request,false,job.Scratch);
                        for(int i=0;i<count;i++)ConsiderGround(job,job.Scratch[i],walls,moats);
                        continue;
                    }
                    job.Snapshot.HitCandidates=job.Result.Values.ToArray();job.Phase=3;
                }
                complete=job.Phase==3;return true;
            }
            finally {System.Threading.Monitor.Exit(EngineGate);}
        }
        private void ConsiderGround(GroundPreparation job,int tile,bool walls,bool moats)
        {
            if(!job.Discovered.Add(tile))return;
            var tiles=GameTileManagerAPI.Instance;var flags=tiles.GetTilePropertyFlag(tile);
            if((!walls || (flags&TilePropertyFlag.IsWall)==0) && (!moats || (flags&TilePropertyFlag.IsMoat)==0))return;
            var point=tiles.GetTileVectorFromId(tile);
            void Add(DemolitionTarget target)
            {
                if(target==null || !job.Selection.Matches(target))return;
                job.Result[target.Identity]=new PreviewHitCandidate {Root=target,RequestTiles=ExpandNeighbourRequests(target.OccupiedTiles,Neighbours)};
                job.Snapshot.Roots.Add(target);
            }
            if(walls)Add(WallAt(tile,point.X,point.Y,flags,job.Actor));
            if(moats)Add(MoatAt(tile,point.X,point.Y,flags,job.Actor));
        }
        private static bool WantsWalls(SelectionSpec selection) => selection.SameType ? selection.Start?.Kind==TargetKind.Wall :
            (selection.Filters&((1<<(int)TargetGroup.Walls)|(1<<(int)TargetGroup.Fortifications)))!=0;
        private bool PreviewClosure(PreviewSnapshot snapshot,DemolitionTarget root,int actor,out List<DemolitionTarget> closure)
        {
            closure=new List<DemolitionTarget>();
            var result=closure;var seenDecorations=new HashSet<string>(StringComparer.Ordinal);
            bool AddDecorations(int tile)
            {
                if ((GameTileManagerAPI.Instance.GetMiscDisplayLayer()[tile]&0x1000)==0 || !snapshot.Decorations.TryGetValue(tile,out var group)) return true;
                if (!OwnDecorationSet(group,actor)) return false;
                foreach (var target in group)
                {
                    var current=Resolve(target);
                    if (current==null || !SameState(target,current)) return false;
                    if (seenDecorations.Add(target.Identity)) result.Add(target);
                }
                return true;
            }
            if (root==null || root.Owner!=actor) return false;
            if (root.Kind==TargetKind.Decoration) return AddDecorations(root.TileId) && closure.Any(t=>t.Identity==root.Identity);
            if (root.Kind!=TargetKind.Building) { closure.Add(root);return root.Kind!=TargetKind.Wall || AddDecorations(root.TileId); }
            var queue=new Queue<int>();var seen=new HashSet<int>();var expandedCompounds=new HashSet<uint>();queue.Enqueue(root.Slot);
            while (queue.Count>0)
            {
                int slot=queue.Dequeue();if (!seen.Add(slot)) continue;
                var expected=snapshot.Index.Target(slot);var current=expected==null?null:Resolve(expected);
                if (current==null || current.Owner!=actor || !SameState(expected,current)) return false;
                if (!snapshot.Geometry.TryGetValue(slot,out var geometry))
                {
                    geometry=Building(slot);
                    if (geometry==null || !SameState(expected,geometry)) return false;
                    snapshot.Geometry[slot]=geometry;
                }
                closure.Add(geometry);
                int size=expected.TaskIndex>>16;
                for (int dy=0;dy<size;dy++) for (int dx=0;dx<size;dx++)
                { int tile=TileAt(expected.X+dx,expected.Y+dy);if (tile>=0 && !AddDecorations(tile)) return false; }
                if (expected.Flags!=0 && expandedCompounds.Add(expected.Flags))
                {
                    if (!snapshot.Index.TryCompound(slot,actor,out var children)) return false;
                    foreach (int child in children) queue.Enqueue(child);
                }
                if ((eStructs)expected.Type==eStructs.STRUCT_GATE_MAIN || (eStructs)expected.Type==eStructs.STRUCT_GATE_INNER)
                {
                    int oldX=*(int*)((byte*)native.Buildings+0x31B7D0),oldY=*(int*)((byte*)native.Buildings+0x31B7D4);
                    try
                    {
                        int previous=0;
                        for (int n=0;n<2;n++)
                        {
                            int child=native.Find(native.Buildings,actor,expected.X,expected.Y,size,(int)eStructs.STRUCT_DRAWBRIDGE,previous);
                            if (child<=0) break;
                            queue.Enqueue(child);previous=child;
                        }
                    }
                    finally { *(int*)((byte*)native.Buildings+0x31B7D0)=oldX;*(int*)((byte*)native.Buildings+0x31B7D4)=oldY; }
                }
            }
            return true;
        }

        private static bool WantsGround(SelectionSpec selection,TargetKind kind,TargetGroup group) =>
            selection.SameType ? selection.Start!=null && selection.Start.Kind==kind : (selection.Filters&(1<<(int)group))!=0;

        // Membership comes from the rendered frozen quote, never from a changed live world.
        internal DemolitionTarget[][] MakeBatches(DemolitionTarget[] approved) =>
            available ? DemolitionPolicy.MakeBatches(approved) : Array.Empty<DemolitionTarget[]>();
        internal sealed class Operation
        {
            internal DemolitionTarget Root;
            internal List<DemolitionTarget> Closure;
        }

        // Whole components remain atomic for permission/identity checks. Unrelated components
        // in the same transport packet continue after an ordinary rejection.
        internal static ApplyResult[] ApplyComponents(DemolitionTarget[] targets,Func<DemolitionTarget[],ApplyResult[]> apply)
        {
            if (targets==null) return Array.Empty<ApplyResult>();
            var results=targets.Select(target=>new ApplyResult {Identity=target?.Identity??"",Status=ApplyStatus.Forbidden,Detail="Remaining component skipped after a native failure."}).ToArray();
            foreach (var group in targets.Select((target,index)=>new {Target=target,Index=index}).GroupBy(item=>item.Target.Dependency))
            {
                var members=group.ToArray();
                var received=apply(members.Select(item=>item.Target).ToArray());
                for (int i=0;i<members.Length;i++) results[members[i].Index]=received[i];
                if (received.Any(result=>result.Status==ApplyStatus.Failed)) break;
            }
            return results;
        }

        // The actual commit preflight, with immediate native views supplied by the adapter.
        // Frozen membership is independent of whether a parent still exists at receipt time.
        internal static bool PlanComponent(int actor,DemolitionTarget[] targets,Func<DemolitionTarget,DemolitionTarget> resolve,
            Func<DemolitionTarget,DemolitionTarget> canonical,Func<DemolitionTarget,List<DemolitionTarget>> ownClosure,
            Func<DemolitionTarget,bool> permitted,out Dictionary<string,DemolitionTarget> approved,out List<Operation> plans,out ApplyResult[] results)
        {
            approved=new Dictionary<string,DemolitionTarget>(StringComparer.Ordinal);plans=new List<Operation>();
            results=targets.Select(target=>new ApplyResult {Identity=target?.Identity??"",Status=ApplyStatus.Forbidden,Detail="Frozen dependent component rejected."}).ToArray();
            if (actor<1 || actor>8 || targets.Length==0) return false;
            var current=new Dictionary<string,DemolitionTarget>(StringComparer.Ordinal);bool blocked=false;
            for (int i=0;i<targets.Length;i++)
            {
                var expected=targets[i];
                if (expected==null) {blocked=true;continue;}
                if (approved.ContainsKey(expected.Identity)) {blocked=true;results[i].Detail="Duplicate frozen identity.";continue;}
                approved.Add(expected.Identity,expected);
                var live=resolve(expected);
                if (live==null) {results[i].Status=ApplyStatus.Missing;results[i].Detail="Frozen target no longer exists.";blocked=true;continue;}
                if (!SameState(expected,live)) {results[i].Status=ApplyStatus.Changed;results[i].Detail="Frozen target identity or state changed.";blocked=true;continue;}
                if (expected.Owner!=actor) {results[i].Detail="Frozen target is not owned by the issuer.";blocked=true;continue;}
                current.Add(expected.Identity,live);
            }
            if (blocked) return false;
            var covered=new HashSet<string>(StringComparer.Ordinal);
            foreach (var root in current.Values)
            {
                var principal=canonical(root);
                if (principal==null) return false;
                if (principal.Identity!=root.Identity)
                {
                    if (!current.TryGetValue(principal.Identity,out var expectedPrincipal) || !SameState(expectedPrincipal,principal)) return false;
                    continue; // Linked/reserved children never receive independent refunds.
                }
                var closure=ownClosure(root);
                if (closure==null || !closure.Any(child=>child.Identity==root.Identity) ||
                    !ClosureMatchesApproval(closure,approved,current) || !permitted(root)) return false;
                foreach (var child in closure) covered.Add(child.Identity);
                plans.Add(new Operation {Root=root,Closure=closure});
            }
            return current.Keys.All(covered.Contains);
        }

        internal sealed class ExpectedRejectionException : Exception
        {
            internal ExpectedRejectionException(ApplyStatus status,string message):base(message) {Status=status;}
            internal readonly ApplyStatus Status;
        }
        internal sealed class MutationAttempt
        {
            internal bool Started {get;private set;}
            // Set before entering a live helper: even a throwing Post observer may follow a refund.
            internal void BeforeMutation() {Started=true;}
        }
        internal static bool TryMutationAttempt(Action<MutationAttempt> execute,out ExpectedRejectionException rejection)
        {
            var attempt=new MutationAttempt();rejection=null;
            try {execute(attempt);return true;}
            catch (ExpectedRejectionException error) when (!attempt.Started) {rejection=error;return false;}
        }

        internal ApplyResult[] Apply(int actor,DemolitionTarget[] targets)
        {
            lock (EngineGate) return ApplyComponents(targets,component=>ApplyComponent(actor,component));
        }

        private ApplyResult[] ApplyComponent(int actor,DemolitionTarget[] targets)
        {
            if (targets==null) return Array.Empty<ApplyResult>();
            lock (EngineGate)
            {
                var results=targets.Select(t=>new ApplyResult { Identity=t?.Identity??"",Status=ApplyStatus.Forbidden }).ToArray();
                if (!available)
                {
                    if (results.Length>0) {results[0].Status=ApplyStatus.Failed;results[0].Detail="Native demolition is disabled after a previous failure.";}
                    return results;
                }
                if (actor<1 || actor>8) return results;
                var executed=new HashSet<string>(StringComparer.Ordinal);
                var attempted=new HashSet<string>(StringComparer.Ordinal);
                try
                {
                    if (!PlanComponent(actor,targets,Resolve,CanonicalBuilding,
                        root=>ClosureOwn(root,actor,out var closure)?closure:null,root=>Permitted(root,actor),
                        out var approved,out var plans,out results)) return results;
                    bool aborted=false;
                    foreach (var plan in plans.OrderBy(p=>p.Root.Kind).ThenBy(p=>p.Root.Slot))
                    {
                        if (executed.Contains(plan.Root.Identity)) continue;
                        // Live SE refund/bulldoze events can reenter while this monitor is held.
                        // Revalidate after earlier operations and again after the permission callback path.
                        if (!RefreshOperation(plan,actor,approved,executed,out var freshRoot,out var freshClosure))
                        { aborted=true;break; }
                        attempted.Add(plan.Root.Identity);
                        if (!TryMutationAttempt(attempt=>Execute(freshRoot,actor,freshClosure,approved,executed,attempt),out var rejection))
                        {
                            attempted.Remove(plan.Root.Identity);
                            var result=results.First(r=>r.Identity==plan.Root.Identity);result.Status=rejection.Status;result.Detail=rejection.Message;
                            aborted=true;break;
                        }
                        foreach (var child in freshClosure) if (Applied(child)) executed.Add(child.Identity);
                        if (!executed.Contains(freshRoot.Identity)) { aborted=true;break; }
                    }
                    CompleteResults(targets,results,executed,attempted,aborted);
                }
                catch (Exception error)
                {
                    Diagnostic="Native demolition failed; remaining work disabled.";available=false;
                    CompleteResults(targets,results,executed,attempted,true);
                    // Exceptions during preflight must stop later components/batches too.
                    var failed=results.FirstOrDefault(result=>result.Status!=ApplyStatus.Applied);
                    if (failed!=null) failed.Status=ApplyStatus.Failed;
                    for (int i=0;i<results.Length;i++) if (results[i].Status==ApplyStatus.Failed) results[i].Detail=error.GetType().Name;
                    string diagnostic=DateTime.Now.ToString("HH:mm:ss.fff")+" DEMOLITION_APPLY_FAILED "+error;
                    try { DiagnosticProduced?.Invoke(diagnostic); } catch { /* Diagnostics cannot interrupt/repeat a mutation. */ }
                }
                return results;
            }
        }

        private bool RefreshOperation(Operation plan,int actor,IDictionary<string,DemolitionTarget> approved,HashSet<string> executed,out DemolitionTarget freshRoot,out List<DemolitionTarget> freshClosure)
        {
            freshRoot=null;freshClosure=null;
            if (!PendingIdentitiesCurrent(plan.Closure,executed,Resolve)) return false;
            var root=Resolve(plan.Root);
            if (root==null || root.Owner!=actor || !SameState(plan.Root,root) || !Permitted(root,actor)) return false;
            // The query enters live mod chains too; don't reuse the pre-query metadata.
            root=Resolve(plan.Root);
            if (root==null || root.Owner!=actor || !SameState(plan.Root,root) || !PendingIdentitiesCurrent(plan.Closure,executed,Resolve)) return false;
            var canonical=CanonicalBuilding(root);
            if (canonical==null || canonical.Identity!=root.Identity || !ClosureOwn(root,actor,out var closure)) return false;
            var current=new Dictionary<string,DemolitionTarget>(StringComparer.Ordinal);
            foreach (var child in closure)
            {
                var live=Resolve(child);
                if (live==null || live.Owner!=actor || !SameState(child,live)) return false;
                current[child.Identity]=live;
            }
            if (!ClosureMatchesApproval(closure,approved,current)) return false;
            freshRoot=root;freshClosure=closure;return true;
        }

        internal static bool PendingIdentitiesCurrent(IEnumerable<DemolitionTarget> closure,HashSet<string> executed,Func<DemolitionTarget,DemolitionTarget> resolve) =>
            IdentitiesCurrent(closure.Where(t=>!executed.Contains(t.Identity)),resolve);

        internal static void CompleteResults(DemolitionTarget[] targets,ApplyResult[] results,HashSet<string> executed,HashSet<string> attempted,bool aborted)
        {
            for (int i=0;i<targets.Length;i++)
            {
                string identity=targets[i]?.Identity;
                if (identity==null) continue;
                if (executed.Contains(identity)) { results[i].Status=ApplyStatus.Applied;results[i].Detail=""; }
                else if (attempted.Contains(identity) || !aborted)
                { results[i].Status=ApplyStatus.Failed;results[i].Detail="Native operation did not reach the expected mark/removal state."; }
                else
                {
                    if (results[i].Status!=ApplyStatus.Missing && results[i].Status!=ApplyStatus.Changed) results[i].Status=ApplyStatus.Forbidden;
                    if (string.IsNullOrEmpty(results[i].Detail)) results[i].Detail="Remaining dependent component skipped after live state changed.";
                }
            }
        }

        internal static bool IdentitiesCurrent(IEnumerable<DemolitionTarget> approved,Func<DemolitionTarget,DemolitionTarget> resolve)
        {
            foreach (var expected in approved)
            {
                if (expected==null || expected.Owner<1 || expected.Owner>8) return false;
                var current=resolve(expected);
                if (current==null || !SameState(expected,current)) return false;
            }
            return true;
        }

        internal static bool ClosureMatchesApproval(IEnumerable<DemolitionTarget> closure,IDictionary<string,DemolitionTarget> approved,IDictionary<string,DemolitionTarget> current)
        {
            foreach (var child in closure)
                if (!approved.TryGetValue(child.Identity,out var expected) || !current.ContainsKey(child.Identity) || !SameState(expected,child)) return false;
            return true;
        }

        internal static bool SameState(DemolitionTarget expected, DemolitionTarget current) =>
            expected.Identity == current.Identity && expected.Type == current.Type && expected.Owner == current.Owner &&
            expected.X == current.X && expected.Y == current.Y && expected.Flags == current.Flags;

        private DemolitionTarget Resolve(DemolitionTarget target)
        {
            if (target.Kind == TargetKind.Building) return Building(target.Slot,false);
            if (target.Kind == TargetKind.Decoration) return Decoration(target.Slot);
            if (!IsValidTile(target.X, target.Y)) return null;
            var tiles=GameTileManagerAPI.Instance;
            if (tiles.GetTileId(target.X,target.Y)!=target.TileId) return null;
            var flags=tiles.GetTilePropertyFlag(target.TileId);
            if (target.Kind==TargetKind.Pitch) return (flags&TilePropertyFlag.PitchTrap)!=0 ? Pitch(target.Slot) : null;
            if (target.Kind==TargetKind.Wall) return WallAt(target.TileId,target.X,target.Y,flags);
            if (target.Kind==TargetKind.DugMoat) return MoatAt(target.TileId,target.X,target.Y,flags);
            return null;
        }

        private DemolitionTarget Building(int slot,bool geometry=true)
        {
            if (slot<=0 || !global::APIShared.BuildingAccess.TryGetById(slot,out var b, out _) || b==null || b->r_AliveState!=AliveState.IsAlive || b->r_GlobalId==0 ||
                b->r_OccupyTileGridSize<1 || b->r_OccupyTileGridSize>13 || !IsValidTile(b->r_TilePositionXBegin,b->r_TilePositionYBegin)) return null;
            var tiles=GameTileManagerAPI.Instance;
            int[] cells=Array.Empty<int>();
            if (geometry)
            {
                fixed (ushort* structures=tiles.GetStructureLayer())
                fixed (int* alpha=tiles.GetAlphaGfxLayer())
                fixed (int* flags=tiles.GetLogicLayer())
                    cells=CollectRegisteredTiles(slot,b,structures,alpha,(uint*)flags,GameTileManagerView.NativePackedTileCapacity,TileAt);
            }
            if (geometry && cells.Length==0) return null;
            int minX=b->r_TilePositionXBegin,minY=b->r_TilePositionYBegin,maxX=minX,maxY=minY;
            if (cells.Length>0)
            {
                minX=int.MaxValue;minY=int.MaxValue;maxX=int.MinValue;maxY=int.MinValue;
                foreach (int tile in cells) { var point=tiles.GetTileVectorFromId(tile); minX=Math.Min(minX,point.X);minY=Math.Min(minY,point.Y);maxX=Math.Max(maxX,point.X);maxY=Math.Max(maxY,point.Y); }
            }
            return new DemolitionTarget { Kind=TargetKind.Building,Group=Classify(b->r_BuildingType),Slot=slot,Type=(int)b->r_BuildingType,Owner=b->r_PlayerIdOwner,
                GlobalId=b->r_GlobalId,Flags=b->N0000178A,
                // Per-kind frozen stamp: grid size and reserved-child parent prevent stale regrouping/refunds.
                TaskIndex=((int)b->r_OccupyTileGridSize<<16)|(IsReserved(b->r_BuildingType)?b->r_StoneQuarry_StockPileBuildingId:0),X=b->r_TilePositionXBegin,Y=b->r_TilePositionYBegin,TileId=checked((int)b->r_TileIdBegin),
                Bounds=new AreaRect(minX,minY,maxX,maxY),OccupiedTiles=cells };
        }

        private int TileAt(int x,int y) => IsValidTile(x,y) ? GameTileManagerAPI.Instance.GetTileId(x,y) : -1;

        // The fixed 36-value array is optional constructor-specific data, not a size-squared grid.
        // Every accepted point is corroborated by the live registration layer.
        internal static int[] CollectRegisteredTiles(int slot,GameBuilding* building,ushort* structures,int* alpha,uint* flags,int capacity,Func<int,int,int> tileAt)
        {
            int size=(int)building->r_OccupyTileGridSize;
            if (size<1 || size>13) return Array.Empty<int>();
            var cells=new List<int>(size*size+36);
            void Add(int tile,bool checkDuplicate)
            { if (tile>=0 && tile<capacity && structures[tile]==slot && (!checkDuplicate || !cells.Contains(tile))) cells.Add(tile); }
            int x=building->r_TilePositionXBegin,y=building->r_TilePositionYBegin;
            for (int dy=0;dy<size;dy++) for (int dx=0;dx<size;dx++) Add(tileAt(x+dx,y+dy),false);
            uint* extra=&building->r_OccupiedTileIdsArrayBegin;
            for (int i=0;i<36;i++) if (extra[i]<(uint)capacity) Add((int)extra[i],true);
            if (building->r_BuildingType==eStructs.STRUCT_GOODS_YARD)
                for (int dy=-3;dy<=4;dy++) for (int dx=-3;dx<=4;dx++)
                {
                    int tile=tileAt(x+dx,y+dy);
                    if (tile>=0 && tile<capacity && alpha[tile]==slot && (flags[tile]&(uint)TilePropertyFlag.GoodsyardConnection)==(uint)TilePropertyFlag.GoodsyardConnection && !cells.Contains(tile)) cells.Add(tile);
                }
            cells.Sort();return cells.ToArray();
        }

        private DemolitionTarget CanonicalBuilding(DemolitionTarget target)
        {
            if (target==null || target.Kind!=TargetKind.Building) return target;
            var buildings=GameBuildingManagerAPI.Instance;
            if (!global::APIShared.BuildingAccess.TryGetById(buildings, target.Slot,out var b, out _)) return null;
            if (IsReserved(b->r_BuildingType))
            {
                int parentId=b->r_StoneQuarry_StockPileBuildingId; // Constructor's typed +192 parent link for reserved children.
                var parent=Building(parentId,false);
                if (parent==null || !global::APIShared.BuildingAccess.TryGetById(buildings, parentId,out var p, out _) || p->N0000178A==0 || p->N0000178A!=b->N0000178A || p->r_PlayerIdOwner!=b->r_PlayerIdOwner || IsReserved(p->r_BuildingType)) return null;
                return parent;
            }
            if (b->r_BuildingType==eStructs.STRUCT_GOODS_YARD || b->r_BuildingType==eStructs.STRUCT_QUARRYPILE)
            {
                uint compound=b->N0000178A;
                if (compound==0) return target;
                var span=buildings.GetBuildingsAsSpan();
                for (int i=0;i<span.Length;i++)
                    if (span[i].r_AliveState==AliveState.IsAlive && span[i].N0000178A==compound && span[i].r_PlayerIdOwner==target.Owner &&
                        (b->r_BuildingType==eStructs.STRUCT_GOODS_YARD ? span[i].r_BuildingType==eStructs.STRUCT_GOODS_YARD : span[i].r_BuildingType==eStructs.STRUCT_QUARRY)) return Building(i+1,false);
            }
            return target;
        }

        private static bool IsReserved(eStructs type) => type==eStructs.STRUCT_PARADEGROUND_OIL || type==eStructs.STRUCT_PARADEGROUND_ENG || type==eStructs.STRUCT_PARADEGROUND_TUN ||
            type==eStructs.STRUCT_PARADEGROUND_MISS || type==eStructs.STRUCT_PARADEGROUND_LGT || type==eStructs.STRUCT_PARADEGROUND_HVY;

        internal int CountLogicalBuildings(DemolitionTarget[] targets)
        {
            if (targets==null) return 0;
            var buildings=targets.Where(t=>t!=null && t.Kind==TargetKind.Building).ToArray();
            var index=new PreviewIndex(buildings.Select(t=>new BuildingMember(t.Slot,t.Owner,AliveState.IsAlive,t.Flags,t)));
            var roots=new HashSet<string>(StringComparer.Ordinal);
            foreach (var target in buildings) { var root=index.Canonical(target);if (root!=null) roots.Add(root.Identity); }
            return roots.Count;
        }

        internal static bool IsDecorationType(ProjectileType type) => type==ProjectileType.Flag1 || type==ProjectileType.Flag3 || type==ProjectileType.Flag2 ||
            type==ProjectileType.CrusaderFlag || type==ProjectileType.Brazier || type==ProjectileType.Heads;

        internal static int DecorationOwner(GameProjectile* projectile) => *(ushort*)&projectile->r_PlayerSourceId;

        internal static bool OwnDecorationSet(IEnumerable<DemolitionTarget> group,int actor) =>
            group.All(t=>t!=null && t.Kind==TargetKind.Decoration && t.Slot>=25 && t.Owner==actor && IsDecorationType((ProjectileType)t.Type));

        private DemolitionTarget Decoration(int slot)
        {
            var span=GameProjectileManagerAPI.Instance.GetProjectilesAsSpan();
            // Projectile span includes native slot zero, unlike the building API's sentinel-adjusted span.
            if (slot<25 || slot>=span.Length) return null;
            fixed (GameProjectile* first=span)
            {
                GameProjectile* p=first+slot;
                if (p->r_AliveState!=AliveState.IsAlive || !IsDecorationType(p->r_ProjectileType) || p->r_CurrentTileId>int.MaxValue) return null;
                int tile=(int)p->r_CurrentTileId;
                var tiles=GameTileManagerAPI.Instance;
                if (!tiles.IsValidTileId(tile)) return null;
                var point=tiles.GetTileVectorFromId(tile);
                if (!IsValidTile(point.X,point.Y) || p->r_CurrentTileX!=point.X || p->r_CurrentTileY!=point.Y) return null;
                return Ground(TargetKind.Decoration,TargetGroup.Decorations,slot,(int)p->r_ProjectileType,DecorationOwner(p),point.X,point.Y,tile,p->r_GlobalId,0,0);
            }
        }

        private DemolitionTarget DecorationSnapshotSlot(int slot)
        {
            var span=GameProjectileManagerAPI.Instance.GetProjectilesAsSpan();
            if(slot<1 || slot>=span.Length || span[slot].r_AliveState!=AliveState.IsAlive || !IsDecorationType(span[slot].r_ProjectileType))return null;
            var target=Decoration(slot);
            if(target!=null)return target;
            // Native9F410 would still touch this unidentifiable record; retain the same blocker.
            uint rawTile=span[slot].r_CurrentTileId;
            if(rawTile>=GameTileManagerView.NativePackedTileCapacity)return null;
            var point=GameTileManagerAPI.Instance.GetTileVectorFromId((int)rawTile);
            return Ground(TargetKind.Decoration,TargetGroup.Decorations,slot,(int)span[slot].r_ProjectileType,0,point.X,point.Y,(int)rawTile,0,0,0);
        }
        private Dictionary<int,List<DemolitionTarget>> DecorationsByTile()
        {
            var result=new Dictionary<int,List<DemolitionTarget>>();
            int count=GameProjectileManagerAPI.Instance.GetProjectilesAsSpan().Length;
            for(int slot=1;slot<count;slot++)
            {
                var target=DecorationSnapshotSlot(slot);if(target==null)continue;
                if(!result.TryGetValue(target.TileId,out var group))result.Add(target.TileId,group=new List<DemolitionTarget>());
                group.Add(target);
            }
            return result;
        }
        private Dictionary<int, DemolitionTarget> PitchByTile()
        {
            var result = new Dictionary<int, DemolitionTarget>();
            var slots = GamePitchManagerAPI.Instance.GetPitchArrayAsSpan();
            for (int slot = 1; slot < slots.Length; slot++)
            {
                ref var pitch = ref slots[slot];
                if (!pitch.IsAlive || !IsValidTile(pitch.TileX,pitch.TileY)) continue;
                int tile=GameTileManagerAPI.Instance.GetTileId(pitch.TileX,pitch.TileY);
                result[tile]=Ground(TargetKind.Pitch,TargetGroup.Pitch,slot,(int)eStructs.STRUCT_PITCH_DITCH,pitch.OwnerId,pitch.TileX,pitch.TileY,tile,pitch.GlobalId,0,0);
            }
            return result;
        }

        private DemolitionTarget Pitch(int slot)
        {
            if (!GamePitchManagerAPI.Instance.TryGetPitchById(slot,out var pitch) || !pitch->IsAlive || !IsValidTile(pitch->TileX,pitch->TileY)) return null;
            int tile=GameTileManagerAPI.Instance.GetTileId(pitch->TileX,pitch->TileY);
            return Ground(TargetKind.Pitch,TargetGroup.Pitch,slot,(int)eStructs.STRUCT_PITCH_DITCH,pitch->OwnerId,pitch->TileX,pitch->TileY,tile,pitch->GlobalId,0,0);
        }

        private DemolitionTarget WallAt(int tile,int x,int y,TilePropertyFlag flags,int actor=0)
        {
            var tiles=GameTileManagerAPI.Instance;
            if ((flags&TilePropertyFlag.IsWall)==0 || (flags&TilePropertyFlag.GoodsyardRelated)!=0 || tiles.GetStructureLayer()[tile]!=0) return null;
            int owner=(tiles.GetWallOwnerLayer()[tile]&7)+1;
            if (actor>0 && actor!=owner) return null;
            return CreateWallIdentity(tile,x,y,flags,owner,tiles.GetHeightLayer()[tile]-tiles.GetDefaultHeightLayer()[tile]);
        }
        internal static DemolitionTarget CreateWallIdentity(int tile,int x,int y,TilePropertyFlag flags,int owner,int heightDifference)
        {
            // Dirty-neighbour graphics update 65830/65EF0 can add CrenelatedHigh to the same wall.
            // Omit only that dynamic bit from comparison; live gameplay flags remain untouched.
            uint stableFlags=(uint)flags&WallIdentityMask;
            int signature=unchecked((int)stableFlags)|(heightDifference<<24);
            return Ground(TargetKind.Wall,WallGroup(flags),tile,signature,owner,x,y,tile,0,0,stableFlags);
        }
        internal static TargetGroup WallGroup(TilePropertyFlag flags) => (flags&TilePropertyFlag.IsStairs)!=0?TargetGroup.Fortifications:TargetGroup.Walls;

        private DemolitionTarget MoatAt(int tile,int x,int y,TilePropertyFlag flags,int actor=0)
        {
            var tiles=GameTileManagerAPI.Instance;
            if ((flags&TilePropertyFlag.IsMoat)==0 || !tiles.TryGetMoatWorkTaskAtTile(x,y,out var task) || task.Pointer->r_TileId!=tile) return null;
            if (actor>0 && task.Pointer->r_OwnerPlayerId!=actor) return null;
            return Ground(TargetKind.DugMoat,TargetGroup.DugMoats,tile,(int)eStructs.STRUCT_NEW_FILL_MOAT,task.Pointer->r_OwnerPlayerId,x,y,tile,0,tiles.GetMoatWorkTaskIndexAtTile(x,y),(uint)TilePropertyFlag.IsMoat);
        }

        private static DemolitionTarget Ground(TargetKind kind,TargetGroup group,int slot,int type,int owner,int x,int y,int tile,uint globalId,int task,uint flags) =>
            new DemolitionTarget { Kind=kind,Group=group,Slot=slot,Type=type,Owner=owner,X=x,Y=y,TileId=tile,GlobalId=globalId,TaskIndex=task,Flags=flags,
                Bounds=new AreaRect(x,y,x,y),OccupiedTiles=new[]{tile} };

        private bool ClosureOwn(DemolitionTarget root, int actor, out List<DemolitionTarget> closure,Dictionary<int,List<DemolitionTarget>> decorations=null)
        {
            closure=new List<DemolitionTarget>();
            if (root==null || root.Owner!=actor) return false;
            var result=closure;
            var seenDecorations=new HashSet<string>(StringComparer.Ordinal);
            bool AddDecorations(int tile)
            {
                if ((GameTileManagerAPI.Instance.GetMiscDisplayLayer()[tile]&0x1000)==0) return true;
                decorations=decorations??DecorationsByTile();
                if (!decorations.TryGetValue(tile,out var group)) return true;
                if (!OwnDecorationSet(group,actor)) return false;
                foreach (var decoration in group)
                    if (seenDecorations.Add(decoration.Identity)) result.Add(decoration);
                return true;
            }
            if (root.Kind==TargetKind.Decoration)
                return AddDecorations(root.TileId) && closure.Any(t=>t.Identity==root.Identity);
            if (root.Kind!=TargetKind.Building)
            { closure.Add(root);return root.Kind!=TargetKind.Wall || AddDecorations(root.TileId); }
            var queue=new Queue<int>(); var seen=new HashSet<int>(); queue.Enqueue(root.Slot);
            var buildings=GameBuildingManagerAPI.Instance;
            while (queue.Count>0)
            {
                int id=queue.Dequeue(); if (!seen.Add(id)) continue;
                var target=Building(id);
                if (target==null || target.Owner!=actor) return false;
                closure.Add(target);
                if (!global::APIShared.BuildingAccess.TryGetById(buildings, id,out var b, out _)) return false;
                //61EF0 removes static decoration on the core square, not on optional farm hints.
                for (int dy=0;dy<(int)b->r_OccupyTileGridSize;dy++) for (int dx=0;dx<(int)b->r_OccupyTileGridSize;dx++)
                {
                    int tile=TileAt(b->r_TilePositionXBegin+dx,b->r_TilePositionYBegin+dy);
                    if (tile>=0 && !AddDecorations(tile)) return false;
                }
                uint compound=b->N0000178A; // raw manager record +0x304, native C4290's linked-record key.
                if (compound!=0)
                {
                    var span=buildings.GetBuildingsAsSpan();
                    for (int index=0;index<span.Length;index++)
                        if (span[index].r_AliveState!=AliveState.None && span[index].N0000178A==compound)
                        {
                            if (span[index].r_PlayerIdOwner!=actor) return false;
                            // C4290 marks every nonzero member, including a newly spawned NeedsInit child.
                            // Unsupported/not-yet-previewable members therefore reject the parent.
                            if (span[index].r_AliveState!=AliveState.MarkedForDeletion) queue.Enqueue(index+1);
                        }
                }
                if (b->r_BuildingType==eStructs.STRUCT_GATE_MAIN || b->r_BuildingType==eStructs.STRUCT_GATE_INNER)
                {
                    int previous=0;
                    int oldX=*(int*)((byte*)native.Buildings+0x31B7D0), oldY=*(int*)((byte*)native.Buildings+0x31B7D4);
                    try
                    {
                        for (int n=0;n<2;n++)
                        {
                            int child=native.Find(native.Buildings,actor,b->r_TilePositionXBegin,b->r_TilePositionYBegin,checked((int)b->r_OccupyTileGridSize),(int)eStructs.STRUCT_DRAWBRIDGE,previous);
                            if (child<=0) break;
                            queue.Enqueue(child); previous=child;
                        }
                    }
                    finally { *(int*)((byte*)native.Buildings+0x31B7D0)=oldX; *(int*)((byte*)native.Buildings+0x31B7D4)=oldY; }
                }
            }
            return true;
        }

        private bool Permitted(DemolitionTarget target,int actor)
        {
            if (target.Owner!=actor || !IsValidTile(target.X,target.Y) || (target.Kind==TargetKind.Wall && (wallPolicy<0 || native.WallPolicy!=wallPolicy))) return false;
            if (target.Kind==TargetKind.Decoration)
                return target.Slot>=25 && IsDecorationType((ProjectileType)target.Type) && (GameTileManagerAPI.Instance.GetMiscDisplayLayer()[target.TileId]&0x1000)!=0;
            return native.Allows(target,actor);
        }

        private void Execute(DemolitionTarget target,int actor,List<DemolitionTarget> closure,IDictionary<string,DemolitionTarget> approved,HashSet<string> executed,MutationAttempt attempt)
        {
            if (target.Kind==TargetKind.Building) { ExecuteBuilding(target,actor,closure,approved,executed,attempt);return; }
            if (!GuardMutation(target,actor,closure,approved,executed)) return;
            switch (target.Kind)
            {
                case TargetKind.Pitch:attempt.BeforeMutation();native.RemovePitch(native.Tiles,target.Slot);break;
                case TargetKind.Wall:
                    var tiles=GameTileManagerAPI.Instance;
                    int percentage=WallRefundPercent(wallPolicy,tiles.GetHeightLayer()[target.TileId]-tiles.GetDefaultHeightLayer()[target.TileId]);
                    attempt.BeforeMutation();native.Refund(native.Buildings,-1,actor,percentage);
                    if (!GuardMutation(target,actor,closure,approved,executed)) return;
                    attempt.BeforeMutation();native.Wall(native.Tiles,actor,target.TileId);break;
                case TargetKind.DugMoat:attempt.BeforeMutation();native.RemoveDugMoat(target.TaskIndex,target.TileId);break;
                case TargetKind.Decoration:attempt.BeforeMutation();native.RemoveDecorationTile(target.TileId);break;
            }
            RecordApplied(closure,executed);
        }

        private bool GuardMutation(DemolitionTarget target,int actor,List<DemolitionTarget> closure,IDictionary<string,DemolitionTarget> approved,HashSet<string> executed)
        {
            if (executed.Contains(target.Identity)) return false;
            if (!RefreshOperation(new Operation { Root=target,Closure=closure },actor,approved,executed,out _,out _))
                throw new ExpectedRejectionException(ApplyStatus.Forbidden,"Frozen demolition target/closure changed before the next native mutation.");
            return true;
        }

        private void RecordApplied(IEnumerable<DemolitionTarget> closure,HashSet<string> executed)
        { foreach (var child in closure) if (Applied(child)) executed.Add(child.Identity); }

        private GameBuilding* ExpectedBuilding(DemolitionTarget expected,int actor)
        {
            var current=Resolve(expected);
            if (current==null) throw new ExpectedRejectionException(ApplyStatus.Missing,"Frozen building no longer exists.");
            if (current.Owner!=actor || !SameState(expected,current) || !global::APIShared.BuildingAccess.TryGetById(expected.Slot,out var b, out _))
                throw new ExpectedRejectionException(ApplyStatus.Changed,"Frozen building changed before the next native mutation.");
            return b;
        }

        internal static int WallRefundPercent(int policy,int heightDifference) => policy==1 || heightDifference>60 ? 50 : 25;

        private void ExecuteBuilding(DemolitionTarget target,int actor,List<DemolitionTarget> closure,IDictionary<string,DemolitionTarget> approved,HashSet<string> executed,MutationAttempt attempt)
        {
            int oldContext=*(int*)(native.ModulePointer+NativeBindings.DemolitionContextRva);
            try
            {
                var b=ExpectedBuilding(target,actor);
                if (b->r_BuildingType==eStructs.STRUCT_GATE_MAIN || b->r_BuildingType==eStructs.STRUCT_GATE_INNER)
                {
                    int previous=0;
                    for (int n=0;n<2;n++)
                    {
                        if (!GuardMutation(target,actor,closure,approved,executed)) return;
                        b=ExpectedBuilding(target,actor);
                        int child=native.Find(native.Buildings,actor,b->r_TilePositionXBegin,b->r_TilePositionYBegin,checked((int)b->r_OccupyTileGridSize),(int)eStructs.STRUCT_DRAWBRIDGE,previous);
                        if (child<=0) break;
                        var expected=closure.FirstOrDefault(t=>t.Kind==TargetKind.Building && t.Slot==child);
                        if (expected==null) throw new ExpectedRejectionException(ApplyStatus.Changed,"New gate child is not approved.");
                        if (!GuardMutation(target,actor,closure,approved,executed)) return;
                        var bridge=ExpectedBuilding(expected,actor);
                        if (bridge->r_AliveState!=AliveState.MarkedForDeletion)
                        {
                            *(int*)(native.ModulePointer+NativeBindings.DemolitionContextRva)=1;
                            attempt.BeforeMutation();native.Refund(native.Buildings,child,actor,50);
                        }
                        // Refund Pre/Post subscribers may replace or transfer either object.
                        if (!GuardMutation(target,actor,closure,approved,executed)) return;
                        ExpectedBuilding(expected,actor);
                        *(int*)(native.ModulePointer+NativeBindings.DemolitionContextRva)=1;
                        attempt.BeforeMutation();native.Delete(native.Buildings,child);
                        RecordApplied(closure,executed);
                        previous=child;
                    }
                }
                if (!GuardMutation(target,actor,closure,approved,executed)) return;
                b=ExpectedBuilding(target,actor);
                if (ShouldRefund(b))
                {
                    *(int*)(native.ModulePointer+NativeBindings.DemolitionContextRva)=1;
                    attempt.BeforeMutation();native.Refund(native.Buildings,target.Slot,actor,50);
                }
                if (!GuardMutation(target,actor,closure,approved,executed)) return;
                b=ExpectedBuilding(target,actor);
                if (*(int*)(native.ModulePointer+NativeBindings.ActorRva)==actor)
                {
                    bool stone=b->r_BuildingType==eStructs.STRUCT_GATE_MAIN || b->r_BuildingType==eStructs.STRUCT_GATE_INNER ||
                        (b->r_BuildingType>=eStructs.STRUCT_TOWER1 && b->r_BuildingType<=eStructs.STRUCT_TOWER5);
                    attempt.BeforeMutation();native.Sound(IntPtr.Zero,target.X,target.Y,stone?0x96:0xAA);
                }
                if (!GuardMutation(target,actor,closure,approved,executed)) return;
                ExpectedBuilding(target,actor);
                *(int*)(native.ModulePointer+NativeBindings.DemolitionContextRva)=1;
                attempt.BeforeMutation();native.Delete(native.Buildings,target.Slot);
                RecordApplied(closure,executed);
            }
            finally { *(int*)(native.ModulePointer+NativeBindings.DemolitionContextRva)=oldContext; }
        }

        private bool Applied(DemolitionTarget target)
        {
            if (target.Kind==TargetKind.Building)
                return !global::APIShared.BuildingAccess.TryGetById(target.Slot,out var b, out _) || b->r_GlobalId!=target.GlobalId || b->r_AliveState==AliveState.MarkedForDeletion || b->r_AliveState==AliveState.None;
            if (target.Kind==TargetKind.Decoration)
            {
                var span=GameProjectileManagerAPI.Instance.GetProjectilesAsSpan();
                return target.Slot<1 || target.Slot>=span.Length || span[target.Slot].r_GlobalId!=target.GlobalId || span[target.Slot].r_AliveState==AliveState.MarkedForDeletion || span[target.Slot].r_AliveState==AliveState.None;
            }
            return Resolve(target)==null;
        }

        internal static bool ShouldRefund(GameBuilding* building) =>
            *(ushort*)((byte*)building+0x288)==0 && building->r_AliveState!=AliveState.MarkedForDeletion;

        internal static TargetGroup Classify(eStructs type)
        {
            switch (type)
            {
                case eStructs.STRUCT_GOODS_YARD:case eStructs.STRUCT_GRANARY:case eStructs.STRUCT_ARMOURY:return TargetGroup.Stockpiles;
                case eStructs.STRUCT_DOG_CAGE:return TargetGroup.DogCages;
                case eStructs.STRUCT_KILLING_PIT:return TargetGroup.KillingPits;
                case eStructs.STRUCT_MAYPOLE:case eStructs.STRUCT_GARDEN:case eStructs.STRUCT_GARDEN_SMALL:case eStructs.STRUCT_GARDEN_MED:case eStructs.STRUCT_GARDEN_LARGE:
                case eStructs.STRUCT_STATUE:case eStructs.STRUCT_SHRINE:case eStructs.STRUCT_BEE_HIVE:case eStructs.STRUCT_DANCING_BEAR:
                case eStructs.STRUCT_POND:case eStructs.STRUCT_POND_SMALL:case eStructs.STRUCT_POND_LARGE:case eStructs.STRUCT_POND5:case eStructs.STRUCT_POND6:case eStructs.STRUCT_POND7:case eStructs.STRUCT_POND8:return TargetGroup.PositiveFear;
                case eStructs.STRUCT_GALLOWS:case eStructs.STRUCT_STOCKS:case eStructs.STRUCT_WITCH_HOIST:case eStructs.STRUCT_CESS_PIT:case eStructs.STRUCT_BURNING_STAKE:
                case eStructs.STRUCT_GIBBET:case eStructs.STRUCT_DUNGEON:case eStructs.STRUCT_RACK_STRETCHING:case eStructs.STRUCT_RACK_FLOGGING:
                case eStructs.STRUCT_CHOPPING_BLOCK:case eStructs.STRUCT_DUNKING_STOOL:case eStructs.STRUCT_HEAD_ON_SPIKE:return TargetGroup.NegativeFear;
                case eStructs.STRUCT_GATE_MAIN:case eStructs.STRUCT_GATE_INNER:case eStructs.STRUCT_GATE_WOOD:case eStructs.STRUCT_GATE_POSTERN:case eStructs.STRUCT_DRAWBRIDGE:
                case eStructs.STRUCT_GATEHOUSE:case eStructs.STRUCT_TOWER:case eStructs.STRUCT_TOWER1:case eStructs.STRUCT_TOWER2:case eStructs.STRUCT_TOWER3:case eStructs.STRUCT_TOWER4:case eStructs.STRUCT_TOWER5:
                case eStructs.STRUCT_GATE_WOOD1A:case eStructs.STRUCT_GATE_WOOD1B:case eStructs.STRUCT_GATE_WOOD1C:case eStructs.STRUCT_GATE_WOOD1D:
                case eStructs.STRUCT_GATE_STONE1A:case eStructs.STRUCT_GATE_STONE1B:case eStructs.STRUCT_GATE_STONE2A:case eStructs.STRUCT_GATE_STONE2B:
                case eStructs.STRUCT_STAIRS:return TargetGroup.Fortifications;
                case eStructs.STRUCT_WOOD_WALL:case eStructs.STRUCT_STONE_WALL:case eStructs.STRUCT_CRENAL_WALL:return TargetGroup.Walls;
                default:return TargetGroup.Buildings;
            }
        }
    }
}
