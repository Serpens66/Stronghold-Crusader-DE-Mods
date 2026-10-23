using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Iced.Intel;
using RedBird.X64.Extensions;
using RedBird.X64.Hooks;
using static Iced.Intel.AssemblerRegisters;

namespace ExtraFeatures
{
    // Executes the production fear-factor stubs next to the original Vanilla bytes.
    // A fake image keeps the audited RVA distances, so the copied RIP-relative LEA and
    // RDI-based loads address a fake fear table. Every GPR (except RSP) is compared.
    internal static class FearFactorStubTests
    {
        private const ulong PreferredImageBase = 0x180000000UL;
        private const int FakeImageSize = 0x3800000;
        private const int StubRva = 0x3000000;
        private const int HarnessRva = 0x3100000;
        private const int RegisterCount = 16; // RAX..R15 in encoding order; RSP slot unused.

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr size, uint type, uint protect);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualFree(IntPtr address, UIntPtr size, uint type);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void HarnessDelegate();

        internal static void Run(byte[] image, Action<bool, string> check)
        {
            FearFactorNativeDefinition.Validate(image, PreferredImageBase);
            check(true, "fear-factor production validation accepts the canonical image");
            CheckRedBirdSpan(image, FearFactorNativeDefinition.DamageFunctionRva,
                FearFactorNativeDefinition.DamageHookLength, check, "damage");
            CheckRedBirdSpan(image, FearFactorNativeDefinition.UiHookRva,
                FearFactorNativeDefinition.UiHookLength, check, "overlay");
            CheckNoDirectBranchIntoDamageSpan(image, check);

            IntPtr block = VirtualAlloc(IntPtr.Zero, (UIntPtr)FakeImageSize, 0x3000, 0x40);
            check(block != IntPtr.Zero, "fake image allocation");
            try
            {
                var fake = new FakeImage(block);
                fake.Copy(image, FearFactorNativeDefinition.DamageFunctionRva,
                    FearFactorNativeDefinition.DamageFunctionLength + 1);
                fake.Copy(image, FearFactorNativeDefinition.UiHookRva, FearFactorNativeDefinition.UiHookLength);
                // The Vanilla reference returns to the harness where TEST EAX,EAX follows.
                fake.WriteByte(FearFactorNativeDefinition.UiHookRva + FearFactorNativeDefinition.UiHookLength, 0xC3);
                int[] fear = { 0, -5, 0, 7, 20, -3, 11, 1, 2 };
                for (int player = 0; player < fear.Length; player++)
                    fake.WriteInt32(FearFactorNativeDefinition.FearTableRva +
                        player * (int)FearFactorNativeDefinition.PlayerRecordStride, fear[player]);
                IntPtr flag = fake.Address(StubRva - 0x100);

                var harness = new Harness(fake.Address(HarnessRva));
                ulong damageReturn = fake.Va(FearFactorNativeDefinition.DamageFunctionRva + FearFactorNativeDefinition.DamageHookLength);
                ulong damageStub = fake.Assemble(StubRva, damageReturn, asm => FearFactorNativeStubs.GenerateDamage(
                    asm,
                    Decode(fake, FearFactorNativeDefinition.DamageFunctionRva, FearFactorNativeDefinition.DamageHookLength),
                    damageReturn,
                    fake.Va(StubRva - 0x100),
                    fake.Va(FearFactorNativeDefinition.FearTableRva)));
                ulong overlayReturn = fake.Va(FearFactorNativeDefinition.UiHookRva + FearFactorNativeDefinition.UiHookLength);
                ulong overlayStub = fake.Assemble(StubRva + 0x1000, overlayReturn, asm => FearFactorNativeStubs.GenerateOverlay(
                    asm,
                    Decode(fake, FearFactorNativeDefinition.UiHookRva, FearFactorNativeDefinition.UiHookLength),
                    overlayReturn,
                    fake.Va(StubRva - 0x100)));

                foreach (int player in new[] { 1, 2, 3, 4, 5 })
                {
                    foreach (int baseDamage in new[] { 0, 1, 37, 100, 1000 })
                    {
                        ulong[] input = Sentinels(0x100 + player * 16 + baseDamage);
                        input[2] = 0xAAAA000000000000UL | unchecked((uint)baseDamage); // RDX: upper bits ignored
                        input[8] = 0xBBBB000000000000UL | unchecked((uint)player);     // R8D
                        ulong[] vanilla = harness.Run(fake.Va(FearFactorNativeDefinition.DamageFunctionRva), input);
                        check((int)vanilla[0] == FearFactorNeutralizationPolicy.CalculateVanillaDamage(baseDamage, fear[player]),
                            $"Vanilla damage reference player={player} damage={baseDamage}");

                        Marshal.WriteByte(flag, 0);
                        ulong[] disabled = harness.Run(damageStub, input);
                        check(disabled.SequenceEqual(vanilla), $"disabled damage stub equals Vanilla in every GPR p={player} d={baseDamage}");

                        Marshal.WriteByte(flag, 1);
                        ulong[] enabled = harness.Run(damageStub, input);
                        check(enabled[0] == unchecked((uint)FearFactorNeutralizationPolicy.CalculateNeutralDamage(baseDamage)),
                            $"enabled damage stub returns zero-extended neutral damage p={player} d={baseDamage}");
                        foreach (int preserved in new[] { 3, 5, 6, 7, 12, 13, 14, 15 })
                            check(enabled[preserved] == input[preserved], $"enabled damage stub preserves non-volatile GPR {preserved}");
                    }

                    ulong[] overlayInput = Sentinels(0x900 + player);
                    overlayInput[7] = fake.Va(0);                                   // RDI: image base
                    overlayInput[10] = unchecked((ulong)(long)player);               // R10: owner
                    ulong[] overlayVanilla = harness.Run(fake.Va(FearFactorNativeDefinition.UiHookRva), overlayInput);
                    check((int)overlayVanilla[0] == fear[player] && overlayVanilla[0] >> 32 == 0,
                        $"Vanilla overlay load reads the owner's fear p={player}");

                    Marshal.WriteByte(flag, 0);
                    ulong[] overlayDisabled = harness.Run(overlayStub, overlayInput);
                    check(overlayDisabled.SequenceEqual(overlayVanilla), $"disabled overlay stub equals Vanilla in every GPR p={player}");

                    Marshal.WriteByte(flag, 1);
                    ulong[] overlayEnabled = harness.Run(overlayStub, overlayInput);
                    check(overlayEnabled[0] == 0 && overlayEnabled.Skip(1).SequenceEqual(overlayVanilla.Skip(1)),
                        $"enabled overlay stub changes only RAX to zero p={player}");
                }
            }
            finally
            {
                VirtualFree(block, UIntPtr.Zero, 0x8000);
            }
        }

