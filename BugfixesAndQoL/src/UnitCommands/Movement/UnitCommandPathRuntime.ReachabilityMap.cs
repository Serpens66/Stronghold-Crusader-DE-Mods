using BepInEx.Logging;
using APIShared;
using RedBird.Backends.NativeX64;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using R3;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
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
    internal sealed unsafe partial class UnitCommandPathRuntime : IDisposable
    {

        // Each fallible reset step is isolated, so a provider, diagnostic or UI failure can
        // never leave command plans and trackers of the previous map alive.
        private void ResetMapStep(string stage, Action step)
        {
            try { step(); }
            catch (Exception ex) { TryLogDiagnosticFailure("map-reset-" + stage, ex); }
        }

        internal void ResetMapState()
        {
            ResetManualGroupAdmission();
            // TEMP_GATE_ROUTE_ACCEPTANCE: maps never retain an unfinished order association.
#if SERP_DEVDIAG
            if (temporaryAssassinScope != null)
                ResetMapStep("temporary-assassin", () => TemporaryGateRouteAcceptanceBridge.ReportFailure("assassin-map-unpaired-order"));
            temporaryAssassinScope = null;
#endif
            manualCommandContexts?.Clear(); targetCommandParents?.Clear();
            moveEventObservers?.Clear(); moveEventDepths?.Clear(); targetEventObservers?.Clear();
#if SERP_DEVDIAG
            ManualProbeCapture.Invalidate(false, false, true);
#endif
            nativeCursorAnswers.Clear(); nativeCursorAnswerTick = int.MinValue;
            ResetMapStep("fast-moat-metrics", LogAndResetFastMoatMetrics);
            ResetMapStep("unit-move-frames", ClearUnitMoveFrames);
            mapEpoch++;
            ResetMapStep("fast-moat-data", InvalidateFastMoatData);
            cursorTopologies.Clear(); noBuilderDetails = 0; preBuilderRejections.Clear();
#if SERP_DEVDIAG
cursorDecisionCounts.Clear();

cursorDecisionDetails.Clear();
#endif

            fillRouteDecisions.Clear(); fillRouteLogTick = -1; fillRouteLogCount = 0; formationOwner = null;
            if (formationRuntime != null) ResetMapStep("formation", formationRuntime.ResetTransientState);
            ResetMapStep("moat-work-target", ResetMoatWorkTargetSelection);
            ResetMapStep("direct-moat-scopes", ResetDirectMoatCommandScopes);
            cacheMapEpoch = -1;
            cacheStartX = -1;
            cacheStartY = -1;
            cachePlayerId = -1;
            cachedTraversedRegionCount = 0;
            cachedReachabilityMapHits = 0;
            cachedRouteSummary = default;
#if SERP_DEVDIAG
            loggedBuildingCursorReachabilityDecisions.Clear();

            lastUnscopedAttackModes.Clear();

            lastAttackCommandCandidates.Clear();
#endif

            trackedAttackUnits.Clear();
            activeMoveCommand = null;
            if (Traversal != null) ResetMapStep("traversal-provider", Traversal.ResetMapState);
            ResetMapStep("deferred-fast-move", ClearDeferredFastMoveScope);
            activePlan = null;
            pendingPlan = null;
            pendingAttackCursorPair = null;
            activeAttackCommand = null;
            activeAttackApproachDiagnostic = null;
            activeBuildingApproachPerformance = null;
            activeBuildingConsumerPerformance = null;
            trackedMoatMoves.Clear();
            requiredBackgroundTrackedUnitIds.Clear();
#if SERP_DEVDIAG
            requiredBackgroundDiagnostics.Clear();

            requiredBackgroundSuppressedDiagnostics.Clear();
#endif

            requiredBackgroundDiagnosticTick = int.MinValue;
            trackedNativeWaypointQueues.Clear();
#if SERP_DEVDIAG
            loggedDiggerDecisions.Clear();

            lastWeightedPublicationDecisionByUnit.Clear();
#endif

        }

    }
}
