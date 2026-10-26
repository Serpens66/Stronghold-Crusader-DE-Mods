using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.X64.Hooks.Transaction;

namespace BugfixesAndQoL.UnitCommands
{
    internal sealed unsafe partial class UnitCommandPathRuntime
    {
        private readonly HashSet<HookTransaction> publishedCommandTransactions = new HashSet<HookTransaction>();
        private CommitResult CommitPermanentHooks(HookTransaction candidate)
        {
            CommitResult result = candidate.Commit();
            // RollbackAndThrow handles failed, unpublished transactions inside RedBird.
            // Every returned committed transaction is retained before further validation.
            publishedCommandTransactions.Add(candidate);
            return result;
        }
        private bool IsPublished(HookTransaction transaction) =>
            transaction != null && publishedCommandTransactions.Contains(transaction);
        private void RollbackUnpublishedTransaction(HookTransaction transaction)
        {
            if (transaction != null && !IsPublished(transaction)) transaction.Dispose();
        }

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate void FastPlayerMoveDelegate(IntPtr manager, int tribe, int x, int y, int patrol, int flags);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate void FastChoreDelegate();
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate void FastAppendDelegate(IntPtr manager, int tribe, ushort x, ushort y, int index, short mode);

        internal sealed class TraversalCommandHooks
        {
            internal FastPlayerMoveDelegate Move;
            internal FastChoreDelegate Target;
            internal FastAppendDelegate Append;
        }
        private TraversalCommandHooks traversalCommandHooks;

        internal FastAppendDelegate queueAppendProvider;
        internal FastChoreDelegate queueTargetProvider;
        internal Action<SHCDESE.EventAPI.Tribes.TribeIssueOrderMoveHereEventArgs> queueMoveEvent;
        internal Func<SHCDESE.EventAPI.Tribes.TribeIssueOrderMoveHereEventArgs, bool> queueDefersMove;
        internal Action<SHCDESE.EventAPI.Tribes.TribeIssueOrderWithTargetEventArgs> queueTargetEvent;
        private FastAppendDelegate traversalAppendProvider;
        private FastChoreDelegate traversalTargetProvider;
        private FastPlayerMoveDelegate nativePlayerMove, traversalMoveProvider;
        // Native detour barriers: providers may come from other assemblies. An exception
        // never unwinds into CrusaderDE; Vanilla runs once unless a provider already reached it.
        [ThreadStatic] private static long playerMoveEntries, appendEntries, targetEntries;
        private static FastPlayerMoveDelegate CountPlayerMove(FastPlayerMoveDelegate native) =>
            (manager, tribe, x, y, patrol, flags) => { playerMoveEntries++; native(manager, tribe, x, y, patrol, flags); };
        private static FastAppendDelegate CountAppend(FastAppendDelegate native) =>
            (manager, tribe, x, y, index, mode) => { appendEntries++; native(manager, tribe, x, y, index, mode); };
        private static FastChoreDelegate CountTarget(FastChoreDelegate native) =>
            () => { targetEntries++; native(); };
        private void DispatchPlayerMove(IntPtr manager, int tribe, int x, int y, int patrol, int flags)
        {
            long entries = playerMoveEntries;
            ManualPlayerMoveFrame parentPlayerMove = manualPlayerMove;
            bool preserve = manualAdmissionReady && manager == nativeTribeManager && tribe > 0 && tribe < MaximumTribeCount;
            ushort previousMode = preserve ? *(ushort*)((byte*)manager + tribe * TribeRecordSize + 0x56C) : (ushort)0;
            manualPlayerMove = new ManualPlayerMoveFrame { Valid = preserve, Tribe = tribe };
            try
            {
                if (TraversalEnabled && traversalMoveProvider != null) traversalMoveProvider(manager, tribe, x, y, patrol, flags);
                else nativePlayerMove(manager, tribe, x, y, patrol, flags);
            }
            catch (Exception ex)
            {
                ReportNativeDetourBarrier("196100-player-move", ex);
                if (playerMoveEntries != entries) return;
                try { nativePlayerMove(manager, tribe, x, y, patrol, flags); }
                catch (Exception fallback) { ReportNativeDetourBarrier("196100-player-move-fallback", fallback); }
            }
            finally
            {
                // 196100 updates this group flag before the 11B520 event. A fully
                // rejected order must preserve that earlier write as well.
                if (preserve && manualPlayerMove.Rejected)
                    *(ushort*)((byte*)manager + tribe * TribeRecordSize + 0x56C) = previousMode;
                manualPlayerMove = parentPlayerMove;
            }
        }
        private FastAppendDelegate nativeAppend;
        private FastChoreDelegate nativeTarget;

