using Iced.Intel;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Extensions;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using static Iced.Intel.AssemblerRegisters;

namespace AIAttackTest
{
    internal sealed class AIAttackPermanentNativeOverrides
    {
        private const int RecruitInstructionRva = AIAttackNativeContract.RecruitImmediateRva - 6;
        private const int RecruitDisplacedBytes = 15;
        private const int VanillaRecruitTicks = 0x12C0;
        private const int LordDisplacedBytes = 14;
        private const int LordEnabledTargetRva = AIAttackNativeContract.LordBranchRva + 0x14;

        private HookTransaction transaction;
        private readonly HookHandle<X64InlineHook> recruitHook = new HookHandle<X64InlineHook>();
        private readonly HookHandle<X64InlineHook> lordHook = new HookHandle<X64InlineHook>();
        private readonly IntPtr recruitTicks;
        private readonly IntPtr attackLord;
        private bool published;

        internal AIAttackPermanentNativeOverrides(
            ScanRegion region,
            ReadOnlySpan<byte> memory,
            ulong moduleBase)
        {
            recruitTicks = Marshal.AllocHGlobal(sizeof(int));
            attackLord = Marshal.AllocHGlobal(sizeof(int));
            Marshal.WriteInt32(recruitTicks, VanillaRecruitTicks);
            Marshal.WriteInt32(attackLord, 0);
            try
            {
                ValidateLiveSpan(memory, moduleBase, RecruitInstructionRva, RecruitDisplacedBytes,
                    "AI recruitment comparison (possibly owned by Fixes)");
                ValidateLiveSpan(memory, moduleBase, AIAttackNativeContract.LordBranchRva,
                    LordDisplacedBytes, "AI lord limiter");
                ulong counterAddress = ResolveRecruitCounterAddress(memory, moduleBase);
                ulong ticksAddress = unchecked((ulong)recruitTicks.ToInt64());
                ulong lordFlagAddress = unchecked((ulong)attackLord.ToInt64());
                transaction = new HookTransaction(
                    region,
                    SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                    new HookTransactionOptions
                    {
                        FailureMode = TransactionFailureMode.RollbackAndThrow,
                        OwnsHooks = true
                    });
                transaction.AddInline(
                    recruitHook,
                    HookTarget.FromAddress(moduleBase + RecruitInstructionRva),
                    (assembler, instructions, returnAddress) =>
                        GenerateRecruitComparison(assembler, instructions, ticksAddress, counterAddress),
                    hookSize: RecruitDisplacedBytes);
                transaction.AddInline(
                    lordHook,
                    HookTarget.FromAddress(moduleBase + AIAttackNativeContract.LordBranchRva),
                    (assembler, instructions, returnAddress) =>
                        GenerateLordBranch(
                            assembler, instructions, lordFlagAddress,
                            moduleBase + LordEnabledTargetRva),
                    hookSize: LordDisplacedBytes);
                CommitResult result = transaction.Commit();
                if (!result.IsCompleteSuccess || !recruitHook.Success || !lordHook.Success ||
                    !recruitHook.IsInstalled || !lordHook.IsInstalled ||
                    recruitHook.Hook.DisplacedByteCount != RecruitDisplacedBytes ||
                    lordHook.Hook.DisplacedByteCount != LordDisplacedBytes)
                    throw new InvalidOperationException($"AI attack permanent hooks failed validation: {result}.");
            }
            catch
            {
                transaction?.Dispose();
                Marshal.FreeHGlobal(recruitTicks);
                Marshal.FreeHGlobal(attackLord);
                throw;
            }
        }

        internal void MarkPublished() => published = true;

        internal void Apply(int ticks, bool includeLord)
        {
            if (!recruitHook.IsInstalled || !lordHook.IsInstalled)
                throw new InvalidOperationException("An AI attack permanent hook is no longer installed.");
            Thread.MemoryBarrier();
            Marshal.WriteInt32(recruitTicks, ticks);
            Marshal.WriteInt32(attackLord, includeLord ? 1 : 0);
            Thread.MemoryBarrier();
        }

        internal void RestoreVanilla() => Apply(VanillaRecruitTicks, false);

        internal void RollbackUnpublished()
        {
            if (published) return;
            transaction?.Dispose();
            transaction = null;
            Marshal.FreeHGlobal(recruitTicks);
            Marshal.FreeHGlobal(attackLord);
        }

        private static ulong ResolveRecruitCounterAddress(ReadOnlySpan<byte> memory, ulong moduleBase)
        {
            int displacement = Shared.NativePatternResolver.ReadInt32(memory, RecruitInstructionRva + 2);
            return unchecked((ulong)((long)(moduleBase + RecruitInstructionRva + 10) + displacement));
        }

        private static void ValidateLiveSpan(
            ReadOnlySpan<byte> memory,
            ulong moduleBase,
            int rva,
            int length,
            string label)
        {
            if (rva < 0 || rva > memory.Length - length)
                throw new InvalidOperationException($"The {label} span lies outside the native image.");
            var live = new byte[length];
            Marshal.Copy(unchecked((IntPtr)(long)(moduleBase + unchecked((ulong)rva))), live, 0, length);
            if (!memory.Slice(rva, length).SequenceEqual(live))
                throw new InvalidOperationException($"The {label} is already modified; this competing hook will not be installed.");
        }

        private static void GenerateRecruitComparison(
            Assembler assembler,
            ReadOnlySpan<Instruction> instructions,
            ulong ticksAddress,
            ulong counterAddress)
        {
            if (instructions.Length != 3)
                throw new InvalidOperationException("Unexpected AI recruitment comparison hook boundary.");
            assembler.push(rax);
            assembler.push(rcx);
            assembler.mov(rax, ticksAddress);
            assembler.mov(eax, __dword_ptr[rax]);
            assembler.mov(rcx, counterAddress);
            assembler.cmp(__dword_ptr[rcx], eax);
            assembler.pop(rcx);
            assembler.pop(rax);
            assembler.AddInstruction(instructions[1]);
            assembler.AddInstruction(instructions[2]);
        }

        private static void GenerateLordBranch(
            Assembler assembler,
            ReadOnlySpan<Instruction> instructions,
            ulong flagAddress,
            ulong enabledTarget)
        {
            if (instructions.Length != 4)
                throw new InvalidOperationException("Unexpected AI lord-limiter hook boundary.");
            Label vanilla = assembler.CreateLabel("aiAttackLordVanilla");
            assembler.pushfq();
            assembler.push(rax);
            assembler.mov(rax, flagAddress);
            assembler.cmp(__dword_ptr[rax], 0);
            assembler.je(vanilla);
            assembler.pop(rax);
            assembler.popfq();
            assembler.AddUnrestrictedJmp(enabledTarget);
            assembler.Label(ref vanilla);
            assembler.pop(rax);
            assembler.popfq();
            foreach (Instruction instruction in instructions) assembler.AddInstruction(instruction);
        }
    }
}
