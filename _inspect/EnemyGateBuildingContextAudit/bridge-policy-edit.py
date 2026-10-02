from pathlib import Path

root = Path('Testmods/EnemyGatePathfindingTest')
def write(p, s):
    expected = s.replace('\r\n', '\n').replace('\n', '\r\n')
    p.write_bytes(expected.encode('utf-8'))
    assert p.read_bytes().decode('utf-8') == expected
def replace(s, old, new):
    assert old in s, old[:100]
    return s.replace(old, new, 1)

p = root / 'src/GateTopologySnapshotProvider.cs'
s = p.read_text(encoding='utf-8-sig')
s = replace(s, 'if (TryResolvePassageAxis(info, gateInfo, linkedBridge,', 'if (TryResolvePassageAxis(info, gateInfo, linkedBridge,')
s = replace(s, 'out bool horizontalPassage, out PassageAxisSource axisSource))', 'out bool horizontalPassage, out PassageAxisSource axisSource) || isBridge)')
s = replace(s, 'tiles, info.Tiles, horizontalPassage, masks, ownership, info.GateId,', 'tiles, info.Tiles, masks, ownership, info.GateId, info.BridgeId,')
s = replace(s, 'isGate ? "entry-exit-outer" : "center"', 'isGate ? "entry-exit-outer" : "native-closure-cells"')
start = s.index('        private static int ClearDrawbridgePassageDirections(')
end = s.index('        private static int ClearBoundary(', start)
s = s[:start] + '''        private static int ClearDrawbridgePassageDirections(
            GameTileManagerAPI tiles, TileDiagnostic[] diagnostics,
            byte[] masks, GateEdgeOwnership ownership, int gateId, int bridgeId,
            out int firstFrom, out int firstTo, out int firstDirection,
            out int secondFrom, out int secondTo, out int secondDirection)
        {
            firstFrom = firstTo = firstDirection = -1;
            secondFrom = secondTo = secondDirection = -1;
            int changed = 0;
            foreach (TileDiagnostic tile in diagnostics)
            {
                if (!tile.ClosedBridgeCell) continue;
                if (firstFrom < 0)
                {
                    firstFrom = tile.TileId;
                    firstTo = tiles.GetTileId(tile.X, tile.Y - 1);
                    firstDirection = 0;
                }
                changed += DrawbridgeClosurePolicy.BlockCell(masks, tile.TileId, tile.X, tile.Y,
                    tiles.GetTileId, ownership, gateId, bridgeId);
            }
            return changed;
        }

''' + s[end:]
s = replace(s, 'diagnostics = BuildTileDiagnostics(tiles, footprint);', '''HashSet<int> closedCells = null;
            if (building.r_BuildingType == eStructs.STRUCT_DRAWBRIDGE)
            {
                closedCells = new HashSet<int>();
                if (!TryReadDrawbridgeClosure(tiles, ref building, closedCells, out _)) return false;
            }
            diagnostics = BuildTileDiagnostics(tiles, footprint, closedCells);''')