        [ThreadStatic] private static Stack<bool> moveEventObservers;
        [ThreadStatic] private static Stack<int> moveEventDepths;
        [ThreadStatic] private static Stack<ManualRegionAdmissionBatch> moveAdmissionParents;
        [ThreadStatic] private static Stack<Action> targetCommandParents;
        [ThreadStatic] private static Stack<bool> targetEventObservers;
        internal void DispatchMoveEvent(SHCDESE.EventAPI.Tribes.TribeIssueOrderMoveHereEventArgs args)
        {
            if (args.Phase == SHCDESE.EventAPI.EventHookPhase.Pre)
            {
                ManualRegionAdmissionBatch admissionParent = BeginManualGroupAdmission(args);
                if (moveAdmissionParents == null) moveAdmissionParents = new Stack<ManualRegionAdmissionBatch>();
                moveAdmissionParents.Push(admissionParent);
                int depth = manualCommandContexts?.Count ?? 0;
                if (moveEventObservers == null) moveEventObservers = new Stack<bool>();
                if (moveEventDepths == null) moveEventDepths = new Stack<int>();
                moveEventObservers.Push(false); moveEventDepths.Push(depth);
                try
                {
                    formationRuntime?.OnTribeIssueOrderMoveHere(args);
                    if (args.SkipOriginalFunction)
                    {
                        moveEventObservers.Pop(); moveEventDepths.Pop();
                        EndManualGroupAdmission(moveAdmissionParents.Pop());
                        return;
                    }
                    queueMoveEvent?.Invoke(args);
                    if (!args.SkipOriginalFunction)
                    {
                        ObserveTribeMoveOrder(args);
                        moveEventObservers.Pop(); moveEventObservers.Push(true);
                    }
                    else
                    {
                        moveEventObservers.Pop(); moveEventDepths.Pop();
                        EndManualGroupAdmission(moveAdmissionParents.Pop());
                    }
                }
                catch
                {
                    while ((manualCommandContexts?.Count ?? 0) > depth) RestoreManualCommandContext();
                    if (args.SkipOriginalFunction)
                    {
                        moveEventObservers.Pop(); moveEventDepths.Pop();
                        EndManualGroupAdmission(moveAdmissionParents.Pop());
                    }
                    throw;
                }
            }
            else
            {
                bool observe = moveEventObservers?.Count > 0 && moveEventObservers.Pop();
                int depth = moveEventDepths?.Count > 0 ? moveEventDepths.Pop() : 0;
                try { if (observe) ObserveTribeMoveOrder(args); queueMoveEvent?.Invoke(args); }
                finally
                {
                    while ((manualCommandContexts?.Count ?? 0) > depth) RestoreManualCommandContext();
                    try { formationRuntime?.OnTribeIssueOrderMoveHere(args); }
                    finally { if (moveAdmissionParents?.Count > 0) EndManualGroupAdmission(moveAdmissionParents.Pop()); }
                }
            }
        }
        internal void DispatchTargetEvent(SHCDESE.EventAPI.Tribes.TribeIssueOrderWithTargetEventArgs args)
        {
            if (args.Phase == SHCDESE.EventAPI.EventHookPhase.Pre)
            {
                if (targetCommandParents == null) targetCommandParents = new Stack<Action>();
                if (targetEventObservers == null) targetEventObservers = new Stack<bool>();
                targetCommandParents.Push(CaptureTargetCommandContext());
                targetEventObservers.Push(false);
                try
                {
                    queueTargetEvent?.Invoke(args);
                    if (args.SkipOriginalFunction)
                    {
                        targetEventObservers.Pop(); targetCommandParents.Pop().Invoke();
                        return;
                    }
                    ObserveTribeTargetOrder(args);
                    targetEventObservers.Pop(); targetEventObservers.Push(true);
                }
                catch
                {
                    if (args.SkipOriginalFunction)
                    { targetEventObservers.Pop(); targetCommandParents.Pop().Invoke(); }
                    throw;
                }
            }
            else
            {
                bool observe = targetEventObservers?.Count > 0 && targetEventObservers.Pop();
                try { if (observe) ObserveTribeTargetOrder(args); }
                finally { if (targetCommandParents?.Count > 0) targetCommandParents.Pop()?.Invoke(); }
            }
        }
        private void DispatchAppend(IntPtr manager, int tribe, ushort x, ushort y, int index, short mode)
        {
            long entries = appendEntries;
            try
            {
                if (queueAppendProvider != null) queueAppendProvider(manager, tribe, x, y, index, mode);
                else ContinueQueueAppend(manager, tribe, x, y, index, mode);
            }
            catch (Exception ex)
            {
                ReportNativeDetourBarrier("11C3A0-queue-append", ex);
                if (appendEntries != entries) return;
                try { nativeAppend(manager, tribe, x, y, index, mode); }
                catch (Exception fallback) { ReportNativeDetourBarrier("11C3A0-queue-append-fallback", fallback); }
            }
        }
        internal void ContinueQueueAppend(IntPtr manager, int tribe, ushort x, ushort y, int index, short mode)
        {
            if (traversalAppendProvider != null) traversalAppendProvider(manager, tribe, x, y, index, mode);
            else nativeAppend(manager, tribe, x, y, index, mode);
        }
        private void DispatchTarget()
        {
            long entries = targetEntries;
            try
            {
                if (traversalTargetProvider != null) traversalTargetProvider();
                else ContinueTraversalTarget();
            }
            catch (Exception ex)
            {
                ReportNativeDetourBarrier("12BF0-target-chore", ex);
                if (targetEntries != entries) return;
                try { nativeTarget(); }
                catch (Exception fallback) { ReportNativeDetourBarrier("12BF0-target-chore-fallback", fallback); }
            }
        }
        private void ContinueTraversalTarget()
        {
            if (queueTargetProvider != null) queueTargetProvider();
            else nativeTarget();
        }
        internal void InstallQueueCommandHooks(ReadOnlySpan<byte> memory, ulong libraryBase,
            FastAppendDelegate append, FastChoreDelegate target)
        {
            if (nativeAppend != null) throw new InvalidOperationException("Queue command hooks already installed.");
            HookTransaction candidate = CreateOwnedHookTransaction();
            try
            {
                var targetHook = InstallConnectivityObserver(candidate, memory, libraryBase, 0x12BF0,
                    "40 53 48 83 EC 30 8B 05 F0 63 5E 08 C7 05 EE 63 5E 08 0F 00 00 00", (FastChoreDelegate)DispatchTarget);
                var appendHook = InstallConnectivityObserver(candidate, memory, libraryBase, 0x11C3A0,
                    "4C 63 5C 24 28 4C 63 D2 49 69 C2 88 06 00 00 49 69 D2 A2 01 00 00", (FastAppendDelegate)DispatchAppend);
                CommitResult result = CommitPermanentHooks(candidate);
                nativeTarget = CountTarget(targetHook.Original);
                nativeAppend = CountAppend(appendHook.Original);
                ValidatePublishedCommandHooks(candidate);
                if (!result.IsCompleteSuccess || !targetHook.Committed || !appendHook.Committed)
                    throw new InvalidOperationException("Shared queue hooks failed their publication contract.");
                queueAppendProvider = append;
                queueTargetProvider = target;
            }
            catch { RollbackUnpublishedTransaction(candidate); throw; }
        }
        internal void ContinueQueueTarget() => nativeTarget();

