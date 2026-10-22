using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using BepInEx.Logging;
using Iced.Intel;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.Interop;

namespace AISellThresholdFixTest
{
    internal sealed unsafe partial class SellRuntime
    {
        private readonly ManualLogSource log;
        private readonly IntPtr state;
        private readonly ulong image;
        private HookTransaction transaction;
        private readonly HookHandle<X64InlineHook>[] handles = new HookHandle<X64InlineHook>[2];
        private bool published, tickLogged;

        internal SellRuntime(ManualLogSource logger, CrusaderLibraryLoadContext context)
        {
            log = logger; image = unchecked((ulong)context.ModuleHandle.ToInt64());
            state = Marshal.AllocHGlobal(64);
            for (int i = 0; i < 64; i += 8) Marshal.WriteInt64(state, i, 0);
            try
            {
                string native = null;
                foreach (ProcessModule module in Process.GetCurrentProcess().Modules)
                    if (module.BaseAddress == context.ModuleHandle) { native = module.FileName; break; }
                if (native == null || Shared.DebugLogHelper.GetFileSha256(native) != SellAssembly.NativeHash)
                    throw new InvalidOperationException("Unknown native DLL hash; both sell fixes disabled.");
                VerifyBackend();
                if (sizeof(GamePlayerResources) != SellAssembly.PlayerStride ||
                    Marshal.OffsetOf(typeof(GamePlayerResources), nameof(GamePlayerResources.r_TotalGoodsNull)).ToInt32() != 0x4D0 ||
                    Marshal.OffsetOf(typeof(GamePlayerResources), nameof(GamePlayerResources.r_TotalGoodsFoodAle)).ToInt32() != 0x508 ||
                    Marshal.OffsetOf(typeof(GamePlayerResources), nameof(GamePlayerResources.r_TotalGoodsGold)).ToInt32() != 0x50C ||
                    (int)eGoods.STORED_FOOD_ALE != 14 || (int)eGoods.STORED_GOLD != 15 || (int)eGoods.Count != 25)
                    throw new InvalidOperationException("Installed goods/player layout differs from the audited contract.");
                // Protect the comparison, price-call and epilogue semantics as well as patch spans.
                ValidateLive(0x3F202, "443BCA7F16");
                ValidateLive(0x29732, "E859540A00");
                ValidateLive(SellAssembly.SaleEpilog, "488B6C2440488B5C2448488B7424504883C4305FC3");
                for (int i = 0; i < handles.Length; i++)
                {
                    Resolve(new ReadOnlySpan<byte>((void*)image, context.Memory.Length), SellAssembly.Sites[i], SellAssembly.Hex(SellAssembly.OriginalHex[i]));
                    ValidateLive(SellAssembly.Sites[i], SellAssembly.OriginalHex[i]);
                    using (var probe = new X64InlineHook(image + (uint)SellAssembly.Sites[i], 15))
                        if (probe.DisplacedByteCount != 15) throw new InvalidOperationException("Actual RedBird displacement differs.");
                    var original = SellAssembly.Decode(SellAssembly.Hex(SellAssembly.OriginalHex[i]), image + (uint)SellAssembly.Sites[i]);
                    var assembler = new Assembler(64);
                    SellAssembly.Generate(assembler, original, image + (uint)SellAssembly.Sites[i] + 15,
                        i, image, unchecked((ulong)state.ToInt64()));
                    SellAssembly.Assemble(assembler, image + 0x20000000);
                }
                transaction = new HookTransaction(context.Region, SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                    new HookTransactionOptions { FailureMode = TransactionFailureMode.RollbackAndThrow, OwnsHooks = true });
                for (int i = 0; i < handles.Length; i++)
                {
                    int index = i; handles[i] = new HookHandle<X64InlineHook>();
                    transaction.AddInline(handles[i], HookTarget.FromAddress(image + (uint)SellAssembly.Sites[i]),
                        (a, original, back) => SellAssembly.Generate(a, original, back, index, image,
                            unchecked((ulong)state.ToInt64())), hookSize: 15);
                }
                var result = transaction.Commit();
                if (!result.IsCompleteSuccess) throw new InvalidOperationException("Incomplete sell hook transaction.");
                for (int i = 0; i < handles.Length; i++)
                {
                    var hook = handles[i].Hook;
                    if (!handles[i].Success || !hook.IsInstalled || hook.DisplacedByteCount != 15 ||
                        hook.TargetAddress != image + (uint)SellAssembly.Sites[i] || hook.StubAddress == IntPtr.Zero)
                        throw new InvalidOperationException("Committed hook contract differs.");
                    var patch = new ReadOnlySpan<byte>((void*)hook.TargetAddress, 15);
                    if (patch[0] != 0xFF || patch[1] != 0x25 || BitConverter.ToInt32(patch.Slice(2, 4).ToArray(), 0) != 0 ||
                        BitConverter.ToInt64(patch.Slice(6, 8).ToArray(), 0) != hook.StubAddress.ToInt64() || patch[14] != 0x90)
                        throw new InvalidOperationException("Committed jump/pointer/padding differs.");
                }
            }
            catch { RollbackUnpublished(); throw; }
        }
        internal void Publish()
        {
            published = true;
            Volatile.Write(ref *(int*)state, 1);
            Shared.DebugLogHelper.LogInfo(log,
                "hook confirmed: AISellThresholdFixTest active; decision RVA=0x3F1F3..0x3F202, sale RVA=0x29723..0x29732; spans=15/15; reserve>=0, sale<=stock.");
            DiagStart();
        }
        internal void OnTick(int tick)
        {
            if (!tickLogged)
            {
                tickLogged = true;
                Shared.DebugLogHelper.LogInfo(log, "AISellThresholdFixTest runtime after startup cleanup: persistent Extender OnTick reached; native guards remain installed.");
            }
            DiagTick(tick);
        }
        partial void DiagStart();
        partial void DiagTick(int tick);
        private void RollbackUnpublished()
        {
            if (published) throw new InvalidOperationException("Published hooks cannot be rolled back.");
            transaction?.Dispose(); transaction = null;
            Marshal.FreeHGlobal(state);
        }
        private void ValidateLive(int rva, string hex)
        {
            var expected = SellAssembly.Hex(hex);
            if (!new ReadOnlySpan<byte>((void*)(image + (uint)rva), expected.Length).SequenceEqual(expected))
                throw new InvalidOperationException($"Live native bytes differ at RVA=0x{rva:X}; refusing overlapping/changed code.");
        }
        private void Resolve(ReadOnlySpan<byte> memory, int expectedRva, byte[] expected)
        {
            bool direct = memory.Slice(expectedRva, expected.Length).SequenceEqual(expected);
            if (!direct)
            {
                int pe = BitConverter.ToInt32(memory.Slice(0x3C, 4).ToArray(), 0);
                int sections = BitConverter.ToUInt16(memory.Slice(pe + 6, 2).ToArray(), 0);
                int table = pe + 24 + BitConverter.ToUInt16(memory.Slice(pe + 20, 2).ToArray(), 0), match = -1;
                for (int i = 0; i < sections; i++)
                {
                    int section = table + i * 40;
                    if ((BitConverter.ToUInt32(memory.Slice(section + 36, 4).ToArray(), 0) & 0x20000000) == 0) continue;
                    int start = BitConverter.ToInt32(memory.Slice(section + 12, 4).ToArray(), 0);
                    int length = BitConverter.ToInt32(memory.Slice(section + 8, 4).ToArray(), 0);
                    if (start < 0 || length < 0 || (long)start + length > memory.Length) throw new InvalidOperationException("Invalid PE section.");
                    for (int p = start; p <= start + length - expected.Length; p++)
                        if (memory.Slice(p, expected.Length).SequenceEqual(expected))
                        { if (match >= 0) throw new InvalidOperationException("Ambiguous sell pattern."); match = p; }
                }
                // Layouts and downstream targets are hash-bound: a moved duplicate is not an update fallback.
                if (match != expectedRva) throw new InvalidOperationException("Sell site occupied/changed; no validated same-image pattern fallback.");
            }
            Shared.DebugLogHelper.LogInfo(log, $"Sell hook resolved: method={(direct ? "reference-rva" : "pattern-fallback")}, RVA=0x{expectedRva:X}, span=15.");
        }
        private static void VerifyBackend()
        {
            foreach (var item in new[] {
                new[] { typeof(X64InlineHook).Assembly.Location, "E085B83B2B87B5D2142C980090D8D9EC616A90A443BD125C17B41E0BB47A44EB" },
                new[] { typeof(RedBird.Core.Memory.ScanRegion).Assembly.Location, "EE4B036C486077F7D8898AE054F124EDDE73E6AE1A059E8991C361E2B720D29F" },
                new[] { typeof(IHook).Assembly.Location, "339CECE116F66F59ECC7DA5E9A5C0B2DFE24F4E7A5E7EFC005864FA77C027A1E" },
                new[] { typeof(Assembler).Assembly.Location, "CC915D1C2E7BE60933C5B129842CA3D08D1FE3CDA6C6D6ADE8824DCDDE16191F" } })
                if (Shared.DebugLogHelper.GetFileSha256(item[0]) != item[1])
                    throw new InvalidOperationException("Installed backend differs: " + item[0]);
        }
    }
}
