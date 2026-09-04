// Feature: Let Vanilla store and display the selected controlled Lord in control groups.
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Zhuqiaomon.Windows;

namespace BugfixesAndQoL
{
    internal sealed unsafe class LordControlGroupNativePatch : IDisposable
    {
        private static readonly byte[] VanillaAddBranch = ParseBytes(
            LordControlGroupNativeDefinition.VanillaAddLordBranch);
        private static readonly byte[] VanillaReplaceBranch = ParseBytes(
            LordControlGroupNativeDefinition.VanillaReplaceLordBranch);
        private static readonly byte[] BypassBranch = ParseBytes(
            LordControlGroupNativeDefinition.BypassLordBranch);

        private readonly NativePatchSite addBranch;
        private readonly NativePatchSite replaceBranch;
        private readonly NativePatchSite lordSummaryEntry;
        private bool disposed;

        internal ulong ControlGroupRecordsAddress { get; }

        public LordControlGroupNativePatch(
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            bool referenceHashMatches)
        {
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
            ValidateMixedDisbandContract(memory, referenceHashMatches);
            int addPatternRva = ResolveUniquePattern(
                memory,
                LordControlGroupNativeDefinition.AddClassifierPattern,
                LordControlGroupNativeDefinition.AddClassifierPatternRva,
                "control-group add classifier");
            int replacePatternRva = ResolveUniquePattern(
                memory,
                LordControlGroupNativeDefinition.ReplaceClassifierPattern,
                LordControlGroupNativeDefinition.ReplaceClassifierPatternRva,
                "control-group replace classifier");
            int summaryPatternRva = ResolveUniquePattern(
                memory,
                LordControlGroupNativeDefinition.SummaryClassifierPattern,
                LordControlGroupNativeDefinition.SummaryClassifierPatternRva,
                "control-group summary classifier");
            int controlGroupStoragePatternRva = ResolveUniquePattern(
                memory,
                ControlGroupNativeDefinition.ControlGroupStoragePattern,
                ControlGroupNativeDefinition.ControlGroupStoragePatternRva,
                "control-group storage reference");

            int summaryTypeTableRva = Shared.NativePatternResolver.ReadInt32(
                memory,
                checked(summaryPatternRva +
                    LordControlGroupNativeDefinition.SummaryTypeTableDisplacementOffset));
            int summaryDispatchTableRva = Shared.NativePatternResolver.ReadInt32(
                memory,
                checked(summaryPatternRva +
                    LordControlGroupNativeDefinition.SummaryDispatchTableDisplacementOffset));
            ValidateSummaryTables(memory, summaryTypeTableRva, summaryDispatchTableRva);
            int controlGroupStorageRva = checked(
                controlGroupStoragePatternRva +
                ControlGroupNativeDefinition.ControlGroupStorageNextInstructionOffset +
                Shared.NativePatternResolver.ReadInt32(
                    memory,
                    controlGroupStoragePatternRva +
                    ControlGroupNativeDefinition.ControlGroupStorageDisplacementOffset));
            ValidateControlGroupStorage(memory.Length, controlGroupStorageRva);
            ControlGroupRecordsAddress = libraryBase + unchecked((ulong)controlGroupStorageRva);

            addBranch = new NativePatchSite(
                libraryBase + unchecked((ulong)(addPatternRva +
                    LordControlGroupNativeDefinition.AddLordBranchOffset)),
                VanillaAddBranch,
                BypassBranch,
                "control-group Add Lord exclusion");
            replaceBranch = new NativePatchSite(
                libraryBase + unchecked((ulong)(replacePatternRva +
                    LordControlGroupNativeDefinition.ReplaceLordBranchOffset)),
                VanillaReplaceBranch,
                BypassBranch,
                "control-group Replace Lord exclusion");
            lordSummaryEntry = new NativePatchSite(
                libraryBase + unchecked((ulong)LordControlGroupNativeDefinition.LordSummaryEntryRva),
                new[] { LordControlGroupNativeDefinition.VanillaUnmappedSummaryClass },
                new[] { LordControlGroupNativeDefinition.EuropeanArcherSummaryClass },
                "control-group Lord summary icon mapping");

            try
            {
                // Validate the whole transaction before changing any executable byte.
                addBranch.ValidateOriginal();
                replaceBranch.ValidateOriginal();
                lordSummaryEntry.ValidateOriginal();
                addBranch.Apply();
                replaceBranch.Apply();
                lordSummaryEntry.Apply();
            }
            catch (Exception installError)
            {
                try
                {
                    RestoreAppliedSites();
                }
                catch (Exception rollbackError)
                {
                    throw new AggregateException(
                        "Installing the Lord control-group patch failed and rollback also failed.",
                        installError,
                        rollbackError);
                }
                throw;
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            RestoreAppliedSites();
            disposed = true;
        }

        internal static void ValidateMixedDisbandContract(
            ReadOnlySpan<byte> memory,
            bool referenceHashMatches)
        {
            if (memory.IsEmpty)
                throw new ArgumentException("The loaded CrusaderDE image is empty.", nameof(memory));
            if (!referenceHashMatches)
                throw new InvalidOperationException(
                    "The loaded CrusaderDE.dll does not match the audited native baseline.");

            ValidateUnitTypeContracts();
            ResolveUniquePattern(
                memory,
                ControlGroupNativeDefinition.DisbandDispatcherInstructions,
                ControlGroupNativeDefinition.DisbandDispatcherRva,
                "UIT_DISBAND unit-type dispatcher");
            ValidateBytes(
                memory,
                ControlGroupNativeDefinition.DisbandBranchRva,
                ParseBytes(ControlGroupNativeDefinition.DisbandBranchInstructions),
                "UIT_DISBAND normal-unit block");
            ValidateByte(
                memory,
                LordControlGroupNativeDefinition.LordDisbandClassEntryRva,
                LordControlGroupNativeDefinition.LordDisbandClass,
                "Lord disband class");
            ValidateByte(
                memory,
                LordControlGroupNativeDefinition.EuropeanArcherDisbandClassEntryRva,
                LordControlGroupNativeDefinition.EuropeanArcherDisbandClass,
                "European Archer disband class");
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
            int callOffset = ControlGroupNativeDefinition.DisbandCallRva -
                ControlGroupNativeDefinition.DisbandBranchRva;
            if (block[callOffset] != 0xE8)
                throw new InvalidOperationException("The audited UIT_DISBAND call opcode is missing.");
            int callDisplacement = Shared.NativePatternResolver.ReadInt32(
                memory,
                ControlGroupNativeDefinition.DisbandCallRva + 1);
            int callTarget = checked(
                ControlGroupNativeDefinition.DisbandCallRva + 5 + callDisplacement);
            if (callTarget != ControlGroupNativeDefinition.DisbandFunctionRva)
            {
                throw new InvalidOperationException(
                    $"The UIT_DISBAND call targets RVA 0x{callTarget:X}, expected RVA 0x{ControlGroupNativeDefinition.DisbandFunctionRva:X}.");
            }
        }

        private void RestoreAppliedSites()
        {
            // Restore in reverse transaction order. Each site validates that it still owns its bytes.
            Exception firstFailure = null;
            RestoreSite(lordSummaryEntry, ref firstFailure);
            RestoreSite(replaceBranch, ref firstFailure);
            RestoreSite(addBranch, ref firstFailure);
            if (firstFailure != null)
            {
                throw new InvalidOperationException(
                    "One or more Lord control-group patch sites could not be restored.",
                    firstFailure);
            }
        }

        private static void RestoreSite(NativePatchSite site, ref Exception firstFailure)
        {
            try
            {
                site?.RestoreIfApplied();
            }
            catch (Exception ex)
            {
                if (firstFailure == null)
                    firstFailure = ex;
            }
        }

        private static int ResolveUniquePattern(
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
            if ((int)eChimps.CHIMP_TYPE_LORD != LordControlGroupNativeDefinition.LordUnitType ||
                (int)eChimps.CHIMP_TYPE_ARCHER != LordControlGroupNativeDefinition.EuropeanArcherUnitType)
            {
                throw new InvalidOperationException(
                    "The Script Extender unit-type enum differs from the audited control-group indexes.");
            }
        }

        private static void ValidateSummaryTables(
            ReadOnlySpan<byte> memory,
            int typeTableRva,
            int dispatchTableRva)
        {
            if (typeTableRva != LordControlGroupNativeDefinition.SummaryTypeTableRva ||
                dispatchTableRva != LordControlGroupNativeDefinition.SummaryDispatchTableRva)
            {
                throw new InvalidOperationException(
                    "The control-group summary tables differ from the audited native contract.");
            }

            ValidateByte(
                memory,
                LordControlGroupNativeDefinition.LordSummaryEntryRva,
                LordControlGroupNativeDefinition.VanillaUnmappedSummaryClass,
                "Lord summary class");
            ValidateByte(
                memory,
                LordControlGroupNativeDefinition.EuropeanArcherSummaryEntryRva,
                LordControlGroupNativeDefinition.EuropeanArcherSummaryClass,
                "European Archer summary class");
            ValidateInt32(
                memory,
                dispatchTableRva +
                    LordControlGroupNativeDefinition.EuropeanArcherSummaryClass * sizeof(int),
                LordControlGroupNativeDefinition.EuropeanArcherSummaryTargetRva,
                "European Archer summary target");
            ValidateInt32(
                memory,
                dispatchTableRva +
                    LordControlGroupNativeDefinition.VanillaUnmappedSummaryClass * sizeof(int),
                LordControlGroupNativeDefinition.UnmappedSummaryTargetRva,
                "unmapped summary target");
        }

        private static void ValidateControlGroupStorage(int imageLength, int storageRva)
        {
            long byteLength = checked(
                (long)ControlGroupNativeDefinition.ControlGroupCount *
                ControlGroupNativeDefinition.ControlGroupCapacity *
                ControlGroupNativeDefinition.ControlGroupRecordIntCount * sizeof(int));
            if (storageRva != ControlGroupNativeDefinition.ControlGroupStorageRva ||
                storageRva < 0 || (long)storageRva + byteLength > imageLength)
            {
                throw new InvalidOperationException(
                    $"The control-group storage differs from the audited layout: RVA 0x{storageRva:X}.");
            }
        }

        private static void ValidateByte(
            ReadOnlySpan<byte> memory,
            int rva,
            byte expected,
            string label)
        {
            if ((uint)rva >= (uint)memory.Length || memory[rva] != expected)
            {
                int actual = (uint)rva < (uint)memory.Length ? memory[rva] : -1;
                throw new InvalidOperationException(
                    $"The {label} is {actual}, expected {expected} at RVA 0x{rva:X}.");
            }
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

        private static void ValidateBytes(
            ReadOnlySpan<byte> memory,
            int rva,
            byte[] expected,
            string label)
        {
            if (rva < 0 || expected == null || rva > memory.Length - expected.Length)
                throw new InvalidOperationException($"The {label} lies outside the loaded image.");
            for (int i = 0; i < expected.Length; i++)
            {
                if (memory[rva + i] != expected[i])
                {
                    throw new InvalidOperationException(
                        $"The {label} differs at RVA 0x{rva + i:X}: " +
                        $"0x{memory[rva + i]:X2}, expected 0x{expected[i]:X2}.");
                }
            }
        }

        private static byte[] ParseBytes(string value)
        {
            string[] tokens = value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var result = new byte[tokens.Length];
            for (int i = 0; i < tokens.Length; i++)
                result[i] = Convert.ToByte(tokens[i], 16);
            return result;
        }

        private sealed class NativePatchSite
        {
            private readonly ulong address;
            private readonly byte[] original;
            private readonly byte[] replacement;
            private readonly string label;
            private bool applied;

            internal NativePatchSite(
                ulong address,
                byte[] original,
                byte[] replacement,
                string label)
            {
                if (address == 0)
                    throw new ArgumentOutOfRangeException(nameof(address));
                if (original == null || replacement == null || original.Length == 0 ||
                    original.Length != replacement.Length)
                {
                    throw new ArgumentException("Native patch byte spans must be non-empty and equal in length.");
                }

                this.address = address;
                this.original = (byte[])original.Clone();
                this.replacement = (byte[])replacement.Clone();
                this.label = label ?? throw new ArgumentNullException(nameof(label));
            }

            internal void ValidateOriginal() => VerifyCurrentBytes(original, "preflight");

            internal void Apply()
            {
                VerifyCurrentBytes(original, "apply");
                try
                {
                    WriteBytes(replacement);
                    applied = true;
                }
                catch
                {
                    // A protection/cache failure can occur after the bytes were copied.
                    // Mark ownership so the outer transaction still attempts rollback.
                    applied = CurrentBytesMatch(replacement);
                    throw;
                }
            }

            internal void RestoreIfApplied()
            {
                if (!applied)
                    return;
                VerifyCurrentBytes(replacement, "rollback");
                WriteBytes(original);
                applied = false;
            }

            private void VerifyCurrentBytes(byte[] expected, string operation)
            {
                byte[] current = ReadBytes(expected.Length);
                for (int i = 0; i < expected.Length; i++)
                {
                    if (current[i] != expected[i])
                    {
                        throw new InvalidOperationException(
                            $"Cannot {operation} native patch '{label}': byte +0x{i:X} is " +
                            $"0x{current[i]:X2}, expected 0x{expected[i]:X2}.");
                    }
                }
            }

            private bool CurrentBytesMatch(byte[] expected)
            {
                byte[] current = ReadBytes(expected.Length);
                for (int i = 0; i < expected.Length; i++)
                {
                    if (current[i] != expected[i])
                        return false;
                }
                return true;
            }

            private byte[] ReadBytes(int length)
            {
                var bytes = new byte[length];
                Marshal.Copy(unchecked((IntPtr)(long)address), bytes, 0, length);
                return bytes;
            }

            private void WriteBytes(byte[] bytes)
            {
                IntPtr pointer = unchecked((IntPtr)(long)address);
                UIntPtr size = unchecked((UIntPtr)(uint)bytes.Length);
                if (!Kernel32.VirtualProtect(
                        pointer,
                        size,
                        Kernel32.MemoryPermissions.PAGE_EXECUTE_READWRITE,
                        out Kernel32.MemoryPermissions oldProtection))
                {
                    throw new InvalidOperationException(
                        $"VirtualProtect failed for native patch '{label}'.");
                }

                try
                {
                    Marshal.Copy(bytes, 0, pointer, bytes.Length);
                }
                finally
                {
                    if (!Kernel32.VirtualProtect(pointer, size, oldProtection, out _))
                    {
                        throw new InvalidOperationException(
                            $"Restoring memory protection failed for native patch '{label}'.");
                    }
                }

                if (!MinWinAPI.FlushInstructionCache(
                        Process.GetCurrentProcess().Handle,
                        pointer,
                        size))
                {
                    throw new InvalidOperationException(
                        $"Flushing the instruction cache failed for native patch '{label}'.");
                }

                VerifyCurrentBytes(bytes, "verify");
            }
        }
    }
}
