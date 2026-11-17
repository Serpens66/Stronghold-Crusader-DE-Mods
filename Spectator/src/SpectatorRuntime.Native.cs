using APIShared.GameModes;
using APIShared;
using BepInEx;
using BepInEx.Logging;
using CrusaderDE;
using HarmonyLib;
using Iced.Intel;
using Noesis;
using R3;
using RedBird.Abstractions.Hooks;
using RedBird.Backends.NativeX64;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.Detours;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.Player;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using Path = System.IO.Path;
using System.Reflection;
using System.Runtime.InteropServices;
using Marshal = System.Runtime.InteropServices.Marshal;
using System.Security.Cryptography;
using System.Threading;

namespace Spectator
{
    internal static partial class SpectatorRuntime
    {
        private static volatile bool availabilityReady, visualsReady;
        private static void OnNativeLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            byte[] image;
            try
            {
                string path = Path.Combine(Paths.GameRootPath,
                    "Stronghold Crusader Definitive Edition_Data", "Plugins", "x86_64",
                    "CrusaderDE.dll");
                image = File.ReadAllBytes(path);
                using (var sha = SHA256.Create())
                {
                    string hash = BitConverter.ToString(sha.ComputeHash(image)).Replace("-", "");
                    if (!string.Equals(hash, NativeSha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Native DLL hash differs: " + hash);
                }
            }
            catch (Exception error)
            {
                if (nativeOrder == null) log.LogError("TEST_AI_ORDERS_DISABLED: " + error);
                log.LogError("TEST_NATIVE_ACTIONS_DISABLED: " + error);
                if (availabilityDetour == null)
                    log.LogError("SPECTATOR_EDITOR_BUILD_AVAILABILITY_DISABLED: " + error);
                return;
            }

            InitializeNativeOrders(context, image);
            InitializeNativeActions(context, image);
            if (availabilityDetour != null) return;
            NativeDetour<BuildAvailabilityDelegate> candidate = null;
            bool published = false;
            try
            {
                if (typeof(NativeDetour<>).Assembly.GetName().Version !=
                    new Version(1, 5, 0, 0))
                    throw new InvalidOperationException("Installed RedBird NativeX64 differs from 1.5.0.0.");
                ulong entry = unchecked((ulong)context.ModuleHandle.ToInt64()) + AvailabilityRva;
                // No live copied-entry probe: the exact RedBird pin above, the candidate checks
                // before Enable and VerifyAvailabilityPatch after it cover the backend contract;
                // tests/Test-CopiedProbe.ps1 runs the installed backend on the real entry offline.
                VerifyAvailabilityEntry(entry);
                var request = new DetourRequest<BuildAvailabilityDelegate>
                {
                    Name = "Spectator building availability",
                    TargetAddress = entry,
                    Callback = CheckBuildingAvailability
                };
                candidate = NativeDetourBackend.Instance.CreateDetour(in request)
                    as NativeDetour<BuildAvailabilityDelegate>;
                if (candidate == null || candidate.Scheme.ToString() != "Indirect" ||
                    candidate.DisplacedByteCount != AvailabilityEntry.Length ||
                    candidate.TargetAddress != entry || candidate.IsInstalled)
                    throw new InvalidOperationException("Availability detour candidate differs from audited entry.");
                nativeImageBase = unchecked((ulong)context.ModuleHandle.ToInt64());
                availabilityDetour = candidate;
                published = true; // Enable may publish before throwing; retain its root.
                candidate.Enable();
                VerifyAvailabilityPatch(candidate, entry);
                availabilityReady = true;
                candidate = null;
                published = true;
                AvailabilityObservation.ReportReady();
                log.LogInfo("SPECTATOR_EDITOR_BUILD_AVAILABILITY_READY rva=0xCC420 scheme=Indirect displaced=10");
                InstallVisualDetours(context);
            }
            catch (Exception error)
            {
                // Only a candidate that has not passed validation may be rolled back.
                if (!published)
                {
                    availabilityDetour = null;
                    nativeImageBase = 0;
                    candidate?.Dispose();
                }
                log.LogError("SPECTATOR_EDITOR_BUILD_AVAILABILITY_DISABLED: " + error);
            }
        }

        private static void VerifyAvailabilityEntry(ulong entry)
        {
            byte[] bytes = new byte[32];
            Marshal.Copy(unchecked((IntPtr)(long)entry), bytes, 0, bytes.Length);
            for (int index = 0; index < AvailabilityEntry.Length; index++)
                if (bytes[index] != AvailabilityEntry[index])
                    throw new InvalidOperationException("Native availability entry mismatch at byte " + index);
            Decoder decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes));
            int displaced = 0;
            while (displaced < AvailabilityEntry.Length)
            {
                Instruction instruction = decoder.Decode();
                if (decoder.LastError != DecoderError.None || instruction.IsInvalid ||
                    instruction.FlowControl != FlowControl.Next)
                    throw new InvalidOperationException("Availability entry contains an unexpected instruction.");
                displaced += instruction.Length;
            }
            if (displaced != AvailabilityEntry.Length)
                throw new InvalidOperationException("Availability entry crosses the audited 10-byte boundary.");
        }

