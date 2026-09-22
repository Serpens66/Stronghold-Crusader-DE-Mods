// Feature: Let Vanilla store the selected controlled Lord in control groups.
using RedBird.Core.Memory;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;

namespace BugfixesAndQoL
{
    internal sealed class LordControlGroupNativePatch : IDisposable
    {
        private const int AddExpectedDisplacedBytes = 14;
        private const int ReplaceExpectedDisplacedBytes = 16;
        private readonly PermanentInstructionSkipPatch patch;
        private bool disposed;

        public LordControlGroupNativePatch(
            ScanRegion region,
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            bool referenceHashMatches)
        {
            if (memory.IsEmpty) throw new ArgumentException("The loaded CrusaderDE image is empty.", nameof(memory));
            if (libraryBase == 0) throw new ArgumentOutOfRangeException(nameof(libraryBase));
            if (!referenceHashMatches)
                throw new InvalidOperationException("The loaded CrusaderDE.dll does not match the audited native baseline.");

            ValidateUnitTypeContracts();
            ValidateMixedDisbandContract(memory, referenceHashMatches);
            int addPatternRva = ResolveUniquePattern(
                memory, LordControlGroupNativeDefinition.AddClassifierPattern,
                LordControlGroupNativeDefinition.AddClassifierPatternRva,
                "control-group add classifier");
            int replacePatternRva = ResolveUniquePattern(
                memory, LordControlGroupNativeDefinition.ReplaceClassifierPattern,
                LordControlGroupNativeDefinition.ReplaceClassifierPatternRva,
                "control-group replace classifier");
            int addRva = checked(addPatternRva + LordControlGroupNativeDefinition.AddLordBranchOffset);
            int replaceRva = checked(replacePatternRva + LordControlGroupNativeDefinition.ReplaceLordBranchOffset);
            ValidateBytes(memory, addRva, ParseBytes(LordControlGroupNativeDefinition.VanillaAddLordBranch), "control-group Add Lord exclusion");
            ValidateBytes(memory, replaceRva, ParseBytes(LordControlGroupNativeDefinition.VanillaReplaceLordBranch), "control-group Replace Lord exclusion");

            patch = new PermanentInstructionSkipPatch(
                region,
                new PermanentInstructionSkipPatch.Site(
                    libraryBase + unchecked((ulong)addRva), 6,
                    AddExpectedDisplacedBytes, 1, "control-group Add Lord exclusion"),
                new PermanentInstructionSkipPatch.Site(
                    libraryBase + unchecked((ulong)replaceRva), 6,
                    ReplaceExpectedDisplacedBytes, 1, "control-group Replace Lord exclusion"));
            patch.SetEnabled(true);
        }

        internal void SetEnabled(bool enabled)
        {
            if (!disposed) patch.SetEnabled(enabled);
        }

        public void Dispose()
        {
            if (disposed) return;
            patch.SetEnabled(false);
            disposed = true;
        }

