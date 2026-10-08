using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace Shared
{
    internal static class AtomicFileReplacement
    {
        private const uint MoveFileReplaceExisting = 0x1;
        private const uint MoveFileWriteThrough = 0x8;

        internal static void Replace(string sourcePath, string destinationPath)
        {
            if (MoveFileExW(
                    sourcePath,
                    destinationPath,
                    MoveFileReplaceExisting | MoveFileWriteThrough))
            {
                return;
            }

            int error = Marshal.GetLastWin32Error();
            throw new IOException(
                $"Atomic file replacement failed with Win32 error {error}: " +
                $"source=[{sourcePath}], destination=[{destinationPath}].",
                new Win32Exception(error));
        }

        [DllImport(
            "kernel32.dll",
            EntryPoint = "MoveFileExW",
            CharSet = CharSet.Unicode,
            ExactSpelling = true,
            SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool MoveFileExW(
            string existingFileName,
            string newFileName,
            uint flags);
    }
}