        private static void VerifyAvailabilityPatch(NativeDetour<BuildAvailabilityDelegate> detour,
            ulong entry)
        {
            if (!detour.IsInstalled || detour.TargetAddress != entry ||
                detour.Scheme.ToString() != "Indirect" ||
                detour.DisplacedByteCount != AvailabilityEntry.Length ||
                detour.PointerSlot == IntPtr.Zero ||
                detour.HookEntryPointAddress == IntPtr.Zero ||
                detour.OriginalEntryPointAddress == IntPtr.Zero ||
                detour.TrampolineAddress == IntPtr.Zero || detour.ChainDepth != 1)
                throw new InvalidOperationException("Installed availability detour contract differs.");
            byte[] patch = new byte[6];
            Marshal.Copy(unchecked((IntPtr)(long)entry), patch, 0, patch.Length);
            if (patch[0] != 0xFF || patch[1] != 0x25 ||
                checked((long)entry + 6 + BitConverter.ToInt32(patch, 2)) !=
                    detour.PointerSlot.ToInt64() ||
                Marshal.ReadInt64(detour.PointerSlot) != detour.HookEntryPointAddress.ToInt64())
                throw new InvalidOperationException("Installed availability detour patch differs.");
        }

        private static int CheckBuildingAvailability(IntPtr manager, int mapper, int owner,
            int showMessage)
        {
            var finish = AvailabilityObservation.Begin(owner, mapper, showMessage);
            bool nativeEvaluated = false;
            int? result = null;
            try
            {
                int value = CheckBuildingAvailabilityCore(manager, mapper, owner, showMessage, out nativeEvaluated);
                result = value;
                return value;
            }
            finally { finish?.Invoke(result, nativeEvaluated); }
        }

