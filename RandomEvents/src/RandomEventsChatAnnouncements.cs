using System;
using System.Collections.Concurrent;

namespace RandomEvents
{
    internal sealed class RandomEventChatAnnouncement
    {
        public RandomEventChatAnnouncement(long sessionId, RandomEventKind kind, int targetPlayerId)
        {
            SessionId = sessionId;
            Kind = kind;
            TargetPlayerId = targetPlayerId;
        }

        public long SessionId { get; }
        public RandomEventKind Kind { get; }
        public int TargetPlayerId { get; }
    }

    // The simulation only publishes immutable results; draining belongs to the Unity thread.
    internal sealed class RandomEventsChatAnnouncements
    {
        private readonly ConcurrentQueue<RandomEventChatAnnouncement> pending =
            new ConcurrentQueue<RandomEventChatAnnouncement>();
        private bool draining;

        public bool HasPending => !pending.IsEmpty;

        public bool RecordResult(long sessionId, RandomEventKind kind, int targetPlayerId, bool applied)
        {
            if (!applied || sessionId <= 0)
                return false;

            pending.Enqueue(new RandomEventChatAnnouncement(sessionId, kind, targetPlayerId));
            return true;
        }

        public void Drain(
            Func<long> currentSession,
            Func<bool> isEnabled,
            Func<RandomEventChatAnnouncement, bool> tryDisplay,
            Action<Exception> reportFailure)
        {
            if (draining)
                return;
            draining = true;
            try
            {
                while (pending.TryPeek(out RandomEventChatAnnouncement announcement))
                {
                    try
                    {
                        if (announcement.SessionId == currentSession() && isEnabled() && !tryDisplay(announcement))
                            return; // Keep the head and its order while the HUD is being created.
                    }
                    catch (Exception ex)
                    {
                        pending.TryDequeue(out _);
                        reportFailure(ex);
                        continue;
                    }
                    pending.TryDequeue(out _);
                }
            }
            finally { draining = false; }
        }
    }
}
