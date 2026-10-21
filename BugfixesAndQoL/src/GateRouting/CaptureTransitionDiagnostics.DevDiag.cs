using System;
using System.Collections.Generic;

namespace BugfixesAndQoL.GateRouting
{
    internal sealed partial class DeferredCaptureRefreshRequest
    {
        // The request is committed before any optional observation. Failure of the observer or
        // its own error reporting cannot erase a functional recovery request.
        internal bool RequestWithDiagnostics(int epoch, Action observe, Action failed)
        {
            if (!Request(epoch)) return false;
            try { observe?.Invoke(); }
            catch { try { failed?.Invoke(); } catch { } }
            return true;
        }
    }

    // Counts observations, not units. Confirmation requires a publication AFTER
    // registration, the same map/identity/owner and precisely the observed capturer.
    internal sealed class CaptureTransitionDiagnostics
    {
        private readonly object sync = new object();
        // Observe runs inside native gate comparisons until the next access publication;
        // a value key avoids building a formatted string for every repeated observation.
        private readonly Dictionary<RowKey, Row> rows = new Dictionary<RowKey, Row>();
        private readonly struct RowKey : IEquatable<RowKey>
        {
            internal RowKey(long epoch, long generation, int building, uint global, int owner,
                int oldCapturer, int observedCapturer)
            {
                Epoch = epoch; Generation = generation; Building = building; Global = global;
                Owner = owner; OldCapturer = oldCapturer; ObservedCapturer = observedCapturer;
            }
            private readonly long Epoch, Generation;
            private readonly int Building, Owner, OldCapturer, ObservedCapturer;
            private readonly uint Global;
            public bool Equals(RowKey other) => Epoch == other.Epoch && Generation == other.Generation &&
                Building == other.Building && Global == other.Global && Owner == other.Owner &&
                OldCapturer == other.OldCapturer && ObservedCapturer == other.ObservedCapturer;
            public override bool Equals(object obj) => obj is RowKey other && Equals(other);
            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = (int)Epoch;
                    hash = (hash * 397) ^ (int)Generation;
                    hash = (hash * 397) ^ Building;
                    hash = (hash * 397) ^ (int)Global;
                    hash = (hash * 397) ^ (Owner | (OldCapturer << 8) | (ObservedCapturer << 16));
                    return hash;
                }
            }
            public override string ToString() =>
                "epoch=" + Epoch + ",generation=" + Generation +
                ",building=" + Building + ",global=" + Global + ",owner=" + Owner +
                ",oldCapturer=" + OldCapturer + ",observedCapturer=" + ObservedCapturer;
        }
        private long epoch, publishedGeneration, total, recovered;
        private sealed class Row
        {
            internal int Building, Owner, Capturer;
            internal uint Global;
            internal long SourceGeneration, MinimumGeneration, Count, Pending, Recovered, ProofGeneration;
            internal long LoggedCount, LoggedRecovered;
        }
        internal void Reset(long mapEpoch)
        {
            lock (sync) { rows.Clear(); epoch = mapEpoch; publishedGeneration = total = recovered = 0; }
        }
        internal bool Observe(NativeGateAccessSnapshot source, int buildingId, uint globalId,
            int owner, int nativeCapturer)
        {
            lock (sync)
            {
                if (!CaptureSnapshotMismatch.RequiresRefresh(source, epoch, publishedGeneration,
                    buildingId, globalId, owner, nativeCapturer)) return false;
                NativeGateAccessRecord old = source.RecordsByBuildingId[buildingId];
                var key = new RowKey(epoch, source.DiagnosticGeneration, buildingId, globalId, owner,
                    old.CapturedByPlayerId, nativeCapturer);
                if (!rows.TryGetValue(key, out Row row))
                {
                    row = new Row { Building = buildingId, Global = globalId, Owner = owner,
                        Capturer = nativeCapturer, SourceGeneration = source.DiagnosticGeneration };
                    rows.Add(key, row);
                }
                row.MinimumGeneration = Math.Max(row.MinimumGeneration, publishedGeneration);
                row.Count++; row.Pending++; total++;
                return true;
            }
        }
        internal void Publish(NativeGateAccessSnapshot snapshot)
        {
            lock (sync)
            {
                if (snapshot == null || snapshot.DiagnosticEpoch != epoch || snapshot.DiagnosticGeneration <= publishedGeneration) return;
                publishedGeneration = snapshot.DiagnosticGeneration;
                foreach (Row row in rows.Values)
                {
                    if (row.Pending == 0 || publishedGeneration <= row.SourceGeneration ||
                        publishedGeneration <= row.MinimumGeneration ||
                        !snapshot.MatchesGateIdentity(row.Building, row.Global) ||
                        row.Building >= snapshot.RecordsByBuildingId.Length) continue;
                    NativeGateAccessRecord record = snapshot.RecordsByBuildingId[row.Building];
                    if (!record.Valid || record.OwnerPlayerId != row.Owner || record.CapturedByPlayerId != row.Capturer) continue;
                    recovered += row.Pending; row.Recovered += row.Pending; row.Pending = 0;
                    row.ProofGeneration = publishedGeneration;
                }
            }
        }
        internal long Recovered { get { lock (sync) return recovered; } }
        internal string Summary { get { lock (sync) return "epoch=" + epoch + ",generation=" + publishedGeneration +
            ",observed=" + total + ",confirmedRecovered=" + recovered + ",unresolved=" + (total - recovered); } }
        internal string[] UnresolvedDetails()
        {
            lock (sync)
            {
                var output = new List<string>();
                foreach (var pair in rows)
                    if (pair.Value.Pending > 0) output.Add(pair.Key.ToString() + ",unresolved=" + pair.Value.Pending);
                output.Sort(StringComparer.Ordinal);
                return output.ToArray();
            }
        }
        internal string[] DrainChanges()
        {
            lock (sync)
            {
                var output = new List<string>();
                foreach (var pair in rows)
                {
                    Row row = pair.Value;
                    if (row.Count == row.LoggedCount && row.Recovered == row.LoggedRecovered) continue;
                    output.Add(pair.Key.ToString() + ",count=" + row.Count + ",confirmedRecovered=" + row.Recovered +
                        ",unresolved=" + row.Pending + ",proofGeneration=" + row.ProofGeneration);
                    row.LoggedCount = row.Count; row.LoggedRecovered = row.Recovered;
                }
                output.Sort(StringComparer.Ordinal);
                return output.ToArray();
            }
        }
    }
}
