using BepInEx.Logging;
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

namespace MoveMoatTest
{
    internal sealed unsafe partial class MoveMoatPathTest
    {
        private BuilderWeightedScope TryCaptureBuilderWeightedScope(IntPtr pathManager, PlanScope builderPlan = null)
        {
            try
            {
                if (disposed || !ExtensionsEnabled || RequiredOnlyMode || weightedShadowBusy || pathManager == IntPtr.Zero ||
                    pathManager != nativePathManager || nativeUnitManager == null)
                    return null;

                byte* manager = (byte*)pathManager.ToPointer();
                fillRouteDecisions.TryGetValue("builder-entry", out long builderEntries);
                fillRouteDecisions["builder-entry"] = builderEntries + 1;
                byte* nativePath = *(byte**)(manager + PathManagerOutputBufferOffset);
                byte* firstUnitPath = nativeUnitManager + NativeUnitPathBufferOffset;
                if (nativePath == null || nativePath < firstUnitPath)
                    return RejectWeightedCapture("output-buffer");

                long pathOffset = nativePath - firstUnitPath;
                if (pathOffset <= 0 || pathOffset % NativeUnitPathBufferStride != 0)
                    return RejectWeightedCapture("output-alignment");
                long unitId64 = pathOffset / NativeUnitPathBufferStride;
                if (unitId64 <= 0 || unitId64 > MaximumUnitCount)
                    return RejectWeightedCapture("unit-slot");
                int unitId = (int)unitId64;
                UnitMoveFrame ownerFrame = GetCurrentUnitMoveFrame();
                PlanScope effectivePlan = builderPlan ?? ownerFrame?.Plan;
                if (ownerFrame != null && ownerFrame.Args.UnitId != unitId) return RejectWeightedCapture("frame-owner");
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
                    unit == null || unit->r_AliveState != AliveState.IsAlive || !CanDigMoat(unit))
                    return null;

                if (ownerFrame?.Plan != null && ownerFrame.Plan.IdentityBound &&
                    ownerFrame.Plan.UnitGlobalId != unit->r_GlobalId) return null;
                int startX = *(int*)(manager + 0x08);
                int startY = *(int*)(manager + 0x0C);
                int targetX = *(int*)(manager + 0x10);
                int targetY = *(int*)(manager + 0x14);
                if (startX < 0 || startX >= MapWidth || startY < 0 || startY >= MapWidth ||
                    targetX < 0 || targetX >= MapWidth || targetY < 0 || targetY >= MapWidth ||
                    (startX == targetX && startY == targetY))
                    return null;

                // Exact 0x196280 start selection: unrelated shared-builder calls fail closed.
                bool vanillaUsesCurrentTile = unit->r_PathPlanStateBitFlags == 0 &&
                    unit->r_MovingRelevant == 8;
                int expectedStartX = vanillaUsesCurrentTile
                    ? unit->r_CurrentTilePositionX
                    : unit->r_NextTilePositionX2;
                int expectedStartY = vanillaUsesCurrentTile
                    ? unit->r_CurrentTilePositionY
                    : unit->r_NextTilePositionY2;
                if (startX != expectedStartX || startY != expectedStartY)
                    return RejectWeightedCapture("native-start");

                int playerId = unit->r_ControllableForPlayerId;
                WeightedMovementCostProfile costProfile = default;
                bool validPlayer = GamePlayerManagerAPI.Instance.IsPlayerIdValid(playerId);
                bool validProfile = validPlayer && TryCaptureWeightedMovementCostProfile(
                    unit, out costProfile, out _);
                if (!validProfile)
                {
                    return RejectWeightedCapture("cost-profile");
                }

                int targetTileId = GameTileManagerAPI.Instance.GetTileId(targetX, targetY);
                bool allowReservedTarget = IsPublishedWalkableBuildingApproach(unitId, targetTileId);
                ResolveCommandDiagnosticContext(
                    unitId, unit, out TribeAICommand command, out string commandContext,
                    out int commandSequence);
                uint rawCommand = unchecked((uint)unit->r_AI_LastIssuedTribeCommand);
                string workKind = rawCommand == (uint)TribeAICommand.DigMoatTileId
                    ? "dig-moat-work"
                    : rawCommand == (uint)TribeAICommand.Unknown7
                        ? "fill-moat-work"
                        : "not-moat-work";
                string workPhase = rawCommand == (uint)TribeAICommand.DigMoatTileId ||
                    rawCommand == (uint)TribeAICommand.Unknown7
                    ? activeMoveCommand?.IsNewOrder == true
                        ? "initial-command"
                        : "automatic-follow-up"
                    : "not-applicable";
                bool calibratable = (activePlan != null && activePlan.UnitId == unitId &&
                    activePlan.PostCombatRepath) ||
                    IsIsolatedActiveGroupUnit(unitId, unit->r_TribeId);
                return new BuilderWeightedScope(
                    mapEpoch,
                    unitId,
                    unit->r_UnitChimp,
                    playerId,
                    unit->r_TribeId,
                    command,
                    commandContext,
                    commandSequence,
                    startX,
                    startY,
                    targetX,
                    targetY,
                    unit->r_CurrentTilePositionX,
                    unit->r_CurrentTilePositionY,
                    unchecked((uint)unit->r_AIState),
                    rawCommand,
                    workKind,
                    workPhase,
                    costProfile,
                    allowReservedTarget,
                    calibratable) { UnitGlobalId = unit->r_GlobalId,
                        FillPlan = TryGetTerminalFillContact(effectivePlan, unit, targetX, targetY, out _)
                            ? CopyMovementPlan(effectivePlan, unitId, targetX, targetY) : null };
            }
            catch (Exception ex)
            {
                TryLogDiagnosticFailure("weighted-builder-capture", ex);
                return null;
            }
        }

