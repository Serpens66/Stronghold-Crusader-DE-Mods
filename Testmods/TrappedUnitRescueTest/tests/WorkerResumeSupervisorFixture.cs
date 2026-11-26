using System;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace TrappedUnitRescueTest.Tests
{
    internal static class WorkerResumeSupervisorFixture
    {
        private static int checks;
        private static void Check(bool value,string reason){checks++;if(!value)throw new InvalidOperationException(reason);}
        private sealed class World : IWorkerResumeWorld,IWorkerResumeAuthorityAware,IWorkerResumeBarrierView,IWorkerStartedProbeWorld
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
            internal WorkerProbePreparation Preparation=WorkerProbePreparation.Rejected;
            internal int Normalizations;
            internal Action DuringPreparation;
            internal Func<bool> BeforePreparation;
            internal bool ThrowPreparation,PreparationPublishesPose=true;
            public WorkerProbePreparation PrepareStartedProbe(WorkerResumeTicket ticket,UnitSnapshot before)
            {
                Check(BeforePreparation==null || BeforePreparation(),"normalization has reserved ownership before native effects");
                if(Preparation==WorkerProbePreparation.Rejected)return Preparation;
                Normalizations++;DuringPreparation?.Invoke();
                if(ThrowPreparation)throw new InvalidOperationException("normalization wrapper failed");
                if(Preparation==WorkerProbePreparation.Ready && PreparationPublishesPose){AllowProbe=true;State=WorkerReceiptState.MatchingStopped;}
                return Preparation;
            }
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
            VerifyStartedMoatPreparation();
            VerifyNativeAuthority();
            for(int id=1;id<=WorkerResumeLedger.Capacity;id++)
            {supervisor.Ledger.TryReserve(Ticket(id),out var token);supervisor.Ledger.CommitPark(token,999);}
            bool mutated=false;world.Ticket=Ticket(900);
            Check(!supervisor.TryRecover(null,()=>{mutated=true;return true;},4,out _,out _) && !mutated,"capacity prevents native mutation");
            Console.WriteLine("PASS: worker supervisor "+checks+" checks; reservation, safe park, failed resume, replacement and capacity; no game execution.");
        }
        private static UnitSnapshot StartedMoatBefore()=>new UnitSnapshot { Id=99,GlobalId=99,Type=(int)eChimps.CHIMP_TYPE_QUARRY_OX,
            X=5,Y=5,TileId=10,NextX=6,NextY=5,NextTileId=11,Flags=2,Substep=0,OrdinaryStep=true,
            AffectedCurrent=true,AffectedNext=true,CurrentWasOrdinary=true,NextWasOrdinary=true,Job=Ticket(99).Job };
        private static unsafe void VerifyStartedMoatPreparation()
        {
            uint moat=(uint)TilePropertyFlag.IsMoat,wall=(uint)TilePropertyFlag.IsWall;
            var before=StartedMoatBefore();
            for(int substep=0;substep<8;substep++)foreach(int flags in new[]{2,4})
                Check(NativeWorkerResumeWorld.IsStartedMoatProbeGeometry(before,10,5,5,11,6,5,flags,substep,moat,moat),
                    "captured original ordinary step may advance before canonical same-Current normalization");
            for(int substep=1;substep<8;substep++)
            {
                Check(NativeWorkerResumeWorld.IsStartedMoatProbeGeometry(before,11,6,5,11,6,5,2,substep,moat,moat) &&
                    NativeWorkerResumeWorld.IsCapturedMoatMidpoint(before,11,6,5,11,6,5,2,substep,moat,moat),
                    "native midpoint may commit only the old acceptedNext while its remaining half-step runs");
                Check(!NativeWorkerResumeWorld.AdmissibleCapturePosition(before,11,6,5,2,substep,11,6,5) &&
                    NativeWorkerResumeWorld.AdmissibleCapturePosition(before,11,6,5,2,substep,11,6,5,true),
                    "post-midpoint admission needs the separately proved causal moat capability; defaults remain strict");
            }
            var nextOnly=StartedMoatBefore();nextOnly.AffectedCurrent=false;
            for(int substep=0;substep<8;substep++)foreach(int flags in new[]{2,4})
            {
                Check(!NativeWorkerResumeWorld.IsStartedMoatProbeGeometry(nextOnly,10,5,5,11,6,5,flags,substep,0,moat,true),
                    "Next-only before midpoint must retain ordinary Current through nonprobe reserved recovery");
                Check(NativeWorkerResumeWorld.IsCapturedMoatMidpoint(nextOnly,11,6,5,11,6,5,flags,substep,0,moat,true),
                    "only the captured newly moated Next can normalize after its actual midpoint commit");
            }
            Check(!NativeWorkerResumeWorld.IsCapturedMoatMidpoint(nextOnly,11,6,5,11,6,5,2,4,0,moat),
                "Next-only midpoint requires independently confirmed unchanged ordinary old Current");
            Check(!NativeWorkerResumeWorld.IsCapturedMoatMidpoint(nextOnly,11,6,5,11,6,5,2,4,wall,moat,true),
                "a newly walled old Current cannot use the Next-only ordinary-bank contract");
            Check(!NativeWorkerResumeWorld.IsCapturedMoatMidpoint(nextOnly,11,6,5,12,7,5,2,4,0,moat,true),
                "Next-only midpoint never inherits an unobserved second step");
            Check(!NativeWorkerResumeWorld.IsStartedMoatProbeGeometry(before,10,5,5,11,6,5,2,4,moat,0,true),
                "Current-only moat with a healthy accepted Next keeps native physical exit ownership");
            var legalCurrentWorld=new World{Ticket=Ticket(99),AllowProbe=false,Resume=true,State=WorkerReceiptState.MatchingActive};
            var legalCurrentOwner=new WorkerResumeSupervisor(legalCurrentWorld);int sameCurrentNormalizations=0;
            Check(legalCurrentOwner.TryRecover(nextOnly,()=>{
                Check(legalCurrentOwner.Ledger.ReservedCount==1 && legalCurrentOwner.IsExecutingNativeCall,
                    "Next-only legal Current is normalized only after a durable intent reservation");
                sameCurrentNormalizations++;legalCurrentWorld.State=WorkerReceiptState.MatchingStopped;return true;
            },1,out var legalCurrentOutcome,out _) && legalCurrentOutcome==WorkerRecoveryOutcome.Resumed &&
                sameCurrentNormalizations==1 && legalCurrentWorld.Probes==0 && legalCurrentWorld.Normalizations==0 &&
                legalCurrentWorld.Attempts==1 && legalCurrentOwner.Ledger.Count==0,
                "legal Current route takes one normalization and original-goal resume without a moat-source probe");
            foreach(var change in new Action<UnitSnapshot>[] { b=>b.AffectedNext=false,b=>b.CurrentWasOrdinary=false,
                b=>b.NextWasOrdinary=false,b=>b.GroundFlags=moat,b=>b.NextGroundFlags=moat,b=>b.GroundFlags=wall,
                b=>b.NextGroundFlags=wall,b=>b.OrdinaryStep=false,b=>b.Substep=8,b=>b.Flags=0,b=>b.Job=null,
                b=>b.Unsupported="foreign-state" })
            {
                var invalid=StartedMoatBefore();invalid.AffectedCurrent=false;change(invalid);
                Check(!NativeWorkerResumeWorld.IsCapturedMoatMidpoint(invalid,11,6,5,11,6,5,2,4,0,moat,true),
                    "Next-only midpoint preserves all old ordinary, causal, typed-intent and single-step restrictions");
            }
            var live=new GameUnit();live.r_UnitChimp=eChimps.CHIMP_TYPE_QUARRY_OX;live.r_AIState=1;live.r_CurrentHealth=1;
            live.r_PathPlanStateBitFlags=2;live.r_MovementSubstep=4;
            live.r_CurrentTilePositionX=live.r_NextTilePositionX2=6;live.r_CurrentTilePositionY=live.r_NextTilePositionY2=5;
            Check(RescueRuntime.ClassifyState(&live,true)=="step-in-progress",
                "a fresh live snapshot without historical causal input does not adopt a midpoint pose");
            foreach(var change in new Action<UnitSnapshot>[] { b=>b.AffectedCurrent=false,b=>b.AffectedNext=false,
                b=>b.CurrentWasOrdinary=false,b=>b.NextWasOrdinary=false,b=>b.GroundFlags=moat,b=>b.NextGroundFlags=moat,
                b=>b.GroundFlags=wall,b=>b.NextGroundFlags=wall,b=>b.OrdinaryStep=false,b=>b.Substep=8,b=>b.Flags=0,b=>b.Job=null,
                b=>b.Unsupported="foreign-state" })
            {
                var invalid=StartedMoatBefore();change(invalid);
                Check(!NativeWorkerResumeWorld.IsStartedMoatProbeGeometry(invalid,10,5,5,11,6,5,2,0,moat,moat),
                    "missing both-new causal endpoint proof cannot cancel a pending step");
                Check(!NativeWorkerResumeWorld.IsCapturedMoatMidpoint(invalid,11,6,5,11,6,5,2,4,moat,moat),
                    "post-midpoint capability retains every both-new causal proof requirement");
            }
            foreach(uint currentFlags in new[]{0u,moat,wall,moat|wall})foreach(uint nextFlags in new[]{0u,moat,wall,moat|wall})
                Check(NativeWorkerResumeWorld.IsStartedMoatProbeGeometry(before,10,5,5,11,6,5,2,0,currentFlags,nextFlags)==
                    (currentFlags==moat && nextFlags==moat),"free Current, free acceptedNext and wall mixtures never normalize");
            Check(!NativeWorkerResumeWorld.IsStartedMoatProbeGeometry(before,11,6,5,12,7,5,2,0,moat,moat),
                "a subsequent step from old acceptedNext cannot inherit the old normalization proof");
            Check(!NativeWorkerResumeWorld.IsStartedMoatProbeGeometry(before,10,5,5,12,5,6,2,0,moat,moat),
                "another acceptedNext cannot inherit old reachability");
            Check(!NativeWorkerResumeWorld.IsStartedMoatProbeGeometry(before,10,5,6,11,6,5,2,0,moat,moat),
                "packed Current and XY must match the captured original endpoint");
            Check(!NativeWorkerResumeWorld.IsStartedMoatProbeGeometry(before,10,5,5,11,6,5,2,8,moat,moat),
                "already stopped boundary follows the existing strict probe without started preparation");
            foreach(var type in new[]{eChimps.CHIMP_TYPE_SPEARMAN,eChimps.CHIMP_TYPE_CATAPULT,eChimps.CHIMP_TYPE_SIEGE_TOWER,
                eChimps.CHIMP_TYPE_BATTERING_RAM,eChimps.CHIMP_TYPE_PORTABLE_SHIELD,eChimps.CHIMP_TYPE_ARAB_BALLISTA})
            {
                var memory=new CommandJourneyFixture.Memory(type,101,199);
                Check(CommandJourneySnapshot.TryCapture(type,101,memory.Word,memory.Dword,out var command),"real typed command/crew snapshot admits known active phase");
                var commandBefore=StartedMoatBefore();commandBefore.Id=199;commandBefore.GlobalId=199;
                commandBefore.Type=(int)type;commandBefore.AIState=101;commandBefore.Owner=2;commandBefore.Tribe=2;commandBefore.TribeGlobalId=33;
                commandBefore.RequestedX=20;commandBefore.RequestedY=30;commandBefore.Job=null;commandBefore.Command=command;
                Check(NativeWorkerResumeWorld.IsCapturedMoatMidpoint(commandBefore,11,6,5,11,6,5,2,4,moat,moat),
                    "qualified active command uses the same exact committed old step geometry");
                commandBefore.AffectedCurrent=false;
                Check(NativeWorkerResumeWorld.IsCapturedMoatMidpoint(commandBefore,11,6,5,11,6,5,2,4,0,moat,true),
                    "Next-only midpoint admits the same immutable command and complete crew schema");
                commandBefore.AffectedCurrent=true;
                var commandTicket=WorkerResumeTicket.ForCommand(199,199,type,101,2,2,33,20,30,command);
                var commandWorld=new World{Ticket=commandTicket,AllowProbe=false,Preparation=WorkerProbePreparation.Ready};
                var commandOwner=new WorkerResumeSupervisor(commandWorld);
                Check(commandOwner.TryProbeOrRecover(commandBefore,()=>{commandWorld.State=WorkerReceiptState.MatchingStopped;return true;},1,out var commandResult,out _) &&
                    commandResult==WorkerRecoveryOutcome.Parked && commandOwner.Ledger.TryGetParkedByUnit(199,out var parkedCommand) &&
                    ReferenceEquals(parkedCommand.Command,command) && command.Matches(type,101,memory.Word,memory.Dword),
                    "normalization and failed probe retain the exact immutable original command and every crew binding");
                Check(commandOwner.Ledger.TrySerialize(out var commandSave),"settled normalized command park uses the existing save schema");
                var restored=new WorkerResumeLedger();
                Check(restored.TryRestore(commandSave,1) && restored.TryGetParkedByUnit(199,out var restoredCommand) &&
                    restoredCommand.Kind==ResumeReceiptKind.Command && restoredCommand.Command.Matches(type,101,memory.Word,memory.Dword),
                    "saved/restored continuation retains command and full crew identity without new pose authority");
                if(CommandJourneySnapshot.RequiredCrewCount(type)>0)
                {
                    memory.Word(0x96C,777);
                    Check(!command.Matches(type,101,memory.Word,memory.Dword),"foreign crew rebinding defeats the unchanged native command matcher");
                }
            }
            var world=new World {Ticket=Ticket(99),AllowProbe=false,Preparation=WorkerProbePreparation.Ready,ProbeAccepted=true,FreshMoat=true};
            var owner=new WorkerResumeSupervisor(world);int relocations=0;
            world.BeforePreparation=()=>owner.Ledger.ReservedCount==1 && !owner.Ledger.TrySerialize(out _) && owner.IsExecutingNativeCall;
            Check(owner.TryProbeOrRecover(before,()=>{relocations++;return true;},1,out var result,out _) && result==WorkerRecoveryOutcome.Resumed,
                "actual native accepted moat route after normalization needs no spatial bank relocation");
            Check(world.Normalizations==1 && world.Probes==1 && world.Attempts==0 && relocations==0 && owner.Ledger.Count==0,
                "same-tile normalization alone is not counted as completed recovery or a second reissue");
            world.AllowProbe=false;world.ProbeAccepted=false;world.FreshMoat=false;world.Resume=true;world.State=WorkerReceiptState.MatchingActive;
            Check(owner.TryProbeOrRecover(before,()=>{relocations++;world.State=WorkerReceiptState.MatchingStopped;return true;},2,out result,out _) &&
                result==WorkerRecoveryOutcome.Resumed,"actual native refusal permits existing owned bank fallback after normalization");
            Check(world.Normalizations==2 && world.Probes==2 && world.Attempts==1 && relocations==1,
                "worst case bounded by normalization, one probe, one spatial fallback, one reissue: four native operations");
            foreach(bool throws in new[]{false,true})
            {
                var uncertainWorld=new World {Ticket=Ticket(100),AllowProbe=false,Preparation=WorkerProbePreparation.OwnershipUncertain,ThrowPreparation=throws};
                var uncertain=new WorkerResumeSupervisor(uncertainWorld);int fallback=0;
                Check(!uncertain.TryProbeOrRecover(before,()=>{fallback++;return true;},3,out result,out _) &&
                    result==WorkerRecoveryOutcome.OwnershipUncertain && uncertainWorld.Normalizations==1 && uncertainWorld.Probes==0 && fallback==0 &&
                    uncertain.Ledger.ReservedCount==1 && !uncertain.Ledger.TrySerialize(out _),
                    "partial normalization or wrapper exception retains reserved ownership and blocks saving without probe/fallback");
            }
            var badWorld=new World {Ticket=Ticket(101),AllowProbe=false,Preparation=WorkerProbePreparation.Ready,PreparationPublishesPose=false};
            var bad=new WorkerResumeSupervisor(badWorld);
            Check(!bad.TryProbeOrRecover(before,()=>true,4,out result,out _) && result==WorkerRecoveryOutcome.OwnershipUncertain &&
                badWorld.Probes==0 && bad.Ledger.ReservedCount==1,"reported normalization success cannot bypass the unchanged CanProbeOriginal guard");
            var nestedWorld=new World {Ticket=Ticket(102),AllowProbe=false,Preparation=WorkerProbePreparation.Ready,Observation=WorkerOrderObservation.Release};
            var nested=new WorkerResumeSupervisor(nestedWorld);int nestedFallback=0;
            nestedWorld.DuringPreparation=()=>{Check(nested.ObserveOrderPre(102),"external order sees reserved normalization ownership");nested.ObserveOrderPost(102,5,1);};
            Check(!nested.TryProbeOrRecover(before,()=>{nestedFallback++;return true;},5,out result,out _) &&
                result==WorkerRecoveryOutcome.Superseded && nestedWorld.Probes==0 && nestedFallback==0 && nested.Ledger.Count==0,
                "foreign native order during normalization wins without any stale probe, bank movement or goal restore");
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
