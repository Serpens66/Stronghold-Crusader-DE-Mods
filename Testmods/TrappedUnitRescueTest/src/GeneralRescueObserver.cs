using System;
using System.Collections.Generic;
using System.Threading;
using APIShared.WorldMutations;
using APIShared.Simulation;

namespace TrappedUnitRescueTest
{
    // The production coordinator and offline fixture share these native access boundaries.
    internal interface IRescueMutationWorld
    {
        bool TryCapture(WorldMutationContext context,out List<UnitSnapshot> before,out string reason);
        bool HasChangedTrapCandidate(UnitSnapshot before);
        bool TryRescue(UnitSnapshot before,string cause,bool resume,out RescueResult result);
        bool TryGetWorld(out long session,out long generation);
        bool TryGetTick(out int tick);
    }
    // Null means an unknown write envelope; empty means proved no geometry writes.
    // A complete envelope survives failed occupant discovery and zero occupants.
    internal interface IRescueMutationFootprintWorld : IRescueMutationWorld
    {
        bool TryCapture(WorldMutationContext context,out List<UnitSnapshot> before,out IReadOnlyList<RescueTile> plannedWrites,out string reason);
    }
    internal sealed class NativeRescueMutationWorld : IRescueMutationFootprintWorld
    {
        private readonly RescueRuntime runtime;
        private readonly NativeMutationFootprints footprints;
        internal NativeRescueMutationWorld(RescueRuntime runtime)
        {
            this.runtime=runtime ?? throw new ArgumentNullException(nameof(runtime));
            footprints=new NativeMutationFootprints(runtime.Native,new RescueFootprintBudget(2048,131072));
        }
        public bool TryCapture(WorldMutationContext context,out List<UnitSnapshot> before,out string reason)
            =>TryCapture(context,out before,out _,out reason);
        public bool TryCapture(WorldMutationContext context,out List<UnitSnapshot> before,out IReadOnlyList<RescueTile> plannedWrites,out string reason)
        {
#if SERP_DEVDIAG
            long diagnosticStarted=0;
            Shared.DevDiag.BeginBatch();
            Shared.DevDiag.Start(ref diagnosticStarted);
            try
            {
#endif
            before=null;plannedWrites=null;
            bool planned;List<RescueTile> tiles;int owner;
#if SERP_DEVDIAG
            long planStarted=0;Shared.DevDiag.Start(ref planStarted);
            try
            {
#endif
                planned=footprints.TryPlan(context,out tiles,out owner,out reason);
#if SERP_DEVDIAG
            }
            finally{Shared.DevDiag.StopSpike(ref planStarted,"rescue.capture.plan",2000);}
#endif
            if(!planned)return false;
            Shared.DevDiag.Count("rescue.capture.envelopeTiles",tiles.Count);
            plannedWrites=tiles;
            if(tiles.Count==0){before=new List<UnitSnapshot>();return true;}
            if(!runtime.TryCaptureMutation(tiles,out before,out reason))return false;
            before.RemoveAll(unit=>unit.Unsupported!=null || owner>0 && unit.Owner!=owner);
            return true;
#if SERP_DEVDIAG
            }
            finally
            {
                Shared.DevDiag.StopSpike(ref diagnosticStarted,"rescue.capture.total.inclusive",2000);
                Shared.DevDiag.EndBatch();
            }
#endif
        }
        public bool HasChangedTrapCandidate(UnitSnapshot before)=>runtime.HasChangedTrapCandidate(before);
        public bool TryRescue(UnitSnapshot before,string cause,bool resume,out RescueResult result)=>runtime.TryRescue(before,cause,resume,out result);
        public bool TryGetWorld(out long session,out long generation)=>WorldMutationEvents.TryGetCurrentWorld(out session,out generation);
        public bool TryGetTick(out int tick)=>SimulationTickEvents.TryGetCurrentTick(out tick);
    }

