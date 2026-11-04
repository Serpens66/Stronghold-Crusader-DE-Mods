using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Iced.Intel;
using Microsoft.Extensions.Logging.Abstractions;
using RedBird.Abstractions.Hooks;
using RedBird.X64.Assembly;
using RedBird.X64.Extensions;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Context;
using SHCDESE.API.LowLevel;
using static Iced.Intel.AssemblerRegisters;

namespace AIBuildDiagnoseTest
{
    internal sealed unsafe class WorkerCauseNativeHooks
    {
        internal sealed class Site
        {
            internal readonly int Rva, Length;
            internal readonly bool ReturnSite;
            internal readonly byte[] Bytes;
            internal Site(int rva, int length, bool returning, string hex)
            { Rva = rva; Length = length; ReturnSite = returning; Bytes = new byte[hex.Length / 2];
              for (int i = 0; i < Bytes.Length; i++) Bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16); }
        }
        internal static readonly Site[] Sites = {
            new Site(0x12CD07, 18, true, "664289842118090000664289B42120090000"),
            new Site(0x12D1A2, 21, false, "664283BC23480A000000751F420FBF9423EE060000")
        };
        private static readonly List<WorkerCauseNativeHooks> published = new List<WorkerCauseNativeHooks>();
        private readonly List<X64InlineHook> hooks = new List<X64InlineHook>();
        private readonly List<ContextHookDelegate> callbacks = new List<ContextHookDelegate>();
        private readonly IntPtr demand = Marshal.AllocHGlobal(2); // Process lifetime, data only, one gate per site.
        private readonly bool[] ready = new bool[2];
        private readonly EconomyDiagnostics owner;
        internal bool CounterReady => ready[1];
        internal WorkerCauseNativeHooks(EconomyDiagnostics owner) { this.owner = owner; Marshal.WriteByte(demand, 0, 0); Marshal.WriteByte(demand, 1, 0); published.Add(this); }
        internal void SetActive(bool active)
        { for (int i = 0; i < ready.Length; i++) Marshal.WriteByte(demand, i, active && ready[i] ? (byte)1 : (byte)0); }
        internal void Install(CrusaderLibraryLoadContext context, string hash)
        {
            if (!string.Equals(hash, NativeDiagnosticContracts.SupportedHash, StringComparison.OrdinalIgnoreCase))
            { owner.Coverage("worker-native", false, "unsupported native hash; no worker writes observed", true); return; }
            foreach (var site in Sites)
            {
                X64InlineHook candidate = null;
                bool publicationAttempted = false;
                int index = site.ReturnSite ? 0 : 1;
                try
                {
                    if (!context.Memory.Slice(site.Rva, site.Bytes.Length).SequenceEqual(site.Bytes)) throw new InvalidOperationException("Pristine site fingerprint differs");
                    ulong address = unchecked((ulong)context.ModuleHandle.ToInt64()) + (uint)site.Rva;
                    var live = new byte[site.Bytes.Length]; Marshal.Copy(unchecked((IntPtr)(long)address), live, 0, live.Length);
                    for (int i = 0; i < live.Length; i++) if (live[i] != site.Bytes[i]) throw new InvalidOperationException("Live site occupied/changed");
                    ContextHookDelegate callback = ctx =>
                    {
                        try
                        {
                        // R14 is reused for vegetation in some return paths. Use the actual
                        // unit-record offset of the displaced stores, not a stale context ID.
                        if (!owner.WorkerRootsMatch(ctx.Pointer->R12, site.ReturnSite ? 0 : ctx.Pointer->RBP))
                        { owner.HookCaptureFailed(new InvalidOperationException("Worker native manager context differs")); return; }
                        int unitId = UnitIdFromOffsets(site.ReturnSite, ctx.Pointer->RCX, ctx.Pointer->RBX);
                        owner.ObserveWorkerNative(site.ReturnSite, unitId,
                            site.ReturnSite ? 0 : unchecked((int)ctx.Pointer->RSI));
                        }
                        catch (Exception ex) { owner.HookCaptureFailed(ex); }
                    };
                    callbacks.Add(callback);
                    var options = new ContextHookOptions { Registers = X64SmartCPUContextRegs.All,
                        Placement = OverwrittenInstructionPlacement.Suppress, HookSize = site.Length };
                    candidate = CreatePrepared(address, site, IntPtr.Add(demand, index), hook => ContextStub.CreateGenerator(hook,
                        ContextStub.Guard(callback, options, address, NullLogger.Instance, "worker cause"), options));
                    hooks.Add(candidate); // Root before executable publication, including post-enable failure.
                    publicationAttempted = true;
                    candidate.Enable();
                    ValidatePublished(candidate, site.Length);
                    ready[index] = true;
                    owner.Coverage("worker-native-" + site.Rva.ToString("X"), true,
                        $"method=reference-rva; hash-bound; displaced={site.Length}; stub=0x{candidate.StubAddress.ToInt64():X}; original-after-callback; permanent");
                }
                catch (Exception ex)
                {
                    if (candidate != null && !publicationAttempted) candidate.Dispose(); // No post-publication executable mutation.
                    owner.Coverage("worker-native-" + site.Rva.ToString("X"), false, ex.Message);
                }
            }
        }

