using BepInEx.Logging;
using SHCDESE.GameGlobals;
using System;
using System.Runtime.InteropServices;

namespace RandomEvents
{
    internal sealed class NativeWildlifeEventDispatcher
    {
        private const int WildlifeHandlerRva = 0x11E0B0;
        private const int WildlifeBranchRva = 0x11E598;
        private const int RabbitPredicateRva = 0x117700;
        private const int RabbitSpawnerRva = 0x123A20;
        private const int RabbitTileMaskRva = 0x123AE6;
        private const int RabbitWrapperRva = 0x1048CA;
        private const int RabbitSourceWriteRva = 0x123B33;
        private const int LionCaseRva = 0x11E1C1;
        private const int LionActivationRva = 0x104BC4;
        private const int LionAction = 221;
        private const int RabbitAction = 222;
        private const int VanillaRabbitLimit = 160;

        private const string WildlifeHandlerPattern =
            "44 89 4C 24 20 44 89 44 24 18 89 54 24 10 53 55 56 57 41 54 41 55 41 57 " +
            "48 83 EC 50 4D 63 F8 8B F2 45 33 C0 4D 63 E1 33 D2 48 8B E9 E8 ?? ?? ?? ??";
        private const string WildlifeBranchPattern =
            "81 FE DD 00 00 00 75 ?? 45 8B C4 41 8B D7 48 8B CD E8 ?? ?? ?? ?? 8B C7 " +
            "48 83 C4 50 41 5F 41 5D 41 5C 5F 5E 5D 5B C3 81 FE E2 00 00 00";
        private const string RabbitPredicatePattern =
            "83 3D ?? ?? ?? ?? 00 75 ?? 81 3D ?? ?? ?? ?? A0 00 00 00 7D ?? 33 C0 " +
            "4C 8D 0D ?? ?? ?? ?? 41 BA 1F 03 00 00 48 83 F8 04 7D ?? 4D 0F BF 84 81 ?? ?? ?? ??";
        private const string RabbitSpawnerPattern =
            "48 8B C4 41 57 48 83 EC 50 83 3D ?? ?? ?? ?? 00 4C 8B F9 0F 85 ?? ?? ?? ?? " +
            "81 3D ?? ?? ?? ?? A0 00 00 00";
        private const string RabbitTileMaskPattern =
            "43 8B 84 B4 ?? ?? ?? ?? A9 ?? ?? ?? ?? 74 ?? 0F BA E0 0C";
        private const string RabbitWrapperPattern =
            "B8 B0 04 00 00 48 8B CB 66 89 05 ?? ?? ?? ?? E8 ?? ?? ?? ??";
        private const string RabbitSourceWritePattern =
            "BA DE 00 00 00 49 8B CF E8 ?? ?? ?? ?? 41 89 BF ?? ?? ?? ?? 41 89 AF ?? ?? ?? ??";
        private const string LionCasePattern =
            "81 FE DD 00 00 00 75 ?? 48 63 C1 41 F7 84 81 ?? ?? ?? ?? ?? ?? ?? ??";
        private const string LionActivationPattern =
            "48 69 CF ?? ?? ?? ?? FF C6 3B 35 ?? ?? ?? ?? C7 84 19 ?? ?? ?? ?? 00 00 01 00";

        private readonly ManualLogSource log;
        private readonly NativeVanillaEventDispatcher presentationDispatcher;
        private WildlifeHandlerDelegate wildlifeHandler;
        private int wildlifeHandlerRva;
        private IntPtr rabbitGlobalGateAddress;
        private IntPtr rabbitCountAddress;
        private IntPtr rabbitTimerAddress;
        private int rabbitSourceXOffset;
        private int rabbitSourceYOffset;
        private uint rabbitRejectedTileMask;
        private uint lionRejectedTileMask;
        private int tribeStride;
        private int lionActivationOffset;
        private string rabbitUnavailableReason = "native wildlife resolution has not run.";
        private string lionUnavailableReason = "native wildlife resolution has not run.";

        public NativeWildlifeEventDispatcher(
            ManualLogSource log,
            NativeVanillaEventDispatcher presentationDispatcher)
        {
            this.log = log;
            this.presentationDispatcher = presentationDispatcher;
        }

