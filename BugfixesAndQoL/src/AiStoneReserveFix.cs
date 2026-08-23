// Feature: Recompute the AI seller's stone-building reserve from live AIV steps.
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.Extensions;
using SHCDESE.GameGlobals;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using Zhuqiaomon.Assembly;
using Zhuqiaomon.Hooks;
using Zhuqiaomon.Hooks.Transaction;
using Zhuqiaomon.Memory;

namespace BugfixesAndQoL
{
    internal sealed unsafe class AiStoneReserveFix : IDisposable
    {
        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly byte* aivTable;
        private readonly Func<short, int?> stoneCostResolver;
        private readonly int[] lastLoggedReserve = new int[AiStoneReservePolicy.MaximumPlayerId + 1];
        private HookTransaction transaction;
        private HookRef<X64InlineHook> reserveHook = new HookRef<X64InlineHook>();
        private bool correctionAvailable = true;
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
            for (int playerId = 0; playerId < lastLoggedReserve.Length; playerId++)
                lastLoggedReserve[playerId] = int.MinValue;

            Shared.NativeResolution resolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                AiStoneReserveNativeDefinition.SellerReservePattern,
                AiStoneReserveNativeDefinition.SellerReservePatternRva,
                referenceHashMatches,
                "AI seller stone reserve branch",
                log);

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
                transaction = new HookTransaction(
                    memory,
                    libraryBase,
                    loggerFactory: null,
                    failureMode: TransactionFailureMode.RollbackAndThrow);
                transaction.AddContextHook(
                    ref reserveHook,
                    libraryBase + unchecked((ulong)hookRva),
                    RefreshStoneBuildingReserve,
                    regs: X64SmartCPUContextRegs.RCX | X64SmartCPUContextRegs.R8 | X64SmartCPUContextRegs.R9,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                transaction.Commit();

                if (!reserveHook.Success)
                    throw new InvalidOperationException("The AI seller stone-reserve hook was not installed.");

                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Bugfixes and QoL AI stone-reserve hook installed: method={resolution.Method}, " +
                    $"patternRva=0x{resolution.Rva:X}, hookRva=0x{hookRva:X}, enabled={IsEnabled}.");
                if (!referenceHashMatches)
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        "Bugfixes and QoL AI stone-reserve fix is running on an unknown CrusaderDE.dll because the seller signature and AIV table bounds were validated.");
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
            if (disposed)
                return;

            disposed = true;
            correctionAvailable = false;
            transaction?.Unload();
            transaction?.Dispose();
            transaction = null;
        }

        private void RefreshStoneBuildingReserve(NativePointer<X64SmartCPUContext> context)
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
                        $"The AIV table did not contain exactly one slot for player {playerId}.");
                }

                var slot = table.Slice(slotOffset, AiStoneReservePolicy.AivSlotSize);
                if (!AiStoneReservePolicy.TryCalculateReserve(slot, stoneCostResolver, out int reserve))
                {
                    throw new InvalidOperationException(
                        $"The AIV slot for player {playerId} failed layout, status, or cost validation.");
                }

                // The overwritten Vanilla instruction adds R9D to its already calculated
                // MaxStone + MaxResourceVariance threshold after this callback returns.
                registers->R9 = unchecked((uint)reserve);
                if (lastLoggedReserve[playerId] != reserve)
                {
                    lastLoggedReserve[playerId] = reserve;
                    Shared.DebugLogHelper.LogDebug(
                        log,
                        $"AI stone-building reserve refreshed: player={playerId}, reserve={reserve}.");
                }
            }
            catch (Exception ex)
            {
                DisableCorrectionToVanilla(ex);
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

            int stoneCost = GameBuildingManagerAPI.Instance.GetStoneCost(building);
            if (stoneCost < 0)
            {
                throw new InvalidOperationException(
                    $"Building {building} resolved to invalid stone cost {stoneCost}.");
            }
            return stoneCost;
        }

        private void DisableCorrectionToVanilla(Exception ex)
        {
            if (!correctionAvailable)
                return;

            correctionAvailable = false;
            Shared.DebugLogHelper.LogError(
                log,
                $"Bugfixes and QoL AI stone-reserve fix disabled for this process; " +
                $"the original Vanilla reserve remains active: {ex}");
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
                case eMappers.MAPPER_PITCH_DITCH:
                    return true;
                default:
                    return false;
            }
        }
    }
}
