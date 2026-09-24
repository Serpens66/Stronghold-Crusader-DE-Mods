using SHCDESE.API;
using SHCDESE.GameGlobals;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ActiveAIVDetector
{
    // Passive snapshot of every fit-relevant native tile in the type-41 start constructor.
    // Native offsets are gated by the detector's installed-DLL hash check.
    internal sealed unsafe class OracleStartStructureCapture
    {
        private const int TileCount = 320800;
        private const int FailureOffset = 0x204E6FC;
        private const int FailureReasonOffset = 0x204E704;
        private const int StartCleanupOffset = 0x204E7FC;
        private const int DestroyedRecordMarkerOffset = 0x204E778;

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

        private int[] beforeValues;
        private BuildingRecord[] beforeBuildings;

        private OracleStartStructureCapture(int playerId, int x, int y, int orientation,
            int scale, bool isFree, int[] beforeValues, int beforeFailureFlag,
            int beforeFailureReason, int beforeStartCleanup,
            int beforeDestroyedRecordMarker, double beforeScanMilliseconds,
            BuildingRecord[] beforeBuildings)
        {
            PlayerId = playerId;
            X = x;
            Y = y;
            Orientation = orientation;
            Scale = scale;
            IsFree = isFree;
            this.beforeValues = beforeValues;
            this.beforeBuildings = beforeBuildings;
            BeforeFailureFlag = beforeFailureFlag;
            BeforeFailureReason = beforeFailureReason;
            BeforeStartCleanup = beforeStartCleanup;
            BeforeDestroyedRecordMarker = beforeDestroyedRecordMarker;
            BeforeScanMilliseconds = beforeScanMilliseconds;
        }

        public int PlayerId { get; }
        public int X { get; }
        public int Y { get; }
        public int Orientation { get; }
        public int Scale { get; }
        public bool IsFree { get; }
        public int BeforeFailureFlag { get; }
        public int BeforeFailureReason { get; }
        public int BeforeStartCleanup { get; }
        public int BeforeDestroyedRecordMarker { get; }
        public double BeforeScanMilliseconds { get; }
        public int SampleCount => TileCount;

        public static OracleStartStructureCapture Begin(int playerId, int x, int y,
            int orientation, int scale, bool isFree)
        {
            ulong address = GameGlobalsManager.Instance.GameTileManagerVA;
            if (address == 0)
                throw new InvalidOperationException("The native tile-manager address is zero.");
            byte* root = (byte*)address;
            var timer = Stopwatch.StartNew();
            int[] values = ReadSamples(address);
            BuildingRecord[] buildings = OraclePrebuildStateCapture.ReadBuildings();
            timer.Stop();
            return new OracleStartStructureCapture(playerId, x, y, orientation, scale,
                isFree, values,
                *(int*)(root + FailureOffset), *(int*)(root + FailureReasonOffset),
                *(int*)(root + StartCleanupOffset),
                *(int*)(root + DestroyedRecordMarkerOffset),
                timer.Elapsed.TotalMilliseconds, buildings);
        }

        public OracleStartStructureResult Complete()
        {
            ulong address = GameGlobalsManager.Instance.GameTileManagerVA;
            if (address == 0)
                throw new InvalidOperationException("The native tile-manager address is zero after construction.");

            if (beforeValues == null || beforeValues.Length != TileCount * Layers.Length)
                throw new InvalidOperationException("The Keep-start capture is missing its pre-call snapshot.");
            ushort* columns = GameTileManagerAPI.Instance.MapColumnLookupTable;
            int* rows = GameTileManagerAPI.Instance.MapRowLookupTable;
            if (columns == null || rows == null)
                throw new InvalidOperationException("The native tile-coordinate lookup is null.");
            byte* root = (byte*)address;
            var timer = Stopwatch.StartNew();
            var changes = new List<OracleStartTileChange>();
            var buildingChanges = new List<OraclePrebuildBuildingRecordChange>();
            for (int tileId = 0; tileId < TileCount; tileId++)
            {
                for (int layer = 0; layer < Layers.Length; layer++)
                {
                    int previous = beforeValues[tileId * Layers.Length + layer];
                    int current = Read(root + Layers[layer].Offset, tileId, Layers[layer].Width);
                    if (previous != current)
                    {
                        int y = columns[tileId];
                        if ((uint)y >= 800)
                            throw new InvalidOperationException("The native tile-coordinate row is invalid.");
                        int x = tileId - rows[y * 3];
                        if ((uint)x >= 800)
                            throw new InvalidOperationException("The native tile-coordinate column is invalid.");
                        changes.Add(new OracleStartTileChange(
                            x, y, tileId, Layers[layer].Name, previous, current));
                    }
                }
            }

            BuildingRecord[] afterBuildings = OraclePrebuildStateCapture.ReadBuildings();
            if (beforeBuildings == null || beforeBuildings.Length != afterBuildings.Length)
                throw new InvalidOperationException("The Keep-start building snapshot is incomplete.");
            for (int spanIndex = 0; spanIndex < beforeBuildings.Length; spanIndex++)
            {
                if (!beforeBuildings[spanIndex].Equals(afterBuildings[spanIndex]))
                    buildingChanges.Add(new OraclePrebuildBuildingRecordChange(
                        spanIndex + 1, beforeBuildings[spanIndex], afterBuildings[spanIndex]));
            }

            beforeValues = null;
            beforeBuildings = null;
            timer.Stop();
            return new OracleStartStructureResult(this,
                *(int*)(root + FailureOffset),
                *(int*)(root + FailureReasonOffset),
                *(int*)(root + StartCleanupOffset),
                *(int*)(root + DestroyedRecordMarkerOffset),
                changes, buildingChanges, timer.Elapsed.TotalMilliseconds);
        }

        private static int[] ReadSamples(ulong address)
        {
            byte* root = (byte*)address;
            var values = new int[TileCount * Layers.Length];
            for (int tileId = 0; tileId < TileCount; tileId++)
            {
                for (int layer = 0; layer < Layers.Length; layer++)
                    values[tileId * Layers.Length + layer] =
                        Read(root + Layers[layer].Offset, tileId, Layers[layer].Width);
            }
            return values;
        }

        private static int Read(byte* source, int tileId, int width)
        {
            switch (width)
            {
                case 1: return source[tileId];
                case 2: return ((ushort*)source)[tileId];
                case 4: return ((int*)source)[tileId];
                default: throw new InvalidOperationException("Invalid native tile-layer width.");
            }
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

    internal readonly struct OracleStartTileChange
    {
        public OracleStartTileChange(int x, int y, int tileId, string layer, int before, int after)
        {
            X = x;
            Y = y;
            TileId = tileId;
            Layer = layer;
            Before = before;
            After = after;
        }
        public int X { get; }
        public int Y { get; }
        public int TileId { get; }
        public string Layer { get; }
        public int Before { get; }
        public int After { get; }
    }

    internal sealed class OracleStartStructureResult
    {
        public OracleStartStructureResult(OracleStartStructureCapture capture,
            int failureFlag, int failureReason, int afterStartCleanup,
            int afterDestroyedRecordMarker, IReadOnlyList<OracleStartTileChange> changes,
            IReadOnlyList<OraclePrebuildBuildingRecordChange> buildingChanges,
            double afterScanMilliseconds)
        {
            Capture = capture;
            FailureFlag = failureFlag;
            FailureReason = failureReason;
            AfterStartCleanup = afterStartCleanup;
            AfterDestroyedRecordMarker = afterDestroyedRecordMarker;
            Changes = changes;
            BuildingRecordChanges = buildingChanges;
            AfterScanMilliseconds = afterScanMilliseconds;
            int newCells = 0;
            int clearedCells = 0;
            int replacedCells = 0;
            foreach (OracleStartTileChange change in changes)
            {
                if (change.Layer != "buildingId")
                    continue;
                if (change.Before == 0)
                    newCells++;
                else if (change.After == 0)
                    clearedCells++;
                else
                    replacedCells++;
            }
            NewBuildingCells = newCells;
            ClearedBuildingCells = clearedCells;
            ReplacedBuildingCells = replacedCells;
        }
        public OracleStartStructureCapture Capture { get; }
        public int FailureFlag { get; }
        public int FailureReason { get; }
        public int AfterStartCleanup { get; }
        public int AfterDestroyedRecordMarker { get; }
        public IReadOnlyList<OracleStartTileChange> Changes { get; }
        public IReadOnlyList<OraclePrebuildBuildingRecordChange> BuildingRecordChanges { get; }
        public double AfterScanMilliseconds { get; }
        public int NewBuildingCells { get; }
        public int ReplacedBuildingCells { get; }
        public int ClearedBuildingCells { get; }
    }
}
