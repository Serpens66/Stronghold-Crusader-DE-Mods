using RedBird.Abstractions.Hooks;
using RedBird.Backends.NativeX64;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace ArabianIdleSoundTest
{
    internal static class NativeContract
    {
        internal const string NativeHash = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        internal const string ManagedHash = "BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789";
        internal const string BackendHash = "0843DD4C381A3E77DD6D8B51D5CCF95465B3FB49BFDF2980F39A212D774AADB0";
        internal const int ArabianRva = 0x16F590, EmitRva = 0x1A41E0, CurrentUnitRva = 0x9302C4;
        internal const string ArabianPattern = "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 41 54 41 55 41 56 41 57 48 83 EC 50 48 63 15 ?? ?? ?? ?? 4C 8D 2D ?? ?? ?? ?? 48 69 CA 90 04 00 00";
        internal const string EmitPattern = "48 63 41 08 4C 8B D9 4C 6B D0 2A 48 8B 01 66 45 89 44 02 02 48 63 41 08 4C 6B C0 2A 48 8B 01 66 45 89 4C 00 04";
        internal static readonly byte[] ArabianBytes = { 0x48,0x89,0x5C,0x24,0x08,0x48,0x89,0x6C,0x24,0x10,0x48,0x89,0x74,0x24,0x18 };
        internal static readonly byte[] EmitBytes = { 0x48,0x63,0x41,0x08,0x4C,0x8B,0xD9,0x4C,0x6B,0xD0,0x2A,0x48,0x8B,0x01 };

        internal static string HashFile(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
        }

        internal static NativeDetourBackend Backend(int size) => new NativeDetourBackend(new NativeDetourOptions
        {
            AllowedSchemes = DetourScheme.Absolute, FollowCalls = false, FollowJumps = false,
            Chaining = HookChainingPolicy.Refuse, FunctionScanLimit = size
        });

        internal static void Validate<T>(NativeDetour<T> hook, int length, ulong address) where T : Delegate
        {
            if (hook.Scheme != DetourScheme.Absolute || hook.DisplacedByteCount != length ||
                hook.TargetAddress != address || hook.PointerSlot != IntPtr.Zero ||
                hook.HookEntryPointAddress == IntPtr.Zero || hook.TrampolineAddress == IntPtr.Zero)
                throw new InvalidOperationException("NativeX64 Absolute contract mismatch at 0x" + address.ToString("X"));
            if (!hook.IsInstalled) return;
            byte[] bytes = new byte[length];
            Marshal.Copy(new IntPtr(unchecked((long)address)), bytes, 0, length);
            if (bytes[0] != 0xFF || bytes[1] != 0x25 || BitConverter.ToInt32(bytes, 2) != 0 ||
                BitConverter.ToInt64(bytes, 6) != hook.HookEntryPointAddress.ToInt64())
                throw new InvalidOperationException("Published Absolute patch mismatch.");
            for (int i = 14; i < length; i++) if (bytes[i] != 0x90)
                throw new InvalidOperationException("Published Absolute padding mismatch.");
        }
    }
}