pos = s.index('        private static bool TryReadSparseFootprint(')
s = s[:pos] + '''        private static bool TryReadDrawbridgeClosure(GameTileManagerAPI tiles,
            ref GameBuilding building, HashSet<int> destination, out ulong signature)
        {
            signature = 14695981039346656037UL;
            int orientation = building.r_SpriteVariationIndex;
            if (building.r_OccupyTileGridSize != 5 || (uint)orientation > 7) return false;
            Span<ushort> moatIndex = tiles.GetMoatWorkTaskIndexLayer();
            fixed (GameBuilding* pointer = &building)
            {
                uint* occupied = &pointer->r_OccupiedTileIdsArrayBegin;
                for (int cell = 0; cell < 25; cell++)
                {
                    if (!DrawbridgeClosurePolicy.IsClosureCell(orientation, cell)) continue;
                    uint rawTile = occupied[cell];
                    if (rawTile == 0 || rawTile > int.MaxValue ||
                        rawTile >= moatIndex.Length || !tiles.IsValidTileId((int)rawTile)) return false;
                    bool closed = moatIndex[(int)rawTile] != 0;
                    signature = MixSignature(signature, rawTile);
                    signature = MixSignature(signature, closed ? 1u : 0u);
                    if (closed) destination?.Add((int)rawTile);
                }
            }
            return true;
        }

''' + s[pos:]
s = replace(s, 'GameTileManagerAPI tiles, HashSet<int> footprint)\n', 'GameTileManagerAPI tiles, HashSet<int> footprint, HashSet<int> closedCells = null)\n')
s = replace(s, 'tiles.IsTileWalkableAndUnoccupied(tileId)));', 'tiles.IsTileWalkableAndUnoccupied(tileId), closedCells?.Contains(tileId) == true));')
s = replace(s, 'int buildingId, int flags, bool walkable)', 'int buildingId, int flags, bool walkable, bool closedBridgeCell = false)')
s = replace(s, 'BuildingId = buildingId; Flags = flags; Walkable = walkable; }', 'BuildingId = buildingId; Flags = flags; Walkable = walkable; ClosedBridgeCell = closedBridgeCell; }')
s = replace(s, 'internal bool Walkable { get; }', 'internal bool Walkable { get; }\n            internal bool ClosedBridgeCell { get; }')
s = replace(s, ':w" + (Walkable ? 1 : 0);', ':w" + (Walkable ? 1 : 0) + ":closureCell=" + (ClosedBridgeCell ? 1 : 0);')
s = replace(s, 'signature = MixSignature(signature, (uint)(footprint.Fingerprint >> 32));', '''signature = MixSignature(signature, (uint)(footprint.Fingerprint >> 32));
                    if (building.r_BuildingType == eStructs.STRUCT_DRAWBRIDGE)
                    {
                        bool validClosure = TryReadDrawbridgeClosure(tiles, ref building, null, out ulong closure);
                        signature = MixSignature(signature, validClosure ? 1u : 0u);
                        signature = MixSignature(signature, (uint)closure);
                        signature = MixSignature(signature, (uint)(closure >> 32));
                    }''')
s = replace(s, 'hash = (hash ^ (uint)tile.TileId) * 1099511628211UL;', '''{
                        hash = (hash ^ (uint)tile.TileId) * 1099511628211UL;
                        hash = (hash ^ (tile.ClosedBridgeCell ? 1u : 0u)) * 1099511628211UL;
                    }''')
s = replace(s, 'if (tile.Footprint) leftTiles.Add(tile.TileId);', 'if (tile.Footprint) leftTiles.Add(tile.TileId * 2 + (tile.ClosedBridgeCell ? 1 : 0));')
s = replace(s, 'if (tile.Footprint) rightTiles.Add(tile.TileId);', 'if (tile.Footprint) rightTiles.Add(tile.TileId * 2 + (tile.ClosedBridgeCell ? 1 : 0));')
s = replace(s, '.Append("/barrier=").Append(barrier)', '.Append("/gateId=").Append(info.GateId).Append("/gateGlobal=").Append(info.GateGlobal)\n                .Append("/bridgeId=").Append(info.BridgeId).Append("/bridgeGlobal=").Append(info.BridgeGlobal)\n                .Append("/barrier=").Append(barrier)')
# Precompute diagnostic candidates per snapshot; no live lookups at search callbacks.
pos = s.index('        internal void SetGateAccess')
s = s[:pos] + '''        internal string DescribeBridgePclCandidates(int player, int pcl)
        {
            TopologySnapshot current = snapshot;
            return current.BridgeCandidates.TryGetValue(((long)player << 32) | (uint)pcl,
                out string candidates) ? candidates : "none";
        }

''' + s[pos:]
needle = 'internal ulong Fingerprint { get; }'
pos = s.index(needle, s.index('private sealed class TopologySnapshot'))
s = s[:pos] + '''internal readonly Dictionary<long, string> BridgeCandidates = new Dictionary<long, string>();
            private void BuildBridgeCandidates()
            {
                foreach (GateBridgeInfo info in Combinations)
                {
                    if (info.BridgeId <= 0) continue;
                    for (int player = 1; player <= 8; player++)
                    foreach (int pcl in info.RelevantPcls)
                    {
                        if (pcl <= 0) continue;
                        long key = ((long)player << 32) | (uint)pcl;
                        string subject = "gate=" + info.GateId + "/global=" + info.GateGlobal +
                            "/bridge=" + info.BridgeId + "/global=" + info.BridgeGlobal +
                            "/policy=" + (info.UnrelatedByPlayer[player] ? "blocked" : "allowed");
                        BridgeCandidates.TryGetValue(key, out string previous);
                        BridgeCandidates[key] = previous == null ? subject : previous + ";" + subject;
                    }
                }
            }
            ''' + s[pos:]
