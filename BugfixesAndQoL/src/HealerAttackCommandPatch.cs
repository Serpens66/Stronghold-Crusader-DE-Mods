// Feature: Keep Bedouin Healers stationary when a mixed group attacks a unit.
using BepInEx.Logging;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using Zhuqiaomon.Windows;

namespace BugfixesAndQoL
{
    internal sealed unsafe class HealerAttackCommandPatch : IDisposable
    {
        private byte* firstHealerEntry;
        private byte* secondHealerEntry;
        private bool firstPatched;
        private bool secondPatched;
        private bool disposed;

        public HealerAttackCommandPatch(
            ManualLogSource log,
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            bool referenceHashMatches)
        {
            if (log == null)
                throw new ArgumentNullException(nameof(log));
            if (memory.IsEmpty)
                throw new ArgumentException("The loaded CrusaderDE image is empty.", nameof(memory));
            if (libraryBase == 0)
                throw new ArgumentOutOfRangeException(nameof(libraryBase));
            if (!referenceHashMatches)
            {
                throw new InvalidOperationException(
                    "The loaded CrusaderDE.dll does not match the audited native baseline.");
            }

            ValidateUnitTypeContracts();
            int firstClassifierRva = ResolveUniqueClassifier(
                memory,
                HealerAttackCommandFixNativeDefinition.FirstClassifierPattern,
                HealerAttackCommandFixNativeDefinition.FirstClassifierRva,
                "AttackUnit first unit classifier");
            int secondClassifierRva = ResolveUniqueClassifier(
                memory,
                HealerAttackCommandFixNativeDefinition.SecondClassifierPattern,
                HealerAttackCommandFixNativeDefinition.SecondClassifierRva,
                "AttackUnit formation-assignment classifier");

            int firstTableRva = ReadAbsoluteTableRva(
                memory,
                firstClassifierRva + HealerAttackCommandFixNativeDefinition.FirstTableInstructionOffset,
                HealerAttackCommandFixNativeDefinition.TableDisplacementOffset,
                "first classifier table");
            int secondTableRva = ReadAbsoluteTableRva(
                memory,
                secondClassifierRva + HealerAttackCommandFixNativeDefinition.SecondTableInstructionOffset,
                HealerAttackCommandFixNativeDefinition.TableDisplacementOffset,
                "second classifier table");
            int firstDispatchTableRva = ReadAbsoluteTableRva(
                memory,
                firstClassifierRva + HealerAttackCommandFixNativeDefinition.FirstDispatchInstructionOffset,
                HealerAttackCommandFixNativeDefinition.DispatchDisplacementOffset,
                "first dispatch-target table");
            int secondDispatchTableRva = ReadAbsoluteTableRva(
                memory,
                secondClassifierRva + HealerAttackCommandFixNativeDefinition.SecondDispatchInstructionOffset,
                HealerAttackCommandFixNativeDefinition.DispatchDisplacementOffset,
                "second dispatch-target table");

            ValidateTableLocations(
                firstTableRva,
                secondTableRva,
                firstDispatchTableRva,
                secondDispatchTableRva);
            ValidateDispatchTargets(memory, firstDispatchTableRva, secondDispatchTableRva);

            int engineerIndex = HealerAttackCommandFixNativeDefinition.EngineerType -
                HealerAttackCommandFixNativeDefinition.UnitTypeTableMinimum;
            int healerIndex = HealerAttackCommandFixNativeDefinition.BedouinHealerType -
                HealerAttackCommandFixNativeDefinition.UnitTypeTableMinimum;
            ValidateByte(memory, firstTableRva + engineerIndex,
                HealerAttackCommandFixNativeDefinition.FirstNoOpClass,
                "first Engineer classification");
            ValidateByte(memory, secondTableRva + engineerIndex,
                HealerAttackCommandFixNativeDefinition.SecondNoOpClass,
                "second Engineer classification");
            ValidateByte(memory, firstTableRva + healerIndex,
                HealerAttackCommandFixNativeDefinition.FirstVanillaHealerClass,
                "first Bedouin Healer classification");
            ValidateByte(memory, secondTableRva + healerIndex,
                HealerAttackCommandFixNativeDefinition.SecondVanillaHealerClass,
                "second Bedouin Healer classification");

            firstHealerEntry = (byte*)(libraryBase + unchecked((ulong)(firstTableRva + healerIndex)));
            secondHealerEntry = (byte*)(libraryBase + unchecked((ulong)(secondTableRva + healerIndex)));
            try
            {
                WriteValidatedByte(
                    firstHealerEntry,
                    HealerAttackCommandFixNativeDefinition.FirstVanillaHealerClass,
                    HealerAttackCommandFixNativeDefinition.FirstNoOpClass,
                    "first Bedouin Healer AttackUnit classifier");
                firstPatched = true;
                WriteValidatedByte(
                    secondHealerEntry,
                    HealerAttackCommandFixNativeDefinition.SecondVanillaHealerClass,
                    HealerAttackCommandFixNativeDefinition.SecondNoOpClass,
                    "second Bedouin Healer AttackUnit classifier");
                secondPatched = true;
            }
            catch
            {
                // Account for a write succeeding immediately before protection restoration fails.
                firstPatched = firstHealerEntry != null &&
                    *firstHealerEntry == HealerAttackCommandFixNativeDefinition.FirstNoOpClass;
                secondPatched = secondHealerEntry != null &&
                    *secondHealerEntry == HealerAttackCommandFixNativeDefinition.SecondNoOpClass;
                RestorePatchedBytes();
                throw;
            }

            Shared.DebugLogHelper.LogDebug(
                log,
                "Bugfixes and QoL Healer attack-command fix installed; native table entries=2.");
        }

