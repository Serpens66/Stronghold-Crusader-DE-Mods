using System;
using Iced.Intel;
using RedBird.X64.Extensions;
using static Iced.Intel.AssemblerRegisters;

namespace OutpostTest
{
    internal static class OutpostSelectionGate
    {
        internal const int Rva=0x8A1F9, Length=18, ReturnRva=0x8A20B, RejectRva=0x89FF2;
        internal static readonly byte[] Bytes={0x83,0x3D,0x28,0xBD,0x5D,0x03,0x01,0x0F,0x85,0xEC,0xFD,0xFF,0xFF,0xBB,0x2D,0,0,0};
        internal static void Generate(Assembler a,ReadOnlySpan<Instruction> original,ulong back,ulong flag,ulong reject)
        {
            if(original.Length!=3 || original[0].Length!=7 || original[1].Length!=6 || original[2].Length!=5 ||
                original[0].Mnemonic!=Mnemonic.Cmp || original[1].Mnemonic!=Mnemonic.Jne || original[2].Mnemonic!=Mnemonic.Mov ||
                original[1].NearBranchTarget!=reject || original[2].NextIP!=back || original[2].Op0Register!=Register.EBX || original[2].Immediate32!=45)
                throw new InvalidOperationException("Outpost selection instructions changed.");
            var vanilla=a.CreateLabel();
            a.pushfq();a.push(rax);a.mov(rax,flag);a.cmp(__dword_ptr[rax],0);a.je(vanilla);
            a.pop(rax);a.popfq();a.mov(ebx,45);a.AddUnrestrictedJmp(back);
            a.Label(ref vanilla);a.pop(rax);a.popfq();
            foreach(var instruction in original) a.AddInstruction(instruction);
            a.AddUnrestrictedJmp(back);
        }
        internal static void Validate(byte[] code,byte[][] tables,ulong image)
        {
            ulong start=image+Rva,end=start+Length;
            var decoder=Decoder.Create(64,new ByteArrayCodeReader(code),image+0x89F40);
            while(decoder.IP<image+0x8A75B) {
                var i=decoder.Decode();
                if(i.IsInvalid) throw new InvalidOperationException("Selection decode failed.");
                if((i.FlowControl==FlowControl.ConditionalBranch || i.FlowControl==FlowControl.UnconditionalBranch || i.FlowControl==FlowControl.Call) &&
                    i.NearBranchTarget>start && i.NearBranchTarget<end)
                    throw new InvalidOperationException("Branch enters selection hook interior.");
            }
            foreach(var table in tables) for(int i=0;i<table.Length;i+=4) {
                uint target=BitConverter.ToUInt32(table,i);
                if(target>Rva && target<ReturnRva) throw new InvalidOperationException("Jump table enters selection hook interior.");
            }
        }
    }
}