using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Iced.Intel;
using static Iced.Intel.AssemblerRegisters;

namespace TrappedUnitRescueTest.Tests
{
    internal static class MountedKeepContractFixture
    {
        private const ulong Image=0x180000000,Gate=0x123456780,Corrected=Image+0x187110,Original=Image+0xE2610;
        private static int checks;
        private static void Check(bool value,string reason){checks++;if(!value)throw new Exception(reason);}
        internal static int Run(string actualMainAssembly)
        {
            var assembly=Assembly.LoadFile(Path.GetFullPath(actualMainAssembly));
            Check(string.Equals(Path.GetFullPath(assembly.Location),Path.GetFullPath(actualMainAssembly),StringComparison.OrdinalIgnoreCase),
                "Decoder regression loaded a different main package");
            using(var module=Mono.Cecil.ModuleDefinition.ReadModule(actualMainAssembly))
                Check(module.Mvid==assembly.ManifestModule.ModuleVersionId,"Selected/loaded main module identity differs");
            var method=assembly.GetType("BugfixesAndQoL.TrappedUnitRescue.MountedScannerCapture",true)
                .GetMethod("EmitConditionalCall",BindingFlags.NonPublic|BindingFlags.Static);
            var emit=(Action<Assembler,ulong,ulong,ulong>)Delegate.CreateDelegate(typeof(Action<Assembler,ulong,ulong,ulong>),method);
            var code=Build(emit,false);Check(Valid(code),"Actual production gated emitter was rejected");
            int on=code.FindIndex(i=>i.Mnemonic==Mnemonic.Call),off=code.FindLastIndex(i=>i.Mnemonic==Mnemonic.Call),start=on-7;
            Check(!MountedKeepCallContract.Validate(code,Corrected,Gate+4,Original,false,out _),"Foreign gate cell accepted");
            Check(!MountedKeepCallContract.Validate(code,Corrected,Gate,Original+1,false,out _),"Arbitrary original OFF target accepted");
            Change(code,start+5,i=>{i.Code=Code.Jne_rel8_64;return i;},"Inverted ON/OFF branch accepted");
            Change(code,start+5,i=>{i.NearBranch64=code[on].IP;return i;},"OFF branch can bypass flags restore/enter ON call");
            Change(code,start+1,i=>{i.Op0Register=Register.RCX;return i;},"Missing RAX save accepted");
            Change(code,on-1,i=>{i.Code=Code.Nopd;return i;},"Missing ON flags restoration accepted");
            Change(code,off-1,i=>{i.Code=Code.Nopd;return i;},"Missing OFF flags restoration accepted");
            Change(code,on,i=>{i.NearBranch64=Original;return i;},"ON branch wrong target accepted");
            Change(code,on+1,i=>{i.NearBranch64=code[off].IP;return i;},"ON branch can fall through second CALL");
            Change(code,off+2,i=>{i.Immediate8=0x20;return i;},"Wrong shadow-space restore accepted");
            Change(code,off+3,i=>{i.Op0Register=Register.RAX;return i;},"Wrong original register restoration accepted");
            var extra=new List<Instruction>(code);extra.Add(Instruction.CreateBranch(Code.Call_rel32_64,Original));Check(!Valid(extra),"Third unrelated native CALL accepted");
            Change(code,5,i=>{i.Code=Code.Jmp_rel8_64;i.Op0Kind=OpKind.NearBranch64;i.NearBranch64=code[on].IP;return i;},"Foreign branch into dispatch accepted");
            var scalar=Build(emit,true);
            Check(MountedKeepCallContract.Validate(scalar,Corrected,0,0,true,out _),"Qualified AlreadyFixed scalar CALL rejected");
            Check(!MountedKeepCallContract.Validate(scalar,Corrected,Gate,Original,false,out _),"Single CALL accepted without qualified AlreadyFixed plan");
            Console.WriteLine("PASS mounted published-call decoder checks="+checks+"; actual production gate emitter; no native execution/game writes");return checks;
        }
        private static bool Valid(IReadOnlyList<Instruction> code)=>MountedKeepCallContract.Validate(code,Corrected,Gate,Original,false,out _);
        private static void Change(List<Instruction> original,int index,Func<Instruction,Instruction> mutate,string reason)
        {var copy=new List<Instruction>(original);copy[index]=mutate(copy[index]);Check(!Valid(copy),reason);}
        private static List<Instruction> Build(Action<Assembler,ulong,ulong,ulong> emit,bool scalar)
        {
            var a=new Assembler(64);a.pushfq();a.push(rax);a.push(rcx);a.push(rdx);a.push(r8);a.nop();
            if(scalar)a.call(Corrected);else emit(a,Gate,Corrected,Original);
            a.add(rsp,0x28);foreach(var r in new[]{r11,r10,r9,r12,rdi,rsi,rbx,r8,rdx,rcx,rax})a.pop(r);a.popfq();a.ret();
            using(var stream=new MemoryStream())
            {
                a.Assemble(new StreamCodeWriter(stream),Image);var decoder=Decoder.Create(64,new ByteArrayCodeReader(stream.ToArray()));decoder.IP=Image;
                var code=new List<Instruction>();while(decoder.IP<Image+(ulong)stream.Length)code.Add(decoder.Decode());return code;
            }
        }
    }
}
