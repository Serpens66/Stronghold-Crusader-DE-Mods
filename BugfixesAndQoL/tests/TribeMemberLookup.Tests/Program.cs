using BugfixesAndQoL.VanillaPerformance;
using Iced.Intel;
using RedBird.X64.Hooks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using static Iced.Intel.AssemblerRegisters;

namespace BugfixesAndQoL.TribeMemberLookupTests
{
    // V14 contract: the hooked 0x119F90 answers exactly like Vanilla's machine code. Vanilla's body is copied
    // from the installed CrusaderDE.dll (it has no RIP-relative operand and no call, so a copy runs
    // unchanged); a second copy receives the production stub through the same RedBird X64InlineHook backend.
    // Both run through one harness on random and edge-case tribe records, with the flag on and off, and the
    // harness checks the registers the stub must keep (r8 like Vanilla, rbx as callee-saved).
    internal static unsafe class Program
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr size, uint allocation, uint protection);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FlushInstructionCache(IntPtr process, IntPtr address, UIntPtr size);
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int Harness(IntPtr target, IntPtr manager, int tribe, int index, IntPtr output);
        private static readonly List<object> Roots = new List<object>();
        private const ulong Sentinel = 0x5A5A123456789ABCUL;
        private const int Tribes = 6, PageBytes = 0x4000, OriginalOffset = 0, HookedOffset = 0x1000, HarnessOffset = 0x2000;
        private static int checks;

        private static int Main(string[] args)
        {
            try
            {
                string dll = args.Length > 0 && File.Exists(args[0]) ? args[0] : Path.Combine(
                    @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition",
                    "Stronghold Crusader Definitive Edition_Data", "Plugins", "x86_64", "CrusaderDE.dll");
                byte[] body = ReadBody(File.ReadAllBytes(dll));
                AuditBody(body);
                int cases = RunMachineCases(body);
                Console.WriteLine($"PASS: tribe member lookup V14: {cases} cases identical to Vanilla's 0x119F90 copy " +
                    $"(flag on/off), r8/rbx kept, actual X64InlineHook span; {checks} checks.");
                return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }

        private static void Require(bool condition, string message) { checks++; if (!condition) throw new InvalidOperationException(message); }

        private static byte[] ReadBody(byte[] image)
        {
            int pe = BitConverter.ToInt32(image, 0x3C), sections = BitConverter.ToUInt16(image, pe + 6);
            int table = pe + 24 + BitConverter.ToUInt16(image, pe + 20);
            for (int i = 0; i < sections; i++)
            {
                int entry = table + i * 40;
                uint va = BitConverter.ToUInt32(image, entry + 12), size = BitConverter.ToUInt32(image, entry + 16), raw = BitConverter.ToUInt32(image, entry + 20);
                if (TribeMemberLookupCode.Rva < va || TribeMemberLookupCode.Rva + TribeMemberLookupCode.BodyLength > va + size) continue;
                byte[] body = new byte[TribeMemberLookupCode.BodyLength];
                Array.Copy(image, raw + (TribeMemberLookupCode.Rva - va), body, 0, body.Length);
                using (var sha = SHA256.Create())
                    Require(BitConverter.ToString(sha.ComputeHash(body)).Replace("-", "") == TribeMemberLookupCode.BodySha256,
                        "installed 0x119F90 body differs from the audited bytes");
                return body;
            }
            throw new InvalidOperationException("0x119F90 is outside every section.");
        }

        // The hook span (18 bytes) is four whole instructions without RIP-relative operands, and no branch of
        // the body enters it; the body itself contains no call and no RIP-relative access.
        private static void AuditBody(byte[] body)
        {
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(body));
            decoder.IP = TribeMemberLookupCode.Rva;
            int spanInstructions = 0;
            for (int i = 0; i < TribeMemberLookupCode.DisplacedLength; i++)
                Require(body[i] == TribeMemberLookupCode.DisplacedBytes[i], "displaced bytes");
            while (decoder.IP < (ulong)(TribeMemberLookupCode.Rva + TribeMemberLookupCode.BodyLength))
            {
                decoder.Decode(out Instruction instruction);
                Require(!instruction.IsInvalid, "valid instruction in body");
                Require(!instruction.IsIPRelativeMemoryOperand && instruction.FlowControl != FlowControl.Call &&
                    instruction.FlowControl != FlowControl.IndirectBranch, "position-independent body without calls");
                if (instruction.IP < (ulong)(TribeMemberLookupCode.Rva + TribeMemberLookupCode.DisplacedLength))
                {
                    spanInstructions++;
                    Require(instruction.NextIP <= (ulong)(TribeMemberLookupCode.Rva + TribeMemberLookupCode.DisplacedLength), "span ends on an instruction boundary");
                }
                if (instruction.FlowControl == FlowControl.ConditionalBranch || instruction.FlowControl == FlowControl.UnconditionalBranch)
                {
                    ulong target = instruction.NearBranchTarget;
                    Require(target <= TribeMemberLookupCode.Rva || target >= (ulong)(TribeMemberLookupCode.Rva + TribeMemberLookupCode.DisplacedLength),
                        "no branch into the hook span");
                }
            }
            Require(spanInstructions == 4, "four displaced instructions");
        }

        private static int RunMachineCases(byte[] body)
        {
            IntPtr page = VirtualAlloc(IntPtr.Zero, (UIntPtr)PageBytes, 0x3000, 0x40);
            Require(page != IntPtr.Zero, "executable test allocation"); Roots.Add(page);
            ulong ip = unchecked((ulong)page.ToInt64());
            Marshal.Copy(body, 0, page + OriginalOffset, body.Length);
            Marshal.Copy(body, 0, page + HookedOffset, body.Length);
            IntPtr flag = Marshal.AllocHGlobal(4); Marshal.WriteInt32(flag, 1);
            ulong hookedIp = ip + HookedOffset;
            var hook = new X64InlineHook(hookedIp, TribeMemberLookupCode.DisplacedLength);
            Require(hook.DisplacedByteCount == TribeMemberLookupCode.DisplacedLength && hook.TargetAddress == hookedIp, "actual RedBird span/target");
            hook.Generate((a, original, ret) => TribeMemberLookupCode.Emit(a, unchecked((ulong)flag.ToInt64()), original));
            hook.Enable(); Roots.Add(hook);
            byte* patch = (byte*)page + HookedOffset;
            Require(patch[0] == 0xFF && patch[1] == 0x25 && *(uint*)(patch + 2) == 0 && *(ulong*)(patch + 6) != 0, "actual absolute-indirect patch");
            for (int i = 14; i < TribeMemberLookupCode.DisplacedLength; i++) Require(patch[i] == 0x90, "actual padding");
            byte[] harness = AssembleHarness(ip + HarnessOffset);
            Marshal.Copy(harness, 0, page + HarnessOffset, harness.Length);
            Require(FlushInstructionCache(GetCurrentProcess(), page, (UIntPtr)PageBytes), "instruction cache flush");
            var call = (Harness)Marshal.GetDelegateForFunctionPointer(page + HarnessOffset, typeof(Harness)); Roots.Add(call);

            int stride = TribeMemberLookupCode.TribeStride;
            IntPtr manager = Marshal.AllocHGlobal(Tribes * stride), output = Marshal.AllocHGlobal(16);
            var random = new Random(119);
            int cases = 0;
            try
            {
                for (int round = 0; round < 1500; round++)
                {
                    byte* tribe = (byte*)manager + (round % Tribes) * stride;
                    int setBits = FillTribe(tribe, random, round);
                    short count = *(short*)(tribe + TribeMemberLookupCode.CountOffset);
                    var indices = new List<int> { int.MinValue, -1, 0, 1, count - 1, count, count + 1, setBits - 1, setBits, setBits + 1, int.MaxValue };
                    for (int i = 0; i < 6; i++) indices.Add(random.Next(-2, Math.Max(2, Math.Max(count, setBits) + 3)));
                    foreach (int index in indices)
                    {
                        int expected = Run(call, page + OriginalOffset, manager, round % Tribes, index, output);
                        Marshal.WriteInt32(flag, 1);
                        int fast = Run(call, page + HookedOffset, manager, round % Tribes, index, output);
                        Marshal.WriteInt32(flag, 0);
                        int vanilla = Run(call, page + HookedOffset, manager, round % Tribes, index, output);
                        Require(fast == expected, $"fast lookup {fast} != Vanilla {expected} (round {round}, index {index}, count {count}, bits {setBits})");
                        Require(vanilla == expected, "disabled stub differs from Vanilla");
                        cases++;
                    }
                }
            }
            finally { Marshal.FreeHGlobal(manager); Marshal.FreeHGlobal(output); }
            return cases;
        }

        private static int Run(Harness call, IntPtr target, IntPtr manager, int tribe, int index, IntPtr output)
        {
            int result = call(target, manager, tribe, index, output);
            Require(unchecked((ulong)Marshal.ReadInt64(output)) == (uint)index, "r8 kept like Vanilla");
            Require(unchecked((ulong)Marshal.ReadInt64(output, 8)) == Sentinel, "rbx kept");
            return result;
        }

        // Tribe kinds, signed counts and member layouts that cover empty, sparse, dense, full, the first and the
        // last word, sign bits and unit slot 0. Returns the number of set member bits.
        private static int FillTribe(byte* tribe, Random random, int round)
        {
            for (int i = 0; i < TribeMemberLookupCode.TribeStride; i++) tribe[i] = (byte)random.Next(256);
            short[] kinds = { 2, 2, 2, 2, 0, 1, 3, -2 };
            *(short*)(tribe + TribeMemberLookupCode.KindOffset) = kinds[round % kinds.Length];
            ushort* words = (ushort*)(tribe + TribeMemberLookupCode.WordsOffset);
            int layout = round % 9, bits = 0;
            for (int w = 0; w < TribeMemberLookupCode.WordCount; w++)
            {
                ushort value = 0;
                switch (layout)
                {
                    case 0: break;
                    case 1: for (int b = 0; b < 16; b++) if (random.NextDouble() < 0.03) value |= (ushort)(1 << b); break;
                    case 2: value = (ushort)random.Next(65536); break;
                    case 3: value = 0xFFFF; break;
                    case 4: value = w == 0 ? (ushort)1 : (ushort)0; break;
                    case 5: value = w == TribeMemberLookupCode.WordCount - 1 ? (ushort)random.Next(1, 65536) : (ushort)0; break;
                    case 6: value = (ushort)(random.NextDouble() < 0.1 ? 0x8000 : 0); break;
                    case 7: value = w >= 600 ? (ushort)random.Next(65536) : (ushort)0; break;
                    default: value = random.NextDouble() < 0.2 ? (ushort)(1 << random.Next(16)) : (ushort)0; break;
                }
                words[w] = value;
                for (int b = 0; b < 16; b++) if ((value >> b & 1) != 0) bits++;
            }
            short[] counts = { 0, 1, -1, short.MinValue, short.MaxValue };
            short count = round % 4 == 0 ? counts[random.Next(counts.Length)] :
                (short)Math.Min(short.MaxValue, Math.Max(0, bits + random.Next(-3, 4)));
            *(short*)(tribe + TribeMemberLookupCode.CountOffset) = count;
            return bits;
        }

        // rcx = target, rdx = manager, r8d = tribe, r9d = index, [rsp+0x28] = output (r8, rbx after the call).
        private static byte[] AssembleHarness(ulong ip)
        {
            var a = new Assembler(64);
            a.push(rbx); a.push(r12); a.sub(rsp, 0x28);
            a.mov(r12, __qword_ptr[rsp + 0x60]);
            a.mov(rax, rcx); a.mov(rcx, rdx); a.mov(edx, r8d); a.mov(r8d, r9d);
            a.mov(rbx, Sentinel);
            a.call(rax);
            a.mov(__qword_ptr[r12], r8); a.mov(__qword_ptr[r12 + 8], rbx);
            a.add(rsp, 0x28); a.pop(r12); a.pop(rbx); a.ret();
            var writer = new BytesWriter();
            a.Assemble(writer, ip);
            return writer.ToArray();
        }

        private sealed class BytesWriter : CodeWriter
        {
            private readonly List<byte> bytes = new List<byte>();
            public override void WriteByte(byte value) => bytes.Add(value);
            internal byte[] ToArray() => bytes.ToArray();
        }
    }
}
