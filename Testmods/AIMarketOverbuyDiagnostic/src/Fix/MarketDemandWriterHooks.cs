using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using RedBird.Backends.NativeX64;
using SHCDESE.API.LowLevel;
using Shared;
using Iced.Intel;

namespace AIMarketOverbuyDiagnostic
{
    // Mid-function stores have no callable managed ABI. This relay observes a saved
    // register image, restores the native state, then jumps through the original
    // trampoline. Published executable pages, hooks and delegates live to exit.
    internal static class MarketDemandWriterHooks
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void Observer(IntPtr snapshot, int siteIndex);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void SiteCall();

        internal const int SnapshotBytes = 128;
        internal const int R15 = 0, R14 = 8, R13 = 16, R12 = 24, R11 = 32, R10 = 40,
            R9 = 48, R8 = 56, Rdi = 64, Rsi = 72, Rbp = 80, Rbx = 88,
            Rdx = 96, Rcx = 104, Rax = 112, Rflags = 120;
        private const int PageBytes = 4096, OriginalOffset = PageBytes - 8, ContinuationOffset = PageBytes - 16;
        private static readonly object rootsGate = new object();
        private static readonly List<WriterHook> roots = new List<WriterHook>();
        // RedBird requires a delegate even with an explicit native intermediary.
        // It is never the destination of a published patch or called by the relay.
        private static readonly SiteCall placeholderRoot = UnreachableManagedEntry;
        private static void UnreachableManagedEntry() =>
            throw new InvalidOperationException("A native writer relay must never enter the managed placeholder.");

        internal static WriterHook Prepare(CrusaderLibraryLoadContext context, int rva, byte[] store,
            int siteIndex, IntPtr activeGate, Observer observer) =>
            Prepare(context.Memory, unchecked((ulong)context.ModuleHandle.ToInt64()), rva, store,
                siteIndex, activeGate, observer);

        internal static WriterHook PrepareSource(CrusaderLibraryLoadContext context, int rva, byte[] store,
            int siteIndex, IntPtr activeGate, Observer observer, IntPtr correctionGate) =>
            PrepareSource(context.Memory, unchecked((ulong)context.ModuleHandle.ToInt64()), rva, store,
                siteIndex, activeGate, observer, correctionGate);

        internal static WriterHook PrepareSource(ReadOnlySpan<byte> loadImage, ulong moduleBase, int rva,
            byte[] store, int siteIndex, IntPtr activeGate, Observer observer, IntPtr correctionGate)
        {
            if (correctionGate == IntPtr.Zero) throw new ArgumentNullException(nameof(correctionGate));
            ValidateSourceStore(store);
            return PrepareCore(loadImage, moduleBase, rva, store, siteIndex, activeGate, observer, correctionGate);
        }

        // Offline fixtures use the same implementation and installed backend.
        internal static WriterHook Prepare(ReadOnlySpan<byte> loadImage, ulong moduleBase, int rva,
            byte[] store, int siteIndex, IntPtr activeGate, Observer observer) =>
            PrepareCore(loadImage, moduleBase, rva, store, siteIndex, activeGate, observer, IntPtr.Zero);

        private static unsafe WriterHook PrepareCore(ReadOnlySpan<byte> loadImage, ulong moduleBase, int rva,
            byte[] store, int siteIndex, IntPtr activeGate, Observer observer, IntPtr correctionGate)
        {
            if (activeGate == IntPtr.Zero) throw new ArgumentNullException(nameof(activeGate));
            if (observer == null) throw new ArgumentNullException(nameof(observer));
            var hook = new WriterHook(siteIndex, observer);
            try
            {
                hook.page = VirtualAlloc(IntPtr.Zero, (UIntPtr)PageBytes, 0x3000, 0x40);
                if (hook.page == IntPtr.Zero) throw new InvalidOperationException("Writer relay allocation failed.");
                ulong address = unchecked((ulong)hook.page.ToInt64());
                ulong callback = unchecked((ulong)Marshal.GetFunctionPointerForDelegate(observer).ToInt64());
                byte[] code = correctionGate == IntPtr.Zero ? Generate(address, address + OriginalOffset,
                    unchecked((ulong)activeGate.ToInt64()), callback, siteIndex) :
                    GenerateSource(address, address + OriginalOffset, address + ContinuationOffset,
                        unchecked((ulong)activeGate.ToInt64()), unchecked((ulong)correctionGate.ToInt64()), callback, siteIndex, store);
                if (code.Length >= ContinuationOffset) throw new InvalidOperationException("Writer relay exceeds its private page.");
                Marshal.Copy(code, 0, hook.page, code.Length);
                hook.relay = placeholderRoot;
                hook.feature = "ai-market-demand-writer." + siteIndex + "." + rva.ToString("X");
                hook.detour = AuditedEntryDetour.PrepareNativeTarget(loadImage, moduleBase, rva, store,
                    hook.relay, hook.page, hook.feature);
                // Slot is written exactly once, while the relay is still unpublished.
                *(long*)(hook.page + OriginalOffset) = hook.detour.TrampolineAddress.ToInt64();
                *(long*)(hook.page + ContinuationOffset) = unchecked((long)(moduleBase + (uint)rva + (uint)store.Length));
                if (Marshal.ReadInt64(hook.page + OriginalOffset) != hook.detour.TrampolineAddress.ToInt64())
                    throw new InvalidOperationException("Writer trampoline slot readback differs.");
                if (unchecked((ulong)Marshal.ReadInt64(hook.page + ContinuationOffset)) != moduleBase + (uint)rva + (uint)store.Length)
                    throw new InvalidOperationException("Writer continuation slot readback differs.");
                if (!FlushInstructionCache(GetCurrentProcess(), hook.page, (UIntPtr)code.Length))
                    throw new InvalidOperationException("Writer relay instruction-cache flush failed.");
                return hook;
            }
            catch { hook.DisposeUnpublished(); throw; }
        }

