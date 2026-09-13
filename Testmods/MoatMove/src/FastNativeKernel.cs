using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Iced.Intel;
using RedBird.Core.Memory;

namespace MoatMove
{
    // Private relocatable copy; never Enable a hook or write a game address.
    internal sealed class FastNativeKernel
    {
        internal const int SourceRva = 0xDA590, SourceLength = 0x4BC;
        private const string SourceHash = "1F1DFB439C437557B2626E2634AEC8936E583EE1C951A43BB3BAE281C28F043E";
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate int Flood(IntPtr slab, int x, int y, int targetX, int targetY, int limit, int continuation);
        private static readonly Dictionary<long, FastNativeKernel> roots = new Dictionary<long, FastNativeKernel>();
        private static byte[] source;
        private static ulong allocationAnchor;
        internal readonly Layout Memory;
        internal readonly Flood Run;
        internal readonly byte[] CodeBytes;
        internal readonly IntPtr Address;

        internal sealed class Layout
        {
            internal readonly int Width, Height, Count, Guard, Rows, Steps, Availability, Mask, Distance, Stamp, Queue, QueueY, Bytes;
            internal Layout(int width, int height)
            {
                if (width < 1 || height < 1 || width > 800 || height > 800) throw new ArgumentOutOfRangeException(nameof(width));
                Width = width; Height = height; Count = checked(width * height); Guard = width + 1;
                Rows = 64; Steps = Rows + height * 12;
                Availability = Steps + height * 32; Mask = Availability + Count;
                int offset = (Mask + Count + 7) & ~7;
                Distance = offset + Guard * 4; offset += (Count + 2 * Guard) * 4;
                Stamp = offset + Guard * 2; offset += (Count + 2 * Guard) * 2;
                Queue = (offset + 7) & ~7; QueueY = Queue + Count * 4;
                Bytes = QueueY + Count * 2;
            }
        }

        internal static void Initialize(byte[] body, ulong anchor)
        {
            using (var sha = SHA256.Create())
                if (body == null || body.Length != SourceLength ||
                    BitConverter.ToString(sha.ComputeHash(body)).Replace("-", "") != SourceHash)
                    throw new InvalidOperationException("FastNative DA590 source contract mismatch.");
            if (source == null) { source = (byte[])body.Clone(); allocationAnchor = anchor; }
        }

        internal static FastNativeKernel Get(int width, int height)
        {
            long key = ((long)width << 32) | (uint)height;
            lock (roots)
            {
                if (!roots.TryGetValue(key, out FastNativeKernel kernel))
                {
                    if (source == null) throw new InvalidOperationException("FastNative was not initialized.");
                    kernel = new FastNativeKernel(width, height);
                    roots.Add(key, kernel);
                }
                return kernel;
            }
        }

        private FastNativeKernel(int width, int height)
        {
            Memory = new Layout(width, height);
            var instructions = Transform(source, Memory);
            // No external data/branches remain: relocation size is position-independent.
            byte[] measured = Encode(instructions, 0x10000000);
            Validate(measured, 0x10000000);
            Address = NativeMemoryManager.AllocateStub(allocationAnchor, measured.Length + 64);
            if (Address == IntPtr.Zero) throw new InvalidOperationException("FastNative executable allocation failed.");
            CodeBytes = Encode(instructions, unchecked((ulong)Address.ToInt64()));
            if (CodeBytes.Length > measured.Length + 64) throw new InvalidOperationException("FastNative relocation grew beyond allocation.");
            Validate(CodeBytes, unchecked((ulong)Address.ToInt64()));
            if (!NativeMemoryManager.WriteStub(Address, CodeBytes)) throw new InvalidOperationException("FastNative executable publication failed.");
            Run = (Flood)Marshal.GetDelegateForFunctionPointer(Address, typeof(Flood));
        }

        private static Register Wide(Register register)
        {
            switch (register)
            {
                case Register.R9W: return Register.R9D;
                case Register.R10W: return Register.R10D;
                case Register.CX: return Register.ECX;
                default: throw new InvalidOperationException("Unexpected distance register " + register);
            }
        }

