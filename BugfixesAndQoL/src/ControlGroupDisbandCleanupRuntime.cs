// Remove a unit from local control groups immediately after Vanilla processes its disband.
using BepInEx.Logging;
using MonoMod.RuntimeDetour;
using System;
using System.Runtime.InteropServices;

namespace BugfixesAndQoL
{
    internal sealed unsafe class ControlGroupDisbandCleanupRuntime : IDisposable
    {
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate byte DisbandUnitDelegate(IntPtr unitManager, int unitId, byte playSound);

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly int* controlGroupRecords;
        private DisbandUnitDelegate original;
        private DisbandUnitDelegate rootedDetour;
        private NativeDetour detour;
        private bool callbackErrorLogged;

        internal ControlGroupDisbandCleanupRuntime(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings,
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            bool referenceHashMatches)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            if (memory.IsEmpty)
                throw new ArgumentException("The loaded CrusaderDE image is empty.", nameof(memory));
            if (libraryBase == 0)
                throw new ArgumentOutOfRangeException(nameof(libraryBase));
            if (!referenceHashMatches)
                throw new InvalidOperationException("The loaded CrusaderDE.dll does not match the audited native baseline.");

            int storagePatternRva = ResolveUniquePattern(
                memory,
                ControlGroupNativeDefinition.ControlGroupStoragePattern,
                ControlGroupNativeDefinition.ControlGroupStoragePatternRva,
                "control-group storage reference");
            int storageRva = checked(
                storagePatternRva + ControlGroupNativeDefinition.ControlGroupStorageNextInstructionOffset +
                Shared.NativePatternResolver.ReadInt32(
                    memory,
                    storagePatternRva + ControlGroupNativeDefinition.ControlGroupStorageDisplacementOffset));
            ValidateStorage(memory.Length, storageRva);
            int disbandFunctionRva = ValidateDisbandTarget(memory);

            controlGroupRecords = (int*)(libraryBase + unchecked((ulong)storageRva));
            rootedDetour = DisbandUnit;
            IntPtr target = new IntPtr(unchecked((long)(libraryBase + (ulong)disbandFunctionRva)));
            IntPtr replacement = Marshal.GetFunctionPointerForDelegate(rootedDetour);
            NativeDetour pending = null;
            try
            {
                pending = new NativeDetour(target, replacement, new NativeDetourConfig { ManualApply = true });
                original = pending.GenerateTrampoline<DisbandUnitDelegate>();
                pending.Apply();
                detour = pending;
            }
            catch
            {
                pending?.Dispose();
                original = null;
                rootedDetour = null;
                throw;
            }
        }

        public void Dispose()
        {
            detour?.Dispose();
            detour = null;
            original = null;
            rootedDetour = null;
        }

        private byte DisbandUnit(IntPtr unitManager, int unitId, byte playSound)
        {
            byte result = original(unitManager, unitId, playSound);
            if (!ControlGroupDisbandCleanupPolicy.ShouldClean(
                    settings.EnableClientFeatures,
                    settings.EnableDisbandedUnitControlGroupCleanup))
            {
                return result;
            }

            try
            {
                RemoveUnitFromAllGroups(unitId);
            }
            catch (Exception ex)
            {
                if (!callbackErrorLogged)
                {
                    callbackErrorLogged = true;
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Bugfixes and QoL disbanded-unit control-group cleanup failed; Vanilla behavior remains active: {ex}");
                }
            }
            return result;
        }

        private void RemoveUnitFromAllGroups(int unitId)
        {
            if (unitId < 0)
                return;

            int recordsPerGroup = checked(
                ControlGroupNativeDefinition.ControlGroupCapacity *
                ControlGroupNativeDefinition.ControlGroupRecordIntCount);
            for (int group = 0; group < ControlGroupNativeDefinition.ControlGroupCount; group++)
            {
                int* records = controlGroupRecords + checked(group * recordsPerGroup);
                for (int index = 0; index < ControlGroupNativeDefinition.ControlGroupCapacity; index++)
                {
                    int* record = records + index * ControlGroupNativeDefinition.ControlGroupRecordIntCount;
                    if (record[0] == unitId)
                        record[0] = -1;
                }
            }
        }

        private static int ValidateDisbandTarget(ReadOnlySpan<byte> memory)
        {
            ResolveUniquePattern(
                memory,
                ControlGroupNativeDefinition.DisbandDispatcherInstructions,
                ControlGroupNativeDefinition.DisbandDispatcherRva,
                "UIT_DISBAND unit-type dispatcher");
            ResolveUniquePattern(
                memory,
                ControlGroupNativeDefinition.DisbandBranchInstructions,
                ControlGroupNativeDefinition.DisbandBranchRva,
                "UIT_DISBAND normal-unit block");

            int callRva = ControlGroupNativeDefinition.DisbandCallRva;
            if (memory[callRva] != 0xE8)
                throw new InvalidOperationException("The audited UIT_DISBAND helper call opcode is missing.");
            int targetRva = checked(
                callRva + 5 + Shared.NativePatternResolver.ReadInt32(memory, callRva + 1));
            if (targetRva != ControlGroupNativeDefinition.DisbandFunctionRva)
            {
                throw new InvalidOperationException(
                    $"The UIT_DISBAND call targets RVA 0x{targetRva:X}, expected RVA 0x{ControlGroupNativeDefinition.DisbandFunctionRva:X}.");
            }
            return targetRva;
        }

        private static void ValidateStorage(int imageLength, int storageRva)
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

        private static int ResolveUniquePattern(
            ReadOnlySpan<byte> memory,
            string pattern,
            int expectedRva,
            string label)
        {
            int resolvedRva = Shared.NativePatternResolver.FindUniquePattern(memory, pattern, label);
            if (resolvedRva != expectedRva)
                throw new InvalidOperationException($"The {label} resolved to RVA 0x{resolvedRva:X}, not audited RVA 0x{expectedRva:X}.");
            return resolvedRva;
        }
    }
}