        public bool TryGetRabbitTileMask(out uint mask, out string failure)
        {
            mask = rabbitRejectedTileMask;
            failure = rabbitUnavailableReason;
            return wildlifeHandler != null && rabbitGlobalGateAddress != IntPtr.Zero &&
                rabbitCountAddress != IntPtr.Zero && rabbitTimerAddress != IntPtr.Zero &&
                rabbitSourceXOffset > 0 && rabbitSourceYOffset > 0 && mask != 0 &&
                presentationDispatcher.IsPresentationAvailable;
        }

        public bool TryGetLionTileMask(out uint mask, out string failure)
        {
            mask = lionRejectedTileMask;
            failure = lionUnavailableReason;
            return wildlifeHandler != null && tribeStride > 0 && lionActivationOffset > 0 &&
                mask != 0 && presentationDispatcher.IsPresentationAvailable;
        }

        public void InitializeNative(IntPtr libraryHandle, ReadOnlySpan<byte> memory, bool referenceHashMatches)
        {
            Reset();
            string commonFailure = null;
            try
            {
                NativeResolution handler = NativePatternResolver.ResolveUnique(
                    memory,
                    WildlifeHandlerPattern,
                    WildlifeHandlerRva,
                    referenceHashMatches,
                    "Vanilla wildlife handler");
                NativeResolution branches = NativePatternResolver.ResolveUnique(
                    memory,
                    WildlifeBranchPattern,
                    WildlifeBranchRva,
                    referenceHashMatches,
                    "Vanilla lion/rabbit source branches");
                if (branches.Rva < handler.Rva || branches.Rva > handler.Rva + 0x800)
                    throw new InvalidOperationException("validated lion/rabbit branches are outside the wildlife handler.");

                wildlifeHandler = Marshal.GetDelegateForFunctionPointer<WildlifeHandlerDelegate>(
                    AtRva(libraryHandle, handler.Rva));
                wildlifeHandlerRva = handler.Rva;
                LogInfo(
                    $"Native wildlife handler ready: handlerRva=0x{handler.Rva:X}/{handler.Strategy}, " +
                    $"branchRva=0x{branches.Rva:X}/{branches.Strategy}.");
            }
            catch (Exception ex)
            {
                commonFailure = ex.Message;
                wildlifeHandler = null;
                wildlifeHandlerRva = 0;
            }

            InitializeRabbitCompatibility(libraryHandle, memory, referenceHashMatches, commonFailure);
            InitializeLionCompatibility(memory, referenceHashMatches, commonFailure);
        }

        public NativeEventDispatchStatus DispatchRabbits(int tileX, int tileY, int height, out string detail)
        {
            if (wildlifeHandler == null || rabbitGlobalGateAddress == IntPtr.Zero || rabbitCountAddress == IntPtr.Zero ||
                rabbitTimerAddress == IntPtr.Zero || rabbitSourceXOffset <= 0 || rabbitSourceYOffset <= 0 ||
                !presentationDispatcher.IsPresentationAvailable)
            {
                detail = rabbitUnavailableReason;
                return NativeEventDispatchStatus.Unavailable;
            }

            int globalGate = Marshal.ReadInt32(rabbitGlobalGateAddress);
            int rabbitCount = Marshal.ReadInt32(rabbitCountAddress);
            if (globalGate != 0 || rabbitCount >= VanillaRabbitLimit)
            {
                detail = $"Vanilla prerequisite failed: globalGate={globalGate}, rabbitCount={rabbitCount}, limit={VanillaRabbitLimit}.";
                return NativeEventDispatchStatus.PrerequisiteNotMet;
            }

            IntPtr tribeManager = GetTribeManager();
            if (tribeManager == IntPtr.Zero)
            {
                detail = "native tribe manager is unavailable.";
                return NativeEventDispatchStatus.Unavailable;
            }

            short originalTimer = Marshal.ReadInt16(rabbitTimerAddress);
            Marshal.WriteInt16(rabbitTimerAddress, 1200);
            int tribeId = wildlifeHandler(tribeManager, RabbitAction, tileX, tileY, height);
            if (tribeId <= 0)
            {
                Marshal.WriteInt16(rabbitTimerAddress, originalTimer);
                detail = "Vanilla wildlife handler rejected the selected rabbit tile or could not allocate a tribe.";
                return NativeEventDispatchStatus.PrerequisiteNotMet;
            }

            // Vanilla stores the selected source after the shared handler so its rabbit state machine sees it.
            Marshal.WriteInt32(IntPtr.Add(tribeManager, rabbitSourceXOffset), tileX);
            Marshal.WriteInt32(IntPtr.Add(tribeManager, rabbitSourceYOffset), tileY);

            if (!presentationDispatcher.TryQueuePresentation(
                    201, 7, "action_rabbits.bik", "Random_Events7.wav", out string presentationFailure))
            {
                detail = $"rabbit tribe {tribeId} was created, but Vanilla presentation failed: {presentationFailure}";
                return NativeEventDispatchStatus.Unavailable;
            }

            detail = $"Vanilla rabbit tribe created: tribeId={tribeId}, sourceTile=({tileX},{tileY}); original presentation queued.";
            return NativeEventDispatchStatus.Applied;
        }

