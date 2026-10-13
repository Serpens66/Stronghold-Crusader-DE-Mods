using System;
using SHCDESE.Interop;

namespace FarmBuildPreviewTest
{
    internal sealed class GroundSnapshot
    {
        internal readonly long Session;
        internal readonly eMappers Mapper;
        internal readonly int X,Y;
        internal readonly GroundCounts Counts;
        internal GroundSnapshot(long session,eMappers mapper,int x,int y,GroundCounts counts)
        { Session=session;Mapper=mapper;X=x;Y=y;Counts=counts; }
    }
    internal static class GroundTooltipModel
    {
        internal static bool Matches(GroundSnapshot snapshot,long session,eMappers mapper,int x,int y,bool hover,bool validPosition) =>
            !hover && validPosition && session != 0 && snapshot != null && snapshot.Session == session && snapshot.Mapper == mapper && snapshot.X == x && snapshot.Y == y;
        internal static string Value(string label,int? current,int minimum, bool rtl = false) => label + ": " +
            (rtl ? "\u2066" : "") + (current.HasValue ? current.Value + "/" + minimum : "\u2265" + minimum) + (rtl ? "\u2069" : "");
        internal static string LabelKey(eMappers mapper) => mapper == eMappers.MAPPER_QUARRY ? "Ground.Stone" :
            mapper == eMappers.MAPPER_IRON_MINE ? "Ground.Iron" : "Ground.Pitch";
    }
}
