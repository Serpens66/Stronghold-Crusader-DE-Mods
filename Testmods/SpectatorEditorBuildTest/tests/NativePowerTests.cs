using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.IO;
using Iced.Intel;
using RedBird.X64.Hooks;
using static Iced.Intel.AssemblerRegisters;

namespace SpectatorEditorBuildTest
{
    internal static class NativePowerTests
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void RunCall();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void SelectCall(int power, int actor);
        private static readonly List<object> roots = new List<object>();
        private static int checks, answer, callbacks;
        private static void Expect(bool value, string name) { checks++; if (!value) throw new Exception(name); }
        private static int Query(int site) { callbacks++; return answer; }
        private static void Write(ulong address, byte[] bytes) => Marshal.Copy(bytes, 0, new IntPtr((long)address), bytes.Length);
        private static int Emit(ulong address, Action<Assembler> build) { var a = new Assembler(64); build(a); var bytes=NativePowerAssembly.Assemble(a, address);Write(address,bytes);return bytes.Length; }
        private static long Read(ulong address) => Marshal.ReadInt64(new IntPtr((long)address));
        private static int Main()
        {
            try
            {
                IntPtr memory = VirtualAlloc(IntPtr.Zero, (UIntPtr)0x8900000, 0x3000, 0x40);
                if (memory == IntPtr.Zero) throw new Exception("private native fixture allocation failed");
                ulong image = (ulong)memory.ToInt64(), results = image + 0x3000;
                byte[] file = File.ReadAllBytes(@"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll");
                int pe=BitConverter.ToInt32(file,60), table=pe+24+BitConverter.ToUInt16(file,pe+20);
                for(int i=0;i<BitConverter.ToUInt16(file,pe+6);i++)
                {
                    int row=table+i*40,start=BitConverter.ToInt32(file,row+12),size=BitConverter.ToInt32(file,row+16);
                    if(0x105510>=start && 0x105510+400<=start+size)
                    { var body=new byte[400];Array.Copy(file,BitConverter.ToInt32(file,row+20)+0x105510-start,body,0,400);Write(image+0x105510,body); }
                }
                Marshal.WriteInt32(new IntPtr((long)(image + 0x88E3D70)), 7);
                Emit(image + 0x23990, a => { a.mov(rax, results+64); a.inc(__qword_ptr[rax]); a.ret(); });
                var callback = new NativePowerContext.Query(Query); roots.Add(callback);
                var continuations = new Dictionary<ulong,byte[]>();
                for (int site=0; site<4; site++)
                {
                    int index=site; ulong address = image + (uint)NativePowerAssembly.Sites[site];
                    byte[] bytes = NativePowerAssembly.Bytes(site); Write(address,bytes);
                    var saved = new byte[160];Marshal.Copy(new IntPtr((long)(address+(uint)bytes.Length)),saved,0,saved.Length);
                    int changed = Emit(address+(uint)bytes.Length, a => {
                        a.mov(r11,results); a.mov(__qword_ptr[r11],rdx); a.mov(__qword_ptr[r11+8],rcx);
                        a.mov(__qword_ptr[r11+16],r15); a.mov(__qword_ptr[r11+24],r8); a.mov(__qword_ptr[r11+32],rax);
                        a.pushfq(); a.pop(rax); a.mov(__qword_ptr[r11+40],rax);
                        a.add(rsp,40); a.pop(r15); a.pop(r14); a.pop(r13); a.pop(r12);
                        a.pop(rdi); a.pop(rsi); a.pop(rbp); a.pop(rbx); a.ret();
                    });
                    var restore=new byte[changed];Array.Copy(saved,restore,changed);continuations[address+(uint)bytes.Length]=restore;
                    var hook = new X64InlineHook(address,bytes.Length); roots.Add(hook);
                    Expect(hook.DisplacedByteCount==bytes.Length,"backend exact span "+site);
                    hook.Generate((a,original,back) => NativePowerAssembly.Hook(a,original,back,index,(ulong)Marshal.GetFunctionPointerForDelegate(callback).ToInt64()));
                    hook.Enable();
                    Emit(image+0x1000+(uint)site*0x200,a => {
                        a.push(rbx);a.push(rbp);a.push(rsi);a.push(rdi);a.push(r12);a.push(r13);a.push(r14);a.push(r15);a.sub(rsp,40);
                        a.mov(rax,0x11223344);a.mov(edx,55);a.mov(ecx,66);a.mov(r15d,7);a.mov(r8d,99);a.stc();a.jmp(address);
                    });
                    var run = (RunCall)Marshal.GetDelegateForFunctionPointer(new IntPtr((long)(image+0x1000+(uint)site*0x200)),typeof(RunCall));
                    foreach (int selected in new[]{0,2,8})
                    {
                        answer=selected;callbacks=0;Marshal.WriteInt64(new IntPtr((long)(results+64)),0);run();
                        Expect(callbacks==1,"exactly one query "+site);
                        if(site==0) { Expect(Read(results)==(selected==0?7:selected),"selection actor");Expect(Read(results+8)==(selected==0?7:selected)*0x583C,"resource stride"); }
                        if(site==1) { Expect(Read(results+16)==(selected==0?7:selected),"target actor");Expect((Read(results+40)&1)==1,"flags preserved at actor site"); }
                        if(site>=2) Expect(Read(results+64)==(selected==0?1:0),"accepted target suppresses duplicate chore");
                        if(site==2) Expect(Read(results+24)==2,"native confirmation continuation r8");
                        if(site<2) Expect(Read(results+32)==0x11223344,"scratch RAX preserved");
                    }
                }
                // Restore the actual selection body after the isolated continuation checks; leave its two hooks installed.
                foreach(var saved in continuations) Write(saved.Key,saved.Value);
                Emit(image+0x5F20,a=>a.ret()); Emit(image+0x2940,a=>a.ret());
                Emit(image+0x4000,a=>NativePowerAssembly.SelectionGateway(a,image));
                var select=(SelectCall)Marshal.GetDelegateForFunctionPointer(new IntPtr((long)(image+0x4000)),typeof(SelectCall));
                foreach(int actor in new[]{2,8}) foreach(int power in new[]{0,1,2,3,4,5,6,7})
                {
                    answer=actor;callbacks=0;
                    Marshal.WriteInt32(new IntPtr((long)(image+0x379E7A4+(uint)actor*0x583C)),7000);
                    Marshal.WriteInt32(new IntPtr((long)(image+0x60AD56C)),0);
                    select(power,actor);
                    Expect(Marshal.ReadInt32(new IntPtr((long)(image+0x60AD56C)))==(power==5?0:5),"actual Vanilla selection mode");
                    if(power!=5) Expect(Marshal.ReadInt32(new IntPtr((long)(image+0x60AD574)))==power,"actual Vanilla selected power");
                    Expect(callbacks==(power==5?2:1),"actual Vanilla selection/Gold confirmation queries");
                    Expect(Marshal.ReadInt32(new IntPtr((long)(image+0x88E3D70)))==7,"global human identity unchanged");
                }
                Console.WriteLine("PASS: "+checks+" native power generator/backend checks (inactive, AI 2/8, registers, flags, exactly-once submission).");
                return 0;
            }
            catch(Exception e) { Console.Error.WriteLine(e);return 1; }
        }
        [DllImport("kernel32",SetLastError=true)] private static extern IntPtr VirtualAlloc(IntPtr address,UIntPtr size,uint allocation,uint protection);
    }
}
