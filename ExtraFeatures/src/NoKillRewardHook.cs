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

namespace ExtraFeatures
{
    internal sealed unsafe class NoKillRewardHook
    {
        internal const int RewardRva = 0x15C4EA;
        internal const int RewardLength = 14;
        internal const int CleanupRva = 0x15C980;
        internal const int GoldMessageRva = 0x15CA65;
        internal const int GoldMessageLength = 15;
        internal const int GoldMessageReturnRva = 0x15CA74;
        internal const int ResourceBaseRva = 0x366C210;
        internal const string RewardPattern = "4C 69 D5 0F 16 00 00 4C 69 ED 3C 58 00 00";
        internal const string GoldMessagePattern = "8B D6 44 2B E0 44 89 64 24 20 E8 5C FE EB FF";

        private readonly ManualLogSource log;
        private static NoKillRewardHook rootedPublishedInstance;
        private readonly HookHandle<X64InlineHook> rewardHandle = new HookHandle<X64InlineHook>();
        private readonly HookHandle<X64InlineHook> goldMessageHandle = new HookHandle<X64InlineHook>();
        private HookTransaction transaction;
        private ulong moduleBase;
        private int suppressionMask;
        private bool installed;

        internal NoKillRewardHook(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
        }

        internal void Install(CrusaderLibraryLoadContext context)
        {
            if (rootedPublishedInstance != null)
                throw new InvalidOperationException("The no-kill-reward hooks are already published.");
            if (context == null || context.ModuleHandle == IntPtr.Zero)
                throw new ArgumentException("Native library context is unavailable.", nameof(context));
            if (!Shared.DebugLogHelper.IsCurrentNativeLibraryVersion())
                throw new InvalidOperationException("The installed native DLL differs from the audited hash.");

            ReadOnlySpan<byte> memory = context.Memory;
            if (memory.Length < GoldMessageRva + 64)
                throw new InvalidOperationException("Native snapshot does not contain both defeat-loot sites.");

            Shared.NativeResolution reward = Shared.NativePatternResolver.ResolveUnique(
                memory, RewardPattern, RewardRva, referenceHashMatches: true,
                "Vanilla lord-defeat reward setup", log);
            Shared.NativeResolution goldMessage = Shared.NativePatternResolver.ResolveUnique(
                memory, GoldMessagePattern, GoldMessageRva, referenceHashMatches: true,
                "Vanilla lord-defeat gold message", log);
            if (reward.Rva != RewardRva || goldMessage.Rva != GoldMessageRva ||
                RewardRva + RewardLength != 0x15C4F8 ||
                GoldMessageRva + GoldMessageLength != GoldMessageReturnRva)
                throw new InvalidOperationException("The audited defeat-loot control flow changed.");

            moduleBase = unchecked((ulong)context.ModuleHandle.ToInt64());
            Probe(memory, RewardRva, RewardLength, (probe, copiedAddress) =>
                NoKillRewardStubContract.VerifyReward(
                    probe, SuppressForSelectedWinner,
                    CreateRewardOptions(copiedAddress + (ulong)(CleanupRva - RewardRva)),
                    copiedAddress + (ulong)(CleanupRva - RewardRva),
                    copiedAddress + RewardLength));
            Probe(memory, GoldMessageRva, GoldMessageLength, (probe, copiedAddress) =>
                NoKillRewardStubContract.VerifyGoldMessage(
                    probe, copiedAddress + GoldMessageLength));

            // Memory is the load-time image. Another mod may already have patched
            // the executable pages even while the audited snapshot still matches.
            VerifyLiveSpans(memory);

            transaction = new HookTransaction(
                context.Region,
                SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                new HookTransactionOptions
                {
                    FailureMode = TransactionFailureMode.RollbackAndThrow,
                    OwnsHooks = true
                });
            bool published = false;
            try
            {
                transaction.AddContextHook(
                    rewardHandle,
                    HookTarget.FromAddress(moduleBase + RewardRva),
                    SuppressForSelectedWinner,
                    CreateRewardOptions(moduleBase + CleanupRva));
                transaction.AddInline(
                    goldMessageHandle,
                    HookTarget.FromAddress(moduleBase + GoldMessageRva),
                    NoKillRewardStubContract.GenerateGoldMessageGuard,
                    hookSize: GoldMessageLength);
                VerifyLiveSpans(memory);
                CommitResult result = transaction.Commit();
                // From this point onward the transaction may own executable patches.
                // Keep them rooted even if a post-commit assertion fails.
                rootedPublishedInstance = this;
                published = true;
                if (!result.IsCompleteSuccess ||
                    !rewardHandle.Success || !goldMessageHandle.Success ||
                    !rewardHandle.IsInstalled || !goldMessageHandle.IsInstalled ||
                    rewardHandle.Hook.DisplacedByteCount != RewardLength ||
                    goldMessageHandle.Hook.DisplacedByteCount != GoldMessageLength)
                    throw new InvalidOperationException("The two defeat-loot hooks did not commit with their audited spans.");
                installed = true;
            }
            catch
            {
                Volatile.Write(ref suppressionMask, 0);
                if (!published)
                {
                    transaction.Dispose();
                    transaction = null;
                }
                throw;
            }

            Shared.DebugLogHelper.LogDebug(log, "Extra Features No Kill Reward hooks installed.");
        }

