using System;
using System.Collections.Generic;
using System.Linq;
using Iced.Intel;

namespace TrappedUnitRescueTest
{
    // Acceptance-only decoder for the actual published stub. No native calls or writes.
    internal static class MountedKeepCallContract
    {
        internal static bool Validate(IReadOnlyList<Instruction> code,ulong corrected,ulong gate,ulong original,bool scalarFixed,out string reason)
        {
            reason=null;
            if(code==null || code.Count<5 || code[0].Mnemonic!=Mnemonic.Pushfq ||
                !RegisterOp(code[1],Mnemonic.Push,Register.RAX) || !RegisterOp(code[2],Mnemonic.Push,Register.RCX) ||
                !RegisterOp(code[3],Mnemonic.Push,Register.RDX) || !RegisterOp(code[4],Mnemonic.Push,Register.R8))
                return Fail("original-entry-register-save",out reason);
            var calls=new List<int>();
            for(int i=0;i<code.Count;i++)if(code[i].Mnemonic==Mnemonic.Call)
            {if(code[i].Op0Kind!=OpKind.NearBranch64)return Fail("unknown-indirect-call",out reason);calls.Add(i);}
            if(scalarFixed)
                return calls.Count==1 && code[calls[0]].NearBranchTarget==corrected && Restore(code,calls[0]+1) || Fail("scalar-fixed-call-or-restore",out reason);
            if(gate==0 || original==0 || calls.Count!=2)return Fail("gated-target-count-or-identity",out reason);
            int on=calls[0],off=calls[1],start=on-7;
            if(start<5 || off!=on+3 || off+1>=code.Count)return Fail("gated-call-layout",out reason);
            var load=code[start+2];var compare=code[start+3];
            if(code[start].Mnemonic!=Mnemonic.Pushfq || !RegisterOp(code[start+1],Mnemonic.Push,Register.RAX) ||
                load.Code!=Code.Mov_r64_imm64 || load.Op0Register!=Register.RAX || load.Immediate64!=gate ||
                compare.Code!=Code.Cmp_rm8_imm8 || compare.Op0Kind!=OpKind.Memory || compare.MemoryBase!=Register.RAX ||
                compare.MemoryIndex!=Register.None || compare.MemoryDisplacement64!=0 || compare.SegmentPrefix!=Register.None || compare.Immediate8!=0 ||
                !RegisterOp(code[start+4],Mnemonic.Pop,Register.RAX) || code[start+5].Mnemonic!=Mnemonic.Je ||
                code[start+5].NearBranchTarget!=code[off-1].IP || code[on-1].Mnemonic!=Mnemonic.Popfq ||
                code[on].NearBranchTarget!=corrected || code[on+1].Mnemonic!=Mnemonic.Jmp ||
                code[on+1].Op0Kind!=OpKind.NearBranch64 || code[on+1].NearBranchTarget!=code[off+1].IP ||
                code[off-1].Mnemonic!=Mnemonic.Popfq || code[off].NearBranchTarget!=original ||
                code[off+1].Mnemonic!=Mnemonic.Nop || code[off+1].Length!=1 || !Restore(code,off+2))
                return Fail("gated-branch-register-flags-or-target-contract",out reason);
            // No branch may bypass the dispatch's save/compare/restore sequence.
            ulong first=code[start].IP,last=code[off+1].IP;
            for(int i=0;i<code.Count;i++)
            {
                var instruction=code[i];
                if(i>=start && i<=off+1 && i!=start+5 && i!=on+1)continue;
                if(instruction.Op0Kind==OpKind.NearBranch64 && instruction.FlowControl!=FlowControl.Call &&
                    instruction.NearBranchTarget>=first && instruction.NearBranchTarget<=last && i!=start+5 && i!=on+1)
                    return Fail("foreign-entry-into-gated-dispatch",out reason);
            }
            for(int i=start;i<off+1;i++)if(code[i].NextIP!=code[i+1].IP)return Fail("noncontiguous-dispatch",out reason);
            return true;
        }
        private static bool RegisterOp(Instruction instruction,Mnemonic operation,Register register)=>
            instruction.Mnemonic==operation && instruction.Op0Kind==OpKind.Register && instruction.Op0Register==register;
        private static bool Restore(IReadOnlyList<Instruction> code,int start)
        {
            Register[] saved={Register.R11,Register.R10,Register.R9,Register.R12,Register.RDI,Register.RSI,Register.RBX,
                Register.R8,Register.RDX,Register.RCX,Register.RAX};
            if(start<0 || start+saved.Length+1>=code.Count)return false;
            var add=code[start++];
            if(add.Mnemonic!=Mnemonic.Add || add.Op0Kind!=OpKind.Register || add.Op0Register!=Register.RSP ||
                add.Op1Kind!=OpKind.Immediate8to64 || add.Immediate8to64!=0x28)return false;
            foreach(var register in saved)if(!RegisterOp(code[start++],Mnemonic.Pop,register))return false;
            return code[start].Mnemonic==Mnemonic.Popfq;
        }
        private static bool Fail(string value,out string reason){reason=value;return false;}
    }
}
