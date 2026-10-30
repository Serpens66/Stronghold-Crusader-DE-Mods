using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Iced.Intel;
using RedBird.Core.Memory;
using SHCDESE.Interop;
using static Iced.Intel.AssemblerRegisters;

namespace ExtraFeatures
{
    // Private, read-only predicate. No live game instructions are patched. The seventh
    // Win64 argument is a civilian squared radius; the first six retain Vanilla's ABI.
    internal sealed class EnemyProximityQuery
    {
        internal const int Rva = 0xEE640, Length = 502;
        internal const string BodyHash = "1D2DDB19C37B3EC505D426272421E57FE4336CAA2BF9FA80BE6F1143282C565D";
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate long QueryDelegate(IntPtr unused, int player, int x, int y,
            int militaryRadius, byte unknown, int civilianSquaredRadius);
        private readonly QueryDelegate query;
        // Published allocations and delegates live as long as the event's runtime owner.
        private readonly IntPtr allocation;

        internal EnemyProximityQuery(ReadOnlySpan<byte> image, ulong module)
        {
            byte[] body = image.Slice(Rva, Length).ToArray();
            Validate(body);
            allocation = NativeMemoryManager.AllocateStub(4096);
            if (allocation == IntPtr.Zero) throw new InvalidOperationException("Enemy proximity helper allocation failed.");
            ulong address = unchecked((ulong)allocation.ToInt64());
            byte[] bytes = Build(body, module, address, module, true);
            if (bytes.Length > 3072) throw new InvalidOperationException("Enemy proximity helper exceeds its allocation.");
            var buffer = new byte[4096];
            Array.Copy(bytes, buffer, bytes.Length);
            byte[] categories = Categories();
            Array.Copy(categories, 0, buffer, 3072, categories.Length);
            // One unpublished write through the installed allocator also flushes the
            // instruction cache. Shared slabs cannot be freed or finalized per helper.
            if (!NativeMemoryManager.WriteStub(allocation, buffer))
                throw new InvalidOperationException("Enemy proximity helper preparation failed.");
            query = (QueryDelegate)Marshal.GetDelegateForFunctionPointer(allocation, typeof(QueryDelegate));
        }

        internal long Invoke(int player, int x, int y, int military, byte unknown, int civilian) =>
            query(IntPtr.Zero, player, x, y, military, unknown, checked(civilian * civilian));

        internal static void Validate(byte[] body)
        {
            using (var hash = SHA256.Create())
                if (body.Length != Length || BitConverter.ToString(hash.ComputeHash(body)).Replace("-", "") != BodyHash)
                    throw new InvalidOperationException("The complete Vanilla enemy proximity predicate differs.");
        }

        internal static byte[] Categories()
        {
            var result = new byte[(int)eChimps.CHIMP_NUM_TYPES];
            foreach (eChimps value in Enum.GetValues(typeof(eChimps)))
                if ((int)value < result.Length && IsCivilian(value)) result[(int)value] = 1;
            return result;
        }

        internal static bool IsCivilian(eChimps value)
        {
            switch (value)
            {
                case eChimps.CHIMP_TYPE_PEASANT:
                case eChimps.CHIMP_TYPE_WOODCUTTER:
                case eChimps.CHIMP_TYPE_FLETCHER:
                case eChimps.CHIMP_TYPE_HUNTER:
                case eChimps.CHIMP_TYPE_QUARRY_MASON:
                case eChimps.CHIMP_TYPE_QUARRY_GRUNT:
                case eChimps.CHIMP_TYPE_QUARRY_OX:
                case eChimps.CHIMP_TYPE_PITCHMAN:
                case eChimps.CHIMP_TYPE_FARMER_WHEAT:
                case eChimps.CHIMP_TYPE_FARMER_HOPS:
                case eChimps.CHIMP_TYPE_FARMER_APPLE:
                case eChimps.CHIMP_TYPE_FARMER_CATTLE:
                case eChimps.CHIMP_TYPE_MILLER:
                case eChimps.CHIMP_TYPE_BAKER:
                case eChimps.CHIMP_TYPE_BREWER:
                case eChimps.CHIMP_TYPE_POLETURNER:
                case eChimps.CHIMP_TYPE_BLACKSMITH:
                case eChimps.CHIMP_TYPE_ARMOURER:
                case eChimps.CHIMP_TYPE_TANNER:
                case eChimps.CHIMP_TYPE_MINER1:
                case eChimps.CHIMP_TYPE_MINER2:
                case eChimps.CHIMP_TYPE_PRIEST:
                case eChimps.CHIMP_TYPE_HEALER:
                case eChimps.CHIMP_TYPE_DRUNKARD:
                case eChimps.CHIMP_TYPE_INNKEEPER:
                case eChimps.CHIMP_TYPE_TRADER:
                case eChimps.CHIMP_TYPE_TRADER_HORSE:
                case eChimps.CHIMP_TYPE_FIREMAN:
                case eChimps.CHIMP_TYPE_LADY:
                case eChimps.CHIMP_TYPE_JESTER:
                case eChimps.CHIMP_TYPE_MOTHER:
                case eChimps.CHIMP_TYPE_CHILD:
                case eChimps.CHIMP_TYPE_JUGGLER:
                case eChimps.CHIMP_TYPE_FIREEATER: return true;
                default: return false;
            }
        }

