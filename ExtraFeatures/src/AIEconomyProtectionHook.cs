// Feature: Protect AI production buildings, hovels, and emergency economy structures.
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using Zhuqiaomon.Assembly;
using Zhuqiaomon.Extensions;
using Zhuqiaomon.Hooks;
using Zhuqiaomon.Hooks.Transaction;
using Zhuqiaomon.Memory;

namespace ExtraFeatures
{
    internal sealed unsafe class AIEconomyProtectionHook : IDisposable
    {
        // c_game_building_sync_sleep_state:
        // cmp [r8], cl; je unchanged; mov [r8], cl; begin destructive reset block
        private const string SleepStateComparisonPattern =
            "41 38 08 0F 84 ?? ?? ?? ?? 41 88 08 66 41 89 B8 ?? ?? ?? ??";

        // c_game_building_sync_sleep_state function start. This is the vanilla
        // manager-wide synchronization routine containing the comparison above.
        private const string SleepStateSynchronizationFunctionPattern =
            "40 53 41 BA 01 00 00 00 48 8B D9 44 39 51 50 0F 8E ?? ?? ?? ?? 48 89 74 24 10 4C 8D 81 1E 06 00 00";

        // c_game_ai_strategy_update:
        // cmp emergencyDemolitionRequested, 0; je afterEmergencyDemolition
        // The skipped block selectively bulldozes the AI's buildings to recover
        // resources while it is under pressure. Other demolition paths stay intact.
        private const string EmergencyDemolitionComparisonPattern =
            "80 BC 24 80 00 00 00 00 0F 84 ?? ?? ?? ?? 4C 8D BD ?? ?? ?? ?? 8B D6 4D 03 FE";

        // AI hovel-demolition routine:
        // After checking the AI economy thresholds, it requests structure type 1
        // (STRUCT_HOVEL), grants the demolition refund, and deletes that building.
        // Hooking this decision point keeps defeat cleanup and every other game-side
        // call to c_game_building_delete completely outside this setting.
        private const string AIHovelDemolitionFunctionPattern =
            "48 89 5C 24 08 57 48 83 EC 20 48 63 FA 48 8D 15 ?? ?? ?? ?? 48 69 CF 3C 58 00 00 83 BC 11 C0 0E 13 00 00 74 ?? 8B 84 11 40 0D 13 00 3B 84 11 34 EC 12 00";

        // c_game_ai_check_inaccessible_building:
        // cmp inaccessibleChecks, 20; jl return; begin this helper's delete block.
        // Lowering CX before this comparison skips only the unreachable-building deletion.
        // Reference DLL SHA-256: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
        private const string InaccessibleBuildingComparisonPattern =
            "66 83 F9 14 7C ?? 45 0F BF 84 31 4C 01 00 00 48 8D 0D ?? ?? ?? ?? 41 0F BF 94 31 4A 01 00 00";
        private const int SleepStateComparisonRva = 0xC7DCB;
        private const int SleepStateSynchronizationFunctionRva = 0xC7D50;
        private const int EmergencyDemolitionComparisonRva = 0x2F454;
        private const int AIHovelDemolitionFunctionRva = 0x3B1D0;
        private const int InaccessibleBuildingComparisonRva = 0x3B2FF;
        private const ulong InaccessibleCounterOffsetFromR8 = 0x338;

        private const byte ActiveState = 0;
        private const byte SleepingState = 1;

        private static readonly ulong PlayerOwnerDistanceFromSleeping = GetPlayerOwnerDistanceFromSleeping();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int AIHovelDemolitionDelegate(IntPtr aiManager, int playerId);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void SynchronizeSleepStatesDelegate(NativePointer<GameBuildingManager> buildingManager);

