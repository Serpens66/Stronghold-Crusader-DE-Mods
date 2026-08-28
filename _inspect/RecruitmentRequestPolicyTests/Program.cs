using Shared;

static class Program
{
    private static int assertions;

    public static void Main()
    {
        TestUnconstrainedRequests();
        TestBindingConstraints();
        TestVanillaFailureOwnership();
        TestHookOrderIndependence();
        TestPendingReservationAcrossHookOrders();
        TestBlockedInnerHookDoesNotReserve();
        TestConcreteThousandIsNotReinterpreted();
        TestStandaloneContext();
        TestCrossAssemblyContext();
        TestRapidRepeatedRequests();
        Console.WriteLine($"Recruitment request policy tests passed: assertions={assertions}.");
    }

    private static void TestUnconstrainedRequests()
    {
        AssertPreserved(1, 1, int.MaxValue, 1);
        AssertPreserved(5, 5, int.MaxValue, 5);
        AssertPreserved(1000, 37, int.MaxValue, 37);
        AssertPreserved(1000, 100, 100, 100);
        AssertPreserved(8, 100, 8, 8);
    }

    private static void TestBindingConstraints()
    {
        AssertForwarded(1000, 40, 12, 40, 12);
        AssertForwarded(5, 5, 3, 5, 3);
        AssertBlocked(1000, 40, 0, 40);
        AssertBlocked(1, 1, -1, 1);
    }

    private static void TestVanillaFailureOwnership()
    {
        RecruitmentConstraintDecision decision = RecruitmentRequestPolicy.ApplyMaximum(1000, 0, 25);
        Assert(decision.Action == RecruitmentConstraintAction.PreserveOriginal,
            "A zero Vanilla preview must remain Vanilla's responsibility.");
        Assert(decision.AmountToForward == 1000,
            "The Ctrl sentinel must survive a Vanilla-owned failure.");
    }

    private static void TestHookOrderIndependence()
    {
        int costsThenLimit = ApplyForwardedAmount(
            ApplyForwardedAmount(1000, 20, 7),
            20,
            5);
        int limitThenCosts = ApplyForwardedAmount(
            ApplyForwardedAmount(1000, 20, 5),
            20,
            7);
        Assert(costsThenLimit == 5, "Costs-then-limit chaining produced the wrong amount.");
        Assert(limitThenCosts == 5, "Limit-then-costs chaining produced the wrong amount.");

        int unconstrained = ApplyForwardedAmount(
            ApplyForwardedAmount(1000, 20, 50),
            20,
            40);
        Assert(unconstrained == 1000,
            "Non-binding hooks must preserve the Ctrl sentinel in either chain.");
    }

    private static void TestRapidRepeatedRequests()
    {
        int remaining = 12;
        RecruitmentConstraintDecision first = RecruitmentRequestPolicy.ApplyMaximum(1000, 8, remaining);
        Assert(first.Action == RecruitmentConstraintAction.PreserveOriginal,
            "The first non-binding Ctrl request should remain unchanged.");
        remaining -= first.EffectiveRequestedAmount;

        RecruitmentConstraintDecision second = RecruitmentRequestPolicy.ApplyMaximum(1000, 8, remaining);
        Assert(second.Action == RecruitmentConstraintAction.ForwardAmount,
            "The second rapid request should be constrained by the pending reservation.");
        Assert(second.AmountToForward == 4,
            "The second rapid request exceeded the remaining unit limit.");
    }

