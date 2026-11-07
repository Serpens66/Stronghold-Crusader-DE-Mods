using APIShared.NativeSites;
using BepInEx.Logging;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API.LowLevel;
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace BugfixesAndQoL.VanillaPerformance
{
    // V14: exact replacement of Vanilla's tribe member lookup 0x119F90 (TribeMemberLookupCode). With AI
    // groups of ~600 members every group order spends tens of ms in it (about 20 % of the 255 ms ticks on
    // test_stresstest5, 02.11.2026), Vanilla's own loops in 0x11B520 and the mods' member captures alike.
    // The answers are identical, so simulation and multiplayer are unaffected. Installed once from
    // LibraryLoaded as a permanent inline hook at the function entry; the setting only switches the
    // native enabled flag, and a disabled stub runs the original body.
    internal static unsafe class TribeMemberLookupOptimization
    {
        private const string SiteName = "TribeMemberLookup";
        private static readonly HookHandle<X64InlineHook> hook = new HookHandle<X64InlineHook>();
        private static HookTransaction transaction;
        private static IntPtr enabledFlag;
        private static int installed, published;

        private static bool BodyMatches(ReadOnlySpan<byte> memory, int rva, int length, string sha256)
        {
            if (rva < 0 || length <= 0 || rva > memory.Length - length) return false;
            using (var sha = System.Security.Cryptography.SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(memory.Slice(rva, length).ToArray())).Replace("-", "") == sha256;
        }

        internal static void Install(ManualLogSource log, CrusaderLibraryLoadContext context)
        {
            if (Interlocked.Exchange(ref installed, 1) != 0) return;
            bool claimed = false;
            try
            {
                if (context == null || context.ModuleHandle == IntPtr.Zero || !Shared.DebugLogHelper.IsCurrentNativeLibraryVersion())
                    throw new InvalidOperationException("the tribe member lookup is bound to the audited native DLL " +
                        Shared.DebugLogHelper.CurrentNativeSha256 + ".");
                if (!BodyMatches(context.Memory, TribeMemberLookupCode.Rva,
                        TribeMemberLookupCode.BodyLength, TribeMemberLookupCode.BodySha256))
                    throw new InvalidOperationException("the 0x119F90 body differs from the audited 0x8C bytes.");
                ulong target = unchecked((ulong)context.ModuleHandle.ToInt64()) + TribeMemberLookupCode.Rva;
                // The live entry must still be the image's bytes: no other hook may own the span.
                byte* live = (byte*)target;
                for (int i = 0; i < TribeMemberLookupCode.DisplacedLength; i++)
                    if (live[i] != TribeMemberLookupCode.DisplacedBytes[i])
                        throw new InvalidOperationException("the 0x119F90 entry is already patched.");
                var claim = NativeSiteRegistry.Register(BugfixesAndQoLPlugin.PluginGuid, SiteName, new[]
                    { new NativeSiteSpan(TribeMemberLookupCode.Rva, TribeMemberLookupCode.DisplacedLength, NativeSiteMode.Exclusive) });
                if (!claim.Succeeded) throw new InvalidOperationException(claim.Reason);
                claimed = true;
                enabledFlag = Marshal.AllocHGlobal(sizeof(int));
                Marshal.WriteInt32(enabledFlag, 0);
                ulong flag = unchecked((ulong)enabledFlag.ToInt64());
                HookTransaction pending = BugfixesHookInfrastructure.CreateOwnedTransaction(context.Region);
                try
                {
                    pending.AddInline(hook, HookTarget.FromAddress(target),
                        (a, original, ret) => TribeMemberLookupCode.Emit(a, flag, original),
                        hookSize: TribeMemberLookupCode.DisplacedLength, name: "TribeMemberLookup");
                    CommitResult result = pending.Commit();
                    if (!result.IsCompleteSuccess || !hook.Success || !hook.Hook.IsInstalled ||
                        hook.Hook.TargetAddress != target || hook.Hook.DisplacedByteCount != TribeMemberLookupCode.DisplacedLength)
                        throw new InvalidOperationException("the 0x119F90 hook did not install as audited: " + result);
                    if (live[0] != 0xFF || live[1] != 0x25 || *(uint*)(live + 2) != 0 ||
                        *(ulong*)(live + 6) != unchecked((ulong)hook.Hook.StubAddress.ToInt64()))
                        throw new InvalidOperationException("the installed 0x119F90 jump differs.");
                    for (int i = 14; i < TribeMemberLookupCode.DisplacedLength; i++)
                        if (live[i] != 0x90) throw new InvalidOperationException("the installed 0x119F90 padding differs.");
                    transaction = pending;
                    pending = null;
                }
                finally { pending?.Dispose(); } // Only a failed, unpublished candidate is rolled back.
                Volatile.Write(ref published, 1);
                Shared.DebugLogHelper.LogInfo(log, "VANILLA_PERF_TRIBE_MEMBER_LOOKUP_INSTALLED: inline 0x119F90 (18 bytes, method=reference-rva); " +
                    "exact member lookup, enabled with the Vanilla performance setting.");
            }
            catch (Exception exception)
            {
                if (Volatile.Read(ref published) == 0)
                {
                    if (enabledFlag != IntPtr.Zero && transaction == null) { Marshal.FreeHGlobal(enabledFlag); enabledFlag = IntPtr.Zero; }
                    if (claimed) NativeSiteRegistry.Unregister(BugfixesAndQoLPlugin.PluginGuid, SiteName);
                }
                Shared.DebugLogHelper.LogError(log, "VANILLA_PERF_TRIBE_MEMBER_LOOKUP_UNAVAILABLE: Vanilla lookup stays active: " + exception.Message);
            }
        }

        // The flag is read by every call on the simulation thread; an aligned dword write switches it.
        internal static void SetEnabled(bool value)
        {
            if (Volatile.Read(ref published) == 0) return;
            Thread.MemoryBarrier();
            Marshal.WriteInt32(enabledFlag, value ? 1 : 0);
            Thread.MemoryBarrier();
        }
    }
}
