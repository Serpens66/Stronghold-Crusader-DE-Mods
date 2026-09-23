using SHCDESE.API;
using SHCDESE.Interop;
using System;
using System.Collections.Generic;

namespace ActiveAIVDetector
{
    // All offsets are valid only under the detector's current native-hash gate.
    // The eight layers below include every per-tile input read by the audited
    // placement validator. Other constructor effects remain a research boundary.
    internal sealed unsafe class OraclePrebuildStateCapture
    {
        private const int TileCount = 320800;
        private static readonly Layer[] Layers =
        {
            new Layer("logic", 0x898400, 4),
            new Layer("logic2", 0x9D2500, 1),
            new Layer("organism", 0xA6F260, 2),
            new Layer("buildingId", 0xB0BCA0, 2),
            new Layer("tileUnitId", 0xBF6C00, 2),
            new Layer("height", 0xD7E5A0, 1),
            new Layer("defaultHeight", 0xDCCAC0, 1),
            new Layer("wallOwner", 0xE1AFE0, 1)
        };

        private readonly int[][] before = new int[Layers.Length][];
        private BuildingRecord[] beforeBuildings;
        private bool captured;

        public OraclePrebuildStateCapture()
        {
            for (int i = 0; i < before.Length; i++)
                before[i] = new int[TileCount];
        }

        public void CaptureBefore(ulong tileManagerAddress)
        {
            captured = false;
            if (tileManagerAddress == 0)
                throw new ArgumentException("The native tile manager pointer is null.", nameof(tileManagerAddress));
            for (int layer = 0; layer < Layers.Length; layer++)
            {
                byte* source = (byte*)tileManagerAddress + Layers[layer].Offset;
                int[] values = before[layer];
                for (int tileId = 0; tileId < TileCount; tileId++)
                    values[tileId] = Read(source, tileId, Layers[layer].Width);
            }
            beforeBuildings = ReadBuildings();
            captured = true;
        }

        public void CaptureAfter(
            ulong tileManagerAddress,
            List<OraclePrebuildLayerChange> layerChanges,
            List<OraclePrebuildBuildingRecordChange> buildingChanges)
        {
            if (!captured || tileManagerAddress == 0)
                throw new InvalidOperationException("The prebuild tile snapshot is incomplete.");
            for (int layer = 0; layer < Layers.Length; layer++)
            {
                byte* source = (byte*)tileManagerAddress + Layers[layer].Offset;
                int[] values = before[layer];
                for (int tileId = 0; tileId < TileCount; tileId++)
                {
                    int after = Read(source, tileId, Layers[layer].Width);
                    if (values[tileId] != after)
                        layerChanges.Add(new OraclePrebuildLayerChange(
                            Layers[layer].Name, tileId, values[tileId], after));
                }
            }

            BuildingRecord[] afterBuildings = ReadBuildings();
            if (beforeBuildings.Length != afterBuildings.Length)
                throw new InvalidOperationException("The native building array capacity changed during a build step.");
            for (int spanIndex = 0; spanIndex < beforeBuildings.Length; spanIndex++)
            {
                if (!beforeBuildings[spanIndex].Equals(afterBuildings[spanIndex]))
                    buildingChanges.Add(new OraclePrebuildBuildingRecordChange(
                        spanIndex + 1, beforeBuildings[spanIndex], afterBuildings[spanIndex]));
            }
            captured = false;
        }

        private static int Read(byte* source, int tileId, int width)
        {
            switch (width)
            {
                case 1: return source[tileId];
                case 2: return ((ushort*)source)[tileId];
                case 4: return ((int*)source)[tileId];
                default: throw new InvalidOperationException("Invalid tile layer width.");
            }
        }

        private static BuildingRecord[] ReadBuildings()
        {
            Span<GameBuilding> source = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            var result = new BuildingRecord[source.Length];
            for (int spanIndex = 0; spanIndex < source.Length; spanIndex++)
            {
                ref GameBuilding building = ref source[spanIndex];
                result[spanIndex] = new BuildingRecord(
                    (int)building.r_AliveState,
                    (int)building.r_BuildingType,
                    building.r_PlayerIdOwner,
                    building.r_GlobalId,
                    building.r_TileIdBegin,
                    building.r_OccupyTileGridSize,
                    building.r_TilePositionXBegin,
                    building.r_TilePositionYBegin);
            }
            return result;
        }

        private readonly struct Layer
        {
            public Layer(string name, int offset, int width)
            {
                Name = name;
                Offset = offset;
                Width = width;
            }
            public string Name { get; }
            public int Offset { get; }
            public int Width { get; }
        }
    }

    internal readonly struct OraclePrebuildLayerChange
    {
        public OraclePrebuildLayerChange(string layer, int tileId, int before, int after)
        {
            Layer = layer;
            TileId = tileId;
            Before = before;
            After = after;
        }
        public string Layer { get; }
        public int TileId { get; }
        public int Before { get; }
        public int After { get; }
    }

    internal readonly struct BuildingRecord : IEquatable<BuildingRecord>
    {
        public BuildingRecord(int aliveState, int type, int owner, uint globalId,
            uint tileIdBegin, uint occupyTileGridSize, int tileX, int tileY)
        {
            AliveState = aliveState;
            Type = type;
            Owner = owner;
            GlobalId = globalId;
            TileIdBegin = tileIdBegin;
            OccupyTileGridSize = occupyTileGridSize;
            TileX = tileX;
            TileY = tileY;
        }
        public int AliveState { get; }
        public int Type { get; }
        public int Owner { get; }
        public uint GlobalId { get; }
        public uint TileIdBegin { get; }
        public uint OccupyTileGridSize { get; }
        public int TileX { get; }
        public int TileY { get; }
        public bool Equals(BuildingRecord other) =>
            AliveState == other.AliveState && Type == other.Type &&
            Owner == other.Owner && GlobalId == other.GlobalId &&
            TileIdBegin == other.TileIdBegin &&
            OccupyTileGridSize == other.OccupyTileGridSize &&
            TileX == other.TileX && TileY == other.TileY;
        public override bool Equals(object obj) => obj is BuildingRecord other && Equals(other);
        public override int GetHashCode() => GlobalId.GetHashCode();
    }

    internal readonly struct OraclePrebuildBuildingRecordChange
    {
        public OraclePrebuildBuildingRecordChange(int buildingId, BuildingRecord before, BuildingRecord after)
        {
            BuildingId = buildingId;
            Before = before;
            After = after;
        }
        public int BuildingId { get; }
        public BuildingRecord Before { get; }
        public BuildingRecord After { get; }
    }
}
