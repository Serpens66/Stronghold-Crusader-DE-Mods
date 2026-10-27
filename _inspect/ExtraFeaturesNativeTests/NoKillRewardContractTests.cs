using Iced.Intel;
using RedBird.Abstractions.Hooks;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Context;
using System;
using System.Runtime.InteropServices;
using System.Reflection;
using SHCDESE.Interop;
using static Iced.Intel.AssemblerRegisters;

namespace ExtraFeatures
{
    internal static unsafe class NoKillRewardContractTests
    {
        internal static void Run(byte[] image)
        {
            NoKillRewardHook.VerifyResourceContract(image);
            CheckTransfer();
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
            ExecuteGoldMessageStub();
            ExecuteRewardBranches();
        }

        private static void CheckTransfer()
        {
            foreach (int goldPercent in new[] { -1, 0, 1, 25, 100 })
            foreach (int goodsPercent in new[] { -1, 0, 1, 25, 100 })
            {
                GamePlayerResources winner = default, defeated = default;
                winner.r_TotalGoodsGold = 100;
                defeated.r_TotalGoodsGold = 12345;
                int statistic = 200;
                int* totals = (int*)&defeated.r_TotalGoodsNull;
                int* incoming = (int*)&winner.r_incomingNull;
                foreach (eGoods good in NoKillRewardHook.RewardGoods) { totals[(int)good] = 103; incoming[(int)good] = 7; }
                int gold = NoKillRewardHook.ApplyReward(&winner, &defeated, &statistic, goldPercent, goodsPercent, -1);
                if (gold != NoKillRewardPolicy.Gold(12345, goldPercent, -1) || winner.r_TotalGoodsGold != 100 + gold || statistic != 200)
                    throw new Exception("Production reward writes gold once and leaves statistic to Vanilla cleanup.");
                foreach (eGoods good in NoKillRewardHook.RewardGoods)
                    if (incoming[(int)good] != 7 + NoKillRewardPolicy.Goods(103, goodsPercent) || totals[(int)good] != 103)
                        throw new Exception("Production goods transfer changed its source or lost rounding.");
                if (*(int*)((byte*)&winner + NoKillRewardHook.GoodsNotificationOffset) != (goodsPercent == 0 ? 0 : 2))
                    throw new Exception("Goods notification must follow actual goods credit.");
            }
            GamePlayerResources fullWinner = default, richDefeated = default;
            fullWinner.r_TotalGoodsGold = int.MaxValue - 9; richDefeated.r_TotalGoodsGold = int.MaxValue;
            fullWinner.r_incomingWoodPlanks = int.MaxValue - 3; richDefeated.r_TotalGoodsWoodPlanks = int.MaxValue;
            int fullStatistic = int.MaxValue - 5;
            int limited = NoKillRewardHook.ApplyReward(&fullWinner, &richDefeated, &fullStatistic, 100, 100, int.MaxValue);
            if (limited != 5 || fullWinner.r_TotalGoodsGold != int.MaxValue - 4 || fullWinner.r_incomingWoodPlanks != int.MaxValue)
                throw new Exception("Gold, statistics and pending goods headroom must not overflow.");
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void GoldMessageFixture();
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr size, uint allocationType, uint protect);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualFree(IntPtr address, UIntPtr size, uint freeType);

        private static void ExecuteGoldMessageStub()
        {
            // Execute the production assembler with a native leaf recorder. The recorder
            // deliberately clobbers volatile registers like the real Win64 callee may.
            IntPtr page = VirtualAlloc(IntPtr.Zero, (UIntPtr)4096, 0x3000, 0x40);
            if (page == IntPtr.Zero) throw new System.ComponentModel.Win32Exception();
            IntPtr counters = Marshal.AllocHGlobal(24);
            try
            {
                ulong address = unchecked((ulong)page.ToInt64());
                var callback = new Assembler(64);
                callback.mov(r11, unchecked((ulong)counters.ToInt64()));
                var invalid = callback.CreateLabel(); var record = callback.CreateLabel();
                callback.cmp(edx, 1); callback.jne(invalid);
                callback.cmp(rcx, 0x12345678); callback.jne(invalid);
                callback.cmp(r8d, 7); callback.jne(invalid);
                callback.test(r9d, r9d); callback.je(record);
                callback.Label(ref invalid); callback.inc(__dword_ptr[r11 + 12]);
                callback.Label(ref record);
                callback.movsx(eax, __word_ptr[rsp + 0x28]);
                callback.movsx(edx, __word_ptr[rsp + 0x30]);
                callback.imul(edx, edx, 30000); callback.add(eax, edx);
                callback.add(__qword_ptr[r11], rax); callback.inc(__dword_ptr[r11 + 8]);
                callback.xor(eax, eax); callback.xor(ecx, ecx); callback.xor(edx, edx); callback.xor(r10d, r10d); callback.ret();
                CopyAssembly(callback, page, address);
                foreach (int amount in new[] { 0, 1, 499, 500, 29999, 30000, 100000, 900000000, 900000001, int.MaxValue })
                {
                    for (int offset = 0; offset < 24; offset += 4) Marshal.WriteInt32(counters, offset, 0);
                    var wrapper = new Assembler(64);
                    wrapper.push(rbp); wrapper.push(rsi); wrapper.push(r12); wrapper.push(r13); wrapper.sub(rsp, 0x38);
                    wrapper.mov(ebp, 7); wrapper.mov(esi, 1); wrapper.mov(r12d, amount); wrapper.mov(r13, 0x12345678);
                    var done = new Assembler(64); done.add(rsp, 0x38); done.pop(r13); done.pop(r12); done.pop(rsi); done.pop(rbp); done.ret();
                    CopyAssembly(done, IntPtr.Add(page, 2048), address + 2048);
                    var decoder = Decoder.Create(64, new ByteArrayCodeReader(new byte[] {
                        0x8B, 0xD6, 0x44, 0x2B, 0xE0, 0x44, 0x89, 0x64, 0x24, 0x20, 0xE8, 0, 0, 0, 0 }));
                    var originals = new[] { decoder.Decode(), decoder.Decode(), decoder.Decode(), decoder.Decode() };
                    originals[3].NearBranch64 = address;
                    NoKillRewardStubContract.GenerateGoldMessageGuard(wrapper, originals, address + 2048);
                    CopyAssembly(wrapper, IntPtr.Add(page, 512), address + 512);
                    Marshal.GetDelegateForFunctionPointer<GoldMessageFixture>(IntPtr.Add(page, 512))();
                    int expectedCount = amount == 0 ? 0 : (int)(((long)amount + NoKillRewardPolicy.MaximumMessageChunk - 1) / NoKillRewardPolicy.MaximumMessageChunk);
                    if (Marshal.ReadInt64(counters) != amount || Marshal.ReadInt32(counters, 8) != expectedCount || Marshal.ReadInt32(counters, 12) != 0)
                        throw new Exception("Executed gold message lost its amount, winner or chunk count: " + amount);
                }
            }
            finally { Marshal.FreeHGlobal(counters); VirtualFree(page, UIntPtr.Zero, 0x8000); }
        }

