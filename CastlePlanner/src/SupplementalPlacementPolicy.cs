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
        private readonly SortedDictionary<string, int> counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        private readonly List<string> examples = new List<string>();
        internal int Count { get; private set; }
        internal void Add(string reason, string detail)
        {
            Count++;
            counts.TryGetValue(reason, out int previous);
            counts[reason] = previous + 1;
            if (examples.Count < 5) examples.Add(detail + ": " + reason);
        }
        public override string ToString()
        {
            var reasons = new List<string>();
            foreach (KeyValuePair<string, int> pair in counts) reasons.Add(pair.Key + "=" + pair.Value);
            return $"skipped={Count}, reasons=[{string.Join(", ", reasons)}], examples=[{string.Join("; ", examples)}]";
        }
    }

    internal static class SupplementalPlacementPolicy
    {
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
