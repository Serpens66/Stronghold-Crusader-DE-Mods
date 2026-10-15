using System;

namespace BugfixesAndQoL.UnitCommands
{
    // Exception barriers for the central movement detour transaction. RedBird invokes the
    // rooted managed callbacks directly from native code; a managed exception must never
    // unwind into CrusaderDE. Each barrier snapshots its own original's per-thread entry
    // counter (nested calls of other originals do not count): if Vanilla has not run yet
    // it runs exactly once, otherwise the latest result of that original is returned.
    // Native contract (FBCB9319): F4930 and E32B0 return the published path length at
    // +0x155F68, and 0 is their own failure result.
    internal sealed unsafe partial class UnitCommandPathRuntime
    {
        [ThreadStatic] private static long centralOriginalEntries;
        [ThreadStatic] private static long builderOriginalEntries;
        [ThreadStatic] private static long reconstructionOriginalEntries;
        [ThreadStatic] private static long floodOriginalEntries;
        [ThreadStatic] private static long groupOriginalEntries;
        [ThreadStatic] private static long regionOriginalEntries;
        [ThreadStatic] private static long pairOriginalEntries;
        [ThreadStatic] private static long resumeOriginalEntries;
        [ThreadStatic] private static long stagerOriginalEntries;
        [ThreadStatic] private static int lastCentralMovementPlanResult;
        [ThreadStatic] private static int lastPathBuilderResult;
        [ThreadStatic] private static int lastPathReconstructionResult;
        [ThreadStatic] private static int lastTribeFloodFillResult;
        [ThreadStatic] private static int lastFirstGroupUnitResult;
        [ThreadStatic] private static int lastRegionReachabilityResult;
        [ThreadStatic] private static int lastCursorTilePairResult;

        private void InstallNativeOriginalEntryCounters()
        {
            CentralMovementPlanDelegate plan = originalCentralMovementPlan;
            originalCentralMovementPlan = (manager, unit, x, y) =>
            { centralOriginalEntries++; return lastCentralMovementPlanResult = plan(manager, unit, x, y); };
            PathBuilderDelegate builder = originalPathBuilder;
            originalPathBuilder = (manager, movementClass, profile) =>
            { builderOriginalEntries++; return lastPathBuilderResult = builder(manager, movementClass, profile); };
            PathReconstructionDelegate reconstruction = originalPathReconstruction;
            originalPathReconstruction = manager =>
            { reconstructionOriginalEntries++; return lastPathReconstructionResult = reconstruction(manager); };
            TribeFloodFillMembershipDelegate flood = originalTribeFloodFillMembership;
            originalTribeFloodFillMembership = (manager, tribe, stamp) =>
            { floodOriginalEntries++; return lastTribeFloodFillResult = flood(manager, tribe, stamp); };
            FirstGroupUnitOnCompletedMoatDelegate group = originalFirstGroupUnitOnCompletedMoat;
            originalFirstGroupUnitOnCompletedMoat = (manager, tribe) =>
            { groupOriginalEntries++; return lastFirstGroupUnitResult = group(manager, tribe); };
            RegionReachabilityDelegate region = originalRegionReachability;
            originalRegionReachability = (manager, movementClass, targetRegion, x, y) =>
            { regionOriginalEntries++; return lastRegionReachabilityResult = region(manager, movementClass, targetRegion, x, y); };
            CursorTilePairReachabilityDelegate pair = originalCursorTilePairReachability;
            originalCursorTilePairReachability = (manager, target, selected, cache) =>
            { pairOriginalEntries++; return lastCursorTilePairResult = pair(manager, target, selected, cache); };
            CombatFinishResumeDelegate resume = originalCombatFinishResume;
            originalCombatFinishResume = (manager, unit) => { resumeOriginalEntries++; resume(manager, unit); };
            CursorMoveStagerDelegate stager = originalCursorMoveStager;
            originalCursorMoveStager = (manager, tribe, x, y, context, flags) =>
            { stagerOriginalEntries++; stager(manager, tribe, x, y, context, flags); };
        }

        private int CentralMovementPlanBarrier(IntPtr unitManager, int unitId, int targetX, int targetY)
        {
            long entries = centralOriginalEntries;
            try { return RunCentralMovementPlanWithContext(unitManager, unitId, targetX, targetY); }
            catch (Exception ex)
            {
                ReportNativeDetourBarrier("central-movement-plan", ex);
                if (centralOriginalEntries != entries) return lastCentralMovementPlanResult;
                try { return originalCentralMovementPlan(unitManager, unitId, targetX, targetY); }
                catch (Exception fallback) { ReportNativeDetourBarrier("central-movement-plan-fallback", fallback); return 0; }
            }
        }

