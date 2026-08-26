using BepInEx.Logging;
using System;
using System.Threading;
using Zhuqiaomon.Assembly;
using Zhuqiaomon.Assembly.Stateful;
using Zhuqiaomon.Hooks;
using Zhuqiaomon.Hooks.Transaction;
using Zhuqiaomon.Memory;

namespace ImprovedHunters
{
    /// <summary>
    /// Temporary workaround for SHCDE Script Extender issue 123. The public
    /// Hunter query event currently reports saved caller RBX instead of the
    /// native Hunter ID. Remove this once the minimum supported Extender has
    /// fixed https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/work_items/123
    /// and a real runtime test has verified the fix. Revalidate the native pattern,
    /// hook context, and event semantics after every Script Extender update; this
    /// workaround may need adaptation before it can be removed.
    /// </summary>
    internal sealed unsafe class HunterQueryActorWorkaround : IDisposable
    {
        private const int HunterQueryCandidateLoopRva = 0x18AFC0;
        private const int MaxCaptureFailureLogs = 10;
        private const string HunterQueryCandidateLoopPattern =
            "66 83 BB EC FD FF FF 02 0F 85 ?? ?? ?? ?? " +
            "66 83 3B 00 0F 85 ?? ?? ?? ?? " +
            "66 83 BB F6 FD FF FF 00 0F 85 ?? ?? ?? ??";

        private static long nextGeneration;

        [ThreadStatic] private static long capturedGeneration;
        [ThreadStatic] private static int capturedQueryUnitId;
        [ThreadStatic] private static int capturedHunterUnitId;

        private readonly ManualLogSource log;
        private readonly long generation;
        private HookTransaction transaction;
        private HookRef<X64InlineHook> candidateLoopHook = new HookRef<X64InlineHook>();
        private bool available = true;
        private int captureFailureLogs;
        private bool disposed;

        public HunterQueryActorWorkaround(
            ManualLogSource log,
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            bool referenceHashMatches)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            if (memory.Length == 0 || libraryBase == 0)
                throw new ArgumentException("The Crusader library is unavailable.");

            generation = Interlocked.Increment(ref nextGeneration);
            int hookRva = Shared.NativePatternResolver.ResolveUnique(
                memory,
                HunterQueryCandidateLoopPattern,
                HunterQueryCandidateLoopRva,
                referenceHashMatches,
                "Hunter query actor capture",
                log).Rva;

            try
            {
                transaction = new HookTransaction(
                    memory,
                    libraryBase,
                    loggerFactory: null,
                    failureMode: TransactionFailureMode.RollbackAndThrow);
                transaction.AddContextHook(
                    ref candidateLoopHook,
                    libraryBase + unchecked((ulong)hookRva),
                    CaptureQueryActor,
                    regs: X64SmartCPUContextRegs.RSI |
                        X64SmartCPUContextRegs.R13 |
                        X64SmartCPUContextRegs.R14,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                transaction.Commit();

                if (!candidateLoopHook.Success)
                    throw new InvalidOperationException("The Hunter query actor capture hook was not installed.");

                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Improved Hunters temporary Script Extender issue-123 workaround initialized: " +
                    $"rva=0x{hookRva:X}, unitSlotSize=0x{HunterQueryActorPolicy.NativeUnitSlotSize:X}.");
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public bool IsAvailable => available && !disposed && candidateLoopHook.Success;

        public bool TryConsumeHunterUnitId(int queryUnitId, out int hunterUnitId)
        {
            hunterUnitId = 0;
            if (!IsAvailable)
                return false;

            if (capturedGeneration != generation ||
                !HunterQueryActorPolicy.IsMatchingCapture(
                    queryUnitId,
                    capturedQueryUnitId,
                    capturedHunterUnitId))
            {
                ClearThreadCapture();
                return false;
            }

            hunterUnitId = capturedHunterUnitId;
            ClearThreadCapture();
            return true;
        }

        private void CaptureQueryActor(NativePointer<X64SmartCPUContext> context)
        {
            try
            {
                int queryUnitId = unchecked((int)(uint)context.Pointer->RSI);
                if (queryUnitId <= 0 ||
                    !HunterQueryActorPolicy.TryReconstructHunterUnitId(
                        context.Pointer->R13,
                        context.Pointer->R14,
                        out int hunterUnitId))
                {
                    ClearThreadCapture();
                    TryLogCaptureFailure(queryUnitId, context.Pointer->R13, context.Pointer->R14);
                    return;
                }

                capturedGeneration = generation;
                capturedQueryUnitId = queryUnitId;
                capturedHunterUnitId = hunterUnitId;
            }
            catch (Exception exception)
            {
                ClearThreadCapture();
                TryLogCaptureFailure(0, 0, 0, exception);
            }
        }

        private void TryLogCaptureFailure(
            int queryUnitId,
            ulong hunterSlotBase,
            ulong unitManagerBase,
            Exception exception = null)
        {
            if (captureFailureLogs >= MaxCaptureFailureLogs)
                return;

            captureFailureLogs++;
            Shared.DebugLogHelper.LogWarning(
                log,
                $"Improved Hunters issue-123 workaround rejected an invalid native capture: " +
                $"query={queryUnitId}, hunterSlotBase=0x{hunterSlotBase:X}, " +
                $"unitManagerBase=0x{unitManagerBase:X}, error={exception?.Message ?? "none"} " +
                $"({captureFailureLogs}/{MaxCaptureFailureLogs}).");
        }

        private static void ClearThreadCapture()
        {
            capturedGeneration = 0;
            capturedQueryUnitId = 0;
            capturedHunterUnitId = 0;
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            available = false;
            transaction?.Unload();
            transaction?.Dispose();
            transaction = null;
            ClearThreadCapture();
        }
    }
}
