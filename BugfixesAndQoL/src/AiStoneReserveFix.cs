// Feature: Recompute the AI seller's first-build stone reserve from live AIV steps.
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.Extensions;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Abstractions.Hooks;
using RedBird.Core.Memory;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using RedBird.X64.Hooks.Context;

namespace BugfixesAndQoL
{
    internal sealed unsafe class AiStoneReserveFix : IDisposable
    {
        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly IAiStoneReserveAivDataSource aivDataSource;
        private readonly Func<short, int?> stoneCostResolver;
        private readonly object stateLock = new object();
        private HookTransaction transaction;
        private readonly HookHandle<X64InlineHook> reserveHook = new HookHandle<X64InlineHook>();
        private volatile bool correctionAvailable = true;
        private volatile bool logicallyEnabled;
        private bool firstCalculationLogged;
        private bool firstPositiveReserveLogged;
        private bool disposed;

        public AiStoneReserveFix(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings,
            ScanRegion region,
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            bool referenceHashMatches)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));

            Shared.NativeResolution resolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                AiStoneReserveNativeDefinition.SellerReservePattern,
                AiStoneReserveNativeDefinition.SellerReservePatternRva,
                referenceHashMatches,
                "AI seller stone reserve branch",
                log: null);
            ValidateAivNativeLayout(memory, referenceHashMatches);

            // SHCDESE_COARSE_GRID_BUFFER_WORKAROUND: the official API is always attempted
            // first. See Findings/SHCDESE-CoarseGridBuffer-TypeLoadException.md. Remove the
            // marked fallback after the official live-village and build-step views load on Mono.
            int moduleLength = memory.Length;
            aivDataSource = ShcdeSeCoarseGridBufferWorkaround.SelectBackend(
                () => new OfficialAiStoneReserveAivDataSource(GameAIVManagerAPI.Instance),
                () => ShcdeSeCoarseGridBufferWorkaround.CreateRawDataSource(
                    libraryBase,
                    moduleLength),
                out bool workaroundActive,
                out Exception knownScriptExtenderFailure);

            Span<AivVillageState> liveVillages = aivDataSource.GetLiveVillageSlots();
            if (liveVillages.Length != AiStoneReservePolicy.LiveAivSlotCount)
            {
                throw new InvalidOperationException(
                    $"The selected AIV data source returned {liveVillages.Length} live village slots; " +
                    $"expected {AiStoneReservePolicy.LiveAivSlotCount}.");
            }

            Version extenderVersion = typeof(GameAIVManagerAPI).Assembly.GetName().Version;
            if (workaroundActive)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"{ShcdeSeCoarseGridBufferWorkaround.Marker}: compatibility workaround active; " +
                    $"SHCDE-SE {extenderVersion} could not initialize GameAIVManagerAPI. " +
                    $"The official API will be selected automatically after the Extender is fixed. " +
                    $"Detected failure: {knownScriptExtenderFailure.Message}");
            }
            else
            {
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"AI stone-reserve AIV backend: official SHCDE-SE {extenderVersion} API; " +
                    $"{ShcdeSeCoarseGridBufferWorkaround.Marker} workaround inactive.");
            }

            stoneCostResolver = ResolveStoneCost;

            try
            {
                int hookRva = checked(resolution.Rva + AiStoneReserveNativeDefinition.SellerReserveHookOffset);
                if (hookRva < 0 ||
                    hookRva + AiStoneReserveNativeDefinition.SellerReserveOverwriteLength > memory.Length)
                {
                    throw new InvalidOperationException("The AI stone-reserve hook span is outside CrusaderDE.dll.");
                }

                ulong hookAddress = libraryBase + unchecked((ulong)hookRva);
                transaction = BugfixesHookInfrastructure.CreateOwnedTransaction(region);
                BugfixesHookInfrastructure.AddContextHook(transaction, reserveHook,
                    hookAddress,
                    RefreshStoneBuildingReserve,
                    registers: X64SmartCPUContextRegs.All,
                    hookSize: AiStoneReserveNativeDefinition.SellerReserveOverwriteLength,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                CommitResult commitResult = transaction.Commit();

                if (!commitResult.IsCompleteSuccess || !reserveHook.Success)
                    throw new InvalidOperationException("The AI seller stone-reserve hook was not installed.");
                if (reserveHook.Hook.DisplacedByteCount !=
                    AiStoneReserveNativeDefinition.SellerReserveOverwriteLength)
                {
                    throw new InvalidOperationException(
                        "The AI seller stone-reserve hook displaced an unexpected native span.");
                }

                ApplySetting();

                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"Bugfixes and QoL AI stone-reserve hook installed: method={resolution.Method}, " +
                    $"patternRva=0x{resolution.Rva:X}, hookRva=0x{hookRva:X}, " +
                    $"nativeHookActive={reserveHook.IsInstalled}, enabled={IsEnabled}.");
                if (!referenceHashMatches)
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        "Bugfixes and QoL AI stone-reserve fix is running on an unknown CrusaderDE.dll because the seller, AIV layout, first-build lifecycle, and typed Script Extender views were validated.");
                }
            }
            catch
            {
                correctionAvailable = false;
                transaction?.Dispose();
                transaction = null;
                disposed = true;
                throw;
            }
        }

        public void Dispose()
        {
            lock (stateLock)
            {
                if (disposed)
                    return;

                correctionAvailable = false;
                logicallyEnabled = false;
                disposed = true;
            }
        }

        public void ApplySetting()
        {
            lock (stateLock)
            {
                if (disposed || !reserveHook.Success)
                    return;

                if (!reserveHook.IsInstalled)
                {
                    correctionAvailable = false;
                    logicallyEnabled = false;
                    Shared.DebugLogHelper.LogError(
                        log,
                        "Bugfixes and QoL AI stone-reserve fix was disabled because its permanent native hook is no longer installed.");
                    return;
                }

                logicallyEnabled = settings.EnableMod && settings.EnableAiStoneReserveFix;
            }
        }

        private void RefreshStoneBuildingReserve(NativePointer<X64SmartCPUContext> context)
        {
            lock (stateLock)
            {
                X64SmartCPUContext* registers = context.Pointer;
                if (!correctionAvailable || !IsEnabled ||
                    unchecked((int)(uint)registers->RCX) != AiStoneReserveNativeDefinition.StoneTradeCategory)
                {
                    return;
                }

                try
                {
                    if (!AiStoneReservePolicy.TryGetPlayerId(registers->R8, out int playerId))
                    {
                        throw new InvalidOperationException(
                            $"The seller player offset is invalid: r8=0x{registers->R8:X}.");
                    }

                    Span<AivVillageState> liveVillages = aivDataSource.GetLiveVillageSlots();
                    if (liveVillages.Length != AiStoneReservePolicy.LiveAivSlotCount)
                    {
                        throw new InvalidOperationException(
                            $"The Script Extender returned {liveVillages.Length} live AIV village slots.");
                    }

                    Span<int> ownerPlayerIds = stackalloc int[AiStoneReservePolicy.LiveAivSlotCount];
                    for (int index = 0; index < liveVillages.Length; index++)
                        ownerPlayerIds[index] = liveVillages[index].OwnerPlayerId;

                    if (!AiStoneReservePolicy.TryFindUniquePlayerSlot(
                        ownerPlayerIds, playerId, out int liveSlotIndex))
                    {
                        throw new InvalidOperationException(
                            $"The live AIV villages did not contain exactly one slot for player {playerId}.");
                    }

                    int villageSlot = checked(liveSlotIndex + 1);
                    ref AivVillageState village = ref liveVillages[liveSlotIndex];
                    Span<AivBuildStep> buildSteps = aivDataSource.GetBuildSteps(villageSlot);
                    if (buildSteps.Length != ShcdeSeCoarseGridBufferWorkaround.BuildStepCapacity)
                    {
                        throw new InvalidOperationException(
                            $"The AIV village for player {playerId} exposes {buildSteps.Length} build steps " +
                            $"instead of {ShcdeSeCoarseGridBufferWorkaround.BuildStepCapacity}.");
                    }
                    int maximumBuildStep = village.MaximumBuildStep;
                    if (!AiStoneReservePolicy.IsValidMaximumBuildStep(
                        maximumBuildStep, buildSteps.Length))
                    {
                        throw new InvalidOperationException(
                            $"The AIV village for player {playerId} has invalid maximum build step " +
                            $"{maximumBuildStep} for capacity {buildSteps.Length}.");
                    }

                    int reserve = 0;
                    // Vanilla stores the highest valid index, not a step count.
                    for (int index = 0; index <= maximumBuildStep; index++)
                    {
                        ref AivBuildStep step = ref buildSteps[index];
                        if (!AiStoneReservePolicy.TryAccumulateReserve(
                            unchecked((byte)step.State),
                            unchecked((short)step.BuildingType),
                            stoneCostResolver,
                            ref reserve))
                        {
                            throw new InvalidOperationException(
                                $"AIV build step {index} for player {playerId} failed state or cost validation.");
                        }
                    }

                    int maximumStone = unchecked((int)(uint)registers->RAX);
                    int variance = unchecked((int)(uint)registers->R11);
                    if (!AiStoneReservePolicy.TryValidateThreshold(maximumStone, variance, reserve))
                    {
                        throw new InvalidOperationException(
                            $"The stone threshold would overflow: maximum={maximumStone}, " +
                            $"variance={variance}, reserve={reserve}.");
                    }

                    // The displaced Vanilla code calculates the base threshold after this
                    // callback; only its later R9D surcharge is replaced.
                    registers->R9 = unchecked((uint)reserve);
                    if (!firstCalculationLogged)
                    {
                        firstCalculationLogged = true;
                        Shared.DebugLogHelper.LogDebug(
                            log,
                            $"AI stone-reserve first live calculation succeeded: player={playerId}, " +
                            $"slot={villageSlot}, maximumBuildStep={maximumBuildStep}, reserve={reserve}.");
                    }
                    if (reserve > 0 && !firstPositiveReserveLogged)
                    {
                        firstPositiveReserveLogged = true;
                        Shared.DebugLogHelper.LogDebug(
                            log,
                            $"AI stone-reserve first positive first-build buffer observed: " +
                            $"player={playerId}, reserve={reserve}.");
                    }
                }
                catch (Exception ex)
                {
                    DisableCorrectionToVanilla(ex);
                }
            }
        }

        private int? ResolveStoneCost(short commandBuildingType)
        {
            eMappers mapper = (eMappers)commandBuildingType;
            if (!Enum.IsDefined(typeof(eMappers), mapper))
                throw new InvalidOperationException($"Unknown AIV command building type {commandBuildingType}.");

            // Multi-tile fortification and terrain commands are covered by MaxStone itself;
            // they must not turn an entire wall run into a building reserve.
            if (IsExcludedMultiTileCommand(mapper))
                return null;

            eStructs building = mapper.ConvertToEStructs();
            if (building == eStructs.STRUCT_NULL)
                return null;
            if ((int)building <= (int)eStructs.STRUCT_NULL ||
                (int)building >= (int)eStructs.STRUCT_MAX)
                throw new InvalidOperationException($"AIV command {mapper} mapped outside the building table: {building}.");

            int stoneCost = GameBuildingManagerAPI.Instance.GetStoneCost(building);
            if (stoneCost < 0)
            {
                throw new InvalidOperationException(
                    $"Building {building} resolved to invalid stone cost {stoneCost}.");
            }
            if (stoneCost == 0)
                return null;
            return stoneCost;
        }

        private void DisableCorrectionToVanilla(Exception ex)
        {
            if (!correctionAvailable)
                return;

            correctionAvailable = false;
            logicallyEnabled = false;
            Shared.DebugLogHelper.LogError(
                log,
                $"Bugfixes and QoL AI stone-reserve fix disabled logically for this process; " +
                $"the permanent native hook remains installed and its callback is now a no-op: {ex}");
        }

        private bool IsEnabled => logicallyEnabled;

        private static bool IsExcludedMultiTileCommand(eMappers mapper)
        {
            switch (mapper)
            {
                case eMappers.MAPPER_WALL:
                case eMappers.MAPPER_CRENAL:
                case eMappers.MAPPER_CRENAL2:
                case eMappers.MAPPER_STAIR:
                case eMappers.MAPPER_STAIR1:
                case eMappers.MAPPER_STAIR2:
                case eMappers.MAPPER_STAIR3:
                case eMappers.MAPPER_STAIR4:
                case eMappers.MAPPER_STAIR5:
                case eMappers.MAPPER_STAIR6:
                case eMappers.MAPPER_UNDUGMOAT:
                case eMappers.MAPPER_DUGMOAT:
                case eMappers.MAPPER_MOAT:
                case eMappers.MAPPER_ANTIMOAT:
                case eMappers.MAPPER_WOODWALL:
                case eMappers.MAPPER_OIL:
                case eMappers.MAPPER_PITCH_DITCH:
                    return true;
                default:
                    return false;
            }
        }

        private void ValidateAivNativeLayout(ReadOnlySpan<byte> memory, bool referenceHashMatches)
        {
            Shared.NativePatternResolver.ResolveUnique(
                memory,
                AiStoneReserveNativeDefinition.AivSlotLayoutPattern,
                AiStoneReserveNativeDefinition.AivSlotLayoutPatternRva,
                referenceHashMatches,
                "AI AIV slot layout",
                log: null);
            Shared.NativePatternResolver.ResolveUnique(
                memory,
                AiStoneReserveNativeDefinition.AivStepLayoutPattern,
                AiStoneReserveNativeDefinition.AivStepLayoutPatternRva,
                referenceHashMatches,
                "AI AIV build-step layout",
                log: null);
            Shared.NativePatternResolver.ResolveUnique(
                memory,
                AiStoneReserveNativeDefinition.AivHighestFramePattern,
                AiStoneReserveNativeDefinition.AivHighestFramePatternRva,
                referenceHashMatches,
                "AI AIV highest-frame layout",
                log: null);
            Shared.NativePatternResolver.ResolveUnique(
                memory,
                AiStoneReserveNativeDefinition.AivInitialFirstBuildStatePattern,
                AiStoneReserveNativeDefinition.AivInitialFirstBuildStatePatternRva,
                referenceHashMatches,
                "AI AIV initial first-build state",
                log: null);
            Shared.NativePatternResolver.ResolveUnique(
                memory,
                AiStoneReserveNativeDefinition.AivResourceShortageReturnPattern,
                AiStoneReserveNativeDefinition.AivResourceShortageReturnPatternRva,
                referenceHashMatches,
                "AI AIV resource-shortage state preservation",
                log: null);
            Shared.NativePatternResolver.ResolveUnique(
                memory,
                AiStoneReserveNativeDefinition.AivFirstBuildSuccessPattern,
                AiStoneReserveNativeDefinition.AivFirstBuildSuccessPatternRva,
                referenceHashMatches,
                "AI AIV first-build success state",
                log: null);
            Shared.NativePatternResolver.ResolveUnique(
                memory,
                AiStoneReserveNativeDefinition.AivPlacementRetryPattern,
                AiStoneReserveNativeDefinition.AivPlacementRetryPatternRva,
                referenceHashMatches,
                "AI AIV placement-retry state",
                log: null);
        }

    }
}
