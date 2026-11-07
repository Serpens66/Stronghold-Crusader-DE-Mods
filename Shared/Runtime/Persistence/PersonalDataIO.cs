using Microsoft.Win32.SafeHandles;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Shared
{
    internal sealed class PersonalDataSizeException : IOException
    {
        internal PersonalDataSizeException(string path) : base("Personal data file is too large: " + path) { FilePath = path; }
        internal string FilePath { get; }
    }

    // FileStream is constructed from an already-open Unicode handle on Windows. In particular,
    // Unity/Mono never has to reinterpret an extended path through its legacy path implementation.
    internal static class PersonalDataIO
    {
        internal static readonly bool IsWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;
        internal static Action<string, string> TestBeforeOperation;
        private const uint ReadAccess = 0x80000000, WriteAccess = 0x40000000, DeleteAccess = 0x10000;

        internal static string Normalize(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("An absolute data path is required.", nameof(path));
            if (!System.IO.Path.IsPathRooted(path)) throw new ArgumentException("An absolute data path is required.", nameof(path));
            if (!IsWindows) return System.IO.Path.GetFullPath(path);
            path = path.Replace('/', '\\');
            if (path.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase)) path = @"\\" + path.Substring(8);
            else if (path.StartsWith(@"\\?\", StringComparison.Ordinal)) path = path.Substring(4);
            var buffer = new StringBuilder(32768);
            uint length = GetFullPathNameW(path, (uint)buffer.Capacity, buffer, IntPtr.Zero);
            if (length == 0 || length >= buffer.Capacity) throw Error("normalize", path);
            return buffer.ToString();
        }

        internal static string Extended(string path)
        {
            if (!IsWindows) return path;
            if (path.StartsWith(@"\\?\", StringComparison.Ordinal)) return path;
            return path.StartsWith(@"\\", StringComparison.Ordinal) ? @"\\?\UNC\" + path.Substring(2) : @"\\?\" + path;
        }

        internal static byte[] ReadOptional(string path, long maximumBytes)
        {
            Before("read", path);
            try
            {
                using (FileStream stream = Open(path, false, false))
                {
                    long length = stream.Length;
                    if (length > maximumBytes || length > int.MaxValue) throw new PersonalDataSizeException(path);
                    var bytes = new byte[(int)length];
                    int offset = 0;
                    while (offset < bytes.Length)
                    {
                        int read = stream.Read(bytes, offset, bytes.Length - offset);
                        if (read == 0) throw new IOException("Personal data changed while reading: " + path);
                        offset += read;
                    }
                    if (stream.ReadByte() != -1) throw new IOException("Personal data changed while reading: " + path);
                    return bytes;
                }
            }
            catch (FileNotFoundException) { return null; }
            catch (DirectoryNotFoundException) { return null; }
        }

        private static FileStream Open(string path, bool write, bool createNew, bool forDeletion = false)
        {
            if (!IsWindows)
                return new FileStream(path, createNew ? FileMode.CreateNew : FileMode.Open,
                    write ? FileAccess.Write : FileAccess.Read, FileShare.Read);
            SafeFileHandle handle = CreateFileW(Extended(path), (write ? WriteAccess : ReadAccess) | (forDeletion ? DeleteAccess : 0),
                1, IntPtr.Zero, createNew ? 1u : 3u, 0x80, IntPtr.Zero);
            if (handle.IsInvalid)
            {
                int code = Marshal.GetLastWin32Error();
                handle.Dispose();
                if (code == 2) throw new FileNotFoundException("Personal data file was not found.", path);
                if (code == 3) throw new DirectoryNotFoundException("Personal data directory was not found: " + path);
                throw Error("open", path, code);
            }
            try { return new OwnedFileStream(handle, write ? FileAccess.Write : FileAccess.Read); }
            catch { handle.Dispose(); throw; }
        }

        internal static void EnsureDirectory(string path)
        {
            if (!IsWindows) { Directory.CreateDirectory(path); return; }
            if (CreateDirectoryW(Extended(path), IntPtr.Zero)) return;
            int code = Marshal.GetLastWin32Error();
            if (code == 183)
            {
                uint attributes = GetFileAttributesW(Extended(path));
                if (attributes != uint.MaxValue && (attributes & 0x10) != 0) return;
                throw Error("create directory", path, code);
            }
            if (code == 3)
            {
                string parent = Parent(path);
                if (!string.IsNullOrEmpty(parent) && parent != path)
                {
                    EnsureDirectory(parent);
                    if (CreateDirectoryW(Extended(path), IntPtr.Zero)) return;
                    code = Marshal.GetLastWin32Error();
                    uint attributes = GetFileAttributesW(Extended(path));
                    if (code == 183 && attributes != uint.MaxValue && (attributes & 0x10) != 0) return;
                }
            }
            throw Error("create directory", path, code);
        }

        internal static void Publish(string path, byte[] bytes, bool replace)
        {
            string parent = Parent(path);
            EnsureDirectory(parent);
            string temporary = parent + System.IO.Path.DirectorySeparatorChar + "p-" + Guid.NewGuid().ToString("N").Substring(0, 12) + ".tmp";
            try
            {
                Before("write", path);
                using (FileStream stream = Open(temporary, true, true)) { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                byte[] verified = ReadOptional(temporary, bytes.LongLength);
                if (!Equal(bytes, verified)) throw new IOException("Personal data temporary-file verification failed: " + path);
                Before("publish", path);
                if (IsWindows)
                {
                    if (!MoveFileExW(Extended(temporary), Extended(path), 8u | (replace ? 1u : 0u))) throw Error("publish", path);
                }
                else if (replace && File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
                Before("verify", path);
                if (!Equal(bytes, ReadOptional(path, bytes.LongLength))) throw new IOException("Personal data publication verification failed: " + path);
            }
            finally { try { Delete(temporary); } catch { } }
        }

        internal static void Delete(string path)
        {
            Before("delete", path);
            if (!IsWindows) { File.Delete(path); return; }
            if (!DeleteFileW(Extended(path)))
            {
                int code = Marshal.GetLastWin32Error();
                if (code != 2 && code != 3) throw Error("delete", path, code);
            }
        }

        internal static void Move(string source, string destination, bool replace)
        {
            Before("rename", destination);
            if (IsWindows)
            {
                if (!MoveFileExW(Extended(source), Extended(destination), 8u | (replace ? 1u : 0u))) throw Error("rename", destination);
            }
            else if (replace && File.Exists(destination)) File.Replace(source, destination, null);
            else File.Move(source, destination);
        }

        // The hash and deletion use the SAME source handle, opened without write/delete sharing.
        // An old mod cannot replace the verified source between the comparison and the delete.
        internal static bool DeleteUnchanged(string path, string expectedHash, long maximumBytes)
        {
            Before("cleanup", path);
            try
            {
                using (FileStream stream = Open(path, false, false, true))
                {
                    if (stream.Length > maximumBytes) return false;
                    string hash;
                    using (SHA256 sha = SHA256.Create()) hash = Hex(sha.ComputeHash(stream));
                    if (!string.Equals(hash, expectedHash, StringComparison.Ordinal)) return false;
                    if (IsWindows)
                    {
                        int delete = 1;
                        if (!SetFileInformationByHandle(stream.SafeFileHandle, 4, ref delete, 4)) throw Error("cleanup", path);
                    }
                    else File.Delete(path);
                    return true;
                }
            }
            catch (FileNotFoundException) { return true; }
            catch (DirectoryNotFoundException) { return true; }
        }

        internal static string Hash(byte[] bytes) { using (SHA256 sha = SHA256.Create()) return Hex(sha.ComputeHash(bytes)); }
        private static string Parent(string path)
        {
            int separator = path.LastIndexOf(IsWindows ? '\\' : '/');
            if (separator < 0) throw new ArgumentException("An absolute parent directory is required.", nameof(path));
            if (IsWindows && separator == 2 && path[1] == ':') return path.Substring(0, 3);
            return separator == 0 ? path.Substring(0, 1) : path.Substring(0, separator);
        }
        internal static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", string.Empty);
        internal static bool Equal(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length) return left == right;
            for (int index = 0; index < left.Length; index++) if (left[index] != right[index]) return false;
            return true;
        }
        private static void Before(string operation, string path) => TestBeforeOperation?.Invoke(operation, path);
        private static IOException Error(string operation, string path, int code = 0)
        {
            if (code == 0) code = Marshal.GetLastWin32Error();
            return new IOException("Personal data " + operation + " failed (" + code + "): " + path, new Win32Exception(code));
        }

        // Unity's Mono borrows the supplied SafeFileHandle instead of closing it with FileStream.
        // Explicit ownership is necessary before verifying/renaming a freshly written temporary file.
        private sealed class OwnedFileStream : FileStream
        {
            private readonly SafeFileHandle owned;
            internal OwnedFileStream(SafeFileHandle handle, FileAccess access) : base(handle, access, 4096, false) { owned = handle; }
            protected override void Dispose(bool disposing)
            {
                try { base.Dispose(disposing); }
                finally { if (disposing) owned.Dispose(); }
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateFileW(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool CreateDirectoryW(string path, IntPtr security);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern uint GetFileAttributesW(string path);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern uint GetFullPathNameW(string path, uint size, StringBuilder buffer, IntPtr filePart);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool MoveFileExW(string source, string destination, uint flags);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool DeleteFileW(string path);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetFileInformationByHandle(SafeFileHandle handle, int kind, ref int information, int size);
    }
}
