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
            {
                centralOriginalEntries++;
                long measured = 0;
                Shared.DevDiag.Start(ref measured);
                try { return lastCentralMovementPlanResult = plan(manager, unit, x, y); }
                finally { Shared.DevDiag.StopSpike(ref measured, "uc.vanilla.central", 20000); }
            };
            PathBuilderDelegate builder = originalPathBuilder;
            originalPathBuilder = (manager, movementClass, profile) =>
            {
#if SERP_DEVDIAG
                long freeze = GateAttackFreezeCapture.Enter(GateAttackFreezeCapture.OriginalBuilder, default);
                bool freezeCompleted = false;
#endif
                builderOriginalEntries++;
                long measured = 0;
                Shared.DevDiag.Start(ref measured);
                try
                {
                    lastPathBuilderResult = builder(manager, movementClass, profile);
#if SERP_DEVDIAG
                    freezeCompleted = true;
#endif
                    return lastPathBuilderResult;
                }
                finally
                {
#if SERP_DEVDIAG
                    GateAttackFreezeCapture.Leave(freeze, lastPathBuilderResult, freezeCompleted);
#endif
                    Shared.DevDiag.StopSpike(ref measured, "uc.vanilla.builder", 20000);
                }
            };
            PathReconstructionDelegate reconstruction = originalPathReconstruction;
            originalPathReconstruction = manager =>
            {
                reconstructionOriginalEntries++;
                long measured = 0;
                Shared.DevDiag.Start(ref measured);
                try { return lastPathReconstructionResult = reconstruction(manager); }
                finally { Shared.DevDiag.StopSpike(ref measured, "uc.vanilla.reconstruction", 20000); }
            };
            TribeFloodFillMembershipDelegate flood = originalTribeFloodFillMembership;
            originalTribeFloodFillMembership = (manager, tribe, stamp) =>
            {
                floodOriginalEntries++;
                long measured = 0;
                Shared.DevDiag.Start(ref measured);
                try { return lastTribeFloodFillResult = flood(manager, tribe, stamp); }
                finally { Shared.DevDiag.StopSpike(ref measured, "uc.vanilla.flood", 20000); }
            };
            FirstGroupUnitOnCompletedMoatDelegate group = originalFirstGroupUnitOnCompletedMoat;
            originalFirstGroupUnitOnCompletedMoat = (manager, tribe) =>
            { groupOriginalEntries++; return lastFirstGroupUnitResult = group(manager, tribe); };
            RegionReachabilityDelegate region = originalRegionReachability;
            originalRegionReachability = (manager, movementClass, targetRegion, x, y) =>
            {
                regionOriginalEntries++;
                long measured = 0;
                Shared.DevDiag.Start(ref measured);
                try { return lastRegionReachabilityResult = region(manager, movementClass, targetRegion, x, y); }
                finally { Shared.DevDiag.StopSpike(ref measured, "uc.vanilla.region", 20000); }
            };
            CursorTilePairReachabilityDelegate pair = originalCursorTilePairReachability;
            originalCursorTilePairReachability = (manager, target, selected, cache) =>
            { pairOriginalEntries++; return lastCursorTilePairResult = pair(manager, target, selected, cache); };
            DiagWrapCursorPairOriginal();
            CombatFinishResumeDelegate resume = originalCombatFinishResume;
            originalCombatFinishResume = (manager, unit) => { resumeOriginalEntries++; resume(manager, unit); };
            CursorMoveStagerDelegate stager = originalCursorMoveStager;
            originalCursorMoveStager = (manager, tribe, x, y, context, flags) =>
            { stagerOriginalEntries++; stager(manager, tribe, x, y, context, flags); };
        }

        private int CentralMovementPlanBarrier(IntPtr unitManager, int unitId, int targetX, int targetY)
        {
            long measured = 0;
            Shared.DevDiag.Start(ref measured);
            long entries = centralOriginalEntries;
            try { return RunCentralMovementPlanWithContext(unitManager, unitId, targetX, targetY); }
            catch (Exception ex)
            {
                ReportNativeDetourBarrier("central-movement-plan", ex);
                if (centralOriginalEntries != entries) return lastCentralMovementPlanResult;
                try { return originalCentralMovementPlan(unitManager, unitId, targetX, targetY); }
                catch (Exception fallback) { ReportNativeDetourBarrier("central-movement-plan-fallback", fallback); return 0; }
            }
            finally { Shared.DevDiag.StopSpike(ref measured, "uc.central", 20000); }
        }

        private int PathBuilderBarrier(IntPtr pathManager, int movementClass, int movementProfile)
        {
            long measured = 0;
            Shared.DevDiag.Start(ref measured);
            long entries = builderOriginalEntries;
            try { return BuildPathWithCompletedMoatRouteVariant(pathManager, movementClass, movementProfile); }
            catch (Exception ex)
            {
                ReportNativeDetourBarrier("F4930-builder", ex);
                if (builderOriginalEntries != entries) return lastPathBuilderResult;
                try { return originalPathBuilder(pathManager, movementClass, movementProfile); }
                catch (Exception fallback) { ReportNativeDetourBarrier("F4930-builder-fallback", fallback); return 0; }
            }
            finally { Shared.DevDiag.StopSpike(ref measured, "uc.builder", 20000); }
        }

        private int PathReconstructionBarrier(IntPtr pathManager)
        {
            long measured = 0;
            Shared.DevDiag.Start(ref measured);
            long entries = reconstructionOriginalEntries;
            try { return BuildReconstructedUnitPath(pathManager); }
            catch (Exception ex)
            {
                ReportNativeDetourBarrier("E32B0-reconstruction", ex);
                if (reconstructionOriginalEntries != entries) return lastPathReconstructionResult;
                try { return originalPathReconstruction(pathManager); }
                catch (Exception fallback) { ReportNativeDetourBarrier("E32B0-reconstruction-fallback", fallback); return 0; }
            }
            finally { Shared.DevDiag.StopSpike(ref measured, "uc.reconstruction", 20000); }
        }

        private int TribeFloodFillBarrier(IntPtr tribeManager, int tribeId, int floodFillStamp)
        {
            long measured = 0;
            Shared.DevDiag.Start(ref measured);
            long entries = floodOriginalEntries;
            try { return AllowTribeFloodFillForMoveOrder(tribeManager, tribeId, floodFillStamp); }
            catch (Exception ex)
            {
                ReportNativeDetourBarrier("tribe-flood-fill", ex);
                if (floodOriginalEntries != entries) return lastTribeFloodFillResult;
                try { return originalTribeFloodFillMembership(tribeManager, tribeId, floodFillStamp); }
                catch (Exception fallback) { ReportNativeDetourBarrier("tribe-flood-fill-fallback", fallback); return 0; }
            }
            finally { Shared.DevDiag.StopSpike(ref measured, "uc.flood", 20000); }
        }

        private int FirstGroupUnitBarrier(IntPtr tribeManager, int tribeId)
        {
            long measured = 0;
            Shared.DevDiag.Start(ref measured);
            long entries = groupOriginalEntries;
            try { return SelectOwnerSafeGroupMoatMode(tribeManager, tribeId); }
            catch (Exception ex)
            {
                ReportNativeDetourBarrier("group-moat-mode", ex);
                if (groupOriginalEntries != entries) return lastFirstGroupUnitResult;
                try { return originalFirstGroupUnitOnCompletedMoat(tribeManager, tribeId); }
                catch (Exception fallback) { ReportNativeDetourBarrier("group-moat-mode-fallback", fallback); return 0; }
            }
            finally { Shared.DevDiag.StopSpike(ref measured, "uc.groupMoat", 20000); }
        }

        private int RegionReachabilityBarrier(
            IntPtr pathManager, int movementClass, int targetRegion, int startX, int startY)
        {
            long measured = 0;
            Shared.DevDiag.Start(ref measured);
            long entries = regionOriginalEntries;
            try { return AllowBuilderAfterFailedRegionSearch(pathManager, movementClass, targetRegion, startX, startY); }
            catch (Exception ex)
            {
                ReportNativeDetourBarrier("region-reachability", ex);
                if (regionOriginalEntries != entries) return lastRegionReachabilityResult;
                try { return originalRegionReachability(pathManager, movementClass, targetRegion, startX, startY); }
                catch (Exception fallback) { ReportNativeDetourBarrier("region-reachability-fallback", fallback); return 0; }
            }
            finally { Shared.DevDiag.StopSpike(ref measured, "uc.region", 20000); }
        }

        private int CursorTilePairBarrier(
            IntPtr pathManager, int targetTileId, int selectedUnitTileId, byte useCache)
        {
            long measured = 0;
            Shared.DevDiag.Start(ref measured);
            long entries = pairOriginalEntries;
            try { return AllowAttackCursorTilePairThroughCompletedMoat(pathManager, targetTileId, selectedUnitTileId, useCache); }
            catch (Exception ex)
            {
                ReportNativeDetourBarrier("cursor-tile-pair", ex);
                if (pairOriginalEntries != entries) return lastCursorTilePairResult;
                try { return originalCursorTilePairReachability(pathManager, targetTileId, selectedUnitTileId, useCache); }
                catch (Exception fallback) { ReportNativeDetourBarrier("cursor-tile-pair-fallback", fallback); return 0; }
            }
            finally { DiagFinishCursorPair(ref measured, targetTileId, selectedUnitTileId, useCache, pairOriginalEntries - entries); }
        }

        partial void DiagFinishCursorPair(ref long measured, int targetTileId, int selectedTileId, byte useCache, long originalCalls);
        partial void DiagWrapCursorPairOriginal();

        private void CombatFinishResumeBarrier(IntPtr unitManager, int unitId)
        {
            long measured = 0;
            Shared.DevDiag.Start(ref measured);
            long entries = resumeOriginalEntries;
            try { ResumeMovementAfterCombatWithMoatContext(unitManager, unitId); }
            catch (Exception ex)
            {
                ReportNativeDetourBarrier("combat-finish-resume", ex);
                if (resumeOriginalEntries != entries) return;
                try { originalCombatFinishResume(unitManager, unitId); }
                catch (Exception fallback) { ReportNativeDetourBarrier("combat-finish-resume-fallback", fallback); }
            }
            finally { Shared.DevDiag.StopSpike(ref measured, "uc.combatResume", 20000); }
        }

        private void CursorMoveStagerBarrier(
            IntPtr unitManager, int tribeId, int targetX, int targetY, int targetContext, int actionFlags)
        {
            long measured = 0;
            Shared.DevDiag.Start(ref measured);
            long entries = stagerOriginalEntries;
            try { StageDirectCursorMoveWithOwnerRoute(unitManager, tribeId, targetX, targetY, targetContext, actionFlags); }
            catch (Exception ex)
            {
                ReportNativeDetourBarrier("cursor-move-stager", ex);
                if (stagerOriginalEntries != entries) return;
                try { originalCursorMoveStager(unitManager, tribeId, targetX, targetY, targetContext, actionFlags); }
                catch (Exception fallback) { ReportNativeDetourBarrier("cursor-move-stager-fallback", fallback); }
            }
            finally { Shared.DevDiag.StopSpike(ref measured, "uc.cursorStager", 20000); }
        }
    }
}
