using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Iced.Intel;
using RedBird.X64.Extensions;
using RedBird.X64.Hooks;

namespace ExtraFeatures
{
    internal static class Program
    {
        private const string DllPath = @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll";
        private static int assertions;

        private static int Main()
        {
            try
            {
                byte[] file = File.ReadAllBytes(DllPath);
                Check(Hash(file) == ElevatedMoatNativeContract.ReferenceSha256, "canonical DLL hash");
                byte[] image = MapPeImage(file);
                ElevatedMoatNativeContract.ValidateAdaptiveHeightHooks(image);
                Check(ElevatedMoatNativeContract.LowerDrawbridgeHookLength == 15,
                    "lowered-drawbridge RedBird hook spans exactly 15 bytes");
                Check(ElevatedMoatNativeContract.LowerDrawbridgeHeightWriteLength == 8,
                    "lowered-drawbridge height write spans exactly 8 bytes");
                Check(ElevatedMoatNativeContract.LowerDrawbridgeImageBaseLeaRva ==
                    ElevatedMoatNativeContract.LowerDrawbridgeHeightWriteRva + 8,
                    "image-base LEA immediately follows the height write");
                Check(ElevatedMoatNativeContract.CompletedDrawbridgeHookLength == 17,
                    "completed-drawbridge RedBird hook spans exactly 17 bytes");
                Check(ElevatedMoatNativeContract.CompletedDrawbridgeHeightWriteLength == 9,
                    "completed-drawbridge height write spans exactly 9 bytes");
                ExpectContractFailure(image, ElevatedMoatNativeContract.LowerDrawbridgeHeightWriteRva + 2,
                    "lowered-drawbridge RBX/RDI operand mutation");
                ExpectContractFailure(image, ElevatedMoatNativeContract.LowerDrawbridgeImageBaseLeaRva + 2,
                    "lowered-drawbridge image-base LEA mutation");
                ExpectContractFailure(image,
                    ElevatedMoatNativeContract.CompletedDrawbridgeHeightWriteRva + 3,
                    "completed-drawbridge RBX/R14 operand mutation");
                ExpectContractFailure(image,
                    ElevatedMoatNativeContract.CompletedDrawbridgeStateCallRva,
                    "completed-drawbridge state call mutation");
                ExpectContractFailure(image,
                    ElevatedMoatNativeContract.CompletedDrawbridgeJumpRva,
                    "completed-drawbridge continuation mutation");
                ValidateInstalledRedBirdSpans();
                ValidateProductionGenerators(image);
                CheckNoIncomingTargets(
                    image,
                    ElevatedMoatNativeContract.CompletedDrawbridgeHookRva,
                    ElevatedMoatNativeContract.CompletedDrawbridgeHookLength,
                    ElevatedMoatNativeContract.DrawbridgeFunctionRva,
                    ElevatedMoatNativeContract.DrawbridgeFunctionLength,
                    "completed-drawbridge hook block");
                CheckNoIncomingTargets(
                    image,
                    ElevatedMoatNativeContract.LowerDrawbridgeHookRva,
                    ElevatedMoatNativeContract.LowerDrawbridgeHookLength,
                    ElevatedMoatNativeContract.LowerDrawbridgeFunctionRva,
                    ElevatedMoatNativeContract.LowerDrawbridgeFunctionLength,
                    "lowered-drawbridge hook block");
                CheckHeight(0, 0, 0);
                CheckHeight(8, 0, 0);
                CheckHeight(12, 4, 4);
                CheckHeight(13, 5, 13);
                CheckHeight(16, 8, 16);
                CheckHeight(255, 247, 255);
                Console.WriteLine($"PASS: elevated-moat height tests ({assertions} assertions).");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("FAIL: " + exception);
                return 1;
            }
        }

