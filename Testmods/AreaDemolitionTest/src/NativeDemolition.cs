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
            Fingerprint = NativeBindings.SupportedHash + ":demolition-v3:registered-footprints,buildings,stockpiles,pitch,dug-moat,decorations10-15:wall=" + wallPolicy;
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
                if (!IsValidTile(x,y)) return null;
                var tiles=GameTileManagerAPI.Instance;
                int tile=tiles.GetTileId(x,y);
                var decorations=DecorationsByTile();
                if (decorations.TryGetValue(tile,out var sameTile))
                {
                    var decoration=sameTile.FirstOrDefault(t=>t.Owner==actor);
                    if (decoration!=null && Permitted(decoration,actor) && ClosureOwn(decoration,actor,out _,decorations)) return decoration;
                }
                var target=CanonicalBuilding(Building(BuildingAtTile(tile)));
                if (target!=null) return target.Owner==actor && Permitted(target,actor) && ClosureOwn(target,actor,out _,decorations) ? target : null;
                foreach (var ground in GroundAt(tile,x,y))
                    if (ground.Owner==actor && Permitted(ground,actor) && ClosureOwn(ground,actor,out _,decorations)) return ground;
                return null;
            }
        }

        // One copied metadata/compound/decor pass per preview snapshot, shared by projection and quoting.
        // This is advisory preview data; MakeBatches/Apply never use it as mutation authority.
        internal sealed class PreviewSnapshot
        {
            internal int Actor;
            internal SelectionSpec Selection;
            internal PreviewIndex Index;
            internal DemolitionTarget[] Candidates=Array.Empty<DemolitionTarget>();
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
                foreach (var member in source)
                {
                    members[member.Slot]=member;
                    if (member.Compound==0) continue;
                    if (!compounds.TryGetValue(member.Compound,out var group)) compounds.Add(member.Compound,group=new List<BuildingMember>());
                    group.Add(member);
                }
                foreach (var group in compounds.Values) group.Sort((left,right)=>left.Slot.CompareTo(right.Slot));
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
            (selection.Filters&(((1<<(int)TargetGroup.Pitch)-1)|(1<<(int)TargetGroup.Stockpiles)))!=0;

        private PreviewSnapshot CreatePreviewSnapshot(int actor,SelectionSpec selection)
        {
            var snapshot=new PreviewSnapshot { Actor=actor,Selection=selection };
            if (!available || actor<1 || actor>8) return snapshot;
            lock (EngineGate)
            {
                var members=new List<BuildingMember>();
                if (WantsBuildings(selection))
                {
                    var span=GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
                    for (int index=0;index<span.Length;index++)
                    {
                        ref var building=ref span[index];
                        if (building.r_AliveState==AliveState.None) continue;
                        members.Add(new BuildingMember(index+1,building.r_PlayerIdOwner,building.r_AliveState,building.N0000178A,Building(index+1,false)));
                    }
                }
                snapshot.Index=new PreviewIndex(members);
                var candidates=new List<DemolitionTarget>();
                foreach (var member in snapshot.Index.Members)
                {
                    if (member.Owner!=actor || member.Target==null) continue;
                    var root=snapshot.Index.Canonical(member.Target);
                    if (root==null || !selection.Matches(root)) continue;
                    var geometry=Building(member.Slot);
                    if (geometry!=null) { snapshot.Geometry[member.Slot]=geometry;candidates.Add(geometry); }
                }
                snapshot.Pitch=WantsGround(selection,TargetKind.Pitch,TargetGroup.Pitch)?PitchByTile():new Dictionary<int,DemolitionTarget>();
                foreach (var target in snapshot.Pitch.Values) if (target.Owner==actor && selection.Matches(target)) candidates.Add(target);
                snapshot.Decorations=DecorationsByTile();
                if (WantsGround(selection,TargetKind.Decoration,TargetGroup.Decorations))
                    foreach (var group in snapshot.Decorations.Values)
                        foreach (var target in group) if (target.Owner==actor && selection.Matches(target)) candidates.Add(target);
                snapshot.Candidates=candidates.ToArray();
                return snapshot;
            }
        }

        internal bool TryCreatePreviewSnapshot(int actor,SelectionSpec selection,out PreviewSnapshot snapshot)
        {
            snapshot=null;
            if (!System.Threading.Monitor.TryEnter(EngineGate)) return false;
            try { snapshot=CreatePreviewSnapshot(actor,selection);return true; }
            finally { System.Threading.Monitor.Exit(EngineGate); }
        }

        internal sealed class PreviewQuote
        {
            internal PreviewSnapshot Snapshot;
            internal int[] Tiles;
            internal int Actor,NextTile,NextRoot;
            internal readonly HashSet<int> BuildingHits=new HashSet<int>();
            internal SelectionSpec Selection;
            internal readonly Dictionary<string,DemolitionTarget> Roots=new Dictionary<string,DemolitionTarget>(StringComparer.Ordinal);
            internal readonly Dictionary<string,DemolitionTarget> Expanded=new Dictionary<string,DemolitionTarget>(StringComparer.Ordinal);
            internal DemolitionTarget[] OrderedRoots;
            internal DemolitionTarget[] Result;
        }

        internal PreviewQuote BeginQuote(PreviewSnapshot snapshot,int[] selectedIds,int actor,SelectionSpec selection) =>
            new PreviewQuote { Snapshot=snapshot,Tiles=selectedIds??Array.Empty<int>(),Actor=actor,Selection=selection };

        internal bool TryStepQuote(PreviewQuote quote,int maximumRoots,long deadlineStopwatch,out bool complete,out DemolitionTarget[] targets)
        {
            complete=false;targets=Array.Empty<DemolitionTarget>();
            if (quote==null || !available || quote.Snapshot?.Index==null || quote.Actor!=quote.Snapshot.Actor || quote.Actor<1 || quote.Actor>8)
            { complete=true;return true; }
            if (quote.Result!=null) { complete=true;targets=quote.Result;return true; }
            if (maximumRoots<1 || !System.Threading.Monitor.TryEnter(EngineGate)) return false;
            try
            {
                var snapshot=quote.Snapshot;var selection=quote.Selection;
                var tiles=GameTileManagerAPI.Instance;
                bool buildings=WantsBuildings(selection),pitch=WantsGround(selection,TargetKind.Pitch,TargetGroup.Pitch);
                bool walls=WantsGround(selection,TargetKind.Wall,TargetGroup.Fortifications),moats=WantsGround(selection,TargetKind.DugMoat,TargetGroup.DugMoats);
                bool decorationWanted=WantsGround(selection,TargetKind.Decoration,TargetGroup.Decorations);
                void Add(DemolitionTarget target)
                { if (target!=null && target.Owner==quote.Actor && selection.Matches(target)) quote.Roots[target.Identity]=target; }
                while (quote.NextTile<quote.Tiles.Length && System.Diagnostics.Stopwatch.GetTimestamp()<deadlineStopwatch)
                {
                    int tile=quote.Tiles[quote.NextTile++];
                    if (!tiles.IsValidTileId(tile)) continue;
                    var point=tiles.GetTileVectorFromId(tile);
                    if (!IsValidTile(point.X,point.Y)) continue;
                    if (buildings)
                    {
                        int slot=BuildingAtTile(tile);
                        if (slot>0 && quote.BuildingHits.Add(slot)) Add(snapshot.Index.Canonical(snapshot.Index.Target(slot)));
                    }
                    var flags=tiles.GetTilePropertyFlag(tile);
                    if (pitch && (flags&TilePropertyFlag.PitchTrap)!=0 && snapshot.Pitch.TryGetValue(tile,out var trap)) Add(trap);
                    if (walls) Add(WallAt(tile,point.X,point.Y,flags,quote.Actor));
                    if (moats) Add(MoatAt(tile,point.X,point.Y,flags,quote.Actor));
                    if (decorationWanted && snapshot.Decorations.TryGetValue(tile,out var group)) foreach (var target in group) Add(target);
                }
                if (quote.NextTile<quote.Tiles.Length) return true;
                if (quote.OrderedRoots==null) quote.OrderedRoots=quote.Roots.Values.ToArray();
                complete=StepPreviewRoots(quote,maximumRoots,deadlineStopwatch,expected=>EvaluatePreviewRoot(snapshot,expected,quote.Actor));
                if (complete) targets=quote.Result;
                return true;
            }
            finally { System.Threading.Monitor.Exit(EngineGate); }
        }

        private IEnumerable<DemolitionTarget> EvaluatePreviewRoot(PreviewSnapshot snapshot,DemolitionTarget expected,int actor)
        {
            var current=Resolve(expected);
            if (current==null || !SameState(expected,current) || !Permitted(current,actor)) return null;
            return PreviewClosure(snapshot,expected,actor,out var closure)?closure:null;
        }

        // Actual production root-step boundary; deterministic clock injection is only for offline fixtures.
        private static readonly Func<long> PreviewClock=System.Diagnostics.Stopwatch.GetTimestamp;
        internal static bool StepPreviewRoots(PreviewQuote quote,int maximumRoots,long deadlineStopwatch,Func<DemolitionTarget,IEnumerable<DemolitionTarget>> evaluate,Func<long> clock=null)
        {
            clock=clock??PreviewClock;
            maximumRoots=Math.Min(maximumRoots,64); // Native preview boundary caps work even if a caller supplies a larger budget.
            int processed=0;
            while (quote.NextRoot<quote.OrderedRoots.Length && processed<maximumRoots && clock()<deadlineStopwatch)
            {
                var expected=quote.OrderedRoots[quote.NextRoot++];processed++;
                var closure=evaluate(expected);
                if (closure!=null) foreach (var target in closure) quote.Expanded[target.Identity]=target;
            }
            if (quote.NextRoot!=quote.OrderedRoots.Length) return false;
            quote.Result=DemolitionPolicy.Canonicalize(quote.Expanded.Values);return true;
        }

        internal bool TryCaptureGroundCandidateTiles(int[] broadPhaseTileIds,int actor,SelectionSpec selection,out int[] filtered)
        {
            filtered=Array.Empty<int>();
            if (!available || broadPhaseTileIds==null || actor<1 || actor>8) return true;
            bool walls=WantsGround(selection,TargetKind.Wall,TargetGroup.Fortifications),moats=WantsGround(selection,TargetKind.DugMoat,TargetGroup.DugMoats);
            if (!walls && !moats) return true;
            if (!System.Threading.Monitor.TryEnter(EngineGate)) return false;
            try
            {
                var tiles=GameTileManagerAPI.Instance;var result=new List<int>();
                foreach (int tile in broadPhaseTileIds)
                {
                    if (!tiles.IsValidTileId(tile)) continue;
                    var flags=tiles.GetTilePropertyFlag(tile);
                    if ((!walls || (flags&TilePropertyFlag.IsWall)==0) && (!moats || (flags&TilePropertyFlag.IsMoat)==0)) continue;
                    var point=tiles.GetTileVectorFromId(tile);
                    if (!IsValidTile(point.X,point.Y)) continue;
                    var target=walls?WallAt(tile,point.X,point.Y,flags,actor):null;
                    if (target==null && moats) target=MoatAt(tile,point.X,point.Y,flags,actor);
                    if (target!=null && selection.Matches(target)) result.Add(tile);
                }
                filtered=result.ToArray();return true;
            }
            finally { System.Threading.Monitor.Exit(EngineGate); }
        }

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

        private int BuildingAtTile(int tile)
        {
            var tiles=GameTileManagerAPI.Instance;
            int slot=tiles.GetStructureLayer()[tile];
            if (slot>0) return slot;
            // Stockpile cross cells have no StructureGrid ID. Their alpha entry is a live sector ID.
            if ((tiles.GetTilePropertyFlag(tile)&TilePropertyFlag.GoodsyardConnection)==TilePropertyFlag.GoodsyardConnection)
            {
                int candidate=tiles.GetAlphaGfxLayer()[tile];
                if (candidate>0 && GameBuildingManagerAPI.Instance.IsValidId(candidate) && GameBuildingManagerAPI.Instance.TryGetBuildingById(candidate,out var b) && b->r_BuildingType==eStructs.STRUCT_GOODS_YARD) return candidate;
            }
            return 0;
        }

        private void AddRoot(DemolitionTarget target,int actor,SelectionSpec selection,Dictionary<string,DemolitionTarget> roots)
        { if (target!=null && target.Owner==actor && selection.Matches(target) && Permitted(target,actor)) roots[target.Identity]=target; }

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
                // Frozen dependency membership is not on the wire. Never orphan surviving children.
                if (!IdentitiesCurrent(all.Values,Resolve)) return Array.Empty<DemolitionTarget[]>();
                var parent = all.Keys.ToDictionary(id => id, id => id, StringComparer.Ordinal);
                string Find(string id)
                {
                    string root = id;
                    while (parent[root] != root) root = parent[root];
                    while (parent[id] != id) { string next = parent[id]; parent[id] = root; id = next; }
                    return root;
                }
                foreach (var item in all)
                {
                    if (!ClosureOwn(item.Value, item.Value.Owner, out var closure)) return Array.Empty<DemolitionTarget[]>();
                    foreach (var child in closure)
                    {
                        if (!all.TryGetValue(child.Identity,out var expected) || !SameState(expected,child)) return Array.Empty<DemolitionTarget[]>();
                        string left = Find(item.Key), right = Find(child.Identity);
                        if (left != right) parent[right] = left;
                    }
                }
                var batches = new List<DemolitionTarget[]>();
                var current = new List<DemolitionTarget>();
                foreach (var group in all.Keys.GroupBy(Find).OrderBy(g => g.Key, StringComparer.Ordinal))
                {
                    var component = group.ToArray();
                    if (component.Length > DemolitionPolicy.MaximumBatchTargets) continue;
                    if (current.Count + component.Length > DemolitionPolicy.MaximumBatchTargets)
                    { batches.Add(DemolitionPolicy.Canonicalize(current)); current.Clear(); }
                    current.AddRange(component.Select(id => all[id]));
                }
                if (current.Count > 0) batches.Add(DemolitionPolicy.Canonicalize(current));
                return batches.ToArray();
            }
        }

        private sealed class Operation
        {
            internal DemolitionTarget Root;
            internal List<DemolitionTarget> Closure;
        }

        internal ApplyResult[] Apply(int actor,DemolitionTarget[] targets)
        {
            if (targets==null) return Array.Empty<ApplyResult>();
            lock (EngineGate)
            {
                var results=targets.Select(t=>new ApplyResult { Identity=t?.Identity??"",Status=ApplyStatus.Forbidden }).ToArray();
                if (!available || actor<1 || actor>8) return results;
                var approved=new Dictionary<string,DemolitionTarget>(StringComparer.Ordinal);
                var current=new Dictionary<string,DemolitionTarget>(StringComparer.Ordinal);
                var blocked=new HashSet<string>(StringComparer.Ordinal);
                var plans=new List<Operation>();
                var executed=new HashSet<string>(StringComparer.Ordinal);
                var attempted=new HashSet<string>(StringComparer.Ordinal);
                try
                {
                    // Freeze and validate every batch identity before the first mutation.
                    for (int i=0;i<targets.Length;i++)
                    {
                        var expected=targets[i];
                        if (expected==null) continue;
                        approved[expected.Identity]=expected;
                        var live=Resolve(expected);
                        if (live==null) { results[i].Status=ApplyStatus.Missing;blocked.Add(expected.Identity);continue; }
                        if (!SameState(expected,live)) { results[i].Status=ApplyStatus.Changed;blocked.Add(expected.Identity);continue; }
                        if (expected.Owner!=actor) { blocked.Add(expected.Identity);continue; }
                        current[expected.Identity]=live;
                    }
                    // User-selected conservative policy: any stale identity rejects the entire packet.
                    if (blocked.Count>0) return results;
                    var decorations=DecorationsByTile();
                    foreach (var item in current)
                    {
                        var root=item.Value;
                        var canonical=CanonicalBuilding(root);
                        bool ownClosure=ClosureOwn(root,actor,out var closure,decorations);
                        if (canonical==null || !ownClosure)
                        { return results; }
                        if (canonical.Identity!=root.Identity)
                        {
                            if (!current.ContainsKey(canonical.Identity)) return results;
                            continue; // Reserved/compound children never receive independent refunds.
                        }
                        if (!Permitted(root,actor) || !ClosureMatchesApproval(closure,approved,current)) return results;
                        plans.Add(new Operation { Root=root,Closure=closure });
                    }
                    bool aborted=false;
                    foreach (var plan in plans.OrderBy(p=>p.Root.Kind).ThenBy(p=>p.Root.Slot))
                    {
                        if (executed.Contains(plan.Root.Identity)) continue;
                        // Live SE refund/bulldoze events can reenter while this monitor is held.
                        // Revalidate after earlier operations and again after the permission callback path.
                        if (!RefreshOperation(plan,actor,approved,executed,out var freshRoot,out var freshClosure))
                        { aborted=true;break; }
                        attempted.Add(plan.Root.Identity);
                        Execute(freshRoot,actor,freshClosure,approved,executed);
                        foreach (var child in freshClosure) if (Applied(child)) executed.Add(child.Identity);
                        if (!executed.Contains(freshRoot.Identity)) { aborted=true;break; }
                    }
                    CompleteResults(targets,results,executed,attempted,aborted);
                }
                catch (Exception error)
                {
                    Diagnostic="Native demolition failed; remaining work disabled.";available=false;
                    CompleteResults(targets,results,executed,attempted,true);
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
                { results[i].Status=ApplyStatus.Forbidden;results[i].Detail="Remaining packet skipped after live state changed."; }
            }
        }

        internal bool AreIdentitiesCurrent(DemolitionTarget[] targets)
        {
            if (!available || targets==null) return false;
            lock (EngineGate) return IdentitiesCurrent(targets,Resolve);
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
            if (slot<=0 || !GameBuildingManagerAPI.Instance.TryGetBuildingById(slot,out var b) || b==null || b->r_AliveState!=AliveState.IsAlive || b->r_GlobalId==0 ||
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
            if (!buildings.TryGetBuildingById(target.Slot,out var b)) return null;
            if (IsReserved(b->r_BuildingType))
            {
                int parentId=b->r_StoneQuarry_StockPileBuildingId; // Constructor's typed +192 parent link for reserved children.
                var parent=Building(parentId,false);
                if (parent==null || !buildings.TryGetBuildingById(parentId,out var p) || p->N0000178A==0 || p->N0000178A!=b->N0000178A || p->r_PlayerIdOwner!=b->r_PlayerIdOwner || IsReserved(p->r_BuildingType)) return null;
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

        private Dictionary<int,List<DemolitionTarget>> DecorationsByTile()
        {
            var result=new Dictionary<int,List<DemolitionTarget>>();
            var span=GameProjectileManagerAPI.Instance.GetProjectilesAsSpan();
            for (int slot=1;slot<span.Length;slot++)
            {
                if (span[slot].r_AliveState!=AliveState.IsAlive || !IsDecorationType(span[slot].r_ProjectileType)) continue;
                var target=Decoration(slot);
                if (target==null)
                {
                    // Native9F410 would still touch an unidentifiable static record: retain a blocker.
                    uint rawTile=span[slot].r_CurrentTileId;
                    if (rawTile>=GameTileManagerView.NativePackedTileCapacity) continue;
                    var point=GameTileManagerAPI.Instance.GetTileVectorFromId((int)rawTile);
                    target=Ground(TargetKind.Decoration,TargetGroup.Decorations,slot,(int)span[slot].r_ProjectileType,0,point.X,point.Y,(int)rawTile,0,0,0);
                }
                if (!result.TryGetValue(target.TileId,out var group)) result.Add(target.TileId,group=new List<DemolitionTarget>());
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

        private bool ClosureOwn(DemolitionTarget root, int actor, out List<DemolitionTarget> closure,Dictionary<int,List<DemolitionTarget>> decorations=null)
        {
            closure=new List<DemolitionTarget>();
            if (root==null || root.Owner!=actor) return false;
            decorations=decorations??DecorationsByTile();
            var result=closure;
            var seenDecorations=new HashSet<string>(StringComparer.Ordinal);
            bool AddDecorations(int tile)
            {
                if ((GameTileManagerAPI.Instance.GetMiscDisplayLayer()[tile]&0x1000)==0) return true;
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
                if (!buildings.TryGetBuildingById(id,out var b)) return false;
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

        private void Execute(DemolitionTarget target,int actor,List<DemolitionTarget> closure,IDictionary<string,DemolitionTarget> approved,HashSet<string> executed)
        {
            if (target.Kind==TargetKind.Building) { ExecuteBuilding(target,actor,closure,approved,executed);return; }
            if (!GuardMutation(target,actor,closure,approved,executed)) return;
            switch (target.Kind)
            {
                case TargetKind.Pitch:native.RemovePitch(native.Tiles,target.Slot);break;
                case TargetKind.Wall:
                    var tiles=GameTileManagerAPI.Instance;
                    int percentage=WallRefundPercent(wallPolicy,tiles.GetHeightLayer()[target.TileId]-tiles.GetDefaultHeightLayer()[target.TileId]);
                    native.Refund(native.Buildings,-1,actor,percentage);
                    if (!GuardMutation(target,actor,closure,approved,executed)) return;
                    native.Wall(native.Tiles,actor,target.TileId);break;
                case TargetKind.DugMoat:native.RemoveDugMoat(target.TaskIndex,target.TileId);break;
                case TargetKind.Decoration:native.RemoveDecorationTile(target.TileId);break;
            }
            RecordApplied(closure,executed);
        }

        private bool GuardMutation(DemolitionTarget target,int actor,List<DemolitionTarget> closure,IDictionary<string,DemolitionTarget> approved,HashSet<string> executed)
        {
            if (executed.Contains(target.Identity)) return false;
            if (!RefreshOperation(new Operation { Root=target,Closure=closure },actor,approved,executed,out _,out _))
                throw new InvalidOperationException("Frozen demolition target/closure changed inside a live native callback; remaining work stopped.");
            return true;
        }

        private void RecordApplied(IEnumerable<DemolitionTarget> closure,HashSet<string> executed)
        { foreach (var child in closure) if (Applied(child)) executed.Add(child.Identity); }

        private GameBuilding* ExpectedBuilding(DemolitionTarget expected,int actor)
        {
            var current=Resolve(expected);
            if (current==null || current.Owner!=actor || !SameState(expected,current) || !GameBuildingManagerAPI.Instance.TryGetBuildingById(expected.Slot,out var b))
                throw new InvalidOperationException("Frozen building changed inside a live native callback.");
            return b;
        }

        internal static int WallRefundPercent(int policy,int heightDifference) => policy==1 || heightDifference>60 ? 50 : 25;

        private void ExecuteBuilding(DemolitionTarget target,int actor,List<DemolitionTarget> closure,IDictionary<string,DemolitionTarget> approved,HashSet<string> executed)
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
                        if (expected==null) throw new InvalidOperationException("New gate child is not approved.");
                        if (!GuardMutation(target,actor,closure,approved,executed)) return;
                        var bridge=ExpectedBuilding(expected,actor);
                        if (bridge->r_AliveState!=AliveState.MarkedForDeletion)
                        {
                            *(int*)(native.ModulePointer+NativeBindings.DemolitionContextRva)=1;
                            native.Refund(native.Buildings,child,actor,50);
                        }
                        // Refund Pre/Post subscribers may replace or transfer either object.
                        if (!GuardMutation(target,actor,closure,approved,executed)) return;
                        ExpectedBuilding(expected,actor);
                        *(int*)(native.ModulePointer+NativeBindings.DemolitionContextRva)=1;
                        native.Delete(native.Buildings,child);
                        RecordApplied(closure,executed);
                        previous=child;
                    }
                }
                if (!GuardMutation(target,actor,closure,approved,executed)) return;
                b=ExpectedBuilding(target,actor);
                if (ShouldRefund(b))
                {
                    *(int*)(native.ModulePointer+NativeBindings.DemolitionContextRva)=1;
                    native.Refund(native.Buildings,target.Slot,actor,50);
                }
                if (!GuardMutation(target,actor,closure,approved,executed)) return;
                b=ExpectedBuilding(target,actor);
                if (*(int*)(native.ModulePointer+NativeBindings.ActorRva)==actor)
                {
                    bool stone=b->r_BuildingType==eStructs.STRUCT_GATE_MAIN || b->r_BuildingType==eStructs.STRUCT_GATE_INNER ||
                        (b->r_BuildingType>=eStructs.STRUCT_TOWER1 && b->r_BuildingType<=eStructs.STRUCT_TOWER5);
                    native.Sound(IntPtr.Zero,target.X,target.Y,stone?0x96:0xAA);
                }
                if (!GuardMutation(target,actor,closure,approved,executed)) return;
                ExpectedBuilding(target,actor);
                *(int*)(native.ModulePointer+NativeBindings.DemolitionContextRva)=1;
                native.Delete(native.Buildings,target.Slot);
                RecordApplied(closure,executed);
            }
            finally { *(int*)(native.ModulePointer+NativeBindings.DemolitionContextRva)=oldContext; }
        }

        private bool Applied(DemolitionTarget target)
        {
            if (target.Kind==TargetKind.Building)
                return !GameBuildingManagerAPI.Instance.TryGetBuildingById(target.Slot,out var b) || b->r_GlobalId!=target.GlobalId || b->r_AliveState==AliveState.MarkedForDeletion || b->r_AliveState==AliveState.None;
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
                case eStructs.STRUCT_GOODS_YARD:return TargetGroup.Stockpiles;
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
