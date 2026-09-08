// Feature: Protect AI production buildings from resource-shortage sleep and selected demolitions.
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;

namespace BugfixesAndQoL
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

        // General AI accessibility sweep, immediately after c_game_building_is_accessible.
        // Results 0 and 2 both enter Vanilla's state-3/heatmap path at this site.
        private const string InaccessibleBuildingDecisionPattern =
            "85 C0 75 06 66 44 89 3B EB 11 83 F8 02 75 06 66 44 89 3B EB 06 66 44 39 3B 75 52";
        private const int SleepStateComparisonRva = 0xC7DCB;
        private const int SleepStateComparisonDisplacedLength = 20;
        private const int SleepStateSynchronizationFunctionRva = 0xC7D50;
        private const int EmergencyDemolitionComparisonRva = 0x2F454;
        private const int AIHovelDemolitionFunctionRva = 0x3B1D0;
        private const int InaccessibleBuildingSweepRva = 0xC8F50;
        private const int BuildingAccessibilityFunctionRva = 0xC90E0;
        private const int BuildingAccessibilityCallRva = 0xC8FD2;
        private const int InaccessibleBuildingDecisionRva = 0xC8FD7;
        private const int InaccessibleBuildingDecisionLength = 15;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int AIHovelDemolitionDelegate(IntPtr aiManager, int playerId);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void AIResourceShortageSleepDelegate(IntPtr aiManager, int playerId);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void SynchronizeSleepStatesDelegate(NativePointer<GameBuildingManager> buildingManager);

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly bool aiResourceShortageSleepProtectionSupported;
        private readonly bool inaccessibleBuildingProtectionSupported;
        private readonly HookTransaction transaction;
        private readonly HookHandle<X64InlineHook> sleepStateHook = new HookHandle<X64InlineHook>();
        private readonly HookHandle<X64InlineHook> emergencyDemolitionHook = new HookHandle<X64InlineHook>();
        private readonly HookHandle<X64InlineHook> inaccessibleBuildingDemolitionHook = new HookHandle<X64InlineHook>();
        private readonly DetourHandle<AIHovelDemolitionDelegate> aiHovelDemolitionHook = new DetourHandle<AIHovelDemolitionDelegate>();
        private readonly DetourHandle<AIResourceShortageSleepDelegate> aiResourceShortageSleepHook =
            new DetourHandle<AIResourceShortageSleepDelegate>();
        private readonly SynchronizeSleepStatesDelegate synchronizeSleepStates;
        private readonly AIBuildingTemporaryAccessClassifier temporaryAccessClassifier;
        private SingleBuildingPauseHook singleBuildingPauseHook;
        private bool resourceShortageSleepCallbackFailureLogged;
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
            BugfixesAndQoLViewModel settings,
            ScanRegion region,
            IntPtr libraryHandle,
            ReadOnlySpan<byte> memory,
            bool referenceHashMatches)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            aiResourceShortageSleepProtectionSupported = referenceHashMatches;
            inaccessibleBuildingProtectionSupported = referenceHashMatches;

            ulong libraryBase = unchecked((ulong)libraryHandle.ToInt64());
            int synchronizationRva = Resolve(
                memory, SleepStateSynchronizationFunctionPattern, SleepStateSynchronizationFunctionRva,
                referenceHashMatches, "building sleep-state synchronization function");
            int sleepComparisonRva = Resolve(
                memory, SleepStateComparisonPattern, SleepStateComparisonRva,
                referenceHashMatches, "building sleep-state comparison");
            int resourceShortageSleepRva = referenceHashMatches
                ? Resolve(
                    memory,
                    AIResourceShortageSleepNativeDefinition.FunctionPattern,
                    AIResourceShortageSleepNativeDefinition.FunctionRva,
                    true,
                    "AI resource-shortage sleep planner")
                : -1;
            int emergencyRva = Resolve(
                memory, EmergencyDemolitionComparisonPattern, EmergencyDemolitionComparisonRva,
                referenceHashMatches, "AI emergency-demolition comparison");
            int aiHovelDemolitionRva = Resolve(
                memory, AIHovelDemolitionFunctionPattern, AIHovelDemolitionFunctionRva,
                referenceHashMatches, "AI hovel-demolition function");
            int inaccessibleBuildingDecisionRva = referenceHashMatches
                ? Resolve(
                    memory, InaccessibleBuildingDecisionPattern, InaccessibleBuildingDecisionRva,
                    true, "general AI inaccessible-building decision")
                : -1;

            temporaryAccessClassifier = new AIBuildingTemporaryAccessClassifier(
                log,
                unchecked((IntPtr)(long)(libraryBase +
                    AIBuildingTemporaryAccessClassifier.NativePathManagerRva)));
            if (!referenceHashMatches)
            {
                log.LogWarning(
                    $"[{TimestampNow()}] Bugfixes and QoL layout-dependent AI resource-shortage sleep and inaccessible " +
                    "building-demolition protection are disabled for this unknown CrusaderDE.dll; " +
                    "Vanilla behavior is retained for those settings.");
            }

            synchronizeSleepStates = Marshal.GetDelegateForFunctionPointer<SynchronizeSleepStatesDelegate>(
                unchecked((IntPtr)(long)(libraryBase + (ulong)synchronizationRva)));

            if (inaccessibleBuildingProtectionSupported)
            {
                ValidateInaccessibleBuildingNativeContract(memory, inaccessibleBuildingDecisionRva);
                using (var probe = new X64InlineHook(
                    libraryBase + (ulong)inaccessibleBuildingDecisionRva,
                    InaccessibleBuildingDecisionLength))
                {
                    if (probe.DisplacedByteCount != InaccessibleBuildingDecisionLength)
                    {
                        throw new InvalidOperationException(
                            "Unexpected RedBird inaccessible-building decision span before installation.");
                    }
                }
            }

            transaction = BugfixesHookInfrastructure.CreateOwnedTransaction(region);

            BugfixesHookInfrastructure.AddContextHook(transaction, sleepStateHook,
                libraryBase + unchecked((ulong)sleepComparisonRva),
                ApplySingleBuildingSleepOverrideDuringSynchronization,
                registers: X64SmartCPUContextRegs.Volatile,
                errorMode: CallbackErrorMode.LogAndContinue,
                placement: OverwrittenInstructionPlacement.AfterCallback);

            BugfixesHookInfrastructure.AddContextHook(transaction, emergencyDemolitionHook,
                libraryBase + unchecked((ulong)emergencyRva),
                PreventEmergencyDemolition,
                registers: X64SmartCPUContextRegs.Volatile,
                errorMode: CallbackErrorMode.LogAndContinue,
                placement: OverwrittenInstructionPlacement.AfterCallback);

            if (inaccessibleBuildingProtectionSupported)
            {
                BugfixesHookInfrastructure.AddContextHook(transaction, inaccessibleBuildingDemolitionHook,
                    libraryBase + unchecked((ulong)inaccessibleBuildingDecisionRva),
                    PreventInaccessibleBuildingDemolition,
                    registers: X64SmartCPUContextRegs.Volatile |
                        X64SmartCPUContextRegs.RBX | X64SmartCPUContextRegs.RSI |
                        X64SmartCPUContextRegs.R14 | X64SmartCPUContextRegs.R15,
                    hookSize: InaccessibleBuildingDecisionLength,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
            }

            transaction.AddDetour(
                aiHovelDemolitionHook,
                HookTarget.FromAddress(libraryBase + unchecked((ulong)aiHovelDemolitionRva)),
                PreventAIHovelDemolition);

            if (aiResourceShortageSleepProtectionSupported)
            {
                transaction.AddDetour(
                    aiResourceShortageSleepHook,
                    HookTarget.FromAddress(libraryBase + unchecked((ulong)resourceShortageSleepRva)),
                    PreventAIResourceShortageSleep);
            }

            CommitResult commitResult = transaction.Commit();

            if (!commitResult.IsCompleteSuccess || !sleepStateHook.Success)
                throw new InvalidOperationException("The AI building sleep-state AOB signature was not found.");
            if (sleepStateHook.Hook.DisplacedByteCount != SleepStateComparisonDisplacedLength)
            {
                transaction.Dispose();
                throw new InvalidOperationException(
                    "Unexpected actual building sleep-state overwrite length; hook transaction rolled back.");
            }
            if (!emergencyDemolitionHook.Success)
                throw new InvalidOperationException("The AI emergency-demolition AOB signature was not found.");
            if (!aiHovelDemolitionHook.Success)
                throw new InvalidOperationException("The AI hovel-demolition AOB signature was not found.");
            if (aiResourceShortageSleepProtectionSupported && !aiResourceShortageSleepHook.Success)
                throw new InvalidOperationException("The AI resource-shortage sleep planner hook was not installed.");
            if (inaccessibleBuildingProtectionSupported && !inaccessibleBuildingDemolitionHook.Success)
                throw new InvalidOperationException("The AI inaccessible-building demolition AOB signature was not found.");
            if (inaccessibleBuildingProtectionSupported &&
                inaccessibleBuildingDemolitionHook.Hook.DisplacedByteCount !=
                    InaccessibleBuildingDecisionLength)
            {
                transaction.Dispose();
                throw new InvalidOperationException(
                    "Unexpected actual inaccessible-building overwrite length; hook transaction rolled back.");
            }

            // The callback is inside the manager loop and is only needed while at least
            // one individual building override exists.
            try
            {
                sleepStateHook.Hook.Disable();
                if (sleepStateHook.IsInstalled)
                    throw new InvalidOperationException("The building sleep-state hook remained active after preparation.");
            }
            catch
            {
                transaction.Dispose();
                throw;
            }
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

        private static void ValidateInaccessibleBuildingNativeContract(
            ReadOnlySpan<byte> memory,
            int resolvedDecisionRva)
        {
            if (resolvedDecisionRva != InaccessibleBuildingDecisionRva)
                throw new InvalidOperationException("The general AI accessibility decision resolved outside its audited RVA.");

            ValidateExactBytes(memory, InaccessibleBuildingSweepRva, new byte[]
            {
                0x40, 0x56, 0x57, 0x41, 0x56, 0x48, 0x83, 0xEC,
                0x20, 0xBE, 0x01, 0x00, 0x00, 0x00, 0x44, 0x8B,
                0xF2, 0x48, 0x8B, 0xF9
            }, "general AI accessibility sweep prologue");
            ValidateExactBytes(memory, BuildingAccessibilityFunctionRva, new byte[]
            {
                0x44, 0x89, 0x44, 0x24, 0x18, 0x55, 0x41, 0x57,
                0x48, 0x83, 0xEC, 0x58, 0x48, 0x63, 0xEA, 0x4C,
                0x8B, 0xF9
            }, "building accessibility function prologue");
            ValidateExactBytes(memory, InaccessibleBuildingDecisionRva, new byte[]
            {
                0x85, 0xC0, 0x75, 0x06, 0x66, 0x44, 0x89, 0x3B,
                0xEB, 0x11, 0x83, 0xF8, 0x02, 0x75, 0x06
            }, "complete inaccessible-building decision hook span");

            if ((uint)(BuildingAccessibilityCallRva + 5) > (uint)memory.Length ||
                memory[BuildingAccessibilityCallRva] != 0xE8)
            {
                throw new InvalidOperationException("The audited building-accessibility CALL is missing.");
            }
            int displacement = memory[BuildingAccessibilityCallRva + 1] |
                memory[BuildingAccessibilityCallRva + 2] << 8 |
                memory[BuildingAccessibilityCallRva + 3] << 16 |
                memory[BuildingAccessibilityCallRva + 4] << 24;
            int callTargetRva = BuildingAccessibilityCallRva + 5 + displacement;
            if (callTargetRva != BuildingAccessibilityFunctionRva ||
                BuildingAccessibilityCallRva + 5 != InaccessibleBuildingDecisionRva)
            {
                throw new InvalidOperationException(
                    "The general AI sweep no longer calls the audited accessibility function immediately before the hook.");
            }

            ValidateStructFieldOffset(typeof(GameBuilding), nameof(GameBuilding.r_AliveState), 0xD0);
            ValidateStructFieldOffset(typeof(GameBuilding), nameof(GameBuilding.r_BuildingType), 0xD2);
            ValidateStructFieldOffset(typeof(GameBuilding), nameof(GameBuilding.r_PlayerIdOwner), 0xD6);
            ValidateStructFieldOffset(typeof(GameBuilding), nameof(GameBuilding.r_GlobalId), 0xD8);
            ValidateStructFieldOffset(typeof(GameBuilding), nameof(GameBuilding.r_TilePositionXEnd), 0xFE);
            ValidateStructFieldOffset(typeof(GameBuilding), nameof(GameBuilding.r_TilePositionYEnd), 0x100);
            ValidateStructFieldOffset(typeof(GamePlayerResources), nameof(GamePlayerResources.r_KeepTileId), 0xA0);
        }

        private static void ValidateExactBytes(
            ReadOnlySpan<byte> memory,
            int rva,
            byte[] expected,
            string description)
        {
            if (rva < 0 || expected == null || rva > memory.Length - expected.Length ||
                !memory.Slice(rva, expected.Length).SequenceEqual(expected))
            {
                throw new InvalidOperationException($"Unexpected native bytes for {description} at RVA 0x{rva:X}.");
            }
        }

        private static void ValidateStructFieldOffset(
            Type structType,
            string fieldName,
            int expectedOffset)
        {
            int actualOffset = Marshal.OffsetOf(structType, fieldName).ToInt32();
            if (actualOffset != expectedOffset)
            {
                throw new InvalidOperationException(
                    $"Unexpected {structType.Name}.{fieldName} offset: 0x{actualOffset:X}, expected 0x{expectedOffset:X}.");
            }
        }

        internal void SynchronizeSleepStatesNow()
        {
            synchronizeSleepStates(GameBuildingManagerAPI.Instance.GetBuildingManager());
        }

        internal void SetSingleBuildingPauseHook(SingleBuildingPauseHook hook)
        {
            singleBuildingPauseHook = hook ?? throw new ArgumentNullException(nameof(hook));
        }

        internal void SetSingleBuildingOverrideInterceptionEnabled(bool enabled)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(AIEconomyProtectionHook));
            if (!sleepStateHook.Success)
                throw new InvalidOperationException("The single-building sleep-state hook is unavailable.");

            if (enabled)
            {
                if (!sleepStateHook.IsInstalled)
                    sleepStateHook.Hook.Enable();
                if (!sleepStateHook.IsInstalled)
                    throw new InvalidOperationException("The single-building sleep-state hook did not become active.");
            }
            else if (sleepStateHook.IsInstalled)
            {
                sleepStateHook.Hook.Disable();
                if (sleepStateHook.IsInstalled)
                    throw new InvalidOperationException("The single-building sleep-state hook remained active.");
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            transaction.Dispose();
        }

        private void ApplySingleBuildingSleepOverrideDuringSynchronization(
            NativePointer<X64SmartCPUContext> context)
        {
            ApplySingleBuildingSleepOverride(context.Pointer);
        }

        private void PreventAIResourceShortageSleep(IntPtr aiManager, int playerId)
        {
            // Preserve the complete Vanilla shortage counters, purchasing decisions,
            // and recovery scheduling before clearing this routine's sleep outputs.
            aiResourceShortageSleepHook.Original(aiManager, playerId);

            try
            {
                if (!settings.EnableMod || !settings.PreventAIPause ||
                    !aiResourceShortageSleepProtectionSupported)
                {
                    return;
                }

                GamePlayerManagerAPI playerManagerApi = GamePlayerManagerAPI.Instance;
                if (!playerManagerApi.IsPlayerIdValid(playerId) ||
                    !playerManagerApi.IsAIPlayer(playerId))
                {
                    return;
                }

                IntPtr playerManager = playerManagerApi.GetPlayerManager();
                if (playerManager == IntPtr.Zero ||
                    !AIResourceShortageSleepPolicy.TryClearSleepRequests(
                        (byte*)playerManager.ToPointer(), playerId))
                {
                    throw new InvalidOperationException("The native player manager or player id is invalid.");
                }
            }
            catch (Exception ex)
            {
                if (!resourceShortageSleepCallbackFailureLogged)
                {
                    resourceShortageSleepCallbackFailureLogged = true;
                    LogError(
                        $"AI resource-shortage sleep prevention failed; this AI cycle uses vanilla behavior: {ex}");
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
                SingleBuildingPauseHook pauseHook = singleBuildingPauseHook;
                if (pauseHook == null ||
                    !pauseHook.TryResolveManualOverrideForSleepingAddress(
                        sleepingAddress,
                        out SingleBuildingPauseHook.ManualSleepOverrideMatch match))
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

            return aiHovelDemolitionHook.Original(aiManager, playerId);
        }

        private void PreventInaccessibleBuildingDemolition(NativePointer<X64SmartCPUContext> context)
        {
            X64SmartCPUContext* registers = context.Pointer;
            ulong originalRax = registers->RAX;
            try
            {
                int mode = settings.InaccessibleAIBuildingDemolitionProtection;
                if (!settings.EnableMod || !inaccessibleBuildingProtectionSupported ||
                    mode == TemporaryGateBlockagePolicy.VanillaMode)
                    return;

                int vanillaResult = unchecked((int)(uint)registers->RAX);
                if (vanillaResult != TemporaryGateBlockagePolicy.NoEntranceResult &&
                    vanillaResult != TemporaryGateBlockagePolicy.DisconnectedEntranceResult)
                    return;

                int buildingId = unchecked((int)(uint)registers->RSI);
                int playerId = unchecked((int)(uint)registers->R14);
                GameBuilding* building = null;
                bool isLivingAiBuilding =
                    buildingId > 0 &&
                    GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out building) &&
                    building != null &&
                    building->r_AliveState == AliveState.IsAlive &&
                    building->r_PlayerIdOwner == playerId &&
                    GamePlayerManagerAPI.Instance.IsAIPlayer(building->r_PlayerIdOwner);
                if (!isLivingAiBuilding)
                    return;

                AIBuildingAccessDiagnostic diagnostic =
                    AIBuildingAccessDiagnostic.Unavailable(int.MinValue, "classification-not-required");
                bool classificationAvailable = false;
                if (mode == TemporaryGateBlockagePolicy.ImprovedReachabilityMode &&
                    vanillaResult == TemporaryGateBlockagePolicy.DisconnectedEntranceResult &&
                    building->r_BuildingType != eStructs.STRUCT_STABLES)
                {
                    classificationAvailable = temporaryAccessClassifier.TryClassify(
                        buildingId,
                        playerId,
                        out diagnostic);
                }

                int effectiveResult = TemporaryGateBlockagePolicy.ResolveAccessibilityResult(
                    mode,
                    isLivingAiBuilding,
                    building->r_BuildingType,
                    vanillaResult,
                    classificationAvailable,
                    diagnostic.IsReachableUnderImprovedCheck);
                LogInaccessibleBuildingComparison(
                    buildingId,
                    building,
                    playerId,
                    vanillaResult,
                    effectiveResult,
                    mode,
                    classificationAvailable,
                    diagnostic);
                if (effectiveResult != vanillaResult)
                    registers->RAX = unchecked((uint)effectiveResult);
            }
            catch (Exception ex)
            {
                registers->RAX = originalRax;
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
            int playerId,
            int vanillaResult,
            int effectiveResult,
            int mode,
            bool classificationAvailable,
            AIBuildingAccessDiagnostic diagnostic)
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
                : effectiveResult != vanillaResult &&
                    building->r_BuildingType == eStructs.STRUCT_STABLES
                    ? "ExplicitStableException"
                    : mode == TemporaryGateBlockagePolicy.AlwaysPreventMode &&
                        effectiveResult != vanillaResult
                        ? "AlwaysPreventMode"
                        : "UnavailableFailOpen";
            string summary = string.IsNullOrEmpty(diagnostic.Details)
                ? "failureReason=classification-data-unavailable"
                : diagnostic.Details;
            var sampleKey = new InaccessibleDiagnosticKey(
                mode,
                classification,
                vanillaResult,
                effectiveResult,
                building->r_BuildingType);
            if (inaccessibleDiagnosticSamples.Contains(sampleKey))
                return;
            if (inaccessibleDiagnosticSamples.Count >= MaximumInaccessibleDiagnosticSamplesPerMap)
            {
                if (!inaccessibleDiagnosticSamplingCompleteLogged)
                {
                    inaccessibleDiagnosticSamplingCompleteLogged = true;
                    Shared.DebugLogHelper.LogDebug(
                        log,
                        $"Bugfixes and QoL AI inaccessible-building diagnostics sampled " +
                        $"{MaximumInaccessibleDiagnosticSamplesPerMap} distinct outcomes; further samples on this map are omitted.");
                }
                return;
            }
            inaccessibleDiagnosticSamples.Add(sampleKey);

            Shared.DebugLogHelper.LogDebug(
                log,
                $"Bugfixes and QoL AI inaccessible-building sample: " +
                $"tick={tick}, buildingId={buildingId}, buildingGlobalId={building->r_GlobalId}, " +
                $"buildingType={building->r_BuildingType}, owner={playerId}, " +
                $"vanillaAccessibilityResult={vanillaResult}, effectiveAccessibilityResult={effectiveResult}, " +
                $"buildingPcl={diagnostic.BuildingPcl}, keepPcl={diagnostic.KeepPcl}, " +
                $"mode={mode}, modClassification={classification}, " +
                $"modDecision={(effectiveResult != vanillaResult ? "SuppressAccessibilityDemolition" : "AllowVanilla")}, " +
                summary);
        }

        private void LogError(string message)
        {
            log.LogError($"[{TimestampNow()}] Bugfixes and QoL {message}");
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
                int vanillaResult,
                int effectiveResult,
                eStructs buildingType)
            {
                Mode = mode;
                Classification = classification ?? string.Empty;
                VanillaResult = vanillaResult;
                EffectiveResult = effectiveResult;
                BuildingType = buildingType;
            }

            private int Mode { get; }
            private string Classification { get; }
            private int VanillaResult { get; }
            private int EffectiveResult { get; }
            private eStructs BuildingType { get; }

            public bool Equals(InaccessibleDiagnosticKey other) =>
                Mode == other.Mode &&
                VanillaResult == other.VanillaResult &&
                EffectiveResult == other.EffectiveResult &&
                BuildingType == other.BuildingType &&
                string.Equals(Classification, other.Classification, StringComparison.Ordinal);

            public override bool Equals(object obj) =>
                obj is InaccessibleDiagnosticKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = Mode;
                    hash = hash * 397 ^ VanillaResult;
                    hash = hash * 397 ^ EffectiveResult;
                    hash = hash * 397 ^ (int)BuildingType;
                    return hash * 397 ^ StringComparer.Ordinal.GetHashCode(Classification);
                }
            }
        }

    }
}
