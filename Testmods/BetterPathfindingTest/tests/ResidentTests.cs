using System;
using System.Collections.Generic;
using System.IO;
using BetterPathfindingTest.Api;
using BetterPathfindingTest.Core;
using BetterPathfindingTest.Service;

internal static partial class Program
{
    private sealed class ManualNavigationWorker
    {
        internal readonly Queue<Action> Jobs = new Queue<Action>();
        internal void Enqueue(Action action) => Jobs.Enqueue(action);
        internal void Run() { int count = 0; while (Jobs.Count != 0) { if (++count > 10000) throw new InvalidOperationException("Resident worker did not drain."); Jobs.Dequeue()(); } }
    }
    private static OwnedNavigationSnapshot Frame(long session, GridSnapshot graph, double time = 0, Func<int, GridSnapshot> player = null, Action prepare = null, Action persist = null)
        => new OwnedNavigationSnapshot { Session = session, LocalPlayer = 1, CapturedAt = time, Width = graph.Width, Height = graph.Height, Bytes = graph.BufferBytes + 1024,
            Prepare = budget => { budget.Check(); prepare?.Invoke(); }, Persist = persist,
            BuildGround = (id, budget) => { budget.Check(); var value = player == null ? graph : player(id); return new GridSnapshot(value.Width, value.Height, value.CopyValidity(), value.CopyEdges()); } };
    private static void ResidentContracts()
    {
        var queue = new ManualNavigationWorker(); double clock = 0;
        var service = new ResidentNavigation(queue.Enqueue, 256L * 1024 * 1024, () => clock);
        NavigationApi.Bind(service);
        try
        {
            Check(NavigationApi.GetStatus().State == NavigationState.NotLoaded, "Resident has no implicit map.");
            service.Begin(1, 1); queue.Run(); Check(!NavigationApi.GetStatus().IsReady, "Ready before first snapshot.");
            var map = Grid(41, 9); Check(service.Offer(Frame(1, map)), "First resident input."); queue.Run();
            var status = NavigationApi.GetStatus(); Check(status.IsReady && status.Context.Session == 1 && status.Context.Generation != 0 && !status.PerformanceAccepted, "Resident functional readiness is separate from performance.");
            Check(status.MemoryBytes <= status.MemoryLimitBytes, "Resident reservation exceeded cap.");
            var input = new[] { new NavigationRequest(81, 0, 0, 40, 8), new NavigationRequest(7, 40, 8, 0, 0) };
            Check(NavigationApi.SubmitBatch(status.Context, input, out var ticket) == NavigationSubmission.Queued, "External batch rejected.");
            input[0] = new NavigationRequest(999, 0, 0, 0, 0); Check(NavigationApi.Poll(ticket).State == NavigationBatchState.Pending, "Submit blocked until completion."); queue.Run();
            var result = NavigationApi.Poll(ticket); Check(result.State == NavigationBatchState.Completed && result.Routes.Count == 2 && result.Routes[0].RequestId == 81 && result.Routes[1].RequestId == 7, "Batch input copy/identity.");
            Check(result.Routes[0].Cost == 40 && result.Routes[0].FirstSteps.Count == 8, "Ground route or prefix.");
            var handle = result.Routes[0].Handle;
            Check(NavigationApi.SubmitSlices(new[] { new NavigationSliceRequest(handle, 0, 2000) }, out var sliceTicket) == NavigationSubmission.Queued, "Full route request."); queue.Run();
            var slice = NavigationApi.Poll(sliceTicket); Check(slice.State == NavigationBatchState.Completed && slice.Slices[0].Directions.Count == 40 && !slice.Slices[0].HasMore, "Full route reconstruction.");
            var bytes = new byte[40]; for (int i = 0; i < bytes.Length; i++) bytes[i] = slice.Slices[0].Directions[i];
            Check(map.Describe(new RouteQuery { Start = 0, Target = 8 * 41 + 40 }, bytes).Status == SearchStatus.Found, "API route contains illegal edges.");
            Check(NavigationApi.Release(sliceTicket), "Slice release.");

            clock = 2; Check(service.Offer(Frame(1, map, clock)), "Unchanged resident snapshot.");
            Check(NavigationApi.GetStatus().State == NavigationState.Updating, "Pending snapshot falsely ready."); queue.Run();
            var unchanged = NavigationApi.GetStatus(); Check(unchanged.IsReady && unchanged.Context.Equals(status.Context), "Tick/snapshot invalidated an unchanged graph.");
            Check(NavigationApi.Poll(ticket).State == NavigationBatchState.Completed, "Unchanged snapshot invalidated route handle.");
            clock = 3; Check(NavigationApi.GetStatus().SnapshotAgeMilliseconds == 1000, "Snapshot age not exposed.");
            Check(NavigationApi.PrepareContext(2), "Second perspective request."); queue.Run();
            Check(NavigationApi.GetStatus(2).IsReady, "Shared identical perspective not ready.");
            long before = NavigationApi.GetStatus().MemoryBytes;
            for (int i = 0; i < 80; i++)
            {
                Check(NavigationApi.SubmitBatch(status.Context, new[] { new NavigationRequest(i, 0, 0, 40, 8) }, out var repeat) == NavigationSubmission.Queued, "Resident repeated batch queue.");
                queue.Run(); Check(NavigationApi.Poll(repeat).State == NavigationBatchState.Completed, "Resident repeated batch."); NavigationApi.Release(repeat);
            }
            Check(NavigationApi.GetStatus().MemoryBytes - before < 1024 * 1024, "Returned route leases accumulate across resident batches.");
            Check(NavigationApi.Release(ticket) && service.ClientBytes == 0, "Route batch did not release client reservation.");

            Check(NavigationApi.SubmitBatch(status.Context, new[] { new NavigationRequest(3, 0, 0, 40, 8) }, out var deferred) == NavigationSubmission.Queued, "Deferred query setup.");
            service.Offer(Frame(1, map)); queue.Run();
            Check(NavigationApi.Poll(deferred).State == NavigationBatchState.Completed, "An unchanged pending capture cancelled a queued query."); NavigationApi.Release(deferred);
            Check(NavigationApi.SubmitBatch(status.Context, new[] { new NavigationRequest(4, 0, 0, 40, 8) }, out var releasedPending) == NavigationSubmission.Queued, "Pending release setup.");
            NavigationApi.Release(releasedPending); queue.Run(); Check(service.ClientBytes == 0, "Released queued closure deducted its reservation twice.");

            var capacity = new NavigationTicket[32];
            for (int i = 0; i < capacity.Length; i++) Check(NavigationApi.SubmitBatch(status.Context, new[] { new NavigationRequest(i, 0, 0, 40, 8) }, out capacity[i]) == NavigationSubmission.Queued, "Bounded queue filled early.");
            Check(NavigationApi.SubmitBatch(status.Context, new[] { new NavigationRequest(99, 0, 0, 40, 8) }, out _) == NavigationSubmission.NoCapacity, "Queue is unbounded.");
            foreach (var reserved in capacity) NavigationApi.Release(reserved); queue.Run(); Check(service.ClientBytes == 0, "Capacity test retained released inputs.");

            var barrier = Grid(41, 9, (a,b,d) => a % 41 == 20 || b % 41 == 20 ? 0 : 1U << d);
            Check(service.Offer(Frame(1, barrier, clock)), "Blocked snapshot."); queue.Run();
            var blocked = NavigationApi.GetStatus(); Check(blocked.IsReady && !blocked.Context.Equals(status.Context), "Changed topology did not publish a new generation.");
            var player2 = NavigationApi.GetStatus(2).Context;
            service.Offer(Frame(1, barrier, player: id => id == 1 ? map : barrier)); queue.Run();
            Check(NavigationApi.GetStatus(1).IsReady && NavigationApi.GetStatus(2).Context.Equals(player2), "Perspective split invalidated unchanged player.");
            service.Offer(Frame(1, barrier)); queue.Run(); blocked = NavigationApi.GetStatus();
            Check(NavigationApi.SubmitBatch(status.Context, new[] { new NavigationRequest(1, 0, 0, 40, 8) }, out _) == NavigationSubmission.StaleContext, "Stale generation accepted.");
            Check(NavigationApi.SubmitBatch(blocked.Context, new[] { new NavigationRequest(1, 0, 0, 40, 8) }, out var noRoute) == NavigationSubmission.Queued, "Unreachable query rejected instead of answered."); queue.Run();
            Check(NavigationApi.Poll(noRoute).Routes[0].State == NavigationRouteState.Unreachable, "Blocked ground route found."); NavigationApi.Release(noRoute);
            Check(service.Offer(Frame(1, map, clock)), "Reopened snapshot."); queue.Run();
            var reopened = NavigationApi.GetStatus(); Check(reopened.IsReady && !reopened.Context.Equals(blocked.Context), "New shortcut did not update generation.");
            Check(NavigationApi.SubmitBatch(reopened.Context, new[] { new NavigationRequest(2, 0, 0, 40, 8) }, out var cancel) == NavigationSubmission.Queued, "Cancel setup.");
            Check(NavigationApi.Cancel(cancel), "Queued cancellation."); queue.Run(); Check(NavigationApi.Poll(cancel).State == NavigationBatchState.Cancelled, "Cancelled query executed."); NavigationApi.Release(cancel);

            Check(service.Offer(Frame(1, map, clock, persist: () => { throw new IOException("report fixture"); })), "Write failure fixture input."); queue.Run(); Check(NavigationApi.GetStatus().IsReady, "Diagnostic write failure stopped valid navigation.");
            service.Begin(2, 1); service.Offer(Frame(2, map)); service.Begin(3, 1); service.Offer(Frame(3, map)); queue.Run();
            Check(NavigationApi.GetStatus().IsReady && NavigationApi.GetStatus().Context.Session == 3, "Queued capture/map switch published or disposed wrong session.");
            service.End(3); queue.Run(); Check(NavigationApi.GetStatus().State == NavigationState.NotLoaded && service.SourceBytes == 0 && service.ClientBytes == 0, "Session teardown retained data.");

            service.Begin(4, 1); service.Offer(Frame(4, map, prepare: () => { throw new NavigationSourcePendingException("publication fixture"); })); queue.Run();
            Check(!NavigationApi.GetStatus().IsReady, "Missing policy became unrestricted ready.");
            service.Offer(Frame(4, map)); queue.Run(); Check(NavigationApi.GetStatus().IsReady, "Retry after transient policy failure.");
            service.Begin(5, 1); queue.Run(); clock += 300; service.Tick(); Check(NavigationApi.GetStatus().State == NavigationState.Faulted, "Initialization deadline ignored.");
            service.Begin(6, 1); service.Offer(Frame(6, map)); queue.Run(); Check(NavigationApi.GetStatus().IsReady, "New session could not recover after deadline.");
            var faultContext = NavigationApi.GetStatus().Context;
            NavigationApi.SubmitBatch(faultContext, new[] { new NavigationRequest(5, 0, 0, 40, 8) }, out var faultTicket);
            service.Fail("session fixture"); queue.Run();
            Check(NavigationApi.GetStatus().State == NavigationState.Faulted && NavigationApi.Poll(faultTicket).State != NavigationBatchState.Completed && !service.TryBeginCapture(), "Session failure allowed a queued result or capture.");
            NavigationApi.Release(faultTicket); Check(service.SourceBytes == 0 && service.ClientBytes == 0, "Fault cleanup retained snapshot or request.");
            service.End(6); queue.Run();
            service.Begin(7, 1); queue.Run();
            bool readyWhileBehind = true;
            service.Offer(Frame(7, map, player: id => { service.Offer(Frame(7, barrier)); readyWhileBehind = service.GetStatus(1).IsReady; return map; })); queue.Run();
            Check(!readyWhileBehind && service.GetStatus(1).IsReady, "Initialization exposed ready while a newer snapshot was waiting.");
            NavigationApi.SubmitBatch(service.GetStatus(1).Context, new[] { new NavigationRequest(6, 0, 0, 40, 8) }, out var expired);
            clock += 121; queue.Run(); Check(NavigationApi.Poll(expired).State == NavigationBatchState.Failed && NavigationApi.Poll(expired).Reason.Contains("deadline"), "Queue expiry did not report an incomplete job."); NavigationApi.Release(expired);
            service.End(7); queue.Run();
            service.Begin(8, 1); queue.Run();
            service.Offer(Frame(8, map, player: id => { service.Begin(9, 1); service.Offer(Frame(9, barrier)); return map; })); queue.Run();
            Check(service.GetStatus(1).IsReady && service.GetStatus(1).Context.Session == 9, "Map switch during graph construction published the retired session."); service.End(9); queue.Run();
            Check(NavigationMemory.SelectBudget(() => 8UL * 1024 * 1024 * 1024) == 1024L * 1024 * 1024, "Memory maximum.");
            Check(NavigationMemory.SelectBudget(() => 2UL * 1024 * 1024 * 1024) == 512L * 1024 * 1024, "Quarter-free-memory selection.");
            Check(NavigationMemory.SelectBudget(() => { throw new NotSupportedException(); }) == 256L * 1024 * 1024, "Memory query fallback.");
            var low = new ResidentNavigation(queue.Enqueue, 64L * 1024 * 1024, () => clock); low.Begin(1, 1); queue.Run(); Check(low.GetStatus(1).State == NavigationState.Unsupported && !low.PrepareContext(2), "Insufficient RAM incorrectly ready."); low.End(1); queue.Run();
            long selected = 512L * 1024 * 1024; var dynamicMemory = new ResidentNavigation(queue.Enqueue, 256L * 1024 * 1024, () => clock, selectMemory: () => selected);
            dynamicMemory.Begin(1, 1); queue.Run(); Check(dynamicMemory.MaximumBytes == selected, "RAM budget was fixed before map initialization.");
            dynamicMemory.End(1); queue.Run(); selected = 64L * 1024 * 1024; dynamicMemory.Begin(2, 1); queue.Run(); Check(dynamicMemory.GetStatus(1).State == NavigationState.Unsupported, "Next map reused stale physical-memory selection."); dynamicMemory.End(2); queue.Run();
            service.Begin(10); var editor = Frame(10, map); editor.LocalPlayer = 0; service.Offer(editor); queue.Run(); Check(service.GetStatus(0).IsReady && service.GetStatus(0).Context.PlayerId == 1, "Editor with no local player never prepared a diagnostic perspective."); service.End(10); queue.Run();
        }
        finally { NavigationApi.Bind(null); }
        Console.WriteLine("PASS: resident snapshot/readiness, public batch/slice, leases, generations, coalescing, cancellation, recovery and memory contracts.");
    }
}
