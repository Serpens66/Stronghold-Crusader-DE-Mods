using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using BepInEx.Logging;
using Iced.Intel;
using RedBird.Core.Memory;
using RedBird.X64.Extensions;
using RedBird.X64.Hooks;
using static Iced.Intel.AssemblerRegisters;

namespace BugfixesAndQoL
{
    // One native demand byte, no managed transition in the hot path. All code and slots are
    // prepared before publication. Published pages/hooks (including a failed partial install)
    // remain rooted forever; a settings change writes only the ordinary data byte.
    internal sealed unsafe class PermanentNativeSwitch
    {
        private static readonly List<PermanentNativeSwitch> Published = new List<PermanentNativeSwitch>();
        private readonly List<X64AssemblyPatch> patches = new List<X64AssemblyPatch>();
        private readonly IntPtr demand = Marshal.AllocHGlobal(1);
        private bool ready;

        internal PermanentNativeSwitch(ManualLogSource log, ulong imageBase, NativeSwitchPlan[] plans, string contractSha256)
        {
            Volatile.Write(ref *(byte*)demand, 0);
            // Root before the first executable entry is published, even if Enable fails later.
            lock (Published) Published.Add(this);
            foreach (NativeSwitchPlan plan in plans)
                patches.Add(Prepare(imageBase, plan));
            foreach (NativeSwitchPlan plan in plans)
            {
                var decoder = Decoder.Create(64, new ByteArrayCodeReader(plan.Original));
                decoder.IP = imageBase + (uint)plan.Rva;
                var lengths = new List<int>();
                ulong end = decoder.IP + (uint)plan.PhysicalLength;
                while (decoder.IP < end) lengths.Add(decoder.Decode().Length);
                Shared.DebugLogHelper.LogInfo(log, "Native switch validated RVA 0x" + plan.Rva.ToString("X") +
                    "..0x" + (plan.Rva + plan.PhysicalLength).ToString("X") + "; instruction lengths=" +
                    string.Join(",", lengths) + "; following=" + BitConverter.ToString(plan.Following) + ".");
            }
            foreach (X64AssemblyPatch patch in patches)
            {
                patch.Enable();
                if (!patch.IsInstalled)
                    throw new InvalidOperationException("Native logical switch was not installed: " + patch.Name);
            }
            ready = true;
            Shared.DebugLogHelper.LogInfo(log, "Native logical switch installed with gate off; spans=" +
                plans.Length + "; contract sha256=" + contractSha256 + ".");
        }

        internal void SetEnabled(bool value) => Volatile.Write(ref *(byte*)demand, (byte)(ready && value ? 1 : 0));

