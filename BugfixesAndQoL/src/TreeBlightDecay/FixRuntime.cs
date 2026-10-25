using APIShared.NativeSites;
using BepInEx.Logging;
using Iced.Intel;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API.LowLevel;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;

namespace BugfixesAndQoL.TreeBlightDecay
{
    internal sealed unsafe class FixRuntime
    {
        private const string Registration = "TreeBlightDecay";
        private readonly ManualLogSource log;
        private readonly HookHandle<X64InlineHook>[] handles = { new HookHandle<X64InlineHook>(), new HookHandle<X64InlineHook>() };
        private HookTransaction transaction;
        private IntPtr state;
        private bool published, ready;

        internal FixRuntime(ManualLogSource log) { this.log = log; }

        internal void Install(CrusaderLibraryLoadContext context, bool referenceHashMatches)
        {
            if (context == null || context.ModuleHandle == IntPtr.Zero || !referenceHashMatches ||
                !string.Equals(Shared.DebugLogHelper.InstalledNativeSha256(), NativeCode.Hash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Tree blight requires the audited native image/layout: " + NativeCode.Hash);
            ValidateLayout();
            ulong image = unchecked((ulong)context.ModuleHandle.ToInt64());
            int[] sites = { NativeCode.EventRva, NativeCode.GrowthRva };
            byte[][] expected = { NativeCode.EventBytes, NativeCode.GrowthBytes };
            byte[][] following = { NativeCode.EventFollowing, NativeCode.GrowthFollowing };
            var spans = new NativeSiteSpan[2];
            for (int i = 0; i < sites.Length; i++)
            {
                int site = sites[i];
                var resolution = Shared.NativePatternResolver.ResolveUnique(context.Memory,
                    BitConverter.ToString(expected[i]).Replace('-', ' '), site, true, "tree blight " + i, log);
                if (resolution.Rva != site || !context.Memory.Slice(site, NativeCode.Span).SequenceEqual(expected[i]))
                    throw new InvalidOperationException("Tree-blight site moved outside its audited layout.");
                if (!context.Memory.Slice(site + NativeCode.Span, following[i].Length).SequenceEqual(following[i]))
                    throw new InvalidOperationException("Tree-blight continuation changed.");
                byte[] live = new byte[NativeCode.Span];
                Marshal.Copy(new IntPtr(unchecked((long)(image + (uint)site))), live, 0, live.Length);
                if (!live.AsSpan().SequenceEqual(expected[i])) throw new InvalidOperationException("Occupied tree-blight site: " + site.ToString("X"));
                using (var probe = new X64InlineHook(image + (uint)site, NativeCode.Span))
                    if (probe.DisplacedByteCount != NativeCode.Span || probe.IsInstalled)
                        throw new InvalidOperationException("Tree-blight RedBird span changed.");
                using (var sha = SHA256.Create())
                    spans[i] = new NativeSiteSpan(site, NativeCode.Span, NativeSiteMode.Exclusive,
                        BitConverter.ToString(sha.ComputeHash(expected[i])).Replace("-", ""));
                Shared.DebugLogHelper.LogInfo(log, $"TREE_BLIGHT_RESOLVED method={resolution.Method}; rva=0x{site:X}; end=0x{site + NativeCode.Span:X}; span={NativeCode.Span}; instructionLengths={(i == 0 ? "3,7,4" : "8,6")}; followingLength={following[i].Length}.");
            }
            var claim = NativeSiteRegistry.Register(BugfixesAndQoLPlugin.PluginGuid, Registration, spans);
            if (!claim.Succeeded) throw new InvalidOperationException(claim.Reason);
            try
            {
                state = Marshal.AllocHGlobal(32);
                Marshal.Copy(new byte[32], 0, state, 32);
                Marshal.WriteInt64(state, 8, unchecked((long)(image + NativeCode.TableRva)));
                Marshal.WriteInt64(state, 16, unchecked((long)(image + NativeCode.ModeRva)));
                Marshal.WriteInt64(state, 24, unchecked((long)(image + NativeCode.ScreenRva)));
                transaction = BugfixesHookInfrastructure.CreateOwnedTransaction(context.Region);
                for (int i = 0; i < sites.Length; i++)
                {
                    bool growth = i == 1;
                    ulong address = image + (uint)sites[i];
                    // Assemble both producers before committing either site.
                    var a = new Assembler(64);
                    NativeCode.Emit(a, growth, unchecked((ulong)state.ToInt64()), image + NativeCode.GrowthSkip,
                        NativeCode.Decode(expected[i], address));
                    NativeCode.Assemble(a, address);
                    transaction.AddInline(handles[i], HookTarget.FromAddress(address),
                        (assembler, original, continuation) => NativeCode.Emit(assembler, growth,
                            unchecked((ulong)state.ToInt64()), image + NativeCode.GrowthSkip, original),
                        hookSize: NativeCode.Span, name: growth ? "TreeBlightDecay_Growth" : "TreeBlightDecay_Event");
                }
                CommitResult result = transaction.Commit();
                if (!result.IsCompleteSuccess) throw new InvalidOperationException("Tree-blight transaction failed: " + result);
                // Published code is never restored, even if a subsequent verification fails.
                published = true;
                for (int i = 0; i < sites.Length; i++)
                {
                    ulong address = image + (uint)sites[i];
                    var handle = handles[i];
                    if (!handle.Success || handle.Hook.DisplacedByteCount != NativeCode.Span || handle.Hook.TargetAddress != address)
                        throw new InvalidOperationException("Committed tree-blight span differs.");
                    byte* live = (byte*)address;
                    if (live[0] != 0xFF || live[1] != 0x25 || *(uint*)(live + 2) != 0 ||
                        *(ulong*)(live + 6) != unchecked((ulong)handle.Hook.StubAddress.ToInt64()))
                        throw new InvalidOperationException("Committed tree-blight jump differs.");
                }
                ready = true;
                Shared.DebugLogHelper.LogInfo(log, "TREE_BLIGHT_READY: two permanent native stubs; no load scan, managed per-tree callback, save extension or RNG call.");
            }
            catch
            {
                ready = false;
                SetEnabled(false, false);
                if (!published)
                {
                    transaction?.Dispose(); // Unpublished initialization candidate only.
                    if (state != IntPtr.Zero) Marshal.FreeHGlobal(state);
                    state = IntPtr.Zero;
                    NativeSiteRegistry.Unregister(BugfixesAndQoLPlugin.PluginGuid, Registration);
                }
                throw;
            }
        }

        internal static bool IsEnabled(bool hostEnabled, bool optionEnabled) => hostEnabled && optionEnabled;
        internal void SetEnabled(bool hostEnabled, bool optionEnabled)
        {
            if (state != IntPtr.Zero) Volatile.Write(ref *(int*)state, ready && IsEnabled(hostEnabled, optionEnabled) ? 1 : 0);
        }

        private static void ValidateLayout()
        {
            if (Marshal.SizeOf(typeof(GameVegetation)) != 0x9C ||
                (int)VegetationType.ChestnutTree != 1 || (int)VegetationType.OakTree != 2 ||
                (int)VegetationType.PineTree != 3 || (int)VegetationType.BirchTree != 4 ||
                (int)TreeGrowthStage.Stage5 != 5 || (int)AliveState.IsAlive != 2 ||
                (int)eGameTypeModes.GAMETYPE_BUILDER != 1 || (int)eGameTypeModes.GAMETYPE_SIEGE_THAT_BUILDER != 6 ||
                Marshal.OffsetOf(typeof(GameVegetation), nameof(GameVegetation.r_AliveState)).ToInt32() != 0x50 ||
                Marshal.OffsetOf(typeof(GameVegetation), nameof(GameVegetation.r_VegetationType)).ToInt32() != 0x52 ||
                Marshal.OffsetOf(typeof(GameVegetation), nameof(GameVegetation.r_Health)).ToInt32() != 0x7E ||
                Marshal.OffsetOf(typeof(GameVegetation), nameof(GameVegetation.r_IsFelled)).ToInt32() != 0x82 ||
                Marshal.OffsetOf(typeof(GameVegetation), nameof(GameVegetation.r_GrowthStage)).ToInt32() != 0x8C ||
                Marshal.OffsetOf(typeof(GameVegetation), nameof(GameVegetation.r_GrowthProgress)).ToInt32() != 0x90)
                throw new InvalidOperationException("Tree-blight vegetation layout changed.");
        }
    }
}
