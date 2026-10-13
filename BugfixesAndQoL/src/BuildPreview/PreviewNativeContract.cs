using System;
using System.Runtime.InteropServices;
using Iced.Intel;
using RedBird.Abstractions.Hooks;
using RedBird.Backends.NativeX64;
using SHCDESE.Interop;

namespace BugfixesAndQoL.BuildPreview
{
    internal static class PreviewNativeContract
    {
        internal const string NativeHash = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        internal const int PreviewRva = 0x5DD70;
        internal const int PreviewEndRva = 0x60CAC;
        internal const int ManagerRva = 0x405EDB0;
        internal const int ValidMapMaskRva = 0x3A11EA4;
        internal const int PackedRowRva = 0x402FF2C;
        internal const int PlacementStateOffset = 0x204E6FC;
        internal const int PreviewOverrideOffset = 0x204E7EC;
        internal const int DisplacedBytes = 6;
        internal const string Signature = "48 89 5C 24 20 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 60 8B 81 EC E7 04 02";
        internal static readonly byte[] Entry = { 0x48, 0x89, 0x5C, 0x24, 0x20, 0x55 };

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void PreviewDelegate(IntPtr manager, int x, int y, short mapper, int scale);

        internal static NativeDetourBackend Backend()
        {
            if (typeof(NativeDetour<>).Assembly.GetName().Version != new Version(1, 5, 0, 0))
                throw new InvalidOperationException("Unreviewed RedBird NativeX64 version.");
            return new NativeDetourBackend(new NativeDetourOptions
            {
                AllowedSchemes = DetourScheme.Indirect,
                FollowJumps = false,
                FollowCalls = false,
                Chaining = HookChainingPolicy.Refuse
            });
        }

        internal static void ValidateEntry(ulong target)
        {
            var bytes = new byte[Entry.Length];
            Marshal.Copy((IntPtr)(long)target, bytes, 0, bytes.Length);
            for (int i = 0; i < bytes.Length; i++)
                if (bytes[i] != Entry[i]) throw new InvalidOperationException("Preview entry bytes differ.");
        }

        internal static void Validate(NativeDetour<PreviewDelegate> hook, ulong target, bool installed)
        {
            if (hook == null || hook.TargetAddress != target || hook.Scheme != DetourScheme.Indirect ||
                hook.DisplacedByteCount != DisplacedBytes || hook.IsInstalled != installed ||
                hook.PointerSlot == IntPtr.Zero || hook.TrampolineAddress == IntPtr.Zero ||
                hook.HookEntryPointAddress == IntPtr.Zero || hook.OriginalEntryPointAddress == IntPtr.Zero)
                throw new InvalidOperationException("Preview detour backend contract differs.");
            ValidateEntry((ulong)hook.TrampolineAddress.ToInt64());
            var bytes = new byte[32];
            Marshal.Copy(hook.TrampolineAddress, bytes, 0, bytes.Length);
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes));
            decoder.IP = (ulong)hook.TrampolineAddress.ToInt64();
            if (decoder.Decode().Length != 5 || decoder.Decode().Length != 1)
                throw new InvalidOperationException("Trampoline instruction boundaries differ.");
            Instruction jump = decoder.Decode();
            ulong continuation;
            if (jump.FlowControl == FlowControl.UnconditionalBranch)
                continuation = jump.NearBranchTarget;
            else if (jump.FlowControl == FlowControl.IndirectBranch && jump.IsIPRelativeMemoryOperand)
                continuation = (ulong)Marshal.ReadInt64((IntPtr)(long)jump.IPRelativeMemoryAddress);
            else throw new InvalidOperationException("Trampoline has no audited continuation jump.");
            if (continuation != target + DisplacedBytes)
                throw new InvalidOperationException("Trampoline continuation differs.");
            if (!installed) return;
            if (hook.ChainDepth != 1) throw new InvalidOperationException("Unexpected detour chain.");
            var patch = new byte[DisplacedBytes];
            Marshal.Copy((IntPtr)(long)target, patch, 0, patch.Length);
            if (patch[0] != 0xFF || patch[1] != 0x25 ||
                checked((long)target + DisplacedBytes + BitConverter.ToInt32(patch, 2)) != hook.PointerSlot.ToInt64() ||
                Marshal.ReadInt64(hook.PointerSlot) != hook.HookEntryPointAddress.ToInt64())
                throw new InvalidOperationException("Installed FF25 patch or pointer slot differs.");
        }
    }
}
