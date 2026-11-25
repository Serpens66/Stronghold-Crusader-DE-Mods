using System;
using System.Collections.Generic;
using System.Reflection;
using APIShared.Units;
using APIShared.WorldMutations;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace TrappedUnitRescueTest.Tests
{
    // Real coordinator, queue, join, typed receipts and codecs. Only the engine and
    // admitted intent boundary are fake; native validation has its own928 checks.
    internal static class ReplanCoordinatorFixture
    {
        private static int checks;
        private static void Check(bool value,string why){checks++;if(!value)throw new Exception("Replan coordinator: "+why);}
        private sealed class Engine : IRescueMutationFootprintWorld
        {
            internal long Session=9,Generation=3;
            internal int Now=100,RescueCalls,WorldReads,TickReads;
            internal bool RescueSuccess=true;
            internal IReadOnlyList<RescueTile> PlannedWrites=Array.Empty<RescueTile>();
            internal bool CaptureSupported=true;
            internal Action<UnitSnapshot> DuringRescue;
            internal readonly List<UnitSnapshot> Captured=new List<UnitSnapshot>();
            internal readonly List<UnitSnapshot> Rescued=new List<UnitSnapshot>();
            public bool TryCapture(WorldMutationContext c,out List<UnitSnapshot> before,out string reason)
            {before=new List<UnitSnapshot>(Captured);reason=CaptureSupported?null:"fixture capture failure";return CaptureSupported;}
            public bool TryCapture(WorldMutationContext c,out List<UnitSnapshot> before,out IReadOnlyList<RescueTile> plannedWrites,out string reason)
            {plannedWrites=PlannedWrites;return TryCapture(c,out before,out reason);}
            public bool HasChangedTrapCandidate(UnitSnapshot before)=>true;
            public bool TryRescue(UnitSnapshot before,string cause,bool resume,out RescueResult result)
            {RescueCalls++;Rescued.Add(before);DuringRescue?.Invoke(before);result=new RescueResult{Status=RescueSuccess?RescueStatus.Rescued:RescueStatus.NoCandidate};return RescueSuccess;}
            public bool TryGetWorld(out long session,out long generation){WorldReads++;session=Session;generation=Generation;return true;}
            public bool TryGetTick(out int tick){TickReads++;tick=Now;return true;}
        }
        private sealed class IntentBoundary : IRescueReplanIntentWorld
        {
            internal int Captures,Refreshes;
            internal readonly Dictionary<int,UnitSnapshot> Live=new Dictionary<int,UnitSnapshot>();
            internal readonly HashSet<int> Rejected=new HashSet<int>();
            internal bool CaptureAllowed=true,ThrowCapture,ThrowRefresh;
            public bool TryCapture(int id,int x,int y,int extra,out RescueReplanIntent intent)
            {
                Captures++;intent=null;if(ThrowCapture)throw new InvalidOperationException("fixture capture failure");
                if(!CaptureAllowed || extra!=0 || !Live.TryGetValue(id,out var u) || u.RequestedX!=x || u.RequestedY!=y || u.Command==null)return false;
                var receipt=WorkerResumeTicket.ForCommand(u.Id,u.GlobalId,(eChimps)u.Type,u.AIState,(ushort)u.Owner,u.Tribe,u.TribeGlobalId,
                    (ushort)u.RequestedX,(ushort)u.RequestedY,u.Command);
                if(!receipt.IsValid)return false;intent=new RescueReplanIntent(u,receipt);return true;
            }
            public bool IsCurrent(RescueReplanIntent intent)=>intent!=null && !Rejected.Contains(intent.UnitId);
            public bool TryGetSnapshot(RescueReplanIntent intent,out UnitSnapshot exact)
            {
                Refreshes++;exact=null;
                if(ThrowRefresh)throw new InvalidOperationException("fixture validation failure");
                if(!IsCurrent(intent))return false;
                exact=intent.CopyBefore();return true;
            }
        }
        private sealed class Harness
        {
            internal readonly Engine Engine=new Engine();
            internal readonly IntentBoundary Intents=new IntentBoundary();
            internal readonly GeneralRescueObserver Observer;
            private long batch,move;
            internal Harness(bool active=true)
            {Observer=new GeneralRescueObserver(Engine,null,null){ReplanIntents=Intents,ReplanOwnerGuid="ours",OwnReplanScope=()=>false};if(active)Observer.SetSession(Engine.Session);}
            internal void Enqueue(params UnitSnapshot[] units)
            {
                Engine.Captured.Clear();Engine.Captured.AddRange(units);
                var context=Mutation(++batch,Engine.Session,Engine.Generation);var receipt=Observer.TryBegin(context);
                Check(receipt!=null,"observed mutation captured");receipt.Complete(MutationEnd(context));Observer.OnBatchCompleted(BatchEnd(batch,Engine.Session,Engine.Generation));
            }
            internal IUnitMoveInvocation Begin(UnitSnapshot fresh,out UnitMoveContext context,int extra=0)
            {
                Intents.Live[fresh.Id]=fresh;
                // Production observer priority: canonical effective order invalidation
                // precedes P7's capture of the final-original immutable context.
                Observer.ObserveOrder(fresh.Id);
                context=Move(++move,Engine.Session,Engine.Generation,fresh.Id,fresh.RequestedX,fresh.RequestedY,extra);
                return Observer.TryBegin(context);
            }
            internal void Fail(UnitSnapshot fresh,int extra=0)
            {var receipt=Begin(fresh,out var context,extra);Check(receipt!=null,"retained causal geometry interests actual original");receipt.Complete(MoveEnd(context));}
            internal void Tick(int tick){Engine.Now=tick;Observer.Tick(tick);}
        }
        private static UnitSnapshot Old(int id=1,bool step=false)
        {
            int x=20+id%20,y=30+id/20,tile=1000+id*3;
            var u=new UnitSnapshot{Id=id,GlobalId=(uint)(1000+id),Type=(int)eChimps.CHIMP_TYPE_SPEARMAN,Owner=1,
                X=x,Y=y,TileId=tile,NextX=step?x+1:x,NextY=y,NextTileId=step?tile+1:tile,
                Flags=step?2:0,Substep=step?3:8,OrdinaryStep=step,AIState=step?101:0,Health=100,
                RequestedX=70,RequestedY=80,AffectedCurrent=true,AffectedNext=step,CurrentWasOrdinary=true,NextWasOrdinary=true,
                CrewIdentity=Array.Empty<uint>()};
            u.Candidates.Add(new RescueCandidate{X=x,Y=y,Tile=tile,Distance=0});
            u.Candidates.Add(new RescueCandidate{X=x+1,Y=y,Tile=tile+1,Distance=1});
            u.Candidates.Add(new RescueCandidate{X=x-8,Y=y-8,Tile=tile+2,Distance=8});
            u.Command=Command(u,1);return u;
        }
        private static UnitSnapshot Fresh(UnitSnapshot old,bool atNext=false,ushort purpose=9)
        {
            var u=RescueReplanIntent.CopySnapshot(old);u.GeometryWitness=null;u.Candidates.Clear();
            if(atNext){u.X=old.NextX;u.Y=old.NextY;u.TileId=old.NextTileId;}
            u.NextX=u.X;u.NextY=u.Y;u.NextTileId=u.TileId;u.Flags=2;u.Substep=8;u.OrdinaryStep=false;
            u.AIState=101;u.AffectedCurrent=false;u.AffectedNext=false;u.CurrentWasOrdinary=false;u.NextWasOrdinary=false;
            u.GroundFlags=0x40000000u;u.NextGroundFlags=u.GroundFlags;u.Command=Command(u,purpose);return u;
        }
        private static CommandJourneySnapshot Command(UnitSnapshot u,ushort purpose)
        {
            Func<int,ushort> word=offset=>offset==0x6E4?(ushort)AliveState.IsAlive:offset==0x6E6?(ushort)u.Type:
                offset==0x6EE?(ushort)u.Owner:offset==0x918?(ushort)u.AIState:offset==0x930?(ushort)u.Tribe:
                offset==0x958?purpose:offset==0xA9E?(ushort)1:(ushort)0;
            Func<int,uint> dword=offset=>offset==0x6F0?u.GlobalId:offset==0x93C?u.TribeGlobalId:0u;
            CommandJourneySnapshot command;
            bool valid=u.Flags==0?CommandJourneySnapshot.TryCaptureStationary((eChimps)u.Type,u.AIState,word,dword,out command):
                CommandJourneySnapshot.TryCapture((eChimps)u.Type,u.AIState,word,dword,out command);
            Check(valid,"real typed command fixture");return command;
        }
        private static WorldMutationContext Mutation(long batch,long session,long generation)=>(WorldMutationContext)Activator.CreateInstance(typeof(WorldMutationContext),
            BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{WorldMutationSource.BuildStructure,batch,0L,batch,session,generation,new long[]{123,1,2,3,4,1,0,0}},null);
        private static WorldMutationCompletion MutationEnd(WorldMutationContext context)
        {
            var c=(WorldMutationCompletion)Activator.CreateInstance(typeof(WorldMutationCompletion),BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{context},null);
            typeof(WorldMutationCompletion).GetMethod("Set",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,new object[]{true,true,0L,null});return c;
        }
        private static WorldMutationBatchCompletion BatchEnd(long batch,long session,long generation)
        {
            var c=(WorldMutationBatchCompletion)Activator.CreateInstance(typeof(WorldMutationBatchCompletion),BindingFlags.Instance|BindingFlags.NonPublic,null,
                new object[]{batch,session,generation,WorldMutationSource.BuildStructure},null);
            typeof(WorldMutationBatchCompletion).GetMethod("Set",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,new object[]{true,false,true});return c;
        }
        private static UnitMoveContext Move(long invocation,long session,long generation,int id,int x,int y,int extra=0,string owner=null)=>
            (UnitMoveContext)Activator.CreateInstance(typeof(UnitMoveContext),BindingFlags.Instance|BindingFlags.NonPublic,null,
                new object[]{invocation,0L,session,generation,id,x,y,extra,owner},null);
        private static UnitMoveCompletion MoveEnd(UnitMoveContext context,long actual=0,bool invoked=true,bool returned=true,bool current=true,Exception failure=null)
        {
            var c=(UnitMoveCompletion)Activator.CreateInstance(typeof(UnitMoveCompletion),BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{context},null);
            typeof(UnitMoveCompletion).GetMethod("Set",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,new object[]{invoked,returned,actual,failure,current});return c;
        }
        internal static void Run()
        {
            checks=0;EnvelopeOverlapRetirement();FreshIntentAndSaveBoundary();OriginalAcceptedNextRoundTrip();NestedPublication();FirstBoundaryAndNewOrder();RejectedCallsAndInvalidation();
            OriginalLifetimeAndAttempts();CapacityAndBudgets();GeometryOnlyLoadAndDisable();
            Console.WriteLine("ReplanCoordinatorFixture: "+checks+" checks passed; actual General coordinator/queue/join/codecs, no native calls");
        }
        private static void FreshIntentAndSaveBoundary()
        {
            var h=new Harness();var old=Old();h.Enqueue(old);var fresh=Fresh(old);
            h.Engine.DuringRescue=snapshot=>Check(!h.Observer.TryCapturePending(out _) && !h.Observer.TryCaptureGeometry(out _),"atomic save also waits until synchronous recovery finishes");
            var receipt=h.Begin(fresh,out var context);
            Check(receipt!=null && h.Observer.GetOrderSerial(old.Id)==1 && h.Observer.PendingCount==1,"external Pre revokes old queue intent and retains geometry");
            Check(h.Engine.RescueCalls==0 && h.Intents.Refreshes==0,"Begin never validates late intent or mutates");
            Check(h.Observer.HasUnsettledReplans && !h.Observer.TryCapturePending(out _) && !h.Observer.TryCaptureGeometry(out _),"save refused from Begin until admitted Tick");
            receipt.Complete(MoveEnd(context));receipt.Complete(MoveEnd(context));
            Check(h.Observer.HasUnsettledReplans && h.Engine.RescueCalls==0,"actual zero Completion is single-use and not recovery");
            Check(!h.Observer.TryCapturePending(out _) && !h.Observer.TryCaptureGeometry(out _),"original completion is still before caller/legacy Post boundary");
            h.Tick(100);
            Check(h.Engine.RescueCalls==1 && h.Observer.PendingCount==0 && !h.Observer.HasUnsettledReplans,"first admitted Tick joins and rescues once");
            var joined=h.Engine.Rescued[0];
            Check(ReferenceEquals(joined.Command,fresh.Command) && !ReferenceEquals(joined.Command,old.Command) && joined.AIState==101,"same-goal fresh command purpose replaces old idle purpose");
            Check(joined.Flags==2 && joined.Substep==8 && !joined.OrdinaryStep && joined.GroundFlags==old.GroundFlags && joined.CurrentWasOrdinary,"fresh pose and original geometry stay separate");
            Check(joined.GeometryWitness!=null && joined.Candidates.Count==old.Candidates.Count && joined.Candidates[2].Tile==old.Candidates[2].Tile,"old reachable bank remains original evidence");
            h.Tick(100);Check(h.Engine.RescueCalls==1,"duplicate simulation callback cannot rescue twice");
            Check(h.Observer.TryCapturePending(out var pending) && pending.Length==0 && h.Observer.TryCaptureGeometry(out var geometry) && geometry.Length==0,"settled successful Tick restores atomic-save admission");
        }
        private static void EnvelopeOverlapRetirement()
        {
            for(int kind=0;kind<6;kind++)
            {
                var h=new Harness();var old=Old(step:kind==2 || kind==4);
                if(kind<=2)
                {
                    h.Enqueue(old);
                    if(kind==0)h.Observer.ObserveOrder(old.Id);
                    if(kind==2){h.Engine.RescueSuccess=false;h.Fail(Fresh(old,true));h.Tick(100);}
                }
                else if(kind==3)h.Observer.StagePending(new[]{new RescuePendingCandidate<UnitSnapshot>("BuildStructure",old,0,0,0)});
                else
                {
                    Check(RescueGeometryWitness.TryCapture(old,out var witness),"historical storage witness fixture");
                    if(kind==4)
                    {Check(RescueGeometryWitness.TryJoin(witness,Fresh(old,true),out var joined),"joined staged fixture");h.Observer.StagePending(new[]{new RescuePendingCandidate<UnitSnapshot>("replan/BuildStructure",joined,0,0,0)});}
                    else h.Observer.StageGeometry(new[]{new RescuePendingGeometry("BuildStructure",witness,0,0,0)});
                }
                int calls=h.Engine.RescueCalls;Check(h.Observer.PendingCount==1,"historical storage admitted kind"+kind);
                h.Engine.Captured.Clear();
                // Joined cases use the original left radius8 border: distance9 from
                // fresh acceptedNext, not the incorrectly shifted fresh square.
                h.Engine.PlannedWrites=new[]{new RescueTile(old.X-(kind==2 || kind==4?8:-3),old.Y+3,20000)};
                Check(h.Observer.TryBegin(Mutation(900,h.Engine.Session,h.Engine.Generation))==null,"later writer has zero local actors kind"+kind);
                Check(h.Observer.PendingCount==0,"envelope overlap retires historical proof before original kind"+kind);
                h.Observer.OnBatchCompleted(BatchEnd(900,h.Engine.Session,h.Engine.Generation));h.Tick(101);
                Check(h.Engine.RescueCalls==calls,"retired proof cannot rescue later kind"+kind);
            }
            var separate=new Harness();var near=Old();var far=Old(2);Shift(far,200,200);separate.Enqueue(near,far);
            separate.Observer.ObserveOrder(near.Id);separate.Observer.ObserveOrder(far.Id);separate.Engine.Captured.Clear();
            separate.Engine.PlannedWrites=new[]{new RescueTile(near.X+3,near.Y+3,20000)};separate.Engine.CaptureSupported=false;
            Check(separate.Observer.TryBegin(Mutation(901,9,3))==null,"unit discovery failure publishes no new proof");
            Check(separate.Observer.PendingCount==1 && separate.Observer.IsInterestedInOrder(far.Id) && !separate.Observer.IsInterestedInOrder(near.Id),
                "complete plan survives failed unit capture and preserves unrelated historical bank");
            separate.Engine.PlannedWrites=null;
            Check(separate.Observer.TryBegin(Mutation(902,9,3))==null && separate.Observer.PendingCount==0,"unknown plan retires all unknown affected histories");
            var sparse=new Harness();near=Old();sparse.Enqueue(near);sparse.Observer.ObserveOrder(near.Id);sparse.Engine.Captured.Clear();
            sparse.Engine.PlannedWrites=new[]{new RescueTile(0,0,20000),new RescueTile(100,100,20001)};
            sparse.Observer.TryBegin(Mutation(903,9,3));Check(sparse.Observer.PendingCount==1,"bounding-box overlap alone does not discard unrelated sparse writes");
            var active=new Harness();near=Old();active.Enqueue(near);var waiting=active.Begin(Fresh(near),out var moveContext);
            active.Engine.Captured.Clear();active.Engine.PlannedWrites=new[]{new RescueTile(near.X+3,near.Y+3,20000)};
            active.Observer.TryBegin(Mutation(904,9,3));waiting.Complete(MoveEnd(moveContext));active.Tick(101);
            Check(active.Observer.PendingCount==0 && active.Engine.RescueCalls==0 && !active.Observer.HasUnsettledReplans,"conflict cancels unsettled receipt and late zero cannot revive it");
            CompoundEnvelopeCases();
        }
        private static void Shift(UnitSnapshot u,int dx,int dy)
        {u.X+=dx;u.Y+=dy;u.NextX+=dx;u.NextY+=dy;for(int i=0;i<u.Candidates.Count;i++){var c=u.Candidates[i];c.X+=dx;c.Y+=dy;u.Candidates[i]=c;}}
        private static void CompoundEnvelopeCases()
        {
            foreach(bool failedFirst in new[]{true,false})
            {
                var h=new Harness();var old=Old();var outer=Mutation(910,9,3);
                h.Engine.Captured.Add(old);h.Engine.CaptureSupported=!failedFirst;h.Engine.PlannedWrites=failedFirst?null:Array.Empty<RescueTile>();
                var first=h.Observer.TryBegin(outer);h.Engine.CaptureSupported=failedFirst;h.Engine.PlannedWrites=failedFirst?Array.Empty<RescueTile>():null;
                var child=Mutation(910,9,3);var next=h.Observer.TryBegin(child);
                first?.Complete(MutationEnd(outer));next?.Complete(MutationEnd(child));h.Observer.OnBatchCompleted(BatchEnd(910,9,3));h.Tick(100);
                Check(h.Observer.PendingCount==0 && h.Engine.RescueCalls==0,"failed first/child prevents partial samebatch evidence first="+failedFirst);
            }
            var complete=new Harness();var source=Old();complete.Engine.Captured.Add(source);var parent=Mutation(911,9,3);
            var parentReceipt=complete.Observer.TryBegin(parent);var nested=Mutation(911,9,3);
            complete.Engine.PlannedWrites=new[]{new RescueTile(source.X+3,source.Y+3,20000)};
            var nestedReceipt=complete.Observer.TryBegin(nested);nestedReceipt.Complete(MutationEnd(nested));parentReceipt.Complete(MutationEnd(parent));
            complete.Observer.OnBatchCompleted(BatchEnd(911,9,3));complete.Tick(100);
            Check(complete.Engine.RescueCalls==1 && ReferenceEquals(source,complete.Engine.Rescued[0]),"successful complete samebatch nested captures preserve earliest fresh event proof");
            var conflict=new Harness();source=Old();conflict.Enqueue(source);conflict.Engine.Captured.Clear();
            conflict.Engine.PlannedWrites=new[]{new RescueTile(source.X+3,source.Y+3,20000)};var firstWriter=Mutation(912,9,3);
            Check(conflict.Observer.TryBegin(firstWriter)==null && conflict.Observer.PendingCount==0,"zero-actor first writer retires prior normal queue");
            conflict.Engine.Captured.Add(Old());var secondWriter=Mutation(912,9,3);var second=conflict.Observer.TryBegin(secondWriter);
            second?.Complete(MutationEnd(secondWriter));conflict.Observer.OnBatchCompleted(BatchEnd(912,9,3));conflict.Tick(100);
            Check(conflict.Engine.RescueCalls==0,"samecompound child cannot re-admit a retired conflicting actor");
            var overflow=new Harness();overflow.Engine.CaptureSupported=false;overflow.Engine.PlannedWrites=null;
            for(int i=0;i<65;i++)overflow.Observer.TryBegin(Mutation(1000+i,9,3));
            overflow.Engine.CaptureSupported=true;overflow.Engine.PlannedWrites=Array.Empty<RescueTile>();overflow.Engine.Captured.Add(Old());
            Check(overflow.Observer.TryBegin(Mutation(1100,9,3))==null,"failed-batch tombstone overflow remains fail-closed without unbounded metadata");
            overflow.Engine.Generation++;var resumed=overflow.Observer.TryBegin(Mutation(1101,9,overflow.Engine.Generation));
            Check(resumed!=null,"new world resets bounded failed-batch overflow latch");
        }
        private static void OriginalAcceptedNextRoundTrip()
        {
            var h=new Harness();h.Engine.RescueSuccess=false;var old=Old(step:true);h.Enqueue(old);h.Fail(Fresh(old,true));h.Tick(100);
            Check(h.Observer.TryCapturePending(out var pending) && pending.Length==1,"failed local recovery remains bounded pending work");
            var saved=pending[0];var u=saved.Snapshot;
            Check(u.X==old.NextX && u.NextX==u.X && !u.OrdinaryStep && u.GeometryWitness.HasAcceptedNext,"fresh Current rebases onto original accepted Next without invented successor");
            Check(Math.Abs(u.Candidates[2].X-u.X)==9 && u.Candidates[0].Distance==0 && u.Candidates[0].X==old.X && u.Candidates[0].Tile==old.TileId,"old radius and zero-distance anchor do not shift/truncate");
            RescuePendingCandidate<UnitSnapshot> loaded=default;
            Check(RescuePendingCodec.TryEncode(saved,out var bytes) && RescuePendingCodec.TryDecode(bytes,out loaded),"version4 joined pending proof round trips");
            Check(loaded.Snapshot.GeometryWitness.Current.X==old.X && loaded.Snapshot.Candidates[2].X==old.Candidates[2].X &&
                loaded.Snapshot.Candidates[0].Distance==0 && RescueGeometryCodec.CanonicalJoin(loaded.Snapshot),"loaded joined proof validates in original coordinate frame");
            var corrupt=RescueReplanIntent.CopySnapshot(loaded.Snapshot);corrupt.Candidates[0]=new RescueCandidate{X=u.X,Y=u.Y,Tile=u.TileId,Distance=0};
            Check(!RescuePendingCodec.TryEncode(new RescuePendingCandidate<UnitSnapshot>(saved.Source,corrupt,saved.Age,saved.Attempts,saved.RetryIn),out _),"shifted historical zero-anchor rejected by codec");
            var restored=new Harness();restored.Engine.Now=200;restored.Observer.StagePending(new[]{loaded});restored.Tick(200);
            Check(restored.Engine.RescueCalls==0,"saved retry delay is preserved on load");restored.Tick(201);
            Check(restored.Engine.RescueCalls==1 && restored.Engine.Rescued[0].GeometryWitness.Current.Tile==old.TileId,"loaded retry uses fresh intent and original source bank");
        }
        private static void NestedPublication()
        {
            var h=new Harness();var old=Old();h.Enqueue(old);var outer=h.Begin(Fresh(old),out var outerContext);
            var inner=h.Begin(Fresh(old,purpose:11),out var innerContext);inner.Complete(MoveEnd(innerContext));outer.Complete(MoveEnd(outerContext,1));
            h.Tick(100);Check(h.Engine.RescueCalls==0 && h.Observer.PendingCount==0,"older outer positive original revokes newer nested zero per unit");
            h=new Harness();var a=Old(1);var b=Old(2);h.Enqueue(a,b);
            var first=h.Begin(Fresh(a),out var ca);var other=h.Begin(Fresh(b),out var cb);
            first.Complete(MoveEnd(ca));other.Complete(MoveEnd(cb,1));h.Tick(100);
            Check(h.Engine.RescueCalls==1 && h.Engine.Rescued[0].Id==a.Id,"another unit's positive original does not revoke matching zero");
            h=new Harness();old=Old();h.Enqueue(old);outer=h.Begin(Fresh(old),out outerContext);
            inner=h.Begin(Fresh(old,purpose:12),out innerContext);inner.Complete(MoveEnd(innerContext));outer.Complete(MoveEnd(outerContext));h.Tick(100);
            Check(h.Engine.RescueCalls==1 && h.Engine.Rescued[0].Command.TryGetValue(0x958,out uint purpose) && purpose==12,"older outer zero cannot overwrite newer nested fresh purpose");
        }
        private static void RejectedCallsAndInvalidation()
        {
            foreach(int mode in new[]{0,1,2,3,4,5})
            {
                var h=new Harness();var old=Old();h.Enqueue(old);var receipt=h.Begin(Fresh(old),out var context,mode==5?1:0);
                Check(receipt!=null,"interested unsuccessful invocation has private completion");
                receipt.Complete(MoveEnd(context,mode==4?2:0,invoked:mode!=0,returned:mode!=1,current:mode!=2,
                    failure:mode==3?new InvalidOperationException():null));h.Tick(100);
                Check(h.Engine.RescueCalls==0 && !h.Observer.HasUnsettledReplans,"veto/throw/staleworld/failure/unsupportedresult/selector cannot rescue mode"+mode);
            }
            foreach(string invalid in new[]{"new-goal","AI","job-or-role","pose","unit-lifetime","group","external-target"})
            {
                var h=new Harness();var old=Old();h.Enqueue(old);h.Fail(Fresh(old));
                // The fake denies the exact read-only boundary. Its native implementation
                // separately tests each reason; this tests coordinator propagation.
                h.Intents.Rejected.Add(old.Id);h.Tick(100);
                Check(h.Engine.RescueCalls==0 && !h.Observer.HasUnsettledReplans && h.Observer.PendingCount==0,"Tick-time boundary rejection retires both trigger and geometry: "+invalid);
            }
            foreach(bool beginThrows in new[]{true,false})
            {
                var h=new Harness();var old=Old();h.Enqueue(old);h.Intents.ThrowCapture=beginThrows;h.Intents.ThrowRefresh=!beginThrows;
                h.Fail(Fresh(old));h.Tick(100);Check(h.Engine.RescueCalls==0 && !h.Observer.HasUnsettledReplans,"isolated boundary exception does not authorize recovery");
                Check(h.Observer.PendingCount==(beginThrows?1:0),"capture exception retains only unknown geometry; Tick validation exception retires qualified proof");
            }
            var changed=new Harness();var source=Old(step:true);changed.Enqueue(source);var escaped=Fresh(source,true);escaped.X++;escaped.NextX=escaped.X;escaped.TileId+=50;escaped.NextTileId=escaped.TileId;
            Check(changed.Begin(escaped,out _)==null,"qualified departure is rejected before creating a replan receipt");
            changed.Tick(100);Check(changed.Engine.RescueCalls==0,"fresh unproved second pose never joins original bank");
            Check(changed.Observer.PendingCount==0,"observed departure retires spatial witness rather than retaining it for a later return");
        }
        private static void FirstBoundaryAndNewOrder()
        {
            var h=new Harness();var old=Old();h.Enqueue(old);var receipt=h.Begin(Fresh(old),out var context);
            h.Tick(100);receipt.Complete(MoveEnd(context));h.Tick(101);
            Check(h.Engine.RescueCalls==0 && h.Observer.PendingCount==1 && !h.Observer.HasUnsettledReplans,"late zero completion cannot arm a later arbitrary caller boundary");
            h=new Harness();old=Old();h.Enqueue(old);receipt=h.Begin(Fresh(old),out context);
            h.Tick(100);receipt.Complete(MoveEnd(context,1));
            Check(h.Observer.PendingCount==0,"late positive completion still retires the retained witness");
            h=new Harness();old=Old();h.Enqueue(old);h.Fail(Fresh(old));
            h.Observer.ObserveOrder(old.Id);h.Tick(100);
            Check(h.Engine.RescueCalls==0 && h.Observer.TryCaptureGeometry(out var geometry) && geometry.Length==1,"new order vetoed before final-original Begin cancels older trigger without replay");
            h=new Harness();old=Old();h.Enqueue(old);h.Fail(Fresh(old));var newer=Fresh(old,purpose:13);newer.RequestedX++;
            h.Fail(newer);h.Tick(100);
            Check(h.Engine.RescueCalls==1 && h.Engine.Rescued[0].RequestedX==newer.RequestedX &&
                ReferenceEquals(h.Engine.Rescued[0].Command,newer.Command),"new goal and purpose captured by newer zero are authoritative");
        }
        private static void OriginalLifetimeAndAttempts()
        {
            var h=new Harness();h.Engine.RescueSuccess=false;var old=Old();h.Enqueue(old);
            for(int attempt=0;attempt<4;attempt++)
            {
                h.Engine.Now=100+attempt;h.Fail(Fresh(old,purpose:(ushort)(20+attempt)));h.Tick(100+attempt);
                Check(h.Engine.RescueCalls==attempt+1,"replan shares original cumulative attempt budget "+attempt);
                if(attempt<3)
                {Check(h.Observer.TryCapturePending(out var records) && records.Length==1 && records[0].Attempts==attempt+1 && records[0].Age==attempt,"new order never resets original age/attempts");}
            }
            Check(h.Observer.PendingCount==0,"fourth unsuccessful attempt exhausts original allowance");
            var fifth=h.Begin(Fresh(old),out _);Check(fifth==null,"fifth order cannot invent fresh geometry after exhausted proof");
            h=new Harness();old=Old();h.Enqueue(old);h.Intents.CaptureAllowed=false;
            foreach(int tick in new[]{100,140,179})
            {h.Engine.Now=tick;h.Fail(Fresh(old));h.Tick(tick);Check(h.Observer.TryCaptureGeometry(out var records) && records.Length==1 && records[0].Age==tick-100,"failed replan retention preserves original TTL");}
            h.Engine.Now=180;h.Intents.CaptureAllowed=true;h.Fail(Fresh(old));h.Tick(180);
            Check(h.Engine.RescueCalls==0 && h.Observer.PendingCount==0,"original80-tick expiry cannot be renewed by another failed order");
            var delayed=new Harness();old=Old();Check(RescueGeometryWitness.TryCapture(old,out var witness),"loaded retry geometry fixture");
            delayed.Engine.Now=400;delayed.Observer.StageGeometry(new[]{new RescuePendingGeometry("BuildStructure",witness,17,2,1)});
            delayed.Fail(Fresh(old,purpose:31));delayed.Fail(Fresh(old,purpose:32));delayed.Tick(400);
            Check(delayed.Engine.RescueCalls==0 && delayed.Observer.TryCapturePending(out var waiting) && waiting.Length==1 &&
                waiting[0].Age==17 && waiting[0].Attempts==2 && waiting[0].RetryIn==1,"repeated replans preserve original absolute retry due tick and counters");
            delayed.Tick(401);Check(delayed.Engine.RescueCalls==1 && delayed.Engine.Rescued[0].Command.TryGetValue(0x958,out uint selected) && selected==32,
                "saved retry becomes due once with newest fresh purpose");
        }
        private static void CapacityAndBudgets()
        {
            var h=new Harness();var units=new UnitSnapshot[512];for(int i=0;i<units.Length;i++)units[i]=Old(i+1);h.Enqueue(units);
            Check(h.Observer.PendingCount==512,"normal queue reaches bounded512 capacity");h.Observer.ObserveOrder(1);
            RescuePendingGeometry[] geometry=null;RescuePendingCandidate<UnitSnapshot>[] pending=null;
            Check(h.Observer.PendingCount==512 && h.Observer.TryCaptureGeometry(out geometry) && geometry.Length==1 &&
                h.Observer.TryCapturePending(out pending) && pending.Length==511,"geometry transfer shares capacity instead of allocating a parallel allowance");
            h.Enqueue(Old(513));Check(h.Observer.PendingCount==512 && !h.Observer.IsInterestedInOrder(513),"new normal source cannot exceed shared512 budget");
            var loaded=new Harness();loaded.Observer.StagePending(pending);loaded.Observer.StageGeometry(geometry);
            Check(loaded.Observer.PendingCount==512,"mixed staged pending/geometry shares same capacity");
            h=new Harness();units=new UnitSnapshot[47];for(int i=0;i<40;i++)units[i]=Old(i+1);
            for(int i=40;i<47;i++)units[i]=Old(101+i-40);h.Enqueue(units);
            for(int i=0;i<40;i++)h.Fail(Fresh(units[i]));h.Tick(100);
            Check(h.Intents.Refreshes==32 && h.Engine.RescueCalls==8,"first Tick bounds expensive joins32 and shared recovery attempts8");
            int normal=0,replans=0;foreach(var rescued in h.Engine.Rescued)if(rescued.GeometryWitness==null)normal++;else replans++;
            Check(normal==7 && replans==1,"normal and replan candidates consume one shared8-attempt allowance");
            Check(!h.Observer.HasUnsettledReplans,"unvisited first-boundary triggers are discarded, not moved to arbitrary later tails");
            h.Tick(100);Check(h.Intents.Refreshes==32 && h.Engine.RescueCalls==8,"duplicate Tick cannot spend join/attempt budgets twice");
        }
        private static void GeometryOnlyLoadAndDisable()
        {
            var h=new Harness();var old=Old();h.Enqueue(old);h.Engine.Now=120;h.Observer.ObserveOrder(old.Id);
            Check(h.Observer.TryCaptureGeometry(out var geometry) && geometry.Length==1 && geometry[0].Age==20 && geometry[0].Attempts==0,"settled retained geometry exports original metadata only");
            RescuePendingGeometry record=default;
            Check(RescueGeometryCodec.TryEncode(geometry[0],out var bytes) && RescueGeometryCodec.TryDecode(bytes,out record),"geometry-only record round trips independently of invalidated old purpose");
            var disabled=new Harness(false);disabled.Observer.StageGeometry(new[]{record});int reads=disabled.Engine.WorldReads;
            Check(disabled.Observer.TryCaptureGeometry(out var paused) && paused.Length==1 && disabled.Engine.WorldReads==reads,"disabled paused load can re-save managed geometry without unit/world reads");
            disabled.Tick(300);Check(disabled.Observer.PendingCount==0 && disabled.Engine.RescueCalls==0,"disabled simulation progress expires staged geometry without action");
            var active=new Harness();active.Engine.Now=200;active.Observer.StageGeometry(new[]{record});
            active.Fail(Fresh(old));active.Tick(200);
            Check(active.Engine.RescueCalls==1 && active.Engine.Rescued[0].GeometryWitness.Current.Tile==old.TileId,"first new failed order can rebind loaded original geometry to fresh intent");
            var expired=new Harness();expired.Engine.Now=500;
            expired.Observer.StageGeometry(new[]{new RescuePendingGeometry(record.Source,record.Witness,79,3,0)});
            expired.Intents.CaptureAllowed=false;expired.Fail(Fresh(old));expired.Tick(500);
            expired.Engine.Now=501;expired.Intents.CaptureAllowed=true;expired.Fail(Fresh(old));expired.Tick(501);
            Check(expired.Engine.RescueCalls==0 && expired.Observer.PendingCount==0,"loaded remaining lifetime is not refreshed by new orders");
        }
    }
}
