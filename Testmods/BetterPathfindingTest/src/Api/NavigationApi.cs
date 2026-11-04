using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;

namespace BetterPathfindingTest.Api
{
    // Experimental v1: snapshot-ground queries only. No game command or movement authority.
    public enum NavigationState { NotLoaded, Initializing, Ready, Updating, Unsupported, Faulted }
    public enum NavigationSubmission { Queued, NotReady, Unsupported, InvalidInput, NoCapacity, StaleContext }
    public enum NavigationBatchState { Pending, Completed, Cancelled, Stale, Failed, Unknown }
    public enum NavigationRouteState { Found, Unreachable, Incomplete, InvalidEndpoint }
    public readonly struct NavigationContext : IEquatable<NavigationContext>
    {
        public long Session { get; }
        public long Generation { get; }
        public int PlayerId { get; }
        internal NavigationContext(long session, long generation, int player) { Session = session; Generation = generation; PlayerId = player; }
        public bool Equals(NavigationContext other) => Session == other.Session && Generation == other.Generation && PlayerId == other.PlayerId;
        public override bool Equals(object other) => other is NavigationContext value && Equals(value);
        public override int GetHashCode() => Session.GetHashCode() ^ Generation.GetHashCode() ^ PlayerId;
    }
    public sealed class NavigationStatus
    {
        public NavigationState State { get; }
        public NavigationContext Context { get; }
        public bool IsReady => State == NavigationState.Ready;
        public string Phase { get; }
        public string Reason { get; }
        public double SnapshotAgeMilliseconds { get; }
        public long MemoryBytes { get; }
        public long MemoryLimitBytes { get; }
        public int PendingBatches { get; }
        public bool PerformanceAccepted => false;
        internal NavigationStatus(NavigationState state, NavigationContext context, string phase, string reason, double age, long bytes, long limit, int pending)
        { State = state; Context = context; Phase = phase; Reason = reason; SnapshotAgeMilliseconds = age; MemoryBytes = bytes; MemoryLimitBytes = limit; PendingBatches = pending; }
    }
    public readonly struct NavigationRequest
    {
        public long RequestId { get; }
        public int StartX { get; }
        public int StartY { get; }
        public int TargetX { get; }
        public int TargetY { get; }
        public int MaximumSteps { get; }
        public NavigationRequest(long id, int startX, int startY, int targetX, int targetY, int maximumSteps = 2000)
        { RequestId = id; StartX = startX; StartY = startY; TargetX = targetX; TargetY = targetY; MaximumSteps = maximumSteps; }
    }
    public readonly struct NavigationTicket
    {
        public long Id { get; }
        public long Session { get; }
        internal NavigationTicket(long id, long session) { Id = id; Session = session; }
    }
    public readonly struct NavigationRouteHandle
    {
        public long Id { get; }
        public NavigationContext Context { get; }
        public bool IsEmpty => Id == 0;
        internal NavigationRouteHandle(long id, NavigationContext context) { Id = id; Context = context; }
    }
    public readonly struct NavigationSliceRequest
    {
        public NavigationRouteHandle Route { get; }
        public int Offset { get; }
        public int Count { get; }
        public NavigationSliceRequest(NavigationRouteHandle route, int offset, int count) { Route = route; Offset = offset; Count = count; }
    }
    public sealed class NavigationRoute
    {
        public long RequestId { get; }
        public NavigationRouteState State { get; }
        public long Cost { get; }
        public int StepCount { get; }
        public NavigationRouteHandle Handle { get; }
        public IReadOnlyList<byte> FirstSteps { get; }
        internal NavigationRoute(long id, NavigationRouteState state, long cost, int steps, NavigationRouteHandle handle, byte[] prefix)
        { RequestId = id; State = state; Cost = cost; StepCount = steps; Handle = handle; FirstSteps = Array.AsReadOnly(prefix); }
    }
    public sealed class NavigationSlice
    {
        public NavigationRouteHandle Route { get; }
        public int Offset { get; }
        public IReadOnlyList<byte> Directions { get; }
        public bool HasMore { get; }
        internal NavigationSlice(NavigationRouteHandle route, int offset, byte[] values, bool more) { Route = route; Offset = offset; Directions = Array.AsReadOnly(values); HasMore = more; }
    }
    public sealed class NavigationBatchResult
    {
        public NavigationTicket Ticket { get; }
        public NavigationBatchState State { get; }
        public NavigationContext Context { get; }
        public IReadOnlyList<NavigationRoute> Routes { get; }
        public IReadOnlyList<NavigationSlice> Slices { get; }
        public string Reason { get; }
        public double QueueMilliseconds { get; }
        public double CalculationMilliseconds { get; }
        internal NavigationBatchResult(NavigationTicket ticket, NavigationBatchState state, NavigationContext context, NavigationRoute[] routes = null, NavigationSlice[] slices = null, string reason = "", double queued = 0, double calculated = 0)
        { Ticket = ticket; State = state; Context = context; Routes = Array.AsReadOnly(routes ?? new NavigationRoute[0]); Slices = Array.AsReadOnly(slices ?? new NavigationSlice[0]); Reason = reason; QueueMilliseconds = queued; CalculationMilliseconds = calculated; }
    }
    internal interface INavigationService
    {
        NavigationStatus GetStatus(int player);
        bool PrepareContext(int player);
        NavigationSubmission Submit(NavigationContext context, NavigationRequest[] requests, out NavigationTicket ticket);
        NavigationSubmission SubmitSlices(NavigationSliceRequest[] requests, out NavigationTicket ticket);
        NavigationBatchResult Poll(NavigationTicket ticket);
        bool Cancel(NavigationTicket ticket);
        bool Release(NavigationTicket ticket);
    }
    public static class NavigationApi
    {
        public const int ContractVersion = 1;
        private static INavigationService service;
        internal static void Bind(INavigationService value) => Volatile.Write(ref service, value);
        /// <summary>Readiness is per player and captured generation; it does not certify live movement or speed.</summary>
        public static NavigationStatus GetStatus(int playerId = 0) => Volatile.Read(ref service)?.GetStatus(playerId) ?? new NavigationStatus(NavigationState.Unsupported, default(NavigationContext), "inactive", "Diagnostic service unavailable; use the /devdiag prototype.", -1, 0, 0, 0);
        public static bool PrepareContext(int playerId) => Volatile.Read(ref service)?.PrepareContext(playerId) ?? false;
        public static NavigationSubmission SubmitBatch(NavigationContext context, NavigationRequest[] requests, out NavigationTicket ticket)
        { var current = Volatile.Read(ref service); if (current != null) return current.Submit(context, requests, out ticket); ticket = default(NavigationTicket); return NavigationSubmission.Unsupported; }
        public static NavigationSubmission SubmitSlices(NavigationSliceRequest[] requests, out NavigationTicket ticket)
        { var current = Volatile.Read(ref service); if (current != null) return current.SubmitSlices(requests, out ticket); ticket = default(NavigationTicket); return NavigationSubmission.Unsupported; }
        public static NavigationBatchResult Poll(NavigationTicket ticket) => Volatile.Read(ref service)?.Poll(ticket) ?? new NavigationBatchResult(ticket, NavigationBatchState.Unknown, default(NavigationContext), reason: "Service unavailable.");
        public static bool Cancel(NavigationTicket ticket) => Volatile.Read(ref service)?.Cancel(ticket) ?? false;
        /// <summary>Releases this batch and all its route handles. Caller-held result snapshots remain caller-owned.</summary>
        public static bool Release(NavigationTicket ticket) => Volatile.Read(ref service)?.Release(ticket) ?? false;
    }
}
