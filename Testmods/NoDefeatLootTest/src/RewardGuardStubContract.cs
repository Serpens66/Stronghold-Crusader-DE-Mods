using Iced.Intel;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Context;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace NoDefeatLootTest
{
    internal static class RewardGuardStubContract
    {
        private const int ExpectedDisplacedByteCount = 16;
        private static readonly byte[] TestDecisionBytes = { 0x85, 0xD2 }; // TEST EDX,EDX

        internal static IEnumerable<Instruction> SelectInstructions(IReadOnlyList<Instruction> original)
        {
            if (original.Count != 2 ||
                original[0].Mnemonic != Mnemonic.Je || original[0].Length != 6 ||
                original[1].Mnemonic != Mnemonic.Cmp || original[1].Length != 10)
                throw new InvalidOperationException("The displaced Vanilla reward guard changed.");

            Decoder decoder = Decoder.Create(64, new ByteArrayCodeReader(TestDecisionBytes));
            Instruction test = decoder.Decode();
            if (test.IsInvalid || test.Length != 2 ||
                test.Mnemonic != Mnemonic.Test ||
                test.Op0Register != Register.EDX || test.Op1Register != Register.EDX)
                throw new InvalidOperationException("Iced did not decode the decision test as expected.");
            return new[] { test, original[0], original[1] };
        }

        internal static void Verify(X64InlineHook probe, ContextHookDelegate callback,
            ContextHookOptions options, ulong expectedSkipTarget)
        {
            if (probe.DisplacedByteCount != ExpectedDisplacedByteCount)
                throw new InvalidOperationException("The probe displaced a different guard span.");

            probe.Generate(ContextStub.CreateGenerator(probe, callback, options));
            VerifyEmitted(probe, expectedSkipTarget);
        }

        internal static void VerifyEmitted(X64InlineHook probe, ulong expectedSkipTarget)
        {
            if (probe.StubAddress == IntPtr.Zero)
                throw new InvalidOperationException("RedBird did not generate the guard stub.");

            // NativeMemoryManager rounds stub allocations to a Windows page.
            // Decode within the first page rather than guessing an instruction count.
            const int scanBytes = 1024;
            byte[] bytes = new byte[scanBytes];
            Marshal.Copy(probe.StubAddress, bytes, 0, scanBytes);
            ulong stubAddress = unchecked((ulong)probe.StubAddress.ToInt64());
            Decoder decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes), stubAddress);
            bool found = false;
            for (int i = 0; i < scanBytes / 2 && decoder.IP < stubAddress + scanBytes - 32; i++)
            {
                Instruction test = decoder.Decode();
                if (test.IsInvalid)
                    break;
                if (test.Mnemonic != Mnemonic.Test || test.Op0Register != Register.EDX ||
                    test.Op1Register != Register.EDX)
                    continue;

                Instruction branch = decoder.Decode();
                Instruction compare = decoder.Decode();
                Instruction continuation = decoder.Decode();
                if (branch.Mnemonic == Mnemonic.Je &&
                    branch.NearBranchTarget == expectedSkipTarget &&
                    compare.Mnemonic == Mnemonic.Cmp && compare.Length == 10 &&
                    continuation.Mnemonic == Mnemonic.Jmp)
                {
                    found = true;
                    break;
                }
                throw new InvalidOperationException("The generated guard stub has an unexpected branch sequence.");
            }
            if (!found)
                throw new InvalidOperationException("The generated guard stub lacks the decision test and Vanilla branches.");
        }
    }
}