        private static void CheckHeight(byte defaultHeight, byte expectedMoat, byte expectedDrawbridge)
        {
            Check(ElevatedMoatNativeContract.CalculateCompletedHeight(defaultHeight) == expectedMoat,
                $"moat height for {defaultHeight}");
            Check(ElevatedMoatNativeContract.CalculateDrawbridgeHeight(defaultHeight) == expectedDrawbridge,
                $"drawbridge height for {defaultHeight}");
            Check(ElevatedMoatNativeContract.CalculateRestoredHeight(defaultHeight) == defaultHeight,
                $"restored height for {defaultHeight}");
        }

        private static void ValidateInstalledRedBirdSpans()
        {
            byte[] lowered = SliceFixture(
                ElevatedMoatNativeContract.LowerDrawbridgeHookBytes,
                16);
            byte[] completed = SliceFixture(
                ElevatedMoatNativeContract.CompletedDrawbridgeHookBytes,
                16);
            byte[] completedAtWrite =
            {
                0x41, 0xC6, 0x84, 0x1E, 0xA0, 0xE5, 0xD7, 0x00, 0x00,
                0xEB, 0x0C,
                0x42, 0x81, 0xA4, 0xB3, 0x00, 0x84, 0x89, 0x00, 0xFF, 0xFF, 0xFF, 0xBF
            };

            CheckRedBirdSpan(lowered, 8, 15,
                "requested 8-byte lowered-drawbridge span expands to 15 bytes");
            CheckRedBirdSpan(lowered, 15, 15,
                "audited lowered-drawbridge span remains 15 bytes");
            CheckRedBirdSpan(completedAtWrite, 9, 23,
                "requested 9-byte completed-drawbridge span expands to 23 bytes");
            CheckRedBirdSpan(completed, 17, 17,
                "audited completed-drawbridge span remains 17 bytes");
        }

        private static void ValidateProductionGenerators(byte[] image)
        {
            const ulong imageBase = 0x180000000;
            byte[] completed = new byte[ElevatedMoatNativeContract.CompletedDrawbridgeHookLength];
            Buffer.BlockCopy(image, ElevatedMoatNativeContract.CompletedDrawbridgeHookRva,
                completed, 0, completed.Length);
            Instruction[] completedInstructions = DecodeExact(
                completed,
                imageBase + (ulong)ElevatedMoatNativeContract.CompletedDrawbridgeHookRva);
            byte[] completedStub = AssembleAndDecode(
                (assembler, returnAddress) => ElevatedMoatDrawbridgeHooks.GenerateCompleted(
                    assembler,
                    completedInstructions,
                    returnAddress,
                    imageBase + 0x2000000,
                    imageBase + (ulong)ElevatedMoatNativeContract.DrawbridgeStateUpdateRva),
                imageBase + (ulong)ElevatedMoatNativeContract.CompletedDrawbridgeHookRva +
                    (ulong)ElevatedMoatNativeContract.CompletedDrawbridgeHookLength,
                "completed-drawbridge production generator");
            ValidateCompletedGeneratorOutput(
                completedStub,
                imageBase + (ulong)ElevatedMoatNativeContract.DrawbridgeStateUpdateRva,
                imageBase + (ulong)ElevatedMoatNativeContract.CompletedDrawbridgeHookRva +
                    (ulong)ElevatedMoatNativeContract.CompletedDrawbridgeHookLength);

            byte[] lowered = new byte[ElevatedMoatNativeContract.LowerDrawbridgeHookLength];
            Buffer.BlockCopy(image, ElevatedMoatNativeContract.LowerDrawbridgeHookRva,
                lowered, 0, lowered.Length);
            Instruction[] loweredInstructions = DecodeExact(
                lowered,
                imageBase + (ulong)ElevatedMoatNativeContract.LowerDrawbridgeHookRva);
            byte[] loweredStub = AssembleAndDecode(
                (assembler, returnAddress) => ElevatedMoatDrawbridgeHooks.GenerateLowered(
                    assembler,
                    loweredInstructions,
                    returnAddress,
                    imageBase + 0x2000000,
                    imageBase),
                imageBase + (ulong)ElevatedMoatNativeContract.LowerDrawbridgeContinuationRva,
                "lowered-drawbridge production generator");
            ValidateLoweredGeneratorOutput(
                loweredStub,
                imageBase,
                imageBase + (ulong)ElevatedMoatNativeContract.LowerDrawbridgeContinuationRva);
        }

