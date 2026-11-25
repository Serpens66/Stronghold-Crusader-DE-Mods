using System;
using SHCDESE.Interop;

namespace TrappedUnitRescueTest.Tests
{
    internal static class WorkerResumeSupervisorFixture
    {
        private static int checks;
        private static void Check(bool value,string reason){checks++;if(!value)throw new InvalidOperationException(reason);}
        private sealed class World : IWorkerResumeWorld
        {
            internal WorkerResumeTicket Ticket;
            internal WorkerReceiptState State=WorkerReceiptState.MatchingActive;
            internal bool CanPark=true, Resume;
            internal int Writes,Attempts;
            internal WorkerOrderObservation Observation;
            internal WorkerResumeTicket Replacement;
            internal ushort LastRequestedX;
            internal Action DuringReissue;
            internal bool AllowProbe=true,ProbeAccepted,FreshMoat,PublicationValid=true;
            internal int Probes;
            internal Func<bool> BeforeProbe;
            public bool TryCapture(UnitSnapshot before,out WorkerResumeTicket ticket,out string reason){ticket=Ticket;reason=null;return ticket!=null;}
            public WorkerReceiptState Inspect(WorkerResumeTicket ticket,bool continuation)=>State;
            public bool TryPark(WorkerResumeTicket ticket,bool continuation)
            {if(!CanPark)return false;Writes++;State=WorkerReceiptState.MatchingParked;return true;}
            public bool Reissue(WorkerResumeTicket ticket)
            {Attempts++;LastRequestedX=ticket.RequestedX;DuringReissue?.Invoke();State=Resume?(FreshMoat?WorkerReceiptState.Unknown:WorkerReceiptState.MatchingActive):WorkerReceiptState.MatchingStopped;return Resume;}
            public bool CanProbeOriginal(WorkerResumeTicket ticket)=>AllowProbe;
            public bool ProbeOriginal(WorkerResumeTicket ticket)
            {Probes++;Check(BeforeProbe==null || BeforeProbe(),"probe has reserved ownership");State=ProbeAccepted?(FreshMoat?WorkerReceiptState.Unknown:WorkerReceiptState.MatchingActive):WorkerReceiptState.MatchingStopped;return ProbeAccepted;}
            public bool IsSuccessfulPublication(WorkerResumeTicket ticket)=>PublicationValid && (State==WorkerReceiptState.MatchingActive || FreshMoat);
            public WorkerOrderObservation ObserveOrderPost(WorkerResumeTicket ticket,long nativeResult,out WorkerResumeTicket replacement)
            {replacement=Replacement;return Observation;}
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
            bool sawReceipt=false;
            Check(supervisor.TryRecover(null,()=>{sawReceipt=supervisor.Ledger.Count==1 && !supervisor.Ledger.TrySerialize(out _);
                world.State=WorkerReceiptState.MatchingStopped;return true;},0,out var result,out _),"failed resume remains recoverable");
            Check(sawReceipt && result==WorkerRecoveryOutcome.Parked && world.State==WorkerReceiptState.MatchingParked && supervisor.Ledger.Count==1,"reservation precedes mutation and park survives failure");
            Check(world.Attempts==1 && world.Writes==2,"park before attempt and after failure");
            world.Resume=true;
            Check(supervisor.Tick(0,512,1)==1 && world.Attempts==2 && supervisor.Ledger.Count==0,"bounded retry releases acknowledged success");
            world.State=WorkerReceiptState.MatchingActive;
            Check(!supervisor.TryRecover(null,()=>false,1,out result,out _) && result==WorkerRecoveryOutcome.Rejected && supervisor.Ledger.Count==0,"original active route unchanged needs no hold");
            world.CanPark=false;
            Check(!supervisor.TryRecover(null,()=>{world.State=WorkerReceiptState.MatchingStopped;return true;},2,out result,out _) &&
                result==WorkerRecoveryOutcome.OwnershipUncertain && supervisor.Ledger.ReservedCount==1 && !supervisor.Ledger.TrySerialize(out _),"violated park contract blocks saving rather than abandoning receipt");
            supervisor.Reset();world.CanPark=true;world.Resume=false;world.State=WorkerReceiptState.MatchingActive;
            Check(supervisor.TryRecover(null,()=>{world.State=WorkerReceiptState.MatchingStopped;throw new InvalidOperationException();},3,out result,out _) &&
                result==WorkerRecoveryOutcome.Parked,"native exception retains ownership");
            int previous=world.Attempts;world.State=WorkerReceiptState.ReplacedOrGone;
            Check(supervisor.Tick(3,512,1)==1 && world.Attempts==previous && supervisor.Ledger.Count==0,"replacement or changed order never reissued");
            world.State=WorkerReceiptState.MatchingActive;world.Ticket=Ticket(2);
            supervisor.TryRecover(null,()=>{world.State=WorkerReceiptState.MatchingStopped;return true;},4,out _,out _);
            Check(supervisor.Ledger.TryGetParkedByUnit(2,out var old),"owned receipt lookup");
            world.Replacement=new WorkerResumeTicket(old.UnitId,old.UnitGlobalId,old.UnitType,old.AIState,old.Owner,
                old.TribeId,old.TribeGlobalId,99,100,old.Job,old.Bindings);
            world.Observation=WorkerOrderObservation.Rebind;
            int writes=world.Writes;
            Check(supervisor.ObserveOrderPost(2,4,0) && supervisor.Ledger.Count==1 && world.Writes==writes,"failed external order rebind without native write");
            Check(!supervisor.Ledger.ReleaseParked(old) && !supervisor.Ledger.ReplaceParked(old,old,4),"stale handoff cannot mutate replacement");
            Check(supervisor.Ledger.TryGetParkedByUnit(2,out var rebound) && rebound.RequestedX==99,"authoritative goal retained");
            world.DuringReissue=()=>Check(supervisor.ObserveOrderPost(2,4,0),"foreign nested order is deferred during own reissue");
            supervisor.Tick(4,512,1);world.DuringReissue=null;
            Check(world.LastRequestedX==99,"retry never restores obsolete goal");
            world.Observation=WorkerOrderObservation.Keep;
            Check(!supervisor.ObserveOrderPost(2,5,1) && supervisor.Ledger.Count==1,"unknown postorder keeps ownership");
            world.Observation=WorkerOrderObservation.Release;
            Check(supervisor.ObserveOrderPost(2,5,1) && supervisor.Ledger.Count==0 && world.Writes==writes+1,"active external order releases without park write");
            var before=new UnitSnapshot {TileId=10,X=5,Y=5,OrdinaryStep=true,NextTileId=11,NextX=6,NextY=5};
            Check(NativeWorkerResumeWorld.AdmissibleCapturePosition(before,10,5,5,2,4,11,6,5),"same accepted step may progress");
            Check(NativeWorkerResumeWorld.AdmissibleCapturePosition(before,11,6,5,4,8,11,6,5),"old accepted step may complete");
            Check(!NativeWorkerResumeWorld.AdmissibleCapturePosition(before,12,7,5,2,8,12,7,5),"two steps cannot inherit old proof");
            Check(!NativeWorkerResumeWorld.AdmissibleCapturePosition(before,11,6,6,2,8,11,6,6),"tile coordinate mismatch rejected");
            Check(!NativeWorkerResumeWorld.AdmissibleCapturePosition(before,10,5,5,2,4,12,5,6),"different next step rejected");
            Check(!NativeWorkerResumeWorld.AdmissibleCapturePosition(before,11,6,5,5,8,11,6,5),"only active entry flags");
            before.OrdinaryStep=false;
            Check(!NativeWorkerResumeWorld.AdmissibleCapturePosition(before,11,6,5,2,8,11,6,5),"uncaptured step cannot complete");
            var moatWorld=new World{Ticket=Ticket(3),ProbeAccepted=true,FreshMoat=true};
            var moat=new WorkerResumeSupervisor(moatWorld);int relocations=0;
            moatWorld.BeforeProbe=()=>moat.Ledger.ReservedCount==1 && !moat.Ledger.TrySerialize(out _);
            Check(moat.TryProbeOrRecover(null,()=>{relocations++;return true;},6,out result,out _) && result==WorkerRecoveryOutcome.Resumed,
                "successful original probe accepts audited fresh moat publication");
            Check(moatWorld.Probes==1 && moatWorld.Attempts==0 && moatWorld.Writes==0 && relocations==0 && moat.Ledger.Count==0,
                "accepted probe never teleports, parks or reissues twice");
            moatWorld.ProbeAccepted=false;moatWorld.FreshMoat=false;moatWorld.State=WorkerReceiptState.MatchingActive;
            Check(moat.TryProbeOrRecover(null,()=>{relocations++;moatWorld.State=WorkerReceiptState.MatchingStopped;return true;},7,out result,out _) &&
                result==WorkerRecoveryOutcome.Parked && moatWorld.State==WorkerReceiptState.MatchingParked,"rejected probe falls back to owned retry");
            Check(moatWorld.Probes==2 && moatWorld.Attempts==1 && relocations==1 && moatWorld.Writes==2,"worst case one probe, relocation and one resume");
            moat.Reset();moatWorld.AllowProbe=false;int oldProbes=moatWorld.Probes;
            Check(!moat.TryProbeOrRecover(null,()=>{relocations++;return true;},8,out _,out _) && moatWorld.Probes==oldProbes && relocations==1 && moat.Ledger.Count==0,
                "incoherent probe pose cannot mutate");
            moatWorld.AllowProbe=true;moatWorld.Resume=true;moatWorld.FreshMoat=true;
            Check(moat.TryRecover(null,()=>true,9,out result,out _) && result==WorkerRecoveryOutcome.Resumed && moat.Ledger.Count==0,
                "successful ordinary reissue can newly publish mode1");
            moatWorld.Resume=false;moatWorld.FreshMoat=true;
            Check(moat.TryRecover(null,()=>true,10,out result,out _) && result==WorkerRecoveryOutcome.Parked && moat.Ledger.Count==1,
                "failed native return cannot authorize fresh moat publication");
            var nestedWorld=new World{Ticket=Ticket(4)};var nested=new WorkerResumeSupervisor(nestedWorld);
            var oldNested=nestedWorld.Ticket;
            nestedWorld.Replacement=new WorkerResumeTicket(oldNested.UnitId,oldNested.UnitGlobalId,oldNested.UnitType,oldNested.AIState,
                oldNested.Owner,oldNested.TribeId,oldNested.TribeGlobalId,144,145,oldNested.Job,oldNested.Bindings);
            nestedWorld.Observation=WorkerOrderObservation.Rebind;
            nestedWorld.DuringReissue=()=>Check(nested.ObserveOrderPost(4,11,0),"foreign failed order retained while receipt reserved");
            Check(nested.TryRecover(null,()=>true,11,out result,out _) && result==WorkerRecoveryOutcome.Parked,"reserved immediate recovery completes");
            Check(nested.Ledger.TryGetParkedByUnit(4,out var nestedTicket) && nestedTicket.RequestedX==144 && nested.Ledger.ReservedCount==0,
                "deferred reserved handoff commits authoritative parked goal");
            nestedWorld.DuringReissue=null;nested.Tick(11,512,1);
            Check(nestedWorld.LastRequestedX==144,"deferred handoff replays current goal only");
            int nestedWrites=nestedWorld.Writes;
            nestedWorld.Replacement=new WorkerResumeTicket(oldNested.UnitId,oldNested.UnitGlobalId,oldNested.UnitType,oldNested.AIState,
                oldNested.Owner,oldNested.TribeId,oldNested.TribeGlobalId,155,156,oldNested.Job,oldNested.Bindings);
            nestedWorld.DuringReissue=()=>Check(nested.ObserveOrderPost(4,20,0),"foreign failed order retained during ledger drain");
            nested.Tick(20,512,1);
            Check(nested.Ledger.TryGetParkedByUnit(4,out nestedTicket) && nestedTicket.RequestedX==155 && nestedWorld.Writes==nestedWrites+1,
                "drain flush atomically rebinds without an extra native write");
            for(int id=1;id<=WorkerResumeLedger.Capacity;id++)
            {supervisor.Ledger.TryReserve(Ticket(id),out var token);supervisor.Ledger.CommitPark(token,999);}
            bool mutated=false;world.Ticket=Ticket(900);
            Check(!supervisor.TryRecover(null,()=>{mutated=true;return true;},4,out _,out _) && !mutated,"capacity prevents native mutation");
            Console.WriteLine("PASS: worker supervisor "+checks+" checks; reservation, safe park, failed resume, replacement and capacity; no game execution.");
        }
    }
}
