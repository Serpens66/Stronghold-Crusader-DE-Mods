using BepInEx.Logging;
using APIShared;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace BugfixesAndQoL.UnitCommands
{
    internal sealed unsafe partial class UnitCommandPathRuntime
    {
        private bool assassinExactPublicationLogged;
        internal int BuildPathWithCompletedMoatRouteVariant(
            IntPtr pathManager, int movementClass, int movementProfile)
        {
#if SERP_DEVDIAG
            long freeze = FreezeBuilder(pathManager);
#endif
            IEnemyGatePathPolicy gate = BeginEnemyGateSearch(
                movementClass, EnemyGateSearchKind.Builder, out object gateScope);
            object bridgeSearch = EnemyBridgeDiagnosticBridge.BeginSearch("builder", movementClass);
            // TEMP_GATE_ROUTE_ACCEPTANCE
#if SERP_DEVDIAG
            object temporaryRoute = BeginTemporaryRouteReport(pathManager, "F4930-builder", movementClass, movementProfile);
#endif

            int result = 0;
            bool completed = false;
            AssassinRouteOutputFrame assassinRoute;
            try { assassinRoute = BeginAssassinRoutePublication(pathManager); }
            catch (Exception ex)
            {
                TryLogDiagnosticFailure("assassin-route-capture", ex);
                assassinRoute = AssassinPathAPI.BeginRouteOutput(pathManager, 0, 0, 0, 0, 0, null, null);
            }
            try
            {
                result = BuildPathWithCompletedMoatRouteVariantWithMoat(
                    pathManager, movementClass, movementProfile);
#if SERP_DEVDIAG
                ObserveTemporaryNativeBuilderOutput(temporaryRoute, result);
#endif

                try { result = assassinRoute.Complete(result); }
                catch (Exception ex) { TryLogDiagnosticFailure("assassin-route-publication", ex); }
                try { ObserveCursorGroundBuilder(pathManager, movementClass, movementProfile, result); }
                catch (Exception ex) { TryLogDiagnosticFailure("cursor-ground-reject-capture", ex); }
                completed = true;
                return result;
            }
            finally
            {
                assassinRoute.Leave();
#if SERP_DEVDIAG
                EndTemporaryRouteReport(pathManager, temporaryRoute, completed, result);
#endif

                EndEnemyGateSearch(gate, gateScope, EnemyGateSearchKind.Builder,
                    completed, result > 0);
                EnemyBridgeDiagnosticBridge.EndSearch(bridgeSearch, completed, result);
#if SERP_DEVDIAG
                GateAttackFreezeCapture.Leave(freeze, result, completed);
#endif
            }
        }

        private AssassinRouteOutputFrame BeginAssassinRoutePublication(IntPtr pathManager)
        {
            // Even an unqualified nested builder must shadow the outer handoff.
            if (nativeManualProbe || pathManager != nativePathManager || nativeUnitManager == null)
                return AssassinPathAPI.BeginRouteOutput(pathManager, 0, 0, 0, 0, 0, null, null);
            byte* manager = (byte*)pathManager.ToPointer();
            byte* path = *(byte**)(manager + PathManagerOutputBufferOffset);
            long offset = path - (nativeUnitManager + NativeUnitPathBufferOffset);
            int unitId = offset > 0 && offset % NativeUnitPathBufferStride == 0 &&
                offset / NativeUnitPathBufferStride <= MaximumUnitCount
                ? (int)(offset / NativeUnitPathBufferStride) : 0;
            int sx = *(int*)(manager + 8), sy = *(int*)(manager + 12);
            int tx = *(int*)(manager + 16), ty = *(int*)(manager + 20);
            if (!UnitAccess.TryGetById(unitId, out GameUnit* unit, out _) ||
                unit == null || unit->r_UnitChimp != eChimps.CHIMP_TYPE_ARAB_ASSASIN)
                return AssassinPathAPI.BeginRouteOutput(pathManager, sx, sy, tx, ty, 0, null, null);
            uint global = unit->r_GlobalId;
            int player = unit->r_ControllableForPlayerId;
            int controlPlayer = unit->r_ControllableForPlayerId;
            int speedDelay = unit->r_CurrentSpeed;
            IntPtr expectedPath = (IntPtr)path;
            Func<bool> valid = () =>
            {
                if (pathManager != nativePathManager)
#if SERP_DEVDIAG
            return TemporaryAssassinPublicationRejected("path-manager");
#else
            return false;
#endif

                if (nativeUnitManager == null)
#if SERP_DEVDIAG
            return TemporaryAssassinPublicationRejected("unit-manager");
#else
            return false;
#endif

                if (!UnitAccess.TryGetById(unitId, out GameUnit* live, out _))
#if SERP_DEVDIAG
            return TemporaryAssassinPublicationRejected("unit-lookup");
#else
            return false;
#endif

                if (!UnitAccess.IsReallyAlive(live))
#if SERP_DEVDIAG
            return TemporaryAssassinPublicationRejected("unit-not-alive");
#else
            return false;
#endif

                if (live->r_GlobalId != global)
#if SERP_DEVDIAG
            return TemporaryAssassinPublicationRejected("unit-global");
#else
            return false;
#endif

                if (live->r_UnitChimp != eChimps.CHIMP_TYPE_ARAB_ASSASIN)
#if SERP_DEVDIAG
            return TemporaryAssassinPublicationRejected("unit-type");
#else
            return false;
#endif

                if (live->r_ControllableForPlayerId != controlPlayer)
#if SERP_DEVDIAG
            return TemporaryAssassinPublicationRejected("unit-control");
#else
            return false;
#endif

                if (live->r_CurrentSpeed != speedDelay)
#if SERP_DEVDIAG
            return TemporaryAssassinPublicationRejected("unit-speed");
#else
            return false;
#endif

                GetNativeMovementStart(live, out int x, out int y);
                byte* m = (byte*)pathManager.ToPointer();
                int length = *(int*)(m + PathManagerOutputLengthOffset);
                if (x != sx || y != sy)
#if SERP_DEVDIAG
            return TemporaryAssassinPublicationRejected("unit-start");
#else
            return false;
#endif

                if (*(int*)(m + 8) != sx || *(int*)(m + 12) != sy)
#if SERP_DEVDIAG
            return TemporaryAssassinPublicationRejected("request-start");
#else
            return false;
#endif

                if (*(int*)(m + 16) != tx || *(int*)(m + 20) != ty)
#if SERP_DEVDIAG
            return TemporaryAssassinPublicationRejected("request-target");
#else
            return false;
#endif

                if (length < 0 || length > 2000)
#if SERP_DEVDIAG
            return TemporaryAssassinPublicationRejected("output-length");
#else
            return false;
#endif

                if (*(int*)(m + 0x88) == 0)
#if SERP_DEVDIAG
            return TemporaryAssassinPublicationRejected("assassin-mode88");
#else
            return false;
#endif

                if (*(int*)(m + 0x84) != 0)
#if SERP_DEVDIAG
            return TemporaryAssassinPublicationRejected("output-mode84");
#else
            return false;
#endif

                // F4930 can replace the Assassin reconstruction with a later normal route when +94 != 0.
                // Keep this exclusion: publishing the staged route would overwrite that native output.
                if (*(int*)(m + 0x94) != 0)
#if SERP_DEVDIAG
            return TemporaryAssassinPublicationRejected("output-mode94");
#else
            return false;
#endif

                if (*(IntPtr*)(m + PathManagerOutputBufferOffset) != expectedPath)
#if SERP_DEVDIAG
            return TemporaryAssassinPublicationRejected("output-pointer");
#else
            return false;
#endif

                if ((byte*)expectedPath != nativeUnitManager + NativeUnitPathBufferOffset + unitId * NativeUnitPathBufferStride)
#if SERP_DEVDIAG
                    return TemporaryAssassinPublicationRejected("unit-buffer");
#else
                    return false;
#endif

                return true;
            };
            return AssassinPathAPI.BeginRouteOutput(pathManager, sx, sy, tx, ty, player, valid, (bytes, count) =>
            {
                // No callback or fallible conversion occurs between the final proof and these data writes.
                byte* destination = (byte*)expectedPath;
                for (int i = 0; i < NativeUnitPathBufferStride; i++)
                    destination[i] = i < bytes.Length ? bytes[i] : (byte)0;
                *(int*)((byte*)pathManager.ToPointer() + PathManagerOutputLengthOffset) = count;
                if (!assassinExactPublicationLogged)
                {
                    assassinExactPublicationLogged = true;
                    try { if (Shared.DebugLogHelper.DiagnosticsEnabled)
                              Shared.DebugLogHelper.LogDebugLimited(log, "BugfixesAndQoL/src/UnitCommands/Movement/MovementPathPublication.cs:116", $"Assassin exact route published after native reconstruction; unit={unitId} global={global} start=({sx},{sy}) target=({tx},{ty}) directions={count}."); }
                    catch { /* Logging cannot turn a published route into a native failure. */ }
                }
                return count;
            }, controlPlayer, speedDelay,
#if SERP_DEVDIAG
                TemporaryGateRouteAcceptanceBridge.CurrentRequested is ITemporaryAssassinGateObserver
                    ? (Action<string, string, string>)((stage, result, detail) => ReportTemporaryAssassinStage(stage, result, detail)) : null
#else
                null
#endif
                );
        }

        internal int BuildPathWithCompletedMoatRouteVariantWithMoat(
            IntPtr pathManager, int movementClass, int movementProfile)
        {
            if (nativeManualProbe) return originalPathBuilder(pathManager, movementClass, movementProfile);
            PlanScope scopedPlan = GetBuilderPlan(pathManager, reportMismatch: true);
            PlanScope workHandoff = GetCurrentUnitMoveFrame()?.InheritedPlan ?? scopedPlan;
            BuilderWeightedScope shadow = TryCaptureBuilderWeightedScope(pathManager, scopedPlan);
            try
            {
                int result = BuildPathWithCompletedMoatRouteVariantCore(
                    pathManager, movementClass, movementProfile, scopedPlan, planResolved: true);
                if (shadow != null)
                {
                    AttackCommandScope measuredCommand = activeAttackCommand;
                    long weightedStarted = (DetailedDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L);
                    bool previousWeightedTiming = weightedPhaseTimingActive;
                    weightedPhaseTimingActive = true;
                    try { ObserveWeightedMoatShadowResult(pathManager, result, shadow); }
                    finally
                    {
                        weightedPhaseTimingActive = previousWeightedTiming;
                        if (measuredCommand != null)
                            measuredCommand.WeightedPhaseTicks +=
                                (DetailedDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L) - weightedStarted;
                    }
                }
                return shadow != null && shadow.PublishedBuilderResult >= 0
                    ? shadow.PublishedBuilderResult
                    : result;
            }
            finally
            {
                if (workHandoff != null && workHandoff.MoatWorkMovement &&
                    ReferenceEquals(pendingPlan, workHandoff))
                {
                    pendingPlan = null;
                }
            }
        }

        internal int BuildPathWithCompletedMoatRouteVariantCore(
            IntPtr pathManager, int movementClass, int movementProfile,
            PlanScope resolvedPlan = null, bool planResolved = false, bool reconstruction = false)
        {
            // Manual command fixes authorize existing native paths. Their builder and
            // reconstruction retain Vanilla's complete path publication contract.
            // The independent fill optimizer runs in the enclosing shadow phase.
            if (!TraversalEnabled)
                return CallVanillaBuilder(pathManager, movementClass, movementProfile, reconstruction);
            return Traversal.BuildPathWithCompletedMoatRouteVariantTraversal(
                pathManager, movementClass, movementProfile, resolvedPlan, planResolved, reconstruction);
        }

        internal int CallVanillaBuilder(IntPtr pathManager, int movementClass, int movementProfile, bool reconstruction) =>
            reconstruction ? EnemyBridgeDiagnosticBridge.NativeResult(originalPathReconstruction(pathManager)) :
                EnemyBridgeDiagnosticBridge.NativeResult(originalPathBuilder(pathManager, movementClass, movementProfile));

        internal PlanScope GetBuilderPlan(IntPtr pathManager, bool reportMismatch = false)
        {
            if (pathManager == IntPtr.Zero || pathManager != nativePathManager ||
                nativeUnitManager == null)
                return null;
            byte* manager = (byte*)pathManager.ToPointer();
            byte* path = *(byte**)(manager + PathManagerOutputBufferOffset);
            int targetX = *(int*)(manager + 0x10);
            int targetY = *(int*)(manager + 0x14);
            UnitMoveFrame frame = GetCurrentUnitMoveFrame();
            if (frame != null)
            {
                PlanScope request = frame.Plan;
                // Non-diggers and calls without a mode callback have no mod plan.
                if (request == null)
                    return null;
                string rejection = null;
                if (request.UnitId != frame.Args.UnitId ||
                    request.UnitId <= 0 || request.UnitId > MaximumUnitCount)
                    rejection = "missing-unit-context";
                else if (path != nativeUnitManager + NativeUnitPathBufferOffset +
                    request.UnitId * NativeUnitPathBufferStride)
                    rejection = "unit-buffer";
                else if (!APIShared.UnitAccess.TryGetById(request.UnitId, out GameUnit* unit, out _) || unit == null)
                    rejection = "missing-unit-context";
                else if (request.IdentityBound && (request.UnitGlobalId != unit->r_GlobalId ||
                    request.PlayerId != unit->r_ControllableForPlayerId)) rejection = "unit-identity-changed";
                else
                {
                    GetNativeMovementStart(unit, out int startX, out int startY);
                    if (*(int*)(manager + 0x08) != startX || *(int*)(manager + 0x0C) != startY)
                        rejection = "start";
                    else if (targetX < 0 || targetX >= MapWidth || targetY < 0 || targetY >= MapWidth)
                        rejection = "target";
                    else if (request.TargetX == targetX && request.TargetY == targetY &&
                        request.RouteStartX == startX && request.RouteStartY == startY)
                        return request;
                    else
                    {
                        // Native MoveHere may route to a region intermediate. Bind a local
                        // plan, never overwrite the unit request or borrow another buffer.
                        PlanScope effective = CopyMovementPlan(request, request.UnitId, targetX, targetY);
                        effective.RouteStartX = startX;
                        effective.RouteStartY = startY;
                        effective.ExactRouteEndpoints = true;
                        try
                        {
                            effective.FriendlyRouteQualified = TryFindRequiredFriendlyCompletedMoatRouteForPlan(
                                effective, out _);
                            if (reportMismatch && activeMoveCommand != null)
                                activeMoveCommand.BuilderIntermediateTargets++;
                            return effective;
                        }
                        catch (Exception ex)
                        {
                            TryLogDiagnosticFailure("builder-endpoint-qualification", ex);
                            rejection = "endpoint-search";
                        }
                    }
                }
                if (reportMismatch)
                    RecordFallbackContractRejection(request, rejection, pathManager);
                return null;
            }
            if (MatchesBuilderPlan(activePlan, path, targetX, targetY))
                return activePlan;
            if (MatchesBuilderPlan(pendingPlan, path, targetX, targetY))
                return pendingPlan;
            long pathOffset = path - (nativeUnitManager + NativeUnitPathBufferOffset);
            if (reportMismatch && (activePlan != null || pendingPlan != null) &&
                pathOffset > 0 && pathOffset <= (long)MaximumUnitCount * NativeUnitPathBufferStride &&
                pathOffset % NativeUnitPathBufferStride == 0)
                RecordFallbackContractRejection(activePlan ?? pendingPlan, "unscoped-plan-mismatch", pathManager);
            return null;
        }

        internal bool MatchesBuilderPlan(PlanScope plan, byte* path, int targetX, int targetY) =>
            plan != null && plan.UnitId > 0 && plan.UnitId <= MaximumUnitCount &&
            plan.TargetX == targetX && plan.TargetY == targetY &&
            path == nativeUnitManager + NativeUnitPathBufferOffset +
                plan.UnitId * NativeUnitPathBufferStride;

        [System.Diagnostics.Conditional("SERP_DEVDIAG")]
        internal void RecordFallbackContractRejection(
            PlanScope plan, string reason = "retry-contract", IntPtr pathManager = default)
        {
#if SERP_DEVDIAG

            fallbackContractRejections++;
            if (activeMoveCommand != null)
            {
                activeMoveCommand.FallbackContractRejections++;
                activeMoveCommand.ContractRejectionReasons.TryGetValue(reason, out int count);
                activeMoveCommand.ContractRejectionReasons[reason] = count + 1;
            }
            if (DetailedDiagnosticsEnabled && fallbackContractRejections <= 12)
            {
                try
                {
                    byte* manager = pathManager == nativePathManager ? (byte*)pathManager.ToPointer() : null;
                    UnitMoveFrame frame = GetCurrentUnitMoveFrame();
                    Shared.DebugLogHelper.LogWarningLimited(log, "BugfixesAndQoL/src/UnitCommands/Movement/MovementPathPublication.cs:540", $"Bugfixes and QoL stage=friendly-moat-movement-fallback-contract-rejected unit={plan?.UnitId ?? 0} " +
                        $"count={fallbackContractRejections} reason={reason} " +
                        $"click=({activeMoveCommand?.TargetX ?? -1},{activeMoveCommand?.TargetY ?? -1}) " +
                        $"request=({frame?.Args.TileX ?? plan?.TargetX ?? -1},{frame?.Args.TileY ?? plan?.TargetY ?? -1}) " +
                        $"builder=({(manager != null ? *(int*)(manager + 0x10) : -1)}," +
                        $"{(manager != null ? *(int*)(manager + 0x14) : -1)}).");
                }
                catch
                {
                    // A diagnostic failure must not escape into the native builder.
                }
            }
#endif
        }

        internal static string FormatContractRejectionReasons(MoveCommandScope command)
        {
            if (command == null || command.ContractRejectionReasons.Count == 0)
                return "none";
            var parts = new List<string>();
            foreach (KeyValuePair<string, int> entry in command.ContractRejectionReasons)
                parts.Add(entry.Key + ":" + entry.Value);
            return string.Join(",", parts);
        }

        internal bool IsCompletedEnemyMoatForPlayer(int playerId, int tileId) =>
            IsValidTileId(tileId) && IsCompletedMoatTile(tileId) &&
            ResolveCompletedMoatRelationship(playerId, tileId) ==
                CompletedMoatRelationship.Enemy;

    }
}
