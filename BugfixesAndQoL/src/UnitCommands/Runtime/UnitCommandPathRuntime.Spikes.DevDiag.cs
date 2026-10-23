using System.Diagnostics;
using System.Globalization;

namespace BugfixesAndQoL.UnitCommands
{
    internal sealed unsafe partial class UnitCommandPathRuntime
    {
        partial void DiagWrapCursorPairOriginal()
        {
            CursorTilePairReachabilityDelegate pair = originalCursorTilePairReachability;
            originalCursorTilePairReachability = (manager, target, selected, cache) =>
            {
                long started = 0;
                Shared.DevDiag.Start(ref started);
                try { return pair(manager, target, selected, cache); }
                finally { Shared.DevDiag.StopSpike(ref started, "uc.vanilla.cursorPair", 100000); }
            };
        }

        partial void DiagWrapBuildingOriginals()
        {
            // Wrap the already resolved originals once during installation. No additional
            // native hooks and no changes to arguments, exception barriers or call counts.
            BuildingApproachBuilderDelegate approach = originalBuildingApproachBuilder;
            originalBuildingApproachBuilder = (manager, tribe, building, requested, region, movementClass) =>
            {
                long started = 0;
                Shared.DevDiag.Start(ref started);
                try { approach(manager, tribe, building, requested, region, movementClass); }
                finally { Shared.DevDiag.StopSpike(ref started, "uc.vanilla.buildingApproach", 100000); }
            };
            BuildingCandidateConsumerDelegate consumer = originalBuildingCandidateConsumer;
            originalBuildingCandidateConsumer = (manager, tribe, variant) =>
            {
                long started = 0;
                Shared.DevDiag.Start(ref started);
                try { consumer(manager, tribe, variant); }
                finally { Shared.DevDiag.StopSpike(ref started, "uc.vanilla.buildingConsumer", 100000); }
            };
        }

        partial void DiagFinishBuildingApproach(ref long measured, int tribeId, int buildingId, int requestedResults, int sourceRegion, int movementClass)
        {
            if (measured == 0) return;
            long elapsed = Stopwatch.GetTimestamp() - measured; measured = 0;
            Shared.DevDiagRuntime.Add("uc.attack.buildingApproach", 1, elapsed);
            if (elapsed < Stopwatch.Frequency / 10) return;
            Shared.DevDiagRuntime.Spike("uc.attack.buildingApproach", elapsed,
                string.Format(CultureInfo.InvariantCulture,
                    "tribe={0} building={1} requestedResults={2} sourceRegion={3} movementClassRaw={4} mapEpoch={5} commandSeq={6}",
                    tribeId, buildingId, requestedResults, sourceRegion, movementClass, mapEpoch, activeAttackCommand?.Sequence ?? 0));
        }

        partial void DiagFinishCursorPair(ref long measured, int targetTileId, int selectedTileId, byte useCache, long originalCalls)
        {
            if (measured == 0) return;
            long elapsed = Stopwatch.GetTimestamp() - measured; measured = 0;
            Shared.DevDiagRuntime.Add("uc.cursorPair", 1, elapsed);
            if (elapsed < Stopwatch.Frequency / 10) return;
            Shared.DevDiagRuntime.Spike("uc.cursorPair", elapsed,
                string.Format(CultureInfo.InvariantCulture,
                    "targetTile={0} selectedTile={1} useCacheRaw={2} originalCalls={3} mapEpoch={4}",
                    targetTileId, selectedTileId, useCache, originalCalls, mapEpoch));
        }
    }
}