        internal static List<Instruction> Transform(byte[] body, Layout layout)
        {
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(body)); decoder.IP = SourceRva;
            var result = new List<Instruction>();
            while (decoder.IP < (ulong)SourceRva + (ulong)body.Length)
            {
                decoder.Decode(out Instruction instruction);
                ulong ip = instruction.IP;
                if (instruction.Code == Code.INVALID) throw new InvalidOperationException("Invalid DA590 instruction.");
                if (ip == 0xDA64A)
                    instruction = Instruction.CreateBranch(Code.Jmp_rel32_64, 0xDA6AF);
                else if (ip >= 0xDA652 && ip < 0xDA6AF)
                    instruction = Instruction.Create(Code.Nopd);
                else if (ip == 0xDA710)
                    instruction = Instruction.Create(Code.Mov_r32_imm32, Register.R10D, uint.MaxValue);
                else if (ip == 0xDA87A || ip == 0xDA8C3 || ip == 0xDA959)
                    instruction = Instruction.Create(Code.Mov_r32_rm32, Register.ECX, Register.R9D);
                else if (ip == 0xDA9A1)
                    instruction = Instruction.Create(Code.Nopd);
                else if (ip == 0xDA778)
                    instruction = Instruction.Create(Code.Inc_rm32, Register.R9D);
                else if (instruction.IsIPRelativeMemoryOperand)
                {
                    if (instruction.Mnemonic != Mnemonic.Lea || instruction.IPRelativeMemoryAddress != 0)
                        throw new InvalidOperationException("Unexpected DA590 RIP reference at " + ip.ToString("X"));
                    instruction = Instruction.Create(Code.Mov_r64_rm64, instruction.Op0Register, Register.RBX);
                }
                else
                {
                    if (ip == 0xDA5B5 || ip == 0xDA5FE) instruction.Immediate32 = (uint)(layout.Width - 1);
                    if (ip == 0xDA5C2 || ip == 0xDA606) instruction.Immediate32 = (uint)(layout.Height - 1);
                    if (ip == 0xDA5CF || ip == 0xDA60E) instruction.Immediate32 = (uint)layout.Width;
                    long d = (long)instruction.MemoryDisplacement64;
                    bool memory = false;
                    for (int op = 0; op < instruction.OpCount; op++) memory |= instruction.GetOpKind(op) == OpKind.Memory;
                    if (memory && instruction.MemoryBase != Register.RSP)
                    {
                        long mapped = d;
                        if (d >= 0x5225B0E && d <= 0x5225B12)
                        {
                            mapped = layout.Distance + (d - 0x5225B10) * 2;
                            instruction.MemoryIndexScale = 4;
                            if (instruction.Code == Code.Movzx_r32_rm16) instruction.Code = Code.Mov_r32_rm32;
                            else if (instruction.Code == Code.Mov_rm16_r16)
                            { instruction.Code = Code.Mov_rm32_r32; instruction.Op1Register = Wide(instruction.Op1Register); }
                            else throw new InvalidOperationException("Unexpected distance access.");
                        }
                        else if (d >= 0x52C254E && d <= 0x52C2552) mapped = layout.Stamp + d - 0x52C2550;
                        else if (d == 0x51890D0) mapped = layout.Mask;
                        else if (d == 0x402FF2C) mapped = layout.Rows;
                        else if (d == 0x405EDB0 || d == 0x405EDC0) mapped = layout.Steps + d - 0x405EDB0;
                        else if (d == 0x3A11EA4) mapped = layout.Availability;
                        else if (d == 0x155F44) mapped = 8;
                        else if (d == 0x155F3C) mapped = 12;
                        else if (d == 0x155F58) mapped = 16;
                        else if (d == 0x155F6C) mapped = layout.Queue;
                        else if (d == 0x28F3EC) mapped = layout.QueueY;
                        else if (d > 0x10000) throw new InvalidOperationException("Unmapped DA590 data operand.");
                        instruction.MemoryDisplacement64 = unchecked((ulong)mapped);
                    }
                }
                instruction.IP = ip;
                result.Add(instruction);
            }
            return result;
        }

        private static byte[] Encode(List<Instruction> instructions, ulong address)
        {
            using (var stream = new MemoryStream())
            {
                var block = new InstructionBlock(new StreamCodeWriter(stream), instructions, address);
                if (!BlockEncoder.TryEncode(64, block, out string error, out _))
                    throw new InvalidOperationException("FastNative relocation: " + error);
                return stream.ToArray();
            }
        }

        private static void Validate(byte[] bytes, ulong address)
        {
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes)); decoder.IP = address;
            var boundaries = new HashSet<ulong>(); var targets = new List<ulong>();
            while (decoder.IP < address + (ulong)bytes.Length)
            {
                decoder.Decode(out Instruction instruction); boundaries.Add(instruction.IP);
                if (instruction.Code == Code.INVALID || instruction.IsIPRelativeMemoryOperand ||
                    instruction.FlowControl == FlowControl.Call || instruction.FlowControl == FlowControl.IndirectCall ||
                    instruction.FlowControl == FlowControl.IndirectBranch)
                    throw new InvalidOperationException("FastNative residual external instruction.");
                if (instruction.FlowControl == FlowControl.ConditionalBranch || instruction.FlowControl == FlowControl.UnconditionalBranch)
                    targets.Add(instruction.NearBranchTarget);
            }
            foreach (ulong target in targets) if (!boundaries.Contains(target))
                throw new InvalidOperationException("FastNative branch outside private instruction boundaries.");
        }
    }
}
