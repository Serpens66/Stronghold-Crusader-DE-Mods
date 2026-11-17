using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;

namespace EnemyBridgePathTest
{
    // Read-only, independent view of gates, drawbridge decks, portals and regions for the
    // in-game acceptance of Pathfinding's drawbridge closure. Never writes game memory; the
    // layout is the audited FBCB9319 one (see Pathfinding/UpdateToNewDLL.md). Call under the
    // engine monitor (AutomationRun.InSimulation).
    internal sealed unsafe class DeckInspector : IDeckGrid
    {
        internal const string NativeHash = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        private const int RvaBuildings = 0x64CCBB0, BuildingStride = 0x32C, BuildingCountOffset = 0x50;
        private const int RvaPathManager = 0x60AD660, PortalBase = 0x2024, PortalStride = 0x204, PortalLimit = 200;
        private const int RvaDirections = 0x51890D0, RvaComponents = 0x50EC690, RvaTileFlags = 0x48F71B0;
        private const int RvaStructures = 0x4B6AA50, RvaMoatTasks = 0x5F011A0, RvaDeckMapper = 0x2D1A30;
        internal const int TileCount = 320800;
        private const uint RaisedDeckFlag = 0x40000000;
        private const int OffAlive = 0x12C, OffType = 0x12E, OffX = 0x14A, OffY = 0x14C, OffSize = 0x154;
        private const int OffOwner = 0x132, OffCapturer = 0x322, OffVariation = 0x15E, OffOccupied = 0x224, OffGateState = 0x2FE, OffPortalId = 0x32E;
        private const short ApertureAxisVariation = 0x50;
        private const int PState = 0, PClass = 4, PBuilding = 0xC, PEntryTile = 0x24, PExitTile = 0x30, PCompA = 0x34, PCompB = 0x38, PCompC = 0x1E8;
        private const int NativeFirstGateType = 0x2D, NativeGateTypeCount = 3, NativeDrawbridgeType = 0x31;

        private byte* buildings, pathManager, directions, tileFlags, deckMapper;
        private ushort* components, moatTasks;
        private short* structures;
        private GameTileManagerAPI tiles;
        private readonly int[] deckStamp = new int[TileCount];
        private int deckGeneration = 1, currentBridge;
        private readonly List<int> deckCells = new List<int>(32);
        private readonly Func<int, bool> isDeck, isOnBridge;
        private readonly Func<int, int> componentOf;

        internal bool Ready { get; private set; }
        internal string Error { get; private set; } = "native library not loaded";

        internal DeckInspector()
        {
            isDeck = tile => (uint)tile < TileCount && deckStamp[tile] == deckGeneration;
            isOnBridge = tile => (uint)tile < TileCount && currentBridge > 0 && structures[tile] == currentBridge;
            componentOf = tile => (uint)tile < TileCount ? components[tile] : 0;
        }

        internal void Attach(CrusaderLibraryLoadContext context)
        {
            if (!string.Equals(Shared.DebugLogHelper.InstalledNativeSha256(), NativeHash, StringComparison.OrdinalIgnoreCase))
            { Error = "CrusaderDE.dll is not the audited build " + NativeHash; return; }
            byte* module = (byte*)context.ModuleHandle;
            buildings = module + RvaBuildings; pathManager = module + RvaPathManager;
            directions = module + RvaDirections; tileFlags = module + RvaTileFlags; deckMapper = module + RvaDeckMapper;
            components = (ushort*)(module + RvaComponents); moatTasks = (ushort*)(module + RvaMoatTasks);
            structures = (short*)(module + RvaStructures);
            tiles = GameTileManagerAPI.Instance;
            Ready = true; Error = null;
        }

        internal sealed class DeckReport
        {
            internal int Gate, GateOwner, GateCaptured, Bridge, Portal, PortalSide, FrontTile, GateTile, LandingTile, InnerTile = -1;
            internal int DeckRegion, FrontRegion, GateRegion, LandingRegion, EndpointField, OtherField, CenterField;
            internal bool Raised;
            internal readonly List<int> DeckTiles = new List<int>();
        }

