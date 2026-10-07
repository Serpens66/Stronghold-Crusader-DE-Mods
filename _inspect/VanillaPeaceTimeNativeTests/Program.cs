using Iced.Intel;
using SHCDESE.Interop;
using RedBird.X64.Extensions;
using RedBird.X64.Hooks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace BugfixesAndQoL
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
            Check(VanillaPeaceTimeNativeContract.PatchSites.Length == 28,
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
            TestWildlifeBehavior(image);
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
                case VanillaPeaceTimePatchKind.MoveEaxOne:
                    Check(bytes.SequenceEqual(new byte[] { 0xB8, 1, 0, 0, 0, 0x90 }),
                        "peace-only mode override is mov eax,1 plus NOP in exactly six bytes");
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

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int Exclusion(IntPtr manager, int attackerId, int targetId, byte filter);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr size, uint allocation, uint protection);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualFree(IntPtr address, UIntPtr size, uint freeType);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualProtect(IntPtr address, UIntPtr size, uint protection, out uint oldProtection);
        [DllImport("kernel32.dll")]
        private static extern bool FlushInstructionCache(IntPtr process, IntPtr address, UIntPtr size);

        private static void TestWildlifeBehavior(byte[] image)
        {
            Check(IntPtr.Size == 8, "native behavior tests run as x64");
            const int classByteOffset = 0x984; // Native sentinel + GameUnit.N000000B8 (+0x328).
            const int teamsRva = 0x37EDF3C;
            const int modeRva = 0x8574B90;
            int codeOffset = (image.Length + 4095) & ~4095;
            IntPtr arena = VirtualAlloc(IntPtr.Zero, (UIntPtr)(codeOffset + 0x4000), 0x3000, 0x04);
            Check(arena != IntPtr.Zero, "isolated native image arena allocated");
            IntPtr manager = Marshal.AllocHGlobal(0x3000);
            X64InlineHook guard = null;
            try
            {
                Marshal.Copy(new byte[0x3000], 0, manager, 0x3000);
                ulong arenaBase = unchecked((ulong)arena.ToInt64());
                IntPtr vanillaAddress = IntPtr.Add(arena, codeOffset);
                IntPtr correctedAddress = IntPtr.Add(arena, codeOffset + 0x1000);
                IntPtr legacyAddress = IntPtr.Add(arena, codeOffset + 0x2000);
                WriteExclusionCopy(image, vanillaAddress, arenaBase, false, false);
                WriteExclusionCopy(image, correctedAddress, arenaBase, true, false);
                WriteExclusionCopy(image, legacyAddress, arenaBase, false, true);
                Check(VirtualProtect(vanillaAddress, (UIntPtr)0x3000, 0x20, out _),
                    "only isolated native function pages become executable");
                Check(FlushInstructionCache(new IntPtr(-1), vanillaAddress, (UIntPtr)0x3000),
                    "isolated function instruction cache flushed");
                var vanilla = Marshal.GetDelegateForFunctionPointer<Exclusion>(vanillaAddress);
                var body = Marshal.GetDelegateForFunctionPointer<Exclusion>(correctedAddress);
                var legacy = Marshal.GetDelegateForFunctionPointer<Exclusion>(legacyAddress);

                guard = new X64InlineHook(unchecked((ulong)correctedAddress.ToInt64()),
                    VanillaPeaceTimeNativeContract.WildlifeHookLength);
                Check(guard.DisplacedByteCount == 18, "real installed inline backend displaces exactly 18 bytes");
                guard.Generate((assembler, instructions, continuation) =>
                {
                    VanillaPeaceTimeNativeContract.EmitWildlifeGuardPrefix(assembler,
                        arenaBase + VanillaPeaceTimeNativeContract.PeaceTimeActiveFlagRva);
                    assembler.AddInstructions(instructions);
                });
                var corrected = Marshal.GetDelegateForFunctionPointer<Exclusion>(guard.StubAddress);
                // Execute the prepared backend stub; never enable/restore a hook in the game or fixture.
                IntPtr mode = IntPtr.Add(arena, modeRva);
                IntPtr peace = IntPtr.Add(arena, VanillaPeaceTimeNativeContract.PeaceTimeActiveFlagRva);
                Marshal.WriteInt32(IntPtr.Add(arena, teamsRva), 0);
                Marshal.WriteInt32(IntPtr.Add(arena, teamsRva + 4), 1);
                Marshal.WriteByte(peace, 0);
                Marshal.WriteInt32(mode, 0);
                SetUnit(manager, 1, eChimps.CHIMP_TYPE_LION, 1, 0, classByteOffset);
                SetUnit(manager, 2, eChimps.CHIMP_TYPE_SPEARMAN, 0, 1, classByteOffset);
                Check(vanilla(manager, 1, 2, 0) == 0 && legacy(manager, 1, 2, 0) == 1 &&
                    corrected(manager, 1, 2, 0) == 0,
                    "legacy unconditional shared branch reproduces lion immunity; corrected native stub restores Vanilla");
                SetUnit(manager, 1, eChimps.CHIMP_TYPE_HYENA, 1, 0, classByteOffset);
                Check(vanilla(manager, 1, 2, 0) == 0 && legacy(manager, 1, 2, 0) == 1 &&
                    corrected(manager, 1, 2, 0) == 0, "legacy hyena immunity is reproduced and corrected");

                long inactiveCases = 0, activeCases = 0;
                foreach (int nativeMode in new[] { 0, 1, 99 })
                foreach (byte active in new byte[] { 0, 1 })
                foreach (int sameTeam in new[] { 0, 1 })
                {
                    Marshal.WriteInt32(mode, nativeMode);
                    Marshal.WriteByte(peace, active);
                    Marshal.WriteInt32(IntPtr.Add(arena, teamsRva + 4), sameTeam == 0 ? 1 : 0);
                    for (int attacker = 0; attacker < (int)eChimps.CHIMP_NUM_TYPES; attacker++)
                    for (int target = 0; target < (int)eChimps.CHIMP_NUM_TYPES; target++)
                    foreach (byte attackerClass in new byte[] { 0, 1 })
                    foreach (byte targetClass in new byte[] { 0, 1 })
                    foreach (byte attackerPlayer in new byte[] { 0, 1 })
                    foreach (byte targetPlayer in new byte[] { 0, 1 })
                    foreach (byte filter in new byte[] { 0, 1 })
                    {
                        SetUnit(manager, 1, (eChimps)attacker, attackerClass, attackerPlayer, classByteOffset);
                        SetUnit(manager, 2, (eChimps)target, targetClass, targetPlayer, classByteOffset);
                        int original = vanilla(manager, 1, 2, filter);
                        bool wildlife = attacker == (int)eChimps.CHIMP_TYPE_LION ||
                            attacker == (int)eChimps.CHIMP_TYPE_HYENA ||
                            attacker == (int)eChimps.CHIMP_TYPE_CROCODILE;
                        int expected = active == 0 ? original :
                            wildlife && targetPlayer != 0 ? 1 : body(manager, 1, 2, filter);
                        int actual = corrected(manager, 1, 2, filter);
                        if (actual != expected)
                            throw new InvalidOperationException($"Native exclusion mismatch: mode={nativeMode}, peace={active}, " +
                                $"attacker={attacker}/{attackerClass}/{attackerPlayer}, target={target}/{targetClass}/{targetPlayer}, " +
                                $"sameTeam={sameTeam}, filter={filter}, expected={expected}, actual={actual}.");
                        if (active == 0) inactiveCases++; else activeCases++;
                        if (active != 0 && nativeMode == 1 && !(wildlife && targetPlayer != 0))
                            Check(actual == original, "unrelated mode-1 active-peace behavior remains Vanilla");
                    }
                }
                Check(inactiveCases > 0 && activeCases == inactiveCases,
                    "exhaustive native cases cover both actual peace states equally");
                foreach (eChimps animal in new[] { eChimps.CHIMP_TYPE_LION, eChimps.CHIMP_TYPE_HYENA,
                    eChimps.CHIMP_TYPE_CROCODILE })
                foreach (int nativeMode in new[] { 0, 1, 99 })
                foreach (byte player in new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 255 })
                {
                    Marshal.WriteInt32(mode, nativeMode);
                    SetUnit(manager, 1, animal, 1, 0, classByteOffset);
                    SetUnit(manager, 2, eChimps.CHIMP_TYPE_SPEARMAN, 0, player, classByteOffset);
                    Marshal.WriteByte(peace, 1);
                    Check(corrected(manager, 1, 2, 0) == (player >= 1 && player <= 8 ? 1 : body(manager, 1, 2, 0)),
                        "active wildlife guard covers exactly player IDs 1..8");
                    Marshal.WriteByte(peace, 0);
                    Check(corrected(manager, 1, 2, 0) == vanilla(manager, 1, 2, 0),
                        "expiry immediately restores Vanilla on the same prepared stub and existing contact");
                }
                Console.WriteLine($"Native wildlife behavior: inactive={inactiveCases}, active={activeCases}; " +
                    "three predators, all unit pairs/classes/owners/teams/filter flags, modes 0/1/99.");
                TestWildlifeSpanRejections(image);
                // AssemblyPatch uses six bytes here, rather than the inline backend's 14-byte minimum.
                IntPtr modeCopy = Marshal.AllocHGlobal(32);
                try
                {
                    Marshal.Copy(image, VanillaPeaceTimeNativeContract.HostilityPeaceModeRva, modeCopy, 32);
                    using (var patch = new X64AssemblyPatch(unchecked((ulong)modeCopy.ToInt64()), 6))
                    {
                        var site = VanillaPeaceTimeNativeContract.PatchSites.Single(x =>
                            x.Rva == VanillaPeaceTimeNativeContract.HostilityPeaceModeRva);
                        patch.Generate((assembler, _) => VanillaPeaceTimeNativeContract.EmitPatch(assembler, site, arenaBase));
                        Check(patch.BodyByteCount == 6 && patch.OverwrittenByteCount == 6,
                            "installed assembly patch backend preserves the six-byte mode-read span");
                    }
                }
                finally { Marshal.FreeHGlobal(modeCopy); }
            }
            finally
            {
                guard?.Dispose(); // Prepared, never enabled or published; candidate rollback only.
                Marshal.FreeHGlobal(manager);
                VirtualFree(arena, UIntPtr.Zero, 0x8000);
            }
        }

        private static void SetUnit(IntPtr manager, int id, eChimps type, byte classFlag, byte player, int classByteOffset)
        {
            int slot = id * VanillaPeaceTimeNativeContract.NativeUnitStride;
            Marshal.WriteInt16(manager, slot + VanillaPeaceTimeNativeContract.NativeUnitTypeOffset, (short)type);
            Marshal.WriteByte(manager, slot + classByteOffset, classFlag);
            // Vanilla reads the owner as a word; the public field itself is the low byte.
            Marshal.WriteInt16(manager, slot + VanillaPeaceTimeNativeContract.NativeUnitPlayerOffset, player);
            Marshal.WriteInt16(manager, slot + 0x6D8, player); // war-dog alternate owner input.
        }

        private static void WriteExclusionCopy(byte[] image, IntPtr destination, ulong imageBase, bool patched, bool legacy)
        {
            int start = VanillaPeaceTimeNativeContract.HostilityFunctionRva;
            byte[] bytes = new byte[VanillaPeaceTimeNativeContract.HostilityFunctionLength];
            Buffer.BlockCopy(image, start, bytes, 0, bytes.Length);
            if (patched || legacy)
            {
                foreach (var site in VanillaPeaceTimeNativeContract.PatchSites.Where(site =>
                    site.Rva >= start && site.Rva < start + bytes.Length &&
                    (!legacy || site.Rva != VanillaPeaceTimeNativeContract.HostilityPeaceModeRva)))
                {
                    var assembler = new Assembler(64);
                    VanillaPeaceTimeNativeContract.EmitPatch(assembler, site, imageBase);
                    byte[] patch = Assemble(assembler, imageBase + (uint)site.Rva, site.Name);
                    Buffer.BlockCopy(patch, 0, bytes, site.Rva - start, patch.Length);
                }
            }
            if (legacy)
            {
                bytes[0x1869A3 - start] = bytes[0x1869A4 - start] = 0x90;
                var former = new VanillaPeaceTimePatchSite(0x1869A7, "0F 85 E1 FE FF FF",
                    VanillaPeaceTimePatchKind.Jump, 0x18688E, "historical regression fixture");
                var assembler = new Assembler(64);
                VanillaPeaceTimeNativeContract.EmitPatch(assembler, former, imageBase);
                byte[] patch = Assemble(assembler, imageBase + (uint)former.Rva, former.Name);
                Buffer.BlockCopy(patch, 0, bytes, former.Rva - start, patch.Length);
            }
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes));
            decoder.IP = imageBase + (uint)start;
            ulong end = decoder.IP + (uint)bytes.Length;
            ulong destinationAddress = unchecked((ulong)destination.ToInt64());
            while (decoder.IP < end)
            {
                Instruction instruction = decoder.Decode();
                Check(!instruction.IsInvalid && instruction.NextIP <= end && instruction.FlowControl != FlowControl.Call,
                    "complete isolated exclusion predicate has no calls or undecoded instructions");
                if (instruction.FlowControl == FlowControl.ConditionalBranch || instruction.FlowControl == FlowControl.UnconditionalBranch)
                    Check(instruction.NearBranchTarget >= imageBase + (uint)start && instruction.NearBranchTarget < end,
                        "all exclusion branches stay inside the complete copied predicate");
                if (instruction.IsIPRelativeMemoryOperand)
                {
                    ConstantOffsets offsets = decoder.GetConstantOffsets(in instruction);
                    Check(offsets.DisplacementSize == 4, "native RIP reference has a four-byte displacement");
                    int offset = (int)(instruction.IP - imageBase - (uint)start);
                    long displacement = unchecked((long)instruction.IPRelativeMemoryAddress) -
                        unchecked((long)(destinationAddress + (uint)offset + (uint)instruction.Length));
                    Check(displacement >= int.MinValue && displacement <= int.MaxValue,
                        "relocated native RIP reference stays in range");
                    Buffer.BlockCopy(BitConverter.GetBytes((int)displacement), 0, bytes,
                        offset + offsets.DisplacementOffset, 4);
                }
            }
            Marshal.Copy(bytes, 0, destination, bytes.Length);
        }

        private static void TestWildlifeSpanRejections(byte[] image)
        {
            int start = VanillaPeaceTimeNativeContract.HostilityFunctionRva;
            byte original = image[start];
            try
            {
                image[start] = 0x90;
                Expect<InvalidOperationException>(() => VanillaPeaceTimeNativeContract.Validate(image, (ulong)Base, true),
                    "wildlife entry byte changes reject the complete candidate");
            }
            finally { image[start] = original; }
            int branch = start + 0x25; // Existing six-byte JBE, outside exact-byte validation spans.
            byte[] saved = image.Skip(branch + 2).Take(4).ToArray();
            try
            {
                Buffer.BlockCopy(BitConverter.GetBytes(start + 5 - (branch + 6)), 0, image, branch + 2, 4);
                bool rejected = false;
                try { VanillaPeaceTimeNativeContract.Validate(image, (ulong)Base, true); }
                catch (InvalidOperationException ex) { rejected = ex.Message.Contains("targets the interior"); }
                Check(rejected, "incoming JBE into the hook interior is rejected specifically by control-flow validation");
            }
            finally { Buffer.BlockCopy(saved, 0, image, branch + 2, saved.Length); }
            int shared = VanillaPeaceTimeNativeContract.HostilitySharedTestRva + 2;
            original = image[shared];
            try
            {
                image[shared] = 0xE9;
                Expect<InvalidOperationException>(() => VanillaPeaceTimeNativeContract.Validate(image, (ulong)Base, true),
                    "shared conditional-result changes reject the complete candidate");
            }
            finally { image[shared] = original; }
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
