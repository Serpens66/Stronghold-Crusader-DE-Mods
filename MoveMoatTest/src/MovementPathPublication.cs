using BepInEx.Logging;
using MonoMod.RuntimeDetour;
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
using Zhuqiaomon.Assembly;

namespace MoveMoatTest
{
    internal sealed unsafe partial class MoveMoatPathTest
    {
        private int BuildPathWithCompletedMoatRouteVariant(
            IntPtr pathManager, int movementClass, int movementProfile)
        {
            PlanScope scopedPlan = GetBuilderPlan(pathManager, reportMismatch: true);
            PlanScope workHandoff = GetCurrentUnitMoveFrame()?.InheritedPlan ?? scopedPlan;
            BuilderWeightedScope shadow = TryCaptureBuilderWeightedScope(pathManager, scopedPlan);
            try
            {
                int result = BuildPathWithCompletedMoatRouteVariantCore(
                    pathManager, movementClass, movementProfile, scopedPlan, planResolved: true);
                if (shadow != null)
                    ObserveWeightedMoatShadowResult(pathManager, result, shadow);
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

        private int BuildPathWithCompletedMoatRouteVariantCore(
            IntPtr pathManager, int movementClass, int movementProfile,
            PlanScope resolvedPlan = null, bool planResolved = false, bool reconstruction = false)
        {
            PlanScope plan = planResolved ? resolvedPlan : GetBuilderPlan(pathManager, reportMismatch: true);
            MoveCommandScope command = activeMoveCommand;
            UnitMoveFrame frame = GetCurrentUnitMoveFrame();
            if (frame != null && pathManager == nativePathManager) frame.BuilderReached = true;
            if (plan == null && frame?.Plan != null && pathManager == nativePathManager && frame.Plan.ModeObserved &&
                !frame.Plan.VanillaModeDetected)
            {
                if (reconstruction) return 0;
                int inheritedMode = *moatPathMode;
                *moatPathMode = 0;
                try { return originalPathBuilder(pathManager, movementClass, movementProfile); }
                finally { *moatPathMode = inheritedMode; }
            }
            if (disposed || pathManager != nativePathManager || plan == null ||
                !GameUnitManagerAPI.Instance.TryGetUnitById(plan.UnitId, out GameUnit* unit) ||
                unit == null || !CanDigMoat(unit))
                return CallVanillaBuilder(pathManager, movementClass, movementProfile, reconstruction);

            if (command != null) { command.BuilderCalls++; command.BuilderReached = true; }
            PrepareMovementSearch(plan, unit->r_ControllableForPlayerId);
            MarkTrackedAttackPipeline(plan.UnitId, AttackPipelineStage.Builder, plan.TargetX, plan.TargetY, false);
            int mode = *moatPathMode;
            int* variant = (int*)((byte*)pathManager.ToPointer() + PathManagerRouteVariantOffset);
            int originalVariant = *variant;
            bool owned = TryCaptureUnitFallbackPathBuffer(pathManager, plan, unit,
                out byte* path, out int beforeLength, out byte[] backup);
            bool qualified = plan.FriendlyRouteQualified && plan.ModeObserved;
            int vanilla;
            if (plan.ModeObserved) *moatPathMode = plan.VanillaModeDetected ? 1 : 0;
            try { vanilla = CallVanillaBuilder(pathManager, movementClass, movementProfile, reconstruction); }
            finally { *moatPathMode = mode; }
            RecordVanillaBuilderResult(command, vanilla);
            if (vanilla <= 0 && plan.NativeGroundPrecheck && !plan.QualifiedTerminalRoute.IsValid)
            {
                plan.ExactRouteEndpoints = true;
                try { plan.FriendlyRouteQualified = TryFindRequiredFriendlyCompletedMoatRouteForPlan(plan, out _); }
                catch (Exception ex)
                {
                    plan.FriendlyRouteQualified = false;
                    TryLogDiagnosticFailure("late-route-qualification", ex);
                }
                qualified = plan.FriendlyRouteQualified && plan.ModeObserved;
            }

            // E32B0 consumes a previously populated native field. Changing the mode byte
            // does not undo that field, so a mod-qualified positive result needs an audit.
            bool auditRequired = qualified || reconstruction;
            bool nativeSafe = vanilla > 0 && !auditRequired;
            if (vanilla > 0 && auditRequired && owned)
            {
                try
                {
                    nativeSafe = TryAuditFallbackPath(pathManager, path, vanilla, plan, unit, out string auditReason);
                    if (!nativeSafe)
                    {
                        RecordFallbackContractRejection(plan, auditReason, pathManager);
                        // An edge/owner change invalidates distances as well as the bytes.
                        weightedMoatRoutePlanner.SetSearchSession(null, -1, mapEpoch, CaptureCurrentGameTick());
                        PrepareMovementSearch(plan, unit->r_ControllableForPlayerId);
                    }
                }
                catch (Exception ex) { TryLogDiagnosticFailure("native-path-audit", ex); }
            }
            if (nativeSafe) { RecordBuilderResult(command, vanilla); return vanilla; }
            bool allowManaged = qualified || reconstruction;
            if (!allowManaged || !owned || (originalVariant != 0 && originalVariant != 1))
            {
                if (qualified && !owned) RecordFallbackContractRejection(plan,
                    DescribeFallbackContractFailure(pathManager, plan, unit), pathManager);
                if (auditRequired)
                {
                    if (owned) RestoreFallbackPathBuffer(pathManager, path, backup, beforeLength);
                    *variant = originalVariant; *moatPathMode = mode;
                    if (command != null) command.FallbackRollbacks++;
                    vanilla = 0;
                }
                RecordBuilderResult(command, vanilla);
                return vanilla;
            }

            // No second native flood: reuse the qualified search's destination field.
            // Save the pre-call buffer so an unsafe native reconstruction is never restored.
            int result = 0;
            bool retained = false;
            try
            {
                if (command != null) command.FallbackBuilderCalls++;
                retained = TryReplaceUnsafeFallbackPath(pathManager, path, backup, beforeLength,
                    plan, unit, out result, out _, requireMoat: !reconstruction);
                if (retained)
                {
                    *variant = 0;
                    *moatPathMode = plan.PublishedUsesMoat ? 1 : 0;
                    RouteProbeSummary summary = new RouteProbeSummary(plan.PlayerId)
                        { RouteFound = true, ReachedWithMoat = true, RouteDistance = result };
                    if (plan.PublishedUsesMoat) StartOrRefreshMoatMoveTracker(plan, summary, result);
                }
            }
            catch (Exception ex) { TryLogDiagnosticFailure("path-publication", ex); }
            finally
            {
                if (!retained)
                {
                    RestoreFallbackPathBuffer(pathManager, path, backup, beforeLength);
                    *variant = originalVariant; *moatPathMode = mode; result = 0;
                    if (command != null) command.FallbackRollbacks++;
                }
            }
            RecordBuilderResult(command, result);
            return result;
        }

        private int CallVanillaBuilder(IntPtr pathManager, int movementClass, int movementProfile, bool reconstruction) =>
            reconstruction ? originalPathReconstruction(pathManager) :
                originalPathBuilder(pathManager, movementClass, movementProfile);
        private bool TryCaptureUnitFallbackPathBuffer(
            IntPtr pathManager,
            PlanScope plan,
            GameUnit* unit,
            out byte* path,
            out int length,
            out byte[] backup)
        {
            path = null;
            length = 0;
            backup = null;
            int unitId = plan?.UnitId ?? 0;
            if (pathManager == IntPtr.Zero || pathManager != nativePathManager ||
                nativeUnitManager == null || unit == null || unitId <= 0 || unitId > MaximumUnitCount ||
                !GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* actualUnit) || actualUnit != unit ||
                (plan.IdentityBound && (plan.UnitGlobalId != unit->r_GlobalId || plan.PlayerId != unit->r_ControllableForPlayerId)))
            {
                return false;
            }

            byte* manager = (byte*)pathManager.ToPointer();
            path = *(byte**)(manager + PathManagerOutputBufferOffset);
            byte* expected = nativeUnitManager + NativeUnitPathBufferOffset +
                unitId * NativeUnitPathBufferStride;
            length = *(int*)(manager + PathManagerOutputLengthOffset);
            bool currentTileStart = unit->r_PathPlanStateBitFlags == 0 &&
                unit->r_MovingRelevant == 8;
            int startX = currentTileStart ? unit->r_CurrentTilePositionX : unit->r_NextTilePositionX2;
            int startY = currentTileStart ? unit->r_CurrentTilePositionY : unit->r_NextTilePositionY2;
            if (path == null || path != expected || length < 0 ||
                length > WeightedMoatRoutePlanner.MaximumRouteEdges ||
                *(int*)(manager + 0x08) != startX || *(int*)(manager + 0x0C) != startY ||
                *(int*)(manager + 0x10) != plan.TargetX ||
                *(int*)(manager + 0x14) != plan.TargetY)
            {
                path = null;
                return false;
            }

            backup = new byte[NativeUnitPathBufferStride];
            Marshal.Copy((IntPtr)path, backup, 0, backup.Length);
            return true;
        }

        private PlanScope GetBuilderPlan(IntPtr pathManager, bool reportMismatch = false)
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
                else if (!GameUnitManagerAPI.Instance.TryGetUnitById(request.UnitId, out GameUnit* unit) || unit == null)
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

        private string DescribeFallbackContractFailure(IntPtr pathManager, PlanScope plan, GameUnit* unit)
        {
            if (pathManager == IntPtr.Zero || pathManager != nativePathManager ||
                nativeUnitManager == null || plan == null || unit == null ||
                plan.UnitId <= 0 || plan.UnitId > MaximumUnitCount ||
                !GameUnitManagerAPI.Instance.TryGetUnitById(plan.UnitId, out GameUnit* actual) || actual != unit)
                return "missing-unit-context";
            byte* manager = (byte*)pathManager.ToPointer();
            if (*(byte**)(manager + PathManagerOutputBufferOffset) != nativeUnitManager +
                NativeUnitPathBufferOffset + plan.UnitId * NativeUnitPathBufferStride)
                return "unit-buffer";
            GetNativeMovementStart(unit, out int startX, out int startY);
            if (*(int*)(manager + 8) != startX || *(int*)(manager + 12) != startY)
                return "start";
            if (*(int*)(manager + 16) != plan.TargetX || *(int*)(manager + 20) != plan.TargetY)
                return "target";
            int length = *(int*)(manager + PathManagerOutputLengthOffset);
            return length < 0 || length > WeightedMoatRoutePlanner.MaximumRouteEdges ? "length" : "retry-contract";
        }

        private bool MatchesBuilderPlan(PlanScope plan, byte* path, int targetX, int targetY) =>
            plan != null && plan.UnitId > 0 && plan.UnitId <= MaximumUnitCount &&
            plan.TargetX == targetX && plan.TargetY == targetY &&
            path == nativeUnitManager + NativeUnitPathBufferOffset +
                plan.UnitId * NativeUnitPathBufferStride;

        private void RecordFallbackContractRejection(
            PlanScope plan, string reason = "retry-contract", IntPtr pathManager = default)
        {
            fallbackContractRejections++;
            if (activeMoveCommand != null)
            {
                activeMoveCommand.FallbackContractRejections++;
                activeMoveCommand.ContractRejectionReasons.TryGetValue(reason, out int count);
                activeMoveCommand.ContractRejectionReasons[reason] = count + 1;
            }
            if (fallbackContractRejections <= 12)
            {
                try
                {
                    byte* manager = pathManager == nativePathManager ? (byte*)pathManager.ToPointer() : null;
                    UnitMoveFrame frame = GetCurrentUnitMoveFrame();
                    Shared.DebugLogHelper.LogWarning(log,
                        $"MoveMoat stage=fallback-contract-rejected unit={plan?.UnitId ?? 0} " +
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
        }

        private static string FormatContractRejectionReasons(MoveCommandScope command)
        {
            if (command == null || command.ContractRejectionReasons.Count == 0)
                return "none";
            var parts = new List<string>();
            foreach (KeyValuePair<string, int> entry in command.ContractRejectionReasons)
                parts.Add(entry.Key + ":" + entry.Value);
            return string.Join(",", parts);
        }

        private bool TryAuditFallbackPath(
            IntPtr pathManager,
            byte* path,
            int result,
            PlanScope plan,
            GameUnit* unit,
            out string details)
        {
            details = "invalid-contract";
            if (pathManager == IntPtr.Zero || path == null || result <= 0 ||
                result > WeightedMoatRoutePlanner.MaximumRouteEdges ||
                plan == null || unit == null)
            {
                return false;
            }

            byte* manager = (byte*)pathManager.ToPointer();
            if (*(byte**)(manager + PathManagerOutputBufferOffset) != path ||
                *(int*)(manager + PathManagerOutputLengthOffset) != result)
                return false;
            int x = *(int*)(manager + 0x08);
            int y = *(int*)(manager + 0x0C);
            int expectedTargetX = *(int*)(manager + 0x10);
            int expectedTargetY = *(int*)(manager + 0x14);
            int playerId = unit->r_ControllableForPlayerId;
            GetNativeMovementStart(unit, out int liveStartX, out int liveStartY);
            if (pathManager != nativePathManager || nativeUnitManager == null ||
                plan.UnitId <= 0 || plan.UnitId > MaximumUnitCount ||
                !GameUnitManagerAPI.Instance.TryGetUnitById(plan.UnitId, out GameUnit* liveUnit) || liveUnit != unit ||
                unit->r_AliveState != AliveState.IsAlive ||
                path != nativeUnitManager + NativeUnitPathBufferOffset + plan.UnitId * NativeUnitPathBufferStride ||
                x != liveStartX || y != liveStartY ||
                (plan.IdentityBound && (plan.UnitGlobalId != unit->r_GlobalId || plan.PlayerId != playerId)) ||
                !GamePlayerManagerAPI.Instance.IsPlayerIdValid(playerId))
                return false;

            // Qualification may be a command cache hit. Edge classifications from a
            // previous unit/player/search must not leak into this live owner audit.
            weightedMoatRoutePlanner.BeginReachabilityProbe();
            int enemyTile = -1, enemyIndex = -1;
            int enemyNodeOccurrences = 0;
            for (int nodeIndex = 0; nodeIndex <= result; nodeIndex++)
            {
                int tileId = GameTileManagerAPI.Instance.GetTileId(x, y);
                if (!IsValidTileId(tileId))
                {
                    details = "invalid-path-tile";
                    return false;
                }
                if (IsCompletedMoatTile(tileId))
                {
                    CompletedMoatRelationship relationship =
                        ResolveCompletedMoatRelationship(playerId, tileId);
                    if (relationship == CompletedMoatRelationship.Enemy)
                    {
                        enemyTile = tileId; enemyIndex = nodeIndex;
                        enemyNodeOccurrences++;
                    }
                    else if (relationship != CompletedMoatRelationship.Friendly)
                    {
                        details = "invalid-moat-owner";
                        return false;
                    }
                }
                if (nodeIndex == result)
                    break;

                int direction = (path[nodeIndex >> 1] >> ((nodeIndex & 1) * 4)) & 0x0F;
                if (direction < 0 || direction >= WeightedMoatRoutePlanner.DirectionX.Length)
                {
                    details = "invalid-direction";
                    return false;
                }
                int nextX = x + WeightedMoatRoutePlanner.DirectionX[direction];
                int nextY = y + WeightedMoatRoutePlanner.DirectionY[direction];
                int nextTileId = GameTileManagerAPI.Instance.GetTileId(nextX, nextY);
                if ((direction & 1) != 0 &&
                    (IsCompletedEnemyMoatForPlayer(playerId, GameTileManagerAPI.Instance.GetTileId(nextX, y)) ||
                     IsCompletedEnemyMoatForPlayer(playerId, GameTileManagerAPI.Instance.GetTileId(x, nextY))))
                { details = "enemy-moat-diagonal-corner"; return false; }
                if (!IsValidTileId(nextTileId) ||
                    !weightedMoatRoutePlanner.TryGetTraversalEdge(
                        playerId, x, y, tileId, nextX, nextY, nextTileId,
                        direction, nodeIndex == result - 1,
                        IsPublishedWalkableBuildingApproach(plan.UnitId, nextTileId),
                        MoatTraversalPolicy.AllowEnemyForDiagnostic,
                        out MoatTraversalEdgeKind edgeKind, out _))
                {
                    details = "native-edge-invalid";
                    return false;
                }
                if (edgeKind == MoatTraversalEdgeKind.EnemyMoat &&
                    !IsCompletedEnemyMoatForPlayer(playerId, tileId) &&
                    !IsCompletedEnemyMoatForPlayer(playerId, nextTileId))
                {
                    details = "enemy-moat-diagonal-corner";
                    return false;
                }
                x = nextX;
                y = nextY;
            }

            if (x != expectedTargetX || y != expectedTargetY ||
                x != plan.TargetX || y != plan.TargetY)
            {
                details = "endpoint-mismatch";
                return false;
            }
            if (enemyNodeOccurrences == 0)
            {
                details = "owner-safe-native";
                return true;
            }

            bool terminalWorkContact = enemyNodeOccurrences == 1 &&
                TryGetTerminalFillContact(plan, unit, expectedTargetX, expectedTargetY, out int contact) &&
                WeightedMoatRoutePlanner.IsTerminalFillNode(enemyTile, enemyIndex, result, contact);
            details = terminalWorkContact
                ? "vanilla-fill-terminal-contact"
                : "enemy-traversal";
            return terminalWorkContact;
        }

        private bool IsCompletedEnemyMoatForPlayer(int playerId, int tileId) =>
            IsValidTileId(tileId) && IsCompletedMoatTile(tileId) &&
            ResolveCompletedMoatRelationship(playerId, tileId) ==
                CompletedMoatRelationship.Enemy;

        private bool TryReplaceUnsafeFallbackPath(
            IntPtr pathManager,
            byte* path,
            byte[] backup,
            int originalLength,
            PlanScope plan,
            GameUnit* unit,
            out int result,
            out string details, bool requireMoat = true)
        {
            result = 0;
            details = "replacement-not-attempted";
            if (targetedRouteProbeBusy || weightedShadowBusy || pathManager == IntPtr.Zero ||
                path == null || backup == null || plan == null || unit == null)
            {
                return false;
            }

            byte* manager = (byte*)pathManager.ToPointer();
            int startX = *(int*)(manager + 0x08);
            int startY = *(int*)(manager + 0x0C);
            int targetX = *(int*)(manager + 0x10);
            int targetY = *(int*)(manager + 0x14);
            int playerId = unit->r_ControllableForPlayerId;
            GetNativeMovementStart(unit, out int liveStartX, out int liveStartY);
            if (*(byte**)(manager + PathManagerOutputBufferOffset) != path ||
                path != nativeUnitManager + NativeUnitPathBufferOffset + plan.UnitId * NativeUnitPathBufferStride ||
                startX != liveStartX || startY != liveStartY || targetX != plan.TargetX || targetY != plan.TargetY ||
                (plan.IdentityBound && (plan.UnitGlobalId != unit->r_GlobalId || plan.PlayerId != playerId)))
            { details = "publication-contract-changed"; return false; }
            bool allowReservedTarget = IsPublishedWalkableBuildingApproach(
                plan.UnitId, GameTileManagerAPI.Instance.GetTileId(targetX, targetY));
            WeightedMoatRouteSummary summary = plan.QualifiedTerminalSummary;
            WeightedMoatEncodedRoute route = plan.QualifiedTerminalRoute;
            targetedRouteProbeBusy = true;
            try
            {
                bool found = route.IsValid || weightedMoatRoutePlanner.TryBuildReachabilityEncoded(
                    playerId, startX, startY, targetX, targetY, allowReservedTarget, out summary, out route) ||
                    TryBuildTerminalFillRoute(plan, unit, startX, startY, out summary, out route);
                if (!found ||
                    !route.IsValid || (requireMoat && summary.MoatEdges <= 0) ||
                    route.Bytes.Length > NativeUnitPathBufferStride)
                {
                    details = $"no-owner-safe-replacement:{summary.Reason}";
                    return false;
                }
            }
            finally
            {
                targetedRouteProbeBusy = false;
            }

            try
            {
                for (int index = 0; index < NativeUnitPathBufferStride; index++)
                    path[index] = index < route.Bytes.Length ? route.Bytes[index] : (byte)0;
                *(int*)(manager + PathManagerOutputLengthOffset) = route.DirectionCount;
                if (!TryAuditFallbackPath(
                        pathManager, path, route.DirectionCount, plan, unit,
                        out string audit))
                {
                    throw new InvalidOperationException(
                        $"Owner-safe replacement failed roundtrip audit: {audit}");
                }
                result = route.DirectionCount;
                plan.PublishedUsesMoat = summary.MoatEdges > 0;
                details = $"length={result} moatEdges={summary.MoatEdges} " +
                    $"expanded={summary.ExpandedNodes} searchMs={summary.SearchMilliseconds:F3}";
                return true;
            }
            catch (Exception ex)
            {
                RestoreFallbackPathBuffer(pathManager, path, backup, originalLength);
                details = $"replacement-rollback:{ex.GetType().Name}";
                return false;
            }
        }

        private static void RestoreFallbackPathBuffer(
            IntPtr pathManager, byte* path, byte[] backup, int originalLength)
        {
            if (pathManager == IntPtr.Zero || path == null || backup == null)
                return;
            Marshal.Copy(backup, 0, (IntPtr)path, backup.Length);
            *(byte**)((byte*)pathManager.ToPointer() + PathManagerOutputBufferOffset) = path;
            *(int*)((byte*)pathManager.ToPointer() + PathManagerOutputLengthOffset) =
                originalLength;
        }

    }
}