        private int PathBuilderBarrier(IntPtr pathManager, int movementClass, int movementProfile)
        {
            long entries = builderOriginalEntries;
            try { return BuildPathWithCompletedMoatRouteVariant(pathManager, movementClass, movementProfile); }
            catch (Exception ex)
            {
                ReportNativeDetourBarrier("F4930-builder", ex);
                if (builderOriginalEntries != entries) return lastPathBuilderResult;
                try { return originalPathBuilder(pathManager, movementClass, movementProfile); }
                catch (Exception fallback) { ReportNativeDetourBarrier("F4930-builder-fallback", fallback); return 0; }
            }
        }

        private int PathReconstructionBarrier(IntPtr pathManager)
        {
            long entries = reconstructionOriginalEntries;
            try { return BuildReconstructedUnitPath(pathManager); }
            catch (Exception ex)
            {
                ReportNativeDetourBarrier("E32B0-reconstruction", ex);
                if (reconstructionOriginalEntries != entries) return lastPathReconstructionResult;
                try { return originalPathReconstruction(pathManager); }
                catch (Exception fallback) { ReportNativeDetourBarrier("E32B0-reconstruction-fallback", fallback); return 0; }
            }
        }

        private int TribeFloodFillBarrier(IntPtr tribeManager, int tribeId, int floodFillStamp)
        {
            long entries = floodOriginalEntries;
            try { return AllowTribeFloodFillForMoveOrder(tribeManager, tribeId, floodFillStamp); }
            catch (Exception ex)
            {
                ReportNativeDetourBarrier("tribe-flood-fill", ex);
                if (floodOriginalEntries != entries) return lastTribeFloodFillResult;
                try { return originalTribeFloodFillMembership(tribeManager, tribeId, floodFillStamp); }
                catch (Exception fallback) { ReportNativeDetourBarrier("tribe-flood-fill-fallback", fallback); return 0; }
            }
        }

        private int FirstGroupUnitBarrier(IntPtr tribeManager, int tribeId)
        {
            long entries = groupOriginalEntries;
            try { return SelectOwnerSafeGroupMoatMode(tribeManager, tribeId); }
            catch (Exception ex)
            {
                ReportNativeDetourBarrier("group-moat-mode", ex);
                if (groupOriginalEntries != entries) return lastFirstGroupUnitResult;
                try { return originalFirstGroupUnitOnCompletedMoat(tribeManager, tribeId); }
                catch (Exception fallback) { ReportNativeDetourBarrier("group-moat-mode-fallback", fallback); return 0; }
            }
        }

        private int RegionReachabilityBarrier(
            IntPtr pathManager, int movementClass, int targetRegion, int startX, int startY)
        {
            long entries = regionOriginalEntries;
            try { return AllowBuilderAfterFailedRegionSearch(pathManager, movementClass, targetRegion, startX, startY); }
            catch (Exception ex)
            {
                ReportNativeDetourBarrier("region-reachability", ex);
                if (regionOriginalEntries != entries) return lastRegionReachabilityResult;
                try { return originalRegionReachability(pathManager, movementClass, targetRegion, startX, startY); }
                catch (Exception fallback) { ReportNativeDetourBarrier("region-reachability-fallback", fallback); return 0; }
            }
        }

        private int CursorTilePairBarrier(
            IntPtr pathManager, int targetTileId, int selectedUnitTileId, byte useCache)
        {
            long entries = pairOriginalEntries;
            try { return AllowAttackCursorTilePairThroughCompletedMoat(pathManager, targetTileId, selectedUnitTileId, useCache); }
            catch (Exception ex)
            {
                ReportNativeDetourBarrier("cursor-tile-pair", ex);
                if (pairOriginalEntries != entries) return lastCursorTilePairResult;
                try { return originalCursorTilePairReachability(pathManager, targetTileId, selectedUnitTileId, useCache); }
                catch (Exception fallback) { ReportNativeDetourBarrier("cursor-tile-pair-fallback", fallback); return 0; }
            }
        }

        private void CombatFinishResumeBarrier(IntPtr unitManager, int unitId)
        {
            long entries = resumeOriginalEntries;
            try { ResumeMovementAfterCombatWithMoatContext(unitManager, unitId); }
            catch (Exception ex)
            {
                ReportNativeDetourBarrier("combat-finish-resume", ex);
                if (resumeOriginalEntries != entries) return;
                try { originalCombatFinishResume(unitManager, unitId); }
                catch (Exception fallback) { ReportNativeDetourBarrier("combat-finish-resume-fallback", fallback); }
            }
        }

        private void CursorMoveStagerBarrier(
            IntPtr unitManager, int tribeId, int targetX, int targetY, int targetContext, int actionFlags)
        {
            long entries = stagerOriginalEntries;
            try { StageDirectCursorMoveWithOwnerRoute(unitManager, tribeId, targetX, targetY, targetContext, actionFlags); }
            catch (Exception ex)
            {
                ReportNativeDetourBarrier("cursor-move-stager", ex);
                if (stagerOriginalEntries != entries) return;
                try { originalCursorMoveStager(unitManager, tribeId, targetX, targetY, targetContext, actionFlags); }
                catch (Exception fallback) { ReportNativeDetourBarrier("cursor-move-stager-fallback", fallback); }
            }
        }
    }
}
