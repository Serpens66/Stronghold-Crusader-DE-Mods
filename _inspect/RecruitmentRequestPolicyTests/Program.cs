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
