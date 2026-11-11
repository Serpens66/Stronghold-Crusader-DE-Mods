using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace AreaDemolitionTest
{
    internal enum TargetKind : byte { Building, Pitch, Wall, DugMoat, Decoration }
    internal enum TargetGroup : byte { Buildings, Fortifications, PositiveFear, NegativeFear, DogCages, KillingPits, Pitch, DugMoats, Decorations }

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
        internal int Slot, Type, Owner, X, Y, TileId, TaskIndex;
        internal uint GlobalId, Flags;
        internal AreaRect Bounds;
        internal int[] OccupiedTiles = Array.Empty<int>();
        internal DemolitionTarget Copy() => new DemolitionTarget { Kind=Kind, Group=Group, Slot=Slot, Type=Type, Owner=Owner, X=X, Y=Y, TileId=TileId, TaskIndex=TaskIndex, GlobalId=GlobalId, Flags=Flags, Bounds=Bounds, OccupiedTiles=(int[])OccupiedTiles.Clone() };
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
            .OrderBy(t => t.Kind).ThenBy(t => t.GlobalId).ThenBy(t => t.TileId).ThenBy(t => t.Slot).ToArray();
        internal static string Digest(string value)
        { using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value ?? ""))).Replace("-", ""); }
        internal static string TargetDigest(IEnumerable<DemolitionTarget> targets) => Digest(string.Join("|", targets.Select(t => string.Format(CultureInfo.InvariantCulture,"{0}:{1}:{2}:{3}:{4}:{5}",t.Identity,t.Type,t.Owner,t.X,t.Y,t.Flags))));
        internal static string ResultDigest(IEnumerable<ApplyResult> results) => Digest(string.Join("|", results.Select(r => r.Identity+":"+((int)r.Status).ToString(CultureInfo.InvariantCulture))));
    }
}
