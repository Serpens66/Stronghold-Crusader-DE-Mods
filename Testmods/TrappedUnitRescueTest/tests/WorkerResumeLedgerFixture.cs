using System;
using SHCDESE.Interop;

namespace TrappedUnitRescueTest.Tests
{
    internal static class WorkerResumeLedgerFixture
    {
        private static int checks;
        private static void Check(bool value,string message){checks++;if(!value)throw new InvalidOperationException(message);}
        private static WorkerResumeTicket Ticket(int id)
        {
            WorkerTravelPolicy.TryDescribe(eChimps.CHIMP_TYPE_QUARRY_OX,1,out var rule);
            Func<int,ushort> word=offset=>offset==0x6E6?(ushort)eChimps.CHIMP_TYPE_QUARRY_OX:offset==0x918?(ushort)1:offset==0x6EE?(ushort)2:(ushort)0;
            Func<int,uint> dword=offset=>offset==0x6F0?(uint)id:0;
            Check(WorkerJourneySnapshot.TryCapture(rule,word,dword,out var job),"fixture snapshot");
            return new WorkerResumeTicket(id,(uint)id,rule.UnitType,1,2,2,33,123,456,job);
        }
        internal static void Run()
        {
            checks=0;var ledger=new WorkerResumeLedger();
            var ticket=Ticket(1);
            Check(ledger.TryReserve(ticket,out var token),"reserve before native mutation");
            Check(!ledger.TryReserve(ticket,out _),"duplicate unit forbidden");
            Check(!ledger.TrySerialize(out _),"transient reservation cannot save");
            Check(ledger.CommitPark(token,10),"commit failed resume");
            Check(!ledger.CommitPark(token,10),"commit once");
            int called=0;
            Check(ledger.Drain(9,512,4,t=>{called++;return WorkerResumeDecision.Retry;})==0 && called==0,"deadline");
            Check(ledger.Drain(10,512,1,t=>{called++;return WorkerResumeDecision.Retry;})==1,"attempt budget");
            Check(ledger.Drain(11,512,1,t=>{called++;return WorkerResumeDecision.Retry;})==0,"backoff");
            for(int i=0;i<8;i++)ledger.Drain(1000+i*1000,512,1,t=>{called++;return WorkerResumeDecision.Retry;});
            Check(called==6 && ledger.Count==1,"bounded retries retain dormant receipt");
            Check(ledger.Drain(20000,512,10,t=>{throw new Exception();})==0,"dormant constant time");
            ledger.MarkWorldMutation();
            Check(ledger.Drain(20000,512,1,t=>WorkerResumeDecision.WaitForGeometry)==1,"geometry rearm");
            ledger.Drain(20001,512,1,t=>{called++;return WorkerResumeDecision.Release;});
            Check(called==6 && ledger.Count==1,"same geometry signal consumed once");
            ledger.MarkWorldMutation();
            Check(ledger.Drain(20002,512,1,t=>WorkerResumeDecision.Release)==1 && ledger.Count==0,"explicit terminal ownership ack");
            Check(!ledger.Release(token),"released token stale");

            ledger.TryReserve(Ticket(2),out var resetToken);ledger.Reset();ledger.TryReserve(Ticket(3),out var newToken);
            Check(!ledger.Release(resetToken) && ledger.Count==1,"reset generation rejects stale token");
            ledger.CommitPark(newToken,0);
            Check(ledger.Drain(0,512,1,t=>{throw new InvalidOperationException();})==1 && ledger.Count==1,"exception retains ownership");
            ledger.MarkWorldMutation();
            Check(ledger.Drain(0,512,1,t=>{Check(!ledger.Release(newToken),"callback cannot mutate receipts");return WorkerResumeDecision.Invalidated;})==1 && ledger.Count==0,"new order ack");

            for(int i=1;i<=WorkerResumeLedger.Capacity;i++)
            {Check(ledger.TryReserve(Ticket(i),out var entry),"bounded capacity");Check(ledger.CommitPark(entry,0),"park capacity");}
            Check(!ledger.TryReserve(Ticket(999),out _) && ledger.Count==512,"capacity fails before mutation");
            called=0;
            Check(ledger.Drain(0,7,2,t=>{called++;return WorkerResumeDecision.Release;})==2 && called==2,"native cap");
            Check(ledger.Drain(0,1,20,t=>{called++;return WorkerResumeDecision.Release;})==1 && called==3,"visit cap");
            Check(ledger.TrySerialize(out var bytes),"serialize parked");
            var roundtrip=new WorkerResumeLedger();
            Check(roundtrip.TryRestore(bytes,100) && roundtrip.Count==509,"roundtrip all remaining");
            Check(roundtrip.Drain(99,512,2,t=>WorkerResumeDecision.Release)==0,"restore world tick rebound");
            Check(roundtrip.Drain(100,512,2,t=>{Check(t.RequestedX==123 && t.RequestedY==456 && t.TribeGlobalId==33,"goal and identity");return WorkerResumeDecision.Release;})==2,"restore retry");
            // Every truncation is malformed, never partially replaces existing ownership.
            var single=new WorkerResumeLedger();single.TryReserve(Ticket(700),out var singleToken);single.CommitPark(singleToken,0);
            single.TrySerialize(out var one);
            for(int length=0;length<one.Length;length++)
            {var cut=new byte[length];Array.Copy(one,cut,length);Check(!single.TryRestore(cut,0) && single.Count==1,"truncated atomic parse");}
            Mutate(single,one,0,0,"signature");Mutate(single,one,4,255,"version");Mutate(single,one,6,255,"count");
            Mutate(single,one,18,255,"unit type");Mutate(single,one,22,255,"state");
            Mutate(single,one,45,255,"field count");Mutate(single,one,49,255,"field offset");Mutate(single,one,53,255,"field width");
            var extra=new byte[one.Length+1];Array.Copy(one,extra,one.Length);Check(!single.TryRestore(extra,0),"trailing data");
            var two=new byte[10+(one.Length-10)*2];Array.Copy(one,two,one.Length);two[6]=2;
            Array.Copy(one,10,two,one.Length,one.Length-10);Check(!single.TryRestore(two,0),"duplicate ids");
            Check(single.TrySerialize(out var unchanged) && Equal(one,unchanged),"failures preserve exact data");
            Check(single.Drain(0,512,1,t=>{Check(!single.TrySerialize(out _),"save blocked during recovery");return WorkerResumeDecision.WaitForGeometry;})==1,"recovery save gate");
            Check(single.TrySerialize(out var dormant),"dormant save");
            var sleeping=new WorkerResumeLedger();Check(sleeping.TryRestore(dormant,42),"dormant restore");
            Check(sleeping.Drain(999,512,2,t=>WorkerResumeDecision.Release)==0 && sleeping.Count==1,"save never expires hold");
            sleeping.MarkWorldMutation();Check(sleeping.Drain(999,512,2,t=>WorkerResumeDecision.Release)==1,"dormant restore rearm");
            Console.WriteLine("PASS: worker resume ledger "+checks+" checks; bounded ownership, retries, generation and strict binary records; no game execution.");
        }
        private static void Mutate(WorkerResumeLedger ledger,byte[] source,int index,byte value,string name)
        {var copy=(byte[])source.Clone();copy[index]=value;Check(!ledger.TryRestore(copy,0) && ledger.Count==1,name);}
        private static bool Equal(byte[] a,byte[] b){if(a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true;}
    }
}
