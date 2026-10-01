using System;
using System.Collections.Generic;
using System.Threading;

namespace EnemyGatePathfindingTest
{
    internal sealed class DeferredCaptureRefreshRequest
    {
        private int pending;
        internal void Request() => Interlocked.Exchange(ref pending, 1);
        internal bool Consume() => Interlocked.Exchange(ref pending, 0) != 0;
        internal void Reset() => Interlocked.Exchange(ref pending, 0);
    }

    // Counts observations, not units. Confirmation requires a publication AFTER
    // registration, the same map/identity/owner and precisely the observed capturer.
    internal sealed class CaptureTransitionDiagnostics
    {
        private readonly object sync = new object();
        private readonly Dictionary<string, Row> rows = new Dictionary<string, Row>();
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
                if (source == null || source.DiagnosticEpoch != epoch || source.DiagnosticGeneration <= 0 ||
                    source.DiagnosticGeneration > publishedGeneration ||
                    !source.MatchesGateIdentity(buildingId, globalId) ||
                    buildingId >= source.RecordsByBuildingId.Length || owner <= 0 || owner > 8 ||
                    nativeCapturer < 0 || nativeCapturer > 8) return false;
                NativeGateAccessRecord old = source.RecordsByBuildingId[buildingId];
                if (!old.Valid || old.OwnerPlayerId != owner || old.CapturedByPlayerId == nativeCapturer) return false;
                string key = "epoch=" + epoch + ",generation=" + source.DiagnosticGeneration +
                    ",building=" + buildingId + ",global=" + globalId + ",owner=" + owner +
                    ",oldCapturer=" + old.CapturedByPlayerId + ",observedCapturer=" + nativeCapturer;
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
        internal string[] DrainChanges()
        {
            lock (sync)
            {
                var output = new List<string>();
                foreach (var pair in rows)
                {
                    Row row = pair.Value;
                    if (row.Count == row.LoggedCount && row.Recovered == row.LoggedRecovered) continue;
                    output.Add(pair.Key + ",count=" + row.Count + ",confirmedRecovered=" + row.Recovered +
                        ",unresolved=" + row.Pending + ",proofGeneration=" + row.ProofGeneration);
                    row.LoggedCount = row.Count; row.LoggedRecovered = row.Recovered;
                }
                output.Sort(StringComparer.Ordinal);
                return output.ToArray();
            }
        }
    }
}
