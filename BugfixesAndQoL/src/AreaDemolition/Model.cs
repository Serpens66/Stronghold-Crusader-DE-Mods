using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace BugfixesAndQoL.AreaDemolition
{
    internal enum TargetKind : byte { Building, Pitch, Wall, DugMoat, Decoration }
    internal enum TargetGroup : byte { Buildings, Fortifications, PositiveFear, NegativeFear, DogCages, KillingPits, Pitch, DugMoats, Decorations, Stockpiles=9, Walls=10 }

    // Local preview metadata; never serialized as multiplayer authority.
    internal sealed class PreviewHitCandidate
    {
        internal DemolitionTarget Root;
        internal int[] RequestTiles=Array.Empty<int>();
    }
    internal sealed class PreviewRootResult
    {
        internal DemolitionTarget Root;
        internal DemolitionTarget[] Closure=Array.Empty<DemolitionTarget>();
    }

    internal readonly struct AreaRect
    {
        internal AreaRect(int x1, int y1, int x2, int y2)
        { MinX = Math.Min(x1, x2); MinY = Math.Min(y1, y2); MaxX = Math.Max(x1, x2); MaxY = Math.Max(y1, y2); }
        internal readonly int MinX, MinY, MaxX, MaxY;
        internal bool Intersects(AreaRect other) => MinX <= other.MaxX && MaxX >= other.MinX && MinY <= other.MaxY && MaxY >= other.MinY;
        internal bool Contains(int x, int y) => x >= MinX && x <= MaxX && y >= MinY && y <= MaxY;
    }

    // Slots are lookup hints only; buildings/pitch also require their global identity.
    internal sealed class DemolitionTarget
    {
        internal TargetKind Kind;
        internal TargetGroup Group;
        internal int Slot, Type, Owner, X, Y, TileId, TaskIndex, Dependency;
        internal uint GlobalId, Flags;
        internal AreaRect Bounds;
        internal int[] OccupiedTiles = Array.Empty<int>();
        internal DemolitionTarget Copy() => new DemolitionTarget { Kind=Kind, Group=Group, Slot=Slot, Type=Type, Owner=Owner, X=X, Y=Y, TileId=TileId, TaskIndex=TaskIndex, Dependency=Dependency, GlobalId=GlobalId, Flags=Flags, Bounds=Bounds, OccupiedTiles=(int[])OccupiedTiles.Clone() };
        internal string Identity => string.Format(CultureInfo.InvariantCulture,"{0}:{1}:{2}:{3}:{4}",(int)Kind,Slot,GlobalId,TileId,TaskIndex);
    }

    internal readonly struct SelectionSpec
    {
        internal SelectionSpec(ushort filters, bool sameType, DemolitionTarget start)
        { Filters=filters; SameType=sameType; Start=start; }
        internal readonly ushort Filters;
        internal readonly bool SameType;
        internal readonly DemolitionTarget Start;
        internal bool Matches(DemolitionTarget target)
        {
            if (!SameType) return (Filters & (1 << (int)target.Group)) != 0;
            if (Start == null) return false;
            if (Start.Group == TargetGroup.PositiveFear || Start.Group == TargetGroup.NegativeFear) return target.Group == Start.Group;
            return target.Kind == Start.Kind && target.Type == Start.Type;
        }
    }

    internal enum ApplyStatus : byte { Applied, Missing, Changed, Forbidden, Failed }
    internal sealed class ApplyResult
    {
        internal string Identity;
        internal ApplyStatus Status;
        internal string Detail = "";
    }

    internal static class DemolitionPolicy
    {
        internal const int MaximumBatchTargets = 32;
        internal static DemolitionTarget[] Canonicalize(IEnumerable<DemolitionTarget> targets) => targets
            .GroupBy(t => t.Identity, StringComparer.Ordinal).Select(g => g.First().Copy())
            .OrderBy(t => t.Kind).ThenBy(t => t.GlobalId).ThenBy(t => t.TileId).ThenBy(t => t.Slot).ThenBy(t => t.TaskIndex).ToArray();
        // A closure's approved association survives even when its parent later disappears.
        // Overlap merges whole components; neither this builder nor batching queries live state.
        internal static DemolitionTarget[] FreezeComponents(IEnumerable<PreviewRootResult> roots)
        {
            if(roots==null)throw new ArgumentNullException(nameof(roots));
            var positions=new Dictionary<string,int>(StringComparer.Ordinal);
            var targets=new List<DemolitionTarget>();var parents=new List<int>();
            int Find(int at)
            {while(parents[at]!=at){parents[at]=parents[parents[at]];at=parents[at];}return at;}
            foreach(var root in roots)
            {
                if(root?.Root==null || root.Closure==null || root.Closure.Length==0)throw new ArgumentException("A frozen root requires its complete approved closure.",nameof(roots));
                int first=-1;bool containsRoot=false;
                foreach(var target in root.Closure)
                {
                    if(target==null)throw new ArgumentException("Null approved target.",nameof(roots));
                    string identity=target.Identity;containsRoot|=identity==root.Root.Identity;
                    if(!positions.TryGetValue(identity,out int at))
                    {at=targets.Count;positions.Add(identity,at);targets.Add(target);parents.Add(at);}
                    else if(!SameFrozenRecord(targets[at],target))throw new ArgumentException("Conflicting approved target state.",nameof(roots));
                    if(first<0)first=at;
                    else parents[Find(at)]=Find(first);
                }
                if(!containsRoot)throw new ArgumentException("Approved closure omits its root.",nameof(roots));
            }
            var result=Canonicalize(targets);var labels=new Dictionary<int,int>();
            foreach(var target in result)
            {
                int component=Find(positions[target.Identity]);
                if(!labels.TryGetValue(component,out int label)){label=labels.Count;labels.Add(component,label);}
                target.Dependency=label;
            }
            return result;
        }
        internal static DemolitionTarget[][] MakeBatches(DemolitionTarget[] targets)
        {
            if(targets==null)throw new ArgumentNullException(nameof(targets));
            var unique=new Dictionary<string,DemolitionTarget>(StringComparer.Ordinal);
            foreach(var target in targets)
            {
                if(target==null || target.Dependency<0 || target.Dependency>=targets.Length)throw new ArgumentException("Invalid frozen dependency.",nameof(targets));
                if(unique.TryGetValue(target.Identity,out var existing))
                {if(existing.Dependency!=target.Dependency || !SameFrozenRecord(existing,target))throw new ArgumentException("Conflicting frozen target.",nameof(targets));}
                else unique.Add(target.Identity,target);
            }
            var batches=new List<DemolitionTarget[]>();var packet=new List<DemolitionTarget>();int nextLabel=0;
            foreach(var component in Canonicalize(unique.Values).GroupBy(t=>t.Dependency).OrderBy(g=>g.Key))
            {
                var members=component.ToArray();
                if(members.Length>MaximumBatchTargets)continue;
                if(packet.Count+members.Length>MaximumBatchTargets){batches.Add(Canonicalize(packet));packet.Clear();nextLabel=0;}
                foreach(var member in members){member.Dependency=nextLabel;packet.Add(member);}
                nextLabel++;
            }
            if(packet.Count!=0)batches.Add(Canonicalize(packet));
            return batches.ToArray();
        }
        private static bool SameFrozenRecord(DemolitionTarget a,DemolitionTarget b) =>
            a.Kind==b.Kind && a.Group==b.Group && a.Type==b.Type && a.Owner==b.Owner && a.X==b.X && a.Y==b.Y && a.Flags==b.Flags;
        internal static string Digest(string value)
        { using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value ?? ""))).Replace("-", ""); }
        internal static string TargetDigest(IEnumerable<DemolitionTarget> targets) => Digest(string.Join("|", targets.Select(t => string.Format(CultureInfo.InvariantCulture,"{0}:{1}:{2}:{3}:{4}:{5}:{6}",t.Identity,t.Type,t.Owner,t.X,t.Y,t.Flags,t.Dependency))));
        internal static string ResultDigest(IEnumerable<ApplyResult> results) => Digest(string.Join("|", results.Select(r => r.Identity+":"+((int)r.Status).ToString(CultureInfo.InvariantCulture))));
    }
}
