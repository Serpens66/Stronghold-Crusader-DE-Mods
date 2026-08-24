using BepInEx.Logging;
using MonoMod.RuntimeDetour;
using SHCDESE.GameGlobals;
using Shared;
using System;
using System.Runtime.InteropServices;

namespace RandomEvents
{
    internal sealed class NativeWildlifeEventDispatcher
    {
        private const int WildlifeHandlerRva = 0x11E150;
        private const int WildlifeBranchRva = 0x11E638;
        private const int RabbitPredicateRva = 0x1177A0;
        private const int RabbitSpawnerRva = 0x123AC0;
        private const int RabbitTileMaskRva = 0x123B86;
        private const int RabbitWrapperRva = 0x10496A;
        private const int RabbitSourceWriteRva = 0x123BD3;
        private const int LionCaseRva = 0x11E261;
        private const int LionActivationRva = 0x104C64;
        private const int LionActionPointWrapperRva = 0x104C46;
        private const int ActionPointHandlerRva = 0xF4D40;
        private const int LionAction = 221;
        private const int RabbitAction = 222;
        private const int VanillaRabbitLimit = 160;
        private const string LionAudioFileName = "Random_Events14.wav";

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
        private const string LionActionPointWrapperPattern =
            "44 8B 84 24 ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? 8B 94 24 ?? ?? ?? ?? " +
            "48 63 F8 E8 ?? ?? ?? ?? 48 69 CF ?? ?? ?? ??";
        private const string ActionPointHandlerPattern =
            "40 53 48 83 EC 20 48 63 81 28 3A 01 00 48 8B D9 83 F8 14 7D ?? " +
            "89 94 81 38 39 01 00 48 63 81 28 3A 01 00 44 89 84 81 88 39 01 00 " +
            "FF 15 ?? ?? ?? ?? 48 63";

        private readonly ManualLogSource log;
        private readonly NativeVanillaEventDispatcher presentationDispatcher;
        private WildlifeHandlerDelegate wildlifeHandler;
        private int wildlifeHandlerRva;
        private IntPtr rabbitCountAddress;
        private IntPtr rabbitTimerAddress;
        private int rabbitSourceXOffset;
        private int rabbitSourceYOffset;
        private uint rabbitRejectedTileMask;
        private uint lionRejectedTileMask;
        private int tribeStride;
        private int lionActivationOffset;
        private ActionPointHandlerDelegate actionPointHandler;
        private ActionPointHandlerDelegate actionPointOriginal;
        private ActionPointHandlerDelegate rootedActionPointDetour;
        private NativeDetour actionPointDetour;
        private IntPtr actionPointHandlerAddress;
        private IntPtr actionPointManager;
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
            return wildlifeHandler != null && rabbitCountAddress != IntPtr.Zero &&
                rabbitTimerAddress != IntPtr.Zero &&
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
                    "Vanilla wildlife handler",
                    log);
                NativeResolution branches = NativePatternResolver.ResolveUnique(
                    memory,
                    WildlifeBranchPattern,
                    WildlifeBranchRva,
                    referenceHashMatches,
                    "Vanilla lion/rabbit source branches",
                    log);
                if (branches.Rva < handler.Rva || branches.Rva > handler.Rva + 0x800)
                    throw new InvalidOperationException("validated lion/rabbit branches are outside the wildlife handler.");

