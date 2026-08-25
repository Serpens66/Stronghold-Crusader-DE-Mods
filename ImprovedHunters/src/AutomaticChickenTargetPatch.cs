using BepInEx.Logging;
using SHCDESE.Interop;
using System;
using System.Runtime.InteropServices;
using Zhuqiaomon.Windows;

namespace ImprovedHunters
{
    /// <summary>
    /// Routes chickens through Vanilla's hunter-only automatic target case.
    /// Explicit AttackUnit orders are handled before this dispatch table.
    /// </summary>
    internal sealed class AutomaticChickenTargetPatch : IDisposable
    {
        private const int TargetSelectionTypeDispatchRva = 0x18F2B2;
        private const int DispatchTargetTableRva = 0x18FA14;
        private const int TypeDispatchTableRva = 0x18FA30;
        private const int HunterOnlyCaseRva = 0x18F2DE;
        private const int CommonAcceptanceCaseRva = 0x18F3F6;
        private const int CandidateRejectionCaseRva = 0x18F7A4;
        private const int ManualAttackCommandRva = 0x18EB36;
        private const int ManualAttackTargetAssignmentRva = 0x18ED96;

        private const eChimps DeerType = eChimps.CHIMP_TYPE_DEER;
        private const eChimps ChickenType = eChimps.CHIMP_TYPE_CHICKEN;
        private const int ChickenDispatchEntryRva =
            TypeDispatchTableRva + (int)ChickenType - (int)DeerType;
        private const byte HunterOnlyDispatchIndex = 0;
        private const byte GeneralAcceptanceDispatchIndex = 6;

        private const int TypeTableDisplacementOffset = 0x1C;
        // The disp32 operands begin four bytes into movzx and three bytes into mov.
        private const int TargetTableDisplacementOffset = 0x23;

        private const string TargetSelectionTypeDispatchPattern =
            "46 0F BF 8C 07 E6 06 00 00 41 8D 41 D4 83 F8 2B " +
            "0F 87 ?? ?? ?? ?? 48 98 0F B6 84 02 ?? ?? ?? ?? " +
            "8B 8C 82 ?? ?? ?? ?? 48 03 CA FF E1";
        private const string HunterOnlyCasePattern =
            "66 42 83 BC 06 E6 06 00 00 06 0F 84 ?? ?? ?? ?? E9 ?? ?? ?? ??";
        private const string ManualAttackCommandPattern =
            "42 0F B7 84 06 F4 09 00 00 66 83 F8 04 0F 85 ?? ?? ?? ?? " +
            "4E 0F BF B4 06 F6 09 00 00";
        private const string ManualAttackTargetAssignmentPattern =
            "66 46 89 B4 26 9C 09 00 00 41 8B 8C 2C F0 06 00 00";

        private readonly ManualLogSource log;
        private readonly int dispatchEntryRva;
        private readonly IntPtr dispatchEntryAddress;
        private bool available = true;
        private bool ownsPatch;
        private bool applied;
        private bool disposed;

        public AutomaticChickenTargetPatch(
            ManualLogSource log,
            ReadOnlySpan<byte> memory,
            ulong imageBase,
            bool referenceHashMatches)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            if (memory.Length == 0 || imageBase == 0)
                throw new ArgumentException("The Crusader library is unavailable.");

            dispatchEntryRva = ValidateNativeSemantics(memory, referenceHashMatches);
            dispatchEntryAddress = new IntPtr(checked((long)imageBase + dispatchEntryRva));

            byte currentValue = Marshal.ReadByte(dispatchEntryAddress);
            if (currentValue != GeneralAcceptanceDispatchIndex)
            {
                throw new InvalidOperationException(
                    $"The chicken target dispatch entry has an unexpected initial value: " +
                    $"expected={GeneralAcceptanceDispatchIndex}, actual={currentValue}.");
            }

            Shared.DebugLogHelper.LogInfo(
                log,
                $"Improved Hunters automatic chicken target patch initialized: " +
                $"rva=0x{dispatchEntryRva:X}, address=0x{dispatchEntryAddress.ToInt64():X}, " +
                $"original={GeneralAcceptanceDispatchIndex}, hunterOnly={HunterOnlyDispatchIndex}.");
        }

