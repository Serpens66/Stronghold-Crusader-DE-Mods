// TEMP_GATE_MIGRATION_DIAGNOSTICS / TEMP_GATE_ROUTE_ACCEPTANCE / TEMP_MACEMEN_GATE_DIAGNOSTICS:
// development-only (SERP_DEVDIAG) attachment of the migration diagnostics. Removing this file
// and the other GateRouting *.DevDiag.cs files removes the complete diagnosis; the functional
// runtime then keeps only its partial declarations, whose calls the compiler drops.
using System;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.EventAPI.Units;

namespace BugfixesAndQoL.GateRouting
{
    internal sealed unsafe partial class EnemyGatePathfindingRuntime
    {
        private TemporaryGateRouteAcceptance temporaryAcceptance;
        private MacemenCommandDiagnostics macemen;
        private AttackOrderCorrelationDiagnostics attackOrderDiagnostics;

        partial void DiagObserveCapture(BuildingCaptureEventArgs args) =>
            temporaryAcceptance?.ObserveCapture(args.BuildingId, args.CapturingPlayerId, args.Phase == EventHookPhase.Post);

        partial void DiagCreate()
        {
            if (macemenDiagnostics)
                macemen = new MacemenCommandDiagnostics(log,
                    () => samePclRouteRuntime?.MacemenDiagnosticSnapshot ?? RouteTilePolicySnapshot.Empty,
                    () => gateAccess);
            if (detailedDiagnostics)
            {
                attackOrderDiagnostics = new AttackOrderCorrelationDiagnostics(log, topologyProvider);
                // TEMP_GATE_ROUTE_ACCEPTANCE: process-rooted through this existing static runtime owner.
                temporaryAcceptance = new TemporaryGateRouteAcceptance(log,
                    () => samePclRouteRuntime?.TemporaryAcceptanceSnapshot ?? RouteTilePolicySnapshot.Empty);
                attackOrderDiagnostics.TemporaryAcceptance = temporaryAcceptance;
            }
        }

        partial void DiagSamePclObserver(ref object observer) => observer = attackOrderDiagnostics;

        partial void DiagAttachSamePcl()
        {
            if (samePclRouteRuntime == null) return;
            samePclRouteRuntime.MacemenDiagnostics = macemen;
            if (temporaryAcceptance != null)
                samePclRouteRuntime.TemporaryPolicyPublished = temporaryAcceptance.ObservePolicyPublication;
        }

        partial void DiagInstallNative(CrusaderLibraryLoadContext context)
        {
            if (!detailedDiagnostics) return;
            APIShared.TemporaryGateRouteAcceptanceBridge.Register(temporaryAcceptance);
            try
            {
                temporaryAcceptance.NativeDiagnostics = new NativeAssassinDiagnosticHooks(context, temporaryAcceptance);
                Shared.DebugLogHelper.LogInfo(log, "TEMP_GATE_ROUTE_ACCEPTANCE native reconstruction probes installed; exact branch/selected-edge evidence enabled with map diagnostics.");
                for (int site = 0; site < NativeAssassinDiagnosticHooks.Sites.Length; site++)
                    Shared.DebugLogHelper.LogInfo(log, "TEMP_GATE_ROUTE_ACCEPTANCE native trace resolution=reference-rva, rva=0x" +
                        NativeAssassinDiagnosticHooks.Sites[site].ToString("X") + ",displaced=" +
                        NativeAssassinDiagnosticHooks.Bytes[site].Length / 2 + ",backend=X64InlineHook,permanent=true");
            }
            catch (Exception error)
            {
                Shared.DebugLogHelper.LogError(log, "TEMP_GATE_ROUTE_ACCEPTANCE native reconstruction coverage unavailable: " + error);
            }
        }

        partial void DiagBeginMap()
        {
            attackOrderDiagnostics?.Reset();
            temporaryAcceptance?.Begin();
            if (temporaryAcceptance?.NativeDiagnostics != null)
                AssassinGatehouseDescentFeature.SetObservers(
                    temporaryAcceptance.NativeDiagnostics.ObserveClosedGate,
                    temporaryAcceptance.NativeDiagnostics.ObserveDescentException);
        }

        partial void DiagBeginEpoch() => macemen?.Begin(topologyProvider?.DiagnosticEpoch ?? 0);

        partial void DiagEndMap()
        {
            // TEMP_GATE_ROUTE_ACCEPTANCE: validate complete AI result attribution against functional counters.
            if (temporaryAcceptance != null && samePclRouteRuntime != null)
            {
                SamePclCoverageSnapshot same = samePclRouteRuntime.GetCoverageSnapshot();
                temporaryAcceptance.CompareBuilderTotals(same.AiQueries, same.AiNegativeWithRejectedEdges);
            }
            temporaryAcceptance?.End();
            macemen?.End();
        }

        partial void DiagDeferred()
        {
            attackOrderDiagnostics?.ProcessDeferred();
            temporaryAcceptance?.Deferred();
            macemen?.Deferred();
        }

        partial void DiagTargetOrder(TribeIssueOrderWithTargetEventArgs args)
        {
            macemen?.TargetOrder(args);
            try { attackOrderDiagnostics?.ObserveOrder(args); }
            catch (Exception ex) { TryLogDiagnosticFailure(ex); }
        }

        partial void DiagTribeMove(TribeIssueOrderMoveHereEventArgs args)
        {
            macemen?.GroupMove(args);
            try { attackOrderDiagnostics?.ObserveTribeMove(args); }
            catch (Exception ex) { TryLogDiagnosticFailure(ex); }
        }

        partial void DiagUnitMove(UnitMoveHereEventArgs args)
        {
            macemen?.UnitMove(args);
            try { attackOrderDiagnostics?.ObserveUnitMove(args); }
            catch (Exception ex) { TryLogDiagnosticFailure(ex); }
        }

        partial void DiagGateComparison(bool builderPrecheck, NativeGateSnapshotDecision decision,
            int queryPlayerId, int buildingId, int nativeOwner, int nativeCaptured,
            bool originalZero, bool finalZero, uint subjectGlobalId)
        {
            if (macemen != null)
                macemen.GateComparison(queryPlayerId, buildingId, subjectGlobalId, originalZero, finalZero,
                    builderPrecheck ? 1 : 0, nativeOwner, nativeCaptured);
            if (!detailedDiagnostics) return;
            try
            {
                int exactGateId = gateAccess.MatchesGateIdentity(buildingId, subjectGlobalId)
                    ? buildingId : 0;
                attackOrderDiagnostics?.ObserveGatePrecheck(queryPlayerId, buildingId,
                    exactGateId,
                    originalZero, finalZero, decision, nativeOwner, nativeCaptured, subjectGlobalId, builderPrecheck);
            }
            catch (Exception ex) { TryLogDiagnosticFailure(ex); }
        }

        partial void DiagDescribeOrderCheckpoint(ref string text)
        {
            if (attackOrderDiagnostics != null) text = attackOrderDiagnostics.DescribeCheckpoint();
        }
    }
}
