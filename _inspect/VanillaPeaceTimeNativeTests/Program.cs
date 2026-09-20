using Iced.Intel;
using RedBird.X64.Hooks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace ExtraFeatures
{
    internal static class Program
    {
        private const string DllPath = @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll";
        private const long Base = 0x180000000;
        private const ulong PeacePatchStubAddress = 0x180500000;
        private static int assertions;

        private static int Main()
        {
            try
            {
                byte[] file = File.ReadAllBytes(DllPath);
                Check(Hash(file) == VanillaPeaceTimeNativeContract.ReferenceSha256,
                    "canonical DLL hash matches the audited build");
                byte[] image = MapPeImage(file);
                TestNativeContract(image);
                Console.WriteLine($"PASS: Vanilla peace-time native tests ({assertions} assertions).");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("FAIL: " + ex);
                return 1;
            }
        }

        private static void TestNativeContract(byte[] image)
        {
            Check(VanillaPeaceTimeNativeContract.PatchSites.Length == 29,
                "catalog contains the fixed-rate initializer and every audited gameplay and timer mode bypass");
            Check(VanillaPeaceTimeNativeContract.PeaceFlagReferenceRvas.Length == 36,
                "audit accounts for all active-flag references");
            int[] alreadyModeIndependentReferences =
            {
                0x15D63, 0x15FD4, 0x22561, 0x2AEA4, 0x2AF1C,
                0xCA874, 0xCA8CD, 0xCA917, 0xCA92C,
                0xD1E95, 0xD1F42, 0xD2935, 0x10073D, 0x100820
            };
            int[] gameplayReferences =
            {
                0x88234, 0x8A14A, 0x8D368, 0x8D7C6, 0x8E04C, 0x8E0E0,
                0x8E601, 0x8EA43, 0x8EC88, 0xABC9D, 0xC70BC, 0xCDD6F,
                0x105534, 0x119110, 0x124B98, 0x1256A7, 0x1259D7,
                0x156E8D, 0x17F3CE, 0x1868A0, 0x186964, 0x186991
            };
            Check(alreadyModeIndependentReferences
                    .Concat(gameplayReferences)
                    .OrderBy(value => value)
                    .SequenceEqual(VanillaPeaceTimeNativeContract.PeaceFlagReferenceRvas),
                "all 36 active-flag references are classified exactly once");
            VanillaPeaceTimeNativeContract.Validate(image, unchecked((ulong)Base), true);
            Expect<InvalidOperationException>(
                () => VanillaPeaceTimeNativeContract.Validate(image, unchecked((ulong)Base), false),
                "patch rejects an unaudited native hash");

            foreach (VanillaPeaceTimePatchSite site in VanillaPeaceTimeNativeContract.PatchSites)
                TestPatchGenerator(site);

            TestPeaceTimeUpdateCaller(image);
            TestPeaceTimeFixedRatePatch(image);
            TestStartingTroopsGuardGenerator(image);
        }

        private static void TestPeaceTimeUpdateCaller(byte[] image)
        {
            Instruction gate = DecodeAt(
                image,
                VanillaPeaceTimeNativeContract.PeaceTimeUpdateModeGateRva,
                2,
                "peace-time update mode gate");
            Check(gate.Mnemonic == Mnemonic.Je &&
                gate.NearBranchTarget == unchecked((ulong)(Base +
                    VanillaPeaceTimeNativeContract.PeaceTimeUpdateCallRva + 5)),
                "mode 0 alone skips the Vanilla peace-time update call before patching");

            Instruction call = DecodeAt(
                image,
                VanillaPeaceTimeNativeContract.PeaceTimeUpdateCallRva,
                5,
                "peace-time update call");
            Check(call.Mnemonic == Mnemonic.Call &&
                call.NearBranchTarget == unchecked((ulong)(Base +
                    VanillaPeaceTimeNativeContract.PeaceTimeUpdateFunctionRva)),
                "the opened branch invokes Vanilla's display and expiry function");

            VanillaPeaceTimePatchSite timerPatch =
                VanillaPeaceTimeNativeContract.PatchSites.Single(site =>
                    site.Rva == VanillaPeaceTimeNativeContract.PeaceTimeUpdateModeGateRva);
            var assembler = new Assembler(64);
            VanillaPeaceTimeNativeContract.EmitPatch(
                assembler,
                timerPatch,
                unchecked((ulong)Base));
            Instruction[] patched = DecodeExact(
                Assemble(
                    assembler,
                    unchecked((ulong)(Base + timerPatch.Rva)),
                    timerPatch.Name),
                unchecked((ulong)(Base + timerPatch.Rva)),
                timerPatch.Name);
            Check(patched.All(instruction => instruction.Mnemonic == Mnemonic.Nop),
                "timer patch only removes the mode-0 skip; modes 1 and 99 retain their fallthrough");
        }

        private static void TestPeaceTimeFixedRatePatch(byte[] image)
        {
            Instruction original = DecodeAt(
                image,
                VanillaPeaceTimeNativeContract.PeaceTimeFixedRatePatchRva,
                7,
                "Vanilla peace-time StartingGameSpeed multiplication");
            Check(original.Mnemonic == Mnemonic.Imul &&
                original.Op0Register == Register.EDX &&
                original.IsIPRelativeMemoryOperand &&
                original.IPRelativeMemoryAddress == unchecked((ulong)(Base +
                    VanillaPeaceTimeNativeContract.StartingGameSpeedRva)),
                "Vanilla initializer originally multiplies minutes by StartingGameSpeed");

            VanillaPeaceTimePatchSite site =
                VanillaPeaceTimeNativeContract.PatchSites.Single(candidate =>
                    candidate.Rva == VanillaPeaceTimeNativeContract.PeaceTimeFixedRatePatchRva);
            var assembler = new Assembler(64);
            VanillaPeaceTimeNativeContract.EmitPatch(
                assembler,
                site,
                unchecked((ulong)Base));
            byte[] bytes = Assemble(
                assembler,
                unchecked((ulong)(Base + site.Rva)),
                site.Name);
            Check(bytes.SequenceEqual(new byte[] { 0x6B, 0xD2, 0x28, 0x90, 0x90, 0x90, 0x90 }),
                "fixed-rate patch emits imul edx, edx, 40 followed by four one-byte NOPs");
            Instruction[] patched = DecodeExact(
                bytes,
                unchecked((ulong)(Base + site.Rva)),
                site.Name);
            Check(patched.Length == 5 &&
                patched[0].Mnemonic == Mnemonic.Imul &&
                patched[0].Op0Register == Register.EDX &&
                patched[0].Op1Register == Register.EDX &&
                patched[0].Immediate8 == VanillaPeaceTimeNativeContract.PeaceTimeTicksPerSecond &&
                patched.Skip(1).All(instruction => instruction.Mnemonic == Mnemonic.Nop),
                "fixed-rate replacement fully decodes to the intended seven-byte sequence");

            byte[] initializer = new byte[VanillaPeaceTimeNativeContract.PeaceTimeInitializerLength];
            Buffer.BlockCopy(
                image,
                VanillaPeaceTimeNativeContract.PeaceTimeInitializerRva,
                initializer,
                0,
                initializer.Length);
            Instruction[] initializerInstructions = DecodeExact(
                initializer,
                unchecked((ulong)(Base + VanillaPeaceTimeNativeContract.PeaceTimeInitializerRva)),
                "Vanilla peace-time initializer");
            ulong interiorStart = unchecked((ulong)(Base +
                VanillaPeaceTimeNativeContract.PeaceTimeFixedRatePatchRva + 1));
            ulong interiorEnd = unchecked((ulong)(Base +
                VanillaPeaceTimeNativeContract.PeaceTimeFixedRatePatchRva + 7));
            Check(!initializerInstructions.Any(instruction =>
                (instruction.FlowControl == FlowControl.ConditionalBranch ||
                 instruction.FlowControl == FlowControl.UnconditionalBranch ||
                 instruction.FlowControl == FlowControl.Call) &&
                instruction.NearBranchTarget >= interiorStart &&
                instruction.NearBranchTarget < interiorEnd),
                "no direct control transfer enters RVA CA905-CA90A");
        }

        private static void TestPatchGenerator(VanillaPeaceTimePatchSite site)
        {
            ulong origin = unchecked((ulong)(Base + site.Rva));
            var assembler = new Assembler(64);
            VanillaPeaceTimeNativeContract.EmitPatch(assembler, site, unchecked((ulong)Base));
            byte[] bytes = Assemble(assembler, origin, site.Name);
            Check(bytes.Length == site.ExpectedBytes.Length,
                site.Name + " preserves the audited instruction span");

            Instruction[] instructions = DecodeExact(bytes, origin, site.Name);
            byte[] originalBytes = new byte[site.ExpectedBytes.Length];
            Buffer.BlockCopy(site.ExpectedBytes, 0, originalBytes, 0, originalBytes.Length);
            Instruction original = DecodeExact(originalBytes, origin, site.Name + " original")[0];
            switch (site.Kind)
            {
                case VanillaPeaceTimePatchKind.MultiplyEdxByForty:
                    Check(instructions.Length == 5 &&
                        instructions[0].Mnemonic == Mnemonic.Imul &&
                        instructions[0].Op0Register == Register.EDX &&
                        instructions[0].Op1Register == Register.EDX &&
                        instructions[0].Immediate8 ==
                            VanillaPeaceTimeNativeContract.PeaceTimeTicksPerSecond &&
                        instructions.Skip(1).All(instruction => instruction.Mnemonic == Mnemonic.Nop),
                        site.Name + " fixes the initializer at 40 ticks per second");
                    break;
                case VanillaPeaceTimePatchKind.Nop:
                    foreach (Instruction instruction in instructions)
                        Check(instruction.Mnemonic == Mnemonic.Nop, site.Name + " emits only NOPs");
                    break;
                case VanillaPeaceTimePatchKind.MoveEbxEdi:
                    Check(instructions[0].Mnemonic == Mnemonic.Mov &&
                        instructions[0].Op0Register == Register.EBX &&
                        instructions[0].Op1Register == Register.EDI,
                        site.Name + " selects the peace-aware mode-0 value");
                    break;
                case VanillaPeaceTimePatchKind.MoveEdiEbx:
                    Check(instructions[0].Mnemonic == Mnemonic.Mov &&
                        instructions[0].Op0Register == Register.EDI &&
                        instructions[0].Op1Register == Register.EBX,
                        site.Name + " selects the peace-aware mode-0 value");
                    break;
                case VanillaPeaceTimePatchKind.MoveEbxEbp:
                    Check(instructions[0].Mnemonic == Mnemonic.Mov &&
                        instructions[0].Op0Register == Register.EBX &&
                        instructions[0].Op1Register == Register.EBP,
                        site.Name + " selects the peace-aware mode-0 value");
                    break;
                case VanillaPeaceTimePatchKind.JumpEqual:
                    Check(instructions[0].Mnemonic == Mnemonic.Je &&
                        instructions[0].NearBranchTarget == unchecked((ulong)(Base + site.TargetRva)),
                        site.Name + " redirects only the audited mode branch");
                    break;
                case VanillaPeaceTimePatchKind.Jump:
                    Check(instructions[0].Mnemonic == Mnemonic.Jmp &&
                        instructions[0].NearBranchTarget == unchecked((ulong)(Base + site.TargetRva)),
                        site.Name + " preserves the audited active-peace exit");
                    Check(original.NearBranchTarget == instructions[0].NearBranchTarget,
                        site.Name + " keeps Vanilla's original taken-branch destination");
                    break;
                default:
                    throw new InvalidOperationException("Unknown peace-time patch kind in test.");
            }
        }

        private static void TestStartingTroopsGuardGenerator(byte[] image)
        {
            ulong flagAddress = unchecked((ulong)(Base +
                VanillaPeaceTimeNativeContract.PeaceTimeActiveFlagRva));
            var assembler = new Assembler(64);
            VanillaPeaceTimeNativeContract.EmitStartingTroopsGuardPrefix(assembler, flagAddress);
            assembler.nop(VanillaPeaceTimeNativeContract.StartingTroopsHookLength);
            byte[] bytes = Assemble(assembler, PeacePatchStubAddress, "starting-troop entry guard");
            Instruction[] instructions = DecodeExact(bytes, PeacePatchStubAddress,
                "starting-troop entry guard");

            Check(instructions.Length >= 5 &&
                instructions[0].Mnemonic == Mnemonic.Mov &&
                instructions[0].Op0Register == Register.RAX &&
                instructions[0].Immediate64 == flagAddress,
                "starting-troop guard reads the audited active flag directly");
            Check(instructions[1].Mnemonic == Mnemonic.Cmp &&
                instructions[1].MemoryBase == Register.RAX &&
                instructions[1].Immediate8 == 0,
                "starting-troop guard tests the active flag");
            Check(instructions[2].Mnemonic == Mnemonic.Je &&
                instructions[3].Mnemonic == Mnemonic.Ret &&
                instructions[2].NearBranchTarget == instructions[4].IP,
                "starting-troop guard returns only while peace time is active");

            byte[] copiedEntry = new byte[64];
            Buffer.BlockCopy(image, VanillaPeaceTimeNativeContract.StartingTroopsDispatcherRva,
                copiedEntry, 0, copiedEntry.Length);
            IntPtr copiedMemory = Marshal.AllocHGlobal(copiedEntry.Length);
            try
            {
                Marshal.Copy(copiedEntry, 0, copiedMemory, copiedEntry.Length);
                using (var probe = new X64InlineHook(
                    unchecked((ulong)copiedMemory.ToInt64()),
                    VanillaPeaceTimeNativeContract.StartingTroopsHookLength))
                {
                    Check(probe.DisplacedByteCount ==
                        VanillaPeaceTimeNativeContract.StartingTroopsHookLength,
                        "installed RedBird backend displaces the audited 16-byte entry span");
                }
            }
            finally
            {
                Marshal.FreeHGlobal(copiedMemory);
            }
        }

        private static byte[] Assemble(Assembler assembler, ulong origin, string name)
        {
            using (var stream = new MemoryStream())
            {
                var writer = new StreamCodeWriter(stream);
                Check(assembler.TryAssemble(writer, origin, out string error, out _),
                    name + " assembles with Iced: " + error);
                return stream.ToArray();
            }
        }

        private static Instruction[] DecodeExact(byte[] bytes, ulong origin, string name)
        {
            Decoder decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes));
            decoder.IP = origin;
            ulong end = origin + unchecked((ulong)bytes.Length);
            var instructions = new List<Instruction>();
            while (decoder.IP < end)
            {
                Instruction instruction = decoder.Decode();
                Check(!instruction.IsInvalid && instruction.NextIP <= end,
                    name + " fully decodes on instruction boundaries");
                instructions.Add(instruction);
            }
            Check(decoder.IP == end, name + " consumes the complete generated span");
            return instructions.ToArray();
        }

        private static Instruction DecodeAt(byte[] image, int rva, int length, string name)
        {
            byte[] bytes = new byte[length];
            Buffer.BlockCopy(image, rva, bytes, 0, length);
            Instruction[] instructions = DecodeExact(
                bytes,
                unchecked((ulong)(Base + rva)),
                name);
            Check(instructions.Length == 1, name + " is one complete instruction");
            return instructions[0];
        }

        private static byte[] MapPeImage(byte[] file)
        {
            int pe = ReadInt32(file, 0x3C);
            int count = ReadUInt16(file, pe + 6);
            int optionalSize = ReadUInt16(file, pe + 20);
            int optional = pe + 24;
            int imageSize = ReadInt32(file, optional + 56);
            int headers = ReadInt32(file, optional + 60);
            var image = new byte[imageSize];
            Buffer.BlockCopy(file, 0, image, 0, Math.Min(headers, file.Length));
            int table = optional + optionalSize;
            for (int index = 0; index < count; index++)
            {
                int header = table + index * 40;
                int virtualAddress = ReadInt32(file, header + 12);
                int rawSize = ReadInt32(file, header + 16);
                int raw = ReadInt32(file, header + 20);
                if (rawSize > 0)
                    Buffer.BlockCopy(file, raw, image, virtualAddress,
                        Math.Min(rawSize, file.Length - raw));
            }
            return image;
        }

        private static int ReadInt32(byte[] value, int offset) =>
            value[offset] | value[offset + 1] << 8 |
            value[offset + 2] << 16 | value[offset + 3] << 24;

        private static int ReadUInt16(byte[] value, int offset) =>
            value[offset] | value[offset + 1] << 8;

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

        private static void Expect<T>(Action action, string message) where T : Exception
        {
            assertions++;
            try { action(); }
            catch (T) { return; }
            throw new InvalidOperationException(message);
        }
    }
}
