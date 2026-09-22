// Feature: Keep Bedouin Healers stationary when a mixed group attacks a unit.
using BepInEx.Logging;
using Iced.Intel;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Extensions;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using static Iced.Intel.AssemblerRegisters;

namespace BugfixesAndQoL
{
    internal sealed class HealerAttackCommandPatch : IDisposable
    {
        private const int FirstExpectedDisplacedBytes = 18;
        private const int SecondExpectedDisplacedBytes = 16;
        private HookTransaction transaction;
        private readonly HookHandle<X64InlineHook> firstClassifierHook = new HookHandle<X64InlineHook>();
        private readonly HookHandle<X64InlineHook> secondClassifierHook = new HookHandle<X64InlineHook>();
        private readonly IntPtr enabledFlag;
        private bool published;
        private bool disposed;

        public HealerAttackCommandPatch(
            ManualLogSource log,
            ScanRegion region,
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            bool referenceHashMatches)
        {
            if (log == null) throw new ArgumentNullException(nameof(log));
            if (memory.IsEmpty) throw new ArgumentException("The loaded CrusaderDE image is empty.", nameof(memory));
            if (libraryBase == 0) throw new ArgumentOutOfRangeException(nameof(libraryBase));
            if (!referenceHashMatches)
                throw new InvalidOperationException("The loaded CrusaderDE.dll does not match the audited native baseline.");

            ValidateUnitTypeContracts();
            int firstRva = ResolveUniqueClassifier(memory,
                HealerAttackCommandFixNativeDefinition.FirstClassifierPattern,
                HealerAttackCommandFixNativeDefinition.FirstClassifierRva,
                "AttackUnit first unit classifier");
            int secondRva = ResolveUniqueClassifier(memory,
                HealerAttackCommandFixNativeDefinition.SecondClassifierPattern,
                HealerAttackCommandFixNativeDefinition.SecondClassifierRva,
                "AttackUnit formation-assignment classifier");
            ValidateNativeTables(memory);

            enabledFlag = Marshal.AllocHGlobal(sizeof(int));
            Marshal.WriteInt32(enabledFlag, 0);
            ulong flagAddress = unchecked((ulong)enabledFlag.ToInt64());
            try
            {
                transaction = BugfixesHookInfrastructure.CreateOwnedTransaction(region);
                transaction.AddInline(
                    firstClassifierHook,
                    HookTarget.FromAddress(libraryBase + unchecked((ulong)firstRva)),
                    (assembler, instructions, returnAddress) =>
                        GenerateClassifier(assembler, instructions, flagAddress, prefixInstructionCount: 3),
                    hookSize: FirstExpectedDisplacedBytes);
                transaction.AddInline(
                    secondClassifierHook,
                    HookTarget.FromAddress(libraryBase + unchecked((ulong)secondRva)),
                    (assembler, instructions, returnAddress) =>
                        GenerateClassifier(assembler, instructions, flagAddress, prefixInstructionCount: 2),
                    hookSize: SecondExpectedDisplacedBytes);
                CommitResult result = transaction.Commit();
                if (!result.IsCompleteSuccess || !firstClassifierHook.Success || !secondClassifierHook.Success ||
                    !firstClassifierHook.IsInstalled || !secondClassifierHook.IsInstalled)
                    throw new InvalidOperationException("Healer AttackUnit classifier hooks were not installed atomically.");
                if (firstClassifierHook.Hook.DisplacedByteCount != FirstExpectedDisplacedBytes ||
                    secondClassifierHook.Hook.DisplacedByteCount != SecondExpectedDisplacedBytes)
                    throw new InvalidOperationException("A Healer AttackUnit hook displaced an unexpected native span.");

                SetEnabled(true);
                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"Healer AttackUnit permanent classifier hooks installed: first={FirstExpectedDisplacedBytes}, second={SecondExpectedDisplacedBytes}.");
                published = true;
            }
            catch
            {
                transaction?.Dispose();
                Marshal.FreeHGlobal(enabledFlag);
                throw;
            }
        }

        internal void SetEnabled(bool value)
        {
            if (disposed) return;
            if (!firstClassifierHook.IsInstalled || !secondClassifierHook.IsInstalled)
                throw new InvalidOperationException("A permanent Healer AttackUnit hook is no longer installed.");
            Thread.MemoryBarrier();
            Marshal.WriteInt32(enabledFlag, value ? 1 : 0);
            Thread.MemoryBarrier();
        }