        private readonly ManualLogSource log;
        private readonly ExtraFeaturesViewModel settings;
        private readonly bool aiPauseProtectionSupported;
        private readonly bool inaccessibleBuildingProtectionSupported;
        private readonly HookTransaction transaction;
        private HookRef<X64InlineHook> sleepStateHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> emergencyDemolitionHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> inaccessibleBuildingDemolitionHook = new HookRef<X64InlineHook>();
        private HookRef<X64ManagedFunctionDetourAOB<AIHovelDemolitionDelegate>> aiHovelDemolitionHook =
            new HookRef<X64ManagedFunctionDetourAOB<AIHovelDemolitionDelegate>>();
        private readonly SynchronizeSleepStatesDelegate synchronizeSleepStates;
        private readonly AIBuildingTemporaryAccessClassifier temporaryAccessClassifier;
        private bool pauseCallbackFailureLogged;
        private bool singleBuildingOverrideCallbackFailureLogged;
        private bool emergencyCallbackFailureLogged;
        private bool hovelDemolitionCallbackFailureLogged;
        private bool inaccessibleDemolitionCallbackFailureLogged;
        private const int MaximumInaccessibleDiagnosticSamplesPerMap = 8;
        private readonly HashSet<InaccessibleDiagnosticKey> inaccessibleDiagnosticSamples =
            new HashSet<InaccessibleDiagnosticKey>();
        private int lastInaccessibleDiagnosticTick = int.MinValue;
        private bool inaccessibleDiagnosticSamplingCompleteLogged;
        private bool disposed;

        public AIEconomyProtectionHook(
            ManualLogSource log,
            ExtraFeaturesViewModel settings,
            IntPtr libraryHandle,
            ReadOnlySpan<byte> memory,
            bool referenceHashMatches)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            aiPauseProtectionSupported = referenceHashMatches;
            inaccessibleBuildingProtectionSupported = referenceHashMatches;

            ulong libraryBase = unchecked((ulong)libraryHandle.ToInt64());
            int synchronizationRva = Resolve(
                memory, SleepStateSynchronizationFunctionPattern, SleepStateSynchronizationFunctionRva,
                referenceHashMatches, "building sleep-state synchronization function");
            int sleepComparisonRva = Resolve(
                memory, SleepStateComparisonPattern, SleepStateComparisonRva,
                referenceHashMatches, "building sleep-state comparison");
            int emergencyRva = Resolve(
                memory, EmergencyDemolitionComparisonPattern, EmergencyDemolitionComparisonRva,
                referenceHashMatches, "AI emergency-demolition comparison");
            int aiHovelDemolitionRva = Resolve(
                memory, AIHovelDemolitionFunctionPattern, AIHovelDemolitionFunctionRva,
                referenceHashMatches, "AI hovel-demolition function");
            int inaccessibleBuildingComparisonRva = referenceHashMatches
                ? Resolve(
                    memory, InaccessibleBuildingComparisonPattern, InaccessibleBuildingComparisonRva,
                    true, "AI inaccessible-building demolition comparison")
                : -1;

            temporaryAccessClassifier = new AIBuildingTemporaryAccessClassifier(log);
            if (!referenceHashMatches)
            {
                log.LogWarning(
                    $"[{TimestampNow()}] Extra Features layout-dependent AI pause and inaccessible " +
                    "building-demolition protection are disabled for this unknown CrusaderDE.dll; " +
                    "Vanilla behavior is retained for those settings.");
            }

            synchronizeSleepStates = Marshal.GetDelegateForFunctionPointer<SynchronizeSleepStatesDelegate>(
                unchecked((IntPtr)(long)(libraryBase + (ulong)synchronizationRva)));

            transaction = new HookTransaction(
                memory,
                unchecked((ulong)libraryHandle.ToInt64()),
                loggerFactory: null,
                failureMode: TransactionFailureMode.RollbackAndThrow);

            transaction.AddContextHook(
                ref sleepStateHook,
                libraryBase + unchecked((ulong)sleepComparisonRva),
                PreventAIPause,
                regs: X64SmartCPUContextRegs.Volatile,
                errorMode: CallbackErrorMode.LogAndContinue,
                placement: OverwrittenInstructionPlacement.AfterCallback);

            transaction.AddContextHook(
                ref emergencyDemolitionHook,
                libraryBase + unchecked((ulong)emergencyRva),
                PreventEmergencyDemolition,
                regs: X64SmartCPUContextRegs.Volatile,
                errorMode: CallbackErrorMode.LogAndContinue,
                placement: OverwrittenInstructionPlacement.AfterCallback);

