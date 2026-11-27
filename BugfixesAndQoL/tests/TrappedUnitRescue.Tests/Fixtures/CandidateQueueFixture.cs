using System;
using System.Collections.Generic;

namespace TrappedUnitRescueTest.Tests
{
    internal static class CandidateQueueFixture
    {
        private static int checks;
        private static RescueIdentity Identity(int id, uint global = 1, long order = 1, long session = 7, long world = 9) =>
            new RescueIdentity(session, world, id, global, order);
        private static void RestoredStationaryPriority()
        {
            var q=new RescueCandidateQueue<string>(lifetimeMode:RescueQueueLifetimeMode.ActiveWindow);q.Reset(7,9);
            q.Offer(Identity(1),1,"source","old unevaluated",100);
            var fresh=new RescuePendingCandidate<string>("replan/source","fresh idle",200,2,0,RescueQueueLifetimeMode.ActiveWindow,7,false);
            Require(q.OfferRestored(Identity(2),2,fresh,100,true),"fresh stationary priority admitted");
            Require(q.TryCopyPending(100,out var records) && records.Length==2 && records[0].Snapshot=="fresh idle" &&
                records[0].Age==200 && records[0].ActiveAge==7 && records[0].Attempts==2 && records[1].ActiveAge==-1,
                "priority preserves both original source/window and untouched metadata admission");
            int first=0;q.Drain(100,1,1,c=>{first=c.Identity.UnitId;return RescueQueueDecision.Complete;});
            Require(first==2 && q.Count==1,"fresh stationary work runs before old unevaluated metadata");
            q.Reset(7,9);q.Offer(Identity(1),1,"source","old metadata",100);
            Require(q.OfferRestored(Identity(2),2,fresh,100,true) && q.OfferRestored(Identity(3),3,fresh,100,true),"two preferred rows admitted");
            var priorityOrder=new List<int>();q.Drain(100,1,1,c=>{priorityOrder.Add(c.Identity.UnitId);return RescueQueueDecision.Complete;});
            Require(q.OfferRestored(Identity(4),4,fresh,100,true),"new priority wave admitted");
            q.Drain(100,8,8,c=>{priorityOrder.Add(c.Identity.UnitId);return RescueQueueDecision.Complete;});
            Require(priorityOrder.Count==4 && priorityOrder[0]==2 && priorityOrder[1]==3 && priorityOrder[2]==4 && priorityOrder[3]==1,
                "preferred FIFO protects older ready windows from newer geometry waves");
            q.Reset(7,9);q.Offer(Identity(1),1,"source","metadata one",100);q.Offer(Identity(2),2,"source","metadata two",100);
            Require(q.OfferRestored(Identity(3),3,fresh,100,true),"resource-wait priority admitted");
            var admitted=new List<int>();q.Drain(100,8,8,c=>{admitted.Add(c.Identity.UnitId);return c.Identity.UnitId==3?RescueQueueDecision.AwaitResources:RescueQueueDecision.Complete;});
            Require(admitted.Count==3 && admitted[0]==3 && admitted[1]==1 && admitted[2]==2 && q.Count==1,
                "preferred resource wait cannot starve older ordinary admissions in the same pass");
            q.Reset(7,9);q.Offer(Identity(1),1,"source","one",100);q.Offer(Identity(2),2,"source","two",100);
            var seen=new List<int>();
            q.Drain(100,8,8,c=>
            {
                seen.Add(c.Identity.UnitId);
                if(c.Identity.UnitId==1)Require(q.OfferRestored(Identity(3),3,fresh,100,true),"priority can be offered during callback");
                return RescueQueueDecision.Complete;
            });
            Require(seen.Count==1 && seen[0]==1 && q.Count==2,"priority never admits a new sequence into the running drain");
            q.Drain(101,8,8,c=>{seen.Add(c.Identity.UnitId);return RescueQueueDecision.Complete;});
            Require(seen.Count==3 && seen[1]==3 && seen[2]==2 && q.Count==0,"deferred priority and older rows remain fair on the next pass");
        }
        internal static void Run()
        {
            checks = 0;RestoredStationaryPriority();
            var queue = new RescueCandidateQueue<string>(2, 2, 8, 2);
            Require(queue.Offer(Identity(1), 1, "bridge", "old", 10) == RescueOfferResult.Inactive, "inactive capture");
            queue.Reset(7, 9);
            Require(queue.Offer(Identity(0), 1, "bridge", "old", 10) == RescueOfferResult.InvalidIdentity, "reserved slot");
            Require(queue.Offer(Identity(10000), 1, "bridge", "old", 10) == RescueOfferResult.InvalidIdentity, "native upper bound");
            Require(queue.Offer(Identity(1, 0), 1, "bridge", "old", 10) == RescueOfferResult.InvalidIdentity, "missing global identity");
            Require(queue.Offer(Identity(1), 1, "bridge", "old", 10) == RescueOfferResult.Added, "initial capture");
            Require(queue.Offer(Identity(1), 1, "bridge", "replacement", 11) == RescueOfferResult.Duplicate, "duplicate invocation");
            string proof = null;
            var completed = queue.Drain(11, 10, 10, item => { proof = item.Snapshot; return RescueQueueDecision.Complete; });
            Require(proof == "old" && completed.Completed == 1 && queue.Count == 0 && queue.PendingCount == 0, "dedup preserves original historical proof");

            queue.Offer(Identity(1), 1, "bridge", "first geometry", 10);
            Require(queue.Offer(Identity(1), 2, "wall", "second geometry", 10) == RescueOfferResult.ConflictingMutation,
                "a second mutation is not a stronger first proof");
            Require(queue.Count == 0 && queue.PendingCount == 0 && queue.ConflictCount == 1, "conflicting proof retired immediately");
            queue.Offer(Identity(1), 3, "bridge", "slot A", 12);
            queue.Offer(Identity(1, 2), 4, "wall", "slot B", 12);
            queue.Drain(12, 3, 3, item => { proof = item.Snapshot; return RescueQueueDecision.Complete; });
            Require(proof == "slot B", "reused slot cannot execute old global identity");

            queue.Offer(Identity(1, 2, 3), 5, "bridge", "new order", 13);
            Require(queue.Offer(Identity(1, 2, 2), 6, "wall", "old order", 13) == RescueOfferResult.InvalidIdentity, "late old order does not evict newer order");
            Require(!queue.InvalidateOrder(1, 2, 3), "same order remains current");
            Require(queue.InvalidateOrder(1, 2, 4) && queue.Count == 0 && queue.PendingCount == 0, "new command retires pending proof");

            queue.Offer(Identity(8), 7, "bridge", "eight", 20);
            queue.Offer(Identity(3), 8, "wall", "three", 20);
            Require(queue.Offer(Identity(2), 9, "wall", "overflow", 20) == RescueOfferResult.CapacityExceeded && queue.OverflowCount == 1, "visible overload");
            var visited = new List<int>();
            var one = queue.Drain(20, 100, 1, item => { visited.Add(item.Identity.UnitId); return RescueQueueDecision.Complete; });
            Require(one.Attempted == 1 && queue.Count == 1, "attempt budget");
            queue.Drain(20, 1, 100, item => { visited.Add(item.Identity.UnitId); return RescueQueueDecision.Complete; });
            Require(visited.Count == 2 && visited[0] == 8 && visited[1] == 3, "stable FIFO across budgets");
            int emptyCalls = 0;
            var empty = queue.Drain(20, 100, 100, item => { emptyCalls++; return RescueQueueDecision.Complete; });
            Require(empty.Visited == 0 && emptyCalls == 0, "empty queue performs no lookup/evaluation");

            queue.Offer(Identity(1), 10, "bridge", "retry", 30);
            var retry = queue.Drain(30, 100, 100, item => RescueQueueDecision.Retry);
            Require(retry.Attempted == 1 && retry.Retried == 1 && queue.Count == 1, "retry executes at most once per drain");
            var notDue = queue.Drain(31, 100, 100, item => throw new Exception("must not run"));
            Require(notDue.Attempted == 0 && queue.Count == 1, "retry cadence uses native ticks");
            var exhausted = queue.Drain(32, 100, 100, item => RescueQueueDecision.Retry);
            Require(exhausted.Attempted == 1 && exhausted.Expired == 1 && !queue.HasWork, "maximum attempts terminates retries");

            queue.Offer(Identity(1), 11, "bridge", "expiry", 40);
            var expired = queue.Drain(48, 10, 10, item => throw new Exception("expired must not run"));
            Require(expired.Expired == 1 && expired.Attempted == 0, "proof expires before evaluator");
            queue.Offer(Identity(1), 12, "bridge", "clock backwards", 40);
            Require(queue.Drain(39, 10, 10, item => RescueQueueDecision.Complete).Expired == 1, "backwards/ambiguous native time rejects proof");
            queue.Offer(Identity(1), 13, "bridge", "wrap", int.MaxValue);
            Require(queue.Drain(int.MinValue, 10, 10, item => RescueQueueDecision.Retry).Retried == 1, "signed tick wrap is forward time");
            Require(queue.Drain(unchecked(int.MinValue + 1), 10, 10, item => RescueQueueDecision.Complete).Attempted == 0, "wrapped retry not early");
            Require(queue.Drain(unchecked(int.MinValue + 2), 10, 10, item => RescueQueueDecision.Complete).Completed == 1, "wrapped retry due");

            queue.Offer(Identity(1), 14, "bridge", "old world", 50);
            queue.Reset(7, 10);
            Require(!queue.HasWork && queue.Offer(Identity(2), 15, "wall", "stale world", 50) == RescueOfferResult.Inactive,
                "same-session native reload invalidates old world");
            queue.Reset(7, 9);
            queue.Offer(Identity(1), 16, "bridge", "callback reset", 50);
            queue.Offer(Identity(2), 16, "bridge", "same batch", 50);
            int callbacks = 0;
            queue.Drain(50, 10, 10, item => { callbacks++; queue.Reset(8, 11); return RescueQueueDecision.Retry; });
            Require(callbacks == 1 && queue.Count == 0 && queue.PendingCount == 0, "world retirement during callback cannot resurrect queue");

            queue.Reset(7, 9);
            queue.Offer(Identity(1), 17, "bridge", "nested drain", 60);
            queue.Drain(60, 10, 10, item => {
                Require(queue.Drain(60, 10, 10, nested => throw new Exception("reentrant")).Attempted == 0, "no reentrant evaluator");
                queue.InvalidateUnit(1); return RescueQueueDecision.Retry;
            });
            Require(queue.Count == 0 && queue.PendingCount == 0, "self-invalidated callback cannot retry");
            queue.Offer(Identity(1), 18, "bridge", "failure", 61);
            queue.Offer(Identity(2), 18, "bridge", "healthy", 61);
            var failure = queue.Drain(61, 10, 10, item => item.Identity.UnitId == 1 ? throw new InvalidOperationException("isolated") : RescueQueueDecision.Complete);
            Require(failure.Failed == 1 && failure.Rejected == 1 && failure.Completed == 1, "one evaluator failure does not skip healthy candidate");

            queue.Offer(Identity(1), 19, "bridge", "callback offers", 62);
            int offers = 0;
            queue.Drain(62, 10, 10, item => { offers++; queue.Offer(Identity(2), 19, "bridge", "next pass", 62); return RescueQueueDecision.Complete; });
            Require(offers == 1 && queue.Count == 1, "callback-created work waits until next drain");
            queue.Reset(7, 9);
            for (int i = 0; i < 10000; i++)
            {
                queue.Offer(Identity(1, (uint)(i + 1)), i + 1, "replace", "bounded", 63);
                Require(queue.Count == 1 && queue.PendingCount == 1, "slot replacement retains no FIFO tombstones");
            }
            Require(queue.InvalidateUnit(1) && queue.Count == 0 && queue.PendingCount == 0, "explicit death/invalidation releases proof");
            ObserveWithoutNativeAttempt();
            ActiveWindowLifetime();
            Console.WriteLine("PASS: candidate queue checks=" + checks + "; production pure queue, no native calls.");
        }
        private static void ObserveWithoutNativeAttempt()
        {
            var queue=new RescueCandidateQueue<string>();queue.Reset(7,9);
            queue.Offer(Identity(1),1,"next-only","original-step",0);
            int calls=0;
            // SS0..SS3 still have a safe Current. No native recovery was attempted.
            for(int tick=0;tick<8;tick++)
            {
                var observed=queue.Drain(tick,32,8,item=>{calls++;return RescueQueueDecision.Observe;});
                Require(observed.Visited==1 && observed.Attempted==0 && observed.Retried==0 && queue.Count==1,"observation does not consume native attempt "+tick);
                Require(queue.TryCopyPending(tick,out var saved) && saved.Length==1 && saved[0].Attempts==0 && saved[0].Age==tick && saved[0].RetryIn==1,"save preserves original age and zero attempts "+tick);
                var repeated=queue.Drain(tick,32,8,item=>throw new Exception("same-tick observation repeated"));
                Require(repeated.Attempted==0 && repeated.Failed==0,"same-tick observation is not evaluated twice "+tick);
            }
            Require(calls==8,"more than four actual observations remain available");
            var complete=queue.Drain(8,32,8,item=>{Require(item.Attempts==1,"first real native attempt retains existing numbering");return RescueQueueDecision.Complete;});
            Require(complete.Attempted==1 && complete.Completed==1 && queue.Count==0,"eventual first native completion after midpoint");

            queue.Offer(Identity(1),2,"next-only","expiry",0);int expiryCalls=0;
            for(int tick=0;tick<80;tick++)queue.Drain(tick,1,1,item=>{expiryCalls++;return RescueQueueDecision.Observe;});
            var expired=queue.Drain(80,1,1,item=>throw new Exception("expired observation"));
            Require(expiryCalls==80 && expired.Expired==1 && expired.Attempted==0 && !queue.HasWork,"TTL still bounds observations without native attempts");

            queue.Reset(7,9);queue.Offer(Identity(1),3,"next-only","one",0);queue.Offer(Identity(2),3,"next-only","two",0);
            queue.Offer(Identity(3),3,"bridge","actual",0);
            var order=new List<int>();
            var fair=queue.Drain(0,2,1,item=>{order.Add(item.Identity.UnitId);return RescueQueueDecision.Observe;});
            Require(fair.Visited==2 && fair.Attempted==0 && order.Count==2,"visit budget bounds observation callbacks");
            var next=queue.Drain(0,2,1,item=>{order.Add(item.Identity.UnitId);return RescueQueueDecision.Complete;});
            Require(next.Completed==1 && next.Attempted==1 && order[2]==3,"observations rotate and preserve other-candidate fairness");

            queue.Reset(7,9);queue.Offer(Identity(1),4,"next-only","saved",10);
            queue.Drain(10,1,1,item=>RescueQueueDecision.Observe);
            Require(queue.TryCopyPending(10,out var records),"observed candidate can be saved outside drain");
            var restored=new RescueCandidateQueue<string>();restored.Reset(8,11);
            Require(restored.OfferRestored(Identity(1,1,0,8,11),1,records[0],100),"observed proof restores with new world");
            Require(restored.Drain(100,1,1,item=>throw new Exception("restore cadence early")).Attempted==0,"restored observation keeps due delay");
            var retry=restored.Drain(101,1,1,item=>{Require(item.Attempts==1,"saved observations did not become attempts");return RescueQueueDecision.Retry;});
            Require(retry.Attempted==1 && retry.Retried==1,"ordinary retry semantics remain unchanged after observation");
            Require(restored.TryCopyPending(101,out records) && records[0].Attempts==1,"only actual retry counted in subsequent save");

            queue.Reset(7,9);queue.Offer(Identity(1),5,"next-only","retired",0);
            var ended=queue.Drain(0,1,1,item=>{queue.Reset(8,11);return RescueQueueDecision.Observe;});
            Require(ended.Attempted==0 && queue.Count==0,"observation cannot reinsert ended-world candidate");
            queue.Reset(7,9);queue.Offer(Identity(1),6,"next-only","old-life",0);
            queue.Drain(0,1,1,item=>{queue.Offer(Identity(1,2),7,"new","new-life",0);return RescueQueueDecision.Observe;});
            Require(queue.TryTakeUnit(1,out var replacement) && replacement.Identity.GlobalId==2 && replacement.Attempts==0,"observation rollback cannot alter replacement lifetime");
        }
        private static RescueCandidateQueue<string> ActiveQueue(int capacity=512)
        {
            var q=new RescueCandidateQueue<string>(capacity:capacity,lifetimeMode:RescueQueueLifetimeMode.ActiveWindow);
            q.Reset(7,9);return q;
        }
        private static void ActiveWindowLifetime()
        {
            var q=ActiveQueue();q.Offer(Identity(1),1,"bridge","waiting-original",0);
            Require(q.TryCopyPending(600,out var records) && records.Length==1 && records[0].Age==600 && records[0].ActiveAge==-1 &&
                records[0].LifetimeMode==RescueQueueLifetimeMode.ActiveWindow && !records[0].AwaitingResources,
                "unvisited backlog retains original source age without beginning active lifetime");
            q.Drain(600,32,8,item=>{Require(item.SourceCapturedTick==0 && item.ActiveAge==0 && item.Attempts==1,"first real evaluation starts active age without refreshing source");return RescueQueueDecision.Retry;});
            Require(q.TryCopyPending(679,out records) && records.Length==1 && records[0].Age==679 && records[0].ActiveAge==79,"save exports actual source and remaining active window separately");
            Require(q.Drain(680,32,8,item=>throw new Exception("active80")).Expired==1,"active window expires at exactly80 ticks despite young attempt count");

            q=ActiveQueue();q.Offer(Identity(1),2,"bridge","resource-await",0);
            for(int tick=0;tick<160;tick++)
            {
                var wait=q.Drain(tick,32,8,item=>RescueQueueDecision.AwaitResources);
                Require(wait.Attempted==0 && wait.Visited==1 && q.Count==1,"resource checks use visit budget, not native-attempt budget");
            }
            Require(q.TryCopyPending(159,out records) && records[0].Age==159 && records[0].ActiveAge==-1 && records[0].Attempts==0 && records[0].AwaitingResources,
                "first resource wait never starts active clock and preserves source age");
            var saved=records[0];var restored=ActiveQueue();
            Require(restored.OfferRestored(Identity(1),20,saved,1000),"active resource wait restores after long source age");
            Require(restored.Drain(1000,32,8,item=>throw new Exception("restore due")).Attempted==0,"resource wait restore retains exact retry cadence");
            restored.Drain(1001,32,8,item=>{Require(item.ActiveAge==0 && item.Attempts==1,"resource availability begins first real active window");return RescueQueueDecision.Retry;});
            Require(restored.TryCopyPending(1001,out records) && records[0].Age==160 && records[0].ActiveAge==0 && records[0].Attempts==1 && !records[0].AwaitingResources,
                "restore rebases only timestamps, never historical source or attempts");
            for(int tick=1002;tick<=1004;tick++)restored.Drain(tick,32,8,item=>RescueQueueDecision.Retry);
            Require(!restored.HasWork,"four actual native attempts still terminate active work");

            q=ActiveQueue();q.Offer(Identity(1),3,"bridge","started-await",0);
            q.Drain(0,32,8,item=>RescueQueueDecision.Retry);
            q.Drain(10,32,8,item=>RescueQueueDecision.AwaitResources);
            Require(q.TryCopyPending(1000,out records) && records[0].Age==1000 && records[0].ActiveAge==10 && records[0].Attempts==1,
                "already active work pauses at resource-wait boundary");
            restored=ActiveQueue();Require(restored.OfferRestored(Identity(1),21,records[0],2000),"partially consumed active window restores");
            restored.Drain(2000,32,8,item=>{Require(item.ActiveAge==10 && item.Attempts==2,"unpaused work keeps consumed active ticks and attempts");return RescueQueueDecision.Observe;});
            Require(restored.TryCopyPending(2069,out records) && records[0].ActiveAge==79,"observe does not freeze the started active window");
            Require(restored.Drain(2070,32,8,item=>throw new Exception("restored expiry")).Expired==1,"save/load does not replenish active lifetime");

            q=ActiveQueue();q.Offer(Identity(1),4,"bridge","observe",0);
            q.Drain(200,1,1,item=>RescueQueueDecision.Observe);
            Require(q.TryCopyPending(200,out records) && records[0].ActiveAge==0 && records[0].Attempts==0 && !records[0].AwaitingResources,
                "first ordinary observation begins window but consumes no native attempt");
            Require(q.Drain(280,1,1,item=>throw new Exception("observe expiry")).Expired==1,"ordinary observations are not unlimited resource waits");

            var legacy=new RescuePendingCandidate<string>("legacy","proof",79,1,0);
            q=ActiveQueue();Require(q.OfferRestored(Identity(1),5,legacy,1000),"active queue accepts young legacy record conservatively");
            Require(q.TryCopyPending(1000,out records) && records[0].ActiveAge==79 && records[0].Age==79,"legacy age becomes already-consumed active age");
            Require(q.Drain(1001,1,1,item=>throw new Exception("legacy rejuvenation")).Expired==1,"legacy record gets no new80-tick lifetime");
            Require(!q.OfferRestored(Identity(1),6,new RescuePendingCandidate<string>("legacy","proof",80,0,0),1000),"expired legacy record cannot migrate to fresh active wait");
            var absolute=new RescueCandidateQueue<string>();absolute.Reset(7,9);
            var active=new RescuePendingCandidate<string>("new","proof",1000,0,0,RescueQueueLifetimeMode.ActiveWindow,-1,true);
            Require(!absolute.OfferRestored(Identity(1),7,active,1000),"absolute queue rejects active-mode persistence instead of reinterpreting it");
            Require(!q.OfferRestored(Identity(1),7,new RescuePendingCandidate<string>("bad","proof",10,1,0,RescueQueueLifetimeMode.ActiveWindow,-1,true),1000),"unstarted window cannot hide previous native attempts");
            Require(!q.OfferRestored(Identity(1),7,new RescuePendingCandidate<string>("bad","proof",10,0,0,RescueQueueLifetimeMode.ActiveWindow,11,false),1000),"active elapsed cannot exceed original source age");

            q=ActiveQueue();q.Offer(Identity(1),8,"bridge","clock",int.MaxValue);
            q.Drain(int.MinValue,1,1,item=>RescueQueueDecision.AwaitResources);
            Require(q.TryCopyPending(unchecked(int.MinValue+100),out records) && records[0].Age==101,"forward signed tick wrap remains serializable while waiting");
            q.Drain(unchecked(int.MinValue+101),1,1,item=>RescueQueueDecision.AwaitResources);
            Require(q.Drain(unchecked(int.MinValue+100),1,1,item=>throw new Exception("backwards resource time")).Expired==1,"even resource wait rejects backwards evaluation time");
            q=ActiveQueue();q.Offer(Identity(1),9,"bridge","ambiguous",0);
            Require(q.Drain(int.MinValue,1,1,item=>throw new Exception("ambiguous source age")).Expired==1,"half-range source age fails closed before evaluation");

            q=ActiveQueue(9999);
            for(int id=1;id<=9999;id++)Require(q.Offer(Identity(id),10,"bridge","bounded",0)==RescueOfferResult.Added,"explicit capacity accepts each real native ID once");
            Require(q.CopyOwnedSnapshots().Length==9999,"invalidation copy remains bounded by configured real slot capacity");
            var bounded=q.Drain(1000,32,8,item=>{Require(!q.TryCopyPending(1000,out _),"draining save barrier remains intact in active mode");return RescueQueueDecision.AwaitResources;});
            Require(bounded.Visited==32 && bounded.Attempted==0 && q.Count==9999,"resource backlog never exceeds32 visits even without native attempts");
            var eight=q.Drain(1001,32,8,item=>RescueQueueDecision.Retry);
            Require(eight.Attempted==8 && eight.Visited==8,"active backlog retains8 native-attempt bound");
            q.Reset(8,11);Require(!q.HasWork,"world reset clears waiting active proofs too");
            q=ActiveQueue();q.Offer(Identity(1),11,"bridge","old-life",0);
            q.Drain(500,32,8,item=>{q.Offer(Identity(1,2),12,"new","new-life",500);return RescueQueueDecision.AwaitResources;});
            Require(q.TryTakeUnit(1,out var replacement) && replacement.Identity.GlobalId==2 && replacement.ActiveAge==-1 &&
                replacement.Attempts==0 && !replacement.AwaitingResources,"resource rollback cannot mutate a replacement lifetime");
            q=ActiveQueue();q.Offer(Identity(1),13,"bridge","ended",0);
            var retired=q.Drain(500,32,8,item=>{q.Reset(8,11);return RescueQueueDecision.AwaitResources;});
            Require(retired.Attempted==0 && !q.HasWork,"resource wait cannot resurrect an ended world");
        }
        private static void Require(bool condition, string message) { checks++; if (!condition) throw new InvalidOperationException(message); }
    }
}
