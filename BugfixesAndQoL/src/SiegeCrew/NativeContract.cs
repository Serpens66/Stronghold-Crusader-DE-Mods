using System;
using System.Runtime.InteropServices;
using Iced.Intel;
using RedBird.X64.Extensions;
using static Iced.Intel.AssemblerRegisters;

namespace BugfixesAndQoL.SiegeCrew
{
    internal static class NativeContract
    {
        internal const string NativeHash = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        internal const int CrewTimeoutOffset = 0x974; // Relative to unit manager + one-based ID * 0x490.

        internal static readonly Site[] TimeoutSites =
        {
            new Site("Arab ballista", 0xA68EF, "664689B429180900004288B42986090000", "664239B429B0090000", r13),
            new Site("Catapult", 0xA916A, "6646898C29180900004288B42986090000", "664239B429B0090000", r13),
            new Site("Trebuchet", 0xA97F6, "6646898C311809000042C684318609000000", "7515", r14)
        };

        internal sealed class Site
        {
            internal Site(string name, int rva, string original, string continuation, AssemblerRegister64 manager)
            {
                Name = name;
                Rva = rva;
                Original = DecodeHex(original);
                Continuation = DecodeHex(continuation);
                Manager = manager;
            }

            internal string Name { get; }
            internal int Rva { get; }
            internal byte[] Original { get; }
            internal byte[] Continuation { get; }
            internal AssemblerRegister64 Manager { get; }
            internal int End => Rva + Original.Length;

            internal void Verify(ReadOnlySpan<byte> original, ReadOnlySpan<byte> continuation, ReadOnlySpan<byte> live)
            {
                if (!original.SequenceEqual(Original) || !continuation.SequenceEqual(Continuation))
                    throw new InvalidOperationException($"{Name} load-image contract differs at RVA 0x{Rva:X}.");
                if (!live.SequenceEqual(Original))
                    throw new InvalidOperationException($"{Name} live span is occupied at RVA 0x{Rva:X}.");
            }
        }

        internal static void EmitTimeout(Assembler assembler, Site site, ulong activation, ReadOnlySpan<Instruction> original)
        {
            // RCX is the slot displacement. Both native MOVs and the Tribok's
            // following JNE must see the original registers and flags.
            Label vanilla = assembler.CreateLabel();
            assembler.pushfq();
            assembler.push(rax);
            assembler.mov(rax, activation);
            assembler.cmp(__dword_ptr[rax], 0);
            assembler.je(vanilla);
            assembler.mov(__dword_ptr[site.Manager + rcx + CrewTimeoutOffset], 0);
            assembler.Label(ref vanilla);
            assembler.pop(rax);
            assembler.popfq();
            assembler.AddInstructions(original);
        }

        internal static void Publish(IntPtr activation, Action installAndVerify, bool enabled)
        {
            // Leave even partially published stubs dormant if installation throws.
            installAndVerify();
            SetActivation(activation, true, enabled);
        }

        internal static void SetActivation(IntPtr activation, bool enableMod, bool enableFix)
        {
            if (activation != IntPtr.Zero)
                Marshal.WriteInt32(activation, enableMod && enableFix ? 1 : 0);
        }

        private static byte[] DecodeHex(string hex)
        {
            var bytes = new byte[hex.Length / 2];
            for (int index = 0; index < bytes.Length; index++)
                bytes[index] = Convert.ToByte(hex.Substring(index * 2, 2), 16);
            return bytes;
        }
    }
}
