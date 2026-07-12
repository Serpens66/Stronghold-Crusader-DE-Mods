using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Zhuqiaomon.Assembly;
using Zhuqiaomon.Extensions;
using Zhuqiaomon.Hooks;
using Zhuqiaomon.Hooks.Transaction;
using Zhuqiaomon.Memory;

namespace SomeSettings
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

        // c_game_building_delete:
        // This catches non-UI AI demolition paths which do not call c_game_building_bulldoze.
        private const string BuildingDeletePattern =
            "48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 48 89 7C 24 ?? 41 56 48 83 EC ?? 41 BE";

        private const byte ActiveState = 0;
        private const byte SleepingState = 1;
        private const long SingleBuildingOverrideInfoLogIntervalMilliseconds = 2000;

        private static readonly ulong PlayerOwnerDistanceFromSleeping = GetPlayerOwnerDistanceFromSleeping();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate long BuildingDeleteDelegate(NativePointer<GameBuildingManager> buildingManager, int buildingId);

        private readonly ManualLogSource log;
        private readonly SomeSettingsViewModel settings;
        private readonly HookTransaction transaction;
        private readonly Dictionary<int, NativeSleepOverrideLogState> nativeSleepOverrideLogStates = new Dictionary<int, NativeSleepOverrideLogState>();
        private HookRef<X64InlineHook> sleepStateHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> emergencyDemolitionHook = new HookRef<X64InlineHook>();
        private HookRef<X64ManagedFunctionDetourAOB<BuildingDeleteDelegate>> buildingDeleteHook =
            new HookRef<X64ManagedFunctionDetourAOB<BuildingDeleteDelegate>>();
        private bool pauseCallbackFailureLogged;
        private bool singleBuildingOverrideCallbackFailureLogged;
        private bool emergencyCallbackFailureLogged;
        private bool deleteCallbackFailureLogged;
        private bool disposed;

        public AIEconomyProtectionHook(ManualLogSource log, SomeSettingsViewModel settings, IntPtr libraryHandle, ReadOnlySpan<byte> memory)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));

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

            transaction.AddContextHook(
                ref emergencyDemolitionHook,
                EmergencyDemolitionComparisonPattern,
                PreventEmergencyDemolition,
                regs: X64SmartCPUContextRegs.Volatile,
                errorMode: CallbackErrorMode.LogAndContinue,
                placement: OverwrittenInstructionPlacement.AfterCallback);

            transaction.AddDetour(
                ref buildingDeleteHook,
                BuildingDeletePattern,
                PreventLiveAIHovelDelete);

            transaction.Commit();

            if (!sleepStateHook.Success)
                throw new InvalidOperationException("The AI building sleep-state AOB signature was not found.");
            if (!emergencyDemolitionHook.Success)
                throw new InvalidOperationException("The AI emergency-demolition AOB signature was not found.");
            if (!buildingDeleteHook.Success)
                throw new InvalidOperationException("The building delete AOB signature was not found.");

            LogInfo("AI economy native hooks installed; single-building sleep override support is active.");
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            transaction.Unload();
            transaction.Dispose();
        }

        private void PreventAIPause(NativePointer<X64SmartCPUContext> context)
        {
            try
            {
                X64SmartCPUContext* registers = context.Pointer;
                if (ApplySingleBuildingSleepOverride(registers))
                    return;

                if (!settings.EnableMod || !settings.PreventAIPause)
                    return;

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
                if (!pauseCallbackFailureLogged)
                {
                    pauseCallbackFailureLogged = true;
                    LogError($"AI pause prevention callback failed; this pause uses vanilla behavior: {ex}");
                }
            }
        }

        private bool ApplySingleBuildingSleepOverride(X64SmartCPUContext* registers)
        {
            try
            {
                if (!settings.EnableMod)
                    return false;

                IntPtr sleepingAddress = unchecked((IntPtr)(long)registers->R8);
                if (!SingleBuildingPauseHook.TryResolveManualOverrideForSleepingAddress(sleepingAddress, out SingleBuildingPauseHook.ManualSleepOverrideMatch match))
                    return false;

                byte desiredState = (byte)(match.IsSleeping ? 1 : 0);
                byte requestedState = (byte)registers->RCX;
                byte currentState = *(byte*)registers->R8;
                bool adjustedRequest = requestedState != desiredState;
                bool wroteMemory = currentState != desiredState;

                if (wroteMemory)
                    *(byte*)registers->R8 = desiredState;

                if (adjustedRequest)
                    registers->RCX = (registers->RCX & ~0xFFUL) | desiredState;

                if (adjustedRequest || wroteMemory)
                {
                    LogSingleBuildingNativeOverride(
                        match.BuildingId,
                        match.BuildingType,
                        match.Owner,
                        currentState,
                        requestedState,
                        desiredState,
                        adjustedRequest,
                        wroteMemory);
                }

                return true;
            }
            catch (Exception ex)
            {
                if (!singleBuildingOverrideCallbackFailureLogged)
                {
                    singleBuildingOverrideCallbackFailureLogged = true;
                    LogError($"single-building sleep native override failed; this sync uses vanilla behavior: {ex}");
                }

                return false;
            }
        }

        private void LogSingleBuildingNativeOverride(
            int buildingId,
            eStructs buildingType,
            int owner,
            byte currentState,
            byte requestedState,
            byte desiredState,
            bool adjustedRequest,
            bool wroteMemory)
        {
            long now = Stopwatch.GetTimestamp();
            if (!nativeSleepOverrideLogStates.TryGetValue(buildingId, out NativeSleepOverrideLogState state))
            {
                state = new NativeSleepOverrideLogState();
                nativeSleepOverrideLogStates[buildingId] = state;
            }

            long elapsedMilliseconds = state.LastInfoLogTimestamp == 0
                ? SingleBuildingOverrideInfoLogIntervalMilliseconds
                : (now - state.LastInfoLogTimestamp) * 1000 / Stopwatch.Frequency;

            if (elapsedMilliseconds >= SingleBuildingOverrideInfoLogIntervalMilliseconds)
            {
                string suppressedSummary = state.SuppressedRepeats > 0
                    ? $", suppressedRepeats={state.SuppressedRepeats}"
                    : string.Empty;

                LogInfo($"single-building sleep native override: buildingId={buildingId}, type={buildingType}, owner={owner}, currentState={currentState}, vanillaRequested={requestedState}, desiredState={desiredState}, adjustedRequest={adjustedRequest}, wroteMemory={wroteMemory}{suppressedSummary}.");
                state.LastInfoLogTimestamp = now;
                state.SuppressedRepeats = 0;
                return;
            }

            state.SuppressedRepeats++;
        }

        private void PreventEmergencyDemolition(NativePointer<X64SmartCPUContext> context)
        {
            try
            {
                if (!settings.EnableMod || !settings.PreventEmergencyDemolition)
                    return;

                X64SmartCPUContext* registers = context.Pointer;
                byte* emergencyDemolitionRequested = (byte*)(registers->RSP + 0x80);
                if (*emergencyDemolitionRequested == 0)
                    return;

                *emergencyDemolitionRequested = 0;
            }
            catch (Exception ex)
            {
                if (!emergencyCallbackFailureLogged)
                {
                    emergencyCallbackFailureLogged = true;
                    LogError($"AI emergency-demolition prevention callback failed; this check uses vanilla behavior: {ex}");
                }
            }
        }

        private long PreventLiveAIHovelDelete(NativePointer<GameBuildingManager> buildingManager, int buildingId)
        {
            try
            {
                if (settings.EnableMod &&
                    settings.PreventHovelDeletion &&
                    ShouldBlockLiveAIHovelDelete(buildingId, out _))
                {
                    // In the measured AI retry loop, the same hovel was targeted
                    // around 22 times per minute. Blocking here is still cheap:
                    // this detour runs only on actual delete attempts and returns
                    // before the game mutates building state.
                    return 0;
                }
            }
            catch (Exception ex)
            {
                if (!deleteCallbackFailureLogged)
                {
                    deleteCallbackFailureLogged = true;
                    LogError($"AI live hovel deletion prevention failed; this delete uses vanilla behavior: {ex}");
                }
            }

            return buildingDeleteHook.Value.Hook.Trampoline(buildingManager, buildingId);
        }

        private static bool ShouldBlockLiveAIHovelDelete(int buildingId, out ushort ownerId)
        {
            ownerId = 0;

            if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out GameBuilding* building))
                return false;

            if (building->r_AliveState != AliveState.IsAlive ||
                building->r_BuildingType != eStructs.STRUCT_HOVEL ||
                building->r_CurrentHealth <= 0)
            {
                return false;
            }

            ownerId = building->r_PlayerIdOwner;
            return GamePlayerManagerAPI.Instance.IsAIPlayer(ownerId);
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

        private void LogInfo(string message)
        {
            log.LogInfo($"[{TimestampNow()}] SomeSettings {message}");
        }

        private void LogError(string message)
        {
            log.LogError($"[{TimestampNow()}] SomeSettings {message}");
        }

        private static string TimestampNow()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
        }

        private sealed class NativeSleepOverrideLogState
        {
            public long LastInfoLogTimestamp;
            public int SuppressedRepeats;
        }
    }
}
