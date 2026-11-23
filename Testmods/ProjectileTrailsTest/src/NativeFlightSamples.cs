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
using SHCDESE.Interop.Enums;
using Shared;
using static Iced.Intel.AssemblerRegisters;

namespace ProjectileTrailsTest
{
    // One native producer (the simulation's projectile sweep), one Unity reader.
    internal sealed unsafe class NativeFlightSamples
    {
        private const string Feature = "projectile afterimage observer";
        private const string Hash = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        private const int Entry = 0x9BFC0;
        private const int CallSite = 0x9FED0;
        // Incremented by 0xCE03B before the projectile sweep at 0xCE245.
        private const int TickRva = 0x3665F58;
        private const int CurrentSlotRva = 0x8F8C30;
        private const int SubtileTableRva = 0x2DB520;
        private const int ProjectileStride = 0xE8;
        private const int TypeOffset = 0x3E;
        private const int HeaderSize = 32;
        internal const int Capacity = TrailBuffer.CaptureCapacity;
        private static readonly byte[] Prologue = { 0x48, 0x89, 0x5C, 0x24, 0x10,
            0x48, 0x89, 0x6C, 0x24, 0x18, 0x57, 0x48, 0x83, 0xEC, 0x30 };
        private static readonly byte[] Following = { 0x44, 0x8B, 0x15, 0x62, 0xCE, 0x5C, 0x03 };
        private static readonly byte[] EmitCall = { 0xE8, 0xEB, 0xC0, 0xFF, 0xFF };

        private readonly byte* buffer;
        private readonly byte* image;
        private readonly ManualLogSource log;
        private int generation;
        private HookTransaction transaction;

        [StructLayout(LayoutKind.Sequential)]
        internal struct EntrySample
        {
            internal long Sequence;
            internal int Generation;
            internal uint Tick;
            internal short X, Y, Height, File;
            internal int Frame, Slot;
        }

        internal NativeFlightSamples(CrusaderLibraryLoadContext context, string ownerGuid, ManualLogSource log)
        {
            this.log = log;
            if (!string.Equals(DebugLogHelper.InstalledNativeSha256(), Hash, StringComparison.Ordinal))
                throw new InvalidOperationException("CrusaderDE.dll has a different SHA-256.");

            int length = Prologue.Length;
            var memory = context.Memory;
            if (!memory.Slice(Entry, length).SequenceEqual(Prologue) ||
                !memory.Slice(Entry + length, Following.Length).SequenceEqual(Following) ||
                !memory.Slice(CallSite, EmitCall.Length).SequenceEqual(EmitCall) ||
                sizeof(EntrySample) != 32)
                throw new InvalidOperationException("Afterimage observer machine contract changed.");

            image = (byte*)context.ModuleHandle;
            NativePatchGuard.Bind(context, log);
            ulong target = (ulong)(image + Entry);
            NativePatchGuard.RequireUnpatched(target, length, Feature, Feature);
            using (var probe = new X64InlineHook(target, length))
                if (probe.IsInstalled || probe.TargetAddress != target || probe.DisplacedByteCount != length)
                    throw new InvalidOperationException("RedBird displaced span differs from the audited 5/5/1/4 prologue.");

            var claim = NativeSiteRegistry.Register(ownerGuid, Feature, Entry, length, NativeSiteMode.Exclusive);
            if (!claim.Succeeded) throw new InvalidOperationException(claim.Reason);
            int size = HeaderSize + Capacity * sizeof(EntrySample);
            buffer = (byte*)Marshal.AllocHGlobal(size);
            for (int i = 0; i < size; i += sizeof(long)) *(long*)(buffer + i) = 0;
        }

        internal void Install(CrusaderLibraryLoadContext context)
        {
            ulong target = (ulong)(image + Entry);
            var handle = new HookHandle<X64InlineHook>();
            transaction = new HookTransaction(context.Region,
                SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory, new HookTransactionOptions
                {
                    InstallOnCommit = false,
                    OwnsHooks = true,
                    FailureMode = TransactionFailureMode.RollbackAndThrow
                });
            transaction.AddInline(handle, HookTarget.FromAddress(target),
                (a, original, _) => Emit(a, original),
                hookSize: Prologue.Length, name: Feature);
            var committed = NativePatchGuard.Commit(transaction, Feature);
            if (!committed.IsCompleteSuccess || !handle.Success || !handle.Hook.IsInstalled ||
                handle.Hook.TargetAddress != target || handle.Hook.DisplacedByteCount != Prologue.Length ||
                *(ushort*)target != 0x25FF || *(uint*)(target + 2) != 0 ||
                *(ulong*)(target + 6) != (ulong)handle.Hook.StubAddress)
                throw new InvalidOperationException("Afterimage observer did not publish the audited jump.");

            log.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] {Feature}: RVA 0x9BFC0–0x9BFCE (5/5/1/4 bytes), next instruction 448B1562CE5C03");
        }

