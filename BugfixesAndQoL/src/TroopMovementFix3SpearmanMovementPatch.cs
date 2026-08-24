// Feature: Restore the intended spearman walk/run decision for synchronized movement.
using BepInEx.Logging;
using Iced.Intel;
using System;
using System.Runtime.InteropServices;
using Zhuqiaomon.Extensions;
using Zhuqiaomon.Hooks;
using Zhuqiaomon.Hooks.Transaction;
using static Iced.Intel.AssemblerRegisters;

namespace BugfixesAndQoL
{
    /// <summary>
    /// Keeps Vanilla's walking-only behavior while Improved Spearmen are
    /// disabled. When enabled, replaces only their special movement branch
    /// with the ordinary Archer walk/run decision. The option remains
    /// available to Vanilla for its unrelated combat behavior.
    /// </summary>
    internal sealed class SpearmanMovementPatch : IDisposable
    {
        // Spearman handler:
        //   cmp [unit + 0x914], 0
        //   jne walking
        //   cmp [unit + 0x99E], 0
        //   jne improved-running
        //   cmp [AdvOpt_ImprovedSpearmen], 0
        //
        // Hooking at the first comparison lets the replacement bypass both
        // Spearman-only movement conditions while leaving the rest of the
        // unit handler untouched.
        private const string SpearmanMovementDecisionPattern =
            "66 42 39 BC 3B 14 09 00 00 75 2D " +
            "66 42 39 BC 3B 9E 09 00 00 " +
            "0F 85 ?? ?? ?? ?? 39 3D ?? ?? ?? ?? " +
            "74 16 41 83 FE 63";
        private const int SpearmanMovementDecisionRva = 0x143BD9;

        // The 20-byte hook ends immediately before the original conditional
        // jump to the Improved-Spearman running block.
        private const int HookSize = 20;
        private const int ImprovedSpearmanFlagDisplacementOffset = 0x1C;
        private const int ImprovedSpearmanFlagInstructionEndOffset = 0x20;
        private const ulong WalkingTargetFromReturnAddress = 0x24;
        private const ulong RunningTargetFromReturnAddress = 0x164;

        private readonly HookTransaction transaction;
        private HookRef<X64InlineHook> movementDecisionHook =
            new HookRef<X64InlineHook>();
        private bool disposed;

        public SpearmanMovementPatch(
            ManualLogSource log,
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            bool referenceHashMatches)
        {
            if (log == null)
                throw new ArgumentNullException(nameof(log));

            int decisionRva = Shared.NativePatternResolver.ResolveUnique(
                memory,
                SpearmanMovementDecisionPattern,
                SpearmanMovementDecisionRva,
                referenceHashMatches,
                "Spearman movement decision",
                log).Rva;
            ulong decisionAddress = libraryBase + unchecked((ulong)decisionRva);
            ulong improvedSpearmanFlagAddress = ResolveImprovedSpearmanFlagAddress(
                memory,
                libraryBase,
                decisionAddress);

            transaction = new HookTransaction(
                memory,
                libraryBase,
                loggerFactory: null,
                failureMode: TransactionFailureMode.RollbackAndThrow);

            transaction.AddInline(
                ref movementDecisionHook,
                decisionAddress,
                (assembler, instructions, returnAddress) =>
                    GenerateMovementDecision(
                        assembler,
                        instructions,
                        returnAddress,
                        improvedSpearmanFlagAddress),
                hookSize: HookSize);

            transaction.Commit();

            if (!movementDecisionHook.Success)
            {
                throw new InvalidOperationException(
                    "The native Spearman movement decision was not found.");
            }

            TroopMovementFix3ModLog.Debug(
                log,
                "Native Spearman movement-option branch replaced with the " +
                "ordinary Archer walk/run decision while the Improved " +
                "Spearman option is enabled; disabled Spearmen retain their " +
                "Vanilla walking-only behavior.");
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            transaction.Unload();
            transaction.Dispose();
        }

        private static ulong ResolveImprovedSpearmanFlagAddress(
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            ulong decisionAddress)
        {
            int displacement = Marshal.ReadInt32(
                new IntPtr(unchecked((long)(
                    decisionAddress +
                    ImprovedSpearmanFlagDisplacementOffset))));
            ulong flagAddress = unchecked((ulong)(
                (long)(
                    decisionAddress +
                    ImprovedSpearmanFlagInstructionEndOffset) +
                displacement));
            ulong moduleEnd =
                libraryBase + unchecked((ulong)memory.Length);

            if (flagAddress < libraryBase ||
                flagAddress + sizeof(int) > moduleEnd)
            {
                throw new InvalidOperationException(
                    "The Improved Spearman option flag is outside the game module.");
            }

            return flagAddress;
        }

        private static void GenerateMovementDecision(
            Assembler assembler,
            ReadOnlySpan<Instruction> overwrittenInstructions,
            ulong returnAddress,
            ulong improvedSpearmanFlagAddress)
        {
            if (overwrittenInstructions.Length != 3)
            {
                throw new InvalidOperationException(
                    "Unexpected Spearman movement hook boundary.");
            }

            Label walking = assembler.CreateLabel("spearmanWalking");
            Label running = assembler.CreateLabel("spearmanRunning");

            // Preserve Vanilla's walking-only Spearman behavior when the
            // official option is disabled. RAX is safe scratch here: both
            // destination blocks overwrite it before any later use.
            assembler.mov(rax, improvedSpearmanFlagAddress);
            assembler.cmp(__dword_ptr[rax], 0);
            assembler.je(walking);

            // This is the Archer decision translated to the registers used by
            // the Spearman handler when Improved Spearmen are enabled:
            //
            //   if unit[0x914] != 0: walk
            //   tribe = tribes[unit.tribeId]
            //   if tribe[0x582] != 0: walk
            //   if unit[0xA64] == 0: run
            //   walk
            assembler.cmp(__word_ptr[rbx + r15 + 0x914], di);
            assembler.jne(walking);
            assembler.movsx(rax, __word_ptr[rbx + r15 + 0x930]);
            assembler.imul(rcx, rax, 0x688);
            assembler.cmp(__word_ptr[rcx + r13 + 0x582], di);
            assembler.jne(walking);
            assembler.cmp(__byte_ptr[rbx + r15 + 0xA64], dil);
            assembler.je(running);

            assembler.Label(ref walking);
            assembler.AddUnrestrictedJmp(
                returnAddress + WalkingTargetFromReturnAddress);

            assembler.Label(ref running);
            assembler.AddUnrestrictedJmp(
                returnAddress + RunningTargetFromReturnAddress);
        }
    }
}
