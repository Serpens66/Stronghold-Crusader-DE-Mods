using BepInEx.Logging;
using SHCDESE.Interop;
using System;
using System.Runtime.InteropServices;
using Zhuqiaomon.Hooks;
using Zhuqiaomon.Hooks.Transaction;
using Zhuqiaomon.Memory;

namespace TroopMovementFix
{
    internal sealed unsafe class TribeSelectionSpeedHook : IDisposable
    {
        // Assign one unit to a tribe and recalculate the tribe's movement speeds.
        private const string AssignSingleAndRecalculatePattern =
            "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 41 56 41 57 " +
            "48 83 EC 20 41 8B C0 4D 63 F1 33 F6";

        // Assign all matching units to a tribe and calculate the same movement
        // fields. Both helpers remain fully Vanilla; the callback runs afterwards.
        private const string AssignMatchingAndRecalculatePattern =
            "40 55 56 41 56 48 83 EC 30 44 8B 0D ?? ?? ?? ?? 45 33 D2 " +
            "48 89 7C 24 58 45 8B F2";

        // Selection-only helper called immediately after the bulk assignment. It
        // copies Vanilla's tribe-0 template state into the newly created selection
        // tribe. This is the last synchronous point before unit-type handlers can
        // observe that new tribe.
        private const string CopySelectionTribeStatePattern =
            "48 83 EC 18 4D 63 C8 49 69 D1 88 06 00 00 " +
            "83 B9 14 6B 73 00 00 0F 84 ?? ?? ?? ??";

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void AssignSingleAndRecalculateDelegate(
            NativePointer<GameTribeManager> tribeManager,
            int playerId,
            int unitId,
            int tribeId);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void AssignMatchingAndRecalculateDelegate(
            NativePointer<GameTribeManager> tribeManager,
            int playerId,
            int tribeId,
            int matchContext);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void CopySelectionTribeStateDelegate(
            NativePointer<GameTribeManager> tribeManager,
            int playerId,
            int tribeId);

        internal delegate void TribeRecalculatedDelegate(
            int tribeId,
            string source);

        internal delegate void SelectionTribeStateCopiedDelegate(int tribeId);

        private readonly ManualLogSource log;
        private readonly TribeRecalculatedDelegate tribeRecalculated;
        private readonly SelectionTribeStateCopiedDelegate selectionTribeStateCopied;
        private readonly HookTransaction transaction;
        private HookRef<X64ManagedFunctionDetourAOB<AssignSingleAndRecalculateDelegate>> singleHook =
            new HookRef<X64ManagedFunctionDetourAOB<AssignSingleAndRecalculateDelegate>>();
        private HookRef<X64ManagedFunctionDetourAOB<AssignMatchingAndRecalculateDelegate>> matchingHook =
            new HookRef<X64ManagedFunctionDetourAOB<AssignMatchingAndRecalculateDelegate>>();
        private HookRef<X64ManagedFunctionDetourAOB<CopySelectionTribeStateDelegate>> copyStateHook =
            new HookRef<X64ManagedFunctionDetourAOB<CopySelectionTribeStateDelegate>>();

        private bool callbackFailureLogged;
        private bool disposed;

        public TribeSelectionSpeedHook(
            ManualLogSource log,
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            TribeRecalculatedDelegate tribeRecalculated,
            SelectionTribeStateCopiedDelegate selectionTribeStateCopied)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.tribeRecalculated =
                tribeRecalculated ?? throw new ArgumentNullException(nameof(tribeRecalculated));
            this.selectionTribeStateCopied =
                selectionTribeStateCopied ??
                throw new ArgumentNullException(nameof(selectionTribeStateCopied));

            transaction = new HookTransaction(
                memory,
                libraryBase,
                loggerFactory: null,
                failureMode: TransactionFailureMode.RollbackAndThrow);

            transaction.AddDetour(
                ref singleHook,
                AssignSingleAndRecalculatePattern,
                AssignSingleAndRecalculate);

            transaction.AddDetour(
                ref matchingHook,
                AssignMatchingAndRecalculatePattern,
                AssignMatchingAndRecalculate);

            transaction.AddDetour(
                ref copyStateHook,
                CopySelectionTribeStatePattern,
                CopySelectionTribeState);

            transaction.Commit();

            if (!singleHook.Success || !matchingHook.Success || !copyStateHook.Success)
            {
                throw new InvalidOperationException(
                    "One or more native selection-tribe movement helpers were not found.");
            }

            Shared.DebugLogHelper.LogInfo(
                log,
                "Native single-unit, bulk, and final selection-tribe state-copy hooks installed successfully.");
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            transaction?.Dispose();
        }

        private void AssignSingleAndRecalculate(
            NativePointer<GameTribeManager> tribeManager,
            int playerId,
            int unitId,
            int tribeId)
        {
            singleHook.Value.Hook.Trampoline(
                tribeManager,
                playerId,
                unitId,
                tribeId);
            NotifyTribeRecalculated(tribeId, "AssignSingle");
        }

        private void AssignMatchingAndRecalculate(
            NativePointer<GameTribeManager> tribeManager,
            int playerId,
            int tribeId,
            int matchContext)
        {
            matchingHook.Value.Hook.Trampoline(
                tribeManager,
                playerId,
                tribeId,
                matchContext);
            NotifyTribeRecalculated(tribeId, "AssignMatching");
        }

        private void CopySelectionTribeState(
            NativePointer<GameTribeManager> tribeManager,
            int playerId,
            int tribeId)
        {
            copyStateHook.Value.Hook.Trampoline(
                tribeManager,
                playerId,
                tribeId);
            NotifySelectionTribeStateCopied(tribeId);
        }

        private void NotifyTribeRecalculated(int tribeId, string source)
        {
            try
            {
                tribeRecalculated(tribeId, source);
            }
            catch (Exception ex)
            {
                if (callbackFailureLogged)
                    return;

                callbackFailureLogged = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Could not preserve a selection-rebuilt tribe's Vanilla movement state; " +
                    $"the recalculated tribe remains unchanged: {ex}");
            }
        }

        private void NotifySelectionTribeStateCopied(int tribeId)
        {
            try
            {
                selectionTribeStateCopied(tribeId);
            }
            catch (Exception ex)
            {
                if (callbackFailureLogged)
                    return;

                callbackFailureLogged = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Could not inherit the previous Vanilla movement state after " +
                    $"selection-tribe initialization; the template state remains active: {ex}");
            }
        }
    }
}
