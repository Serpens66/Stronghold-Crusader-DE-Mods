using System;
using System.Collections.Generic;
using System.Reflection;
using APIShared.WorldMutations;

namespace TrappedUnitRescueTest.Tests
{
    // Execute the production coordinator, queue and receipts. Only engine access is supplied
    // by a fake; the private API constructors are used solely to construct broker inputs.
    internal static class GeneralObserverFixture
    {
        private static int assertions;
        private sealed class World : IRescueMutationWorld
        {
            internal long Session=9,Generation=3;
            internal int TickValue=100,CaptureCalls,ChangedCalls,RescueCalls,WorldReads,TickReads;
            internal bool Available=true,CaptureSupported=true,TickAvailable=true,Changed=true;
            internal List<UnitSnapshot> Next=new List<UnitSnapshot>();
            internal readonly List<UnitSnapshot> Rescued=new List<UnitSnapshot>();
            internal Action OnRescue;
            internal bool RescueSuccess=true,ThrowRescue;
            internal RescueStatus FailureStatus=RescueStatus.NoCandidate;
            public bool TryCapture(WorldMutationContext context,out List<UnitSnapshot> before,out string reason)
            {CaptureCalls++;before=Next;reason=CaptureSupported?null:"unsupported";return CaptureSupported;}
            public bool HasChangedTrapCandidate(UnitSnapshot before){ChangedCalls++;return Changed;}
            public bool TryRescue(UnitSnapshot before,string cause,bool resume,out RescueResult result)
            {
                RescueCalls++;Rescued.Add(before);OnRescue?.Invoke();
                if(ThrowRescue)throw new InvalidOperationException("native fixture failure");
                result=new RescueResult{Status=RescueSuccess?RescueStatus.Rescued:FailureStatus};
                return RescueSuccess;
            }
            public bool TryGetWorld(out long session,out long generation){WorldReads++;session=Session;generation=Generation;return Available;}
            public bool TryGetTick(out int tick){TickReads++;tick=TickValue;return TickAvailable;}
        }
        private static GeneralRescueObserver New(World world,Action<bool> suppression=null,Action<string> report=null)
        {var observer=new GeneralRescueObserver(world,suppression,report);observer.SetSession(world.Session);return observer;}
        private static UnitSnapshot Unit(int id)=>new UnitSnapshot{Id=id,GlobalId=(uint)(1000+id),Owner=1,TileId=50+id,NextTileId=60+id,
            RequestedX=20,RequestedY=30,AffectedCurrent=true,CurrentWasOrdinary=true,Health=100,
            Type=(int)SHCDESE.Interop.eChimps.CHIMP_TYPE_SPEARMAN,CrewIdentity=Array.Empty<uint>()};
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
            knownReceipt.Complete(Outcome(known));supported.OnBatchCompleted(Batch(40));Require(supported.PendingCount==1,"unsupported child alone does not destroy valid full outer proof");
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
            var expiryWorld=new World();var expiry=New(expiryWorld);Enqueue(expiry,expiryWorld,110,Units(1,100));expiry.Tick(180);
            Require(expiry.PendingCount==68 && expiryWorld.RescueCalls==0,"expired candidates consume the 32-visit budget without native calls");
            expiry.Tick(180);Require(expiry.PendingCount==68,"same tick cannot spend another visit budget");
            expiry.Tick(181);Require(expiry.PendingCount==36,"later tick expires another bounded set");
            var capacityWorld=new World();var capacity=New(capacityWorld);Enqueue(capacity,capacityWorld,120,Units(1,300));Enqueue(capacity,capacityWorld,121,Units(301,300));
            Require(capacity.PendingCount==512,"queue admission remains bounded at 512 across batches");
            var oversizeWorld=new World();var oversize=New(oversizeWorld);Enqueue(oversize,oversizeWorld,130,Units(1,513));Require(oversize.PendingCount==0,"oversize batch fails closed instead of taking a partial proof");
            var batchWorld=new World();var batches=New(batchWorld);var contexts=new List<WorldMutationContext>();var receipts=new List<IWorldMutationInvocation>();
            for(int i=0;i<65;i++){var context=Context(200+i,i+1);batchWorld.Next=Units(i+1,1);var receipt=batches.TryBegin(context);if(i<64){Require(receipt!=null,"bounded batch accepted");contexts.Add(context);receipts.Add(receipt);}else Require(receipt==null,"65th active batch rejected");}
            for(int i=0;i<64;i++){receipts[i].Complete(Outcome(contexts[i]));batches.OnBatchCompleted(Batch(200+i));}
            Require(batches.PendingCount==64,"accepted batches retain independent proof at the batch limit");
        }
        private static void ReentrancyAndExceptions()
        {
            var world=new World();var states=new List<bool>();var observer=New(world,value=>states.Add(value));int mutationCallbacks=0;
            observer.WorldMutated=()=>mutationCallbacks++;Enqueue(observer,world,300,Units(1,2));int captureCalls=world.CaptureCalls;
            world.OnRescue=()=>{Require(observer.IsRecovering,"native callback executes within recovery guard");observer.ObserveOrder(2);observer.OnBatchCompleted(Batch(999));Require(observer.TryBegin(Context(999))==null,"own native callback cannot capture another rescue");observer.Tick(101);};
            observer.Tick(100);Require(world.RescueCalls==2 && observer.PendingCount==0,"own orders do not invalidate the second candidate or recurse");
            Require(mutationCallbacks==1 && world.CaptureCalls==captureCalls,"own completion does not publish a foreign world mutation");
            Require(states.Count==2 && states[0] && !states[1] && !observer.IsRecovering,"suppression and busy guard are balanced");
            var changingWorld=new World();var changing=New(changingWorld);Enqueue(changing,changingWorld,305,Units(1,2));
            changingWorld.OnRescue=()=>changingWorld.Generation++;changing.Tick(100);
            Require(changingWorld.RescueCalls==1 && changing.PendingCount==0 && !changing.IsRecovering,"world replacement inside a native callback aborts remaining stale candidates");
            var callbackWorld=new World();var callback=New(callbackWorld,report:_=>{throw new Exception("logger");});callback.WorldMutated=()=>{throw new Exception("consumer");};
            Enqueue(callback,callbackWorld,310,Units(1,1));Require(callback.PendingCount==1,"mutable observer exception cannot interrupt batch cleanup/admission");callback.Tick(100);Require(callbackWorld.RescueCalls==1,"callback/logger failures do not lose valid rescue proof");
            var throwingWorld=new World{ThrowRescue=true};var throwingStates=new List<bool>();var throwing=New(throwingWorld,b=>throwingStates.Add(b));Enqueue(throwing,throwingWorld,320,Units(1,1));throwing.Tick(100);
            Require(!throwing.IsRecovering && throwing.PendingCount==0 && throwingStates.Count==2 && !throwingStates[1],"native exception retires candidate and clears suppression/busy");
            var suppressionWorld=new World();var suppressionStates=new List<bool>();bool fail=true;var suppression=New(suppressionWorld,b=>{suppressionStates.Add(b);if(b && fail){fail=false;throw new Exception("suppression");}});
            Enqueue(suppression,suppressionWorld,330,Units(1,1));try{suppression.Tick(100);}catch(Exception){}
            Require(!suppression.IsRecovering && suppressionStates.Count==2 && !suppressionStates[1] && suppression.PendingCount==1,"failed suppression enable always attempts disable and resets busy");
            suppression.Tick(101);Require(suppressionWorld.RescueCalls==1,"a later tick can recover after suppression exception");
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
            Require(bounded.PendingCount==512 && bounded.TryCapturePending(out var boundedRecords) && boundedRecords.Length==512,"combined staged/live admission obeys the same global capacity");
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
        }
        private static void Require(bool condition,string message)
        {assertions++;if(!condition)throw new InvalidOperationException(message);}
    }
}