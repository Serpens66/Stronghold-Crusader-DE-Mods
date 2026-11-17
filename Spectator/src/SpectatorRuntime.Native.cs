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

            // Every Spectator hook checks its span against this load snapshot (foreign patches).
            Shared.NativePatchGuard.Bind(context, RawLog);
            InitializeNativeOrders(context, image);
            InitializeNativeActions(context, image);
            if (availabilityDetour != null) return;
            NativeDetour<BuildAvailabilityDelegate> candidate = null;
            bool published = false;
            try
            {
                ulong entry = unchecked((ulong)context.ModuleHandle.ToInt64()) + AvailabilityRva;
                // No live copied-entry probe and no runtime RedBird pin: the candidate and its trampoline
                // are checked before Enable and the patch after it; tests/Test-CopiedProbe.ps1 runs the
                // installed backend on the real entries offline and gates the audited RedBird version.
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
                VerifyTrampoline(candidate.TrampolineAddress, candidate.TrampolineSize, entry, AvailabilityEntry);
                Shared.NativePatchGuard.RequireUnpatched(candidate, "Spectator building availability");
                nativeImageBase = unchecked((ulong)context.ModuleHandle.ToInt64());
                availabilityDetour = candidate;
                published = true; // Enable may publish before throwing; retain its root.
                candidate.Enable();
                VerifyAvailabilityPatch(candidate, entry);
                Shared.NativePatchGuard.Watch(candidate, "Spectator building availability");
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
                Shared.NativePatchGuard.RequireUnpatched(map, "Spectator visual map");
                Shared.NativePatchGuard.RequireUnpatched(resource, "Spectator visual resource");
                visualMapDetour = map;
                visualResourceDetour = resource;
                published = true; // Retain all roots once any live publication is attempted.
                map.Enable();
                resource.Enable();
                VerifyVisualPatch(map, image + VisualMapRva, VisualMapEntry.Length);
                VerifyVisualPatch(resource, image + VisualResourceRva, VisualResourceEntry.Length);
                Shared.NativePatchGuard.Watch(map, "Spectator visual map");
                Shared.NativePatchGuard.Watch(resource, "Spectator visual resource");
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
            try
            {
                if (candidate == null || candidate.Scheme.ToString() != "Indirect" ||
                    candidate.DisplacedByteCount != expected.Length ||
                    candidate.TargetAddress != entry || candidate.IsInstalled)
                    throw new InvalidOperationException("Visual detour candidate differs: " + name);
                VerifyTrampoline(candidate.TrampolineAddress, candidate.TrampolineSize, entry, expected);
            }
            catch
            {
                candidate?.Dispose(); // Unpublished candidate only.
                throw;
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

        // The relocated prologue must replay the audited entry bytes and continue at entry + displaced
        // (checked on the unpublished candidate, so no RedBird version pin is needed at runtime).
        private static void VerifyTrampoline(IntPtr trampoline, int size, ulong entry, byte[] expected)
        {
            if (trampoline == IntPtr.Zero || size < expected.Length + 5)
                throw new InvalidOperationException("Detour trampoline is missing or too short.");
            var bytes = new byte[Math.Min(size, expected.Length + 16)]; // Never past the trampoline.
            Marshal.Copy(trampoline, bytes, 0, bytes.Length);
            for (int i = 0; i < expected.Length; i++)
                if (bytes[i] != expected[i])
                    throw new InvalidOperationException("Detour trampoline does not replay the audited entry.");
            ulong start = unchecked((ulong)trampoline.ToInt64());
            Decoder decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes, expected.Length,
                bytes.Length - expected.Length), start + (ulong)expected.Length);
            Instruction jump = decoder.Decode();
            ulong continuation;
            if (jump.FlowControl == FlowControl.UnconditionalBranch && jump.Op0Kind == OpKind.NearBranch64)
                continuation = jump.NearBranchTarget;
            else if (jump.FlowControl == FlowControl.IndirectBranch && jump.IsIPRelativeMemoryOperand)
                continuation = unchecked((ulong)Marshal.ReadInt64((IntPtr)(long)jump.IPRelativeMemoryAddress));
            else throw new InvalidOperationException("Detour trampoline has no continuation jump.");
            if (continuation != entry + (ulong)expected.Length)
                throw new InvalidOperationException("Detour trampoline continues at the wrong address.");
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