        private static byte[] SliceFixture(byte[] prefix, int trailingNops)
        {
            byte[] fixture = new byte[prefix.Length + trailingNops];
            Buffer.BlockCopy(prefix, 0, fixture, 0, prefix.Length);
            for (int index = prefix.Length; index < fixture.Length; index++)
                fixture[index] = 0x90;
            return fixture;
        }

        private static void CheckRedBirdSpan(
            byte[] fixture,
            int requestedLength,
            int expectedLength,
            string message)
        {
            IntPtr memory = Marshal.AllocHGlobal(fixture.Length);
            try
            {
                Marshal.Copy(fixture, 0, memory, fixture.Length);
                using (var probe = new X64InlineHook(
                    unchecked((ulong)memory.ToInt64()), requestedLength, null, message))
                {
                    Check(probe.DisplacedByteCount == expectedLength, message);
                    Check(!probe.IsInstalled, message + " remains decode-only");
                }

                byte[] after = new byte[fixture.Length];
                Marshal.Copy(memory, after, 0, after.Length);
                Check(AreEqual(fixture, after), message + " preserves fixture bytes");
            }
            finally
            {
                Marshal.FreeHGlobal(memory);
            }
        }

        private static Instruction[] DecodeExact(byte[] bytes, ulong instructionPointer)
        {
            var reader = new ByteArrayCodeReader(bytes);
            Decoder decoder = Decoder.Create(64, reader);
            decoder.IP = instructionPointer;
            ulong end = instructionPointer + (ulong)bytes.Length;
            var instructions = new List<Instruction>();
            while (decoder.IP < end)
            {
                Instruction instruction = decoder.Decode();
                Check(!instruction.IsInvalid && instruction.NextIP <= end,
                    "Vanilla hook fixture decodes on exact instruction boundaries");
                instructions.Add(instruction);
            }

            Check(decoder.IP == end, "Vanilla hook fixture consumes its exact span");
            return instructions.ToArray();
        }

        private static byte[] AssembleAndDecode(
            Action<Assembler, ulong> generate,
            ulong returnAddress,
            string description)
        {
            var assembler = new Assembler(64);
            generate(assembler, returnAddress);
            assembler.AddUnrestrictedJmp(returnAddress);

            const ulong stubAddress = 0x180100000;
            byte[] stub;
            using (var stream = new MemoryStream())
            {
                var writer = new StreamCodeWriter(stream);
                Check(assembler.TryAssemble(writer, stubAddress, out string error, out _),
                    description + " assembles: " + error);
                stub = stream.ToArray();
            }

            int offset = 0;
            int decoded = 0;
            while (offset < stub.Length)
            {
                if (IsAbsoluteJump(stub, offset))
                {
                    offset += 14;
                    decoded++;
                    continue;
                }

                byte[] remaining = new byte[stub.Length - offset];
                Buffer.BlockCopy(stub, offset, remaining, 0, remaining.Length);
                var reader = new ByteArrayCodeReader(remaining);
                Decoder decoder = Decoder.Create(64, reader);
                decoder.IP = stubAddress + (ulong)offset;
                Instruction instruction = decoder.Decode();
                Check(!instruction.IsInvalid && instruction.Length <= remaining.Length,
                    description + " decodes without truncated instructions");
                offset += instruction.Length;
                decoded++;
            }

            Check(offset == stub.Length && decoded > 0,
                description + " consumes the full generated stub");
            return stub;
        }

