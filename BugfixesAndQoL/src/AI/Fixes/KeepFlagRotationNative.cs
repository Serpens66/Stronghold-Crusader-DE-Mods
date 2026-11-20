using System;
using System.Runtime.InteropServices;
using System.Threading;
using APIShared.NativeSites;
using BepInEx.Logging;
using Iced.Intel;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API.LowLevel;
using SHCDESE.Interop;

namespace BugfixesAndQoL
{
    internal sealed unsafe partial class KeepFlagRotationNative
    {
        private const string Feature = "Multiplayer main flag rotation";
        private const int CameraRotationRva = 0x60AD40C;
        private static readonly int[] Sites = { 0x95B7A, 0x95EEE };

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void MarkMainFlagSpawn(int playerId);
        private static readonly MarkMainFlagSpawn Mark = playerId => expectedPlayer = playerId;
        [ThreadStatic]
        private static int expectedPlayer;

        private HookTransaction retainedTransaction;
        private readonly X64InlineHook[] hooks = new X64InlineHook[Sites.Length];
        private IntPtr activeFlag;
        private Func<bool> checkIntegrity;
        private bool attempted;

        internal bool IsReady => Volatile.Read(ref checkIntegrity)?.Invoke() == true;

        internal bool IsActive => activeFlag != IntPtr.Zero && Volatile.Read(ref *(int*)activeFlag) != 0;

        internal void SetActive(bool active)
        {
            expectedPlayer = 0;
            if (activeFlag != IntPtr.Zero)
                Volatile.Write(ref *(int*)activeFlag, active ? 1 : 0);
        }

        internal static bool ConsumeMainFlagSpawn(int playerId)
        {
            int expected = expectedPlayer;
            expectedPlayer = 0;
            return expected != 0 && expected == playerId;
        }

        internal void Install(CrusaderLibraryLoadContext context, bool referenceHashMatches, ManualLogSource log)
        {
            if (attempted) return;
            attempted = true;
            HookTransaction candidate = null;
            IntPtr flag = IntPtr.Zero;
            bool claimed = false;
            bool publicationStarted = false;
            try
            {
                if (context == null || context.ModuleHandle == IntPtr.Zero || !referenceHashMatches)
                    throw new InvalidOperationException("The native image does not match the audited flag layout.");
                if (Marshal.SizeOf<GameBuilding>() != 0x32C ||
                    Marshal.OffsetOf<GameBuilding>(nameof(GameBuilding.N00012042_2)).ToInt32() != 0x92)
                    throw new InvalidOperationException("The native keep orientation layout differs.");

                ulong image = unchecked((ulong)context.ModuleHandle.ToInt64());
                var spans = new NativeSiteSpan[Sites.Length];
                for (int i = 0; i < Sites.Length; i++)
                {
                    int site = Sites[i];
                    if (context.Memory.Length < site + SpanLength ||
                        !context.Memory.Slice(site, SpanLength).SequenceEqual(OriginalBytes[i]))
                        throw new InvalidOperationException($"Audited main flag span differs at 0x{site:X}.");
                    Shared.NativePatchGuard.RequireUnpatched(image + (uint)site, SpanLength, Feature, Feature);
                    spans[i] = new NativeSiteSpan(site, SpanLength, NativeSiteMode.Exclusive);
                }
                var claim = NativeSiteRegistry.Register(BugfixesAndQoLPlugin.PluginGuid, Feature, spans);
                if (!claim.Succeeded) throw new InvalidOperationException(claim.Reason);
                claimed = true;

                flag = Marshal.AllocHGlobal(sizeof(int));
                Marshal.WriteInt32(flag, 0);
                ulong stateAddress = unchecked((ulong)flag.ToInt64());
                ulong markerAddress = unchecked((ulong)Marshal.GetFunctionPointerForDelegate(Mark).ToInt64());
                candidate = BugfixesHookInfrastructure.CreateOwnedTransaction(context.Region, installOnCommit: false);
                var handles = new HookHandle<X64InlineHook>[Sites.Length];
                for (int i = 0; i < Sites.Length; i++)
                {
                    ulong site = image + (uint)Sites[i];
                    var handle = new HookHandle<X64InlineHook>();
                    handles[i] = handle;
                    candidate.AddInline(handle, HookTarget.FromAddress(site), (asm, original, continuation) =>
                    {
                        if (continuation != site + SpanLength ||
                            original.Length < 3 || original[0].Length != 6 ||
                            original[0].Mnemonic != Mnemonic.Mov || original[0].Op0Register != Register.EAX ||
                            !original[0].IsIPRelativeMemoryOperand ||
                            original[0].IPRelativeMemoryAddress != image + CameraRotationRva)
                            throw new InvalidOperationException("RedBird decoded a different main flag camera load.");
                        Emit(asm, original, stateAddress, markerAddress);
                    }, hookSize: SpanLength, name: Feature + $" 0x{Sites[i]:X}");
                }
                var result = candidate.Commit();
                if (!result.IsCompleteSuccess) throw new InvalidOperationException(result.ToString());
                for (int i = 0; i < handles.Length; i++)
                {
                    var hook = handles[i].Hook;
                    if (!handles[i].Success || hook == null || hook.IsInstalled ||
                        hook.TargetAddress != image + (uint)Sites[i] || hook.DisplacedByteCount != SpanLength ||
                        hook.StubAddress == IntPtr.Zero)
                        throw new InvalidOperationException("Prepared RedBird main flag span differs.");
                    Shared.NativePatchGuard.RequireUnpatched(hook, Feature);
                    hooks[i] = hook;
                }

                publicationStarted = true;
                retainedTransaction = candidate;
                foreach (var hook in hooks) hook.Enable();
                foreach (var hook in hooks)
                {
                    if (!HasExpectedJump(hook)) throw new InvalidOperationException("Published main flag jump differs.");
                    Shared.NativePatchGuard.Watch(hook, Feature);
                }
                foreach (var hook in hooks)
                    if (!HasExpectedJump(hook)) throw new InvalidOperationException("Published main flag jump changed before integrity tracking began.");
                var verify = Shared.NativePatchGuard.CaptureIntegrity(hooks, Feature);
                activeFlag = flag;
                Volatile.Write(ref checkIntegrity, verify);
                Shared.DebugLogHelper.LogInfo(log, "MP_MAIN_FLAG_READY sites=0x95B7A,0x95EEE span=15 referenceView=0");
            }
            catch (Exception error)
            {
                Shared.DebugLogHelper.LogError(log, "MP_MAIN_FLAG_UNAVAILABLE " + error.Message);
                // Keep any potentially published code and its gate alive, even after partial publication.
                if (!publicationStarted)
                {
                    candidate?.Dispose();
                    if (flag != IntPtr.Zero) Marshal.FreeHGlobal(flag);
                    if (claimed) NativeSiteRegistry.Unregister(BugfixesAndQoLPlugin.PluginGuid, Feature);
                }
            }
        }

        private static bool HasExpectedJump(X64InlineHook hook)
        {
            if (!hook.IsInstalled) return false;
            byte* live = (byte*)hook.TargetAddress;
            return live[0] == 0xFF && live[1] == 0x25 && *(uint*)(live + 2) == 0 &&
                *(ulong*)(live + 6) == unchecked((ulong)hook.StubAddress.ToInt64()) && live[14] == 0x90;
        }

    }
}
