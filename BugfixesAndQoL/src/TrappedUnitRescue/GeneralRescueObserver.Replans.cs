using System;
using System.Collections.Generic;
using System.Threading;
using APIShared.Units;

namespace BugfixesAndQoL.TrappedUnitRescue
{
    internal sealed partial class GeneralRescueObserver : IUnitMoveObserver
    {
        private sealed class RetainedGeometry
        {
            internal RescueGeometryWitness Witness;
            internal string Source;
            internal int CapturedTick,Attempts,DueTick,ActiveAge=-1,ActiveUpdatedTick;
            internal bool AwaitingResources;
            internal long Session,World,Mutation;
            internal RescueReplanIntent Fresh;
            internal UnitSnapshot FreshIdle;
            internal bool CheckIdle;
            internal long FreshSerial;
            internal LinkedListNode<RetainedGeometry> Node;
        }
        private readonly Dictionary<int,RetainedGeometry> geometry=new Dictionary<int,RetainedGeometry>();
        private readonly LinkedList<RetainedGeometry> geometrySchedule=new LinkedList<RetainedGeometry>();
        private readonly HashSet<int> unsettledReplans=new HashSet<int>();
        private long replanBoundary;
        private int passiveIdleBudget;
        private readonly Dictionary<int,RescuePendingGeometry> stagedGeometry=new Dictionary<int,RescuePendingGeometry>();
        private long geometryRevision,preparedFreshRevision=-1,preparedFreshSession,preparedFreshWorld;
        private int preparedFreshTick;
        private readonly HashSet<int> preparedFreshIds=new HashSet<int>();
        internal IRescueReplanIntentWorld ReplanIntents {get;set;}
        internal string ReplanOwnerGuid {get;set;}
        internal Func<bool> OwnReplanScope {get;set;}
        private int GeometryCount=>geometry.Count+stagedGeometry.Count;
        internal bool HasUnsettledReplans=>unsettledReplans.Count!=0 || externalOrderDepth!=0;
        private bool IsReplanInterested(int id)=>geometry.ContainsKey(id) || stagedGeometry.ContainsKey(id);
        private void RetireAllHistoricalGeometry()
        {
            queue.Reset(session,world);staged.Clear();ResetGeometry(true);
        }
        private IReadOnlyList<int> RetireHistoricalGeometry(IReadOnlyList<RescueTile> writes,out bool completeEnvelope)
        {
            InvalidateFreshSave();
            completeEnvelope=writes!=null && writes.Count<=2048;
            if(PendingCount==0 || writes!=null && writes.Count==0)return Array.Empty<int>();
            // This bound is the native adapter's complete-envelope budget. A provider
            // exceeding it has supplied no usable bounded proof, never a partial one.
            if(!completeEnvelope){RetireAllHistoricalGeometry();return Array.Empty<int>();}
            var retired=new List<int>();
            var cells=new HashSet<long>();int minX=799,minY=799,maxX=0,maxY=0;
            foreach(var tile in writes)
            {
                if((uint)tile.X>=800 || (uint)tile.Y>=800 || (uint)tile.TileId>=NativeRelocation.TileCapacity)
                {completeEnvelope=false;RetireAllHistoricalGeometry();return retired;}
                cells.Add(CoordinateKey(tile.X,tile.Y));
                minX=Math.Min(minX,tile.X);maxX=Math.Max(maxX,tile.X);minY=Math.Min(minY,tile.Y);maxY=Math.Max(maxY,tile.Y);
            }
            var remove=new List<int>();
            foreach(var item in geometry.Values)
                if(EnvelopeOverlaps(item.Witness.Current.X,item.Witness.Current.Y,cells,minX,minY,maxX,maxY))remove.Add(item.Witness.UnitId);
            foreach(int id in remove){retired.Add(id);DropGeometry(id);}
            remove.Clear();
            foreach(var record in stagedGeometry.Values)
                if(EnvelopeOverlaps(record.Witness.Current.X,record.Witness.Current.Y,cells,minX,minY,maxX,maxY))remove.Add(record.Witness.UnitId);
            foreach(int id in remove){retired.Add(id);stagedGeometry.Remove(id);}
            remove.Clear();
            foreach(var record in staged.Values)
                if(SnapshotEnvelopeOverlaps(record.Snapshot,cells,minX,minY,maxX,maxY))remove.Add(record.Snapshot.Id);
            foreach(int id in remove){retired.Add(id);staged.Remove(id);}
            if(!queue.HasWork)return retired;
            if(IsRecovering)
            {
                // Save export intentionally refuses a draining queue. Invalidation
                // instead copies bounded owned references, including the active one.
                foreach(var snapshot in queue.CopyOwnedSnapshots())
                    if(SnapshotEnvelopeOverlaps(snapshot,cells,minX,minY,maxX,maxY))
                    {retired.Add(snapshot.Id);queue.InvalidateUnit(snapshot.Id);}
                return retired;
            }
            if(!access.TryGetTick(out int tick) || !queue.TryCopyPending(tick,out var queued))
            {completeEnvelope=false;RetireAllHistoricalGeometry();return retired;}
            foreach(var record in queued)
                if(SnapshotEnvelopeOverlaps(record.Snapshot,cells,minX,minY,maxX,maxY))
                {retired.Add(record.Snapshot.Id);queue.InvalidateUnit(record.Snapshot.Id);}
            return retired;
        }
        private static bool SnapshotEnvelopeOverlaps(UnitSnapshot snapshot,HashSet<long> cells,int minX,int minY,int maxX,int maxY)
        {
            if(snapshot==null)return true;
            var origin=snapshot.GeometryWitness;
            return EnvelopeOverlaps(origin==null?snapshot.X:origin.Current.X,origin==null?snapshot.Y:origin.Current.Y,cells,minX,minY,maxX,maxY);
        }
        // Coordinate keys are NOT native packed tile IDs. All old BFS paths stay
        // inside the original radius8 square, even when fresh Current is old Next.
        private static long CoordinateKey(int x,int y)=>((long)(uint)x<<32)|(uint)y;
        private static bool EnvelopeOverlaps(int x,int y,HashSet<long> cells,int minX,int minY,int maxX,int maxY)
        {
            if((uint)x>=800 || (uint)y>=800)return true;
            if(maxX<x-8 || minX>x+8 || maxY<y-8 || minY>y+8)return false;
            for(int row=Math.Max(0,y-8);row<=Math.Min(799,y+8);row++)
                for(int column=Math.Max(0,x-8);column<=Math.Min(799,x+8);column++)
                    if(cells.Contains(CoordinateKey(column,row)))return true;
            return false;
        }
        private void InvalidateFreshSave()
        {geometryRevision=unchecked(geometryRevision+1);preparedFreshRevision=-1;preparedFreshIds.Clear();}
        private bool RetireStagedGeometry(int id,uint global=0)
        {
            if(!stagedGeometry.TryGetValue(id,out var record))return false;
            stagedGeometry.Remove(id);InvalidateFreshSave();return global!=0 && record.Witness.GlobalId==global;
        }
        // Source age never refreshes when intent, admission or world binding changes.
        private static bool GeometryAge(int captured,int tick,out int age)
        {uint delta=unchecked((uint)(tick-captured));age=delta<=int.MaxValue?(int)delta:0;return delta<=int.MaxValue;}
        private static bool GeometryActiveAge(RetainedGeometry item,int tick,out int age)
        {
            age=item.ActiveAge;uint delta=unchecked((uint)(tick-item.ActiveUpdatedTick));
            if(delta>int.MaxValue)return false;
            if(age>=0 && !item.AwaitingResources)
            {if(age>int.MaxValue-(int)delta)return false;age+=(int)delta;}
            return age<80;
        }
        private static int GeometryDelay(int due,int tick)=>unchecked(tick-due)>=0?0:unchecked(due-tick);
        private static string ReplanSource(string source)=>source.StartsWith("replan/",StringComparison.Ordinal)?source:"replan/"+source;
        private void ScheduleGeometry(RetainedGeometry item)
        {
            bool ready=item.Fresh!=null || item.FreshIdle!=null || item.CheckIdle || item.ActiveAge>=0 && !item.AwaitingResources;
            if(ready && item.Node==null)item.Node=geometrySchedule.AddLast(item);
            if(!ready && item.Node!=null){geometrySchedule.Remove(item.Node);item.Node=null;}
        }
        private void DropGeometry(int id)
        {
            if(geometry.TryGetValue(id,out var item))
            {if(item.Node!=null)geometrySchedule.Remove(item.Node);geometry.Remove(id);InvalidateFreshSave();}
            unsettledReplans.Remove(id);
        }
        private void ResetGeometry(bool clearStaged)
        {
            geometry.Clear();geometrySchedule.Clear();unsettledReplans.Clear();
            if(clearStaged)stagedGeometry.Clear();InvalidateFreshSave();
        }
        private bool TryGeometryRecord(RetainedGeometry item,int tick,out RescuePendingGeometry record)
        {
            record=default;
            if(!GeometryAge(item.CapturedTick,tick,out int age) || !GeometryActiveAge(item,tick,out int activeAge) || item.Attempts>=4)return false;
            int delay=GeometryDelay(item.DueTick,tick);if(delay>1)return false;
            record=new RescuePendingGeometry(item.Source,item.Witness,age,item.Attempts,delay,
                RescueQueueLifetimeMode.ActiveWindow,activeAge,item.AwaitingResources);return true;
        }
        // Called by the pending export before its immediately paired geometry export,
        // under the engine monitor. This is readonly validation of an existing intent:
        // no new capture, BFS, command or native write is permitted here.
        private bool TryAppendFreshPending(int tick,List<RescuePendingCandidate<UnitSnapshot>> records)
        {
            InvalidateFreshSave();
            if(HasUnsettledReplans || IsRecovering || !IsCurrent(session,world))return false;
            long revision=geometryRevision;var included=new HashSet<int>();
            foreach(var record in records)if(!included.Add(record.Snapshot.Id))return false;
            foreach(var item in geometry.Values)
            {
                if(item.Fresh==null && item.FreshIdle==null || item.FreshSerial!=orders[item.Witness.UnitId] || !TryGeometryRecord(item,tick,out var saved))continue;
                bool joined=false;UnitSnapshot snapshot=null;
                try{joined=TryJoinExistingFresh(item,out snapshot);}
                catch{ReportBounded("save-fresh-intent-validation-failed");}
                if(geometryRevision!=revision || !IsCurrent(session,world))return false;
                if(!joined)continue; // Invalid intent is saved as geometry only, never invented.
                if(!included.Add(snapshot.Id) || records.Count>=MaximumPending)return false;
                records.Add(new RescuePendingCandidate<UnitSnapshot>(ReplanSource(item.Source),snapshot,saved.Age,saved.Attempts,saved.RetryIn,
                    saved.LifetimeMode,saved.ActiveAge,saved.AwaitingResources));preparedFreshIds.Add(snapshot.Id);
            }
            preparedFreshSession=session;preparedFreshWorld=world;preparedFreshTick=tick;preparedFreshRevision=revision;return true;
        }
        internal bool TryCaptureGeometry(out RescuePendingGeometry[] records)
        {
            records=null;if(HasUnsettledReplans || IsRecovering)return false;
            int tick=0;
            if((geometry.Count!=0 || preparedFreshRevision>=0) && (!IsCurrent(session,world) || !access.TryGetTick(out tick)))return false;
            bool paired=preparedFreshRevision==geometryRevision && preparedFreshSession==session && preparedFreshWorld==world && preparedFreshTick==tick;
            var result=new List<RescuePendingGeometry>(stagedGeometry.Values);
            foreach(var item in geometry.Values)
                if((!paired || !preparedFreshIds.Contains(item.Witness.UnitId)) && TryGeometryRecord(item,tick,out var saved))result.Add(saved);
            preparedFreshRevision=-1;preparedFreshIds.Clear();
            result.Sort((a,b)=>a.Witness.UnitId.CompareTo(b.Witness.UnitId));records=result.ToArray();return true;
        }
        internal void StageGeometry(RescuePendingGeometry[] records)
        {
            stagedGeometry.Clear();InvalidateFreshSave();
            if(records==null || records.Length+queue.Count+staged.Count+geometry.Count>MaximumPending)return;
            foreach(var record in records)
            {
                if(!RescueGeometryCodec.TryEncode(record,out _) || stagedGeometry.ContainsKey(record.Witness.UnitId) ||
                    queue.ContainsUnit(record.Witness.UnitId) || staged.ContainsKey(record.Witness.UnitId) || geometry.ContainsKey(record.Witness.UnitId))
                {stagedGeometry.Clear();ReportBounded("invalid-staged-geometry");return;}
                stagedGeometry.Add(record.Witness.UnitId,record);
            }
        }
        private void RestoreGeometry(int tick)
        {
            var records=new List<RescuePendingGeometry>(stagedGeometry.Values);stagedGeometry.Clear();InvalidateFreshSave();
            foreach(var record in records)
                if(!geometry.ContainsKey(record.Witness.UnitId) && !queue.ContainsUnit(record.Witness.UnitId))
                {
                    RetainGeometry(record.Witness,record.Source,unchecked(tick-record.Age),record.Attempts,record.RetryIn,long.MaxValue,tick,
                        record.ActiveAge,record.AwaitingResources,record.LifetimeMode);
                    // A geometry archive grants no old intent. At its new-world
                    // bind boundary it may request one fully fresh idle check.
                    if(access is IRescuePassiveIdleWorld && geometry.TryGetValue(record.Witness.UnitId,out var restored))
                    {restored.CheckIdle=true;restored.FreshSerial=orders[record.Witness.UnitId];ScheduleGeometry(restored);}
                }
        }
        private void RetainGeometry(RescueGeometryWitness witness,string source,int captured,int attempts,int retry,long mutation,int? boundTick=null,
            int activeAge=-1,bool awaiting=false,RescueQueueLifetimeMode mode=RescueQueueLifetimeMode.ActiveWindow)
        {
            int now=boundTick??captured;
            if(ReplanIntents==null || witness==null || attempts>=4 || PendingCount>=MaximumPending ||
                !GeometryAge(captured,now,out int sourceAge))return;
            if(mode==RescueQueueLifetimeMode.AbsoluteSourceAge)
            {if(sourceAge>=80)return;activeAge=sourceAge;awaiting=false;}
            else if(mode!=RescueQueueLifetimeMode.ActiveWindow)return;
            if(activeAge< -1 || activeAge>=80 || activeAge>sourceAge || activeAge<0 && attempts!=0)return;
            var item=new RetainedGeometry{Witness=witness,Source=source,CapturedTick=captured,Attempts=attempts,DueTick=unchecked(now+retry),
                ActiveAge=activeAge,AwaitingResources=awaiting,ActiveUpdatedTick=now,Session=session,World=world,Mutation=mutation};
            geometry.Add(witness.UnitId,item);InvalidateFreshSave();ScheduleGeometry(item);
        }
        // Transfer only spatial authority; the old queued intent is removed even when
        // this transfer cannot be admitted. The canonical order serial advances next.
        private void RetainBeforeOrder(int id)
        {
            if(HasRemovalEvidence(id) || ReplanIntents==null)return;
            if(!access.TryGetWorld(out long currentSession,out long generation) || currentSession!=session || generation==0 ||
                !access.TryGetTick(out int tick))
            {DropGeometry(id);return;}
            if(world!=generation)ResetWorld(generation);
            if(stagedGeometry.Count!=0)RestoreGeometry(tick);
            InvalidateFreshSave();
            if(geometry.TryGetValue(id,out var previous)){previous.Fresh=null;previous.FreshIdle=null;previous.CheckIdle=false;unsettledReplans.Remove(id);ScheduleGeometry(previous);}
            if(queue.TryTakeUnit(id,out var queued))
            {
                DropGeometry(id);
                if(GeometryAge(queued.SourceCapturedTick,tick,out _) && queued.TryActiveAge(tick,out int activeAge) && activeAge<80 && queued.Attempts<4)
                {
                    var witness=queued.Snapshot.GeometryWitness;
                    if(witness==null)RescueGeometryWitness.TryCapture(queued.Snapshot,out witness);
                    int delay=GeometryDelay(queued.DueTick,tick);
                    if(delay<=1)RetainGeometry(witness,queued.Source,queued.SourceCapturedTick,queued.Attempts,delay,queued.MutationId,tick,activeAge,queued.AwaitingResources);
                }
            }
            // Restored pending intent is equally revocable before its first Tick.
            if(staged.TryGetValue(id,out var saved))
            {
                RetireStaged(id);DropGeometry(id);
                var witness=saved.Snapshot.GeometryWitness;
                if(witness==null)RescueGeometryWitness.TryCapture(saved.Snapshot,out witness);
                RetainGeometry(witness,saved.Source,unchecked(tick-saved.Age),saved.Attempts,saved.RetryIn,long.MaxValue,tick,
                    saved.ActiveAge,saved.AwaitingResources,saved.LifetimeMode);
            }
        }
        public IUnitMoveInvocation TryBegin(UnitMoveContext context)
        {
            if(context==null || !IsActive || !geometry.TryGetValue(context.UnitId,out var item) ||
                context.SessionId!=item.Session || context.WorldGeneration!=item.World || !IsCurrent(item.Session,item.World))return null;
            bool own=context.OriginOwnerGuid==ReplanOwnerGuid && OwnReplanScope!=null && OwnReplanScope();
            RescueReplanIntent fresh=null;
            if(!own)
            {
                unsettledReplans.Add(context.UnitId);
                try
                {
                    if(ReplanIntents!=null && ReplanIntents.TryCapture(context.UnitId,context.X,context.Y,context.Extra,out var capture))
                    {
                        if(!RescueGeometryWitness.TryJoin(item.Witness,capture.CopyBefore(),out _))
                        {DropGeometry(context.UnitId);return null;}
                        fresh=capture;
                    }
                }
                catch{ReportBounded("replan-intent-capture-failed");}
            }
            return new ReplanCompletion(this,item,context,orders[context.UnitId],fresh,own,replanBoundary);
        }
        private sealed class ReplanCompletion : IUnitMoveInvocation
        {
            private readonly GeneralRescueObserver owner;
            private readonly RetainedGeometry item;
            private readonly UnitMoveContext context;
            private readonly long serial;
            private readonly RescueReplanIntent fresh;
            private readonly bool own;
            private readonly long boundary;
            private int completed;
            internal ReplanCompletion(GeneralRescueObserver owner,RetainedGeometry item,UnitMoveContext context,long serial,RescueReplanIntent fresh,bool own,long boundary)
            {this.owner=owner;this.item=item;this.context=context;this.serial=serial;this.fresh=fresh;this.own=own;this.boundary=boundary;}
            public void Complete(UnitMoveCompletion completion)
            {
                if(Interlocked.Exchange(ref completed,1)!=0 || completion==null || !ReferenceEquals(completion.Context,context) ||
                    !owner.geometry.TryGetValue(context.UnitId,out var current) || current.Session!=context.SessionId || current.World!=context.WorldGeneration)return;
                if(!completion.IsCurrentWorld || !owner.IsCurrent(current.Session,current.World))
                {owner.DropGeometry(context.UnitId);return;}
                // A later actual publication wins even when an older outer Begin
                // executes its original after a newer nested failed call.
                if(completion.NativeInvoked && completion.NativeReturned && completion.NativeResult!=0)
                {owner.DropGeometry(context.UnitId);return;}
                if(own || boundary!=owner.replanBoundary || !ReferenceEquals(current,item) || owner.orders[context.UnitId]!=serial)return;
                bool refused=completion.NativeInvoked && completion.NativeReturned && completion.Failure==null && completion.NativeResult==0;
                current.Fresh=refused?fresh:null;current.FreshIdle=null;
                // Caller tail must finish before a failed call can authorize a
                // one-shot fresh idle check. This is not an old-intent retry.
                current.CheckIdle=refused && context.Extra==0 && owner.access is IRescuePassiveIdleWorld;
                current.FreshSerial=serial;
                owner.InvalidateFreshSave();owner.ScheduleGeometry(current);
            }
        }
        private bool TryJoinExistingFresh(RetainedGeometry item,out UnitSnapshot snapshot)
        {
            snapshot=null;
            if(item.FreshIdle!=null)return access is IRescuePassiveIdleWorld idle && idle.IsCurrentIdle(item.FreshIdle) &&
                RescueGeometryWitness.TryJoinStationary(item.Witness,item.FreshIdle,out snapshot);
            return item.Fresh!=null && ReplanIntents!=null && ReplanIntents.TryGetSnapshot(item.Fresh,out var exact) &&
                RescueGeometryWitness.TryJoin(item.Witness,exact,out snapshot);
        }
        // Called before access.TryRescue, so this path cannot follow a partial
        // native failure. Only a freshly admitted idle phase replaces old intent.
        private bool TryRetainPassiveIdle(RescueQueuedCandidate<UnitSnapshot> candidate,int tick,bool staleStationary=false)
        {
            var before=candidate.Snapshot;
            if(before==null || before.CivilianRemoval || before.Command==null || (staleStationary?before.Flags!=0 || before.GeometryWitness==null:before.Flags!=2 && before.Flags!=4) ||
                !(access is IRescuePassiveIdleWorld idle) || !IsCurrent(candidate.Identity.SessionId,candidate.Identity.WorldGeneration))return false;
            var witness=before.GeometryWitness;
            if(witness==null)RescueGeometryWitness.TryCapture(before,out witness);
            UnitSnapshot fresh=null,joined=null;bool settling=false;
            if(passiveIdleBudget<=0)return false;
            passiveIdleBudget--;
            try
            {
                if(!idle.TryCaptureIdle(before.Id,out fresh))settling=idle.IsKnownIdleTransition(witness);
            }
            catch{ReportBounded("passive-idle-capture-failed");return false;}
            if(!settling && !RescueGeometryWitness.TryJoinStationary(witness,fresh,out joined) ||
                !IsCurrent(candidate.Identity.SessionId,candidate.Identity.WorldGeneration) || orders[before.Id]!=candidate.Identity.OrderSerial ||
                !queue.TryGetSnapshot(before.Id,out var owned) || !ReferenceEquals(owned,before) ||
                !candidate.TryActiveAge(tick,out int activeAge))return false;
            // The queue incremented its provisional attempt before this read-only
            // evaluation. No native action was made, so preserve the prior count.
            if(!queue.TryTakeUnit(before.Id,out var taken) || !ReferenceEquals(taken,candidate))return false;
            DropGeometry(before.Id);
            RetainGeometry(witness,candidate.Source,candidate.SourceCapturedTick,candidate.Attempts-1,0,candidate.MutationId,tick,
                activeAge,candidate.AwaitingResources);
            if(geometry.TryGetValue(before.Id,out var item))
            {item.FreshIdle=joined;item.CheckIdle=settling;item.FreshSerial=orders[before.Id];ScheduleGeometry(item);InvalidateFreshSave();}
            return true;
        }
        private void ProcessReplans(int tick,int visitBudget)
        {
            int available=Math.Min(visitBudget,geometrySchedule.Count);
            while(available-->0 && geometrySchedule.Count!=0)
            {
                var item=geometrySchedule.First.Value;geometrySchedule.RemoveFirst();item.Node=null;
                if(!IsCurrent(item.Session,item.World) || !TryGeometryRecord(item,tick,out var saved))
                {DropGeometry(item.Witness.UnitId);continue;}
                bool hasFresh=item.Fresh!=null || item.FreshIdle!=null || item.CheckIdle;
                if(hasFresh && item.FreshSerial==orders[item.Witness.UnitId])
                {
                    bool geometryOnly=item.Fresh==null && item.FreshIdle==null;
                    bool joined=false,settling=false,budgetWait=false;UnitSnapshot snapshot=null;
                    try
                    {
                        // Leave room for at least eight already queued actions.
                        // Fresh active-intent validation keeps its existing budget.
                        int reserve=queue.HasWork?8:0;
                        if(item.FreshIdle!=null)
                        {
                            if(passiveIdleBudget<=reserve)budgetWait=true;
                            else
                            {
                                passiveIdleBudget--;joined=TryJoinExistingFresh(item,out snapshot);
                                // A changed animation is never tolerated against
                                // the old receipt. Capture the full current idle
                                // semantics anew within this same bounded ID visit.
                                if(!joined && access is IRescuePassiveIdleWorld freshIdle &&
                                    freshIdle.TryCaptureIdle(item.Witness.UnitId,out var currentIdle))
                                    joined=RescueGeometryWitness.TryJoinStationary(item.Witness,currentIdle,out snapshot);
                            }
                        }
                        else joined=TryJoinExistingFresh(item,out snapshot);
                        if(!joined && !budgetWait && item.CheckIdle && access is IRescuePassiveIdleWorld idle)
                        {
                            if(passiveIdleBudget<=reserve)budgetWait=true;
                            else
                            {
                                passiveIdleBudget--;
                                if(idle.TryCaptureIdle(item.Witness.UnitId,out var currentIdle))
                                    joined=RescueGeometryWitness.TryJoinStationary(item.Witness,currentIdle,out snapshot);
                                else settling=idle.IsKnownIdleTransition(item.Witness);
                            }
                        }
                    }
                    catch{ReportBounded("replan-intent-validation-failed");}
                    // A readonly adapter can still be supplied by a consumer: never
                    // transfer an item invalidated/replaced inside that boundary.
                    if(!geometry.TryGetValue(item.Witness.UnitId,out var current) || !ReferenceEquals(current,item) ||
                        !IsCurrent(item.Session,item.World) || item.FreshSerial!=orders[item.Witness.UnitId])continue;
                    if(joined)
                    {
                        DropGeometry(item.Witness.UnitId);
                        var record=new RescuePendingCandidate<UnitSnapshot>(ReplanSource(item.Source),snapshot,saved.Age,saved.Attempts,saved.RetryIn,
                            saved.LifetimeMode,saved.ActiveAge,saved.AwaitingResources);
                        if(!queue.OfferRestored(new RescueIdentity(item.Session,item.World,snapshot.Id,snapshot.GlobalId,item.FreshSerial),item.Mutation,record,tick,snapshot.Flags==0))
                            ReportBounded("replan-candidate-transfer-rejected");
                        continue;
                    }
                    if(budgetWait)
                    {
                        // Admission scarcity is not active semantic work. Suspend
                        // the existing window, never the original source clock.
                        item.ActiveAge=saved.ActiveAge;item.ActiveUpdatedTick=tick;item.AwaitingResources=true;
                        ScheduleGeometry(item);continue;
                    }
                    if(settling)
                    {
                        // Only the proved AI101->idle caller-tail transition may
                        // wait again, within its original finite active window.
                        item.Fresh=null;item.FreshIdle=null;item.CheckIdle=true;
                        item.ActiveAge=saved.ActiveAge<0?0:saved.ActiveAge;item.ActiveUpdatedTick=tick;item.AwaitingResources=false;
                        item.DueTick=unchecked(tick+1);ScheduleGeometry(item);continue;
                    }
                    if(geometryOnly)
                    {
                        // A failed one-shot check grants no idle authority, but
                        // does not refute the original ground proof. Future real
                        // native order receipts may still supply a fresh intent.
                        item.CheckIdle=false;ScheduleGeometry(item);continue;
                    }
                    DropGeometry(item.Witness.UnitId);continue;
                }
                ScheduleGeometry(item);
            }
            unsettledReplans.Clear();replanBoundary=unchecked(replanBoundary+1);
        }
    }
}