    // Effective sources supply old geometry; the next admitted simulation callback owns
    // recovery. Child completions never mutate inside construction or a worker handler.
    internal sealed partial class GeneralRescueObserver : IWorldMutationObserver
    {
        private sealed class Captured
        {
            internal UnitSnapshot Before;
            internal long Order;
            internal string Source;
        }
        private sealed class Batch
        {
            internal long Session,World;
            internal bool Invalid;
            internal readonly SortedDictionary<int,Captured> Units=new SortedDictionary<int,Captured>();
            internal readonly HashSet<int> RetiredUnits=new HashSet<int>();
        }
        private readonly IRescueMutationWorld access;
        private readonly Action<bool> suppression;
        private readonly Action<string> report;
        private readonly Dictionary<long,Batch> batches=new Dictionary<long,Batch>();
        private readonly RescueCandidateQueue<UnitSnapshot> queue=new RescueCandidateQueue<UnitSnapshot>();
        private readonly long[] orders=new long[NativeRelocation.MaximumUnitId+1];
        private readonly int[] capturedInterest=new int[NativeRelocation.MaximumUnitId+1];
        private HashSet<int> stagedIds=new HashSet<int>();
        private long session,world,captures,attempts,rescues;
        private int busy,reported,lastDrainTick;
        private bool drainedTick;
        private bool failedBatchOverflow;
        private RescuePendingCandidate<UnitSnapshot>[] staged=Array.Empty<RescuePendingCandidate<UnitSnapshot>>();
        internal long CaptureCount=>Interlocked.Read(ref captures);
        internal long AttemptCount=>Interlocked.Read(ref attempts);
        internal long RescueCount=>Interlocked.Read(ref rescues);
        internal bool IsActive=>Interlocked.Read(ref session)!=0;
        internal bool IsRecovering=>Volatile.Read(ref busy)!=0;
        internal int PendingCount=>queue.Count+staged.Length+GeometryCount;
        internal bool HasEvidence=>PendingCount!=0 || batches.Count!=0;
        internal bool IsInterestedInOrder(int id)=>id>0 && id<orders.Length &&
            (capturedInterest[id]!=0 || queue.ContainsUnit(id) || stagedIds.Contains(id) || IsReplanInterested(id));
        internal Action WorldMutated { get; set; }
        internal GeneralRescueObserver(RescueRuntime runtime,Action<bool> suppression,Action<string> report)
            : this(new NativeRescueMutationWorld(runtime),suppression,report) { }
        internal GeneralRescueObserver(IRescueMutationWorld access,Action<bool> suppression,Action<string> report)
        {
            this.access=access ?? throw new ArgumentNullException(nameof(access));
            this.suppression=suppression;this.report=report;
        }
        internal void SetSession(long id)
        {
            if(Interlocked.Exchange(ref session,id)==id)return;
            ResetWorld(0);
        }
        // Save callers own the engine monitor. Unbound loaded records are managed data and
        // remain serializable while the feature is disabled; no native reads are needed then.
        internal bool TryCapturePending(out RescuePendingCandidate<UnitSnapshot>[] records)
        {
            records=null;
            if(HasUnsettledReplans)return false;
            if(!queue.HasWork){records=(RescuePendingCandidate<UnitSnapshot>[])staged.Clone();return true;}
            if(!IsCurrent(session,world) || !access.TryGetTick(out int tick) || !queue.TryCopyPending(tick,out var live))return false;
            records=new RescuePendingCandidate<UnitSnapshot>[staged.Length+live.Length];
            Array.Copy(staged,records,staged.Length);Array.Copy(live,0,records,staged.Length,live.Length);
            return true;
        }
        internal void StagePending(RescuePendingCandidate<UnitSnapshot>[] records)
        {
            stagedIds.Clear();
            if(records==null || records.Length+GeometryCount>512){staged=Array.Empty<RescuePendingCandidate<UnitSnapshot>>();return;}
            var seen=new HashSet<int>();
            foreach(var record in records)
                if(record.Snapshot==null || record.Snapshot.Id<1 || record.Snapshot.Id>=orders.Length || record.Snapshot.GlobalId==0 ||
                    string.IsNullOrEmpty(record.Source) || record.Age<0 || record.Age>=80 || record.Attempts<0 || record.Attempts>=4 ||
                    record.RetryIn<0 || record.RetryIn>1 || !seen.Add(record.Snapshot.Id))
                {staged=Array.Empty<RescuePendingCandidate<UnitSnapshot>>();ReportBounded("invalid-staged-candidates");return;}
            staged=(RescuePendingCandidate<UnitSnapshot>[])records.Clone();
            stagedIds=seen;
        }
        // Inactive paused loads can be re-saved unchanged. Actual simulation progress
        // retires that historical proof without reading or mutating native game state.
        internal void ExpireInactivePending()
        {if(!IsActive){staged=Array.Empty<RescuePendingCandidate<UnitSnapshot>>();stagedIds.Clear();ResetGeometry(true);}}
        internal void ClearPending()
        {
            staged=Array.Empty<RescuePendingCandidate<UnitSnapshot>>();stagedIds.Clear();ResetGeometry(true);ResetWorld(0);
        }
        private bool RetireStaged(int id,uint identity=0)
        {
            if(!stagedIds.Contains(id))return false;
            bool same=false;int removed=0;
            foreach(var record in staged)if(record.Snapshot.Id==id){removed++;same|=identity!=0 && record.Snapshot.GlobalId==identity;}
            if(removed==0)return false;
            var next=new RescuePendingCandidate<UnitSnapshot>[staged.Length-removed];int index=0;
            foreach(var record in staged)if(record.Snapshot.Id!=id)next[index++]=record;
            staged=next;stagedIds.Remove(id);return same;
        }
        private void RestoreStaged(int tick)
        {
            var records=staged;staged=Array.Empty<RescuePendingCandidate<UnitSnapshot>>();stagedIds.Clear();
            for(int index=0;index<records.Length;index++)
            {
                var record=records[index];var before=record.Snapshot;
                if(orders[before.Id]!=0)continue;
                queue.OfferRestored(new RescueIdentity(session,world,before.Id,before.GlobalId,0),long.MaxValue-index,record,tick);
            }
        }
        private void ResetWorld(long generation)
        {
            world=generation;batches.Clear();queue.Reset(session,generation);drainedTick=false;failedBatchOverflow=false;
            Array.Clear(orders,0,orders.Length);reported=0;
            Array.Clear(capturedInterest,0,capturedInterest.Length);
            ResetGeometry(false);
        }
        private bool IsCurrent(long expectedSession,long expectedWorld)=>IsActive && session==expectedSession && world==expectedWorld &&
            access.TryGetWorld(out long currentSession,out long currentWorld) && currentSession==session && currentWorld==world;
        private void ReportBounded(string message)
        {if(reported>=32)return;reported++;try{report?.Invoke(message);}catch{}}
        // Called for every external effective move attempt, before it can replace the route.
        internal long GetOrderSerial(int id)=>id>0 && id<orders.Length?orders[id]:-1;
        // Our synchronous recovery calls are excluded; a cancelled attempt may conservatively
        // retire old proof, but can never authorize restoring an obsolete target.
        internal void ObserveOrder(int id,bool independentlyExternal=false)
        {
            if(IsRecovering && !independentlyExternal || id<1 || id>=orders.Length)return;
            if(!IsActive){RetireStaged(id);RetireStagedGeometry(id);return;}
            RetainBeforeOrder(id);RetireStaged(id);
            if(orders[id]==long.MaxValue){queue.InvalidateUnit(id);DropGeometry(id);return;}
            orders[id]++;queue.InvalidateUnit(id);
        }
        public IWorldMutationInvocation TryBegin(WorldMutationContext context)
        {
            if(!IsActive || IsRecovering || context==null || context.SessionId!=session || context.WorldGeneration==0)return null;
            if(!access.TryGetWorld(out long liveSession,out long liveWorld) || liveSession!=context.SessionId || liveWorld!=context.WorldGeneration)return null;
            if(world!=liveWorld)ResetWorld(liveWorld);
            if(failedBatchOverflow)return null;
            List<UnitSnapshot> before=null;IReadOnlyList<RescueTile> writes=null;string reason=null;bool captured=false;
            try
            {
                captured=access is IRescueMutationFootprintWorld withFootprint?
                    withFootprint.TryCapture(context,out before,out writes,out reason):access.TryCapture(context,out before,out reason);
            }
            catch{reason="mutation-capture-exception";}
            // This runs before the original even when no local actor was found.
            // Active captures belong to this compound event, not historical caches.
            var retired=RetireHistoricalGeometry(writes,out bool completeEnvelope);
            if(!captured || before==null || !completeEnvelope)
            {
                if(!batches.TryGetValue(context.BatchId,out Batch partial))
                {
                    if(batches.Count>=64)
                    {
                        // A failed first parent cannot disappear merely because the
                        // bounded batch ledger is full. Fail closed until world reset.
                        failedBatchOverflow=true;RetireAllHistoricalGeometry();
                        foreach(var existing in batches.Values)existing.Invalid=true;
                        ReportBounded("failed-mutation-batch-capacity");return null;
                    }
                    partial=new Batch{Session=context.SessionId,World=context.WorldGeneration};batches.Add(context.BatchId,partial);
                }
                partial.Invalid=true;
                ReportBounded("mutation capture deferred source="+context.Source+" reason="+reason);return null;
            }
            if(before.Count==0 && retired.Count==0)return null;
            if(!batches.TryGetValue(context.BatchId,out Batch batch))
            {
                if(batches.Count>=64){ReportBounded("mutation-batch-capacity");return null;}
                batch=new Batch{Session=context.SessionId,World=context.WorldGeneration};batches.Add(context.BatchId,batch);
            }
            if(batch.Session!=context.SessionId || batch.World!=context.WorldGeneration){batch.Invalid=true;return null;}
            foreach(int id in retired)
            {
                if(batch.Units.Remove(id))capturedInterest[id]--;
                batch.RetiredUnits.Add(id);
            }
            if(before.Count==0)return null;
            foreach(var unit in before)
            {
                if(batch.RetiredUnits.Contains(unit.Id))continue;
                if(batch.Units.TryGetValue(unit.Id,out Captured previous))
                {
                    // Keep the earliest old reachable fields. Later nested observations may
                    // only add affected endpoints of the same still-unmoved identity/order.
                    if(previous.Before.GlobalId!=unit.GlobalId || previous.Order!=orders[unit.Id] ||
                        previous.Before.TileId!=unit.TileId || previous.Before.NextTileId!=unit.NextTileId)
                    {
                        // A new order or reused actor retires only its own old proof.
                        // Unchanged members still have complete independent geometry.
                        batch.Units.Remove(unit.Id);capturedInterest[unit.Id]--;batch.RetiredUnits.Add(unit.Id);continue;
                    }
                    previous.Before.AffectedCurrent|=unit.AffectedCurrent;
                    previous.Before.AffectedNext|=unit.AffectedNext;
                }
                else if(batch.Units.Count<512)
                {batch.Units.Add(unit.Id,new Captured{Before=unit,Order=orders[unit.Id],Source=context.Source.ToString()});capturedInterest[unit.Id]++;}
                else batch.Invalid=true;
            }
            Interlocked.Increment(ref captures);
            return new Completion(context,context.SessionId,id=>IsCurrent(id,context.WorldGeneration),()=>{},
                () => batch.Invalid=true);
        }
        public void OnBatchCompleted(WorldMutationBatchCompletion completion)
        {
            if(IsRecovering)return;
            if(completion!=null && completion.ReturnedNormally && !completion.ContainsOriginalFailure && completion.IsCurrentWorld &&
                IsCurrent(completion.SessionId,completion.WorldGeneration))
            {
                try{WorldMutated?.Invoke();}catch{ReportBounded("world-mutation callback failed");}
            }
            if(completion==null || !batches.TryGetValue(completion.BatchId,out Batch batch))return;
            if(completion.SessionId!=batch.Session || completion.WorldGeneration!=batch.World)return;
            batches.Remove(completion.BatchId);
            foreach(int id in batch.Units.Keys)capturedInterest[id]--;
            if(batch.Invalid || !completion.ReturnedNormally || completion.ContainsOriginalFailure || !completion.IsCurrentWorld ||
                !IsCurrent(batch.Session,batch.World) || !access.TryGetTick(out int tick))return;
            foreach(var entry in batch.Units.Values)
            {
                var before=entry.Before;if(!access.HasChangedTrapCandidate(before))continue;
                // A fresh geometry cause cannot silently overwrite unbound saved proof.
                if(RetireStaged(before.Id,before.GlobalId)){queue.InvalidateUnit(before.Id);continue;}
                if(RetireStagedGeometry(before.Id,before.GlobalId)){queue.InvalidateUnit(before.Id);DropGeometry(before.Id);continue;}
                DropGeometry(before.Id);
                if(entry.Order!=orders[before.Id])
                {
                    queue.InvalidateUnit(before.Id);
                    if(RescueGeometryWitness.TryCapture(before,out var witness))RetainGeometry(witness,entry.Source,tick,0,0,completion.BatchId);
                    continue;
                }
                if(PendingCount>=512){queue.InvalidateUnit(before.Id);ReportBounded("mutation-pending-capacity");continue;}
                var identity=new RescueIdentity(batch.Session,batch.World,before.Id,before.GlobalId,entry.Order);
                var outcome=queue.Offer(identity,completion.BatchId,entry.Source,before,tick);
                if(outcome!=RescueOfferResult.Added && outcome!=RescueOfferResult.Duplicate)
                    ReportBounded("mutation candidate deferred unit="+before.Id+" reason="+outcome);
            }
        }
        internal void Tick(int tick)
        {
            if(!IsActive){ExpireInactivePending();return;}
            if(PendingCount==0 || drainedTick && lastDrainTick==tick || Interlocked.CompareExchange(ref busy,1,0)!=0)return;
            try
            {
                if(staged.Length!=0 || stagedGeometry.Length!=0)
                {
                    if(!access.TryGetWorld(out long liveSession,out long liveWorld) || liveSession!=session || liveWorld==0)return;
                    if(world!=liveWorld)ResetWorld(liveWorld);
                    RestoreStaged(tick);
                    RestoreGeometry(tick);
                }
                if(!IsCurrent(session,world)){ResetWorld(0);return;}
                drainedTick=true;lastDrainTick=tick;
#if SERP_DEVDIAG
            long replanStarted=0;
            Shared.DevDiag.Start(ref replanStarted);
            try
            {
#endif
                ProcessReplans(tick,32);
#if SERP_DEVDIAG
            }
            finally
            {
                Shared.DevDiag.StopSpike(ref replanStarted,"rescue.tick.replans",2000);
            }
#endif
                if(!queue.HasWork)return;
                suppression?.Invoke(true);
#if SERP_DEVDIAG
            long recoveryStarted=0;
            Shared.DevDiag.Start(ref recoveryStarted);
            try
            {
#endif
                queue.Drain(tick,32,8,candidate=>
                {
                    if(!IsCurrent(candidate.Identity.SessionId,candidate.Identity.WorldGeneration))
                    {ResetWorld(0);return RescueQueueDecision.Reject;}
                    if(candidate.Identity.OrderSerial!=orders[candidate.Identity.UnitId])return RescueQueueDecision.Reject;
                    Interlocked.Increment(ref attempts);
                    if(access.TryRescue(candidate.Snapshot,"observed-"+candidate.Source,true,out RescueResult result))
                    {Interlocked.Increment(ref rescues);return RescueQueueDecision.Complete;}
                    return result.Status==RescueStatus.NoCandidate ? RescueQueueDecision.Retry : RescueQueueDecision.Reject;
                });
#if SERP_DEVDIAG
            }
            finally
            {
                Shared.DevDiag.StopSpike(ref recoveryStarted,"rescue.tick.recovery",2000);
            }
#endif
            }
            finally{try{suppression?.Invoke(false);}finally{Volatile.Write(ref busy,0);}}
        }
        // The real invocation and the offline fixture share this single-use identity gate.
        internal sealed class Completion : IWorldMutationInvocation
        {
            private readonly object context;
            private readonly long session;
            private readonly Func<long,bool> current;
            private readonly Action after,failed;
            private int completed;
            internal Completion(object context,long session,Func<long,bool> current,Action after,Action failed=null)
            {this.context=context;this.session=session;this.current=current;this.after=after;this.failed=failed;}
            public void Complete(WorldMutationCompletion completion)=>Accept(completion?.Context,completion!=null && completion.OriginalInvoked && completion.OriginalReturned);
            internal void Accept(object actual,bool returned)
            {
                if(Interlocked.Exchange(ref completed,1)!=0)return;
                if(!ReferenceEquals(context,actual) || !returned || !current(session)){failed?.Invoke();return;}
                after();
            }
        }
    }
}