s = replace(s, 'RoutePolicy = routePolicy ?? RouteTilePolicySnapshot.Empty; }', 'RoutePolicy = routePolicy ?? RouteTilePolicySnapshot.Empty; BuildBridgeCandidates(); }')
write(p, s)

for name in ['EnemyGatePathfindingTest.csproj', 'EnemyGatePathfindingTest.PolicyTests.csproj']:
    p = root / name; s = p.read_text(encoding='utf-8-sig')
    s = replace(s, '<Compile Include="src\\GateEdgeOwnership.cs" />', '<Compile Include="src\\GateEdgeOwnership.cs" />\n    <Compile Include="src\\DrawbridgeClosurePolicy.cs" />')
    if 'PolicyTests' in name:
        s = replace(s, '<Compile Include="tests\\Program.cs" />', '<Compile Include="tests\\Program.cs" />\n    <Compile Include="tests\\DrawbridgeClosureTests.cs" />')
    write(p, s)

p = root / 'src/AttackOrderCorrelationDiagnostics.cs'; s = p.read_text(encoding='utf-8-sig')
s = replace(s, '"edge=" + fromTile + "->" + toTile + ",direction=" + direction +', '"bridgeId=" + (frame.Probe.Snapshot.EdgeOwners?[player]?.ResolveBridge(fromTile, direction) ?? 0) +\n                ",edge=" + fromTile + "->" + toTile + ",direction=" + direction +')
s = replace(s, 'Frame frame = Current();\n                if (frame != null && frame.Player != player) frame = null;', '''Frame frame = Current();
                if (frame != null && frame.Player != player) frame = null;
                string stage = sourceComponentId > 0 && sourceComponentId == destinationComponentId
                    ? "same-pcl" : "regions";
                string sourceBridges = topology.DescribeBridgePclCandidates(player, sourceComponentId);
                string targetBridges = topology.DescribeBridgePclCandidates(player, destinationComponentId);
                if (sourceBridges != "none" || targetBridges != "none")
                    totals.RecordGateState(player, 0, "bridge-reachability",
                        "searchStage=" + stage + ",source=" + source +
                        ",vanilla=" + Result(vanillaResult) + ",effective=" + Result(effectiveResult),
                        frame?.Command ?? 0, frame?.Tribe ?? 0, sourceComponentId, destinationComponentId,
                        "attribution=pcl-candidates-only,sourceBridges=[" + sourceBridges +
                        "],targetBridges=[" + targetBridges + "]");''')
s = replace(s, '"source=" + source + ",vanilla=" + Result(vanillaResult) +', '"searchStage=" + stage + ",source=" + source + ",vanilla=" + Result(vanillaResult) +')
write(p, s)

p = root / 'tests/Program.cs'; s = p.read_text(encoding='utf-8-sig')
s = replace(s, 'assertions += GateRoutePolicyTests.Run();', 'assertions += GateRoutePolicyTests.Run();\n                assertions += DrawbridgeClosureTests.Run();')
s = replace(s, '''Assert(bridge.IndexOf("(minY + maxY) >> 1", StringComparison.Ordinal) >= 0 &&
                    bridge.IndexOf("(minX + maxX) >> 1", StringComparison.Ordinal) >= 0,
                "drawbridge keeps its proven middle seam");''', '''Assert(bridge.IndexOf("DrawbridgeClosurePolicy.BlockCell", StringComparison.Ordinal) >= 0 &&
                    bridge.IndexOf("tile.ClosedBridgeCell", StringComparison.Ordinal) >= 0 &&
                    bridge.IndexOf("horizontalPassage", StringComparison.Ordinal) < 0,
                "drawbridge isolates only native closure cells independently of gate axis");''')
write(p, s)
print('Targeted bridge policy edits complete')
