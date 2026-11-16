using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Iced.Intel;
using RedBird.Abstractions.Hooks;
using RedBird.Backends.NativeX64;

namespace BugfixesAndQoL
{
    internal static class AIEmergencyDemolitionNativeContract
    {
        internal const int FunctionRva = 0x2D810;
        internal const int FunctionSize = 474;
        internal const string Signature = "40 55 56 57 41 56 48 83 EC 28 41 8B F8 BE 01 00 00 00 44 8B 05 ?? ?? ?? ?? 41 0F B6 E9 44 8B F2 44 3B C6";
        internal static readonly byte[] Entry = { 0x40, 0x55, 0x56, 0x57, 0x41, 0x56 };
        private const ulong ImageBase = 0x180000000;
        private const string StrategyHash = "2E9D1B6B3F1715D69E6CB33E9FCA5AE8CCB9233984C00491BC48060A2AEE5784";

        internal static NativeDetourBackend Backend() => new NativeDetourBackend(new NativeDetourOptions
        {
            AllowedSchemes = DetourScheme.Indirect,
            FollowJumps = false,
            FollowCalls = false,
            Chaining = HookChainingPolicy.Refuse
        });

        internal static void ValidateEntry(ulong target)
        {
            var bytes = new byte[Entry.Length];
            Marshal.Copy(unchecked((IntPtr)(long)target), bytes, 0, bytes.Length);
            for (int i = 0; i < bytes.Length; i++)
                Require(bytes[i] == Entry[i], "Live demolition entry differs from the audited image.");
        }

        // Whole bodies are compared, not just their prologues. Only RIP data displacements
        // and external branch/call displacements are normalized to allow address relocation.
        // Internal branch offsets, field offsets, selectors and the refund percentage stay pinned.
        internal static void ValidateImage(ReadOnlySpan<byte> image, int rva)
        {
            Require(NormalizedHash(image, rva, FunctionSize) ==
                "F15F3620CBBA68665EE5182F1E25D9A0C6B71BFCBFD61EC7044426C8B46893EF",
                "Emergency-demolition body differs from the audited selective refund/delete loop.");
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(image.Slice(rva, FunctionSize).ToArray()));
            decoder.IP = ImageBase + (uint)rva;
            var calls = new List<Instruction>();
            while (decoder.IP < ImageBase + (uint)(rva + FunctionSize))
            {
                Instruction instruction = decoder.Decode();
                if (instruction.FlowControl == FlowControl.Call) calls.Add(instruction);
            }
            Require(calls.Count == 2 && calls[0].IP == ImageBase + (uint)rva + 0x186 &&
                calls[1].IP == ImageBase + (uint)rva + 0x194,
                "Emergency demolition must call only refund followed by delete.");
            int refund = checked((int)(calls[0].NearBranchTarget - ImageBase));
            int delete = checked((int)(calls[1].NearBranchTarget - ImageBase));
            Require(NormalizedHash(image, refund, 1320) ==
                "0C066A62D8BC81DC382611174CE7B722B5CBCE859940D953037E49A21082B40F",
                "Emergency demolition refund target changed.");
            Require(NormalizedHash(image, delete, 257) ==
                "D7B7AF51784EFC3DCC7B392BBD29FBE55705765638E4C27C0ECA15705A61E8F7",
                "Emergency demolition delete target changed.");
        }

