using System;
using System.Collections.Generic;
using APIShared.Pathfinding;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace MoatMove
{
    // Consumer of APIShared route tile rules (option AvoidPlague): every tile where a plague cloud would
    // damage a unit standing there (PlagueDamageModel) costs the largest entry penalty for every player, with
    // a replan policy of its own (PlagueWaitSeconds; abort after the wait like fire). The tiles also feed
    // the escape (plagueTiles) and their 7 x 7 margin around each cloud (plagueNear: clouds drift) counts
    // as front for escape targets.
    // Clouds are type-22 records (ProjectileType.Disease) of the effect manager FireRouteRules binds, in
    // state 2 (damaging until state 3, fade included). A cloud steps 1/8 tile every 31 ticks; the tile of
    // its next step (direction +0xB0, kept while +0xB2 steps are left) is marked ahead. A cloud an
    // apothecary removes leaves the set with the next scan. Read on the simulation tick, never written.
    internal sealed unsafe class PlagueRouteRules
    {
        private const int ScanInterval = 4, MapSize = RouteTileRules.MapSize, Penalty = RouteTileRules.MaximumPenalty - 1, NearRadius = 3;
        // FBCB9319 record fields (UpdateToNewDLL.md, "Plague clouds"): +0x3C state, +0x3E type, +0x4C/+0x4E
        // position in 1/8 tiles, +0xB0 step direction 0..7, +0xB2 steps left in that direction.
        private const int EffectSlots = 6000, EffectStride = 0xE8, EffectState = 0x3C, EffectType = 0x3E, EffectSubX = 0x4C, EffectSubY = 0x4E,
            EffectDirection = 0xB0, EffectStepsLeft = 0xB2, ActiveState = 2;
        // 0x9F960's step per direction: 0 N, 1 NE, 2 E, 3 SE, 4 S, 5 SW, 6 W, 7 NW.
        private static readonly int[] StepX = { 0, 1, 1, 1, 0, -1, -1, -1 }, StepY = { -1, -1, 0, 1, 1, 1, 0, -1 };
        // The effective APIShared plague height policy (all owners, e.g. ExtraFeatures' PlagueDamage settings).
        private readonly APIShared.ModApiClient api;
        private bool policyUnavailable;

        private readonly HazardTileSet tiles, near;
        private readonly PlagueDamageModel model;
        private byte* effects;
        private RouteTileRuleSet rules;
        private HashSet<int> marked = new HashSet<int>(), current = new HashSet<int>(), nearMarked = new HashSet<int>(), nearCurrent = new HashSet<int>();
        internal int Clouds { get; private set; }
        internal bool PolicyActive { get; private set; }

        internal PlagueRouteRules(HazardTileSet tiles, HazardTileSet near, PlagueDamageModel model, APIShared.ModApiClient api)
        { this.tiles = tiles; this.near = near; this.model = model; this.api = api; }

        // The policy the native damage path applies right now; inactive (Vanilla) while APIShared's plague
        // service is unavailable. Owners set it before or after simulation, identically on all peers.
        private PlagueHeightRules ReadPolicy()
        {
            if (api == null || policyUnavailable) return default;
            try { return ReadApiSharedPolicy(); }
            catch (Exception ex) when (ex is MissingMethodException || ex is TypeLoadException) { policyUnavailable = true; return default; }
        }

        // Separate method: an APIShared older than the policy reader fails here when compiled, not in Tick.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private PlagueHeightRules ReadApiSharedPolicy()
        {
            if (!api.TryGetPlagueHeightPolicy(out APIShared.PlagueHeightPolicy policy, out _)) return default;
            return new PlagueHeightRules { Walls = policy.WallsTolerance + 1, Gates = policy.GatesTolerance + 1,
                Towers = policy.TowersTolerance + 1, Keeps = policy.KeepsTolerance + 1 };
        }

        internal void Bind(byte* effectManager) => effects = effectManager;

        internal bool TryRegister(string ownerGuid, RouteReplanPolicy policy, out string reason)
        {
            if (!RouteTileRules.TryRegister(ownerGuid, "plague", out rules, out reason)) return false;
            rules.ReplanPolicy = policy;
            return true;
        }

        internal void Tick(int tick)
        {
            if (rules == null || effects == null || tick % ScanInterval != 0) return;
            PlagueHeightRules policy = ReadPolicy();
            PolicyActive = policy.Active;
            current.Clear(); nearCurrent.Clear();
            int clouds = 0;
            for (int slot = 1; slot < EffectSlots; slot++)
            {
                byte* record = effects + slot * EffectStride;
                if (*(short*)(record + EffectState) != ActiveState || *(ushort*)(record + EffectType) != (ushort)ProjectileType.Disease) continue;
                clouds++;
                int subX = *(short*)(record + EffectSubX), subY = *(short*)(record + EffectSubY);
                int x = subX >> 3, y = subY >> 3;
                Mark(x, y, policy);
                // The next step keeps its direction while steps are left; at 0 a new direction is drawn.
                int direction = *(short*)(record + EffectDirection), steps = *(short*)(record + EffectStepsLeft);
                if (steps > 1 && (uint)direction < 8)
                {
                    int nextX = (subX + StepX[direction]) >> 3, nextY = (subY + StepY[direction]) >> 3;
                    if (nextX != x || nextY != y) Mark(nextX, nextY, policy);
                }
            }
            Clouds = clouds;
            foreach (int node in current)
                if (!marked.Contains(node)) { rules.Set(node % MapSize, node / MapSize, RouteTileEffect.Penalty, Penalty); tiles.Set(node, true); }
            foreach (int node in marked)
                if (!current.Contains(node)) { rules.Clear(node % MapSize, node / MapSize); tiles.Set(node, false); }
            HashSet<int> swap = marked; marked = current; current = swap;
            foreach (int node in nearCurrent) if (!nearMarked.Contains(node)) near.Set(node, true);
            foreach (int node in nearMarked) if (!nearCurrent.Contains(node)) near.Set(node, false);
            swap = nearMarked; nearMarked = nearCurrent; nearCurrent = swap;
        }

        // The 5 x 5 damage area of a cloud on (x, y) (0x9C730: tile, 8 neighbours, 16-tile ring) and its margin.
        private void Mark(int x, int y, PlagueHeightRules policy)
        {
            if ((uint)x >= MapSize || (uint)y >= MapSize) return;
            GameTileManagerAPI api = GameTileManagerAPI.Instance;
            int cloudTile = api.GetTileId(x, y);
            if (cloudTile < 0) return;
            for (int dy = -NearRadius; dy <= NearRadius; dy++)
                for (int dx = -NearRadius; dx <= NearRadius; dx++)
                {
                    int tx = x + dx, ty = y + dy;
                    if ((uint)tx >= MapSize || (uint)ty >= MapSize) continue;
                    int node = ty * MapSize + tx;
                    nearCurrent.Add(node);
                    int ring = Math.Max(Math.Abs(dx), Math.Abs(dy));
                    if (ring > 2) continue;
                    int tile = api.GetTileId(tx, ty);
                    if (tile >= 0 && model.WouldDamage(cloudTile, tile, ring, policy)) current.Add(node);
                }
        }

        // A new map: the old map's clouds no longer apply.
        internal void Reset()
        {
            rules?.ClearAll(); tiles.Clear(); near.Clear(); marked.Clear(); current.Clear(); nearMarked.Clear(); nearCurrent.Clear();
            Clouds = 0;
        }

        // Production terrain: BugfixesAndQoL's bound layers and the Extender's building records.
        internal sealed class NativeTerrain : IPlagueTerrain
        {
            private readonly uint* flags; private readonly byte* heights; private readonly ushort* buildings;
            internal NativeTerrain(uint* flags, byte* heights, ushort* buildings) { this.flags = flags; this.heights = heights; this.buildings = buildings; }
            public uint Flags(int tile) => flags[tile];
            public int RawHeight(int tile) => heights[tile];
            public int DefaultHeight(int tile) => GameTileManagerAPI.Instance.GetTileDefaultHeight(tile);
            public int BuildingId(int tile) => (short)buildings[tile];
            public bool TryGetBuilding(int buildingId, out eStructs type, out int baseHeight)
            {
                type = default; baseHeight = 0;
                if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out GameBuilding* building) || building == null) return false;
                type = building->r_BuildingType; baseHeight = building->r_HeightElevation;
                return true;
            }
        }
    }
}