        internal static void ValidateMixedDisbandContract(ReadOnlySpan<byte> memory, bool referenceHashMatches)
        {
            if (memory.IsEmpty) throw new ArgumentException("The loaded CrusaderDE image is empty.", nameof(memory));
            if (!referenceHashMatches)
                throw new InvalidOperationException("The loaded CrusaderDE.dll does not match the audited native baseline.");

            ValidateUnitTypeContracts();
            ResolveUniquePattern(
                memory, ControlGroupNativeDefinition.DisbandDispatcherInstructions,
                ControlGroupNativeDefinition.DisbandDispatcherRva, "UIT_DISBAND unit-type dispatcher");
            ValidateBytes(
                memory, ControlGroupNativeDefinition.DisbandBranchRva,
                ParseBytes(ControlGroupNativeDefinition.DisbandBranchInstructions),
                "UIT_DISBAND normal-unit block");
            ValidateByte(memory, LordControlGroupNativeDefinition.LordDisbandClassEntryRva,
                LordControlGroupNativeDefinition.LordDisbandClass, "Lord disband class");
            ValidateByte(memory, LordControlGroupNativeDefinition.EuropeanArcherDisbandClassEntryRva,
                LordControlGroupNativeDefinition.EuropeanArcherDisbandClass, "European Archer disband class");
            ValidateInt32(
                memory,
                ControlGroupNativeDefinition.DisbandTargetTableRva +
                    LordControlGroupNativeDefinition.EuropeanArcherDisbandClass * sizeof(int),
                ControlGroupNativeDefinition.DisbandBranchRva,
                "normal-unit disband target");
            ValidateInt32(
                memory,
                ControlGroupNativeDefinition.DisbandTargetTableRva +
                    LordControlGroupNativeDefinition.LordDisbandClass * sizeof(int),
                ControlGroupNativeDefinition.DisbandDefaultTargetRva,
                "Lord no-op disband target");

            byte[] block = ParseBytes(ControlGroupNativeDefinition.DisbandBranchInstructions);
            int callOffset = ControlGroupNativeDefinition.DisbandCallRva - ControlGroupNativeDefinition.DisbandBranchRva;
            if (block[callOffset] != 0xE8)
                throw new InvalidOperationException("The audited UIT_DISBAND call opcode is missing.");
            int callTarget = checked(
                ControlGroupNativeDefinition.DisbandCallRva + 5 +
                Shared.NativePatternResolver.ReadInt32(memory, ControlGroupNativeDefinition.DisbandCallRva + 1));
            if (callTarget != ControlGroupNativeDefinition.DisbandFunctionRva)
                throw new InvalidOperationException($"The UIT_DISBAND call targets RVA 0x{callTarget:X}.");
        }

        private static int ResolveUniquePattern(ReadOnlySpan<byte> memory, string pattern, int expectedRva, string label)
        {
            int resolvedRva = Shared.NativePatternResolver.FindUniquePattern(memory, pattern, label);
            if (resolvedRva != expectedRva)
                throw new InvalidOperationException($"The {label} resolved to RVA 0x{resolvedRva:X}, not 0x{expectedRva:X}.");
            return resolvedRva;
        }

        private static void ValidateUnitTypeContracts()
        {
            if ((int)eChimps.CHIMP_TYPE_LORD != LordControlGroupNativeDefinition.LordUnitType ||
                (int)eChimps.CHIMP_TYPE_ARCHER != LordControlGroupNativeDefinition.EuropeanArcherUnitType)
            {
                throw new InvalidOperationException("The Script Extender unit-type enum differs from the audited control-group indexes.");
            }
        }

        private static void ValidateByte(ReadOnlySpan<byte> memory, int rva, byte expected, string label)
        {
            if ((uint)rva >= (uint)memory.Length || memory[rva] != expected)
                throw new InvalidOperationException($"The {label} differs at RVA 0x{rva:X}.");
        }

        private static void ValidateInt32(ReadOnlySpan<byte> memory, int rva, int expected, string label)
        {
            int actual = Shared.NativePatternResolver.ReadInt32(memory, rva);
            if (actual != expected)
                throw new InvalidOperationException($"The {label} is RVA 0x{actual:X}, expected RVA 0x{expected:X}.");
        }

        private static void ValidateBytes(ReadOnlySpan<byte> memory, int rva, byte[] expected, string label)
        {
            if (rva < 0 || expected == null || rva > memory.Length - expected.Length ||
                !memory.Slice(rva, expected.Length).SequenceEqual(expected))
            {
                throw new InvalidOperationException($"The {label} differs at RVA 0x{rva:X}.");
            }
        }

        private static byte[] ParseBytes(string value)
        {
            string[] tokens = value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var result = new byte[tokens.Length];
            for (int index = 0; index < tokens.Length; index++) result[index] = Convert.ToByte(tokens[index], 16);
            return result;
        }
    }
}
