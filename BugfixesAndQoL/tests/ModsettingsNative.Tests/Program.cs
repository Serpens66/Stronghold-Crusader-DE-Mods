using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using BugfixesAndQoL;
using Iced.Intel;
using static Iced.Intel.AssemblerRegisters;

// Exercises the production installer on private executable copies, never on the game process.
internal static class Program
{
    private const string DllPath = @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll";
    private static long checks;
    private static readonly List<object> Roots = new List<object>();
    [DllImport("kernel32.dll")] private static extern IntPtr VirtualAlloc(IntPtr p, UIntPtr n, uint flags, uint protection);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate uint Mapper(IntPtr manager, uint mapper);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int GuardProbe(IntPtr results, int compare);
    private static void Check(bool ok, string why) { checks++; if (!ok) throw new Exception(why); }
    public static int Main()
    {
        try
        {
            byte[] file=File.ReadAllBytes(DllPath);
            using(var sha=SHA256.Create()) Check(BitConverter.ToString(sha.ComputeHash(file)).Replace("-","")==NoDogsNativeContract.ReferenceSha256,"canonical hash");
            byte[] image=Map(file);
            Guard();
            NoDogs(image);
            Peace(image);
            Console.WriteLine("PASS: production native settings switches, "+checks+" checks.");return 0;
        }
        catch(Exception ex) { Console.Error.WriteLine(ex);return 1; }
    }
    private static byte[] Map(byte[] file)
    {
        int pe=BitConverter.ToInt32(file,0x3c), opt=pe+24, table=opt+BitConverter.ToInt16(file,pe+20);
        byte[] image=new byte[BitConverter.ToInt32(file,opt+56)];Buffer.BlockCopy(file,0,image,0,BitConverter.ToInt32(file,opt+60));
        for(int n=0;n<BitConverter.ToInt16(file,pe+6);n++)
        {
            int s=table+n*40,size=BitConverter.ToInt32(file,s+16);
            if(size!=0)Buffer.BlockCopy(file,BitConverter.ToInt32(file,s+20),image,BitConverter.ToInt32(file,s+12),size);
        }
        return image;
    }
    private static IntPtr Image(byte[] bytes)
    {
        IntPtr p=VirtualAlloc(IntPtr.Zero,(UIntPtr)bytes.Length,0x3000,0x40);Check(p!=IntPtr.Zero,"fixture allocation");
        Marshal.Copy(bytes,0,p,bytes.Length);Roots.Add(p);return p;
    }
    private static void Copy(IntPtr p,int offset,byte[] bytes)=>Marshal.Copy(bytes,0,IntPtr.Add(p,offset),bytes.Length);
    private static byte[] Read(IntPtr p,int offset,int count)
    { byte[] b=new byte[count];Marshal.Copy(IntPtr.Add(p,offset),b,0,count);return b; }
    private static void Guard()
    {
        IntPtr page=Image(new byte[4096]);ulong b=(ulong)page.ToInt64();IntPtr demand=page+3000;
        var wrapper=new Assembler(64);
        wrapper.sub(rsp,40);wrapper.mov(__qword_ptr[rcx+8],rsp);
        wrapper.mov(r10,0x123456789ABCDEF0UL);wrapper.mov(r11,0x0FEDCBA987654321UL);
        wrapper.movq(xmm0,r10);wrapper.cmp(edx,0);wrapper.call(b+128);wrapper.add(rsp,40);wrapper.ret();
        Copy(page,0,PermanentNativeSwitch.Assemble(wrapper,b));
        Copy(page,128,PermanentNativeSwitch.BuildGuard(b+128,b+3000,b+512,b+768));
        for(int mode=0;mode<2;mode++)
        {
            var block=new Assembler(64);block.mov(eax,mode);block.sete(__byte_ptr[rcx]);
            block.mov(__qword_ptr[rcx+16],rsp);block.mov(__qword_ptr[rcx+24],rcx);
            block.mov(__qword_ptr[rcx+32],r10);block.mov(__qword_ptr[rcx+40],r11);block.movq(__qword_ptr[rcx+48],xmm0);block.ret();
            Copy(page,mode==0?512:768,PermanentNativeSwitch.Assemble(block,b+(uint)(mode==0?512:768)));
        }
        var run=(GuardProbe)Marshal.GetDelegateForFunctionPointer(page,typeof(GuardProbe));Roots.Add(run);
        IntPtr output=Marshal.AllocHGlobal(64);Roots.Add(output);
        foreach(byte flag in new byte[]{0,1,0,2,255})foreach(int compare in new[]{0,1})
        {
            Marshal.WriteByte(demand,flag);Check(run(output,compare)==(flag==0?0:1),"production guard selects exactly one branch");
            Check(Marshal.ReadByte(output)==(compare==0?1:0),"incoming flags survive");
            Check(Marshal.ReadInt64(output,16)==Marshal.ReadInt64(output,8)-8,"guard stack balanced");
            Check(Marshal.ReadInt64(output,24)==output.ToInt64(),"RCX restored");
            Check((ulong)Marshal.ReadInt64(output,32)==0x123456789ABCDEF0UL && (ulong)Marshal.ReadInt64(output,40)==0x0FEDCBA987654321UL,"volatile GPR preserved");
            Check(Marshal.ReadInt64(output,48)==Marshal.ReadInt64(output,32),"XMM preserved");
        }
    }
    private static void NoDogs(byte[] image)
    {
        IntPtr arena=Image(image),manager=Marshal.AllocHGlobal(4096);Roots.Add(manager);
        for(int i=0;i<2048;i++)Marshal.WriteInt16(manager,i*2,unchecked((short)((i*17)%97-20)));
        var run=(Mapper)Marshal.GetDelegateForFunctionPointer(arena+0xffc70,typeof(Mapper));Roots.Add(run);
        ulong b=(ulong)arena.ToInt64();var inputs=new List<Tuple<int,int,int,int,int,uint>>();
        foreach(int context in new[]{0,1,2,3,4,5})foreach(int mode in new[]{0,1,2,3,4,99})
        foreach(int dogs in new[]{0,1})foreach(int restricted in new[]{0,1})foreach(int population in new[]{0,1,2,3,4})
        for(uint mapper=0;mapper<0x1c0;mapper++)inputs.Add(Tuple.Create(context,mode,dogs,restricted,population,mapper));
        uint[] original=new uint[inputs.Count],active=new uint[inputs.Count];
        PermanentNativeSwitch installed=null;byte[] published=null;
        for(int pass=0;pass<5;pass++)
        {
            if(pass==1)Copy(arena,NoDogsNativeContract.PatchRva,NoDogsNativeContract.ReplacementBytes);
            if(pass==2)
            {
                // Both preceding variants used an unpublished private fixture. Publication starts here.
                Copy(arena,NoDogsNativeContract.PatchRva,NoDogsNativeContract.ExpectedBytes);
                installed=new PermanentNativeSwitch(null,b,NativeSwitchCatalog.CreatePlans(image,b,true));Roots.Add(installed);
                published=Read(arena,0xffd94,6);
            }
            if(pass>=2)installed.SetEnabled(pass==3);
            for(int i=0;i<inputs.Count;i++)
            {
                var p=inputs[i];Marshal.WriteInt32(arena,0x3665f28,p.Item1);Marshal.WriteInt32(arena,0x8574b90,p.Item2);
                Marshal.WriteInt32(arena,0x37ef964,p.Item3);Marshal.WriteInt32(arena,0x36681e0,p.Item4);Marshal.WriteInt32(arena,0x905cf8,25+p.Item5);
                Marshal.WriteInt32(arena,0x87ee2f0,p.Item4);Marshal.WriteInt32(arena,0x87ee2f4,p.Item4);
                Marshal.WriteInt32(arena,0x3669040,p.Item4);Marshal.WriteInt32(arena,0x3669048,p.Item5);
                uint value=run(manager,p.Item6);
                if(pass==0)original[i]=value;else if(pass==1)active[i]=value;
                else Check(value==(pass==3?active[i]:original[i]),"complete production NoDogs function equivalence: pass="+pass+",input="+i);
            }
            if(pass>=2)Check(Read(arena,0xffd94,6).SequenceEqual(published),"settings transitions never repatch native code");
        }
        Check(original.Where((v,i)=>v!=active[i]).Count()==40,"reference reaches intended mode99 differences");
        Console.WriteLine("PASS: full NoDogs function, "+inputs.Count+" inputs, original/old active/production off/on/off; executable bytes unchanged after publication.");
    }
    private static void Peace(byte[] image)
    {
        IntPtr arena=Image(image);ulong b=(ulong)arena.ToInt64();
        var plans=NativeSwitchCatalog.CreatePlans(image,b,false);
        Check(plans.Length==24 && plans.Count(p=>p.EntryKind!=0)==2,"complete grouped peace plan");
        // Reject any changed byte copied by the larger blocks, not only the old patch bytes.
        foreach(var plan in plans)
        {
            for(int n=0;n<Math.Max(plan.Original.Length,plan.PhysicalLength+plan.Following.Length);n++)
            {
                image[plan.Rva+n]^=1;
                bool rejected=false;try {NativeSwitchCatalog.CreatePlans(image,b,false);}catch(InvalidOperationException){rejected=true;}
                finally {image[plan.Rva+n]^=1;}
                Check(rejected,"every changed grouped/following byte rejected");
            }
        }
        var installed=new PermanentNativeSwitch(null,b,plans);Roots.Add(installed);
        byte[][] before=plans.Select(p=>Read(arena,p.Rva,p.PhysicalLength)).ToArray();
        foreach(bool value in new[]{false,true,false,true})
        {
            installed.SetEnabled(value);
            for(int i=0;i<plans.Length;i++)Check(Read(arena,plans[i].Rva,plans[i].PhysicalLength).SequenceEqual(before[i]),"peace switch updates only demand data");
        }
        foreach(var plan in plans)
        {
            Check(Read(arena,plan.Rva,2).SequenceEqual(new byte[]{0xff,0x25}),"actual installed six-byte dispatch");
            foreach(bool enabled in new[]{false,true}) VerifyRelocation(plan,b,b+0x20000000+(uint)plan.Rva,enabled);
            byte[] inactive=PermanentNativeSwitch.BuildBlock(plan,b,b+0x20000000+(uint)plan.Rva,false);
            var d=Decoder.Create(64,new ByteArrayCodeReader(inactive));d.IP=b+0x20000000+(uint)plan.Rva;
            ulong end=d.IP+(uint)inactive.Length;
            Instruction last=default;while(d.IP<end){last=d.Decode();Check(!last.IsInvalid,"dormant block completely decoded");}
            Check(last.Mnemonic==Mnemonic.Jmp && last.NearBranchTarget==b+(uint)plan.Rva+(uint)plan.PhysicalLength,"dormant block returns to original continuation");
        }
        Console.WriteLine("PASS: real 24-group peace installer, unchanged executable code across toggles, rejection and original continuations.");
    }
    private static List<Instruction> Decode(byte[] bytes,ulong ip)
    {
        var d=Decoder.Create(64,new ByteArrayCodeReader(bytes));d.IP=ip;
        var result=new List<Instruction>();ulong end=ip+(uint)bytes.Length;
        while(d.IP<end){var i=d.Decode();Check(!i.IsInvalid && d.IP<=end,"complete relocated instruction");result.Add(i);}
        return result;
    }
    private static void VerifyRelocation(NativeSwitchPlan plan,ulong imageBase,ulong ip,bool enabled)
    {
        byte[] source=enabled?plan.Active:plan.Original.Take(plan.PhysicalLength).ToArray();
        var original=Decode(source,imageBase+(uint)plan.Rva);
        var actual=Decode(PermanentNativeSwitch.BuildBlock(plan,imageBase,ip,enabled),ip);
        // Entry prefixes are tested independently by the native peace suite; compare the
        // relocated original body after that prefix and before the explicit continuation.
        int offset=actual.Count-original.Count-1;
        Check(offset>=0 && (enabled && plan.EntryKind!=0 || offset==0),"only audited entry guards add prefix instructions");
        for(int n=0;n<original.Count;n++)
        {
            Instruction x=original[n],y=actual[offset+n];
            Check(x.Mnemonic==y.Mnemonic && x.OpCount==y.OpCount,"relocated operation preserved");
            for(int op=0;op<x.OpCount;op++)
            {
                Check(x.GetOpKind(op)==y.GetOpKind(op),"relocated operand kind");
                OpKind kind=x.GetOpKind(op);
                if(kind==OpKind.Register)Check(x.GetOpRegister(op)==y.GetOpRegister(op),"relocated register");
                else if(kind==OpKind.NearBranch64)
                {
                    ulong target=x.NearBranchTarget,start=imageBase+(uint)plan.Rva;
                    int inside=original.FindIndex(i=>i.IP==target);
                    ulong expected=target>=start && target<start+(uint)source.Length && inside>=0?actual[offset+inside].IP:target;
                    Check(y.NearBranchTarget==expected,"relocated branch destination");
                }
                else if(kind==OpKind.Memory)
                {
                    Check(x.MemoryBase==y.MemoryBase && x.MemoryIndex==y.MemoryIndex && x.MemoryIndexScale==y.MemoryIndexScale,"relocated memory addressing");
                    Check(x.IsIPRelativeMemoryOperand?x.IPRelativeMemoryAddress==y.IPRelativeMemoryAddress:x.MemoryDisplacement64==y.MemoryDisplacement64,"relocated memory target");
                }
                else Check(x.GetImmediate(op)==y.GetImmediate(op),"relocated immediate");
            }
        }
        Instruction last=actual.Last();
        Check(last.Mnemonic==Mnemonic.Jmp && last.NearBranchTarget==imageBase+(uint)plan.Rva+(uint)source.Length,"exact continuation after complete relocated body");
    }
}
namespace Shared
{
    internal static class DebugLogHelper
    {
        // External logging boundary; production switch emission/installation is source-linked.
        internal static void LogInfo(BepInEx.Logging.ManualLogSource log,string message) { }
    }
}
