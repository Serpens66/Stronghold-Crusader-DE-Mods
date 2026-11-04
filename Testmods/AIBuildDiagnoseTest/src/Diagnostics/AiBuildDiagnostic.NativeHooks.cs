using BepInEx.Logging;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Hooks.Transaction;
using RedBird.Backends.NativeX64;
using Iced.Intel;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace AIBuildDiagnoseTest
{
    internal static partial class AiBuildDiagnostic
    {
        private sealed class SchedulerService
        {
            private readonly DetourHandle<SchedulerDelegate> hook = new DetourHandle<SchedulerDelegate>();
            private HookTransaction transaction;

            internal void Install(long baseAddress, ScanRegion scanRegion)
            {
                var pending = new HookTransaction(scanRegion,
                    SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                    new HookTransactionOptions
                    {
                        FailureMode = TransactionFailureMode.RollbackAndThrow,
                        OwnsHooks = false
                    });
                try
                {
                    pending.AddDetour(hook, HookTarget.FromAddress(unchecked((ulong)baseAddress) + SchedulerRva), Invoke);
                    CommitResult result = pending.Commit();
                    if (!result.IsCompleteSuccess || !hook.Success)
                        throw new InvalidOperationException("Scheduler diagnostic hook did not commit completely.");
                    transaction = pending;
                    ValidateDetour(hook.Hook as NativeDetour<SchedulerDelegate>,
                        unchecked((ulong)baseAddress) + SchedulerRva);
                }
                catch
                {
                    if (!hook.Success) pending.Dispose(); // Unpublished candidate only.
                    throw;
                }
            }

            private void Invoke(ulong state, int playerId)
            {
                if (playerId >= 1 && playerId <= 8)
                {
                    try { PublishScheduler("scheduler-before", playerId); }
                    catch (Exception ex) { Shared.DebugLogHelper.LogError(log, "AI scheduler pre-observation failed: " + ex); }
                }
                try { hook.Original(state, playerId); }
                finally
                {
                    if (playerId >= 1 && playerId <= 8)
                    {
                        try { PublishScheduler("scheduler-after", playerId); }
                        catch (Exception ex) { Shared.DebugLogHelper.LogError(log, "AI scheduler post-observation failed: " + ex); }
                    }
                }
            }

            private static void PublishScheduler(string stage, int playerId)
            {
                long offset = checked(moduleBase + (long)playerId * PlayerStride);
                int village = Marshal.ReadInt32(new IntPtr(offset + VillageSlotRva));
                int delay = Marshal.ReadInt32(new IntPtr(offset + SchedulerDelayRva));
                int phase = Marshal.ReadInt32(new IntPtr(offset + EconomyPhaseRva));
                Publish(stage, playerId, village, delay, phase);
                // The first four values mirror the operands read by Vanilla's farm branch.
                int profile = Marshal.ReadInt32(new IntPtr(offset + FarmProfileRva));
                int choiceIndex = Marshal.ReadInt32(new IntPtr(offset + FarmChoiceIndexRva));
                int selectedType = -1;
                int profileGoal = -1, profileMinimum = -1;
                if (profile > 0 && profile <= 64 && choiceIndex >= 0 && choiceIndex < 8)
                {
                    long profileBase = checked(moduleBase + AicProfileRootRva +
                        (long)(profile - 1) * 0x5E4);
                    selectedType = Marshal.ReadInt32(new IntPtr(profileBase + 0x2C + choiceIndex * 4));
                    profileGoal = Marshal.ReadInt32(new IntPtr(profileBase + 0x70));
                    profileMinimum = Marshal.ReadInt32(new IntPtr(profileBase + 0x4C));
                }
                string suffix = stage == "scheduler-before" ? "before" : "after";
                Publish("farm-scheduler-" + suffix, playerId,
                    profile, choiceIndex, selectedType, phase);
                Publish("farm-limits-" + suffix, playerId,
                    Marshal.ReadInt32(new IntPtr(offset + FarmCountRva)),
                    Marshal.ReadInt16(new IntPtr(offset + FarmLimitRva)),
                    Marshal.ReadInt16(new IntPtr(offset + FarmCooldownRva)),
                    ((long)profileGoal << 32) | (uint)profileMinimum);
            }
        }

        private sealed class RouteService
        {
            private readonly DetourHandle<RouteDelegate> hook = new DetourHandle<RouteDelegate>();
            private HookTransaction transaction;

            internal void Install(long baseAddress, ScanRegion scanRegion)
            {
                var pending = new HookTransaction(scanRegion,
                    SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                    new HookTransactionOptions
                    {
                        FailureMode = TransactionFailureMode.RollbackAndThrow,
                        OwnsHooks = false
                    });
                try
                {
                    ulong target = unchecked((ulong)baseAddress) + RouteRva;
                    pending.AddDetour(hook, HookTarget.FromAddress(target), Invoke);
                    CommitResult result = pending.Commit();
                    if (!result.IsCompleteSuccess || !hook.Success)
                        throw new InvalidOperationException("Route diagnostic hook did not commit completely.");
                    transaction = pending;
                    ValidateRouteDetour(hook.Hook as NativeDetour<RouteDelegate>, target);
                }
                catch
                {
                    if (!hook.Success) pending.Dispose(); // Unpublished candidate only.
                    throw;
                }
            }

            private int Invoke(ulong manager, int playerId, int mapperIndex, int tileX, int tileY)
            {
                bool observe = mapperIndex == 3 &&
                    TryGetCurrentWoodAttempt(out long id, out int owner) && owner == playerId && HasObserver;
                AiRouteEvidence evidence = null;
                if (observe && AiBuildExperimentBridge.HasDetails)
                {
                    try { evidence = AiBuildExperimentBridge.CaptureRoute(playerId, mapperIndex, tileX, tileY); }
                    catch (Exception ex)
                    {
                        Shared.DebugLogHelper.LogError(log, "AI route evidence capture failed: " + ex);
                        evidence = null;
                    }
                }
                int result = hook.Original(manager, playerId, mapperIndex, tileX, tileY);
                // Only the diagnostic observer receives the small result for other AI site routes.
                // The full connection snapshot remains limited to the attributed wood attempt.
                if (!observe && HasObserver && playerId >= 1 && playerId <= 8 &&
                    (mapperIndex == 4 || mapperIndex == 5 || mapperIndex == 0x14))
                    Publish("site-route-result", playerId, result, mapperIndex, tileX, tileY);
                if (observe)
                {
                    try
                    {
                        if (evidence != null) PublishRouteEvidence(playerId, evidence);
                        Publish("route-result", playerId, result, mapperIndex, tileX, tileY);
                    }
                    catch (Exception ex) { Shared.DebugLogHelper.LogError(log, "AI route result observation failed: " + ex); }
                }
                return result;
            }

        }

        private static void ValidateEntry(byte[] bytes, int rva, int displaced)
        {
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes));
            decoder.IP = 0x180000000UL + (uint)rva;
            int length = 0;
            while (length < 6)
            {
                Instruction instruction = decoder.Decode();
                if (decoder.LastError != DecoderError.None || instruction.IsInvalid ||
                    instruction.FlowControl != FlowControl.Next)
                    throw new InvalidOperationException("Diagnostic detour entry is not a straight-line prologue.");
                length += instruction.Length;
            }
            if (length != displaced)
                throw new InvalidOperationException("Diagnostic indirect displacement differs from audit.");
        }

        private static void ValidateRouteLayout()
        {
            if (Marshal.SizeOf(typeof(PathConnectionRecord)) != 0x204 ||
                Marshal.OffsetOf(typeof(PathConnectionRecord), nameof(PathConnectionRecord.r_IsActive)).ToInt32() != 0 ||
                Marshal.OffsetOf(typeof(PathConnectionRecord), nameof(PathConnectionRecord.r_ConnectionClass)).ToInt32() != 4 ||
                Marshal.OffsetOf(typeof(PathConnectionRecord), nameof(PathConnectionRecord.r_BuildingId)).ToInt32() != 0x0C ||
                Marshal.OffsetOf(typeof(PathConnectionRecord), nameof(PathConnectionRecord.r_IsEnabledOrOpen)).ToInt32() != 0x18 ||
                Marshal.OffsetOf(typeof(PathConnectionRecord), nameof(PathConnectionRecord.r_PathComponentA)).ToInt32() != 0x34 ||
                Marshal.OffsetOf(typeof(PathConnectionRecord), nameof(PathConnectionRecord.r_PathComponentB)).ToInt32() != 0x38 ||
                Marshal.OffsetOf(typeof(PathConnectionRecord), nameof(PathConnectionRecord.r_OwnerOrAccessPlayerId)).ToInt32() != 0x1E4 ||
                Marshal.OffsetOf(typeof(PathConnectionRecord), nameof(PathConnectionRecord.r_PathComponentC)).ToInt32() != 0x1E8)
                throw new InvalidOperationException("Path connection layout differs from the audited native record.");
        }

        private static void ProbeRouteBackend(ulong entry)
        {
            IntPtr copy = AllocateRouteProbeNear(entry);
            NativeDetour<RouteDelegate> probe = null;
            try
            {
                byte[] bytes = new byte[64];
                Marshal.Copy(unchecked((IntPtr)(long)entry), bytes, 0, bytes.Length);
                Marshal.Copy(bytes, 0, copy, bytes.Length);
                RouteDelegate noop = (manager, playerId, mapperIndex, tileX, tileY) => 1;
                var request = new DetourRequest<RouteDelegate>
                {
                    Name = "AIBuildDiagnoseTest copied route entry",
                    TargetAddress = unchecked((ulong)copy.ToInt64()),
                    Callback = noop
                };
                probe = NativeDetourBackend.Instance.CreateDetour(in request) as NativeDetour<RouteDelegate>;
                if (probe == null || probe.Scheme.ToString() != "Indirect" ||
                    probe.DisplacedByteCount != RouteDisplacedBytes || probe.IsInstalled ||
                    probe.TargetAddress != unchecked((ulong)copy.ToInt64()) ||
                    probe.PointerSlot == IntPtr.Zero || probe.HookEntryPointAddress == IntPtr.Zero)
                    throw new InvalidOperationException("Installed RedBird backend rejects the route entry contract.");
                probe.Enable();
                ValidateRouteDetour(probe, unchecked((ulong)copy.ToInt64()));
                GC.KeepAlive(noop);
            }
            finally
            {
                try { probe?.Dispose(); }
                finally
                {
                    if (!VirtualFree(copy, UIntPtr.Zero, 0x8000))
                        Shared.DebugLogHelper.LogError(log, "AI route probe scratch release failed: " + Marshal.GetLastWin32Error());
                }
            }
        }

        private static IntPtr AllocateRouteProbeNear(ulong entry)
        {
            // The installed NativeX64 indirect backend reserves a trampoline within
            // +/-2 GiB of its target. A heap copy may sit outside usable address space.
            const long stride = 0x1000000;
            const long granularity = 0x10000;
            long aligned = ((long)entry + granularity - 1) & ~(granularity - 1);
            for (int distance = 8; distance <= 96; distance++)
            {
                foreach (int direction in new[] { 1, -1 })
                {
                    long candidate;
                    try { candidate = checked(aligned + direction * distance * stride); }
                    catch (OverflowException) { continue; }
                    if (candidate <= 0) continue;
                    IntPtr allocation = VirtualAlloc(new IntPtr(candidate), new UIntPtr(0x10000),
                        0x3000, 0x40);
                    if (allocation != IntPtr.Zero &&
                        Math.Abs(allocation.ToInt64() - (long)entry) < 0x70000000)
                        return allocation;
                    if (allocation != IntPtr.Zero)
                        VirtualFree(allocation, UIntPtr.Zero, 0x8000);
                }
            }
            throw new InvalidOperationException("No near-module scratch buffer is available for the route backend probe.");
        }

        private static void ValidateRouteDetour(NativeDetour<RouteDelegate> detour, ulong target)
        {
            if (detour == null || !detour.IsInstalled || detour.TargetAddress != target ||
                detour.Scheme.ToString() != "Indirect" ||
                detour.DisplacedByteCount != RouteDisplacedBytes ||
                detour.PointerSlot == IntPtr.Zero || detour.HookEntryPointAddress == IntPtr.Zero)
                throw new InvalidOperationException("Committed route detour differs from the indirect backend contract.");
            byte[] patch = new byte[6];
            Marshal.Copy(unchecked((IntPtr)(long)target), patch, 0, patch.Length);
            if (patch[0] != 0xFF || patch[1] != 0x25 ||
                checked((long)target + 6 + BitConverter.ToInt32(patch, 2)) != detour.PointerSlot.ToInt64() ||
                Marshal.ReadInt64(detour.PointerSlot) != detour.HookEntryPointAddress.ToInt64())
                throw new InvalidOperationException("Committed route pointer slot or hook entry differs.");
        }

        private static void ProbeBackend(ulong entry, Action<AiBuildDiagnosticRecord> callback)
        {
            IntPtr copy = AllocateRouteProbeNear(entry);
            NativeDetour<SchedulerDelegate> probe = null;
            try
            {
                byte[] bytes = new byte[64];
                Marshal.Copy(unchecked((IntPtr)(long)entry), bytes, 0, bytes.Length);
                Marshal.Copy(bytes, 0, copy, bytes.Length);
                SchedulerDelegate noop = (state, playerId) => { };
                var request = new DetourRequest<SchedulerDelegate>
                {
                    Name = "AIBuildDiagnoseTest copied scheduler entry",
                    TargetAddress = unchecked((ulong)copy.ToInt64()),
                    Callback = noop
                };
                probe = NativeDetourBackend.Instance.CreateDetour(in request) as NativeDetour<SchedulerDelegate>;
                if (probe == null || probe.Scheme.ToString() != "Indirect" ||
                    probe.DisplacedByteCount != ExpectedDisplacedBytes || probe.IsInstalled ||
                    probe.TargetAddress != unchecked((ulong)copy.ToInt64()) ||
                    probe.PointerSlot == IntPtr.Zero || probe.HookEntryPointAddress == IntPtr.Zero)
                    throw new InvalidOperationException("Installed RedBird backend rejects the scheduler entry contract.");
                probe.Enable();
                ValidateDetour(probe, unchecked((ulong)copy.ToInt64()));
                GC.KeepAlive(callback);
                GC.KeepAlive(noop);
            }
            finally
            {
                probe?.Dispose();
                if (!VirtualFree(copy, UIntPtr.Zero, 0x8000))
                    Shared.DebugLogHelper.LogError(log, "AI scheduler probe scratch release failed: " + Marshal.GetLastWin32Error());
            }
        }

        private static void ValidateDetour(NativeDetour<SchedulerDelegate> detour, ulong target)
        {
            if (detour == null || !detour.IsInstalled || detour.TargetAddress != target ||
                detour.Scheme.ToString() != "Indirect" ||
                detour.DisplacedByteCount != ExpectedDisplacedBytes ||
                detour.PointerSlot == IntPtr.Zero || detour.HookEntryPointAddress == IntPtr.Zero)
                throw new InvalidOperationException("Committed scheduler detour differs from the indirect backend contract.");
            byte[] patch = new byte[6];
            Marshal.Copy(unchecked((IntPtr)(long)target), patch, 0, patch.Length);
            if (patch[0] != 0xFF || patch[1] != 0x25 ||
                checked((long)target + 6 + BitConverter.ToInt32(patch, 2)) != detour.PointerSlot.ToInt64() ||
                Marshal.ReadInt64(detour.PointerSlot) != detour.HookEntryPointAddress.ToInt64())
                throw new InvalidOperationException("Committed scheduler pointer slot or hook entry differs.");
        }
    }
}
