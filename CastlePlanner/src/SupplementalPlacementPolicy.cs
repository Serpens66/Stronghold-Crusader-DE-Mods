using System;
using System.Collections.Generic;
using AIVParser.Core;
using AIVPlacement.Core;
using SHCDESE.Interop.Enums;
using AivRotation = AIVParser.Core.AivRotation;

namespace CastlePlanner
{
    internal sealed class SupplementalSpawnReport
    {
        private readonly SortedDictionary<string, SupplementalSpawnSummary> summaries =
            new SortedDictionary<string, SupplementalSpawnSummary>(StringComparer.Ordinal);
        internal int Count { get; private set; }
        internal IEnumerable<SupplementalSpawnSummary> Summaries => summaries.Values;

        internal static string CategoryFor(string reason)
        {
            switch (ReasonCode(reason))
            {
                case "out-of-bounds":
                case "footprint-out-of-bounds":
                case "water-terrain":
                case "impassable-terrain":
                case "physical-terrain-obstacle":
                case "occupied-or-already-created":
                case "duplicate-prefab-request":
                case "decoration-already-present-or-queued":
                    return "expected";
                case "source-anchor-out-of-bounds":
                case "source-footprint-out-of-bounds":
                case "invalid-map-tile":
                case "unknown-misc":
                case "unsupported-supplemental-visual":
                case "creation-threw":
                case "unit-creation-threw":
                case "decoration-creation-threw":
                case "invalid-decoration-coordinate":
                case "invalid-projectile-id":
                    return "technical";
                default:
                    // A failed native call or identity check does not prove lack of space.
                    return "unexplained";
            }
        }

        internal static string ReasonCode(string reason)
        {
            if (string.IsNullOrEmpty(reason)) return "unspecified";
            // Prefab capture reasons carry variable observations in parentheses.
            if (reason.StartsWith("unconfirmed-spawn(", StringComparison.Ordinal)) return "unconfirmed-spawn";
            if (reason.StartsWith("changed-spawn-identity(", StringComparison.Ordinal)) return "changed-spawn-identity";
            if (reason.StartsWith("unsupported-supplemental-visual(", StringComparison.Ordinal)) return "unsupported-supplemental-visual";
            return reason;
        }

        internal void Add(string reason, string detail)
        {
            string category = CategoryFor(reason);
            if (!summaries.TryGetValue(category, out SupplementalSpawnSummary summary))
            {
                summary = new SupplementalSpawnSummary(category);
                summaries.Add(category, summary);
            }
            summary.Add(reason, detail);
            Count++;
        }
    }

    internal sealed class SupplementalSpawnSummary
    {
        private const int MaximumReasonKinds = 16;
        private readonly SortedDictionary<string, int> counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        private readonly List<string> examples = new List<string>();
        internal string Category { get; }
        internal bool IsExpected => Category == "expected";
        internal int Count { get; private set; }
        internal IReadOnlyDictionary<string, int> Reasons => counts;
        internal IReadOnlyList<string> Examples => examples;

        internal SupplementalSpawnSummary(string category) { Category = category; }

        internal void Add(string reason, string detail)
        {
            Count++;
            string code = SupplementalSpawnReport.ReasonCode(reason);
            if (!counts.ContainsKey(code) && counts.Count >= MaximumReasonKinds)
                code = "other-reasons";
            counts.TryGetValue(code, out int previous);
            counts[code] = previous + 1;
            if (examples.Count < 5) examples.Add(detail + ": " + reason);
        }
        public override string ToString()
        {
            var reasons = new List<string>();
            foreach (KeyValuePair<string, int> pair in counts) reasons.Add(pair.Key + "=" + pair.Value);
            return $"category={Category}, skipped={Count}, reasons=[{string.Join(", ", reasons)}], examples=[{string.Join("; ", examples)}]";
        }
    }

    internal static class SupplementalPlacementPolicy
    {
        internal static bool TryResolveMiscTile(int encodedPosition, int keepX, int keepY,
            AivRotation rotation, Func<AivWorldTile, string> checkTile,
            out AivWorldTile tile, out string reason)
        {
            tile = default;
            if (encodedPosition < 0 || encodedPosition >= AivGridPoint.GridSize * AivGridPoint.GridSize)
            { reason = "source-anchor-out-of-bounds"; return false; }
            tile = AivWorldTransform.ProjectNativeFit(new AivGridPoint(encodedPosition), keepX, keepY, rotation);
            reason = checkTile(tile);
            return string.IsNullOrEmpty(reason);
        }

        internal static string CheckMiscTerrain(TilePropertyFlag flags)
        {
            if ((flags & (TilePropertyFlag.Sea | TilePropertyFlag.River |
                TilePropertyFlag.Ford | TilePropertyFlag.IsMoat)) != 0)
                return "water-terrain";
            // ImpassableEdge also occurs on fortifications; preserve their valid misc placements.
            if ((flags & TilePropertyFlag.ImpassableEdge) != 0 &&
                (flags & (TilePropertyFlag.IsWall | TilePropertyFlag.IsElevated)) == 0)
                return "impassable-terrain";
            return string.Empty;
        }

        internal static bool CanPlace(int mapper, int encodedPosition, int keepX, int keepY,
            AivRotation rotation, Func<AivWorldTile, string> checkTile, out string reason)
        {
            if (encodedPosition < 0 || encodedPosition >= AivGridPoint.GridSize * AivGridPoint.GridSize)
            { reason = "source-anchor-out-of-bounds"; return false; }
            int size = Math.Max(1, AivMapperCatalog.Resolve(mapper).FootprintSize.GetValueOrDefault(1));
            AivGridPoint anchor = new AivGridPoint(encodedPosition);
            for (int rowOffset = 0; rowOffset < size; rowOffset++)
                for (int columnOffset = 0; columnOffset < size; columnOffset++)
                {
                    int row = anchor.Row - rowOffset, column = anchor.Column + columnOffset;
                    if (row < 0 || row >= AivGridPoint.GridSize || column < 0 || column >= AivGridPoint.GridSize)
                    { reason = "source-footprint-out-of-bounds"; return false; }
                    AivWorldTile tile = AivWorldTransform.ProjectNativeFit(new AivGridPoint(row, column), keepX, keepY, rotation);
                    reason = checkTile(tile);
                    if (!string.IsNullOrEmpty(reason)) return false;
                }
            reason = string.Empty;
            return true;
        }

        internal static string CheckPhysicalTile(int mapper, int buildingId, int height, TilePropertyFlag flags)
        {
            if (buildingId != 0 || (flags & TilePropertyFlag.IsWall) != 0)
                return "occupied-or-already-created";
            // Shared with the audited offline AIV evaluator, including mapper exceptions.
            if (height > AivTerrainPlacementRules.NativeMaximumHeight || AivTerrainPlacementRules.IsTerrainBlocked(mapper, unchecked((int)flags)))
                return "physical-terrain-obstacle";
            return string.Empty;
        }
    }
}
