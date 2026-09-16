using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using R3;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace MoatMove
{
    internal sealed unsafe partial class FriendlyMoatMovementRuntime
    {
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate void FastPlayerMoveDelegate(IntPtr manager, int tribe, int x, int y, int patrol, int flags);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate void FastChoreDelegate();
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate void FastAppendDelegate(IntPtr manager, int tribe, ushort x, ushort y, int index, short mode);
        private FastPlayerMoveDelegate originalFastPlayerMove;
        private FastChoreDelegate originalFastTargetChore;
        private FastAppendDelegate originalFastAppend;
        private readonly FastCommandQueue fastCommands = new FastCommandQueue();
        private readonly List<Delegate> fastCommandDelegates = new List<Delegate>();
        private HookTransaction fastCommandHooks;
        private IDisposable fastCancelSubscription, fastLoadSubscription;
        private bool fastSchedulerInitialized, fastDispatching, fastLoadReady, fastSaveLoading;
        private int fastPlayerActionDepth, fastLastTick = int.MinValue, fastRoundRobin;
        private IntPtr fastChoreMode;
        private byte[] fastLoadedCommands;
        private long fastQueuedCommands, fastExecutedCommands, fastCommandRetries;

        private sealed class FastPendingSearch : IDisposable
        {
            internal FastFieldLease Ground, Friendly;
            public void Dispose() { Ground?.Dispose(); Friendly?.Dispose(); Ground = Friendly = null; }
        }

        private void InstallFastCommandRuntime(ReadOnlySpan<byte> memory, ulong libraryBase)
        {
            // 196100 is a real 89-byte function, not the E8 call at 10CAE.
            // Entry span: sub rsp,48; movsxd rax,edx; lea r11,[TribeManager] (14 bytes).
            // Original six Win64 arguments retain their signed int bits on replay.
            ValidateExactBytes(memory, 0x196100, new byte[] {
                0x48,0x83,0xEC,0x48,0x48,0x63,0xC2,0x4C,0x8D,0x1D,0x12,0x06,0xB3,0x07 }, "Fast player move entry");
            originalFastPlayerMove = Marshal.GetDelegateForFunctionPointer<FastPlayerMoveDelegate>((IntPtr)(libraryBase + 0x196100));
            // Saved Fast queues can also be restored while running Precise.
            ValidateExactBytes(memory, 0x11C3A0, new byte[] {
                0x4C,0x63,0x5C,0x24,0x28,0x4C,0x63,0xD2,0x49,0x69,0xC2,0x88,0x06,0,0,
                0x49,0x69,0xD2,0xA2,0x01,0,0 }, "Fast saved waypoint entry");
            originalFastAppend = Marshal.GetDelegateForFunctionPointer<FastAppendDelegate>((IntPtr)(libraryBase + 0x11C3A0));
            HookTransaction pending = null;
            bool registered = false, tickRegistered = false;
            try
            {
                if (RequiredOnlyMode)
                {
                    pending = CreateOwnedHookTransaction();
                    FastPlayerMoveDelegate move = CaptureFastPlayerMove;
                    FastChoreDelegate target = ObserveFastTargetChore;
                    FastAppendDelegate append = AppendAfterFastPendingMove;
                    fastCommandDelegates.Add(move); fastCommandDelegates.Add(target); fastCommandDelegates.Add(append);
                    var moveHook = InstallConnectivityObserver(pending, memory, libraryBase, 0x196100,
                        "48 83 EC 48 48 63 C2 4C 8D 1D 12 06 B3 07", move);
                    // Full first 22-byte span; RIP-relative reads/writes are relocated by RedBird.
                    var targetHook = InstallConnectivityObserver(pending, memory, libraryBase, 0x12BF0,
                        "40 53 48 83 EC 30 8B 05 F0 63 5E 08 C7 05 EE 63 5E 08 0F 00 00 00", target);
                    var appendHook = InstallConnectivityObserver(pending, memory, libraryBase, 0x11C3A0,
                        "4C 63 5C 24 28 4C 63 D2 49 69 C2 88 06 00 00 49 69 D2 A2 01 00 00", append);
                    var result = pending.Commit();
                    if (!result.IsCompleteSuccess || !moveHook.Committed || !targetHook.Committed || !appendHook.Committed)
                        throw new InvalidOperationException("Fast command hook transaction failed.");
                    originalFastPlayerMove = moveHook.Original;
                    originalFastTargetChore = targetHook.Original;
                    originalFastAppend = appendHook.Original;
                    fastChoreMode = (IntPtr)(libraryBase + 0x85F8FEC);
                }
                fastCommands.Removed += ReleaseFastPendingSearch;
                fastCancelSubscription = TribeR3EventHooks.OnTribeIssueOrderWithTarget.Observable.Subscribe(CancelFastPlayerTarget);
                fastLoadSubscription = Shared.MissionEvents.SaveLoading.Subscribe(args =>
                {
                    if (args.IsBeforeInitialization)
                    { fastSaveLoading = true; fastCommands.Clear(); fastLoadedCommands = null; fastLoadReady = false; }
                    else
                    {
                        fastSaveLoading = false; fastLoadReady = !args.IsBeforeInitialization;
                        if (!fastLoadReady) fastLoadedCommands = null;
                    }
                });
                registered = ModSaveDataAPI.Instance.RegisterModDataHandler("MoatMove_Serp",
                    context => context.IsSaveFile ? CaptureFastSave() : null,
                    (bytes, context) => { if (context.IsSaveFile) fastLoadedCommands = (byte[])bytes.Clone(); });
                if (!registered) throw new InvalidOperationException("Fast save handler already registered.");
                GameTimeManagerAPI.Instance.OnTick += ProcessFastCommands;
                tickRegistered = true;
                fastCommandHooks = pending;
                fastSchedulerInitialized = true;
            }
            catch
            {
                // Only a failed initialization candidate reaches this rollback.
                if (tickRegistered) GameTimeManagerAPI.Instance.OnTick -= ProcessFastCommands;
                if (registered) ModSaveDataAPI.Instance.UnregisterModDataHandler("MoatMove_Serp");
                fastCancelSubscription?.Dispose(); fastLoadSubscription?.Dispose();
                fastCommands.Removed -= ReleaseFastPendingSearch;
                pending?.Dispose();
                throw;
            }
        }

        private void ReleaseFastPendingSearch(FastPendingCommand command)
        { (command.SearchState as FastPendingSearch)?.Dispose(); command.SearchState = null; }

        private byte[] CaptureFastSave()
        {
            if (fastLoadReady && fastLoadedCommands != null)
            {
                // Saving a paused, just-loaded game may precede its first simulation tick.
                var staged = new FastCommandQueue(); staged.Load(fastLoadedCommands); return staged.Save();
            }
            return fastCommands.Save();
        }

        private List<FastUnitIdentity> CaptureFastMembers(int tribeId)
        {
            var result = new List<FastUnitIdentity>();
            if (!TryCaptureOrderedActiveGroupUnits(nativeTribeManager, tribeId, out int[] ids)) return result;
            foreach (int id in ids)
                if (GameUnitManagerAPI.Instance.TryGetUnitById(id, out GameUnit* unit) && unit != null && unit->r_GlobalId != 0)
                    result.Add(new FastUnitIdentity(id, unit->r_GlobalId));
            result.Sort(); return result;
        }

        private bool TryGetFastMember(FastUnitIdentity identity, int player, out GameUnit* unit)
        {
            return GameUnitManagerAPI.Instance.TryGetUnitById(identity.Id, out unit) && unit != null &&
                unit->r_GlobalId == identity.Global && unit->r_AliveState == AliveState.IsAlive &&
                unit->r_ControllableForPlayerId == player;
        }

        private void CaptureFastPlayerMove(IntPtr manager, int tribeId, int x, int y, int patrol, int flags)
        {
            try { if (TryCaptureFastPlayerMove(tribeId, x, y, patrol, flags)) return; }
            catch (Exception ex) { TryLogDiagnosticFailure("fast-capture", ex); }
            // No catch around Original: a failing native invocation must never be replayed.
            originalFastPlayerMove(manager, tribeId, x, y, patrol, flags);
        }

        private bool TryCaptureFastPlayerMove(int tribeId, int x, int y, int patrol, int flags)
        {
            if (!fastSchedulerInitialized || fastDispatching || !RequiredOnlyMode || (uint)x >= MapWidth || (uint)y >= MapWidth ||
                !GameTribeManagerAPI.Instance.TryGetTribeById(tribeId, out GameTribe* tribe) || tribe == null ||
                tribe->r_AliveState != AliveState.IsAlive ||
                !GamePlayerManagerAPI.Instance.IsPlayerIdValid(tribe->r_PlayerIdOwner) ||
                GamePlayerManagerAPI.Instance.IsAIPlayer(tribe->r_PlayerIdOwner))
                return false;
            List<FastUnitIdentity> members = CaptureFastMembers(tribeId);
            if (members.Count == 0) return false;
            fastCommands.Supersede(members);
            bool ordinary = patrol == 0;
            int target = GameTileManagerAPI.Instance.GetTileId(x, y);
            bool needsSearch = false;
            foreach (FastUnitIdentity member in members)
            {
                if (!TryGetFastMember(member, tribe->r_PlayerIdOwner, out GameUnit* unit) || !CanDigMoat(unit)) continue;
                GetNativeMovementStart(unit, out int sx, out int sy);
                if ((uint)sx >= MapWidth || (uint)sy >= MapWidth ||
                    !IsSamePositiveGroundRegion(GameTileManagerAPI.Instance.GetTileId(sx, sy), target)) needsSearch = true;
            }
            if (!ordinary || !needsSearch)
                return false;
            fastCommands.Enqueue(tribe->r_PlayerIdOwner, tribeId, tribe->r_GlobalId,
                x, y, patrol, flags, CaptureCurrentGameTick(), members);
            fastQueuedCommands++;
            return true;
        }

        private void ObserveFastTargetChore()
        {
            bool execute = fastChoreMode != IntPtr.Zero && Marshal.ReadInt32(fastChoreMode) == 0;
            if (execute) fastPlayerActionDepth++;
            try { originalFastTargetChore(); }
            finally { if (execute) fastPlayerActionDepth--; }
        }

        private void CancelFastPlayerTarget(TribeIssueOrderWithTargetEventArgs args)
        {
            if (!fastSchedulerInitialized || fastDispatching || fastPlayerActionDepth == 0 || args.Phase != EventHookPhase.Pre) return;
            try
            {
                var members = CaptureFastMembers(args.TribeId);
                fastCommands.Supersede(members);
                foreach (var member in members) fastUnitDistributions.Remove(member);
            }
            catch (Exception ex) { TryLogDiagnosticFailure("fast-replacement", ex); }
        }

        private void AppendAfterFastPendingMove(IntPtr manager, int tribeId, ushort x, ushort y, int index, short mode)
        {
            try { if (TryHoldFastWaypoint(tribeId, x, y, index, mode)) return; }
            catch (Exception ex) { TryLogDiagnosticFailure("fast-waypoint-capture", ex); }
            originalFastAppend(manager, tribeId, x, y, index, mode);
        }

        private bool TryHoldFastWaypoint(int tribeId, ushort x, ushort y, int index, short mode)
        {
            // 8C5F0 increments the separate input counter when staging the click,
            // independently of 11B520 execution. Preserve all decoded slot arguments.
            if (!fastDispatching && fastSchedulerInitialized && (uint)index < 10 && x < MapWidth && y < MapWidth &&
                GameTribeManagerAPI.Instance.TryGetTribeById(tribeId, out GameTribe* tribe) && tribe != null)
            {
                var members = CaptureFastMembers(tribeId);
                if (fastCommands.HasPredecessor(members))
                {
                    fastCommands.AppendWaypoint(tribe->r_PlayerIdOwner, tribeId, tribe->r_GlobalId,
                        x, y, index, mode, CaptureCurrentGameTick(), members);
                    return true;
                }
            }
            return false;
        }

        private void ProcessFastCommands(int tick)
        {
            if (!fastSchedulerInitialized || fastDispatching || fastSaveLoading || tick == fastLastTick) return;
            fastLastTick = tick;
            try
            {
                if (fastLoadReady && fastLoadedCommands != null)
                {
                    byte[] staged = fastLoadedCommands; fastLoadedCommands = null; fastLoadReady = false;
                    fastCommands.Load(staged);
                }
                if ((tick & 63) == 0 && fastUnitDistributions.Count != 0)
                    foreach (var identity in new List<FastUnitIdentity>(fastUnitDistributions.Keys))
                        if (!TryGetFastMember(identity, fastUnitDistributions[identity].Player, out _))
                            fastUnitDistributions.Remove(identity);
                if (fastCommands.Commands.Count == 0) return;
                if (!RequiredOnlyMode)
                {
                    foreach (FastPendingCommand command in new List<FastPendingCommand>(fastCommands.Commands))
                        if (!fastCommands.HasPredecessor(command.Members, command.Sequence)) DispatchFastCommand(command);
                    return;
                }
                RefreshFastRouting(GetFastRouting(false));
                int budget = 8192;
                var active = new List<FastPendingCommand>();
                foreach (FastPendingCommand command in fastCommands.Commands)
                { active.Add(command); if (active.Count == (settings.NativeFast ? GetFastRouting(false).Pool.PendingGroupCapacity : 4)) break; }
                int idle = 0;
                while (budget > 0 && active.Count != 0 && idle < active.Count)
                {
                    int index = fastRoundRobin % active.Count;
                    fastRoundRobin = (index + 1) % active.Count;
                    FastPendingCommand command = active[index]; int before = budget;
                    int slice = Math.Min(1024, budget), sliceBefore = slice;
                    bool ready = AdvanceFastCommand(command, ref slice);
                    budget -= sliceBefore - slice;
                    if (ready)
                    {
                        DispatchFastCommand(command); active.RemoveAt(index); idle = 0;
                    }
                    else if (budget == before) idle++; else idle = 0;
                }
            }
            catch (Exception ex) { fastCommandRetries++; TryLogDiagnosticFailure("fast-scheduler-retry", ex); }
            finally { if ((tick & 255) == 0) LogAndResetFastMoatMetrics(); }
        }

        private bool AdvanceFastCommand(FastPendingCommand command, ref int budget)
        {
            command.Members.RemoveAll(member => !TryGetFastMember(member, command.Player, out _));
            if (command.Members.Count == 0) { fastCommands.Remove(command); return true; }
            if (command.IsWaypoint) return !fastCommands.HasPredecessor(command.Members, command.Sequence);
            var search = command.SearchState as FastPendingSearch;
            if (search == null) command.SearchState = search = new FastPendingSearch();
            FastRoutingState state = GetFastRouting(false);
            RefreshFastRouting(state);
            int target = command.Y * MapWidth + command.X;
            bool pending = false;
            foreach (FastUnitIdentity member in command.Members)
            {
                if (!TryGetFastMember(member, command.Player, out GameUnit* unit) || !CanDigMoat(unit)) continue;
                GetNativeMovementStart(unit, out int x, out int y);
                if ((uint)x >= MapWidth || (uint)y >= MapWidth) continue;
                int start = y * MapWidth + x;
                search.Ground = search.Ground ?? state.Pool.Acquire(new FastFieldKey(command.Player, target, true));
                FastRouteStatus ground = search.Ground?.Field.Status(start, int.MaxValue) ?? FastRouteStatus.Pending;
                if (ground == FastRouteStatus.Pending)
                {
                    long before = search.Ground?.Field.Work ?? 0;
                    ground = AdvanceFastField(search.Ground, start, Math.Min(1024, budget), int.MaxValue);
                    budget -= (int)((search.Ground?.Field.Work ?? 0) - before);
                }
                if (ground == FastRouteStatus.Pending) { pending = true; break; }
                if (ground == FastRouteStatus.Found) continue;
                search.Friendly = search.Friendly ?? state.Pool.Acquire(new FastFieldKey(command.Player, target, false));
                FastRouteStatus friendly = search.Friendly?.Field.Status(start) ?? FastRouteStatus.Pending;
                if (friendly == FastRouteStatus.Pending)
                {
                    long before = search.Friendly?.Field.Work ?? 0;
                    friendly = AdvanceFastField(search.Friendly, start, Math.Min(1024, budget));
                    budget -= (int)((search.Friendly?.Field.Work ?? 0) - before);
                }
                if (friendly == FastRouteStatus.Pending) { pending = true; break; }
            }
            return !pending;
        }

        private bool DispatchFastCommand(FastPendingCommand command)
        {
            command.Members.RemoveAll(member => !TryGetFastMember(member, command.Player, out _));
            if (command.Members.Count == 0) { fastCommands.Remove(command); return true; }
            var groups = new SortedDictionary<int, List<FastUnitIdentity>>();
            foreach (FastUnitIdentity member in command.Members)
            {
                if (!TryGetFastMember(member, command.Player, out GameUnit* unit)) continue;
                int tribe = unit->r_TribeId;
                if (!groups.TryGetValue(tribe, out var list)) groups.Add(tribe, list = new List<FastUnitIdentity>());
                list.Add(member);
            }
            bool previous = fastDispatching; fastDispatching = true;
            try
            {
                foreach (var group in groups)
                {
                    NativeFastQueueSnapshot inheritedQueue = null;
                    if (command.IsWaypoint && !TryCaptureFastNativeQueue(group.Key, command.Player, out inheritedQueue))
                    { fastCommandRetries++; return false; }
                    if (!TryBindFastDispatchGroup(command.Player, group.Key, group.Value, out int tribe))
                    { fastCommandRetries++; return false; }
                    if (command.IsWaypoint)
                    {
                        if (tribe != group.Key) RestoreFastNativeQueue(tribe, inheritedQueue);
                        originalFastAppend(nativeTribeManager, tribe, (ushort)command.X, (ushort)command.Y, command.MoveFlags, (short)command.Patrol);
                    }
                    else originalFastPlayerMove((IntPtr)nativeUnitManager, tribe, command.X, command.Y, command.Patrol, command.MoveFlags);
                    foreach (FastUnitIdentity member in group.Value) command.Members.Remove(member);
                }
                fastMaximumQueueWaitTicks = Math.Max(fastMaximumQueueWaitTicks,
                    Math.Max(0L, (long)CaptureCurrentGameTick() - command.EnqueuedTick));
                fastCommands.Remove(command); fastExecutedCommands++; return true;
            }
            finally { fastDispatching = previous; }
        }

        private bool TryBindFastDispatchGroup(int player, int originalId, List<FastUnitIdentity> members, out int tribeId)
        {
            tribeId = originalId;
            var tribes = GameTribeManagerAPI.Instance;
            GameTribe* original = null;
            if (originalId > 0 && (!tribes.TryGetTribeById(originalId, out original) || original == null ||
                original->r_AliveState != AliveState.IsAlive || original->r_PlayerIdOwner != player)) return false;
            var before = new List<FastGroupMemberSnapshot>();
            foreach (var member in members)
            {
                if (!TryGetFastMember(member, player, out GameUnit* unit) || unit->r_TribeId != originalId ||
                    (originalId > 0 && !FastGroupContains(originalId, member.Id))) return false;
                before.Add(new FastGroupMemberSnapshot { Identity = member, Leader = unit->r_TribeLeaderUnitId,
                    Ordinal = unit->UnknownAIFlag, GroupGlobal = unit->N000000AF });
            }
            if (original != null && original->r_UnitsInGroup == members.Count) return true;
            ushort previousLeader = original == null ? (ushort)0 : original->r_LeaderUnitId;
            uint previousGlobal = original == null ? 0 : original->r_GlobalId;
            long created = tribes.Create(player, false);
            if (created <= 0 || created >= MaximumTribeCount) return false;
            int destination = (int)created;
            bool committed = false;
            try
            {
                if (!tribes.TryGetTribeById(destination, out GameTribe* candidate) || candidate == null ||
                    candidate->r_AliveState != AliveState.IsAlive || candidate->r_PlayerIdOwner != player || candidate->r_UnitsInGroup != 0)
                    return false;
                if (originalId > 0) tribes.SetStance(destination, tribes.GetStance(originalId));
                foreach (FastUnitIdentity member in members)
                {
                    if (!TryGetFastMember(member, player, out GameUnit* unit) || unit->r_TribeId != originalId ||
                        (originalId > 0 && (!tribes.UnassignUnit(originalId, member.Id) ||
                         !TryGetFastMember(member, player, out unit) || unit->r_TribeId != 0 || FastGroupContains(originalId, member.Id))) ||
                        !tribes.AssignUnit(destination, member.Id) || !TryGetFastMember(member, player, out unit) ||
                        unit->r_TribeId != destination || !FastGroupContains(destination, member.Id)) return false;
                }
                committed = true; tribeId = destination; return true;
            }
            finally
            {
                if (!committed)
                {
                    bool restored = true;
                    foreach (var snapshot in before)
                    {
                        try
                        {
                            if (!TryGetFastMember(snapshot.Identity, player, out GameUnit* unit)) { restored = false; continue; }
                            if (unit->r_TribeId == destination) tribes.UnassignUnit(destination, snapshot.Identity.Id);
                            if (unit->r_TribeId == 0 && originalId > 0) tribes.AssignUnit(originalId, snapshot.Identity.Id);
                            if (unit->r_TribeId != originalId || (originalId > 0 && !FastGroupContains(originalId, snapshot.Identity.Id)))
                            { restored = false; continue; }
                            // Restore the additional 11D370 writes, not only the membership ID.
                            unit->r_TribeLeaderUnitId = snapshot.Leader;
                            unit->UnknownAIFlag = snapshot.Ordinal; unit->N000000AF = snapshot.GroupGlobal;
                        }
                        catch (Exception ex) { restored = false; TryLogDiagnosticFailure("fast-group-rollback", ex); }
                    }
                    if (restored && originalId > 0 && tribes.TryGetTribeById(originalId, out original) && original != null &&
                        original->r_GlobalId == previousGlobal)
                    {
                        original->r_LeaderUnitId = previousLeader;
                        foreach (var member in CaptureFastMembers(originalId))
                            if (TryGetFastMember(member, player, out GameUnit* unit)) unit->r_TribeLeaderUnitId = previousLeader;
                    }
                    // A partially restored group is retained along with its pending command.
                    if (tribes.TryGetTribeById(destination, out GameTribe* remaining) && remaining != null && remaining->r_UnitsInGroup == 0)
                        tribes.DeleteTribeSafe(destination);
                }
            }
        }

        private struct FastGroupMemberSnapshot
        {
            internal FastUnitIdentity Identity;
            internal ushort Leader, Ordinal;
            internal uint GroupGlobal;
        }

        private bool FastGroupContains(int tribeId, int unitId)
        {
            if (tribeId <= 0 || tribeId >= MaximumTribeCount || unitId <= 0 || unitId > 10000) return false;
            ushort bits = *(ushort*)((byte*)nativeTribeManager + tribeId * TribeRecordSize + 0x60 + (unitId >> 4) * 2);
            return (bits & (1 << (unitId & 15))) != 0;
        }

        private sealed class NativeFastQueueSnapshot
        {
            internal short Mode, Index, Count, Timer;
            internal readonly ushort[] Coordinates = new ushort[20];
        }

        private bool TryCaptureFastNativeQueue(int tribeId, int player, out NativeFastQueueSnapshot snapshot)
        {
            snapshot = null;
            if (tribeId <= 0 || tribeId >= MaximumTribeCount ||
                !GameTribeManagerAPI.Instance.TryGetTribeById(tribeId, out GameTribe* tribe) || tribe == null ||
                tribe->r_PlayerIdOwner != player || tribe->r_AliveState != AliveState.IsAlive) return false;
            byte* record = (byte*)nativeTribeManager + tribeId * TribeRecordSize;
            short count = *(short*)(record + 0x5DE), index = *(short*)(record + 0x5DC);
            if (count < 0 || count > 10 || index < 0 || index >= 10) return false;
            snapshot = new NativeFastQueueSnapshot { Count = count, Index = index,
                Mode = *(short*)(record + 0x582), Timer = *(short*)(record + 0x56) };
            for (int i = 0; i < count * 2; i++) snapshot.Coordinates[i] = *(ushort*)(record + 0x5B4 + i * 2);
            return true;
        }

        private void RestoreFastNativeQueue(int tribeId, NativeFastQueueSnapshot snapshot)
        {
            // 11C3A0 writes exact coordinates/count/mode; 11A980 advances the
            // signed 16-bit index only after its 16-bit timer and arrival test.
            for (int i = 0; i < snapshot.Count; i++)
                originalFastAppend(nativeTribeManager, tribeId, snapshot.Coordinates[i * 2],
                    snapshot.Coordinates[i * 2 + 1], i, snapshot.Mode);
            byte* record = (byte*)nativeTribeManager + tribeId * TribeRecordSize;
            *(short*)(record + 0x5DC) = snapshot.Index;
            *(short*)(record + 0x56) = snapshot.Timer;
        }

        private void ResetFastCommandMap()
        {
            fastCommands.Clear(); fastLastTick = int.MinValue; fastRoundRobin = 0;
            fastSimulationRouting?.Pool.Clear(); fastCursorRouting?.Pool.Clear();
            fastSimulationRouting = fastCursorRouting = null;
            fastDistribution = null;
            fastUnitDistributions.Clear();
            if (!fastSaveLoading) { fastLoadedCommands = null; fastLoadReady = false; }
        }
    }
}
