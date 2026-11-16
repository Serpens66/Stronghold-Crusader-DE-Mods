using System;
using System.Runtime.InteropServices;

namespace CrashDiagnostics
{
    internal static class NativeMethods
    {
        private const string Dll = "CrashDiagnostics.Native.dll";
        [DllImport(Dll, CallingConvention = CallingConvention.StdCall)] internal static extern uint CD_GetAbiVersion();
        [DllImport(Dll, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        internal static extern uint CD_Initialize(string sessionDirectory, string collectorExe,
            string bepLog, string playerLog, string crashDirectory);
        [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
        internal static extern uint CD_RegisterImage(ulong baseAddress, ulong imageSize,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string sha256);
        [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
        internal static extern uint CD_PublishContext([MarshalAs(UnmanagedType.LPUTF8Str)] string json);
        [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
        internal static extern void CD_RecordEvent(uint kind, long value1, long value2, long value3);
        [DllImport(Dll, CallingConvention = CallingConvention.StdCall)] internal static extern void CD_MarkShutdown();
        [DllImport(Dll, CallingConvention = CallingConvention.StdCall)] internal static extern uint CD_GetStatus();
        [DllImport(Dll, CallingConvention = CallingConvention.StdCall)] internal static extern void CD_Bootstrap(uint budgetMicros);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern IntPtr LoadLibraryExW(string path, IntPtr reserved, uint flags);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ReadProcessMemory(IntPtr process, IntPtr address, byte[] destination,
            UIntPtr size, out UIntPtr bytesRead);
        [DllImport("kernel32.dll")] internal static extern IntPtr GetCurrentProcess();
    }
}
