using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using Zhuqiaomon.Assembly;
using Zhuqiaomon.Hooks;
using Zhuqiaomon.Hooks.Transaction;
using Zhuqiaomon.Memory;

namespace HunterQueryTargetDiagnostic
{
    /// <summary>
    /// Passive observer for the independently proven stable no-target State-7 writer.
    /// The adjacent shared-writer hooks remain disabled because the old save crashes with them.
    /// </summary>
    internal sealed unsafe class HunterState7CauseDiagnostic
    {
        private const int UnitSlotSize = 0x490;
        private const int UnitArrayOffset = 0x65C;
        private const int UnknownThresholdFieldOffset = 0x2A2;
        private const int RawTargetUnitIdOffset = 0x39A;
        private const int RawTargetGlobalIdOffset = 0x39C;
        private const int NoTargetThresholdState7WriterRva = 0x12FEC1;
        private const int DetailLogLimit = 120;
        private const int CallbackErrorLimit = 5;

        private const string NoTargetThresholdState7WriterPattern =
            "B8 07 00 00 00 66 42 89 84 2A 18 09 00 00 E9 ?? ?? ?? ??";

        private readonly ManualLogSource log;
        private readonly HookTransaction transaction;
        private HookRef<X64InlineHook> noTargetThresholdState7WriterHook = new HookRef<X64InlineHook>();
        private long occurrenceCount;
        private long noTargetThresholdCount;
        private int detailLogCount;
        private int callbackErrorCount;
        private bool firstCallbackLogged;

        public HunterState7CauseDiagnostic(
            ManualLogSource log,
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            bool referenceHashMatches)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            if (memory.Length == 0 || libraryBase == 0)
                throw new ArgumentException("The Crusader library is unavailable.");

            int thresholdWriterRva = Shared.NativePatternResolver.ResolveUnique(
                memory,
                NoTargetThresholdState7WriterPattern,
                NoTargetThresholdState7WriterRva,
                referenceHashMatches,
                "Hunter no-target threshold state-7 writer",
                log).Rva;

            transaction = new HookTransaction(
                memory,
                libraryBase,
                loggerFactory: null,
                failureMode: TransactionFailureMode.RollbackAndThrow);
            transaction.AddContextHook(
                ref noTargetThresholdState7WriterHook,
                libraryBase + unchecked((ulong)thresholdWriterRva),
                ObserveNoTargetThresholdState7Writer,
                regs: X64SmartCPUContextRegs.Volatile | X64SmartCPUContextRegs.R13,
                errorMode: CallbackErrorMode.LogAndContinue,
                placement: OverwrittenInstructionPlacement.AfterCallback);
            transaction.Commit();

            if (!noTargetThresholdState7WriterHook.Success)
            {
                throw new InvalidOperationException("At least one Hunter state-7 cause hook was not installed.");
            }

            LogInfo(
                "STATE7_SAFE_HOOKS_INSTALLED: hookCount=1, stableThresholdOnly=true, " +
                "observerOnly=true, registerMutation=false, " +
                $"noTargetThresholdWriterRva=0x{thresholdWriterRva:X}, " +
                "disabledCrashRegion=0x12FD96+0x12FDA4, expansionHooksDisabled=true.");
        }

        private void ObserveNoTargetThresholdState7Writer(NativePointer<X64SmartCPUContext> context)
        {
            noTargetThresholdCount++;
            ObserveState7Write(
                "no_target_and_unknown_2A2_gt_400",
                NoTargetThresholdState7WriterRva,
                context.Pointer->R13,
                context.Pointer->RDX);
        }

