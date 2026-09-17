using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Iced.Intel;
using RedBird.X64.Extensions;
using RedBird.X64.Hooks;
using static Iced.Intel.AssemblerRegisters;

namespace OutpostTest
{
    internal static class Program
    {
        private const string Game = @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition";
        private static int count;
        private static int Main()
        {
            AppDomain.CurrentDomain.AssemblyResolve += (_, e) => {
                string file = new AssemblyName(e.Name).Name + ".dll";
                foreach (string dir in new[] { @"BepInEx\plugins\000shcdese", @"BepInEx\plugins\APIShared_Serp", @"BepInEx\core", @"Stronghold Crusader Definitive Edition_Data\Managed" })
                { string p = Path.Combine(Game, dir, file); if (File.Exists(p)) return Assembly.LoadFrom(p); }
                return null;
            };
            try { Run(); Console.WriteLine($"OutpostTest: {count} checks passed (static/native synthetic tests, not a game test)."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        private static void Check(bool value, string name)
        { if (!value) throw new Exception(name); count++; }
        private static void Run()
        {
            OutpostNative.ValidateLayouts(); count++;
            RallyTests.Run(Check);
            Check(OutpostSchedule.IsOutpost(2) && OutpostSchedule.IsOutpost(106) && OutpostSchedule.IsOutpost(107) && !OutpostSchedule.IsOutpost(8), "three variants");
            Check(OutpostSchedule.GroupWait(0,3,false)==2000, "initial group wait");
            Check(OutpostSchedule.GroupWait(33,3,false)==1967, "completed group acceleration");
            Check(OutpostSchedule.GroupWait(2000,3,false)==600, "ordinary minimum");
            Check(OutpostSchedule.GroupWait(2000,3,true)==400, "game mode minimum");
            Check(OutpostSchedule.GroupWait(2000,0,true)==100 && OutpostSchedule.GroupWait(2000,99,true)==100, "native mode exceptions");
            Check(OutpostSchedule.SpawnWait(0,1)==212, "default size integer interval");
            Check(OutpostSchedule.SpawnWait(4,1)==209, "spawn acceleration");
            Check(OutpostSchedule.SpawnWait(150,1)==85 && OutpostSchedule.SpawnWait(500,1)==85, "profile minimum");
            Check(OutpostSchedule.Target(0,0)==10 && OutpostSchedule.Target(0,9)==19, "small group bounds");
            Check(OutpostSchedule.Target(1,0)==20 && OutpostSchedule.Target(1,9)==38, "default group bounds");
            Check(!OutpostSchedule.Complete(5,20,0) && !OutpostSchedule.Complete(19,20,0), "partial group never handed off");
            Check(OutpostSchedule.Complete(20,20,0) && !OutpostSchedule.Complete(20,20,1), "target and delay gate");
            Check(OutpostSchedule.DelayBlocks(19,20,1) && !OutpostSchedule.DelayBlocks(18,20,1), "delay retains last unit");
            Check(OutpostSchedule.Batch(0,20,1,true)==10, "AI delayed initial half group");
            Check(OutpostSchedule.Batch(0,20,0,true)==1 && OutpostSchedule.Batch(0,20,1,false)==1 && OutpostSchedule.Batch(10,20,1,true)==1, "ordinary single spawns");
            int members=0, handedOff=0;
            for(int attempt=0;attempt<22;attempt++) {
                // Two failed allocations must not count toward a full group.
                if(attempt != 3 && attempt != 9) members++;
                if(OutpostSchedule.Complete(members,20,0)) handedOff++;
            }
            Check(members==20 && handedOff==1, "failed partial allocations retain group until target");
            Check(!OutpostSchedule.HasCapacity(3,100,0,100) && !OutpostSchedule.HasCapacity(3,99,1,100), "capacity with current tick allocations");
            Check(OutpostSchedule.HasCapacity(3,98,1,100) && OutpostSchedule.HasCapacity(0,100,0,100), "capacity exceptions");
            Check(OutpostSchedule.SameIdentity(10,1,106,10,1,106),"same building after save");
            Check(!OutpostSchedule.SameIdentity(10,1,106,11,1,106),"slot reuse invalidates");
            Check(!OutpostSchedule.SameIdentity(10,1,106,10,2,106),"owner change invalidates");
            Check(!OutpostSchedule.SameIdentity(10,1,106,10,1,107),"type change invalidates");
            Check(OutpostSchedule.FinishRetired(true,true,106),"clear linked group before native delete");
            Check(!OutpostSchedule.FinishRetired(false,false,106) && !OutpostSchedule.FinishRetired(false,false,107),"native cleanup already hands off European/Arab groups");
            Check(OutpostSchedule.FinishRetired(false,false,2),"Bedouin deleted record needs handoff");
            Check(!OutpostSchedule.FinishRetired(false,true,2),"already detached group not handed off twice");
            int roll=OutpostSchedule.Roll(42,int.MaxValue,2,10);
            Check(roll>=0 && roll<10 && roll==OutpostSchedule.Roll(42,int.MaxValue,2,10), "stable bounded random choice");
            bool sizeRejected=false;
            try { OutpostSchedule.SpawnWait(0,7); } catch(InvalidOperationException) { sizeRejected=true; }
            Check(sizeRejected,"unsupported size fails closed");
            byte[] dll = File.ReadAllBytes(Path.Combine(Game, @"Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll"));
            using (var sha = SHA256.Create()) Check(BitConverter.ToString(sha.ComputeHash(dll)).Replace("-", "") == Shared.DebugLogHelper.CurrentNativeSha256, "native hash");
            Check(ReadRva(dll, OutpostGate.Rva, 16).SequenceEqual(OutpostGate.Bytes), "gate bytes");
            byte[] profile=ReadRva(dll,0x2DD880+2*52,52);
            Check(BitConverter.ToInt32(profile,0)==250 && BitConverter.ToInt32(profile,4)==100 && BitConverter.ToInt32(profile,8)==10 && BitConverter.ToInt32(profile,12)==10 && BitConverter.ToInt32(profile,16)==26 && BitConverter.ToInt32(profile,48)==184,"audited Macemen profile");
            Check(ReadRva(dll,0x196100,14).SequenceEqual(new byte[]{0x48,0x83,0xEC,0x48,0x48,0x63,0xC2,0x4C,0x8D,0x1D,0x12,0x06,0xB3,0x07}),"native run wrapper entry matches audited ABI");
            Check(ReadRva(dll,0x19612E,4).SequenceEqual(new byte[]{0x0F,0xBA,0xF0,0x07}),"native run wrapper consumes bit seven");
            byte[] createCall=ReadRva(dll,0x11E17C,5);
            Check(createCall[0]==0xE8 && 0x11E17C+5+BitConverter.ToInt32(createCall,1)==0x119C00,"public tribe allocator target");
            byte[] body = ReadRva(dll, 0xABB90, 0xACDE8 - 0xABB90);
            OutpostNative.ValidateControlFlow(body, 0x180000000); count++;
            // An external predecessor targeting the gate's interior must be rejected.
            body[0] = 0xE9;
            Array.Copy(BitConverter.GetBytes(0xABC80 - (0xABB90 + 5)), 0, body, 1, 4);
            bool rejected = false;
            try { OutpostNative.ValidateControlFlow(body, 0x180000000); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "incoming branch regression");
            ExecuteGate();
        }
        private static byte[] ReadRva(byte[] file, int rva, int size)
        {
            int pe = BitConverter.ToInt32(file, 0x3C), n = BitConverter.ToUInt16(file, pe + 6);
            int table = pe + 24 + BitConverter.ToUInt16(file, pe + 20);
            for (int i = 0; i < n; i++)
            {
                int p = table + i * 40, start = BitConverter.ToInt32(file, p + 12), raw = BitConverter.ToInt32(file, p + 16);
                if (rva >= start && rva + size <= start + raw)
                { byte[] result = new byte[size]; Array.Copy(file, BitConverter.ToInt32(file, p + 20) + rva - start, result, 0, size); return result; }
            }
            throw new Exception("RVA outside file-backed section");
        }
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Probe(int unused, int mode);
        private static void ExecuteGate()
        {
            IntPtr memory = VirtualAlloc(IntPtr.Zero, (UIntPtr)4096, 0x3000, 0x40);
            if (memory == IntPtr.Zero) throw new Exception("VirtualAlloc failed");
            try
            {
                ulong b = unchecked((ulong)memory.ToInt64());
                var decoder = Decoder.Create(64, new ByteArrayCodeReader(OutpostGate.Bytes), b + 0x200);
                var original = new[] { decoder.Decode(), decoder.Decode(), decoder.Decode() };
                original[0].MemoryDisplacement64 = b + 0xE20;
                original[1].NearBranch64 = b + 0x600;
                // Real installed RedBird decoder, on copied bytes, without enabling a hook.
                Marshal.Copy(OutpostGate.Bytes, 0, (IntPtr)(b + 0x200), 16);
                using (var probe = new X64InlineHook(b + 0x200, 7))
                    Check(probe.DisplacedByteCount == 16, "installed backend expands 7 to 16");
                var entry = new Assembler(64);
                entry.push(r15); entry.mov(r15d, edx); entry.mov(r11d, 1);
                entry.mov(rax, 0x12345UL); entry.AddUnrestrictedJmp(b + 0x800);
                Emit(entry, b);
                var exit = new Assembler(64); var bad = exit.CreateLabel();
                exit.cmp(rax, 0x12345); exit.jne(bad); exit.mov(eax, 10); exit.pop(r15); exit.ret();
                exit.Label(ref bad); exit.mov(eax, 99); exit.pop(r15); exit.ret(); Emit(exit, b + 0x600);
                var back = new Assembler(64); var zero = back.CreateLabel();
                back.je(zero); back.mov(eax, 21); back.pop(r15); back.ret();
                back.Label(ref zero); back.mov(eax, 20); back.pop(r15); back.ret(); Emit(back, b + 0x210);
                var gate = new Assembler(64);
                OutpostGate.Generate(gate, original, b + 0x210, b + 0xE00, b + 0x600);
                Emit(gate, b + 0x800);
                if (!FlushInstructionCache(GetCurrentProcess(), memory, (UIntPtr)4096)) throw new Exception("FlushInstructionCache failed");
                var run = Marshal.GetDelegateForFunctionPointer<Probe>(memory);
                foreach (int enabled in new[] { 0, 1 })
                foreach (int gameMode in new[] { 0, 1, 6 })
                foreach (int r15Mode in new[] { 0, 99 })
                {
                    Marshal.WriteInt32((IntPtr)(b + 0xE00), enabled);
                    Marshal.WriteInt64((IntPtr)(b + 0xE08), 0);
                    Marshal.WriteInt32((IntPtr)(b + 0xE20), gameMode);
                    int expected = enabled != 0 || gameMode == 1 ? 10 : r15Mode == 0 ? 20 : 21;
                    Check(run(0, r15Mode) == expected, $"executable gate enabled={enabled} editor={gameMode} r15={r15Mode}");
                    Check(Marshal.ReadInt64((IntPtr)(b + 0xE08)) == enabled, "native gate counter");
                }
            }
            finally { VirtualFree(memory, UIntPtr.Zero, 0x8000); }
        }
        private static void Emit(Assembler a, ulong address)
        {
            using (var stream = new MemoryStream())
            {
                if (!a.TryAssemble(new StreamCodeWriter(stream), address, out string error, out _)) throw new Exception(error);
                byte[] bytes = stream.ToArray();
                // RedBird's AddUnrestrictedJmp emits FF 25 00000000 followed by
                // dq(target), not eight additional instruction bytes. Validate
                // every instruction and literal separately (ASLR-independent).
                var boundaries = new System.Collections.Generic.HashSet<ulong>();
                var branches = new System.Collections.Generic.List<ulong>();
                for (int offset = 0; offset < bytes.Length;)
                {
                    boundaries.Add(address + (ulong)offset);
                    var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes.Skip(offset).ToArray()), address + (ulong)offset);
                    var instruction = decoder.Decode();
                    Check(!instruction.IsInvalid, "generated instruction decodes");
                    if (offset + 6 <= bytes.Length && bytes[offset] == 0xFF && bytes[offset+1] == 0x25 &&
                        BitConverter.ToInt32(bytes, offset+2) == 0)
                    {
                        Check(offset + 14 <= bytes.Length && instruction.Length == 6 && instruction.FlowControl == FlowControl.IndirectBranch,
                            "RedBird absolute jump instruction/literal contract");
                        Check(BitConverter.ToUInt64(bytes, offset+6) != 0, "absolute jump target is nonzero");
                        branches.Add(BitConverter.ToUInt64(bytes, offset+6));
                        offset += 14;
                    }
                    else
                    {
                        if (instruction.FlowControl == FlowControl.ConditionalBranch || instruction.FlowControl == FlowControl.UnconditionalBranch)
                            branches.Add(instruction.NearBranchTarget);
                        offset += instruction.Length;
                    }
                }
                foreach (ulong target in branches)
                    if (target >= address && target < address + (ulong)bytes.Length)
                        Check(boundaries.Contains(target), "generated jump does not enter literal/instruction interior");
                Marshal.Copy(bytes, 0, (IntPtr)address, bytes.Length);
            }
        }
        [DllImport("kernel32.dll")] private static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr size, uint type, uint protection);
        [DllImport("kernel32.dll")] private static extern bool VirtualFree(IntPtr address, UIntPtr size, uint type);
        [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll")] private static extern bool FlushInstructionCache(IntPtr process, IntPtr address, UIntPtr size);
    }
}
