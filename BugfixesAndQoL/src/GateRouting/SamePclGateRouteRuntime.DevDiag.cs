// TEMP_GATE_MIGRATION_DIAGNOSTICS / TEMP_GATE_ROUTE_ACCEPTANCE / TEMP_MACEMEN_GATE_DIAGNOSTICS:
// development-only (SERP_DEVDIAG) forwarding to the optional order/route observers. Without this
// file the functional runtime registers only the FunctionalGatePolicyAdapter.
using System;
using System.Threading;
using APIShared;

namespace BugfixesAndQoL.GateRouting
{
    internal sealed unsafe partial class SamePclGateRouteRuntime
    {
        private AttackOrderCorrelationDiagnostics attackOrderDiagnostics;
        // TEMP_MACEMEN_GATE_DIAGNOSTICS: no optional API observer interface is exposed.
        internal MacemenCommandDiagnostics MacemenDiagnostics;

        partial void DiagInitialize(object diagnostics) =>
            attackOrderDiagnostics = diagnostics as AttackOrderCorrelationDiagnostics;

        partial void DiagIsDetailed(ref bool detailed) => detailed = attackOrderDiagnostics != null;

        partial void DiagRegionPair(int playerId, int sourceComponentId, int destinationComponentId,
            int queryMode, int vanillaResult, int effectiveResult, string source) =>
            attackOrderDiagnostics?.ObserveRegionPair(playerId, sourceComponentId,
                destinationComponentId, queryMode, vanillaResult, effectiveResult, source);

        partial void DiagAssassinStage(object token, int player, string stage, string result, string detail) =>
            attackOrderDiagnostics?.ObserveTemporaryAssassinStage(token, player, stage, result, detail);

        partial void DiagAssassinDecision(object token, int player, int from, int to, int direction,
            bool prepared, AssassinTransitionKind movement, bool allowed, int gate, uint global, string evidence) =>
            attackOrderDiagnostics?.ObserveTemporaryAssassinDecision(token, player, from, to, direction, prepared, movement, allowed, gate, global, evidence);

        partial void DiagBeginAssassinSearch(ref object token, int startX, int startY, int targetX, int targetY,
            int maximumNodes, int continuation, string nativeState)
        {
            token = attackOrderDiagnostics?.BeginAssassinSearch(publishedPolicy, startX, startY,
                targetX, targetY, maximumNodes, continuation, nativeState);
            // TEMP_GATE_ROUTE_ACCEPTANCE: generation of the existing published mask, not a game query.
            attackOrderDiagnostics?.ObserveTemporaryAssassinStage(token, 0, "policy-entry", "captured",
                "policyGeneration=" + policyGeneration);
        }

        partial void DiagAssassinEdge(object token, int playerId, int fromTile, int toTile, int direction, bool climb) =>
            attackOrderDiagnostics?.ObserveAssassinEdge(token, playerId, fromTile, toTile, direction, climb);

        partial void DiagAssassinPolicyFiltering(object token, int playerId, long ground, long climb) =>
            attackOrderDiagnostics?.ObserveAssassinPolicyFiltering(token, playerId, ground, climb);

        partial void DiagAssassinBuildingSearch(int tribeId, int buildingId, int sourceRegion, int rawSearchPlayer) =>
            attackOrderDiagnostics?.ObserveAssassinBuildingSearch(tribeId, buildingId, sourceRegion, rawSearchPlayer);

        partial void DiagEndAssassinSearch(object token, int playerId, int vanillaResult, int effectiveResult,
            string outcome, bool cacheHit, int routeLength) =>
            attackOrderDiagnostics?.EndAssassinSearch(token, publishedPolicy, playerId,
                vanillaResult, effectiveResult, outcome, cacheHit, routeLength);