        private static void CopyAssembly(Assembler assembler, IntPtr destination, ulong address)
        {
            using (var stream = new System.IO.MemoryStream())
            {
                assembler.Assemble(new StreamCodeWriter(stream), address);
                byte[] bytes = stream.ToArray(); Marshal.Copy(bytes, 0, destination, bytes.Length);
            }
        }

        private static void ExecuteRewardBranches()
        {
            IntPtr page = VirtualAlloc(IntPtr.Zero, (UIntPtr)4096, 0x3000, 0x40);
            if (page == IntPtr.Zero) throw new System.ComponentModel.Win32Exception();
            IntPtr result = Marshal.AllocHGlobal(32);
            try
            {
                ulong address = unchecked((ulong)page.ToInt64());
                for (int decision = 0; decision <= 1; decision++)
                {
                    for (int branch = 0; branch <= 1; branch++)
                    {
                        var continuation = new Assembler(64);
                        continuation.mov(rax, unchecked((ulong)result.ToInt64()));
                        continuation.mov(__dword_ptr[rax], branch);
                        continuation.mov(__qword_ptr[rax + 8], r10);
                        continuation.mov(__qword_ptr[rax + 16], r13);
                        continuation.mov(__qword_ptr[rax + 24], r12);
                        continuation.pop(r13); continuation.pop(r12); continuation.pop(rbp); continuation.ret();
                        CopyAssembly(continuation, IntPtr.Add(page, 1024 + branch * 256), address + (uint)(1024 + branch * 256));
                    }
                    var decoder = Decoder.Create(64, new ByteArrayCodeReader(new byte[] {
                        0x4C, 0x69, 0xD5, 0x0F, 0x16, 0, 0, 0x4C, 0x69, 0xED, 0x3C, 0x58, 0, 0 }));
                    var original = new[] { decoder.Decode(), decoder.Decode() };
                    var wrapper = new Assembler(64);
                    wrapper.push(rbp); wrapper.push(r12); wrapper.push(r13);
                    wrapper.mov(ebp, 7); wrapper.mov(edx, decision); wrapper.mov(r10d, 42); wrapper.mov(r13d, 43); wrapper.mov(r12d, 123);
                    foreach (Instruction instruction in NoKillRewardStubContract.SelectRewardInstructions(original, address + 1024)) wrapper.AddInstruction(instruction);
                    wrapper.jmp(address + 1280);
                    CopyAssembly(wrapper, page, address);
                    Marshal.GetDelegateForFunctionPointer<GoldMessageFixture>(page)();
                    if (Marshal.ReadInt32(result) != decision || Marshal.ReadInt64(result, 8) != (decision == 0 ? 42 : 7 * 0x160F) ||
                        Marshal.ReadInt64(result, 16) != (decision == 0 ? 43 : 7 * 0x583C) || Marshal.ReadInt64(result, 24) != 123)
                        throw new Exception("Executed reward decision lost its cleanup/Vanilla indices or credited gold.");
                }
            }
            finally { Marshal.FreeHGlobal(result); VirtualFree(page, UIntPtr.Zero, 0x8000); }
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
                    bool rejected = false;
                    try { NoKillRewardStubContract.VerifyLiveSpan(expected, address, rva); }
                    catch (InvalidOperationException) { rejected = true; }
                    if (!rejected)
                        throw new InvalidOperationException("Changed live hook bytes were accepted.");
                    Marshal.WriteByte(live, offset, expected[offset]);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(live);
            }
            bool truncatedRejected = false;
            try { NoKillRewardStubContract.VerifyLiveSpan(expected, new byte[length - 1], rva); }
            catch (InvalidOperationException) { truncatedRejected = true; }
            if (!truncatedRejected)
                throw new InvalidOperationException("Truncated live hook bytes were accepted.");
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
