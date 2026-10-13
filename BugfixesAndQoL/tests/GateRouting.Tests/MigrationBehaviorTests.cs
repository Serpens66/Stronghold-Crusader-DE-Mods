using System;
using APIShared;
namespace BugfixesAndQoL.GateRouting
{
    internal static class MigrationBehaviorTests
    {
        internal static int Run()
        {
            int count=0; Action<bool,string> check=(ok,why)=>{count++;if(!ok)throw new InvalidOperationException(why);};
            var records=new NativeGateAccessRecord[2];
            // Extension deny for an own gate; identity checks still precede that decision.
            records[1]=new NativeGateAccessRecord(true,1,0,2,0,2,2);
            var denied=new NativeGateAccessSnapshot(records,1,new uint[]{0,42});
            check(denied.Evaluate(1,1,1,0)==NativeGateSnapshotDecision.ExcludeAccessRule,"own-gate added restriction reaches comparison policy");
            check(denied.Evaluate(1,1,2,0)==NativeGateSnapshotDecision.OwnerMismatch,"extension never bypasses identity");
            records=new NativeGateAccessRecord[2];records[1]=new NativeGateAccessRecord(true,1,3,2,8,0,4);
            var granted=new NativeGateAccessSnapshot(records,2,new uint[]{0,43});
            check(granted.Evaluate(2,1,1,3)==NativeGateSnapshotDecision.PreserveAccessRule,"foreign capture exception preserves native comparison, not unconditional native access");
            var routes=new GateRoutePolicySource();var masks=new byte[9][];masks[2]=new byte[]{0};
            routes.Publish(new RouteTilePolicySnapshot(masks,1));
            check(routes.TryCaptureRoutePolicy(2,out var old)&&old.IsCurrent,"publication initially current");
            check(GateAccessRules.TryRegister("migration.fixture","revision",q=>GateAccessDecision.Unchanged,out _),"registration succeeds");
            check(!old.IsCurrent&&!routes.TryCaptureRoutePolicy(2,out _),"registration invalidates route and cache identity");
            check(granted.Evaluate(2,1,1,3)==NativeGateSnapshotDecision.UntrackedConnection,"stale capture publication never creates a new grant");
            routes.Publish(new RouteTilePolicySnapshot(masks,2));
            check(routes.TryCaptureRoutePolicy(2,out var fresh)&&fresh.IsCurrent&&!ReferenceEquals(old,fresh),"new stable snapshot enables reads");
            GateAccessRules.Invalidate("migration.fixture","revision");
            check(!fresh.IsCurrent,"explicit invalidation invalidates caches without a new route");
            routes.Publish(RouteTilePolicySnapshot.Empty);
            check(routes.TryCaptureRoutePolicy(2,out var empty)&&empty.IsCurrent&&empty.IsDirectionAllowed(0,0),"disable publishes current inactive policy");
            return count;
        }
    }
}
