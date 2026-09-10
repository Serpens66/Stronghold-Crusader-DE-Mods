using System;

namespace OxTetherIdleFixTest
{
    internal static class Program
    {
        private static int assertions;

        private static int Main()
        {
            TestBlockadeEligibility();
            TestBlockerRouteRestorationPolicy();
            TestExactIdleEpisodeAndRepairVerification();
            TestGeneralStallIdentity();
            TestDiagnosticTransitions();
            Console.WriteLine($"OxTetherIdleFixTest policy tests: {assertions} assertions passed.");
            return 0;
        }

        private static void TestBlockerRouteRestorationPolicy()
        {
            Assert(OxTargetBlockadePolicy.ShouldReissueOriginalBlockerRoute(1, 1, 10, 10, 20, 20),
                "unchanged moving state restores original route");
            Assert(!OxTargetBlockadePolicy.ShouldReissueOriginalBlockerRoute(1, 2, 10, 10, 20, 20),
                "changed AI state is not rewound with an old route");
            Assert(!OxTargetBlockadePolicy.ShouldReissueOriginalBlockerRoute(1, 1, 10, 10, 10, 10),
                "stationary blocker does not receive a synthetic route");
        }

        private static void TestBlockadeEligibility()
        {
            OxObservation moving = Observation(state: 1, pathFlags: 2, pathCursor: 3, pathSize: 10);
            Assert(OxTargetBlockadePolicy.IsEligible(moving, episodeActive: false), "moving Ox eligible");
            Assert(!OxTargetBlockadePolicy.IsEligible(moving, episodeActive: true), "active episode excluded");
            Assert(!OxTargetBlockadePolicy.IsEligible(Observation(state: 2, pathFlags: 2), false), "work state excluded");
            Assert(!OxTargetBlockadePolicy.IsEligible(Observation(state: 1, pathFlags: 0), false), "inactive path excluded");
            Assert(!OxTargetBlockadePolicy.IsEligible(
                Observation(state: 1, pathFlags: 2, currentX: 20, currentY: 21, requestedX: 20, requestedY: 21),
                false), "already at target excluded");
            Assert(OxTargetBlockadePolicy.IsEligibleMovingBlocker(moving), "moving Ox accepted as blocker");
            Assert(!OxTargetBlockadePolicy.IsEligibleMovingBlocker(Observation(state: 4, pathFlags: 0)),
                "stationary work-state Ox excluded as blocker");
            Assert(!OxTargetBlockadePolicy.IsEligibleMovingBlocker(Observation(state: 1, pathFlags: 0)),
                "travel-state Ox without active path excluded as blocker");
            Assert(OxTargetBlockadePolicy.HasIndependentTarget(moving, 30, 30),
                "blocker with a different route can vacate the synthetic target");
            Assert(!OxTargetBlockadePolicy.HasIndependentTarget(moving, moving.RequestedX, moving.RequestedY),
                "blocker already routed to the synthetic target is excluded");
            Assert(OxTargetBlockadePolicy.DidBlockerAdvance(10, 10, 3, 11, 10, 3),
                "blocker tile movement resets no-progress watchdog");
            Assert(OxTargetBlockadePolicy.DidBlockerAdvance(10, 10, 3, 10, 10, 4),
                "forward path cursor resets no-progress watchdog");
            Assert(!OxTargetBlockadePolicy.DidBlockerAdvance(10, 10, 3, 10, 10, 3),
                "unchanged blocker snapshot does not reset watchdog");
            Assert(!OxTargetBlockadePolicy.DidBlockerAdvance(10, 10, 3, 10, 10, 1),
                "path cursor reset alone does not fake progress");
        }

        private static void TestExactIdleEpisodeAndRepairVerification()
        {
            OxIdleEpisodePolicy policy = new OxIdleEpisodePolicy();
            OxObservation stuck = Observation(state: 1, pathFlags: 0, marker: 7);
            for (int tick = 1; tick < OxIdleEpisodePolicy.RequiredConsecutiveTicks; tick++)
                Assert(policy.Observe(stuck, tick) == OxEpisodeAction.None, "candidate remains pending");
            Assert(
                policy.Observe(stuck, OxIdleEpisodePolicy.RequiredConsecutiveTicks) == OxEpisodeAction.ConfirmAndRepair,
                "candidate confirms exactly at threshold");
            OxObservation progressed = Observation(state: 2, pathFlags: 0, marker: 0);
            Assert(
                policy.Observe(progressed, OxIdleEpisodePolicy.RequiredConsecutiveTicks + 1) == OxEpisodeAction.Verified,
                "expected Vanilla transition verifies repair");
        }

        private static void TestGeneralStallIdentity()
        {
            OxObservation first = Observation(state: 3, pathFlags: 2);
            Assert(first.IsSameGeneralStallAs(Observation(state: 3, pathFlags: 0, marker: 9)),
                "general stall ignores hypothesized path signature");
            Assert(!first.IsSameGeneralStallAs(Observation(state: 3, pathFlags: 2, currentX: 11)),
                "movement ends general stall");
            Assert(!first.IsSameGeneralStallAs(Observation(state: 4, pathFlags: 2)),
                "state transition ends general stall");
            Assert(!first.IsSameGeneralStallAs(Observation(state: 3, pathFlags: 2, globalId: 43)),
                "unit ID reuse ends general stall");
        }

        private static void TestDiagnosticTransitions()
        {
            OxObservation first = Observation(state: 1, pathFlags: 2);
            Assert(Observation(state: 1, pathFlags: 2, pathCursor: 4).HasDiagnosticTransitionFrom(first),
                "path cursor transition captured");
            Assert(Observation(state: 1, pathFlags: 2, carryGoods: 8).HasDiagnosticTransitionFrom(first),
                "goods transition captured");
            Assert(!first.HasDiagnosticTransitionFrom(first), "identical diagnostic snapshot stays quiet");
        }

        private static OxObservation Observation(
            ushort state,
            ushort pathFlags,
            ushort marker = 0,
            ushort currentX = 10,
            ushort currentY = 10,
            ushort requestedX = 20,
            ushort requestedY = 20,
            ushort pathCursor = 1,
            uint pathSize = 10,
            uint globalId = 42,
            uint carryGoods = 0) =>
            new OxObservation(
                5,
                globalId,
                state,
                pathFlags,
                marker,
                currentX,
                currentY,
                requestedX,
                requestedY,
                pathCursor,
                pathSize,
                carryGoods: carryGoods);

        private static void Assert(bool condition, string message)
        {
            assertions++;
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
