using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using BepInEx;
using BepInEx.Logging;
using Iced.Intel;
using RedBird.Abstractions.Hooks;
using RedBird.Backends.NativeX64;
using SHCDESE.API.LowLevel;

namespace BugfixesAndQoL
{
    internal static class AIKeepRangeNativeContract
    {
        internal const string NativeHash = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        internal const string FunctionHash = "83D062DDDBAFEC9EB33F704FA914609B6761E16DAE351A64F7491319984DF12E";
        internal const int Rva = 0xEEF90, FunctionSize = 317, Displaced = 10;
        internal const string Pattern = "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 41 54 41 55 41 56 41 57 48 83 EC 30 49 63 E8 49 63 F1 81 FD 1F 03 00 00";

        internal static ulong Resolve(CrusaderLibraryLoadContext context, out string resolution)
        {
            if (context == null || context.ModuleHandle == IntPtr.Zero || context.Memory.Length == 0)
                throw new InvalidOperationException("Native load context unavailable.");
            string path = Path.Combine(Paths.GameRootPath,
                "Stronghold Crusader Definitive Edition_Data", "Plugins", "x86_64", "CrusaderDE.dll");
            bool known = Hash(File.ReadAllBytes(path)) == NativeHash;
            var resolved = Shared.NativePatternResolver.ResolveUnique(context.Memory, Pattern, Rva,
                known, "AI keep distance predicate");
            // Full caller/xref coverage is bound to this binary; a pattern alone is not sufficient.
            if (!known || resolved.Rva != Rva)
                throw new InvalidOperationException("Unknown native identity/entry: re-audit callers and incoming edges before enabling.");
            ulong target = unchecked((ulong)context.ModuleHandle.ToInt64()) + (ulong)resolved.Rva;
            ValidateFunction(Capture(target, FunctionSize), target);
            resolution = "method=" + resolved.Method + ", rva=0x" + resolved.Rva.ToString("X");
            return target;
        }

