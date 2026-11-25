using System;
using SHCDESE.Interop;

namespace TrappedUnitRescueTest.Tests
{
    internal static class CommandJourneyFixture
    {
        private static int checks;
        private static void Check(bool value,string reason){checks++;if(!value)throw new InvalidOperationException(reason);}
        private sealed class Memory
        {
            internal readonly byte[] Data=new byte[0xAEC];
            internal ushort Word(int at)=>(ushort)(Data[at]|Data[at+1]<<8);
            internal uint Dword(int at)=>(uint)(Word(at)|(uint)Word(at+2)<<16);
            internal void Word(int at,uint value){Data[at]=(byte)value;Data[at+1]=(byte)(value>>8);}
            internal void Dword(int at,uint value){Word(at,value);Word(at+2,value>>16);}
            internal Memory(eChimps type,int state,uint global=42)
            {
                Word(0x6E4,2);Word(0x6E6,(uint)type);Word(0x6EE,2);Dword(0x6F0,global);Word(0x918,(uint)state);
                Word(0x930,2);Dword(0x93C,33);Word(0x956,3);Word(0xA9E,1);
                int count=CommandJourneySnapshot.RequiredCrewCount(type);Word(0xA0C,(uint)count);
                for(int i=0;i<count;i++){Word(0x96C+i*2,(uint)(100+i));Dword(0x974+i*4,0x10000u+(uint)i);}
            }
        }
        internal static void Run()
        {
            checks=0;
            foreach(eChimps type in Enum.GetValues(typeof(eChimps)))
            {
                for(int state=0;state<=101;state++)
                {
                    var memory=new Memory(type,state);
                    bool expected=CommandResumePolicy.IsKnownType(type) && type!=eChimps.CHIMP_TYPE_TREBUCHET &&
                        (state==101 || CommandJourneySnapshot.RequiredCrewCount(type)>0 && (state==0 || state==1));
                    bool captured=CommandJourneySnapshot.TryCapture(type,state,memory.Word,memory.Dword,out var snapshot);
                    Check(captured==expected,"explicit type and functional phase matrix");
                    if(!captured)continue;
                    Check(snapshot.Matches(type,state,memory.Word,memory.Dword),"unchanged command matches");
                    for(int field=0;field<snapshot.FieldCount;field++)
                    {
                        var descriptor=snapshot.Field(field);byte original=memory.Data[descriptor.NativeOffset];memory.Data[descriptor.NativeOffset]^=1;
                        Check(!snapshot.Matches(type,state,memory.Word,memory.Dword),"every captured command and crew field is exact");
                        memory.Data[descriptor.NativeOffset]=original;
                    }
                    memory.Word(0x74E,5);memory.Word(0x750,8);memory.Word(0x9C8,1);memory.Word(0x9BA,44);
                    memory.Dword(0x908,123);memory.Dword(0x9DC,999);memory.Word(0x9A2,99);
                    Check(snapshot.Matches(type,state,memory.Word,memory.Dword),"route, ammo, timers and movement do not become command identity");
                    var values=snapshot.CopyValues();var offsets=new int[values.Length];var widths=new int[values.Length];
                    for(int i=0;i<values.Length;i++){offsets[i]=snapshot.Field(i).NativeOffset;widths[i]=snapshot.Field(i).Width;}
                    Check(CommandJourneySnapshot.TryRestoreKnownSnapshot(type,state,offsets,widths,values,out var restored) &&
                        restored.Matches(type,state,memory.Word,memory.Dword),"strict command field schema restores");
                    values[0]=0;Check(restored.TryGetValue(0x6E4,out uint alive) && alive==2,"restored command values immutable");
                    widths[0]=4;Check(!CommandJourneySnapshot.TryRestoreKnownSnapshot(type,state,offsets,widths,values,out _),"mutated command schema fails");
                }
            }
            var engineer=new Memory(eChimps.CHIMP_TYPE_ENGINEER,101);engineer.Word(0x9E0,1);
            Check(!CommandJourneySnapshot.TryCapture(eChimps.CHIMP_TYPE_ENGINEER,101,engineer.Word,engineer.Dword,out _),"engineer job payload requires its native owner");
            var dig=new Memory(eChimps.CHIMP_TYPE_SPEARMAN,101);dig.Word(0x956,6);
            Check(!CommandJourneySnapshot.TryCapture(eChimps.CHIMP_TYPE_SPEARMAN,101,dig.Word,dig.Dword,out _),"dig intent is not a plain move");
            var siege=new Memory(eChimps.CHIMP_TYPE_CATAPULT,101);siege.Word(0xA0C,1);
            Check(!CommandJourneySnapshot.TryCapture(eChimps.CHIMP_TYPE_CATAPULT,101,siege.Word,siege.Dword,out _),"missing crew cannot be adopted");
            siege.Word(0xA0C,2);siege.Word(0x96E,100);
            Check(!CommandJourneySnapshot.TryCapture(eChimps.CHIMP_TYPE_CATAPULT,101,siege.Word,siege.Dword,out _),"duplicate crew cannot be adopted");
            var actor=new Memory(eChimps.CHIMP_TYPE_ARCHER,101,42);
            Check(CommandJourneySnapshot.TryCapture(eChimps.CHIMP_TYPE_ARCHER,101,actor.Word,actor.Dword,out var command),"command ticket fixture");
            var ticket=WorkerResumeTicket.ForCommand(7,42,eChimps.CHIMP_TYPE_ARCHER,101,2,2,33,20,30,command);
            Check(ticket.IsValid && ticket.Kind==ResumeReceiptKind.Command && ticket.Job==null && ticket.Bindings.IsEmpty,"command receipt contains no invented worker job");
            var ledger=new WorkerResumeLedger();Check(ledger.TryReserve(ticket,out var token) && ledger.CommitPark(token,0),"reserve command before mutation");
            Check(ledger.TrySerialize(out var encoded),"serialize explicit command kind");
            var loaded=new WorkerResumeLedger();Check(loaded.TryRestore(encoded,100) && loaded.TryGetParkedByUnit(7,out var restoredTicket) &&
                restoredTicket.Kind==ResumeReceiptKind.Command && restoredTicket.Job==null && restoredTicket.Command.Matches(eChimps.CHIMP_TYPE_ARCHER,101,actor.Word,actor.Dword),"command codec roundtrip");
            var unknown=(byte[])encoded.Clone();unknown[45]=255;Check(!loaded.TryRestore(unknown,100) && loaded.Count==1,"unknown kind cannot replace existing ownership");
            var masquerade=(byte[])encoded.Clone();masquerade[45]=(byte)ResumeReceiptKind.Worker;
            Check(!loaded.TryRestore(masquerade,100),"command cannot masquerade as worker schema");
            var wideType=(byte[])encoded.Clone();wideType[20]=1;
            Check(!loaded.TryRestore(wideType,100),"wide serialized enum cannot truncate into an admitted type");
            var invalidId=(byte[])encoded.Clone();Array.Copy(BitConverter.GetBytes(NativeRelocation.MaximumUnitId+1),0,invalidId,10,4);
            Check(!loaded.TryRestore(invalidId,100),"out-of-range native unit identity rejected before adoption");
            Console.WriteLine("PASS: command journey "+checks+" checks; explicit phases, crew, intent schemas and command-kind codec; no game execution.");
        }
    }
}
