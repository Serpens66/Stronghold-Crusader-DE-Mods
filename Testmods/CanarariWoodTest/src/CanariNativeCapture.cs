using AIBuildDiagnoseTest;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace CanarariWoodTest
{
    // Existing hash-bound readers moved intact; no native functions are called here.
    internal sealed class CanariNativeCapture : IAiBuildDetailCapture
    {
        private const int PlayerStride = 0x583C;
        private readonly long moduleBase;
        internal CanariNativeCapture(long address, string hash)
        {
            moduleBase = string.Equals(hash, NativeDiagnosticContracts.SupportedHash,
                StringComparison.OrdinalIgnoreCase) ? address : 0;
        }
        public AiEconomyGridEvidence CaptureEconomyGridEvidence(ulong state, int mode)
        {
            if (!AiBuildDiagnostic.HasObserver || state == 0 || moduleBase == 0)
                return new AiEconomyGridEvidence("unavailable", state, mode, -1, -1, -1, -1, -1, null);
            try
            {
                // Same hash-bound AIV fields already used by LogFirstSearchOrigin and the wood replay.
                int x = Marshal.ReadInt32(new IntPtr(checked((long)state + 0x18783C)));
                int y = Marshal.ReadInt32(new IntPtr(checked((long)state + 0x1A083C)));
                return CaptureEconomyGridEvidence(state, mode, x, y);
            }
            catch (Exception ex)
            {
                return new AiEconomyGridEvidence("search-origin-unavailable:" + ex.GetType().Name,
                    state, mode, -1, -1, -1, -1, -1, null);
            }
        }

        /// <summary>Diagnostic-only snapshot of a selected coarse cell in the current AIV system.</summary>
        public unsafe AiEconomyGridEvidence CaptureEconomyGridEvidence(int coarseX, int coarseY)
        {
            if (!AiBuildDiagnostic.HasObserver)
                return new AiEconomyGridEvidence("unavailable", 0, -1, coarseX, coarseY,
                    -1, -1, -1, null);
            try
            {
                AivSystem* system = GameAIVManagerAPI.Instance.GetAIVSystemPointer();
                return CaptureEconomyGridEvidence((ulong)system, -1, coarseX, coarseY);
            }
            catch (Exception ex)
            {
                return new AiEconomyGridEvidence("capture-exception:" + ex.GetType().Name,
                    0, -1, coarseX, coarseY, -1, -1, -1, null);
            }
        }

        /// <summary>Diagnostic-only snapshot of any valid 5-by-5 coarse cell.</summary>
        public AiEconomyGridEvidence CaptureEconomyGridEvidence(ulong state, int mode,
            int coarseX, int coarseY)
        {
            if (!AiBuildDiagnostic.HasObserver || state == 0 || moduleBase == 0)
                return new AiEconomyGridEvidence("unavailable", state, mode, coarseX, coarseY,
                    -1, -1, -1, null);
            if (coarseX < 0 || coarseX >= 160 || coarseY < 0 || coarseY >= 160)
                return new AiEconomyGridEvidence("invalid-coarse-cell", state, mode, coarseX, coarseY,
                    -1, -1, -1, null);
            try
            {
                long cell = checked((long)state + 0x5B834 +
                    ((long)coarseX * 160 + coarseY) * 0x30);
                int reference = Marshal.ReadInt32(new IntPtr(checked((long)state + 0x5B504)));
                int foreign = Marshal.ReadByte(new IntPtr(cell));
                int treeWeight = Marshal.ReadByte(new IntPtr(cell + 3));
                IReadOnlyList<AiPathTileSample> tiles = CaptureTiles(coarseX * 5, coarseY * 5, 5, 5);
                bool allTilesReady = tiles.Count == 25;
                foreach (AiPathTileSample tile in tiles)
                    if (tile.Status != "ok") allTilesReady = false;
                string status = allTilesReady && reference > 0 ? "ok" : "tile-grid-not-ready";
                return new AiEconomyGridEvidence(status, state, mode, coarseX, coarseY, reference,
                    foreign, treeWeight, tiles);
            }
            catch (Exception ex)
            {
                return new AiEconomyGridEvidence("capture-exception:" + ex.GetType().Name,
                    state, mode, coarseX, coarseY, -1, -1, -1, null);
            }
        }

        public AiNearbyPathEvidence CaptureNearbyPathEvidence(ulong state,
            int inputX, int inputY, int resultX, int resultY, bool captureBefore)
        {
            AiCoarseCellSample inputCell = SampleCoarseCell(state, inputX, inputY);
            AiCoarseCellSample resultCell = resultX >= 0 && resultY >= 0
                ? SampleCoarseCell(state, resultX, resultY) : null;
            var nearbyCells = new AiCoarseCellSample[25];
            int coarseIndex = 0;
            for (int dx = -2; dx <= 2; dx++)
                for (int dy = -2; dy <= 2; dy++)
                    nearbyCells[coarseIndex++] = SampleCoarseCell(state, inputX + dx, inputY + dy);
            Span<ushort> grid = GamePathingManagerAPI.Instance.GetPathComponentGrid();
            if (grid.Length != 320800 || moduleBase == 0)
                return new AiNearbyPathEvidence("path-grid-unavailable:" + grid.Length,
                    inputX, inputY, resultX, resultY,
                    new AiPathTileSample[0], new AiPathTileSample[0], inputCell, resultCell, nearbyCells);
            var anchors = new AiPathTileSample[25];
            int index = 0;
            for (int dx = -2; dx <= 2; dx++)
                for (int dy = -2; dy <= 2; dy++)
                    anchors[index++] = SamplePathTile((inputX + dx) * 5,
                        (inputY + dy) * 5, grid);
            AiPathTileSample[] footprint = resultX >= 0 && resultY >= 0
                ? new AiPathTileSample[9] : new AiPathTileSample[0];
            index = 0;
            for (int dx = 0; dx < 3 && footprint.Length != 0; dx++)
                for (int dy = 0; dy < 3; dy++)
                    footprint[index++] = SamplePathTile(resultX * 5 + dx,
                        resultY * 5 + dy, grid);
            int[] nativeAnchors = null;
            int[] apiAnchors = null;
            byte[] coarseCells = null;
            if (captureBefore && state != 0)
            {
                const int side = 160;
                nativeAnchors = new int[side * side];
                apiAnchors = new int[side * side];
                coarseCells = new byte[side * side * 16];
                for (int coarseX = 0; coarseX < side; coarseX++)
                    for (int coarseY = 0; coarseY < side; coarseY++)
                    {
                        int snapshotIndex = coarseX * side + coarseY;
                        nativeAnchors[snapshotIndex] = -1;
                        apiAnchors[snapshotIndex] = -1;
                        long cellAddress = checked((long)state + 0x5B834 +
                            (long)snapshotIndex * 0x30);
                        Marshal.Copy(new IntPtr(cellAddress), coarseCells,
                            snapshotIndex * 16, 16);
                        int tileX = coarseX * 5;
                        int tileY = coarseY * 5;
                        if (!GameTileManagerAPI.Instance.IsTileInsideMapBounds(tileX, tileY))
                            continue;
                        int tileId = Marshal.ReadInt32(new IntPtr(checked(moduleBase +
                            0x402FF2C + (long)tileY * 12))) + tileX;
                        if ((uint)tileId >= (uint)grid.Length) continue;
                        nativeAnchors[snapshotIndex] = (ushort)Marshal.ReadInt16(
                            new IntPtr(checked(moduleBase + 0x50EC690 + (long)tileId * 2)));
                        apiAnchors[snapshotIndex] = grid[tileId];
                    }
            }
            AiPathTileSample[] coarseTiles = resultX >= 0 && resultY >= 0
                ? CaptureTiles(resultX * 5, resultY * 5, 5, 5, grid) : null;
            AiPathTileSample[] footprintRing = resultX >= 0 && resultY >= 0
                ? CaptureTiles(resultX * 5 - 1, resultY * 5 - 1, 5, 5, grid) : null;
            return new AiNearbyPathEvidence("ok", inputX, inputY, resultX, resultY,
                anchors, footprint, inputCell, resultCell, nearbyCells,
                nativeAnchors, apiAnchors, coarseCells, coarseTiles, footprintRing,
                state == 0 ? -1 : Marshal.ReadInt32(new IntPtr(checked((long)state + 0x5B504))));
        }

        /// <summary>Copies a bounded live tile rectangle for a diagnostic probe.</summary>
        public IReadOnlyList<AiPathTileSample> CaptureTiles(int x, int y,
            int width, int height)
        {
            if (!AiBuildDiagnostic.HasObserver || width < 1 || height < 1 || width > 8 || height > 8 ||
                moduleBase == 0) return Array.AsReadOnly(new AiPathTileSample[0]);
            Span<ushort> grid = GamePathingManagerAPI.Instance.GetPathComponentGrid();
            if (grid.Length != 320800) return Array.AsReadOnly(new AiPathTileSample[0]);
            return Array.AsReadOnly(CaptureTiles(x, y, width, height, grid));
        }

        private AiPathTileSample[] CaptureTiles(int x, int y, int width,
            int height, Span<ushort> grid)
        {
            var samples = new AiPathTileSample[width * height];
            int index = 0;
            for (int dy = 0; dy < height; dy++)
                for (int dx = 0; dx < width; dx++)
                    samples[index++] = SamplePathTile(x + dx, y + dy, grid);
            return samples;
        }

        private AiCoarseCellSample SampleCoarseCell(ulong state, int x, int y)
        {
            if (state == 0 || x < 0 || x >= 160 || y < 0 || y >= 160)
                return new AiCoarseCellSample(x, y, "", "invalid-coarse-cell");
            byte[] bytes = new byte[16];
            long address = checked((long)state + 0x5B834 + ((long)x * 160 + y) * 0x30);
            Marshal.Copy(new IntPtr(address), bytes, 0, bytes.Length);
            return new AiCoarseCellSample(x, y, BitConverter.ToString(bytes), "ok");
        }

        private AiPathTileSample SamplePathTile(int x, int y, Span<ushort> grid)
        {
            if (x < 0 || x >= 800 || y < 0 || y >= 800 ||
                !GameTileManagerAPI.Instance.IsTileInsideMapBounds(x, y))
                return new AiPathTileSample(x, y, -1, -1, -1, 0, -1, -1, -1, "outside-map");
            int tileId = Marshal.ReadInt32(new IntPtr(checked(moduleBase + 0x402FF2C +
                (long)y * 12))) + x;
            if ((uint)tileId >= (uint)grid.Length)
                return new AiPathTileSample(x, y, tileId, -1, -1, 0, -1, -1, -1, "tile-id-out-of-range");
            int nativeComponent = (ushort)Marshal.ReadInt16(new IntPtr(checked(
                moduleBase + 0x50EC690 + (long)tileId * 2)));
            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            Span<byte> wallOwners = tiles.GetWallOwnerLayer();
            Span<ushort> organisms = tiles.GetOrganismLayer();
            Span<CompactPlayerBitMask> occupancies = tiles.GetOccupancyLayer();
            Span<byte> heights = tiles.GetHeightLayer();
            if ((uint)tileId >= (uint)wallOwners.Length ||
                (uint)tileId >= (uint)organisms.Length ||
                (uint)tileId >= (uint)occupancies.Length ||
                (uint)tileId >= (uint)heights.Length)
                return new AiPathTileSample(x, y, tileId, nativeComponent, grid[tileId],
                    0, -1, -1, -1, "tile-layer-out-of-range");
            return new AiPathTileSample(x, y, tileId, nativeComponent, grid[tileId],
                (uint)tiles.GetTilePropertyFlag(tileId), (int)tiles.GetTileType(tileId),
                tiles.GetTileBuildingId(tileId), wallOwners[tileId], "ok",
                organisms[tileId], (byte)occupancies[tileId], heights[tileId]);
        }

        private AiRouteEvidence Unavailable(string status, int x, int y,
            int sourceTile, int targetTile) =>
            new AiRouteEvidence(status, x, y, sourceTile, targetTile,
                -1, -1, -1, -1, -1, -1, new AiRouteConnection[0]);

        public unsafe AiRouteEvidence CaptureRouteEvidence(int playerId,
            int mapperIndex, int tileX, int tileY)
        {
            // Audit: 0xC3BF0 reads the player's keep tile and the target tile through
            // the 320800-entry PCL grid. The map's XY raster is 800 by 800.
            if (playerId < 1 || playerId > 8)
                return Unavailable("invalid-player", tileX, tileY, -1, -1);
            if (tileX < 0 || tileX >= 800 || tileY < 0 || tileY >= 800)
                return Unavailable("outside-800-raster", tileX, tileY, -1, -1);
            if (!GameTileManagerAPI.Instance.IsTileInsideMapBounds(tileX, tileY))
                return Unavailable("outside-playable-diamond", tileX, tileY, -1, -1);
            Span<ushort> grid = GamePathingManagerAPI.Instance.GetPathComponentGrid();
            if (grid.Length != 320800)
                return Unavailable("path-grid-capacity:" + grid.Length, tileX, tileY, -1, -1);
            int sourceTile = Marshal.ReadInt32(new IntPtr(checked(moduleBase + 0x379AFB0 +
                (long)playerId * PlayerStride)));
            int targetTile = Marshal.ReadInt32(new IntPtr(checked(moduleBase + 0x402FF2C +
                (long)tileY * 12))) + tileX;
            if ((uint)sourceTile >= (uint)grid.Length ||
                (uint)targetTile >= (uint)grid.Length)
                return Unavailable("tile-id-outside-path-grid", tileX, tileY, sourceTile, targetTile);
            int source = grid[sourceTile];
            int target = grid[targetTile];
            int sourceNative = (ushort)Marshal.ReadInt16(new IntPtr(checked(
                moduleBase + 0x50EC690 + (long)sourceTile * 2)));
            int targetNative = (ushort)Marshal.ReadInt16(new IntPtr(checked(
                moduleBase + 0x50EC690 + (long)targetTile * 2)));
            int bypass = Marshal.ReadInt32(new IntPtr(checked(moduleBase + 0x2E4FE0 + mapperIndex * 4L)));
            int playerToken = Marshal.ReadInt32(new IntPtr(checked(moduleBase + 0x37EDF3C +
                playerId * 4L)));
            var connections = new AiRouteConnection[199];
            int buildingCapacity = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan().Length;
            GamePathingManagerAPI pathing = GamePathingManagerAPI.Instance;
            for (int recordId = 1; recordId <= 199; recordId++)
            {
                if (!pathing.TryGetPathConnectionRecordById(recordId, out PathConnectionRecord* record) ||
                    record == null)
                    return Unavailable("connection-record-unavailable:" + recordId,
                        tileX, tileY, sourceTile, targetTile);
                int owner = record->r_OwnerOrAccessPlayerId;
                int ownerToken = owner >= 0 && owner <= 8
                    ? Marshal.ReadInt32(new IntPtr(checked(moduleBase + 0x37EDF3C +
                        owner * 4L))) : int.MinValue;
                int buildingId = record->r_BuildingId;
                int gateFlag = buildingId >= 0 && buildingId < buildingCapacity
                    ? Marshal.ReadInt16(new IntPtr(checked(moduleBase + 0x64CCED2 +
                        (long)buildingId * 0x32C))) : int.MinValue;
                connections[recordId - 1] = new AiRouteConnection(recordId,
                    record->r_IsActive, record->r_IsEnabledOrOpen,
                    (int)record->r_ConnectionClass, owner, buildingId,
                    record->r_PathComponentA, record->r_PathComponentB,
                    record->r_PathComponentC, ownerToken, gateFlag);
            }
            return new AiRouteEvidence("ok", tileX, tileY, sourceTile, targetTile,
                source, target, sourceNative, targetNative, bypass, playerToken, connections);
        }
    }
}
