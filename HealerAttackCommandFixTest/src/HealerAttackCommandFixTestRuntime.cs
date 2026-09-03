using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using Zhuqiaomon.Windows;

namespace HealerAttackCommandFixTest
{
    internal sealed unsafe class HealerAttackCommandFixTestRuntime : IDisposable
    {
        private readonly ManualLogSource log;
        private readonly ulong libraryBase;
        private IDisposable mapStartSubscription;
        private IDisposable loadSaveSubscription;
        private byte* firstHealerEntry;
        private byte* secondHealerEntry;
        private bool firstPatched;
        private bool secondPatched;
        private bool tickSubscribed;
        private bool verificationPending;
        private bool applied;

        public HealerAttackCommandFixTestRuntime(
            ManualLogSource log,
            ulong libraryBase)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            if (libraryBase == 0)
                throw new ArgumentOutOfRangeException(nameof(libraryBase));

            this.libraryBase = libraryBase;
        }

        public void Apply(ReadOnlySpan<byte> memory)
        {
            if (applied)
                return;
            if (memory.IsEmpty)
                throw new ArgumentException("The loaded CrusaderDE image is empty.", nameof(memory));

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

                mapStartSubscription = MapLoaderR3EventHooks.OnStartMap.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(args => ArmRuntimeVerification($"new map campaignMapId={args.CampaignMapId}"));
                loadSaveSubscription = MapLoaderR3EventHooks.OnLoadSave.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(args => ArmRuntimeVerification($"loaded save file={args.FileName ?? "<null>"}"));
                GameTimeManagerAPI.Instance.OnTick += OnGameTick;
                tickSubscribed = true;
            }
            catch
            {
                if (tickSubscribed)
                {
                    GameTimeManagerAPI.Instance.OnTick -= OnGameTick;
                    tickSubscribed = false;
                }
                mapStartSubscription?.Dispose();
                loadSaveSubscription?.Dispose();
                mapStartSubscription = null;
                loadSaveSubscription = null;

                // A protection-restore failure can occur after the byte was written
                // but before WriteValidatedByte returns and updates its tracking flag.
                firstPatched = firstHealerEntry != null &&
                    *firstHealerEntry == HealerAttackCommandFixNativeDefinition.FirstNoOpClass;
                secondPatched = secondHealerEntry != null &&
                    *secondHealerEntry == HealerAttackCommandFixNativeDefinition.SecondNoOpClass;
                RestorePatchedBytes();
                throw;
            }
            applied = true;

            Shared.DebugLogHelper.LogInfo(
                log,
                "HEALER_ATTACK_GROUP_FIX_READY: correctionActive=true, command=AttackUnit/4, " +
                $"unitType=BedouinHealer/{HealerAttackCommandFixNativeDefinition.BedouinHealerType}, " +
                $"firstEntryRva=0x{firstTableRva + healerIndex:X}, firstClass=" +
                $"{HealerAttackCommandFixNativeDefinition.FirstVanillaHealerClass}->" +
                $"{HealerAttackCommandFixNativeDefinition.FirstNoOpClass}, secondEntryRva=" +
                $"0x{secondTableRva + healerIndex:X}, secondClass=" +
                $"{HealerAttackCommandFixNativeDefinition.SecondVanillaHealerClass}->" +
                $"{HealerAttackCommandFixNativeDefinition.SecondNoOpClass}, nativeHooks=0.");
        }

        public void Dispose()
        {
            if (!applied)
                return;

            if (tickSubscribed)
            {
                GameTimeManagerAPI.Instance.OnTick -= OnGameTick;
                tickSubscribed = false;
            }
            mapStartSubscription?.Dispose();
            loadSaveSubscription?.Dispose();
            mapStartSubscription = null;
            loadSaveSubscription = null;
            RestorePatchedBytes();
            applied = false;
        }

        private int ResolveUniqueClassifier(
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

            Shared.DebugLogHelper.LogInfo(
                log,
                $"Native address resolved: name={label}, method=unique-signature, rva=0x{resolvedRva:X}.");
            return resolvedRva;
        }

        private static void ValidateUnitTypeContracts()
        {
            if ((int)eChimps.CHIMP_TYPE_ENGINEER !=
                    HealerAttackCommandFixNativeDefinition.EngineerType ||
                (int)eChimps.CHIMP_TYPE_BEDOUIN_HEALER !=
                    HealerAttackCommandFixNativeDefinition.BedouinHealerType)
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
            int displacementRva = checked(instructionRva + displacementOffset);
            int tableRva = Shared.NativePatternResolver.ReadInt32(memory, displacementRva);
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

        private void ArmRuntimeVerification(string reason)
        {
            verificationPending = true;
            Shared.DebugLogHelper.LogInfo(
                log,
                $"HEALER_ATTACK_GROUP_FIX_MAP_ACTIVE: reason={reason}; awaitingSimulationTick=true.");
        }

        private void OnGameTick(int tick)
        {
            if (!verificationPending)
                return;

            verificationPending = false;
            byte first = *firstHealerEntry;
            byte second = *secondHealerEntry;
            if (first == HealerAttackCommandFixNativeDefinition.FirstNoOpClass &&
                second == HealerAttackCommandFixNativeDefinition.SecondNoOpClass)
            {
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"HEALER_ATTACK_GROUP_FIX_RUNTIME_CONFIRMED: tick={tick}, " +
                    $"firstClass={first}, secondClass={second}, patchStillActive=true.");
                return;
            }

            Shared.DebugLogHelper.LogError(
                log,
                $"HEALER_ATTACK_GROUP_FIX_RUNTIME_INVALID: tick={tick}, firstClass={first}, " +
                $"secondClass={second}, expected=" +
                $"{HealerAttackCommandFixNativeDefinition.FirstNoOpClass}/" +
                $"{HealerAttackCommandFixNativeDefinition.SecondNoOpClass}.");
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
