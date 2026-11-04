using System;

namespace Spectator
{
    internal static class OrderTargetPolicy
    {
        // A remaining resource/Lord record does not keep an eliminated player selectable.
        internal static bool IsAvailable(bool actionable, bool eliminated)
        { return actionable && !eliminated; }

        internal static int Revalidate(int selected, Func<int, bool> available)
        { return selected > 0 && available(selected) ? selected : 0; }

        internal static bool CanExecute(TestActionKind kind, bool actorAvailable, bool targetAvailable)
        { return actorAvailable && (kind == TestActionKind.Cancel || targetAvailable); }
    }

    internal static class ToolPolicy
    {
        internal static bool PowerContextMatches(long expectedSession, long session, int actor, int selectedActor,
            long expectedGeneration, long generation, bool enabled)
        { return enabled && session > 0 && expectedSession == session && actor > 0 && actor == selectedActor && expectedGeneration == generation; }

        internal static bool SameBuilding(long expectedSession, long session, int actor, int owner,
            int expectedId, int id, uint expectedGlobal, uint global, bool alive)
        { return session > 0 && session == expectedSession && actor == owner && id > 0 && id == expectedId && global != 0 && global == expectedGlobal && alive; }

        internal static bool InRadius(int x, int y, int originX, int originY, int squaredRadius)
        {
            long dx = (long)x - originX, dy = (long)y - originY;
            return squaredRadius >= 0 && dx >= -(long)squaredRadius && dx <= squaredRadius &&
                dy >= -(long)squaredRadius && dy <= squaredRadius && dx * dx + dy * dy <= squaredRadius;
        }

        internal static T WithoutCost<T>(Func<uint> read, Action<uint> write, uint required, Func<T> execute)
        {
            uint before = read();
            try { write(required); return execute(); }
            finally { write(before); }
        }
    }
}
