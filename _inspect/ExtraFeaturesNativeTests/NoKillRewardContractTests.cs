using Iced.Intel;
using RedBird.Abstractions.Hooks;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Context;
using System;
using System.Runtime.InteropServices;
using System.Reflection;

namespace ExtraFeatures
{
    internal static class NoKillRewardContractTests
    {
        internal static void Run(byte[] image)
        {
            for (int human = 0; human <= 1; human++)
                for (int ai = 0; ai <= 1; ai++)
                {
                    int mask = NoKillRewardPolicy.ComposeMask(human != 0, ai != 0);
                    if (NoKillRewardPolicy.Suppresses(mask, false) != (human != 0) ||
                        NoKillRewardPolicy.Suppresses(mask, true) != (ai != 0))
                        throw new InvalidOperationException("No Kill Reward role mask changed.");
                }
            CheckBytes(image, 0x15C4EA, new byte[] {
                0x4C, 0x69, 0xD5, 0x0F, 0x16, 0x00, 0x00,
                0x4C, 0x69, 0xED, 0x3C, 0x58, 0x00, 0x00
            });
            CheckBytes(image, 0x15CA65, new byte[] {
                0x8B, 0xD6, 0x44, 0x2B, 0xE0,
                0x44, 0x89, 0x64, 0x24, 0x20,
                0xE8, 0x5C, 0xFE, 0xEB, 0xFF
            });
            CheckLiveSpan(image, 0x15C4EA, 14);
            CheckLiveSpan(image, 0x15CA65, 15);
            CheckRewardStub();
            CheckGoldMessageStub();
        }

        private static void CheckLiveSpan(byte[] image, int rva, int length)
        {
            var expected = new byte[length];
            Buffer.BlockCopy(image, rva, expected, 0, length);
            IntPtr live = Marshal.AllocHGlobal(length);
            try
            {
                Marshal.Copy(expected, 0, live, length);
                ulong address = unchecked((ulong)live.ToInt64());
                NoKillRewardStubContract.VerifyLiveSpan(expected, address, rva);
                foreach (int offset in new[] { 0, length / 2, length - 1 })
                {
                    Marshal.WriteByte(live, offset, (byte)(expected[offset] ^ 0x01));
                    try
                    {
                        NoKillRewardStubContract.VerifyLiveSpan(expected, address, rva);
                        throw new InvalidOperationException("Changed live bytes were accepted at RVA 0x" + (rva + offset).ToString("X") + ".");
                    }
                    catch (InvalidOperationException ex)
                    {
                        if (!ex.Message.Contains("changed at RVA 0x" + (rva + offset).ToString("X")))
                            throw;
                    }
                    Marshal.WriteByte(live, offset, expected[offset]);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(live);
            }
            try
            {
                NoKillRewardStubContract.VerifyLiveSpan(expected, new byte[length - 1], rva);
                throw new InvalidOperationException("Truncated live hook bytes were accepted.");
            }
            catch (InvalidOperationException ex)
            {
                if (!ex.Message.Contains("different length"))
                    throw;
            }
        }

        private static void CheckBytes(byte[] image, int rva, byte[] expected)
        {
            if (rva + expected.Length > image.Length)
                throw new InvalidOperationException("No Kill Reward RVA exceeds native image.");
            for (int i = 0; i < expected.Length; i++)
                if (image[rva + i] != expected[i])
                    throw new InvalidOperationException("No Kill Reward native bytes differ at RVA 0x" + (rva + i).ToString("X"));
        }

        private static void CheckRewardStub()
        {
            byte[] reward = {
                0x4C, 0x69, 0xD5, 0x0F, 0x16, 0x00, 0x00,
                0x4C, 0x69, 0xED, 0x3C, 0x58, 0x00, 0x00
            };
            WithCopy(reward, (probe, address) =>
            {
                ulong cleanup = address + 0x496;
                var options = new ContextHookOptions
                {
                    Registers = X64SmartCPUContextRegs.All,
                    HookSize = reward.Length,
                    Placement = OverwrittenInstructionPlacement.AfterCallback,
                    InstructionSelector = original =>
                    NoKillRewardStubContract.SelectRewardInstructions(original, cleanup)
                };
                // Desktop CLR cannot marshal RedBird's NativePointer callback.
                // Use the installed context assembler; Mono uses CreateGenerator.
                Type generator = typeof(ContextStub).Assembly.GetType(
                    "RedBird.X64.Assembly.ContextAssemblyGenerator", throwOnError: true);
                MethodInfo generate = generator.GetMethod("Generate",
                    BindingFlags.Public | BindingFlags.Static);
                if (generate == null)
                    throw new InvalidOperationException("Installed RedBird context generator is unavailable.");
                probe.Generate((assembler, original, returnAddress) =>
                {
                    generate.Invoke(null, new object[] {
                        assembler, new IntPtr(0x12345678), options.Registers
                    });
                    foreach (Instruction instruction in options.InstructionSelector(original.ToArray()))
                        assembler.AddInstruction(instruction);
                });
                if (probe.DisplacedByteCount != 14)
                    throw new InvalidOperationException("Reward hook did not displace 14 bytes.");
                NoKillRewardStubContract.VerifyRewardEmitted(probe, cleanup, address + 14);
            });
        }

        private static void CheckGoldMessageStub()
        {
            byte[] message = {
                0x8B, 0xD6, 0x44, 0x2B, 0xE0,
                0x44, 0x89, 0x64, 0x24, 0x20,
                0xE8, 0x5C, 0xFE, 0xEB, 0xFF
            };
            WithCopy(message, (probe, address) =>
            {
                if (probe.DisplacedByteCount != 15)
                    throw new InvalidOperationException("Gold-message hook did not displace 15 bytes.");
                probe.Generate(NoKillRewardStubContract.GenerateGoldMessageGuard);
                NoKillRewardStubContract.VerifyGoldMessageEmitted(probe, address + 15);
            });
        }

        private static void WithCopy(byte[] original, Action<X64InlineHook, ulong> check)
        {
            byte[] source = new byte[64];
            for (int i = 0; i < source.Length; i++)
                source[i] = 0x90;
            Buffer.BlockCopy(original, 0, source, 0, original.Length);
            IntPtr copy = Marshal.AllocHGlobal(source.Length);
            try
            {
                Marshal.Copy(source, 0, copy, source.Length);
                ulong address = unchecked((ulong)copy.ToInt64());
                using (var probe = new X64InlineHook(address, original.Length))
                    check(probe, address);
            }
            finally
            {
                Marshal.FreeHGlobal(copy);
            }
        }
    }
}
