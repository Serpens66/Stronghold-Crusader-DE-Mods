using BepInEx.Logging;
using Iced.Intel;
using SHCDESE.API;
using SHCDESE.Interop;
using System;
using System.Runtime.InteropServices;
using Zhuqiaomon.Assembly;
using Zhuqiaomon.Extensions;
using Zhuqiaomon.Hooks;
using Zhuqiaomon.Hooks.Transaction;
using Zhuqiaomon.Memory;

namespace AIEconomyProtection
{
    internal sealed unsafe class AIEconomyProtectionHook : IDisposable
    {
        // c_game_building_sync_sleep_state:
        // cmp [r8], cl; je unchanged; mov [r8], cl; begin destructive reset block
        private const string SleepStateComparisonPattern =
            "41 38 08 0F 84 ?? ?? ?? ?? 41 88 08 66 41 89 B8 ?? ?? ?? ??";

        // c_game_ai_strategy_update:
        // cmp emergencyDemolitionRequested, 0; je afterEmergencyDemolition
        // The skipped block selectively bulldozes the AI's buildings to recover
        // resources while it is under pressure. Other demolition paths stay intact.
        private const string EmergencyDemolitionComparisonPattern =
            "80 BC 24 80 00 00 00 00 0F 84 ?? ?? ?? ?? 4C 8D BD ?? ?? ?? ?? 8B D6 4D 03 FE";

        private const byte ActiveState = 0;
        private const byte SleepingState = 1;

        private static readonly ulong PlayerOwnerDistanceFromSleeping = GetPlayerOwnerDistanceFromSleeping();

        private readonly ManualLogSource log;
        private readonly HookTransaction transaction;
        private HookRef<X64InlineHook> sleepStateHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> emergencyDemolitionHook = new HookRef<X64InlineHook>();
        private bool callbackFailureLogged;
        private bool disposed;

        public AIEconomyProtectionHook(ManualLogSource log, IntPtr libraryHandle, ReadOnlySpan<byte> memory)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));

            transaction = new HookTransaction(
                memory,
                unchecked((ulong)libraryHandle.ToInt64()),
                loggerFactory: null,
                failureMode: TransactionFailureMode.RollbackAndThrow);

            transaction.AddContextHook(
                ref sleepStateHook,
                SleepStateComparisonPattern,
                PreventAIPause,
                regs: X64SmartCPUContextRegs.Volatile,
                errorMode: CallbackErrorMode.LogAndContinue,
                placement: OverwrittenInstructionPlacement.AfterCallback);

            transaction.AddInline(
                ref emergencyDemolitionHook,
                EmergencyDemolitionComparisonPattern,
                (assembler, overwrittenInstructions, returnAddress) =>
                {
                    if (overwrittenInstructions.Length != 2 ||
                        overwrittenInstructions[0].Mnemonic != Mnemonic.Cmp ||
                        overwrittenInstructions[1].Mnemonic != Mnemonic.Je)
                    {
                        throw new InvalidOperationException(
                            "The AI emergency-demolition hook did not resolve to the expected cmp/je instruction pair.");
                    }

                    // Always take the original JE target. This bypasses only the emergency
                    // resource-recovery demolition block and preserves surrounding AI logic.
                    assembler.AddUnrestrictedJmp(overwrittenInstructions[1].NearBranchTarget);
                },
                hookSize: 14);

            transaction.Commit();

            if (!sleepStateHook.Success)
                throw new InvalidOperationException("The AI building sleep-state AOB signature was not found.");
            if (!emergencyDemolitionHook.Success)
                throw new InvalidOperationException("The AI emergency-demolition AOB signature was not found.");

            log.LogDebug(
                $"AI building sleep-state hook installed. Player-owner distance from r_IsSleeping: 0x{PlayerOwnerDistanceFromSleeping:X}.");
            log.LogDebug("AI emergency resource-recovery demolition hook installed.");
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            transaction.Unload();
            transaction.Dispose();
            log.LogDebug("AI economy protection hooks disabled.");
        }

        private void PreventAIPause(NativePointer<X64SmartCPUContext> context)
        {
            try
            {
                X64SmartCPUContext* registers = context.Pointer;
                byte requestedState = (byte)registers->RCX;
                byte currentState = *(byte*)registers->R8;

                if (requestedState != SleepingState || currentState != ActiveState)
                    return;

                ushort playerId = *(ushort*)(registers->R8 - PlayerOwnerDistanceFromSleeping);
                if (!GamePlayerManagerAPI.Instance.IsAIPlayer(playerId))
                    return;

                // Preserve every other RCX bit. The original cmp/je now sees 0 == 0
                // and skips the write plus the complete destructive reset block.
                registers->RCX &= ~0xFFUL;
            }
            catch (Exception ex)
            {
                // Native callbacks must never let managed exceptions escape. Fail open so
                // the original game behavior remains intact if an unexpected problem occurs.
                if (!callbackFailureLogged)
                {
                    callbackFailureLogged = true;
                    log.LogError($"AI pause prevention callback failed; this pause uses vanilla behavior: {ex}");
                }
            }
        }

        private static ulong GetPlayerOwnerDistanceFromSleeping()
        {
            int sleepingOffset = Marshal.OffsetOf(typeof(GameBuilding), nameof(GameBuilding.r_IsSleeping)).ToInt32();
            int playerOwnerOffset = Marshal.OffsetOf(typeof(GameBuilding), nameof(GameBuilding.r_PlayerIdOwner)).ToInt32();
            int distance = sleepingOffset - playerOwnerOffset;

            if (distance <= 0)
                throw new InvalidOperationException("The GameBuilding layout has an invalid r_IsSleeping/r_PlayerIdOwner ordering.");

            return checked((ulong)distance);
        }
    }
}
