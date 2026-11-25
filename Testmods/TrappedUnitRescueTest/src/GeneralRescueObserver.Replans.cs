using System;
using System.Collections.Generic;
using System.Threading;
using APIShared.Units;

namespace TrappedUnitRescueTest
{
    internal sealed partial class GeneralRescueObserver : IUnitMoveObserver
    {
        private sealed class RetainedGeometry
        {
            internal RescueGeometryWitness Witness;
            internal string Source;
            internal int CapturedTick,Attempts,RetryIn;
            internal long Session,World,Mutation;
            internal RescueReplanIntent Fresh;
            internal long FreshSerial;
            internal LinkedListNode<RetainedGeometry> Node;
        }
        private readonly Dictionary<int,RetainedGeometry> geometry=new Dictionary<int,RetainedGeometry>();
        private readonly LinkedList<RetainedGeometry> geometrySchedule=new LinkedList<RetainedGeometry>();
        private readonly HashSet<int> unsettledReplans=new HashSet<int>();
        private RescuePendingGeometry[] stagedGeometry=Array.Empty<RescuePendingGeometry>();
        private readonly HashSet<int> stagedGeometryIds=new HashSet<int>();
        internal IRescueReplanIntentWorld ReplanIntents {get;set;}
        internal string ReplanOwnerGuid {get;set;}
        internal Func<bool> OwnReplanScope {get;set;}
        private int GeometryCount=>geometry.Count+stagedGeometry.Length;
        internal bool HasUnsettledReplans=>unsettledReplans.Count!=0;
        private bool IsReplanInterested(int id)=>geometry.ContainsKey(id) || stagedGeometryIds.Contains(id);
        private bool RetireStagedGeometry(int id,uint global=0)
        {
            if(!stagedGeometryIds.Remove(id))return false;
            bool same=false;var kept=new List<RescuePendingGeometry>();
            foreach(var r in stagedGeometry)
                if(r.Witness.UnitId==id)same|=global!=0 && r.Witness.GlobalId==global;else kept.Add(r);
            stagedGeometry=kept.ToArray();return same;
        }
        private static bool GeometryAge(int captured,int tick,out int age)
        {uint delta=unchecked((uint)(tick-captured));age=delta<=int.MaxValue?(int)delta:0;return delta<80;}
        private void DropGeometry(int id)
        {
            if(geometry.TryGetValue(id,out var item))
            {if(item.Node!=null)geometrySchedule.Remove(item.Node);geometry.Remove(id);}
            unsettledReplans.Remove(id);
        }
        private void ResetGeometry(bool clearStaged)
        {
            geometry.Clear();geometrySchedule.Clear();unsettledReplans.Clear();
            if(clearStaged){stagedGeometry=Array.Empty<RescuePendingGeometry>();stagedGeometryIds.Clear();}
        }
        internal bool TryCaptureGeometry(out RescuePendingGeometry[] records)
        {
            records=null;if(HasUnsettledReplans || IsRecovering)return false;
            var result=new List<RescuePendingGeometry>(stagedGeometry);
            if(geometry.Count!=0)
            {
                if(!IsCurrent(session,world) || !access.TryGetTick(out int tick))return false;
                foreach(var item in geometry.Values)
                    if(GeometryAge(item.CapturedTick,tick,out int age) && item.Attempts<4)
                        result.Add(new RescuePendingGeometry(item.Source,item.Witness,age,item.Attempts,item.RetryIn));
            }
            result.Sort((a,b)=>a.Witness.UnitId.CompareTo(b.Witness.UnitId));records=result.ToArray();return true;
        }
        internal void StageGeometry(RescuePendingGeometry[] records)
        {
            stagedGeometry=Array.Empty<RescuePendingGeometry>();stagedGeometryIds.Clear();
            if(records==null || records.Length+queue.Count+staged.Length+geometry.Count>512)return;
            foreach(var record in records)
                if(!RescueGeometryCodec.TryEncode(record,out _) || !stagedGeometryIds.Add(record.Witness.UnitId) ||
                    queue.ContainsUnit(record.Witness.UnitId) || stagedIds.Contains(record.Witness.UnitId) || geometry.ContainsKey(record.Witness.UnitId))
                {stagedGeometryIds.Clear();ReportBounded("invalid-staged-geometry");return;}
            stagedGeometry=(RescuePendingGeometry[])records.Clone();
        }
        private void RestoreGeometry(int tick)
        {
            var records=stagedGeometry;stagedGeometry=Array.Empty<RescuePendingGeometry>();stagedGeometryIds.Clear();
            foreach(var record in records)
                if(!geometry.ContainsKey(record.Witness.UnitId) && !queue.ContainsUnit(record.Witness.UnitId))
                    RetainGeometry(record.Witness,record.Source,unchecked(tick-record.Age),record.Attempts,record.RetryIn,long.MaxValue);
        }
        private void RetainGeometry(RescueGeometryWitness witness,string source,int captured,int attempts,int retry,long mutation)
        {
            if(ReplanIntents==null || witness==null || attempts>=4 || PendingCount>=512)return;
            var item=new RetainedGeometry{Witness=witness,Source=source,CapturedTick=captured,Attempts=attempts,RetryIn=retry,
                Session=session,World=world,Mutation=mutation};
            item.Node=geometrySchedule.AddLast(item);geometry.Add(witness.UnitId,item);
        }
        // Transfer only spatial authority; the old queued intent is removed even when
        // this transfer cannot be admitted. The canonical order serial advances next.
        private void RetainBeforeOrder(int id)
        {
            if(ReplanIntents==null)return;
            if(!access.TryGetWorld(out long currentSession,out long generation) || currentSession!=session || generation==0 ||
                !access.TryGetTick(out int tick))
            {DropGeometry(id);return;}
            if(world!=generation)ResetWorld(generation);
            if(stagedGeometry.Length!=0)RestoreGeometry(tick);
            if(geometry.TryGetValue(id,out var previous)){previous.Fresh=null;unsettledReplans.Remove(id);}
            if(queue.TryTakeUnit(id,out var queued))
            {
                DropGeometry(id);
                if(GeometryAge(queued.CapturedTick,tick,out _) && queued.Attempts<4 &&
                    (queued.Snapshot.GeometryWitness!=null || RescueGeometryWitness.TryCapture(queued.Snapshot,out _)))
                {
                    var witness=queued.Snapshot.GeometryWitness;
                    if(witness==null)RescueGeometryWitness.TryCapture(queued.Snapshot,out witness);
                    RetainGeometry(witness,queued.Source,queued.CapturedTick,queued.Attempts,0,queued.MutationId);
                }
            }
            // Restored pending intent is equally revocable before its first Tick.
            if(stagedIds.Contains(id))
            {
                RescuePendingCandidate<UnitSnapshot> saved=default;bool found=false;
                foreach(var record in staged)if(record.Snapshot.Id==id){saved=record;found=true;break;}
                RetireStaged(id);DropGeometry(id);
                if(found)
                {
                    var witness=saved.Snapshot.GeometryWitness;
                    if(witness==null)RescueGeometryWitness.TryCapture(saved.Snapshot,out witness);
                    RetainGeometry(witness,saved.Source,unchecked(tick-saved.Age),saved.Attempts,saved.RetryIn,long.MaxValue);
                }
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
                    if(ReplanIntents!=null && ReplanIntents.TryCapture(context.UnitId,context.X,context.Y,context.Extra,out var capture) &&
                        RescueGeometryWitness.TryJoin(item.Witness,capture.CopyBefore(),out _))fresh=capture;
                }
                catch{ReportBounded("replan-intent-capture-failed");}
            }
            return new ReplanCompletion(this,item,context,orders[context.UnitId],fresh,own);
        }
        private sealed class ReplanCompletion : IUnitMoveInvocation
        {
            private readonly GeneralRescueObserver owner;
            private readonly RetainedGeometry item;
            private readonly UnitMoveContext context;
            private readonly long serial;
            private readonly RescueReplanIntent fresh;
            private readonly bool own;
            private int completed;
            internal ReplanCompletion(GeneralRescueObserver owner,RetainedGeometry item,UnitMoveContext context,long serial,RescueReplanIntent fresh,bool own)
            {this.owner=owner;this.item=item;this.context=context;this.serial=serial;this.fresh=fresh;this.own=own;}
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
                if(own || !ReferenceEquals(current,item) || owner.orders[context.UnitId]!=serial)return;
                current.Fresh=completion.NativeInvoked && completion.NativeReturned && completion.Failure==null && completion.NativeResult==0?fresh:null;
                current.FreshSerial=serial;
            }
        }
        private void ProcessReplans(int tick,int visitBudget)
        {
            int available=Math.Min(visitBudget,geometrySchedule.Count);
            while(available-->0 && geometrySchedule.Count!=0)
            {
                var item=geometrySchedule.First.Value;geometrySchedule.RemoveFirst();item.Node=null;
                if(!GeometryAge(item.CapturedTick,tick,out int age) || item.Attempts>=4 || !IsCurrent(item.Session,item.World))
                {DropGeometry(item.Witness.UnitId);continue;}
                var fresh=item.Fresh;item.Fresh=null;
                if(fresh!=null && item.FreshSerial==orders[item.Witness.UnitId])
                {
                    bool joined=false;UnitSnapshot snapshot=null;
                    try{joined=ReplanIntents.TryGetSnapshot(fresh,out var exact) && RescueGeometryWitness.TryJoin(item.Witness,exact,out snapshot);}
                    catch{ReportBounded("replan-intent-validation-failed");}
                    if(joined)
                    {
                        DropGeometry(item.Witness.UnitId);
                        string source=item.Source.StartsWith("replan/",StringComparison.Ordinal)?item.Source:"replan/"+item.Source;
                        var record=new RescuePendingCandidate<UnitSnapshot>(source,snapshot,age,item.Attempts,item.RetryIn);
                        if(!queue.OfferRestored(new RescueIdentity(item.Session,item.World,snapshot.Id,snapshot.GlobalId,item.FreshSerial),item.Mutation,record,tick))
                            ReportBounded("replan-candidate-transfer-rejected");
                        continue;
                    }
                }
                item.Node=geometrySchedule.AddLast(item);
            }
            // Unvisited failures cannot be acted on at a later arbitrary caller tail.
            // This is bounded metadata cleanup, with no unit reads or native calls.
            foreach(var item in geometry.Values)item.Fresh=null;
            unsettledReplans.Clear();
        }
    }
}
