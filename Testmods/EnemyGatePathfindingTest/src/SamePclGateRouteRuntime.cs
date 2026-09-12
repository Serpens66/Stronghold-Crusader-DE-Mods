using BepInEx.Logging;
using Iced.Intel;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Context;
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
            long cursorPclChecks, long cursorNativeRefreshes, long cursorCacheHits,
            long cursorThrottleDeferrals, long cursorPolicyBlocked,
            long cursorReachable, long cursorRejectedEdges, long cursorUnitPending,
            long cursorValidationTicks,
            long cursorValidationMaxTicks,
            long missingContexts, long invalidPlayers, long scopeMismatches,
            long slotConflicts, long poolExhaustions, long exceptions)
        {
            Installed = installed; OwnerConflict = ownerConflict; Queries = queries;
            Preserved = preserved; RejectedEdges = rejectedEdges; Detours = detours;
            NoRoutes = noRoutes; HumanDetours = humanDetours; AiDetours = aiDetours;
            AttackEdges = attackEdges; BuildingEdges = buildingEdges;
            CandidateEdges = candidateEdges; CursorCommandEdges = cursorCommandEdges;
            DirectCursorQueries = directCursorQueries; DirectCursorEdges = directCursorEdges;
            CursorPclChecks = cursorPclChecks; CursorNativeRefreshes = cursorNativeRefreshes;
            CursorCacheHits = cursorCacheHits; CursorThrottleDeferrals = cursorThrottleDeferrals;
            CursorPolicyBlocked = cursorPolicyBlocked; CursorReachable = cursorReachable;
            CursorRejectedEdges = cursorRejectedEdges;
            CursorUnitPending = cursorUnitPending; CursorValidationTicks = cursorValidationTicks;
            CursorValidationMaxTicks = cursorValidationMaxTicks;
            MissingContexts = missingContexts; InvalidPlayers = invalidPlayers;
            ScopeMismatches = scopeMismatches; SlotConflicts = slotConflicts;
            PoolExhaustions = poolExhaustions; Exceptions = exceptions;
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
        internal long CursorNativeRefreshes { get; }
        internal long CursorCacheHits { get; }
        internal long CursorThrottleDeferrals { get; }
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
    }

    // Vanilla remains the only route finder. Managed detours merely bind an immutable
    // player mask for the duration of a complete query; eleven native adapters AND that
    // mask into Vanilla's own direction loads without changing the global grid.
    internal sealed unsafe class SamePclGateRouteRuntime
    {
        private const int ThreadSlotStride = 32;
        private const int NativeSnapshotPoolSize = 4;
        private static readonly long CursorRefreshInterval = Math.Max(
            1L, Stopwatch.Frequency / 5L);
        private enum QueryKind
        {
            HumanBuilder, AiBuilder, Attack, BuildingApproach,
            AlternateBuildingApproach, CandidateSearch, CursorCommand, DirectCursor,
            CursorPreview
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
        private readonly IntPtr threadSlots;
        private HookTransaction transaction;
        private readonly ScanRegion region;
        private readonly ulong libraryBase;
        private readonly object maskGate = new object();
        private readonly NativeMaskSnapshot[] maskPool;
        private volatile NativeMaskSnapshot currentMasks = NativeMaskSnapshot.Empty;
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
        private PathBuilderDelegate originalBuilder, rootedBuilder;
        private AttackApproachDelegate originalAttack, rootedAttack;
        private BuildingApproachDelegate originalBuilding, rootedBuilding;
        private BuildingConsumerDelegate originalConsumer, rootedConsumer;
        private BuildingConsumerDelegate originalAlternateConsumer, rootedAlternateConsumer;
        private CursorMoveDelegate originalCursor, rootedCursor;
        private CandidateSearchDelegate originalCandidateSearch, rootedCandidateSearch;
        private readonly DirectTileSearchDelegate originalDirectTileSearch;
        private readonly DirectTileSearchDelegate rootedDirectCursorSearch;
        private readonly HookHandle<X64InlineHook> directCursorHook = new HookHandle<X64InlineHook>();
        private readonly HookHandle<X64InlineHook> cursorPclDecisionHook =
            new HookHandle<X64InlineHook>();
        private readonly HookHandle<X64InlineHook>[] edgeHooks =
            new HookHandle<X64InlineHook>[EnemyGatePathfindingNativeDefinition.DirectionFilterRvas.Length];
        private long queries, preserved, rejectedEdges, detours, noRoutes;
        private long humanDetours, aiDetours, attackEdges, buildingEdges, candidateEdges;
        private long cursorCommandEdges, directCursorQueries, directCursorEdges;
        private long cursorPclChecks, cursorNativeRefreshes, cursorCacheHits;
        private long cursorThrottleDeferrals, cursorPolicyBlocked, cursorReachable,
            cursorRejectedEdges;
        private long cursorUnitPending, cursorValidationTicks, cursorValidationMaxTicks;
        private long missingContexts, invalidPlayers, scopeMismatches;
        private long slotConflicts, poolExhaustions, exceptions;
        private int lastPoolExhaustionGeneration = -1;
        private readonly int[] samplePublished = new int[9];
        private readonly int[] sampleRequested = new int[9];
        private readonly int[] sampleNative = new int[9];
        private readonly int[] sampleTribe = new int[9];
        private readonly int[] sampleUsed = new int[9];
        private int cursorRequestSequence, cursorRequestWriter;
        private int cursorRequestPlayer, cursorRequestUnit, cursorRequestTargetX,
            cursorRequestTargetY, cursorRequestTargetTile;
        private long cursorRequestFingerprint;
        private int cursorCacheSequence;
        private int cursorCacheValid, cursorCachePlayer, cursorCacheUnit,
            cursorCacheGlobal, cursorCacheStartX, cursorCacheStartY,
            cursorCacheTargetX, cursorCacheTargetY, cursorCacheAllowed;
        private long cursorCacheFingerprint;
        private long nextCursorValidationAt;
        private int cursorSampleState, cursorSamplePlayer, cursorSampleUnit,
            cursorSampleGlobal, cursorSampleStartX, cursorSampleStartY,
            cursorSampleTargetX, cursorSampleTargetY, cursorSampleTargetTile,
            cursorSampleAllowed;
        private long cursorSampleFingerprint, cursorSampleRejectedEdges,
            cursorSampleElapsedTicks;
        private long nextPlayerRefresh;
        private int installAttempted;

        internal SamePclGateRouteRuntime(ManualLogSource log, ReadOnlySpan<byte> memory,
            ScanRegion region, ulong libraryBase, bool existingHookOwner)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.region = region;
            this.libraryBase = libraryBase;
            ownerConflict = existingHookOwner;
            maskPool = new NativeMaskSnapshot[existingHookOwner ? 0 : NativeSnapshotPoolSize];
            for (int index = 0; index < maskPool.Length; index++)
                maskPool[index] = new NativeMaskSnapshot(true);
            if (existingHookOwner)
            {
                Shared.DebugLogHelper.LogWarning(log,
                    "Vanilla Gate-Filter suppressed: BugfixesAndQoL_Serp owns overlapping search hooks; " +
                    "hookOwnerConflict=NOT_APPLICABLE.");
                return;
            }
            EnemyGatePathfindingNativeDefinition.ValidateSamePclNativeFilterContracts(memory);
            originalDirectTileSearch = Marshal.GetDelegateForFunctionPointer<DirectTileSearchDelegate>(
                new IntPtr(unchecked((long)(libraryBase +
                    EnemyGatePathfindingNativeDefinition.DirectTileSearchRva))));
            rootedDirectCursorSearch = FilterDirectCursorSearch;
            threadSlots = Marshal.AllocHGlobal(
                DirectionFilterAdapterEmitter.ThreadSlotCount * ThreadSlotStride);
            for (int index = 0;
                 index < DirectionFilterAdapterEmitter.ThreadSlotCount * ThreadSlotStride;
                 index++)
                ((byte*)threadSlots)[index] = 0;
        }

        private void InstallHooks()
        {
            transaction = new HookTransaction(region,
                SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                new HookTransactionOptions { FailureMode = TransactionFailureMode.RollbackAndThrow, OwnsHooks = false });
            builder = AddDetour(EnemyGatePathfindingNativeDefinition.PathBuilderRva,
                rootedBuilder = FilterBuilder, libraryBase);
            attack = AddDetour(EnemyGatePathfindingNativeDefinition.AttackApproachRva,
                rootedAttack = FilterAttack, libraryBase);
            building = AddDetour(EnemyGatePathfindingNativeDefinition.BuildingApproachRva,
                rootedBuilding = FilterBuilding, libraryBase);
            consumer = AddDetour(EnemyGatePathfindingNativeDefinition.BuildingConsumerRva,
                rootedConsumer = FilterConsumer, libraryBase);
            alternateConsumer = AddDetour(
                EnemyGatePathfindingNativeDefinition.AlternateBuildingConsumerRva,
                rootedAlternateConsumer = FilterAlternateConsumer, libraryBase);
            cursor = AddDetour(EnemyGatePathfindingNativeDefinition.CursorMoveStagerRva,
                rootedCursor = FilterCursor, libraryBase);
            candidateSearch = AddDetour(
                EnemyGatePathfindingNativeDefinition.PlayerAwareCandidateSearchRva,
                rootedCandidateSearch = FilterCandidateSearch, libraryBase);
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
            using (var probe = new X64InlineHook(
                libraryBase + EnemyGatePathfindingNativeDefinition.CursorPclDecisionRva,
                EnemyGatePathfindingNativeDefinition.CursorPclDecisionLength))
                if (probe.DisplacedByteCount !=
                    EnemyGatePathfindingNativeDefinition.CursorPclDecisionLength)
                    throw new InvalidOperationException(
                        $"RedBird cursor-PCL span was {probe.DisplacedByteCount}, expected " +
                        EnemyGatePathfindingNativeDefinition.CursorPclDecisionLength + ".");
            transaction.AddContextHook(
                cursorPclDecisionHook,
                HookTarget.FromAddress(libraryBase +
                    EnemyGatePathfindingNativeDefinition.CursorPclDecisionRva),
                FilterNormalCursorPclDecision,
                new ContextHookOptions
                {
                    Registers = X64SmartCPUContextRegs.All,
                    HookSize = EnemyGatePathfindingNativeDefinition.CursorPclDecisionLength,
                    ErrorMode = CallbackErrorMode.LogAndContinue,
                    Placement = OverwrittenInstructionPlacement.BeforeCallback
                });
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
            CommitResult result = transaction.Commit();
            if (!result.IsCompleteSuccess || !builder.Committed || !attack.Committed ||
                !building.Committed || !consumer.Committed || !alternateConsumer.Committed ||
                !cursor.Committed || !candidateSearch.Committed ||
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
            originalBuilder = builder.Handle.Original; originalAttack = attack.Handle.Original;
            originalBuilding = building.Handle.Original; originalConsumer = consumer.Handle.Original;
            originalAlternateConsumer = alternateConsumer.Handle.Original;
            originalCursor = cursor.Handle.Original;
            originalCandidateSearch = candidateSearch.Handle.Original;
            Shared.DebugLogHelper.LogInfo(log,
                "Vanilla player-aware gate filter installed: " +
                "scopes=builder/attack/building/consumer/alternateConsumer/candidateSearch/" +
                "cursorCommand/directCursorDB650/cursorPclPreview, directionAdapters=11, " +
                "cursorPclDecision=" +
                $"0x{EnemyGatePathfindingNativeDefinition.CursorPclDecisionRva:X}/" +
                $"{EnemyGatePathfindingNativeDefinition.CursorPclDecisionLength}, directCursorCallsite=" +
                $"0x{EnemyGatePathfindingNativeDefinition.DirectCursorSearchCallRva:X}, " +
                "nativeSnapshotPool=4, managedNodeCallbacks=0, managedCursorSearches=0, " +
                "managedReplacementSearches=0, cursorRefreshMs=200, representativeUnits=1, " +
                "globalDirectionGridWrites=0, tileBounds=unsigned<" +
                EnemyGatePathfindingNativeDefinition.MaximumTileIdExclusive + ", contracts=[" +
                DirectionFilterAdapterEmitter.DescribeContracts() + "].");
        }

        internal bool Installed => builder != null && builder.Committed;
        private DetourCandidate<T> AddDetour<T>(int rva, T callback, ulong libraryBase) where T : Delegate
        {
            ulong address = libraryBase + unchecked((ulong)rva);
            var candidate = new DetourCandidate<T>(address);
            transaction.AddDetour(candidate.Handle, HookTarget.FromAddress(address), callback);
            return candidate;
        }

        internal void UpdatePolicy(RouteTilePolicySnapshot policy)
        {
            if (ownerConflict || policy == null || policy.NonEmptyPlayerMaskCount == 0)
            {
                lock (maskGate)
                {
                    policyGeneration++;
                    pendingPolicy = null;
                    currentMasks = NativeMaskSnapshot.Empty;
                }
                return;
            }
            try
            {
                if (!Installed && Interlocked.CompareExchange(ref installAttempted, 1, 0) == 0)
                    InstallHooks();
                if (!Installed) return;
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
            ProcessCursorPreview(now);
            if (now < Volatile.Read(ref nextPlayerRefresh)) return;
            Volatile.Write(ref nextPlayerRefresh, now + Stopwatch.Frequency);
            var ai = new bool[9];
            for (int player = 1; player <= 8; player++)
                ai[player] = GamePlayerManagerAPI.Instance.IsAIPlayer(player);
            playerKinds = new PlayerKindSnapshot(ai);
            Span<GameTribe> tribes = GameTribeManagerAPI.Instance.GetTribeAsSpan();
            var owners = new int[tribes.Length + 1];
            for (int spanIndex = 0; spanIndex < tribes.Length; spanIndex++)
                owners[spanIndex + 1] = tribes[spanIndex].r_PlayerIdOwner;
            tribePlayers = new TribePlayerSnapshot(owners);
        }

        private void FilterNormalCursorPclDecision(
            NativePointer<X64SmartCPUContext> context)
        {
            X64SmartCPUContext* registers = context.Pointer;
            if (registers == null) return;

            // RedBird 1.1.0 changes flags while building the callback context. The
            // displaced TEST EAX,EAX is therefore reconstructed from its operand.
            bool originalZero = unchecked((int)(uint)registers->RAX) == 0;
            bool finalZero = originalZero;
            try
            {
                if (!originalZero &&
                    unchecked((int)(uint)registers->RSI) ==
                    unchecked((int)(uint)registers->RDI))
                {
                    Interlocked.Increment(ref cursorPclChecks);
                    int player = *(int*)(libraryBase +
                        EnemyGatePathfindingNativeDefinition.ActivePlayerIdRva);
                    int unitId = unchecked((int)(uint)registers->R14);
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
                        PublishCursorRequest(player, unitId, targetX, targetY,
                            tileAfter, snapshot.Fingerprint);
                        bool allowed;
                        if (TryReadCursorCache(player, unitId, targetX, targetY,
                                snapshot.Fingerprint, out allowed))
                        {
                            Interlocked.Increment(ref cursorCacheHits);
                            if (!allowed)
                            {
                                finalZero = true;
                                Interlocked.Increment(ref cursorPolicyBlocked);
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
                finalZero = originalZero;
            }
            registers->Rflags = EnemyGatePathfindingPolicy.SetZeroFlag(
                registers->Rflags, finalZero);
        }

        private void PublishCursorRequest(int player, int unitId, int targetX,
            int targetY, int targetTile, ulong fingerprint)
        {
            if (Interlocked.CompareExchange(ref cursorRequestWriter, 1, 0) != 0)
                return;
            try
            {
                Interlocked.Increment(ref cursorRequestSequence);
                cursorRequestPlayer = player;
                cursorRequestUnit = unitId;
                cursorRequestTargetX = targetX;
                cursorRequestTargetY = targetY;
                cursorRequestTargetTile = targetTile;
                cursorRequestFingerprint = unchecked((long)fingerprint);
                Interlocked.Increment(ref cursorRequestSequence);
            }
            finally
            {
                Volatile.Write(ref cursorRequestWriter, 0);
            }
        }

        private bool TryReadCursorRequest(out int player, out int unitId,
            out int targetX, out int targetY, out int targetTile, out ulong fingerprint)
        {
            player = unitId = targetX = targetY = targetTile = 0;
            fingerprint = 0;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                int before = Volatile.Read(ref cursorRequestSequence);
                if ((before & 1) != 0 || before == 0) continue;
                player = cursorRequestPlayer;
                unitId = cursorRequestUnit;
                targetX = cursorRequestTargetX;
                targetY = cursorRequestTargetY;
                targetTile = cursorRequestTargetTile;
                fingerprint = unchecked((ulong)cursorRequestFingerprint);
                if (before == Volatile.Read(ref cursorRequestSequence)) return true;
            }
            return false;
        }

        private bool TryReadCursorCache(int player, int unitId, int targetX,
            int targetY, ulong fingerprint, out bool allowed)
        {
            allowed = true;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                int before = Volatile.Read(ref cursorCacheSequence);
                if ((before & 1) != 0 || before == 0) continue;
                int valid = cursorCacheValid;
                int cachedPlayer = cursorCachePlayer;
                int cachedUnit = cursorCacheUnit;
                int cachedTargetX = cursorCacheTargetX;
                int cachedTargetY = cursorCacheTargetY;
                ulong cachedFingerprint = unchecked((ulong)cursorCacheFingerprint);
                int cachedAllowed = cursorCacheAllowed;
                if (before != Volatile.Read(ref cursorCacheSequence)) continue;
                if (!EnemyGatePathfindingPolicy.CursorPreviewCacheMatches(
                        valid != 0, cachedPlayer, cachedUnit, cachedTargetX,
                        cachedTargetY, cachedFingerprint, player, unitId,
                        targetX, targetY, fingerprint)) return false;
                allowed = cachedAllowed != 0;
                return true;
            }
            return false;
        }

        private void ProcessCursorPreview(long now)
        {
            int player, unitId, targetX, targetY, targetTile;
            ulong fingerprint;
            if (!Installed || !TryReadCursorRequest(out player, out unitId,
                    out targetX, out targetY, out targetTile, out fingerprint)) return;
            if (now < Volatile.Read(ref nextCursorValidationAt))
            {
                Interlocked.Increment(ref cursorThrottleDeferrals);
                return;
            }
            Volatile.Write(ref nextCursorValidationAt, now + CursorRefreshInterval);

            NativeMaskSnapshot snapshot = currentMasks;
            if (player <= 0 || player > 8 || fingerprint == 0 ||
                snapshot.Fingerprint != fingerprint ||
                snapshot.PlayerMasks[player] == IntPtr.Zero) return;
            if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
                unit == null || unit->r_AliveState != AliveState.IsAlive ||
                unit->r_ControllableForPlayerId != player)
            {
                Interlocked.Increment(ref cursorUnitPending);
                return;
            }
            int startX = unit->r_CurrentTilePositionX;
            int startY = unit->r_CurrentTilePositionY;
            if (startX >= 800 || startY >= 800 ||
                targetX < 0 || targetX >= 800 || targetY < 0 || targetY >= 800 ||
                targetTile < 0 ||
                targetTile >= EnemyGatePathfindingNativeDefinition.MaximumTileIdExclusive ||
                *(int*)(libraryBase + EnemyGatePathfindingNativeDefinition.TileRowStartRva +
                    unchecked((ulong)(targetY * 12))) + targetX != targetTile) return;

            long started = Stopwatch.GetTimestamp();
            int result = 0;
            bool completed = false;
            long touched = 0;
            CaptureScopeSample(QueryKind.CursorPreview, player, player, -1, player);
            QueryScope scope = Enter(player);
            if (scope.Snapshot.Fingerprint != fingerprint)
            {
                Complete(scope, QueryKind.CursorPreview, true, false);
                return;
            }
            try
            {
                result = originalDirectTileSearch(
                    new IntPtr(unchecked((long)(libraryBase +
                        EnemyGatePathfindingNativeDefinition.NativePathManagerRva))),
                    startX, startY, targetX, targetY,
                    EnemyGatePathfindingNativeDefinition.DirectCursorSearchNodeLimit);
                completed = true;
            }
            catch
            {
                Interlocked.Increment(ref exceptions);
                return;
            }
            finally
            {
                touched = Complete(scope, QueryKind.CursorPreview, true,
                    completed && result > 0);
            }
            long elapsed = Stopwatch.GetTimestamp() - started;
            Interlocked.Increment(ref cursorNativeRefreshes);
            Interlocked.Add(ref cursorValidationTicks, elapsed);
            UpdateMaximum(ref cursorValidationMaxTicks, elapsed);
            if (result > 0) Interlocked.Increment(ref cursorReachable);
            bool allowed = !completed ||
                !EnemyGatePathfindingPolicy.ShouldBlockCursorPreview(result, touched);

            Interlocked.Increment(ref cursorCacheSequence);
            cursorCachePlayer = player;
            cursorCacheUnit = unitId;
            cursorCacheGlobal = unchecked((int)unit->r_GlobalId);
            cursorCacheStartX = startX;
            cursorCacheStartY = startY;
            cursorCacheTargetX = targetX;
            cursorCacheTargetY = targetY;
            cursorCacheFingerprint = unchecked((long)fingerprint);
            cursorCacheAllowed = allowed ? 1 : 0;
            cursorCacheValid = 1;
            Interlocked.Increment(ref cursorCacheSequence);

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
                cursorSampleAllowed = allowed ? 1 : 0;
                Volatile.Write(ref cursorSampleState, 2);
            }
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
                Read(ref cursorPclChecks), Read(ref cursorNativeRefreshes),
                Read(ref cursorCacheHits), Read(ref cursorThrottleDeferrals),
                Read(ref cursorPolicyBlocked), Read(ref cursorReachable),
                Read(ref cursorRejectedEdges), Read(ref cursorUnitPending),
                Read(ref cursorValidationTicks),
                Read(ref cursorValidationMaxTicks),
                Read(ref missingContexts), Read(ref invalidPlayers), Read(ref scopeMismatches),
                Read(ref slotConflicts), Read(ref poolExhaustions), Read(ref exceptions));
        internal void ResetCounters()
        {
            Reset(ref queries); Reset(ref preserved); Reset(ref rejectedEdges); Reset(ref detours);
            Reset(ref noRoutes); Reset(ref humanDetours); Reset(ref aiDetours);
            Reset(ref attackEdges); Reset(ref buildingEdges); Reset(ref candidateEdges);
            Reset(ref cursorCommandEdges); Reset(ref directCursorQueries); Reset(ref directCursorEdges);
            Reset(ref cursorPclChecks); Reset(ref cursorNativeRefreshes); Reset(ref cursorCacheHits);
            Reset(ref cursorThrottleDeferrals); Reset(ref cursorPolicyBlocked);
            Reset(ref cursorReachable); Reset(ref cursorRejectedEdges);
            Reset(ref cursorUnitPending);
            Reset(ref cursorValidationTicks); Reset(ref cursorValidationMaxTicks);
            Reset(ref missingContexts); Reset(ref invalidPlayers);
            Reset(ref scopeMismatches); Reset(ref slotConflicts); Reset(ref poolExhaustions);
            Reset(ref exceptions);
            Volatile.Write(ref cursorRequestSequence, 0);
            Volatile.Write(ref cursorRequestWriter, 0);
            Volatile.Write(ref cursorCacheSequence, 0);
            Volatile.Write(ref cursorCacheValid, 0);
            Volatile.Write(ref cursorSampleState, 0);
            Volatile.Write(ref nextCursorValidationAt, 0);
            for (int index = 0; index < samplePublished.Length; index++)
                Volatile.Write(ref samplePublished[index], 0);
        }

        private int FilterBuilder(IntPtr manager, int player, int profile)
        {
            QueryKind kind = playerKinds.IsAi(player) ? QueryKind.AiBuilder : QueryKind.HumanBuilder;
            CaptureScopeSample(kind, player, player, -1, player);
            QueryScope scope = Enter(player); int result = 0; bool completed = false;
            try { result = originalBuilder(manager, player, profile); completed = true; return result; }
            catch { Interlocked.Increment(ref exceptions); throw; }
            finally { Complete(scope, kind, true, completed && result > 0); }
        }
        private void FilterAttack(IntPtr manager, int unused2, int unused3, uint x, uint y,
            int count, int targetPcl, int player)
        {
            CaptureScopeSample(QueryKind.Attack, player, player, -1, player);
            QueryScope scope = Enter(player);
            try { originalAttack(manager, unused2, unused3, x, y, count, targetPcl, player); }
            catch { Interlocked.Increment(ref exceptions); throw; }
            finally { Complete(scope, QueryKind.Attack, false, false); }
        }
        private void FilterBuilding(IntPtr manager, int tribe, int buildingId,
            int count, int targetPcl, int player)
        {
            int tribePlayer = ResolveTribePlayer(tribe);
            int used = ValidateExplicitPlayer(player, tribePlayer);
            CaptureScopeSample(QueryKind.BuildingApproach, player, player, tribePlayer, used);
            QueryScope scope = Enter(used);
            try { originalBuilding(manager, tribe, buildingId, count, targetPcl, player); }
            catch { Interlocked.Increment(ref exceptions); throw; }
            finally { Complete(scope, QueryKind.BuildingApproach, false, false); }
        }
        private void FilterConsumer(IntPtr tribeManager, int tribe, int variant)
        {
            int player = ResolveTribePlayer(tribe);
            CaptureScopeSample(QueryKind.BuildingApproach, tribe, -1, player, player);
            QueryScope scope = Enter(player);
            try { originalConsumer(tribeManager, tribe, variant); }
            catch { Interlocked.Increment(ref exceptions); throw; }
            finally { Complete(scope, QueryKind.BuildingApproach, false, false); }
        }
        private void FilterAlternateConsumer(IntPtr tribeManager, int tribe, int variant)
        {
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
            CaptureScopeSample(QueryKind.CandidateSearch, player, player, -1, player);
            QueryScope scope = Enter(player);
            try { originalCandidateSearch(manager, startTileId, count,
                    targetPcl, player, mode, candidateClass); }
            catch { Interlocked.Increment(ref exceptions); throw; }
            finally { Complete(scope, QueryKind.CandidateSearch, false, false); }
        }
        private void FilterCursor(IntPtr manager, int tribe, int x, int y, int context, int flags)
        {
            int nativePlayer = *(int*)(libraryBase +
                EnemyGatePathfindingNativeDefinition.ActivePlayerIdRva);
            int tribePlayer = ResolveTribePlayer(tribe);
            int player = ValidateExplicitPlayer(nativePlayer, tribePlayer);
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

        private int ValidateExplicitPlayer(int nativePlayer, int tribePlayer)
        {
            if (nativePlayer <= 0 || nativePlayer > 8 || tribePlayer <= 0 || tribePlayer > 8)
                return -1;
            if (nativePlayer != tribePlayer)
            {
                Interlocked.Increment(ref scopeMismatches);
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
                ",allowed=" + (cursorSampleAllowed != 0) +
                ",elapsedTicks=" + cursorSampleElapsedTicks;
        }

        private QueryScope Enter(int player)
        {
            Interlocked.Increment(ref queries);
            NativeMaskSnapshot snapshot;
            IntPtr mask;
            lock (maskGate)
            {
                snapshot = currentMasks;
                snapshot.Readers++;
                mask = player > 0 && player < snapshot.PlayerMasks.Length
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
                Interlocked.Increment(ref slotConflicts); return new QueryScope(snapshot, null, IntPtr.Zero, 0);
            }
            if (owner == 0 && Interlocked.CompareExchange(
                    ref *(int*)(slot + DirectionFilterAdapterEmitter.SlotOwnerOffset), unchecked((int)thread), 0) != 0)
            {
                Interlocked.Increment(ref slotConflicts); return new QueryScope(snapshot, null, IntPtr.Zero, 0);
            }
            IntPtr previous = *(IntPtr*)(slot + DirectionFilterAdapterEmitter.SlotMaskOffset);
            long previousTouched = *(long*)(slot + DirectionFilterAdapterEmitter.SlotTouchedOffset);
            (*(int*)(slot + DirectionFilterAdapterEmitter.SlotDepthOffset))++;
            *(IntPtr*)(slot + DirectionFilterAdapterEmitter.SlotMaskOffset) = mask;
            *(long*)(slot + DirectionFilterAdapterEmitter.SlotTouchedOffset) = 0;
            return new QueryScope(snapshot, slot, previous, previousTouched);
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
            if (!success) { Interlocked.Increment(ref noRoutes); return touched; }
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
            internal QueryScope(NativeMaskSnapshot snapshot, byte* slot, IntPtr previousMask, long previousTouched)
            { Snapshot = snapshot; Slot = slot; PreviousMask = previousMask; PreviousTouched = previousTouched; }
            internal NativeMaskSnapshot Snapshot { get; }
            internal byte* Slot { get; }
            internal IntPtr PreviousMask { get; }
            internal long PreviousTouched { get; }
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
