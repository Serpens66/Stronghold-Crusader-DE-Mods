namespace Spectator
{
    internal static class ObserverBuildPolicy
    {
        internal static bool IsReadOnly(bool eliminated, bool placement, bool validTarget)
        { return eliminated && !(placement && validTarget); }
        internal static bool AllowMapper(bool localInput, bool readOnly, int mapper)
        { return !localInput || !readOnly || mapper == 0; }
    }
}
