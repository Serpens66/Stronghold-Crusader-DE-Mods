// Offline Windows worker helper; never compiled into the game plugin.
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;

namespace DependencyBootstrapProbe.PackageTools
{
    public static class VerifiedFileDeletion
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct FileInformation
        {
            public uint Attributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME Creation, Access, Write;
            public uint Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct Disposition { [MarshalAs(UnmanagedType.Bool)] public bool Delete; }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetFileInformationByHandle(SafeFileHandle file, out FileInformation information);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetFileInformationByHandle(SafeFileHandle file, int kind, ref Disposition disposition, uint size);

        public static void Delete(string path, string expectedHash)
        {
            // DELETE + GENERIC_READ, no write/delete sharing, OPEN_EXISTING, OPEN_REPARSE_POINT.
            using (var handle = CreateFile(path, 0x80010000, 1, IntPtr.Zero, 3, 0x00200000, IntPtr.Zero))
            {
                if (handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
                FileInformation info;
                if (!GetFileInformationByHandle(handle, out info)) throw new Win32Exception(Marshal.GetLastWin32Error());
                if ((info.Attributes & 0x410) != 0 || info.Links != 1)
                    throw new IOException("Directory, reparse point or hard-linked file rejected.");
                using (var stream = new FileStream(handle, FileAccess.Read))
                using (var sha = SHA256.Create())
                {
                    string actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                    if (!string.Equals(actual, expectedHash, StringComparison.Ordinal)) throw new IOException("Hash changed before deletion.");
                    var disposition = new Disposition { Delete = true };
                    if (!SetFileInformationByHandle(handle, 4, ref disposition, (uint)Marshal.SizeOf(typeof(Disposition))))
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
        }
    }
}
