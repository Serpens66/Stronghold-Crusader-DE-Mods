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
using static Iced.Intel.AssemblerRegisters;

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
        private const int ThreadSlotCount = 64;
        private const int ThreadSlotStride = 32;
        private const int SlotOwnerOffset = 0;
        private const int SlotMaskOffset = 8;
        private const int SlotTouchedOffset = 16;
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
        private readonly HookTransaction transaction;
        private readonly List<NativeMaskSnapshot> retired = new List<NativeMaskSnapshot>();
        private volatile NativeMaskSnapshot currentMasks = NativeMaskSnapshot.Empty;
        private volatile PlayerKindSnapshot playerKinds = PlayerKindSnapshot.Empty;
        private readonly bool ownerConflict;
        private readonly DetourCandidate<PathBuilderDelegate> builder;
        private readonly DetourCandidate<AttackApproachDelegate> attack;
        private readonly DetourCandidate<BuildingApproachDelegate> building;
        private readonly DetourCandidate<BuildingConsumerDelegate> consumer;
        private readonly DetourCandidate<CursorMoveDelegate> cursor;
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

        internal SamePclGateRouteRuntime(ManualLogSource log, ReadOnlySpan<byte> memory,
            ScanRegion region, ulong libraryBase, bool existingHookOwner)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            ownerConflict = existingHookOwner;
            if (existingHookOwner)
            {
                Shared.DebugLogHelper.LogWarning(log,
                    "Vanilla Gate-Filter suppressed: BugfixesAndQoL_Serp owns overlapping search hooks; " +
                    "hookOwnerConflict=NOT_APPLICABLE.");
                return;
            }
            EnemyGatePathfindingNativeDefinition.ValidateSamePclNativeFilterContracts(memory);
            threadSlots = Marshal.AllocHGlobal(ThreadSlotCount * ThreadSlotStride);
            for (int index = 0; index < ThreadSlotCount * ThreadSlotStride; index++)
                ((byte*)threadSlots)[index] = 0;
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
                using (var probe = new X64InlineHook(libraryBase + unchecked((ulong)rva), 14))
                    if (probe.DisplacedByteCount != length)
                        throw new InvalidOperationException(
                            $"RedBird direction-filter span {index} was {probe.DisplacedByteCount}, expected {length}.");
                transaction.AddInline(edgeHooks[index],
                    HookTarget.FromAddress(libraryBase + unchecked((ulong)rva)),
                    (asm, original, returnAddress) => EmitDirectionFilter(
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
            NativeMaskSnapshot next;
            try { next = policy == null || policy.TopologyFingerprint == 0
                    ? NativeMaskSnapshot.Empty : new NativeMaskSnapshot(policy); }
            catch (Exception ex)
            {
                Interlocked.Increment(ref exceptions);
                Shared.DebugLogHelper.LogWarning(log,
                    $"Native gate-mask publication failed open: {ex.GetType().Name}: {ex.Message}");
                next = NativeMaskSnapshot.Empty;
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
            byte* slot = (byte*)threadSlots + ((thread & (ThreadSlotCount - 1)) * ThreadSlotStride);
            int owner = Volatile.Read(ref *(int*)(slot + SlotOwnerOffset));
            if (owner != 0 && owner != unchecked((int)thread))
            {
                Interlocked.Increment(ref slotConflicts); return new QueryScope(snapshot, null, IntPtr.Zero, 0);
            }
            if (owner == 0 && Interlocked.CompareExchange(
                    ref *(int*)(slot + SlotOwnerOffset), unchecked((int)thread), 0) != 0)
            {
                Interlocked.Increment(ref slotConflicts); return new QueryScope(snapshot, null, IntPtr.Zero, 0);
            }
            IntPtr previous = *(IntPtr*)(slot + SlotMaskOffset);
            long previousTouched = *(long*)(slot + SlotTouchedOffset);
            *(IntPtr*)(slot + SlotMaskOffset) = mask; *(long*)(slot + SlotTouchedOffset) = 0;
            return new QueryScope(snapshot, slot, previous, previousTouched);
        }

        private void Complete(QueryScope scope, QueryKind kind, bool success)
        {
            long touched = 0;
            if (scope.Slot != null)
            {
                touched = *(long*)(scope.Slot + SlotTouchedOffset);
                *(IntPtr*)(scope.Slot + SlotMaskOffset) = scope.PreviousMask;
                *(long*)(scope.Slot + SlotTouchedOffset) = scope.PreviousTouched;
                if (scope.PreviousMask == IntPtr.Zero)
                    Volatile.Write(ref *(int*)(scope.Slot + SlotOwnerOffset), 0);
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

        private static void EmitDirectionFilter(Assembler asm, ReadOnlySpan<Instruction> original,
            int site, ulong slots)
        {
            if (original.Length < 1) throw new InvalidOperationException("Empty direction-filter span.");
            if (site == 1 || site == 2) EmitR11Destination(asm, original, slots);
            else if (site == 4 || site == 5) EmitRcxDestination(asm, original, slots, site == 4);
            else EmitRaxDestination(asm, original, slots, site);
        }
        private static void EmitRaxDestination(Assembler asm, ReadOnlySpan<Instruction> original,
            ulong slots, int site)
        {
            Label vanilla = asm.CreateLabel(), done = asm.CreateLabel(), unchanged = asm.CreateLabel();
            asm.push(r8); asm.push(r9); asm.push(r11);
            asm.mov(r9d, __dword_ptr.gs[0x48]); asm.mov(r11d, r9d); asm.and(r11d, ThreadSlotCount - 1);
            asm.shl(r11, 5); asm.mov(r9, slots); asm.add(r11, r9); asm.mov(r9d, __dword_ptr.gs[0x48]);
            asm.cmp(__dword_ptr[r11 + SlotOwnerOffset], r9d); asm.jne(vanilla);
            asm.mov(r9, __qword_ptr[r11 + SlotMaskOffset]); asm.test(r9, r9); asm.je(vanilla);
            if (site == 0) asm.add(r9, rdx); else if (site == 3) asm.add(r9, rbx); else asm.add(r9, rdi);
            asm.AddInstruction(original[0]); asm.mov(r8b, al); asm.and(al, __byte_ptr[r9]);
            asm.cmp(al, r8b); asm.je(unchanged); asm.inc(__qword_ptr[r11 + SlotTouchedOffset]);
            asm.Label(ref unchanged); asm.pop(r11); asm.pop(r9); asm.pop(r8);
            for (int index = 1; index < original.Length; index++) asm.AddInstruction(original[index]);
            asm.jmp(done); asm.Label(ref vanilla); asm.pop(r11); asm.pop(r9); asm.pop(r8);
            for (int index = 0; index < original.Length; index++) asm.AddInstruction(original[index]);
            asm.Label(ref done);
        }

        private static void EmitR11Destination(Assembler asm, ReadOnlySpan<Instruction> original, ulong slots)
        {
            Label vanilla = asm.CreateLabel(), done = asm.CreateLabel(), unchanged = asm.CreateLabel();
            asm.push(rax); asm.push(r9); asm.push(r10);
            asm.mov(eax, __dword_ptr.gs[0x48]); asm.mov(r10d, eax); asm.and(r10d, ThreadSlotCount - 1);
            asm.shl(r10, 5); asm.mov(rax, slots); asm.add(r10, rax); asm.mov(eax, __dword_ptr.gs[0x48]);
            asm.cmp(__dword_ptr[r10 + SlotOwnerOffset], eax); asm.jne(vanilla);
            asm.mov(rax, __qword_ptr[r10 + SlotMaskOffset]); asm.test(rax, rax); asm.je(vanilla);
            asm.add(rax, rdi); asm.AddInstruction(original[0]); asm.mov(r9b, r11b);
            asm.and(r11b, __byte_ptr[rax]); asm.cmp(r11b, r9b); asm.je(unchanged);
            asm.inc(__qword_ptr[r10 + SlotTouchedOffset]);
            asm.Label(ref unchanged); asm.pop(r10); asm.pop(r9); asm.pop(rax);
            for (int index = 1; index < original.Length; index++) asm.AddInstruction(original[index]);
            asm.jmp(done); asm.Label(ref vanilla); asm.pop(r10); asm.pop(r9); asm.pop(rax);
            for (int index = 0; index < original.Length; index++) asm.AddInstruction(original[index]);
            asm.Label(ref done);
        }

        private static void EmitRcxDestination(Assembler asm, ReadOnlySpan<Instruction> original,
            ulong slots, bool tileInRax)
        {
            Label vanilla = asm.CreateLabel(), done = asm.CreateLabel(), unchanged = asm.CreateLabel();
            asm.push(r9); asm.push(r10); asm.push(r11);
            asm.mov(r10d, __dword_ptr.gs[0x48]); asm.mov(r11d, r10d); asm.and(r11d, ThreadSlotCount - 1);
            asm.shl(r11, 5); asm.mov(r10, slots); asm.add(r11, r10); asm.mov(r10d, __dword_ptr.gs[0x48]);
            asm.cmp(__dword_ptr[r11 + SlotOwnerOffset], r10d); asm.jne(vanilla);
            asm.mov(r10, __qword_ptr[r11 + SlotMaskOffset]); asm.test(r10, r10); asm.je(vanilla);
            if (tileInRax) asm.add(r10, rax); else asm.add(r10, rcx);
            asm.AddInstruction(original[0]); asm.mov(r9b, cl); asm.and(cl, __byte_ptr[r10]);
            asm.cmp(cl, r9b); asm.je(unchanged); asm.inc(__qword_ptr[r11 + SlotTouchedOffset]);
            asm.Label(ref unchanged); asm.pop(r11); asm.pop(r10); asm.pop(r9);
            for (int index = 1; index < original.Length; index++) asm.AddInstruction(original[index]);
            asm.jmp(done); asm.Label(ref vanilla); asm.pop(r11); asm.pop(r10); asm.pop(r9);
            for (int index = 0; index < original.Length; index++) asm.AddInstruction(original[index]);
            asm.Label(ref done);
        }

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();
        private static long Read(ref long value) => Interlocked.Read(ref value);
        private static void Reset(ref long value) => Interlocked.Exchange(ref value, 0);
    }
}