        public bool IsApplied => available && applied && ownsPatch && !disposed;
        public bool IsAvailable => available && !disposed;

        public bool TrySetEnabled(bool enabled)
        {
            if (disposed)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Improved Hunters automatic chicken target state request rejected: " +
                    $"requestedEnabled={enabled}, outcome=already-disposed.");
                return false;
            }

            if (!available)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Improved Hunters automatic chicken target state request rejected: " +
                    $"requestedEnabled={enabled}, outcome=patch-unavailable, applied={applied}, ownsPatch={ownsPatch}.");
                return false;
            }

            byte expected = ownsPatch
                ? HunterOnlyDispatchIndex
                : GeneralAcceptanceDispatchIndex;
            byte current;
            try
            {
                current = Marshal.ReadByte(dispatchEntryAddress);
            }
            catch (Exception exception)
            {
                available = false;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Improved Hunters automatic chicken target state read failed: " +
                    $"requestedEnabled={enabled}, expected={expected}, outcome=patch-unavailable, error={exception}");
                return false;
            }

            if (current != expected)
            {
                DisableForConflict(expected, current);
                return false;
            }

            if (enabled == applied)
            {
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Improved Hunters automatic chicken target state unchanged: requestedEnabled={enabled}, " +
                    $"outcome={(enabled ? "already-applied" : "already-vanilla")}, current={current}, " +
                    $"available={available}, ownsPatch={ownsPatch}.");
                return enabled ? IsApplied : true;
            }

            byte desired = enabled
                ? HunterOnlyDispatchIndex
                : GeneralAcceptanceDispatchIndex;
            bool ownedBeforeWrite = ownsPatch;
            try
            {
                WriteAndVerify(expected, desired);
                ownsPatch = enabled;
                applied = enabled;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Improved Hunters automatic chicken target patch {(enabled ? "applied" : "restored")}: " +
                    $"rva=0x{dispatchEntryRva:X}, address=0x{dispatchEntryAddress.ToInt64():X}, " +
                    $"previous={expected}, current={desired}.");
                return true;
            }
            catch (Exception exception)
            {
                byte actual;
                try
                {
                    actual = Marshal.ReadByte(dispatchEntryAddress);
                }
                catch (Exception readException)
                {
                    // Conservatively retain ownership so disposal will retry restoration.
                    ownsPatch = ownedBeforeWrite || enabled;
                    applied = ownsPatch;
                    available = false;
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Improved Hunters automatic chicken target patch failed and its final value could not be read: " +
                        $"requestedEnabled={enabled}, expected={expected}, desired={desired}, " +
                        $"ownsPatch={ownsPatch}, writeError={exception}, readError={readException}");
                    return false;
                }

                if (actual == HunterOnlyDispatchIndex)
                {
                    // Preserve ownership after a failed restore so Dispose can retry.
                    ownsPatch = ownedBeforeWrite || enabled;
                    applied = true;
                }
                else
                {
                    ownsPatch = false;
                    applied = false;
                }

                available = false;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Improved Hunters automatic chicken target patch is unavailable; " +
                    $"chicken ownership neutralization remains inactive: expected={expected}, " +
                    $"desired={desired}, actual={actual}, error={exception}");
                return false;
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                Shared.DebugLogHelper.LogInfo(
                    log,
                    "Improved Hunters automatic chicken target patch disposal skipped: outcome=already-disposed.");
                return;
            }

            if (ownsPatch)
            {
                byte current;
                try
                {
                    current = Marshal.ReadByte(dispatchEntryAddress);
                }
                catch (Exception exception)
                {
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Improved Hunters automatic chicken target patch disposal could not read the owned entry: " +
                        $"outcome=restore-not-attempted, error={exception}");
                    FinishDisposal();
                    return;
                }

                if (current == HunterOnlyDispatchIndex)
                {
                    try
                    {
                        WriteAndVerify(HunterOnlyDispatchIndex, GeneralAcceptanceDispatchIndex);
                        Shared.DebugLogHelper.LogInfo(
                            log,
                            $"Improved Hunters automatic chicken target patch restored during disposal: " +
                            $"rva=0x{dispatchEntryRva:X}, current={GeneralAcceptanceDispatchIndex}.");
                    }
                    catch (Exception exception)
                    {
                        Shared.DebugLogHelper.LogError(
                            log,
                            $"Improved Hunters could not restore its automatic chicken target patch during disposal: {exception}");
                    }
                }
                else
                {
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Improved Hunters did not restore the automatic chicken target entry because it changed externally: " +
                        $"expected={HunterOnlyDispatchIndex}, actual={current}.");
                }
            }
            else
            {
                try
                {
                    byte current = Marshal.ReadByte(dispatchEntryAddress);
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"Improved Hunters automatic chicken target patch disposed without native restore: " +
                        $"outcome=restore-not-required, available={available}, applied={applied}, current={current}.");
                }
                catch (Exception exception)
                {
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Improved Hunters automatic chicken target patch disposal could not verify the unowned entry: " +
                        $"outcome=restore-not-required, error={exception}");
                }
            }

            FinishDisposal();
        }

        private void FinishDisposal()
        {
            ownsPatch = false;
            applied = false;
            available = false;
            disposed = true;
        }

        private void DisableForConflict(byte expected, byte actual)
        {
            available = false;
            applied = false;
            ownsPatch = false;
            Shared.DebugLogHelper.LogError(
                log,
                $"Improved Hunters disabled its automatic chicken target patch because the native entry changed externally: " +
                $"rva=0x{dispatchEntryRva:X}, expected={expected}, actual={actual}. " +
                "The foreign value was not overwritten and chicken ownership neutralization remains inactive.");
        }

        private void WriteAndVerify(byte expected, byte desired)
        {
            byte current = Marshal.ReadByte(dispatchEntryAddress);
            if (current != expected)
            {
                throw new InvalidOperationException(
                    $"The chicken target dispatch entry changed before writing: expected={expected}, actual={current}.");
            }

            UIntPtr size = (UIntPtr)1;
            if (!Kernel32.VirtualProtect(
                    dispatchEntryAddress,
                    size,
                    Kernel32.MemoryPermissions.PAGE_EXECUTE_READWRITE,
                    out Kernel32.MemoryPermissions oldProtection))
            {
                throw new InvalidOperationException("VirtualProtect failed for the chicken target dispatch entry.");
            }

            try
            {
                Marshal.WriteByte(dispatchEntryAddress, desired);
            }
            finally
            {
                if (!Kernel32.VirtualProtect(dispatchEntryAddress, size, oldProtection, out _))
                {
                    throw new InvalidOperationException(
                        "Restoring memory protection failed for the chicken target dispatch entry.");
                }
            }

            byte verified = Marshal.ReadByte(dispatchEntryAddress);
            if (verified != desired)
            {
                throw new InvalidOperationException(
                    $"The chicken target dispatch patch verification failed: expected={desired}, actual={verified}.");
            }
        }

        private int ValidateNativeSemantics(
            ReadOnlySpan<byte> memory,
            bool referenceHashMatches)
        {
            int typeDispatchRva = Shared.NativePatternResolver.ResolveUnique(
                memory,
                TargetSelectionTypeDispatchPattern,
                TargetSelectionTypeDispatchRva,
                referenceHashMatches,
                "automatic ranged target type dispatch",
                log).Rva;

            int resolvedTypeTableRva = Shared.NativePatternResolver.ReadInt32(
                memory,
                typeDispatchRva + TypeTableDisplacementOffset);
            int resolvedTargetTableRva = Shared.NativePatternResolver.ReadInt32(
                memory,
                typeDispatchRva + TargetTableDisplacementOffset);
            if (referenceHashMatches &&
                (resolvedTypeTableRva != TypeDispatchTableRva ||
                    resolvedTargetTableRva != DispatchTargetTableRva))
            {
                throw new InvalidOperationException(
                    $"The ranged target dispatch tables changed: " +
                    $"expectedType=0x{TypeDispatchTableRva:X}, actualType=0x{resolvedTypeTableRva:X}, " +
                    $"expectedTargets=0x{DispatchTargetTableRva:X}, actualTargets=0x{resolvedTargetTableRva:X}.");
            }

            int resolvedChickenDispatchEntryRva = checked(
                resolvedTypeTableRva + (int)ChickenType - (int)DeerType);
            ValidateRange(memory, resolvedTargetTableRva, (GeneralAcceptanceDispatchIndex + 1) * sizeof(int));
            ValidateRange(memory, resolvedChickenDispatchEntryRva, sizeof(byte));
            int hunterOnlyTarget = Shared.NativePatternResolver.ReadInt32(
                memory,
                resolvedTargetTableRva + HunterOnlyDispatchIndex * sizeof(int));
            int generalAcceptanceTarget = Shared.NativePatternResolver.ReadInt32(
                memory,
                resolvedTargetTableRva + GeneralAcceptanceDispatchIndex * sizeof(int));
            byte chickenDispatch = memory[resolvedChickenDispatchEntryRva];
            if (referenceHashMatches &&
                (hunterOnlyTarget != HunterOnlyCaseRva ||
                    generalAcceptanceTarget != CommonAcceptanceCaseRva ||
                    resolvedChickenDispatchEntryRva != ChickenDispatchEntryRva))
            {
                throw new InvalidOperationException(
                    $"The ranged target dispatch semantics changed: hunterOnlyTarget=0x{hunterOnlyTarget:X}, " +
                    $"generalTarget=0x{generalAcceptanceTarget:X}, " +
                    $"chickenEntry=0x{resolvedChickenDispatchEntryRva:X}.");
            }

            if (chickenDispatch != GeneralAcceptanceDispatchIndex)
                throw new InvalidOperationException($"The chicken target dispatch entry has unexpected value {chickenDispatch}.");

            if (!Shared.NativePatternResolver.MatchesPatternAt(
                    memory,
                    hunterOnlyTarget,
                    HunterOnlyCasePattern))
            {
                throw new InvalidOperationException("The hunter-only target case failed byte validation.");
            }

            int acceptanceTarget = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory,
                hunterOnlyTarget + 12,
                hunterOnlyTarget + 16);
            int rejectionTarget = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory,
                hunterOnlyTarget + 17,
                hunterOnlyTarget + 21);
            if (acceptanceTarget != generalAcceptanceTarget ||
                (referenceHashMatches && rejectionTarget != CandidateRejectionCaseRva))
            {
                throw new InvalidOperationException(
                    $"The hunter-only target branches changed: acceptance=0x{acceptanceTarget:X}, " +
                    $"rejection=0x{rejectionTarget:X}.");
            }

            int manualAttackCommandRva = Shared.NativePatternResolver.ResolveUnique(
                memory,
                ManualAttackCommandPattern,
                ManualAttackCommandRva,
                referenceHashMatches,
                "explicit AttackUnit command path",
                log).Rva;
            int manualAttackTargetAssignmentRva = Shared.NativePatternResolver.ResolveUnique(
                memory,
                ManualAttackTargetAssignmentPattern,
                ManualAttackTargetAssignmentRva,
                referenceHashMatches,
                "explicit AttackUnit target assignment",
                log).Rva;
            if (manualAttackCommandRva >= manualAttackTargetAssignmentRva ||
                manualAttackTargetAssignmentRva >= typeDispatchRva)
            {
                throw new InvalidOperationException(
                    "The explicit AttackUnit path no longer precedes automatic target dispatch.");
            }

            return resolvedChickenDispatchEntryRva;
        }

        private static void ValidateRange(ReadOnlySpan<byte> memory, int offset, int length)
        {
            if (offset < 0 || length <= 0 || offset > memory.Length - length)
                throw new InvalidOperationException("A ranged target dispatch table lies outside the game module.");
        }
    }
}