            if (inaccessibleBuildingProtectionSupported)
            {
                transaction.AddContextHook(
                    ref inaccessibleBuildingDemolitionHook,
                    libraryBase + unchecked((ulong)inaccessibleBuildingComparisonRva),
                    PreventInaccessibleBuildingDemolition,
                    regs: X64SmartCPUContextRegs.Volatile | X64SmartCPUContextRegs.RDI,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
            }

            transaction.AddDetour(
                ref aiHovelDemolitionHook,
                libraryBase + unchecked((ulong)aiHovelDemolitionRva),
                PreventAIHovelDemolition);

            transaction.Commit();

            if (!sleepStateHook.Success)
                throw new InvalidOperationException("The AI building sleep-state AOB signature was not found.");
            if (!emergencyDemolitionHook.Success)
                throw new InvalidOperationException("The AI emergency-demolition AOB signature was not found.");
            if (!aiHovelDemolitionHook.Success)
                throw new InvalidOperationException("The AI hovel-demolition AOB signature was not found.");
            if (inaccessibleBuildingProtectionSupported && !inaccessibleBuildingDemolitionHook.Success)
                throw new InvalidOperationException("The AI inaccessible-building demolition AOB signature was not found.");
        }

        private int Resolve(
            ReadOnlySpan<byte> memory,
            string pattern,
            int referenceRva,
            bool referenceHashMatches,
            string name)
        {
            return Shared.NativePatternResolver.ResolveUnique(
                memory, pattern, referenceRva, referenceHashMatches, name, log).Rva;
        }

        internal void SynchronizeSleepStatesNow()
        {
            synchronizeSleepStates(GameBuildingManagerAPI.Instance.GetBuildingManager());
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

                if (!settings.EnableMod || !settings.PreventAIPause || !aiPauseProtectionSupported)
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
                bool adjustedRequest = requestedState != desiredState;

                if (adjustedRequest)
                    registers->RCX = (registers->RCX & ~0xFFUL) | desiredState;

                // Do not write the state field here. The overwritten native
                // comparison must see a real change and execute the game's full
                // worker reset/reassignment bookkeeping before writing the state.

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

        private int PreventAIHovelDemolition(IntPtr aiManager, int playerId)
        {
            try
            {
                if (settings.EnableMod &&
                    settings.PreventHovelDeletion &&
                    GamePlayerManagerAPI.Instance.IsAIPlayer(playerId))
                {
                    // Returning false mirrors the routine's no-demolition result.
                    // Its sole caller then continues the normal AI update without
                    // issuing a refund or entering the global building delete path.
                    return 0;
                }
            }
            catch (Exception ex)
            {
                if (!hovelDemolitionCallbackFailureLogged)
                {
                    hovelDemolitionCallbackFailureLogged = true;
                    LogError($"AI hovel-demolition prevention failed; this AI decision uses vanilla behavior: {ex}");
                }
            }

            return aiHovelDemolitionHook.Value.Hook.Trampoline(aiManager, playerId);
        }

        private void PreventInaccessibleBuildingDemolition(NativePointer<X64SmartCPUContext> context)
        {
            try
            {
                int mode = settings.InaccessibleAIBuildingDemolitionProtection;
                if (!settings.EnableMod || !inaccessibleBuildingProtectionSupported ||
                    mode == TemporaryGateBlockagePolicy.VanillaMode)
                    return;

                X64SmartCPUContext* registers = context.Pointer;
                ushort vanillaCounter = (ushort)registers->RCX;
                if (vanillaCounter < 20)
                    return;

                int buildingId = unchecked((int)(uint)registers->RDI);
                GameBuilding* building = null;
                bool isLivingAiBuilding =
                    buildingId > 0 &&
                    GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out building) &&
                    building != null &&
                    building->r_AliveState == AliveState.IsAlive &&
                    GamePlayerManagerAPI.Instance.IsAIPlayer(building->r_PlayerIdOwner);
                if (!isLivingAiBuilding)
                    return;

                bool classificationAvailable = temporaryAccessClassifier.TryClassify(
                    buildingId,
                    out AIBuildingAccessDiagnostic diagnostic);
                bool reachableUnderImprovedCheck =
                    classificationAvailable && diagnostic.IsReachableUnderImprovedCheck;

                bool suppressDemolition = TemporaryGateBlockagePolicy.ShouldSuppressDemolition(
                    mode,
                    isLivingAiBuilding,
                    classificationAvailable,
                    reachableUnderImprovedCheck);

                ushort storedCounterBefore = vanillaCounter;
                ushort storedCounterAfter = vanillaCounter;
                bool counterReset = false;
                if (suppressDemolition)
                {
                    if (registers->R8 == 0)
                        throw new InvalidOperationException("Vanilla inaccessible-building counter base register R8 is null.");

                    ushort* storedCounter = (ushort*)(registers->R8 + InaccessibleCounterOffsetFromR8);
                    storedCounterBefore = *storedCounter;
                    if (storedCounterBefore != vanillaCounter)
                    {
                        throw new InvalidOperationException(
                            $"Vanilla inaccessible-building counter mismatch: cx={vanillaCounter}, stored={storedCounterBefore}.");
                    }

                    // Reset the source value as well as CX so reopening a gate cannot expose a stale 20.
                    *storedCounter = 0;
                    storedCounterAfter = *storedCounter;
                    registers->RCX &= ~0xFFFFUL;
                    counterReset = true;
                }

                LogInaccessibleBuildingComparison(
                    buildingId,
                    building,
                    vanillaCounter,
                    storedCounterBefore,
                    storedCounterAfter,
                    counterReset,
                    mode,
                    classificationAvailable,
                    diagnostic,
                    suppressDemolition);
            }
            catch (Exception ex)
            {
                if (!inaccessibleDemolitionCallbackFailureLogged)
                {
                    inaccessibleDemolitionCallbackFailureLogged = true;
                    LogError($"AI inaccessible-building demolition callback failed; this check uses vanilla behavior: {ex}");
                }
            }
        }

