using System;
using System.Threading;

namespace BugfixesAndQoL
{
    // Optional context for a separately installed wood-site test. This does not
    // inspect tiles, change Vanilla decisions, or install a hook of its own.
    public static class AiWoodBuildCallScope
    {
        [ThreadStatic] private static int currentPlayerId;
        [ThreadStatic] private static long currentCallId;
        private static int registered;
        private static long nextCallId;

        public struct Previous
        {
            internal readonly int PlayerId;
            internal readonly long CallId;
            internal readonly bool Entered;

            internal Previous(int playerId, long callId, bool entered)
            {
                PlayerId = playerId;
                CallId = callId;
                Entered = entered;
            }
        }

        // Registration is process-lifetime. The caller's native patch is likewise
        // permanent, while its own mission activation decides whether to intervene.
        public static void RegisterConsumer()
        {
            Interlocked.Exchange(ref registered, 1);
        }

        public static Previous Enter(int playerId)
        {
            if (Volatile.Read(ref registered) == 0 || playerId < 1 || playerId > 8)
                return default(Previous);
            var previous = new Previous(currentPlayerId, currentCallId, true);
            currentPlayerId = playerId;
            currentCallId = Interlocked.Increment(ref nextCallId);
            return previous;
        }

        public static void Leave(Previous previous)
        {
            if (!previous.Entered) return;
            currentPlayerId = previous.PlayerId;
            currentCallId = previous.CallId;
        }

        public static bool TryGetCurrent(out int playerId, out long callId)
        {
            playerId = currentPlayerId;
            callId = currentCallId;
            return Volatile.Read(ref registered) != 0 && playerId >= 1 &&
                playerId <= 8 && callId != 0;
        }
    }
}