        private bool ObserveWeightedMoatShadowResult(
            IntPtr pathManager, int builderResult, BuilderWeightedScope shadow)
        {
            try
            {
                if (shadow.MapEpoch != mapEpoch || pathManager == IntPtr.Zero ||
                    pathManager != nativePathManager)
                {
                    return false;
                }

                bool snapshotAvailable = GameUnitManagerAPI.Instance.TryGetUnitById(
                    shadow.UnitId, out GameUnit* snapshotUnit) && snapshotUnit != null &&
                    snapshotUnit->r_AliveState == AliveState.IsAlive;
                bool identityMatches = snapshotAvailable && snapshotUnit->r_GlobalId == shadow.UnitGlobalId && CanDigMoat(snapshotUnit) &&
                    snapshotUnit->r_UnitChimp == shadow.UnitType &&
                    snapshotUnit->r_ControllableForPlayerId == shadow.PlayerId &&
                    snapshotUnit->r_TribeId == shadow.TribeId;
                bool snapshotPositionMatches = identityMatches &&
                    snapshotUnit->r_CurrentTilePositionX == shadow.SnapshotCurrentX &&
                    snapshotUnit->r_CurrentTilePositionY == shadow.SnapshotCurrentY;
                WeightedMovementCostProfile currentCostProfile = default;
                bool currentProfileValid = snapshotPositionMatches &&
                    TryCaptureWeightedMovementCostProfile(
                        snapshotUnit, out currentCostProfile, out _);
                if (!currentProfileValid || !currentCostProfile.Equals(shadow.CostProfile))
                {
                    LogWeightedShadowDecision(shadow, "no-valid-shadow-route");
                    return true;
                }

                byte* manager = (byte*)pathManager.ToPointer();
                int builderStartX = *(int*)(manager + 0x08);
                int builderStartY = *(int*)(manager + 0x0C);
                int builderTargetX = *(int*)(manager + 0x10);
                int builderTargetY = *(int*)(manager + 0x14);
                int nativeLength = *(int*)(manager + PathManagerOutputLengthOffset);
                byte* nativePath = *(byte**)(manager + PathManagerOutputBufferOffset);
                byte* expectedPath = nativeUnitManager + NativeUnitPathBufferOffset +
                    shadow.UnitId * NativeUnitPathBufferStride;
                bool publishedToUnit = nativePath == expectedPath;
                bool vanillaStillUsesCurrentTile =
                    snapshotUnit->r_PathPlanStateBitFlags == 0 &&
                    snapshotUnit->r_MovingRelevant == 8;
                int revalidatedStartX = vanillaStillUsesCurrentTile
                    ? snapshotUnit->r_CurrentTilePositionX
                    : snapshotUnit->r_NextTilePositionX2;
                int revalidatedStartY = vanillaStillUsesCurrentTile
                    ? snapshotUnit->r_CurrentTilePositionY
                    : snapshotUnit->r_NextTilePositionY2;
                if (!publishedToUnit || builderStartX != shadow.StartX ||
                    builderStartY != shadow.StartY || builderTargetX != shadow.TargetX ||
                    builderTargetY != shadow.TargetY || revalidatedStartX != shadow.StartX ||
                    revalidatedStartY != shadow.StartY)
                {
                    LogWeightedShadowDecision(shadow, "no-valid-shadow-route");
                    return false;
                }

                WeightedMoatRouteSummary nativeSummary = default;
                bool nativeValid = false;
                if (!weightedShadowBusy && builderResult > 0 && nativeLength == builderResult &&
                    nativeLength <= WeightedMoatRoutePlanner.MaximumRouteEdges &&
                    nativePath != null)
                {
                    weightedShadowBusy = true;
                    try
                    {
                        nativeValid = DescribeWeightedRoute(shadow, nativePath, nativeLength, out nativeSummary, comparisonOnly: true);
                    }
                    finally
                    {
                        weightedShadowBusy = false;
                    }
                }

                if (!nativeValid)
                {
                    if (shadow != null)
                    {
                        if (shadow.WorkKind == "fill-moat-work" && shadow.FillPlan == null)
                            RecordFillRouteDecision(shadow, "fill-context-missing");
                        string reason = nativeSummary.Reason ?? "native-builder-failed";
                        LogWeightedShadowDecision(shadow, reason.StartsWith("native-endpoint-mismatch", StringComparison.Ordinal)
                            ? "native-endpoint-mismatch" : reason);
                    }
                    return true;
                }

                if (weightedMoatRoutePlanner.LastRejectedEdge >= 0)
                    RecordFillRouteDecision(shadow, "native-cost-only-traversal-differs");

                int minimumEdges = Math.Max(
                    Math.Abs(shadow.TargetX - shadow.StartX),
                    Math.Abs(shadow.TargetY - shadow.StartY));
                shadow.OptimisticLowerBoundTicks = shadow.CostProfile.EstimateRouteTicks(
                    Math.Max(0, minimumEdges - 1), 1);
                bool couldMeetMargin = shadow.OptimisticLowerBoundTicks != long.MaxValue &&
                    nativeSummary.EstimatedTicks >= shadow.OptimisticLowerBoundTicks &&
                    nativeSummary.EstimatedTicks - shadow.OptimisticLowerBoundTicks >=
                        WeightedPublicationSafetyMarginTicks;
                QualifiedMovementRoute qualified = GetReusableQualifiedRoute(
                    GetCurrentUnitMoveFrame()?.Plan ?? activePlan ?? pendingPlan, snapshotUnit);
                bool alreadyOptimal = qualified != null && qualified.Optimal && qualified.Profile.Equals(shadow.CostProfile) &&
                    qualified.Route.DirectionCount == nativeLength;
                if (alreadyOptimal)
                    for (int i=0;i<qualified.Route.Bytes.Length;i++)
                        if (qualified.Route.Bytes[i] != nativePath[i]) { alreadyOptimal=false; break; }
                string decision = nativeSummary.MoatEdges > 0 ? "native-friendly-moat" : "native-ground";
                int effectiveBuilderResult = builderResult;
                string publicationDetails = alreadyOptimal ? "qualified-optimal" : "cost-lower-bound";
                if (!alreadyOptimal && couldMeetMargin && builderResult > 0 && nativeValid && publishedToUnit &&
                    TryPublishSafelyFasterWeightedRoute(
                        pathManager,
                        nativePath,
                        nativeLength,
                        shadow,
                        nativeSummary,
                        out WeightedMoatRouteSummary publishedSummary,
                        out long guaranteedSaving,
                        out string cadenceProfiles,
                        out publicationDetails))
                {
                    effectiveBuilderResult = shadow.PublishedBuilderResult;
                    shadow.CandidateFound = true;
                    shadow.Candidate = publishedSummary;
                    decision = "weighted-path-published";
                    int consumerModeBefore = *moatPathMode;
                    // 0x196280 persists this global into unit+0x9C8 immediately after
                    // 0xF4930 returns, then clears it. Without that marker 0x1855A0/
                    // 0xDCE60 reject the first moat edge and rebuild the ground detour.
                    *moatPathMode = 1;
                    if (activeMoveCommand == null || activeMoveCommand.WeightedPublished < 3)
                    LogWeightedPublicationDecision(
                        shadow.UnitId,
                        $"MoveMoat stage=weighted-path-published captureSource={shadow.CaptureSource} " +
                        $"work={shadow.WorkKind} workPhase={shadow.WorkPhase} " +
                        $"unit={shadow.UnitId} aiState={shadow.AiState} " +
                        $"type={shadow.UnitType} commandSeq={shadow.CommandSequence} " +
                        $"command={shadow.Command} " +
                        $"start=({shadow.StartX},{shadow.StartY}) " +
                        $"target=({shadow.TargetX},{shadow.TargetY}) " +
                        $"commandContext={shadow.CommandContext} handlerProfiles={cadenceProfiles} " +
                        $"length={publishedSummary.RouteLength} ground={publishedSummary.GroundEdges} " +
                        $"moat={publishedSummary.MoatEdges} " +
                        $"structure={publishedSummary.StructuralEdges} " +
                        $"diagonal={publishedSummary.DiagonalEdges} " +
                        $"fingerprint=0x{publishedSummary.RouteFingerprint:X16} " +
                        $"guaranteedSavingTicks={guaranteedSaving} " +
                        $"profileCosts={publicationDetails} " +
                        $"searchMsTotal={shadow.AccumulatedSearchMilliseconds:F3} " +
                        $"searchPasses={shadow.SearchPasses} " +
                        $"roundtrip=True pathBuffer=unit " +
                        $"consumerMode={consumerModeBefore}->1 " +
                        "persistentUnitMode=deferred-to-0x196280.");
                }

                bool moatRelevantDiagnostic = shadow.WorkKind != "not-moat-work" ||
                    nativeSummary.MoatEdges > 0 ||
                    (shadow.CandidateFound && shadow.Candidate.MoatEdges > 0);
                // Every priced request has an outcome, even if no moat candidate was found.
                if (shadow != null)
                {
                    if (decision != "weighted-path-published") RecordFillRouteDecision(shadow, publicationDetails);
                    LogWeightedShadowDecision(shadow, decision);
                }
                if (moatRelevantDiagnostic && effectiveBuilderResult > 0 && publishedToUnit)
                {
                    StartOrRefreshWeightedShadowTracker(
                        shadow, effectiveBuilderResult, nativeValid ? nativeSummary : default,
                        nativeValid, decision);
                }
                return true;
            }
            catch (Exception ex)
            {
                TryLogDiagnosticFailure("weighted-shadow-result", ex);
                return true;
            }
        }