        internal sealed class WriterHook
        {
            private readonly Observer observer;
            internal IntPtr page;
            internal SiteCall relay;
            internal NativeDetour<SiteCall> detour;
            internal string feature;
            private bool publishAttempted, disposed;
            private Func<bool> integrity;
            internal WriterHook(int siteIndex, Observer callback) { observer = callback; }
            internal bool IsPublished => publishAttempted;
            internal bool IntegrityReady => integrity != null && integrity();

            internal void Publish()
            {
                if (disposed || detour == null || publishAttempted)
                    throw new InvalidOperationException("Writer candidate is not unpublished and prepared.");
                // Root before attempting any executable mutation. Even partial backend
                // failure must leave every callback/trampoline allocation valid.
                lock (rootsGate) roots.Add(this);
                publishAttempted = true;
                AuditedEntryDetour.Publish(detour, relay, feature);
                integrity = NativePatchGuard.CaptureIntegrity(new RedBird.Abstractions.Hooks.IHook[] { detour }, feature);
                GC.KeepAlive(observer);
            }

            internal void DisposeUnpublished()
            {
                if (publishAttempted || AuditedEntryDetour.WasPublished(detour) || disposed) return;
                disposed = true;
                detour?.Dispose();
                if (page != IntPtr.Zero) { VirtualFree(page, UIntPtr.Zero, 0x8000); page = IntPtr.Zero; }
                GC.KeepAlive(observer);
            }
        }

        // Pure emitter, also executed by the offline native sentinel harness.
        // The observer MUST catch all managed exceptions. A reverse-P/Invoke
        // exception cannot cross an arbitrary native store site's stack.
        internal static byte[] Generate(ulong start, ulong originalSlot, ulong activeGate,
            ulong observer, int siteIndex)
        {
            if (originalSlot == 0 || activeGate == 0 || observer == 0)
                throw new ArgumentException("Writer relay requires non-null native addresses.");
            var code = new List<byte>(192);
            int dormantFixup = EmitGatedObserver(code, activeGate, observer, siteIndex);
            EmitOriginalJump(code, start, originalSlot);
            int dormant = code.Count;
            code.Add(0x58); code.Add(0x9D);
            EmitOriginalJump(code, start, originalSlot);
            PatchBranch(code, dormantFixup, dormant);
            return code.ToArray();
        }

        internal static byte[] GenerateSource(ulong start, ulong originalSlot, ulong continuationSlot,
            ulong activeGate, ulong correctionGate, ulong observer, int siteIndex, byte[] store)
        {
            if (originalSlot == 0 || continuationSlot == 0 || activeGate == 0 || correctionGate == 0 || observer == 0)
                throw new ArgumentException("Source relay requires non-null native addresses.");
            if (siteIndex < 0 || siteIndex >= 256) throw new ArgumentOutOfRangeException(nameof(siteIndex));
            ValidateSourceStore(store);
            var code = new List<byte>(384);
            int originalDormantFixup = EmitGatedObserver(code, activeGate, observer, siteIndex);
            // Full original RSP/GPR/flags/FX state is restored here. Exactly this
            // validated MOV is replayed; the active path never uses its trampoline.
            code.AddRange(store);
            int correctionDormantFixup = EmitGatedObserver(code, correctionGate, observer, siteIndex + 256);
            EmitOriginalJump(code, start, continuationSlot);
            int correctionDormant = code.Count;
            code.Add(0x58); code.Add(0x9D);
            EmitOriginalJump(code, start, continuationSlot);
            int originalDormant = code.Count;
            code.Add(0x58); code.Add(0x9D);
            EmitOriginalJump(code, start, originalSlot);
            PatchBranch(code, correctionDormantFixup, correctionDormant);
            PatchBranch(code, originalDormantFixup, originalDormant);
            return code.ToArray();
        }

