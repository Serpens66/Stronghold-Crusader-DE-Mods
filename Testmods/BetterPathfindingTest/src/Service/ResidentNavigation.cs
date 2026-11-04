#if SERP_DEVDIAG
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using BetterPathfindingTest.Api;
using BetterPathfindingTest.Core;

namespace BetterPathfindingTest.Service
{
    // Factories own only copied arrays and immutable policy data, never game pointers.
    internal sealed class OwnedNavigationSnapshot
    {
        internal long Session, Sequence, Bytes;
        internal int LocalPlayer, Width, Height;
        internal double CapturedAt;
        internal Action<WorkBudget> Prepare;
        internal Func<int, WorkBudget, GridSnapshot> BuildGround;
        internal Action Persist;
    }
    internal sealed class NavigationSourcePendingException : Exception
    { internal NavigationSourcePendingException(string message) : base(message) { } }

    internal sealed class ResidentNavigation : INavigationService
    {
        private const long MiB = 1024L * 1024;
        private readonly object sync = new object();
        private readonly Action<Action> enqueue;
        private readonly Func<double> now;
        private readonly Action<string> log;
        internal long MaximumBytes { get; private set; }
        private long sourcePool, clientPool, fixedPools;
        private readonly Func<long> selectMemory;
        private long session, workerSession, sequence, generation, ticketCounter, routeCounter;
        private long sourceBytes, sourceWork, captureReservation, clientBytes, routeBytes, backendBytes, peak;
        private int localPlayer;
        private bool drainQueued, stopped = true;
        private NavigationState baseState = NavigationState.NotLoaded;
        private string baseReason = "No active map.";
        private OwnedNavigationSnapshot pending, current;
        private CancellationTokenSource cancellation = new CancellationTokenSource();
        private readonly Dictionary<int, PlayerContext> contexts = new Dictionary<int, PlayerContext>();
        private readonly List<Group> groups = new List<Group>(); // worker-owned native contexts
        private readonly Dictionary<long, Job> jobs = new Dictionary<long, Job>();
        private readonly Dictionary<long, StoredRoute> routes = new Dictionary<long, StoredRoute>();
        private Job activeJob;
        private double operationDeadline = double.PositiveInfinity;
        private double lastWorkerPulse;
        private double lastPreparationMilliseconds, maximumNativeSlice;
        private long appliedSnapshots, coalescedSnapshots, changedContexts;

        private sealed class PlayerContext
        {
            internal int Player;
            internal long Generation;
            internal NavigationState State = NavigationState.Initializing;
            internal string Phase = "awaiting-snapshot", Reason = "";
            internal double SnapshotTime = -1, Started;
            internal Group Group;
        }
        private sealed class Group
        {
            internal GridSnapshot Map;
            internal IResidentBackend Navigator;
        }
        private sealed class Job
        {
            internal NavigationTicket Ticket;
            internal NavigationContext Context;
            internal NavigationRequest[] Requests;
            internal NavigationSliceRequest[] Slices;
            internal NavigationBatchResult Result;
            internal long InputCharge, RouteCharge;
            internal double Submitted;
            internal bool Cancelled, Running, ReleaseRequested;
            internal readonly List<long> Handles = new List<long>();
        }
        private sealed class StoredRoute
        { internal NavigationRouteHandle Handle; internal byte[] Directions; internal long Ticket; }