        public NativeEventDispatchStatus DispatchLions(
            int tileX,
            int tileY,
            int height,
            int strength,
            out string detail)
        {
            if (wildlifeHandler == null || tribeStride <= 0 || lionActivationOffset <= 0 ||
                !presentationDispatcher.IsPresentationAvailable)
            {
                detail = lionUnavailableReason;
                return NativeEventDispatchStatus.Unavailable;
            }

            IntPtr tribeManager = GetTribeManager();
            if (tribeManager == IntPtr.Zero)
            {
                detail = "native tribe manager is unavailable.";
                return NativeEventDispatchStatus.Unavailable;
            }

            int requestedGroups = Math.Max(1, strength);
            int createdGroups = 0;
            for (int group = 0; group < requestedGroups; group++)
            {
                int tribeId = wildlifeHandler(tribeManager, LionAction, tileX, tileY, height);
                if (tribeId <= 0)
                    break;

                // Vanilla's event wrapper arms every freshly created lion tribe after the shared spawn handler returns.
                long activationAddress = checked(
                    tribeManager.ToInt64() + (long)tribeId * tribeStride + lionActivationOffset);
                Marshal.WriteInt32(new IntPtr(activationAddress), 0x10000);
                createdGroups++;
            }

            if (createdGroups == 0)
            {
                detail = "Vanilla wildlife handler rejected the selected lion tile or could not allocate a tribe.";
                return NativeEventDispatchStatus.PrerequisiteNotMet;
            }

            if (!presentationDispatcher.TryQueuePresentation(
                    201, 8, string.Empty, "Random_Events8.wav", out string presentationFailure))
            {
                detail = $"{createdGroups} lion tribes were created, but Vanilla presentation failed: {presentationFailure}";
                return NativeEventDispatchStatus.Unavailable;
            }

            detail = $"Vanilla lion tribes created and armed: groups={createdGroups}/{requestedGroups}, sourceTile=({tileX},{tileY}); original presentation queued.";
            return NativeEventDispatchStatus.Applied;
        }

