using SHCDESE.Interop;
using APIShared;
using RedBird.X64.Assembly;
using SHCDESE.Interop.Enums;
using System;
using System.Runtime.InteropServices;

namespace BugfixesAndQoL.GatehouseLivingCapture
{
    internal static class NativeDefinition
    {
        internal const string Sha256 = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        internal const int HookRva = 0xB7540, HookLength = 18, ContinueRva = 0xB7552, SkipRva = 0xB75B7;
        internal const int UnitSize = 0x490;
        internal const ulong EligibilityDisplacement = 0x67E8CFC;
        internal const string Pattern = "48 98 48 69 F8 90 04 00 00 66 46 39 B4 2F FC 8C 7E 06 74 63 4A 0F BF B4 2F EE 8A 7E 06 48 8D 0D ? ? ? ?";
        internal static readonly byte[] Original = Hex("48 98 48 69 F8 90 04 00 00 66 46 39 B4 2F FC 8C 7E 06");

        internal static void ValidateLayout()
        {
            // Existing APIShared versions stay unchanged during testing, so verify the capability itself.
            var referenceHelper = typeof(UnitAccess).GetMethod("IsReallyAlive", new[] { typeof(GameUnit).MakeByRefType() });
            var pointerHelper = typeof(UnitAccess).GetMethod("IsReallyAlive", new[] { typeof(GameUnit).MakePointerType() });
            if (referenceHelper == null || pointerHelper == null || !referenceHelper.IsStatic || !pointerHelper.IsStatic ||
                referenceHelper.ReturnType != typeof(bool) || pointerHelper.ReturnType != typeof(bool))
                throw new InvalidOperationException("APIShared requires both public IsReallyAlive overloads; rebuild/install APIShared first.");
            if (Marshal.SizeOf(typeof(GameUnit)) != UnitSize ||
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_AliveState)).ToInt32() != 0x88 ||
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.N0000019A)).ToInt32() != 0x29C ||
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_CurrentHealth)).ToInt32() != 0x3C4 ||
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_ControllableForPlayerId)).ToInt32() != 0x92 ||
                (short)AliveState.IsAlive != 2)
                throw new InvalidOperationException("Installed GameUnit layout/life enum differs from the audited contract.");
            string[] fields = { "RAX", "RBX", "RCX", "RDX", "RSI", "RDI", "RBP", "RSP",
                "R8", "R9", "R10", "R11", "R12", "R13", "R14", "R15", "Rflags" };
            for (int i = 0; i < fields.Length; i++)
                if (Marshal.OffsetOf(typeof(X64SmartCPUContext), fields[i]).ToInt32() != i * 8)
                    throw new InvalidOperationException("Installed RedBird context layout differs: " + fields[i]);
            if (Marshal.SizeOf(typeof(X64SmartCPUContext)) != 136)
                throw new InvalidOperationException("Installed RedBird context size differs.");
        }

        internal static void ValidateCode(ReadOnlySpan<byte> memory, int rva)
        {
            if (rva != HookRva || rva < 0 || rva > memory.Length - HookLength - 2)
                throw new InvalidOperationException("Capture hook resolved outside its audited location.");
            for (int i = 0; i < Original.Length; i++)
                if (memory[rva + i] != Original[i]) throw new InvalidOperationException("Capture hook bytes changed.");
            if (memory[ContinueRva] != 0x74 || memory[ContinueRva + 1] != 0x63 ||
                ContinueRva + 2 + memory[ContinueRva + 1] != SkipRva)
                throw new InvalidOperationException("Original capture JE/skip continuation changed.");
        }

        internal static byte[] Hex(string value) => Array.ConvertAll(value.Split(' '), s => Convert.ToByte(s, 16));
    }
}
