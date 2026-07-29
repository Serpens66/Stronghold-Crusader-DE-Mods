using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using Zhuqiaomon.Assembly;
using Zhuqiaomon.Hooks;
using Zhuqiaomon.Hooks.Transaction;
using Zhuqiaomon.Memory;

namespace TroopMovementFix
{
    internal sealed unsafe class SelectionUnitTypeMovementGuard : IDisposable
    {
        // Immediately before Vanilla dispatches the current unit's type handler.
        // RDX still contains the one-based current Unit ID.
        private const string BeforeUnitTypeHandlerPattern =
            "48 0F BF 84 19 E6 06 00 00 " +
            "41 FF 94 C6 ?? ?? ?? ?? " +
            "8B 15 ?? ?? ?? ??";

        // The first instruction after the same indirect handler call.
        private const string AfterUnitTypeHandlerPattern =
            "8B 15 ?? ?? ?? ?? 48 63 C2 48 69 C8 90 04 00 00 " +
            "66 83 BC 19 E6 06 00 00 37";

        internal delegate bool TryGetSelectionTribeViewDelegate(
            int unitId,
            out ushort tribeId);

        private readonly ManualLogSource log;
        private readonly TryGetSelectionTribeViewDelegate tryGetSelectionTribeView;
        private readonly HookTransaction transaction;
        private readonly GameUnit* unitArray;
        private readonly int unitArrayLength;

        private HookRef<X64InlineHook> beforeHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> afterHook = new HookRef<X64InlineHook>();

        private GameUnit* guardedUnit;
        private ushort temporaryTribeId;
        private bool callbackFailureLogged;
        private bool unexpectedMutationLogged;
        private bool disposed;

        public SelectionUnitTypeMovementGuard(
            ManualLogSource log,
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            TryGetSelectionTribeViewDelegate tryGetSelectionTribeView)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.tryGetSelectionTribeView =
                tryGetSelectionTribeView ??
                throw new ArgumentNullException(nameof(tryGetSelectionTribeView));

            var units = GameUnitManagerAPI.Instance.GetUnitArray();
            unitArray = units._array;
            unitArrayLength = units.Length;
            if (unitArray == null || unitArrayLength <= 0)
                throw new InvalidOperationException("The native unit array is not available.");

            transaction = new HookTransaction(
                memory,
                libraryBase,
                loggerFactory: null,
                failureMode: TransactionFailureMode.RollbackAndThrow);

            transaction.AddContextHook(
                ref beforeHook,
                BeforeUnitTypeHandlerPattern,
                BeforeUnitTypeHandler,
                regs: X64SmartCPUContextRegs.Volatile,
                errorMode: CallbackErrorMode.LogAndContinue,
                placement: OverwrittenInstructionPlacement.AfterCallback);

            transaction.AddContextHook(
                ref afterHook,
                AfterUnitTypeHandlerPattern,
                AfterUnitTypeHandler,
                regs: X64SmartCPUContextRegs.Volatile,
                errorMode: CallbackErrorMode.LogAndContinue,
                placement: OverwrittenInstructionPlacement.AfterCallback);

            transaction.Commit();

            if (!beforeHook.Success || !afterHook.Success)
            {
                throw new InvalidOperationException(
                    "One or more native unit-type movement guard points were not found.");
            }

            Shared.DebugLogHelper.LogInfo(
                log,
                "Native pre/post unit-type movement guard hooks installed successfully.");
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            RestoreGuardedUnit();
            transaction?.Dispose();
        }

        private void BeforeUnitTypeHandler(
            NativePointer<X64SmartCPUContext> context)
        {
            try
            {
                RestoreGuardedUnit();

                int unitId = checked((int)context.Pointer->RDX);
                if (unitId <= 0 || unitId > unitArrayLength)
                    return;

                GameUnit* unit = &unitArray[unitId - 1];
                if (unit->r_AliveState != AliveState.IsAlive ||
                    unit->r_TribeId != 0 ||
                    !tryGetSelectionTribeView(unitId, out ushort tribeId) ||
                    tribeId == 0)
                {
                    return;
                }

                guardedUnit = unit;
                temporaryTribeId = tribeId;
                unit->r_TribeId = tribeId;
            }
            catch (Exception ex)
            {
                RestoreGuardedUnit();
                if (callbackFailureLogged)
                    return;

                callbackFailureLogged = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"The pre-handler selection movement guard failed; " +
                    $"the affected unit keeps Vanilla selection behavior: {ex}");
            }
        }

        private void AfterUnitTypeHandler(
            NativePointer<X64SmartCPUContext> context)
        {
            try
            {
                RestoreGuardedUnit();
            }
            catch (Exception ex)
            {
                if (callbackFailureLogged)
                    return;

                callbackFailureLogged = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"The post-handler selection movement guard failed: {ex}");
            }
        }

        private void RestoreGuardedUnit()
        {
            if (guardedUnit == null)
                return;

            // Do not overwrite a legitimate assignment if a handler ever starts
            // changing tribe membership itself in a future game version.
            if (guardedUnit->r_TribeId == temporaryTribeId)
            {
                guardedUnit->r_TribeId = 0;
            }
            else if (!unexpectedMutationLogged)
            {
                unexpectedMutationLogged = true;
                Shared.DebugLogHelper.LogWarning(
                    log,
                    "A guarded unit changed tribe inside its Vanilla type handler; " +
                    "Fix2 kept the new tribe assignment.");
            }

            guardedUnit = null;
            temporaryTribeId = 0;
        }
    }
}
