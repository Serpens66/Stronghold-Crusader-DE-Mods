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

namespace APIShared
{
    // AIBuildDiagnoseTest BEGIN -- remove this file and its two project/bootstrap references together.
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

    /// <summary>Read-only snapshot of the Canari coarse cell and its live tile layers.</summary>
    public sealed class AiEconomyGridEvidence
    {
        internal AiEconomyGridEvidence(string status, ulong state, int mode, int referenceComponent,
            int storedForeignCount, int treeWeight, IReadOnlyList<AiPathTileSample> tiles)
        {
            Status = status; State = state; Mode = mode; ReferenceComponent = referenceComponent;
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
        /// <summary>Reference path component stored by Vanilla's full coarse rebuild.</summary>
        public int ReferenceComponent { get; }
        /// <summary>Stored foreign-component count in coarse cell 69,97.</summary>
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

    /// <summary>Optional process-lifetime AI construction observer. Inert without registration.</summary>
    public static class AiBuildDiagnostic
    {
        private const int SchedulerRva = 0x539B0;
        private const int SchedulerSize = 743;
        private const int ExpectedDisplacedBytes = 10;
        private const string SchedulerHash = "477FC129E9B1A2BBFD9BE2093F30C2E21BC0964337A9843CC7F7BF34531FED8E";
        private const int RouteRva = 0xC3BF0;
        private const int RouteSize = 133;
        private const int RouteDisplacedBytes = 7;
        private const string RouteHash = "1AC2D046356187774EE0EF0C7F782C59B3E416BAEDDBD463305304CF709EB0B0";
        private const int PlayerStride = 0x583C;
        private const int VillageSlotRva = 0x379D0CC;
        private const int SchedulerDelayRva = 0x379D8B0;
        private const int EconomyPhaseRva = 0x379E630;
        private static readonly object Sync = new object();
        private static Action<AiBuildDiagnosticRecord> observer;
        private static SchedulerService scheduler;
        private static RouteService route;
        private static int schedulerReady;
        private static int routeReady;
        private static long nextAttemptId;
        [ThreadStatic] private static Stack<WoodAttempt> woodAttempts;
        private static long moduleBase;
        private static string nativeHash;
        private static ScanRegion region;
        private static ManualLogSource log;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr size,
            uint allocationType, uint protect);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool VirtualFree(IntPtr address, UIntPtr size, uint freeType);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void SchedulerDelegate(ulong state, int playerId);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int RouteDelegate(ulong manager, int playerId, int mapperIndex, int tileX, int tileY);

        private struct WoodAttempt
        {
            internal long Id;
            internal int PlayerId;
        }

        internal static void Initialize(long baseAddress, string hash, ScanRegion scanRegion, ManualLogSource logger)
        {
            lock (Sync)
            {
                moduleBase = baseAddress;
                nativeHash = hash;
                region = scanRegion;
                log = logger;
            }
        }

        /// <summary>True only after the test observer has registered.</summary>
        public static bool HasObserver => Volatile.Read(ref observer) != null;
        /// <summary>Whether the native scheduler observation point is ready.</summary>
        public static bool SchedulerReady => Volatile.Read(ref schedulerReady) != 0;
        /// <summary>Whether the native route observation point is ready.</summary>
        public static bool RouteReady => Volatile.Read(ref routeReady) != 0;

        /// <summary>Registers a single observer and installs the audited observation hooks once.</summary>
        public static bool TryRegister(string ownerGuid, Action<AiBuildDiagnosticRecord> callback, out string error)
        {
            error = null;
            if (ownerGuid != "AIBuildDiagnoseTest_Serp" || callback == null)
            {
                error = "The diagnostic owner or callback is invalid.";
                return false;
            }
            lock (Sync)
            {
                if (observer != null)
                {
                    if (ReferenceEquals(observer, callback)) return true;
                    error = "A different diagnostic observer is already registered.";
                    return false;
                }
                if (moduleBase == 0 || region == null ||
                    !string.Equals(nativeHash, ApiSharedRuntime.SupportedHash, StringComparison.OrdinalIgnoreCase))
                {
                    error = "The validated native module is unavailable.";
                    return false;
                }
                try
                {
                    byte[] bytes = new byte[SchedulerSize];
                    Marshal.Copy(new IntPtr(checked(moduleBase + SchedulerRva)), bytes, 0, bytes.Length);
                    if (!string.Equals(ApiSharedRuntime.ComputeSha256(bytes), SchedulerHash, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Scheduler function hash differs from the audited build.");
                    ValidateEntry(bytes, SchedulerRva, ExpectedDisplacedBytes);
                    ProbeBackend(unchecked((ulong)moduleBase) + SchedulerRva, callback);
                    var candidate = new SchedulerService();
                    scheduler = candidate;
                    candidate.Install(moduleBase, region);
                    Volatile.Write(ref schedulerReady, 1);
                    byte[] routeBytes = new byte[RouteSize];
                    Marshal.Copy(new IntPtr(checked(moduleBase + RouteRva)), routeBytes, 0, routeBytes.Length);
                    if (!string.Equals(ApiSharedRuntime.ComputeSha256(routeBytes), RouteHash, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Route function hash differs from the audited build.");
                    ValidateRouteLayout();
                    ValidateEntry(routeBytes, RouteRva, RouteDisplacedBytes);
                    ProbeRouteBackend(unchecked((ulong)moduleBase) + RouteRva);
                    var routeCandidate = new RouteService();
                    route = routeCandidate;
                    routeCandidate.Install(moduleBase, region);
                    Volatile.Write(ref routeReady, 1);
                    Volatile.Write(ref observer, callback);
                    return true;
                }
                catch (Exception ex)
                {
                    // Any successfully installed hook stays process-rooted. The existing
                    // BugfixesAndQoL observations remain useful if another hook fails.
                    Volatile.Write(ref observer, callback);
                    error = ex.Message;
                    return false;
                }
            }
        }

        /// <summary>Publishes a record only while the test observer is present.</summary>
        public static void Publish(string stage, int playerId, long a = 0, long b = 0, long c = 0, long d = 0)
        {
            Action<AiBuildDiagnosticRecord> target = Volatile.Read(ref observer);
            if (target == null) return;
            long attemptId = TryGetCurrentWoodAttempt(out long currentId, out int currentPlayer) &&
                currentPlayer == playerId ? currentId : 0;
            try { target(new AiBuildDiagnosticRecord(stage, playerId, attemptId, a, b, c, d)); }
            catch (Exception ex) { NativeApiLog.Error(log, "AI diagnostic observer failed: " + ex); }
        }

        /// <summary>Diagnostic-only snapshot around Vanilla's existing 0x50720 call.</summary>
        public static void PublishEconomyGridEvidence(string stage, ulong state, int mode)
        {
            Action<AiBuildDiagnosticRecord> target = Volatile.Read(ref observer);
            if (target == null) return;
            AiEconomyGridEvidence evidence = CaptureEconomyGridEvidence(state, mode);
            try { target(new AiBuildDiagnosticRecord(stage, 0, 0, mode, 0, 0, 0,
                null, null, evidence)); }
            catch (Exception ex) { NativeApiLog.Error(log, "AI economy grid observer failed: " + ex); }
        }

        /// <summary>Copies one monitored coarse cell; intended for the diagnostic tick publisher.</summary>
        public static AiEconomyGridEvidence CaptureEconomyGridEvidence(ulong state, int mode)
        {
            if (!HasObserver || state == 0 || moduleBase == 0)
                return new AiEconomyGridEvidence("unavailable", state, mode, -1, -1, -1, null);
            try
            {
                const int coarseX = 69, coarseY = 97;
                long cell = checked((long)state + 0x5B834 +
                    ((long)coarseX * 160 + coarseY) * 0x30);
                int reference = Marshal.ReadInt32(new IntPtr(checked((long)state + 0x5B504)));
                int foreign = Marshal.ReadByte(new IntPtr(cell));
                int treeWeight = Marshal.ReadByte(new IntPtr(cell + 3));
                IReadOnlyList<AiPathTileSample> tiles = CaptureTiles(345, 485, 5, 5);
                bool allTilesReady = tiles.Count == 25;
                foreach (AiPathTileSample tile in tiles)
                    if (tile.Status != "ok") allTilesReady = false;
                string status = allTilesReady && reference > 0 ? "ok" : "tile-grid-not-ready";
                return new AiEconomyGridEvidence(status, state, mode, reference,
                    foreign, treeWeight, tiles);
            }
            catch (Exception ex)
            {
                return new AiEconomyGridEvidence("capture-exception:" + ex.GetType().Name,
                    state, mode, -1, -1, -1, null);
            }
        }

        private static void PublishRouteEvidence(int playerId, AiRouteEvidence evidence)
        {
            Action<AiBuildDiagnosticRecord> target = Volatile.Read(ref observer);
            if (target == null) return;
            long attemptId = TryGetCurrentWoodAttempt(out long currentId, out int currentPlayer) &&
                currentPlayer == playerId ? currentId : 0;
            try
            {
                target(new AiBuildDiagnosticRecord("route-evidence", playerId, attemptId,
                    evidence.SourceComponent, evidence.TargetComponent,
                    evidence.SourceTile, evidence.TargetTile, evidence));
            }
            catch (Exception ex) { NativeApiLog.Error(log, "AI route evidence observer failed: " + ex); }
        }

        /// <summary>Copies coarse cells and tile layers around Vanilla's existing nearby search.</summary>
        public static void PublishNearbyPathEvidence(string stage, int playerId,
            ulong state, int inputX, int inputY, int resultX, int resultY)
        {
            Action<AiBuildDiagnosticRecord> recipient = Volatile.Read(ref observer);
            if (recipient == null || !TryGetCurrentWoodAttempt(out long id, out int owner) ||
                owner != playerId) return;
            AiNearbyPathEvidence evidence;
            try { evidence = CaptureNearbyPathEvidence(state, inputX, inputY, resultX,
                resultY, stage == "wood-nearby-path-before"); }
            catch (Exception ex)
            {
                NativeApiLog.Error(log, "AI nearby path capture failed: " + ex);
                evidence = new AiNearbyPathEvidence("capture-exception:" + ex.GetType().Name,
                    inputX, inputY, resultX, resultY,
                    new AiPathTileSample[0], new AiPathTileSample[0], null, null, null);
            }
            try
            {
                recipient(new AiBuildDiagnosticRecord(stage, playerId, id,
                    inputX, inputY, resultX, resultY, null, evidence));
            }
            catch (Exception ex) { NativeApiLog.Error(log, "AI nearby path observer failed: " + ex); }
        }

        private static AiNearbyPathEvidence CaptureNearbyPathEvidence(ulong state,
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
        public static IReadOnlyList<AiPathTileSample> CaptureTiles(int x, int y,
            int width, int height)
        {
            if (!HasObserver || width < 1 || height < 1 || width > 8 || height > 8 ||
                moduleBase == 0) return Array.AsReadOnly(new AiPathTileSample[0]);
            Span<ushort> grid = GamePathingManagerAPI.Instance.GetPathComponentGrid();
            if (grid.Length != 320800) return Array.AsReadOnly(new AiPathTileSample[0]);
            return Array.AsReadOnly(CaptureTiles(x, y, width, height, grid));
        }

        private static AiPathTileSample[] CaptureTiles(int x, int y, int width,
            int height, Span<ushort> grid)
        {
            var samples = new AiPathTileSample[width * height];
            int index = 0;
            for (int dy = 0; dy < height; dy++)
                for (int dx = 0; dx < width; dx++)
                    samples[index++] = SamplePathTile(x + dx, y + dy, grid);
            return samples;
        }

        private static AiCoarseCellSample SampleCoarseCell(ulong state, int x, int y)
        {
            if (state == 0 || x < 0 || x >= 160 || y < 0 || y >= 160)
                return new AiCoarseCellSample(x, y, "", "invalid-coarse-cell");
            byte[] bytes = new byte[16];
            long address = checked((long)state + 0x5B834 + ((long)x * 160 + y) * 0x30);
            Marshal.Copy(new IntPtr(address), bytes, 0, bytes.Length);
            return new AiCoarseCellSample(x, y, BitConverter.ToString(bytes), "ok");
        }

        private static AiPathTileSample SamplePathTile(int x, int y, Span<ushort> grid)
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

        /// <summary>Enters the synchronous Vanilla wood call; zero means diagnostics are absent.</summary>
        public static long BeginWoodAttempt(int playerId)
        {
            if (!HasObserver || playerId < 1 || playerId > 8) return 0;
            long id = Interlocked.Increment(ref nextAttemptId);
            if (woodAttempts == null) woodAttempts = new Stack<WoodAttempt>();
            woodAttempts.Push(new WoodAttempt { Id = id, PlayerId = playerId });
            return id;
        }

        /// <summary>Restores the previous synchronous wood context after the Vanilla call.</summary>
        public static void EndWoodAttempt(long id)
        {
            if (id == 0 || woodAttempts == null || woodAttempts.Count == 0) return;
            if (woodAttempts.Peek().Id == id) woodAttempts.Pop();
            else woodAttempts.Clear();
        }

        /// <summary>Gets the active attempt on the current simulation thread.</summary>
        public static bool TryGetCurrentWoodAttempt(out long id, out int playerId)
        {
            if (woodAttempts != null && woodAttempts.Count != 0)
            {
                WoodAttempt current = woodAttempts.Peek();
                id = current.Id;
                playerId = current.PlayerId;
                return true;
            }
            id = 0;
            playerId = 0;
            return false;
        }

        /// <summary>Reads the audited native placement flags without changing them.</summary>
        public static bool TryReadPlacementStatus(out int preparation, out int rejected, out int placementMode)
        {
            preparation = rejected = placementMode = 0;
            if (!HasObserver || moduleBase == 0) return false;
            try
            {
                long tileManager = checked(moduleBase + 0x405EDB0);
                preparation = Marshal.ReadInt32(new IntPtr(tileManager + 0x204E6F8));
                rejected = Marshal.ReadInt32(new IntPtr(tileManager + 0x204E6FC));
                placementMode = Marshal.ReadInt32(new IntPtr(tileManager + 0x204E7FC));
                return true;
            }
            catch { return false; }
        }

        private sealed class SchedulerService
        {
            private readonly DetourHandle<SchedulerDelegate> hook = new DetourHandle<SchedulerDelegate>();
            private HookTransaction transaction;

            internal void Install(long baseAddress, ScanRegion scanRegion)
            {
                var pending = new HookTransaction(scanRegion,
                    SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                    new HookTransactionOptions
                    {
                        FailureMode = TransactionFailureMode.RollbackAndThrow,
                        OwnsHooks = false
                    });
                try
                {
                    pending.AddDetour(hook, HookTarget.FromAddress(unchecked((ulong)baseAddress) + SchedulerRva), Invoke);
                    CommitResult result = pending.Commit();
                    if (!result.IsCompleteSuccess || !hook.Success)
                        throw new InvalidOperationException("Scheduler diagnostic hook did not commit completely.");
                    transaction = pending;
                    ValidateDetour(hook.Hook as NativeDetour<SchedulerDelegate>,
                        unchecked((ulong)baseAddress) + SchedulerRva);
                }
                catch
                {
                    if (!hook.Success) pending.Dispose(); // Unpublished candidate only.
                    throw;
                }
            }

            private void Invoke(ulong state, int playerId)
            {
                if (playerId >= 1 && playerId <= 8)
                {
                    try { PublishScheduler("scheduler-before", playerId); }
                    catch (Exception ex) { NativeApiLog.Error(log, "AI scheduler pre-observation failed: " + ex); }
                }
                try { hook.Original(state, playerId); }
                finally
                {
                    if (playerId >= 1 && playerId <= 8)
                    {
                        try { PublishScheduler("scheduler-after", playerId); }
                        catch (Exception ex) { NativeApiLog.Error(log, "AI scheduler post-observation failed: " + ex); }
                    }
                }
            }

            private static void PublishScheduler(string stage, int playerId)
            {
                long offset = checked(moduleBase + (long)playerId * PlayerStride);
                int village = Marshal.ReadInt32(new IntPtr(offset + VillageSlotRva));
                int delay = Marshal.ReadInt32(new IntPtr(offset + SchedulerDelayRva));
                int phase = Marshal.ReadInt32(new IntPtr(offset + EconomyPhaseRva));
                Publish(stage, playerId, village, delay, phase);
            }
        }

        private sealed class RouteService
        {
            private readonly DetourHandle<RouteDelegate> hook = new DetourHandle<RouteDelegate>();
            private HookTransaction transaction;

            internal void Install(long baseAddress, ScanRegion scanRegion)
            {
                var pending = new HookTransaction(scanRegion,
                    SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                    new HookTransactionOptions
                    {
                        FailureMode = TransactionFailureMode.RollbackAndThrow,
                        OwnsHooks = false
                    });
                try
                {
                    ulong target = unchecked((ulong)baseAddress) + RouteRva;
                    pending.AddDetour(hook, HookTarget.FromAddress(target), Invoke);
                    CommitResult result = pending.Commit();
                    if (!result.IsCompleteSuccess || !hook.Success)
                        throw new InvalidOperationException("Route diagnostic hook did not commit completely.");
                    transaction = pending;
                    ValidateRouteDetour(hook.Hook as NativeDetour<RouteDelegate>, target);
                }
                catch
                {
                    if (!hook.Success) pending.Dispose(); // Unpublished candidate only.
                    throw;
                }
            }

            private int Invoke(ulong manager, int playerId, int mapperIndex, int tileX, int tileY)
            {
                bool observe = mapperIndex == 3 &&
                    TryGetCurrentWoodAttempt(out long id, out int owner) && owner == playerId && HasObserver;
                AiRouteEvidence evidence = null;
                if (observe)
                {
                    try { evidence = CaptureRouteEvidence(playerId, mapperIndex, tileX, tileY); }
                    catch (Exception ex)
                    {
                        NativeApiLog.Error(log, "AI route evidence capture failed: " + ex);
                        evidence = Unavailable("capture-exception:" + ex.GetType().Name,
                            tileX, tileY, -1, -1);
                    }
                }
                int result = hook.Original(manager, playerId, mapperIndex, tileX, tileY);
                if (observe)
                {
                    try
                    {
                        PublishRouteEvidence(playerId, evidence);
                        Publish("route-result", playerId, result, mapperIndex, tileX, tileY);
                    }
                    catch (Exception ex) { NativeApiLog.Error(log, "AI route result observation failed: " + ex); }
                }
                return result;
            }

            private static AiRouteEvidence Unavailable(string status, int x, int y,
                int sourceTile, int targetTile) =>
                new AiRouteEvidence(status, x, y, sourceTile, targetTile,
                    -1, -1, -1, -1, -1, -1, new AiRouteConnection[0]);

            private static unsafe AiRouteEvidence CaptureRouteEvidence(int playerId,
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

        private static void ValidateEntry(byte[] bytes, int rva, int displaced)
        {
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes));
            decoder.IP = 0x180000000UL + (uint)rva;
            int length = 0;
            while (length < 6)
            {
                Instruction instruction = decoder.Decode();
                if (decoder.LastError != DecoderError.None || instruction.IsInvalid ||
                    instruction.FlowControl != FlowControl.Next)
                    throw new InvalidOperationException("Diagnostic detour entry is not a straight-line prologue.");
                length += instruction.Length;
            }
            if (length != displaced)
                throw new InvalidOperationException("Diagnostic indirect displacement differs from audit.");
        }

        private static void ValidateRouteLayout()
        {
            if (Marshal.SizeOf(typeof(PathConnectionRecord)) != 0x204 ||
                Marshal.OffsetOf(typeof(PathConnectionRecord), nameof(PathConnectionRecord.r_IsActive)).ToInt32() != 0 ||
                Marshal.OffsetOf(typeof(PathConnectionRecord), nameof(PathConnectionRecord.r_ConnectionClass)).ToInt32() != 4 ||
                Marshal.OffsetOf(typeof(PathConnectionRecord), nameof(PathConnectionRecord.r_BuildingId)).ToInt32() != 0x0C ||
                Marshal.OffsetOf(typeof(PathConnectionRecord), nameof(PathConnectionRecord.r_IsEnabledOrOpen)).ToInt32() != 0x18 ||
                Marshal.OffsetOf(typeof(PathConnectionRecord), nameof(PathConnectionRecord.r_PathComponentA)).ToInt32() != 0x34 ||
                Marshal.OffsetOf(typeof(PathConnectionRecord), nameof(PathConnectionRecord.r_PathComponentB)).ToInt32() != 0x38 ||
                Marshal.OffsetOf(typeof(PathConnectionRecord), nameof(PathConnectionRecord.r_OwnerOrAccessPlayerId)).ToInt32() != 0x1E4 ||
                Marshal.OffsetOf(typeof(PathConnectionRecord), nameof(PathConnectionRecord.r_PathComponentC)).ToInt32() != 0x1E8)
                throw new InvalidOperationException("Path connection layout differs from the audited native record.");
        }

        private static void ProbeRouteBackend(ulong entry)
        {
            IntPtr copy = AllocateRouteProbeNear(entry);
            NativeDetour<RouteDelegate> probe = null;
            try
            {
                byte[] bytes = new byte[64];
                Marshal.Copy(unchecked((IntPtr)(long)entry), bytes, 0, bytes.Length);
                Marshal.Copy(bytes, 0, copy, bytes.Length);
                RouteDelegate noop = (manager, playerId, mapperIndex, tileX, tileY) => 1;
                var request = new DetourRequest<RouteDelegate>
                {
                    Name = "AIBuildDiagnoseTest copied route entry",
                    TargetAddress = unchecked((ulong)copy.ToInt64()),
                    Callback = noop
                };
                probe = NativeDetourBackend.Instance.CreateDetour(in request) as NativeDetour<RouteDelegate>;
                if (probe == null || probe.Scheme.ToString() != "Indirect" ||
                    probe.DisplacedByteCount != RouteDisplacedBytes || probe.IsInstalled ||
                    probe.TargetAddress != unchecked((ulong)copy.ToInt64()) ||
                    probe.PointerSlot == IntPtr.Zero || probe.HookEntryPointAddress == IntPtr.Zero)
                    throw new InvalidOperationException("Installed RedBird backend rejects the route entry contract.");
                probe.Enable();
                ValidateRouteDetour(probe, unchecked((ulong)copy.ToInt64()));
                GC.KeepAlive(noop);
            }
            finally
            {
                try { probe?.Dispose(); }
                finally
                {
                    if (!VirtualFree(copy, UIntPtr.Zero, 0x8000))
                        NativeApiLog.Error(log, "AI route probe scratch release failed: " + Marshal.GetLastWin32Error());
                }
            }
        }

        private static IntPtr AllocateRouteProbeNear(ulong entry)
        {
            // The installed NativeX64 indirect backend reserves a trampoline within
            // +/-2 GiB of its target. A heap copy may sit outside usable address space.
            const long stride = 0x1000000;
            const long granularity = 0x10000;
            long aligned = ((long)entry + granularity - 1) & ~(granularity - 1);
            for (int distance = 8; distance <= 96; distance++)
            {
                foreach (int direction in new[] { 1, -1 })
                {
                    long candidate;
                    try { candidate = checked(aligned + direction * distance * stride); }
                    catch (OverflowException) { continue; }
                    if (candidate <= 0) continue;
                    IntPtr allocation = VirtualAlloc(new IntPtr(candidate), new UIntPtr(0x10000),
                        0x3000, 0x40);
                    if (allocation != IntPtr.Zero &&
                        Math.Abs(allocation.ToInt64() - (long)entry) < 0x70000000)
                        return allocation;
                    if (allocation != IntPtr.Zero)
                        VirtualFree(allocation, UIntPtr.Zero, 0x8000);
                }
            }
            throw new InvalidOperationException("No near-module scratch buffer is available for the route backend probe.");
        }

        private static void ValidateRouteDetour(NativeDetour<RouteDelegate> detour, ulong target)
        {
            if (detour == null || !detour.IsInstalled || detour.TargetAddress != target ||
                detour.Scheme.ToString() != "Indirect" ||
                detour.DisplacedByteCount != RouteDisplacedBytes ||
                detour.PointerSlot == IntPtr.Zero || detour.HookEntryPointAddress == IntPtr.Zero)
                throw new InvalidOperationException("Committed route detour differs from the indirect backend contract.");
            byte[] patch = new byte[6];
            Marshal.Copy(unchecked((IntPtr)(long)target), patch, 0, patch.Length);
            if (patch[0] != 0xFF || patch[1] != 0x25 ||
                checked((long)target + 6 + BitConverter.ToInt32(patch, 2)) != detour.PointerSlot.ToInt64() ||
                Marshal.ReadInt64(detour.PointerSlot) != detour.HookEntryPointAddress.ToInt64())
                throw new InvalidOperationException("Committed route pointer slot or hook entry differs.");
        }

        private static void ProbeBackend(ulong entry, Action<AiBuildDiagnosticRecord> callback)
        {
            IntPtr copy = Marshal.AllocHGlobal(64);
            NativeDetour<SchedulerDelegate> probe = null;
            try
            {
                byte[] bytes = new byte[64];
                Marshal.Copy(unchecked((IntPtr)(long)entry), bytes, 0, bytes.Length);
                Marshal.Copy(bytes, 0, copy, bytes.Length);
                SchedulerDelegate noop = (state, playerId) => { };
                var request = new DetourRequest<SchedulerDelegate>
                {
                    Name = "AIBuildDiagnoseTest copied scheduler entry",
                    TargetAddress = unchecked((ulong)copy.ToInt64()),
                    Callback = noop
                };
                probe = NativeDetourBackend.Instance.CreateDetour(in request) as NativeDetour<SchedulerDelegate>;
                if (probe == null || probe.Scheme.ToString() != "Indirect" ||
                    probe.DisplacedByteCount != ExpectedDisplacedBytes || probe.IsInstalled ||
                    probe.TargetAddress != unchecked((ulong)copy.ToInt64()) ||
                    probe.PointerSlot == IntPtr.Zero || probe.HookEntryPointAddress == IntPtr.Zero)
                    throw new InvalidOperationException("Installed RedBird backend rejects the scheduler entry contract.");
                probe.Enable();
                ValidateDetour(probe, unchecked((ulong)copy.ToInt64()));
                GC.KeepAlive(callback);
                GC.KeepAlive(noop);
            }
            finally
            {
                probe?.Dispose();
                Marshal.FreeHGlobal(copy);
            }
        }

        private static void ValidateDetour(NativeDetour<SchedulerDelegate> detour, ulong target)
        {
            if (detour == null || !detour.IsInstalled || detour.TargetAddress != target ||
                detour.Scheme.ToString() != "Indirect" ||
                detour.DisplacedByteCount != ExpectedDisplacedBytes ||
                detour.PointerSlot == IntPtr.Zero || detour.HookEntryPointAddress == IntPtr.Zero)
                throw new InvalidOperationException("Committed scheduler detour differs from the indirect backend contract.");
            byte[] patch = new byte[6];
            Marshal.Copy(unchecked((IntPtr)(long)target), patch, 0, patch.Length);
            if (patch[0] != 0xFF || patch[1] != 0x25 ||
                checked((long)target + 6 + BitConverter.ToInt32(patch, 2)) != detour.PointerSlot.ToInt64() ||
                Marshal.ReadInt64(detour.PointerSlot) != detour.HookEntryPointAddress.ToInt64())
                throw new InvalidOperationException("Committed scheduler pointer slot or hook entry differs.");
        }
    }
    // AIBuildDiagnoseTest END
}
