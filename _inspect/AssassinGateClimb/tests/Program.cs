using APIShared;
using Iced.Intel;
using RedBird.Abstractions.Hooks;
using RedBird.Backends.NativeX64;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.IO;
using System.Runtime.InteropServices;
using static Iced.Intel.AssemblerRegisters;

internal static class Program
{
    [DllImport("kernel32.dll")] private static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr size, uint type, uint protect);
    [DllImport("kernel32.dll")] private static extern bool VirtualFree(IntPtr address, UIntPtr size, uint type);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int Fixture();
    private static int checks;
    private static void Check(bool value, string text) { if (!value) throw new Exception(text); checks++; }
    private static int Main()
    {
        try
        {
            Check((int)eStructs.STRUCT_GATE_MAIN == 45 && (int)eStructs.STRUCT_GATE_INNER == 46 &&
                (int)TilePropertyFlag.IsWall == 256 && (int)AliveState.MarkedForDeletion == 3, "Installed enum contracts");
            TestBackend();
            TestEndpoints();
            Console.WriteLine("PASS: " + checks + " Assassin shared native execution checks.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static void Write(IntPtr address, byte[] code) => Marshal.Copy(code, 0, address, code.Length);
    private static byte[] Assemble(Assembler assembler, ulong address)
    {
        using (var stream = new MemoryStream())
        {
            assembler.Assemble(new StreamCodeWriter(stream), address);
            byte[] bytes = stream.ToArray();
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes), address);
            while (decoder.IP < address + (uint)bytes.Length)
                Check(decoder.Decode().Code != Code.INVALID, "Generated instruction decode");
            return bytes;
        }
    }
    private static void TestBackend()
    {
        IntPtr code = VirtualAlloc(IntPtr.Zero, (UIntPtr)4096, 0x3000, 0x40);
        if (code == IntPtr.Zero) throw new Exception("Native fixture allocation");
        NativeDetour<AssassinPathBuilder> candidate = null;
        AssassinPathBuilder original = null;
        int calls = 0;
        AssassinPathBuilder callback = (c, sx, sy, tx, ty, limit, continuation) =>
        { calls++; return original(c, sx, sy, tx, ty, limit, continuation); };
        try
        {
            // Actual D9C40 Indirect displaced prologue and native sixth-argument continuation.
            Write(code, new byte[] { 0x48,0x89,0x5C,0x24,0x08,0x48,0x89,0x6C,0x24,0x18,0x8B,0x44,0x24,0x30,0xC3 });
            var request = new DetourRequest<AssassinPathBuilder> {
                Name = "Shared Assassin exact NativeX64 probe", TargetAddress = (ulong)code.ToInt64(), Callback = callback };
            candidate = AssassinPathAPI.Backend.CreateDetour(in request) as NativeDetour<AssassinPathBuilder>;
            Check(candidate != null && candidate.Scheme == DetourScheme.Indirect && candidate.DisplacedByteCount == 10,
                "Actual NativeX64 Indirect displacement");
            original = candidate.Original;
            candidate.Enable();
            AssassinPathNativeDefinition.ValidateInstalledDetour(candidate, (ulong)code.ToInt64() - 0xD9C40);
            var invoke = Marshal.GetDelegateForFunctionPointer<AssassinPathBuilder>(code);
            Check(invoke(IntPtr.Zero, 10, 20, 30, 40, 12345, 0) == 12345 && calls == 1, "Exact callback/original ABI");
            Check(invoke(IntPtr.Zero, 10, 20, -1, -1, 54321, 1) == 54321 && calls == 2, "Continuation/flood ABI");
        }
        finally
        {
            candidate?.Dispose(); // Isolated test allocation; never a published game hook.
            VirtualFree(code, UIntPtr.Zero, 0x8000);
            GC.KeepAlive(callback);
        }
    }
    private static void TestEndpoints()
    {
        IntPtr allocation = VirtualAlloc(IntPtr.Zero, (UIntPtr)0x7000000, 0x3000, 0x40);
        if (allocation == IntPtr.Zero) throw new Exception("Native endpoint fixture allocation");
        ulong module = (ulong)allocation.ToInt64();
        IntPtr policy = IntPtr.Add(allocation, 0x200);
        try
        {
            // All code is prepared before publishing delegates. Subsequent test writes are data only.
            foreach (int rva in new[] { 0xD9FA6, 0xE1A8F, 0xE1A87 }) Write(IntPtr.Add(allocation,rva), new byte[] { 0x31,0xC0,0xC3 });
            Write(IntPtr.Add(allocation,0xE1A1F), new byte[] { 0xB8,1,0,0,0,0xC3 });
            Marshal.WriteInt32(IntPtr.Add(allocation,0x405EDB0),1); // Direction-zero neighbor delta.
            var fixtures = new Fixture[4];
            for (int site = 0; site < 4; site++)
            {
                string[] tokens = AssassinPathNativeDefinition.Patterns[site].Split(' ');
                byte[] bytes = Array.ConvertAll(tokens, t => Convert.ToByte(t,16));
                IntPtr copiedSite = IntPtr.Add(allocation, AssassinPathNativeDefinition.Sites[site]);
                Write(copiedSite, bytes);
                using (var probe = new RedBird.X64.Hooks.X64InlineHook((ulong)copiedSite.ToInt64(), 14))
                {
                    int capturedSite = site;
                    probe.Generate((assembler, original, continuation) => AssassinEndpointEmitter.Emit(
                        assembler, original, module, (ulong)policy.ToInt64(), capturedSite));
                    Check(probe.DisplacedByteCount == AssassinPathNativeDefinition.Lengths[site],
                        "Actual installed RedBird inline generator/displacement");
                }
                var decoder = Decoder.Create(64,new ByteArrayCodeReader(bytes),module+(uint)AssassinPathNativeDefinition.Sites[site]);
                var instructions = new System.Collections.Generic.List<Instruction>();
                while (decoder.IP < module+(uint)(AssassinPathNativeDefinition.Sites[site]+bytes.Length)) instructions.Add(decoder.Decode());
                ulong stubAddress = module + (uint)(0x1000 + site*0x1000);
                var stub = new Assembler(64);
                AssassinEndpointEmitter.Emit(stub,instructions.ToArray(),module,(ulong)policy.ToInt64(),site);
                if (site == 3) stub.je(module+0xE1A87); // Complete the real final wall-test continuation.
                stub.mov(eax,1); stub.ret();
                Write(new IntPtr((long)stubAddress),Assemble(stub,stubAddress));
                ulong wrapperAddress = stubAddress+0x800;
                var wrapper = new Assembler(64);
                wrapper.push(rbx); wrapper.push(rbp); wrapper.push(rsi); wrapper.push(rdi);
                wrapper.push(r12); wrapper.push(r13); wrapper.push(r14); wrapper.push(r15);
                wrapper.mov(r15,module); wrapper.mov(r8,100UL); wrapper.mov(r13,101UL);
                wrapper.mov(rcx,101UL); wrapper.mov(r12,202UL); wrapper.mov(r14,0UL);
                wrapper.mov(rdx,site < 2 ? module : 0UL);
                wrapper.mov(r11,module+AssassinPathAPI.BuildingGridRva);
                wrapper.movzx(eax,__word_ptr[r11+200]);
                wrapper.mov(r10d,eax); wrapper.mov(r11d,eax);
                wrapper.mov(rax,module+AssassinPathAPI.TileFlagsRva);
                wrapper.mov(r9d,__dword_ptr[rax+400]); wrapper.mov(edi,__dword_ptr[rax+404]);
                if (site == 1 || site == 3)
                {
                    wrapper.mov(rax,module+AssassinPathAPI.BuildingGridRva);
                    wrapper.movzx(eax,__word_ptr[rax+202]);
                    wrapper.cmp(eax,site == 3 ? r10d : eax); // Below: distinct zero/source comparison.
                    if (site == 1) wrapper.test(eax,eax);
                }
                else wrapper.test(r10d,r10d);
                wrapper.mov(rax,stubAddress); wrapper.call(rax);
                wrapper.pop(r15); wrapper.pop(r14); wrapper.pop(r13); wrapper.pop(r12);
                wrapper.pop(rdi); wrapper.pop(rsi); wrapper.pop(rbp); wrapper.pop(rbx); wrapper.ret();
                Write(new IntPtr((long)wrapperAddress),Assemble(wrapper,wrapperAddress));
                fixtures[site]=Marshal.GetDelegateForFunctionPointer<Fixture>(new IntPtr((long)wrapperAddress));
            }
            for (int policyValue = 0; policyValue <= 3; policyValue++)
            {
                Marshal.WriteInt32(policy,policyValue);
                foreach (int type in new[] { 45,46,47,41 })
                foreach (int alive in new[] { 0,1,2,3 })
                foreach (bool sourceGate in new[] { false,true })
                foreach (bool targetGate in new[] { false,true })
                {
                    Marshal.WriteInt16(IntPtr.Add(allocation,AssassinPathAPI.BuildingGridRva+200),(short)(sourceGate?1:0));
                    Marshal.WriteInt16(IntPtr.Add(allocation,AssassinPathAPI.BuildingGridRva+202),(short)(targetGate?1:0));
                    Marshal.WriteInt16(IntPtr.Add(allocation,AssassinPathAPI.BuildingTypeBaseRva+AssassinPathAPI.BuildingStride),(short)type);
                    Marshal.WriteInt16(IntPtr.Add(allocation,AssassinPathAPI.BuildingAliveBaseRva+AssassinPathAPI.BuildingStride),(short)alive);
                    Marshal.WriteInt32(IntPtr.Add(allocation,AssassinPathAPI.TileFlagsRva+400),256);
                    Marshal.WriteInt32(IntPtr.Add(allocation,AssassinPathAPI.TileFlagsRva+404),256);
                    bool gateAllowed = (policyValue & 2)!=0 && (type==45 || type==46) && alive!=0 && alive!=3;
                    for (int site=0; site<4; site++)
                    {
                        bool occupied = site==1 || site==3 ? targetGate : sourceGate;
                        bool broad = site>=2 && (policyValue&1)!=0;
                        // In Vanilla, source rejection makes the neighbor compare's source ID zero.
                        // Test the neighbor's disabled compare both ways as native instructions specify.
                        bool vanilla = site==3 ? targetGate==sourceGate : !occupied;
                        bool active = site>=2 ? policyValue!=0 : (policyValue&2)!=0;
                        bool expected = active ? broad || !occupied || gateAllowed : vanilla;
                        Check(fixtures[site]() == (expected?1:0),
                            $"Endpoint site={site}/policy={policyValue}/type={type}/alive={alive}/source={sourceGate}/target={targetGate}");
                    }
                }
            }
            GC.KeepAlive(fixtures);
        }
        finally { VirtualFree(allocation,UIntPtr.Zero,0x8000); }
    }
}
