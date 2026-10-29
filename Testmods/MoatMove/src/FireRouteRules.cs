using System.Collections.Generic;
using APIShared.Pathfinding;
using SHCDESE.API;
using SHCDESE.Interop.Enums;

namespace MoatMove
{
    // Consumer of APIShared route tile rules (option AvoidFire): burning pitch tiles cost the largest
    // entry penalty for every player, so engine routes avoid fire whenever a detour exists. The
    // replan policy covers units already walking when the fire starts: re-plan 3 steps before the
    // fire; a detour may add 50 % or at least 10 steps, otherwise wait up to about 10 s (400 ticks).
    // Only public APIShared and Extender API; the pitch array is read on the simulation tick.
    internal sealed class FireRouteRules
    {
        private const int FirePenalty = RouteTileRules.MaximumPenalty - 1, ScanInterval = 4;
        private RouteTileRuleSet rules;
        // Marked tiles and this scan's burning tiles, packed y * MapSize + x; swapped per scan.
        private HashSet<int> marked = new HashSet<int>(), burning = new HashSet<int>();
        internal int Burning => marked.Count;
        internal long Scans, Changes;

        internal bool TryRegister(string ownerGuid, out string reason)
        {
            if (!RouteTileRules.TryRegister(ownerGuid, "fire", out rules, out reason)) return false;
            rules.ReplanPolicy = new RouteReplanPolicy(3, 50, 10, 400);
            return true;
        }

        internal void Tick(int tick)
        {
            if (rules == null || tick % ScanInterval != 0) return;
            Scans++;
            burning.Clear();
            var pitches = GamePitchManagerAPI.Instance.GetPitchArrayAsSpan();
            for (int id = 0; id < pitches.Length; id++)
            {
                ref var pitch = ref pitches[id];
                if (!pitch.IsAlive || pitch.State != PitchState.OnFire || pitch.TileX >= RouteTileRules.MapSize || pitch.TileY >= RouteTileRules.MapSize) continue;
                int key = pitch.TileY * RouteTileRules.MapSize + pitch.TileX;
                if (burning.Add(key) && !marked.Contains(key) && rules.Set(pitch.TileX, pitch.TileY, RouteTileEffect.Penalty, FirePenalty)) Changes++;
            }
            foreach (int key in marked)
                if (!burning.Contains(key) && rules.Clear(key % RouteTileRules.MapSize, key / RouteTileRules.MapSize)) Changes++;
            HashSet<int> swap = marked; marked = burning; burning = swap;
        }

        // A new map: the old map's fire no longer applies.
        internal void Reset() { rules?.ClearAll(); marked.Clear(); burning.Clear(); }
    }
}