        internal void SetEnabled(bool human, bool ai)
        {
            Volatile.Write(ref suppressionMask, installed ? NoKillRewardPolicy.ComposeMask(human, ai) : 0);
        }

        private static void Probe(ReadOnlySpan<byte> memory, int rva, int length,
            Action<X64InlineHook, ulong> verify)
        {
            IntPtr copy = Marshal.AllocHGlobal(64);
            try
            {
                Marshal.Copy(memory.Slice(rva, 64).ToArray(), 0, copy, 64);
                ulong address = unchecked((ulong)copy.ToInt64());
                using (var probe = new X64InlineHook(address, length))
                {
                    if (probe.DisplacedByteCount != length)
                        throw new InvalidOperationException("The installed RedBird backend displaced a different span at 0x" + rva.ToString("X") + ".");
                    verify(probe, address);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(copy);
            }
        }

        private void VerifyLiveSpans(ReadOnlySpan<byte> snapshot)
        {
            VerifyLiveSpan(snapshot, RewardRva, RewardLength);
            VerifyLiveSpan(snapshot, GoldMessageRva, GoldMessageLength);
        }

        private void VerifyLiveSpan(ReadOnlySpan<byte> snapshot, int rva, int length)
        {
            NoKillRewardStubContract.VerifyLiveSpan(
                snapshot.Slice(rva, length), moduleBase + (ulong)rva, rva);
        }

        private static ContextHookOptions CreateRewardOptions(ulong cleanupAddress)
        {
            return new ContextHookOptions
            {
                Registers = X64SmartCPUContextRegs.All,
                HookSize = RewardLength,
                ErrorMode = CallbackErrorMode.LogAndContinue,
                Placement = OverwrittenInstructionPlacement.AfterCallback,
                InstructionSelector = original =>
                    NoKillRewardStubContract.SelectRewardInstructions(original, cleanupAddress)
            };
        }

        private void SuppressForSelectedWinner(NativePointer<X64SmartCPUContext> context)
        {
            X64SmartCPUContext* registers = context.Pointer;
            if (registers == null)
                return;

            // RDX is dead here and will be overwritten by Vanilla on either path.
            // The generated TEST executes after RedBird's flag-changing wrapper.
            registers->RDX = 1; // Fail open to the unchanged reward path.
            int mask = Volatile.Read(ref suppressionMask);
            if (mask == 0)
                return;
            int winnerId = unchecked((int)(uint)registers->RBP);
            int defeatedId = unchecked((int)(uint)registers->RBX);
            if (winnerId < 1 || winnerId > GamePlayerManagerAPI.MAX_PLAYERS ||
                defeatedId < 1 || defeatedId > GamePlayerManagerAPI.MAX_PLAYERS ||
                winnerId == defeatedId)
            {
                return;
            }

            try
            {
                bool isAI = GamePlayerManagerAPI.Instance.IsAIPlayer(winnerId);
                if (!NoKillRewardPolicy.Suppresses(mask, isAI))
                    return;

                // At 15C4EA Vanilla already incremented the victory counter and
                // initialized RBX, R14 and R11. The first award write is at 15C4FC.
                // Cleanup at 15C980 needs RCX as the resource base and zero R12D.
                registers->RCX = moduleBase + ResourceBaseRva;
                registers->R12 = 0;
                registers->RDX = 0;
            }
            catch
            {
                // A classification failure preserves Vanilla's complete reward path.
                registers->RDX = 1;
            }
        }
    }
}
