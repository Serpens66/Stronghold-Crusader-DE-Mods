using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RandomEvents
{
    internal static class RandomEventsChatAnnouncementTests
    {
        internal static void Run(Action<bool, string> check)
        {
            var queue = new RandomEventsChatAnnouncements();
            var shown = new List<RandomEventChatAnnouncement>();
            long session = 41;
            bool enabled = true;
            bool hudReady = false;
            int failures = 0;
            Func<RandomEventChatAnnouncement, bool> display = item =>
            {
                if (!hudReady) return false;
                shown.Add(item);
                return true;
            };
            Action drain = () => queue.Drain(() => session, () => enabled, display, _ => failures++);

            check(!queue.RecordResult(session, RandomEventKind.WheatInfestation, 2, false),
                "failed/no-effect dispatch must not queue chat");
            check(!queue.RecordResult(0, RandomEventKind.Fire, 2, true), "inactive session must not queue chat");
            check(!queue.HasPending, "unsuccessful results left a pending message");

            // Target identity, not perspective or role, is carried across the thread boundary.
            // Both distribution modes use this same result sink, including self and AI targets.
            queue.RecordResult(session, RandomEventKind.Fire, 1, true);
            queue.RecordResult(session, RandomEventKind.Marriage, 8, true);
            drain();
            check(queue.HasPending && shown.Count == 0, "missing HUD lost announcements");
            hudReady = true;
            drain();
            drain();
            check(shown.Count == 2 && shown[0].TargetPlayerId == 1 && shown[1].TargetPlayerId == 8 &&
                shown[0].Kind == RandomEventKind.Fire && shown[1].SessionId == session,
                "announcements must preserve identity and FIFO and display exactly once");

            enabled = false;
            queue.RecordResult(session, RandomEventKind.Plague, 3, true);
            drain();
            enabled = true;
            drain();
            check(shown.Count == 2 && !queue.HasPending, "disabled local preference must discard, not postpone, chat");

            hudReady = false;
            queue.RecordResult(session, RandomEventKind.Bard, 4, true);
            drain();
            session++;
            queue.RecordResult(session, RandomEventKind.Fair, 5, true);
            hudReady = true;
            drain();
            check(shown.Count == 3 && shown.Last().Kind == RandomEventKind.Fair,
                "a session replacement must discard stale HUD work while retaining new work");
            queue.RecordResult(session, RandomEventKind.Fire, 5, true);
            session = 0;
            drain();
            check(shown.Count == 3 && !queue.HasPending, "unload must discard pending chat");

            session = 43;
            queue.RecordResult(session, RandomEventKind.Fire, 2, true);
            queue.RecordResult(session, RandomEventKind.Bard, 3, true);
            queue.Drain(() => session, () => true, item =>
            {
                if (item.Kind == RandomEventKind.Fire) throw new InvalidOperationException("HUD failure");
                // A UI observer can cause nested delivery; the same head must not be shown twice.
                drain();
                shown.Add(item);
                return true;
            }, _ => failures++);
            check(failures == 1 && shown.Count == 4 && shown.Last().Kind == RandomEventKind.Bard && !queue.HasPending,
                "failed presentation or reentrancy blocked/duplicated a later event");

            // Producers may run on the simulation thread while the main thread drains.
            Task producer = Task.Run(() =>
            {
                for (int i = 0; i < 200; i++) queue.RecordResult(session, RandomEventKind.Fire, i, true);
            });
            var targets = new List<int>();
            Action concurrentDrain = () => queue.Drain(() => session, () => true,
                item => { targets.Add(item.TargetPlayerId); return true; }, _ => failures++);
            while (!producer.IsCompleted) concurrentDrain();
            producer.GetAwaiter().GetResult();
            concurrentDrain();
            check(targets.SequenceEqual(Enumerable.Range(0, 200)) && failures == 1,
                "concurrent result publication lost, duplicated or reordered chat");
        }
    }
}
