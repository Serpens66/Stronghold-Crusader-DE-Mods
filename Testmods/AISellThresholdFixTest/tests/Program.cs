using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using AISellThresholdFixTest;
using Iced.Intel;
using RedBird.X64.Extensions;
using RedBird.X64.Hooks;
using SHCDESE.Interop;
using static Iced.Intel.AssemblerRegisters;

internal static class Program
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void Sale(IntPtr root, int player, int good, int amount);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void Decision(IntPtr root, int player);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Invoke();
    [DllImport("kernel32", SetLastError=true)] private static extern IntPtr VirtualAlloc(IntPtr p, UIntPtr n, uint allocation, uint protection);
    [DllImport("kernel32", SetLastError=true)] private static extern bool VirtualFree(IntPtr p, UIntPtr n, uint mode);
    [DllImport("kernel32", SetLastError=true)] private static extern bool VirtualProtect(IntPtr p, UIntPtr n, uint protection, out uint old);
    [DllImport("kernel32")] private static extern IntPtr GetCurrentProcess();
    [DllImport("kernel32")] private static extern bool FlushInstructionCache(IntPtr process, IntPtr p, UIntPtr n);
    private static int checks;
    private const int ImageSize = 0x9000000, Player = 1, Manager = 0x64CCBB0, Profile = 0x404C950;
    private static readonly int[] Goods = { 2,3,4,6,7,9,10,11,12,13,14,16,17,18,19,20,21,22,23,24 };
    private static readonly int[] Categories = { 1,6,3,0,8,2,2,2,2,2,5,7,4,4,4,4,4,4,4,4 };
    private static readonly int[] ThresholdOffsets = { 0xB0,0xA8,0x7C,0xAC,0xB8,0xBC,0xB0,0xB4,0xC0 };
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
    private static void Put(IntPtr image, int offset, int value) => Marshal.WriteInt32(image, offset, value);
    private static int Get(IntPtr image, int offset) => Marshal.ReadInt32(image, offset);
    private static int StockOffset(int player, int good) => SellAssembly.GoodsBase + player * SellAssembly.PlayerStride + good * 4;
    private static int GoldOffset(int player) => SellAssembly.GoldBase + player * SellAssembly.PlayerStride;
    private static void Word(IntPtr image, int offset, int value) => Marshal.WriteInt16(image, offset, (short)value);
    private static void Copy(IntPtr image, int rva, byte[] bytes) => Marshal.Copy(bytes, 0, image + rva, bytes.Length);

    private static void Main(string[] args)
    {
        if (args.Length == 3 && args[0] == "--artifact") { ValidateArtifact(args[1], bool.Parse(args[2])); return; }
        var native = File.ReadAllBytes(args[0]);
        Check(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(native)) == SellAssembly.NativeHash, "native identity");
        var image = VirtualAlloc(IntPtr.Zero, (UIntPtr)ImageSize, 0x3000, 0x40);
        var state = Marshal.AllocHGlobal(64);
        if (image == IntPtr.Zero) throw new Exception("Private native image allocation failed.");
        try
        {
            for (int i=0;i<64;i+=8) Marshal.WriteInt64(state,i,0);
            byte[] At(int rva, int length)
            {
                int pe = BitConverter.ToInt32(native,0x3C), count = BitConverter.ToUInt16(native,pe+6);
                int table = pe+24+BitConverter.ToUInt16(native,pe+20);
                for (int i=0;i<count;i++)
                {
                    int s=table+i*40, va=BitConverter.ToInt32(native,s+12), size=BitConverter.ToInt32(native,s+16);
                    if (rva>=va && rva+length<=va+size)
                        return native.Skip(BitConverter.ToInt32(native,s+20)+rva-va).Take(length).ToArray();
                }
                throw new Exception("RVA outside PE file.");
            }
            foreach (var f in new[] {(0x29700,160),(0x3EE10,1105),(0xCEB90,31),(0xB7D80,953),(0xBFD30,14),(0xC1CE0,639),(0xCB050,63)})
                Copy(image,f.Item1,At(f.Item1,f.Item2));
            Copy(image,0x2CEC90,At(0x2CEC90,160)); Copy(image,0x2E76D0,At(0x2E76D0,100));
            // External rendering boundaries only. Actual physical stock removal and totals run unchanged.
            Copy(image,0xC05B0,new byte[]{0xC3}); Copy(image,0x6E620,new byte[]{0xC3});
            Check(Marshal.SizeOf<GamePlayerResources>() == SellAssembly.PlayerStride, "installed player stride");
            Check((int)eGoods.STORED_FOOD_ALE == 14 && (int)eGoods.Count == 25, "installed goods contract");
            var sale = Marshal.GetDelegateForFunctionPointer<Sale>(image+0x29700);
            var decide = Marshal.GetDelegateForFunctionPointer<Decision>(image+0x3EE10);
            Reset(image); Threshold(image,5,-999); SetStock(image,1,14,0);
            decide(image+Profile,1);
            Check(Get(image,GoldOffset(1))>1000 && Get(image,StockOffset(1,14))==0, "unpatched original Vanilla reproduces empty-ale money exploit");
            Reset(image); SetStock(image,1,14,3); sale(image+Profile,1,14,99);
            Check(Get(image,GoldOffset(1))==1198 && Get(image,StockOffset(1,14))==0, "unpatched credit uses 99 while actual removal can remove only 3");

            var hooks = new List<X64InlineHook>();
            try
            {
                for (int i=0;i<2;i++)
                {
                    int index=i, site=SellAssembly.Sites[i];
                    Check(At(site,15).SequenceEqual(SellAssembly.Hex(SellAssembly.OriginalHex[i])), "real native hook fingerprint "+i);
                    var hook=new X64InlineHook((ulong)(image+site).ToInt64(),15);
                    Check(hook.DisplacedByteCount==15, "actual installed RedBird span "+i);
                    hook.Generate((a,original,back)=>SellAssembly.Generate(a,original,back,index,(ulong)image.ToInt64(),(ulong)state.ToInt64()));
                    hooks.Add(hook); hook.Enable();
                    Check(hook.IsInstalled && Marshal.ReadByte(image,site)==0xFF && Marshal.ReadByte(image,site+14)==0x90,"actual backend patch "+i);
                }
                // Fixture hooks are private and never published to the game. Test inactive transparency first.
                Reset(image); Threshold(image,5,-999); decide(image+Profile,1);
                Check(Get(image,GoldOffset(1))>1000,"inactive generators preserve Vanilla exploit (no accidental behavior change)");
                Marshal.WriteInt32(state,1);
                TestTransactions(image,sale);
                TestDecisions(image,decide,state);
                TestRegisterAndStackContracts(image,state);
            }
            finally { foreach (var hook in hooks) hook.Dispose(); }
        }
        finally { Marshal.FreeHGlobal(state); VirtualFree(image,UIntPtr.Zero,0x8000); }
        Console.WriteLine($"PASS: {checks} checks; original native exploit reproduced, actual native sale/decision/removal/recount executed, installed RedBird stubs executed, all 20 goods and 8 categories covered.");
    }
    private static void ValidateArtifact(string path, bool diagnostics)
    {
        using var stream = File.OpenRead(path);
        using var pe = new System.Reflection.PortableExecutable.PEReader(stream);
        var metadata = System.Reflection.Metadata.PEReaderExtensions.GetMetadataReader(pe);
        var definition = metadata.GetAssemblyDefinition();
        Check(definition.Version == new Version(0,1,0,0), "compiled assembly version");
        bool marker = false;
        foreach (var handle in definition.GetCustomAttributes())
        {
            var attribute = metadata.GetCustomAttribute(handle);
            if (attribute.Constructor.Kind != System.Reflection.Metadata.HandleKind.MemberReference) continue;
            var constructor = metadata.GetMemberReference((System.Reflection.Metadata.MemberReferenceHandle)attribute.Constructor);
            if (constructor.Parent.Kind != System.Reflection.Metadata.HandleKind.TypeReference) continue;
            var type = metadata.GetTypeReference((System.Reflection.Metadata.TypeReferenceHandle)constructor.Parent);
            if (metadata.GetString(type.Name) != "AssemblyMetadataAttribute") continue;
            var blob = metadata.GetBlobReader(attribute.Value);
            Check(blob.ReadUInt16() == 1, "assembly metadata attribute encoding");
            string key = blob.ReadSerializedString(), value = blob.ReadSerializedString();
            if (key == "SerpDevDiagnostics" && value == "true") marker = true;
        }
        Check(marker == diagnostics, "DEVDIAG artifact marker matches explicit build choice");
        Check(metadata.TypeDefinitions.Select(h=>metadata.GetString(metadata.GetTypeDefinition(h).Name)).Contains("DevDiagRuntime") == diagnostics,
            "development diagnostic implementation compiled only in DEVDIAG artifact");
        foreach (var h in metadata.AssemblyReferences)
        {
            string name = metadata.GetString(metadata.GetAssemblyReference(h).Name);
            Check(name != "APIShared" && name != "Assembly-CSharp" && name.IndexOf("Json",StringComparison.OrdinalIgnoreCase)<0 && name!="System.Web.Extensions",
                "standalone runtime reference: "+name);
        }
        Console.WriteLine($"PASS: runtime artifact version, direct dependencies and DEVDIAG compile boundary (diagnostics={diagnostics}).");
    }
    private static void Reset(IntPtr image)
    {
        for (int player=1;player<=8;player++)
        {
            for(int g=0;g<25;g++) Put(image,StockOffset(player,g),0);
            Put(image,GoldOffset(player),1000);
            Put(image,0x379D0D0+player*SellAssembly.PlayerStride,1);
            Put(image,0x379AE30+player*SellAssembly.PlayerStride,10000);
            Put(image,0x379E6C0+player*SellAssembly.PlayerStride,0);
            Put(image,0x379AE24+player*SellAssembly.PlayerStride,0);
            Put(image,0x87EED98+player*4,0);
            for(int g=0;g<25;g++)
            {
                Put(image,0x379D840+player*SellAssembly.PlayerStride+g*4,0);
                Put(image,0x379FDA0+player*SellAssembly.PlayerStride+g*4,0);
            }
        }
        for(int i=0;i<0x5E4;i+=4) Put(image,Profile+i,0);
        foreach(int offset in ThresholdOffsets) Put(image,Profile+offset,1000000);
        for(int g=0;g<25;g++) Put(image,0x366C210+0x1817BC+g*8,10); // sell unit price=2
        Put(image,Manager+0x50,4);
        for(int id=1;id<=3;id++)
        {
            int row=Manager+id*0x32C;
            for(int g=0;g<25;g++) Put(image,row+0x17C+g*4,0);
            Word(image,row+0x12C,2); Word(image,row+0x12E,id==1?10:id==2?19:11); Word(image,row+0x132,1);
        }
    }
    private static void SetStock(IntPtr image,int player,int good,int stock)
    {
        Put(image,StockOffset(player,good),stock);
        int id=good>=17?3:good>=10&&good<=13?2:1;
        Word(image,Manager+id*0x32C+0x132,player);
        Put(image,Manager+id*0x32C+0x17C+good*4,stock);
    }
    private static void Threshold(IntPtr image,int category,int value) => Put(image,Profile+ThresholdOffsets[category],value);

    private static void TestTransactions(IntPtr image,Sale sale)
    {
        foreach(int player in new[]{1,8})
        foreach(int good in Goods)
        foreach(int stock in new[]{-1,0,1,3,100})
        foreach(int request in new[]{int.MinValue,-1,0,1,2,100,int.MaxValue})
        {
            Reset(image); SetStock(image,player,good,stock);
            sale(image+Profile,player,good,request);
            int amount=Math.Min(Math.Max(request,0),Math.Max(stock,0));
            Check(Get(image,GoldOffset(player))==1000+amount*2,$"credit bound p={player},g={good},s={stock},q={request}");
            Check(Get(image,StockOffset(player,good))==stock-amount,"actual removal equals credited quantity");
            Check(Get(image,0x87EED98+player*4)==amount*2 && Get(image,0x379AE24+player*SellAssembly.PlayerStride)==amount*2,"all three native sale bookings use limited quantity");
        }
        Reset(image); sale(image+Profile,1,(int)eGoods.STORED_GOLD,1);
        Check(Get(image,GoldOffset(1))==1000,"pseudo sale of gold rejected");
        foreach(int good in new[]{-1,0,25,int.MaxValue})
        { Reset(image); sale(image+Profile,1,good,1); Check(Get(image,GoldOffset(1))==1000,"invalid goods rejected before memory index"); }
        Console.WriteLine("PASS: full native sale, price, physical resource subtraction and total recount; negative/zero/oversized requests, signed stock bounds, players 1/8.");
    }
    private static void TestDecisions(IntPtr image,Decision decide,IntPtr state)
    {
        foreach(int index in Enumerable.Range(0,Goods.Length))
        foreach(int threshold in new[]{int.MinValue,-999,-1,0,1,10,int.MaxValue})
        foreach(int stock in new[]{0,1,20})
        foreach(int minimum in new[]{int.MinValue,-1,0,5,int.MaxValue})
        foreach(int branch in new[]{0,1,2,3})
        {
            Reset(image); int good=Goods[index]; Threshold(image,Categories[index],threshold);
            Put(image,Profile+0xC4,minimum); SetStock(image,1,good,stock);
            if(branch==1) { Put(image,Profile+0x18,20000); }
            if(branch==2) { Put(image,0x379E6C0+SellAssembly.PlayerStride,1); Put(image,GoldOffset(1),100); }
            if(branch==3) { Put(image,Profile+0xD8,good); Put(image,0x379D840+SellAssembly.PlayerStride+good*4,7); }
            int before=Get(image,GoldOffset(1)); decide(image+Profile,1);
            long credited=(long)Get(image,GoldOffset(1))-before;
            int after=Get(image,StockOffset(1,good));
            Check(credited>=0 && credited<=stock*2L && after>=0 && credited==(stock-after)*2L,
                $"decision accounting g={good},t={threshold},s={stock},m={minimum},branch={branch}");
        }
        // The first negative category previously captured every sale opportunity with zero stock.
        Reset(image); Threshold(image,1,-999); Threshold(image,5,-999); SetStock(image,1,14,4);
        decide(image+Profile,1);
        Check(Get(image,StockOffset(1,14))==0 && Get(image,GoldOffset(1))==1008,"empty earlier category does not starve later stocked ale");
        for(int i=0;i<20;i++) decide(image+Profile,1);
        Check(Get(image,GoldOffset(1))==1008,"repeated empty decisions cannot generate gold");
        // Compare regular, valid transactions byte-for-byte in observable native accounting.
        foreach(int index in Enumerable.Range(0,Goods.Length))
        foreach(int branch in new[]{0,1,2,3})
        {
            int good=Goods[index];
            int[] Run(int active)
            {
                Reset(image); Threshold(image,Categories[index],10); Put(image,Profile+0xC4,3); SetStock(image,1,good,30);
                if(branch==1) Put(image,Profile+0x18,20000);
                if(branch==2) {Put(image,0x379E6C0+SellAssembly.PlayerStride,1);Put(image,GoldOffset(1),100);}
                if(branch==3) Put(image,Profile+0xD8,good);
                Marshal.WriteInt32(state,active); decide(image+Profile,1);
                return new[]{Get(image,StockOffset(1,good)),Get(image,GoldOffset(1)),Get(image,0x87EED98+4),Get(image,0x379AE24+SellAssembly.PlayerStride)};
            }
            Check(Run(0).SequenceEqual(Run(1)),"valid Vanilla sale parity category/branch "+index+"/"+branch);
        }
        Marshal.WriteInt32(state,1);
        Console.WriteLine("PASS: actual full decision loop: 20 goods, all category mappings, signed extreme thresholds/minimums, low popularity, emergency, demand/sell_all, repeated empty stock and Vanilla parity.");
    }
    private sealed class Code : IDisposable
    {
        internal readonly IntPtr Address;
        internal readonly byte[] Bytes;
        internal Code(Action<Assembler> generate)
        {
            Address=VirtualAlloc(IntPtr.Zero,(UIntPtr)4096,0x3000,0x04);
            if(Address==IntPtr.Zero) throw new Exception("Fixture code allocation failed");
            var a=new Assembler(64);generate(a);var bytes=SellAssembly.Assemble(a,(ulong)Address.ToInt64()); Bytes=bytes;
            Marshal.Copy(bytes,0,Address,bytes.Length);
            if(!VirtualProtect(Address,(UIntPtr)4096,0x20,out _) || !FlushInstructionCache(GetCurrentProcess(),Address,(UIntPtr)bytes.Length)) throw new Exception("Fixture code protection failed");
        }
        public void Dispose()=>VirtualFree(Address,UIntPtr.Zero,0x8000);
    }
    private static void TestRegisterAndStackContracts(IntPtr image,IntPtr state)
    {
        // Execute the production generator itself with native sentinels and an exact continuation.
        var capture=Marshal.AllocHGlobal(128);
        try
        {
            foreach(int index in new[]{0,1})
            foreach(int input in new[]{-999,0,1,int.MaxValue})
            {
                Reset(image);SetStock(image,1,14,3);
                ulong m=(ulong)image.ToInt64(), c=(ulong)capture.ToInt64();
                using var done=new Code(a=>{
                    a.pushfq();a.push(rax);a.mov(rax,c);
                    a.mov(__qword_ptr[rax],rsp);a.mov(__qword_ptr[rax+8],rcx);a.mov(__qword_ptr[rax+16],rdx);
                    a.mov(__qword_ptr[rax+24],rbx);a.mov(__qword_ptr[rax+32],r8);a.mov(__qword_ptr[rax+40],r9);
                    a.mov(__qword_ptr[rax+48],r10);a.mov(__qword_ptr[rax+56],r11);
                    a.movdqu(__xmmword_ptr[rax+64],xmm0);
                    a.mov(rcx,__qword_ptr[rsp+8]);a.mov(__qword_ptr[rax+80],rcx);a.mov(rcx,__qword_ptr[rax+8]);
                    a.pop(rax);a.popfq();
                    a.add(rsp,40);a.pop(r15);a.pop(r14);a.pop(r13);a.pop(r12);a.pop(rdi);a.pop(rsi);a.pop(rbp);a.pop(rbx);a.mov(eax,1);a.ret();
                });
                // A mini fixture replaces the continuation with an external jump, never game memory.
                int back=SellAssembly.Sites[index]+15;
                var original=SellAssembly.Decode(SellAssembly.Hex(SellAssembly.OriginalHex[index]),m+(uint)SellAssembly.Sites[index]);
                using var stub=new Code(a=>SellAssembly.Generate(a,original,m+(uint)back,index,m,(ulong)state.ToInt64()));
                using var wrapper=new Code(a=>{
                    a.push(rbx);a.push(rbp);a.push(rsi);a.push(rdi);a.push(r12);a.push(r13);a.push(r14);a.push(r15);a.sub(rsp,40);
                    a.mov(rax,c);a.mov(__qword_ptr[rax+88],rsp);
                    a.mov(rax,0x1234UL);a.mov(rcx,0x2345UL);a.mov(edx,input);a.mov(ebx,input);a.mov(edi,1);
                    a.mov(rbp,SellAssembly.PlayerStride+14*4UL);a.mov(r8,14UL);a.mov(r9,(ulong)(uint)input);
                    a.mov(r10,0xAAAAUL);a.mov(r11,0xBBBBUL);a.movq(xmm0,r11);a.mov(r11,0xBBBBUL);
                    a.mov(rax,0x246UL);a.push(rax);a.popfq();a.mov(rax,0x1234UL);
                    a.AddUnrestrictedJmp((ulong)stub.Address.ToInt64());
                });
                var saved=new byte[32];Marshal.Copy(image+back,saved,0,32);
                var jump=new Assembler(64);jump.AddUnrestrictedJmp((ulong)done.Address.ToInt64());Copy(image,back,SellAssembly.Assemble(jump,m+(uint)back));
                // Empty sale path needs the same register-capture exit instead of its real epilogue.
                var epilog=new byte[32];Marshal.Copy(image+SellAssembly.SaleEpilog,epilog,0,32);
                var exit=new Assembler(64);exit.AddUnrestrictedJmp((ulong)done.Address.ToInt64());Copy(image,SellAssembly.SaleEpilog,SellAssembly.Assemble(exit,m+SellAssembly.SaleEpilog));
                try
                {
                    Check(Marshal.GetDelegateForFunctionPointer<Invoke>(wrapper.Address)()==1,"machine contract returned");
                    Check(Marshal.ReadInt64(capture)+16==Marshal.ReadInt64(capture,88),"exact stack balance across generated hook");
                    Check(Marshal.ReadInt64(capture,48)==0xAAAA && Marshal.ReadInt64(capture,56)==0xBBBB && Marshal.ReadInt64(capture,64)==0xBBBB,"volatile and SIMD sentinels preserved");
                    Check(Marshal.ReadInt64(capture,80)==0x246,"incoming flags preserved");
                    if(index==0) Check(Marshal.ReadInt64(capture,16)==Math.Max(0,input),"decision EDX final reserve");
                    else if(input>0) Check(Marshal.ReadInt64(capture,24)==Math.Min(input,3) && Marshal.ReadInt64(capture,40)==Math.Min(input,3),"both amount registers agree");
                }
                finally {Copy(image,back,saved);Copy(image,SellAssembly.SaleEpilog,epilog);}
            }
        }
        finally {Marshal.FreeHGlobal(capture);}
        Console.WriteLine("PASS: production native generators execute with stack/register/SIMD sentinels and both amount outputs.");
    }
}
