#if SERP_DEVDIAG
using System;
using SHCDESE.Interop.Enums;

namespace CrenelatedWallTest
{
    // Multiplayer comparison lines, development build only (build.bat /devdiag). Host and client logs
    // are compared by simulation tick: every CRENEL_MP_STATE line (every 100 ticks) and every hash in
    // the event lines must be identical on all peers at the same tick. A difference points to the
    // first diverging tick; the event lines before it show which path (redraw report, damage,
    // bulldoze, start adoption, save data, resync) changed heights or the managed set.
    internal sealed partial class WallRuntime
    {
        private const int StateInterval = 100;
        private int diagTick = -1;

        partial void DiagTick(int tick)
        {
            diagTick = tick;
            if (tick % StateInterval != 0 || !initialized || !ready || repairing || !heights) return;
            Shared.DebugLogHelper.LogInfo(log, "CRENEL_MP_STATE tick=" + tick + "; " + StateLine());
        }

        partial void DiagRedraw(int reported, int changed, int writes) =>
            Shared.DebugLogHelper.LogInfo(log, "CRENEL_MP_REDRAW tick=" + diagTick + "; reported=" + reported + "; changedShape=" + changed +
                "; heightWrites=" + writes + "; " + StateLine());

        partial void DiagEvent(string phase, int tile, int writes) =>
            Shared.DebugLogHelper.LogInfo(log, "CRENEL_MP_EVENT phase=" + phase + "; tick=" + diagTick + "; tile=" + tile + "; heightWrites=" + writes +
                "; repairing=" + repairing + "; " + StateLine());

        partial void DiagSession(string phase) =>
            Shared.DebugLogHelper.LogInfo(log, "CRENEL_MP_SESSION phase=" + phase + "; tick=" + diagTick + "; session=" + sessionId +
                "; realMp=" + (context != null && context.Mode.IsRealMultiplayer) + "; save=" + (context != null && context.IsSave) +
                "; editor=" + (context != null && context.IsEditor) + "; heights=" + heights + "; existing editor/save/map=" +
                existingInEditor + "/" + existingOnSave + "/" + existingOnMapStart + "; " + StateLine());

        // FNV-1a over the managed tiles in id order (SortedDictionary): id, live wall/crenel/high bits, owner.
        private string StateLine()
        {
            ulong hash = 14695981039346656037UL;
            int high = 0;
            Span<int> logic = world.Tiles.GetLogicLayer();
            int mask = (int)(TilePropertyFlag.IsWall | TilePropertyFlag.CrenelatedLow | TilePropertyFlag.CrenelatedHigh);
            foreach (var pair in managed)
            {
                int id = pair.Key;
                int bits = id >= 0 && id < logic.Length ? logic[id] & mask : -1;
                if (bits > 0 && (bits & (int)TilePropertyFlag.CrenelatedHigh) != 0) high++;
                hash = (hash ^ (uint)id) * 1099511628211UL;
                hash = (hash ^ (uint)bits) * 1099511628211UL;
                hash = (hash ^ (uint)pair.Value.Owner) * 1099511628211UL;
            }
            return "managed=" + managed.Count + "; high=" + high + "; hash=" + hash.ToString("X16");
        }
    }
}
#endif