        internal static int UnitIdFromOffsets(bool returning, ulong rcx, ulong rbx)
        {
            ulong offset = returning ? rcx : rbx;
            if (offset % 0x490 != 0 || offset / 0x490 > int.MaxValue)
                throw new InvalidOperationException("Worker record offset is not an integral game ID");
            return (int)(offset / 0x490);
        }
        internal static void ValidatePublished(X64InlineHook hook, int length)
        {
            if (!hook.IsInstalled || hook.DisplacedByteCount != length) throw new InvalidOperationException("Published span differs");
            var bytes = new byte[length]; Marshal.Copy(unchecked((IntPtr)(long)hook.TargetAddress), bytes, 0, length);
            if (bytes[0] != 0xFF || bytes[1] != 0x25 || BitConverter.ToInt32(bytes, 2) != 0 ||
                BitConverter.ToInt64(bytes, 6) != hook.StubAddress.ToInt64()) throw new InvalidOperationException("Published worker jump differs");
            for (int i = 14; i < length; i++) if (bytes[i] != 0x90) throw new InvalidOperationException("Published worker padding differs");
        }

        // The production guard is also executed with a pure native body in tests.
        // No flags/registers/stack changes survive the dormant path. Active return-site
        // flags are dead; at the increment site the displaced CMP computes the live flags.
        internal static X64InlineHook CreatePrepared(ulong address, Site site, IntPtr demand,
            Func<X64InlineHook, InlineHookGenerator> bodyFactory)
        {
            var hook = new X64InlineHook(address, site.Length, NullLogger.Instance, "AI worker " + site.Rva.ToString("X"));
            try
            {
                if (hook.DisplacedByteCount != site.Length || hook.TargetAddress != address) throw new InvalidOperationException("Worker whole-instruction span differs");
                var body = bodyFactory(hook);
                hook.Generate((asm, original, next) =>
                {
                    var dormant = asm.CreateLabel(); var enabled = asm.CreateLabel(); var displaced = asm.CreateLabel();
                    asm.push(rcx);
                    asm.mov(rcx, unchecked((ulong)demand.ToInt64()));
                    asm.movzx(ecx, __byte_ptr[rcx]);
                    asm.jrcxz(dormant);
                    asm.pop(rcx);
                    asm.jmp(enabled);
                    asm.Label(ref dormant);
                    asm.pop(rcx);
                    asm.jmp(displaced);
                    asm.Label(ref enabled);
                    if (site.ReturnSite)
                    {
                        // Only the failure value reaches managed code; the ordinary state-6
                        // transition shares this store. Restore even dead flags on its bypass.
                        var failure = asm.CreateLabel();
                        asm.pushfq(); asm.cmp(ax, 0x6E); asm.je(failure);
                        asm.popfq(); asm.jmp(displaced);
                        asm.Label(ref failure); asm.popfq();
                    }
                    body(asm, original, next);
                    asm.Label(ref displaced);
                    asm.AddInstructions(original); // Exactly once, including the conditional branch.
                });
                if (hook.State != HookState.Prepared || hook.StubAddress == IntPtr.Zero) throw new InvalidOperationException("Worker stub preparation failed");
                return hook;
            }
            catch { hook.Dispose(); throw; } // Never enabled here.
        }
    }
}
