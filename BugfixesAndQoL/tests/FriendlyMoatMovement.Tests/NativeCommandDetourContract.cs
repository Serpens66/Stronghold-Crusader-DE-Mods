using Iced.Intel;
using RedBird.Abstractions.Hooks;
using RedBird.Backends.NativeX64;
using RedBird.X64.Assembly;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;

internal static class NativeCommandDetourContract
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int Probe(IntPtr manager,int id);
    private static readonly List<object> roots=new();
    internal static void Validate(string root)
    {
        string game=@"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition";
        string extender=Path.Combine(game,"BepInEx/plugins/000shcdese");
        foreach(var assembly in new[]{typeof(NativeDetourBackend).Assembly,typeof(NativeDetour<>).Assembly,
            typeof(Assembler).Assembly,typeof(DetourRequest<>).Assembly})
        {
            string installed=Path.Combine(extender,Path.GetFileName(assembly.Location));
            if(!SHA256.HashData(File.ReadAllBytes(installed)).SequenceEqual(SHA256.HashData(File.ReadAllBytes(assembly.Location))))
                throw new Exception("Test backend differs from installed assembly: "+assembly.FullName);
        }
        using var native=File.OpenRead(Path.Combine(game,"Stronghold Crusader Definitive Edition_Data/Plugins/x86_64/CrusaderDE.dll"));
        using var pe=new PEReader(native);
        string table=File.ReadAllText(Path.Combine(root,"APIShared/src/UnitCommands/Native/NativeDetourContracts.cs"));
        // Complete function entries used by the shared command transactions. The
        // mode helper additionally has an executable EAX/ZF test in its own fixture.
        int[] targets={0x18E1E0,0x1853F0,0x195E30,0xF4930,0xE32B0,0x124740,0x117BC0,
            0x196840,0xE7C40,0xE9FF0,0xE2CA0,0xE9D90,0xB70C0,0xDBC60,0xDA020,0x123090,
            0xE2610,0x69D60,0x6AF60,0x6C490,0xE1D30,0xE0970,0x118E00,0x181890,0xF03C0,
            0xD90D0,0x59210,0x61E70,0xDAA50,0x196100,0x12BF0,0x11C3A0,0xE49D0,0xC3E50,0xDE6A0};
        var backend=new NativeDetourBackend(new NativeDetourOptions {
            AllowedSchemes=DetourScheme.Indirect,FollowJumps=false });
        foreach(int rva in targets)
        {
            Match entry=Regex.Match(table,@"\{ 0x"+rva.ToString("X")+@", ""([0-9A-F]+)"" \}");
            if(!entry.Success)throw new Exception("Missing native prefix "+rva.ToString("X"));
            byte[] prefix=Convert.FromHexString(entry.Groups[1].Value);
            if(!pe.GetSectionData(rva).GetContent(0,prefix.Length).SequenceEqual(prefix))
                throw new Exception("Installed native prefix differs: "+rva.ToString("X"));
            var target=ExecutableFunction.Create(asm=>{asm.db(prefix);asm.nop();asm.ret();});
            Probe callback=(m,id)=>37;
            var request=new DetourRequest<Probe> { Name="shared command contract "+rva.ToString("X"),
                TargetAddress=target.Address,Callback=callback };
            var candidate=backend.CreateDetour(in request) as NativeDetour<Probe>;
            if(candidate==null || candidate.Scheme!=DetourScheme.Indirect ||
                candidate.DisplacedByteCount!=prefix.Length || candidate.TargetAddress!=target.Address ||
                candidate.TrampolineAddress==IntPtr.Zero || candidate.PointerSlot==IntPtr.Zero ||
                candidate.OriginalEntryPointAddress!=candidate.TrampolineAddress || candidate.IsInstalled)
                throw new Exception("NativeX64 prepublication contract "+rva.ToString("X"));
            roots.Add(target);roots.Add(callback);roots.Add(candidate);
            VerifyContinuation(candidate,target.Address+(uint)prefix.Length);
            candidate.Enable(); // The published fixture is kept until process exit.
            IntPtr start=target.EntryPoint;
            if(!candidate.IsInstalled || Marshal.ReadByte(start)!=0xFF || Marshal.ReadByte(start,1)!=0x25 ||
                IntPtr.Add(start,6+Marshal.ReadInt32(start,2))!=candidate.PointerSlot ||
                Marshal.ReadInt64(candidate.PointerSlot)!=candidate.HookEntryPointAddress.ToInt64() ||
                target.GetDelegate<Probe>()(IntPtr.Zero,1)!=37)
                throw new Exception("NativeX64 installed patch/slot/callback contract "+rva.ToString("X"));
        }
        Console.WriteLine($"PASS: {targets.Length} actual NativeX64 Indirect detours; displaced boundaries, trampoline continuations, slots and published callbacks.");
    }
    private static void VerifyContinuation(NativeDetour<Probe> hook,ulong expected)
    {
        byte[] bytes=new byte[256];Marshal.Copy(hook.TrampolineAddress,bytes,0,bytes.Length);
        var decoder=Decoder.Create(64,new ByteArrayCodeReader(bytes));
        decoder.IP=(ulong)hook.TrampolineAddress.ToInt64();
        bool found=false;
        for(int index=0;index<32;index++)
        {
            var instruction=decoder.Decode();if(instruction.IsInvalid)break;
            if(instruction.Mnemonic!=Mnemonic.Jmp)continue;
            if(instruction.Op0Kind is OpKind.NearBranch64 or OpKind.NearBranch32)
                found=instruction.NearBranchTarget==expected;
            else if(instruction.IsIPRelativeMemoryOperand)
                found=(ulong)Marshal.ReadInt64(new IntPtr((long)instruction.IPRelativeMemoryAddress))==expected;
            break;
        }
        if(!found)throw new Exception("Trampoline does not resume at complete displaced boundary");
    }
}