        private static void ValidateCompletedGeneratorOutput(
            byte[] stub,
            ulong stateUpdateAddress,
            ulong returnAddress)
        {
            Instruction[] instructions = DecodeGeneratedInstructions(stub, out ulong[] jumpTargets);
            int stateCalls = 0;
            int heightWrites = 0;
            foreach (Instruction instruction in instructions)
            {
                if (instruction.Mnemonic == Mnemonic.Call &&
                    instruction.NearBranchTarget == stateUpdateAddress)
                {
                    stateCalls++;
                }
                if (instruction.Mnemonic == Mnemonic.Mov &&
                    HasMemoryOperands(instruction, Register.RBX, Register.R14) &&
                    instruction.MemoryDisplacement64 ==
                        ElevatedMoatNativeContract.TileHeightGridOffset)
                {
                    heightWrites++;
                }
            }

            Check(stateCalls == 1,
                "completed generator calls Vanilla state update exactly once");
            Check(heightWrites == 2,
                "completed generator emits adaptive and Vanilla height writes");
            Check(CountBranchTargets(instructions, jumpTargets, returnAddress) == 2,
                "completed generator returns both height branches to Vanilla continuation");
        }

        private static void ValidateLoweredGeneratorOutput(
            byte[] stub,
            ulong imageBase,
            ulong returnAddress)
        {
            Instruction[] instructions = DecodeGeneratedInstructions(stub, out ulong[] jumpTargets);
            int heightWrites = 0;
            int imageBaseLoads = 0;
            foreach (Instruction instruction in instructions)
            {
                if (instruction.Mnemonic == Mnemonic.Mov &&
                    HasMemoryOperands(instruction, Register.RBX, Register.RDI) &&
                    instruction.MemoryDisplacement64 ==
                        ElevatedMoatNativeContract.TileHeightGridOffset)
                {
                    heightWrites++;
                }
                if (instruction.Mnemonic == Mnemonic.Lea &&
                    instruction.Op0Register == Register.RDI &&
                    instruction.MemoryBase == Register.RIP &&
                    instruction.MemoryDisplacement64 == imageBase)
                {
                    imageBaseLoads++;
                }
            }

            Check(heightWrites == 1,
                "lowered generator retains only the disabled Vanilla height write");
            Check(imageBaseLoads == 1,
                "lowered generator restores RDI to the image base exactly once");
            Check(CountBranchTargets(instructions, jumpTargets, returnAddress) == 1,
                "lowered generator returns to the exact Vanilla continuation");
        }

        private static Instruction[] DecodeGeneratedInstructions(
            byte[] stub,
            out ulong[] absoluteJumpTargets)
        {
            const ulong stubAddress = 0x180100000;
            var instructions = new List<Instruction>();
            var jumpTargets = new List<ulong>();
            int offset = 0;
            while (offset < stub.Length)
            {
                if (IsAbsoluteJump(stub, offset))
                {
                    jumpTargets.Add(BitConverter.ToUInt64(stub, offset + 6));
                    offset += 14;
                    continue;
                }

                var reader = new ByteArrayCodeReader(Slice(stub, offset));
                Decoder decoder = Decoder.Create(64, reader);
                decoder.IP = stubAddress + (ulong)offset;
                Instruction instruction = decoder.Decode();
                Check(!instruction.IsInvalid && instruction.Length <= stub.Length - offset,
                    "generated instruction decodes completely");
                instructions.Add(instruction);
                offset += instruction.Length;
            }

            absoluteJumpTargets = jumpTargets.ToArray();
            return instructions.ToArray();
        }

        private static bool HasMemoryOperands(
            Instruction instruction,
            Register first,
            Register second) =>
            (instruction.MemoryBase == first && instruction.MemoryIndex == second) ||
            (instruction.MemoryBase == second && instruction.MemoryIndex == first);

        private static int CountBranchTargets(
            Instruction[] instructions,
            ulong[] absoluteTargets,
            ulong expected)
        {
            int count = 0;
            foreach (Instruction instruction in instructions)
            {
                if ((instruction.Op0Kind == OpKind.NearBranch16 ||
                     instruction.Op0Kind == OpKind.NearBranch32 ||
                     instruction.Op0Kind == OpKind.NearBranch64) &&
                    instruction.NearBranchTarget == expected)
                {
                    count++;
                }
            }
            foreach (ulong value in absoluteTargets)
            {
                if (value == expected)
                    count++;
            }
            return count;
        }

