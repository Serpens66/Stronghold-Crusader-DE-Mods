extern alias BugfixesRuntime;
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
using BugfixesRuntime::BugfixesAndQoL.UnitCommands;
using static BugfixesRuntime::BugfixesAndQoL.UnitCommands.UnitCommandPathRuntime;

namespace MoatMove
{
    // Moved from BugfixesAndQoL MovementPathPublication.cs: friendly-moat code runs only with MoatMove.
    internal sealed unsafe partial class FriendlyMoatTraversalProvider
    {
        internal override int BuildPathWithCompletedMoatRouteVariantTraversal(
            IntPtr pathManager, int movementClass, int movementProfile,
            PlanScope resolvedPlan, bool planResolved, bool reconstruction)
        {
            PlanScope plan = planResolved ? resolvedPlan : runtime.GetBuilderPlan(pathManager, reportMismatch: true);
            MoveCommandScope command = activeMoveCommand;
            UnitMoveFrame frame = runtime.GetCurrentUnitMoveFrame();
            if (frame != null && pathManager == runtime.nativePathManager) frame.BuilderReached = true;
            if (plan == null && frame?.Plan != null && pathManager == runtime.nativePathManager && frame.Plan.ModeObserved &&
                !frame.Plan.VanillaModeDetected)
            {
                if (reconstruction) return 0;
                int inheritedMode = *runtime.moatPathMode;
                *runtime.moatPathMode = 0;
                try { return EnemyBridgeDiagnosticBridge.NativeResult(runtime.originalPathBuilder(pathManager, movementClass, movementProfile)); }
                finally { *runtime.moatPathMode = inheritedMode; }
            }
            // Anchor dispatch: units that cannot enter moats have no BugfixesAndQoL plan; their
            // ground route comes from the group's ground-only task instead of Vanilla's search.
            if (plan == null && !reconstruction && !runtime.disposed &&
                TryPublishAnchorGroundWithoutPlan(pathManager, frame, out int anchorPublished))
            { RecordBuilderResult(command, anchorPublished); return anchorPublished; }
            if (plan != null) anchorDecline = "builder:noDiggerOrManager";
            if (runtime.disposed || pathManager != runtime.nativePathManager || plan == null ||
                !APIShared.UnitAccess.TryGetById(plan.UnitId, out GameUnit* unit, out _) ||
                unit == null || !runtime.CanDigMoat(unit))
                return CallVanillaBuilderTimed(pathManager, movementClass, movementProfile, reconstruction, plan?.UnitId ?? frame?.Args.UnitId ?? 0, plan);

            if (command != null) { command.BuilderCalls++; command.BuilderReached = true; }
            runtime.PrepareMovementSearch(plan, unit->r_ControllableForPlayerId);
            runtime.MarkTrackedAttackPipeline(plan.UnitId, AttackPipelineStage.Builder, plan.TargetX, plan.TargetY, false);
            int mode = *runtime.moatPathMode;
            int* variant = (int*)((byte*)pathManager.ToPointer() + PathManagerRouteVariantOffset);
            int originalVariant = *variant;
            bool owned = TryCaptureUnitFallbackPathBuffer(pathManager, plan, unit,
                out byte* path, out int beforeLength, out byte[] backup);
            bool qualified = plan.FriendlyRouteQualified && plan.ModeObserved;
            bool groundReconstruction = false;
            if (runtime.RequiredOnlyMode && reconstruction && owned && (originalVariant == 0 || originalVariant == 1))
            {
                groundReconstruction = TryPrepareGroundReconstruction(plan, unit);

            }
            anchorDecline = reconstruction ? "builder:reconstruction" : qualified ? "builder:qualifiedMoat" : !owned ? "builder:buffer" :
                originalVariant != 0 && originalVariant != 1 ? "builder:variant" : "builder:notRequiredMode";
            // Anchor dispatch, ground decision: the core's ground route replaces Vanilla's search.
            bool anchorGround = runtime.RequiredOnlyMode && !reconstruction && !qualified && owned &&
                (originalVariant == 0 || originalVariant == 1) && TryPrepareAnchorGround(plan, unit);
            if (runtime.RequiredOnlyMode && owned && (qualified || groundReconstruction || anchorGround) && (originalVariant == 0 || originalVariant == 1))
            {
                // 196280 has already bound its real unit buffer. A ready shared field
                // supplies the route without running F4930 again for this member.
                long publishStarted = System.Diagnostics.Stopwatch.GetTimestamp();
                bool replaced = TryReplaceUnsafeFallbackPath(pathManager, path, backup, beforeLength, plan, unit,
                    out int ready, out string publishDetails, requireMoat: !groundReconstruction && !anchorGround);
                RecordAnchorPublish(publishStarted, replaced, anchorGround ? "ground" : groundReconstruction ? "reconstruction" : "qualified", publishDetails);
                if (replaced)
                {
                    // Same terminal publication state as the shared fallback below:
                    // the native consumer latches this mode after the builder returns.
                    *variant = 0; *runtime.moatPathMode = plan.PublishedUsesMoat ? 1 : 0;
                    RequiredRouteMetrics publicationMetrics = command?.Required ?? activeAttackCommand?.Required;
                    if (publicationMetrics != null) publicationMetrics.Published++;
                    try
                    {
                        if (plan.PublishedUsesMoat) StartOrRefreshMoatMoveTracker(plan,
                            new RouteProbeSummary(plan.PlayerId) { RouteFound = true, ReachedWithMoat = true, RouteDistance = ready }, ready);
                    }
                    catch (Exception ex) { runtime.TryLogDiagnosticFailure("fast-publication-tracker", ex); }
                    RecordBuilderResult(command, ready); return ready;
                }
                if (groundReconstruction)
                {
                    RestoreFallbackPathBuffer(pathManager, path, backup, beforeLength);
                    *variant = originalVariant; *runtime.moatPathMode = mode;
                    RecordBuilderResult(command, 0); return 0;
                }
                if (anchorGround)
                {
                    // Rejected anchor route: restore and let Vanilla's builder run unchanged.
                    anchorDecline = "builder:publishReject";
                    RestoreFallbackPathBuffer(pathManager, path, backup, beforeLength);
                    *variant = originalVariant; *runtime.moatPathMode = mode;
                    plan.QualifiedRoute = null;
                }
            }
            int vanilla;
            if (plan.ModeObserved) *runtime.moatPathMode = plan.VanillaModeDetected ? 1 : 0;
            AttackCommandScope measuredBuilderCommand = activeAttackCommand;
            long nativeBuilderStarted = (DetailedDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L);
            try { vanilla = CallVanillaBuilderTimed(pathManager, movementClass, movementProfile, reconstruction, plan.UnitId, plan); }
            finally
            {
                *runtime.moatPathMode = mode;
                if (measuredBuilderCommand != null)
                {
                    measuredBuilderCommand.NativeBuilderCalls++;
                    measuredBuilderCommand.NativeBuilderTicks +=
                        (DetailedDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L) - nativeBuilderStarted;
                }
            }
            RecordVanillaBuilderResult(command, vanilla);
            if (vanilla <= 0 && plan.NativeGroundPrecheck && !plan.QualifiedTerminalRoute.IsValid)
            {
                plan.ExactRouteEndpoints = true;
                try { plan.FriendlyRouteQualified = runtime.TryFindRequiredFriendlyCompletedMoatRouteForPlan(plan, out _); }
                catch (Exception ex)
                {
                    plan.FriendlyRouteQualified = false;
                    runtime.TryLogDiagnosticFailure("late-route-qualification", ex);
                }
                qualified = plan.FriendlyRouteQualified && plan.ModeObserved;
            }

            // E32B0 consumes a previously populated native field. Changing the mode byte
            // does not undo that field, so a mod-qualified positive result needs an audit.
            bool auditRequired = qualified || reconstruction;
            RequiredRouteMetrics requiredMetrics = runtime.RequiredOnlyMode && qualified && !reconstruction
                ? activeMoveCommand?.Required ?? activeAttackCommand?.Required
                : null;
            long requiredPublicationStarted = requiredMetrics != null
                ? (DetailedDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L)
                : 0;
            bool nativeSafe = vanilla > 0 && !auditRequired;
            if (vanilla > 0 && auditRequired && owned)
            {
                try
                {
                    if (requiredMetrics != null) requiredPublicationTimingDepth++;
                    string auditReason;
                    try
                    {
                        nativeSafe = TryAuditFallbackPath(pathManager, path, vanilla, plan, unit, out auditReason);
                    }
                    finally
                    {
                        if (requiredMetrics != null) requiredPublicationTimingDepth--;
                    }
                    if (!nativeSafe)
                    {
                        runtime.RecordFallbackContractRejection(plan, auditReason, pathManager);
                        // An edge/owner change invalidates distances as well as the bytes.
                        runtime.weightedMoatRoutePlanner.SetSearchSession(null, -1, runtime.mapEpoch, CaptureCurrentGameTick());
                        runtime.PrepareMovementSearch(plan, unit->r_ControllableForPlayerId);
                    }
                }
                catch (Exception ex) { runtime.TryLogDiagnosticFailure("native-path-audit", ex); }
            }
            if (nativeSafe)
            {
                if (requiredMetrics != null)
                {
                    requiredMetrics.Published++;
                    requiredMetrics.PublicationTicks +=
                        (DetailedDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L) - requiredPublicationStarted;
                }
                RecordBuilderResult(command, vanilla);
                return vanilla;
            }
            bool allowManaged = qualified || reconstruction;
            if (!allowManaged || !owned || (originalVariant != 0 && originalVariant != 1))
            {
                if (qualified && !owned) runtime.RecordFallbackContractRejection(plan,
                    DescribeFallbackContractFailure(pathManager, plan, unit), pathManager);
                if (auditRequired)
                {
                    if (owned) RestoreFallbackPathBuffer(pathManager, path, backup, beforeLength);
                    *variant = originalVariant; *runtime.moatPathMode = mode;
                    if (command != null) command.FallbackRollbacks++;
                    vanilla = 0;
                }
                RecordBuilderResult(command, vanilla);
                if (requiredMetrics != null)
                {
                    requiredMetrics.Reject("publication-contract-or-route-variant");
                    requiredMetrics.PublicationTicks +=
                        (DetailedDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L) - requiredPublicationStarted;
                }
                return vanilla;
            }

            // No second native flood: reuse the qualified search's destination field.
            // Save the pre-call buffer so an unsafe native reconstruction is never restored.
            int result = 0;
            bool retained = false;
            string publicationDetails = "publication-not-attempted";
            try
            {
                if (command != null) command.FallbackBuilderCalls++;
                if (requiredMetrics != null) requiredPublicationTimingDepth++;
                try
                {
                    retained = TryReplaceUnsafeFallbackPath(pathManager, path, backup, beforeLength,
                        plan, unit, out result, out publicationDetails, requireMoat: !reconstruction);
                }
                finally
                {
                    if (requiredMetrics != null) requiredPublicationTimingDepth--;
                }
                if (retained)
                {
                    if (requiredMetrics != null) requiredMetrics.Published++;
                    *variant = 0;
                    *runtime.moatPathMode = plan.PublishedUsesMoat ? 1 : 0;
                    RouteProbeSummary summary = new RouteProbeSummary(plan.PlayerId)
                        { RouteFound = true, ReachedWithMoat = true, RouteDistance = result };
                    if (plan.PublishedUsesMoat) StartOrRefreshMoatMoveTracker(plan, summary, result);
                }
            }
            catch (Exception ex) { runtime.TryLogDiagnosticFailure("path-publication", ex); }
            finally
            {
                if (!retained)
                {
                    if (requiredMetrics != null)
                        requiredMetrics.Reject(publicationDetails);
                    RestoreFallbackPathBuffer(pathManager, path, backup, beforeLength);
                    *variant = originalVariant; *runtime.moatPathMode = mode; result = 0;
                    if (command != null) command.FallbackRollbacks++;
                }
                if (requiredMetrics != null)
                    requiredMetrics.PublicationTicks +=
                        (DetailedDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L) - requiredPublicationStarted;
                }
            RecordBuilderResult(command, result);
            return result;
        }
        internal bool TryCaptureUnitFallbackPathBuffer(
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
            if (pathManager == IntPtr.Zero || pathManager != runtime.nativePathManager ||
                runtime.nativeUnitManager == null || unit == null || unitId <= 0 || unitId > MaximumUnitCount ||
                !APIShared.UnitAccess.TryGetById(unitId, out GameUnit* actualUnit, out _) || actualUnit != unit ||
                (plan.IdentityBound && (plan.UnitGlobalId != unit->r_GlobalId || plan.PlayerId != unit->r_ControllableForPlayerId)))
            {
                return false;
            }

            byte* manager = (byte*)pathManager.ToPointer();
            path = *(byte**)(manager + PathManagerOutputBufferOffset);
            byte* expected = runtime.nativeUnitManager + NativeUnitPathBufferOffset +
                unitId * NativeUnitPathBufferStride;
            length = *(int*)(manager + PathManagerOutputLengthOffset);
            bool currentTileStart = unit->r_PathPlanStateBitFlags == 0 &&
                unit->r_MovementSubstep == 8;
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

        internal string DescribeFallbackContractFailure(IntPtr pathManager, PlanScope plan, GameUnit* unit)
        {
            if (pathManager == IntPtr.Zero || pathManager != runtime.nativePathManager ||
                runtime.nativeUnitManager == null || plan == null || unit == null ||
                plan.UnitId <= 0 || plan.UnitId > MaximumUnitCount ||
                !APIShared.UnitAccess.TryGetById(plan.UnitId, out GameUnit* actual, out _) || actual != unit)
                return "missing-unit-context";
            byte* manager = (byte*)pathManager.ToPointer();
            if (*(byte**)(manager + PathManagerOutputBufferOffset) != runtime.nativeUnitManager +
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

        // edgesAudited: the route bytes passed the anchor core's live edge audit in this tick and
        // placement revision under a stricter policy (FriendlyOnly/GroundOnly reject every edge
        // AllowEnemyForDiagnostic rejects); only the per-edge traversal check is skipped then.
        internal bool TryAuditFallbackPath(
            IntPtr pathManager,
            byte* path,
            int result,
            PlanScope plan,
            GameUnit* unit,
            out string details, bool edgesAudited = false)
        {
            AttackCommandScope command = activeAttackCommand;
            long started = (DetailedDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L);
            try
            {
                return TryAuditFallbackPathCore(
                    pathManager, path, result, plan, unit, out details, edgesAudited);
            }
            finally
            {
                long elapsed = (DetailedDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L) - started;
                if (command != null)
                {
                    command.AuditCalls++;
                    command.AuditTicks += elapsed;
                    if (weightedPhaseTimingActive)
                        command.WeightedAuditTicks += elapsed;
                }
                if (requiredPublicationTimingDepth > 0)
                {
                    RequiredRouteMetrics required = activeMoveCommand?.Required ?? activeAttackCommand?.Required;
                    if (required != null) required.PublicationAuditTicks += elapsed;
                }
            }
        }

        internal bool TryAuditFallbackPathCore(
            IntPtr pathManager,
            byte* path,
            int result,
            PlanScope plan,
            GameUnit* unit,
            out string details, bool edgesAudited = false)
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
            if (pathManager != runtime.nativePathManager || runtime.nativeUnitManager == null ||
                plan.UnitId <= 0 || plan.UnitId > MaximumUnitCount ||
                !APIShared.UnitAccess.TryGetById(plan.UnitId, out GameUnit* liveUnit, out _) || liveUnit != unit ||
                !APIShared.UnitAccess.IsReallyAlive(unit) ||
                path != runtime.nativeUnitManager + NativeUnitPathBufferOffset + plan.UnitId * NativeUnitPathBufferStride ||
                x != liveStartX || y != liveStartY ||
                (plan.IdentityBound && (plan.UnitGlobalId != unit->r_GlobalId || plan.PlayerId != playerId)) ||
                !GamePlayerManagerAPI.Instance.IsPlayerIdValid(playerId))
                return false;

            // Qualification may be a command cache hit. Edge classifications from a
            // previous unit/player/search must not leak into this live owner audit.
            runtime.weightedMoatRoutePlanner.BeginReachabilityProbe();
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
                if (runtime.IsCompletedMoatTile(tileId))
                {
                    CompletedMoatRelationship relationship =
                        runtime.ResolveCompletedMoatRelationship(playerId, tileId);
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
                    (runtime.IsCompletedEnemyMoatForPlayer(playerId, GameTileManagerAPI.Instance.GetTileId(nextX, y)) ||
                     runtime.IsCompletedEnemyMoatForPlayer(playerId, GameTileManagerAPI.Instance.GetTileId(x, nextY))))
                { details = "enemy-moat-diagonal-corner"; return false; }
                if (!IsValidTileId(nextTileId))
                {
                    details = "native-edge-invalid";
                    return false;
                }
                if (edgesAudited)
                {
                    x = nextX;
                    y = nextY;
                    continue;
                }
                if (!runtime.weightedMoatRoutePlanner.TryGetTraversalEdge(
                        playerId, x, y, tileId, nextX, nextY, nextTileId,
                        direction, nodeIndex == result - 1,
                        // A reserved target concerns only the final edge (once per route, not per step).
                        nodeIndex == result - 1 && runtime.IsPublishedWalkableBuildingApproach(plan.UnitId, nextTileId),
                        MoatTraversalPolicy.AllowEnemyForDiagnostic,
                        out MoatTraversalEdgeKind edgeKind, out _))
                {
                    details = "native-edge-invalid";
                    return false;
                }
                if (edgeKind == MoatTraversalEdgeKind.EnemyMoat &&
                    !runtime.IsCompletedEnemyMoatForPlayer(playerId, tileId) &&
                    !runtime.IsCompletedEnemyMoatForPlayer(playerId, nextTileId))
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
                runtime.TryGetTerminalFillContact(plan, unit, expectedTargetX, expectedTargetY, out int contact) &&
                WeightedMoatRoutePlanner.IsTerminalFillNode(enemyTile, enemyIndex, result, contact);
            details = terminalWorkContact
                ? "vanilla-fill-terminal-contact"
                : "enemy-traversal";
            return terminalWorkContact;
        }

        internal bool TryReplaceUnsafeFallbackPath(
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
            if (targetedRouteProbeBusy || runtime.weightedShadowBusy || pathManager == IntPtr.Zero ||
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
                path != runtime.nativeUnitManager + NativeUnitPathBufferOffset + plan.UnitId * NativeUnitPathBufferStride ||
                startX != liveStartX || startY != liveStartY || targetX != plan.TargetX || targetY != plan.TargetY ||
                (plan.IdentityBound && (plan.UnitGlobalId != unit->r_GlobalId || plan.PlayerId != playerId)))
            { details = "publication-contract-changed"; return false; }
            bool allowReservedTarget = runtime.IsPublishedWalkableBuildingApproach(
                plan.UnitId, GameTileManagerAPI.Instance.GetTileId(targetX, targetY));
            WeightedMoatRouteSummary summary = plan.QualifiedTerminalSummary;
            WeightedMoatEncodedRoute route = plan.QualifiedTerminalRoute;
            QualifiedMovementRoute reusable = runtime.GetReusableQualifiedRoute(plan, unit);
            if (reusable != null) { route = reusable.Route; summary = reusable.Summary; }
            bool edgesAudited = reusable != null && IsAnchorAuditedRoute(reusable);
            targetedRouteProbeBusy = true;
            try
            {
                bool found = route.IsValid;
                if (!found && !runtime.RequiredOnlyMode)
                {
                    found = TryBuildMovementReachabilityEncoded(
                        playerId, startX, startY, targetX, targetY, allowReservedTarget,
                        out summary, out route) || TryBuildTerminalFillRoute(
                        plan, unit, startX, startY, out summary, out route);
                }
                if (!found ||
                    !route.IsValid || (requireMoat && summary.MoatEdges <= 0) ||
                    route.Bytes.Length > NativeUnitPathBufferStride)
                {
                    details = runtime.RequiredOnlyMode && !found
                        ? "qualified-route-unavailable-fast"
                        : $"no-owner-safe-replacement:{summary.Reason}";
                    return false;
                }
            }
            finally
            {
                targetedRouteProbeBusy = false;
            }

            string audit = null;
            try
            {
                for (int index = 0; index < NativeUnitPathBufferStride; index++)
                    path[index] = index < route.Bytes.Length ? route.Bytes[index] : (byte)0;
                *(int*)(manager + PathManagerOutputLengthOffset) = route.DirectionCount;
                if (!TryAuditFallbackPath(
                        pathManager, path, route.DirectionCount, plan, unit,
                        out audit, edgesAudited))
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
                // The audit's own reason when the roundtrip audit rejected the route.
                details = $"replacement-rollback:{(audit != null && ex is InvalidOperationException ? audit + (edgesAudited ? "/edgesAudited" : "/edgesLive") : ex.GetType().Name)}";
                try
                {
                    Shared.DebugLogHelper.LogWarningLimited(runtime.log, "BugfixesAndQoL/src/UnitCommands/Movement/MovementPathPublication.cs:812", $"Bugfixes and QoL stage=friendly-moat-movement-replacement-rollback unit={plan?.UnitId ?? 0} " +
                        $"reason={ex.GetType().Name} bufferRestored=1.");
                }
                catch { }
                return false;
            }
        }

        internal static void RestoreFallbackPathBuffer(
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
