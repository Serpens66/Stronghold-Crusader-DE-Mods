using BepInEx.Logging;
using APIShared;
using Iced.Intel;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace EnemyGatePathfindingTest
{
    internal readonly struct SamePclCoverageSnapshot
    {
        internal SamePclCoverageSnapshot(bool installed, bool ownerConflict, long queries,
            long preserved, long rejectedEdges, long detours, long noRoutes,
            long humanDetours, long aiDetours, long attackEdges,
            long buildingEdges, long candidateEdges, long cursorCommandEdges,
            long directCursorQueries, long directCursorEdges,
            long cursorPclChecks, long cursorPclWrapperCalls,
            long cursorSamePclEligible, long cursorDifferentPcl,
            long cursorDifferentPclEligible,
            long cursorValidationRequests, long cursorResultForcedZero,
            long cursorNativeRefreshes, long cursorCacheHits,
            long cursorExactCacheHits, long cursorReferenceNoRoutes,
            long cursorProvenPolicyBlocks,
            long cursorValidationDeferrals, long cursorPolicyBlocked,
            long cursorReachable, long cursorRejectedEdges, long cursorUnitPending,
            long cursorValidationTicks,
            long cursorValidationMaxTicks,
            long missingContexts, long invalidPlayers, long scopeMismatches,
            long slotConflicts, long poolExhaustions, long exceptions,
            long aiQueries, long aiNoRoutes, long attackQueries,
            long buildingApproachQueries, long buildingConsumerQueries,
            long alternateBuildingConsumerQueries, long candidateQueries,
            long aiTacticalTargetQueries, long aiTacticalBuildingEdges,
            long aiTacticalUnitEdges, long aiTacticalFallbackEdges,
            long aiTacticalInvalidPlayers, long aiTacticalScopeConflicts,
            long aiTacticalExceptions)
        {
            Installed = installed; OwnerConflict = ownerConflict; Queries = queries;
            Preserved = preserved; RejectedEdges = rejectedEdges; Detours = detours;
            NoRoutes = noRoutes; HumanDetours = humanDetours; AiDetours = aiDetours;
            AttackEdges = attackEdges; BuildingEdges = buildingEdges;
            CandidateEdges = candidateEdges; CursorCommandEdges = cursorCommandEdges;
            DirectCursorQueries = directCursorQueries; DirectCursorEdges = directCursorEdges;
            CursorPclChecks = cursorPclChecks; CursorNativeRefreshes = cursorNativeRefreshes;
            CursorPclWrapperCalls = cursorPclWrapperCalls;
            CursorSamePclEligible = cursorSamePclEligible;
            CursorDifferentPcl = cursorDifferentPcl;
            CursorDifferentPclEligible = cursorDifferentPclEligible;
            CursorValidationRequests = cursorValidationRequests;
            CursorResultForcedZero = cursorResultForcedZero;
            CursorCacheHits = cursorCacheHits; CursorExactCacheHits = cursorExactCacheHits;
            CursorReferenceNoRoutes = cursorReferenceNoRoutes;
            CursorProvenPolicyBlocks = cursorProvenPolicyBlocks;
            CursorValidationDeferrals = cursorValidationDeferrals;
            CursorPolicyBlocked = cursorPolicyBlocked; CursorReachable = cursorReachable;
            CursorRejectedEdges = cursorRejectedEdges;
            CursorUnitPending = cursorUnitPending; CursorValidationTicks = cursorValidationTicks;
            CursorValidationMaxTicks = cursorValidationMaxTicks;
            MissingContexts = missingContexts; InvalidPlayers = invalidPlayers;
            ScopeMismatches = scopeMismatches; SlotConflicts = slotConflicts;
            PoolExhaustions = poolExhaustions; Exceptions = exceptions;
            AiQueries = aiQueries; AiNoRoutes = aiNoRoutes;
            AttackQueries = attackQueries; BuildingApproachQueries = buildingApproachQueries;
            BuildingConsumerQueries = buildingConsumerQueries;
            AlternateBuildingConsumerQueries = alternateBuildingConsumerQueries;
            CandidateQueries = candidateQueries;
            AiTacticalTargetQueries = aiTacticalTargetQueries;
            AiTacticalBuildingEdges = aiTacticalBuildingEdges;
            AiTacticalUnitEdges = aiTacticalUnitEdges;
            AiTacticalFallbackEdges = aiTacticalFallbackEdges;
            AiTacticalInvalidPlayers = aiTacticalInvalidPlayers;
            AiTacticalScopeConflicts = aiTacticalScopeConflicts;
            AiTacticalExceptions = aiTacticalExceptions;
        }
        internal bool Installed { get; }
        internal bool OwnerConflict { get; }
        internal long Queries { get; }
        internal long Preserved { get; }
        internal long RejectedEdges { get; }
        internal long Detours { get; }
        internal long NoRoutes { get; }
        internal long HumanDetours { get; }
        internal long AiDetours { get; }
        internal long AttackEdges { get; }
        internal long BuildingEdges { get; }
        internal long CandidateEdges { get; }
        internal long CursorCommandEdges { get; }
        internal long DirectCursorQueries { get; }
        internal long DirectCursorEdges { get; }
        internal long CursorPclChecks { get; }
        internal long CursorPclWrapperCalls { get; }
        internal long CursorSamePclEligible { get; }
        internal long CursorDifferentPcl { get; }
        internal long CursorDifferentPclEligible { get; }
        internal long CursorValidationRequests { get; }
        internal long CursorResultForcedZero { get; }
        internal long CursorNativeRefreshes { get; }
        internal long CursorCacheHits { get; }
        internal long CursorExactCacheHits { get; }
        internal long CursorReferenceNoRoutes { get; }
        internal long CursorProvenPolicyBlocks { get; }
        internal long CursorValidationDeferrals { get; }
        internal long CursorPolicyBlocked { get; }
        internal long CursorReachable { get; }
        internal long CursorRejectedEdges { get; }
        internal long CursorUnitPending { get; }
        internal long CursorValidationTicks { get; }
        internal long CursorValidationMaxTicks { get; }
        internal long MissingContexts { get; }
        internal long InvalidPlayers { get; }
        internal long ScopeMismatches { get; }
        internal long SlotConflicts { get; }
        internal long PoolExhaustions { get; }
        internal long Exceptions { get; }
        internal long AiQueries { get; }
        internal long AiNoRoutes { get; }
        internal long AttackQueries { get; }
        internal long BuildingApproachQueries { get; }
        internal long BuildingConsumerQueries { get; }
        internal long AlternateBuildingConsumerQueries { get; }
        internal long CandidateQueries { get; }
        internal long AiTacticalTargetQueries { get; }
        internal long AiTacticalBuildingEdges { get; }
        internal long AiTacticalUnitEdges { get; }
        internal long AiTacticalFallbackEdges { get; }
        internal long AiTacticalInvalidPlayers { get; }
        internal long AiTacticalScopeConflicts { get; }
        internal long AiTacticalExceptions { get; }
    }

    // Vanilla remains the only route finder. Managed detours merely bind an immutable
    // player mask for the duration of a complete query; eleven movement adapters and
    // three tactical-target adapters AND that mask into Vanilla's own edge checks
    // without changing the global grid.
    internal sealed unsafe class SamePclGateRouteRuntime : IEnemyGatePathPolicy,
        IEnemyGateRegionPairObserver
    {
        private const int ThreadSlotStride = 32;
        private const int NativeSnapshotPoolSize = 4;
        private static readonly long CursorRefreshInterval = Math.Max(
            1L, Stopwatch.Frequency / 5L);
        private enum QueryKind
        {
            HumanBuilder, AiBuilder, Attack, BuildingApproach,
            AlternateBuildingApproach, CandidateSearch, CursorCommand, DirectCursor,
            CursorPreview, AiTacticalTarget
        }

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int PathBuilderDelegate(IntPtr manager, int playerId, int profile);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate void AttackApproachDelegate(IntPtr manager, int tribeId,
            int targetContext, uint x, uint y, int resultCount, int region, int movementClass);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate void BuildingApproachDelegate(IntPtr manager, int tribeId,
            int buildingId, int resultCount, int region, int movementClass);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate void BuildingConsumerDelegate(IntPtr tribeManager, int tribeId, int variant);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate void CursorMoveDelegate(IntPtr unitManager, int tribeId,
            int targetX, int targetY, int targetContext, int actionFlags);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate void CandidateSearchDelegate(IntPtr manager, int startTileId,
            int resultCount, int targetPcl, int playerId, int mode, int candidateClass);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int DirectTileSearchDelegate(IntPtr manager, int x, int y,
            int argument4, int argument5, int argument6);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int PclReachabilityDelegate(IntPtr manager, int player,
            int targetPcl, int sourcePcl, int mode);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int CursorPclDecisionDelegate(IntPtr manager, int player,
            int targetPcl, int sourcePcl, int mode, int unitId);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate void AiTacticalTargetDelegate(IntPtr tribeManager, int tribeId);

        private sealed class DetourCandidate<T> where T : Delegate
        {
            internal readonly DetourHandle<T> Handle = new DetourHandle<T>();
            internal readonly ulong Address;
            internal DetourCandidate(ulong address) { Address = address; }
            internal bool Committed => Handle.Success && Handle.IsInstalled &&
                Handle.Failure == null && Handle.ResolvedAddress == Address;
        }

        private sealed class PlayerKindSnapshot
        {
            internal static readonly PlayerKindSnapshot Empty = new PlayerKindSnapshot(new bool[9]);
            private readonly bool[] ai;
            internal PlayerKindSnapshot(bool[] values) { ai = values ?? new bool[9]; }
            internal bool IsAi(int player) => player > 0 && player < ai.Length && ai[player];
        }

        private sealed class TribePlayerSnapshot
        {
            internal static readonly TribePlayerSnapshot Empty =
                new TribePlayerSnapshot(Array.Empty<int>());
            private readonly int[] owners;
            internal TribePlayerSnapshot(int[] values) { owners = values ?? Array.Empty<int>(); }
            internal int Resolve(int tribeId) => tribeId > 0 && tribeId < owners.Length
                ? owners[tribeId] : -1;
        }

        private sealed class NativeMaskSnapshot
        {
            internal static readonly NativeMaskSnapshot Empty = new NativeMaskSnapshot();
            internal readonly IntPtr[] PlayerMasks = new IntPtr[9];
            internal int Readers;
            internal bool Filling;
            internal ulong Fingerprint;
            private readonly IntPtr storage;
            private NativeMaskSnapshot() { }
            internal NativeMaskSnapshot(bool allocate)
            {
                if (!allocate) return;
                storage = Marshal.AllocHGlobal(checked(
                    EnemyGatePathfindingNativeDefinition.MaximumTileIdExclusive * 8));
            }
            internal void Fill(RouteTilePolicySnapshot source)
            {
                for (int player = 1; player <= 8; player++)
                {
                    PlayerMasks[player] = IntPtr.Zero;
                    byte[] mask = source?.DirectionMasks != null &&
                        player < source.DirectionMasks.Length ? source.DirectionMasks[player] : null;
                    if (mask == null || mask.Length !=
                        EnemyGatePathfindingNativeDefinition.MaximumTileIdExclusive) continue;
                    IntPtr target = IntPtr.Add(storage, checked((player - 1) * mask.Length));
                    Marshal.Copy(mask, 0, target, mask.Length);
                    PlayerMasks[player] = target;
                }
                Fingerprint = source?.TopologyFingerprint ?? 0UL;
            }
        }

        private readonly ManualLogSource log;
        private readonly AttackOrderCorrelationDiagnostics attackOrderDiagnostics;
        private readonly IntPtr threadSlots;
        private readonly IntPtr tacticalThreadSlots;
        private HookTransaction transaction;
        private readonly ScanRegion region;
        private readonly ulong libraryBase;
        private readonly object maskGate = new object();
        private readonly NativeMaskSnapshot[] maskPool;
        private volatile NativeMaskSnapshot currentMasks = NativeMaskSnapshot.Empty;
        private volatile RouteTilePolicySnapshot publishedPolicy = RouteTilePolicySnapshot.Empty;
        private RouteTilePolicySnapshot pendingPolicy;
        private int policyGeneration;
        private volatile PlayerKindSnapshot playerKinds = PlayerKindSnapshot.Empty;
        private volatile TribePlayerSnapshot tribePlayers = TribePlayerSnapshot.Empty;
        private readonly bool ownerConflict;
        private DetourCandidate<PathBuilderDelegate> builder;
        private DetourCandidate<AttackApproachDelegate> attack;
        private DetourCandidate<BuildingApproachDelegate> building;
        private DetourCandidate<BuildingConsumerDelegate> consumer;
        private DetourCandidate<BuildingConsumerDelegate> alternateConsumer;
        private DetourCandidate<CursorMoveDelegate> cursor;
        private DetourCandidate<CandidateSearchDelegate> candidateSearch;
        private DetourCandidate<AiTacticalTargetDelegate> aiTacticalTarget;
        private PathBuilderDelegate originalBuilder, rootedBuilder;
        private AttackApproachDelegate originalAttack, rootedAttack;
        private BuildingApproachDelegate originalBuilding, rootedBuilding;
        private BuildingConsumerDelegate originalConsumer, rootedConsumer;
        private BuildingConsumerDelegate originalAlternateConsumer, rootedAlternateConsumer;
        private CursorMoveDelegate originalCursor, rootedCursor;
        private CandidateSearchDelegate originalCandidateSearch, rootedCandidateSearch;
        private AiTacticalTargetDelegate originalAiTacticalTarget, rootedAiTacticalTarget;
        private readonly DirectTileSearchDelegate originalDirectTileSearch;
        private readonly DirectTileSearchDelegate rootedDirectCursorSearch;
        private readonly PclReachabilityDelegate originalPclReachability;
        private readonly CursorPclDecisionDelegate rootedCursorPclDecision;
        private readonly HookHandle<X64InlineHook> directCursorHook = new HookHandle<X64InlineHook>();
        private readonly HookHandle<X64InlineHook> cursorPclDecisionHook =
            new HookHandle<X64InlineHook>();
        private readonly HookHandle<X64InlineHook>[] edgeHooks =
            new HookHandle<X64InlineHook>[EnemyGatePathfindingNativeDefinition.DirectionFilterRvas.Length];
        private readonly HookHandle<X64InlineHook>[] tacticalEdgeHooks =
            new HookHandle<X64InlineHook>[EnemyGatePathfindingNativeDefinition.AiTacticalFilterRvas.Length];
        private long queries, preserved, rejectedEdges, detours, noRoutes;
        private long humanDetours, aiDetours, attackEdges, buildingEdges, candidateEdges;
        private long cursorCommandEdges, directCursorQueries, directCursorEdges;
        private long cursorPclChecks, cursorPclWrapperCalls, cursorSamePclEligible,
            cursorDifferentPcl, cursorDifferentPclEligible,
            cursorValidationRequests, cursorResultForcedZero;
        private long cursorNativeRefreshes, cursorCacheHits,
            cursorExactCacheHits, cursorReferenceNoRoutes, cursorProvenPolicyBlocks;
        private long cursorValidationDeferrals, cursorPolicyBlocked, cursorReachable,
            cursorRejectedEdges;
        private long cursorUnitPending, cursorValidationTicks, cursorValidationMaxTicks;
        private long missingContexts, invalidPlayers, scopeMismatches;
        private long slotConflicts, poolExhaustions, exceptions;
        private long aiQueries, aiNoRoutes, attackQueries, buildingApproachQueries,
            buildingConsumerQueries, alternateBuildingConsumerQueries, candidateQueries;
        private long aiTacticalTargetQueries, aiTacticalBuildingEdges,
            aiTacticalUnitEdges, aiTacticalFallbackEdges, aiTacticalInvalidPlayers,
            aiTacticalScopeConflicts, aiTacticalExceptions;
        private int lastPoolExhaustionGeneration = -1;
        private readonly int[] samplePublished = new int[10];
        private readonly int[] sampleRequested = new int[10];
        private readonly int[] sampleNative = new int[10];
        private readonly int[] sampleTribe = new int[10];
        private readonly int[] sampleUsed = new int[10];
        private readonly int[] tacticalEdgeSampleState = new int[3];
        private readonly int[] tacticalEdgeSampleSource = new int[3];
        private readonly int[] tacticalEdgeSampleTarget = new int[3];
        private readonly int[] tacticalEdgeSampleDirection = new int[3];
        private readonly CursorPreviewCache cursorCache = new CursorPreviewCache(CursorRefreshInterval);
        private long cursorEpoch;
        private int cursorValidationActive;
        private int cursorSampleState, cursorSamplePlayer, cursorSampleUnit,
            cursorSampleGlobal, cursorSampleStartX, cursorSampleStartY,
            cursorSampleTargetX, cursorSampleTargetY, cursorSampleTargetTile,
            cursorSampleAllowed, cursorSampleReferenceResult, cursorSampleFilteredResult;
        private long cursorSampleFingerprint, cursorSampleRejectedEdges,
            cursorSampleElapsedTicks;
        private int cursorDecisionSampleState, cursorDecisionSamplePlayer,
            cursorDecisionSampleUnit, cursorDecisionSampleTargetPcl,
            cursorDecisionSampleSourcePcl, cursorDecisionSampleVanillaResult,
            cursorDecisionSampleFinalResult;
        private long nextPlayerRefresh;
        private int installAttempted;
        private bool hooksInstalled;

        internal SamePclGateRouteRuntime(ManualLogSource log, ReadOnlySpan<byte> memory,
            ScanRegion region, ulong libraryBase, bool existingHookOwner,
            AttackOrderCorrelationDiagnostics attackOrderDiagnostics)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.region = region;
            this.libraryBase = libraryBase;
            this.attackOrderDiagnostics = attackOrderDiagnostics;
            ownerConflict = existingHookOwner;
            maskPool = new NativeMaskSnapshot[NativeSnapshotPoolSize];
            for (int index = 0; index < maskPool.Length; index++)
                maskPool[index] = new NativeMaskSnapshot(true);
            EnemyGatePathfindingNativeDefinition.ValidateSamePclNativeFilterContracts(
                memory, existingHookOwner);
            originalDirectTileSearch = Marshal.GetDelegateForFunctionPointer<DirectTileSearchDelegate>(
                new IntPtr(unchecked((long)(libraryBase +
                    EnemyGatePathfindingNativeDefinition.DirectTileSearchRva))));
            originalPclReachability = Marshal.GetDelegateForFunctionPointer<PclReachabilityDelegate>(
                new IntPtr(unchecked((long)(libraryBase +
                    EnemyGatePathfindingNativeDefinition.PclReachabilityRva))));
            rootedDirectCursorSearch = FilterDirectCursorSearch;
            rootedCursorPclDecision = FilterCursorPclDecision;
            threadSlots = Marshal.AllocHGlobal(
                DirectionFilterAdapterEmitter.ThreadSlotCount * ThreadSlotStride);
            for (int index = 0;
                 index < DirectionFilterAdapterEmitter.ThreadSlotCount * ThreadSlotStride;
                 index++)
                ((byte*)threadSlots)[index] = 0;
            tacticalThreadSlots = Marshal.AllocHGlobal(
                AiTacticalTargetAdapterEmitter.ThreadSlotCount *
                AiTacticalTargetAdapterEmitter.ThreadSlotStride);
            for (int index = 0;
                 index < AiTacticalTargetAdapterEmitter.ThreadSlotCount *
                    AiTacticalTargetAdapterEmitter.ThreadSlotStride;
                 index++)
                ((byte*)tacticalThreadSlots)[index] = 0;
        }

        private void InstallHooks()
        {
            transaction = new HookTransaction(region,
                SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                new HookTransactionOptions { FailureMode = TransactionFailureMode.RollbackAndThrow, OwnsHooks = false });
            if (!ownerConflict)
            {
                builder = AddDetour(EnemyGatePathfindingNativeDefinition.PathBuilderRva,
                    rootedBuilder = FilterBuilder, libraryBase);
                attack = AddDetour(EnemyGatePathfindingNativeDefinition.AttackApproachRva,
                    rootedAttack = FilterAttack, libraryBase);
                building = AddDetour(EnemyGatePathfindingNativeDefinition.BuildingApproachRva,
                    rootedBuilding = FilterBuilding, libraryBase);
                consumer = AddDetour(EnemyGatePathfindingNativeDefinition.BuildingConsumerRva,
                    rootedConsumer = FilterConsumer, libraryBase);
            }
            alternateConsumer = AddDetour(
                EnemyGatePathfindingNativeDefinition.AlternateBuildingConsumerRva,
                rootedAlternateConsumer = FilterAlternateConsumer, libraryBase);
            if (!ownerConflict)
                cursor = AddDetour(EnemyGatePathfindingNativeDefinition.CursorMoveStagerRva,
                    rootedCursor = FilterCursor, libraryBase);
            candidateSearch = AddDetour(
                EnemyGatePathfindingNativeDefinition.PlayerAwareCandidateSearchRva,
                rootedCandidateSearch = FilterCandidateSearch, libraryBase);
            aiTacticalTarget = AddDetour(
                EnemyGatePathfindingNativeDefinition.AiTacticalTargetSelectionRva,
                rootedAiTacticalTarget = FilterAiTacticalTarget, libraryBase);
            ulong directCursorWrapper = unchecked((ulong)Marshal.GetFunctionPointerForDelegate(
                rootedDirectCursorSearch).ToInt64());
            DirectCursorCallAdapterEmitter.AssembleAndValidate(
                EnemyGatePathfindingNativeDefinition.GetDirectCursorSearchBlockBytes(),
                libraryBase + EnemyGatePathfindingNativeDefinition.DirectCursorSearchBlockRva,
                directCursorWrapper, libraryBase + 0x02100000UL);
            using (var probe = new X64InlineHook(
                libraryBase + EnemyGatePathfindingNativeDefinition.DirectCursorSearchBlockRva,
                EnemyGatePathfindingNativeDefinition.DirectCursorSearchBlockLength))
                if (probe.DisplacedByteCount !=
                    EnemyGatePathfindingNativeDefinition.DirectCursorSearchBlockLength)
                    throw new InvalidOperationException(
                        $"RedBird direct-cursor span was {probe.DisplacedByteCount}, expected " +
                        EnemyGatePathfindingNativeDefinition.DirectCursorSearchBlockLength + ".");
            transaction.AddInline(directCursorHook,
                HookTarget.FromAddress(libraryBase +
                    EnemyGatePathfindingNativeDefinition.DirectCursorSearchBlockRva),
                (asm, original, returnAddress) => DirectCursorCallAdapterEmitter.Emit(
                    asm, original, directCursorWrapper),
                hookSize: EnemyGatePathfindingNativeDefinition.DirectCursorSearchBlockLength);
            ulong cursorPclWrapper = unchecked((ulong)Marshal.GetFunctionPointerForDelegate(
                rootedCursorPclDecision).ToInt64());
            CursorPclCallAdapterEmitter.AssembleAndValidate(
                EnemyGatePathfindingNativeDefinition.GetCursorPclDecisionBytes(),
                libraryBase + EnemyGatePathfindingNativeDefinition.CursorPclDecisionRva,
                cursorPclWrapper, libraryBase + 0x02200000UL);
            using (var probe = new X64InlineHook(
                libraryBase + EnemyGatePathfindingNativeDefinition.CursorPclDecisionRva,
                EnemyGatePathfindingNativeDefinition.CursorPclDecisionLength))
                if (probe.DisplacedByteCount !=
                    EnemyGatePathfindingNativeDefinition.CursorPclDecisionLength)
                    throw new InvalidOperationException(
                        $"RedBird cursor-PCL span was {probe.DisplacedByteCount}, expected " +
                        EnemyGatePathfindingNativeDefinition.CursorPclDecisionLength + ".");
            transaction.AddInline(cursorPclDecisionHook,
                HookTarget.FromAddress(libraryBase +
                    EnemyGatePathfindingNativeDefinition.CursorPclDecisionRva),
                (asm, original, returnAddress) => CursorPclCallAdapterEmitter.Emit(
                    asm, original, cursorPclWrapper),
                hookSize: EnemyGatePathfindingNativeDefinition.CursorPclDecisionLength);
            for (int index = 0; index < edgeHooks.Length; index++)
            {
                edgeHooks[index] = new HookHandle<X64InlineHook>();
                int captured = index;
                int rva = EnemyGatePathfindingNativeDefinition.DirectionFilterRvas[index];
                int length = EnemyGatePathfindingNativeDefinition.DirectionFilterLengths[index];
                DirectionFilterAdapterEmitter.AssembleAndValidate(
                    EnemyGatePathfindingNativeDefinition.GetDirectionFilterBytes(index),
                    libraryBase + unchecked((ulong)rva), index,
                    unchecked((ulong)threadSlots.ToInt64()),
                    libraryBase + 0x02000000UL + unchecked((ulong)(index * 0x1000)));
                using (var probe = new X64InlineHook(libraryBase + unchecked((ulong)rva), 14))
                    if (probe.DisplacedByteCount != length)
                        throw new InvalidOperationException(
                            $"RedBird direction-filter span {index} was {probe.DisplacedByteCount}, expected {length}.");
                transaction.AddInline(edgeHooks[index],
                    HookTarget.FromAddress(libraryBase + unchecked((ulong)rva)),
                    (asm, original, returnAddress) => DirectionFilterAdapterEmitter.Emit(
                        asm, original, captured, unchecked((ulong)threadSlots.ToInt64())), hookSize: 14);
            }
            for (int index = 0; index < tacticalEdgeHooks.Length; index++)
            {
                tacticalEdgeHooks[index] = new HookHandle<X64InlineHook>();
                int captured = index;
                int rva = EnemyGatePathfindingNativeDefinition.AiTacticalFilterRvas[index];
                int length = EnemyGatePathfindingNativeDefinition.AiTacticalFilterLengths[index];
                ulong reject = libraryBase + unchecked((ulong)
                    EnemyGatePathfindingNativeDefinition.AiTacticalRejectRvas[index]);
                AiTacticalTargetAdapterEmitter.AssembleAndValidate(
                    EnemyGatePathfindingNativeDefinition.GetAiTacticalFilterBytes(index),
                    libraryBase + unchecked((ulong)rva), index,
                    unchecked((ulong)tacticalThreadSlots.ToInt64()), reject,
                    libraryBase + 0x02300000UL + unchecked((ulong)(index * 0x1000)));
                using (var probe = new X64InlineHook(libraryBase + unchecked((ulong)rva), length))
                    if (probe.DisplacedByteCount != length)
                        throw new InvalidOperationException(
                            $"RedBird AI tactical-filter span {index} was " +
                            $"{probe.DisplacedByteCount}, expected {length}.");
                transaction.AddInline(tacticalEdgeHooks[index],
                    HookTarget.FromAddress(libraryBase + unchecked((ulong)rva)),
                    (asm, original, returnAddress) => AiTacticalTargetAdapterEmitter.Emit(
                        asm, original, captured,
                        unchecked((ulong)tacticalThreadSlots.ToInt64()),
                        libraryBase + unchecked((ulong)
                            EnemyGatePathfindingNativeDefinition.AiTacticalRejectRvas[captured])),
                    hookSize: length);
            }
            CommitResult result = transaction.Commit();
            if (!result.IsCompleteSuccess ||
                (!ownerConflict && (!builder.Committed || !attack.Committed ||
                    !building.Committed || !consumer.Committed || !cursor.Committed)) ||
                !alternateConsumer.Committed || !candidateSearch.Committed || !aiTacticalTarget.Committed ||
                !directCursorHook.Success || !directCursorHook.IsInstalled ||
                directCursorHook.Failure != null ||
                !cursorPclDecisionHook.Success || !cursorPclDecisionHook.IsInstalled ||
                cursorPclDecisionHook.Failure != null ||
                directCursorHook.Hook.DisplacedByteCount !=
                    EnemyGatePathfindingNativeDefinition.DirectCursorSearchBlockLength ||
                cursorPclDecisionHook.Hook.DisplacedByteCount !=
                    EnemyGatePathfindingNativeDefinition.CursorPclDecisionLength)
            {
                transaction.DisableAll();
                throw new InvalidOperationException($"Vanilla Gate-Filter was not installed atomically: {result}");
            }
            for (int index = 0; index < edgeHooks.Length; index++)
            {
                if (!edgeHooks[index].Success || !edgeHooks[index].IsInstalled ||
                    edgeHooks[index].Failure != null || edgeHooks[index].Hook.DisplacedByteCount !=
                        EnemyGatePathfindingNativeDefinition.DirectionFilterLengths[index])
                {
                    transaction.DisableAll();
                    throw new InvalidOperationException($"Direction-filter hook {index} failed its committed contract.");
                }
            }
            for (int index = 0; index < tacticalEdgeHooks.Length; index++)
            {
                if (!tacticalEdgeHooks[index].Success || !tacticalEdgeHooks[index].IsInstalled ||
                    tacticalEdgeHooks[index].Failure != null ||
                    tacticalEdgeHooks[index].Hook.DisplacedByteCount !=
                        EnemyGatePathfindingNativeDefinition.AiTacticalFilterLengths[index])
                {
                    transaction.DisableAll();
                    throw new InvalidOperationException(
                        $"AI tactical-filter hook {index} failed its committed contract.");
                }
            }
            if (!ownerConflict)
            {
                originalBuilder = builder.Handle.Original; originalAttack = attack.Handle.Original;
                originalBuilding = building.Handle.Original; originalConsumer = consumer.Handle.Original;
                originalCursor = cursor.Handle.Original;
            }
            originalAlternateConsumer = alternateConsumer.Handle.Original;
            originalCandidateSearch = candidateSearch.Handle.Original;
            originalAiTacticalTarget = aiTacticalTarget.Handle.Original;
            hooksInstalled = true;
            Shared.DebugLogHelper.LogInfo(log,
                "Vanilla player-aware gate filter installed: " +
                $"sharedHookOwner={ownerConflict}, " +
                "scopes=builder/attack/building/consumer/alternateConsumer/candidateSearch/" +
                "cursorCommand/directCursorDB650/cursorPclCallAdapter/aiTacticalTarget, " +
                "directionAdapters=11, tacticalAdapters=3, " +
                "cursorPclCallAdapter=" +
                $"0x{EnemyGatePathfindingNativeDefinition.CursorPclDecisionRva:X}/" +
                $"{EnemyGatePathfindingNativeDefinition.CursorPclDecisionLength}, directCursorCallsite=" +
                $"0x{EnemyGatePathfindingNativeDefinition.DirectCursorSearchCallRva:X}, " +
                "nativeSnapshotPool=4, managedNodeCallbacks=0, managedCursorSearches=0, " +
                "managedReplacementSearches=0, cursorCacheTtlMs=200, cursorCacheEntries=32, cursorValidation=on-demand, representativeUnits=1, " +
                "globalDirectionGridWrites=0, tileBounds=unsigned<" +
                EnemyGatePathfindingNativeDefinition.MaximumTileIdExclusive + ", contracts=[" +
                DirectionFilterAdapterEmitter.DescribeContracts() + "], tacticalContracts=[" +
                AiTacticalTargetAdapterEmitter.DescribeContracts() + "].");
        }

        internal bool Installed => hooksInstalled;

        bool IEnemyGatePathPolicy.HasPublishedMask =>
            publishedPolicy.NonEmptyPlayerMaskCount != 0;

        void IEnemyGateRegionPairObserver.ObserveRegionPair(int playerId,
            int sourceComponentId, int destinationComponentId, int queryMode,
            int vanillaResult, int effectiveResult, string source) =>
            attackOrderDiagnostics?.ObserveRegionPair(playerId, sourceComponentId,
                destinationComponentId, queryMode, vanillaResult, effectiveResult, source);

        bool IEnemyGatePathPolicy.IsDirectionAllowed(int playerId, int tileId, int direction) =>
            publishedPolicy.IsDirectionAllowed(playerId, tileId, direction);

        int IEnemyGatePathPolicy.ResolveTribePlayer(int tribeId) =>
            tribePlayers.Resolve(tribeId);

        int IEnemyGatePathPolicy.ResolveBuildingPlayer(int rawSearchArgument, int tribeId)
        {
            int tribePlayer = tribePlayers.Resolve(tribeId);
            int usedPlayer = ValidateExplicitPlayer(rawSearchArgument, tribePlayer, "shared-building");
            try { attackOrderDiagnostics?.ObserveBuildingContext(rawSearchArgument, tribeId, tribePlayer, usedPlayer); }
            catch { Interlocked.Increment(ref exceptions); }
            return usedPlayer;
        }

        int IEnemyGatePathPolicy.ResolveCursorPlayer(int tribeId)
        {
            int nativePlayer = *(int*)(libraryBase +
                EnemyGatePathfindingNativeDefinition.ActivePlayerIdRva);
            return ValidateExplicitPlayer(nativePlayer, tribePlayers.Resolve(tribeId), "shared-cursor");
        }

        object IEnemyGatePathPolicy.EnterNativeSearch(int playerId, EnemyGateSearchKind kind)
        {
            QueryKind localKind = SharedKind(kind, playerId);
            if (localKind == QueryKind.AiBuilder) Interlocked.Increment(ref aiQueries);
            CaptureScopeSample(localKind, playerId, playerId, -1, playerId);
            QueryScope scope = Enter(playerId);
            if (kind == EnemyGateSearchKind.Builder)
            {
                try { attackOrderDiagnostics?.BeginBuilder(); }
                catch { Interlocked.Increment(ref exceptions); }
            }
            return scope;
        }

        void IEnemyGatePathPolicy.ExitNativeSearch(
            object scope, EnemyGateSearchKind kind, bool completed, bool success)
        {
            if (!(scope is QueryScope query)) return;
            QueryKind localKind = SharedKind(kind, -1);
            if (kind == EnemyGateSearchKind.Builder)
                localKind = playerKinds.IsAi(query.PlayerId)
                    ? QueryKind.AiBuilder : QueryKind.HumanBuilder;
            long touched = Complete(query, localKind,
                kind == EnemyGateSearchKind.Builder, completed && success);
            if (kind == EnemyGateSearchKind.Builder)
            {
                try { attackOrderDiagnostics?.ObserveBuilder(
                    query.PlayerId, completed, success, touched, query.Snapshot.Fingerprint); }
                catch { Interlocked.Increment(ref exceptions); }
            }
        }

        private QueryKind SharedKind(EnemyGateSearchKind kind, int playerId)
        {
            switch (kind)
            {
                case EnemyGateSearchKind.Builder:
                    return playerKinds.IsAi(playerId) ? QueryKind.AiBuilder : QueryKind.HumanBuilder;
                case EnemyGateSearchKind.Attack: return QueryKind.Attack;
                case EnemyGateSearchKind.BuildingApproach:
                case EnemyGateSearchKind.BuildingConsumer: return QueryKind.BuildingApproach;
                case EnemyGateSearchKind.CursorCommand: return QueryKind.CursorCommand;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }
        private DetourCandidate<T> AddDetour<T>(int rva, T callback, ulong libraryBase) where T : Delegate
        {
            ulong address = libraryBase + unchecked((ulong)rva);
            var candidate = new DetourCandidate<T>(address);
            transaction.AddDetour(candidate.Handle, HookTarget.FromAddress(address), callback);
            return candidate;
        }

        internal void UpdatePolicy(RouteTilePolicySnapshot policy)
        {
            if (policy == null || policy.NonEmptyPlayerMaskCount == 0)
            {
                lock (maskGate)
                {
                    policyGeneration++;
                    pendingPolicy = null;
                    currentMasks = NativeMaskSnapshot.Empty;
                    publishedPolicy = RouteTilePolicySnapshot.Empty;
                }
                return;
            }
            try
            {
                if (!Installed && Interlocked.CompareExchange(ref installAttempted, 1, 0) == 0)
                    InstallHooks();
                if (!Installed) return;
                if (ownerConflict && !EnemyGatePathPolicyBridge.TryRegister(this))
                    throw new InvalidOperationException("another enemy-gate policy provider owns APIShared");
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref exceptions);
                Shared.DebugLogHelper.LogWarning(log,
                    $"Native gate-mask publication failed open: {ex.GetType().Name}: {ex.Message}");
                return;
            }
            int generation;
            lock (maskGate)
            {
                generation = ++policyGeneration;
                pendingPolicy = policy;
                // Never let a query acquire stale access policy while a replacement is built.
                currentMasks = NativeMaskSnapshot.Empty;
                publishedPolicy = RouteTilePolicySnapshot.Empty;
            }
            TryPublishPending(policy, generation);
        }

        internal void ProcessDeferred()
        {
            RouteTilePolicySnapshot retry;
            int generation;
            lock (maskGate)
            {
                retry = pendingPolicy;
                generation = policyGeneration;
            }
            if (retry != null) TryPublishPending(retry, generation);
            long now = Stopwatch.GetTimestamp();
            if (now < Volatile.Read(ref nextPlayerRefresh)) return;
            Volatile.Write(ref nextPlayerRefresh, now + Stopwatch.Frequency);
            var ai = new bool[9];
            for (int player = 1; player <= 8; player++)
                ai[player] = GamePlayerManagerAPI.Instance.IsAIPlayer(player);
            playerKinds = new PlayerKindSnapshot(ai);
            Span<GameTribe> tribes = GameTribeManagerAPI.Instance.GetTribeAsSpan();
            var owners = new int[tribes.Length];
            // Script Extender 2.7 exposes the complete native tribe-slot span.
            // Slot 0 is reserved; every other span index is already the tribe ID.
            for (int tribeId = 1; tribeId < tribes.Length; tribeId++)
                owners[tribeId] = tribes[tribeId].r_PlayerIdOwner;
            tribePlayers = new TribePlayerSnapshot(owners);
        }

        private int FilterCursorPclDecision(IntPtr manager, int player,
            int targetPcl, int sourcePcl, int mode, int unitId)
        {
            int vanillaResult = originalPclReachability(
                manager, player, targetPcl, sourcePcl, mode);
            int finalResult = vanillaResult;
            Interlocked.Increment(ref cursorPclWrapperCalls);
            try
            {
                bool samePcl = targetPcl == sourcePcl;
                if (targetPcl != sourcePcl)
                    Interlocked.Increment(ref cursorDifferentPcl);
                if (vanillaResult > 0)
                {
                    Interlocked.Increment(ref cursorPclChecks);
                    if (samePcl) Interlocked.Increment(ref cursorSamePclEligible);
                    else Interlocked.Increment(ref cursorDifferentPclEligible);
                    NativeMaskSnapshot snapshot = currentMasks;
                    int tileBefore = *(int*)(libraryBase +
                        EnemyGatePathfindingNativeDefinition.CursorMouseTileIdRva);
                    int targetX = *(int*)(libraryBase +
                        EnemyGatePathfindingNativeDefinition.CursorMouseTileXRva);
                    int targetY = *(int*)(libraryBase +
                        EnemyGatePathfindingNativeDefinition.CursorMouseTileYRva);
                    int tileAfter = *(int*)(libraryBase +
                        EnemyGatePathfindingNativeDefinition.CursorMouseTileIdRva);
                    bool valid = player > 0 && player <= 8 && unitId > 0 &&
                        snapshot.Fingerprint != 0 &&
                        snapshot.PlayerMasks[player] != IntPtr.Zero &&
                        tileBefore == tileAfter && targetX >= 0 && targetX < 800 &&
                        targetY >= 0 && targetY < 800 && tileAfter >= 0 &&
                        tileAfter < EnemyGatePathfindingNativeDefinition.MaximumTileIdExclusive &&
                        *(int*)(libraryBase + EnemyGatePathfindingNativeDefinition.TileRowStartRva +
                            unchecked((ulong)(targetY * 12))) + targetX == tileAfter;
                    if (valid)
                    {
                        Interlocked.Increment(ref cursorValidationRequests);
                        bool allowed;
                        if (TryValidateCursorPreview(player, unitId, targetX, targetY,
                                tileAfter, targetPcl, sourcePcl, snapshot.Fingerprint,
                                out allowed))
                        {
                            finalResult = EnemyGatePathfindingPolicy.ApplyCursorPreviewResult(
                                vanillaResult, true, allowed);
                            if (finalResult == 0 && vanillaResult != 0)
                            {
                                Interlocked.Increment(ref cursorPolicyBlocked);
                                Interlocked.Increment(ref cursorResultForcedZero);
                            }
                        }
                        else
                        {
                            Interlocked.Increment(ref cursorUnitPending);
                        }
                    }
                }
            }
            catch
            {
                Interlocked.Increment(ref exceptions);
                finalResult = vanillaResult;
            }
            CaptureCursorDecisionSample(player, unitId, targetPcl, sourcePcl,
                vanillaResult, finalResult);
            return finalResult;
        }

        private bool TryValidateCursorPreview(int player, int unitId, int targetX,
            int targetY, int targetTile, int targetPcl, int sourcePcl,
            ulong fingerprint, out bool allowed)
        {
            allowed = true;
            if (!Installed) return false;
            if (Interlocked.CompareExchange(ref cursorValidationActive, 1, 0) != 0)
            {
                Interlocked.Increment(ref cursorValidationDeferrals);
                return false;
            }
            try
            {
                uint thread = GetCurrentThreadId();
                byte* activeSlot = (byte*)threadSlots + ((thread &
                    (DirectionFilterAdapterEmitter.ThreadSlotCount - 1)) * ThreadSlotStride);
                byte* tacticalSlot = (byte*)tacticalThreadSlots + ((thread &
                    (AiTacticalTargetAdapterEmitter.ThreadSlotCount - 1)) *
                    AiTacticalTargetAdapterEmitter.ThreadSlotStride);
                if (Volatile.Read(ref *(int*)(activeSlot + DirectionFilterAdapterEmitter.SlotDepthOffset)) != 0 ||
                    Volatile.Read(ref *(int*)(tacticalSlot + AiTacticalTargetAdapterEmitter.SlotDepthOffset)) != 0)
                {
                    Interlocked.Increment(ref cursorValidationDeferrals);
                    return false;
                }
                long epoch = Volatile.Read(ref cursorEpoch);
                int generation = Volatile.Read(ref policyGeneration);
                long now = Stopwatch.GetTimestamp();
                NativeMaskSnapshot snapshot = currentMasks;
                if (player <= 0 || player > 8 || fingerprint == 0 ||
                    snapshot.Fingerprint != fingerprint ||
                    snapshot.PlayerMasks[player] == IntPtr.Zero) return false;
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
                    unit == null || unit->r_AliveState != AliveState.IsAlive ||
                    unit->r_ControllableForPlayerId != player)
                {
                    Interlocked.Increment(ref cursorUnitPending);
                    return false;
                }
                int startX = unit->r_CurrentTilePositionX;
                int startY = unit->r_CurrentTilePositionY;
                if (startX < 0 || startY < 0 || startX >= 800 || startY >= 800 ||
                    targetX < 0 || targetX >= 800 || targetY < 0 || targetY >= 800 ||
                    targetTile < 0 ||
                    targetTile >= EnemyGatePathfindingNativeDefinition.MaximumTileIdExclusive ||
                    *(int*)(libraryBase + EnemyGatePathfindingNativeDefinition.TileRowStartRva +
                        unchecked((ulong)(targetY * 12))) + targetX != targetTile) return false;

                CursorPreviewCache.Key key = new CursorPreviewCache.Key(player, unitId,
                    unchecked((int)unit->r_GlobalId), startX, startY, targetX, targetY,
                    targetPcl, sourcePcl, epoch, generation, fingerprint);
                if (cursorCache.TryGet(key, now, out allowed))
                {
                    if (!CursorSnapshotMatches(snapshot, epoch, generation)) return false;
                    Interlocked.Increment(ref cursorCacheHits);
                    Interlocked.Increment(ref cursorExactCacheHits);
                    return true;
                }
                long started = Stopwatch.GetTimestamp();
                int referenceResult = 0;
                int filteredResult = 0;
                long touched = 0;
                IntPtr manager = new IntPtr(unchecked((long)(libraryBase +
                    EnemyGatePathfindingNativeDefinition.NativePathManagerRva)));
                QueryScope referenceScope = Enter(player, true);
                if (referenceScope.Slot == null ||
                    !ReferenceEquals(referenceScope.Snapshot, snapshot))
                {
                    Complete(referenceScope, QueryKind.CursorPreview, true, false);
                    return false;
                }
                try
                {
                    referenceResult = originalDirectTileSearch(manager,
                        startX, startY, targetX, targetY,
                        EnemyGatePathfindingNativeDefinition.DirectCursorSearchNodeLimit);
                }
                catch
                {
                    Interlocked.Increment(ref exceptions);
                    return false;
                }
                finally
                {
                    Complete(referenceScope, QueryKind.CursorPreview, true,
                        referenceResult > 0);
                }
                if (!CursorSnapshotMatches(snapshot, epoch, generation)) return false;
                if (referenceResult <= 0)
                    Interlocked.Increment(ref cursorReferenceNoRoutes);
                else
                {
                    CaptureScopeSample(QueryKind.CursorPreview, player, player, -1, player);
                    QueryScope filteredScope = Enter(player);
                    if (filteredScope.Slot == null ||
                        !ReferenceEquals(filteredScope.Snapshot, snapshot))
                    {
                        Complete(filteredScope, QueryKind.CursorPreview, true, false);
                        return false;
                    }
                    try
                    {
                        filteredResult = originalDirectTileSearch(manager,
                            startX, startY, targetX, targetY,
                            EnemyGatePathfindingNativeDefinition.DirectCursorSearchNodeLimit);
                    }
                    catch
                    {
                        Interlocked.Increment(ref exceptions);
                        return false;
                    }
                    finally
                    {
                        touched = Complete(filteredScope, QueryKind.CursorPreview, true,
                            filteredResult > 0);
                    }
                }
                if (!CursorSnapshotMatches(snapshot, epoch, generation)) return false;
                long elapsed = Stopwatch.GetTimestamp() - started;
                Interlocked.Increment(ref cursorNativeRefreshes);
                Interlocked.Add(ref cursorValidationTicks, elapsed);
                UpdateMaximum(ref cursorValidationMaxTicks, elapsed);
                if (filteredResult > 0) Interlocked.Increment(ref cursorReachable);
                allowed = !EnemyGatePathfindingPolicy.ShouldBlockCursorPreview(
                    referenceResult, filteredResult, touched);
                if (!allowed) Interlocked.Increment(ref cursorProvenPolicyBlocks);

                if (unit->r_GlobalId != unchecked((uint)key.Global) ||
                    unit->r_CurrentTilePositionX != startX || unit->r_CurrentTilePositionY != startY ||
                    unit->r_AliveState != AliveState.IsAlive || unit->r_ControllableForPlayerId != player)
                    return false;
                cursorCache.Put(key, now, allowed);

                if (Interlocked.CompareExchange(ref cursorSampleState, 1, 0) == 0)
                {
                    cursorSamplePlayer = player;
                    cursorSampleUnit = unitId;
                    cursorSampleGlobal = unchecked((int)unit->r_GlobalId);
                    cursorSampleStartX = startX;
                    cursorSampleStartY = startY;
                    cursorSampleTargetX = targetX;
                    cursorSampleTargetY = targetY;
                    cursorSampleTargetTile = targetTile;
                    cursorSampleFingerprint = unchecked((long)fingerprint);
                    cursorSampleRejectedEdges = touched;
                    cursorSampleElapsedTicks = elapsed;
                    cursorSampleReferenceResult = referenceResult;
                    cursorSampleFilteredResult = filteredResult;
                    cursorSampleAllowed = allowed ? 1 : 0;
                    Volatile.Write(ref cursorSampleState, 2);
                }
                return CursorSnapshotMatches(snapshot, epoch, generation);
            }
            finally
            {
                Volatile.Write(ref cursorValidationActive, 0);
            }
        }

        private bool CursorSnapshotMatches(NativeMaskSnapshot snapshot, long epoch, int generation) =>
            ReferenceEquals(currentMasks, snapshot) &&
            Volatile.Read(ref cursorEpoch) == epoch &&
            Volatile.Read(ref policyGeneration) == generation;

        private void CaptureCursorDecisionSample(int player, int unitId,
            int targetPcl, int sourcePcl, int vanillaResult, int finalResult)
        {
            if (Interlocked.CompareExchange(ref cursorDecisionSampleState, 1, 0) != 0)
                return;
            cursorDecisionSamplePlayer = player;
            cursorDecisionSampleUnit = unitId;
            cursorDecisionSampleTargetPcl = targetPcl;
            cursorDecisionSampleSourcePcl = sourcePcl;
            cursorDecisionSampleVanillaResult = vanillaResult;
            cursorDecisionSampleFinalResult = finalResult;
            Volatile.Write(ref cursorDecisionSampleState, 2);
        }

        private void TryPublishPending(RouteTilePolicySnapshot policy, int generation)
        {
            NativeMaskSnapshot slot = null;
            lock (maskGate)
            {
                if (generation != policyGeneration || !ReferenceEquals(policy, pendingPolicy)) return;
                for (int index = 0; index < maskPool.Length; index++)
                {
                    NativeMaskSnapshot candidate = maskPool[index];
                    if (candidate.Filling || candidate.Readers != 0 ||
                        ReferenceEquals(candidate, currentMasks)) continue;
                    candidate.Filling = true;
                    slot = candidate;
                    break;
                }
            }
            if (slot == null)
            {
                if (Interlocked.Exchange(ref lastPoolExhaustionGeneration, generation) != generation)
                    Interlocked.Increment(ref poolExhaustions);
                return;
            }
            try
            {
                slot.Fill(policy);
                lock (maskGate)
                {
                    slot.Filling = false;
                    if (generation != policyGeneration || !ReferenceEquals(policy, pendingPolicy)) return;
                    currentMasks = slot;
                    publishedPolicy = policy;
                    pendingPolicy = null;
                }
            }
            catch (Exception ex)
            {
                lock (maskGate) slot.Filling = false;
                Interlocked.Increment(ref exceptions);
                Shared.DebugLogHelper.LogWarning(log,
                    $"Native gate-mask slot fill failed open: {ex.GetType().Name}: {ex.Message}");
            }
        }

        internal SamePclCoverageSnapshot GetCoverageSnapshot() =>
            new SamePclCoverageSnapshot(Installed, ownerConflict, Read(ref queries),
                Read(ref preserved), Read(ref rejectedEdges), Read(ref detours), Read(ref noRoutes),
                Read(ref humanDetours), Read(ref aiDetours), Read(ref attackEdges),
                Read(ref buildingEdges), Read(ref candidateEdges), Read(ref cursorCommandEdges),
                Read(ref directCursorQueries), Read(ref directCursorEdges),
                Read(ref cursorPclChecks), Read(ref cursorPclWrapperCalls),
                Read(ref cursorSamePclEligible), Read(ref cursorDifferentPcl),
                Read(ref cursorDifferentPclEligible),
                Read(ref cursorValidationRequests), Read(ref cursorResultForcedZero),
                Read(ref cursorNativeRefreshes),
                Read(ref cursorCacheHits), Read(ref cursorExactCacheHits),
                Read(ref cursorReferenceNoRoutes), Read(ref cursorProvenPolicyBlocks),
                Read(ref cursorValidationDeferrals),
                Read(ref cursorPolicyBlocked), Read(ref cursorReachable),
                Read(ref cursorRejectedEdges), Read(ref cursorUnitPending),
                Read(ref cursorValidationTicks),
                Read(ref cursorValidationMaxTicks),
                Read(ref missingContexts), Read(ref invalidPlayers), Read(ref scopeMismatches),
                Read(ref slotConflicts), Read(ref poolExhaustions), Read(ref exceptions),
                Read(ref aiQueries), Read(ref aiNoRoutes), Read(ref attackQueries),
                Read(ref buildingApproachQueries), Read(ref buildingConsumerQueries),
                Read(ref alternateBuildingConsumerQueries), Read(ref candidateQueries),
                Read(ref aiTacticalTargetQueries), Read(ref aiTacticalBuildingEdges),
                Read(ref aiTacticalUnitEdges), Read(ref aiTacticalFallbackEdges),
                Read(ref aiTacticalInvalidPlayers), Read(ref aiTacticalScopeConflicts),
                Read(ref aiTacticalExceptions));
        internal void ResetCounters()
        {
            Reset(ref queries); Reset(ref preserved); Reset(ref rejectedEdges); Reset(ref detours);
            Reset(ref noRoutes); Reset(ref humanDetours); Reset(ref aiDetours);
            Reset(ref attackEdges); Reset(ref buildingEdges); Reset(ref candidateEdges);
            Reset(ref cursorCommandEdges); Reset(ref directCursorQueries); Reset(ref directCursorEdges);
            Reset(ref cursorPclChecks); Reset(ref cursorPclWrapperCalls);
            Reset(ref cursorSamePclEligible); Reset(ref cursorDifferentPcl);
            Reset(ref cursorDifferentPclEligible);
            Reset(ref cursorValidationRequests); Reset(ref cursorResultForcedZero);
            Reset(ref cursorNativeRefreshes); Reset(ref cursorCacheHits);
            Reset(ref cursorExactCacheHits); Reset(ref cursorReferenceNoRoutes);
            Reset(ref cursorProvenPolicyBlocks);
            Reset(ref cursorValidationDeferrals); Reset(ref cursorPolicyBlocked);
            Reset(ref cursorReachable); Reset(ref cursorRejectedEdges);
            Reset(ref cursorUnitPending);
            Reset(ref cursorValidationTicks); Reset(ref cursorValidationMaxTicks);
            Reset(ref missingContexts); Reset(ref invalidPlayers);
            Reset(ref scopeMismatches); Reset(ref slotConflicts); Reset(ref poolExhaustions);
            Reset(ref exceptions);
            Reset(ref aiQueries); Reset(ref aiNoRoutes); Reset(ref attackQueries);
            Reset(ref buildingApproachQueries); Reset(ref buildingConsumerQueries);
            Reset(ref alternateBuildingConsumerQueries); Reset(ref candidateQueries);
            Reset(ref aiTacticalTargetQueries); Reset(ref aiTacticalBuildingEdges);
            Reset(ref aiTacticalUnitEdges); Reset(ref aiTacticalFallbackEdges);
            Reset(ref aiTacticalInvalidPlayers); Reset(ref aiTacticalScopeConflicts);
            Reset(ref aiTacticalExceptions);
            Interlocked.Increment(ref cursorEpoch);
            cursorCache.Clear();
            Volatile.Write(ref cursorSampleState, 0);
            Volatile.Write(ref cursorDecisionSampleState, 0);
            for (int index = 0; index < samplePublished.Length; index++)
                Volatile.Write(ref samplePublished[index], 0);
            for (int index = 0; index < tacticalEdgeSampleState.Length; index++)
                Volatile.Write(ref tacticalEdgeSampleState[index], 0);
        }

        private int FilterBuilder(IntPtr manager, int player, int profile)
        {
            QueryKind kind = playerKinds.IsAi(player) ? QueryKind.AiBuilder : QueryKind.HumanBuilder;
            if (kind == QueryKind.AiBuilder) Interlocked.Increment(ref aiQueries);
            CaptureScopeSample(kind, player, player, -1, player);
            QueryScope scope = Enter(player); int result = 0; bool completed = false;
            try { attackOrderDiagnostics?.BeginBuilder(); }
            catch { Interlocked.Increment(ref exceptions); }
            try { result = originalBuilder(manager, player, profile); completed = true; return result; }
            catch { Interlocked.Increment(ref exceptions); throw; }
            finally
            {
                long touched = Complete(scope, kind, true, completed && result > 0);
                try { attackOrderDiagnostics?.ObserveBuilder(player, completed, result > 0,
                    touched, scope.Snapshot.Fingerprint); }
                catch { Interlocked.Increment(ref exceptions); }
            }
        }
        private void FilterAttack(IntPtr manager, int unused2, int unused3, uint x, uint y,
            int count, int targetPcl, int player)
        {
            Interlocked.Increment(ref attackQueries);
            CaptureScopeSample(QueryKind.Attack, player, player, -1, player);
            QueryScope scope = Enter(player);
            try { originalAttack(manager, unused2, unused3, x, y, count, targetPcl, player); }
            catch { Interlocked.Increment(ref exceptions); throw; }
            finally { Complete(scope, QueryKind.Attack, false, false); }
        }
        private void FilterBuilding(IntPtr manager, int tribe, int buildingId,
            int count, int targetPcl, int player)
        {
            Interlocked.Increment(ref buildingApproachQueries);
            int tribePlayer = ResolveTribePlayer(tribe);
            int used = ValidateExplicitPlayer(player, tribePlayer, "building-approach");
            CaptureScopeSample(QueryKind.BuildingApproach, player, player, tribePlayer, used);
            QueryScope scope = Enter(used);
            try { originalBuilding(manager, tribe, buildingId, count, targetPcl, player); }
            catch { Interlocked.Increment(ref exceptions); throw; }
            finally { Complete(scope, QueryKind.BuildingApproach, false, false); }
        }
        private void FilterConsumer(IntPtr tribeManager, int tribe, int variant)
        {
            Interlocked.Increment(ref buildingConsumerQueries);
            int player = ResolveTribePlayer(tribe);
            CaptureScopeSample(QueryKind.BuildingApproach, tribe, -1, player, player);
            QueryScope scope = Enter(player);
            try { originalConsumer(tribeManager, tribe, variant); }
            catch { Interlocked.Increment(ref exceptions); throw; }
            finally { Complete(scope, QueryKind.BuildingApproach, false, false); }
        }
        private void FilterAlternateConsumer(IntPtr tribeManager, int tribe, int variant)
        {
            Interlocked.Increment(ref alternateBuildingConsumerQueries);
            int player = ResolveTribePlayer(tribe);
            CaptureScopeSample(QueryKind.AlternateBuildingApproach, tribe, -1, player, player);
            QueryScope scope = Enter(player);
            try { originalAlternateConsumer(tribeManager, tribe, variant); }
            catch { Interlocked.Increment(ref exceptions); throw; }
            finally { Complete(scope, QueryKind.AlternateBuildingApproach, false, false); }
        }
        private void FilterCandidateSearch(IntPtr manager, int startTileId, int count,
            int targetPcl, int player, int mode, int candidateClass)
        {
            Interlocked.Increment(ref candidateQueries);
            CaptureScopeSample(QueryKind.CandidateSearch, player, player, -1, player);
            QueryScope scope = Enter(player);
            try { originalCandidateSearch(manager, startTileId, count,
                    targetPcl, player, mode, candidateClass); }
            catch { Interlocked.Increment(ref exceptions); throw; }
            finally { Complete(scope, QueryKind.CandidateSearch, false, false); }
        }
        private void FilterAiTacticalTarget(IntPtr tribeManager, int tribe)
        {
            Interlocked.Increment(ref aiTacticalTargetQueries);
            int snapshotPlayer = ResolveTribePlayer(tribe);
            int nativePlayer = -1;
            int player = -1;
            if (tribeManager != IntPtr.Zero && snapshotPlayer > 0 && snapshotPlayer <= 8)
            {
                nativePlayer = *(int*)((byte*)tribeManager +
                    tribe * EnemyGatePathfindingNativeDefinition.NativeTribeRecordStride +
                    EnemyGatePathfindingNativeDefinition.NativeTribePlayerIdOffset);
                if (nativePlayer > 0 && nativePlayer <= 8)
                {
                    if (nativePlayer == snapshotPlayer) player = nativePlayer;
                    else Interlocked.Increment(ref aiTacticalScopeConflicts);
                }
            }
            if (player < 1) Interlocked.Increment(ref aiTacticalInvalidPlayers);
            CaptureScopeSample(QueryKind.AiTacticalTarget, snapshotPlayer,
                nativePlayer, snapshotPlayer, player);
            TacticalQueryScope scope = EnterTactical(player);
            try { originalAiTacticalTarget(tribeManager, tribe); }
            catch
            {
                Interlocked.Increment(ref aiTacticalExceptions);
                Interlocked.Increment(ref exceptions);
                throw;
            }
            finally { CompleteTactical(scope); }
        }
        private void FilterCursor(IntPtr manager, int tribe, int x, int y, int context, int flags)
        {
            int nativePlayer = *(int*)(libraryBase +
                EnemyGatePathfindingNativeDefinition.ActivePlayerIdRva);
            int tribePlayer = ResolveTribePlayer(tribe);
            int player = ValidateExplicitPlayer(nativePlayer, tribePlayer, "cursor-command");
            CaptureScopeSample(QueryKind.CursorCommand, nativePlayer, nativePlayer,
                tribePlayer, player);
            QueryScope scope = Enter(player);
            try { originalCursor(manager, tribe, x, y, context, flags); }
            catch { Interlocked.Increment(ref exceptions); throw; }
            finally { Complete(scope, QueryKind.CursorCommand, false, false); }
        }

        private int FilterDirectCursorSearch(IntPtr manager, int x, int y,
            int argument4, int argument5, int argument6)
        {
            int player = *(int*)(libraryBase +
                EnemyGatePathfindingNativeDefinition.ActivePlayerIdRva);
            CaptureScopeSample(QueryKind.DirectCursor, player, player, -1, player);
            Interlocked.Increment(ref directCursorQueries);
            QueryScope scope = Enter(player); int result = 0; bool completed = false;
            try
            {
                result = originalDirectTileSearch(manager, x, y, argument4, argument5, argument6);
                completed = true;
                return result;
            }
            catch { Interlocked.Increment(ref exceptions); throw; }
            finally { Complete(scope, QueryKind.DirectCursor, true, completed && result > 0); }
        }
        private int ResolveTribePlayer(int tribeId) => tribePlayers.Resolve(tribeId);

        private int ValidateExplicitPlayer(int nativePlayer, int tribePlayer, string source)
        {
            if (nativePlayer <= 0 || nativePlayer > 8 || tribePlayer <= 0 || tribePlayer > 8)
                return -1;
            if (nativePlayer != tribePlayer)
            {
                Interlocked.Increment(ref scopeMismatches);
                try { attackOrderDiagnostics?.ObserveScopeMismatch(source, nativePlayer, tribePlayer); }
                catch { Interlocked.Increment(ref exceptions); }
                return -1;
            }
            return nativePlayer;
        }

        private void CaptureScopeSample(QueryKind kind, int requested, int native, int tribe, int used)
        {
            int index = (int)kind;
            if (Interlocked.CompareExchange(ref samplePublished[index], 1, 0) != 0) return;
            sampleRequested[index] = requested;
            sampleNative[index] = native;
            sampleTribe[index] = tribe;
            sampleUsed[index] = used;
            Volatile.Write(ref samplePublished[index], 2);
        }

        internal string DescribeScopeSamples()
        {
            var text = new System.Text.StringBuilder();
            for (int index = 0; index < samplePublished.Length; index++)
            {
                if (Volatile.Read(ref samplePublished[index]) != 2) continue;
                if (text.Length > 0) text.Append(';');
                text.Append((QueryKind)index).Append("(requested=").Append(sampleRequested[index])
                    .Append(",native=").Append(sampleNative[index])
                    .Append(",tribe=").Append(sampleTribe[index])
                    .Append(",used=").Append(sampleUsed[index]).Append(')');
            }
            return text.Length == 0 ? "none" : text.ToString();
        }

        internal string DescribeCursorPreviewSample()
        {
            if (Volatile.Read(ref cursorSampleState) != 2) return "none";
            return "player=" + cursorSamplePlayer +
                ",unit=" + cursorSampleUnit +
                ",global=" + cursorSampleGlobal +
                ",start=" + cursorSampleStartX + "/" + cursorSampleStartY +
                ",target=" + cursorSampleTargetX + "/" + cursorSampleTargetY +
                ",tile=" + cursorSampleTargetTile +
                ",fingerprint=0x" + unchecked((ulong)cursorSampleFingerprint).ToString("X16") +
                ",rejectedEdges=" + cursorSampleRejectedEdges +
                ",referenceResult=" + cursorSampleReferenceResult +
                ",filteredResult=" + cursorSampleFilteredResult +
                ",allowed=" + (cursorSampleAllowed != 0) +
                ",elapsedTicks=" + cursorSampleElapsedTicks;
        }

        internal string DescribeCursorDecisionSample()
        {
            if (Volatile.Read(ref cursorDecisionSampleState) != 2) return "none";
            return "player=" + cursorDecisionSamplePlayer +
                ",unit=" + cursorDecisionSampleUnit +
                ",targetPcl=" + cursorDecisionSampleTargetPcl +
                ",sourcePcl=" + cursorDecisionSampleSourcePcl +
                ",vanillaResult=" + cursorDecisionSampleVanillaResult +
                ",finalResult=" + cursorDecisionSampleFinalResult;
        }

        internal string DescribeAiTacticalEdgeSamples()
        {
            string[] names = { "building", "unit", "fallback" };
            var text = new System.Text.StringBuilder();
            for (int index = 0; index < tacticalEdgeSampleState.Length; index++)
            {
                if (Volatile.Read(ref tacticalEdgeSampleState[index]) != 2) continue;
                if (text.Length > 0) text.Append(';');
                text.Append(names[index]).Append("(source=")
                    .Append(tacticalEdgeSampleSource[index]).Append(",target=")
                    .Append(tacticalEdgeSampleTarget[index]).Append(",directionBit=0x")
                    .Append(tacticalEdgeSampleDirection[index].ToString("X2")).Append(')');
            }
            return text.Length == 0 ? "none" : text.ToString();
        }

        private QueryScope Enter(int player, bool unmasked = false)
        {
            Interlocked.Increment(ref queries);
            NativeMaskSnapshot snapshot;
            IntPtr mask;
            lock (maskGate)
            {
                snapshot = currentMasks;
                snapshot.Readers++;
                mask = !unmasked && player > 0 && player < snapshot.PlayerMasks.Length
                    ? snapshot.PlayerMasks[player] : IntPtr.Zero;
            }
            if (player <= 0 || player >= snapshot.PlayerMasks.Length)
            {
                Interlocked.Increment(ref invalidPlayers);
                Interlocked.Increment(ref missingContexts);
            }
            uint thread = GetCurrentThreadId();
            byte* slot = (byte*)threadSlots + ((thread &
                (DirectionFilterAdapterEmitter.ThreadSlotCount - 1)) * ThreadSlotStride);
            int owner = Volatile.Read(ref *(int*)(slot + DirectionFilterAdapterEmitter.SlotOwnerOffset));
            if (owner != 0 && owner != unchecked((int)thread))
            {
                Interlocked.Increment(ref slotConflicts); return new QueryScope(snapshot, null, IntPtr.Zero, 0, player);
            }
            if (owner == 0 && Interlocked.CompareExchange(
                    ref *(int*)(slot + DirectionFilterAdapterEmitter.SlotOwnerOffset), unchecked((int)thread), 0) != 0)
            {
                Interlocked.Increment(ref slotConflicts); return new QueryScope(snapshot, null, IntPtr.Zero, 0, player);
            }
            IntPtr previous = *(IntPtr*)(slot + DirectionFilterAdapterEmitter.SlotMaskOffset);
            long previousTouched = *(long*)(slot + DirectionFilterAdapterEmitter.SlotTouchedOffset);
            (*(int*)(slot + DirectionFilterAdapterEmitter.SlotDepthOffset))++;
            *(IntPtr*)(slot + DirectionFilterAdapterEmitter.SlotMaskOffset) = mask;
            *(long*)(slot + DirectionFilterAdapterEmitter.SlotTouchedOffset) = 0;
            return new QueryScope(snapshot, slot, previous, previousTouched, player);
        }

        private TacticalQueryScope EnterTactical(int player)
        {
            NativeMaskSnapshot snapshot;
            IntPtr mask;
            lock (maskGate)
            {
                snapshot = currentMasks;
                snapshot.Readers++;
                mask = player > 0 && player < snapshot.PlayerMasks.Length
                    ? snapshot.PlayerMasks[player] : IntPtr.Zero;
            }
            uint thread = GetCurrentThreadId();
            byte* slot = (byte*)tacticalThreadSlots + ((thread &
                (AiTacticalTargetAdapterEmitter.ThreadSlotCount - 1)) *
                AiTacticalTargetAdapterEmitter.ThreadSlotStride);
            int owner = Volatile.Read(ref *(int*)(slot +
                AiTacticalTargetAdapterEmitter.SlotOwnerOffset));
            if (owner != 0 || (owner == 0 && Interlocked.CompareExchange(
                    ref *(int*)(slot + AiTacticalTargetAdapterEmitter.SlotOwnerOffset),
                    unchecked((int)thread), 0) != 0))
            {
                Interlocked.Increment(ref aiTacticalScopeConflicts);
                return new TacticalQueryScope(snapshot, null);
            }
            *(IntPtr*)(slot + AiTacticalTargetAdapterEmitter.SlotMaskOffset) = mask;
            *(long*)(slot + AiTacticalTargetAdapterEmitter.SlotBuildingTouchedOffset) = 0;
            *(long*)(slot + AiTacticalTargetAdapterEmitter.SlotUnitTouchedOffset) = 0;
            *(long*)(slot + AiTacticalTargetAdapterEmitter.SlotFallbackTouchedOffset) = 0;
            *(int*)(slot + AiTacticalTargetAdapterEmitter.SlotDepthOffset) = 1;
            return new TacticalQueryScope(snapshot, slot);
        }

        private void CompleteTactical(TacticalQueryScope scope)
        {
            if (scope.Slot != null)
            {
                long building = *(long*)(scope.Slot +
                    AiTacticalTargetAdapterEmitter.SlotBuildingTouchedOffset);
                long unit = *(long*)(scope.Slot +
                    AiTacticalTargetAdapterEmitter.SlotUnitTouchedOffset);
                long fallback = *(long*)(scope.Slot +
                    AiTacticalTargetAdapterEmitter.SlotFallbackTouchedOffset);
                if (building != 0)
                {
                    Interlocked.Add(ref aiTacticalBuildingEdges, building);
                    CaptureTacticalEdgeSample(0, scope.Slot,
                        AiTacticalTargetAdapterEmitter.SlotBuildingSourceOffset,
                        AiTacticalTargetAdapterEmitter.SlotBuildingTargetOffset,
                        AiTacticalTargetAdapterEmitter.SlotBuildingDirectionOffset);
                }
                if (unit != 0)
                {
                    Interlocked.Add(ref aiTacticalUnitEdges, unit);
                    CaptureTacticalEdgeSample(1, scope.Slot,
                        AiTacticalTargetAdapterEmitter.SlotUnitSourceOffset,
                        AiTacticalTargetAdapterEmitter.SlotUnitTargetOffset,
                        AiTacticalTargetAdapterEmitter.SlotUnitDirectionOffset);
                }
                if (fallback != 0)
                {
                    Interlocked.Add(ref aiTacticalFallbackEdges, fallback);
                    CaptureTacticalEdgeSample(2, scope.Slot,
                        AiTacticalTargetAdapterEmitter.SlotFallbackSourceOffset,
                        AiTacticalTargetAdapterEmitter.SlotFallbackTargetOffset,
                        AiTacticalTargetAdapterEmitter.SlotFallbackDirectionOffset);
                }
                *(IntPtr*)(scope.Slot + AiTacticalTargetAdapterEmitter.SlotMaskOffset) = IntPtr.Zero;
                *(int*)(scope.Slot + AiTacticalTargetAdapterEmitter.SlotDepthOffset) = 0;
                Volatile.Write(ref *(int*)(scope.Slot +
                    AiTacticalTargetAdapterEmitter.SlotOwnerOffset), 0);
            }
            lock (maskGate) scope.Snapshot.Readers--;
        }

        private void CaptureTacticalEdgeSample(int category, byte* slot,
            int sourceOffset, int targetOffset, int directionOffset)
        {
            if (Interlocked.CompareExchange(ref tacticalEdgeSampleState[category], 1, 0) != 0)
                return;
            tacticalEdgeSampleSource[category] = *(int*)(slot + sourceOffset);
            tacticalEdgeSampleTarget[category] = *(int*)(slot + targetOffset);
            tacticalEdgeSampleDirection[category] = *(int*)(slot + directionOffset);
            Volatile.Write(ref tacticalEdgeSampleState[category], 2);
        }

        private long Complete(QueryScope scope, QueryKind kind, bool hasResultContract, bool success)
        {
            long touched = 0;
            if (scope.Slot != null)
            {
                touched = *(long*)(scope.Slot + DirectionFilterAdapterEmitter.SlotTouchedOffset);
                *(IntPtr*)(scope.Slot + DirectionFilterAdapterEmitter.SlotMaskOffset) = scope.PreviousMask;
                *(long*)(scope.Slot + DirectionFilterAdapterEmitter.SlotTouchedOffset) = scope.PreviousTouched;
                int depth = --(*(int*)(scope.Slot + DirectionFilterAdapterEmitter.SlotDepthOffset));
                if (depth == 0)
                    Volatile.Write(ref *(int*)(scope.Slot + DirectionFilterAdapterEmitter.SlotOwnerOffset), 0);
            }
            lock (maskGate) scope.Snapshot.Readers--;
            if (touched == 0)
            {
                if (kind != QueryKind.CursorPreview)
                    Interlocked.Increment(ref preserved);
                return 0;
            }
            Interlocked.Add(ref rejectedEdges, touched);
            if (kind == QueryKind.CursorPreview)
            {
                Interlocked.Add(ref cursorRejectedEdges, touched);
                return touched;
            }
            if (!hasResultContract)
            {
                switch (kind)
                {
                    case QueryKind.Attack: Interlocked.Add(ref attackEdges, touched); break;
                    case QueryKind.BuildingApproach:
                    case QueryKind.AlternateBuildingApproach:
                        Interlocked.Add(ref buildingEdges, touched); break;
                    case QueryKind.CandidateSearch: Interlocked.Add(ref candidateEdges, touched); break;
                    case QueryKind.CursorCommand: Interlocked.Add(ref cursorCommandEdges, touched); break;
                }
                return touched;
            }
            if (kind == QueryKind.DirectCursor)
                Interlocked.Add(ref directCursorEdges, touched);
            if (!success)
            {
                Interlocked.Increment(ref noRoutes);
                if (kind == QueryKind.AiBuilder) Interlocked.Increment(ref aiNoRoutes);
                return touched;
            }
            Interlocked.Increment(ref detours);
            switch (kind)
            {
                case QueryKind.AiBuilder: Interlocked.Increment(ref aiDetours); break;
                case QueryKind.HumanBuilder: Interlocked.Increment(ref humanDetours); break;
            }
            return touched;
        }

        private readonly struct QueryScope
        {
            internal QueryScope(NativeMaskSnapshot snapshot, byte* slot, IntPtr previousMask,
                long previousTouched, int playerId)
            { Snapshot = snapshot; Slot = slot; PreviousMask = previousMask;
              PreviousTouched = previousTouched; PlayerId = playerId; }
            internal NativeMaskSnapshot Snapshot { get; }
            internal byte* Slot { get; }
            internal IntPtr PreviousMask { get; }
            internal long PreviousTouched { get; }
            internal int PlayerId { get; }
        }

        private readonly struct TacticalQueryScope
        {
            internal TacticalQueryScope(NativeMaskSnapshot snapshot, byte* slot)
            { Snapshot = snapshot; Slot = slot; }
            internal NativeMaskSnapshot Snapshot { get; }
            internal byte* Slot { get; }
        }

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();
        private static void UpdateMaximum(ref long target, long candidate)
        {
            long observed;
            while (candidate > (observed = Interlocked.Read(ref target)) &&
                Interlocked.CompareExchange(ref target, candidate, observed) != observed) { }
        }
        private static long Read(ref long value) => Interlocked.Read(ref value);
        private static void Reset(ref long value) => Interlocked.Exchange(ref value, 0);
    }
}