        public void Dispose()
        {
            if (disposed)
                return;
            RestorePatchedBytes();
            disposed = true;
        }

        private static int ResolveUniqueClassifier(
            ReadOnlySpan<byte> memory,
            string pattern,
            int expectedRva,
            string label)
        {
            int resolvedRva = Shared.NativePatternResolver.FindUniquePattern(memory, pattern, label);
            if (resolvedRva != expectedRva)
            {
                throw new InvalidOperationException(
                    $"The {label} resolved to RVA 0x{resolvedRva:X}, not audited RVA 0x{expectedRva:X}.");
            }
            return resolvedRva;
        }

        private static void ValidateUnitTypeContracts()
        {
            if ((int)eChimps.CHIMP_TYPE_ENGINEER != HealerAttackCommandFixNativeDefinition.EngineerType ||
                (int)eChimps.CHIMP_TYPE_BEDOUIN_HEALER != HealerAttackCommandFixNativeDefinition.BedouinHealerType)
            {
                throw new InvalidOperationException(
                    "The Script Extender unit-type enum differs from the audited native classifier indexes.");
            }
        }

        private static int ReadAbsoluteTableRva(
            ReadOnlySpan<byte> memory,
            int instructionRva,
            int displacementOffset,
            string label)
        {
            int tableRva = Shared.NativePatternResolver.ReadInt32(
                memory,
                checked(instructionRva + displacementOffset));
            if (tableRva <= 0 || tableRva >= memory.Length)
                throw new InvalidOperationException($"The {label} lies outside the loaded game image.");
            return tableRva;
        }

        private static void ValidateTableLocations(
            int firstTableRva,
            int secondTableRva,
            int firstDispatchTableRva,
            int secondDispatchTableRva)
        {
            if (firstTableRva != HealerAttackCommandFixNativeDefinition.FirstTableRva ||
                secondTableRva != HealerAttackCommandFixNativeDefinition.SecondTableRva ||
                firstDispatchTableRva != HealerAttackCommandFixNativeDefinition.FirstDispatchTableRva ||
                secondDispatchTableRva != HealerAttackCommandFixNativeDefinition.SecondDispatchTableRva)
            {
                throw new InvalidOperationException(
                    "One or more AttackUnit classification-table locations differ from the audited native contract.");
            }
        }

