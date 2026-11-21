using System;
using System.Runtime.InteropServices;

namespace Shared
{
    // Bounded reads of native memory whose size is not published, such as RedBird stubs packed into
    // 64-KiB slabs: a fixed-size read from a stub near a slab end would fault on the next page.
    internal static class CommittedMemory
    {
        private const uint MemCommit = 0x1000;
        private const uint PageNoAccess = 0x01;
        private const uint PageGuard = 0x100;

        // Returns at most maxBytes, never past the committed, readable region containing address.
        internal static byte[] ReadUpTo(IntPtr address, int maxBytes)
        {
            if (address == IntPtr.Zero) throw new ArgumentException("Address is null.", nameof(address));
            if (maxBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maxBytes));
            if (VirtualQuery(address, out MemoryBasicInformation region,
                    (UIntPtr)Marshal.SizeOf<MemoryBasicInformation>()) == UIntPtr.Zero ||
                region.State != MemCommit || (region.Protect & (PageNoAccess | PageGuard)) != 0 ||
                region.Protect == 0)
                throw new InvalidOperationException("Native memory at 0x" + address.ToInt64().ToString("X") +
                    " is not committed readable memory.");
            ulong start = unchecked((ulong)address.ToInt64());
            ulong end = unchecked((ulong)region.BaseAddress.ToInt64() + (ulong)region.RegionSize.ToInt64());
            int length = (int)Math.Min((ulong)maxBytes, end - start);
            var bytes = new byte[length];
            Marshal.Copy(address, bytes, 0, length);
            return bytes;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MemoryBasicInformation
        {
            internal IntPtr BaseAddress;
            internal IntPtr AllocationBase;
            internal uint AllocationProtect;
            internal ushort PartitionId;
            internal IntPtr RegionSize;
            internal uint State;
            internal uint Protect;
            internal uint Type;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern UIntPtr VirtualQuery(IntPtr address, out MemoryBasicInformation info, UIntPtr length);
    }
}
