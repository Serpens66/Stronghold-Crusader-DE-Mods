using System;
using System.Collections.Generic;
using System.Reflection;
using APIShared.WorldMutations;
using APIShared.Units;

namespace TrappedUnitRescueTest.Tests
{
    // Execute the production coordinator, queue and receipts. Only engine access is supplied
    // by a fake; the private API constructors are used solely to construct broker inputs.
    internal static class GeneralObserverFixture
    {
        private static int assertions;
        private sealed class World : IRescueMutationFootprintWorld, IRescueMutationEnvelopeWorld
        {
            internal long Session=9,Generation=3;
            internal int TickValue=100,CaptureCalls,ChangedCalls,RescueCalls,WorldReads,TickReads,RemovalRequests,NativeWrites;
            internal bool Available=true,CaptureSupported=true,TickAvailable=true,Changed=true;
            internal List<UnitSnapshot> Next=new List<UnitSnapshot>();
            internal IReadOnlyList<RescueTile> Writes=Array.Empty<RescueTile>();
            internal readonly List<UnitSnapshot> Rescued=new List<UnitSnapshot>();
            internal Action OnRescue;
            internal Action<UnitSnapshot> OnOperation;
            internal Func<UnitSnapshot,RescueStatus> ResolveStatus;
            internal bool RescueSuccess=true,ThrowRescue;
            internal RescueStatus FailureStatus=RescueStatus.NoCandidate;
            public bool TryCapture(WorldMutationContext context,out List<UnitSnapshot> before,out string reason)
            {CaptureCalls++;before=Next;reason=CaptureSupported?null:"unsupported";return CaptureSupported;}
            // This fixture isolates queue/receipt policy; real geometry conflicts
            // and nonempty envelopes are exercised in ReplanCoordinatorFixture.
            public bool TryCapture(WorldMutationContext context,out List<UnitSnapshot> before,out IReadOnlyList<RescueTile> plannedWrites,out string reason)
            {plannedWrites=Writes;return TryCapture(context,out before,out reason);}
            public bool TryPlan(WorldMutationContext context,out IReadOnlyList<RescueTile> plannedWrites,out string reason)
            {plannedWrites=Writes;reason=null;return true;}
            public bool HasChangedTrapCandidate(UnitSnapshot before){ChangedCalls++;return Changed;}
            public bool TryRescue(UnitSnapshot before,string cause,bool resume,out RescueResult result)
            {
                RescueCalls++;Rescued.Add(before);OnOperation?.Invoke(before);OnRescue?.Invoke();
                if(ThrowRescue)throw new InvalidOperationException("native fixture failure");
                var status=ResolveStatus!=null?ResolveStatus(before):RescueSuccess?
                    (before.CivilianRemoval?RescueStatus.RemovalRequested:RescueStatus.Rescued):FailureStatus;
                if(status==RescueStatus.RemovalRequested)RemovalRequests++;
                if(status==RescueStatus.Rescued || status==RescueStatus.RemovalRequested)NativeWrites++;
                result=new RescueResult{Status=status};
                return status==RescueStatus.Rescued || status==RescueStatus.RemovalRequested;
            }
            public bool TryGetWorld(out long session,out long generation){WorldReads++;session=Session;generation=Generation;return Available;}
            public bool TryGetTick(out int tick){TickReads++;tick=TickValue;return TickAvailable;}
        }
        // Older/custom test adapters can capture normally but cannot plan during
        // reentry. The coordinator must fail closed without calling capture again.
        private sealed class CaptureOnlyWorld : IRescueMutationFootprintWorld
        {
            private readonly World inner;
            internal CaptureOnlyWorld(World inner){this.inner=inner;}
            public bool TryCapture(WorldMutationContext c,out List<UnitSnapshot> before,out string reason)=>inner.TryCapture(c,out before,out reason);
            public bool TryCapture(WorldMutationContext c,out List<UnitSnapshot> before,out IReadOnlyList<RescueTile> writes,out string reason)=>inner.TryCapture(c,out before,out writes,out reason);
            public bool HasChangedTrapCandidate(UnitSnapshot before)=>inner.HasChangedTrapCandidate(before);
            public bool TryRescue(UnitSnapshot before,string cause,bool resume,out RescueResult result)=>inner.TryRescue(before,cause,resume,out result);
            public bool TryGetWorld(out long session,out long generation)=>inner.TryGetWorld(out session,out generation);
            public bool TryGetTick(out int tick)=>inner.TryGetTick(out tick);
        }
        private static GeneralRescueObserver New(World world,Action<bool> suppression=null,Action<string> report=null)
        {var observer=new GeneralRescueObserver(world,suppression,report);observer.SetSession(world.Session);return observer;}
        private static UnitSnapshot Unit(int id,uint? global=null)
        {
            var unit=new UnitSnapshot{Id=id,GlobalId=global??(uint)(1000+id),Owner=1,TileId=50+id,NextTileId=50+id,Substep=8,
                RequestedX=20,RequestedY=30,AffectedCurrent=true,CurrentWasOrdinary=true,Health=100,
                Type=(int)SHCDESE.Interop.eChimps.CHIMP_TYPE_SPEARMAN,CrewIdentity=Array.Empty<uint>()};
            CommandJourneySnapshot.TryCaptureStationary((SHCDESE.Interop.eChimps)unit.Type,unit.AIState,
                offset=>offset==0x6E4?(ushort)2:offset==0x6E6?(ushort)unit.Type:offset==0x6EE?(ushort)unit.Owner:
                    offset==0xA9E?(ushort)1:(ushort)0,offset=>offset==0x6F0?unit.GlobalId:0,out unit.Command);
            return unit;
        }
        private static List<UnitSnapshot> Units(int first,int count)
        {var result=new List<UnitSnapshot>();for(int i=0;i<count;i++)result.Add(Unit(first+i));return result;}
        private static WorldMutationContext Context(long batch,long invocation=1,long session=9,long generation=3,
            WorldMutationSource source=WorldMutationSource.BuildStructure)=>
            (WorldMutationContext)Activator.CreateInstance(typeof(WorldMutationContext),BindingFlags.Instance|BindingFlags.NonPublic,null,
                new object[]{source,invocation,0L,batch,session,generation,new long[]{123,1,2,3,4,1,0,0}},null);
        private static WorldMutationCompletion Outcome(WorldMutationContext context,bool invoked=true,bool returned=true)
        {
            var result=(WorldMutationCompletion)Activator.CreateInstance(typeof(WorldMutationCompletion),BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{context},null);
            typeof(WorldMutationCompletion).GetMethod("Set",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(result,new object[]{invoked,returned,0L,null});
            return result;
        }
        private static WorldMutationBatchCompletion Batch(long batch,bool returned=true,bool failure=false,bool current=true,long session=9,long generation=3)
        {
            var result=(WorldMutationBatchCompletion)Activator.CreateInstance(typeof(WorldMutationBatchCompletion),BindingFlags.Instance|BindingFlags.NonPublic,null,
                new object[]{batch,session,generation,WorldMutationSource.BuildStructure},null);
            typeof(WorldMutationBatchCompletion).GetMethod("Set",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(result,new object[]{returned,failure,current});
            return result;
        }
        private static IWorldMutationInvocation Begin(GeneralRescueObserver observer,World world,long batch,List<UnitSnapshot> units,long invocation=1)
        {world.Next=units;return observer.TryBegin(Context(batch,invocation,world.Session,world.Generation));}
        private static void Enqueue(GeneralRescueObserver observer,World world,long batch,List<UnitSnapshot> units)
        {
            var context=Context(batch,1,world.Session,world.Generation);world.Next=units;
            var receipt=observer.TryBegin(context);Require(receipt!=null,"capture produces a receipt");
            receipt.Complete(Outcome(context));observer.OnBatchCompleted(Batch(batch,session:world.Session,generation:world.Generation));
        }
        internal static void Run()
        {
            assertions=0;
            ReceiptsAndDeferredExecution();
            NestedAndConflictingMutations();
            FailureAndLifetime();
            BudgetsAndCapacity();
            ReentrancyAndExceptions();
            PendingSaveAndRestore();
            MixedRemovalAndMovement();
            RemovalSurvivesOrders();
            Console.WriteLine("PASS: production general coordinator assertions="+assertions+"; actual queue/batches/receipts; no native calls or hooks");
        }
        private static void ReceiptsAndDeferredExecution()
        {
            var world=new World();var observer=New(world);var before=Unit(1);world.Next=new List<UnitSnapshot>{before};
            var context=Context(1);var receipt=observer.TryBegin(context);
            Require(receipt!=null && observer.CaptureCount==1 && observer.PendingCount==0,"begin only captures old proof");
            Require(world.RescueCalls==0 && world.ChangedCalls==0,"child begin does not evaluate or relocate");
            receipt.Complete(Outcome(context));receipt.Complete(Outcome(context));
            Require(observer.PendingCount==0 && world.RescueCalls==0 && world.ChangedCalls==0,"child completion never performs native recovery or postcheck");
            observer.OnBatchCompleted(Batch(1));
            Require(observer.PendingCount==1 && world.RescueCalls==0 && world.ChangedCalls==1,"outer completion queues after postcheck");
            observer.Tick(100);
            Require(observer.PendingCount==0 && world.RescueCalls==1 && observer.RescueCount==1,"admitted tick performs recovery");
            Require(ReferenceEquals(before,world.Rescued[0]),"the original snapshot reaches the real queue evaluator");
            Require(!observer.IsInterestedInOrder(1) && !observer.IsInterestedInOrder(9999),"completed work leaves no per-unit command interest");
            var splitWorld=new World();var split=New(splitWorld);var outerSplit=Context(991,1);var innerSplit=Context(991,2);
            splitWorld.Next=Units(1,2);var outerReceipt=split.TryBegin(outerSplit);
            Require(split.IsInterestedInOrder(1) && split.IsInterestedInOrder(2) && !split.IsInterestedInOrder(3),"batch interest is exact and indexed by actor");
            var replacedActor=Unit(1);replacedActor.GlobalId=999;splitWorld.Next=new List<UnitSnapshot>{replacedActor};
            var innerReceipt=split.TryBegin(innerSplit);innerReceipt.Complete(Outcome(innerSplit));outerReceipt.Complete(Outcome(outerSplit));
            split.OnBatchCompleted(Batch(991));split.Tick(100);
            Require(splitWorld.Rescued.Count==1 && splitWorld.Rescued[0].Id==2,"one retired actor cannot discard another actor's complete valid proof");
            var noopWorld=new World();var noop=New(noopWorld);noopWorld.Next=new List<UnitSnapshot>();
            Require(noop.TryBegin(Context(2))==null,"empty region has no receipt");noop.OnBatchCompleted(Batch(2));noop.Tick(100);
            Require(noop.PendingCount==0 && noopWorld.RescueCalls==0 && noopWorld.ChangedCalls==0,"empty mutation creates no rescue work");
            var unchangedWorld=new World{Changed=false};var unchanged=New(unchangedWorld);Enqueue(unchanged,unchangedWorld,3,Units(1,1));
            Require(unchanged.PendingCount==0 && unchangedWorld.RescueCalls==0,"normal zero-result no-op needs actual new trap proof");
        }
        private static void NestedAndConflictingMutations()
        {
            var world=new World();var observer=New(world);var earliest=Unit(1);earliest.Candidates.Add(new RescueCandidate{Tile=4});
            var outer=Context(10,10,source:WorldMutationSource.AivBuildStep);world.Next=new List<UnitSnapshot>{earliest};var a=observer.TryBegin(outer);
            var later=Unit(1);later.AffectedCurrent=false;later.AffectedNext=true;later.Candidates.Add(new RescueCandidate{Tile=999});
            var inner=Context(10,11);world.Next=new List<UnitSnapshot>{later};var b=observer.TryBegin(inner);
            b.Complete(Outcome(inner));a.Complete(Outcome(outer));observer.OnBatchCompleted(Batch(10));
            Require(observer.PendingCount==1,"nested duplicates share one queued candidate");observer.Tick(100);
            Require(ReferenceEquals(earliest,world.Rescued[0]) && earliest.Candidates.Count==1 && earliest.Candidates[0].Tile==4,"nested capture retains earliest reachable proof");
            Require(earliest.AffectedCurrent && earliest.AffectedNext,"nested capture merges only affected endpoints");
            var conflictWorld=new World();var conflict=New(conflictWorld);Enqueue(conflict,conflictWorld,20,Units(1,1));Enqueue(conflict,conflictWorld,21,Units(1,1));
            Require(conflict.PendingCount==0,"a later distinct mutation retires both proofs");conflict.Tick(100);Require(conflictWorld.RescueCalls==0,"conflicting proofs never teleport");
            var mismatchWorld=new World();var mismatch=New(mismatchWorld);var first=Context(30,1);mismatchWorld.Next=Units(1,1);var old=mismatch.TryBegin(first);
            var second=Context(30,2);mismatchWorld.Next=Units(1,1);mismatchWorld.Next[0].TileId++;var moved=mismatch.TryBegin(second);
            moved.Complete(Outcome(second));old.Complete(Outcome(first));mismatch.OnBatchCompleted(Batch(30));Require(mismatch.PendingCount==0,"nested movement cannot inherit earlier geometry proof");
            var supportedWorld=new World();var supported=New(supportedWorld);var known=Context(40,1,source:WorldMutationSource.AivBuildStep);supportedWorld.Next=Units(1,1);var knownReceipt=supported.TryBegin(known);
            supportedWorld.CaptureSupported=false;Require(supported.TryBegin(Context(40,2))==null,"unsupported child has no private capture");
            knownReceipt.Complete(Outcome(known));supported.OnBatchCompleted(Batch(40));Require(supported.PendingCount==0,"failed child invalidates partial samebatch capture rather than publishing incomplete evidence");
        }
        private static void FailureAndLifetime()
        {
            foreach(int mode in new[]{0,1,2,3,4})
            {
                var world=new World();var observer=New(world);var context=Context(50);world.Next=Units(1,1);var receipt=observer.TryBegin(context);
                receipt.Complete(Outcome(mode==0?Context(50,99):context,mode!=1,mode!=2));
                observer.OnBatchCompleted(Batch(50,returned:mode!=3,failure:mode==4));
                Require(observer.PendingCount==0,"mismatch, absent/failed original, wrapper failure and unknown child failure reject proof: "+mode);
            }
            var worldSession=new World();var session=New(worldSession);Enqueue(session,worldSession,60,Units(1,1));session.SetSession(10);
            Require(session.PendingCount==0 && session.TryBegin(Context(61))==null,"mission replacement discards queue and old begin context");session.Tick(100);Require(worldSession.RescueCalls==0,"old session cannot relocate");
            var worldGeneration=new World();var generation=New(worldGeneration);Enqueue(generation,worldGeneration,70,Units(1,1));worldGeneration.Generation++;
            generation.Tick(100);Require(generation.PendingCount==0 && worldGeneration.RescueCalls==0,"world token change retires pending proof on tick");
            Enqueue(generation,worldGeneration,71,Units(2,1));Require(generation.PendingCount==1,"new native world can capture fresh proof");
            var worldOrder=new World();var order=New(worldOrder);Enqueue(order,worldOrder,80,Units(1,1));order.ObserveOrder(1);
            Require(order.PendingCount==0,"external move invalidates queued old command");
            var pendingContext=Context(81);worldOrder.Next=Units(2,1);var pending=order.TryBegin(pendingContext);order.ObserveOrder(2);pending.Complete(Outcome(pendingContext));order.OnBatchCompleted(Batch(81));
            Require(order.PendingCount==0,"external move during mutation invalidates captured old command");
            var noTickWorld=new World{TickAvailable=false};var noTick=New(noTickWorld);Enqueue(noTick,noTickWorld,90,Units(1,1));Require(noTick.PendingCount==0,"no tick identity means no queue admission");
        }
        private static void BudgetsAndCapacity()
        {
            var world=new World();var observer=New(world);Enqueue(observer,world,100,Units(1,20));observer.Tick(100);
            Require(world.RescueCalls==8 && observer.PendingCount==12,"at most eight attempts per admitted tick");
            observer.Tick(100);Require(world.RescueCalls==8,"repeated callback for the same tick cannot spend another attempt budget");
            observer.Tick(101);Require(world.RescueCalls==16 && observer.PendingCount==4,"a later tick receives a fresh bounded budget");
            var delayedWorld=new World();var delayed=New(delayedWorld);Enqueue(delayed,delayedWorld,110,Units(1,100));delayed.Tick(180);
            Require(delayed.PendingCount==92 && delayedWorld.RescueCalls==8,"source age80 does not expire unevaluated burst members");
            delayed.Tick(180);Require(delayed.PendingCount==92 && delayedWorld.RescueCalls==8,"same tick cannot spend another visit/action budget");
            delayed.Tick(181);Require(delayed.PendingCount==84 && delayedWorld.RescueCalls==16,"later tick admits another bounded first evaluation");
            var capacityWorld=new World();var capacity=New(capacityWorld);Enqueue(capacity,capacityWorld,120,Units(1,300));Enqueue(capacity,capacityWorld,121,Units(301,300));
            Require(capacity.PendingCount==600 && capacity.TryCapturePending(out var sixHundred) && sixHundred.Length==600,"600 independent actors across batches are all retained");
            for(int tick=200;tick<300 && capacity.PendingCount!=0;tick++)
            {int before=capacityWorld.RescueCalls;capacityWorld.TickValue=tick;capacity.Tick(tick);Require(capacityWorld.RescueCalls-before<=8,"two-batch600 burst obeys action budget");}
            Require(capacity.PendingCount==0 && capacityWorld.NativeWrites==600 && capacity.RescueCount==600,"all600 actors from distinct source batches complete without source-age expiry");
            CompleteBurst();AwaitResourcesBurst();FullCapacityReplacement();
            var oversizeWorld=new World();var oversize=New(oversizeWorld);var invalid=Units(1,9999);invalid.Add(Unit(10000));
            var invalidContext=Context(130);oversizeWorld.Next=invalid;var invalidReceipt=oversize.TryBegin(invalidContext);
            invalidReceipt?.Complete(Outcome(invalidContext));oversize.OnBatchCompleted(Batch(130));
            Require(oversize.PendingCount==0 && oversizeWorld.NativeWrites==0 && oversizeWorld.ChangedCalls==0,"invalid native ID10000 invalidates the entire10000-record batch without publishing9999 partial actors");
            var batchWorld=new World();var batches=New(batchWorld);var contexts=new List<WorldMutationContext>();var receipts=new List<IWorldMutationInvocation>();
            for(int i=0;i<65;i++){var context=Context(200+i,i+1);batchWorld.Next=Units(i+1,1);var receipt=batches.TryBegin(context);if(i<64){Require(receipt!=null,"bounded batch accepted");contexts.Add(context);receipts.Add(receipt);}else Require(receipt==null,"65th active batch rejected");}
            for(int i=0;i<64;i++){receipts[i].Complete(Outcome(contexts[i]));batches.OnBatchCompleted(Batch(200+i));}
            Require(batches.PendingCount==64,"accepted batches retain independent proof at the batch limit");
        }
        private static void CompleteBurst()
        {
            var world=new World();var observer=New(world);var originals=Units(1,9999);Enqueue(observer,world,140,originals);
            Require(observer.PendingCount==9999 && world.RescueCalls==0,"all9999 actual native-domain actor IDs queue atomically without early actions");
            world.TickValue=500;
            Require(observer.TryCapturePending(out var untouched) && untouched.Length==9999 && untouched[9998].Age==400 &&
                untouched[9998].LifetimeMode==RescueQueueLifetimeMode.ActiveWindow && untouched[9998].ActiveAge==-1 && untouched[9998].Attempts==0,
                "oldest untouched tail retains original source age but has no invented active window");
            for(int tick=500;tick<2000 && observer.PendingCount!=0;tick++)
            {
                int before=world.RescueCalls;world.TickValue=tick;observer.Tick(tick);
                Require(world.RescueCalls-before>0 && world.RescueCalls-before<=8,"full burst advances with at most8 actions per native tick");
                observer.Tick(tick);Require(world.RescueCalls-before<=8,"duplicate callback cannot spend a second full-burst budget");
            }
            Require(observer.PendingCount==0 && observer.RescueCount==9999 && world.Rescued.Count==9999 && world.NativeWrites==9999,"every burst actor receives its first evaluation even after80 source ticks");
            var ids=new HashSet<int>();foreach(var actor in world.Rescued)
                Require(ids.Add(actor.Id) && ReferenceEquals(actor,originals[actor.Id-1]),"full burst neither duplicates nor replaces original causal actor identity");
        }
        private static void AwaitResourcesBurst()
        {
            var world=new World{ResolveStatus=_=>RescueStatus.AwaitResources};var observer=New(world);
            Enqueue(observer,world,145,Units(1,64));world.TickValue=500;observer.Tick(500);
            Require(world.RescueCalls==32 && world.NativeWrites==0 && observer.AttemptCount==0 && observer.PendingCount==64,"resource-only checks consume32 visits and zero native attempts or writes");
            observer.Tick(500);Require(world.RescueCalls==32,"resource waiting cannot reopen the same tick visit budget");
            world.TickValue=501;observer.Tick(501);
            Require(world.RescueCalls==64 && world.NativeWrites==0 && observer.AttemptCount==0,
                "next tick visits the other32 untouched members without starving the tail");
            Require(observer.TryCapturePending(out var waiting) && waiting.Length==64,"all64 waiting records remain exportable");
            foreach(var record in waiting)Require(record.Age==401 && record.ActiveAge==-1 && record.Attempts==0 && record.AwaitingResources,"waiting metadata retains source age without starting/refilling active deadline");
            world.ResolveStatus=_=>RescueStatus.Rescued;world.TickValue=502;observer.Tick(502);
            Require(world.RescueCalls==72 && world.NativeWrites==8 && observer.AttemptCount==8 && observer.PendingCount==56,"available resources admit only8 actions despite prior refunded waiting checks");
            var mixedWorld=new World{ResolveStatus=u=>u.Id<=24?RescueStatus.AwaitResources:RescueStatus.Rescued};var mixed=New(mixedWorld);
            Enqueue(mixed,mixedWorld,146,Units(1,64));mixed.Tick(100);
            Require(mixedWorld.RescueCalls==32 && mixedWorld.NativeWrites==8 && mixed.AttemptCount==8 && mixed.PendingCount==56,"24 waiting checks plus8 actual actions share the32-visit bound");
            var saved=RoundTrip(new[]{waiting[32]});var loadedWorld=new World{Session=31,Generation=4,TickValue=900,ResolveStatus=_=>RescueStatus.AwaitResources};var loaded=New(loadedWorld);
            loaded.StagePending(saved);loaded.Tick(900);
            Require(loadedWorld.RescueCalls==0 && loaded.PendingCount==1,"saved resource retry delay is honored before loaded evaluation");
            loadedWorld.TickValue=901;loaded.Tick(901);
            Require(loadedWorld.RescueCalls==1 && loadedWorld.NativeWrites==0 && loaded.AttemptCount==0 && loaded.TryCapturePending(out var reloaded) &&
                reloaded.Length==1 && reloaded[0].Age==402 && reloaded[0].ActiveAge==-1 && reloaded[0].AwaitingResources,"resource-wait save/load neither resets source age nor starts a false active window");
        }
        private static void FullCapacityReplacement()
        {
            foreach(bool reuse in new[]{true,false})
            {
                var world=new World();var observer=New(world);var originals=Units(1,9999);
                foreach(var old in originals){old.X=350;old.Y=400;}
                Enqueue(observer,world,reuse?150:152,originals);
                var replacement=Unit(1,reuse?50001u:originals[0].GlobalId);
                replacement.X=450;replacement.Y=400;replacement.TileId=replacement.NextTileId=30000;
                // Actual complete later writer envelope is outside EVERY old radius8
                // field. It must not accidentally free capacity during retirement.
                world.Writes=new[]{new RescueTile(450,400,30000)};
                Enqueue(observer,world,reuse?151:153,new List<UnitSnapshot>{replacement});
                Require(observer.TryCapturePending(out var pending),"full-capacity replacement retains a coherent export");
                if(reuse)
                {
                    Require(observer.PendingCount==9999 && pending.Length==9999 && pending[0].Snapshot.Id==1 &&
                        pending[0].Snapshot.GlobalId==50001 && ReferenceEquals(pending[0].Snapshot,replacement),
                        "full capacity replaces stale lifetime atomically without dropping new disjoint causal proof");
                    observer.Tick(100);
                    Require(world.Rescued.Count==8 && world.Rescued[0].Id==2 && world.Rescued[7].Id==9 && world.NativeWrites==8,
                        "reused lifetime goes to tail and cannot displace older valid actors' bounded FIFO turn");
                }
                else
                    Require(observer.PendingCount==9998 && pending.Length==9998 && pending[0].Snapshot.Id==2,
                        "same lifetime distinct mutation still retires both competing causes at capacity rather than upgrading geometry");
            }
        }
        private static void ReentrancyAndExceptions()
        {
            var world=new World();var states=new List<bool>();var observer=New(world,value=>states.Add(value));int mutationCallbacks=0;
            observer.WorldMutated=()=>mutationCallbacks++;Enqueue(observer,world,300,Units(1,2));int captureCalls=world.CaptureCalls;
            world.OnRescue=()=>{Require(observer.IsRecovering,"native callback executes within recovery guard");observer.ObserveOrder(2);observer.OnBatchCompleted(Batch(999));Require(observer.TryBegin(Context(999))==null,"own native callback cannot capture another rescue");observer.Tick(101);};
            observer.Tick(100);Require(world.RescueCalls==2 && observer.PendingCount==0,"own orders do not invalidate the second candidate or recurse");
            Require(mutationCallbacks==1 && world.CaptureCalls==captureCalls,"own completion does not publish a foreign world mutation");
            Require(states.Count==4 && states[0] && !states[1] && states[2] && !states[3] && !observer.IsRecovering,"suppression and busy guard are balanced");
            var captureOnlyWorld=new World();var captureOnly=new GeneralRescueObserver(new CaptureOnlyWorld(captureOnlyWorld),null,null);
            captureOnly.SetSession(captureOnlyWorld.Session);Enqueue(captureOnly,captureOnlyWorld,998,Units(1,2));int priorCaptures=captureOnlyWorld.CaptureCalls;
            captureOnlyWorld.OnRescue=()=>Require(captureOnly.TryBegin(Context(997))==null,"missing reentry planner never falls back to full capture");
            captureOnly.Tick(100);
            Require(captureOnlyWorld.RescueCalls==1 && captureOnlyWorld.CaptureCalls==priorCaptures && captureOnly.PendingCount==0 && !captureOnly.IsRecovering,
                "missing geometry-only planner conservatively retires every remaining old proof");
            var changingWorld=new World();var changingStates=new List<bool>();var changing=New(changingWorld,b=>changingStates.Add(b));Enqueue(changing,changingWorld,305,Units(1,2));
            changingWorld.OnRescue=()=>changingWorld.Generation++;changing.Tick(100);
            Require(changingWorld.RescueCalls==1 && changing.PendingCount==0 && !changing.IsRecovering && changingStates.Count==2 && changingStates[0] && !changingStates[1],"world replacement inside a native callback aborts remaining stale candidates");
            var callbackWorld=new World();var callback=New(callbackWorld,report:_=>{throw new Exception("logger");});callback.WorldMutated=()=>{throw new Exception("consumer");};
            Enqueue(callback,callbackWorld,310,Units(1,1));Require(callback.PendingCount==1,"mutable observer exception cannot interrupt batch cleanup/admission");callback.Tick(100);Require(callbackWorld.RescueCalls==1,"callback/logger failures do not lose valid rescue proof");
            var throwingWorld=new World{ThrowRescue=true};var throwingStates=new List<bool>();var throwing=New(throwingWorld,b=>throwingStates.Add(b));Enqueue(throwing,throwingWorld,320,Units(1,1));throwing.Tick(100);
            Require(!throwing.IsRecovering && throwing.PendingCount==0 && throwingStates.Count==2 && !throwingStates[1],"native exception retires candidate and clears suppression/busy");
            var suppressionWorld=new World();var suppressionStates=new List<bool>();bool fail=true;var suppression=New(suppressionWorld,b=>{suppressionStates.Add(b);if(b && fail){fail=false;throw new Exception("suppression");}});
            Enqueue(suppression,suppressionWorld,330,Units(1,1));try{suppression.Tick(100);}catch(Exception){}
            Require(!suppression.IsRecovering && suppressionStates.Count==2 && !suppressionStates[1] && suppression.PendingCount==0 && suppressionWorld.RescueCalls==0,"failed suppression enable disables, retires its exact action and resets busy");
            Enqueue(suppression,suppressionWorld,331,Units(2,1));suppression.Tick(101);Require(suppressionWorld.RescueCalls==1,"a later tick can recover after suppression exception");
        }
        private static UnitSnapshot Removal(int id)
        {
            var unit=Unit(id);unit.CivilianRemoval=true;unit.Type=(int)SHCDESE.Interop.eChimps.CHIMP_TYPE_PEASANT;
            unit.Command=null;unit.CurrentHadExit=true;return unit;
        }
        private static void MixedRemovalAndMovement()
        {
            bool suppressed=false;var states=new List<bool>();var world=new World();
            var observer=New(world,value=>{states.Add(value);suppressed=value;});
            world.OnOperation=before=>Require(suppressed==!before.CivilianRemoval,
                "actual operation boundary suppresses movement only, never native civilian cleanup");
            var context=Context(350);world.Next=new List<UnitSnapshot>{Removal(1),Unit(2),Removal(3),Unit(4)};
            var receipt=observer.TryBegin(context);Require(receipt!=null,"mixed source captures one actual batch receipt");
            receipt.Complete(Outcome(context));receipt.Complete(Outcome(context));
            observer.OnBatchCompleted(Batch(350));observer.OnBatchCompleted(Batch(350));
            observer.Tick(100);observer.Tick(100);observer.Tick(101);
            Require(world.RescueCalls==4 && world.RemovalRequests==2 && observer.RemovalCount==2 && observer.RescueCount==2 &&
                observer.AttemptCount==4 && observer.PendingCount==0,"duplicates cannot double mark; removal and rescue accounting stay distinct");
            Require(states.Count==4 && states[0] && !states[1] && states[2] && !states[3] && !suppressed && !observer.IsRecovering,
                "each military operation owns one balanced suppression pair in a mixed queue");
            world.Changed=false;Enqueue(observer,world,351,new List<UnitSnapshot>{Removal(5)});observer.Tick(102);
            Require(world.RemovalRequests==2 && observer.RemovalCount==2 && states.Count==4,
                "a no-op cannot mark another civilian or toggle movement suppression");

            var removalWorld=new World();var noCallback=New(removalWorld);
            Enqueue(noCallback,removalWorld,352,new List<UnitSnapshot>{Removal(1)});noCallback.Tick(100);
            Require(removalWorld.RemovalRequests==1 && noCallback.RemovalCount==1 && noCallback.RescueCount==0,
                "removal runs with a null suppression delegate");
            var throwingWorld=new World{ThrowRescue=true};var throwingStates=new List<bool>();
            var throwing=New(throwingWorld,b=>throwingStates.Add(b));
            Enqueue(throwing,throwingWorld,353,new List<UnitSnapshot>{Removal(1)});throwing.Tick(100);
            Require(throwingStates.Count==0 && !throwing.IsRecovering && throwing.PendingCount==0 && throwing.RemovalCount==0,
                "throwing removal retires its proof and clears busy without a suppression callback");

            var deferredWorld=new World();bool wait=true;var deferredStates=new List<bool>();
            deferredWorld.ResolveStatus=before=>wait?RescueStatus.RemovalDeferred:RescueStatus.RemovalRequested;
            var deferred=New(deferredWorld,b=>deferredStates.Add(b));
            Enqueue(deferred,deferredWorld,354,new List<UnitSnapshot>{Removal(1)});
            for(int tick=100;tick<108;tick++)deferred.Tick(tick);
            deferredWorld.TickValue=108;
            Require(deferred.PendingCount==1 && deferredWorld.RescueCalls==8 && deferredWorld.RemovalRequests==0 &&
                deferred.AttemptCount==0 && deferred.RemovalCount==0 && deferredStates.Count==0,
                "current-still-ordinary observation survives more than four ticks without spending native attempts");
            Require(deferred.TryCapturePending(out var pending) && pending.Length==1 && pending[0].Attempts==0 && pending[0].Age==8,
                "deferred observation preserves original age with no retry-budget consumption");
            wait=false;deferred.Tick(108);deferred.Tick(109);
            Require(deferredWorld.RemovalRequests==1 && deferred.RemovalCount==1 && deferred.RescueCount==0 &&
                deferred.AttemptCount==1 && deferred.PendingCount==0 && deferredStates.Count==0,
                "deferred source eventually marks once when the actual world adapter admits removal");
            var ttlWorld=new World{ResolveStatus=before=>RescueStatus.RemovalDeferred};var ttl=New(ttlWorld);
            Enqueue(ttl,ttlWorld,355,new List<UnitSnapshot>{Removal(1)});
            for(int tick=100;tick<=180;tick++)ttl.Tick(tick);
            Require(ttlWorld.RescueCalls==80 && ttlWorld.RemovalRequests==0 && ttl.AttemptCount==0 && ttl.PendingCount==0,
                "observation keeps bounded causal TTL despite never spending a native attempt");
        }
        private sealed class NoHumanIntent : IRescueReplanIntentWorld
        {
            internal int Calls;
            public bool TryCapture(int id,int x,int y,int extra,out RescueReplanIntent intent){Calls++;intent=null;return false;}
            public bool IsCurrent(RescueReplanIntent intent){Calls++;return false;}
            public bool TryGetSnapshot(RescueReplanIntent intent,out UnitSnapshot snapshot){Calls++;snapshot=null;return false;}
        }
        private static UnitMoveContext RemovalMove(int id,long invocation=1,long session=9,long world=3) =>
            (UnitMoveContext)Activator.CreateInstance(typeof(UnitMoveContext),BindingFlags.Instance|BindingFlags.NonPublic,null,
                new object[]{invocation,0L,session,world,id,70,80,0,null},null);
        private static UnitMoveCompletion RemovalMoveEnd(UnitMoveContext context,long result=0,bool invoked=true,bool returned=true,bool current=true)
        {
            var completed=(UnitMoveCompletion)Activator.CreateInstance(typeof(UnitMoveCompletion),BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{context},null);
            typeof(UnitMoveCompletion).GetMethod("Set",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(completed,new object[]{invoked,returned,result,null,current});return completed;
        }
        private static RescueOrderObserver RemovalOrders(GeneralRescueObserver observer) =>
            new RescueOrderObserver("ours",()=>false,id=>observer.ObserveOrder(id,true),
                (id,result)=>observer.EndExternalOrder(id),observer.IsInterestedInOrder,observer.EndExternalOrder);
        private static void RemovalSurvivesOrders()
        {
            var world=new World();var observer=New(world);var noIntent=new NoHumanIntent();observer.ReplanIntents=noIntent;
            Enqueue(observer,world,600,new List<UnitSnapshot>{Removal(1)});
            world.TickValue=102;observer.ObserveOrder(1);observer.ObserveOrder(1);
            Require(observer.PendingCount==1 && observer.TryCapturePending(out var proof) && proof[0].Age==2 && proof[0].Attempts==0,
                "one-shot new orders retain independent removal cause without refreshing age/budget");
            observer.Tick(102);Require(world.RemovalRequests==1 && noIntent.Calls==0,"queued removal survives changed serial without P7 or old-goal reconstruction");

            foreach(bool nestedCapture in new[]{false,true})
            {
                world=new World();observer=New(world);observer.ReplanIntents=new NoHumanIntent();
                var context=Context(nestedCapture?602:601);world.Next=new List<UnitSnapshot>{Removal(1)};
                var receipt=observer.TryBegin(context);observer.ObserveOrder(1);
                if(nestedCapture)
                {var child=Context(602,2);var childReceipt=observer.TryBegin(child);childReceipt.Complete(Outcome(child));}
                receipt.Complete(Outcome(context));observer.OnBatchCompleted(Batch(nestedCapture?602:601));observer.Tick(100);
                Require(world.RemovalRequests==1,"mid-batch serial changes retain same-lifetime removal geometry, nested="+nestedCapture);
            }
            world=new World();observer=New(world);Enqueue(observer,world,603,new List<UnitSnapshot>{Removal(1)});
            Require(observer.TryCapturePending(out proof),"removal can be saved before admitted tick");
            var restoredWorld=new World{Session=12,Generation=7,TickValue=500};var restored=New(restoredWorld);restored.ReplanIntents=new NoHumanIntent();
            restored.StagePending(RoundTrip(proof));restored.ObserveOrder(1);restored.Tick(500);
            Require(restoredWorld.RemovalRequests==1,"order before first restore tick cannot discard geometry-only removal proof");

            foreach(int outcome in new[]{0,1,2,3})
            {
                world=new World();observer=New(world);Enqueue(observer,world,610+outcome,new List<UnitSnapshot>{Removal(1)});
                var orders=RemovalOrders(observer);var context=RemovalMove(1);var call=orders.TryBegin(context);
                Require(call!=null && observer.HasUnsettledReplans,"actual order receipt opens a half-native-call barrier");
                observer.Tick(100);Require(world.RemovalRequests==0 && !observer.TryCapturePending(out _),"in-flight original blocks tick and atomic save");
                call.Complete(RemovalMoveEnd(context,outcome==1?1:0,outcome!=2,outcome!=3));call.Complete(RemovalMoveEnd(context));
                observer.Tick(100);
                Require(world.RemovalRequests==1 && !observer.HasUnsettledReplans,
                    "actual final Native0/1 or aborted receipt closes once and leaves physical classification authoritative "+outcome);
            }
            world=new World();observer=New(world);Enqueue(observer,world,620,new List<UnitSnapshot>{Removal(1)});
            var broker=RemovalOrders(observer);var outerContext=RemovalMove(1);var outer=broker.TryBegin(outerContext);
            var innerContext=RemovalMove(1,2);var inner=broker.TryBegin(innerContext);
            inner.Complete(RemovalMoveEnd(innerContext));inner.Complete(RemovalMoveEnd(innerContext));observer.Tick(100);
            Require(world.RemovalRequests==0 && observer.HasUnsettledReplans,"nested completion/duplicate cannot close outer native original");
            outer.Complete(RemovalMoveEnd(outerContext));observer.Tick(100);Require(world.RemovalRequests==1,"last nested native original closes removal barrier");

            world=new World();observer=New(world);Enqueue(observer,world,621,Units(1,1));broker=RemovalOrders(observer);
            outerContext=RemovalMove(1);outer=broker.TryBegin(outerContext);
            Require(observer.PendingCount==0 && observer.HasEvidence && observer.IsInterestedInOrder(1),"command retirement cannot disable an already open native receipt");
            innerContext=RemovalMove(1,2);inner=broker.TryBegin(innerContext);inner.Complete(RemovalMoveEnd(innerContext));outer.Complete(RemovalMoveEnd(outerContext));
            Require(!observer.HasEvidence && !observer.HasUnsettledReplans,"scope completes even after all command evidence disappeared");

            world=new World();observer=New(world);Enqueue(observer,world,622,new List<UnitSnapshot>{Removal(1)});
            int aborts=0;var failedBegin=new RescueOrderObserver("ours",()=>false,id=>{observer.ObserveOrder(id,true);throw new InvalidOperationException("begin failed");},
                (id,result)=>observer.EndExternalOrder(id),observer.IsInterestedInOrder,id=>{aborts++;observer.EndExternalOrder(id);});
            bool threw=false;try{failedBegin.TryBegin(RemovalMove(1));}catch(InvalidOperationException){threw=true;}
            Require(threw && aborts==1 && !observer.HasOpenExternalCalls && !observer.HasUnsettledReplans,"failed Begin closes partially opened scope without an issued receipt");
            observer.Tick(100);Require(world.RemovalRequests==1,"Begin failure cannot leave permanent global recovery block");

            world=new World();observer=New(world);Enqueue(observer,world,623,new List<UnitSnapshot>{Removal(1)});
            broker=RemovalOrders(observer);outerContext=RemovalMove(1);outer=broker.TryBegin(outerContext);
            observer.SetSession(12);world.Session=12;world.Generation=7;
            Enqueue(observer,world,624,new List<UnitSnapshot>{Removal(1)});
            innerContext=RemovalMove(1,2,12,7);inner=broker.TryBegin(innerContext);inner.Complete(RemovalMoveEnd(innerContext));observer.Tick(100);
            Require(world.RemovalRequests==0 && observer.HasOpenExternalCalls,"world reset/new same-ID nested call cannot erase an old open original");
            outer.Complete(RemovalMoveEnd(outerContext,current:false));observer.Tick(100);
            Require(world.RemovalRequests==1 && !observer.HasOpenExternalCalls,"old-world aborted completion closes only remaining original scope");

            foreach(var status in new[]{RescueStatus.NotTrapped,RescueStatus.Unsupported,RescueStatus.IdentityFailed})
            {
                world=new World{ResolveStatus=before=>status};observer=New(world);Enqueue(observer,world,630+(int)status,new List<UnitSnapshot>{Removal(1)});
                observer.ObserveOrder(1);observer.Tick(100);
                Require(world.RemovalRequests==0 && observer.PendingCount==0,"new legal exit/recruit/identity refusal remains authoritative "+status);
            }
        }
        private static RescuePendingCandidate<UnitSnapshot>[] RoundTrip(RescuePendingCandidate<UnitSnapshot>[] records)
        {
            var restored=new RescuePendingCandidate<UnitSnapshot>[records.Length];
            for(int i=0;i<records.Length;i++)
            {Require(RescuePendingCodec.TryEncode(records[i],out byte[] bytes),"actual pending codec encodes coordinator record");Require(RescuePendingCodec.TryDecode(bytes,out restored[i]),"actual pending codec decodes coordinator record");}
            return restored;
        }
        private static void PendingSaveAndRestore()
        {
            var world=new World();var observer=New(world);Enqueue(observer,world,400,Units(1,1));
            Require(observer.TryCapturePending(out var captured) && captured.Length==1,"save before first recovery tick retains queued causal proof");
            Require(captured[0].Age==0 && captured[0].Attempts==0 && captured[0].RetryIn==0,"new proof has exact initial lifetime and retry budget");
            var records=RoundTrip(captured);var loadedWorld=new World{Session=12,Generation=7,TickValue=500};var loaded=New(loadedWorld);
            loaded.StagePending(records);Require(loaded.PendingCount==1 && loadedWorld.RescueCalls==0,"load stages proof without native write");loaded.Tick(500);
            Require(loadedWorld.RescueCalls==1 && loaded.PendingCount==0,"first admitted tick rebinds process-local session/world and recovers saved proof");
            var retryWorld=new World{RescueSuccess=false};var retry=New(retryWorld);Enqueue(retry,retryWorld,410,Units(1,1));retry.Tick(100);
            Require(retry.TryCapturePending(out var retryRecords) && retryRecords[0].Attempts==1 && retryRecords[0].RetryIn==1,"save retains consumed attempt and pending retry delay");
            var delayedWorld=new World{TickValue=500,RescueSuccess=false};var delayed=New(delayedWorld);delayed.StagePending(RoundTrip(retryRecords));delayed.Tick(500);
            Require(delayedWorld.RescueCalls==0 && delayed.PendingCount==1,"restore cannot run pending retry early");delayedWorld.TickValue=501;delayed.Tick(501);
            Require(delayedWorld.RescueCalls==1 && delayed.TryCapturePending(out var afterRetry) && afterRetry[0].Age==1 && afterRetry[0].Attempts==2 && afterRetry[0].RetryIn==1,"restore preserves retry count and elapsed age rather than refreshing proof");
            var expiringWorld=new World{TickValue=900};var expiring=New(expiringWorld);
            expiring.StagePending(new[]{new RescuePendingCandidate<UnitSnapshot>(records[0].Source,records[0].Snapshot,79,3,1)});expiring.Tick(900);
            Require(expiring.PendingCount==1 && expiringWorld.RescueCalls==0,"age79 delayed retry remains pending only until next tick");expiring.Tick(901);
            Require(expiring.PendingCount==0 && expiringWorld.RescueCalls==0,"loaded age reaches lifetime limit without an extra attempt");
            var activeWorld=new World{TickValue=950};var active=New(activeWorld);
            var agedActive=new RescuePendingCandidate<UnitSnapshot>(records[0].Source,records[0].Snapshot,900,1,1,RescueQueueLifetimeMode.ActiveWindow,79,false);
            active.StagePending(RoundTrip(new[]{agedActive}));active.Tick(950);
            Require(active.PendingCount==1 && activeWorld.RescueCalls==0 && active.TryCapturePending(out var activeSaved) &&
                activeSaved[0].Age==900 && activeSaved[0].ActiveAge==79 && activeSaved[0].RetryIn==1,"active source age900 load preserves spent79-tick window and exact retry delay");
            activeWorld.TickValue=951;active.Tick(951);
            Require(active.PendingCount==0 && activeWorld.RescueCalls==0,"saved active deadline expires without receiving a new80-tick window");
            var disabledWorld=new World{Available=false,TickAvailable=false};var disabled=New(disabledWorld);disabled.SetSession(0);disabled.StagePending(records);
            Require(disabled.PendingCount==1,"disabled pending count includes staged proof");
            Require(disabled.TryCapturePending(out var preserved) && preserved.Length==1,"disabled paused feature preserves managed staged data");
            Require(preserved[0].Age==records[0].Age && preserved[0].Attempts==records[0].Attempts,"paused disabled capture retains exact saved metadata");disabled.Tick(100);
            Require(disabledWorld.WorldReads==0 && disabledWorld.TickReads==0 && disabledWorld.RescueCalls==0,"disabled stage capture and tick perform no world/tick/native access");
            Require(disabled.PendingCount==0,"first inactive simulation tick retires causal history without native writes");disabled.StagePending(records);
            disabled.ObserveOrder(1);Require(disabled.PendingCount==0,"external order also retires unbound disabled proof");
            disabled.StagePending(records);disabled.ClearPending();disabled.SetSession(99);Require(disabled.PendingCount==0,"explicit mission retirement prevents staged proof crossing into another mission");
            var gatedWorld=new World{Session=44,Generation=55,TickValue=500};var gated=New(gatedWorld);gated.SetSession(0);gated.StagePending(records);gated.SetSession(44);gated.Tick(500);
            Require(gatedWorld.RescueCalls==1,"setting active session does not discard newly loaded unbound records");
            var orderWorld=new World();var order=New(orderWorld);order.StagePending(records);order.ObserveOrder(1);order.Tick(100);
            Require(orderWorld.RescueCalls==0 && order.PendingCount==0,"order before first admitted restore tick permanently retires staged proof");
            var conflictWorld=new World();var conflict=New(conflictWorld);conflict.StagePending(records);Enqueue(conflict,conflictWorld,420,Units(1,1));
            Require(conflict.PendingCount==0 && conflict.TryCapturePending(out var conflicts) && conflicts.Length==0,"new mutation before restore retires both causes and cannot save duplicate unit entries");
            var reusedWorld=new World();var reused=New(reusedWorld);reused.StagePending(records);var replacement=Unit(1);replacement.GlobalId++;
            Enqueue(reused,reusedWorld,430,new List<UnitSnapshot>{replacement});reused.Tick(100);
            Require(reusedWorld.RescueCalls==1 && ReferenceEquals(replacement,reusedWorld.Rescued[0]),"slot reuse retires staged identity while allowing a newly proven replacement");
            var boundedWorld=new World();var bounded=New(boundedWorld);var oldRecords=new RescuePendingCandidate<UnitSnapshot>[300];
            for(int i=0;i<oldRecords.Length;i++)oldRecords[i]=new RescuePendingCandidate<UnitSnapshot>("saved",Unit(i+1),0,0,0);
            bounded.StagePending(oldRecords);Enqueue(bounded,boundedWorld,435,Units(301,300));
            Require(bounded.PendingCount==600 && bounded.TryCapturePending(out var boundedRecords) && boundedRecords.Length==600,"all600 combined staged/live actors retain independent proof");
            var savingWorld=new World();var saving=New(savingWorld);Enqueue(saving,savingWorld,436,Units(1,1));savingWorld.TickAvailable=false;
            Require(!saving.TryCapturePending(out _) && saving.PendingCount==1,"failed live tick read cannot produce an empty successful save");savingWorld.TickAvailable=true;savingWorld.Generation++;
            Require(!saving.TryCapturePending(out _) && saving.PendingCount==1,"stale live world cannot be serialized as current causal proof");
            var captureWorld=new World();var capture=New(captureWorld);Enqueue(capture,captureWorld,440,Units(1,1));
            captureWorld.OnRescue=()=>Require(!capture.TryCapturePending(out _),"save cannot capture a half-drained recovery transaction");capture.Tick(100);
            var invalidWorld=new World();var invalid=New(invalidWorld);invalid.StagePending(new[]{records[0],records[0]});Require(invalid.PendingCount==0,"duplicate staged unit IDs fail closed");
            var retriesWorld=new World{RescueSuccess=false};var retries=New(retriesWorld);Enqueue(retries,retriesWorld,450,Units(1,1));
            for(int tick=100;tick<104;tick++)retries.Tick(tick);
            Require(retriesWorld.RescueCalls==4 && retries.PendingCount==0,"coordinator maps NoCandidate to bounded retry rather than infinite replay");
            var rejectWorld=new World{RescueSuccess=false,FailureStatus=RescueStatus.Unsupported};var reject=New(rejectWorld);Enqueue(reject,rejectWorld,460,Units(1,1));reject.Tick(100);
            Require(rejectWorld.RescueCalls==1 && reject.PendingCount==0,"nonretryable native result retires proof immediately");
            var escapeWorld=new World{RescueSuccess=false,FailureStatus=RescueStatus.EscapeInProgress};var escape=New(escapeWorld);
            Enqueue(escape,escapeWorld,470,Units(1,1));escape.Tick(100);escape.Tick(101);
            Require(escapeWorld.RescueCalls==1 && escape.PendingCount==0 && escape.RescueCount==0,
                "published idle escape transfers observation to its owner without replay or premature rescue success");
        }
        private static void Require(bool condition,string message)
        {assertions++;if(!condition)throw new InvalidOperationException(message);}
    }
}
