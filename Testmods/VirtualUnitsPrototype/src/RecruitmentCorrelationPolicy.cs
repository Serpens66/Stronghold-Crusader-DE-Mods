namespace VirtualUnitsPrototype
{
    internal static class RecruitmentCorrelationPolicy
    {
        internal static bool ShouldComplete(int matched, int requested, int unresolved, int currentTick, int lastTransitionTick, int deadlineTick, int quietTicks)
        {
            if (requested <= 0 || matched < 0 || unresolved < 0 || quietTicks < 0) return true;
            return matched >= requested || currentTick >= deadlineTick ||
                matched > 0 && unresolved == 0 && currentTick >= lastTransitionTick + quietTicks;
        }
    }
}
