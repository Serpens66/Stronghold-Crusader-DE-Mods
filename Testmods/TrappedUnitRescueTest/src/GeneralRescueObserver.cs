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
    // Geometry-only reentry: no occupant discovery or unit snapshots.
    internal interface IRescueMutationEnvelopeWorld
    {
        bool TryPlan(WorldMutationContext context,out IReadOnlyList<RescueTile> plannedWrites,out string reason);
    }
    // Optional, read-only admission for a known identity whose old intent ended.
    // No job/route reconstruction, discovery or candidate BFS belongs here.
    internal interface IRescuePassiveIdleWorld
    {
        bool TryCaptureIdle(int unitId,out UnitSnapshot idle);
        bool IsCurrentIdle(UnitSnapshot idle);
        bool IsKnownIdleTransition(RescueGeometryWitness witness);
    }
    internal sealed class NativeRescueMutationWorld : IRescueMutationFootprintWorld, IRescueMutationEnvelopeWorld, IRescuePassiveIdleWorld
    {
        private readonly RescueRuntime runtime;
        private readonly NativeMutationFootprints footprints;
        internal NativeRescueMutationWorld(RescueRuntime runtime)
        {
            this.runtime=runtime ?? throw new ArgumentNullException(nameof(runtime));
            footprints=new NativeMutationFootprints(runtime.Native,new RescueFootprintBudget(2048,131072));
        }
        public bool TryPlan(WorldMutationContext context,out IReadOnlyList<RescueTile> plannedWrites,out string reason)
        {
            bool planned=footprints.TryPlan(context,out var tiles,out _,out reason);
            plannedWrites=planned?tiles:null;return planned;
        }
        public bool TryCapture(WorldMutationContext context,out List<UnitSnapshot> before,out string reason)
            =>TryCapture(context,out before,out _,out reason);
        public bool TryCapture(WorldMutationContext context,out List<UnitSnapshot> before,out IReadOnlyList<RescueTile> plannedWrites,out string reason)
        {
#if SERP_DEVDIAG
            long diagnosticStarted=0;
            var captureAllocation=RescueCaptureDiagnostics.Begin(context==null?default:context.Source);
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
#if SERP_DEVDIAG
            RescueCaptureDiagnostics.Owner(owner);
#endif
            if(tiles.Count==0){before=new List<UnitSnapshot>();return true;}
            if(!runtime.TryCaptureMutation(tiles,out before,out reason,owner,compactGeometry:true))return false;
            before.RemoveAll(unit=>unit.Unsupported!=null || owner>0 && unit.Owner!=owner);
            return true;
#if SERP_DEVDIAG
            }
            finally
            {
                RescueCaptureDiagnostics.End(ref captureAllocation);
                RescueCaptureDiagnostics.StopCapture(ref diagnosticStarted);
                RescueCaptureDiagnostics.PublishCapture();
                Shared.DevDiag.EndBatch();
            }
#endif
        }
        public bool HasChangedTrapCandidate(UnitSnapshot before)=>runtime.HasChangedTrapCandidate(before);
        public unsafe bool TryCaptureIdle(int unitId,out UnitSnapshot idle)
        {
            idle=null;var n=runtime.Native;
            if(!n.TryUnit(unitId,out var u) || u->r_PathPlanStateBitFlags!=0 ||
                !CommandJourneySnapshot.IsIndependentIdlePhase(u->r_UnitChimp,u->r_AIState))return false;
            var snapshot=runtime.SnapshotUnit(unitId,true,false);
            if(!IsCurrentIdle(snapshot))return false;
            idle=snapshot;return true;
        }
        public unsafe bool IsCurrentIdle(UnitSnapshot b)
        {
            var n=runtime.Native;
            if(!RescueGeometryWitness.IsIndependentIdleSnapshot(b) || !n.TryUnit(b.Id,out var u) || !IsLivingIdleIdentity(b,u) ||
                u->r_TribeId!=b.Tribe ||
                !n.TryTribeGlobal(b.Tribe,out uint global) || global!=b.TribeGlobalId || b.Tribe!=0 && n.JobDword(u,0x93C)!=global ||
                u->r_CurrentTilePositionX!=b.X || u->r_CurrentTilePositionY!=b.Y || (int)u->r_CurrentPositionTileId!=b.TileId ||
                u->r_TargetTilePositionX2!=b.X || u->r_TargetTilePositionY2!=b.Y || u->r_PathPlanStateBitFlags!=0 ||
                !NativeRelocation.IsNormalizedPose(u,n.Tile(b.X,b.Y),n.JobDword(u,0x740)) || u->r_IsKilledByProjectile!=0)return false;
            if(!HasOrdinaryIdleMode(u))return false;
            IntPtr pointer=(IntPtr)u;
            return b.Command.Matches(u->r_UnitChimp,u->r_AIState,at=>n.JobWord((SHCDESE.Interop.GameUnit*)pointer,at),
                at=>n.JobDword((SHCDESE.Interop.GameUnit*)pointer,at));
        }
        // Health is current transaction data, not a lifetime serial. Runtime's
        // WithCurrentHealth preserves it without healing or renewing the proof.
        internal static unsafe bool IsLivingIdleIdentity(UnitSnapshot b,SHCDESE.Interop.GameUnit* u)=>
            b!=null && APIShared.UnitAccess.IsReallyAlive(u) && u->r_CurrentHealth>0 &&
            u->r_GlobalId==b.GlobalId && (int)u->r_UnitChimp==b.Type && u->r_AIState==b.AIState && u->r_ControllableForPlayerId==b.Owner;
        public unsafe bool IsKnownIdleTransition(RescueGeometryWitness w)
        {
            var n=runtime.Native;
            if(w==null || w.Type!=(int)SHCDESE.Interop.eChimps.CHIMP_TYPE_SPEARMAN || !n.TryUnit(w.UnitId,out var u) ||
                u->r_GlobalId!=w.GlobalId || (int)u->r_UnitChimp!=w.Type || u->r_ControllableForPlayerId!=w.Owner ||
                u->r_AIState!=101 || u->r_PathPlanStateBitFlags!=0 || u->r_IsKilledByProjectile!=0 || u->r_CurrentHealth==0 ||
                !NativeRelocation.IsNormalizedPose(u,n.Tile(u->r_CurrentTilePositionX,u->r_CurrentTilePositionY),n.JobDword(u,0x740)) ||
                u->r_TargetTilePositionX2!=u->r_CurrentTilePositionX || u->r_TargetTilePositionY2!=u->r_CurrentTilePositionY)return false;
            int tile=(int)u->r_CurrentPositionTileId,x=u->r_CurrentTilePositionX,y=u->r_CurrentTilePositionY;
            if(!w.Current.Matches(x,y,tile) && !(w.HasAcceptedNext && w.Next.Matches(x,y,tile)))return false;
            if(!HasOrdinaryIdleMode(u))return false;
            foreach(int offset in IdleTargetWords)if(n.Word(u,offset)!=0)return false;
            foreach(int offset in IdleTargetDwords)if(n.JobDword(u,offset)!=0)return false;
            ushort command=n.Word(u,0x9F4);
            return command==(ushort)SHCDESE.Interop.Enums.TribeAICommand.Unknown0 || command==(ushort)SHCDESE.Interop.Enums.TribeAICommand.MoveHerePosition;
        }
        private static readonly int[] IdleTargetWords={0x996,0x99C,0x9F6},IdleTargetDwords={0x998,0x6F8,0x9F8,0xA00};
        private static readonly int[] IdleEmptyWords={0x6EC,0x708,0x8EC,0x8F6,0x8F8,0x9B0,0x9C8};
        internal static unsafe bool HasOrdinaryIdleMode(SHCDESE.Interop.GameUnit* u)
        {
            if(u==null)return false;
            // 8FC is SelectionAllowed:143400 writes1 before every state. It is
            // neither a portal nor an intent gate and19BA40 does not change it.
            foreach(int offset in IdleEmptyWords)
                if(*(ushort*)((byte*)u+offset-0x65C)!=0)return false;
            return true;
        }
        public bool TryRescue(UnitSnapshot before,string cause,bool resume,out RescueResult result)
        {
            // Recheck a joined idle receipt immediately before the existing action.
            // Runtime then retains its own strict semantic, crew and pose checks.
            if(before?.GeometryWitness!=null && before.Flags==0 && !IsCurrentIdle(before))
            {result=new RescueResult{Status=RescueStatus.Unsupported,Detail="fresh-idle-authority-changed"};return false;}
            return runtime.TryRescue(before,cause,resume,out result);
        }
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
        internal const int MaximumPending=NativeRelocation.MaximumUnitId;
        private readonly RescueCandidateQueue<UnitSnapshot> queue=new RescueCandidateQueue<UnitSnapshot>(capacity:MaximumPending,
            maximumUnitId:NativeRelocation.MaximumUnitId,lifetimeMode:RescueQueueLifetimeMode.ActiveWindow);
        private readonly long[] orders=new long[NativeRelocation.MaximumUnitId+1];
        private readonly int[] capturedInterest=new int[NativeRelocation.MaximumUnitId+1];
        private long session,world,captures,attempts,rescues,removals;
        private int busy,reported,lastDrainTick;
        private readonly Dictionary<int,int> externalOrders=new Dictionary<int,int>();
        private int externalOrderDepth;
        private bool drainedTick;
        private bool failedBatchOverflow;
        private readonly Dictionary<int,RescuePendingCandidate<UnitSnapshot>> staged=new Dictionary<int,RescuePendingCandidate<UnitSnapshot>>();
        internal long CaptureCount=>Interlocked.Read(ref captures);
        internal long AttemptCount=>Interlocked.Read(ref attempts);
        internal long RescueCount=>Interlocked.Read(ref rescues);
        internal long RemovalCount=>Interlocked.Read(ref removals);
        internal bool IsActive=>Interlocked.Read(ref session)!=0;
        internal bool IsRecovering=>Volatile.Read(ref busy)!=0;
        internal bool HasOpenExternalCalls=>externalOrderDepth!=0;
        internal int PendingCount=>queue.Count+staged.Count+GeometryCount;
        internal bool HasEvidence=>PendingCount!=0 || batches.Count!=0 || externalOrderDepth!=0;
        internal bool IsInterestedInOrder(int id)=>id>0 && id<orders.Length &&
            (externalOrders.ContainsKey(id) || capturedInterest[id]!=0 || queue.ContainsUnit(id) || staged.ContainsKey(id) || IsReplanInterested(id));
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
            records=null;if(HasUnsettledReplans || IsRecovering)return false;
            var result=new List<RescuePendingCandidate<UnitSnapshot>>(staged.Values);
            if(queue.HasWork || geometry.Count!=0)
            {
                if(!IsCurrent(session,world) || !access.TryGetTick(out int tick) || !queue.TryCopyPending(tick,out var live))return false;
                result.AddRange(live);
                if(!TryAppendFreshPending(tick,result))return false;
            }
            result.Sort((a,b)=>a.Snapshot.Id.CompareTo(b.Snapshot.Id));records=result.ToArray();return true;
        }
        internal void StagePending(RescuePendingCandidate<UnitSnapshot>[] records)
        {
            InvalidateFreshSave();staged.Clear();
            if(records==null || records.Length+queue.Count+GeometryCount>MaximumPending)return;
            var accepted=new Dictionary<int,RescuePendingCandidate<UnitSnapshot>>();
            foreach(var record in records)
            {
                var before=record.Snapshot;
                bool age=record.LifetimeMode==RescueQueueLifetimeMode.AbsoluteSourceAge?
                    record.Age>=0 && record.Age<80 && record.ActiveAge==-1 && !record.AwaitingResources:
                    record.LifetimeMode==RescueQueueLifetimeMode.ActiveWindow && record.Age>=0 && record.ActiveAge>=-1 &&
                    record.ActiveAge<80 && record.ActiveAge<=record.Age && (record.ActiveAge>=0 || record.Attempts==0);
                if(before==null || before.Id<1 || before.Id>=orders.Length || before.GlobalId==0 ||
                    string.IsNullOrEmpty(record.Source) || !age || record.Attempts<0 || record.Attempts>=4 || record.RetryIn<0 || record.RetryIn>1 ||
                    accepted.ContainsKey(before.Id) || queue.ContainsUnit(before.Id) || geometry.ContainsKey(before.Id) || stagedGeometry.ContainsKey(before.Id))
                {ReportBounded("invalid-staged-candidates");return;}
                accepted.Add(before.Id,record);
            }
            foreach(var entry in accepted)staged.Add(entry.Key,entry.Value);
        }
        // Simulation progress OFF invalidates unbound history without game reads.
        internal void ExpireInactivePending()
        {if(!IsActive){InvalidateFreshSave();staged.Clear();ResetGeometry(true);}}
        internal void ClearPending()
        {InvalidateFreshSave();staged.Clear();ResetGeometry(true);ResetWorld(0);}
        private bool RetireStaged(int id,uint identity=0)
        {
            if(!staged.TryGetValue(id,out var record))return false;
            InvalidateFreshSave();staged.Remove(id);return identity!=0 && record.Snapshot.GlobalId==identity;
        }
        private void RestoreStaged(int tick)
        {
            var records=new List<RescuePendingCandidate<UnitSnapshot>>(staged.Values);staged.Clear();InvalidateFreshSave();
            records.Sort((a,b)=>a.Snapshot.Id.CompareTo(b.Snapshot.Id));
            for(int index=0;index<records.Count;index++)
            {
                var record=records[index];var before=record.Snapshot;
                if(!before.CivilianRemoval && orders[before.Id]!=0)continue;
                queue.OfferRestored(new RescueIdentity(session,world,before.Id,before.GlobalId,before.CivilianRemoval?orders[before.Id]:0),long.MaxValue-index,record,tick,
                    before.GeometryWitness!=null && RescueGeometryWitness.IsIndependentIdleSnapshot(before));
            }
        }
        private void ResetWorld(long generation)
        {
            // Do not clear externalOrders: synchronous old-world calls must close
            // before a new world's recovery can run, including nested same-ID calls.
            InvalidateFreshSave();world=generation;batches.Clear();queue.Reset(session,generation);drainedTick=false;failedBatchOverflow=false;
            Array.Clear(orders,0,orders.Length);reported=0;
            Array.Clear(capturedInterest,0,capturedInterest.Length);
            ResetGeometry(false);
        }
        private bool IsCurrent(long expectedSession,long expectedWorld)=>IsActive && session==expectedSession && world==expectedWorld &&
            access.TryGetWorld(out long currentSession,out long currentWorld) && currentSession==session && currentWorld==world;
        private void ReportBounded(string message)
        {if(reported>=32)return;reported++;try{report?.Invoke(message);}catch{}}
        // Orders revoke command intent, not independently proved removal geometry.
        internal long GetOrderSerial(int id)=>id>0 && id<orders.Length?orders[id]:-1;
        private bool HasRemovalEvidence(int id)
        {
            if(queue.TryGetSnapshot(id,out var snapshot) && snapshot.CivilianRemoval)return true;
            return staged.TryGetValue(id,out var saved) && saved.Snapshot.CivilianRemoval;
        }
        internal void ObserveOrder(int id,bool independentlyExternal=false)
        {
            if(IsRecovering && !independentlyExternal || id<1 || id>=orders.Length)return;
            InvalidateFreshSave();
            // Only the real broker path opens a native-call scope. Existing direct
            // metadata observations remain one-shot and require no matching End.
            if(independentlyExternal)
            {
                externalOrders.TryGetValue(id,out int depth);externalOrders[id]=depth+1;externalOrderDepth++;
            }
            if(!IsActive){RetireStaged(id);RetireStagedGeometry(id);return;}
            bool removal=HasRemovalEvidence(id);
            if(!removal){RetainBeforeOrder(id);RetireStaged(id);}
            if(orders[id]==long.MaxValue)
            {if(!removal){queue.InvalidateUnit(id);DropGeometry(id);}return;}
            orders[id]++;
            if(!removal)queue.InvalidateUnit(id);
        }
        internal void EndExternalOrder(int id)
        {
            if(!externalOrders.TryGetValue(id,out int depth))return;
            if(depth==1)externalOrders.Remove(id);else externalOrders[id]=depth-1;
            externalOrderDepth--;
        }
        public IWorldMutationInvocation TryBegin(WorldMutationContext context)
        {
            if(!IsActive || context==null || context.SessionId!=session || context.WorldGeneration==0)return null;
            if(!access.TryGetWorld(out long liveSession,out long liveWorld) || liveSession!=context.SessionId || liveWorld!=context.WorldGeneration)return null;
            if(world!=liveWorld)ResetWorld(liveWorld);
            InvalidateFreshSave();
            if(IsRecovering)
            {
                // A foreign observer can build synchronously inside our native move.
                // Retire old spatial authority before that writer, but never capture
                // new units or enter rescue recursively. Missing planners fail closed.
                if(PendingCount==0)return null;
                IReadOnlyList<RescueTile> reentryWrites=null;
                try
                {
                    if(access is IRescueMutationEnvelopeWorld planner && !planner.TryPlan(context,out reentryWrites,out _))reentryWrites=null;
                }
                catch{reentryWrites=null;}
                if(!IsCurrent(context.SessionId,context.WorldGeneration))return null;
                try{RetireHistoricalGeometry(reentryWrites,out _);}
                catch{RetireAllHistoricalGeometry();ReportBounded("recovery-mutation-retirement-failed");}
                return null;
            }
            if(failedBatchOverflow)return null;
#if SERP_DEVDIAG
            long observerStarted=0;var observerAllocation=RescueCaptureDiagnostics.Read();
            Shared.DevDiag.BeginBatch();Shared.DevDiag.Start(ref observerStarted);
            try
            {
#endif
            List<UnitSnapshot> before=null;IReadOnlyList<RescueTile> writes=null;string reason=null;bool captured=false;
            try
            {
                captured=access is IRescueMutationFootprintWorld withFootprint?
                    withFootprint.TryCapture(context,out before,out writes,out reason):access.TryCapture(context,out before,out reason);
            }
            catch{reason="mutation-capture-exception";}
            // This runs before the original even when no local actor was found.
            // Active captures belong to this compound event, not historical caches.
#if SERP_DEVDIAG
            IReadOnlyList<int> retired;bool completeEnvelope;long retirementStarted=0;
            Shared.DevDiag.Start(ref retirementStarted);
            Shared.DevDiag.Count("rescue.count.retirementPending",PendingCount);
            try{retired=RetireHistoricalGeometry(writes,out completeEnvelope);Shared.DevDiag.Count("rescue.count.retiredRecords",retired.Count);}
            finally{Shared.DevDiag.StopSpike(ref retirementStarted,"rescue.observer.retirement",2000);}
#else
            var retired=RetireHistoricalGeometry(writes,out bool completeEnvelope);
#endif
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
            // The native adapter validates IDs already; keep the coordinator safe
            // for every boundary implementation before indexing its native-ID arrays.
            if(before.Count>MaximumPending){batch.Invalid=true;ReportBounded("mutation-unit-domain");return null;}
            foreach(var actor in before)
                if(actor==null || actor.Id<1 || actor.Id>=orders.Length || actor.GlobalId==0)
                {batch.Invalid=true;ReportBounded("mutation-unit-domain");return null;}
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
                    if(previous.Before.GlobalId!=unit.GlobalId ||
                        (!(previous.Before.CivilianRemoval && unit.CivilianRemoval) && previous.Order!=orders[unit.Id]) ||
                        previous.Before.TileId!=unit.TileId || previous.Before.NextTileId!=unit.NextTileId)
                    {
                        // A new order or reused actor retires only its own old proof.
                        // Unchanged members still have complete independent geometry.
                        batch.Units.Remove(unit.Id);capturedInterest[unit.Id]--;batch.RetiredUnits.Add(unit.Id);continue;
                    }
                    previous.Before.AffectedCurrent|=unit.AffectedCurrent;
                    previous.Before.AffectedNext|=unit.AffectedNext;
                }
                else if(batch.Units.Count<MaximumPending)
                {batch.Units.Add(unit.Id,new Captured{Before=unit,Order=orders[unit.Id],Source=context.Source.ToString()});capturedInterest[unit.Id]++;}
                else batch.Invalid=true;
            }
            Interlocked.Increment(ref captures);
            return new Completion(context,context.SessionId,id=>IsCurrent(id,context.WorldGeneration),()=>{},
                () => batch.Invalid=true);