        // Re-encode every original instruction, retaining IP identities for internal
        // branch relocation. Only CMP R8D,EDI changes. RCX/RDX are dead there; RAX,
        // R8D (distance), all loop registers and the following JGE stay intact.
        // Absolute data anchors remove the need for a near-module allocation.
        internal static byte[] Build(byte[] body, ulong module, ulong address, ulong dataModule, bool split)
        {
            Validate(body);
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(body));
            decoder.IP = module + Rva;
            var instructions = new List<Instruction>();
            ulong synthetic = 0x7000000000000000;
            while (decoder.IP < module + Rva + Length)
            {
                Instruction original = decoder.Decode();
                if (original.IsInvalid) throw new InvalidOperationException("Invalid proximity instruction.");
                var a = new Assembler(64);
                bool replace = false;
                if (original.IsIPRelativeMemoryOperand)
                {
                    ulong target = checked(original.IPRelativeMemoryAddress - module + dataModule);
                    if (original.Mnemonic == Mnemonic.Lea && original.Op0Register == Register.R12) a.mov(r12, target);
                    else if (original.Mnemonic == Mnemonic.Lea && original.Op0Register == Register.RAX) a.mov(rax, target);
                    else if (original.Mnemonic == Mnemonic.Mov && original.Op0Register == Register.EBP)
                    { a.mov(rbp, target); a.mov(ebp, __dword_ptr[rbp]); }
                    else throw new InvalidOperationException("Unknown proximity data anchor.");
                    replace = true;
                }
                else if (split && original.IP == module + 0xEE748)
                {
                    if (original.Mnemonic != Mnemonic.Cmp || original.Op0Register != Register.R8D || original.Op1Register != Register.EDI)
                        throw new InvalidOperationException("Proximity distance boundary changed.");
                    var military = a.CreateLabel(); var compare = a.CreateLabel();
                    a.movzx(ecx, __word_ptr[rax + r12 + 0x67E8AE6]); // same type word as Vanilla at EE791
                    a.cmp(ecx, (int)eChimps.CHIMP_NUM_TYPES);
                    a.jae(military);
                    a.mov(rdx, address + 3072);
                    a.cmp(__byte_ptr[rdx + rcx], 1);
                    a.jne(military);
                    a.mov(ecx, __dword_ptr[rsp + 0x58]); // seventh argument, after four saved registers
                    a.jmp(compare);
                    a.Label(ref military); a.mov(ecx, edi);
                    a.Label(ref compare); a.cmp(r8d, ecx);
                    replace = true;
                }
                if (!replace) { instructions.Add(original); continue; }
                using (var stream = new MemoryStream())
                {
                    a.Assemble(new StreamCodeWriter(stream), synthetic);
                    var d = Decoder.Create(64, new ByteArrayCodeReader(stream.ToArray())); d.IP = synthetic;
                    bool first = true;
                    while (d.IP < synthetic + (ulong)stream.Length)
                    {
                        Instruction i = d.Decode();
                        if (first) { i.IP = original.IP; first = false; }
                        instructions.Add(i);
                    }
                    synthetic += 0x1000;
                }
            }
            using (var stream = new MemoryStream())
            {
                if (!BlockEncoder.TryEncode(64, new InstructionBlock(new StreamCodeWriter(stream), instructions, address), out string error, out BlockEncoderResult result))
                    throw new InvalidOperationException(error);
                return stream.ToArray();
            }
        }

    }
}
