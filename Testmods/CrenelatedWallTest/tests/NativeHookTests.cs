using CrenelatedWallTest;
using RedBird.Abstractions.Hooks;
using RedBird.Backends.NativeX64;
using System;
using System.Reflection;
using System.Runtime.InteropServices;

// Logging-only fixture; the actual production hook, validator and callback are linked.
namespace Shared
{
    internal static class DebugLogHelper
    {
        internal static void LogInfo(BepInEx.Logging.ManualLogSource log, string text) { }
        internal static void LogErrorLimited(BepInEx.Logging.ManualLogSource log, string site, params object[] data) { }
    }
}
internal static class NativeHookTests
{
    [DllImport("kernel32", SetLastError = true)] private static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr size, uint flags, uint protection);
    [DllImport("kernel32", SetLastError = true)] private static extern bool VirtualFree(IntPtr address, UIntPtr size, uint flags);
    private static int checks;
    private static void Check(bool good, string name) { if (!good) throw new Exception(name); checks++; }
    private static int Main()
    {
        IntPtr copy = IntPtr.Zero, data = IntPtr.Zero;
        NativeDetour<StraightGraphicsHook.Predicate> candidate = null;
        StraightGraphicsHook.Predicate invokeCallback = null;
        try
        {
            copy = VirtualAlloc(IntPtr.Zero, (UIntPtr)4096, 0x3000, 0x40);
            data = VirtualAlloc(IntPtr.Zero, (UIntPtr)0x900000, 0x3000, 0x04);
            Check(copy != IntPtr.Zero && data != IntPtr.Zero, "Native fixture allocation");
            byte[] bytes = StraightGraphicsHook.DecodeBody(); Marshal.Copy(bytes, 0, copy, bytes.Length);
            int[] delta = { -40, -39, 1, 41, 40, 39, -1, -41 }; Marshal.Copy(delta, 0, data, delta.Length);
            var owner = new StraightGraphicsHook(null);
            invokeCallback = (StraightGraphicsHook.Predicate)Delegate.CreateDelegate(typeof(StraightGraphicsHook.Predicate), owner,
                typeof(StraightGraphicsHook).GetMethod("Invoke", BindingFlags.NonPublic | BindingFlags.Instance));
            var request = new DetourRequest<StraightGraphicsHook.Predicate> { Name = "copied crenel graphics predicate", TargetAddress = unchecked((ulong)copy.ToInt64()), Callback = invokeCallback };
            candidate = (NativeDetour<StraightGraphicsHook.Predicate>)StraightGraphicsHook.CreateBackend().CreateDetour(in request);
            typeof(StraightGraphicsHook).GetField("hook", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(owner, candidate);
            StraightGraphicsHook.Validate(candidate, unchecked((ulong)copy.ToInt64()));
            Check(!candidate.IsInstalled && candidate.DisplacedByteCount == 15, "Prepared Absolute/15 contract");
            byte[] moved = new byte[15]; Marshal.Copy(candidate.TrampolineAddress, moved, 0, moved.Length);
            for (int i = 0; i < moved.Length; i++) Check(moved[i] == bytes[i], "Trampoline displaced instruction bytes");
            // Only this copied executable fixture is published and later removed.
            candidate.Enable(); StraightGraphicsHook.Validate(candidate, unchecked((ulong)copy.ToInt64()));
            var invoke = Marshal.GetDelegateForFunctionPointer<StraightGraphicsHook.Predicate>(copy);
            for (int shape = 0; shape < 16; shape++)
            {
                foreach (int d in new[] { 0, 2, 4, 6 }) Marshal.WriteInt32(IntPtr.Add(data, 0x898400 + (100 + delta[d]) * 4), 0);
                for (int bit = 0; bit < 4; bit++) if ((shape & (1 << bit)) != 0)
                    Marshal.WriteInt32(IntPtr.Add(data, 0x898400 + (100 + delta[bit * 2]) * 4), 0x200);
                foreach (int direction in new[] { 0, 2, 4, 6 })
                {
                    bool ns = direction == 0 || direction == 4;
                    int expected = shape == (ns ? 5 : 10) ? 1 : 0;
                    Check(candidate.Original(data, 100, 0, direction) == expected, "Executed trampoline semantics");
                    Check(invoke(data, 100, 0, direction) == expected, "Actual disabled callback preserves native predicate");
                }
            }
            owner.Publish(null); Check(invoke(data, 100, 0, 0) == candidate.Original(data, 100, 0, 0), "Empty snapshot remains original");
            Console.WriteLine("PASS: " + checks + " installed NativeX64 fixture checks; Absolute/15, entry, trampoline continuation, exact original ABI and actual disabled callback. No game hook installed.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally
        {
            // The test's copied-memory hook has no connection to any game process.
            candidate?.Dispose();
            if (copy != IntPtr.Zero) VirtualFree(copy, UIntPtr.Zero, 0x8000);
            if (data != IntPtr.Zero) VirtualFree(data, UIntPtr.Zero, 0x8000);
            GC.KeepAlive(invokeCallback);
        }
    }
}
