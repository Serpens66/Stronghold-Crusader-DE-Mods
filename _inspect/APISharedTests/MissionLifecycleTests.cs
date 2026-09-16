using APIShared;
using System;
using System.Collections.Generic;
using System.Linq;

namespace APISharedTests
{
    internal static class MissionLifecycleTests
    {
        private static MissionContext Seed(MissionStartKind kind) => new MissionContext(0, kind, default,
            kind == MissionStartKind.EditorCreated ? null : "same.map", "same map", false, 160, 1, 1);

        internal static void Run(Action<bool, string> check)
        {
            foreach (MissionStartKind first in Enum.GetValues(typeof(MissionStartKind)))
            foreach (MissionStartKind next in Enum.GetValues(typeof(MissionStartKind)))
            {
                var events = new List<MissionLifecycleNotification>();
                var state = new MissionLifecycleState(_ => { });
                state.Register("mod", "observer", events.Add);
                long a = state.Begin(Seed(first));
                state.Checkpoint(a, Seed(first), MissionInitializationPhase.BeforeLoad);
                state.Checkpoint(a, Seed(first), MissionInitializationPhase.BeforeLoad);
                state.Finish(a, Seed(first), true, true, true, false);
                state.Ready(a, Seed(first));
                long b = state.Begin(Seed(next));
                check(state.Current == null, "replacement retained an old ready session");
                state.Finish(a, Seed(first), true, true, true, false);
                state.Finish(b, Seed(next), true, true, true, false);
                state.End(MissionEndReason.SceneChanged);
                state.End(MissionEndReason.Unloaded);
                check(events.Select(e => e.Kind).SequenceEqual(new[] { MissionLifecycleKind.Initialization,
                    MissionLifecycleKind.Start, MissionLifecycleKind.End, MissionLifecycleKind.Start, MissionLifecycleKind.End }),
                    "duplicate/reordered transition for " + first + " -> " + next);
                check(events[2].Context.SessionId == a && events[2].Context.StartKind == first &&
                    events[3].Context.SessionId == b && b > a && events[4].Context.StartKind == next,
                    "End lost its original context or same-size/player replacement reused an identity");
            }

            foreach (bool normal in new[] { true, false })
            foreach (bool native in new[] { true, false })
            foreach (bool managed in new[] { true, false })
            foreach (bool client in new[] { true, false })
            {
                var events = new List<MissionLifecycleNotification>();
                var state = new MissionLifecycleState(_ => { });
                state.Register("matrix", "observer", events.Add);
                long id = state.Begin(Seed(MissionStartKind.LoadedSave));
                bool waiting = state.Finish(id, Seed(MissionStartKind.LoadedSave), normal, native, managed, client);
                check(waiting == (normal && native && !managed && client), "incorrect delayed-client decision");
                check((state.Current != null) == (normal && native && managed), "Ready published for failed/incomplete initialization");
                if (!waiting && state.Current == null)
                    check(events.Count == 1 && events[0].Kind == MissionLifecycleKind.End && !events[0].HasStarted &&
                        events[0].EndReason == (normal ? MissionEndReason.Failed : MissionEndReason.Exception),
                        "failed attempt did not end exactly once with its failure reason");
                if (waiting)
                {
                    state.Finish(id, Seed(MissionStartKind.LoadedSave), true, true, true, true);
                    state.Finish(id, Seed(MissionStartKind.LoadedSave), true, true, true, true);
                    check(events.Count == 1 && events[0].Kind == MissionLifecycleKind.Start, "client completion was missing or duplicated");
                }
            }

            var replayState = new MissionLifecycleState(_ => { });
            long readyId = replayState.Begin(Seed(MissionStartKind.EditorLoaded));
            replayState.Ready(readyId, Seed(MissionStartKind.EditorLoaded));
            var replay = new List<MissionLifecycleNotification>();
            check(replayState.Register("owner", "stable", replay.Add), "valid registration rejected");
            check(!replayState.Register("owner", "stable", replay.Add), "duplicate owner-local ID accepted");
            check(replay.Count == 1 && replay[0].IsReplay && replay[0].Context.SessionId == readyId,
                "late observer did not receive exactly one ready replay");
            replayState.End(MissionEndReason.Unloaded);
            replayState.Register("late", "after-end", replay.Add);
            check(replay.Count == 2 && replay[1].Kind == MissionLifecycleKind.End, "ended session was replayed");

            int failures = 0, survivors = 0;
            var isolated = new MissionLifecycleState(_ => { failures++; throw new Exception("logger"); });
            isolated.Register("bad", "one", _ => throw new Exception("observer"));
            isolated.Register("good", "one", _ => survivors++);
            long isolationId = isolated.Begin(Seed(MissionStartKind.NewGame));
            isolated.Ready(isolationId, Seed(MissionStartKind.NewGame));
            isolated.End(MissionEndReason.Unloaded);
            check(failures == 2 && survivors == 2, "observer/logger failure escaped or starved another observer");

            var order = new List<string>();
            var reentrant = new MissionLifecycleState(_ => { });
            reentrant.Register("first", "one", e =>
            {
                order.Add("first:" + e.Kind);
                if (e.Kind == MissionLifecycleKind.Start) reentrant.End(MissionEndReason.Replaced);
            });
            reentrant.Register("second", "one", e => order.Add("second:" + e.Kind));
            long reentrantId = reentrant.Begin(Seed(MissionStartKind.NewGame));
            reentrant.Ready(reentrantId, Seed(MissionStartKind.NewGame));
            check(order.SequenceEqual(new[] { "first:Start", "second:Start", "first:End", "second:End" }),
                "reentrant End overtook Start for another observer");

            var replaced = new MissionLifecycleState(_ => { });
            long stale = replaced.Begin(Seed(MissionStartKind.LoadedSave));
            check(replaced.Finish(stale, Seed(MissionStartKind.LoadedSave), true, true, false, true), "client did not wait");
            long current = replaced.Begin(Seed(MissionStartKind.NewGame));
            replaced.Ready(stale, Seed(MissionStartKind.LoadedSave));
            check(replaced.Current == null && replaced.IsPending(current), "obsolete client completion activated a replaced mission");
            replaced.End(MissionEndReason.Exception, stale);
            check(replaced.IsPending(current), "obsolete failure ended a newer attempt");
        }
    }
}
