using System;

namespace HookContextPeerA
{
    public static class PeerA
    {
        public static IDisposable Enter(int amount)
        {
            return Shared.RecruitmentHookContext.Enter(amount);
        }

        public static bool ShouldInterpretCtrlSentinel(int amount)
        {
            return Shared.RecruitmentHookContext.ShouldInterpretCtrlSentinel(amount);
        }

        public static (int FinalAmount, bool HasConcreteAmount) GetResult()
        {
            Shared.RecruitmentHookContext.Result result = Shared.RecruitmentHookContext.GetResult();
            return (result.FinalAmount, result.HasConcreteAmount);
        }
    }
}