        partial void DiagBuildingContext(BuildingSearchPlayerContext context)
        {
            if (context.Failure != null)
                GateAttackFreezeCapture.ScopeFailure(new GateAttackFreezeCapture.Mismatch {
                    Reason = context.Failure, Role = context.ArgumentRole, Tribe = context.TribeId,
                    RawPlayer = context.RawSearchPlayer, SnapshotOwner = context.SnapshotOwner,
                    SnapshotGlobal = context.SnapshotGlobal, LiveOwner = context.LiveOwner, LiveGlobal = context.LiveGlobal,
                    Leader = context.LeaderId, LeaderGlobal = context.LeaderGlobal, Control = context.NativeLeaderControl,
                    Planning = context.PlanningPlayer });
            try { attackOrderDiagnostics?.ObserveBuildingContext(context); }
            catch { CountIntegrityError(ref exceptions); }
        }

        partial void DiagBuildingException(int rawSearchArgument, int tribeId, Exception error)
        {
            try { attackOrderDiagnostics?.ObserveBuildingException(rawSearchArgument, tribeId, error.GetType().Name); }
            catch { /* The failed context remains open even if diagnostics also fail. */ }
        }

        partial void DiagBeginSharedSearch(int playerId, EnemyGateSearchKind kind)
        {
            MacemenDiagnostics?.BeginSearch(playerId, (int)kind);
            try { attackOrderDiagnostics?.BeginNativeSearch(kind.ToString(), playerId); }
            catch { CountIntegrityError(ref exceptions); }
            if (kind == EnemyGateSearchKind.Builder)
            {
                try { attackOrderDiagnostics?.BeginBuilder(); }
                catch { CountIntegrityError(ref exceptions); }
            }
        }

        partial void DiagCompleteSharedSearch(EnemyGateSearchKind kind, int playerId, SearchDiagnosticContext diagnostic,
            bool completed, bool success, long touched, ulong fingerprint)
        {
            GateAttackFreezeCapture.GateEdges(touched);
            MacemenDiagnostics?.EndSearch(completed, success, touched);
            if (kind != EnemyGateSearchKind.Builder) return;
            try { attackOrderDiagnostics?.ObserveBuilder(playerId, diagnostic, completed, success, touched, fingerprint); }
            catch { CountIntegrityError(ref exceptions); }
        }

        partial void DiagEndNativeSearch(EnemyGateSearchKind kind, bool completed, bool success)
        {
            try { attackOrderDiagnostics?.EndNativeSearch(kind.ToString(), completed, success); }
            catch { CountIntegrityError(ref exceptions); }
        }

        // TEMP_GATE_ROUTE_ACCEPTANCE: mirror shared diagnostic scopes without another native call.
        partial void DiagBeginAlternateConsumer(int player)
        {
            try { attackOrderDiagnostics?.BeginNativeSearch("AlternateBuildingConsumer", player); }
            catch { CountIntegrityError(ref exceptions); }
        }

        partial void DiagEndAlternateConsumer(bool completed)
        {
            try { attackOrderDiagnostics?.EndNativeSearch("AlternateBuildingConsumer", completed, false); }
            catch { CountIntegrityError(ref exceptions); }
        }

        partial void DiagScopeMismatch(string source, int nativePlayer, int tribePlayer)
        {
            GateAttackFreezeCapture.ScopeFailure(new GateAttackFreezeCapture.Mismatch {
                Reason = "explicit-player-mismatch", Role = source, RawPlayer = nativePlayer, SnapshotOwner = tribePlayer });
            try { attackOrderDiagnostics?.ObserveScopeMismatch(source, nativePlayer, tribePlayer); }
            catch { CountIntegrityError(ref exceptions); }
        }

        partial void DiagScopeResult(int playerId, QueryKind kind, bool hasResultContract, bool completed, bool success, long touched)
        {
            if (kind == QueryKind.CursorPreview) return;
            try { attackOrderDiagnostics?.ObserveScopeResult(playerId, kind.ToString(), hasResultContract, completed, success, touched); }
            catch { CountIntegrityError(ref exceptions); }
        }
    }
}
