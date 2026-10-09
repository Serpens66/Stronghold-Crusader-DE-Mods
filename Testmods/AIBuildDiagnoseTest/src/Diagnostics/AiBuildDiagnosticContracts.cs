using BepInEx.Logging;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Hooks.Transaction;
using RedBird.Backends.NativeX64;
using Iced.Intel;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace AIBuildDiagnoseTest
{
    /// <summary>One copied native macro-connection record; no live pointer escapes the hook.</summary>
    public sealed class AiRouteConnection
    {
        internal AiRouteConnection(int id, int active, int open, int connectionClass,
            int owner, int buildingId, int a, int b, int c, int ownerToken, int gateFlag)
        {
            Id = id; Active = active; Open = open; ConnectionClass = connectionClass;
            Owner = owner; BuildingId = buildingId; A = a; B = b; C = c;
            OwnerToken = ownerToken; GateFlag = gateFlag;
        }
        /// <summary>Native connection record ID, from 1 through 199.</summary>
        public int Id { get; }
        /// <summary>Raw native active flag.</summary>
        public int Active { get; }
        /// <summary>Raw native enabled or open flag.</summary>
        public int Open { get; }
        /// <summary>Raw native connection class.</summary>
        public int ConnectionClass { get; }
        /// <summary>Raw owner or access player ID.</summary>
        public int Owner { get; }
        /// <summary>Native building game ID associated with the connection.</summary>
        public int BuildingId { get; }
        /// <summary>First path component endpoint.</summary>
        public int A { get; }
        /// <summary>Second path component endpoint.</summary>
        public int B { get; }
        /// <summary>Optional third path component endpoint.</summary>
        public int C { get; }
        /// <summary>Raw owner access value, or Int32.MinValue if unavailable.</summary>
        public int OwnerToken { get; }
        /// <summary>Raw 16-bit building gate flag, or Int32.MinValue if unavailable.</summary>
        public int GateFlag { get; }
    }

    /// <summary>Read-only values sampled immediately before Vanilla's route query.</summary>
    public sealed class AiRouteEvidence
    {
        internal AiRouteEvidence(string status, int x, int y, int sourceTile, int targetTile,
            int sourceComponent, int targetComponent, int sourceNativeComponent,
            int targetNativeComponent, int bypass, int playerToken,
            AiRouteConnection[] connections)
        {
            Status = status; TileX = x; TileY = y; SourceTile = sourceTile;
            TargetTile = targetTile; SourceComponent = sourceComponent;
            TargetComponent = targetComponent; Bypass = bypass; PlayerToken = playerToken;
            SourceNativeComponent = sourceNativeComponent;
            TargetNativeComponent = targetNativeComponent;
            Connections = Array.AsReadOnly(connections ?? new AiRouteConnection[0]);
        }
        /// <summary>Read status; ok means all audited input values were captured.</summary>
        public string Status { get; }
        /// <summary>Route target X coordinate.</summary>
        public int TileX { get; }
        /// <summary>Route target Y coordinate.</summary>
        public int TileY { get; }
        /// <summary>Native source keep tile index.</summary>
        public int SourceTile { get; }
        /// <summary>Native target tile index.</summary>
        public int TargetTile { get; }
        /// <summary>Source tile path component.</summary>
        public int SourceComponent { get; }
        /// <summary>Target tile path component.</summary>
        public int TargetComponent { get; }
        /// <summary>Direct native-grid value for the keep tile.</summary>
        public int SourceNativeComponent { get; }
        /// <summary>Direct native-grid value for the target tile.</summary>
        public int TargetNativeComponent { get; }
        /// <summary>Audited Vanilla route mode at the 0xC3BF0 call site.</summary>
        public int QueryMode => 0;
        /// <summary>Raw mapper route bypass flag.</summary>
        public int Bypass { get; }
        /// <summary>Raw 32-bit native player access value.</summary>
        public int PlayerToken { get; }
        /// <summary>Copies of native connection records 1 through 199.</summary>
        public IReadOnlyList<AiRouteConnection> Connections { get; }
    }

    /// <summary>One copied path-component measurement from both audited views.</summary>
    public sealed class AiPathTileSample
    {
        internal AiPathTileSample(int x, int y, int tileId, int nativeComponent,
            int apiComponent, uint propertyFlags, int tileType, int buildingId,
            int wallOwner, string status, int organism = -1, int occupancy = -1,
            int height = -1)
        {
            X = x; Y = y; TileId = tileId; NativeComponent = nativeComponent;
            ApiComponent = apiComponent; PropertyFlags = propertyFlags;
            TileType = tileType; BuildingId = buildingId; WallOwner = wallOwner;
            Status = status; Organism = organism; Occupancy = occupancy;
            Height = height;
        }
        /// <summary>Map X coordinate.</summary>
        public int X { get; }
        /// <summary>Map Y coordinate.</summary>
        public int Y { get; }
        /// <summary>Packed native tile ID, or minus one when unavailable.</summary>
        public int TileId { get; }
        /// <summary>Component read directly from the native grid.</summary>
        public int NativeComponent { get; }
        /// <summary>Component read through the Script Extender grid view.</summary>
        public int ApiComponent { get; }
        /// <summary>Uninterpreted native tile property bits.</summary>
        public uint PropertyFlags { get; }
        /// <summary>Visual tile type byte.</summary>
        public int TileType { get; }
        /// <summary>Building game ID in the tile layer.</summary>
        public int BuildingId { get; }
        /// <summary>Wall owner byte in the tile layer.</summary>
        public int WallOwner { get; }
        /// <summary>Raw organism layer entry; minus one if unavailable.</summary>
        public int Organism { get; }
        /// <summary>Raw player occupancy mask; minus one if unavailable.</summary>
        public int Occupancy { get; }
        /// <summary>Raw tile height; minus one if unavailable.</summary>
        public int Height { get; }
        /// <summary>Whether this tile could be sampled.</summary>
        public string Status { get; }
    }

    /// <summary>Raw bytes of a Vanilla 0x58950 coarse cell; offsets 0 through 15.</summary>
    public sealed class AiCoarseCellSample
    {
        internal AiCoarseCellSample(int x, int y, string bytes, string status)
        { X = x; Y = y; Bytes = bytes; Status = status; }
        /// <summary>Coarse cell X coordinate.</summary>
        public int X { get; }
        /// <summary>Coarse cell Y coordinate.</summary>
        public int Y { get; }
        /// <summary>Hexadecimal representation of the sampled native bytes.</summary>
        public string Bytes { get; }
        /// <summary>Sampling status or reason why the cell was unavailable.</summary>
        public string Status { get; }
    }

    /// <summary>Copied anchor neighborhood and sampled 3x3 tiles at the result.</summary>
    public sealed class AiNearbyPathEvidence
    {
        private const int CoarseSide = 160;
        private const int CoarseSampleSize = 16;
        private readonly int[] capturedNativeAnchors;
        private readonly int[] capturedApiAnchors;
        private readonly byte[] capturedCoarseCells;

        internal AiNearbyPathEvidence(string status, int inputX, int inputY,
            int resultX, int resultY, AiPathTileSample[] anchors,
            AiPathTileSample[] footprint, AiCoarseCellSample inputCell,
            AiCoarseCellSample resultCell, AiCoarseCellSample[] nearbyCells,
            int[] nativeAnchors = null, int[] apiAnchors = null,
            byte[] coarseCells = null, AiPathTileSample[] coarseTiles = null,
            AiPathTileSample[] footprintRing = null, int referenceComponent = -1)
        {
            Status = status; InputX = inputX; InputY = inputY;
            ResultX = resultX; ResultY = resultY;
            Anchors = Array.AsReadOnly(anchors ?? new AiPathTileSample[0]);
            Footprint = Array.AsReadOnly(footprint ?? new AiPathTileSample[0]);
            CoarseTiles = Array.AsReadOnly(coarseTiles ?? new AiPathTileSample[0]);
            FootprintRing = Array.AsReadOnly(footprintRing ?? new AiPathTileSample[0]);
            ReferenceComponent = referenceComponent;
            InputCell = inputCell; ResultCell = resultCell;
            NearbyCells = Array.AsReadOnly(nearbyCells ?? new AiCoarseCellSample[0]);
            capturedNativeAnchors = nativeAnchors;
            capturedApiAnchors = apiAnchors;
            capturedCoarseCells = coarseCells;
        }
        /// <summary>Whether the neighborhood could be sampled.</summary>
        public string Status { get; }
        /// <summary>Input coarse X coordinate.</summary>
        public int InputX { get; }
        /// <summary>Input coarse Y coordinate.</summary>
        public int InputY { get; }
        /// <summary>Result coarse X coordinate, or minus one before the search.</summary>
        public int ResultX { get; }
        /// <summary>Result coarse Y coordinate, or minus one before the search.</summary>
        public int ResultY { get; }
        /// <summary>Five by five coarse-cell anchors centered on the search input.</summary>
        public IReadOnlyList<AiPathTileSample> Anchors { get; }
        /// <summary>Three by three tile sample starting at the search result.</summary>
        public IReadOnlyList<AiPathTileSample> Footprint { get; }
        /// <summary>All 25 live tiles in the selected coarse cell.</summary>
        public IReadOnlyList<AiPathTileSample> CoarseTiles { get; }
        /// <summary>Five by five footprint neighborhood, including its one-tile ring.</summary>
        public IReadOnlyList<AiPathTileSample> FootprintRing { get; }
        /// <summary>Native PCL used when 0x50720 populated foreign-component counts.</summary>
        public int ReferenceComponent { get; }
        /// <summary>Coarse cell at the search input.</summary>
        public AiCoarseCellSample InputCell { get; }
        /// <summary>Coarse cell at the search result, when available.</summary>
        public AiCoarseCellSample ResultCell { get; }
        /// <summary>Nearby coarse cells sampled around the search input.</summary>
        public IReadOnlyList<AiCoarseCellSample> NearbyCells { get; }

        /// <summary>Component-only pre-search snapshot for any coarse anchor.</summary>
        public AiPathTileSample GetCapturedAnchor(int coarseX, int coarseY)
        {
            if (capturedNativeAnchors == null || capturedApiAnchors == null ||
                coarseX < 0 || coarseX >= CoarseSide || coarseY < 0 || coarseY >= CoarseSide)
                return null;
            int index = coarseX * CoarseSide + coarseY;
            if (capturedNativeAnchors[index] < 0 || capturedApiAnchors[index] < 0)
                return null;
            return new AiPathTileSample(coarseX * 5, coarseY * 5, -1,
                capturedNativeAnchors[index], capturedApiAnchors[index], 0,
                -1, -1, -1, "components-only");
        }

        /// <summary>Raw 16-byte pre-search snapshot for any coarse cell.</summary>
        public AiCoarseCellSample GetCapturedCoarseCell(int coarseX, int coarseY)
        {
            if (capturedCoarseCells == null || coarseX < 0 || coarseX >= CoarseSide ||
                coarseY < 0 || coarseY >= CoarseSide) return null;
            var bytes = new byte[CoarseSampleSize];
            Buffer.BlockCopy(capturedCoarseCells,
                (coarseX * CoarseSide + coarseY) * CoarseSampleSize,
                bytes, 0, bytes.Length);
            return new AiCoarseCellSample(coarseX, coarseY,
                BitConverter.ToString(bytes), "pre-search-snapshot");
        }
    }

    /// <summary>Read-only evidence from the native AI construction path.</summary>
    public sealed class AiBuildDiagnosticRecord
    {
        /// <summary>Creates a read-only diagnostic record.</summary>
        public AiBuildDiagnosticRecord(string stage, int playerId, long attemptId, long a, long b, long c, long d)
            : this(stage, playerId, attemptId, a, b, c, d, null) { }

        internal AiBuildDiagnosticRecord(string stage, int playerId, long attemptId,
            long a, long b, long c, long d, AiRouteEvidence routeEvidence,
            AiNearbyPathEvidence nearbyPathEvidence = null,
            AiEconomyGridEvidence economyGridEvidence = null)
        {
            Stage = stage;
            PlayerId = playerId;
            AttemptId = attemptId;
            A = a; B = b; C = c; D = d;
            RouteEvidence = routeEvidence;
            NearbyPathEvidence = nearbyPathEvidence;
            EconomyGridEvidence = economyGridEvidence;
        }
        /// <summary>Native observation stage.</summary>
        public string Stage { get; }
        /// <summary>One-based AI player ID.</summary>
        public int PlayerId { get; }
        /// <summary>Process-unique wood construction attempt, or zero outside one.</summary>
        public long AttemptId { get; }
        /// <summary>Stage-specific first value.</summary>
        public long A { get; }
        /// <summary>Stage-specific second value.</summary>
        public long B { get; }
        /// <summary>Stage-specific third value.</summary>
        public long C { get; }
        /// <summary>Stage-specific fourth value.</summary>
        public long D { get; }
        /// <summary>Copied route inputs and macro-connections, when Stage is route-evidence.</summary>
        public AiRouteEvidence RouteEvidence { get; }
        /// <summary>Copied nearby-search path samples, when present.</summary>
        public AiNearbyPathEvidence NearbyPathEvidence { get; }
        /// <summary>Copied state of the monitored 5-by-5 economy cell.</summary>
        public AiEconomyGridEvidence EconomyGridEvidence { get; }
    }

    /// <summary>Read-only snapshot of one coarse cell and its live tile layers.</summary>
    public sealed class AiEconomyGridEvidence
    {
        internal AiEconomyGridEvidence(string status, ulong state, int mode, int coarseX, int coarseY,
            int referenceComponent,
            int storedForeignCount, int treeWeight, IReadOnlyList<AiPathTileSample> tiles)
        {
            Status = status; State = state; Mode = mode; ReferenceComponent = referenceComponent;
            CoarseX = coarseX; CoarseY = coarseY;
            StoredForeignCount = storedForeignCount; TreeWeight = treeWeight;
            Tiles = tiles ?? Array.AsReadOnly(new AiPathTileSample[0]);
            int different = 0, zero = 0, trees = 0, apples = 0;
            foreach (AiPathTileSample tile in Tiles)
            {
                if (tile.Status != "ok") continue;
                if (referenceComponent > 0 && tile.NativeComponent != referenceComponent) different++;
                if (tile.NativeComponent == 0) zero++;
                if ((tile.PropertyFlags & 0x1000u) != 0) trees++;
                if ((tile.PropertyFlags & 0x04000000u) != 0) apples++;
            }
            CurrentDifferentCount = referenceComponent > 0 && Tiles.Count == 25 ? different : -1;
            CurrentZeroCount = Tiles.Count == 25 ? zero : -1;
            TreeFlagCount = Tiles.Count == 25 ? trees : -1;
            AppleFarmFlagCount = Tiles.Count == 25 ? apples : -1;
        }
        /// <summary>ok only when all monitored tiles and the reference component were read.</summary>
        public string Status { get; }
        /// <summary>Native AIV state pointer; valid only during the current map.</summary>
        public ulong State { get; }
        /// <summary>Native update mode, or minus one for a tick snapshot.</summary>
        public int Mode { get; }
        /// <summary>Zero-based X coordinate of this 5-by-5 coarse-grid cell.</summary>
        public int CoarseX { get; }
        /// <summary>Zero-based Y coordinate of this 5-by-5 coarse-grid cell.</summary>
        public int CoarseY { get; }
        /// <summary>Reference path component stored by Vanilla's full coarse rebuild.</summary>
        public int ReferenceComponent { get; }
        /// <summary>Stored foreign-component count in the selected coarse cell.</summary>
        public int StoredForeignCount { get; }
        /// <summary>Stored Vanilla tree weight; not a count of tree-flagged tiles.</summary>
        public int TreeWeight { get; }
        /// <summary>Current tiles whose native component differs from the reference.</summary>
        public int CurrentDifferentCount { get; }
        /// <summary>Current tiles with native component zero.</summary>
        public int CurrentZeroCount { get; }
        /// <summary>Current tiles carrying the raw tree property bit.</summary>
        public int TreeFlagCount { get; }
        /// <summary>Current tiles carrying the apple-farm property bit.</summary>
        public int AppleFarmFlagCount { get; }
        /// <summary>Copies of all 25 monitored tile records.</summary>
        public IReadOnlyList<AiPathTileSample> Tiles { get; }
    }

}
