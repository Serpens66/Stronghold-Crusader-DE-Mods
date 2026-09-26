using BepInEx.Logging;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Context;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace NoDefeatLootTest
{
    internal sealed unsafe class DefeatLootHook
    {
        internal const int BranchRva = 0x15C477;
        internal const int BranchLength = 16;
        internal const int RewardStartRva = 0x15C48D;
        internal const int SkipTargetRva = 0x15CDC3;
        internal const string BranchPattern =
            "0F 84 46 09 00 00 66 42 83 BC 23 48 0A 00 00 00";

        private readonly ManualLogSource log;
        private readonly HookHandle<X64InlineHook> branchHandle = new HookHandle<X64InlineHook>();
        private HookTransaction transaction;
        private int suppressedCount;
        private int callbackErrorCount;

        internal int SuppressedCount => Volatile.Read(ref suppressedCount);
        internal int CallbackErrorCount => Volatile.Read(ref callbackErrorCount);

        internal DefeatLootHook(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
        }

        internal void Install(CrusaderLibraryLoadContext context)
        {
            if (context == null || context.ModuleHandle == IntPtr.Zero)
                throw new ArgumentException("Native library context is unavailable.", nameof(context));

            ReadOnlySpan<byte> memory = context.Memory;
            if (memory.Length < BranchRva + 64)
                throw new InvalidOperationException("Native snapshot does not contain the defeat-loot branch.");

            Shared.NativeResolution resolution = Shared.NativePatternResolver.ResolveUnique(
                memory, BranchPattern, BranchRva, referenceHashMatches: true,
                "Vanilla lord-defeat reward guard", log);
            if (resolution.Rva != BranchRva)
                throw new InvalidOperationException("The reward guard resolved outside the audited RVA.");

            // The existing JE is the complete Vanilla skip path. Its rel32 is measured
            // from 15C47D, and the following CMP ends at 15C487. No foreign branch
            // enters the displaced region; the next JNE remains outside the hook.
            int relative = BitConverter.ToInt32(memory.Slice(BranchRva + 2, 4).ToArray(), 0);
            if (BranchRva + 6 + relative != SkipTargetRva ||
                RewardStartRva != BranchRva + BranchLength + 6)
                throw new InvalidOperationException("The reward-guard control flow changed.");

            // Probe the installed RedBird X64InlineHook decoder on a copied byte buffer.
            // HookSize is a minimum; the actual displacement must be exactly 16 bytes.
            IntPtr copied = Marshal.AllocHGlobal(64);
            try
            {
                Marshal.Copy(memory.Slice(BranchRva, 64).ToArray(), 0, copied, 64);
                using (var probe = new X64InlineHook(unchecked((ulong)copied.ToInt64()), BranchLength))
                {
                    if (probe.DisplacedByteCount != BranchLength)
                        throw new InvalidOperationException(
                            "The installed RedBird backend displaces a different byte count.");
                    RewardGuardStubContract.Verify(probe, SuppressForHumanWinner,
                        CreateHookOptions(), unchecked((ulong)copied.ToInt64()) +
                        (ulong)(SkipTargetRva - BranchRva));
                }
            }
            finally
            {
                Marshal.FreeHGlobal(copied);
            }

            ulong address = unchecked((ulong)context.ModuleHandle.ToInt64()) + BranchRva;
            transaction = new HookTransaction(
                context.Region,
                SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                new HookTransactionOptions
                {
                    FailureMode = TransactionFailureMode.RollbackAndThrow,
                    OwnsHooks = true
                });
            try
            {
                transaction.AddContextHook(
                    branchHandle,
                    HookTarget.FromAddress(address),
                    SuppressForHumanWinner,
                    CreateHookOptions());
                CommitResult result = transaction.Commit();
                if (!result.IsCompleteSuccess || !branchHandle.Success ||
                    branchHandle.Hook.DisplacedByteCount != BranchLength)
                    throw new InvalidOperationException(
                        "The reward-guard hook did not commit with its audited 16-byte span.");
            }
            catch
            {
                // This initialization candidate has not been published. Rollback is safe.
                transaction.Dispose();
                transaction = null;
                throw;
            }

            Shared.DebugLogHelper.LogInfo(log,
                "NO_DEFEAT_LOOT_TEST hook installed: method=" + resolution.Method +
                " rva=0x" + BranchRva.ToString("X") +
                " displaced=" + branchHandle.Hook.DisplacedByteCount +
                " skip=0x" + SkipTargetRva.ToString("X") +
                " generatedStub=verified" +
                " redBird=" + typeof(X64InlineHook).Assembly.GetName().Version);
        }

        private static ContextHookOptions CreateHookOptions()
        {
            return new ContextHookOptions
            {
                Registers = X64SmartCPUContextRegs.All,
                HookSize = BranchLength,
                ErrorMode = CallbackErrorMode.LogAndContinue,
                Placement = OverwrittenInstructionPlacement.AfterCallback,
                InstructionSelector = RewardGuardStubContract.SelectInstructions
            };
        }

        private void SuppressForHumanWinner(NativePointer<X64SmartCPUContext> context)
        {
            X64SmartCPUContext* registers = context.Pointer;
            if (registers == null)
            {
                Interlocked.Increment(ref callbackErrorCount);
                return;
            }

            // RedBird clobbers flags around its context callback. The relocated
            // TEST EDX,EDX reconstructs the branch condition after the callback.
            // EBP is Vanilla's one-based winning player ID; RDX is dead here.
            int winnerId = unchecked((int)(uint)registers->RBP);
            if (winnerId == 0)
            {
                registers->RDX = 0;
                return;
            }
            registers->RDX = 1; // Fail open: positive and invalid IDs retain Vanilla's reward path.
            if (winnerId < 1 || winnerId > GamePlayerManagerAPI.MAX_PLAYERS)
            {
                Interlocked.Increment(ref callbackErrorCount);
                return;
            }

            try
            {
                if (GamePlayerManagerAPI.Instance.IsAIPlayer(winnerId))
                    return;
                registers->RDX = 0;
                Interlocked.Increment(ref suppressedCount);
            }
            catch
            {
                // RDX remains nonzero, so the reward branch follows Vanilla.
                Interlocked.Increment(ref callbackErrorCount);
            }
        }
    }
}