        private void LogInaccessibleBuildingComparison(
            int buildingId,
            GameBuilding* building,
            ushort vanillaCounter,
            ushort storedCounterBefore,
            ushort storedCounterAfter,
            bool counterReset,
            int mode,
            bool classificationAvailable,
            AIBuildingAccessDiagnostic diagnostic,
            bool suppressDemolition)
        {
            int tick = diagnostic.Tick;
            if (tick < lastInaccessibleDiagnosticTick)
            {
                inaccessibleDiagnosticSamples.Clear();
                inaccessibleDiagnosticSamplingCompleteLogged = false;
            }
            lastInaccessibleDiagnosticTick = tick;

            string classification = classificationAvailable
                ? diagnostic.Kind.ToString()
                : "UnavailableFailOpen";
            string summary = string.IsNullOrEmpty(diagnostic.Details)
                ? "failureReason=classification-data-unavailable"
                : diagnostic.Details;
            var sampleKey = new InaccessibleDiagnosticKey(
                mode,
                classification,
                diagnostic.HasDirectPclPath,
                diagnostic.HasPathWithFriendlyGates,
                diagnostic.NativePlayerAwareReachable,
                counterReset,
                suppressDemolition);
            if (inaccessibleDiagnosticSamples.Contains(sampleKey))
                return;
            if (inaccessibleDiagnosticSamples.Count >= MaximumInaccessibleDiagnosticSamplesPerMap)
            {
                if (!inaccessibleDiagnosticSamplingCompleteLogged)
                {
                    inaccessibleDiagnosticSamplingCompleteLogged = true;
                    Shared.DebugLogHelper.LogDebug(
                        log,
                        $"Extra Features AI inaccessible-building diagnostics sampled " +
                        $"{MaximumInaccessibleDiagnosticSamplesPerMap} distinct outcomes; further samples on this map are omitted.");
                }
                return;
            }
            inaccessibleDiagnosticSamples.Add(sampleKey);

            Shared.DebugLogHelper.LogDebug(
                log,
                $"Extra Features AI inaccessible-building sample: " +
                $"tick={tick}, buildingId={buildingId}, buildingGlobalId={building->r_GlobalId}, " +
                $"buildingType={building->r_BuildingType}, owner={building->r_PlayerIdOwner}, " +
                $"vanillaCounter={vanillaCounter}, " +
                $"storedCounterBefore={storedCounterBefore}, storedCounterAfter={storedCounterAfter}, " +
                $"counterReset={counterReset}, " +
                $"nativePlayerAwareReachable={FormatNullableBoolean(diagnostic.NativePlayerAwareReachable)}, " +
                $"directPclReachable={diagnostic.HasDirectPclPath}, " +
                $"reachableWithAlwaysPassableFriendlyGates={diagnostic.HasPathWithFriendlyGates}, " +
                $"mode={mode}, modClassification={classification}, " +
                $"modDecision={(suppressDemolition ? "SuppressDemolition" : "AllowVanilla")}, " +
                summary);
        }