    private static void TestPendingReservationAcrossHookOrders()
    {
        using (RecruitmentHookContext.Scope outerLimit = RecruitmentHookContext.Enter(1000))
        {
            Assert(RecruitmentHookContext.ShouldInterpretCtrlSentinel(1000),
                "The outer UnitLimit hook must recognize Vanilla's Ctrl sentinel.");

            RecruitmentConstraintDecision limit = RecruitmentRequestPolicy.ApplyMaximum(1000, 20, 20);
            Assert(limit.Action == RecruitmentConstraintAction.PreserveOriginal,
                "The non-binding outer unit limit must preserve Ctrl.");

            using (RecruitmentHookContext.Scope innerCosts = RecruitmentHookContext.Enter(1000))
            {
                Assert(RecruitmentHookContext.ShouldInterpretCtrlSentinel(1000),
                    "An unchanged Ctrl sentinel must remain recognizable by the inner hook.");
                RecruitmentConstraintDecision costs = RecruitmentRequestPolicy.ApplyMaximum(1000, 20, 5);
                Assert(costs.Action == RecruitmentConstraintAction.ForwardAmount,
                    "The inner extra costs should constrain the request.");
                RecruitmentHookContext.RecordForwardedAmount(costs.AmountToForward);
            }

            RecruitmentHookContext.Result result = RecruitmentHookContext.GetResult();
            int reserved = RecruitmentRequestPolicy.ReconcilePendingAmount(
                limit.EffectiveRequestedAmount,
                result.FinalAmount,
                result.HasConcreteAmount);
            Assert(reserved == 5,
                "UnitLimit must reserve the final inner UnitCosts amount, not its earlier preview.");
        }

        using (RecruitmentHookContext.Scope outerCosts = RecruitmentHookContext.Enter(1000))
        {
            RecruitmentConstraintDecision costs = RecruitmentRequestPolicy.ApplyMaximum(1000, 20, 5);
            RecruitmentHookContext.RecordForwardedAmount(costs.AmountToForward);
            using (RecruitmentHookContext.Scope innerLimit = RecruitmentHookContext.Enter(costs.AmountToForward))
            {
                Assert(!RecruitmentHookContext.ShouldInterpretCtrlSentinel(costs.AmountToForward),
                    "A concrete amount from UnitCosts must not be treated as Ctrl by UnitLimit.");
                RecruitmentConstraintDecision limit = RecruitmentRequestPolicy.ApplyMaximum(
                    costs.AmountToForward,
                    20,
                    20,
                    false);
                RecruitmentHookContext.Result result = RecruitmentHookContext.GetResult();
                int reserved = RecruitmentRequestPolicy.ReconcilePendingAmount(
                    limit.EffectiveRequestedAmount,
                    result.FinalAmount,
                    result.HasConcreteAmount);
                Assert(reserved == 5,
                    "UnitLimit must reserve the same final amount when it is the inner hook.");
            }
        }
    }

    private static void TestConcreteThousandIsNotReinterpreted()
    {
        using (RecruitmentHookContext.Scope outer = RecruitmentHookContext.Enter(1000))
        {
            RecruitmentConstraintDecision outerDecision = RecruitmentRequestPolicy.ApplyMaximum(1000, 1500, 1000);
            Assert(outerDecision.Action == RecruitmentConstraintAction.ForwardAmount,
                "The outer hook should explicitly constrain 1500 units to 1000.");
            RecruitmentHookContext.RecordForwardedAmount(outerDecision.AmountToForward);

            using (RecruitmentHookContext.Scope inner = RecruitmentHookContext.Enter(1000))
            {
                bool interpretCtrl = RecruitmentHookContext.ShouldInterpretCtrlSentinel(1000);
                Assert(!interpretCtrl,
                    "A concretely forwarded amount of 1000 must not be reinterpreted as Ctrl.");
                RecruitmentConstraintDecision innerDecision = RecruitmentRequestPolicy.ApplyMaximum(
                    1000,
                    1500,
                    1200,
                    interpretCtrl);
                Assert(innerDecision.EffectiveRequestedAmount == 1000,
                    "The inner hook raised a concrete 1000-unit constraint back to the Vanilla preview.");
                Assert(innerDecision.Action == RecruitmentConstraintAction.PreserveOriginal,
                    "The inner non-binding constraint should preserve the concrete amount.");
            }
        }
    }

    private static void TestBlockedInnerHookDoesNotReserve()
    {
        using (RecruitmentHookContext.Scope outerLimit = RecruitmentHookContext.Enter(1000))
        {
            using (RecruitmentHookContext.Scope innerCosts = RecruitmentHookContext.Enter(1000))
                RecruitmentHookContext.RecordBlocked();

            RecruitmentHookContext.Result result = RecruitmentHookContext.GetResult();
            int reserved = RecruitmentRequestPolicy.ReconcilePendingAmount(
                20,
                result.FinalAmount,
                result.HasConcreteAmount);
            Assert(reserved == 0,
                "UnitLimit must not reserve anything when an inner hook blocks the request.");
        }
    }

