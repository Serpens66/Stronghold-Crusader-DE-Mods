using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.GameGlobals;
using SHCDESE.Interop;
using Shared;
using System;
using System.Runtime.InteropServices;

namespace RandomEvents
{
    internal enum NativeEventDispatchStatus
    {
        Applied,
        PrerequisiteNotMet,
        Unavailable
    }

    internal sealed unsafe class NativeVanillaEventDispatcher
    {
        private const int HasBuildingRva = 0xB8D50;
        private const int WheatHandlerRva = 0xC3130;
        private const int HopsHandlerRva = 0xC2E30;
        private const int AppleHandlerRva = 0xC2C30;
        private const int MadCowUnitHandlerRva = 0x194BF0;
        private const int MadCowBuildingHandlerRva = 0xC6090;
        private const int GranaryTheftHandlerRva = 0xC5F70;
        private const int PresentationCallsiteRva = 0xF9B24;
        private const int PresentationHandlerRva = 0x103160;
        private const int PresentationManagerRva = 0x1B61EE0;

        private const int WheatFarmType = 30;
        private const int HopsFarmType = 31;
        private const int AppleFarmType = 32;
        private const int CattleFarmType = 33;

        private const string HasBuildingPattern =
            "4C 63 51 50 49 83 FA 01 7E 33 41 B9 01 00 00 00 48 81 C1 5E 04 00 00 66 83 79 FA 00 74 10";
        private const string WheatPattern =
            "40 53 45 33 C9 8B DA 4C 8B D9 44 39 49 50 0F 8E ?? ?? ?? ?? 48 89 6C 24 10 48 8D 81 32 01 00 00";
        private const string HopsPattern =
            "48 83 EC 08 45 33 C0 44 8B DA 4C 8B D1 44 39 41 50 0F 8E ?? ?? ?? ?? 48 89 5C 24 10 48 8D 81 32 01 00 00 48 89 6C 24 18 4C 8D 89 28 02 00 00";
        private const string ApplePattern =
            "48 83 EC 08 45 33 C0 44 8B DA 4C 8B D1 44 39 41 50 0F 8E ?? ?? ?? ?? 48 89 1C 24 48 8D 81 32 01 00 00 48 8D 1D ?? ?? ?? ?? 4C 8D 89 28 02 00 00";
        private const string MadCowUnitPattern =
            "48 89 6C 24 10 48 89 74 24 18 57 48 83 EC 20 33 FF 8B EA 48 8B F1 39 39 7E ?? 48 89 5C 24 30 48 8D 99 F8 08 00 00";
        private const string MadCowBuildingPattern =
            "45 33 C0 4C 8B C9 44 39 41 50 7E ?? 48 8D 81 32 01 00 00 41 BA 40 06 00 00 0F 1F 80 00 00 00 00";
        private const string GranaryTheftPattern =
            "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 48 89 7C 24 20 41 56 48 83 EC 30 48 63 F2 4C 8D 35 ?? ?? ?? ?? 41 8B C8 B8 1F 85 EB 51";
        private const string PresentationCallsitePattern =
            "48 89 44 24 20 BA C9 00 00 00 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 41 0F B6 8C 24 7E 06 00 00";
        private const string PresentationHandlerPattern =
            "83 39 00 0F 84 ?? ?? ?? ?? 48 63 81 4C 09 00 00 83 F8 0A 0F 84 ?? ?? ?? ?? 45 33 D2 44 89 94 81 2C 01 00 00";

        private readonly ManualLogSource log;
        private IntPtr presentationManager;
        private HasBuildingDelegate hasBuilding;
        private BuildingEventDelegate wheatHandler;
        private BuildingEventDelegate hopsHandler;
        private BuildingEventDelegate appleHandler;
        private UnitEventDelegate madCowUnitHandler;
        private BuildingEventDelegate madCowBuildingHandler;
        private GranaryTheftDelegate granaryTheftHandler;
        private PresentationDelegate presentationHandler;

        public NativeVanillaEventDispatcher(ManualLogSource log)
        {
            this.log = log;
        }

