using System;
using System.Collections.Generic;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace BugfixesAndQoL
{
    // The ordinary storage branch of native 0x51790 has exactly these success writes.
    // Kept separate from placement so the native state contract can be exercised without a game.
    internal static class PrebuiltAiStorageCompletionPolicy
    {
        internal static List<Shared.StoragePlacement> FilterStartupPlan(
            IEnumerable<Shared.StoragePlacement> source,
            Func<Shared.StoragePlacement, Shared.StorageSectionState> inspect,
            Func<int, AivBuildStepState> readState)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (inspect == null) throw new ArgumentNullException(nameof(inspect));
            if (readState == null) throw new ArgumentNullException(nameof(readState));
            var placements = new List<Shared.StoragePlacement>();
            var missing = new HashSet<Tuple<eMappers, int, int>>();
            var built = new HashSet<Tuple<eMappers, int, int>>();
            foreach (Shared.StoragePlacement placement in source)
            {
                placements.Add(placement);
                var origin = Tuple.Create(placement.Mapper, placement.X, placement.Y);
                if (inspect(placement) == Shared.StorageSectionState.Missing) missing.Add(origin);
                if (readState(placement.SourceIndex) == AivBuildStepState.Built) built.Add(origin);
            }
            var result = new List<Shared.StoragePlacement>(placements.Count);
            foreach (Shared.StoragePlacement placement in placements)
            {
                var origin = Tuple.Create(placement.Mapper, placement.X, placement.Y);
                // Deduplication and completion are section-based. A failed duplicate
                // must not resurrect the same section through another source frame.
                if (!missing.Contains(origin) || !built.Contains(origin)) result.Add(placement);
            }
            return result;
        }

        internal static bool IsMatchingStep(in AivBuildStep step, eMappers mapper, int mapTileId) =>
            (step.State == AivBuildStepState.Pending || step.State == AivBuildStepState.Built ||
             step.State == AivBuildStepState.RetryPending) &&
            step.BuildingType == mapper && step.TileCount == 1 && step.MapTileIdOrBufferIndex == mapTileId;

        internal static void MarkBuilt(ref AivBuildStep step)
        {
            step.State = AivBuildStepState.Built;
            step.RebuildDelay = 10;
        }

        internal static void MarkCreatedBuilding(ref GameBuilding building)
        {
            building.p_UnknownRelevant17 = 1;
        }
    }
}
