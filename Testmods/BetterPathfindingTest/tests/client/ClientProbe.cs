using System;
using System.Diagnostics;
using System.Threading;
using BetterPathfindingTest.Api;

// Compiled independently against the host's public assembly; no implementation sources.
public static class NavigationClientProbe
{
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException("External consumer: " + message); }
    public static void CheckInactive()
    {
        var status = NavigationApi.GetStatus();
        Check(!status.IsReady && status.State == NavigationState.Unsupported, "Inactive build advertised ready.");
        Check(NavigationApi.SubmitBatch(status.Context, new[] { new NavigationRequest(1, 0, 0, 1, 1) }, out _) == NavigationSubmission.Unsupported, "Inactive API submitted work.");
    }
    public static void CheckActive()
    {
        var status = NavigationApi.GetStatus(1); Check(status.IsReady && !status.PerformanceAccepted, "Prepared source is not functionally ready.");
        Check(NavigationApi.PrepareContext(2), "Cannot request second perspective.");
        var clock = Stopwatch.StartNew(); while (!NavigationApi.GetStatus(2).IsReady && clock.ElapsedMilliseconds < 5000) Thread.Sleep(1);
        Check(NavigationApi.GetStatus(2).IsReady, "Second perspective never became ready.");
        var input = new NavigationRequest[32]; for (int i = 0; i < input.Length; i++) input[i] = new NavigationRequest(300-i, i, 0, 63, 15);
        Check(NavigationApi.SubmitBatch(status.Context, input, out var ticket) == NavigationSubmission.Queued, "Public submission failed.");
        input[0] = new NavigationRequest(9999, 0, 0, 0, 0);
        var result = Wait(ticket); Check(result.State == NavigationBatchState.Completed && result.Routes.Count == 32, "Public batch did not complete.");
        Check(result.Routes[0].RequestId == 300 && result.Routes[31].RequestId == 269 && result.Routes[0].State == NavigationRouteState.Found && result.Routes[0].Cost == 63, "Copied request identities/cost changed.");
        Check(result.Routes[0].FirstSteps.Count == 8, "Missing prepared prefix.");
        bool readOnly = false; try { ((System.Collections.Generic.IList<byte>)result.Routes[0].FirstSteps)[0] = 7; } catch (NotSupportedException) { readOnly = true; }
        Check(readOnly, "Result data is externally mutable.");
        Check(NavigationApi.SubmitSlices(new[] { new NavigationSliceRequest(result.Routes[0].Handle, 0, 2000) }, out var sliceTicket) == NavigationSubmission.Queued, "Cannot ask for remaining/full steps.");
        var steps = Wait(sliceTicket); Check(steps.State == NavigationBatchState.Completed && steps.Slices[0].Directions.Count == 63 && !steps.Slices[0].HasMore, "Full public route differs.");
        Check(NavigationApi.Release(sliceTicket) && NavigationApi.Release(ticket), "Public leases were not released.");
        Check(NavigationApi.Poll(ticket).State == NavigationBatchState.Unknown, "Released ticket remains live.");
        Check(NavigationApi.SubmitBatch(status.Context, new[] { new NavigationRequest(1, 0, 0, 1, 1), new NavigationRequest(1, 0, 0, 2, 2) }, out _) == NavigationSubmission.InvalidInput, "Duplicate request IDs accepted.");
    }
    private static NavigationBatchResult Wait(NavigationTicket ticket)
    {
        var clock = Stopwatch.StartNew();
        while (clock.ElapsedMilliseconds < 10000) { var result = NavigationApi.Poll(ticket); if (result.State != NavigationBatchState.Pending) return result; Thread.Sleep(1); }
        throw new TimeoutException("External API request did not finish.");
    }
}
