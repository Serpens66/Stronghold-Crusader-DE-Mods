using APIShared;
using System;
using System.Diagnostics;

namespace BugfixesAndQoL.UnitCommands
{
    internal enum CompletedMoatRelationship : byte
    {
        Invalid = 0,
        Friendly = 1,
        Enemy = 2
    }

    internal enum MoatTraversalPolicy : byte
    {
        FriendlyOnly = 0,
        AllowEnemyForDiagnostic = 1,
        GroundOnly = 2
    }

    internal enum MoatTraversalEdgeKind : byte
    {
        Ground = 0,
        FriendlyMoat = 1,
        EnemyMoat = 2
    }

    internal delegate CompletedMoatRelationship CompletedMoatRelationshipResolver(
        int playerId, int tileId);

    internal delegate bool NativeSpecialStructureResolver(int tileId);

    internal readonly struct WeightedMovementCostProfile : IEquatable<WeightedMovementCostProfile>
    {
        internal const int MoatDelayPenalty = 6;

        internal WeightedMovementCostProfile(
            int currentSpeed,
            int currentSpeed2,
            int speedBonus,
            int additionalSubsteps,
            int extraDelay,
            int moatPhase,
            int currentTerrainPenalty,
            int normalizedDelay,
            int cadenceProgress,
            bool startedOnCompletedMoat)
        {
            CurrentSpeed = currentSpeed;
            CurrentSpeed2 = currentSpeed2;
            SpeedBonus = speedBonus;
            AdditionalSubsteps = additionalSubsteps;
            ExtraDelay = extraDelay;
            MoatPhase = moatPhase;
            CurrentTerrainPenalty = currentTerrainPenalty;
            NormalizedDelay = normalizedDelay;
            CadenceProgress = cadenceProgress;
            StartedOnCompletedMoat = startedOnCompletedMoat;
        }

        public int CurrentSpeed { get; }
        public int CurrentSpeed2 { get; }
        public int SpeedBonus { get; }
        public int AdditionalSubsteps { get; }
        public int ExtraDelay { get; }
        public int MoatPhase { get; }
        public int CurrentTerrainPenalty { get; }
        public int NormalizedDelay { get; }
        public int CadenceProgress { get; }
        public bool StartedOnCompletedMoat { get; }

        public long GroundEdgeFixedCost => GetEdgeFixedCost(false);

        public static bool TryCreate(
            int currentSpeed,
            int currentSpeed2,
            int speedBonus,
            int additionalSubsteps,
            int extraDelay,
            int moatPhase,
            bool currentTileIsCompletedMoat,
            out WeightedMovementCostProfile profile,
            out string rejectionReason)
        {
            profile = new WeightedMovementCostProfile(
                currentSpeed,
                currentSpeed2,
                speedBonus,
                additionalSubsteps,
                extraDelay,
                moatPhase,
                0,
                currentSpeed2,
                0,
                currentTileIsCompletedMoat);
            rejectionReason = null;
            if (currentSpeed < 0 || currentSpeed2 < 0 || speedBonus < 0 ||
                additionalSubsteps < 0 || extraDelay < 0)
            {
                rejectionReason = "negative-runtime-speed-field";
                return false;
            }
            if (moatPhase < 0 || moatPhase > 24 || (moatPhase & 3) != 0)
            {
                rejectionReason = "invalid-moat-slowdown-phase";
                return false;
            }

            // 0x19B260 rebuilds CurrentSpeed2 from several transient movement fields before
            // adding the moat phase. Runtime measurements show that subtracting only +3/+4/+6
            // therefore does not recover the stable cadence while that phase is active.
            int terrainPenalty = 0;
            if (moatPhase != 0)
            {
                // CurrentSpeed remains the stable per-unit base in the confirmed movement
                // contract. This is also the only safe snapshot for a route starting on a moat.
                terrainPenalty = currentSpeed2 - currentSpeed;
                if (terrainPenalty < 0)
                {
                    rejectionReason = "inconsistent-runtime-speed-fields";
                    return false;
                }
            }
            int normalizedDelay = moatPhase != 0 ? currentSpeed : currentSpeed2;
            if (normalizedDelay < 0 || speedBonus == int.MaxValue)
            {
                rejectionReason = "inconsistent-runtime-speed-fields";
                return false;
            }

            if (speedBonus > int.MaxValue - additionalSubsteps - 1)
            {
                rejectionReason = "runtime-speed-overflow";
                return false;
            }
            int cadenceProgress = speedBonus + additionalSubsteps + 1;
            long groundInterval = (long)normalizedDelay + speedBonus + extraDelay + 1L;
            long moatInterval = groundInterval + MoatDelayPenalty;
            if (cadenceProgress <= 0 || groundInterval <= 0 || moatInterval <= 0 ||
                moatInterval > long.MaxValue / 8L)
            {
                rejectionReason = "runtime-speed-overflow";
                return false;
            }

            profile = new WeightedMovementCostProfile(
                currentSpeed,
                currentSpeed2,
                speedBonus,
                additionalSubsteps,
                extraDelay,
                moatPhase,
                terrainPenalty,
                normalizedDelay,
                cadenceProgress,
                currentTileIsCompletedMoat);
            return true;
        }

        public long GetEdgeFixedCost(bool moatEdge)
        {
            long delay = NormalizedDelay + (moatEdge ? MoatDelayPenalty : 0L);
            return 8L * (delay + SpeedBonus + ExtraDelay + 1L);
        }

        public long ConvertFixedCostToTicks(long fixedCost)
        {
            if (fixedCost < 0 || CadenceProgress <= 0)
                return long.MaxValue;
            if (fixedCost == 0)
                return 0;
            return fixedCost > long.MaxValue - (CadenceProgress - 1L)
                ? long.MaxValue
                : (fixedCost + CadenceProgress - 1L) / CadenceProgress;
        }

        public long EstimateRouteTicks(int groundEdges, int moatEdges)
        {
            if (groundEdges < 0 || moatEdges < 0)
                return long.MaxValue;
            long groundCost = GetEdgeFixedCost(false);
            long moatCost = GetEdgeFixedCost(true);
            if ((groundEdges != 0 && groundCost > long.MaxValue / groundEdges) ||
                (moatEdges != 0 && moatCost > long.MaxValue / moatEdges))
            {
                return long.MaxValue;
            }
            long fixedCost = groundCost * groundEdges;
            long moatFixedCost = moatCost * moatEdges;
            if (fixedCost > long.MaxValue - moatFixedCost)
                return long.MaxValue;
            return ConvertFixedCostToTicks(fixedCost + moatFixedCost);
        }

        public bool TryWithSpeedBonus(
            int speedBonus,
            out WeightedMovementCostProfile profile,
            out string rejectionReason)
        {
            profile = default;
            rejectionReason = null;
            if (speedBonus < 0 || speedBonus > short.MaxValue ||
                speedBonus > int.MaxValue - AdditionalSubsteps - 1)
            {
                rejectionReason = "invalid-resolved-speed-bonus";
                return false;
            }

            int cadenceProgress = speedBonus + AdditionalSubsteps + 1;
            long groundInterval = (long)NormalizedDelay + speedBonus + ExtraDelay + 1L;
            long moatInterval = groundInterval + MoatDelayPenalty;
            if (cadenceProgress <= 0 || groundInterval <= 0 || moatInterval <= 0 ||
                moatInterval > long.MaxValue / 8L)
            {
                rejectionReason = "resolved-speed-profile-overflow";
                return false;
            }

            profile = new WeightedMovementCostProfile(
                CurrentSpeed,
                CurrentSpeed2,
                speedBonus,
                AdditionalSubsteps,
                ExtraDelay,
                MoatPhase,
                CurrentTerrainPenalty,
                NormalizedDelay,
                cadenceProgress,
                StartedOnCompletedMoat);
            return true;
        }

        public bool HasSameNormalizedCadence(WeightedMovementCostProfile other) =>
            SpeedBonus == other.SpeedBonus &&
            AdditionalSubsteps == other.AdditionalSubsteps &&
            ExtraDelay == other.ExtraDelay &&
            NormalizedDelay == other.NormalizedDelay &&
            CadenceProgress == other.CadenceProgress;

        public bool HasSameBaseCadenceExceptSpeedBonus(WeightedMovementCostProfile other) =>
            AdditionalSubsteps == other.AdditionalSubsteps &&
            ExtraDelay == other.ExtraDelay &&
            NormalizedDelay == other.NormalizedDelay;

        public bool Equals(WeightedMovementCostProfile other) =>
            CurrentSpeed == other.CurrentSpeed &&
            CurrentSpeed2 == other.CurrentSpeed2 &&
            SpeedBonus == other.SpeedBonus &&
            AdditionalSubsteps == other.AdditionalSubsteps &&
            ExtraDelay == other.ExtraDelay &&
            MoatPhase == other.MoatPhase &&
            CurrentTerrainPenalty == other.CurrentTerrainPenalty &&
            NormalizedDelay == other.NormalizedDelay &&
            CadenceProgress == other.CadenceProgress &&
            StartedOnCompletedMoat == other.StartedOnCompletedMoat;

        public override bool Equals(object obj) =>
            obj is WeightedMovementCostProfile other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = CurrentSpeed;
                hash = hash * 397 ^ CurrentSpeed2;
                hash = hash * 397 ^ SpeedBonus;
                hash = hash * 397 ^ AdditionalSubsteps;
                hash = hash * 397 ^ ExtraDelay;
                hash = hash * 397 ^ MoatPhase;
                hash = hash * 397 ^ CurrentTerrainPenalty;
                hash = hash * 397 ^ NormalizedDelay;
                hash = hash * 397 ^ CadenceProgress;
                return hash * 397 ^ StartedOnCompletedMoat.GetHashCode();
            }
        }
    }

    internal sealed unsafe class WeightedMoatRoutePlanner
    {
        internal int LastRejectedEdge = -1;
        internal int rejectedFrom, rejectedTo, rejectedDirection, rejectedPlayer;
        internal string edgeRejectionRule, rejectedRule;
        internal bool RejectTraversal(string rule) { edgeRejectionRule = rule; return false; }
        internal string DescribeLastRejectedEdge() => LastRejectedEdge < 0 ? "none" :
            $"edge={LastRejectedEdge},tiles={rejectedFrom}->{rejectedTo},direction={rejectedDirection}," +
            $"flags=0x{tileFlags[rejectedFrom]:X}/0x{tileFlags[rejectedTo]:X}," +
            $"masks=0x{occupancyLayer[rejectedFrom]:X}/0x{occupancyLayer[rejectedTo]:X}," +
            $"heights={heightLayer[rejectedFrom]}/{heightLayer[rejectedTo]},rule={rejectedRule},player={rejectedPlayer}," +
            $"owners={(IsCompletedMoat(rejectedFrom) ? resolveCompletedMoatRelationship(rejectedPlayer,rejectedFrom).ToString() : "ground")}/" +
            $"{(IsCompletedMoat(rejectedTo) ? resolveCompletedMoatRelationship(rejectedPlayer,rejectedTo).ToString() : "ground")}";

        // DBC60 records are three ints; preserve the native attack flag and score.
        // Validate every record before changing the shared output (including exceptions).
        internal static int FilterNativeAttackCandidates(int* records, int capacity, Func<int, bool> forbidden)
        {
            if (records == null || capacity <= 0 || capacity > 500) return 0;
            byte* rejected = stackalloc byte[500];
            int count = 0, removed = 0;
            for (; count < capacity; count++)
            {
                int tile = records[count * 3], flag = records[count * 3 + 1];
                if (tile == 0 && flag == 0) break;
                rejected[count] = forbidden(tile) ? (byte)1 : (byte)0;
                removed += rejected[count];
            }
            if (removed == 0) return 0;
            int write = 0;
            for (int read = 0; read < count; read++)
            {
                if (rejected[read] != 0) continue;
                for (int field = 0; field < 3; field++) records[write * 3 + field] = records[read * 3 + field];
                write++;
            }
            for (int i = write * 3; i < capacity * 3; i++) records[i] = 0;
            return removed;
        }
        internal const int MaximumRouteEdges = 2000;
        internal const ulong RouteFingerprintOffsetBasis = 14695981039346656037UL;
        internal const ulong RouteFingerprintPrime = 1099511628211UL;

        internal const int MapWidth = 800;
        internal const int CoordinateCount = MapWidth * MapWidth;
        internal const int NativeTileCount = 0x4E520;
        internal const uint CompletedMoatTileFlag = 0x40000000;
        internal const uint WallOrStairMask = 0x00010900;
        internal const uint NativeMoatAdjacentAlwaysAllowedMask = 0x40000800;
        internal const uint NativeMoatAdjacentImmediateBlockMask = 0x00000100;
        internal const uint NativeSpecialStructureTileFlag = 0x00001000;
        internal const uint MoatReconstructionBlockingMask = 0x0A5014B1;
        internal const uint DynamicStructureHeightTileFlag = 0x10000000;
        internal const int NativeBuildingRecordStride = 0x32C;
        internal const int MaximumNativeBuildingId = 10000;
        internal const ushort NativeLowWallType = 0x2D;
        internal const ushort NativeHighWallType = 0x2E;
        internal const int NativeWallHeightCorrection = 0x5A;

        internal static readonly int[] DirectionX = { 0, 1, 1, 1, 0, -1, -1, -1 };
        internal static readonly int[] DirectionY = { -1, -1, 0, 1, 1, 1, 0, -1 };

        internal readonly int* rowLookup;
        internal Func<bool> AllowAdditionalMoatEntry;
        internal string PublisherGuid { get; set; }
        internal readonly uint* tileFlags;
        internal readonly ushort* buildingLayer;
        internal readonly byte* heightLayer;
        internal readonly byte* occupancyLayer;
        internal readonly byte* directionMasks;
        internal readonly byte* nativeBuildingTypeBias;
        internal readonly CompletedMoatRelationshipResolver resolveCompletedMoatRelationship;
        internal readonly NativeSpecialStructureResolver resolveSpecialStructure;

        internal readonly byte[] moatClassification = new byte[NativeTileCount];
        internal readonly int[] classifiedTiles = new int[NativeTileCount];
        internal readonly byte[] specialStructureClassification = new byte[NativeTileCount];
        internal readonly int[] classifiedSpecialStructureTiles = new int[NativeTileCount];

        internal int classifiedCount;
        internal int classifiedSpecialStructureCount;
        public WeightedMoatRoutePlanner(
            int* rowLookup,
            uint* tileFlags,
            ushort* buildingLayer,
            byte* heightLayer,
            byte* occupancyLayer,
            byte* directionMasks,
            byte* nativeBuildingTypeBias,
            CompletedMoatRelationshipResolver resolveCompletedMoatRelationship,
            NativeSpecialStructureResolver resolveSpecialStructure)
        {
            this.rowLookup = rowLookup;
            this.tileFlags = tileFlags;
            this.buildingLayer = buildingLayer;
            this.heightLayer = heightLayer;
            this.occupancyLayer = occupancyLayer;
            this.directionMasks = directionMasks;
            this.nativeBuildingTypeBias = nativeBuildingTypeBias;
            this.resolveCompletedMoatRelationship = resolveCompletedMoatRelationship ??
                throw new ArgumentNullException(nameof(resolveCompletedMoatRelationship));
            this.resolveSpecialStructure = resolveSpecialStructure ??
                throw new ArgumentNullException(nameof(resolveSpecialStructure));

        }

        public bool TryBuild(
            int playerId,
            int startX,
            int startY,
            int requestedTargetX,
            int requestedTargetY,
            WeightedMovementCostProfile costProfile,
            bool allowReservedTarget,
            out WeightedMoatRouteSummary summary)
        {
            return TryBuildCore(
                playerId, startX, startY, requestedTargetX, requestedTargetY,
                costProfile, allowReservedTarget, captureEncodedRoute: false,
                MoatTraversalPolicy.FriendlyOnly,
                out summary, out _);
        }

        public bool TryBuildEncoded(
            int playerId,
            int startX,
            int startY,
            int requestedTargetX,
            int requestedTargetY,
            WeightedMovementCostProfile costProfile,
            bool allowReservedTarget,
            out WeightedMoatRouteSummary summary,
            out WeightedMoatEncodedRoute encodedRoute)
        {
            return TryBuildCore(
                playerId, startX, startY, requestedTargetX, requestedTargetY,
                costProfile, allowReservedTarget, captureEncodedRoute: true,
                MoatTraversalPolicy.FriendlyOnly,
                out summary, out encodedRoute);
        }

        public bool TryProbeReachability(
            int playerId,
            int startX,
            int startY,
            int requestedTargetX,
            int requestedTargetY,
            bool allowReservedTarget,
            MoatTraversalPolicy policy,
            out WeightedMoatRouteSummary summary,
            int maximumExpanded = int.MaxValue)
        {
            if (!WeightedMovementCostProfile.TryCreate(
                    1, 1, 0, 0, 0, 0, false,
                    out WeightedMovementCostProfile reachabilityProfile, out _))
            {
                summary = WeightedMoatRouteSummary.Failed("invalid-reachability-profile", 0);
                return false;
            }

            return TryBuildCore(
                playerId, startX, startY, requestedTargetX, requestedTargetY,
                reachabilityProfile, allowReservedTarget, captureEncodedRoute: false,
                policy, out summary, out _, reachability: true,
                maximumExpanded: maximumExpanded);
        }

        public bool TryBuildReachabilityEncoded(
            int playerId,
            int startX,
            int startY,
            int requestedTargetX,
            int requestedTargetY,
            bool allowReservedTarget,
            out WeightedMoatRouteSummary summary,
            out WeightedMoatEncodedRoute encodedRoute,
            int maximumExpanded = int.MaxValue)
        {
            if (!WeightedMovementCostProfile.TryCreate(
                    1, 1, 0, 0, 0, 0, false,
                    out WeightedMovementCostProfile reachabilityProfile, out _))
            {
                summary = WeightedMoatRouteSummary.Failed("invalid-reachability-profile", 0);
                encodedRoute = default;
                return false;
            }

            return TryBuildCore(
                playerId, startX, startY, requestedTargetX, requestedTargetY,
                reachabilityProfile, allowReservedTarget, captureEncodedRoute: true,
                MoatTraversalPolicy.FriendlyOnly, out summary, out encodedRoute,
                reachability: true, maximumExpanded: maximumExpanded);
        }

        internal Func<int, int, MoatSearchEdge, IMoatSearchKernel> KernelFactory =
            (width, height, edge) => new WeightedGridSearchKernel(width, height, edge);
        internal readonly IMoatSearchKernel[] searchKernels = new IMoatSearchKernel[6];
        internal object searchSession;
        internal int searchPlayer = -1;
        internal int searchEpoch = -1;
        internal long searchTick = -1;
        internal int kernelPlayer;
        internal void SetSearchSession(object session, int player, int epoch, long tick)
        {
            if (session != null && ReferenceEquals(session, searchSession) &&
                player == searchPlayer && epoch == searchEpoch && tick == searchTick) return;
            searchSession = session; searchPlayer = player; searchEpoch = epoch; searchTick = tick;
            foreach (IMoatSearchKernel kernel in searchKernels) kernel?.Invalidate();
            ResetClassifications();
        }
        internal long SearchNodes
        {
            get { long total = 0; foreach (IMoatSearchKernel k in searchKernels) if (k != null) total += k.Expanded; return total; }
        }
        internal int CachedSearchFields
        {
            get { int count=0; foreach (IMoatSearchKernel kernel in searchKernels) if(kernel!=null)count+=kernel.CachedFields; return count; }
        }
        internal long SearchRuns
        {
            get { long total = 0; foreach (IMoatSearchKernel k in searchKernels) if (k != null) total += k.Searches; return total; }
        }
        internal long CachedFieldHits
        {
            get { long total = 0; foreach (IMoatSearchKernel k in searchKernels) if (k != null) total += k.FieldHits; return total; }
        }
        internal IMoatSearchKernel GetSearchKernel(MoatTraversalPolicy policy, bool reachability)
        {
            int index = (int)policy + (reachability ? 3 : 0);
            if (searchKernels[index] == null)
                searchKernels[index] = KernelFactory?.Invoke(MapWidth, MapWidth,
                    (int from, int to, int direction, out bool moat, out bool structure) =>
                        TryGetEdge(kernelPlayer, from % MapWidth, from / MapWidth,
                            GetTileId(from % MapWidth, from / MapWidth), to % MapWidth, to / MapWidth,
                            GetTileId(to % MapWidth, to / MapWidth), direction, false, false, policy,
                            out moat, out structure));
            return searchKernels[index];
        }

        internal bool TryBuildImprovement(int playerId, int startX, int startY, int endX, int endY,
            WeightedMovementCostProfile profile, bool reservedTarget, MoatSearchLimit[] limits,
            out WeightedMoatRouteSummary summary, out WeightedMoatEncodedRoute route,
            bool requireMoat = true, int maximumEdges = MaximumRouteEdges)
        {
            return TryBuildCore(playerId, startX, startY, endX, endY, profile, reservedTarget, true,
                MoatTraversalPolicy.FriendlyOnly, out summary, out route, limits, true,
                requireMoat: requireMoat, maximumEdges: maximumEdges);
        }

        internal bool TryBuildCore(
            int playerId, int startX, int startY, int requestedTargetX, int requestedTargetY,
            WeightedMovementCostProfile costProfile, bool allowReservedTarget, bool captureEncodedRoute,
            MoatTraversalPolicy traversalPolicy, out WeightedMoatRouteSummary summary,
            out WeightedMoatEncodedRoute encodedRoute, MoatSearchLimit[] limits = null, bool improvement = false, bool reachability = false,
            bool requireMoat = true, int maximumEdges = MaximumRouteEdges,
            int maximumExpanded = int.MaxValue)
        {
            // These events describe the additional preference search, not Vanilla orders
            // or topological reachability queries already supplied by Script Extender.
            APIShared.Pathfinding.RouteSearchEventRegistry.Invocation invocation = null;
            if (!reachability && !string.IsNullOrWhiteSpace(PublisherGuid) &&
                APIShared.Pathfinding.RouteSearchEvents.Registry.HasObservers)
            {
                int edgeLimit = captureEncodedRoute || maximumExpanded != int.MaxValue
                    ? Math.Min(MaximumRouteEdges, maximumEdges) : int.MaxValue;
                var terrain = traversalPolicy == MoatTraversalPolicy.GroundOnly
                    ? APIShared.Pathfinding.RouteSearchTerrain.GroundOnly
                    : traversalPolicy == MoatTraversalPolicy.FriendlyOnly
                        ? APIShared.Pathfinding.RouteSearchTerrain.FriendlyMoat
                        : APIShared.Pathfinding.RouteSearchTerrain.DiagnosticMoat;
                var context = new APIShared.Pathfinding.RouteSearchContext(PublisherGuid, playerId,
                    startX, startY, requestedTargetX, requestedTargetY, terrain, edgeLimit,
                    long.MaxValue / ((long)MapWidth * MapWidth) / 8L);
                invocation = APIShared.Pathfinding.RouteSearchEvents.Registry.Begin(context,
                    costProfile.GetEdgeFixedCost(false), costProfile.GetEdgeFixedCost(true));
                if (invocation?.Pre.SkipOriginalFunction == true)
                {
                    summary = WeightedMoatRouteSummary.Failed("route-search-skipped", 0);
                    encodedRoute = default;
                    return false;
                }
            }
            bool success = TryBuildCoreWithoutEvents(playerId, startX, startY, requestedTargetX, requestedTargetY,
                costProfile, allowReservedTarget, captureEncodedRoute, traversalPolicy, out summary, out encodedRoute,
                limits, improvement, reachability, requireMoat, maximumEdges, maximumExpanded,
                invocation?.Pre.GroundEdgeCost, invocation?.Pre.MoatEdgeCost);
            invocation?.Complete(success, summary.Reason, success ? summary.RouteLength : 0, summary.ExpandedNodes);
            return success;
        }

        private bool TryBuildCoreWithoutEvents(
            int playerId, int startX, int startY, int requestedTargetX, int requestedTargetY,
            WeightedMovementCostProfile costProfile, bool allowReservedTarget, bool captureEncodedRoute,
            MoatTraversalPolicy traversalPolicy, out WeightedMoatRouteSummary summary,
            out WeightedMoatEncodedRoute encodedRoute, MoatSearchLimit[] limits = null, bool improvement = false, bool reachability = false,
            bool requireMoat = true, int maximumEdges = MaximumRouteEdges,
            int maximumExpanded = int.MaxValue, long? groundPreferenceCost = null, long? moatPreferenceCost = null)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            summary = default; encodedRoute = default;
            if (searchSession == null || searchPlayer != playerId)
                SetSearchSession(null, playerId, searchEpoch, searchTick);
            kernelPlayer = playerId;
            if (!IsValidCoordinate(startX, startY) || !IsValidCoordinate(requestedTargetX, requestedTargetY))
            { summary = WeightedMoatRouteSummary.Failed("invalid-coordinate", 0); return false; }
            int startTile = GetTileId(startX, startY), endTile = GetTileId(requestedTargetX, requestedTargetY);
            if ((traversalPolicy == MoatTraversalPolicy.GroundOnly && (IsCompletedMoat(startTile) || IsCompletedMoat(endTile))) ||
                (IsCompletedMoat(startTile) && !IsEndpointRelationshipAllowed(GetMoatRelationship(playerId, startTile), traversalPolicy)) ||
                (IsCompletedMoat(endTile) && !IsEndpointRelationshipAllowed(GetMoatRelationship(playerId, endTile), traversalPolicy)))
            { summary = WeightedMoatRouteSummary.Failed("enemy-or-invalid-moat-endpoint", 0); return false; }
            IMoatSearchKernel kernel = GetSearchKernel(traversalPolicy, reachability);
            if (kernel == null) { summary = WeightedMoatRouteSummary.Failed("traversal-provider-disabled", 0); return false; }
            long before = kernel.Expanded;
            // A topological answer is independent of the native 1000-byte output buffer.
            int maxEdges = captureEncodedRoute || maximumExpanded != int.MaxValue
                ? Math.Min(MaximumRouteEdges, maximumEdges)
                : int.MaxValue;
            bool found = kernel.Search(GetNode(startX, startY), GetNode(requestedTargetX, requestedTargetY),
                reachability ? 1 : groundPreferenceCost ?? costProfile.GetEdgeFixedCost(false),
                reachability ? 1 : moatPreferenceCost ?? costProfile.GetEdgeFixedCost(true), maxEdges,
                improvement && requireMoat, improvement, limits, searchSession != null,
                out int[] nodes, maximumExpanded);
            int expanded = (int)Math.Min(int.MaxValue, kernel.Expanded - before);
            if (!found)
            {
                summary = WeightedMoatRouteSummary.Failed(
                    kernel.LastSearchBudgetExceeded ? "search-budget-exceeded" :
                    improvement ? "no-publishable-improvement" : captureEncodedRoute ? "no-encodable-route" : "unreachable",
                    stopwatch.Elapsed.TotalMilliseconds, expanded);
                return false;
            }
            int ground = 0, moatEdges = 0, structures = 0, diagonal = 0, changes = 0, previous = -1;
            ulong fingerprint = RouteFingerprintOffsetBasis;
            byte[] bytes = captureEncodedRoute ? new byte[nodes.Length / 2] : null;
            for (int i = 1; i < nodes.Length; i++)
            {
                int from = nodes[i - 1], to = nodes[i];
                int direction = kernel.Direction(from, to);
                if (!TryGetEdge(playerId, from % MapWidth, from / MapWidth, GetTileId(from % MapWidth, from / MapWidth),
                    to % MapWidth, to / MapWidth, GetTileId(to % MapWidth, to / MapWidth), direction,
                    i == nodes.Length - 1, allowReservedTarget, traversalPolicy, out bool wet, out bool structure))
                { summary = WeightedMoatRouteSummary.Failed("live-edge-changed", stopwatch.Elapsed.TotalMilliseconds, expanded); return false; }
                if (wet) moatEdges++; else ground++;
                if (structure) structures++;
                if ((direction & 1) != 0) diagonal++;
                if (previous >= 0 && previous != direction) changes++;
                previous = direction; fingerprint = UpdateRouteFingerprint(fingerprint, direction);
                if (bytes != null) bytes[(i - 1) >> 1] |= (byte)(direction << (((i - 1) & 1) * 4));
            }
            summary = WeightedMoatRouteSummary.Succeeded(nodes.Length - 1, ground, moatEdges, structures, diagonal,
                changes, fingerprint, costProfile.EstimateRouteTicks(ground, moatEdges), stopwatch.Elapsed.TotalMilliseconds, expanded);
            if (bytes != null) encodedRoute = new WeightedMoatEncodedRoute(bytes, nodes.Length - 1);
            return true;
        }
        public bool TryDescribeEncodedPath(
            int playerId,
            int startX,
            int startY,
            int expectedTargetX,
            int expectedTargetY,
            WeightedMovementCostProfile costProfile,
            byte* encodedDirections,
            int directionCount,
            bool allowReservedTarget,
            out WeightedMoatRouteSummary summary,
            int terminalFillTileId = -1, bool comparisonOnly = false)
        {
            summary = default;
            ResetClassifications();
            LastRejectedEdge = -1;
            try
            {
                if (encodedDirections == null || directionCount < 0 ||
                    directionCount > MaximumRouteEdges || !IsValidCoordinate(startX, startY))
                {
                    summary = WeightedMoatRouteSummary.Failed("invalid-native-path", 0);
                    return false;
                }

                int x = startX;
                int y = startY;
                int ground = 0;
                int moat = 0;
                int structure = 0;
                int diagonal = 0;
                int directionChanges = 0;
                int previousDirection = -1;
                ulong fingerprint = RouteFingerprintOffsetBasis;
                long fixedCost = 0;
                for (int index = 0; index < directionCount; index++)
                {
                    // 0xE1640 first records target -> start, then 0xE4E90 reverses the
                    // complete nibble sequence. The published buffer is start -> target.
                    int bufferIndex = index;
                    int direction =
                        (encodedDirections[bufferIndex >> 1] >> ((bufferIndex & 1) * 4)) & 0x0F;
                    if (direction >= DirectionX.Length)
                    {
                        summary = WeightedMoatRouteSummary.Failed("invalid-direction-nibble", 0);
                        return false;
                    }
                    int nextX = x + DirectionX[direction];
                    int nextY = y + DirectionY[direction];
                    if (!IsValidCoordinate(nextX, nextY))
                    {
                        summary = WeightedMoatRouteSummary.Failed("native-path-out-of-bounds", 0);
                        return false;
                    }
                    int currentTile = GetTileId(x, y);
                    int nextTile = GetTileId(nextX, nextY);
                    if (!IsNativeTile(currentTile) || !IsNativeTile(nextTile))
                    { summary = WeightedMoatRouteSummary.Failed("invalid-native-tile", 0); return false; }
                    bool targetEndpoint = index == directionCount - 1;
                    bool contactEdge = false;
                    if (terminalFillTileId >= 0)
                    {
                        bool currentEnemy = IsCompletedMoat(currentTile) &&
                            GetMoatRelationship(playerId, currentTile) == CompletedMoatRelationship.Enemy;
                        bool nextEnemy = IsCompletedMoat(nextTile) &&
                            GetMoatRelationship(playerId, nextTile) == CompletedMoatRelationship.Enemy;
                        if ((currentEnemy && !IsTerminalFillNode(currentTile, index, directionCount, terminalFillTileId)) ||
                            (nextEnemy && !IsTerminalFillNode(nextTile, index + 1, directionCount, terminalFillTileId)))
                        { summary = WeightedMoatRouteSummary.Failed("unbound-fill-contact", 0); return false; }
                        contactEdge = currentEnemy || nextEnemy;
                        // The terminal exception must not widen diagonal corner traversal.
                        if (!comparisonOnly && contactEdge && (direction & 1) != 0)
                        {
                            int a = GetTileId(nextX, y), b = GetTileId(x, nextY);
                            if (!IsNativeTile(a) || !IsNativeTile(b) ||
                                (IsCompletedMoat(a) && GetMoatRelationship(playerId, a) != CompletedMoatRelationship.Friendly) ||
                                (IsCompletedMoat(b) && GetMoatRelationship(playerId, b) != CompletedMoatRelationship.Friendly))
                            { summary = WeightedMoatRouteSummary.Failed("fill-diagonal-corner", 0); return false; }
                        }
                    }
                    bool edgeValid = TryGetEdge(
                        playerId, x, y, currentTile, nextX, nextY, nextTile,
                        direction, targetEndpoint, allowReservedTarget,
                        contactEdge ? MoatTraversalPolicy.AllowEnemyForDiagnostic : MoatTraversalPolicy.FriendlyOnly,
                        out bool moatEdge, out bool structuralEdge);
                    if (!edgeValid && LastRejectedEdge < 0)
                    { LastRejectedEdge = index; rejectedFrom = currentTile; rejectedTo = nextTile;
                        rejectedDirection = direction; rejectedPlayer = playerId; rejectedRule = edgeRejectionRule; }
                    if (comparisonOnly)
                    {
                        // Pricing an existing native buffer is not permission to publish it.
                        // Only calibrated ground/friendly moat (or the bound work contact)
                        // can supply a comparison bound. All replacements use the strict mode.
                        structuralEdge = IsStructuralTile(currentTile) || IsStructuralTile(nextTile);
                        moatEdge = IsCompletedMoat(currentTile) || IsCompletedMoat(nextTile);
                        bool forbidden = (IsCompletedMoat(currentTile) &&
                            GetMoatRelationship(playerId, currentTile) != CompletedMoatRelationship.Friendly &&
                            !(GetMoatRelationship(playerId, currentTile) == CompletedMoatRelationship.Enemy &&
                              IsTerminalFillNode(currentTile, index, directionCount, terminalFillTileId))) ||
                            (IsCompletedMoat(nextTile) &&
                            GetMoatRelationship(playerId, nextTile) != CompletedMoatRelationship.Friendly &&
                            !(GetMoatRelationship(playerId, nextTile) == CompletedMoatRelationship.Enemy &&
                              IsTerminalFillNode(nextTile, index + 1, directionCount, terminalFillTileId)));
                        if (forbidden || structuralEdge || !HasCompatibleNativeHeight(currentTile, nextTile))
                        { summary = WeightedMoatRouteSummary.Failed("native-cost-unclassified", 0); return false; }
                    }
                    else if (!edgeValid)
                    {
                        summary = WeightedMoatRouteSummary.Failed("native-edge-invalid", 0);
                        return false;
                    }

                    long edgeFixedCost = costProfile.GetEdgeFixedCost(moatEdge);
                    fixedCost = fixedCost > long.MaxValue - edgeFixedCost
                        ? long.MaxValue
                        : fixedCost + edgeFixedCost;
                    if (moatEdge)
                        moat++;
                    else
                        ground++;
                    if (structuralEdge)
                        structure++;
                    if ((direction & 1) != 0)
                        diagonal++;
                    if (previousDirection >= 0 && previousDirection != direction)
                        directionChanges++;
                    previousDirection = direction;
                    fingerprint = UpdateRouteFingerprint(fingerprint, direction);
                    x = nextX;
                    y = nextY;
                }

                if (x != expectedTargetX || y != expectedTargetY)
                {
                    summary = WeightedMoatRouteSummary.Failed(
                        $"native-endpoint-mismatch-actual-{x}-{y}", 0,
                        routeLength: directionCount);
                    return false;
                }

                summary = WeightedMoatRouteSummary.Succeeded(
                    directionCount, ground, moat, structure, diagonal,
                    directionChanges, fingerprint,
                    costProfile.ConvertFixedCostToTicks(fixedCost), 0, 0);
                return true;
            }
            finally
            {
                ResetClassifications();
            }
        }

        internal static bool IsTerminalFillNode(int tile, int node, int length, int workTile) =>
            workTile >= 0 && tile == workTile && length >= 2 && node == length - 1;

        internal bool TryGetEdge(
            int playerId,
            int currentX,
            int currentY,
            int currentTile,
            int nextX,
            int nextY,
            int nextTile,
            int direction,
            bool targetEndpoint,
            bool allowReservedTarget,
            MoatTraversalPolicy traversalPolicy,
            out bool moatEdge,
            out bool structuralEdge)
        {
            bool traversable = TryGetTraversalEdge(
                playerId,
                currentX,
                currentY,
                currentTile,
                nextX,
                nextY,
                nextTile,
                direction,
                targetEndpoint,
                allowReservedTarget,
                traversalPolicy,
                out MoatTraversalEdgeKind edgeKind,
                out structuralEdge);
            moatEdge = edgeKind != MoatTraversalEdgeKind.Ground;
            return traversable;
        }

        internal bool TryGetTraversalEdge(
            int playerId,
            int currentX,
            int currentY,
            int currentTile,
            int nextX,
            int nextY,
            int nextTile,
            int direction,
            bool targetEndpoint,
            bool allowReservedTarget,
            MoatTraversalPolicy policy,
            out MoatTraversalEdgeKind edgeKind,
            out bool structuralEdge)
        {
            edgeKind = MoatTraversalEdgeKind.Ground;
            structuralEdge = false;
            edgeRejectionRule = "none";
            if (direction < 0 || direction >= DirectionX.Length ||
                currentX < 0 || currentX >= MapWidth ||
                currentY < 0 || currentY >= MapWidth ||
                nextX < 0 || nextX >= MapWidth ||
                nextY < 0 || nextY >= MapWidth ||
                !IsNativeTile(currentTile) || !IsNativeTile(nextTile))
                return RejectTraversal("coordinate-or-tile");

            IEnemyGatePathPolicy gatePolicy = EnemyGatePathPolicyBridge.Current;
            if (gatePolicy != null && gatePolicy.HasPublishedMask &&
                !gatePolicy.IsDirectionAllowed(playerId, currentTile, direction))
                return RejectTraversal("enemy-gate-policy");

            bool currentMoat = IsCompletedMoat(currentTile);
            bool nextMoat = IsCompletedMoat(nextTile);
            if (!currentMoat && nextMoat && policy != MoatTraversalPolicy.AllowEnemyForDiagnostic &&
                AllowAdditionalMoatEntry?.Invoke() != true)
                return RejectTraversal("additional-moat-entry-disabled");
            CompletedMoatRelationship currentRelationship = currentMoat
                ? GetMoatRelationship(playerId, currentTile)
                : CompletedMoatRelationship.Friendly;
            CompletedMoatRelationship nextRelationship = nextMoat
                ? GetMoatRelationship(playerId, nextTile)
                : CompletedMoatRelationship.Friendly;
            if (currentRelationship == CompletedMoatRelationship.Invalid ||
                nextRelationship == CompletedMoatRelationship.Invalid)
            {
                return RejectTraversal("invalid-owner");
            }
            bool enemyMoat =
                currentRelationship == CompletedMoatRelationship.Enemy ||
                nextRelationship == CompletedMoatRelationship.Enemy;
            if ((currentMoat || nextMoat) && policy == MoatTraversalPolicy.GroundOnly)
                return RejectTraversal("ground-only");
            if (enemyMoat && policy == MoatTraversalPolicy.FriendlyOnly)
                return RejectTraversal("enemy-node");

            bool ordinaryEdge = (directionMasks[direction] & occupancyLayer[currentTile]) != 0;
            if ((direction & 1) != 0 && policy == MoatTraversalPolicy.FriendlyOnly)
            {
                int first = GetTileId(nextX, currentY), second = GetTileId(currentX, nextY);
                if (!IsNativeTile(first) || !IsNativeTile(second) ||
                    (IsCompletedMoat(first) && GetMoatRelationship(playerId, first) != CompletedMoatRelationship.Friendly) ||
                    (IsCompletedMoat(second) && GetMoatRelationship(playerId, second) != CompletedMoatRelationship.Friendly))
                    return RejectTraversal("diagonal-corner-owner");
            }
            structuralEdge = IsStructuralTile(currentTile) || IsStructuralTile(nextTile);
            if (!currentMoat && !nextMoat)
            {
                // DAFD0 lets the native direction mask decide ordinary ground, stair,
                // ramp, wall-top and walkable-reservation edges. Structure presence alone
                // is not a blocker; the height correction below is part of that same edge.
                return ordinaryEdge ? (HasCompatibleNativeHeight(currentTile, nextTile) || RejectTraversal("height"))
                    : RejectTraversal("ground-direction-mask");
            }

            edgeKind = enemyMoat
                ? MoatTraversalEdgeKind.EnemyMoat
                : MoatTraversalEdgeKind.FriendlyMoat;
            if (ordinaryEdge)
            {
                return HasCompatibleNativeHeight(currentTile, nextTile) || RejectTraversal("height");
            }
            if (!HasCompatibleNativeHeight(currentTile, nextTile))
                return RejectTraversal("height");
            if (!IsNativeMoatAdjacentTile(currentTile) ||
                !IsNativeMoatAdjacentTile(nextTile))
            {
                return RejectTraversal("moat-adjacent-flags");
            }
            bool enemyOnlyCorner = false;
            if ((direction & 1) != 0 &&
                !IsValidMoatDiagonal(
                    playerId, currentX, currentY, currentTile,
                    nextX, nextY, nextTile, policy, out enemyOnlyCorner))
            {
                return RejectTraversal("moat-diagonal-transition");
            }
            if ((direction & 1) != 0 && enemyOnlyCorner)
                edgeKind = MoatTraversalEdgeKind.EnemyMoat;
            return true;
        }

        internal bool IsValidMoatDiagonal(
            int playerId,
            int currentX,
            int currentY,
            int currentTile,
            int nextX,
            int nextY,
            int nextTile,
            MoatTraversalPolicy policy,
            out bool enemyOnlyCorner)
        {
            enemyOnlyCorner = false;
            int firstTile = GetTileId(nextX, currentY);
            int secondTile = GetTileId(currentX, nextY);
            if (!IsValidCoordinate(nextX, currentY) ||
                !IsValidCoordinate(currentX, nextY))
            {
                return false;
            }

            // DAFD0 accepts either orthogonal corner for a diagonal transition. When
            // entering a moat from ground that corner must itself continue the completed
            // moat; when leaving/crossing a moat the ordinary native corner predicate and
            // its height check are used instead.
            if (!IsCompletedMoat(currentTile) && IsCompletedMoat(nextTile))
            {
                if (IsAllowedCompletedMoatCorner(
                        playerId, firstTile, MoatTraversalPolicy.FriendlyOnly) ||
                    IsAllowedCompletedMoatCorner(
                        playerId, secondTile, MoatTraversalPolicy.FriendlyOnly))
                {
                    return true;
                }
                enemyOnlyCorner = policy == MoatTraversalPolicy.AllowEnemyForDiagnostic &&
                    (IsAllowedCompletedMoatCorner(playerId, firstTile, policy) ||
                     IsAllowedCompletedMoatCorner(playerId, secondTile, policy));
                return enemyOnlyCorner;
            }
            if (IsDiagonalCornerUsable(
                    playerId, currentTile, firstTile, MoatTraversalPolicy.FriendlyOnly) ||
                IsDiagonalCornerUsable(
                    playerId, currentTile, secondTile, MoatTraversalPolicy.FriendlyOnly))
            {
                return true;
            }
            enemyOnlyCorner = policy == MoatTraversalPolicy.AllowEnemyForDiagnostic &&
                (IsDiagonalCornerUsable(playerId, currentTile, firstTile, policy) ||
                 IsDiagonalCornerUsable(playerId, currentTile, secondTile, policy));
            return enemyOnlyCorner;
        }

        internal bool IsDiagonalCornerUsable(
            int playerId, int currentTile, int tileId, MoatTraversalPolicy policy)
        {
            if (!IsNativeTile(tileId))
                return false;
            if (IsCompletedMoat(tileId))
            {
                CompletedMoatRelationship relationship =
                    GetMoatRelationship(playerId, tileId);
                if (relationship != CompletedMoatRelationship.Friendly &&
                    !(relationship == CompletedMoatRelationship.Enemy &&
                      policy == MoatTraversalPolicy.AllowEnemyForDiagnostic))
                {
                    return false;
                }
                return HasCompatibleNativeHeight(currentTile, tileId);
            }
            return IsNativeMoatAdjacentTile(tileId) &&
                HasCompatibleNativeHeight(currentTile, tileId);
        }

        internal bool IsAllowedCompletedMoatCorner(
            int playerId, int tileId, MoatTraversalPolicy policy)
        {
            if (!IsNativeTile(tileId) || !IsCompletedMoat(tileId))
                return false;
            CompletedMoatRelationship relationship = GetMoatRelationship(playerId, tileId);
            return relationship == CompletedMoatRelationship.Friendly ||
                relationship == CompletedMoatRelationship.Enemy &&
                policy == MoatTraversalPolicy.AllowEnemyForDiagnostic;
        }

        internal static bool IsEndpointRelationshipAllowed(
            CompletedMoatRelationship relationship, MoatTraversalPolicy policy)
        {
            if (relationship == CompletedMoatRelationship.Invalid)
                return false;
            if (policy == MoatTraversalPolicy.GroundOnly)
                return relationship == CompletedMoatRelationship.Friendly;
            return relationship == CompletedMoatRelationship.Friendly ||
                policy == MoatTraversalPolicy.AllowEnemyForDiagnostic;
        }

        internal bool IsNativeMoatAdjacentTile(int tileId)
        {
            if (IsCompletedMoat(tileId))
                return true;
            uint flags = tileFlags[tileId];
            if ((flags & NativeMoatAdjacentAlwaysAllowedMask) != 0)
                return true;
            if ((flags & NativeMoatAdjacentImmediateBlockMask) != 0)
                return false;
            if ((flags & MoatReconstructionBlockingMask) == 0)
                return true;
            return (flags & NativeSpecialStructureTileFlag) != 0 &&
                IsNativeSpecialStructure(tileId);
        }

        internal bool HasCompatibleNativeHeight(int currentTile, int nextTile)
        {
            int currentHeight = heightLayer[currentTile];
            int nextHeight = heightLayer[nextTile];
            if ((tileFlags[nextTile] & DynamicStructureHeightTileFlag) != 0)
                nextHeight += GetNativeStructureHeight(nextTile);
            if (Math.Abs(nextHeight - currentHeight) <= 16)
                return true;

            ushort nextType = GetNativeBuildingType(nextTile);
            if (nextType == NativeLowWallType || nextType == NativeHighWallType)
                nextHeight -= NativeWallHeightCorrection;
            else
            {
                ushort currentType = GetNativeBuildingType(currentTile);
                if (currentType == NativeLowWallType || currentType == NativeHighWallType)
                    currentHeight -= NativeWallHeightCorrection;
            }
            return Math.Abs(nextHeight - currentHeight) <= 16;
        }

        internal int GetNativeStructureHeight(int tileId)
        {
            // Exact C07C0 switch used by DAFD0 for flag 0x10000000. The type itself is
            // read through the same 0x32C-byte native building record as the caller.
            switch (GetNativeBuildingType(tileId))
            {
                case 0x28: return 0x40;
                case 0x29: return 0x5C;
                case 0x2A: return 0xBE;
                case 0x2D:
                case 0x2E: return 0x80;
                case 0x45: return 0x76;
                case 0x4A: return 0x128;
                case 0x4B: return 0x94;
                case 0x4C: return 0xB4;
                case 0x4D:
                case 0x4E: return 0xC0;
                default: return 0;
            }
        }

        internal ushort GetNativeBuildingType(int tileId)
        {
            int buildingId = (short)buildingLayer[tileId];
            if (buildingId <= 0 || buildingId > MaximumNativeBuildingId ||
                nativeBuildingTypeBias == null)
            {
                return 0;
            }
            return *(ushort*)(nativeBuildingTypeBias +
                ((long)buildingId * NativeBuildingRecordStride));
        }

        internal bool IsStructuralTile(int tileId) =>
            (tileFlags[tileId] & WallOrStairMask) != 0 ||
            buildingLayer[tileId] != 0 ||
            (tileFlags[tileId] & NativeSpecialStructureTileFlag) != 0 &&
            IsNativeSpecialStructure(tileId);

        internal bool IsNativeSpecialStructure(int tileId)
        {
            byte state = specialStructureClassification[tileId];
            if (state == 0)
            {
                state = resolveSpecialStructure(tileId) ? (byte)1 : (byte)2;
                specialStructureClassification[tileId] = state;
                classifiedSpecialStructureTiles[classifiedSpecialStructureCount++] = tileId;
            }
            return state == 1;
        }

        internal CompletedMoatRelationship GetMoatRelationship(int playerId, int tileId)
        {
            byte state = moatClassification[tileId];
            if (state == 0)
            {
                CompletedMoatRelationship relationship =
                    resolveCompletedMoatRelationship(playerId, tileId);
                state = relationship == CompletedMoatRelationship.Friendly
                    ? (byte)1
                    : relationship == CompletedMoatRelationship.Enemy ? (byte)2 : (byte)3;
                moatClassification[tileId] = state;
                classifiedTiles[classifiedCount++] = tileId;
            }
            return state == 1
                ? CompletedMoatRelationship.Friendly
                : state == 2
                    ? CompletedMoatRelationship.Enemy
                    : CompletedMoatRelationship.Invalid;
        }

        internal void BeginReachabilityProbe() => ResetClassifications();

        internal void EndReachabilityProbe() => ResetClassifications();

        internal bool IsCompletedMoat(int tileId) =>
            (tileFlags[tileId] & CompletedMoatTileFlag) != 0;

        internal int GetTileId(int x, int y) => rowLookup[y * 3] + x;

        internal static int GetNode(int x, int y) => y * MapWidth + x;

        internal static ulong UpdateRouteFingerprint(ulong fingerprint, int direction) =>
            unchecked((fingerprint ^ (byte)direction) * RouteFingerprintPrime);

        internal bool IsValidCoordinate(int x, int y) =>
            x >= 0 && x < MapWidth && y >= 0 && y < MapWidth &&
            IsNativeTile(GetTileId(x, y));

        internal static bool IsNativeTile(int tileId) =>
            tileId >= 0 && tileId < NativeTileCount;

        internal void ResetClassifications()
        {
            for (int index = 0; index < classifiedCount; index++)
                moatClassification[classifiedTiles[index]] = 0;
            classifiedCount = 0;
            for (int index = 0; index < classifiedSpecialStructureCount; index++)
                specialStructureClassification[classifiedSpecialStructureTiles[index]] = 0;
            classifiedSpecialStructureCount = 0;
        }

        internal static int FindDirection(int dx, int dy)
        {
            for (int direction = 0; direction < DirectionX.Length; direction++)
            {
                if (DirectionX[direction] == dx && DirectionY[direction] == dy)
                    return direction;
            }
            return -1;
        }
    }

    internal readonly struct WeightedMoatEncodedRoute
    {
        public WeightedMoatEncodedRoute(byte[] bytes, int directionCount)
        {
            Bytes = bytes;
            DirectionCount = directionCount;
        }

        public byte[] Bytes { get; }
        public int DirectionCount { get; }
        public bool IsValid => Bytes != null && DirectionCount >= 0 &&
            DirectionCount <= WeightedMoatRoutePlanner.MaximumRouteEdges &&
            Bytes.Length == (DirectionCount + 1) / 2;
    }

    internal readonly struct WeightedMoatRouteSummary
    {
        internal WeightedMoatRouteSummary(
            bool found,
            string reason,
            int routeLength,
            int groundEdges,
            int moatEdges,
            int structuralEdges,
            int diagonalEdges,
            int directionChanges,
            ulong routeFingerprint,
            long estimatedTicks,
            double searchMilliseconds,
            int expandedNodes)
        {
            Found = found;
            Reason = reason;
            RouteLength = routeLength;
            GroundEdges = groundEdges;
            MoatEdges = moatEdges;
            StructuralEdges = structuralEdges;
            DiagonalEdges = diagonalEdges;
            DirectionChanges = directionChanges;
            RouteFingerprint = routeFingerprint;
            EstimatedTicks = estimatedTicks;
            SearchMilliseconds = searchMilliseconds;
            ExpandedNodes = expandedNodes;
        }

        public bool Found { get; }
        public string Reason { get; }
        public int RouteLength { get; }
        public int GroundEdges { get; }
        public int MoatEdges { get; }
        public int StructuralEdges { get; }
        public int DiagonalEdges { get; }
        public int DirectionChanges { get; }
        public ulong RouteFingerprint { get; }
        public long EstimatedTicks { get; }
        public double SearchMilliseconds { get; }
        public int ExpandedNodes { get; }

        public static WeightedMoatRouteSummary Succeeded(
            int routeLength,
            int groundEdges,
            int moatEdges,
            int structuralEdges,
            int diagonalEdges,
            int directionChanges,
            ulong routeFingerprint,
            long estimatedTicks,
            double searchMilliseconds,
            int expandedNodes) =>
            new WeightedMoatRouteSummary(
                true, "none", routeLength, groundEdges, moatEdges, structuralEdges, diagonalEdges,
                directionChanges, routeFingerprint,
                estimatedTicks, searchMilliseconds, expandedNodes);

        public static WeightedMoatRouteSummary Failed(
            string reason,
            double searchMilliseconds,
            int expandedNodes = 0,
            int routeLength = 0) =>
            new WeightedMoatRouteSummary(
                false, reason, routeLength, 0, 0, 0, 0, 0, 0, 0,
                searchMilliseconds, expandedNodes);
    }
}