#if SERP_DEVDIAG
            }
            finally
            {
                RescueCaptureDiagnostics.EndObserver(observerAllocation);
                Shared.DevDiag.StopSpike(ref observerStarted,"rescue.observer.begin.inclusive",2000);
                Shared.DevDiag.EndBatch();
            }
#endif
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
                if(!before.CivilianRemoval && entry.Order!=orders[before.Id])
                {
                    queue.InvalidateUnit(before.Id);
                    if(RescueGeometryWitness.TryCapture(before,out var witness))RetainGeometry(witness,entry.Source,tick,0,0,completion.BatchId);
                    continue;
                }
                // Reused native slots replace their own old lifetime even at full capacity.
                if(PendingCount>=MaximumPending && !queue.ContainsUnit(before.Id))
                {ReportBounded("mutation-pending-capacity");continue;}
                var identity=new RescueIdentity(batch.Session,batch.World,before.Id,before.GlobalId,entry.Order);
                var outcome=queue.Offer(identity,completion.BatchId,entry.Source,before,tick);
                if(outcome!=RescueOfferResult.Added && outcome!=RescueOfferResult.Duplicate)
                    ReportBounded("mutation candidate deferred unit="+before.Id+" reason="+outcome);
            }
        }
        internal void Tick(int tick)
        {
            if(!IsActive){ExpireInactivePending();return;}
            if(externalOrderDepth!=0)return;
            if(PendingCount==0 || drainedTick && lastDrainTick==tick || Interlocked.CompareExchange(ref busy,1,0)!=0)return;
            try
            {
                if(staged.Count!=0 || stagedGeometry.Count!=0)
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
                passiveIdleBudget=32;
                ProcessReplans(tick,32);
#if SERP_DEVDIAG
            }
            finally
            {
                Shared.DevDiag.StopSpike(ref replanStarted,"rescue.tick.replans",2000);
            }
#endif
                if(!queue.HasWork)return;
#if SERP_DEVDIAG
            long recoveryStarted=0;
            Shared.DevDiag.Start(ref recoveryStarted);
            try
            {
#endif
                queue.Drain(tick,passiveIdleBudget,8,candidate=>
                {
                    if(!IsCurrent(candidate.Identity.SessionId,candidate.Identity.WorldGeneration))
                    {ResetWorld(0);return RescueQueueDecision.Reject;}
                    if(!candidate.Snapshot.CivilianRemoval && candidate.Identity.OrderSerial!=orders[candidate.Identity.UnitId])return RescueQueueDecision.Reject;
                    if(TryRetainPassiveIdle(candidate,tick))return RescueQueueDecision.Observe;
                    // A consumer's readonly accessor may reenter an order, mutation
                    // or world boundary. Recheck ownership before any native action.
                    if(!IsCurrent(candidate.Identity.SessionId,candidate.Identity.WorldGeneration) ||
                        !queue.TryGetSnapshot(candidate.Identity.UnitId,out var stillOwned) || !ReferenceEquals(stillOwned,candidate.Snapshot) ||
                        !candidate.Snapshot.CivilianRemoval && candidate.Identity.OrderSerial!=orders[candidate.Identity.UnitId])return RescueQueueDecision.Reject;
                    if(candidate.Snapshot.GeometryWitness!=null && candidate.Snapshot.Flags==0)
                    {
                        try
                        {
                            if(!(access is IRescuePassiveIdleWorld currentIdle))return RescueQueueDecision.Reject;
                            if(!currentIdle.IsCurrentIdle(candidate.Snapshot))
                                return TryRetainPassiveIdle(candidate,tick,true)?RescueQueueDecision.Observe:RescueQueueDecision.Reject;
                        }
                        catch{ReportBounded("passive-idle-validation-failed");return RescueQueueDecision.Reject;}
                        if(!IsCurrent(candidate.Identity.SessionId,candidate.Identity.WorldGeneration) ||
                            !queue.TryGetSnapshot(candidate.Identity.UnitId,out var idleOwned) || !ReferenceEquals(idleOwned,candidate.Snapshot) ||
                            candidate.Identity.OrderSerial!=orders[candidate.Identity.UnitId])return RescueQueueDecision.Reject;
                    }
                    // Removal must leave Fixes' own normal unit cleanup authoritative.
                    bool movement=!candidate.Snapshot.CivilianRemoval;
                    RescueResult result;bool completed;
                    try
                    {
                        if(movement)suppression?.Invoke(true);
                        completed=access.TryRescue(candidate.Snapshot,"observed-"+candidate.Source,true,out result);
                    }
                    finally{if(movement)suppression?.Invoke(false);}
                    if(result.Status==RescueStatus.AwaitResources)return RescueQueueDecision.AwaitResources;
                    if(result.Status==RescueStatus.RemovalDeferred)return RescueQueueDecision.Observe;
                    Interlocked.Increment(ref attempts);
                    if(completed)
                    {
                        if(result.Status==RescueStatus.RemovalRequested)Interlocked.Increment(ref removals);
                        else Interlocked.Increment(ref rescues);
                        return RescueQueueDecision.Complete;
                    }
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
            finally{Volatile.Write(ref busy,0);}
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
