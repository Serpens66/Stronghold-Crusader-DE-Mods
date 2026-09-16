using APIShared;
using System;
using System.Collections.Generic;
using System.Linq;

namespace APISharedTests
{
    internal static class EditorMapLifecycleTests
    {
        internal static void Run(Action<bool, string> check)
        {
            var operation = new EditorMapLifecycleState(_ => { });
            var operationEvents = new List<EditorMapLifecycleNotification>();
            operation.Register("test", "operations", operationEvents.Add);
            int calls = 0;
            bool returned = operation.Run(EditorMapOrigin.Created, null, () =>
            {
                calls++;
                operation.ObserveNativeUnload();
                operation.ObserveNativeUnload();
                check(operationEvents.Count == 0, "Ready appeared before managed initialization finished");
                return true;
            });
            check(returned && calls == 1 && operationEvents.Count == 1 && operation.Current != null,
                "nested native unloads prevented one successful creation or Vanilla ran more than once");
            var originalError = new InvalidOperationException("vanilla failure");
            try
            {
                operation.Run(EditorMapOrigin.Loaded, "broken.map", () => { calls++; throw originalError; });
                check(false, "Vanilla exception was swallowed");
            }
            catch (InvalidOperationException ex) { check(ReferenceEquals(ex, originalError), "Vanilla exception was replaced"); }
            check(calls == 2 && operation.Current == null && operationEvents.Count == 2 &&
                operationEvents[1].Kind == EditorMapLifecycleKind.Ended,
                "failed replacement emitted Ready, retained stale state or called Vanilla again");
            check(!operation.Run(EditorMapOrigin.Loaded, "missing.map", () => { calls++; return false; }) &&
                calls == 3 && operationEvents.Count == 2, "false load result was changed or emitted Ready");
            operation.Run(EditorMapOrigin.Loaded, "ok.map", () => true);
            operation.ObserveNativeUnload();
            check(operation.Current == null && operationEvents.Count == 4,
                "exception path leaked the nested-unload suppression depth");

            foreach (bool menuWithStaleEditorFlag in new[] { true, false })
            {
                var screen = new EditorMapLifecycleState(_ => { });
                var screens = new List<EditorMapLifecycleNotification>();
                screen.Register("screen", "transitions", screens.Add);
                screen.Run(EditorMapOrigin.Created, null, () => true);
                long session = screen.Current.SessionId;
                screen.ObserveScreen(true, true);
                check(screen.Current?.SessionId == session && screens.Count == 1,
                    "remaining on the editor main screen ended the session");
                screen.ObserveScreen(!menuWithStaleEditorFlag, menuWithStaleEditorFlag);
                screen.ObserveNativeUnload();
                check(screen.Current == null && screens.Count == 2 &&
                    screens[1].EndReason == EditorMapEndReason.SceneChanged,
                    "menu/gameplay transition failed to end once, or stale editor flag retained the session");
            }

            foreach (var first in new[] { EditorMapOrigin.Created, EditorMapOrigin.Loaded })
            foreach (var next in new[] { EditorMapOrigin.Created, EditorMapOrigin.Loaded })
            {
                var seen = new List<EditorMapLifecycleNotification>();
                var state = new EditorMapLifecycleState(_ => { });
                check(state.Register("mod", "lifecycle", seen.Add), "editor observer registration failed");
                long a = state.Begin();
                state.Complete(a, first, first == EditorMapOrigin.Loaded ? "same.map" : null, true);
                long b = state.Begin();
                check(state.Current == null, "old editor map must be invalidated before replacement finishes");
                state.Complete(b, next, next == EditorMapOrigin.Loaded ? "same.map" : null, true);
                state.Complete(b, next, "same.map", true);
                check(seen.Count == 3 && seen[0].Kind == EditorMapLifecycleKind.Ready &&
                    seen[1].Kind == EditorMapLifecycleKind.Ended && seen[2].Kind == EditorMapLifecycleKind.Ready &&
                    seen[1].SessionId == a && seen[1].EndReason == EditorMapEndReason.MapReplacement &&
                    b > a && seen[2].Origin == next,
                    $"incorrect editor replacement/deduplication: {first} -> {next}");
                long failed = state.Begin();
                state.Complete(failed, EditorMapOrigin.Loaded, "missing.map", false);
                check(state.Current == null && seen.Count == 4,
                    "a failed load retained the previous map or published Ready");
                long cancelled = state.Begin();
                state.End(EditorMapEndReason.SceneChanged);
                state.Complete(cancelled, EditorMapOrigin.Created, null, true);
                check(state.Current == null && seen.Count == 4, "an obsolete operation resurrected an ended session");
                long good = state.Begin();
                state.Complete(good, EditorMapOrigin.Created, null, true);
                state.End(EditorMapEndReason.NativeUnload);
                state.End(EditorMapEndReason.NativeUnload);
                check(seen.Count == 6 && seen[5].SessionId == good,
                    "duplicate unloads emitted multiple editor ends");
            }

            int errors = 0;
            var replayState = new EditorMapLifecycleState(_ => errors++);
            var live = new List<EditorMapLifecycleNotification>();
            var late = new List<EditorMapLifecycleNotification>();
            replayState.Register("bad", "subscriber", _ => throw new InvalidOperationException("expected"));
            replayState.Register("good", "subscriber", e =>
            {
                live.Add(e);
                if (e.Kind == EditorMapLifecycleKind.Ready && late.Count == 0)
                    replayState.Register("late", "subscriber", late.Add);
            });
            long id = replayState.Begin();
            replayState.Complete(id, EditorMapOrigin.Loaded, "a.map", true);
            check(live.Count == 1 && late.Count == 1 && late[0].IsReplay &&
                late[0].SessionId == id && late[0].FilePath == "a.map" && errors == 1,
                "reentrant late registration missed, duplicated or failed to mark its replay");
            check(!replayState.Register("late", "subscriber", late.Add), "duplicate registration was accepted");
            replayState.End(EditorMapEndReason.SceneChanged);
            check(live.Count == 2 && late.Count == 2 && !late[1].IsReplay && errors == 2,
                "observer exception prevented end notification");
            var afterEnd = new List<EditorMapLifecycleNotification>();
            replayState.Register("after", "end", afterEnd.Add);
            check(afterEnd.Count == 0, "ended session was replayed as active");

            var reentrant = new EditorMapLifecycleState(_ => { });
            var order = new List<string>();
            reentrant.Register("a", "one", e =>
            {
                order.Add("a:" + e.Kind);
                if (e.Kind == EditorMapLifecycleKind.Ready) reentrant.End(EditorMapEndReason.SceneChanged);
            });
            reentrant.Register("b", "two", e => order.Add("b:" + e.Kind));
            reentrant.Complete(reentrant.Begin(), EditorMapOrigin.Created, null, true);
            check(order.SequenceEqual(new[] { "a:Ready", "b:Ready", "a:Ended", "b:Ended" }),
                "reentrant end reordered publications between observers");
        }
    }
}
