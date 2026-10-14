using System;
using System.Runtime.InteropServices;
using Iced.Intel;

namespace PlagueHeightTest
{
    // Memory is written and made executable once, before any hook can reach it.
    internal sealed class PreparedNativeCode
    {
        internal IntPtr Address { get; private set; }
        internal byte[] Bytes { get; private set; }
        private bool published;
        internal PreparedNativeCode(Action<Assembler> generate)
        {
            Address = VirtualAlloc(IntPtr.Zero, (UIntPtr)4096, 0x3000, 0x04);
            if (Address == IntPtr.Zero) throw new InvalidOperationException("Native helper allocation failed.");
            try
            {
                var assembler = new Assembler(64); generate(assembler);
                var bytes = PlagueHeightAssembly.Assemble(assembler, unchecked((ulong)Address.ToInt64()));
                Bytes = bytes;
                if (bytes.Length > 4096) throw new InvalidOperationException("Native helper exceeds allocation.");
                Marshal.Copy(bytes, 0, Address, bytes.Length);
                if (!VirtualProtect(Address, (UIntPtr)4096, 0x20, out uint previous) ||
                    !FlushInstructionCache(GetCurrentProcess(), Address, (UIntPtr)bytes.Length))
                    throw new InvalidOperationException("Native helper preparation failed.");
            }
            catch { RollbackUnpublished(); throw; }
        }
        internal void Publish() { published = true; }
        internal void RollbackUnpublished()
        {
            if (published) throw new InvalidOperationException("Published native helper cannot be freed.");
            if (Address != IntPtr.Zero) { VirtualFree(Address, UIntPtr.Zero, 0x8000); Address = IntPtr.Zero; }
        }
        [DllImport("kernel32", SetLastError = true)] private static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr size, uint allocation, uint protection);
        [DllImport("kernel32", SetLastError = true)] private static extern bool VirtualProtect(IntPtr address, UIntPtr size, uint protection, out uint previous);
        [DllImport("kernel32", SetLastError = true)] private static extern bool VirtualFree(IntPtr address, UIntPtr size, uint freeType);
        [DllImport("kernel32")] private static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32", SetLastError = true)] private static extern bool FlushInstructionCache(IntPtr process, IntPtr address, UIntPtr size);
    }
}
