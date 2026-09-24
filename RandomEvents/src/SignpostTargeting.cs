using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace RandomEvents
{
    internal static class KeepAnchorGeometry
    {
        public static bool TryGetReferenceTile(
            int beginX,
            int beginY,
            uint occupyGridSize,
            Func<int, int, bool> isInsideMapBounds,
            out double tileX,
            out double tileY)
        {
            tileX = 0;
            tileY = 0;
            if (isInsideMapBounds == null || !isInsideMapBounds(beginX, beginY))
                return false;

            // occupyGridSize is intentionally ignored: distance needs one valid Keep tile.
            tileX = beginX;
            tileY = beginY;
            return true;
        }
    }

    internal static class SignpostAnchorSelection
    {
        public static bool TrySelect(
            bool keepUsable,
            double keepX,
            double keepY,
            bool lordUsable,
            double lordX,
            double lordY,
            out double tileX,
            out double tileY,
            out string reference)
        {
            tileX = 0;
            tileY = 0;
            reference = string.Empty;
            if (keepUsable)
            {
                tileX = keepX;
                tileY = keepY;
                reference = "keep";
                return true;
            }
            if (!lordUsable)
                return false;

            tileX = lordX;
            tileY = lordY;
            reference = "living-lord";
            return true;
        }
    }

    internal static class RandomEventsSignpostPolicy
    {
        public static bool ShouldSkipEvent(RandomEventDefinition definition, bool signpostsInitialized) =>
            definition.RequiresSignpost && !signpostsInitialized;

        public static bool StartsCooldownOnRoll(RandomEventDefinition definition) =>
            definition.DispatchKind == RandomEventDispatchKind.GameAction && !definition.RequiresSignpost;
    }

    internal static class SignpostInitializationReport
    {
        public static string Format(int[] selectedBuildingIds, bool usableRegistered, bool recoveredAfterFailure, string failureReason)
        {
            string ids = selectedBuildingIds == null ? string.Empty : string.Join(",", selectedBuildingIds);
            string report =
                $"Signpost initialization completed: selectedBuildingIds=[{ids}], " +
                $"usableRegistered={usableRegistered.ToString().ToLowerInvariant()}, " +
                $"recoveredAfterFailure={recoveredAfterFailure.ToString().ToLowerInvariant()}.";
            return string.IsNullOrWhiteSpace(failureReason) ? report : report + " Reason: " + failureReason;
        }
    }

    internal readonly struct SignpostTarget
    {
        public SignpostTarget(int buildingId, int tileX, int tileY, double distance, string distanceReference)
        {
            BuildingId = buildingId;
            TileX = tileX;
            TileY = tileY;
            Distance = distance;
            DistanceReference = distanceReference ?? string.Empty;
        }

        public int BuildingId { get; }
        public int TileX { get; }
        public int TileY { get; }
        public double Distance { get; }
        public string DistanceReference { get; }
    }

    internal static class SignpostTargetSelection
    {
        public static bool TrySelectClosest(IReadOnlyList<SignpostTarget> candidates, out SignpostTarget selected)
        {
            selected = default;
            if (candidates == null || candidates.Count == 0)
                return false;

            bool found = false;
            for (int index = 0; index < candidates.Count; index++)
            {
                SignpostTarget candidate = candidates[index];
                if (candidate.BuildingId <= 0 || candidate.TileX < 0 || candidate.TileX >= 800 ||
                    candidate.TileY < 0 || candidate.TileY >= 800 || double.IsNaN(candidate.Distance) ||
                    double.IsInfinity(candidate.Distance) || candidate.Distance < 0)
                {
                    continue;
                }

                int distanceComparison = candidate.Distance.CompareTo(selected.Distance);
                if (!found || distanceComparison < 0 ||
                    (distanceComparison == 0 && candidate.BuildingId < selected.BuildingId))
                {
                    selected = candidate;
                    found = true;
                }
            }
            return found;
        }
    }

    internal sealed class ArcherSourceTargetingScope : IDisposable
    {
        internal const int SlotCount = 8;
        private readonly IntPtr slotsAddress;
        private readonly IntPtr sourceCoordinatesAddress;
        private readonly int[] originalSlots;
        private readonly int originalSourceX;
        private readonly int originalSourceY;
        private bool disposed;

        private ArcherSourceTargetingScope(
            IntPtr slotsAddress,
            IntPtr sourceCoordinatesAddress,
            int[] originalSlots,
            int originalSourceX,
            int originalSourceY)
        {
            this.slotsAddress = slotsAddress;
            this.sourceCoordinatesAddress = sourceCoordinatesAddress;
            this.originalSlots = originalSlots;
            this.originalSourceX = originalSourceX;
            this.originalSourceY = originalSourceY;
        }

        public static bool TryBegin(
            IntPtr slotsAddress,
            IntPtr sourceCoordinatesAddress,
            SignpostTarget target,
            int sourceTileX,
            int sourceTileY,
            out IDisposable scope,
            out int originalSourceX,
            out int originalSourceY,
            out string failure)
        {
            scope = null;
            originalSourceX = 0;
            originalSourceY = 0;
            failure = string.Empty;
            if (slotsAddress == IntPtr.Zero || sourceCoordinatesAddress == IntPtr.Zero)
            {
                failure = "native signpost slots or archer source coordinates are unavailable.";
                return false;
            }
            if (target.BuildingId <= 0 || sourceTileX < 0 || sourceTileX >= 800 || sourceTileY < 0 || sourceTileY >= 800)
            {
                failure = $"invalid archer source values: buildingId={target.BuildingId}, tile=({sourceTileX},{sourceTileY}).";
                return false;
            }

            int[] savedSlots = new int[SlotCount];
            for (int slot = 0; slot < SlotCount; slot++)
                savedSlots[slot] = Marshal.ReadInt32(slotsAddress, slot * sizeof(int));
            int savedX = Marshal.ReadInt32(sourceCoordinatesAddress);
            int savedY = Marshal.ReadInt32(sourceCoordinatesAddress, sizeof(int));
            var targetingScope = new ArcherSourceTargetingScope(
                slotsAddress,
                sourceCoordinatesAddress,
                savedSlots,
                savedX,
                savedY);

            try
            {
                // Case 148 reads slot zero's paired coordinates after choosing the only exposed signpost.
                Marshal.WriteInt32(sourceCoordinatesAddress, sourceTileX);
                Marshal.WriteInt32(sourceCoordinatesAddress, sizeof(int), sourceTileY);
                for (int slot = 0; slot < SlotCount; slot++)
                    Marshal.WriteInt32(slotsAddress, slot * sizeof(int), slot == 0 ? target.BuildingId : 0);

                for (int slot = 0; slot < SlotCount; slot++)
                {
                    int expected = slot == 0 ? target.BuildingId : 0;
                    int actual = Marshal.ReadInt32(slotsAddress, slot * sizeof(int));
                    if (actual != expected)
                        throw new InvalidOperationException($"signpost slot {slot} contains {actual} instead of {expected}.");
                }
                int actualX = Marshal.ReadInt32(sourceCoordinatesAddress);
                int actualY = Marshal.ReadInt32(sourceCoordinatesAddress, sizeof(int));
                if (actualX != sourceTileX || actualY != sourceTileY)
                {
                    throw new InvalidOperationException(
                        $"archer source contains ({actualX},{actualY}) instead of ({sourceTileX},{sourceTileY}).");
                }
            }
            catch (Exception ex)
            {
                targetingScope.Dispose();
                failure = $"temporary native source prioritization failed: {ex.Message}";
                return false;
            }

            originalSourceX = savedX;
            originalSourceY = savedY;
            scope = targetingScope;
            return true;
        }

        public void Dispose()
        {
            if (disposed)
                return;
            for (int slot = 0; slot < SlotCount; slot++)
                Marshal.WriteInt32(slotsAddress, slot * sizeof(int), originalSlots[slot]);
            Marshal.WriteInt32(sourceCoordinatesAddress, originalSourceX);
            Marshal.WriteInt32(sourceCoordinatesAddress, sizeof(int), originalSourceY);
            disposed = true;
        }
    }
}
