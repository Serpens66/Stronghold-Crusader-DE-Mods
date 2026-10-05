using System;
using System.IO;
using System.Runtime.InteropServices;
using RedBird.Abstractions.Hooks;
using RedBird.Backends.NativeX64;

namespace BugfixesAndQoL
{
    internal static class Program
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr size, uint allocation, uint protect);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualFree(IntPtr address, UIntPtr size, uint freeType);
        private static int checks;
        private static void Check(bool condition, string message)
        { checks++; if (!condition) throw new InvalidOperationException(message); }

        private static int Main(string[] args)
        {
            try
            {
                byte[] body = File.ReadAllBytes(args[0]);
                AIKeepRangeNativeContract.ValidateFunction(body, 0x1800EEF90);
                IntPtr bodyCopy = Marshal.AllocHGlobal(body.Length);
                try
                {
                    Marshal.Copy(body, 0, bodyCopy, body.Length);
                    AIKeepRangeNativeContract.ProbeBackend(unchecked((ulong)bodyCopy.ToInt64()));
                }
                finally { Marshal.FreeHGlobal(bodyCopy); }
                TestPolicy();
                RuntimeHarness.Run();
                TestBackend(body);
                Console.WriteLine("PASS: AI keep range: " + checks + " checks; actual NativeX64 Indirect/10, trampoline execution, AI/human/invalid/disabled forwarding.");
                return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }

        private static void TestPolicy()
        {
            int classified = 0, originals = 0;
            Func<int, bool> isAI = id => { classified++; return id % 2 == 0; };
            AIKeepDistanceCheck original = (manager, player, x, y, range) =>
            {
                originals++;
                Check(manager == (IntPtr)123 && x == 799 && y == uint.MaxValue && range == -1,
                    "Vanilla arguments must be preserved exactly");
                return 37;
            };
            foreach (bool enabled in new[] { false, true })
            foreach (int id in new[] { int.MinValue, -1, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, int.MaxValue })
            {
                int previous = originals, priorClassified = classified;
                bool bypass = AIKeepRangeDecision.Bypass(enabled, id, isAI);
                bool valid = id > 0 && id <= 8;
                Check(classified - priorClassified == (enabled && valid ? 1 : 0), "classify only enabled valid players");
                Check(bypass == (enabled && valid && id % 2 == 0), "AI classification");
                long result = AIKeepRangeDecision.Execute(bypass, original, (IntPtr)123, id, 799, uint.MaxValue, -1);
                Check(result == (bypass ? 0 : 37) && originals - previous == (bypass ? 0 : 1),
                    "AI bypass or exactly one unchanged original call");
            }
        }

        private static void TestBackend(byte[] body)
        {
            IntPtr target = VirtualAlloc(IntPtr.Zero, (UIntPtr)4096, 0x3000, 0x40);
            if (target == IntPtr.Zero) throw new InvalidOperationException("Test allocation failed.");
            NativeDetour<AIKeepDistanceCheck> candidate = null;
            int callbacks = 0;
            bool enabled = false;
            AIKeepDistanceCheck original = null;
            AIKeepDistanceCheck callback = (manager, player, x, y, range) =>
            {
                callbacks++;
                return AIKeepRangeDecision.Execute(AIKeepRangeDecision.Bypass(enabled, player, id => id == 2),
                    original, manager, player, x, y, range);
            };
            try
            {
                // Real audited prologue, then a synthetic leaf continuation returning its fifth argument.
                // This verifies saved registers, stack argument position, callback and trampoline ABI.
                byte[] code = new byte[64];
                Array.Copy(body, code, 10);
                byte[] leaf = { 0x8B, 0x44, 0x24, 0x28, 0xC3 };
                Array.Copy(leaf, 0, code, 10, leaf.Length);
                Marshal.Copy(code, 0, target, code.Length);
                ulong address = unchecked((ulong)target.ToInt64());
                AIKeepRangeNativeContract.ProbeBackend(address);
                var request = new DetourRequest<AIKeepDistanceCheck> { Name = "AI range execution test", TargetAddress = address, Callback = callback };
                candidate = NativeDetourBackend.Instance.CreateDetour(in request) as NativeDetour<AIKeepDistanceCheck>;
                AIKeepRangeNativeContract.ValidateDetour(candidate, address, false);
                original = candidate.Original;
                candidate.Enable();
                AIKeepRangeNativeContract.ValidateDetour(candidate, address, true);
                var invoke = Marshal.GetDelegateForFunctionPointer<AIKeepDistanceCheck>(target);
                Check(invoke(IntPtr.Zero, 2, 0, 0, 70) == 70, "disabled actual trampoline");
                enabled = true;
                Check(invoke(IntPtr.Zero, 2, 0, 0, 70) == 0, "AI actual callback bypass");
                Check(invoke(IntPtr.Zero, 1, 0, 0, 70) == 70, "human actual trampoline");
                Check(invoke(IntPtr.Zero, 0, 0, 0, 80) == 80, "invalid actual trampoline");
                Check(callbacks == 4, "exact actual callback count");
            }
            finally
            {
                candidate?.Dispose(); // Isolated test allocation, never a game hook.
                VirtualFree(target, UIntPtr.Zero, 0x8000);
                GC.KeepAlive(callback);
            }
        }
    }
}
