// Feature: Recompute the AI seller's first-build stone reserve from live AIV steps.
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.Extensions;
using SHCDESE.GameGlobals;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Zhuqiaomon.Assembly;
using Zhuqiaomon.Hooks;
using Zhuqiaomon.Hooks.Transaction;
using Zhuqiaomon.Memory;
using Zhuqiaomon.Windows;

namespace BugfixesAndQoL
{
    internal sealed unsafe class AiStoneReserveFix : IDisposable
    {
        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly byte* aivTable;
        private readonly Func<short, int?> stoneCostResolver;
        private readonly object stateLock = new object();
        private readonly ulong hookAddress;
        private readonly byte[] originalHookBytes;
        private HookTransaction transaction;
        private HookRef<X64InlineHook> reserveHook = new HookRef<X64InlineHook>();
        private bool correctionAvailable = true;
        private bool firstCalculationLogged;
        private bool firstPositiveReserveLogged;
        private bool disposed;

        public AiStoneReserveFix(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings,
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

            ulong tableAddress = GameGlobalsManager.Instance.AIVCastleLayoutTableRVA;
            ulong libraryEnd = checked(libraryBase + unchecked((ulong)memory.Length));
            ulong tableEnd = checked(
                tableAddress +
                unchecked((ulong)(AiStoneReservePolicy.AivSlotCount * AiStoneReservePolicy.AivSlotSize)));
            if (tableAddress < libraryBase || tableEnd > libraryEnd)
            {
                throw new InvalidOperationException(
                    $"The Script Extender AIV table is outside CrusaderDE.dll: " +
                    $"table=0x{tableAddress:X}, module=0x{libraryBase:X}-0x{libraryEnd:X}.");
            }

            aivTable = (byte*)tableAddress;
            stoneCostResolver = ResolveStoneCost;

            try
            {
                int hookRva = checked(resolution.Rva + AiStoneReserveNativeDefinition.SellerReserveHookOffset);
                if (hookRva < 0 ||
                    hookRva + AiStoneReserveNativeDefinition.SellerReserveOverwriteLength > memory.Length)
                {
                    throw new InvalidOperationException("The AI stone-reserve hook span is outside CrusaderDE.dll.");
                }

                hookAddress = libraryBase + unchecked((ulong)hookRva);
                originalHookBytes = memory
                    .Slice(hookRva, AiStoneReserveNativeDefinition.SellerReserveOverwriteLength)
                    .ToArray();
                transaction = new HookTransaction(
                    memory,
                    libraryBase,
                    loggerFactory: null,
                    failureMode: TransactionFailureMode.RollbackAndThrow);
                transaction.AddContextHook(
                    ref reserveHook,
                    hookAddress,
                    RefreshStoneBuildingReserve,
                    regs: X64SmartCPUContextRegs.All,
                    hookSize: AiStoneReserveNativeDefinition.SellerReserveOverwriteLength,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                transaction.Commit();

                if (!reserveHook.Success)
                    throw new InvalidOperationException("The AI seller stone-reserve hook was not installed.");

                ApplySetting();

                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"Bugfixes and QoL AI stone-reserve hook installed: method={resolution.Method}, " +
                    $"patternRva=0x{resolution.Rva:X}, hookRva=0x{hookRva:X}, " +
                    $"nativeHookActive={reserveHook.Value.IsActive}, enabled={IsEnabled}.");
                if (!referenceHashMatches)
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        "Bugfixes and QoL AI stone-reserve fix is running on an unknown CrusaderDE.dll because the seller, AIV layout, first-build lifecycle, and table bounds were validated.");
                }
            }
            catch
            {
                Dispose();
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
                DisableNativeHookAndVerify();
                disposed = true;
                transaction?.Unload();
                transaction?.Dispose();
                transaction = null;
            }
        }

        public void ApplySetting()
        {
            lock (stateLock)
            {
                if (disposed || !reserveHook.Success)
                    return;

                if (!correctionAvailable || !IsEnabled)
                {
                    DisableNativeHookAndVerify();
                    return;
                }

                if (reserveHook.Value.IsActive)
                    return;

                if (!HookBytesMatchOriginal())
                {
                    correctionAvailable = false;
                    Shared.DebugLogHelper.LogError(
                        log,
                        "Bugfixes and QoL AI stone-reserve hook was not re-enabled because its native target no longer contains the verified Vanilla bytes.");
                    return;
                }

                reserveHook.Value.Enable();
                if (!reserveHook.Value.IsActive)
                    throw new InvalidOperationException("The AI stone-reserve native hook did not become active.");

                Shared.DebugLogHelper.LogDebug(
                    log,
                    "Bugfixes and QoL AI stone-reserve native hook enabled by the synchronized AI-fixes setting.");
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

                    int tableLength = AiStoneReservePolicy.AivSlotCount * AiStoneReservePolicy.AivSlotSize;
                    var table = new ReadOnlySpan<byte>(aivTable, tableLength);
                    if (!AiStoneReservePolicy.TryFindPlayerSlot(table, playerId, out int slotOffset))
                    {
                        throw new InvalidOperationException(
                            $"The AIV table did not contain exactly one active slot for player {playerId}.");
                    }

                    var slot = table.Slice(slotOffset, AiStoneReservePolicy.AivSlotSize);
                    if (!AiStoneReservePolicy.TryCalculateReserve(slot, stoneCostResolver, out int reserve))
                    {
                        throw new InvalidOperationException(
                            $"The AIV slot for player {playerId} failed frame, status, or cost validation.");
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
                        int highestFrame = ReadInt32(slot, AiStoneReservePolicy.HighestFrameOffset);
                        Shared.DebugLogHelper.LogDebug(
                            log,
                            $"AI stone-reserve first live calculation succeeded: player={playerId}, " +
                            $"slot={slotOffset / AiStoneReservePolicy.AivSlotSize}, " +
                            $"highestFrame={highestFrame}, reserve={reserve}.");
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
            bool vanillaRestored = DisableNativeHookAndVerify();
            Shared.DebugLogHelper.LogError(
                log,
                $"Bugfixes and QoL AI stone-reserve fix disabled for this process; " +
                $"nativeVanillaBytesRestored={vanillaRestored}: {ex}");
        }

        private bool IsEnabled => settings.EnableMod && settings.EnableAiFixes;

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

        private bool DisableNativeHookAndVerify()
        {
            if (!reserveHook.Success)
                return true;

            bool disableCallSucceeded = true;
            if (reserveHook.Value.IsActive)
            {
                try
                {
                    reserveHook.Value.Disable();
                    Shared.DebugLogHelper.LogDebug(
                        log,
                        "Bugfixes and QoL AI stone-reserve native hook disabled; Vanilla code restoration requested.");
                }
                catch (Exception ex)
                {
                    disableCallSucceeded = false;
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Bugfixes and QoL AI stone-reserve hook disable call failed; " +
                        $"an exact Vanilla-byte restoration will be attempted: {ex}");
                }
            }

            // X64InlineHook currently falls back to reassembly when its internal original-byte
            // snapshot is unavailable. Restore our independently captured bytes if that roundtrip
            // was not byte-exact.
            bool restorationSucceeded = true;
            if (!HookBytesMatchOriginal())
            {
                try
                {
                    restorationSucceeded = RestoreCapturedOriginalBytes();
                }
                catch (Exception ex)
                {
                    restorationSucceeded = false;
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Bugfixes and QoL AI stone-reserve exact Vanilla-byte restoration failed: {ex}");
                }
            }

            bool bytesRestored = restorationSucceeded && HookBytesMatchOriginal();
            bool hookStateConsistent = disableCallSucceeded && !reserveHook.Value.IsActive;
            if (!bytesRestored || !hookStateConsistent)
            {
                correctionAvailable = false;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL AI stone-reserve native hook disable verification failed: " +
                    $"vanillaBytesRestored={bytesRestored}, hookStateConsistent={hookStateConsistent}.");
            }
            return bytesRestored;
        }

        private bool HookBytesMatchOriginal()
        {
            if (originalHookBytes == null || originalHookBytes.Length == 0 || hookAddress == 0)
                return false;

            byte* current = (byte*)hookAddress;
            for (int index = 0; index < originalHookBytes.Length; index++)
            {
                if (current[index] != originalHookBytes[index])
                    return false;
            }
            return true;
        }

        private bool RestoreCapturedOriginalBytes()
        {
            if (originalHookBytes == null || originalHookBytes.Length == 0 || hookAddress == 0)
                return false;

            IntPtr address = unchecked((IntPtr)(long)hookAddress);
            UIntPtr size = unchecked((UIntPtr)(uint)originalHookBytes.Length);
            if (!Kernel32.VirtualProtect(
                    address,
                    size,
                    Kernel32.MemoryPermissions.PAGE_EXECUTE_READWRITE,
                    out Kernel32.MemoryPermissions oldProtection))
            {
                return false;
            }

            bool protectionRestored = false;
            try
            {
                Marshal.Copy(originalHookBytes, 0, address, originalHookBytes.Length);
            }
            finally
            {
                protectionRestored = Kernel32.VirtualProtect(
                    address,
                    size,
                    oldProtection,
                    out _);
            }

            if (!protectionRestored)
                return false;

            return MinWinAPI.FlushInstructionCache(
                Process.GetCurrentProcess().Handle,
                address,
                size);
        }

        private static int ReadInt32(ReadOnlySpan<byte> data, int offset) =>
            data[offset] |
            data[offset + 1] << 8 |
            data[offset + 2] << 16 |
            data[offset + 3] << 24;
    }
}
