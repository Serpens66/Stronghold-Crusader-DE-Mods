using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace EnemyBridgePathTest
{
    internal sealed unsafe class BridgeSnapshot
    {
        internal static readonly BridgeSnapshot Empty = new BridgeSnapshot(Array.Empty<Bridge>());
        internal readonly Bridge[] Bridges;
        private BridgeSnapshot(Bridge[] bridges) { Bridges = bridges; }
        internal bool SameState(BridgeSnapshot previous)
        {
            if (previous == null || previous.Bridges.Length != Bridges.Length) return false;
            for (int i = 0; i < Bridges.Length; i++)
            {
                if (Bridges[i].State != previous.Bridges[i].State) return false;
                for (int player = 0; player <= 8; player++)
                    if (Bridges[i].Relations[player] != previous.Bridges[i].Relations[player]) return false;
            }
            return true;
        }
        internal sealed class Bridge
        {
            internal int Id, GateId, Owner, Captured;
            internal uint Global, GateGlobal;
            internal string State, Link;
            internal GateEdgeOwnership Edges;
            internal string[] Relations;
            internal string Describe(int player) => State + "," + ((uint)player < (uint)Relations.Length ? Relations[player] : "ownerRelation=unknown,captureRelation=unknown");
        }
        private sealed class Gate
        {
            internal int Id, Owner, Captured;
            internal uint Global;
            internal GameBuilding Value;
            internal readonly HashSet<int> Tiles = new HashSet<int>();
        }
        internal static bool Active(AliveState value) => value == AliveState.IsAlive || value == AliveState.NeedsInit;
        internal static bool IsGate(eStructs type) => type == eStructs.STRUCT_GATEHOUSE || type == eStructs.STRUCT_GATE_MAIN ||
            type == eStructs.STRUCT_GATE_INNER || type == eStructs.STRUCT_GATE_WOOD || type == eStructs.STRUCT_GATE_POSTERN;
        private static bool Player(int id) => id > 0 && id <= 8;
        internal static bool Allied(int a, int b) => Player(a) && Player(b) && (a == b || GamePlayerManagerAPI.Instance.IsPlayerAlliedTo(a, b));
        internal static HashSet<int> Footprint(GameBuilding* value)
        {
            var result = new HashSet<int>();
            // Audited inline occupied array capacity is 6x6 (36 cells), not the
            // size of the whole building record. Do not read following fields as tiles.
            int grid = (int)value->r_OccupyTileGridSize;
            if (grid <= 0 || grid > 6) return result;
            uint* occupied = &value->r_OccupiedTileIdsArrayBegin;
            for (int i = 0; i < grid * grid; i++)
                if (occupied[i] > 0 && occupied[i] <= int.MaxValue && GameTileManagerAPI.Instance.IsValidTileId((int)occupied[i])) result.Add((int)occupied[i]);
            return result;
        }
        internal static BridgeSnapshot Capture()
        {
            var manager = GameBuildingManagerAPI.Instance;
            var tiles = GameTileManagerAPI.Instance;
            var buildings = manager.GetBuildingsAsSpan();
            var gates = new Dictionary<int, Gate>();
            for (int spanIndex = 0; spanIndex < buildings.Length; spanIndex++)
            {
                ref GameBuilding building = ref buildings[spanIndex];
                if (!Active(building.r_AliveState) || !IsGate(building.r_BuildingType)) continue;
                var gate = new Gate { Id = spanIndex + 1, Global = building.r_GlobalId, Owner = building.r_PlayerIdOwner,
                    Captured = building.r_CapturedByPlayerId, Value = building };
                fixed (GameBuilding* pointer = &building) gate.Tiles.UnionWith(Footprint(pointer));
                gates.Add(gate.Id, gate);
            }
            var result = new List<Bridge>();
            var scratch = new byte[tiles.GetMoatWorkTaskIndexLayer().Length];
            for (int i = 0; i < scratch.Length; i++) scratch[i] = 255;
            for (int spanIndex = 0; spanIndex < buildings.Length; spanIndex++)
            {
                ref GameBuilding building = ref buildings[spanIndex];
                if (building.r_BuildingType != eStructs.STRUCT_DRAWBRIDGE || !Active(building.r_AliveState)) continue;
                var bridge = new Bridge { Id = spanIndex + 1, Global = building.r_GlobalId, Link = "unresolved",
                    Edges = new GateEdgeOwnership(), Relations = new string[9] };
                HashSet<int> footprint;
                fixed (GameBuilding* pointer = &building) footprint = Footprint(pointer);
                Gate parent = null;
                if (gates.TryGetValue(building.r_GatehouseId, out var nativeParent) && nativeParent.Owner == building.r_PlayerIdOwner)
                { parent = nativeParent; bridge.Link = "native-building-id"; }
                else
                {
                    int candidates = 0;
                    foreach (var gate in gates.Values)
                    {
                        if (gate.Owner != building.r_PlayerIdOwner || !Adjacent(footprint, gate.Tiles)) continue;
                        parent = gate; candidates++;
                    }
                    if (candidates == 1) bridge.Link = "unique-footprint-adjacency";
                    else { parent = null; bridge.Link = candidates == 0 ? "unlinked" : "ambiguous-adjacency"; }
                }
                if (parent != null) { bridge.GateId = parent.Id; bridge.GateGlobal = parent.Global;
                    bridge.Owner = parent.Owner; bridge.Captured = parent.Captured; }
                bool identityStable = bridge.Global != 0 && (parent == null || parent.Global != 0) &&
                    manager.IsValidId(bridge.Id) && manager.TryGetBuildingById(bridge.Id, out GameBuilding* liveBridge) &&
                    liveBridge != null && liveBridge->r_GlobalId == bridge.Global && Active(liveBridge->r_AliveState);
                if (parent != null && (!manager.IsValidId(parent.Id) || !manager.TryGetBuildingById(parent.Id, out GameBuilding* liveGate) ||
                    liveGate == null || liveGate->r_GlobalId != parent.Global || !Active(liveGate->r_AliveState) ||
                    liveGate->r_PlayerIdOwner != parent.Owner || liveGate->r_CapturedByPlayerId != parent.Captured)) identityStable = false;
                if (!identityStable) { bridge.Link = "identity-or-live-state-mismatch"; parent = null; }
                var state = new StringBuilder("bridge=").Append(bridge.Id).Append("/g").Append(bridge.Global)
                    .Append(",parentGate=").Append(bridge.GateId).Append("/g").Append(bridge.GateGlobal)
                    .Append(",link=").Append(bridge.Link).Append(",rawGatehouseId=").Append(building.r_GatehouseId)
                    .Append(",bridgeOwner=").Append(building.r_PlayerIdOwner).Append(",owner=").Append(bridge.Owner)
                    .Append(",bridgeCapturerRaw=").Append(building.r_CapturedByPlayerId)
                    .Append(",capturer=").Append(bridge.Captured).Append(",bridgeAliveRaw=").Append((int)building.r_AliveState)
                    .Append(",bridgeGateStateRaw=").Append(building.r_GateState).Append(",orientation=").Append(building.r_SpriteVariationIndex)
                    .Append(",grid=").Append(building.r_OccupyTileGridSize);
                if (parent != null) state.Append(",gateStateRaw=").Append(parent.Value.r_GateState)
                    .Append(",gateAIWalkableRaw=").Append(parent.Value.r_AIWalkableState);
                var pcl = GamePathingManagerAPI.Instance.GetPathComponentGrid();
                var moat = tiles.GetMoatWorkTaskIndexLayer();
                state.Append(",occupiedRaw=[");
                int rawGrid = (int)building.r_OccupyTileGridSize;
                if (rawGrid > 0 && rawGrid <= 6)
                {
                    fixed (GameBuilding* pointer = &building)
                    {
                        uint* rawTiles = &pointer->r_OccupiedTileIdsArrayBegin;
                        for (int i = 0; i < rawGrid * rawGrid; i++)
                            state.Append(i).Append(':').Append(rawTiles[i]).Append(';');
                    }
                }
                else state.Append("invalid-grid;");
                state.Append(']');
                var ordered = new List<int>(footprint); ordered.Sort();
                state.Append(",footprint=[");
                foreach (int tile in ordered)
                {
                    var xy = tiles.GetTileVectorFromId(tile);
                    state.Append(tile).Append('@').Append(xy.X).Append('/').Append(xy.Y).Append(":pcl=")
                        .Append((uint)tile < (uint)pcl.Length ? pcl[tile] : -1).Append(":flags=0x")
                        .Append(((uint)tiles.GetTilePropertyFlag(tile)).ToString("X8")).Append(';');
                }
                state.Append("],closure=[");
                bool shape = building.r_OccupyTileGridSize == 5 && building.r_SpriteVariationIndex <= 7;
                if (shape)
                {
                    fixed (GameBuilding* pointer = &building)
                    {
                        uint* occupied = &pointer->r_OccupiedTileIdsArrayBegin;
                        for (int i = 0; i < 25; i++)
                        {
                            if (!DrawbridgeClosurePolicy.IsClosureCell(building.r_SpriteVariationIndex, i)) continue;
                            uint raw = occupied[i];
                            if (raw == 0 || raw > int.MaxValue || raw >= moat.Length || !tiles.IsValidTileId((int)raw))
                            { state.Append("invalidCell=").Append(i).Append(":raw=").Append(raw).Append(';'); continue; }
                            int tile = (int)raw;
                            state.Append(tile).Append(":moatRecord=").Append(moat[tile]).Append(';');
                            if (moat[tile] == 0) continue;
                            var xy = tiles.GetTileVectorFromId(tile);
                            DrawbridgeClosurePolicy.BlockCell(scratch, tile, xy.X, xy.Y, tiles.GetTileId, bridge.Edges, bridge.GateId, bridge.Id);
                        }
                    }
                }
                else state.Append("unknown-shape;");
                state.Append("],mask=hypothetical-only");
                bridge.State = state.ToString();
                for (int player = 0; player <= 8; player++)
                {
                    if (!Player(player) || parent == null || !Player(bridge.Owner) || bridge.Captured < 0 || bridge.Captured > 8)
                    { bridge.Relations[player] = "ownerRelation=unknown,captureRelation=unknown,hypotheticalPolicy=unknown"; continue; }
                    bridge.Relations[player] = "ownerRelation=" + (player == bridge.Owner ? "own" : Allied(player, bridge.Owner) ? "allied" : "enemy") +
                        ",captureRelation=" + (bridge.Captured == 0 ? "uncaptured" : player == bridge.Captured ? "captured-by-self" :
                        Allied(player, bridge.Captured) ? "captured-by-ally" : "captured-by-other") + ",hypotheticalPolicy=" +
                        (Allied(player, bridge.Owner) || Allied(player, bridge.Captured) ? "allow" : "restrict") + ",classificationSource=parent-snapshot";
                }
                result.Add(bridge);
            }
            return new BridgeSnapshot(result.ToArray());
        }
        internal static bool Adjacent(HashSet<int> a, HashSet<int> b)
        {
            var api = GameTileManagerAPI.Instance;
            foreach (int tileA in a)
            {
                var xy = api.GetTileVectorFromId(tileA);
                foreach (int tileB in b)
                {
                    var other = api.GetTileVectorFromId(tileB);
                    if (Math.Abs(xy.X - other.X) + Math.Abs(xy.Y - other.Y) == 1) return true;
                }
            }
            return false;
        }
        internal string DescribeEdge(int player, int tile, int direction, bool climb)
        {
            var ids = new List<string>();
            foreach (var bridge in Bridges)
                if (bridge.Edges.ResolveBridge(tile, direction) != 0)
                    ids.Add(bridge.Id + "/g" + bridge.Global + ":gate=" + bridge.GateId + ":" +
                        ((uint)player < 9 ? bridge.Relations[player] : "unknown-player"));
            return (climb ? "climb" : "ground") + ",hypothetical=" + (ids.Count == 0 ? "outside-closure" : string.Join(";", ids));
        }
    }
}
