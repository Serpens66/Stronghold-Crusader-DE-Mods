// TEMP_GATE_ROUTE_ACCEPTANCE: remove this partial and its builder, building and paired order attachment points after acceptance.
using APIShared;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
namespace BugfixesAndQoL.UnitCommands
{
    internal sealed unsafe partial class UnitCommandPathRuntime
    {
        [ThreadStatic] internal static TemporaryRouteReport temporaryRouteCall;
        internal sealed class TemporaryRouteReport
        {
            internal ITemporaryGateRouteAcceptanceObserver Observer;
            internal object Token;
            internal int Unit, Player, X, Y, TX, TY, Epoch, Tribe;
            internal uint Global, TribeGlobal;
            internal UnitMoveFrame Frame;
            internal TemporaryRouteReport Previous;
            internal IntPtr Manager;
            internal string Source, FirstSearch, LastSearch;
            internal long Searches, TargetSearches, Floods, Continuations, WeightedFields;
            internal int Flags84, Flags88;
            internal string Relaxation;
        }
        internal sealed class TemporaryAssassinCall
        {
            internal TemporaryRouteReport Report;
            internal TemporaryAssassinScope Scope;
            internal int X, Y, TX, TY, Continuation;
        }
        // TEMP_GATE_ROUTE_ACCEPTANCE: synchronous provenance; no cross-call target history.
        [ThreadStatic] private static TemporaryAssassinScope temporaryAssassinScope;
        [ThreadStatic] private static long temporaryAssassinSequence;
        internal sealed class TemporaryAssassinScope
        {
            internal TemporaryAssassinScope Previous;
            internal string Source, First, Last;
            internal int Tribe, Command, X, Y, Player;
            internal bool MixedPlayers;
            internal long Sequence, Searches;
        }
        internal static object BeginTemporaryAssassinScope(string source, int tribe, int command, int x, int y)
        {
            if (!(TemporaryGateRouteAcceptanceBridge.Current is ITemporaryAssassinGateObserver)) return null;
            var scope = new TemporaryAssassinScope { Previous = temporaryAssassinScope, Source = source,
                Tribe = tribe, Command = command, X = x, Y = y, Sequence = ++temporaryAssassinSequence };
            temporaryAssassinScope = scope;
            return scope;
        }
        internal static void EndTemporaryAssassinScope(object token, bool completed, long result)
        {
            if (!(token is TemporaryAssassinScope scope)) return;
            try
            {
                if (!ReferenceEquals(scope, temporaryAssassinScope))
                { TemporaryGateRouteAcceptanceBridge.ReportFailure("assassin-scope-pair"); return; }
                if (scope.Searches > 0) ReportTemporaryAssassinStage("synchronous-order", completed ? "completed" : "incomplete",
                    "source=" + scope.Source + ",sequence=" + scope.Sequence + ",tribe=" + scope.Tribe +
                    "," + DescribeTemporaryAssassinArguments(scope) + ",return=" + (scope.Source == "building-query" ? "void" : result.ToString()) +
                    ",searches=" + scope.Searches + ",mixedPlayers=" + scope.MixedPlayers + ",first=[" + scope.First + "],last=[" + scope.Last + "]", scope.Player);
            }
            finally { temporaryAssassinScope = scope.Previous; }
        }
        internal static string CaptureTemporaryAssassinSource()
        {
            if (!(TemporaryGateRouteAcceptanceBridge.Current is ITemporaryAssassinGateObserver)) return "observer-inactive";
            // A nested building query is more specific than its enclosing unit/order frame.
            if (temporaryAssassinScope?.Source == "building-query") return DescribeTemporaryAssassinScope();
            if (temporaryRouteCall?.Token != null) return "source=single-unit,unit=" + temporaryRouteCall.Unit +
                ",unitGlobal=" + temporaryRouteCall.Global + ",tribe=" + temporaryRouteCall.Tribe +
                ",tribeGlobal=" + temporaryRouteCall.TribeGlobal + ",builder=" + temporaryRouteCall.Source;
            return temporaryAssassinScope == null ? "source=unknown" : DescribeTemporaryAssassinScope();
        }
        private static string DescribeTemporaryAssassinScope() => "source=" + temporaryAssassinScope.Source +
            ",sequence=" + temporaryAssassinScope.Sequence + ",tribe=" + temporaryAssassinScope.Tribe +
            "," + DescribeTemporaryAssassinArguments(temporaryAssassinScope);
        private static string DescribeTemporaryAssassinArguments(TemporaryAssassinScope scope) => scope.Source == "building-query"
            ? "rawSearchPlayer=" + scope.Command + ",building=" + scope.X + ",sourcePcl=" + scope.Y
            : (scope.Source == "group-move" ? "moveTypeRaw=" : "commandRaw=") + scope.Command + ",target=" + scope.X + "/" + scope.Y;
        internal static void ObserveTemporaryAssassinOrderPhase(bool pre, string source, int tribe, int command, int x, int y, long result)
        {
            if (!(TemporaryGateRouteAcceptanceBridge.Current is ITemporaryAssassinGateObserver)) return;
            try
            {
                if (pre) BeginTemporaryAssassinScope(source, tribe, command, x, y);
                else if (temporaryAssassinScope?.Source == source && temporaryAssassinScope.Tribe == tribe)
                    EndTemporaryAssassinScope(temporaryAssassinScope, true, result);
                else TemporaryGateRouteAcceptanceBridge.ReportFailure("assassin-order-post-without-pre");
            }
            catch (Exception error) { TemporaryGateRouteAcceptanceBridge.ReportFailure("assassin-order-context", error); }
        }
        internal static void ReportTemporaryAssassinStage(string stage, string result, string detail, int? player = null)
        {
            if (!(TemporaryGateRouteAcceptanceBridge.Current is ITemporaryAssassinGateObserver observer)) return;
            try { observer.ObserveAssassinStage(temporaryRouteCall?.Token, player ?? temporaryRouteCall?.Player ?? 0,
                stage, result, detail); }
            catch (Exception error) { TemporaryGateRouteAcceptanceBridge.ReportFailure("assassin-stage", error); }
        }
        // These counters describe only nested synchronous calls, never provenance of an older field.
        internal static object BeginTemporaryAssassinSearch(IntPtr manager, int x, int y, int tx, int ty, int continuation)
        {
            TemporaryRouteReport report = temporaryRouteCall;
            if (!(TemporaryGateRouteAcceptanceBridge.Current is ITemporaryAssassinGateObserver) &&
                (report?.Token == null || manager != report.Manager)) return null;
            return new TemporaryAssassinCall { Report = report != null && manager == report.Manager ? report : null,
                Scope = temporaryAssassinScope, X = x, Y = y, TX = tx, TY = ty, Continuation = continuation };

        }
        internal static void EndTemporaryAssassinSearch(object token, bool completed, int native, int effective,
            int player, string outcome, bool cache, int nodes)
        {
            if (!(token is TemporaryAssassinCall call)) return;
            try
            {
                TemporaryRouteReport report = call.Report;
                string searchDetail = "native=" + native + ",effective=" + effective + ",outcome=" + outcome + ",player=" + player;
                for (TemporaryAssassinScope scope = call.Scope; scope != null; scope = scope.Previous)
                {
                    scope.Searches++;
                    if (player > 0 && player <= 8 && !scope.MixedPlayers)
                    {
                        if (scope.Player == 0) scope.Player = player;
                        else if (scope.Player != player) { scope.Player = 0; scope.MixedPlayers = true; }
                    }
                    if (scope.First == null) scope.First = searchDetail;
                    scope.Last = searchDetail;
                }
                if (report == null) return;
                report.Searches++;
                if (call.TX < 0 || call.TY < 0) report.Floods++; else report.TargetSearches++;
                if (call.Continuation != 0) report.Continuations++;
                if (completed && effective > 0 && outcome == "weighted-published") report.WeightedFields++;
                string detail = "start=" + call.X + "/" + call.Y + ",target=" + call.TX + "/" + call.TY +
                    ",continuation=" + call.Continuation + ",completed=" + completed + ",native=" + native +
                    ",effective=" + effective + ",player=" + player + ",outcome=" + outcome + ",cache=" + cache + ",nodes=" + nodes;
                if (report.FirstSearch == null) report.FirstSearch = detail;
                report.LastSearch = detail;
            }
            catch (Exception error) { TemporaryGateRouteAcceptanceBridge.ReportFailure("native-search-correlation", error); }
        }
        internal static object UnavailableTemporaryRoute(ITemporaryGateRouteAcceptanceObserver observer, string reason, string source)
        { observer.BeginRoute(0, 0, 0, 0, 0, -1, -1, -1, -1, -1, reason + ",builderSource=" + source); return null; }
        internal object BeginTemporaryRouteReport(IntPtr manager, string source = "unspecified-builder")
        {
            if (TemporaryGateRouteAcceptanceBridge.Current == null) return null;
            TemporaryRouteReport report = CaptureTemporaryRouteReport(manager, source) as TemporaryRouteReport ?? new TemporaryRouteReport();
            report.Previous = temporaryRouteCall; report.Source = source; report.Manager = manager;
            if (report.Token != null)
            {
                byte* context = (byte*)manager.ToPointer();
                report.Flags84 = *(int*)(context + 0x84); report.Flags88 = *(int*)(context + 0x88);
                report.Relaxation = UnitCommandPathAPI.AssassinReconstructionRelaxation?.Invoke() ?? "unavailable";
            }
            temporaryRouteCall = report;
            return report;
        }
        internal object CaptureTemporaryRouteReport(IntPtr manager, string source)
        {
            var observer = TemporaryGateRouteAcceptanceBridge.Current;
            if (observer == null) return null;
            try
            {
                if (manager != nativePathManager || manager == IntPtr.Zero || nativeUnitManager == null)
                    return UnavailableTemporaryRoute(observer, "unknown-path-manager", source);
                byte* context = (byte*)manager.ToPointer();
                byte* path = *(byte**)(context + PathManagerOutputBufferOffset);
                // Read the existing frame directly. Its accessor can abandon frames; no such mutation belongs in diagnosis.
                UnitMoveFrame frame = unitMoveFrame;
                if (frame != null && (frame.Args.SkipOriginalFunction || frame.MapEpoch != mapEpoch ||
                    frame.Tick != CaptureCurrentGameTick() || !ReferenceEquals(frame.Command, activeMoveCommand)))
                    return UnavailableTemporaryRoute(observer, "stale-or-skipped-unit-frame", source);
                if (frame != null && !GameUnitManagerAPI.Instance.IsValidId(frame.Args.UnitId))
                    return UnavailableTemporaryRoute(observer, "invalid-unit-frame", source);
                if (!APIShared.Internal.TemporaryPackedRouteInspection.TryResolveUnit(path - (nativeUnitManager + NativeUnitPathBufferOffset),
                    NativeUnitPathBufferStride, MaximumUnitCount, frame?.Args.UnitId ?? 0, out int unitId))
                    return UnavailableTemporaryRoute(observer, "unit-buffer-mismatch", source);
                var units = GameUnitManagerAPI.Instance;
                if (!units.IsValidId(unitId) || !APIShared.UnitAccess.TryGetById(units, unitId, out GameUnit* unit, out _) || unit == null || unit->r_GlobalId == 0)
                    return UnavailableTemporaryRoute(observer, "missing-unit-identity", source);
                GetNativeMovementStart(unit, out int x, out int y);
                int tx = *(int*)(context + 0x10), ty = *(int*)(context + 0x14);
                if ((uint)x >= MapWidth || (uint)y >= MapWidth || (uint)tx >= MapWidth || (uint)ty >= MapWidth ||
                    *(int*)(context + 0x08) != x || *(int*)(context + 0x0C) != y)
                    return UnavailableTemporaryRoute(observer, "start-or-target-mismatch", source);
                int player = unit->r_ControllableForPlayerId;
                uint tribeGlobal = 0;
                var tribes = GameTribeManagerAPI.Instance;
                if (tribes.IsValidId(unit->r_TribeId) && tribes.TryGetTribeById(unit->r_TribeId, out GameTribe* tribe) && tribe != null && tribe->r_PlayerIdOwner == player)
                    tribeGlobal = tribe->r_GlobalId;
                var report = new TemporaryRouteReport { Observer = observer, Unit = unitId, Global = unit->r_GlobalId,
                    Player = player, X = x, Y = y, TX = tx, TY = ty, Epoch = mapEpoch, Frame = frame,
                    Tribe = unit->r_TribeId, TribeGlobal = tribeGlobal };
                report.Token = observer.BeginRoute(player, unit->r_TribeId, tribeGlobal, unitId, unit->r_GlobalId,
                    (int)unit->r_UnitChimp, x, y, tx, ty,
                    "unitCommandRaw=" + unit->r_AI_LastIssuedTribeCommand + ",unitStateRaw=" + unit->r_AIState +
                    ",unitContextBuildingTileRaw=" + unit->r_AI_ContextTargetBuildingTileId + "," +
                    (activeAttackCommand != null && activeAttackCommand.TribeId == unit->r_TribeId
                        ? "commandSeq=" + activeAttackCommand.Sequence + ",command=" + activeAttackCommand.Command +
                          ",buildingOrUnit=" + activeAttackCommand.TargetValue1 + "/" + activeAttackCommand.TargetValue2
                        : "orderContext=outside-attack-command"));
                return report.Token == null ? null : report;
            }
            catch (Exception error) { TemporaryGateRouteAcceptanceBridge.ReportFailure("route-begin", error); return null; }
        }
        internal void EndTemporaryRouteReport(IntPtr manager, object token, bool completed, int result)
        {
            if (!(token is TemporaryRouteReport report)) return;
            if (report.Token == null) { temporaryRouteCall = report.Previous; return; }
            string status = "unavailable";
            try
            {
                if (!completed) status = "exception";
                else if (result <= 0) status = "search-negative";
                else if (manager == nativePathManager && nativeUnitManager != null && report.Unit > 0 && report.Unit <= MaximumUnitCount &&
                    report.Epoch == mapEpoch && ReferenceEquals(report.Frame, unitMoveFrame) &&
                    GameUnitManagerAPI.Instance.IsValidId(report.Unit) && APIShared.UnitAccess.TryGetById(report.Unit, out GameUnit* unit, out _) && unit != null &&
                    unit->r_GlobalId == report.Global && unit->r_TribeId == report.Tribe &&
                    (unit->r_ControllableForPlayerId) == report.Player)
                {
                    if (report.TribeGlobal != 0 && (!GameTribeManagerAPI.Instance.IsValidId(report.Tribe) ||
                        !GameTribeManagerAPI.Instance.TryGetTribeById(report.Tribe, out GameTribe* tribe) || tribe == null ||
                        tribe->r_GlobalId != report.TribeGlobal || tribe->r_PlayerIdOwner != report.Player))
                    { status = "tribe-identity-changed"; return; }
                    byte* context = (byte*)manager.ToPointer();
                    byte* path = *(byte**)(context + PathManagerOutputBufferOffset);
                    if (path == nativeUnitManager + NativeUnitPathBufferOffset + report.Unit * NativeUnitPathBufferStride &&
                        result <= WeightedMoatRoutePlanner.MaximumRouteEdges && *(int*)(context + PathManagerOutputLengthOffset) == result &&
                        *(int*)(context + 0x08) == report.X && *(int*)(context + 0x0C) == report.Y &&
                        *(int*)(context + 0x10) == report.TX && *(int*)(context + 0x14) == report.TY)
                    {
                        int x = report.X, y = report.Y;
                        status = APIShared.Internal.TemporaryPackedRouteInspection.Decode(new ReadOnlySpan<byte>(path, (result + 1) / 2), result,
                            x, y, report.TX, report.TY, WeightedMoatRoutePlanner.DirectionX, WeightedMoatRoutePlanner.DirectionY, out int[] directions);
                        for (int i = 0; status == "decoded" && i < result; i++)
                        {
                            int d = directions[i];
                            int nx = x + WeightedMoatRoutePlanner.DirectionX[d], ny = y + WeightedMoatRoutePlanner.DirectionY[d];
                            if ((uint)nx >= MapWidth || (uint)ny >= MapWidth) { status = "invalid-coordinate"; break; }
                            int from = GameTileManagerAPI.Instance.GetTileId(x, y), to = GameTileManagerAPI.Instance.GetTileId(nx, ny);
                            if (!IsValidTileId(from) || !IsValidTileId(to)) { status = "invalid-tile"; break; }
                            report.Observer.RouteEdge(report.Token, from, to, d); x = nx; y = ny;
                        }
                    }
                    else status = "output-buffer-or-endpoints-mismatch";
                }
                else status = "unit-or-call-identity-changed";
            }
            catch (Exception error) { status = "diagnostic-exception"; TemporaryGateRouteAcceptanceBridge.ReportFailure("route-read", error); }
            finally
            {
                temporaryRouteCall = report.Previous;
                try
                {
                    byte* context = (byte*)manager.ToPointer();
                    string modes = manager == report.Manager && manager == nativePathManager && manager != IntPtr.Zero
                        ? ",flags84=" + report.Flags84 + "/" + *(int*)(context + 0x84) +
                          ",assassinFlag88=" + report.Flags88 + "/" + *(int*)(context + 0x88) : ",nativeModes=unknown";
                    report.Observer.EndRoute(report.Token, status + "|diag=builderSource=" + report.Source + modes +
                        ",reconstructionRelaxation=" + report.Relaxation + ",nestedAssassinCalls=" + report.Searches +
                        ",targetSearches=" + report.TargetSearches + ",floods=" + report.Floods + ",continuations=" + report.Continuations +
                        ",weightedPublications=" + report.WeightedFields + ",fieldOrigin=" +
                        (report.Searches == 0 ? "unknown-preexisting-field" : "nested-search-observed-consumed-field-unproven") +
                        ",firstSearch=[" + (report.FirstSearch ?? "none") + "],lastSearch=[" + (report.LastSearch ?? "none") + "]", result);
                }
                catch (Exception error) { TemporaryGateRouteAcceptanceBridge.ReportFailure("route-end", error); }
            }
        }
    }
}
