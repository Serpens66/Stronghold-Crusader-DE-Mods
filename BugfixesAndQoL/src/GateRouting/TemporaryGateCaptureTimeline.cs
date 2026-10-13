// TEMP_GATE_MIGRATION_DIAGNOSTICS: retained for old/new acceptance; see removal inventory.
// TEMP_GATE_ROUTE_ACCEPTANCE: latest state per gate identity, never a unit/order history.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
namespace BugfixesAndQoL.GateRouting
{
    internal sealed class TemporaryGateCaptureTimeline
    {
        private sealed class State
        {
            internal uint Global;
            internal int Owner, Capturer, RequestedCapturer, RequestedOwner;
            internal long PublishedAt, EventAt, PostEventAt;
            internal bool Pending;
        }
        private readonly Dictionary<int, State> gates = new Dictionary<int, State>();
        private RouteTilePolicySnapshot published;
        internal long Generation { get; private set; }
        internal long Epoch { get; private set; }
        private static readonly DateTime utcAnchor = DateTime.UtcNow;
        private static readonly long monoAnchor = Stopwatch.GetTimestamp();
        internal static string Time(long ticks) => "mono=" + ticks + ",observedUtc=" +
            utcAnchor.AddSeconds((ticks - monoAnchor) / (double)Stopwatch.Frequency).ToString("O", CultureInfo.InvariantCulture);
        internal void Reset(long epoch) { gates.Clear(); published = null; Generation = 0; Epoch = epoch; }
        internal uint ObserveCapture(int building, int player, long at, bool post = false)
        {
            if (!gates.TryGetValue(building, out State state)) return 0;
            // Extender Post is a delayed notification, not capture success or a new transition.
            if (post)
            {
                if (state.EventAt == 0 || state.RequestedCapturer != player) return 0;
                state.PostEventAt = at;
                return state.Global;
            }
            state.Pending = true; state.EventAt = at; state.PostEventAt = 0;
            state.RequestedCapturer = player; state.RequestedOwner = state.Owner;
            return state.Global;
        }
        internal void Publish(RouteTilePolicySnapshot snapshot, long generation, long at)
        {
            published = snapshot; Generation = generation;
            if (snapshot == null || snapshot == RouteTilePolicySnapshot.Empty) return;
            var retained = new HashSet<int>();
            foreach (var pair in snapshot.GateIdentities)
            {
                retained.Add(pair.Key);
                var identity = pair.Value;
                if (!gates.TryGetValue(pair.Key, out State state) || state.Global != identity.Global)
                    gates[pair.Key] = state = new State { Global = identity.Global };
                state.Owner = identity.Owner; state.Capturer = identity.Capturer; state.PublishedAt = at;
                // A post event or an equivalent publication is not capture confirmation by itself.
                if (state.Pending && state.Global != 0 && state.RequestedOwner == identity.Owner &&
                    state.RequestedCapturer == identity.Capturer && at >= state.EventAt)
                    state.Pending = false;
            }
            var removed = new List<int>();
            foreach (int id in gates.Keys) if (!retained.Contains(id)) removed.Add(id);
            foreach (int id in removed) gates.Remove(id);
        }
        internal string Phase(RouteTilePolicySnapshot entry, int gate, long started, long ended)
        {
            if (entry == null || !entry.GateIdentities.TryGetValue(gate, out var identity) ||
                !gates.TryGetValue(gate, out State state) || identity.Global == 0 || state.Global != identity.Global)
                return "not-attributed";
            if (!ReferenceEquals(entry, published) || state.Pending || identity.Owner != state.Owner ||
                identity.Capturer != state.Capturer || (state.EventAt >= started && state.EventAt <= ended) ||
                (state.PublishedAt > started && state.PublishedAt <= ended)) return "transition";
            if (identity.Capturer == 0) return "before-capture";
            return state.PublishedAt <= started ? "after-confirmed-publication" : "not-attributed";
        }
        internal string Describe(int gate) => gates.TryGetValue(gate, out State s)
            ? "captureEventMono=" + s.EventAt + ",capturePostEventMono=" + s.PostEventAt + ",confirmedPublicationMono=" + s.PublishedAt +
                ",owner=" + s.Owner + ",capturer=" + s.Capturer + ",capturePending=" + s.Pending
            : "captureTimeline=not-attributed";
    }
}