        private void ObserveState7Write(
            string reason,
            int writerRva,
            ulong unitManagerAddress,
            ulong scaledUnitOffset)
        {
            try
            {
                occurrenceCount++;
                bool scaledOffsetValid =
                    scaledUnitOffset >= UnitSlotSize &&
                    scaledUnitOffset % UnitSlotSize == 0 &&
                    scaledUnitOffset / UnitSlotSize <= int.MaxValue;
                int hunterUnitId = scaledOffsetValid
                    ? unchecked((int)(scaledUnitOffset / UnitSlotSize))
                    : -1;

                ulong expectedManagerAddress = unchecked(
                    (ulong)GameUnitManagerAPI.Instance.GetUnitManager().Pointer);
                bool managerMatches = unitManagerAddress == expectedManagerAddress;
                string hunterDescription = DescribeHunter(
                    unitManagerAddress,
                    scaledUnitOffset,
                    hunterUnitId,
                    scaledOffsetValid,
                    managerMatches);

                if (!firstCallbackLogged)
                {
                    firstCallbackLogged = true;
                    LogInfo(
                        "STATE7_CAUSE_HOOK_CONFIRMED: " +
                        $"reason={reason}, writerRva=0x{writerRva:X}, {hunterDescription}.");
                }

                if (detailLogCount < DetailLogLimit)
                {
                    detailLogCount++;
                    LogInfo(
                        "STATE7_CAUSE: " +
                        $"occurrence={occurrenceCount}, reason={reason}, writerRva=0x{writerRva:X}, " +
                        $"{hunterDescription}, " +
                        $"counterNoTargetUnknown2A2Gt400={noTargetThresholdCount}, " +
                        $"detailLogs={detailLogCount}/{DetailLogLimit}.");
                }
            }
            catch (Exception exception)
            {
                callbackErrorCount++;
                if (callbackErrorCount <= CallbackErrorLimit)
                {
                    LogInfo(
                        "STATE7_CAUSE_CALLBACK_ERROR: " +
                        $"count={callbackErrorCount}/{CallbackErrorLimit}, reason={reason}, " +
                        $"writerRva=0x{writerRva:X}, exception={exception}.");
                }
            }
        }

        private static string DescribeHunter(
            ulong unitManagerAddress,
            ulong scaledUnitOffset,
            int hunterUnitId,
            bool scaledOffsetValid,
            bool managerMatches)
        {
            if (!scaledOffsetValid || !managerMatches)
            {
                return
                    $"hunterId={hunterUnitId}, contextValid=false, " +
                    $"manager=0x{unitManagerAddress:X16}, managerMatchesApi={managerMatches}, " +
                    $"scaledUnitOffset=0x{scaledUnitOffset:X}";
            }

            GameUnit* hunter = (GameUnit*)(unitManagerAddress + scaledUnitOffset + UnitArrayOffset);
            byte* bytes = (byte*)hunter;
            short unknown2A2 = *(short*)(bytes + UnknownThresholdFieldOffset);
            ushort rawTargetUnitId = *(ushort*)(bytes + RawTargetUnitIdOffset);
            uint rawTargetGlobalId = *(uint*)(bytes + RawTargetGlobalIdOffset);
            bool plausibleHunter =
                hunter->r_AliveState != AliveState.None &&
                hunter->r_UnitChimp == eChimps.CHIMP_TYPE_HUNTER;

            return
                $"hunter=(id={hunterUnitId}/global={hunter->r_GlobalId}/" +
                $"type={(int)hunter->r_UnitChimp}:{hunter->r_UnitChimp}/" +
                $"alive={(int)hunter->r_AliveState}:{hunter->r_AliveState}/" +
                $"stateBefore={hunter->r_AIState}/plausible={plausibleHunter}/" +
                $"tile={hunter->r_CurrentTilePositionX},{hunter->r_CurrentTilePositionY}/" +
                $"linkedHunterPost={hunter->r_LinkedProductionBuildingId}/" +
                $"unknown2A2={unknown2A2}/rawTarget={rawTargetUnitId}:{rawTargetGlobalId}), " +
                $"manager=0x{unitManagerAddress:X16}, managerMatchesApi={managerMatches}, " +
                $"scaledUnitOffset=0x{scaledUnitOffset:X}";
        }

        private void LogInfo(string message)
        {
            log.LogInfo($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}");
        }
    }
}
