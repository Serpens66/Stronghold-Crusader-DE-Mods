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
    /// <summary>Optional process-lifetime AI construction observer. Inert without registration.</summary>
    internal static partial class AiBuildDiagnostic
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
        // AIBuildDiagnoseTest: native 0x539B0 -> 0x2DBA0/0x2C9F0 -> 0x50D80.
        private const int FarmProfileRva = 0x379D0D0;
        private const int FarmChoiceIndexRva = 0x379D824;
        private const int FarmCountRva = 0x379E6F8;
        private const int FarmLimitRva = 0x379E754;
        private const int FarmCooldownRva = 0x379E74A;
        private const int AicProfileRootRva = 0x404C950;
        private static readonly object Sync = new object();
        private static Action<AiBuildDiagnosticRecord> observer;
        // Test-only, process-rooted callback. Null in ordinary installations.
        private static Func<ulong, int, int, int, Action> nearbyWoodOverlay;
        // AIBuildDiagnoseTest only: optional, process-rooted gate for disposable save-copy probes.
        private static Func<int, bool> woodBuildGate;
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

        /// <summary>Registers the optional test-only gate at the existing wood-economy call site.</summary>
        public static bool TryRegisterWoodBuildGate(string ownerGuid, Func<int, bool> gate,
            out string error)
        {
            error = null;
            if (ownerGuid != "AIBuildDiagnoseTest_Serp" || gate == null)
            { error = "Invalid wood-build test gate owner or callback."; return false; }
            lock (Sync)
            {
                if (woodBuildGate != null)
                {
                    if (ReferenceEquals(woodBuildGate, gate)) return true;
                    error = "Another wood-build test gate is already registered.";
                    return false;
                }
                if (Volatile.Read(ref observer) == null)
                { error = "AI build observer is not registered."; return false; }
                Volatile.Write(ref woodBuildGate, gate);
                return true;
            }
        }

        /// <summary>False in ordinary sessions. A failed test callback never blocks Vanilla.</summary>
        public static bool ShouldDeferWoodBuild(int playerId)
        {
            Func<int, bool> gate = Volatile.Read(ref woodBuildGate);
            if (gate == null) return false;
            try { return gate(playerId); }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, "AI wood-build test gate failed: " + ex);
                return false;
            }
        }

        /// <summary>Registers the optional copy-only wood-nearby data overlay owned by AIBuildDiagnoseTest.</summary>
        public static bool TryRegisterNearbyWoodOverlay(string ownerGuid,
            Func<ulong, int, int, int, Action> begin, out string error)
        {
            error = null;
            if (ownerGuid != "AIBuildDiagnoseTest_Serp" || begin == null)
            { error = "Invalid nearby diagnostic owner or callback."; return false; }
            lock (Sync)
            {
                if (nearbyWoodOverlay != null)
                {
                    if (ReferenceEquals(nearbyWoodOverlay, begin)) return true;
                    error = "Another nearby diagnostic callback is already registered.";
                    return false;
                }
                if (Volatile.Read(ref observer) == null)
                { error = "AI build observer is not registered."; return false; }
                Volatile.Write(ref nearbyWoodOverlay, begin);
                return true;
            }
        }

        /// <summary>Starts optional observation at the existing nearby-search call site.</summary>
        public static Action BeginNearbyWoodObservation(ulong state, int playerId, int x, int y)
        {
            if (Volatile.Read(ref observer) == null || state == 0 ||
                playerId < 1 || playerId > 8 || !TryGetCurrentWoodAttempt(out _, out int owner) ||
                owner != playerId) return null;
            Publish("wood-nearby-before", playerId, x, y);
            PublishNearbyPathEvidence("wood-nearby-path-before", playerId, state, x, y, -1, -1);
            Func<ulong, int, int, int, Action> begin = Volatile.Read(ref nearbyWoodOverlay);
            if (begin == null) return null;
            try { return begin(state, playerId, x, y); }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, "AI nearby wood diagnostic begin failed: " + ex);
                return null;
            }
        }

        /// <summary>Restores test data and publishes the unmodified Vanilla result.</summary>
        public static void EndNearbyWoodObservation(Action restore, ulong state, int playerId, int x, int y)
        {
            if (restore != null)
            {
                try { restore(); }
                catch (Exception ex) { Shared.DebugLogHelper.LogError(log, "AI nearby wood diagnostic restore failed: " + ex); }
            }
            if (Volatile.Read(ref observer) == null || state == 0 ||
                playerId < 1 || playerId > 8 || !TryGetCurrentWoodAttempt(out _, out int owner) ||
                owner != playerId) return;
            int resultX = -1, resultY = -1;
            try
            {
                resultX = Marshal.ReadInt32(new IntPtr(checked((long)state + 0x1B983C)));
                resultY = Marshal.ReadInt32(new IntPtr(checked((long)state + 0x1B9840)));
            }
            catch (Exception ex) { Shared.DebugLogHelper.LogError(log, "AI nearby wood result read failed: " + ex); }
            PublishNearbyPathEvidence("wood-nearby-path-after", playerId, state, x, y,
                resultX, resultY);
            Publish("wood-nearby-after", playerId, resultX, resultY);
        }

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
                    !string.Equals(nativeHash, NativeDiagnosticContracts.SupportedHash, StringComparison.OrdinalIgnoreCase))
                {
                    error = "The validated native module is unavailable.";
                    return false;
                }
                var failures = new List<string>();
                try
                {
                    byte[] bytes = new byte[SchedulerSize];
                    Marshal.Copy(new IntPtr(checked(moduleBase + SchedulerRva)), bytes, 0, bytes.Length);
                    if (!string.Equals(NativeDiagnosticContracts.ComputeSha256(bytes), SchedulerHash, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Scheduler function hash differs from the audited build.");
                    ValidateEntry(bytes, SchedulerRva, ExpectedDisplacedBytes);
                    ProbeBackend(unchecked((ulong)moduleBase) + SchedulerRva, callback);
                    var candidate = new SchedulerService();
                    scheduler = candidate;
                    candidate.Install(moduleBase, region);
                    Volatile.Write(ref schedulerReady, 1);
                }
                catch (Exception ex) { failures.Add("scheduler: " + ex.Message); }
                try
                {
                    byte[] routeBytes = new byte[RouteSize];
                    Marshal.Copy(new IntPtr(checked(moduleBase + RouteRva)), routeBytes, 0, routeBytes.Length);
                    if (!string.Equals(NativeDiagnosticContracts.ComputeSha256(routeBytes), RouteHash, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Route function hash differs from the audited build.");
                    ValidateRouteLayout();
                    ValidateEntry(routeBytes, RouteRva, RouteDisplacedBytes);
                    ProbeRouteBackend(unchecked((ulong)moduleBase) + RouteRva);
                    var routeCandidate = new RouteService();
                    route = routeCandidate;
                    routeCandidate.Install(moduleBase, region);
                    Volatile.Write(ref routeReady, 1);
                }
                catch (Exception ex) { failures.Add("route: " + ex.Message); }
                // Successfully installed hooks stay rooted even if the other point fails.
                Volatile.Write(ref observer, callback);
                if (failures.Count == 0) return true;
                error = string.Join("; ", failures);
                return false;
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
            catch (Exception ex) { Shared.DebugLogHelper.LogError(log, "AI diagnostic observer failed: " + ex); }
        }

        /// <summary>Diagnostic-only snapshot around Vanilla's existing 0x50720 call.</summary>
        public static void PublishEconomyGridEvidence(string stage, ulong state, int mode)
        {
            Action<AiBuildDiagnosticRecord> target = Volatile.Read(ref observer);
            if (target == null) return;
            AiEconomyGridEvidence evidence = CaptureEconomyGridEvidence(state, mode);
            try { target(new AiBuildDiagnosticRecord(stage, 0, 0, mode, 0, 0, 0,
                null, null, evidence)); }
            catch (Exception ex) { Shared.DebugLogHelper.LogError(log, "AI economy grid observer failed: " + ex); }
        }

        /// <summary>Copies one monitored coarse cell; intended for the diagnostic tick publisher.</summary>
        public static AiEconomyGridEvidence CaptureEconomyGridEvidence(ulong state, int mode)
            => CaptureEconomyGridEvidence(state, mode, 69, 97);

        /// <summary>Diagnostic-only snapshot of a selected coarse cell in the current AIV system.</summary>
        public static unsafe AiEconomyGridEvidence CaptureEconomyGridEvidence(int coarseX, int coarseY)
        {
            if (!HasObserver)
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
        public static AiEconomyGridEvidence CaptureEconomyGridEvidence(ulong state, int mode,
            int coarseX, int coarseY)
        {
            if (!HasObserver || state == 0 || moduleBase == 0)
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
            catch (Exception ex) { Shared.DebugLogHelper.LogError(log, "AI route evidence observer failed: " + ex); }
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
                Shared.DebugLogHelper.LogError(log, "AI nearby path capture failed: " + ex);
                evidence = new AiNearbyPathEvidence("capture-exception:" + ex.GetType().Name,
                    inputX, inputY, resultX, resultY,
                    new AiPathTileSample[0], new AiPathTileSample[0], null, null, null);
            }
            try
            {
                recipient(new AiBuildDiagnosticRecord(stage, playerId, id,
                    inputX, inputY, resultX, resultY, null, evidence));
            }
            catch (Exception ex) { Shared.DebugLogHelper.LogError(log, "AI nearby path observer failed: " + ex); }
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

    }
}
