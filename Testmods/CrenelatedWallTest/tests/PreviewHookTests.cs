using CrenelatedWallTest;
using RedBird.Abstractions.Hooks;
using RedBird.Backends.NativeX64;
using System;
using System.Reflection;
using System.Runtime.InteropServices;

internal static class PreviewHookTests
{
    [DllImport("kernel32", SetLastError = true)] private static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr size, uint flags, uint protection);
    [DllImport("kernel32", SetLastError = true)] private static extern bool VirtualFree(IntPtr address, UIntPtr size, uint flags);
    internal static void Run()
    {
        IntPtr code = VirtualAlloc(IntPtr.Zero, (UIntPtr)4096, 0x3000, 0x40);
        IntPtr data = VirtualAlloc(IntPtr.Zero, (UIntPtr)(PreviewValidationHook.InvalidOffset + 4096), 0x3000, 0x04);
        NativeDetour<PreviewValidationHook.Validator> candidate = null;
        PreviewValidationHook.Validator callback = null;
        int checks = 0;
        void Check(bool good, string name) { if (!good) throw new Exception(name); checks++; }
        try
        {
            Check(code != IntPtr.Zero && data != IntPtr.Zero, "Preview allocation");
            byte[] original = PreviewValidationHook.DecodeBody();
            Marshal.Copy(original, 0, code, PreviewValidationHook.Displaced);
            // Exact production prologue, then an isolated ABI probe. The real body
            // depends on module globals and is checked against the DLL by audit-native.
            // Probe records seven arguments/call count, sets a native result and
            // unwinds the exact five pushes and 0x80 stack allocation.
            byte[] tail = Hex("891144894104448949088B8424D000000089410C8B8424D80000008941100FBF8424E0000000894114FF41188B41208981E4E604024881C480000000415D415C5F5D5BC3");
            Marshal.Copy(tail, 0, IntPtr.Add(code, PreviewValidationHook.Displaced), tail.Length);
            int called = 0; bool veto = false, fail = false;
            var owner = new PreviewValidationHook(null, "fixture.preview", (manager, player, x, y, ex, ey, mapper) => {
                called++;
                Check(manager == data && player == 7 && x == 11 && y == 12 && ex == 13 && ey == 14 && mapper == 26,
                    "Preview callback ABI, including stack arguments");
                if (fail) throw new InvalidOperationException("fixture policy failure");
                return veto;
            });
            callback = (PreviewValidationHook.Validator)Delegate.CreateDelegate(typeof(PreviewValidationHook.Validator), owner,
                typeof(PreviewValidationHook).GetMethod("Invoke", BindingFlags.NonPublic | BindingFlags.Instance));
            var request = new DetourRequest<PreviewValidationHook.Validator> { Name = "copied preview ABI probe", TargetAddress = unchecked((ulong)code.ToInt64()), Callback = callback };
            candidate = (NativeDetour<PreviewValidationHook.Validator>)PreviewValidationHook.CreateBackend().CreateDetour(in request);
            typeof(PreviewValidationHook).GetField("hook", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(owner, candidate);
            PreviewValidationHook.Validate(candidate, unchecked((ulong)code.ToInt64()));
            Check(candidate.DisplacedByteCount == 18 && !candidate.IsInstalled, "Prepared Absolute/18");
            var moved = new byte[18]; Marshal.Copy(candidate.TrampolineAddress, moved, 0, moved.Length);
            for (int i = 0; i < moved.Length; i++) Check(moved[i] == original[i], "Exact preview trampoline instructions");
            candidate.Enable(); PreviewValidationHook.Validate(candidate, unchecked((ulong)code.ToInt64()));
            var invoke = Marshal.GetDelegateForFunctionPointer<PreviewValidationHook.Validator>(code);
            void Invoke(int nativeInvalid, int expectedInvalid, bool useTrampoline = false)
            {
                Marshal.WriteInt32(data, 0x20, nativeInvalid);
                int before = Marshal.ReadInt32(data, 0x18);
                (useTrampoline ? candidate.Original : invoke)(data, 7, 11, 12, 13, 14, 26);
                Check(Marshal.ReadInt32(data, 0x18) == before + 1, "Original validator exactly once");
                Check(Marshal.ReadInt32(data, PreviewValidationHook.InvalidOffset) == expectedInvalid, "Preview invalid status");
                for (int i = 0; i < 6; i++) Check(Marshal.ReadInt32(data, i * 4) == new[] { 7, 11, 12, 13, 14, 26 }[i], "Native ABI capture");
            }
            Invoke(0, 0, true); Invoke(1, 1, true);
            veto = true; Invoke(0, 0); Invoke(37, 37);
            Check(called == 0, "Disabled callback never evaluates policy");
            owner.SetActive(true);
            Invoke(37, 37); Check(called == 0, "Vanilla rejection never evaluated or approved");
            Invoke(0, 1); Check(called == 1, "Additional veto after native approval");
            veto = false; Invoke(0, 0); Check(called == 2, "Next native validation clears stale veto");
            fail = true; Invoke(0, 0); Check(called == 3, "Policy exception preserves original status");
            owner.SetActive(false); Invoke(0, 0); Check(called == 3, "Logical deactivation restores full original path");
            invoke(data, int.MinValue, uint.MaxValue, 0x80000000U, 0xFFFFFFFEU, 0x80000001U, short.MinValue);
            Check(Marshal.ReadInt32(data, 0) == int.MinValue && Marshal.ReadInt32(data, 4) == -1 &&
                Marshal.ReadInt32(data, 8) == int.MinValue && Marshal.ReadInt32(data, 12) == -2 &&
                Marshal.ReadInt32(data, 16) == unchecked((int)0x80000001U) && Marshal.ReadInt32(data, 20) == short.MinValue,
                "Disabled ABI preserves all 32-bit coordinate bits and signed short mapper");
            Console.WriteLine("PASS: " + checks + " preview NativeX64 checks; actual production callback, Absolute/18, seven-argument ABI, original exactly once, veto/reset/disabled/error paths. Native game body audited separately.");
        }
        finally
        {
            // Isolated fixture memory only; never game executable memory.
            candidate?.Dispose();
            if (code != IntPtr.Zero) VirtualFree(code, UIntPtr.Zero, 0x8000);
            if (data != IntPtr.Zero) VirtualFree(data, UIntPtr.Zero, 0x8000);
            GC.KeepAlive(callback);
        }
    }
    private static byte[] Hex(string value)
    {
        var bytes = new byte[value.Length / 2];
        for (int i = 0; i < bytes.Length; i++) bytes[i] = Convert.ToByte(value.Substring(i * 2, 2), 16);
        return bytes;
    }
}