        private static string NormalizedHash(ReadOnlySpan<byte> image, int rva, int size)
        {
            Require(rva >= 0 && size > 0 && rva <= image.Length - size, "Native body outside the image.");
            byte[] bytes = image.Slice(rva, size).ToArray();
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes));
            ulong start = ImageBase + (uint)rva, end = start + (uint)size;
            decoder.IP = start;
            while (decoder.IP < end)
            {
                Instruction instruction = decoder.Decode();
                Require(!instruction.IsInvalid && decoder.IP <= end, "Invalid native instruction boundary.");
                ConstantOffsets offsets = decoder.GetConstantOffsets(in instruction);
                int at = checked((int)(instruction.IP - start));
                if (instruction.IsIPRelativeMemoryOperand)
                    Array.Clear(bytes, at + offsets.DisplacementOffset, offsets.DisplacementSize);
                if ((instruction.FlowControl == FlowControl.Call ||
                     instruction.FlowControl == FlowControl.UnconditionalBranch ||
                     instruction.FlowControl == FlowControl.ConditionalBranch) &&
                    (instruction.NearBranchTarget < start || instruction.NearBranchTarget >= end))
                    Array.Clear(bytes, at + offsets.ImmediateOffset, offsets.ImmediateSize);
            }
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "");
        }

        internal static void ValidateIncomingBranches(ReadOnlySpan<byte> image, int rva)
        {
            var callers = new List<int>();
            foreach (var section in ExecutableSections(image))
            {
                var decoder = Decoder.Create(64, new ByteArrayCodeReader(image.Slice(section.Item1, section.Item2).ToArray()));
                decoder.IP = ImageBase + (uint)section.Item1;
                ulong end = decoder.IP + (uint)section.Item2;
                while (decoder.IP < end)
                {
                    Instruction instruction = decoder.Decode();
                    if (instruction.IsInvalid) continue;
                    if (instruction.FlowControl != FlowControl.Call &&
                        instruction.FlowControl != FlowControl.ConditionalBranch &&
                        instruction.FlowControl != FlowControl.UnconditionalBranch) continue;
                    ulong target = instruction.NearBranchTarget;
                    Require(target <= ImageBase + (uint)rva || target >= ImageBase + (uint)rva + 6,
                        "An incoming branch enters the demolition detour span.");
                    if (target == ImageBase + (uint)rva)
                    {
                        Require(instruction.FlowControl == FlowControl.Call,
                            "An unaudited tail branch enters emergency demolition.");
                        callers.Add(checked((int)(instruction.IP - ImageBase)));
                    }
                }
            }
            callers.Sort();
            int[] offsets = { 0x496, 0x4A7, 0x536, 0x54A, 0x55E, 0x589 };
            Require(callers.Count == offsets.Length, "Emergency-demolition caller count changed.");
            int strategy = callers[0] - offsets[0];
            for (int i = 0; i < offsets.Length; i++)
                Require(callers[i] == strategy + offsets[i], "Emergency-demolition caller layout changed.");
            Require(NormalizedHash(image, strategy, 1520) == StrategyHash,
                "Emergency-demolition caller is no longer the audited strategy state machine.");
        }

        private static List<Tuple<int, int>> ExecutableSections(ReadOnlySpan<byte> image)
        {
            Require(image.Length >= 0x40 && image[0] == 0x4D && image[1] == 0x5A, "Native PE header missing.");
            int pe = ReadInt(image, 0x3C);
            Require(pe >= 0 && pe <= image.Length - 24 && ReadInt(image, pe) == 0x4550, "Invalid native PE header.");
            int count = image[pe + 6] | image[pe + 7] << 8;
            int table = checked(pe + 24 + (image[pe + 20] | image[pe + 21] << 8));
            var result = new List<Tuple<int, int>>();
            for (int i = 0; i < count; i++)
            {
                int at = checked(table + i * 40);
                Require(at >= 0 && at <= image.Length - 40, "Native section header outside image.");
                if ((ReadInt(image, at + 36) & 0x20000000) == 0) continue;
                int start = ReadInt(image, at + 12), size = ReadInt(image, at + 8);
                Require(start >= 0 && size > 0 && start <= image.Length - size, "Executable section outside image.");
                result.Add(Tuple.Create(start, size));
            }
            Require(result.Count != 0, "No executable native section found.");
            return result;
        }

        private static int ReadInt(ReadOnlySpan<byte> image, int at) =>
            image[at] | image[at + 1] << 8 | image[at + 2] << 16 | image[at + 3] << 24;

        internal static void Validate(NativeDetour<AIEmergencyDemolitionDispatch.DemolitionDelegate> hook,
            ulong target, bool installed)
        {
            Require(hook != null && hook.TargetAddress == target && hook.Scheme == DetourScheme.Indirect &&
                hook.DisplacedByteCount == 6 && hook.IsInstalled == installed &&
                hook.PointerSlot != IntPtr.Zero && hook.TrampolineAddress != IntPtr.Zero &&
                hook.HookEntryPointAddress != IntPtr.Zero && hook.OriginalEntryPointAddress != IntPtr.Zero,
                "Emergency-demolition detour backend contract differs.");
            var bytes = new byte[32];
            Marshal.Copy(hook.TrampolineAddress, bytes, 0, bytes.Length);
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes));
            decoder.IP = unchecked((ulong)hook.TrampolineAddress.ToInt64());
            // Relocation may omit the redundant REX 40 prefix on PUSH RBP. Validate
            // the complete replay semantics, not the original encoded byte lengths.
            foreach (Register register in new[] { Register.RBP, Register.RSI, Register.RDI, Register.R14 })
            {
                Instruction push = decoder.Decode();
                Require(push.Mnemonic == Mnemonic.Push && push.Op0Kind == OpKind.Register &&
                    push.Op0Register == register, "Emergency-demolition trampoline replay differs.");
            }
            Instruction jump = decoder.Decode();
            ulong continuation;
            if (jump.FlowControl == FlowControl.UnconditionalBranch) continuation = jump.NearBranchTarget;
            else if (jump.FlowControl == FlowControl.IndirectBranch && jump.IsIPRelativeMemoryOperand)
                continuation = unchecked((ulong)Marshal.ReadInt64((IntPtr)(long)jump.IPRelativeMemoryAddress));
            else throw new InvalidOperationException("Emergency-demolition trampoline continuation is missing.");
            Require(continuation == target + 6, "Emergency-demolition trampoline return differs.");
            if (!installed) return;
            Require(hook.ChainDepth == 1, "Unexpected emergency-demolition hook chain.");
            var patch = new byte[6];
            Marshal.Copy(unchecked((IntPtr)(long)target), patch, 0, patch.Length);
            Require(patch[0] == 0xFF && patch[1] == 0x25 &&
                checked((long)target + 6 + BitConverter.ToInt32(patch, 2)) == hook.PointerSlot.ToInt64() &&
                Marshal.ReadInt64(hook.PointerSlot) == hook.HookEntryPointAddress.ToInt64(),
                "Emergency-demolition FF25 patch or pointer slot differs.");
        }

        private static void Require(bool condition, string reason)
        {
            if (!condition) throw new InvalidOperationException(reason);
        }
    }
}
