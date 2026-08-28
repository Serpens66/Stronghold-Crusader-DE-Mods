using System;

namespace HookContextPeerB
{
    public static class PeerB
    {
        public static IDisposable Enter(int amount)
        {
            return Shared.RecruitmentHookContext.Enter(amount);
        }

        public static bool ShouldInterpretCtrlSentinel(int amount)
        {
            return Shared.RecruitmentHookContext.ShouldInterpretCtrlSentinel(amount);
        }

        public static void RecordForwardedAmount(int amount)
        {
            Shared.RecruitmentHookContext.RecordForwardedAmount(amount);
        }
    }
}
