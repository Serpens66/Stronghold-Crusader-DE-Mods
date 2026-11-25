using System;
using System.Reflection;
using System.Runtime.InteropServices;
using BepInEx.Bootstrap;
using SHCDESE.API;
using SHCDESE.Interop.Enums;
using Shared;

namespace MakeDesync
{
    internal sealed unsafe class MoatProbe
    {
        private const int TileManagerRva = 0x405EDB0, LogicOffset = 0x898400;
        private const int Capacity = 320800, DataOffset = 64;
        private const int RepairableLogicTiles = 128 * (0x2728 / sizeof(int));
        private const ushort StageMask = 0xFF00, Dug = 0x0200;
        private const TilePropertyFlag Obstruction = TilePropertyFlag.PlannedMoat | TilePropertyFlag.IsWall |
            TilePropertyFlag.IsBuilding | TilePropertyFlag.HasStone | TilePropertyFlag.HasIron | TilePropertyFlag.IsPitch;
        private readonly IntPtr expectedManager;

        internal MoatProbe(IntPtr module) => expectedManager = IntPtr.Add(module, TileManagerRva);

        internal sealed class Target
        {
            internal Target(long session, int tile, int logic, ushort history)
            { Session = session; Tile = tile; OriginalLogic = logic; OriginalHistory = history; }
            internal readonly long Session;
            internal readonly int Tile, OriginalLogic;
            internal readonly ushort OriginalHistory;
            internal int ChangedLogic => OriginalLogic | (int)TilePropertyFlag.HasStone;
        }

        internal Target Apply(long session, int requestedTile)
        {
            var tiles = GameTileManagerAPI.Instance;
            var logic = Logic();
            var structures = tiles.GetStructureLayer();
            var units = tiles.GetChimpLayer();
            var soil = tiles.GetLogic2Layer();
            if (structures.Length != Capacity || units.Length != Capacity || soil.Length != Capacity)
                throw new InvalidOperationException("Unexpected moat tile layers.");
            var history = ActiveHistory();
            byte[] snapshot = CommittedMemory.ReadUpTo(history, Capacity * sizeof(ushort));
            if (snapshot.Length != Capacity * sizeof(ushort))
                throw new InvalidOperationException("Moat history is not readable.");
            // The final reserved cells of the packed allocation are outside Chore 65's blocks.
            if (requestedTile >= RepairableLogicTiles) throw new InvalidOperationException("Moat test tile is outside the native repair coverage.");
            int first = requestedTile < 0 ? 0 : requestedTile;
            int end = requestedTile < 0 ? RepairableLogicTiles : requestedTile + 1;
            for (int i = first; i < end; i++)
            {
                var flags = (TilePropertyFlag)logic[i];
                if ((flags & TilePropertyFlag.IsMoat) == 0 || (flags & Obstruction) != 0 ||
                    structures[i] != 0 || units[i] != 0) continue;
                int offset = i * sizeof(ushort);
                ushort captured = BitConverter.ToUInt16(snapshot, offset);
                if ((captured & StageMask) != Dug || (byte)captured == soil[i]) continue;
                var target = new Target(session, i, logic[i], captured);
                Marshal.WriteInt16(history, offset, 0);
                logic[i] = target.ChangedLogic;
                return target;
            }
            throw new InvalidOperationException("Dig one unoccupied moat tile on terrain whose soil changes when dug.");
        }

        internal bool StillDiverged(Target target) => ReadLogic(target.Tile) == target.ChangedLogic;
        internal int ReadLogic(int tile) => Logic()[tile];

        internal ushort ReadHistory(int tile) =>
            unchecked((ushort)Marshal.ReadInt16(ActiveHistory(), tile * sizeof(ushort)));

        internal string CheckFilled(Target target)
        {
            var flags = (TilePropertyFlag)ReadLogic(target.Tile);
            if ((flags & (TilePropertyFlag.IsMoat | TilePropertyFlag.PlannedMoat)) != 0)
                return $"MAKE_DESYNC_MOAT_FILL_WAIT tile={target.Tile} reason=still-moat";
            var soil = GameTileManagerAPI.Instance.GetLogic2Layer();
            if (soil.Length != Capacity) throw new InvalidOperationException("Unexpected repaired soil layer.");
            byte expected = (byte)target.OriginalHistory;
            byte actual = soil[target.Tile];
            ushort remaining = ReadHistory(target.Tile);
            return $"MAKE_DESYNC_MOAT_FILL tile={target.Tile} soil={actual} expectedSoil={expected} " +
                $"history={remaining:X4} restored={actual == expected && remaining == 0}";
        }

        private Span<int> Logic()
        {
            var tiles = GameTileManagerAPI.Instance;
            var logic = tiles.GetLogicLayer();
            // MultiplayerResyncData.CheckIntegrity admits the native image hash above this probe.
            fixed (int* first = logic)
                if (tiles.GetTileManager() != expectedManager || logic.Length != Capacity ||
                    (IntPtr)first != IntPtr.Add(expectedManager, LogicOffset))
                    throw new InvalidOperationException("The audited native tile grid is unavailable.");
            return logic;
        }

        // This is a private, version-bound test binding. The runtime retains ownership.
        private IntPtr ActiveHistory()
        {
            if (!Chainloader.PluginInfos.TryGetValue(Plugin.BugfixesGuid, out var plugin) ||
                ReferenceEquals(plugin.Instance, null))
                throw new InvalidOperationException("BugfixesAndQoL is required for the moat test.");
            var assembly = plugin.Instance.GetType().Assembly;
            var runtimeType = assembly.GetType("BugfixesAndQoL.BugfixesAndQoLRuntime", true);
            var terrainType = assembly.GetType("BugfixesAndQoL.MoatTerrain.TerrainRuntime", true);
            var stateType = assembly.GetType("BugfixesAndQoL.MoatTerrain.TerrainState", true);
            const BindingFlags staticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
            const BindingFlags instancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
            if (!Equals(stateType.GetField("Capacity", staticPrivate)?.GetRawConstantValue(), Capacity) ||
                !Equals(stateType.GetField("DataOffset", staticPrivate)?.GetRawConstantValue(), DataOffset) ||
                !Equals(stateType.GetField("Dug", staticPrivate)?.GetRawConstantValue(), Dug))
                throw new InvalidOperationException("The moat history layout differs from the tested version.");
            var runtime = runtimeType.GetField("processMoatTerrainRuntime", staticPrivate)?.GetValue(null);
            if (runtime == null || !Equals(terrainType.GetField("multiplayerWorld", instancePrivate)?.GetValue(runtime), true) ||
                !Equals(terrainType.GetField("multiplayerRepairAvailable", instancePrivate)?.GetValue(runtime), true) ||
                !(terrainType.GetField("buffer", instancePrivate)?.GetValue(runtime) is IntPtr buffer) ||
                buffer == IntPtr.Zero || Marshal.ReadInt32(buffer) != 1)
                throw new InvalidOperationException("Moat terrain restoration is inactive in this multiplayer world.");
            return IntPtr.Add(buffer, DataOffset);
        }
    }
}
