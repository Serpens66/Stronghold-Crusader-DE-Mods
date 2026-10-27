#if SERP_DEVDIAG
using System;
using System.Runtime.InteropServices;

namespace BetterPathfindingTest.Service
{
    internal static class NavigationMemory
    {
        [StructLayout(LayoutKind.Sequential)] private struct MemoryStatus
        {
            internal uint Length, Load;
            internal ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile, TotalVirtual, AvailableVirtual, AvailableExtended;
        }
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
        internal static long SelectBudget(Func<ulong> available = null)
        {
            try
            {
                ulong free;
                if (available != null) free = available();
                else { var status = new MemoryStatus { Length = (uint)Marshal.SizeOf(typeof(MemoryStatus)) }; if (!GlobalMemoryStatusEx(ref status)) throw new InvalidOperationException("Physical memory information unavailable."); free = status.AvailablePhysical; }
                return (long)Math.Min(1024UL * 1024 * 1024, free / 4);
            }
            catch { return 256L * 1024 * 1024; }
        }
    }
}
#endif