        private static int CheckBuildingAvailabilityCore(IntPtr manager, int mapper, int owner,
            int showMessage, out bool nativeEvaluated)
        {
            nativeEvaluated = false;
            if (!availabilityReady)
            { nativeEvaluated = true; return availabilityDetour.Original(manager, mapper, owner, showMessage); }
            try
            {
                BuildingRequest click = buildingClickScope;
                if (click != null && IsCurrentSession(click.SessionId) &&
                    click.Owner == owner && (int)click.Mapper == mapper)
                    return 1;
                if (mapper == (int)eMappers.MAPPER_PITCH_DITCH)
                {
                    PitchRequest pitchClick = pitchClickScope;
                    if (PitchMatches(pitchClick, owner, pitchClick?.X ?? -1,
                        pitchClick?.Y ?? -1)) return 1;
                    lock (pitchLock)
                        foreach (PitchRequest pitch in pendingPitch)
                            if (PitchMatches(pitch, owner, pitch.X, pitch.Y) &&
                                PitchChoreMatches(pitch)) return 1;
                }
                BuildingRequest queued = pendingBuilding;
                if (queued != null && queued.ClickAccepted && !queued.BuildStarted &&
                    IsCurrentSession(queued.SessionId) &&
                    DateTime.UtcNow <= queued.ExpiresAtUtc &&
                    queued.Owner == owner && (int)queued.Mapper == mapper && nativeImageBase != 0 &&
                    Marshal.ReadInt32(unchecked((IntPtr)(long)(nativeImageBase + 0x85F8FF4))) == 10 &&
                    Marshal.ReadInt32(unchecked((IntPtr)(long)(nativeImageBase + 0x85F8FEC))) == 0 &&
                    Marshal.ReadInt32(unchecked((IntPtr)(long)(nativeImageBase + 0x86C132C))) ==
                        queued.X - queued.Scale / 2 &&
                    Marshal.ReadInt32(unchecked((IntPtr)(long)(nativeImageBase + 0x86C1330))) ==
                        queued.Y - queued.Scale / 2 &&
                    Marshal.ReadInt32(unchecked((IntPtr)(long)(nativeImageBase + 0x86C1334))) == mapper &&
                    Marshal.ReadInt32(unchecked((IntPtr)(long)(nativeImageBase + 0x86C1338))) == queued.Scale)
                    return 1;
            }
            catch (Exception error)
            {
                if (buildingDiagnostics++ < 8)
                    log?.LogError("SPECTATOR_EDITOR_BUILD_AVAILABILITY_ERROR: " + error);
            }
            nativeEvaluated = true;
            return availabilityDetour.Original(manager, mapper, owner, showMessage);
        }

        private static void InstallVisualDetours(CrusaderLibraryLoadContext context)
        {
            if (visualMapDetour != null) return;
            NativeDetour<VisualMapDelegate> map = null;
            NativeDetour<VisualResourceDelegate> resource = null;
            bool published = false;
            try
            {
                ulong image = unchecked((ulong)context.ModuleHandle.ToInt64());
                map = CreateVisualDetour("map", image + VisualMapRva,
                    VisualMapEntry, (VisualMapDelegate)UpdateVisualMap);
                if (!APIShared.Presentation.VisibleMapRenderEvents.TryRegister(SpectatorPlugin.PluginGuid,
                    "placement-visibility", args => EnterVisualSpectator(args),
                    args => { if (args.State is int previous) LeaveVisualSpectator(previous); },
                    out visualFrameRegistration, out string reason, order: 20))
                    throw new InvalidOperationException(reason);
                resource = CreateVisualDetour("resource", image + VisualResourceRva,
                    VisualResourceEntry, (VisualResourceDelegate)UpdateVisualResource);
                visualMapDetour = map;
                visualResourceDetour = resource;
                published = true; // Retain all roots once any live publication is attempted.
                map.Enable();
                resource.Enable();
                VerifyVisualPatch(map, image + VisualMapRva, VisualMapEntry.Length);
                VerifyVisualPatch(resource, image + VisualResourceRva, VisualResourceEntry.Length);
                visualsReady = true;
                log.LogInfo("SPECTATOR_EDITOR_VISUAL_READY map=0x65830 frame=0x41D60 resource=0x6E620");
            }
            catch (Exception error)
            {
                visualsReady = false;
                SetRevealHidden(0);
                if (!published)
                {
                    SetRevealHidden(0);
                    visualResourceDetour = null;
                    visualMapDetour = null;
                    resource?.Dispose();
                    map?.Dispose();
                }
                log.LogError("SPECTATOR_EDITOR_VISUAL_DISABLED: " + error);
            }
        }

        private static NativeDetour<T> CreateVisualDetour<T>(string name, ulong entry,
            byte[] expected, T callback) where T : Delegate
        {
            VerifyVisualEntry(entry, expected);
            var request = new DetourRequest<T> {
                Name = "Spectator visual " + name,
                TargetAddress = entry, Callback = callback
            };
            var candidate = NativeDetourBackend.Instance.CreateDetour(in request) as NativeDetour<T>;
            if (candidate == null || candidate.Scheme.ToString() != "Indirect" ||
                candidate.DisplacedByteCount != expected.Length ||
                candidate.TargetAddress != entry || candidate.IsInstalled)
            {
                candidate?.Dispose();
                throw new InvalidOperationException("Visual detour candidate differs: " + name);
            }
            return candidate;
        }