        private X64AssemblyPatch Prepare(ulong imageBase, NativeSwitchPlan plan)
        {
            ulong target = imageBase + (uint)plan.Rva;
            byte[] live = CodePatch.Read(target, plan.Original.Length);
            if (!live.SequenceEqual(plan.Original))
                throw new InvalidOperationException("Native switch live span differs at RVA 0x" + plan.Rva.ToString("X"));
            // The instruction boundary was proven on the unmodified load image (CreatePlans). Live, another
            // owner may already hook exactly that boundary; e.g. the Script Extender's wall-targeting context
            // hook starts at 0x8EA60 right after the peace-time span. Our block returns to the boundary and
            // so enters that hook unchanged. Any other difference means foreign code overlaps the boundary.
            byte[] following = CodePatch.Read(target + (uint)plan.PhysicalLength, plan.Following.Length);
            if (!following.SequenceEqual(plan.Following) && !StartsForeignJump(following))
                throw new InvalidOperationException("Native switch following live instruction differs at RVA 0x" +
                    (plan.Rva + plan.PhysicalLength).ToString("X") + ": " + BitConverter.ToString(following) + ".");
            IntPtr page = NativeMemoryManager.AllocateStub(target, 4096);
            if (page == IntPtr.Zero) throw new InvalidOperationException("Native switch near allocation failed.");
            ulong slot = unchecked((ulong)page.ToInt64()), guard = slot + 16, dormant = slot + 128, active = slot + 1536;
            long delta = checked((long)slot - (long)target - 6);
            if (delta < int.MinValue || delta > int.MaxValue)
                throw new InvalidOperationException("Native switch slot is outside RIP-relative reach.");
            Write(page, BitConverter.GetBytes(guard));
            Write((IntPtr)(long)guard, BuildGuard(guard, unchecked((ulong)demand.ToInt64()), dormant, active));
            byte[] off = BuildBlock(plan, imageBase, dormant, false);
            byte[] on = BuildBlock(plan, imageBase, active, true);
            if (off.Length > 1408 || on.Length > 2560)
                throw new InvalidOperationException("Native switch block exceeds its reserved storage.");
            Write((IntPtr)(long)dormant, off);
            Write((IntPtr)(long)active, on);
            var bytes = new byte[] { 0xFF, 0x25, 0, 0, 0, 0 };
            Buffer.BlockCopy(BitConverter.GetBytes((int)delta), 0, bytes, 2, 4);
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes)); decoder.IP = target;
            Instruction jump = decoder.Decode();
            var patch = new X64AssemblyPatch(target, plan.PhysicalLength, name: "ModsettingsSwitch_" + plan.Rva.ToString("X"));
            patch.Generate((assembler, _) =>
            {
                assembler.AddInstructions(new[] { jump });
                // Keep the audited 16/18-byte spans of the two existing entry hooks.
                if (plan.EntryKind != 0)
                    for (int index = 6; index < plan.PhysicalLength; index++) assembler.nop();
            });
            if (patch.OverwrittenByteCount != plan.PhysicalLength || patch.TargetAddress != target)
                throw new InvalidOperationException("Native switch backend displacement changed.");
            return patch;
        }

        // Entry of a published detour or inline hook: jmp rel32 (E9) or jmp [rip+disp32] (FF 25).
        private static bool StartsForeignJump(byte[] live) =>
            live.Length >= 1 && live[0] == 0xE9 || live.Length >= 2 && live[0] == 0xFF && live[1] == 0x25;

        private static void Write(IntPtr address, byte[] bytes)
        {
            if (!NativeMemoryManager.WriteStub(address, bytes))
                throw new InvalidOperationException("Native switch unpublished storage write failed.");
        }

        internal static byte[] BuildGuard(ulong ip, ulong demandAddress, ulong dormant, ulong active)
        {
            var a = new Assembler(64); var off = a.CreateLabel();
            a.push(rcx); a.mov(rcx, demandAddress); a.movzx(ecx, __byte_ptr[rcx]); a.jrcxz(off);
            a.pop(rcx); a.jmp(active);
            a.Label(ref off); a.pop(rcx); a.jmp(dormant);
            return Assemble(a, ip);
        }

        internal static byte[] BuildBlock(NativeSwitchPlan plan, ulong imageBase, ulong ip, bool active)
        {
            var a = new Assembler(64);
            if (active && plan.EntryKind != 0)
            {
                ulong peace = imageBase + VanillaPeaceTimeNativeContract.PeaceTimeActiveFlagRva;
                if (plan.EntryKind == 1) VanillaPeaceTimeNativeContract.EmitStartingTroopsGuardPrefix(a, peace);
                else VanillaPeaceTimeNativeContract.EmitWildlifeGuardPrefix(a, peace);
            }
            byte[] source = active ? plan.Active : plan.Original.Take(plan.PhysicalLength).ToArray();
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(source)); decoder.IP = imageBase + (uint)plan.Rva;
            var instructions = new List<Instruction>();
            ulong end = decoder.IP + (uint)source.Length;
            while (decoder.IP < end)
            {
                Instruction instruction = decoder.Decode();
                if (instruction.IsInvalid || decoder.IP > end)
                    throw new InvalidOperationException("Native switch block contains an incomplete instruction.");
                instructions.Add(instruction);
            }
            a.AddInstructions(instructions.ToArray());
            a.jmp(end);
            return Assemble(a, ip);
        }

        internal static byte[] Assemble(Assembler assembler, ulong ip)
        {
            using (var stream = new MemoryStream())
            {
                assembler.Assemble(new StreamCodeWriter(stream), ip);
                return stream.ToArray();
            }
        }
    }

    internal sealed class NativeSwitchPlan
    {
        internal NativeSwitchPlan(int rva, int physicalLength, byte[] original, byte[] active, int entryKind, byte[] following)
        { Rva = rva; PhysicalLength = physicalLength; Original = original; Active = active; EntryKind = entryKind; Following = following; }
        internal int Rva { get; }
        internal int PhysicalLength { get; }
        internal byte[] Original { get; }
        internal byte[] Active { get; }
        internal int EntryKind { get; }
        internal byte[] Following { get; }
    }
}