        public bool IsPresentationAvailable => presentationHandler != null && presentationManager != IntPtr.Zero;

        public bool TryQueuePresentation(
            int messageId,
            int presentationId,
            string video,
            string audio,
            out string failure)
        {
            failure = string.Empty;
            if (!IsPresentationAvailable)
            {
                failure = "native Vanilla presentation queue is unavailable.";
                return false;
            }

            try
            {
                QueuePresentation(messageId, presentationId, video, audio);
                return true;
            }
            catch (Exception ex)
            {
                failure = ex.Message;
                return false;
            }
        }

        public void InitializeNative(IntPtr libraryHandle, ReadOnlySpan<byte> memory, bool referenceHashMatches)
        {
            Reset();
            hasBuilding = TryResolveDelegate<HasBuildingDelegate>(
                libraryHandle, memory, HasBuildingPattern, HasBuildingRva, referenceHashMatches, "building prerequisite");
            wheatHandler = TryResolveDelegate<BuildingEventDelegate>(
                libraryHandle, memory, WheatPattern, WheatHandlerRva, referenceHashMatches, "wheat infestation");
            hopsHandler = TryResolveDelegate<BuildingEventDelegate>(
                libraryHandle, memory, HopsPattern, HopsHandlerRva, referenceHashMatches, "hops beetles");
            appleHandler = TryResolveDelegate<BuildingEventDelegate>(
                libraryHandle, memory, ApplePattern, AppleHandlerRva, referenceHashMatches, "apple blight");
            madCowUnitHandler = TryResolveDelegate<UnitEventDelegate>(
                libraryHandle, memory, MadCowUnitPattern, MadCowUnitHandlerRva, referenceHashMatches, "mad-cow unit effect");
            madCowBuildingHandler = TryResolveDelegate<BuildingEventDelegate>(
                libraryHandle, memory, MadCowBuildingPattern, MadCowBuildingHandlerRva, referenceHashMatches, "mad-cow building effect");
            granaryTheftHandler = TryResolveDelegate<GranaryTheftDelegate>(
                libraryHandle, memory, GranaryTheftPattern, GranaryTheftHandlerRva, referenceHashMatches, "granary theft");
            ResolvePresentation(libraryHandle, memory, referenceHashMatches);
        }

        public NativeEventDispatchStatus Dispatch(
            RandomEventKind kind,
            int strength,
            int targetPlayerId,
            out string detail)
        {
            if (!TryGetEventAvailability(kind, out detail))
            {
                return NativeEventDispatchStatus.Unavailable;
            }

            IntPtr buildingManager = new IntPtr(unchecked((long)GameGlobalsManager.Instance.GameBuildingManagerVA));
            if (buildingManager == IntPtr.Zero)
            {
                detail = "native building manager is unavailable.";
                return NativeEventDispatchStatus.Unavailable;
            }

            switch (kind)
            {
                case RandomEventKind.WheatInfestation:
                    return DispatchFarmEvent(
                        buildingManager,
                        targetPlayerId,
                        WheatFarmType,
                        wheatHandler,
                        3,
                        "action_wheat_die.bik",
                        "Random_Events3.wav",
                        out detail);

                case RandomEventKind.HopsBeetles:
                    return DispatchFarmEvent(
                        buildingManager,
                        targetPlayerId,
                        HopsFarmType,
                        hopsHandler,
                        4,
                        "action_hops_die.bik",
                        "Random_Events4.wav",
                        out detail);

                case RandomEventKind.AppleBlight:
                    return DispatchFarmEvent(
                        buildingManager,
                        targetPlayerId,
                        AppleFarmType,
                        appleHandler,
                        5,
                        "action_apples_die.bik",
                        "Random_Events5.wav",
                        out detail);

                case RandomEventKind.MadCows:
                    IntPtr unitManager = new IntPtr(unchecked((long)GameGlobalsManager.Instance.GameUnitManagerVA));
                    if (unitManager == IntPtr.Zero)
                    {
                        detail = "native unit manager is unavailable.";
                        return NativeEventDispatchStatus.Unavailable;
                    }
                    if (hasBuilding(buildingManager, targetPlayerId, CattleFarmType) == 0)
                    {
                        detail = "Vanilla prerequisite failed: target player has no eligible cattle farm.";
                        return NativeEventDispatchStatus.PrerequisiteNotMet;
                    }

                    // Vanilla mutates existing cows first and then arms all matching cattle farms.
                    madCowUnitHandler(unitManager, targetPlayerId);
                    madCowBuildingHandler(buildingManager, targetPlayerId);
                    QueuePresentation(201, 10, string.Empty, "Random_Events10.wav");
                    QueuePresentation(0, 0, "action_mad_cows.bik", string.Empty);
                    detail = "Vanilla cow-unit and cattle-farm handlers applied; original audio and video queued.";
                    return NativeEventDispatchStatus.Applied;

                case RandomEventKind.GranaryTheft:
                    if (!GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(targetPlayerId, out GamePlayerResources* resources) ||
                        resources == null || resources->r_FirstGranaryId == 0)
                    {
                        detail = "Vanilla prerequisite failed: target player has no registered first granary.";
                        return NativeEventDispatchStatus.PrerequisiteNotMet;
                    }

                    granaryTheftHandler(buildingManager, targetPlayerId, strength);
                    QueuePresentation(
                        201,
                        strength == 100 ? 16 : 14,
                        "action_steal_bread.bik",
                        "general_message3.wav");
                    detail = $"Vanilla granary theft applied at {strength}% and original presentation queued.";
                    return NativeEventDispatchStatus.Applied;

                default:
                    detail = $"event kind {kind} has no native timeline-event handler.";
                    return NativeEventDispatchStatus.Unavailable;
            }
        }

