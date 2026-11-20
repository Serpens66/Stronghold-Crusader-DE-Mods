using System;
using System.Runtime.InteropServices;
using System.Threading;
using APIShared.NativeSites;
using BepInEx.Logging;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API.LowLevel;

namespace BugfixesAndQoL.Multiplayer
{
    internal static unsafe class MoatLandRotationPatch
    {
        private const string Feature = "Multiplayer moat land rotation";
        private static HookTransaction retainedTransaction;
        private static IntPtr enabledFlag;
        private static Func<bool> checkIntegrity;
        private static bool attempted;

        internal static bool IsReady => Volatile.Read(ref checkIntegrity)?.Invoke() == true;

        internal static bool Install(CrusaderLibraryLoadContext context, bool referenceHashMatches, ManualLogSource log)
        {
            if (attempted) return IsReady;
            attempted = true;

            HookTransaction candidate = null;
            IntPtr flag = IntPtr.Zero;
            bool claimed = false, publicationStarted = false;
            try
            {
                if (context == null || context.ModuleHandle == IntPtr.Zero || !referenceHashMatches)
                    throw new InvalidOperationException("The native image does not match the audited FBCB9319 layout.");
                MoatLandRotationNative.Validate(context.Memory);

                ulong address = unchecked((ulong)context.ModuleHandle.ToInt64()) + MoatLandRotationNative.Rva;
                Shared.NativePatchGuard.RequireUnpatched(address, MoatLandRotationNative.Length, Feature, Feature);
                var claim = NativeSiteRegistry.Register(BugfixesAndQoLPlugin.PluginGuid, Feature,
                    MoatLandRotationNative.Rva, MoatLandRotationNative.Length, NativeSiteMode.Exclusive);
                if (!claim.Succeeded) throw new InvalidOperationException(claim.Reason);
                claimed = true;

                flag = Marshal.AllocHGlobal(sizeof(int));
                Marshal.WriteInt32(flag, 0);
                ulong flagAddress = unchecked((ulong)flag.ToInt64());
                candidate = BugfixesHookInfrastructure.CreateOwnedTransaction(context.Region, installOnCommit: false);
                var handle = new HookHandle<X64InlineHook>();
                candidate.AddInline(handle, HookTarget.FromAddress(address),
                    (assembler, original, _) => MoatLandRotationNative.Emit(assembler, flagAddress, original),
                    hookSize: MoatLandRotationNative.Length, name: Feature);
                var result = candidate.Commit();
                var hook = handle.Hook;
                if (!result.IsCompleteSuccess || !handle.Success || hook == null || hook.IsInstalled ||
                    hook.TargetAddress != address || hook.DisplacedByteCount != MoatLandRotationNative.Length ||
                    hook.StubAddress == IntPtr.Zero)
                    throw new InvalidOperationException("Prepared RedBird span differs from the audited inline hook.");

                // Once Enable has started, retain the gate and stub even if the backend throws after writing.
                Shared.NativePatchGuard.RequireUnpatched(hook, Feature);
                publicationStarted = true;
                retainedTransaction = candidate;
                hook.Enable();

                var pointer = new IntPtr(unchecked((long)address));
                var patch = new byte[MoatLandRotationNative.Length];
                Marshal.Copy(pointer, patch, 0, patch.Length);
                if (!hook.IsInstalled || patch[0] != 0xFF || patch[1] != 0x25 ||
                    BitConverter.ToInt32(patch, 2) != 0 ||
                    BitConverter.ToInt64(patch, 6) != hook.StubAddress.ToInt64() || patch[14] != 0x90)
                    throw new InvalidOperationException("Published RedBird jump does not match the audited inline hook.");
                Shared.NativePatchGuard.Watch(hook, Feature);
                if (!new ReadOnlySpan<byte>((void*)pointer, patch.Length).SequenceEqual(patch))
                    throw new InvalidOperationException("Published RedBird jump changed before integrity tracking began.");
                var verify = Shared.NativePatchGuard.CaptureIntegrity(new[] { hook }, Feature);
                enabledFlag = flag;
                Volatile.Write(ref checkIntegrity, verify);
                Shared.DebugLogHelper.LogInfo(log,
                    "MP_MOAT_LAND_READY method=reference-rva rva=0x7C8EE end=0x7C8FD instructions=5,3,5,2 next=inc_r12");
                return true;
            }
            catch (Exception error)
            {
                Shared.DebugLogHelper.LogError(log, "MP_MOAT_LAND_UNAVAILABLE " + error.Message);
                if (!publicationStarted)
                {
                    candidate?.Dispose();
                    if (flag != IntPtr.Zero) Marshal.FreeHGlobal(flag);
                    if (claimed) NativeSiteRegistry.Unregister(BugfixesAndQoLPlugin.PluginGuid, Feature);
                }
                return false;
            }
        }

        // The caller freezes the agreed MP capability before native initialization.
        internal static void SetEnabled(bool enabled)
        {
            if (enabledFlag == IntPtr.Zero) return;
            Volatile.Write(ref *(int*)enabledFlag, enabled ? 1 : 0);
        }
    }
}