        private static void ValidateSourceStore(byte[] store)
        {
            if (store == null || store.Length < 6) throw new ArgumentException("Source store bytes are missing.");
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(store));
            Instruction instruction = decoder.Decode();
            if (instruction.IsInvalid || decoder.IP != (ulong)store.Length ||
                (instruction.Code != Code.Mov_rm32_r32 && instruction.Code != Code.Mov_rm32_imm32) ||
                instruction.Op0Kind != OpKind.Memory || instruction.IsIPRelativeMemoryOperand ||
                instruction.MemoryBase == Register.RSP || instruction.MemoryIndex == Register.RSP ||
                instruction.MemoryBase == Register.ESP || instruction.MemoryIndex == Register.ESP ||
                instruction.Op1Register == Register.ESP)
                throw new ArgumentException("Source relay requires one non-RIP, non-RSP dword MOV store.");
        }

        private static void PatchBranch(List<byte> code, int fixup, int target)
        {
            byte[] displacement = BitConverter.GetBytes(target - fixup - 4);
            for (int index = 0; index < 4; index++) code[fixup + index] = displacement[index];
        }

        // Restores the entire entry state before returning to the generated native
        // path. Its not-taken gate branch still has flags/RAX on the original stack.
        private static int EmitGatedObserver(List<byte> code, ulong activeGate, ulong observer, int siteIndex)
        {
            Action<byte[]> emit = bytes => code.AddRange(bytes);
            emit(new byte[] { 0x9C, 0x50, 0x48, 0xB8 }); // pushfq; push rax; mov rax,gate
            emit(BitConverter.GetBytes(activeGate));
            emit(new byte[] { 0x83, 0x38, 0x00, 0x0F, 0x84 }); // cmp dword[rax],0; je dormant
            int dormantFixup = code.Count; emit(new byte[4]);
            emit(new byte[] { 0x51, 0x52, 0x53, 0x55, 0x56, 0x57,
                0x41,0x50, 0x41,0x51, 0x41,0x52, 0x41,0x53,
                0x41,0x54, 0x41,0x55, 0x41,0x56, 0x41,0x57 });
            emit(new byte[] { 0x48,0x89,0xE3, // mov rbx,rsp (snapshot; ABI preserved across callback)
                0x48,0x81,0xEC,0x30,0x02,0x00,0x00, // sub rsp,560
                0x48,0x83,0xE4,0xF0, // and rsp,-16
                0x48,0x0F,0xAE,0x44,0x24,0x20, // fxsave64 [rsp+32], beyond shadow space
                0x48,0x89,0xD9, 0xBA }); // rcx=snapshot; edx=site
            emit(BitConverter.GetBytes(siteIndex));
            emit(new byte[] { 0x48,0xB8 }); emit(BitConverter.GetBytes(observer));
            emit(new byte[] { 0xFF,0xD0, // call rax (Windows x64 ABI)
                0x48,0x0F,0xAE,0x4C,0x24,0x20, // fxrstor64 [rsp+32]
                0x48,0x89,0xDC, // mov rsp,rbx
                0x41,0x5F, 0x41,0x5E, 0x41,0x5D, 0x41,0x5C,
                0x41,0x5B, 0x41,0x5A, 0x41,0x59, 0x41,0x58,
                0x5F,0x5E,0x5D,0x5B,0x5A,0x59,0x58,0x9D });
            return dormantFixup;
        }

        private static void EmitOriginalJump(List<byte> code, ulong start, ulong slot)
        {
            long displacement = checked((long)slot - ((long)start + code.Count + 6));
            if (displacement < int.MinValue || displacement > int.MaxValue)
                throw new ArgumentException("Original slot is outside RIP-relative range.");
            code.Add(0xFF); code.Add(0x25); code.AddRange(BitConverter.GetBytes((int)displacement));
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr size, uint type, uint protection);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualFree(IntPtr address, UIntPtr size, uint type);
        [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FlushInstructionCache(IntPtr process, IntPtr address, UIntPtr size);
    }
}