        private NativeEventDispatchStatus DispatchFarmEvent(
            IntPtr buildingManager,
            int targetPlayerId,
            int farmType,
            BuildingEventDelegate handler,
            int presentationId,
            string video,
            string audio,
            out string detail)
        {
            if (hasBuilding(buildingManager, targetPlayerId, farmType) == 0)
            {
                detail = $"Vanilla prerequisite failed: target player has no eligible farm of type {farmType}.";
                return NativeEventDispatchStatus.PrerequisiteNotMet;
            }

            handler(buildingManager, targetPlayerId);
            QueuePresentation(201, presentationId, video, audio);
            detail = $"Vanilla farm handler applied for building type {farmType}; original presentation queued.";
            return NativeEventDispatchStatus.Applied;
        }

        private void QueuePresentation(int messageId, int presentationId, string video, string audio)
        {
            IntPtr videoPointer = IntPtr.Zero;
            IntPtr audioPointer = IntPtr.Zero;
            try
            {
                // The native queue copies both ANSI strings before returning, including Vanilla's empty strings.
                videoPointer = Marshal.StringToHGlobalAnsi(video ?? string.Empty);
                audioPointer = Marshal.StringToHGlobalAnsi(audio ?? string.Empty);
                presentationHandler(presentationManager, messageId, presentationId, videoPointer, audioPointer);
            }
            finally
            {
                if (audioPointer != IntPtr.Zero)
                    Marshal.FreeHGlobal(audioPointer);
                if (videoPointer != IntPtr.Zero)
                    Marshal.FreeHGlobal(videoPointer);
            }
        }

        private TDelegate TryResolveDelegate<TDelegate>(
            IntPtr libraryHandle,
            ReadOnlySpan<byte> memory,
            string pattern,
            int referenceRva,
            bool referenceHashMatches,
            string name)
            where TDelegate : class
        {
            try
            {
                NativeResolution resolution = NativePatternResolver.ResolveUnique(
                    memory,
                    pattern,
                    referenceRva,
                    referenceHashMatches,
                    name,
                    log);
                TDelegate handler = Marshal.GetDelegateForFunctionPointer(
                    AtRva(libraryHandle, resolution.Rva),
                    typeof(TDelegate)) as TDelegate;
                if (handler == null)
                    throw new InvalidOperationException($"{name} could not be converted to {typeof(TDelegate).Name}.");
                return handler;
            }
            catch (Exception ex)
            {
                LogWarning($"Native handler unavailable: component={name}, reason={ex.Message}");
                return null;
            }
        }

