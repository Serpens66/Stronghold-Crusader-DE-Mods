// Feature: Bound temporary gatehouse reachability diagnostics without hiding state changes.
using System;
using System.Collections.Generic;

namespace BugfixesAndQoL
{
    internal enum GatehouseDiagnosticEmission
    {
        None,
        Detailed,
        Summary
    }

    internal sealed class GatehouseReachabilityDiagnosticThrottle
    {
        internal const int SummaryInterval = 250;
        private const int MaximumTrackedPairs = 256;

        private readonly Dictionary<DiagnosticKey, DiagnosticState> states =
            new Dictionary<DiagnosticKey, DiagnosticState>();

        internal GatehouseDiagnosticEmission Observe(
            int gatehouseId,
            uint gatehouseGlobalId,
            int unitId,
            uint unitGlobalId,
            string fingerprint,
            out int suppressedRepeats)
        {
            if (fingerprint == null)
                throw new ArgumentNullException(nameof(fingerprint));

            suppressedRepeats = 0;
            var key = new DiagnosticKey(
                gatehouseId,
                gatehouseGlobalId,
                unitId,
                unitGlobalId);
            if (!states.TryGetValue(key, out DiagnosticState state))
            {
                if (states.Count >= MaximumTrackedPairs)
                    states.Clear();

                states[key] = new DiagnosticState(fingerprint);
                return GatehouseDiagnosticEmission.Detailed;
            }

            if (!string.Equals(state.Fingerprint, fingerprint, StringComparison.Ordinal))
            {
                suppressedRepeats = state.SuppressedRepeats;
                state.Fingerprint = fingerprint;
                state.SuppressedRepeats = 0;
                return GatehouseDiagnosticEmission.Detailed;
            }

            state.SuppressedRepeats++;
            if (state.SuppressedRepeats < SummaryInterval)
                return GatehouseDiagnosticEmission.None;

            suppressedRepeats = state.SuppressedRepeats;
            state.SuppressedRepeats = 0;
            return GatehouseDiagnosticEmission.Summary;
        }

        internal void Clear() => states.Clear();

        private sealed class DiagnosticState
        {
            internal DiagnosticState(string fingerprint)
            {
                Fingerprint = fingerprint;
            }

            internal string Fingerprint { get; set; }
            internal int SuppressedRepeats { get; set; }
        }

        private readonly struct DiagnosticKey : IEquatable<DiagnosticKey>
        {
            internal DiagnosticKey(
                int gatehouseId,
                uint gatehouseGlobalId,
                int unitId,
                uint unitGlobalId)
            {
                GatehouseId = gatehouseId;
                GatehouseGlobalId = gatehouseGlobalId;
                UnitId = unitId;
                UnitGlobalId = unitGlobalId;
            }

            private int GatehouseId { get; }
            private uint GatehouseGlobalId { get; }
            private int UnitId { get; }
            private uint UnitGlobalId { get; }

            public bool Equals(DiagnosticKey other) =>
                GatehouseId == other.GatehouseId &&
                GatehouseGlobalId == other.GatehouseGlobalId &&
                UnitId == other.UnitId &&
                UnitGlobalId == other.UnitGlobalId;

            public override bool Equals(object obj) =>
                obj is DiagnosticKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = GatehouseId;
                    hash = hash * 397 ^ (int)GatehouseGlobalId;
                    hash = hash * 397 ^ UnitId;
                    return hash * 397 ^ (int)UnitGlobalId;
                }
            }
        }
    }
}