        // Every gate aperture that opens onto a drawbridge deck, lowered or raised.
        internal List<DeckReport> DescribeDecks()
        {
            var result = new List<DeckReport>();
            if (!Ready) return result;
            int count = *(int*)(buildings + BuildingCountOffset);
            for (int gate = 1; gate < count; gate++)
            {
                byte* record = buildings + (long)gate * BuildingStride;
                if (*(short*)(record + OffAlive) == 0 || !IsGateType(*(ushort*)(record + OffType)) || !HasOwnPortal(gate, record)) continue;
                int portalId = *(short*)(record + OffPortalId);
                byte* portal = pathManager + PortalBase + (long)portalId * PortalStride;
                for (int side = 0; side < 2; side++)
                {
                    if (!TryAperture(record, side, out int outside, out int inside)) continue;
                    int bridge = structures[outside];
                    if (bridge <= 0 || bridge >= count) continue;
                    byte* bridgeRecord = buildings + (long)bridge * BuildingStride;
                    if (*(ushort*)(bridgeRecord + OffType) != NativeDrawbridgeType || *(short*)(bridgeRecord + OffAlive) != (short)AliveState.IsAlive) continue;
                    LoadDeck(bridge);
                    if (deckCells.Count == 0 || !DeckClosureCore.FindNearDeck(this, outside, inside, isDeck, isOnBridge, out _, out _)) continue;
                    bool raised = false;
                    foreach (int cell in deckCells) raised |= (*(uint*)(tileFlags + (long)cell * 4) & RaisedDeckFlag) != 0;
                    var report = new DeckReport
                    {
                        Gate = gate, GateOwner = *(short*)(record + OffOwner), GateCaptured = *(short*)(record + OffCapturer), Bridge = bridge, Portal = portalId,
                        FrontTile = outside, GateTile = inside, DeckRegion = components[deckCells[deckCells.Count / 2]], FrontRegion = components[outside], GateRegion = components[inside],
                        Raised = raised, CenterField = *(int*)(portal + PCompC)
                    };
                    report.DeckTiles.AddRange(deckCells);
                    if (TryAperture(record, 1 - side, out int innerOutside, out _)) report.InnerTile = innerOutside;
                    report.LandingRegion = DeckClosureCore.FindLanding(this, outside, inside, isOnBridge, componentOf, out report.LandingTile);
                    if (*(int*)(portal + PEntryTile) == outside) { report.PortalSide = 0; report.EndpointField = *(int*)(portal + PCompA); report.OtherField = *(int*)(portal + PCompB); }
                    else if (*(int*)(portal + PExitTile) == outside) { report.PortalSide = 1; report.EndpointField = *(int*)(portal + PCompB); report.OtherField = *(int*)(portal + PCompA); }
                    else report.PortalSide = -1;
                    result.Add(report);
                }
            }
            return result;
        }

        // Raw inventory of every gate and drawbridge for acceptance diagnostics.
        internal List<string> DescribeStructures()
        {
            var lines = new List<string>();
            if (!Ready) return lines;
            int count = *(int*)(buildings + BuildingCountOffset);
            lines.Add("buildingCount=" + count);
            for (int id = 1; id < count; id++)
            {
                byte* record = buildings + (long)id * BuildingStride;
                ushort type = *(ushort*)(record + OffType);
                if (!IsGateType(type) && type != NativeDrawbridgeType) continue;
                string text = (type == NativeDrawbridgeType ? "bridge" : "gate") + " id=" + id + " type=" + type + " alive=" + *(short*)(record + OffAlive) +
                    " owner=" + *(short*)(record + OffOwner) + " xy=" + *(short*)(record + OffX) + "/" + *(short*)(record + OffY) +
                    " size=" + *(int*)(record + OffSize) + " variation=" + *(short*)(record + OffVariation) + " state=" + record[OffGateState];
                if (type == NativeDrawbridgeType)
                {
                    LoadDeck(id);
                    text += " deckCells=" + deckCells.Count;
                }
                else
                {
                    int portalId = *(short*)(record + OffPortalId);
                    text += " portal=" + portalId + " ownPortal=" + HasOwnPortal(id, record);
                    for (int side = 0; side < 2; side++)
                        if (TryAperture(record, side, out int outside, out int inside))
                        {
                            TryGetCoordinates(outside, out int ox, out int oy); TryGetCoordinates(inside, out int ix, out int iy);
                            text += " aperture" + side + "=" + ox + "/" + oy + "->" + ix + "/" + iy + " structureAtFront=" + structures[outside] +
                                " region=" + components[outside] + "/" + components[inside];
                        }
                        else text += " aperture" + side + "=invalid";
                }
                lines.Add(text);
            }
            return lines;
        }