        private void ResolvePresentation(
            IntPtr libraryHandle,
            ReadOnlySpan<byte> memory,
            bool referenceHashMatches)
        {
            try
            {
                NativeResolution callsite = NativePatternResolver.ResolveUnique(
                    memory,
                    PresentationCallsitePattern,
                    PresentationCallsiteRva,
                    referenceHashMatches,
                    "event presentation callsite",
                    log);
                int managerRva = NativePatternResolver.ResolveRelativeTarget(
                    memory,
                    callsite.Rva + 13,
                    callsite.Rva + 17);
                int handlerRva = NativePatternResolver.ResolveRelativeTarget(
                    memory,
                    callsite.Rva + 18,
                    callsite.Rva + 22);
                if (!NativePatternResolver.MatchesPatternAt(memory, handlerRva, PresentationHandlerPattern))
                    throw new InvalidOperationException($"presentation handler RVA 0x{handlerRva:X} failed semantic validation.");
                if (managerRva <= 0 || managerRva >= memory.Length)
                    throw new InvalidOperationException($"presentation manager RVA 0x{managerRva:X} is outside the module image.");
                if (referenceHashMatches &&
                    (managerRva != PresentationManagerRva || handlerRva != PresentationHandlerRva))
                {
                    throw new InvalidOperationException(
                        $"reference presentation targets differ: manager=0x{managerRva:X}, handler=0x{handlerRva:X}.");
                }

                presentationHandler = Marshal.GetDelegateForFunctionPointer<PresentationDelegate>(AtRva(libraryHandle, handlerRva));
                presentationManager = AtRva(libraryHandle, managerRva);
            }
            catch (Exception ex)
            {
                presentationHandler = null;
                presentationManager = IntPtr.Zero;
                LogWarning($"Native handler unavailable: component=event presentation, reason={ex.Message}");
            }
        }

        private bool TryGetEventAvailability(RandomEventKind kind, out string reason)
        {
            if (presentationHandler == null || presentationManager == IntPtr.Zero)
            {
                reason = "native Vanilla presentation queue is unavailable.";
                return false;
            }

            bool available;
            switch (kind)
            {
                case RandomEventKind.WheatInfestation:
                    available = hasBuilding != null && wheatHandler != null;
                    break;
                case RandomEventKind.HopsBeetles:
                    available = hasBuilding != null && hopsHandler != null;
                    break;
                case RandomEventKind.AppleBlight:
                    available = hasBuilding != null && appleHandler != null;
                    break;
                case RandomEventKind.MadCows:
                    available = hasBuilding != null && madCowUnitHandler != null && madCowBuildingHandler != null;
                    break;
                case RandomEventKind.GranaryTheft:
                    available = granaryTheftHandler != null;
                    break;
                default:
                    reason = $"event kind {kind} has no native event handler.";
                    return false;
            }

            reason = available
                ? string.Empty
                : $"one or more validated native handlers for {kind} are unavailable.";
            return available;
        }

        private void Reset()
        {
            presentationManager = IntPtr.Zero;
            hasBuilding = null;
            wheatHandler = null;
            hopsHandler = null;
            appleHandler = null;
            madCowUnitHandler = null;
            madCowBuildingHandler = null;
            granaryTheftHandler = null;
            presentationHandler = null;
        }

        private static IntPtr AtRva(IntPtr libraryHandle, int rva) =>
            new IntPtr(checked(libraryHandle.ToInt64() + rva));

        private void LogWarning(string message) => Shared.DebugLogHelper.LogWarning(log, message);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int HasBuildingDelegate(IntPtr buildingManager, int playerId, int buildingType);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void BuildingEventDelegate(IntPtr buildingManager, int playerId);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void UnitEventDelegate(IntPtr unitManager, int playerId);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void GranaryTheftDelegate(IntPtr buildingManager, int playerId, int percentage);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void PresentationDelegate(
            IntPtr messageManager,
            int messageId,
            int presentationId,
            IntPtr video,
            IntPtr audio);

    }
}
