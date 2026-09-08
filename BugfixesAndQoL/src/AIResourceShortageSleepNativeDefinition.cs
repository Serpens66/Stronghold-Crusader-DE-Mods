using Iced.Intel;
using System;
using System.Collections.Generic;

namespace BugfixesAndQoL
{
    internal static class AIResourceShortageSleepNativeDefinition
    {
        internal const ulong PreferredImageBase = 0x180000000;
        internal const int FunctionRva = 0x2AA20;
        internal const int FunctionSize = 0x413;
        internal const int SleepWriteBlockStartRva = 0x2AC7B;
        internal const int SleepWriteBlockEndRva = 0x2AD77;
        internal const int CallerRva = 0x5754F;
        internal const int SynchronizeSleepStatesCallRva = 0x57587;
        internal const int SynchronizeSleepStatesFunctionRva = 0xC7D50;

        // DE counterpart of the HD routine commonly named
        // planToBuyWhenLowOnResourceAndSnoozeBuildings.
        internal const string FunctionPattern =
            "40 53 56 48 83 EC 38 48 63 C2 48 8D 35 ?? ?? ?? ?? " +
            "48 69 D8 3C 58 00 00 83 BC 33 C0 0E 13 00 00";

        internal static void Validate(ReadOnlySpan<byte> memory)
        {
            int resolvedRva = Shared.NativePatternResolver.FindUniquePattern(
                memory,
                FunctionPattern,
                "AI resource-shortage sleep planner");
            if (resolvedRva != FunctionRva)
            {
                throw new InvalidOperationException(
                    $"AI resource-shortage sleep planner resolved to unexpected RVA 0x{resolvedRva:X}.");
            }

            int callerTarget = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory,
                CallerRva + 1,
                CallerRva + 5);
            int synchronizationTarget = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory,
                SynchronizeSleepStatesCallRva + 1,
                SynchronizeSleepStatesCallRva + 5);
            if (callerTarget != FunctionRva || synchronizationTarget != SynchronizeSleepStatesFunctionRva)
            {
                throw new InvalidOperationException(
                    $"AI sleep call targets differ: planner=0x{callerTarget:X}, synchronization=0x{synchronizationTarget:X}.");
            }

            ValidateSleepWrites(memory);
        }

        private static void ValidateSleepWrites(ReadOnlySpan<byte> memory)
        {
            int functionEnd = checked(FunctionRva + FunctionSize);
            if (FunctionRva < 0 || functionEnd > memory.Length)
                throw new InvalidOperationException("AI resource-shortage sleep planner lies outside the native image.");

            var expectedOffsets = new HashSet<ulong>();
            foreach (SHCDESE.Interop.eStructs buildingType in AIResourceShortageSleepPolicy.AffectedBuildingTypes)
            {
                expectedOffsets.Add(unchecked((ulong)(AIResourceShortageSleepPolicy.SleepStateTableOffset +
                    (int)buildingType)));
            }

            var actualOffsets = new HashSet<ulong>();
            byte[] code = memory.Slice(FunctionRva, FunctionSize).ToArray();
            Decoder decoder = Decoder.Create(64, new ByteArrayCodeReader(code), DecoderOptions.None);
            decoder.IP = PreferredImageBase + unchecked((uint)FunctionRva);

            while (decoder.IP < PreferredImageBase + unchecked((uint)functionEnd))
            {
                decoder.Decode(out Instruction instruction);
                if (instruction.Code == Code.INVALID)
                    throw new InvalidOperationException("Invalid instruction encountered in the AI resource-shortage sleep planner.");

                int instructionRva = checked((int)(instruction.IP - PreferredImageBase));
                if (instructionRva < SleepWriteBlockStartRva || instructionRva >= SleepWriteBlockEndRva)
                    continue;

                if (instruction.Mnemonic == Mnemonic.Mov &&
                    instruction.Op0Kind == OpKind.Memory &&
                    instruction.MemoryBase == Register.RBX &&
                    instruction.MemoryIndex == Register.R14 &&
                    instruction.MemoryIndexScale == 1)
                {
                    actualOffsets.Add(instruction.MemoryDisplacement64);
                }
            }

            if (!actualOffsets.SetEquals(expectedOffsets) || actualOffsets.Count != 21)
            {
                throw new InvalidOperationException(
                    $"AI resource-shortage sleep outputs differ: expected 21, found {actualOffsets.Count}.");
            }
        }
    }
}