        private static byte[] Slice(byte[] source, int offset)
        {
            byte[] result = new byte[source.Length - offset];
            Buffer.BlockCopy(source, offset, result, 0, result.Length);
            return result;
        }

        private static bool IsAbsoluteJump(byte[] code, int offset) =>
            code.Length - offset >= 14 &&
            code[offset] == 0xFF &&
            code[offset + 1] == 0x25 &&
            code[offset + 2] == 0 &&
            code[offset + 3] == 0 &&
            code[offset + 4] == 0 &&
            code[offset + 5] == 0;

        private static bool AreEqual(byte[] left, byte[] right)
        {
            if (left.Length != right.Length)
                return false;
            for (int index = 0; index < left.Length; index++)
            {
                if (left[index] != right[index])
                    return false;
            }
            return true;
        }

        private static void CheckNoIncomingTargets(
            byte[] image,
            int hookRva,
            int hookLength,
            int functionRva,
            int functionLength,
            string description)
        {
            const ulong imageBase = 0x180000000;
            byte[] function = new byte[functionLength];
            Buffer.BlockCopy(image, functionRva, function, 0, function.Length);
            var reader = new ByteArrayCodeReader(function);
            Decoder decoder = Decoder.Create(64, reader);
            decoder.IP = imageBase + (ulong)functionRva;
            ulong functionEnd = decoder.IP + (ulong)functionLength;
            ulong hookStart = imageBase + (ulong)hookRva;
            ulong hookEnd = hookStart + (ulong)hookLength;
            while (decoder.IP < functionEnd)
            {
                Instruction instruction = decoder.Decode();
                Check(!instruction.IsInvalid && instruction.NextIP <= functionEnd,
                    description + " owner function decodes completely");
                if (instruction.IP >= hookStart && instruction.IP < hookEnd)
                    continue;
                if (instruction.Op0Kind != OpKind.NearBranch16 &&
                    instruction.Op0Kind != OpKind.NearBranch32 &&
                    instruction.Op0Kind != OpKind.NearBranch64)
                {
                    continue;
                }

                ulong target = instruction.NearBranchTarget;
                Check(target <= hookStart || target >= hookEnd,
                    description + " has no incoming direct target inside its displaced span");
            }
        }

        private static byte[] MapPeImage(byte[] file)
        {
            int pe = BitConverter.ToInt32(file, 0x3C);
            int count = BitConverter.ToUInt16(file, pe + 6);
            int optionalSize = BitConverter.ToUInt16(file, pe + 20);
            int optional = pe + 24;
            var image = new byte[BitConverter.ToInt32(file, optional + 56)];
            Buffer.BlockCopy(file, 0, image, 0, BitConverter.ToInt32(file, optional + 60));
            int table = optional + optionalSize;
            for (int index = 0; index < count; index++)
            {
                int header = table + index * 40;
                int virtualAddress = BitConverter.ToInt32(file, header + 12);
                int rawSize = BitConverter.ToInt32(file, header + 16);
                int raw = BitConverter.ToInt32(file, header + 20);
                if (rawSize > 0)
                    Buffer.BlockCopy(file, raw, image, virtualAddress, Math.Min(rawSize, file.Length - raw));
            }
            return image;
        }

        private static string Hash(byte[] value)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(value)).Replace("-", string.Empty);
        }

        private static void Check(bool condition, string message)
        {
            assertions++;
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private static void ExpectContractFailure(byte[] image, int mutationRva, string message)
        {
            assertions++;
            byte[] changed = (byte[])image.Clone();
            changed[mutationRva] ^= 1;
            try
            {
                ElevatedMoatNativeContract.ValidateAdaptiveHeightHooks(changed);
            }
            catch (InvalidOperationException)
            {
                return;
            }

            throw new InvalidOperationException(message);
        }
    }
}
