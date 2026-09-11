using BepInEx.Logging;
using Iced.Intel;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API;
using SHCDESE.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace EnemyGatePathfindingTest
{
    internal readonly struct SamePclCoverageSnapshot
    {
        internal SamePclCoverageSnapshot(bool installed, bool ownerConflict, long queries,
            long preserved, long rejectedEdges, long detours, long noRoutes,
            long humanDetours, long aiDetours, long attackDetours,
            long buildingDetours, long cursorDetours, long missingContexts,
            long slotConflicts, long exceptions, long elapsedTicks)
        {
            Installed = installed; OwnerConflict = ownerConflict; Queries = queries;
            Preserved = preserved; RejectedEdges = rejectedEdges; Detours = detours;
            NoRoutes = noRoutes; HumanDetours = humanDetours; AiDetours = aiDetours;
            AttackDetours = attackDetours; BuildingDetours = buildingDetours;
            CursorDetours = cursorDetours; MissingContexts = missingContexts;
            SlotConflicts = slotConflicts; Exceptions = exceptions; ElapsedTicks = elapsedTicks;
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
        internal long AttackDetours { get; }
        internal long BuildingDetours { get; }
        internal long CursorDetours { get; }
        internal long MissingContexts { get; }
        internal long SlotConflicts { get; }
        internal long Exceptions { get; }
        internal long ElapsedTicks { get; }
    }

    // Vanilla remains the only route finder. Managed detours merely bind an immutable
    // player mask for the duration of a complete query; ten native adapters AND that
    // mask into Vanilla's own direction loads without changing the global grid.
    internal sealed unsafe class SamePclGateRouteRuntime
    {
        private const int ThreadSlotStride = 32;
        private enum QueryKind { HumanBuilder, AiBuilder, Attack, BuildingApproach, Cursor }

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

        private sealed class NativeMaskSnapshot
        {
            internal static readonly NativeMaskSnapshot Empty = new NativeMaskSnapshot();
            internal readonly IntPtr[] PlayerMasks = new IntPtr[9];
            internal int Readers;
            private NativeMaskSnapshot() { }
            internal NativeMaskSnapshot(RouteTilePolicySnapshot source)
            {
                try
                {
                    for (int player = 1; player <= 8; player++)
                    {
                        byte[] mask = source?.DirectionMasks != null &&
                            player < source.DirectionMasks.Length ? source.DirectionMasks[player] : null;
                        if (mask == null || mask.Length !=
                            EnemyGatePathfindingNativeDefinition.MaximumTileIdExclusive) continue;
                        IntPtr native = Marshal.AllocHGlobal(mask.Length);
                        Marshal.Copy(mask, 0, native, mask.Length);
                        PlayerMasks[player] = native;
                    }
                }
                catch { Release(); throw; }
            }
            internal void Release()
            {
                for (int player = 1; player <= 8; player++)
                {
                    if (PlayerMasks[player] == IntPtr.Zero) continue;
                    Marshal.FreeHGlobal(PlayerMasks[player]); PlayerMasks[player] = IntPtr.Zero;
                }
            }
        }

        private readonly ManualLogSource log;
        private readonly IntPtr threadSlots;
        private HookTransaction transaction;
        private readonly ScanRegion region;
        private readonly ulong libraryBase;
        private readonly List<NativeMaskSnapshot> retired = new List<NativeMaskSnapshot>();
        private volatile NativeMaskSnapshot currentMasks = NativeMaskSnapshot.Empty;
        private volatile PlayerKindSnapshot playerKinds = PlayerKindSnapshot.Empty;
        private readonly bool ownerConflict;
        private DetourCandidate<PathBuilderDelegate> builder;
        private DetourCandidate<AttackApproachDelegate> attack;
        private DetourCandidate<BuildingApproachDelegate> building;
        private DetourCandidate<BuildingConsumerDelegate> consumer;
        private DetourCandidate<CursorMoveDelegate> cursor;
        private PathBuilderDelegate originalBuilder, rootedBuilder;
        private AttackApproachDelegate originalAttack, rootedAttack;
        private BuildingApproachDelegate originalBuilding, rootedBuilding;
        private BuildingConsumerDelegate originalConsumer, rootedConsumer;
        private CursorMoveDelegate originalCursor, rootedCursor;
        private readonly HookHandle<X64InlineHook>[] edgeHooks = new HookHandle<X64InlineHook>[10];
        private long queries, preserved, rejectedEdges, detours, noRoutes;
        private long humanDetours, aiDetours, attackDetours, buildingDetours, cursorDetours;
        private long missingContexts, slotConflicts, exceptions, elapsedTicks;
        private long nextPlayerRefresh;
        private int installAttempted;

        internal SamePclGateRouteRuntime(ManualLogSource log, ReadOnlySpan<byte> memory,
            ScanRegion region, ulong libraryBase, bool existingHookOwner)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.region = region;
            this.libraryBase = libraryBase;
            ownerConflict = existingHookOwner;
            if (existingHookOwner)
            {
                Shared.DebugLogHelper.LogWarning(log,
                    "Vanilla Gate-Filter suppressed: BugfixesAndQoL_Serp owns overlapping search hooks; " +
                    "hookOwnerConflict=NOT_APPLICABLE.");
                return;
            }
            EnemyGatePathfindingNativeDefinition.ValidateSamePclNativeFilterContracts(memory);
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
            cursor = AddDetour(EnemyGatePathfindingNativeDefinition.CursorMoveStagerRva,
                rootedCursor = FilterCursor, libraryBase);
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
                !building.Committed || !consumer.Committed || !cursor.Committed)
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
            originalCursor = cursor.Handle.Original;
            Shared.DebugLogHelper.LogInfo(log,
                "Vanilla player-aware gate filter installed: scopes=builder/attack/building/consumer/cursor, " +
                "directionAdapters=10, managedNodeCallbacks=0, managedReplacementSearches=0, " +
                "globalDirectionGridWrites=0.");
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
                NativeMaskSnapshot cleared = Interlocked.Exchange(
                    ref currentMasks, NativeMaskSnapshot.Empty);
                if (cleared != null && cleared != NativeMaskSnapshot.Empty)
                    lock (retired) retired.Add(cleared);
                return;
            }
            NativeMaskSnapshot next = null;
            try
            {
                next = new NativeMaskSnapshot(policy);
                if (!Installed && Interlocked.CompareExchange(ref installAttempted, 1, 0) == 0)
                    InstallHooks();
                if (!Installed)
                {
                    next.Release();
                    return;
                }
            }
            catch (Exception ex)
            {
                next?.Release();
                Interlocked.Increment(ref exceptions);
                Shared.DebugLogHelper.LogWarning(log,
                    $"Native gate-mask publication failed open: {ex.GetType().Name}: {ex.Message}");
                return;
            }
            NativeMaskSnapshot previous = Interlocked.Exchange(ref currentMasks, next);
            if (previous != null && previous != NativeMaskSnapshot.Empty && previous != next)
                lock (retired) retired.Add(previous);
        }

        internal void ProcessDeferred()
        {
            lock (retired)
            {
                for (int index = retired.Count - 1; index >= 0; index--)
                {
                    if (Volatile.Read(ref retired[index].Readers) != 0) continue;
                    retired[index].Release(); retired.RemoveAt(index);
                }
            }
            long now = Stopwatch.GetTimestamp();
            if (now < Volatile.Read(ref nextPlayerRefresh)) return;
            Volatile.Write(ref nextPlayerRefresh, now + Stopwatch.Frequency);
            var ai = new bool[9];
            for (int player = 1; player <= 8; player++)
                ai[player] = GamePlayerManagerAPI.Instance.IsAIPlayer(player);
            playerKinds = new PlayerKindSnapshot(ai);
        }

        internal SamePclCoverageSnapshot GetCoverageSnapshot() =>
            new SamePclCoverageSnapshot(Installed, ownerConflict, Read(ref queries),
                Read(ref preserved), Read(ref rejectedEdges), Read(ref detours), Read(ref noRoutes),
                Read(ref humanDetours), Read(ref aiDetours), Read(ref attackDetours),
                Read(ref buildingDetours), Read(ref cursorDetours), Read(ref missingContexts),
                Read(ref slotConflicts), Read(ref exceptions), Read(ref elapsedTicks));
        internal void ResetCounters()
        {
            Reset(ref queries); Reset(ref preserved); Reset(ref rejectedEdges); Reset(ref detours);
            Reset(ref noRoutes); Reset(ref humanDetours); Reset(ref aiDetours);
            Reset(ref attackDetours); Reset(ref buildingDetours); Reset(ref cursorDetours);
            Reset(ref missingContexts); Reset(ref slotConflicts); Reset(ref exceptions); Reset(ref elapsedTicks);
        }

        private int FilterBuilder(IntPtr manager, int player, int profile)
        {
            QueryKind kind = playerKinds.IsAi(player) ? QueryKind.AiBuilder : QueryKind.HumanBuilder;
            return RunInt(player, kind, () => originalBuilder(manager, player, profile));
        }
        private void FilterAttack(IntPtr manager, int tribe, int context, uint x, uint y,
            int count, int region, int movement) => RunVoid(ResolveTribePlayer(tribe), QueryKind.Attack,
                () => originalAttack(manager, tribe, context, x, y, count, region, movement));
        private void FilterBuilding(IntPtr manager, int tribe, int buildingId,
            int count, int region, int movement) => RunVoid(ResolveTribePlayer(tribe), QueryKind.BuildingApproach,
                () => originalBuilding(manager, tribe, buildingId, count, region, movement));
        private void FilterConsumer(IntPtr tribeManager, int tribe, int variant) =>
            RunVoid(ResolveTribePlayer(tribe), QueryKind.BuildingApproach,
                () => originalConsumer(tribeManager, tribe, variant));
        private void FilterCursor(IntPtr manager, int tribe, int x, int y, int context, int flags)
        {
            int player = GamePlayerManagerAPI.Instance?.GetLocalPlayerId() ?? -1;
            RunVoid(player, QueryKind.Cursor, () => originalCursor(manager, tribe, x, y, context, flags));
        }
        private int ResolveTribePlayer(int tribeId) => GameTribeManagerAPI.Instance != null &&
            GameTribeManagerAPI.Instance.TryGetTribeById(tribeId, out GameTribe* tribe) && tribe != null
                ? tribe->r_PlayerIdOwner : -1;

        private int RunInt(int player, QueryKind kind, Func<int> query)
        {
            QueryScope scope = Enter(player); long start = Stopwatch.GetTimestamp();
            int result = 0; bool completed = false;
            try { result = query(); completed = true; return result; }
            catch { Interlocked.Increment(ref exceptions); throw; }
            finally
            {
                Interlocked.Add(ref elapsedTicks, Stopwatch.GetTimestamp() - start);
                Complete(scope, kind, completed && result > 0);
            }
        }
        private void RunVoid(int player, QueryKind kind, Action query)
        {
            QueryScope scope = Enter(player); long start = Stopwatch.GetTimestamp(); bool completed = false;
            try { query(); completed = true; }
            catch { Interlocked.Increment(ref exceptions); throw; }
            finally
            {
                Interlocked.Add(ref elapsedTicks, Stopwatch.GetTimestamp() - start);
                Complete(scope, kind, completed);
            }
        }

        private QueryScope Enter(int player)
        {
            Interlocked.Increment(ref queries); NativeMaskSnapshot snapshot;
            do
            {
                snapshot = currentMasks; Interlocked.Increment(ref snapshot.Readers);
                if (ReferenceEquals(snapshot, currentMasks)) break;
                Interlocked.Decrement(ref snapshot.Readers);
            } while (true);
            IntPtr mask = player > 0 && player < snapshot.PlayerMasks.Length
                ? snapshot.PlayerMasks[player] : IntPtr.Zero;
            if (player <= 0 || player >= snapshot.PlayerMasks.Length)
                Interlocked.Increment(ref missingContexts);
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
            *(IntPtr*)(slot + DirectionFilterAdapterEmitter.SlotMaskOffset) = mask;
            *(long*)(slot + DirectionFilterAdapterEmitter.SlotTouchedOffset) = 0;
            return new QueryScope(snapshot, slot, previous, previousTouched);
        }

        private void Complete(QueryScope scope, QueryKind kind, bool success)
        {
            long touched = 0;
            if (scope.Slot != null)
            {
                touched = *(long*)(scope.Slot + DirectionFilterAdapterEmitter.SlotTouchedOffset);
                *(IntPtr*)(scope.Slot + DirectionFilterAdapterEmitter.SlotMaskOffset) = scope.PreviousMask;
                *(long*)(scope.Slot + DirectionFilterAdapterEmitter.SlotTouchedOffset) = scope.PreviousTouched;
                if (scope.PreviousMask == IntPtr.Zero)
                    Volatile.Write(ref *(int*)(scope.Slot + DirectionFilterAdapterEmitter.SlotOwnerOffset), 0);
            }
            Interlocked.Decrement(ref scope.Snapshot.Readers);
            if (touched == 0) { Interlocked.Increment(ref preserved); return; }
            Interlocked.Add(ref rejectedEdges, touched);
            if (!success) { Interlocked.Increment(ref noRoutes); return; }
            Interlocked.Increment(ref detours);
            switch (kind)
            {
                case QueryKind.AiBuilder: Interlocked.Increment(ref aiDetours); break;
                case QueryKind.HumanBuilder: Interlocked.Increment(ref humanDetours); break;
                case QueryKind.Attack: Interlocked.Increment(ref attackDetours); break;
                case QueryKind.BuildingApproach: Interlocked.Increment(ref buildingDetours); break;
                case QueryKind.Cursor: Interlocked.Increment(ref cursorDetours); break;
            }
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
        private static long Read(ref long value) => Interlocked.Read(ref value);
        private static void Reset(ref long value) => Interlocked.Exchange(ref value, 0);
    }
}