        internal static void ValidateFunction(byte[] bytes, ulong target)
        {
            if (bytes.Length != FunctionSize || Hash(bytes) != FunctionHash)
                throw new InvalidOperationException("Distance predicate body changed or is already patched.");
            if (typeof(NativeDetour<>).Assembly.GetName().Version != new Version(1, 5, 0, 0))
                throw new InvalidOperationException("Re-audit the installed NativeX64 backend version.");
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes));
            decoder.IP = target;
            while (decoder.IP < target + FunctionSize)
            {
                var instruction = decoder.Decode();
                if (instruction.IsInvalid || decoder.LastError != DecoderError.None)
                    throw new InvalidOperationException("Invalid instruction in the distance predicate.");
                if (instruction.IP < target + Displaced && instruction.Length != 5)
                    throw new InvalidOperationException("Unexpected entry instruction boundary.");
                if (instruction.Op0Kind == OpKind.NearBranch64 &&
                    instruction.NearBranchTarget > target && instruction.NearBranchTarget < target + Displaced)
                    throw new InvalidOperationException("Incoming branch into displaced entry.");
            }
            if (decoder.IP != target + FunctionSize)
                throw new InvalidOperationException("Distance predicate function boundary changed.");
        }

        internal static void ValidateDetour(NativeDetour<AIKeepDistanceCheck> detour, ulong target, bool installed)
        {
            if (detour == null || detour.IsInstalled != installed || detour.TargetAddress != target ||
                detour.Scheme.ToString() != "Indirect" || detour.DisplacedByteCount != Displaced ||
                detour.ChainDepth != 1 || detour.PointerSlot == IntPtr.Zero ||
                detour.TrampolineAddress == IntPtr.Zero || detour.HookEntryPointAddress == IntPtr.Zero ||
                detour.OriginalEntryPointAddress != detour.TrampolineAddress)
                throw new InvalidOperationException("NativeX64 must produce an unchained Indirect/10 detour without intermediary.");
            byte[] trampoline = Capture(unchecked((ulong)detour.TrampolineAddress.ToInt64()), Math.Min(40, detour.TrampolineSize));
            byte[] entry = { 0x48, 0x89, 0x5C, 0x24, 0x08, 0x48, 0x89, 0x6C, 0x24, 0x10 };
            for (int i = 0; i < entry.Length; i++)
                if (trampoline[i] != entry[i]) throw new InvalidOperationException("Relocated prologue differs.");
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(trampoline));
            decoder.IP = unchecked((ulong)detour.TrampolineAddress.ToInt64());
            decoder.Decode(); decoder.Decode();
            var jump = decoder.Decode();
            ulong continuation;
            if (jump.Mnemonic == Mnemonic.Jmp && jump.Op0Kind == OpKind.NearBranch64)
                continuation = jump.NearBranchTarget;
            else if (jump.Mnemonic == Mnemonic.Jmp && jump.IsIPRelativeMemoryOperand)
                continuation = unchecked((ulong)Marshal.ReadInt64((IntPtr)(long)jump.IPRelativeMemoryAddress));
            else throw new InvalidOperationException("Trampoline does not jump back after both saved-register instructions.");
            if (continuation != target + Displaced)
                throw new InvalidOperationException("Incorrect trampoline continuation.");
            if (!installed) return;
            byte[] patch = Capture(target, Displaced);
            if (patch[0] != 0xFF || patch[1] != 0x25 ||
                (long)target + 6 + BitConverter.ToInt32(patch, 2) != detour.PointerSlot.ToInt64() ||
                Marshal.ReadInt64(detour.PointerSlot) != detour.HookEntryPointAddress.ToInt64())
                throw new InvalidOperationException("Entry jump/pointer slot/hook entry mismatch.");
            for (int i = 6; i < Displaced; i++)
                if (patch[i] != 0x90) throw new InvalidOperationException("Unexpected displacement padding.");
        }

        internal static void ProbeBackend(ulong target)
        {
            // The backend scans to the function end (default limit 16 KiB). A 64-byte
            // prefix would let it read unowned memory before this predicate's RET.
            byte[] entry = Capture(target, FunctionSize);
            int capacity = Math.Max(FunctionSize, NativeDetourOptions.Default.FunctionScanLimit);
            IntPtr copy = Marshal.AllocHGlobal(capacity);
            NativeDetour<AIKeepDistanceCheck> candidate = null;
            AIKeepDistanceCheck callback = (_, __, ___, ____, _____) => 0;
            try
            {
                Marshal.Copy(new byte[capacity], 0, copy, capacity);
                Marshal.Copy(entry, 0, copy, entry.Length);
                ulong address = unchecked((ulong)copy.ToInt64());
                var request = new DetourRequest<AIKeepDistanceCheck> { Name = "AI distance copied-entry probe", TargetAddress = address, Callback = callback };
                candidate = NativeDetourBackend.Instance.CreateDetour(in request) as NativeDetour<AIKeepDistanceCheck>;
                ValidateDetour(candidate, address, false);
                candidate.Enable();
                ValidateDetour(candidate, address, true);
            }
            finally
            {
                candidate?.Dispose(); // Private nonexecuted probe, never published to the game.
                byte[] restored = Capture(unchecked((ulong)copy.ToInt64()), entry.Length);
                Marshal.FreeHGlobal(copy);
                GC.KeepAlive(callback);
                for (int i = 0; i < entry.Length; i++)
                    if (restored[i] != entry[i]) throw new InvalidOperationException("Probe rollback changed original bytes.");
            }
        }

        internal static byte[] Capture(ulong address, int length)
        {
            var bytes = new byte[length];
            Marshal.Copy(unchecked((IntPtr)(long)address), bytes, 0, length);
            return bytes;
        }
        internal static string Hash(byte[] bytes)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
        }
    }
}