                wildlifeHandler = Marshal.GetDelegateForFunctionPointer<WildlifeHandlerDelegate>(
                    AtRva(libraryHandle, handler.Rva));
                wildlifeHandlerRva = handler.Rva;
            }
            catch (Exception ex)
            {
                commonFailure = ex.Message;
                wildlifeHandler = null;
                wildlifeHandlerRva = 0;
            }

            InitializeRabbitCompatibility(libraryHandle, memory, referenceHashMatches, commonFailure);
            InitializeLionCompatibility(memory, referenceHashMatches, commonFailure);
            InitializeActionPointCompatibility(libraryHandle, memory, referenceHashMatches);
        }

        public NativeEventDispatchStatus DispatchRabbits(int tileX, int tileY, int height, out string detail)
        {
            if (wildlifeHandler == null || rabbitCountAddress == IntPtr.Zero ||
                rabbitTimerAddress == IntPtr.Zero || rabbitSourceXOffset <= 0 || rabbitSourceYOffset <= 0 ||
                !presentationDispatcher.IsPresentationAvailable)
            {
                detail = rabbitUnavailableReason;
                return NativeEventDispatchStatus.Unavailable;
            }

            int rabbitCount = Marshal.ReadInt32(rabbitCountAddress);
            if (rabbitCount >= VanillaRabbitLimit)
            {
                detail = $"Vanilla prerequisite failed: rabbitCount={rabbitCount}, limit={VanillaRabbitLimit}.";
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

            // Rabbits do not receive this marker in our direct handler path, so add Vanilla's clickable location cue.
            bool actionPointQueued = false;
            if (actionPointHandler != null && actionPointManager != IntPtr.Zero)
            {
                actionPointHandler(actionPointManager, tileX, tileY);
                actionPointQueued = true;
            }

            if (!presentationDispatcher.TryQueuePresentation(
                    201, 7, "action_rabbits.bik", "Random_Events7.wav", out string presentationFailure))
            {
                detail = $"rabbit tribe {tribeId} was created, but Vanilla presentation failed: {presentationFailure}";
                return NativeEventDispatchStatus.Unavailable;
            }

            detail = $"Vanilla rabbit tribe created: tribeId={tribeId}, sourceTile=({tileX},{tileY}), " +
                $"actionPoint={(actionPointQueued ? "queued" : "disabled")}; original presentation queued.";
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
            int actionPointsQueued = 0;
            for (int group = 0; group < requestedGroups; group++)
            {
                int tribeId = wildlifeHandler(tribeManager, LionAction, tileX, tileY, height);
                if (tribeId <= 0)
                    break;

                // Vanilla adds one clickable minimap action point after every spawned lion tribe.
                if (actionPointHandler != null && actionPointManager != IntPtr.Zero)
                {
                    actionPointHandler(actionPointManager, tileX, tileY);
                    actionPointsQueued++;
                }

                // Vanilla's event wrapper then arms every freshly created lion tribe.
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
                    201,
                    8,
                    string.Empty,
                    LionAudioFileName,
                    out string presentationFailure))
            {
                detail = $"{createdGroups} lion tribes were created, but Vanilla presentation failed: {presentationFailure}";
                return NativeEventDispatchStatus.Unavailable;
            }

            string actionPointDetail = actionPointHandler != null
                ? $"actionPoints={actionPointsQueued}"
                : "actionPoints=disabled";
            detail = $"Vanilla lion tribes created and armed: groups={createdGroups}/{requestedGroups}, " +
                $"sourceTile=({tileX},{tileY}), {actionPointDetail}; original presentation queued.";
            return NativeEventDispatchStatus.Applied;
        }

        public bool TryQueueActionPoint(int tileX, int tileY, out string failure)
        {
            failure = string.Empty;
            if (actionPointHandler == null || actionPointManager == IntPtr.Zero)
            {
                failure = "native Vanilla action-point queue is unavailable.";
                return false;
            }

            try
            {
                actionPointHandler(actionPointManager, tileX, tileY);
                return true;
            }
            catch (Exception ex)
            {
                failure = ex.Message;
                return false;
            }
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
                    memory, RabbitPredicatePattern, RabbitPredicateRva, referenceHashMatches, "Vanilla rabbit predicate", log);
                NativeResolution spawner = NativePatternResolver.ResolveUnique(
                    memory, RabbitSpawnerPattern, RabbitSpawnerRva, referenceHashMatches, "Vanilla rabbit spawner", log);
                NativeResolution tileMask = NativePatternResolver.ResolveUnique(
                    memory, RabbitTileMaskPattern, RabbitTileMaskRva, referenceHashMatches, "Vanilla rabbit tile mask", log);
                NativeResolution wrapper = NativePatternResolver.ResolveUnique(
                    memory, RabbitWrapperPattern, RabbitWrapperRva, referenceHashMatches, "Vanilla rabbit event wrapper", log);
                NativeResolution sourceWrite = NativePatternResolver.ResolveUnique(
                    memory, RabbitSourceWritePattern, RabbitSourceWriteRva, referenceHashMatches, "Vanilla rabbit source write", log);
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

                // The first predicate field is unrelated shared state (99 in a normal match).
                // Calling the validated wildlife handler directly must only preserve the rabbit limit.
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
            }
            catch (Exception ex)
            {
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
                    memory, LionCasePattern, LionCaseRva, referenceHashMatches, "Vanilla lion spawn case", log);
                NativeResolution activation = NativePatternResolver.ResolveUnique(
                    memory, LionActivationPattern, LionActivationRva, referenceHashMatches, "Vanilla lion activation write", log);
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
            }
            catch (Exception ex)
            {
                lionRejectedTileMask = 0;
                tribeStride = 0;
                lionActivationOffset = 0;
                actionPointHandler = null;
                actionPointManager = IntPtr.Zero;
                lionUnavailableReason = $"native lion compatibility validation failed: {ex.Message}";
                LogError($"Lion events are disabled while unrelated events remain active: {lionUnavailableReason}");
            }
        }

        private void InitializeActionPointCompatibility(
            IntPtr libraryHandle,
            ReadOnlySpan<byte> memory,
            bool referenceHashMatches)
        {
            try
            {
                NativeResolution wrapper = NativePatternResolver.ResolveUnique(
                    memory,
                    LionActionPointWrapperPattern,
                    LionActionPointWrapperRva,
                    referenceHashMatches,
                    "Vanilla lion action-point wrapper",
                    log);
                NativeResolution handler = NativePatternResolver.ResolveUnique(
                    memory,
                    ActionPointHandlerPattern,
                    ActionPointHandlerRva,
                    referenceHashMatches,
                    "Vanilla action-point handler",
                    log);

                int wrapperHandlerTarget = NativePatternResolver.ResolveRelativeTarget(
                    memory, wrapper.Rva + 26, wrapper.Rva + 30);
                if (wrapperHandlerTarget != handler.Rva)
                    throw new InvalidOperationException("lion wrapper does not call the validated action-point handler.");
                actionPointManager = ResolveRipRelativeAddress(
                    libraryHandle, memory, wrapper.Rva + 8, 3, 7);
                InstallActionPointFilter(AtRva(libraryHandle, handler.Rva));
            }
            catch (Exception ex)
            {
                actionPointHandler = null;
                actionPointManager = IntPtr.Zero;
                LogError(
                    "Rabbit and lion minimap action points are disabled while wildlife spawning remains active: " +
                    $"native compatibility validation failed: {ex.Message}");
            }
        }

        private void InstallActionPointFilter(IntPtr resolvedHandlerAddress)
        {
            if (actionPointDetour != null)
            {
                if (resolvedHandlerAddress != actionPointHandlerAddress)
                {
                    throw new InvalidOperationException(
                        $"action-point handler changed after detour installation: " +
                        $"installed=0x{actionPointHandlerAddress.ToInt64():X}, resolved=0x{resolvedHandlerAddress.ToInt64():X}.");
                }

                actionPointHandler = FilterActionPoint;
                return;
            }

            rootedActionPointDetour = FilterActionPoint;
            IntPtr detourAddress = Marshal.GetFunctionPointerForDelegate(rootedActionPointDetour);
            NativeDetour installedDetour = null;
            try
            {
                var config = new NativeDetourConfig { ManualApply = true };
                installedDetour = new NativeDetour(resolvedHandlerAddress, detourAddress, config);
                ActionPointHandlerDelegate installedOriginal = installedDetour.GenerateTrampoline<ActionPointHandlerDelegate>();
                actionPointHandlerAddress = resolvedHandlerAddress;
                actionPointOriginal = installedOriginal;
                actionPointHandler = FilterActionPoint;
                installedDetour.Apply();
                actionPointDetour = installedDetour;
                LogDebug($"Native minimap action-point target filter installed: address=0x{resolvedHandlerAddress.ToInt64():X}.");
            }
            catch
            {
                installedDetour?.Dispose();
                actionPointHandlerAddress = IntPtr.Zero;
                actionPointOriginal = null;
                actionPointHandler = null;
                rootedActionPointDetour = null;
                throw;
            }
        }

        private void FilterActionPoint(IntPtr manager, int tileX, int tileY)
        {
            // Action points are minimap UI and must not leak events targeted at another human.
            ActionPointHandlerDelegate original = actionPointOriginal;
            if (RandomEventsPresentationScope.IsSuppressed)
            {
                RandomEventsPresentationScope.RecordSuppressedActionPoint();
                return;
            }
            if (original != null)
                original(manager, tileX, tileY);
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
            rabbitCountAddress = IntPtr.Zero;
            rabbitTimerAddress = IntPtr.Zero;
            rabbitSourceXOffset = 0;
            rabbitSourceYOffset = 0;
            rabbitRejectedTileMask = 0;
            lionRejectedTileMask = 0;
            tribeStride = 0;
            lionActivationOffset = 0;
            actionPointHandler = null;
            actionPointManager = IntPtr.Zero;
            rabbitUnavailableReason = "native rabbit compatibility resolution has not run.";
            lionUnavailableReason = "native lion compatibility resolution has not run.";
        }

        private static IntPtr AtRva(IntPtr libraryHandle, int rva) =>
            new IntPtr(checked(libraryHandle.ToInt64() + rva));

        private void LogError(string message) => Shared.DebugLogHelper.LogError(log, message);
        private void LogDebug(string message) => Shared.DebugLogHelper.LogDebug(log, message);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int WildlifeHandlerDelegate(
            IntPtr tribeManager,
            int action,
            int tileX,
            int tileY,
            int height);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void ActionPointHandlerDelegate(
            IntPtr actionPointManager,
            int tileX,
            int tileY);
    }
}
