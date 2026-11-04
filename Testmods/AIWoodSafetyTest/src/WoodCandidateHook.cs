using System;
using System.IO;
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

namespace AIWoodSafetyTest
{
    internal sealed unsafe class WoodCandidateHook
    {
        internal const int Rva = 0x58BAE, Length = 14, FallbackRva = 0x58B2E;
        internal static readonly byte[] Original = { 0x44,0x89,0x99,0x3C,0x98,0x1B,0, 0x44,0x89,0x91,0x40,0x98,0x1B,0 };
        // Process-rooted control data: gate +0, enemy bits +4, AIV root +8, masks +16;
        // candidate/relevant-mask counters +24/+32. No executable repatching.
        private readonly IntPtr control = Marshal.AllocHGlobal(40);
        private readonly GCHandle maskPin;
        private readonly WoodSafetyRuntime owner;
        private ContextHookDelegate callback;
        private X64InlineHook hook;
        internal bool Ready { get; private set; }
        internal WoodCandidateHook(WoodSafetyRuntime owner, ushort[] masks)
        {
            this.owner = owner; maskPin = GCHandle.Alloc(masks, GCHandleType.Pinned);
            Marshal.Copy(new byte[40], 0, control, 40); Marshal.WriteInt64(control, 16, maskPin.AddrOfPinnedObject().ToInt64());
        }
        internal void SetDemand(bool active, int enemies)
        { Marshal.WriteInt32(control, 4, enemies); Marshal.WriteByte(control, Ready && active ? (byte)1 : (byte)0); }
        internal long Candidates => Marshal.ReadInt64(control, 24);
        internal long MaskHits => Marshal.ReadInt64(control, 32);
        internal void ResetCounters() { Marshal.WriteInt64(control, 24, 0); Marshal.WriteInt64(control, 32, 0); }
        internal void Install(CrusaderLibraryLoadContext context)
        {
            bool publicationAttempted = false;
            try
            {
                if (!context.Memory.Slice(Rva, Length).SequenceEqual(Original)) throw new InvalidOperationException("Pristine candidate span differs");
                if (!context.Memory.Slice(FallbackRva, 7).SequenceEqual(new byte[] { 0x0F,0xB6,0x81,0x30,0x78,0x18,0 }) ||
                    !context.Memory.Slice(Rva + Length, 5).SequenceEqual(new byte[] { 0x4C,0x8B,0x64,0x24,0x08 }))
                    throw new InvalidOperationException("Candidate fallback/continuation boundaries differ");
                ulong address = unchecked((ulong)context.ModuleHandle.ToInt64()) + Rva;
                var live = new byte[Length]; Marshal.Copy(unchecked((IntPtr)(long)address), live, 0, Length);
                for (int i = 0; i < Length; i++) if (live[i] != Original[i]) throw new InvalidOperationException("Candidate span already occupied");
                Marshal.WriteInt64(control, 8, context.ModuleHandle.ToInt64() + 0x34A9F70);
                callback = ctx =>
                {
                    // RAX is dead on both paths: fallback first overwrites EAX; success is void.
                    ctx.Pointer->RAX = 0;
                    try { if (owner.RejectCandidate((int)ctx.Pointer->R11, (int)ctx.Pointer->R10, true)) ctx.Pointer->RAX = 1; }
                    catch (Exception ex) { owner.BuildFailure(ex); }
                };
                var options = new ContextHookOptions { HookSize = Length, Registers = X64SmartCPUContextRegs.All, Placement = OverwrittenInstructionPlacement.Suppress };
                hook = CreatePrepared(address, address - Rva + FallbackRva, control, h => ContextStub.CreateGenerator(h,
                    ContextStub.Guard(callback, options, address, NullLogger.Instance, "wood safety candidate"), options));
                publicationAttempted = true; hook.Enable(); ValidatePublished(hook);
                Ready = true;
                owner.Info($"CANDIDATE_READY rva=0x{Rva:X}, displaced={Length}, continuation=0x{Rva + Length:X}, fallback=0x{FallbackRva:X}, stub=0x{hook.StubAddress.ToInt64():X}, permanent=true");
            }
            catch (Exception ex)
            {
                if (!publicationAttempted) hook?.Dispose();
                // After any publication attempt retain hook/callback/control for process lifetime.
                owner.BuildFailure(ex);
            }
        }
        internal static void ValidatePublished(X64InlineHook h)
        {
            if (!h.IsInstalled || h.DisplacedByteCount != Length) throw new InvalidOperationException("Published span differs");
            var bytes = new byte[Length]; Marshal.Copy(unchecked((IntPtr)(long)h.TargetAddress), bytes, 0, Length);
            if (bytes[0] != 0xFF || bytes[1] != 0x25 || BitConverter.ToInt32(bytes, 2) != 0 || BitConverter.ToInt64(bytes, 6) != h.StubAddress.ToInt64())
                throw new InvalidOperationException("Published candidate jump differs");
        }
        internal static X64InlineHook CreatePrepared(ulong address, ulong fallback, IntPtr data,
            Func<X64InlineHook, InlineHookGenerator> bodyFactory)
        {
            var h = new X64InlineHook(address, Length, NullLogger.Instance, "AI wood safety candidate");
            try
            {
                if (h.TargetAddress != address || h.DisplacedByteCount != Length) throw new InvalidOperationException("Candidate backend span differs");
                var body = bodyFactory(h);
                h.Generate((a, original, next) =>
                {
                    var bypass = a.CreateLabel(); var masked = a.CreateLabel(); var accept = a.CreateLabel(); var reject = a.CreateLabel();
                    a.push(rax); a.push(rdx); a.push(r8); a.push(r9);
                    a.mov(rax, unchecked((ulong)data.ToInt64()));
                    a.cmp(__byte_ptr[rax], 0); a.je(bypass);
                    a.cmp(rcx, __qword_ptr[rax + 8]); a.jne(bypass);
                    a.cmp(r11d, 160); a.jae(bypass); a.cmp(r10d, 160); a.jae(bypass);
                    a.inc(__qword_ptr[rax + 24]);
                    a.imul(edx, r11d, 5 * ThreatMasks.MapSide); a.imul(r8d, r10d, 5); a.add(edx, r8d);
                    a.mov(r9d, __dword_ptr[rax + 4]); a.mov(r8, __qword_ptr[rax + 16]);
                    for (int owner = 1; owner <= 8; owner++)
                    {
                        var skip = a.CreateLabel();
                        a.test(r9d, 1 << owner); a.je(skip);
                        a.cmp(__word_ptr[r8 + rdx * 2 + owner * ThreatMasks.OwnerStride * 2], 0); a.jne(masked);
                        a.Label(ref skip); a.nop(); // Iced permits one bound label per instruction.
                    }
                    a.jmp(bypass);
                    a.Label(ref masked); a.inc(__qword_ptr[rax + 32]);
                    a.pop(r9); a.pop(r8); a.pop(rdx); a.pop(rax);
                    body(a, original, next);
                    a.test(eax, eax); a.jne(reject); a.jmp(accept);
                    a.Label(ref bypass); a.pop(r9); a.pop(r8); a.pop(rdx); a.pop(rax); a.jmp(accept);
                    a.Label(ref reject); a.jmp(fallback);
                    a.Label(ref accept); a.AddInstructions(original);
                });
                if (h.State != HookState.Prepared || h.StubAddress == IntPtr.Zero) throw new InvalidOperationException("Candidate stub not prepared");
                return h;
            }
            catch { h.Dispose(); throw; } // Never enabled by this method.
        }
    }
}