        public void Dispose()
        {
            if (disposed) return;
            SetEnabled(false);
            disposed = true;
            if (!published)
            {
                transaction?.Dispose();
                Marshal.FreeHGlobal(enabledFlag);
            }
        }

        private static void GenerateClassifier(
            Assembler assembler,
            ReadOnlySpan<Instruction> overwrittenInstructions,
            ulong enabledFlagAddress,
            int prefixInstructionCount)
        {
            if (overwrittenInstructions.Length < prefixInstructionCount)
                throw new InvalidOperationException("Unexpected Healer classifier hook boundary.");

            Label vanilla = assembler.CreateLabel("healerClassifierVanilla");
            Label mapped = assembler.CreateLabel("healerClassifierMapped");
            Label done = assembler.CreateLabel("healerClassifierDone");
            assembler.pushfq();
            assembler.push(rax);
            assembler.mov(rax, enabledFlagAddress);
            assembler.cmp(__dword_ptr[rax], 0);
            assembler.je(vanilla);
            assembler.pop(rax);
            assembler.popfq();

            for (int index = 0; index < prefixInstructionCount; index++)
                assembler.AddInstruction(overwrittenInstructions[index]);
            assembler.cmp(eax, HealerIndex);
            assembler.jne(mapped);
            assembler.mov(eax, EngineerIndex);

            assembler.Label(ref mapped);
            for (int index = prefixInstructionCount; index < overwrittenInstructions.Length; index++)
                assembler.AddInstruction(overwrittenInstructions[index]);
            assembler.jmp(done);

            assembler.Label(ref vanilla);
            assembler.pop(rax);
            assembler.popfq();
            foreach (Instruction instruction in overwrittenInstructions)
                assembler.AddInstruction(instruction);

            assembler.Label(ref done);
            assembler.nop();
        }

        private static int EngineerIndex => HealerAttackCommandFixNativeDefinition.EngineerType -
            HealerAttackCommandFixNativeDefinition.UnitTypeTableMinimum;
        private static int HealerIndex => HealerAttackCommandFixNativeDefinition.BedouinHealerType -
            HealerAttackCommandFixNativeDefinition.UnitTypeTableMinimum;

        private static int ResolveUniqueClassifier(ReadOnlySpan<byte> memory, string pattern, int expectedRva, string label)
        {
            int resolvedRva = Shared.NativePatternResolver.FindUniquePattern(memory, pattern, label);
            if (resolvedRva != expectedRva)
                throw new InvalidOperationException($"The {label} resolved to RVA 0x{resolvedRva:X}, not 0x{expectedRva:X}.");
            return resolvedRva;
        }

        private static void ValidateUnitTypeContracts()
        {
            if ((int)eChimps.CHIMP_TYPE_ENGINEER != HealerAttackCommandFixNativeDefinition.EngineerType ||
                (int)eChimps.CHIMP_TYPE_BEDOUIN_HEALER != HealerAttackCommandFixNativeDefinition.BedouinHealerType)
                throw new InvalidOperationException("The Script Extender unit-type enum differs from the audited native classifier indexes.");
        }

        private static void ValidateNativeTables(ReadOnlySpan<byte> memory)
        {
            ValidateByte(memory, HealerAttackCommandFixNativeDefinition.FirstHealerEntryRva,
                HealerAttackCommandFixNativeDefinition.FirstVanillaHealerClass, "first Healer class");
            ValidateByte(memory, HealerAttackCommandFixNativeDefinition.SecondHealerEntryRva,
                HealerAttackCommandFixNativeDefinition.SecondVanillaHealerClass, "second Healer class");
            ValidateByte(memory, HealerAttackCommandFixNativeDefinition.FirstTableRva + EngineerIndex,
                HealerAttackCommandFixNativeDefinition.FirstNoOpClass, "first Engineer no-op class");
            ValidateByte(memory, HealerAttackCommandFixNativeDefinition.SecondTableRva + EngineerIndex,
                HealerAttackCommandFixNativeDefinition.SecondNoOpClass, "second Engineer no-op class");
        }

        private static void ValidateByte(ReadOnlySpan<byte> memory, int rva, byte expected, string label)
        {
            if ((uint)rva >= (uint)memory.Length || memory[rva] != expected)
                throw new InvalidOperationException($"The {label} differs at RVA 0x{rva:X}.");
        }
    }
}