        private static string FormatNullableBoolean(bool? value) =>
            value.HasValue ? (value.Value ? "True" : "False") : "NotChecked";

        private static ulong GetPlayerOwnerDistanceFromSleeping()
        {
            int sleepingOffset = Marshal.OffsetOf(typeof(GameBuilding), nameof(GameBuilding.r_IsSleeping)).ToInt32();
            int playerOwnerOffset = Marshal.OffsetOf(typeof(GameBuilding), nameof(GameBuilding.r_PlayerIdOwner)).ToInt32();
            int distance = sleepingOffset - playerOwnerOffset;

            if (distance <= 0)
                throw new InvalidOperationException("The GameBuilding layout has an invalid r_IsSleeping/r_PlayerIdOwner ordering.");

            return checked((ulong)distance);
        }

        private void LogError(string message)
        {
            log.LogError($"[{TimestampNow()}] Extra Features {message}");
        }

        private static string TimestampNow()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
        }

        private readonly struct InaccessibleDiagnosticKey : IEquatable<InaccessibleDiagnosticKey>
        {
            internal InaccessibleDiagnosticKey(
                int mode,
                string classification,
                bool hasDirectPclPath,
                bool hasPathWithFriendlyGates,
                bool? nativePlayerAwareReachable,
                bool counterReset,
                bool suppressDemolition)
            {
                Mode = mode;
                Classification = classification ?? string.Empty;
                HasDirectPclPath = hasDirectPclPath;
                HasPathWithFriendlyGates = hasPathWithFriendlyGates;
                NativePlayerAwareReachable = nativePlayerAwareReachable;
                CounterReset = counterReset;
                SuppressDemolition = suppressDemolition;
            }

            private int Mode { get; }
            private string Classification { get; }
            private bool HasDirectPclPath { get; }
            private bool HasPathWithFriendlyGates { get; }
            private bool? NativePlayerAwareReachable { get; }
            private bool CounterReset { get; }
            private bool SuppressDemolition { get; }

            public bool Equals(InaccessibleDiagnosticKey other) =>
                Mode == other.Mode &&
                HasDirectPclPath == other.HasDirectPclPath &&
                HasPathWithFriendlyGates == other.HasPathWithFriendlyGates &&
                NativePlayerAwareReachable == other.NativePlayerAwareReachable &&
                CounterReset == other.CounterReset &&
                SuppressDemolition == other.SuppressDemolition &&
                string.Equals(Classification, other.Classification, StringComparison.Ordinal);

            public override bool Equals(object obj) =>
                obj is InaccessibleDiagnosticKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = Mode;
                    hash = hash * 397 ^ HasDirectPclPath.GetHashCode();
                    hash = hash * 397 ^ HasPathWithFriendlyGates.GetHashCode();
                    hash = hash * 397 ^ NativePlayerAwareReachable.GetHashCode();
                    hash = hash * 397 ^ CounterReset.GetHashCode();
                    hash = hash * 397 ^ SuppressDemolition.GetHashCode();
                    return hash * 397 ^ StringComparer.Ordinal.GetHashCode(Classification);
                }
            }
        }

    }
}