        internal ResidentNavigation(Action<Action> enqueue, long maximumBytes, Func<double> seconds = null, Action<string> log = null, Func<long> selectMemory = null)
        {
            this.enqueue = enqueue ?? throw new ArgumentNullException(nameof(enqueue));
            now = seconds ?? (() => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency);
            this.log = log ?? (_ => { }); this.selectMemory = selectMemory; ConfigureBudget(maximumBytes);
            if (maximumBytes < 256 * MiB) { baseState = NavigationState.Unsupported; baseReason = "Insufficient available physical memory for a 256 MiB navigation budget."; }
        }
        private void ConfigureBudget(long maximumBytes)
        {
            MaximumBytes = Math.Max(0, Math.Min(1024 * MiB, maximumBytes)); maximumBytes = MaximumBytes;
            sourcePool = Math.Min(96 * MiB, maximumBytes / 4);
            clientPool = Math.Min(64 * MiB, maximumBytes / 8);
            fixedPools = sourcePool + clientPool + 4 * MiB;
        }
        internal void Begin(long id, int defaultPlayer = 0)
        {
            lock (sync)
            {
                if (id <= session) return;
                if (selectMemory != null) { long selected; try { selected = selectMemory(); } catch { selected = 256 * MiB; } ConfigureBudget(selected); }
                cancellation.Cancel(); cancellation = new CancellationTokenSource();
                session = id; localPlayer = defaultPlayer; sequence = 0; stopped = false;
                appliedSnapshots = coalescedSnapshots = changedContexts = 0; lastPreparationMilliseconds = maximumNativeSlice = 0;
                if (pending != null) { sourceBytes -= pending.Bytes; pending = null; }
                contexts.Clear(); baseState = MaximumBytes < 256 * MiB ? NavigationState.Unsupported : NavigationState.Initializing;
                baseReason = baseState == NavigationState.Unsupported ? "Insufficient navigation memory budget." : "Awaiting first coherent snapshot.";
                if (defaultPlayer >= 1 && defaultPlayer <= 8) contexts[defaultPlayer] = new PlayerContext { Player = defaultPlayer, Started = now(), State = baseState, Reason = baseReason };
                foreach (Job job in jobs.Values) job.Cancelled = true;
            }
            enqueue(() => ResetWorker(id));
        }
        internal void End(long id)
        {
            lock (sync)
            {
                if (id != session) return;
                stopped = true; cancellation.Cancel(); contexts.Clear(); baseState = NavigationState.NotLoaded; baseReason = "Map ended.";
                if (pending != null) { sourceBytes -= pending.Bytes; pending = null; }
                foreach (Job job in jobs.Values) job.Cancelled = true;
            }
            enqueue(() => ResetWorker(id));
        }
        private void ResetWorker(long id)
        {
            lock (sync) { if (session != id) return; }
            foreach (Group group in groups) group.Navigator.Dispose(); groups.Clear();
            lock (sync)
            {
                if (current != null) { sourceBytes -= current.Bytes; current = null; }
                jobs.Clear(); routes.Clear(); clientBytes = routeBytes = backendBytes = 0;
                workerSession = id;
                UpdatePeak();
            }
        }
        internal void Fail(string reason, bool unsupported = false)
        {
            long id;
            lock (sync)
            {
                id = session; cancellation.Cancel();
                baseState = unsupported ? NavigationState.Unsupported : NavigationState.Faulted; baseReason = reason;
                foreach (PlayerContext context in contexts.Values) { context.State = baseState; context.Reason = reason; context.Phase = "failed"; }
                if (pending != null) { sourceBytes -= pending.Bytes; pending = null; }
                foreach (Job job in jobs.Values) { job.Cancelled = true; if (!job.Running) Finish(job, NavigationBatchState.Failed, reason); }
            }
            enqueue(() => {
                lock (sync) if (session != id || baseState != NavigationState.Faulted && baseState != NavigationState.Unsupported) return;
                foreach (Group group in groups) group.Navigator.Dispose(); groups.Clear();
                lock (sync) { if (current != null) { sourceBytes -= current.Bytes; current = null; } backendBytes = 0; }
            });
            log("phase=resident-failed session=" + id + " reason=" + reason);
        }
        internal void Tick()
        {
            lock (sync)
                foreach (PlayerContext context in contexts.Values)
                    if (context.Group == null && context.State == NavigationState.Initializing && now() - context.Started >= 300)
                    { context.State = NavigationState.Faulted; context.Phase = "initialization-timeout"; context.Reason = "Initialization exceeded 300 seconds."; }
        }
        internal bool TryBeginCapture(long maximum = 16 * MiB)
        {
            lock (sync)
            {
                if (stopped || baseState == NavigationState.Unsupported || baseState == NavigationState.Faulted || captureReservation != 0 || maximum < 0 || maximum > sourcePool - sourceBytes - sourceWork) return false;
                captureReservation = maximum; sourceWork += maximum; return true;
            }
        }
        internal void EndCapture()
        { lock (sync) { sourceWork -= captureReservation; captureReservation = 0; } }
        internal bool Offer(OwnedNavigationSnapshot snapshot)
        {
            bool schedule = false;
            lock (sync)
            {
                if (stopped || snapshot.Session != session || baseState == NavigationState.Unsupported || baseState == NavigationState.Faulted || snapshot.Width < 1 || snapshot.Height < 1 || (long)snapshot.Width * snapshot.Height > 640000 || snapshot.BuildGround == null || snapshot.Bytes < 0 || snapshot.Bytes > sourcePool) return false;
                long previous = pending?.Bytes ?? 0;
                if (snapshot.Bytes < 0 || sourceBytes - previous + snapshot.Bytes + sourceWork - captureReservation > sourcePool) return false;
                sourceWork -= captureReservation; captureReservation = 0;
                if (pending != null) coalescedSnapshots++;
                sourceBytes += snapshot.Bytes - previous; pending = snapshot; snapshot.Sequence = ++sequence;
                if (snapshot.LocalPlayer >= 1 && snapshot.LocalPlayer <= 8) localPlayer = snapshot.LocalPlayer;
                // The editor may have no local simulation player. Prepare a named
                // diagnostic perspective instead of remaining Initializing forever.
                if (localPlayer < 1 || localPlayer > 8) localPlayer = 1;
                if (localPlayer >= 1 && localPlayer <= 8 && !contexts.ContainsKey(localPlayer)) contexts[localPlayer] = new PlayerContext { Player = localPlayer, Started = now() };
                foreach (PlayerContext context in contexts.Values)
                    if (context.State != NavigationState.Unsupported && context.State != NavigationState.Faulted) { if (context.State == NavigationState.Ready) context.Started = now(); context.State = context.Group == null ? NavigationState.Initializing : NavigationState.Updating; context.Phase = "snapshot-pending"; }
                if (!drainQueued) { drainQueued = true; schedule = true; }
                UpdatePeak();
            }
            if (schedule) enqueue(ApplyLatest);
            return true;
        }
        private void ApplyLatest()
        {
            OwnedNavigationSnapshot frame; CancellationToken token;
            lock (sync)
            {
                if (workerSession != session) { enqueue(ApplyLatest); return; }
                frame = pending; pending = null; drainQueued = false; token = cancellation.Token;
            }
            if (frame == null) return;
            bool retained = false;
            try
            {
                CheckSession(frame.Session, token); double seconds = 120;
                lock (sync) foreach (var context in contexts.Values) if (context.Group == null) { seconds = 300; break; }
                operationDeadline = now() + seconds;
                var budget = new WorkBudget(sourcePool, seconds, token); budget.Checkpoint = CheckOperation;
                frame.Prepare?.Invoke(budget); CheckSession(frame.Session, token);
                try { frame.Persist?.Invoke(); } catch (Exception ex) { log("phase=report-failed session=" + frame.Session + " error=" + ex.Message); }
                lock (sync)
                {
                    CheckSession(frame.Session, token);
                    if (current != null) sourceBytes -= current.Bytes;
                    current = frame; retained = true; baseState = NavigationState.Ready; baseReason = "";
                }
                PrepareRequested(frame, token);
                lock (sync) appliedSnapshots++;
            }
            catch (NavigationSourcePendingException ex)
            { lock (sync) foreach (PlayerContext context in contexts.Values) { context.State = context.Group == null ? NavigationState.Initializing : NavigationState.Updating; context.Phase = "waiting-policy"; context.Reason = ex.Message; } }
            catch (OperationCanceledException) { }
            catch (Exception ex) { if (frame.Session == session) Fail(ex.Message); }
            finally
            {
                operationDeadline = double.PositiveInfinity;
                if (!retained) lock (sync) sourceBytes -= frame.Bytes;
                RefreshMemory();
            }
        }
        private void PrepareRequested(OwnedNavigationSnapshot frame, CancellationToken token, bool onlyNew = false)
        {
            PlayerContext[] requested;
            lock (sync) { requested = new PlayerContext[contexts.Count]; contexts.Values.CopyTo(requested, 0); }
            foreach (PlayerContext context in requested)
            {
                var preparationWatch = Stopwatch.StartNew();
                CheckSession(frame.Session, token);
                lock (sync) if (context.State == NavigationState.Faulted || context.State == NavigationState.Unsupported || onlyNew && context.Group != null) continue;
                double previousDeadline = operationDeadline; operationDeadline = Math.Min(operationDeadline, context.Group == null ? context.Started + 300 : now() + 120);
                long temporary = (long)frame.Width * frame.Height * 30 + 4 * MiB;
                bool reserved = false;
                try
                {
                    lock (sync)
                    {
                        if (sourceBytes + sourceWork + temporary > sourcePool) throw new SearchBudgetException("snapshot-work-pool");
                        sourceWork += temporary; reserved = true; context.Phase = "building-ground-graph";
                    }
                    var buildBudget = new WorkBudget(sourcePool, 300, token); buildBudget.Checkpoint = CheckOperation;
                    GridSnapshot graph = frame.BuildGround(context.Player, buildBudget);
                    Group match = null;
                    foreach (Group group in groups) if (Equal(group.Map, graph)) { match = group; break; }
                    bool changed = context.Group != match;
                    long changedCoordinates = 0;
                    if (match == null)
                    {
                        Group old = context.Group;
                        if (old != null && Members(old) == 1)
                        {
                            old.Navigator.BeginOperation(Allowance(old));
                            old.Navigator.Update(graph); changedCoordinates = old.Navigator.ChangedNodes; old.Map = graph; match = old; changed = changedCoordinates != 0;
                        }
                        else
                        {
                            var work = new WorkBudget(Allowance(null), double.PositiveInfinity, token); work.Checkpoint = CheckOperation;
                            match = new Group { Map = graph, Navigator = new AreaResidentBackend(graph, work) }; groups.Add(match);
                            changedCoordinates = graph.Count;
                        }
                    }
                    lock (sync)
                    {
                        CheckSession(frame.Session, token);
                        if (changed || context.Generation == 0) { Invalidate(context.Player); context.Generation = ++generation; changedContexts++; }
                        context.Group = match; context.SnapshotTime = frame.CapturedAt; context.Reason = "";
                        bool caughtUp = pending == null && sequence == frame.Sequence;
                        context.State = caughtUp ? NavigationState.Ready : NavigationState.Updating;
                        context.Phase = caughtUp ? "ready" : "catching-up";
                    }
                    if (changed) log("phase=resident-context session=" + frame.Session + " player=" + context.Player + " generation=" + context.Generation + " state=" + context.State + " changed=" + changedCoordinates + " backend=area-ground performanceAccepted=false");
                    RemoveUnusedGroups();
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                { lock (sync) { context.State = ex is SearchBudgetException ? NavigationState.Unsupported : NavigationState.Faulted; context.Phase = "context-failed"; context.Reason = ex.Message; } log("phase=context-failed player=" + context.Player + " error=" + ex.Message); }
                finally { operationDeadline = previousDeadline; lock (sync) { if (reserved) sourceWork -= temporary; lastPreparationMilliseconds = preparationWatch.Elapsed.TotalMilliseconds; } RefreshMemory(); }
            }
        }
        private bool Equal(GridSnapshot a, GridSnapshot b)
        {
            if (a.Width != b.Width || a.Height != b.Height) return false;
            for (int i = 0; i < a.Count; i++) { if ((i & 4095) == 0) CheckOperation(); if (a.IsValid(i) != b.IsValid(i) || a.Word(i) != b.Word(i)) return false; }
            return true;
        }
        private int Members(Group group) { lock (sync) { int count = 0; foreach (PlayerContext context in contexts.Values) if (context.Group == group) count++; return count; } }
        private void RemoveUnusedGroups()
        { for (int i = groups.Count - 1; i >= 0; i--) if (Members(groups[i]) == 0) { groups[i].Navigator.Dispose(); groups.RemoveAt(i); } }
        private void CheckSession(long id, CancellationToken token) { token.ThrowIfCancellationRequested(); if (session != id || stopped) throw new OperationCanceledException(); }
        private void CheckOperation()
        {
            if (now() >= operationDeadline) throw new SearchBudgetException("operation-time-budget");
            lock (sync) if (activeJob != null && (activeJob.Cancelled || activeJob.ReleaseRequested || activeJob.Ticket.Session != session)) throw new OperationCanceledException();
            double seconds = now(); if (seconds - lastWorkerPulse >= 5) { lastWorkerPulse = seconds; log("phase=resident-worker session=" + workerSession + " ticket=" + (activeJob?.Ticket.Id ?? 0) + " elapsedClock=" + seconds.ToString("F3")); }
        }
        private long Allowance(Group own)
        {
            long other = 0; foreach (Group group in groups) if (group != own) other += group.Navigator.Budget.Bytes;
            lock (sync) return Math.Max(0, MaximumBytes - fixedPools - routeBytes - other);
        }
        private void RefreshMemory()
        {
            long bytes = 0, temporaryPeak = 0;
            foreach (Group group in groups) { bytes += group.Navigator.Budget.Bytes; temporaryPeak = Math.Max(temporaryPeak, group.Navigator.Budget.PeakBytes - group.Navigator.Budget.Bytes); }
            lock (sync) { backendBytes = bytes; foreach (Group group in groups) maximumNativeSlice = Math.Max(maximumNativeSlice, group.Navigator.MaximumSliceMilliseconds); UpdatePeak(); peak = Math.Max(peak, fixedPools + routeBytes + bytes + temporaryPeak); }
        }
        private void UpdatePeak() { peak = Math.Max(peak, fixedPools + backendBytes + routeBytes); }
        internal long PeakBytes { get { lock (sync) return peak; } }
        internal long SourceBytes { get { lock (sync) return sourceBytes; } }
        internal long ClientBytes { get { lock (sync) return clientBytes; } }
        internal Dictionary<string, object> Metrics()
        {
            lock (sync) return new Dictionary<string, object> { ["appliedSnapshots"] = appliedSnapshots, ["coalescedSnapshots"] = coalescedSnapshots, ["changedContexts"] = changedContexts,
                ["lastContextPreparationMs"] = lastPreparationMilliseconds, ["maximumNativeQuerySliceMs"] = maximumNativeSlice, ["nativeQuerySliceTargetMs"] = 2,
                ["sourceOwnedBytes"] = sourceBytes, ["sourceWorkBytes"] = sourceWork, ["clientOwnedBytes"] = clientBytes, ["routeLeaseBytes"] = routeBytes,
                ["memoryPeakBytes"] = peak, ["maximumBytes"] = MaximumBytes, ["updateMode"] = "full changed-context replacement; unchanged graphs retain fields" };
        }
        // Worker-only diagnostic input generation uses the resident owned graph.
        // It does not retain an otherwise superseded raw game snapshot.
        internal NavigationRequest[] ProbeInputs(int player, UnitSeed[] seeds, int count, out GridSnapshot map)
        {
            map = null;
            lock (sync) { if (!contexts.TryGetValue(player, out PlayerContext context) || context.Group == null) return new NavigationRequest[0]; map = context.Group.Map; }
            var own = new List<UnitSeed>(); foreach (UnitSeed seed in seeds) if (seed.Player == player) own.Add(new UnitSeed { Player = map.Player, Node = seed.Node });
            var input = Workloads.Create(map, own.ToArray(), count, "shared-goal"); var requests = new NavigationRequest[input.Count];
            for (int i = 0; i < input.Count; i++) requests[i] = new NavigationRequest(i, input[i].Start % map.Width, input[i].Start / map.Width, input[i].Target % map.Width, input[i].Target / map.Width);
            return requests;
        }
        public NavigationStatus GetStatus(int player)
        {
            lock (sync)
            {
                if (player == 0) player = localPlayer;
                if (contexts.TryGetValue(player, out PlayerContext context))
                    return new NavigationStatus(context.State, new NavigationContext(session, context.Generation, player), context.Phase, context.Reason, context.SnapshotTime < 0 ? -1 : Math.Max(0, now() - context.SnapshotTime) * 1000, fixedPools + backendBytes + routeBytes, MaximumBytes, PendingCount());
                return new NavigationStatus(stopped ? NavigationState.NotLoaded : baseState == NavigationState.Ready ? NavigationState.Initializing : baseState, new NavigationContext(session, 0, player), "awaiting-context", baseReason, -1, stopped ? 0 : fixedPools + backendBytes + routeBytes, MaximumBytes, PendingCount());
            }
        }
        private int PendingCount() { int count = 0; foreach (Job job in jobs.Values) if (job.Result == null) count++; return count; }
        public bool PrepareContext(int player)
        {
            long id;
            lock (sync)
            {
                if (player < 1 || player > 8 || stopped || baseState == NavigationState.Unsupported || baseState == NavigationState.Faulted) return false;
                if (contexts.ContainsKey(player)) return true;
                contexts[player] = new PlayerContext { Player = player, Started = now() }; id = session;
            }
            enqueue(() => { if (session == id && workerSession == id && current != null) { operationDeadline = now() + 300; try { PrepareRequested(current, cancellation.Token, true); } catch (OperationCanceledException) { } finally { operationDeadline = double.PositiveInfinity; } } });
            return true;
        }
        private NavigationSubmission CheckContext(NavigationContext context, out PlayerContext player)
        {
            player = null;
            if (stopped || context.Session != session) return NavigationSubmission.StaleContext;
            if (!contexts.TryGetValue(context.PlayerId, out player)) return NavigationSubmission.NotReady;
            if (player.State == NavigationState.Unsupported) return NavigationSubmission.Unsupported;
            if (player.Generation != context.Generation) return NavigationSubmission.StaleContext;
            return player.State == NavigationState.Ready ? NavigationSubmission.Queued : NavigationSubmission.NotReady;
        }
        public NavigationSubmission Submit(NavigationContext context, NavigationRequest[] requests, out NavigationTicket ticket)
        {
            ticket = default(NavigationTicket); if (requests == null || requests.Length == 0 || requests.Length > 10000) return NavigationSubmission.InvalidInput;
            Job job;
            lock (sync)
            {
                var readiness = CheckContext(context, out PlayerContext player); if (readiness != NavigationSubmission.Queued) return readiness;
                var ids = new HashSet<long>();
                foreach (NavigationRequest request in requests)
                    if (!ids.Add(request.RequestId) || request.MaximumSteps < 0 || request.MaximumSteps > 2000 || request.StartX < 0 || request.StartY < 0 || request.TargetX < 0 || request.TargetY < 0 || request.StartX >= player.Group.Map.Width || request.TargetX >= player.Group.Map.Width || request.StartY >= player.Group.Map.Height || request.TargetY >= player.Group.Map.Height) return NavigationSubmission.InvalidInput;
                long charge = requests.Length * 768L;
                if (clientBytes + charge > clientPool || PendingCount() >= 32) return NavigationSubmission.NoCapacity;
                ticket = new NavigationTicket(++ticketCounter, session); job = new Job { Ticket = ticket, Context = context, Requests = (NavigationRequest[])requests.Clone(), Submitted = now(), InputCharge = charge };
                clientBytes += charge; jobs.Add(ticket.Id, job);
            }
            enqueue(() => Execute(job)); return NavigationSubmission.Queued;
        }
        public NavigationSubmission SubmitSlices(NavigationSliceRequest[] requests, out NavigationTicket ticket)
        {
            ticket = default(NavigationTicket); if (requests == null || requests.Length == 0 || requests.Length > 10000) return NavigationSubmission.InvalidInput;
            Job job; long charge = requests.Length * 256L;
            lock (sync)
            {
                NavigationContext context = requests[0].Route.Context; var readiness = CheckContext(context, out _); if (readiness != NavigationSubmission.Queued) return readiness;
                foreach (NavigationSliceRequest request in requests)
                {
                    if (!context.Equals(request.Route.Context) || request.Offset < 0 || request.Count < 0 || request.Count > 2000 || !routes.TryGetValue(request.Route.Id, out StoredRoute route) || !route.Handle.Context.Equals(context)) return NavigationSubmission.InvalidInput;
                    if (request.Offset > route.Directions.Length) return NavigationSubmission.InvalidInput;
                    charge += Math.Min(request.Count, route.Directions.Length - request.Offset);
                }
                if (clientBytes + charge > clientPool || PendingCount() >= 32) return NavigationSubmission.NoCapacity;
                ticket = new NavigationTicket(++ticketCounter, session); job = new Job { Ticket = ticket, Context = context, Slices = (NavigationSliceRequest[])requests.Clone(), InputCharge = charge, Submitted = now() };
                clientBytes += charge; jobs.Add(ticket.Id, job);
            }
            enqueue(() => Execute(job)); return NavigationSubmission.Queued;
        }
        private void Execute(Job job)
        {
            PlayerContext player = null; double started = now();
            lock (sync)
            {
                if (!jobs.TryGetValue(job.Ticket.Id, out Job stored) || !ReferenceEquals(stored, job)) return;
                if (!job.Cancelled && !job.ReleaseRequested && CurrentGeneration(job.Context) && (pending != null || drainQueued) && now() - job.Submitted < 120)
                { enqueue(() => Execute(job)); return; }
                if (now() - job.Submitted >= 120) { Finish(job, NavigationBatchState.Failed, "Queue deadline exceeded 120 seconds."); return; }
                if (job.Cancelled || job.ReleaseRequested || CheckContext(job.Context, out player) != NavigationSubmission.Queued)
                { Finish(job, job.Context.Session != session || stopped || !CurrentGeneration(job.Context) ? NavigationBatchState.Stale : NavigationBatchState.Cancelled, "Context or request no longer available."); return; }
                job.Running = true; activeJob = job; operationDeadline = now() + 120;
            }
            try
            {
                if (job.Requests != null) ExecuteRoutes(job, player, started);
                else ExecuteSlices(job, started);
            }
            catch (OperationCanceledException) { lock (sync) Finish(job, job.Ticket.Session == session && !stopped ? NavigationBatchState.Cancelled : NavigationBatchState.Stale, "Cancelled."); }
            catch (Exception ex) { lock (sync) Finish(job, NavigationBatchState.Failed, ex.Message); }
            finally
            {
                lock (sync) { job.Running = false; activeJob = null; operationDeadline = double.PositiveInfinity; if (job.ReleaseRequested) Free(job); }
                RefreshMemory();
            }
        }
        private void ExecuteRoutes(Job job, PlayerContext player, double started)
        {
            Group group = player.Group; long operationBase;
            lock (sync) operationBase = fixedPools + routeBytes + backendBytes - group.Navigator.Budget.Bytes;
            group.Navigator.BeginOperation(Allowance(group));
            var queries = new List<RouteQuery>(); var indices = new List<int>(); var output = new NavigationRoute[job.Requests.Length];
            for (int i = 0; i < job.Requests.Length; i++)
            {
                NavigationRequest request = job.Requests[i]; int start = request.StartY * group.Map.Width + request.StartX, target = request.TargetY * group.Map.Width + request.TargetX;
                if (!group.Map.IsValid(start) || !group.Map.IsValid(target)) { output[i] = new NavigationRoute(request.RequestId, NavigationRouteState.InvalidEndpoint, 0, 0, default(NavigationRouteHandle), new byte[0]); continue; }
                queries.Add(new RouteQuery { Start = start, Target = target, MaximumEdges = request.MaximumSteps, GroundCost = 1, MoatCost = 1 }); indices.Add(i);
            }
            RouteResult[] found = queries.Count == 0 ? new RouteResult[0] : group.Navigator.FindPaths(queries);
            lock (sync) peak = Math.Max(peak, operationBase + group.Navigator.Budget.PeakBytes);
            long transferred = group.Navigator.TransferReturnedBuffers(found);
            lock (sync) { routeBytes += transferred; job.RouteCharge += transferred; }
            for (int i = 0; i < found.Length; i++)
            {
                CheckOperation(); RouteResult route = found[i]; int index = indices[i];
                if (route.Status == SearchStatus.Found)
                {
                    RouteResult described = group.Map.Describe(queries[i], route.Directions);
                    if (described.Status != SearchStatus.Found || described.Cost != route.Cost) throw new InvalidOperationException("Backend emitted an invalid ground route.");
                    var prefix = new byte[Math.Min(8, route.Directions.Length)]; Array.Copy(route.Directions, prefix, prefix.Length);
                    lock (sync)
                    {
                        var handle = new NavigationRouteHandle(++routeCounter, job.Context);
                        routes.Add(handle.Id, new StoredRoute { Handle = handle, Directions = route.Directions, Ticket = job.Ticket.Id }); job.Handles.Add(handle.Id);
                        output[index] = new NavigationRoute(job.Requests[index].RequestId, NavigationRouteState.Found, route.Cost, route.Ground, handle, prefix);
                    }
                }
                else output[index] = new NavigationRoute(job.Requests[index].RequestId, route.Status == SearchStatus.Unreachable ? NavigationRouteState.Unreachable : NavigationRouteState.Incomplete, 0, 0, default(NavigationRouteHandle), new byte[0]);
            }
            lock (sync)
            {
                if (!CurrentGeneration(job.Context) || job.Cancelled) { Finish(job, NavigationBatchState.Stale, "Generation replaced."); return; }
                job.Result = new NavigationBatchResult(job.Ticket, NavigationBatchState.Completed, job.Context, output, queued: (started - job.Submitted) * 1000, calculated: (now() - started) * 1000);
                job.Requests = null;
            }
            log("phase=api-batch session=" + job.Ticket.Session + " ticket=" + job.Ticket.Id + " routes=" + output.Length + " calculationMs=" + ((now() - started) * 1000).ToString("F3") + " performanceAccepted=false");
        }
        private void ExecuteSlices(Job job, double started)
        {
            var output = new NavigationSlice[job.Slices.Length];
            for (int i = 0; i < output.Length; i++)
            {
                CheckOperation(); var request = job.Slices[i]; StoredRoute route;
                lock (sync) if (!routes.TryGetValue(request.Route.Id, out route)) throw new OperationCanceledException();
                int length = Math.Min(request.Count, route.Directions.Length - request.Offset); var copy = new byte[length]; Array.Copy(route.Directions, request.Offset, copy, 0, length);
                output[i] = new NavigationSlice(request.Route, request.Offset, copy, request.Offset + length < route.Directions.Length);
            }
            lock (sync) { CheckOperation(); if (!CurrentGeneration(job.Context)) throw new OperationCanceledException(); job.Result = new NavigationBatchResult(job.Ticket, NavigationBatchState.Completed, job.Context, slices: output, queued: (started - job.Submitted) * 1000, calculated: (now() - started) * 1000); job.Slices = null; }
        }
        private bool CurrentGeneration(NavigationContext context) => !stopped && context.Session == session && contexts.TryGetValue(context.PlayerId, out PlayerContext player) && player.Generation == context.Generation && player.State != NavigationState.Faulted && player.State != NavigationState.Unsupported;
        private void Finish(Job job, NavigationBatchState state, string reason)
        {
            if (!jobs.ContainsKey(job.Ticket.Id)) return;
            DropRoutes(job); job.Requests = null; job.Slices = null;
            long retained = Math.Min(job.InputCharge, 1024); clientBytes -= job.InputCharge - retained; job.InputCharge = retained;
            job.Result = new NavigationBatchResult(job.Ticket, state, job.Context, reason: reason); if (job.ReleaseRequested) Free(job);
        }
        private void DropRoutes(Job job) { foreach (long handle in job.Handles) routes.Remove(handle); job.Handles.Clear(); job.Handles.Capacity = 0; routeBytes -= job.RouteCharge; job.RouteCharge = 0; }
        private void Free(Job job) { DropRoutes(job); if (jobs.Remove(job.Ticket.Id)) clientBytes -= job.InputCharge; job.InputCharge = 0; job.Requests = null; job.Slices = null; }
        private void Invalidate(int player)
        { foreach (Job job in jobs.Values) if (job.Context.PlayerId == player && !job.Running) Finish(job, NavigationBatchState.Stale, "Navigation generation replaced."); }
        public NavigationBatchResult Poll(NavigationTicket ticket)
        {
            lock (sync)
            {
                if (ticket.Session != session || stopped) return new NavigationBatchResult(ticket, NavigationBatchState.Stale, default(NavigationContext), reason: "Map replaced or ended.");
                if (!jobs.TryGetValue(ticket.Id, out Job job)) return new NavigationBatchResult(ticket, NavigationBatchState.Unknown, default(NavigationContext), reason: "Unknown or released ticket.");
                return job.Result ?? new NavigationBatchResult(ticket, NavigationBatchState.Pending, job.Context);
            }
        }
        public bool Cancel(NavigationTicket ticket)
        { lock (sync) { if (!jobs.TryGetValue(ticket.Id, out Job job) || job.Ticket.Session != ticket.Session) return false; job.Cancelled = true; if (!job.Running) Finish(job, NavigationBatchState.Cancelled, "Cancelled by caller."); return true; } }
        public bool Release(NavigationTicket ticket)
        { lock (sync) { if (!jobs.TryGetValue(ticket.Id, out Job job) || job.Ticket.Session != ticket.Session) return false; job.ReleaseRequested = job.Cancelled = true; if (!job.Running) Free(job); return true; } }
    }
}

#endif
