using System;
using SHCDESE.Interop;

namespace TrappedUnitRescueTest.Tests
{
    internal static class WorkerResumeSupervisorFixture
    {
        private static int checks;
        private static void Check(bool value,string reason){checks++;if(!value)throw new InvalidOperationException(reason);}
        private sealed class World : IWorkerResumeWorld,IWorkerResumeAuthorityAware,IWorkerResumeBarrierView
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
            private Func<WorkerResumeTicket,bool> authority;
            private Action<WorkerResumeTicket> invalidateSource;
            internal Action BeforeOriginal,AfterOriginal;
            internal int Originals;
            internal bool ValidateGoal,Coherent=true,CallPoseCurrent=true;
            internal ushort LiveRequestedX;
            public void BindResumeAuthority(Func<WorkerResumeTicket,bool> isCurrent,Action<WorkerResumeTicket> onSourceLost)
            {authority=isCurrent;invalidateSource=onSourceLost;}
            public bool TryCapture(UnitSnapshot before,out WorkerResumeTicket ticket,out string reason){ticket=Ticket;reason=null;return ticket!=null;}
            public WorkerReceiptState Inspect(WorkerResumeTicket ticket,bool continuation)=>ValidateGoal && ticket.RequestedX!=LiveRequestedX?WorkerReceiptState.Unknown:State;
            public bool TryPark(WorkerResumeTicket ticket,bool continuation)
            {if(!CanPark || !Coherent || ValidateGoal && ticket.RequestedX!=LiveRequestedX)return false;
                if(State!=WorkerReceiptState.MatchingParked)Writes++;State=WorkerReceiptState.MatchingParked;return true;}
            public bool Reissue(WorkerResumeTicket ticket)
            {
                Attempts++;LastRequestedX=ticket.RequestedX;BeforeOriginal?.Invoke();DuringReissue?.Invoke();
                if(!Coherent || !CallPoseCurrent){invalidateSource(ticket);return false;}
                if(!authority(ticket))return false;
                Originals++;LiveRequestedX=ticket.RequestedX;
                State=Resume?(FreshMoat?WorkerReceiptState.Unknown:WorkerReceiptState.MatchingActive):WorkerReceiptState.MatchingStopped;
                AfterOriginal?.Invoke();if(!Coherent || !CallPoseCurrent){invalidateSource(ticket);return false;}return Resume && authority(ticket);
            }
            public bool CanProbeOriginal(WorkerResumeTicket ticket)=>AllowProbe;
            public bool ProbeOriginal(WorkerResumeTicket ticket)
            {
                Probes++;Check(BeforeProbe==null || BeforeProbe(),"probe has reserved ownership");BeforeOriginal?.Invoke();
                if(!Coherent || !CallPoseCurrent){invalidateSource(ticket);return false;}
                if(!authority(ticket))return false;
                Originals++;LiveRequestedX=ticket.RequestedX;
                State=ProbeAccepted?(FreshMoat?WorkerReceiptState.Unknown:WorkerReceiptState.MatchingActive):WorkerReceiptState.MatchingParked;
                AfterOriginal?.Invoke();if(!Coherent || !CallPoseCurrent){invalidateSource(ticket);return false;}return ProbeAccepted && authority(ticket);
            }
            public bool IsSuccessfulPublication(WorkerResumeTicket ticket)=>PublicationValid && (State==WorkerReceiptState.MatchingActive || FreshMoat);
            public WorkerOrderObservation ObserveOrderPost(WorkerResumeTicket ticket,long nativeResult,out WorkerResumeTicket replacement)
            {
                replacement=Replacement;
                if(Observation==WorkerOrderObservation.Release)return nativeResult>0?Observation:WorkerOrderObservation.Keep;
                if(Observation==WorkerOrderObservation.Rebind && (replacement==null || !TryPark(replacement,true)))return WorkerOrderObservation.Keep;
                return Observation;
            }
            public bool HasOwnedArrivalBarrier(WorkerResumeTicket ticket)=>State==WorkerReceiptState.MatchingParked && Coherent;
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
            supervisor.Tick(4,0,0);
            Check(!supervisor.Ledger.ReleaseParked(old) && !supervisor.Ledger.ReplaceParked(old,old,4),"stale handoff cannot mutate replacement");
            Check(supervisor.Ledger.TryGetParkedByUnit(2,out var rebound) && rebound.RequestedX==99,"authoritative goal retained");
            world.DuringReissue=()=>Check(supervisor.ObserveOrderPost(2,4,0),"foreign nested order is deferred during own reissue");
            supervisor.Tick(4,512,1);world.DuringReissue=null;
            Check(world.LastRequestedX==99,"retry never restores obsolete goal");
            world.Observation=WorkerOrderObservation.Keep;
            Check(supervisor.ObserveOrderPost(2,5,1) && supervisor.Ledger.Count==1 && supervisor.HasPendingExternalOrders,"unknown postorder keeps ownership");
            world.Observation=WorkerOrderObservation.Release;
            supervisor.ObserveOrderPost(2,5,1);supervisor.Tick(5,0,0);
            Check(supervisor.Ledger.Count==0 && world.Writes==writes,"active external order releases without park write");
            var before=new UnitSnapshot {TileId=10,X=5,Y=5,Flags=2,OrdinaryStep=true,NextTileId=11,NextX=6,NextY=5};
            Check(NativeWorkerResumeWorld.AdmissibleCapturePosition(before,10,5,5,2,4,11,6,5),"same accepted step may progress");
            Check(NativeWorkerResumeWorld.AdmissibleCapturePosition(before,11,6,5,4,8,11,6,5),"old accepted step may complete");
            Check(!NativeWorkerResumeWorld.AdmissibleCapturePosition(before,12,7,5,2,8,12,7,5),"two steps cannot inherit old proof");
            Check(!NativeWorkerResumeWorld.AdmissibleCapturePosition(before,11,6,6,2,8,11,6,6),"tile coordinate mismatch rejected");
            Check(!NativeWorkerResumeWorld.AdmissibleCapturePosition(before,10,5,5,2,4,12,5,6),"different next step rejected");
            Check(!NativeWorkerResumeWorld.AdmissibleCapturePosition(before,11,6,5,5,8,11,6,5),"only active entry flags");
            Check(NativeWorkerResumeWorld.AdmissibleCapturePosition(before,11,6,5,0,8,11,6,5),"exact captured final step may become flags0");
            Check(!NativeWorkerResumeWorld.AdmissibleCapturePosition(before,10,5,5,0,8,10,5,5),"arbitrary stop on old current cannot inherit active intent");
            Check(!NativeWorkerResumeWorld.AdmissibleCapturePosition(before,11,6,5,0,8,12,7,5),"finished step requires coherent current equals next");
            before.OrdinaryStep=false;
            Check(!NativeWorkerResumeWorld.AdmissibleCapturePosition(before,11,6,5,2,8,11,6,5),"uncaptured step cannot complete");
            var moatWorld=new World{Ticket=Ticket(3),ProbeAccepted=true,FreshMoat=true};
            var moat=new WorkerResumeSupervisor(moatWorld);int relocations=0;
            moatWorld.BeforeProbe=()=>moat.Ledger.ReservedCount==1 && !moat.Ledger.TrySerialize(out _);
            Check(moat.TryProbeOrRecover(null,()=>{relocations++;return true;},6,out result,out _) && result==WorkerRecoveryOutcome.Resumed,
                "successful original probe accepts audited fresh moat publication");
            Check(moatWorld.Probes==1 && moatWorld.Attempts==0 && moatWorld.Writes==1 && relocations==0 && moat.Ledger.Count==0,
                "accepted protected probe never teleports or reissues twice");
            moatWorld.ProbeAccepted=false;moatWorld.FreshMoat=false;moatWorld.State=WorkerReceiptState.MatchingActive;
            Check(moat.TryProbeOrRecover(null,()=>{relocations++;moatWorld.State=WorkerReceiptState.MatchingStopped;return true;},7,out result,out _) &&
                result==WorkerRecoveryOutcome.Parked && moatWorld.State==WorkerReceiptState.MatchingParked,"rejected probe falls back to owned retry");
            Check(moatWorld.Probes==2 && moatWorld.Attempts==1 && relocations==1 && moatWorld.Writes==4,"worst case one probe, relocation and one resume");
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
            Check(nested.Ledger.TryGetParkedByUnit(4,out nestedTicket) && nestedTicket.RequestedX==155 && nestedWorld.Writes==nestedWrites,
                "drain flush atomically rebinds without an extra native write");
            VerifyNativeAuthority();
            for(int id=1;id<=WorkerResumeLedger.Capacity;id++)
            {supervisor.Ledger.TryReserve(Ticket(id),out var token);supervisor.Ledger.CommitPark(token,999);}
            bool mutated=false;world.Ticket=Ticket(900);
            Check(!supervisor.TryRecover(null,()=>{mutated=true;return true;},4,out _,out _) && !mutated,"capacity prevents native mutation");
            Console.WriteLine("PASS: worker supervisor "+checks+" checks; reservation, safe park, failed resume, replacement and capacity; no game execution.");
        }
        private static void VerifyNativeAuthority()
        {
            foreach(bool probe in new[]{false,true})foreach(bool beforeOriginal in new[]{false,true})
            foreach(bool foreignSuccess in new[]{false,true})foreach(bool sameGoal in new[]{false,true})
            {
                var old=Ticket(40);ushort goal=sameGoal?old.RequestedX:(ushort)144;
                var world=new World{Ticket=old,Resume=true,ProbeAccepted=true,ValidateGoal=true,LiveRequestedX=old.RequestedX};
                var owner=new WorkerResumeSupervisor(world);
                world.Replacement=new WorkerResumeTicket(old.UnitId,old.UnitGlobalId,old.UnitType,old.AIState,old.Owner,
                    old.TribeId,old.TribeGlobalId,goal,old.RequestedY,old.Job,old.Bindings);
                world.Observation=foreignSuccess?WorkerOrderObservation.Release:WorkerOrderObservation.Rebind;
                Action foreign=()=>
                {
                    int writes=world.Writes;
                    Check(owner.ObserveOrderPre(old.UnitId),"foreign PRE retires old ticket authority before original");
                    world.LiveRequestedX=goal;
                    world.State=foreignSuccess || !beforeOriginal?WorkerReceiptState.MatchingActive:WorkerReceiptState.MatchingParked;
                    Check(owner.ObserveOrderPost(old.UnitId,20,foreignSuccess?1:0),"foreign POST recorded with actual result");
                    Check(world.Writes==writes && owner.HasPendingExternalOrders,"POST cannot park inside foreign caller tail");
                };
                if(beforeOriginal)world.BeforeOriginal=foreign;else world.AfterOriginal=foreign;
                int relocations=0;
                Func<bool> relocate=()=>{relocations++;world.State=WorkerReceiptState.MatchingStopped;return true;};
                WorkerRecoveryOutcome outcome;string reason;
                bool accepted=probe?owner.TryProbeOrRecover(null,relocate,20,out outcome,out reason):owner.TryRecover(null,relocate,20,out outcome,out reason);
                Check(world.Originals==(beforeOriginal?0:1),"old original cannot execute after foreign PRE, including same-goal commands");
                Check(world.LiveRequestedX==goal && !owner.HasPendingExternalOrders,"current authoritative goal survives whole wrapper reconciliation");
                Check(relocations==(probe?0:1),"superseded probe never falls back to teleport");
                if(foreignSuccess)
                    Check(!accepted && outcome==WorkerRecoveryOutcome.Superseded && owner.Ledger.Count==0 && world.Writes==1,
                        "positive foreign publication owns the route without old replay or extra park");
                else
                {
                    Check(accepted && outcome==WorkerRecoveryOutcome.Parked && owner.Ledger.TryGetParkedByUnit(old.UnitId,out var current) &&
                        current.RequestedX==goal && !ReferenceEquals(current,old) && world.State==WorkerReceiptState.MatchingParked,
                        "foreign zero cannot be mistaken for arrival or stale active publication");
                    Check(world.Writes==(beforeOriginal?1:2),"stale flags2 after own1 foreign0 park only at owned boundary");
                }
            }
            var abortedWorld=new World{Ticket=Ticket(41),ValidateGoal=true,LiveRequestedX=20};
            var aborted=new WorkerResumeSupervisor(abortedWorld);
            abortedWorld.BeforeOriginal=()=>
            {
                Check(aborted.ObserveOrderPre(41),"aborted foreign PRE observed");
                int writes=abortedWorld.Writes;
                Check(aborted.ObserveOrderAborted(41,30) && abortedWorld.Writes==writes,"aborted callback changes metadata only");
            };
            Check(aborted.TryProbeOrRecover(null,()=>{throw new InvalidOperationException("No fallback after veto.");},30,out var result,out _) &&
                result==WorkerRecoveryOutcome.Parked && abortedWorld.Originals==0 && aborted.Ledger.ReservedCount==0,"aborted protected probe retains safe flags5 ownership");
            Check(!aborted.HasPendingExternalOrders && aborted.Ledger.TryGetParkedByUnit(41,out var held) && held.RequestedX==20,"aborted invocation invents no replacement target");
            abortedWorld.BeforeOriginal=null;int originals=abortedWorld.Originals;
            aborted.ObserveOrderPre(41);abortedWorld.LiveRequestedX=99;aborted.ObserveOrderAborted(41,31);aborted.Tick(31,512,1);
            Check(abortedWorld.Originals==originals && aborted.Ledger.Count==1 && !aborted.HasPendingExternalOrders,
                "changed context after abort waits without replaying old intent");
            foreach(bool before in new[]{false,true})
            {
                var movedWorld=new World{Ticket=Ticket(42),Resume=true};var moved=new WorkerResumeSupervisor(movedWorld);
                Action transport=()=>{movedWorld.CallPoseCurrent=false;movedWorld.State=WorkerReceiptState.MatchingStopped;};
                if(before)movedWorld.BeforeOriginal=transport;else movedWorld.AfterOriginal=transport;
                Check(!moved.TryRecover(null,()=>true,40,out var outcome,out _) && outcome==WorkerRecoveryOutcome.Superseded &&
                    movedWorld.Originals==(before?0:1) && moved.Ledger.Count==0,"foreign coherent teleport defeats pinned call pose before and after original");
                int calls=movedWorld.Originals;moved.Tick(41,512,4);
                Check(movedWorld.Originals==calls,"retired transported source cannot replay old goal on a later tick");
            }
        }
    }
}