        private void InitializeRabbitCompatibility(
            IntPtr libraryHandle,
            ReadOnlySpan<byte> memory,
            bool referenceHashMatches,
            string commonFailure)
        {
            try
            {
                if (!string.IsNullOrEmpty(commonFailure))
                    throw new InvalidOperationException(commonFailure);
                if (!presentationDispatcher.IsPresentationAvailable)
                    throw new InvalidOperationException("native Vanilla presentation queue is unavailable.");

                NativeResolution predicate = NativePatternResolver.ResolveUnique(
                    memory, RabbitPredicatePattern, RabbitPredicateRva, referenceHashMatches, "Vanilla rabbit predicate");
                NativeResolution spawner = NativePatternResolver.ResolveUnique(
                    memory, RabbitSpawnerPattern, RabbitSpawnerRva, referenceHashMatches, "Vanilla rabbit spawner");
                NativeResolution tileMask = NativePatternResolver.ResolveUnique(
                    memory, RabbitTileMaskPattern, RabbitTileMaskRva, referenceHashMatches, "Vanilla rabbit tile mask");
                NativeResolution wrapper = NativePatternResolver.ResolveUnique(
                    memory, RabbitWrapperPattern, RabbitWrapperRva, referenceHashMatches, "Vanilla rabbit event wrapper");
                NativeResolution sourceWrite = NativePatternResolver.ResolveUnique(
                    memory, RabbitSourceWritePattern, RabbitSourceWriteRva, referenceHashMatches, "Vanilla rabbit source write");
                if (tileMask.Rva < spawner.Rva || tileMask.Rva > spawner.Rva + 0x180)
                    throw new InvalidOperationException("validated rabbit tile mask is outside the rabbit spawner.");
                if (!ContainsRelativeCallTo(memory, spawner.Rva, spawner.Rva + 0x180, wildlifeHandlerRva))
                    throw new InvalidOperationException("rabbit spawner does not call the validated wildlife handler.");
                int wrapperSpawnerTarget = NativePatternResolver.ResolveRelativeTarget(
                    memory, wrapper.Rva + 16, wrapper.Rva + 20);
                int sourceHandlerTarget = NativePatternResolver.ResolveRelativeTarget(
                    memory, sourceWrite.Rva + 9, sourceWrite.Rva + 13);
                if (wrapperSpawnerTarget != spawner.Rva || sourceHandlerTarget != wildlifeHandlerRva ||
                    sourceWrite.Rva < spawner.Rva || sourceWrite.Rva > spawner.Rva + 0x180)
                {
                    throw new InvalidOperationException("rabbit wrapper, spawner, and wildlife handler call chain is inconsistent.");
                }

                rabbitGlobalGateAddress = ResolveRipRelativeAddress(libraryHandle, memory, predicate.Rva, 2, 7);
                rabbitCountAddress = ResolveRipRelativeAddress(libraryHandle, memory, predicate.Rva + 9, 2, 10);
                rabbitTimerAddress = ResolveRipRelativeAddress(libraryHandle, memory, wrapper.Rva + 8, 3, 7);
                rabbitSourceXOffset = NativePatternResolver.ReadInt32(memory, sourceWrite.Rva + 16);
                rabbitSourceYOffset = NativePatternResolver.ReadInt32(memory, sourceWrite.Rva + 23);
                rabbitRejectedTileMask = unchecked((uint)NativePatternResolver.ReadInt32(memory, tileMask.Rva + 9));
                if (rabbitRejectedTileMask == 0 || rabbitSourceXOffset < 0x1000 ||
                    rabbitSourceYOffset != rabbitSourceXOffset + sizeof(int))
                {
                    throw new InvalidOperationException(
                        $"resolved rabbit state is implausible: tileMask=0x{rabbitRejectedTileMask:X8}, " +
                        $"sourceXOffset=0x{rabbitSourceXOffset:X}, sourceYOffset=0x{rabbitSourceYOffset:X}.");
                }

                rabbitUnavailableReason = string.Empty;
                LogInfo(
                    $"Native rabbit event ready: predicateRva=0x{predicate.Rva:X}/{predicate.Strategy}, " +
                    $"spawnerRva=0x{spawner.Rva:X}/{spawner.Strategy}, wrapperRva=0x{wrapper.Rva:X}/{wrapper.Strategy}, " +
                    $"rejectedTileMask=0x{rabbitRejectedTileMask:X8}, sourceOffsets=0x{rabbitSourceXOffset:X}/0x{rabbitSourceYOffset:X}.");
            }
            catch (Exception ex)
            {
                rabbitGlobalGateAddress = IntPtr.Zero;
                rabbitCountAddress = IntPtr.Zero;
                rabbitTimerAddress = IntPtr.Zero;
                rabbitSourceXOffset = 0;
                rabbitSourceYOffset = 0;
                rabbitRejectedTileMask = 0;
                rabbitUnavailableReason = $"native rabbit compatibility validation failed: {ex.Message}";
                LogError($"Rabbit events are disabled while unrelated events remain active: {rabbitUnavailableReason}");
            }
        }