        private bool TryPublishSafelyFasterWeightedRoute(
            IntPtr pathManager, byte* nativePath, int nativeLength, BuilderWeightedScope shadow,
            WeightedMoatRouteSummary nativeSummary,
            out WeightedMoatRouteSummary publishedSummary, out long guaranteedSaving,
            out string cadenceProfiles, out string rejectionReason)
        {
            publishedSummary = default; guaranteedSaving = long.MinValue;
            cadenceProfiles = "none"; rejectionReason = "publication-not-evaluated";
            if (weightedShadowBusy || pathManager != nativePathManager || nativePath == null ||
                nativeLength <= 0 || nativeLength > WeightedMoatRoutePlanner.MaximumRouteEdges ||
                nativeSummary.StructuralEdges > 0) return false;
            if (!nativeMovementCadenceResolver.TryGetPlausibleSpeedBonuses((int)shadow.UnitType,
                shadow.CostProfile.SpeedBonus, out int[] bonuses, out ulong handlerRva, out rejectionReason)) return false;
            var profiles = new List<WeightedMovementCostProfile> { shadow.CostProfile };
            foreach (int bonus in bonuses)
            {
                if (!shadow.CostProfile.TryWithSpeedBonus(bonus, out WeightedMovementCostProfile p, out rejectionReason)) return false;
                if (!profiles.Contains(p)) profiles.Add(p);
            }
            var limits = new MoatSearchLimit[profiles.Count];
            for (int i = 0; i < profiles.Count; i++)
            {
                WeightedMovementCostProfile p = profiles[i];
                long ticks = p.EstimateRouteTicks(nativeSummary.GroundEdges, nativeSummary.MoatEdges);
                long allowed = ticks - (p.Equals(shadow.CostProfile) ? WeightedPublicationSafetyMarginTicks : 1);
                if (allowed < 0) return false;
                // ceil(cost / progress) <= allowed iff cost <= allowed * progress.
                long maximum = allowed > long.MaxValue / p.CadenceProgress ? long.MaxValue : allowed * p.CadenceProgress;
                limits[i] = new MoatSearchLimit(p.GetEdgeFixedCost(false), p.GetEdgeFixedCost(true), maximum);
            }
            PrepareMovementSearch(GetCurrentUnitMoveFrame()?.Plan ?? activePlan ?? pendingPlan, shadow.PlayerId);
            weightedShadowBusy = true;
            try
            {
                long runsBefore = weightedMoatRoutePlanner.SearchRuns;
                GameUnitManagerAPI.Instance.TryGetUnitById(shadow.UnitId, out GameUnit* candidateUnit);
                QualifiedMovementRoute qualified = GetReusableQualifiedRoute(
                    GetCurrentUnitMoveFrame()?.Plan ?? activePlan ?? pendingPlan, candidateUnit);
                bool reuse = qualified != null && qualified.Optimal && qualified.Profile.Equals(shadow.CostProfile) &&
                    qualified.Summary.StructuralEdges == 0 && qualified.Summary.MoatEdges > 0;
                if (reuse) foreach (MoatSearchLimit limit in limits)
                    if (!limit.Allows(qualified.Summary.GroundEdges, qualified.Summary.MoatEdges, 0)) { reuse=false; break; }
                WeightedMoatRouteSummary candidate = default;
                WeightedMoatEncodedRoute route = default;
                if (reuse) { candidate=qualified.Summary;route=qualified.Route;shadow.CandidateFound=true; }
                else shadow.CandidateFound = weightedMoatRoutePlanner.TryBuildImprovement(shadow.PlayerId,
                    shadow.StartX, shadow.StartY, shadow.TargetX, shadow.TargetY, shadow.CostProfile,
                    shadow.AllowReservedTarget, limits, out candidate, out route);
                if (!reuse) shadow.AccumulatedSearchMilliseconds += candidate.SearchMilliseconds;
                if (TryImproveFillPrefix(shadow, nativePath, nativeLength, limits,
                    out WeightedMoatRouteSummary terminal, out WeightedMoatEncodedRoute terminalRoute) &&
                    (!shadow.CandidateFound || terminal.EstimatedTicks < candidate.EstimatedTicks))
                { candidate = terminal; route = terminalRoute; shadow.CandidateFound = true; }
                shadow.Candidate = candidate; shadow.CandidateRoute = route;
                shadow.SearchPasses += (int)(weightedMoatRoutePlanner.SearchRuns - runsBefore);
                if (!shadow.CandidateFound || !route.IsValid || candidate.MoatEdges == 0 || candidate.StructuralEdges != 0)
                { rejectionReason = candidate.Reason; return false; }
                guaranteedSaving = long.MaxValue;
                foreach (WeightedMovementCostProfile p in profiles)
                {
                    long saving = p.EstimateRouteTicks(nativeSummary.GroundEdges, nativeSummary.MoatEdges) -
                        p.EstimateRouteTicks(candidate.GroundEdges, candidate.MoatEdges);
                    if (saving <= 0 || (p.Equals(shadow.CostProfile) && saving < WeightedPublicationSafetyMarginTicks)) return false;
                    guaranteedSaving = Math.Min(guaranteedSaving, saving);
                }
                byte* manager = (byte*)pathManager.ToPointer();
                if (*(byte**)(manager + PathManagerOutputBufferOffset) != nativePath ||
                    *(int*)(manager + PathManagerOutputLengthOffset) != nativeLength ||
                    nativePath != nativeUnitManager + NativeUnitPathBufferOffset + shadow.UnitId * NativeUnitPathBufferStride ||
                    !GameUnitManagerAPI.Instance.TryGetUnitById(shadow.UnitId, out GameUnit* unit) || unit == null ||
                    unit->r_GlobalId != shadow.UnitGlobalId || unit->r_ControllableForPlayerId != shadow.PlayerId)
                { rejectionReason = "publication-owner-changed"; return false; }
                GetNativeMovementStart(unit, out int startX, out int startY);
                if (startX != shadow.StartX || startY != shadow.StartY ||
                    *(int*)(manager + 0x10) != shadow.TargetX || *(int*)(manager + 0x14) != shadow.TargetY)
                { rejectionReason = "publication-context-changed"; return false; }
                int count = Math.Max((nativeLength + 1) >> 1, route.Bytes.Length);
                if (count > NativeUnitPathBufferStride) return false;
                byte[] backup = new byte[count];
                Marshal.Copy((IntPtr)nativePath, backup, 0, count);
                try
                {
                    for (int i = 0; i < count; i++) nativePath[i] = i < route.Bytes.Length ? route.Bytes[i] : (byte)0;
                    *(int*)(manager + PathManagerOutputLengthOffset) = route.DirectionCount;
                    // One live edge/owner audit after writing, independent of profile count.
                    if (!DescribeWeightedRoute(shadow, nativePath, route.DirectionCount,
                        out WeightedMoatRouteSummary verified) ||
                        verified.RouteFingerprint != candidate.RouteFingerprint)
                        throw new InvalidOperationException("Published route failed live roundtrip");
                    for (int i = 0; i < route.Bytes.Length; i++)
                        if (nativePath[i] != route.Bytes[i]) throw new InvalidOperationException("Published bytes changed");
                }
                catch
                {
                    Marshal.Copy(backup, 0, (IntPtr)nativePath, count);
                    *(int*)(manager + PathManagerOutputLengthOffset) = nativeLength;
                    throw;
                }
                shadow.PublishedBuilderResult = route.DirectionCount;
                publishedSummary = candidate;
                cadenceProfiles = $"rva-0x{handlerRva:X}:profiles={profiles.Count}";
                rejectionReason = "all-profile-cost-bounds-and-live-audit";
                return true;
            }
            finally { weightedShadowBusy = false; }
        }
    }
}