        private static void CheckRedBirdSpan(byte[] image, int rva, int length, Action<bool, string> check, string label)
        {
            IntPtr probeMemory = Marshal.AllocHGlobal(64);
            try
            {
                Marshal.Copy(image, rva, probeMemory, 48);
                using (var probe = new X64InlineHook(unchecked((ulong)probeMemory.ToInt64()), length, null, "FearFactorSpanProbe"))
                    check(probe.DisplacedByteCount == length && !probe.IsInstalled,
                        $"installed RedBird displaces exactly the audited {label} span without publishing");
            }
            finally
            {
                Marshal.FreeHGlobal(probeMemory);
            }
        }

        // Conservative raw scan of every executable section: no rel8/rel32 JMP, Jcc or CALL
        // may land inside the damage hook span (its entry itself is allowed).
        private static void CheckNoDirectBranchIntoDamageSpan(byte[] image, Action<bool, string> check)
        {
            int start = FearFactorNativeDefinition.DamageFunctionRva;
            int end = start + FearFactorNativeDefinition.DamageHookLength;
            var hits = new List<string>();
            int pe = BitConverter.ToInt32(image, 0x3C);
            int sections = BitConverter.ToUInt16(image, pe + 6);
            int table = pe + 24 + BitConverter.ToUInt16(image, pe + 20);
            int scanned = 0;
            for (int s = 0; s < sections; s++)
            {
                int header = table + s * 40;
                if ((BitConverter.ToUInt32(image, header + 36) & 0x20000000U) == 0)
                    continue;
                int sectionStart = BitConverter.ToInt32(image, header + 12);
                int sectionEnd = Math.Min(image.Length - 6, sectionStart + BitConverter.ToInt32(image, header + 8));
                scanned++;
                ScanSection(image, sectionStart, sectionEnd, start, end, hits);
            }
            check(scanned > 0 && hits.Count == 0,
                "no direct branch targets the damage hook interior: " + string.Join(", ", hits));
        }

        private static void ScanSection(byte[] image, int sectionStart, int sectionEnd, int start, int end, List<string> hits)
        {
            for (int i = sectionStart; i < sectionEnd; i++)
            {
                byte op = image[i];
                long target = -1;
                if (op == 0xE8 || op == 0xE9)
                    target = i + 5L + BitConverter.ToInt32(image, i + 1);
                else if (op == 0xEB || (op >= 0x70 && op <= 0x7F))
                    target = i + 2L + (sbyte)image[i + 1];
                else if (op == 0x0F && image[i + 1] >= 0x80 && image[i + 1] <= 0x8F)
                    target = i + 6L + BitConverter.ToInt32(image, i + 2);
                if (target > start && target < end && (i < start || i >= end))
                    hits.Add($"0x{i:X}->0x{target:X}");
            }
        }