        private void InitializeLionCompatibility(
            ReadOnlySpan<byte> memory,
            bool referenceHashMatches,
            string commonFailure)
        {
            try
            {
                if (!string.IsNullOrEmpty(commonFailure))
                    throw new InvalidOperationException(commonFailure);
                if (!presentationDispatcher.IsPresentationAvailable)
                    throw new InvalidOperationException("native Vanilla presentation queue is unavailable.");

                NativeResolution lionCase = NativePatternResolver.ResolveUnique(
                    memory, LionCasePattern, LionCaseRva, referenceHashMatches, "Vanilla lion spawn case");
                NativeResolution activation = NativePatternResolver.ResolveUnique(
                    memory, LionActivationPattern, LionActivationRva, referenceHashMatches, "Vanilla lion activation write");
                if (lionCase.Rva < wildlifeHandlerRva || lionCase.Rva > wildlifeHandlerRva + 0x800)
                    throw new InvalidOperationException("validated lion case is outside the wildlife handler.");
                if (!ContainsRelativeCallTo(memory, activation.Rva - 0x60, activation.Rva, wildlifeHandlerRva))
                    throw new InvalidOperationException("lion activation wrapper does not call the validated wildlife handler.");
                lionRejectedTileMask = unchecked((uint)NativePatternResolver.ReadInt32(memory, lionCase.Rva + 19));
                tribeStride = NativePatternResolver.ReadInt32(memory, activation.Rva + 3);
                lionActivationOffset = NativePatternResolver.ReadInt32(memory, activation.Rva + 18);
                if (lionRejectedTileMask == 0 || tribeStride < 0x100 || tribeStride > 0x2000 ||
                    lionActivationOffset <= 0 || lionActivationOffset >= tribeStride)
                {
                    throw new InvalidOperationException(
                        $"resolved lion layout is implausible: mask=0x{lionRejectedTileMask:X8}, " +
                        $"tribeStride=0x{tribeStride:X}, activationOffset=0x{lionActivationOffset:X}.");
                }

                lionUnavailableReason = string.Empty;
                LogInfo(
                    $"Native lion event ready: caseRva=0x{lionCase.Rva:X}/{lionCase.Strategy}, " +
                    $"activationRva=0x{activation.Rva:X}/{activation.Strategy}, " +
                    $"rejectedTileMask=0x{lionRejectedTileMask:X8}, tribeStride=0x{tribeStride:X}, " +
                    $"activationOffset=0x{lionActivationOffset:X}.");
            }
            catch (Exception ex)
            {
                lionRejectedTileMask = 0;
                tribeStride = 0;
                lionActivationOffset = 0;
                lionUnavailableReason = $"native lion compatibility validation failed: {ex.Message}";
                LogError($"Lion events are disabled while unrelated events remain active: {lionUnavailableReason}");
            }
        }

        private static IntPtr ResolveRipRelativeAddress(
            IntPtr libraryHandle,
            ReadOnlySpan<byte> memory,
            int instructionRva,
            int displacementOffset,
            int instructionLength)
        {
            int targetRva = checked(
                instructionRva + instructionLength +
                NativePatternResolver.ReadInt32(memory, instructionRva + displacementOffset));
            if (targetRva <= 0 || targetRva >= memory.Length)
                throw new InvalidOperationException($"resolved native data RVA 0x{targetRva:X} is outside the module image.");
            return AtRva(libraryHandle, targetRva);
        }

        private static IntPtr GetTribeManager() =>
            new IntPtr(unchecked((long)GameGlobalsManager.Instance.GameTribeManagerVA));

        private static bool ContainsRelativeCallTo(
            ReadOnlySpan<byte> memory,
            int startRva,
            int endRva,
            int targetRva)
        {
            int start = Math.Max(0, startRva);
            int end = Math.Min(endRva, memory.Length - 5);
            for (int rva = start; rva <= end; rva++)
            {
                if (memory[rva] != 0xE8)
                    continue;
                long target = (long)rva + 5 + NativePatternResolver.ReadInt32(memory, rva + 1);
                if (target == targetRva)
                    return true;
            }
            return false;
        }

        private void Reset()
        {
            wildlifeHandler = null;
            wildlifeHandlerRva = 0;
            rabbitGlobalGateAddress = IntPtr.Zero;
            rabbitCountAddress = IntPtr.Zero;
            rabbitTimerAddress = IntPtr.Zero;
            rabbitSourceXOffset = 0;
            rabbitSourceYOffset = 0;
            rabbitRejectedTileMask = 0;
            lionRejectedTileMask = 0;
            tribeStride = 0;
            lionActivationOffset = 0;
            rabbitUnavailableReason = "native rabbit compatibility resolution has not run.";
            lionUnavailableReason = "native lion compatibility resolution has not run.";
        }

        private static IntPtr AtRva(IntPtr libraryHandle, int rva) =>
            new IntPtr(checked(libraryHandle.ToInt64() + rva));

        private void LogInfo(string message) => Shared.DebugLogHelper.LogInfo(log, message);
        private void LogError(string message) => Shared.DebugLogHelper.LogError(log, message);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int WildlifeHandlerDelegate(
            IntPtr tribeManager,
            int action,
            int tileX,
            int tileY,
            int height);
    }
}
