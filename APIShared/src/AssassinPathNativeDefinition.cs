using BepInEx.Logging;
using Iced.Intel;
using RedBird.Abstractions.Hooks;
using RedBird.Backends.NativeX64;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace APIShared
{
    internal static class AssassinPathNativeDefinition
    {
        internal static readonly int[] Sites = { 0xD9F0C, 0xD9F1C, 0xE19D8, 0xE19F9 };
        internal static readonly int[] Lengths = { 16, 15, 18, 23 };
        internal static readonly string[] Patterns = {
            "0F 85 94 00 00 00 66 41 83 BC 14 50 AA B6 04 00",
            "0F 85 84 00 00 00 41 0B F9 0F BA E7 08 73 7B",
            "0F 85 B1 00 00 00 49 8D 04 D6 41 8B 84 87 B0 ED 05 04",
            "0F 85 88 00 00 00 45 85 C9 75 1B 41 F7 84 8F B0 71 8F 04 00 01 00 00" };
        internal const string BuilderPattern =
            "48 89 5C 24 08 48 89 6C 24 18 48 89 74 24 20 57 41 54 41 55 41 56 41 57 48 83 EC 30 48 63 EA 48 8B D9 49 63 F9";

        internal static void Validate(ReadOnlySpan<byte> memory, ulong module, ManualLogSource log)
        {
            NativePeImage image = NativePeImage.Parse(memory);
            image.RequireMappedRange(AssassinPathAPI.BuildingGridRva, AssassinPathAPI.TileCount * 2, "Assassin building grid");
            image.RequireMappedRange(AssassinPathAPI.TileFlagsRva, AssassinPathAPI.TileCount * 4, "Assassin tile flags");
            image.RequireMappedRange(AssassinPathAPI.BuildingAliveBaseRva,
                4000 * AssassinPathAPI.BuildingStride + 4, "Assassin building records");
            ResolveExact(memory, image, 0xD9C40, BuilderPattern, log);
            for (int index = 0; index < Sites.Length; index++)
            {
                ResolveExact(memory, image, Sites[index], Patterns[index], log);
                ValidateSpan(memory, module, Sites[index], Lengths[index], 14);
            }
            ValidateSpan(memory, module, 0xD9C40, 10, 6); // Indirect-only NativeX64 backend.
            ValidateIncoming(memory, module, 0xD9C40, 990);
            ValidateIncoming(memory, module, 0xE1640, 1512);
        }

        private static void ResolveExact(ReadOnlySpan<byte> memory, NativePeImage image,
            int rva, string text, ManualLogSource log)
        {
            CompiledBytePattern pattern = CompiledBytePattern.Parse(text);
            image.RequireExecutableRange(rva, pattern.Length, "Assassin hook");
            if (pattern.FindUnique(memory.Slice(rva, pattern.Length)) == 0)
            {
                NativeApiLog.Debug(log, $"Assassin native resolution=reference-rva, rva=0x{rva:X}.");
                return;
            }
            int found = -1;
            foreach (NativeSection section in image.Sections)
            {
                if (!section.Executable) continue;
                int relative = pattern.FindUnique(memory.Slice(section.Start, section.Length));
                if (relative == -2 || (relative >= 0 && found >= 0))
                    throw new InvalidOperationException("Ambiguous Assassin native pattern.");
                if (relative >= 0) found = section.Start + relative;
            }
            // Data and structure layouts remain hash-bound; relocated sites are never guessed.
            if (found != rva) throw new InvalidOperationException($"Assassin native pattern at 0x{rva:X} was missing or relocated (0x{found:X}).");
            NativeApiLog.Debug(log, $"Assassin native resolution=pattern-fallback, rva=0x{found:X}.");
        }

        private static void ValidateSpan(ReadOnlySpan<byte> memory, ulong module, int rva, int expected, int minimum)
        {
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(memory.Slice(rva, expected + 15).ToArray()), module + (uint)rva);
            int length = 0;
            while (length < minimum)
            {
                Instruction instruction = decoder.Decode();
                if (instruction.Code == Code.INVALID) throw new InvalidOperationException("Invalid Assassin hook instruction.");
                length += instruction.Length;
            }
            if (length != expected) throw new InvalidOperationException("Assassin hook instruction boundary mismatch.");
        }

        private static void ValidateIncoming(ReadOnlySpan<byte> memory, ulong module, int rva, int size)
        {
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(memory.Slice(rva, size).ToArray()), module + (uint)rva);
            while (decoder.IP < module + (uint)(rva + size))
            {
                Instruction instruction = decoder.Decode();
                if (instruction.Code == Code.INVALID) throw new InvalidOperationException("Invalid Assassin containing function.");
                if (instruction.FlowControl != FlowControl.ConditionalBranch &&
                    instruction.FlowControl != FlowControl.UnconditionalBranch && instruction.FlowControl != FlowControl.Call) continue;
                var starts = new List<int>(Sites) { 0xD9C40 };
                var lengths = new List<int>(Lengths) { 10 };
                for (int index = 0; index < starts.Count; index++)
                {
                    ulong start = module + (uint)starts[index], end = start + (uint)lengths[index];
                    if (instruction.NearBranchTarget > start && instruction.NearBranchTarget < end &&
                        !(instruction.IP >= start && instruction.IP < end))
                        throw new InvalidOperationException("Incoming branch enters an Assassin hook interior.");
                }
            }
        }

        internal static void ValidateInstalledDetour(IDetour<AssassinPathBuilder> hook, ulong module)
        {
            var native = hook as NativeDetour<AssassinPathBuilder>;
            if (native == null || !native.IsInstalled || native.Scheme != DetourScheme.Indirect ||
                native.DisplacedByteCount != 10 || native.TargetAddress != module + 0xD9C40 || native.PointerSlot == IntPtr.Zero)
                throw new InvalidOperationException("Shared Assassin NativeX64 contract mismatch.");
            IntPtr target = new IntPtr(unchecked((long)native.TargetAddress));
            if (Marshal.ReadByte(target) != 0xFF || Marshal.ReadByte(target, 1) != 0x25 ||
                native.TargetAddress + 6 + unchecked((ulong)(long)Marshal.ReadInt32(target, 2)) != unchecked((ulong)native.PointerSlot.ToInt64()) ||
                Marshal.ReadIntPtr(native.PointerSlot) != native.HookEntryPointAddress)
                throw new InvalidOperationException("Shared Assassin Indirect patch/slot/entry mismatch.");
        }
    }
}