        private static Instruction[] Decode(FakeImage fake, int rva, int length)
        {
            byte[] bytes = new byte[length + 15];
            Marshal.Copy(fake.Address(rva), bytes, 0, bytes.Length);
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes));
            decoder.IP = fake.Va(rva);
            var result = new List<Instruction>();
            int decoded = 0;
            while (decoded < length)
            {
                Instruction instruction = decoder.Decode();
                if (instruction.IsInvalid) throw new InvalidOperationException("Invalid fear-factor instruction.");
                result.Add(instruction);
                decoded += instruction.Length;
            }
            if (decoded != length) throw new InvalidOperationException("Fear-factor span is not an instruction boundary.");
            return result.ToArray();
        }

        private static ulong[] Sentinels(int seed)
        {
            var values = new ulong[RegisterCount];
            for (int i = 0; i < values.Length; i++)
                values[i] = 0x5E00000000000000UL | ((ulong)seed << 24) | ((ulong)i << 8) | 0x5A;
            return values;
        }

        private sealed class FakeImage
        {
            private readonly IntPtr baseAddress;

            internal FakeImage(IntPtr baseAddress) { this.baseAddress = baseAddress; }

            internal IntPtr Address(int rva) => new IntPtr(baseAddress.ToInt64() + rva);
            internal ulong Va(int rva) => unchecked((ulong)baseAddress.ToInt64()) + (ulong)rva;
            internal void Copy(byte[] image, int rva, int length) => Marshal.Copy(image, rva, Address(rva), length);
            internal void WriteByte(int rva, byte value) => Marshal.WriteByte(Address(rva), value);
            internal void WriteInt32(int rva, int value) => Marshal.WriteInt32(Address(rva), value);

            // Mirrors X64InlineHook.Generate: production generator, then RedBird's return jump.
            internal ulong Assemble(int rva, ulong returnAddress, Action<Assembler> generator)
            {
                var assembler = new Assembler(64);
                generator(assembler);
                assembler.AddUnrestrictedJmp(returnAddress);
                byte[] code = AssembleAt(assembler, Va(rva), out _);
                Marshal.Copy(code, 0, Address(rva), code.Length);
                return Va(rva);
            }
        }

        private static byte[] AssembleAt(Assembler assembler, ulong ip, out int length)
        {
            using (var stream = new MemoryStream())
            {
                if (!assembler.TryAssemble(new StreamCodeWriter(stream), ip, out string error, out _, BlockEncoderOptions.None))
                    throw new InvalidOperationException("Fear-factor stub assembly failed: " + error);
                byte[] bytes = stream.ToArray();
                length = bytes.Length;
                return bytes;
            }
        }

        // Loads all GPRs from an input buffer, calls the code, stores all GPRs afterwards.
        // The callee sees RSP = 8 mod 16 like a regular function entry.
        private sealed class Harness
        {
            private const int InputOffset = 0x800;
            private const int TargetOffset = InputOffset + RegisterCount * 8;
            private const int OutputOffset = 0x900;
            private static readonly AssemblerRegister64[] Saved = { rbx, rbp, rsi, rdi, r12, r13, r14, r15 };
            private static readonly AssemblerRegister64[] Loaded =
            {
                rax, rcx, rdx, rbx, rsp, rbp, rsi, rdi, r8, r9, r10, r11, r12, r13, r14, r15
            };
            private readonly IntPtr input;
            private readonly IntPtr output;
            private readonly HarnessDelegate invoke;

            internal Harness(IntPtr address)
            {
                input = new IntPtr(address.ToInt64() + InputOffset);
                output = new IntPtr(address.ToInt64() + OutputOffset);
                ulong ip = unchecked((ulong)address.ToInt64());
                var asm = new Assembler(64);
                foreach (AssemblerRegister64 saved in Saved)
                    asm.push(saved);
                asm.sub(rsp, 16);
                asm.mov(rax, ip + InputOffset);
                for (int i = 1; i < RegisterCount; i++)
                    if (i != 4) asm.mov(Loaded[i], __qword_ptr[rax + i * 8]);
                asm.push(__qword_ptr[rax + RegisterCount * 8]);
                asm.mov(rax, __qword_ptr[rax]);
                asm.call(__qword_ptr[rsp]);
                asm.add(rsp, 8);
                asm.push(rax);
                asm.mov(rax, ip + OutputOffset);
                for (int i = 1; i < RegisterCount; i++)
                    if (i != 4) asm.mov(__qword_ptr[rax + i * 8], Loaded[i]);
                asm.pop(rcx);
                asm.mov(__qword_ptr[rax], rcx);
                asm.add(rsp, 16);
                for (int i = Saved.Length - 1; i >= 0; i--)
                    asm.pop(Saved[i]);
                asm.ret();
                byte[] code = AssembleAt(asm, ip, out int length);
                if (length > InputOffset) throw new InvalidOperationException("Harness too large.");
                Marshal.Copy(code, 0, address, code.Length);
                invoke = Marshal.GetDelegateForFunctionPointer<HarnessDelegate>(address);
            }

            internal ulong[] Run(ulong code, ulong[] registers)
            {
                for (int i = 0; i < RegisterCount; i++)
                    Marshal.WriteInt64(input, i * 8, unchecked((long)registers[i]));
                Marshal.WriteInt64(input, RegisterCount * 8, unchecked((long)code));
                invoke();
                var result = new ulong[RegisterCount];
                for (int i = 0; i < RegisterCount; i++)
                    result[i] = i == 4 ? registers[i] : unchecked((ulong)Marshal.ReadInt64(output, i * 8));
                return result;
            }
        }
    }
}
