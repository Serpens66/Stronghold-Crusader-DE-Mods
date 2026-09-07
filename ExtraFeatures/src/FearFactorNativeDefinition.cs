using Iced.Intel;
using System;

namespace ExtraFeatures
{
    internal static class FearFactorNativeDefinition
    {
        internal const string ReferenceSha256 =
            "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        internal const string AuditedScriptExtenderCommit =
            "a0cd52993b44a6909d4f7f6a92f82fa5888a8e63";
        internal const ulong PreferredImageBase = 0x180000000;

        internal const int DamageFunctionRva = 0x180560;
        internal const int DamageFunctionLength = 47;
        internal const int DamageDetourLength = 10;
        internal const string DamageFunctionPattern =
            "49 63 C0 48 69 C8 3C 58 00 00 48 8D 05 93 C9 61 03 8B 04 01 " +
            "83 C0 14 0F AF C2 8D 0C 80 B8 1F 85 EB 51 F7 E9 C1 FA 05 8B " +
            "C2 C1 E8 1F 03 C2 C3";

        internal const int MeleeCallerStartRva = 0x199110;
        internal const int MeleeCallerLength = 2189;
        internal const int ProjectileCallerStartRva = 0x192750;
        internal const int ProjectileCallerLength = 4664;

        internal const int UiPatternRva = 0x1A19F2;
        internal const int UnitOverlayFunctionRva = 0x1A13C0;
        internal const int UnitOverlayFunctionLength = 5079;
        internal const int UiFearLoadOffset = 7;
        internal const int UiFearLoadRva = UiPatternRva + UiFearLoadOffset;
        internal const int UiFearLoadLength = 7;
        internal const int UiHookRva = UiPatternRva;
        internal const int UiHookLength = 14;
        internal const string UiFearLoadPattern =
            "49 69 CA 3C 58 00 00 8B 84 39 04 CF 79 03 85 C0 74 2A 6B D0 0B";

        internal static void Validate(ReadOnlySpan<byte> memory, ulong imageBase)
        {
            if (memory.Length == 0)
                throw new ArgumentException("The Crusader native image is empty.", nameof(memory));

            ValidateDamageFunction(memory, imageBase);
            ValidateUiFearLoad(memory, imageBase);
            ValidateDamageCallers(memory);
            ValidateNoIncomingDirectBranchTargets(
                memory,
                imageBase,
                UnitOverlayFunctionRva,
                UnitOverlayFunctionLength,
                UiHookRva,
                checked(UiHookRva + UiHookLength),
                "fear-factor unit-overlay load");
        }

        private static void ValidateDamageFunction(ReadOnlySpan<byte> memory, ulong imageBase)
        {
            if (!Shared.NativePatternResolver.MatchesPatternAt(
                    memory,
                    DamageFunctionRva,
                    DamageFunctionPattern))
            {
                throw new InvalidOperationException(
                    "The complete fear-factor damage function differs from the audited bytes.");
            }

            Decoder decoder = CreateDecoder(memory, imageBase, DamageFunctionRva, DamageFunctionLength);
            decoder.Decode(out Instruction first);
            decoder.Decode(out Instruction second);
            if (first.IsInvalid || second.IsInvalid ||
                first.Length != 3 || second.Length != 7 ||
                first.Mnemonic != Mnemonic.Movsxd ||
                first.Op0Register != Register.RAX || first.Op1Register != Register.R8D ||
                second.Mnemonic != Mnemonic.Imul ||
                second.Op0Register != Register.RCX || second.Op1Register != Register.RAX ||
                second.Op2Kind != OpKind.Immediate32to64 || second.Immediate32 != 0x583CU ||
                first.IsIPRelativeMemoryOperand || second.IsIPRelativeMemoryOperand ||
                first.FlowControl != FlowControl.Next || second.FlowControl != FlowControl.Next)
            {
                throw new InvalidOperationException(
                    "The fear-factor damage detour span no longer matches its audited ABI.");
            }

            int decodedLength = first.Length + second.Length;
            if (decodedLength != DamageDetourLength)
                throw new InvalidOperationException("The fear-factor damage detour boundary changed.");

            Instruction last = default;
            int totalLength = 0;
            decoder = CreateDecoder(memory, imageBase, DamageFunctionRva, DamageFunctionLength);
            while (totalLength < DamageFunctionLength)
            {
                decoder.Decode(out last);
                if (last.IsInvalid)
                    throw new InvalidOperationException("The fear-factor damage function contains invalid code.");
                totalLength += last.Length;
            }

            if (totalLength != DamageFunctionLength || last.Mnemonic != Mnemonic.Ret)
                throw new InvalidOperationException("The fear-factor damage function boundary changed.");
            if (memory[DamageFunctionRva + DamageFunctionLength] != 0xCC)
                throw new InvalidOperationException("The byte following the fear-factor damage function changed.");

            ValidateNoIncomingDirectBranchTargets(
                memory,
                imageBase,
                DamageFunctionRva,
                DamageFunctionLength,
                DamageFunctionRva,
                checked(DamageFunctionRva + DamageDetourLength),
                "fear-factor damage detour");
        }

