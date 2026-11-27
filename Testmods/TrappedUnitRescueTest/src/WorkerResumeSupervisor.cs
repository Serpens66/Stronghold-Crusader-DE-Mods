using System;
using System.Collections.Generic;

namespace TrappedUnitRescueTest
{
    internal enum WorkerRecoveryOutcome { Rejected, Resumed, Parked, OwnershipUncertain, Superseded, AwaitResources }
    internal enum WorkerReceiptState { MatchingActive, MatchingParked, MatchingStopped, ReplacedOrGone, Unknown }
    internal enum WorkerOrderObservation { Keep, Release, Rebind }
    internal enum WorkerProbePreparation { Rejected, Ready, OwnershipUncertain }
    internal interface IWorkerStartedProbeWorld
    {WorkerProbePreparation PrepareStartedProbe(WorkerResumeTicket ticket,UnitSnapshot before);}
    internal interface IWorkerResumeAuthorityAware
    {void BindResumeAuthority(Func<WorkerResumeTicket,bool> isCurrent,Action<WorkerResumeTicket> invalidateSource);}
    internal interface IWorkerResumeBarrierView
    {bool HasOwnedArrivalBarrier(WorkerResumeTicket ticket);}
    internal interface IWorkerResumeWorld
    {
        bool TryCapture(UnitSnapshot before,out WorkerResumeTicket ticket,out string reason);
        WorkerReceiptState Inspect(WorkerResumeTicket ticket,bool continuation);
        bool TryPark(WorkerResumeTicket ticket,bool continuation);
        bool Reissue(WorkerResumeTicket ticket);
        bool CanProbeOriginal(WorkerResumeTicket ticket);
        bool ProbeOriginal(WorkerResumeTicket ticket);
        // Only valid immediately after a successful native move return.
        bool IsSuccessfulPublication(WorkerResumeTicket ticket);
        WorkerOrderObservation ObserveOrderPost(WorkerResumeTicket ticket,long nativeResult,out WorkerResumeTicket replacement);
    }

    // Runs only under the caller's simulation lock. The mutation delegate must be
    // synchronous and must never yield, advance a tick or replace a native unit.
    internal sealed class WorkerResumeSupervisor
    {
        private readonly IWorkerResumeWorld world;
        private bool executingNativeCall;
        private int coordinationDepth;
        private readonly Dictionary<int,long> deferredOrders=new Dictionary<int,long>();
        private readonly HashSet<int> foreignOrders=new HashSet<int>();
        private readonly HashSet<int> abortedOrders=new HashSet<int>();
        private readonly HashSet<WorkerResumeTicket> lostSources=new HashSet<WorkerResumeTicket>();
        private long deferredTick;
        internal bool IsExecutingNativeCall=>executingNativeCall;
        internal bool HasPendingExternalOrders=>foreignOrders.Count!=0 || deferredOrders.Count!=0 || lostSources.Count!=0;
        internal readonly WorkerResumeLedger Ledger=new WorkerResumeLedger();
        internal bool CanReserveSlot=>Ledger.CanReserveSlot;
        private IWorkerLegacyRetirementWorld legacyPublication;
        private readonly HashSet<WorkerResumeTicket> publishedLegacyOrders=new HashSet<WorkerResumeTicket>();
        internal bool EnableLegacyCivilianRetirement {get;private set;}
        internal void EnableLegacyRetirement(IWorkerLegacyRetirementWorld retirement)
        {legacyPublication=retirement??throw new ArgumentNullException(nameof(retirement));EnableLegacyCivilianRetirement=true;}
        internal int RetireLegacyCivilianParks(int visits,int attempts,IWorkerLegacyRetirementWorld retirement,Func<bool> currentWorld,
            Action<WorkerResumeTicket,WorkerLegacyRetirement,string> report=null)
        {
            if(!EnableLegacyCivilianRetirement || retirement==null || currentWorld==null || executingNativeCall || coordinationDepth!=0 || !currentWorld())return 0;
            return Ledger.DrainLegacyCivilian(visits,attempts,ticket=>
            {
                // An in-flight foreign command owns the boundary. Completed or
                // aborted commands are re-read below; their active route wins.
                if(foreignOrders.Contains(ticket.UnitId))
                {
                    if(!deferredOrders.ContainsKey(ticket.UnitId) && !abortedOrders.Contains(ticket.UnitId))
                    {ReportLegacy(report,ticket,WorkerLegacyRetirement.Deferred,"foreign-order-in-flight");return WorkerLegacyRetirement.Deferred;}
                    deferredOrders.Remove(ticket.UnitId);abortedOrders.Remove(ticket.UnitId);foreignOrders.Remove(ticket.UnitId);
                }
                if(publishedLegacyOrders.Remove(ticket))return WorkerLegacyRetirement.Released;
                bool Current()=>currentWorld() && AuthorityCurrent(ticket);
                if(!Current()){ReportLegacy(report,ticket,WorkerLegacyRetirement.Deferred,"authority-unavailable");return WorkerLegacyRetirement.Deferred;}
                executingNativeCall=true;
                try
                {
                    var result=retirement.Retire(ticket,Current);ReportLegacy(report,ticket,result,null);return result;
                }
                catch(Exception error)
                {ReportLegacy(report,ticket,WorkerLegacyRetirement.Deferred,"exception-"+error.GetType().Name);return WorkerLegacyRetirement.Deferred;}
                finally{executingNativeCall=false;}
            });
        }