        internal int Generation => Volatile.Read(ref *(int*)buffer);
        internal uint Tick => Volatile.Read(ref *(uint*)(image + TickRva));
        internal long Head => Volatile.Read(ref *(long*)(buffer + 8));

        internal void Reset(bool active) =>
            Volatile.Write(ref *(int*)buffer, active ? ++generation : 0);

        internal bool TryRead(long sequence, int epoch, out EntrySample sample)
        {
            var entry = (EntrySample*)(buffer + HeaderSize + (sequence & (Capacity - 1)) * sizeof(EntrySample));
            sample = default;
            if (Volatile.Read(ref entry->Sequence) != sequence) return false;
            sample = *entry;
            Thread.MemoryBarrier();
            return Volatile.Read(ref entry->Sequence) == sequence && sample.Generation == epoch;
        }

        internal void Subtile(int fineX, int fineY, global::Enums.Dircs rotation, out int x, out int y)
        {
            int u = fineX & 7;
            int v = fineY & 7;
            // The original table is indexed in native view axes. East mirrors U, West mirrors V.
            if (rotation == global::Enums.Dircs.East || rotation == global::Enums.Dircs.South) u = 7 - u;
            if (rotation == global::Enums.Dircs.West || rotation == global::Enums.Dircs.South) v = 7 - v;
            byte* offset = image + SubtileTableRva + (v * 8 + u) * 8;
            x = *(short*)offset;
            y = *(short*)(offset + 4);
        }

        private void Emit(Assembler a, ReadOnlySpan<Instruction> original)
        {
            var restore = a.CreateLabel();
            var append = a.CreateLabel();

            // 9FED0 is the sole caller. Capture the Win64 args on entry, before the allocator
            // returns zero in Extreme. Preserve original RFLAGS and all modified registers.
            a.pushfq();
            a.push(rax);
            a.push(r10);
            a.push(r11);
            a.push(r12);
            a.mov(r10, (ulong)buffer);
            a.mov(r12d, __dword_ptr[r10]);
            a.test(r12d, r12d);
            a.jz(restore);

            a.mov(rax, (ulong)(image + CurrentSlotRva));
            a.movsxd(rax, __dword_ptr[rax]);
            a.imul(r11, rax, ProjectileStride);
            a.cmp(__word_ptr[rcx + r11 + TypeOffset], (int)ProjectileType.ArcherArrow);
            a.je(append);
            a.cmp(__word_ptr[rcx + r11 + TypeOffset], (int)ProjectileType.CrossbowBolt);
            a.je(append);
            // The European Attack Here launch uses this type when it isn't mapped to ArcherArrow.
            a.cmp(__word_ptr[rcx + r11 + TypeOffset], (int)ProjectileType.UnkMissile24);
            a.jne(restore);

            a.Label(ref append);
            a.mov(r11, __qword_ptr[r10 + 8]);
            a.inc(r11);
            // Keep the source slot in EAX while indexing the 32-byte ring by sequence.
            a.push(rax);
            a.mov(eax, r11d);
            a.and(eax, Capacity - 1);
            a.shl(rax, 5);
            a.lea(rax, __qword_ptr[r10 + rax + HeaderSize]);
            a.mov(__qword_ptr[rax], 0);
            a.mov(__dword_ptr[rax + 8], r12d);
            a.mov(r12, (ulong)(image + TickRva));
            a.mov(r12d, __dword_ptr[r12]);
            a.mov(__dword_ptr[rax + 12], r12d);
            a.mov(__word_ptr[rax + 16], dx);
            a.mov(__word_ptr[rax + 18], r8w);
            a.mov(__word_ptr[rax + 20], r9w);
            // Six pushes (48 bytes): original file and frame at entry RSP+0x30/+0x38.
            a.mov(r12d, __dword_ptr[rsp + 0x60]);
            a.mov(__word_ptr[rax + 22], r12w);
            a.mov(r12d, __dword_ptr[rsp + 0x68]);
            a.mov(__dword_ptr[rax + 24], r12d);
            a.pop(r12);
            a.mov(__dword_ptr[rax + 28], r12d);
            // x64 orders stores. Readers reject an in-progress or overwritten record.
            a.mov(__qword_ptr[rax], r11);
            a.mov(__qword_ptr[r10 + 8], r11);

            a.Label(ref restore);
            a.pop(r12);
            a.pop(r11);
            a.pop(r10);
            a.pop(rax);
            a.popfq();
            foreach (var instruction in original) a.AddInstruction(instruction);
        }
    }
}