        private static void ValidateUiFearLoad(ReadOnlySpan<byte> memory, ulong imageBase)
        {
            if (!Shared.NativePatternResolver.MatchesPatternAt(memory, UiPatternRva, UiFearLoadPattern))
                throw new InvalidOperationException("The fear-factor unit-overlay sequence changed.");

            Decoder decoder = CreateDecoder(memory, imageBase, UiHookRva, 20);
            decoder.Decode(out Instruction stride);
            decoder.Decode(out Instruction load);
            decoder.Decode(out Instruction test);
            if (stride.IsInvalid || stride.Length != 7 ||
                stride.Mnemonic != Mnemonic.Imul || stride.Op0Register != Register.RCX ||
                stride.Op1Register != Register.R10 || stride.Op2Kind != OpKind.Immediate32to64 ||
                stride.Immediate32 != 0x583C || stride.IsIPRelativeMemoryOperand ||
                stride.FlowControl != FlowControl.Next ||
                stride.Length + load.Length != UiHookLength ||
                test.IP != imageBase + (ulong)(UiHookRva + UiHookLength) ||
                load.IsInvalid || test.IsInvalid ||
                load.Length != UiFearLoadLength ||
                load.Mnemonic != Mnemonic.Mov ||
                load.Op0Kind != OpKind.Register || load.Op0Register != Register.EAX ||
                load.Op1Kind != OpKind.Memory || load.MemoryBase != Register.RCX ||
                load.MemoryIndex != Register.RDI || load.MemoryIndexScale != 1 ||
                load.MemoryDisplacement64 != 0x379CF04UL ||
                load.IsIPRelativeMemoryOperand || load.FlowControl != FlowControl.Next ||
                test.Length != 2 || test.Mnemonic != Mnemonic.Test ||
                test.Op0Register != Register.EAX || test.Op1Register != Register.EAX)
            {
                throw new InvalidOperationException(
                    "The fear-factor unit-overlay hook boundary or register contract changed.");
            }
        }

        private static void ValidateDamageCallers(ReadOnlySpan<byte> memory)
        {
            int meleeCalls = CountDirectCalls(
                memory,
                PreferredImageBase,
                MeleeCallerStartRva,
                MeleeCallerLength,
                DamageFunctionRva);
            int projectileCalls = CountDirectCalls(
                memory,
                PreferredImageBase,
                ProjectileCallerStartRva,
                ProjectileCallerLength,
                DamageFunctionRva);
            if (meleeCalls != 1 || projectileCalls != 1)
            {
                throw new InvalidOperationException(
                    $"Fear-factor damage caller topology changed: meleeCalls={meleeCalls}, " +
                    $"projectileCalls={projectileCalls}.");
            }
        }

        private static int CountDirectCalls(
            ReadOnlySpan<byte> memory,
            ulong imageBase,
            int functionRva,
            int functionLength,
            int targetRva)
        {
            Decoder decoder = CreateDecoder(memory, imageBase, functionRva, functionLength);
            ulong end = imageBase + unchecked((ulong)checked(functionRva + functionLength));
            int count = 0;
            while (decoder.IP < end)
            {
                decoder.Decode(out Instruction instruction);
                if (instruction.IsInvalid)
                    throw new InvalidOperationException("A fear-factor caller contains invalid code.");
                if (instruction.Mnemonic == Mnemonic.Call &&
                    instruction.Op0Kind == OpKind.NearBranch64 &&
                    instruction.NearBranchTarget == imageBase + unchecked((ulong)targetRva))
                {
                    count++;
                }
            }
            if (decoder.IP != end)
                throw new InvalidOperationException("A fear-factor caller boundary changed.");
            return count;
        }

        private static Decoder CreateDecoder(
            ReadOnlySpan<byte> memory,
            ulong imageBase,
            int rva,
            int maximumLength)
        {
            if (rva < 0 || maximumLength <= 0 || rva > memory.Length - maximumLength)
                throw new InvalidOperationException("A native validation range is outside the loaded image.");
            Decoder decoder = Decoder.Create(
                64,
                new ByteArrayCodeReader(memory.Slice(rva, maximumLength).ToArray()));
            decoder.IP = imageBase + unchecked((ulong)rva);
            return decoder;
        }

        private static void ValidateNoIncomingDirectBranchTargets(
            ReadOnlySpan<byte> memory,
            ulong imageBase,
            int functionRva,
            int functionLength,
            int hookStart,
            int hookEnd,
            string name)
        {
            Decoder decoder = CreateDecoder(memory, imageBase, functionRva, functionLength);
            ulong functionEnd = imageBase + unchecked((ulong)checked(functionRva + functionLength));
            while (decoder.IP < functionEnd)
            {
                decoder.Decode(out Instruction instruction);
                if (instruction.IsInvalid)
                    throw new InvalidOperationException($"The function containing the {name} contains invalid code.");
                if (IsNearBranch(instruction.Op0Kind))
                {
                    ulong target = instruction.NearBranchTarget;
                    ulong source = instruction.IP;
                    ulong hookStartAddress = imageBase + unchecked((ulong)hookStart);
                    ulong hookEndAddress = imageBase + unchecked((ulong)hookEnd);
                    bool sourceInsideSpan = source >= hookStartAddress && source < hookEndAddress;
                    if (!sourceInsideSpan && target > hookStartAddress && target < hookEndAddress)
                    {
                        throw new InvalidOperationException(
                            $"A direct branch at VA 0x{source:X} targets the interior of the {name} at VA 0x{target:X}.");
                    }
                }
            }
            if (decoder.IP != functionEnd)
                throw new InvalidOperationException($"The function containing the {name} changed boundary.");
        }

        private static bool IsNearBranch(OpKind kind) =>
            kind == OpKind.NearBranch16 ||
            kind == OpKind.NearBranch32 ||
            kind == OpKind.NearBranch64;
    }
}
