namespace BugfixesAndQoL
{
    internal enum MoatFillHookOwner
    {
        Standalone,
        MoveMoat,
        Conflict
    }

    internal static class MoatFillHookOwnershipPolicy
    {
        internal static MoatFillHookOwner Resolve(
            bool moveMoatLoaded,
            bool bridgeAvailable,
            int bridgeStatus)
        {
            if (!moveMoatLoaded)
                return MoatFillHookOwner.Standalone;
            if (!bridgeAvailable)
                return MoatFillHookOwner.Conflict;
            if (bridgeStatus == 1)
                return MoatFillHookOwner.MoveMoat;
            if (bridgeStatus == 0)
                return MoatFillHookOwner.Standalone;
            return MoatFillHookOwner.Conflict;
        }
    }
}
