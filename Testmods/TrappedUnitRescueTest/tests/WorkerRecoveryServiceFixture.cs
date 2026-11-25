using System;
using APIShared.Savegames;
using SHCDESE.Interop;

namespace TrappedUnitRescueTest.Tests
{
    internal static class WorkerRecoveryServiceFixture
    {
        private static int checks;
        private static void Check(bool value,string reason){checks++;if(!value)throw new InvalidOperationException(reason);}
        private sealed class World : IWorkerResumeWorld
        {
            internal long Session=10,Generation=1;
            internal bool Available=true;
            internal WorkerResumeTicket Ticket;
            internal Action DuringMove;
            internal int Moves;
            internal bool Read(out long session,out long generation){session=Session;generation=Generation;return Available;}
            public bool TryCapture(UnitSnapshot before,out WorkerResumeTicket ticket,out string reason){ticket=Ticket;reason=null;return ticket!=null;}
            public WorkerReceiptState Inspect(WorkerResumeTicket ticket,bool continuation)=>WorkerReceiptState.MatchingParked;
            public bool TryPark(WorkerResumeTicket ticket,bool continuation)=>true;
            public bool Reissue(WorkerResumeTicket ticket){Moves++;DuringMove?.Invoke();return false;}
            public bool CanProbeOriginal(WorkerResumeTicket ticket)=>false;
            public bool ProbeOriginal(WorkerResumeTicket ticket)=>throw new InvalidOperationException("Unexpected native probe.");
            public bool IsSuccessfulPublication(WorkerResumeTicket ticket)=>false;
            public WorkerOrderObservation ObserveOrderPost(WorkerResumeTicket ticket,long nativeResult,out WorkerResumeTicket replacement)
            {replacement=null;return WorkerOrderObservation.Keep;}
        }
        private static WorkerResumeTicket Ticket(int id)
        {
            WorkerTravelPolicy.TryDescribe(eChimps.CHIMP_TYPE_QUARRY_OX,1,out var rule);
            WorkerJourneySnapshot.TryCapture(rule,offset=>offset==0x6E6?(ushort)rule.UnitType:
                offset==0x918 || offset==0x990 || offset==0x98E?(ushort)1:offset==0x6EE?(ushort)2:(ushort)0,
                offset=>offset==0x6F0?(uint)id:0,out var job);
            return new WorkerResumeTicket(id,(uint)id,rule.UnitType,1,2,0,0,20,30,job,
                new WorkerBindingSnapshot(rule.RequiredExternalBindings,1,0,1,0,0,0));
        }
        internal static void Run()
        {
            checks=0;var world=new World{Ticket=Ticket(1)};var supervisor=new WorkerResumeSupervisor(world);
            bool recovering=false;
            var service=new WorkerRecoveryService(supervisor,()=>recovering,_=>{},world.Read);
            Check(!service.Ready && !service.AdmitTick(0),"no session cannot execute");
            service.BeginSession(10,true);Check(service.Ready && service.AdmitTick(int.MaxValue-1),"bind ready session clock");
            Check(service.AdmitTick(int.MinValue+1) && service.TickValue==3,"native counter wraps monotonically");
            service.BeginSession(10,true);service.AdmitTick(0);
            Check(service.AdmitTick(20) && service.TickValue==20,"forward clock");
            Check(!service.AdmitTick(19) && service.TickValue==20,"backward tick cannot age receipts");
            Check(service.AdmitTick(21) && service.TickValue==21,"rejected backward sample does not poison clock");
            Check(supervisor.Ledger.TryReserve(world.Ticket,out var reserved),"reserve ownership");
            Check(!service.TryCaptureOwnedRecord(out var blocked) && blocked==null,"save refuses transient receipt");
            supervisor.Ledger.CommitPark(reserved,21);
            Check(service.TryCaptureOwnedRecord(out var bytes),"save captures owned park");
            recovering=true;Check(!service.TryCaptureOwnedRecord(out _),"save refuses general native recovery");recovering=false;
            service.EndSession();Check(!service.Ready && supervisor.Ledger.Count==0 && service.TickValue==0,"end retires old world");
            service.StageOwnedRecord(VersionedSaveLoadStatus.Restored,bytes);
            var original=(byte[])bytes.Clone();bytes[0]^=1;
            Check(supervisor.Ledger.Count==0,"load stage never applies before start");
            service.BeginSession(10,true);
            Check(service.Ready && supervisor.Ledger.Count==1,"load clone applies to fresh session");
            Check(service.TryCaptureOwnedRecord(out var restored) && Equal(original,restored),"fresh save preserves parked metadata exactly");
            Check(service.AdmitTick(100) && service.TickValue==0,"restored deadline clock rebinds");
            world.Session=11;Check(!service.AdmitTick(101) && supervisor.Ledger.Count==1,"foreign session cannot discard owned receipt");world.Session=10;
            world.Available=false;Check(!service.AdmitTick(101) && supervisor.Ledger.Count==1,"missing world is no reset signal");world.Available=true;
            world.Generation++;
            Check(service.AdmitTick(101) && service.TickValue==0 && supervisor.Ledger.Count==0,"new world generation drops only old world receipts");
            service.EndSession();service.StageOwnedRecord(VersionedSaveLoadStatus.Restored,original);service.BeginSession(10,false);
            Check(!service.Ready && supervisor.Ledger.Count==1 && world.Moves==0,"inadmissible mode keeps ownership without executing");
            Check(service.TryCaptureOwnedRecord(out var inactive) && Equal(original,inactive),"inactive ownership persists without setting mutation");
            service.EndSession();service.StageOwnedRecord(VersionedSaveLoadStatus.Restored,null);service.BeginSession(10,true);
            Check(!service.Ready && !service.TryCaptureOwnedRecord(out _),"missing required restored entry fails closed");
            Check(service.AdmitTick(0),"unusable worker record does not block independent unit queue clock");
            service.EndSession();service.StageOwnedRecord(VersionedSaveLoadStatus.Restored,new byte[]{1,2,3});service.BeginSession(10,true);
            Check(!service.Ready && !service.TryCaptureOwnedRecord(out _),"invalid binary restore cannot become a fresh empty record");
            service.EndSession();service.StageOwnedRecord(VersionedSaveLoadStatus.TooNew,null);service.BeginSession(10,true);
            Check(!service.Ready && service.AdmitTick(0),"newer worker data blocks workers, not ordinary rescue");
            service.EndSession();service.StageOwnedRecord(VersionedSaveLoadStatus.NoRecord,null);service.BeginSession(10,true);
            Check(service.Ready && supervisor.Ledger.Count==0,"no record starts fresh without old-save inference");
            service.StageOwnedRecord(VersionedSaveLoadStatus.Restored,original);
            service.StageOwnedRecord(VersionedSaveLoadStatus.NoRecord,null);service.BeginSession(10,true);
            Check(supervisor.Ledger.Count==0,"later load cannot inherit earlier staged receipts");
            world.DuringMove=()=>Check(!service.TryCaptureOwnedRecord(out _) && supervisor.IsExecutingNativeCall,"save gate during own native call");
            Check(supervisor.TryRecover(null,()=>true,0,out var outcome,out _) && outcome==WorkerRecoveryOutcome.Parked,"service native-call gate exercised");
            service.EndSession();service.StageOwnedRecord(VersionedSaveLoadStatus.OutOfScope,null);service.BeginSession(10,true);
            Check(service.Ready && supervisor.Ledger.Count==0,"out-of-scope load retires previous ownership");
            Console.WriteLine("PASS: worker recovery service "+checks+" checks; staged ownership, atomic capture gates, mode and native clock lifecycle; no game execution.");
        }
        private static bool Equal(byte[] first,byte[] second)
        {if(first.Length!=second.Length)return false;for(int i=0;i<first.Length;i++)if(first[i]!=second[i])return false;return true;}
    }
}
