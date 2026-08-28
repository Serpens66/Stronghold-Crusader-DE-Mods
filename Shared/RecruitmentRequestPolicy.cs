namespace Shared
{
    internal enum RecruitmentConstraintAction
    {
        PreserveOriginal,
        ForwardAmount,
        Block
    }

    internal readonly struct RecruitmentConstraintDecision
    {
        public RecruitmentConstraintDecision(
            RecruitmentConstraintAction action,
            int effectiveRequestedAmount,
            int amountToForward)
        {
            Action = action;
            EffectiveRequestedAmount = effectiveRequestedAmount;
            AmountToForward = amountToForward;
        }

        public RecruitmentConstraintAction Action { get; }
        public int EffectiveRequestedAmount { get; }
        public int AmountToForward { get; }
    }

    internal static class RecruitmentRequestPolicy
    {
        public const int VanillaCtrlAllAmount = 1000;

        public static RecruitmentConstraintDecision ApplyMaximum(
            int incomingAmount,
            int vanillaCtrlAmount,
            int maximumAllowed)
        {
            int effectiveRequestedAmount = incomingAmount == VanillaCtrlAllAmount
                ? System.Math.Max(0, vanillaCtrlAmount)
                : System.Math.Max(0, incomingAmount);

            if (maximumAllowed <= 0)
            {
                return new RecruitmentConstraintDecision(
                    RecruitmentConstraintAction.Block,
                    effectiveRequestedAmount,
                    0);
            }

            // A zero Vanilla preview means Vanilla has its own reason to reject
            // the request. Preserve the sentinel so Vanilla owns that feedback.
            if (effectiveRequestedAmount <= 0 || maximumAllowed >= effectiveRequestedAmount)
            {
                return new RecruitmentConstraintDecision(
                    RecruitmentConstraintAction.PreserveOriginal,
                    effectiveRequestedAmount,
                    incomingAmount);
            }

            return new RecruitmentConstraintDecision(
                RecruitmentConstraintAction.ForwardAmount,
                effectiveRequestedAmount,
                maximumAllowed);
        }
    }
}
