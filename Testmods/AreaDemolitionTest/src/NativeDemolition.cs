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
        private const uint WallIdentityMask = (uint)(TilePropertyFlag.IsWall | TilePropertyFlag.IsLowWall | TilePropertyFlag.IsStairs | TilePropertyFlag.CrenelatedHigh | TilePropertyFlag.CrenelatedLow);

        private NativeDemolition(NativeBindings native, ManualLogSource log)
        {
            this.native = native; wallPolicy = native.WallPolicy;
            Diagnostic = wallPolicy < 0 ? "Native demolition available; unknown wall refund patch: walls disabled." : "Native demolition available.";
            Fingerprint = NativeBindings.SupportedHash + ":demolition-v1:buildings,pitch,dug-moat:wall=" + wallPolicy;
        }

        internal static bool TryCreate(CrusaderLibraryLoadContext context, ManualLogSource log, out NativeDemolition result)
        {
            result = null;
            try { result = new NativeDemolition(new NativeBindings(context, log), log); return true; }
            catch (Exception error)
            { log?.LogError(DateTime.Now.ToString("HH:mm:ss.fff") + " DEMOLITION_NATIVE_UNAVAILABLE " + error); return false; }
        }

        internal bool IsValidTile(int x, int y) => available && GameTileManagerAPI.Instance.IsTileInsideMapBounds(x, y);

        internal DemolitionTarget AtTile(int x, int y, int actor)
        {
            if (!available || actor < 1 || actor > 8) return null;
            lock (EngineGate)
            {
                if (!IsValidTile(x, y)) return null;
                var tiles = GameTileManagerAPI.Instance;
                int tile = tiles.GetTileId(x, y);
                int buildingId = tiles.GetStructureLayer()[tile];
                DemolitionTarget target = Building(buildingId);
                if (target != null) return target.Owner == actor && Permitted(target, actor) && ClosureOwn(target, actor, out _) ? target : null;
                foreach (var other in GroundAt(tile, x, y))
                    if (other.Owner == actor && Permitted(other, actor)) return other;
                return null;
            }
        }

        internal DemolitionTarget[] Capture(AreaRect area, int actor, SelectionSpec selection)
        {
            if (!available || actor < 1 || actor > 8) return Array.Empty<DemolitionTarget>();
            lock (EngineGate)
            {
                var tiles = GameTileManagerAPI.Instance;
                var roots = new Dictionary<string, DemolitionTarget>(StringComparer.Ordinal);
                var buildingSpan = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
                var structures = tiles.GetStructureLayer();
                bool buildingsWanted=selection.SameType ? selection.Start!=null && selection.Start.Kind==TargetKind.Building : (selection.Filters&((1<<(int)TargetGroup.Pitch)-1))!=0;
                for (int index = 0; buildingsWanted && index < buildingSpan.Length; index++)
                {
                    ref var building = ref buildingSpan[index];
                    if (building.r_AliveState != AliveState.IsAlive || building.r_PlayerIdOwner != actor) continue;
                    var key = new DemolitionTarget { Kind=TargetKind.Building, Group=Classify(building.r_BuildingType), Type=(int)building.r_BuildingType };
                    if (!selection.Matches(key)) continue;
                    var target = Building(index + 1);
                    if (target == null || !area.Intersects(target.Bounds)) continue;
                    bool overlap = false;
                    foreach (int tile in target.OccupiedTiles)
                    {
                        var point = tiles.GetTileVectorFromId(tile);
                        // Test actual registered cells, not the enclosing box's empty corners or AIV yards.
                        if (structures[tile] == target.Slot && area.Contains(point.X, point.Y)) { overlap = true; break; }
                    }
                    if (overlap) AddRoot(target, actor, selection, roots);
                }
                bool pitch = WantsGround(selection, TargetKind.Pitch, TargetGroup.Pitch);
                bool walls = WantsGround(selection, TargetKind.Wall, TargetGroup.Fortifications);
                bool moats = WantsGround(selection, TargetKind.DugMoat, TargetGroup.DugMoats);
                if (pitch)
                    foreach (var target in PitchByTile().Values)
                        if (area.Contains(target.X,target.Y) && (tiles.GetTilePropertyFlag(target.TileId)&TilePropertyFlag.PitchTrap)!=0)
                            AddRoot(target,actor,selection,roots);
                if (walls || moats)
                {
                    int lowX = Math.Max(0, area.MinX), highX = Math.Min(GameTileManagerView.PackedMapAxisCapacity - 1, area.MaxX);
                    int lowY = Math.Max(0, area.MinY), highY = Math.Min(GameTileManagerView.PackedMapAxisCapacity - 1, area.MaxY);
                    for (int y = lowY; y <= highY; y++)
                        for (int x = lowX; x <= highX; x++)
                        {
                            if (!IsValidTile(x,y)) continue;
                            int tile=tiles.GetTileId(x,y); var flags=tiles.GetTilePropertyFlag(tile);
                            if (walls) AddRoot(WallAt(tile,x,y,flags,actor),actor,selection,roots);
                            if (moats) AddRoot(MoatAt(tile,x,y,flags,actor),actor,selection,roots);
                        }
                }
                var expanded = new Dictionary<string, DemolitionTarget>(StringComparer.Ordinal);
                foreach (var root in roots.Values)
                    if (ClosureOwn(root, actor, out var closure))
                        foreach (var target in closure) expanded[target.Identity] = target;
                return DemolitionPolicy.Canonicalize(expanded.Values);
            }
        }

        private void AddRoot(DemolitionTarget target, int actor, SelectionSpec selection, Dictionary<string, DemolitionTarget> roots)
        { if (target != null && target.Owner == actor && selection.Matches(target) && Permitted(target, actor)) roots[target.Identity] = target; }

        private static bool WantsGround(SelectionSpec selection,TargetKind kind,TargetGroup group) =>
            selection.SameType ? selection.Start!=null && selection.Start.Kind==kind : (selection.Filters&(1<<(int)group))!=0;

        // Keep every indirect native consequence in the same authenticated packet as its root.
        // A changed closure is rejected rather than silently adding an unpreviewed object.
        internal DemolitionTarget[][] MakeBatches(DemolitionTarget[] approved)
        {
            if (approved == null || !available) return Array.Empty<DemolitionTarget[]>();
            lock (EngineGate)
            {
                var all = DemolitionPolicy.Canonicalize(approved).ToDictionary(t => t.Identity, StringComparer.Ordinal);
                var parent = all.Keys.ToDictionary(id => id, id => id, StringComparer.Ordinal);
                var invalid = new HashSet<string>(StringComparer.Ordinal);
                string Find(string id)
                {
                    string root = id;
                    while (parent[root] != root) root = parent[root];
                    while (parent[id] != id) { string next = parent[id]; parent[id] = root; id = next; }
                    return root;
                }
                foreach (var item in all)
                {
                    if (!ClosureOwn(item.Value, item.Value.Owner, out var closure)) { invalid.Add(item.Key); continue; }
                    foreach (var child in closure)
                    {
                        if (!all.ContainsKey(child.Identity)) { invalid.Add(item.Key); continue; }
                        string left = Find(item.Key), right = Find(child.Identity);
                        if (left != right) parent[right] = left;
                    }
                }
                var batches = new List<DemolitionTarget[]>();
                var current = new List<DemolitionTarget>();
                foreach (var group in all.Keys.GroupBy(Find).OrderBy(g => g.Key, StringComparer.Ordinal))
                {
                    var component = group.ToArray();
                    if (component.Any(invalid.Contains) || component.Length > DemolitionPolicy.MaximumBatchTargets) continue;
                    if (current.Count + component.Length > DemolitionPolicy.MaximumBatchTargets)
                    { batches.Add(DemolitionPolicy.Canonicalize(current)); current.Clear(); }
                    current.AddRange(component.Select(id => all[id]));
                }
                if (current.Count > 0) batches.Add(DemolitionPolicy.Canonicalize(current));
                return batches.ToArray();
            }
        }

        internal ApplyResult[] Apply(int actor, DemolitionTarget[] targets)
        {
            if (targets == null) return Array.Empty<ApplyResult>();
            lock (EngineGate)
            {
                var approved = new HashSet<string>(targets.Where(t => t != null).Select(t => t.Identity), StringComparer.Ordinal);
                var appliedEarlier = new HashSet<string>(StringComparer.Ordinal);
                var results = new List<ApplyResult>(targets.Length);
                foreach (var target in targets)
                {
                    var result = new ApplyResult { Identity = target?.Identity ?? "", Status = ApplyStatus.Forbidden };
                    results.Add(result);
                    if (!available || actor < 1 || actor > 8 || target == null || target.Owner != actor) continue;
                    if (appliedEarlier.Contains(target.Identity)) { result.Status = ApplyStatus.Applied; continue; }
                    try
                    {
                        var current = Resolve(target);
                        if (current == null) { result.Status = ApplyStatus.Missing; continue; }
                        if (!SameState(target, current)) { result.Status = ApplyStatus.Changed; continue; }
                        if (current.Owner != actor || !Permitted(current, actor) ||
                            !ClosureOwn(current, actor, out var closure) || closure.Any(t => !approved.Contains(t.Identity))) continue;
                        Execute(current, actor);
                        result.Status = Applied(current) ? ApplyStatus.Applied : ApplyStatus.Failed;
                        if (result.Status == ApplyStatus.Applied)
                            foreach (var affected in closure)
                                if (Applied(affected)) appliedEarlier.Add(affected.Identity);
                        if (result.Status == ApplyStatus.Failed) result.Detail = "Native operation did not reach the expected mark/removal state.";
                    }
                    catch (Exception error)
                    {
                        result.Status = ApplyStatus.Failed; result.Detail = error.GetType().Name;
                        // A partial native mutation is not replayable. Stop this process's optional adapter.
                        Diagnostic = "Native demolition failed; remaining work disabled."; available = false;
                        string diagnostic=DateTime.Now.ToString("HH:mm:ss.fff")+" DEMOLITION_APPLY_FAILED "+error;
                        try { DiagnosticProduced?.Invoke(diagnostic); } catch { /* Diagnostics cannot repeat/interrupt a mutation. */ }
                    }
                }
                return results.ToArray();
            }
        }

        private static bool SameState(DemolitionTarget expected, DemolitionTarget current) =>
            expected.Identity == current.Identity && expected.Type == current.Type && expected.Owner == current.Owner &&
            expected.X == current.X && expected.Y == current.Y && expected.Flags == current.Flags;

        private DemolitionTarget Resolve(DemolitionTarget target)
        {
            if (target.Kind == TargetKind.Building) return Building(target.Slot);
            if (!IsValidTile(target.X, target.Y)) return null;
            var tiles=GameTileManagerAPI.Instance;
            if (tiles.GetTileId(target.X,target.Y)!=target.TileId) return null;
            var flags=tiles.GetTilePropertyFlag(target.TileId);
            if (target.Kind==TargetKind.Pitch) return (flags&TilePropertyFlag.PitchTrap)!=0 ? Pitch(target.Slot) : null;
            if (target.Kind==TargetKind.Wall) return WallAt(target.TileId,target.X,target.Y,flags);
            if (target.Kind==TargetKind.DugMoat) return MoatAt(target.TileId,target.X,target.Y,flags);
            return null;
        }

        private DemolitionTarget Building(int slot)
        {
            var buildings = GameBuildingManagerAPI.Instance;
            if (slot <= 0 || !buildings.TryGetBuildingById(slot, out var b) || b == null || b->r_AliveState != AliveState.IsAlive || b->r_GlobalId == 0) return null;
            int size = checked((int)b->r_OccupyTileGridSize);
            // The record has exactly 36 registered-cell slots. Larger reserved AIV areas are not this array.
            if (size < 1 || size > 6 || !IsValidTile(b->r_TilePositionXBegin, b->r_TilePositionYBegin)) return null;
            var tiles = GameTileManagerAPI.Instance;
            uint* ids = &b->r_OccupiedTileIdsArrayBegin;
            var cells = new List<int>(size * size);
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            for (int i = 0; i < size * size; i++)
            {
                if (ids[i] > int.MaxValue || !tiles.IsValidTileId((int)ids[i])) return null;
                int tile = (int)ids[i]; var point = tiles.GetTileVectorFromId(tile);
                if (!IsValidTile(point.X, point.Y)) return null;
                cells.Add(tile); minX = Math.Min(minX, point.X); minY = Math.Min(minY, point.Y); maxX = Math.Max(maxX, point.X); maxY = Math.Max(maxY, point.Y);
            }
            return new DemolitionTarget { Kind=TargetKind.Building, Group=Classify(b->r_BuildingType), Slot=slot,
                Type=(int)b->r_BuildingType, Owner=b->r_PlayerIdOwner, GlobalId=b->r_GlobalId,
                X=b->r_TilePositionXBegin, Y=b->r_TilePositionYBegin, TileId=checked((int)b->r_TileIdBegin),
                Bounds=new AreaRect(minX,minY,maxX,maxY), OccupiedTiles=cells.Distinct().ToArray() };
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

        private IEnumerable<DemolitionTarget> GroundAt(int tile, int x, int y, Dictionary<int, DemolitionTarget> pitchByTile = null)
        {
            var result = new List<DemolitionTarget>(3);
            var tiles=GameTileManagerAPI.Instance;
            if (!IsValidTile(x,y) || tiles.GetTileId(x,y)!=tile) return result;
            TilePropertyFlag flags=tiles.GetTilePropertyFlag(tile);
            if ((flags & TilePropertyFlag.PitchTrap)!=0)
            {
                if (pitchByTile!=null)
                { if (pitchByTile.TryGetValue(tile,out var pitch)) result.Add(pitch); }
                else
                {
                    var slots=GamePitchManagerAPI.Instance.GetPitchArrayAsSpan();
                    for (int slot=1;slot<slots.Length;slot++)
                        if (slots[slot].IsAlive && slots[slot].TileX==x && slots[slot].TileY==y)
                        { var found=Pitch(slot); if (found!=null) result.Add(found); break; }
                }
            }
            var wall=WallAt(tile,x,y,flags); if (wall!=null) result.Add(wall);
            var moat=MoatAt(tile,x,y,flags); if (moat!=null) result.Add(moat);
            return result;
        }

        private DemolitionTarget WallAt(int tile,int x,int y,TilePropertyFlag flags,int actor=0)
        {
            var tiles=GameTileManagerAPI.Instance;
            if ((flags&TilePropertyFlag.IsWall)==0 || (flags&TilePropertyFlag.GoodsyardRelated)!=0 || tiles.GetStructureLayer()[tile]!=0) return null;
            int owner=(tiles.GetWallOwnerLayer()[tile]&7)+1;
            if (actor>0 && actor!=owner) return null;
            // Native tile signature, not another eStruct enum: material/height variants remain distinct.
            int signature=unchecked((int)((uint)flags&WallIdentityMask));
            signature|=(tiles.GetHeightLayer()[tile]-tiles.GetDefaultHeightLayer()[tile])<<24;
            return Ground(TargetKind.Wall,TargetGroup.Fortifications,tile,signature,owner,x,y,tile,0,0,(uint)flags&WallIdentityMask);
        }

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

        private bool ClosureOwn(DemolitionTarget root, int actor, out List<DemolitionTarget> closure)
        {
            closure=new List<DemolitionTarget>();
            if (root==null || root.Owner!=actor) return false;
            if (root.Kind!=TargetKind.Building) { closure.Add(root); return true; }
            var queue=new Queue<int>(); var seen=new HashSet<int>(); queue.Enqueue(root.Slot);
            var buildings=GameBuildingManagerAPI.Instance;
            while (queue.Count>0)
            {
                int id=queue.Dequeue(); if (!seen.Add(id)) continue;
                var target=Building(id);
                if (target==null || target.Owner!=actor) return false;
                closure.Add(target);
                if (!buildings.TryGetBuildingById(id,out var b)) return false;
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
            return native.Allows(target,actor);
        }

        private void Execute(DemolitionTarget target,int actor)
        {
            switch (target.Kind)
            {
                case TargetKind.Building:ExecuteBuilding(target,actor);break;
                case TargetKind.Pitch:native.RemovePitch(native.Tiles,target.Slot);break;
                case TargetKind.Wall:
                    var tiles=GameTileManagerAPI.Instance;
                    int percentage=WallRefundPercent(wallPolicy,tiles.GetHeightLayer()[target.TileId]-tiles.GetDefaultHeightLayer()[target.TileId]);
                    native.Refund(native.Buildings,-1,actor,percentage); native.Wall(native.Tiles,actor,target.TileId);break;
                case TargetKind.DugMoat:
                    native.RemoveDugMoat(target.TaskIndex,target.TileId);break;
            }
        }

        internal static int WallRefundPercent(int policy,int heightDifference) => policy==1 || heightDifference>60 ? 50 : 25;

        private void ExecuteBuilding(DemolitionTarget target,int actor)
        {
            var buildings=GameBuildingManagerAPI.Instance;
            if (!buildings.TryGetBuildingById(target.Slot,out var b)) return;
            int oldContext=*(int*)(native.ModulePointer+NativeBindings.DemolitionContextRva);
            try
            {
                if (b->r_BuildingType==eStructs.STRUCT_GATE_MAIN || b->r_BuildingType==eStructs.STRUCT_GATE_INNER)
                {
                    int previous=0;
                    for (int n=0;n<2;n++)
                    {
                        int child=native.Find(native.Buildings,actor,b->r_TilePositionXBegin,b->r_TilePositionYBegin,checked((int)b->r_OccupyTileGridSize),(int)eStructs.STRUCT_DRAWBRIDGE,previous);
                        if (child<=0) break;
                        if (buildings.TryGetBuildingById(child,out var bridge))
                        {
                            *(int*)(native.ModulePointer+NativeBindings.DemolitionContextRva)=1;
                            if (bridge->r_AliveState!=AliveState.MarkedForDeletion) native.Refund(native.Buildings,child,actor,50);
                            native.Delete(native.Buildings,child);
                        }
                        previous=child;
                    }
                }
                *(int*)(native.ModulePointer+NativeBindings.DemolitionContextRva)=1;
                if (ShouldRefund(b)) native.Refund(native.Buildings,target.Slot,actor,50);
                if (*(int*)(native.ModulePointer+NativeBindings.ActorRva)==actor)
                {
                    bool stone=b->r_BuildingType==eStructs.STRUCT_GATE_MAIN || b->r_BuildingType==eStructs.STRUCT_GATE_INNER ||
                        (b->r_BuildingType>=eStructs.STRUCT_TOWER1 && b->r_BuildingType<=eStructs.STRUCT_TOWER5);
                    // These are the native demolition sound command IDs, not building/domain IDs.
                    native.Sound(IntPtr.Zero,target.X,target.Y,stone?0x96:0xAA);
                }
                native.Delete(native.Buildings,target.Slot);
            }
            finally { *(int*)(native.ModulePointer+NativeBindings.DemolitionContextRva)=oldContext; }
        }

        private bool Applied(DemolitionTarget target)
        {
            if (target.Kind==TargetKind.Building)
                return !GameBuildingManagerAPI.Instance.TryGetBuildingById(target.Slot,out var b) || b->r_GlobalId!=target.GlobalId || b->r_AliveState==AliveState.MarkedForDeletion || b->r_AliveState==AliveState.None;
            return Resolve(target)==null;
        }

        internal static bool ShouldRefund(GameBuilding* building) =>
            *(ushort*)((byte*)building+0x288)==0 && building->r_AliveState!=AliveState.MarkedForDeletion;

        internal static TargetGroup Classify(eStructs type)
        {
            switch (type)
            {
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
                case eStructs.STRUCT_WOOD_WALL:case eStructs.STRUCT_STONE_WALL:case eStructs.STRUCT_CRENAL_WALL:case eStructs.STRUCT_STAIRS:return TargetGroup.Fortifications;
                default:return TargetGroup.Buildings;
            }
        }
    }
}