    private static void TestStandaloneContext()
    {
        using (RecruitmentHookContext.Scope standalone = RecruitmentHookContext.Enter(1000))
        {
            Assert(RecruitmentHookContext.ShouldInterpretCtrlSentinel(1000),
                "A standalone mod must recognize Vanilla's Ctrl sentinel.");
            RecruitmentHookContext.Result result = RecruitmentHookContext.GetResult();
            Assert(!result.HasConcreteAmount && result.FinalAmount == 1000,
                "An unconstrained standalone Ctrl request must remain untouched.");
            Assert(RecruitmentRequestPolicy.ReconcilePendingAmount(17, result.FinalAmount, result.HasConcreteAmount) == 17,
                "Standalone UnitLimit must reserve its Vanilla preview.");
        }
    }

    private static void TestCrossAssemblyContext()
    {
        using (IDisposable outer = HookContextPeerA.PeerA.Enter(1000))
        {
            Assert(HookContextPeerA.PeerA.ShouldInterpretCtrlSentinel(1000),
                "The first assembly did not recognize the root Ctrl request.");
            using (IDisposable inner = HookContextPeerB.PeerB.Enter(1000))
            {
                Assert(HookContextPeerB.PeerB.ShouldInterpretCtrlSentinel(1000),
                    "The second assembly could not see the first assembly's Ctrl context.");
                HookContextPeerB.PeerB.RecordForwardedAmount(6);
            }

            (int finalAmount, bool hasConcreteAmount) = HookContextPeerA.PeerA.GetResult();
            Assert(hasConcreteAmount && finalAmount == 6,
                "The first assembly could not see the second assembly's concrete constraint.");
        }
    }

    private static int ApplyForwardedAmount(int incomingAmount, int vanillaCtrlAmount, int maximumAllowed)
    {
        RecruitmentConstraintDecision decision = RecruitmentRequestPolicy.ApplyMaximum(
            incomingAmount,
            vanillaCtrlAmount,
            maximumAllowed);
        return decision.Action switch
        {
            RecruitmentConstraintAction.PreserveOriginal => incomingAmount,
            RecruitmentConstraintAction.ForwardAmount => decision.AmountToForward,
            _ => 0
        };
    }

    private static void AssertPreserved(
        int incomingAmount,
        int vanillaCtrlAmount,
        int maximumAllowed,
        int expectedEffectiveAmount)
    {
        RecruitmentConstraintDecision decision = RecruitmentRequestPolicy.ApplyMaximum(
            incomingAmount,
            vanillaCtrlAmount,
            maximumAllowed);
        Assert(decision.Action == RecruitmentConstraintAction.PreserveOriginal,
            $"Expected PreserveOriginal for incoming={incomingAmount}, vanilla={vanillaCtrlAmount}, max={maximumAllowed}.");
        Assert(decision.AmountToForward == incomingAmount,
            "PreserveOriginal changed the incoming amount.");
        Assert(decision.EffectiveRequestedAmount == expectedEffectiveAmount,
            "The effective requested amount was incorrect.");
    }

    private static void AssertForwarded(
        int incomingAmount,
        int vanillaCtrlAmount,
        int maximumAllowed,
        int expectedEffectiveAmount,
        int expectedForwardedAmount)
    {
        RecruitmentConstraintDecision decision = RecruitmentRequestPolicy.ApplyMaximum(
            incomingAmount,
            vanillaCtrlAmount,
            maximumAllowed);
        Assert(decision.Action == RecruitmentConstraintAction.ForwardAmount,
            $"Expected ForwardAmount for incoming={incomingAmount}, vanilla={vanillaCtrlAmount}, max={maximumAllowed}.");
        Assert(decision.EffectiveRequestedAmount == expectedEffectiveAmount,
            "The effective requested amount was incorrect.");
        Assert(decision.AmountToForward == expectedForwardedAmount,
            "The forwarded amount was incorrect.");
    }

    private static void AssertBlocked(
        int incomingAmount,
        int vanillaCtrlAmount,
        int maximumAllowed,
        int expectedEffectiveAmount)
    {
        RecruitmentConstraintDecision decision = RecruitmentRequestPolicy.ApplyMaximum(
            incomingAmount,
            vanillaCtrlAmount,
            maximumAllowed);
        Assert(decision.Action == RecruitmentConstraintAction.Block,
            $"Expected Block for incoming={incomingAmount}, vanilla={vanillaCtrlAmount}, max={maximumAllowed}.");
        Assert(decision.EffectiveRequestedAmount == expectedEffectiveAmount,
            "The blocked effective amount was incorrect.");
        Assert(decision.AmountToForward == 0,
            "A blocked request must not forward an amount.");
    }

    private static void Assert(bool condition, string message)
    {
        assertions++;
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