        internal TraversalCommandHooks InstallTraversalCommandHooks(ReadOnlySpan<byte> memory, ulong libraryBase,
            FastPlayerMoveDelegate move, FastChoreDelegate target, FastAppendDelegate append)
        {
            if (traversalCommandHooks != null) throw new InvalidOperationException("Traversal command hooks already registered with BugfixesAndQoL.");
            if (nativeAppend == null || nativeTarget == null) throw new InvalidOperationException("Main queue hook infrastructure unavailable.");
            HookTransaction candidate = CreateOwnedHookTransaction();
            try
            {
                var moveHook = InstallConnectivityObserver(candidate, memory, libraryBase, 0x196100,
                    "48 83 EC 48 48 63 C2 4C 8D 1D 12 06 B3 07", (FastPlayerMoveDelegate)DispatchPlayerMove);
                CommitResult result = CommitPermanentHooks(candidate);
                nativePlayerMove = CountPlayerMove(moveHook.Original);
                var originals = new TraversalCommandHooks {
                    Move = nativePlayerMove, Target = ContinueTraversalTarget, Append = nativeAppend };
                traversalCommandHooks = originals;
                ValidatePublishedCommandHooks(candidate);
                if (!result.IsCompleteSuccess || !moveHook.Committed)
                    throw new InvalidOperationException("Traversal command hook failed its publication contract.");
                traversalMoveProvider = move;
                traversalTargetProvider = target;
                traversalAppendProvider = append;
                return originals;
            }
            catch { RollbackUnpublishedTransaction(candidate); throw; }
        }
    }
}