        private static void ValidateDispatchTargets(
            ReadOnlySpan<byte> memory,
            int firstDispatchTableRva,
            int secondDispatchTableRva)
        {
            ValidateInt32(memory, firstDispatchTableRva,
                HealerAttackCommandFixNativeDefinition.FirstMeleeTargetRva,
                "first melee dispatch target");
            ValidateInt32(memory,
                firstDispatchTableRva + HealerAttackCommandFixNativeDefinition.FirstNoOpClass * sizeof(int),
                HealerAttackCommandFixNativeDefinition.FirstNoOpTargetRva,
                "first no-op dispatch target");
            ValidateInt32(memory, secondDispatchTableRva,
                HealerAttackCommandFixNativeDefinition.SecondMeleeTargetRva,
                "second melee dispatch target");
            ValidateInt32(memory,
                secondDispatchTableRva + HealerAttackCommandFixNativeDefinition.SecondNoOpClass * sizeof(int),
                HealerAttackCommandFixNativeDefinition.SecondNoOpTargetRva,
                "second no-op dispatch target");
        }

        private static void ValidateInt32(
            ReadOnlySpan<byte> memory,
            int rva,
            int expected,
            string label)
        {
            int actual = Shared.NativePatternResolver.ReadInt32(memory, rva);
            if (actual != expected)
            {
                throw new InvalidOperationException(
                    $"The {label} is RVA 0x{actual:X}, expected RVA 0x{expected:X}.");
            }
        }

        private static void ValidateByte(
            ReadOnlySpan<byte> memory,
            int rva,
            byte expected,
            string label)
        {
            if ((uint)rva >= (uint)memory.Length)
                throw new InvalidOperationException($"The {label} lies outside the loaded game image.");
            if (memory[rva] != expected)
            {
                throw new InvalidOperationException(
                    $"The {label} is {memory[rva]}, expected {expected} at RVA 0x{rva:X}.");
            }
        }

        private void RestorePatchedBytes()
        {
            if (secondPatched)
            {
                WriteValidatedByte(
                    secondHealerEntry,
                    HealerAttackCommandFixNativeDefinition.SecondNoOpClass,
                    HealerAttackCommandFixNativeDefinition.SecondVanillaHealerClass,
                    "second Bedouin Healer AttackUnit classifier rollback");
                secondPatched = false;
            }
            if (firstPatched)
            {
                WriteValidatedByte(
                    firstHealerEntry,
                    HealerAttackCommandFixNativeDefinition.FirstNoOpClass,
                    HealerAttackCommandFixNativeDefinition.FirstVanillaHealerClass,
                    "first Bedouin Healer AttackUnit classifier rollback");
                firstPatched = false;
            }
        }

        private static void WriteValidatedByte(
            byte* address,
            byte expected,
            byte replacement,
            string label)
        {
            if (address == null || *address != expected)
            {
                byte actual = address == null ? byte.MaxValue : *address;
                throw new InvalidOperationException(
                    $"Cannot patch {label}: current value {actual}, expected {expected}.");
            }

            IntPtr pointer = unchecked((IntPtr)address);
            UIntPtr size = new UIntPtr(1);
            if (!Kernel32.VirtualProtect(
                    pointer,
                    size,
                    Kernel32.MemoryPermissions.PAGE_EXECUTE_READWRITE,
                    out Kernel32.MemoryPermissions oldProtection))
            {
                throw new InvalidOperationException($"VirtualProtect failed for {label}.");
            }

            try
            {
                *address = replacement;
            }
            finally
            {
                if (!Kernel32.VirtualProtect(pointer, size, oldProtection, out _))
                    throw new InvalidOperationException($"Restoring memory protection failed for {label}.");
            }

            if (*address != replacement)
                throw new InvalidOperationException($"Post-write validation failed for {label}.");
        }
    }
}