        private static void ReportLegacy(Action<WorkerResumeTicket,WorkerLegacyRetirement,string> report,
            WorkerResumeTicket ticket,WorkerLegacyRetirement state,string reason)
        {try{report?.Invoke(ticket,state,reason);}catch{}}
        internal WorkerResumeSupervisor(IWorkerResumeWorld world)
        {
            this.world=world??throw new ArgumentNullException(nameof(world));
            if(world is IWorkerResumeAuthorityAware aware)aware.BindResumeAuthority(AuthorityCurrent,InvalidateSource);
        }
        private bool AuthorityCurrent(WorkerResumeTicket ticket)=>ticket!=null && !foreignOrders.Contains(ticket.UnitId) && !lostSources.Contains(ticket) &&
            Ledger.TryGetOwnedByUnit(ticket.UnitId,out var current) && ReferenceEquals(ticket,current);
        private void InvalidateSource(WorkerResumeTicket ticket)
        {if(ticket!=null && Ledger.TryGetOwnedByUnit(ticket.UnitId,out var current) && ReferenceEquals(ticket,current))lostSources.Add(ticket);}
        internal bool ObserveOrderPre(int unitId)
        {
            if(!Ledger.TryGetOwnedByUnit(unitId,out _))return false;
            foreignOrders.Add(unitId);return true;
        }
        internal bool ObserveOrderAborted(int unitId,long tick)
        {
            if(tick<0 || !Ledger.TryGetOwnedByUnit(unitId,out _))return false;
            deferredOrders.Remove(unitId);foreignOrders.Add(unitId);abortedOrders.Add(unitId);
            deferredTick=Math.Max(deferredTick,tick);return true;
        }
        internal bool TryRecover(UnitSnapshot before,Func<bool> synchronousNativeMutation,long tick,
            out WorkerRecoveryOutcome outcome,out string reason)
            => CoordinateRecovery(before,synchronousNativeMutation,tick,false,out outcome,out reason);
        // Worst case: one same-tile step normalization, one original-goal probe,
        // one fallback relocation and one reissue. The bounded admission budget
        // must allow four native operations per recovery attempt.
        internal bool TryProbeOrRecover(UnitSnapshot before,Func<bool> synchronousFallbackRelocate,long tick,
            out WorkerRecoveryOutcome outcome,out string reason)
            => CoordinateRecovery(before,synchronousFallbackRelocate,tick,true,out outcome,out reason);
        private bool CoordinateRecovery(UnitSnapshot before,Func<bool> mutation,long tick,bool probe,
            out WorkerRecoveryOutcome outcome,out string reason)
        {
            coordinationDepth++;
            bool accepted;int ownedUnit=0;bool hadExternal=false;
            try{accepted=TryRecoverCore(before,mutation,tick,probe,out outcome,out reason,out ownedUnit);}
            finally
            {
                hadExternal=foreignOrders.Contains(ownedUnit) || Ledger.TryGetOwnedByUnit(ownedUnit,out var current) && lostSources.Contains(current);
                if(--coordinationDepth==0)FlushExternalOrders(tick);
            }
            if(hadExternal && ownedUnit!=0 && !Ledger.TryGetOwnedByUnit(ownedUnit,out _))
            {outcome=WorkerRecoveryOutcome.Superseded;reason="worker-native-external-order-or-identity-won";return false;}
            if(!accepted && outcome==WorkerRecoveryOutcome.OwnershipUncertain && Ledger.TryGetParkedByUnit(ownedUnit,out _))
            {outcome=WorkerRecoveryOutcome.Parked;reason="worker-foreign-order-handed-off-to-owned-park";return true;}
            return accepted;
        }
        private bool TryRecoverCore(UnitSnapshot before,Func<bool> synchronousNativeMutation,long tick,bool probeFirst,
            out WorkerRecoveryOutcome outcome,out string reason,out int ownedUnit)
        {
            outcome=WorkerRecoveryOutcome.Rejected;reason=null;ownedUnit=0;
            if(tick<0 || synchronousNativeMutation==null || !world.TryCapture(before,out var ticket,out reason))return false;
            if(ticket==null || !ticket.IsValid){reason="worker-capture-ticket-invalid";return false;}
            if(EnableLegacyCivilianRetirement && WorkerResumeLedger.IsLegacyCivilian(ticket))
            {reason="civilian-resume-policy-retired";return false;}
            // Existing ownership is not a resource shortage, even when full.
            if(Ledger.TryGetOwnedByUnit(ticket.UnitId,out _)){reason="worker-receipt-duplicate";return false;}
            if(!CanReserveSlot){outcome=WorkerRecoveryOutcome.AwaitResources;reason="worker-receipt-capacity";return false;}
            if(!Ledger.TryReserve(ticket,out var token)){reason="worker-receipt-capacity-or-duplicate";return false;}
            ownedUnit=ticket.UnitId;
            if(probeFirst)
            {
                if(!world.CanProbeOriginal(ticket))
                {
                    if(!(world is IWorkerStartedProbeWorld started))
                    {Ledger.Release(token);reason="worker-functional-probe-pose-rejected";return false;}
                    if(!AuthorityCurrent(ticket))
                    {outcome=WorkerRecoveryOutcome.OwnershipUncertain;reason="worker-started-probe-superseded";return false;}
                    var prepared=WorkerProbePreparation.OwnershipUncertain;
                    executingNativeCall=true;
                    try{prepared=started.PrepareStartedProbe(ticket,before);}catch{}
                    finally{executingNativeCall=false;}
                    if(!AuthorityCurrent(ticket))
                    {outcome=WorkerRecoveryOutcome.OwnershipUncertain;reason="worker-started-probe-superseded";return false;}
                    if(prepared==WorkerProbePreparation.Rejected)
                    {Ledger.Release(token);reason="worker-functional-probe-pose-rejected";return false;}
                    if(prepared!=WorkerProbePreparation.Ready || !world.CanProbeOriginal(ticket))
                    {outcome=WorkerRecoveryOutcome.OwnershipUncertain;reason="worker-started-probe-normalization-unconfirmed";return false;}
                }
                if(!AuthorityCurrent(ticket) || !world.TryPark(ticket,false))
                {outcome=WorkerRecoveryOutcome.OwnershipUncertain;reason="worker-functional-probe-arrival-barrier-unavailable";return false;}
                bool accepted=false;executingNativeCall=true;
                try {accepted=world.ProbeOriginal(ticket);}catch{}
                finally {executingNativeCall=false;}
                if(!AuthorityCurrent(ticket))
                {outcome=WorkerRecoveryOutcome.OwnershipUncertain;reason="worker-functional-probe-superseded";return false;}
                if(accepted)
                {
                    if(world.IsSuccessfulPublication(ticket))
                    {Ledger.Release(token);outcome=WorkerRecoveryOutcome.Resumed;reason="worker-original-goal-functional-probe-accepted";return true;}
                    outcome=WorkerRecoveryOutcome.OwnershipUncertain;reason="successful-probe-publication-contract-failed";return false;
                }
                var probeState=world.Inspect(ticket,false);
                if(probeState!=WorkerReceiptState.MatchingActive && probeState!=WorkerReceiptState.MatchingStopped && probeState!=WorkerReceiptState.MatchingParked)
                {
                    if(probeState==WorkerReceiptState.ReplacedOrGone)Ledger.Release(token);
                    outcome=WorkerRecoveryOutcome.OwnershipUncertain;reason="rejected-probe-job-or-mode-changed";return false;
                }
            }
            // No native mutation has occurred before a durable receipt slot exists.
            bool mutationSucceeded;
            executingNativeCall=true;
            try {mutationSucceeded=synchronousNativeMutation();}catch{mutationSucceeded=false;}
            finally {executingNativeCall=false;}
            if(!AuthorityCurrent(ticket))
            {reason="worker-native-operation-superseded";outcome=WorkerRecoveryOutcome.OwnershipUncertain;return false;}
            var state=world.Inspect(ticket,false);
            if(state==WorkerReceiptState.ReplacedOrGone)
            {Ledger.Release(token);reason="worker-replaced-during-native-operation";outcome=WorkerRecoveryOutcome.OwnershipUncertain;return false;}
            if(!mutationSucceeded && state==WorkerReceiptState.MatchingActive)
            {Ledger.Release(token);reason="native-operation-rejected-original-route-retained";return false;}
            if(!world.TryPark(ticket,false))
            {
                // Retain the reservation: neither saving nor new recovery is permitted
                // until the owner explicitly diagnoses this violated native contract.
                reason="worker-park-contract-failed";outcome=WorkerRecoveryOutcome.OwnershipUncertain;return false;
            }
            var immediate=Continue(ticket);
            bool resumed=immediate==WorkerResumeDecision.Release;
            if(resumed || immediate==WorkerResumeDecision.Invalidated)Ledger.Release(token);
            else if(!Ledger.CommitPark(token,tick))throw new InvalidOperationException("Reserved worker receipt could not be committed.");
            if(immediate==WorkerResumeDecision.Invalidated)
            {outcome=WorkerRecoveryOutcome.OwnershipUncertain;reason="worker-native-operation-changed-order";return false;}
            outcome=resumed?WorkerRecoveryOutcome.Resumed:WorkerRecoveryOutcome.Parked;
            reason=resumed?"worker-original-segment-resumed":"worker-owned-park-awaiting-retry";
            return true;
        }
        internal bool TryReissueOriginal(UnitSnapshot before,long tick,out WorkerRecoveryOutcome outcome,out string reason)
            => TryRecover(before,()=>true,tick,out outcome,out reason);
        private WorkerResumeDecision Continue(WorkerResumeTicket ticket)
        {
            if(!AuthorityCurrent(ticket) || EnableLegacyCivilianRetirement && WorkerResumeLedger.IsLegacyCivilian(ticket))return WorkerResumeDecision.WaitForGeometry;
            var state=world.Inspect(ticket,true);
            if(state==WorkerReceiptState.ReplacedOrGone)return WorkerResumeDecision.Invalidated;
            // Flags2/4 without a witnessed positive final-original publication can be
            // stale after an aborted builder. Rebuild the unchanged owned intent only
            // from the coherent stationary boundary, never call stale flags arrival.
            if(state==WorkerReceiptState.MatchingActive)
            {
                if(!world.TryPark(ticket,true))return WorkerResumeDecision.WaitForGeometry;
                state=WorkerReceiptState.MatchingParked;
            }
            if(state!=WorkerReceiptState.MatchingParked)return WorkerResumeDecision.WaitForGeometry;
            bool success;
            executingNativeCall=true;
            try {success=world.Reissue(ticket);}catch{success=false;}
            finally {executingNativeCall=false;}
            if(!AuthorityCurrent(ticket))return WorkerResumeDecision.WaitForGeometry;
            // A successful original-mode planner may legitimately select a moat route.
            // Never infer this permission merely from flags left after a failed call.
            if(success && world.IsSuccessfulPublication(ticket))return WorkerResumeDecision.Release;
            state=world.Inspect(ticket,true);
            if(state==WorkerReceiptState.ReplacedOrGone)return WorkerResumeDecision.Invalidated;
            if(!world.TryPark(ticket,true))return WorkerResumeDecision.WaitForGeometry;
            return WorkerResumeDecision.Retry;
        }
        internal int Tick(long tick,int visitBudget,int nativeAttempts)
        {
            coordinationDepth++;
            try{FlushExternalOrders(tick);return Ledger.Drain(tick,visitBudget,nativeAttempts,Continue);}
            finally{if(--coordinationDepth==0)FlushExternalOrders(tick);}
        }
        // Only a completed EXTERNAL order publication is authoritative here. No polling,
        // native writes or restoration of the receipt's previous goal is performed.
        internal bool ObserveOrderPost(int unitId,long tick,long nativeResult)
        {
            if(tick<0 || !Ledger.TryGetOwnedByUnit(unitId,out var ticket))return false;
            if(EnableLegacyCivilianRetirement && WorkerResumeLedger.IsLegacyCivilian(ticket))
            {
                // Called by the actual final-original receipt, before legacy Post.
                // Persist success until a non-reentrant ledger boundary; a later
                // foreign flags5 must not be mistaken for our historical barrier.
                try{if(legacyPublication.IsPublishedOrder(ticket,nativeResult))publishedLegacyOrders.Add(ticket);}catch{}
            }
            abortedOrders.Remove(unitId);foreignOrders.Add(unitId);deferredOrders[unitId]=nativeResult;deferredTick=Math.Max(deferredTick,tick);
            // The public order observer excludes our own origin AND scope. Foreign
            // nested orders must survive the native call and ledger-drain boundaries.
            return true;
        }
        private bool ReconcileOrder(int unitId,long tick,long nativeResult)
        {
            if(!Ledger.TryGetOwnedByUnit(unitId,out var ticket)){foreignOrders.Remove(unitId);return true;}
            // Migration, not job replay, resolves historical civilian barriers.
            if(EnableLegacyCivilianRetirement && WorkerResumeLedger.IsLegacyCivilian(ticket))
            {
                if(publishedLegacyOrders.Contains(ticket))
                {if(!Ledger.ReleaseOwned(ticket))return false;publishedLegacyOrders.Remove(ticket);}
                foreignOrders.Remove(unitId);return true;
            }
            var observation=world.ObserveOrderPost(ticket,nativeResult,out var replacement);
            bool reconciled=observation==WorkerOrderObservation.Release?Ledger.ReleaseOwned(ticket):
                observation==WorkerOrderObservation.Rebind && Ledger.HandoffOwned(ticket,replacement,tick);
            if(reconciled)foreignOrders.Remove(unitId);
            return reconciled;
        }
        private void FlushExternalOrders(long boundaryTick)
        {
            if(deferredOrders.Count==0 && abortedOrders.Count==0 && lostSources.Count==0)return;
            foreach(var lost in lostSources)Ledger.ReleaseOwned(lost);
            lostSources.Clear();
            var pending=new List<KeyValuePair<int,long>>(deferredOrders);deferredOrders.Clear();long tick=Math.Max(deferredTick,boundaryTick);deferredTick=0;
            // At most one entry per owned receipt; no native call or unit-wide scan.
            foreach(var entry in pending)if(!ReconcileOrder(entry.Key,tick,entry.Value))deferredOrders[entry.Key]=entry.Value;
            var aborted=new List<int>(abortedOrders);abortedOrders.Clear();
            foreach(int id in aborted)
            {
                foreignOrders.Remove(id);
                if(!Ledger.TryGetOwnedByUnit(id,out var ticket) || Ledger.TryGetParkedByUnit(id,out _))continue;
                if(EnableLegacyCivilianRetirement && WorkerResumeLedger.IsLegacyCivilian(ticket))continue;
                var state=world.Inspect(ticket,true);
                bool barrier=state==WorkerReceiptState.MatchingParked ||
                    world is IWorkerResumeBarrierView view && view.HasOwnedArrivalBarrier(ticket);
                if(!barrier && (state==WorkerReceiptState.MatchingActive || state==WorkerReceiptState.MatchingStopped))
                    barrier=world.TryPark(ticket,true);
                if(barrier)Ledger.HandoffOwned(ticket,ticket,tick);
            }
        }
        internal void MarkWorldMutation()=>Ledger.MarkWorldMutation();
        internal void Reset(){Ledger.Reset();deferredOrders.Clear();foreignOrders.Clear();abortedOrders.Clear();lostSources.Clear();publishedLegacyOrders.Clear();deferredTick=0;}
    }
}