        // PCL and direction byte of every tile around a gate (footprint plus margin), row by row,
        // to see which regions the portal's centre field (PCL of X+1/Y+1) can point at.
        internal List<string> DescribeGateSurroundings(int gate, int margin = 2)
        {
            var lines = new List<string>();
            if (!Ready) return lines;
            byte* record = buildings + (long)gate * BuildingStride;
            int x0 = *(short*)(record + OffX), y0 = *(short*)(record + OffY), size = *(int*)(record + OffSize);
            if (size <= 0 || size > 16) return lines;
            for (int y = y0 - margin; y < y0 + size + margin; y++)
            {
                var cells = new System.Text.StringBuilder();
                for (int x = x0 - margin; x < x0 + size + margin; x++)
                {
                    if (!TryGetTile(x, y, out int tile)) { cells.Append(" ----:--"); continue; }
                    cells.Append(' ').Append(components[tile].ToString().PadLeft(4)).Append(':').Append(directions[tile].ToString("X2"))
                        .Append(structures[tile] != 0 ? "b" : " ");
                }
                lines.Add("gate=" + gate + " y=" + y + " x0=" + (x0 - margin) + cells);
            }
            return lines;
        }

        internal static int RegionOf(int tile) => (uint)tile < TileCount ? GamePathingManagerAPI.Instance.GetPathComponentGrid()[tile] : 0;

        // ---- geometry (same audited contract as the closure) --------------------------------

        private static bool IsGateType(ushort type) => (uint)(type - NativeFirstGateType) < NativeGateTypeCount;

        private bool HasOwnPortal(int gate, byte* record)
        {
            int id = *(short*)(record + OffPortalId);
            if (id <= 0 || id >= PortalLimit) return false;
            byte* portal = pathManager + PortalBase + (long)id * PortalStride;
            int cls = *(int*)(portal + PClass);
            return *(int*)(portal + PState) != 0 && *(int*)(portal + PBuilding) == gate &&
                (cls == (int)PathConnectionClass.GatehouseBig || cls == (int)PathConnectionClass.GatehouseSmall);
        }

        private bool TryAperture(byte* record, int side, out int outside, out int inside)
        {
            outside = inside = -1;
            int x = *(short*)(record + OffX), y = *(short*)(record + OffY), size = *(int*)(record + OffSize);
            if (size <= 0 || size > 16) return false;
            int ox, oy, ix, iy;
            if (*(short*)(record + OffVariation) != ApertureAxisVariation)
            {
                int column = x + size / 2;
                ox = ix = column;
                if (side == 0) { oy = y - 1; iy = y; } else { oy = y + size; iy = y + size - 1; }
            }
            else
            {
                int row = y + size / 2;
                oy = iy = row;
                if (side == 0) { ox = x - 1; ix = x; } else { ox = x + size; ix = x + size - 1; }
            }
            return TryGetTile(ox, oy, out outside) && TryGetTile(ix, iy, out inside);
        }

        private void LoadDeck(int bridge)
        {
            if (++deckGeneration == int.MaxValue) { Array.Clear(deckStamp, 0, deckStamp.Length); deckGeneration = 1; }
            deckCells.Clear();
            currentBridge = bridge;
            byte* record = buildings + (long)bridge * BuildingStride;
            int orientation = *(short*)(record + OffVariation);
            if (orientation < 0 || orientation > 6 || (orientation & 1) != 0) return;
            int* mapper = (int*)(deckMapper + (orientation / 2) * 100);
            uint* occupied = (uint*)(record + OffOccupied);
            for (int i = 0; i < 25; i++)
            {
                uint tile = occupied[i];
                if (mapper[i] == 0 || tile >= TileCount || moatTasks[tile] == 0) continue;
                deckStamp[tile] = deckGeneration;
                deckCells.Add((int)tile);
            }
        }

        // ---- IDeckGrid (read-only) -----------------------------------------------------------

        public byte GetDirections(int tile) => directions[tile];
        public bool TryGetCoordinates(int tile, out int x, out int y)
        {
            x = y = 0;
            if ((uint)tile >= TileCount) return false;
            var v = tiles.GetTileVectorFromId(tile);
            x = v.X; y = v.Y;
            return tiles.IsTileInsideMapBounds(x, y) && tiles.GetTileId(x, y) == tile;
        }
        public bool TryGetTile(int x, int y, out int tile)
        {
            tile = -1;
            if (!tiles.IsTileInsideMapBounds(x, y)) return false;
            tile = tiles.GetTileId(x, y);
            return (uint)tile < TileCount;
        }
    }
}
