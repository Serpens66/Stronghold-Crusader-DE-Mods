using System;
using APIShared;

namespace EnemyGatePathfindingTest
{
    internal static class GateRoutePolicyTests
    {
        internal static int Run()
        {
            int count = 0;
            Action<bool> check = value => { count++; if (!value) throw new Exception("Gate snapshot/scope regression"); };
            var masks = new byte[9][];
            masks[2] = new byte[] { 0xFB, 0xFF };
            var source = new GateRoutePolicySource();
            source.Publish(new RouteTilePolicySnapshot(masks, 1));
            check(source.TryCaptureRoutePolicy(2, out var first));
            check(!first.IsDirectionAllowed(0, 2) && first.IsDirectionAllowed(1, 2));
            check(source.TryCaptureRoutePolicy(2, out var again) && ReferenceEquals(first, again));
            check(source.TryCaptureRoutePolicy(1, out var own) && own.IsDirectionAllowed(0, 2));
            var outer = source.Enter(2, false, true);
            check(!source.TryCaptureRoutePolicy(1, out _));
            var inner = source.Enter(2, true, true);
            check(source.TryCaptureRoutePolicy(2, out var unmasked) && unmasked.IsDirectionAllowed(0, 2));
            check(!ReferenceEquals(first, unmasked));
            source.Leave(inner);
            check(source.TryCaptureRoutePolicy(2, out again) && ReferenceEquals(first, again));
            var conflict = source.Enter(2, false, false);
            check(!source.TryCaptureRoutePolicy(2, out _));
            source.Leave(conflict);
            source.Publish(new RouteTilePolicySnapshot(new byte[9][], 2));
            check(!first.IsCurrent && !unmasked.IsCurrent && !source.TryCaptureRoutePolicy(2, out _));
            source.Leave(outer);
            check(source.TryCaptureRoutePolicy(2, out var captured) && captured.IsDirectionAllowed(0, 2));
            source.Publish(new RouteTilePolicySnapshot(masks, 3));
            IEnemyGateRoutePolicySnapshot recaptured = null;
            check(!captured.IsCurrent && source.TryCaptureRoutePolicy(2, out recaptured) && !recaptured.IsDirectionAllowed(0, 2));
            for (int generation = 0; generation < 80; generation++)
            {
                source.Publish(new RouteTilePolicySnapshot(masks, (ulong)generation + 4));
                check(!recaptured.IsCurrent);
                check(source.TryCaptureRoutePolicy(2, out recaptured) && recaptured.IsCurrent);
            }
            source.Publish(RouteTilePolicySnapshot.Empty);
            IEnemyGateRoutePolicySnapshot empty = null;
            check(!recaptured.IsCurrent && source.TryCaptureRoutePolicy(2, out empty));
            source.Publish(RouteTilePolicySnapshot.Empty);
            check(!empty.IsCurrent); // Even equal empty states belong to different map/publication lifetimes.
            check(!source.TryCaptureRoutePolicy(0, out _) && !source.TryCaptureRoutePolicy(9, out _));
            var ownership = new GateEdgeOwnership[9]; ownership[2] = new GateEdgeOwnership(); ownership[2].Record(0,2,578);
            var identities = new System.Collections.Generic.Dictionary<int,RouteTilePolicySnapshot.GateIdentity> {
                {578,new RouteTilePolicySnapshot.GateIdentity(99,1,5)} };
            source.Publish(new RouteTilePolicySnapshot(masks,99,edgeOwners:ownership,gateIdentities:identities));
            identities[578] = new RouteTilePolicySnapshot.GateIdentity(100,2,0);
            check(source.TryCaptureRoutePolicy(2,out var identitySnapshot));
            var typed = (IEnemyGateClimbRoutePolicySnapshot)identitySnapshot;
            check(typed.TryGetBlockedGateIdentity(0,2,out int gate,out uint global,out int owner,out int capturer) && gate==578 && global==99 && owner==1 && capturer==5);
            check(!typed.TryGetBlockedGateIdentity(1,2,out _,out _,out _,out _));
            var reference = source.Enter(2,true,true);
            check(source.TryCaptureRoutePolicy(2,out var unmaskedIdentity));
            check(!((IEnemyGateClimbRoutePolicySnapshot)unmaskedIdentity).TryGetBlockedGateIdentity(0,2,out _,out _,out _,out _));
            source.Leave(reference);
            source.Publish(RouteTilePolicySnapshot.Empty);
            check(!typed.TryGetBlockedGateIdentity(0,2,out _,out _,out _,out _));
            return count;
        }
    }
}