        private static void VerifyVisualEntry(ulong entry, byte[] expected)
        {
            var bytes = new byte[32];
            Marshal.Copy(unchecked((IntPtr)(long)entry), bytes, 0, bytes.Length);
            for (int i = 0; i < expected.Length; i++)
                if (bytes[i] != expected[i])
                    throw new InvalidOperationException("Visual native entry mismatch at " + i);
            Decoder decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes));
            int displaced = 0;
            while (displaced < expected.Length)
            {
                Instruction instruction = decoder.Decode();
                if (decoder.LastError != DecoderError.None || instruction.IsInvalid ||
                    instruction.FlowControl != FlowControl.Next)
                    throw new InvalidOperationException("Visual entry has an unexpected instruction.");
                displaced += instruction.Length;
            }
            if (displaced != expected.Length)
                throw new InvalidOperationException("Visual entry crosses audited instructions.");
        }

        private static void VerifyVisualPatch<T>(NativeDetour<T> detour, ulong entry,
            int displaced) where T : Delegate
        {
            if (!detour.IsInstalled || detour.TargetAddress != entry ||
                detour.Scheme.ToString() != "Indirect" ||
                detour.DisplacedByteCount != displaced ||
                detour.PointerSlot == IntPtr.Zero || detour.HookEntryPointAddress == IntPtr.Zero ||
                detour.OriginalEntryPointAddress == IntPtr.Zero ||
                detour.TrampolineAddress == IntPtr.Zero || detour.ChainDepth != 1)
                throw new InvalidOperationException("Visual detour backend contract differs.");
            var patch = new byte[6];
            Marshal.Copy(unchecked((IntPtr)(long)entry), patch, 0, patch.Length);
            if (patch[0] != 0xFF || patch[1] != 0x25 ||
                checked((long)entry + 6 + BitConverter.ToInt32(patch, 2)) !=
                    detour.PointerSlot.ToInt64() ||
                Marshal.ReadInt64(detour.PointerSlot) != detour.HookEntryPointAddress.ToInt64())
                throw new InvalidOperationException("Visual detour patch form differs.");
        }

        private static void SetRevealHidden(int active)
        {
            Interlocked.Exchange(ref revealHiddenActive, active);
            visualFrameRegistration?.SetEnabled(active != 0 && visualsReady);
        }

        private static unsafe int EnterVisualSpectator(APIShared.Presentation.VisibleMapRenderEventArgs scope = null)
        {
            if (!visualsReady || Volatile.Read(ref revealHiddenActive) == 0 || nativeImageBase == 0) return -1;
            int* flag = (int*)(nativeImageBase + SpectatorFlagRva);
            int previous = *flag;
            // Store (and box) rollback state before changing native memory.
            if (scope != null) scope.State = previous;
            *flag = 1;
            return previous;
        }

        private static unsafe void LeaveVisualSpectator(int previous)
        {
            if (previous >= 0 && nativeImageBase != 0)
                *(int*)(nativeImageBase + SpectatorFlagRva) = previous;
        }

        private static void UpdateVisualMap(IntPtr tileManager)
        {
            int previous = EnterVisualSpectator();
            try
            {
                if (previous >= 0 && Interlocked.Exchange(ref visualMapLogged, 1) == 0)
                    log.LogInfo("SPECTATOR_EDITOR_VISUAL_ACTIVE source=map");
                visualMapDetour.Original(tileManager);
            }
            finally { LeaveVisualSpectator(previous); }
        }

        private static byte UpdateVisualResource(IntPtr tileManager, int buildingId)
        {
            int previous = EnterVisualSpectator();
            try
            {
                if (previous >= 0 && Interlocked.Exchange(ref visualResourceLogged, 1) == 0)
                    log.LogInfo("SPECTATOR_EDITOR_VISUAL_ACTIVE source=resource");
                return visualResourceDetour.Original(tileManager, buildingId);
            }
            finally { LeaveVisualSpectator(previous); }
        }

    }
}
