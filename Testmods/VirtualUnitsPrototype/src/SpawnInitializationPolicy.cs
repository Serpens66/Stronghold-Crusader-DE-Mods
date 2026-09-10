namespace VirtualUnitsPrototype
{
    internal static class SpawnInitializationPolicy
    {
        public static bool CanFinalize(bool identityValid, bool isAlive, bool placementValid, bool visualRequired, bool visualSeen)
            => identityValid && isAlive && placementValid && (!visualRequired || visualSeen);

        public static bool HasTimedOut(int currentFrame, int deadlineFrame) => currentFrame >= deadlineFrame;
    }
}
