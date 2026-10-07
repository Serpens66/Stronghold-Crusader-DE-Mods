// TEMP_GATE_ROUTE_ACCEPTANCE: remove this partial and the two builder attachment points after acceptance.
using APIShared;
using SHCDESE.API;
using SHCDESE.Interop;
using System;
namespace BugfixesAndQoL
{
    internal sealed unsafe partial class FriendlyMoatMovementRuntime
    {
        private sealed class TemporaryRouteReport
        {
            internal ITemporaryGateRouteAcceptanceObserver Observer;
            internal object Token;
            internal int Unit, Player, X, Y, TX, TY;
            internal uint Global;
        }
        private object BeginTemporaryRouteReport(IntPtr manager)
        {
            var observer = TemporaryGateRouteAcceptanceBridge.Current;
            if (observer == null) return null;
            try
            {
                PlanScope plan = GetBuilderPlan(manager);
                if (plan == null || !GameUnitManagerAPI.Instance.TryGetUnitById(plan.UnitId, out GameUnit* unit) || unit == null)
                    return null;
                GetNativeMovementStart(unit, out int x, out int y);
                int player = unit->r_ControllableForPlayerId | ((int)unit->N00000569 << 8);
                uint tribeGlobal = 0;
                if (GameTribeManagerAPI.Instance.TryGetTribeById(unit->r_TribeId, out GameTribe* tribe) && tribe != null && tribe->r_PlayerIdOwner == player)
                    tribeGlobal = tribe->r_GlobalId;
                var report = new TemporaryRouteReport { Observer = observer, Unit = plan.UnitId, Global = unit->r_GlobalId,
                    Player = player, X = x, Y = y, TX = plan.TargetX, TY = plan.TargetY };
                report.Token = observer.BeginRoute(player, unit->r_TribeId, tribeGlobal, plan.UnitId, unit->r_GlobalId,
                    (int)unit->r_UnitChimp, x, y, plan.TargetX, plan.TargetY,
                    activeAttackCommand != null && activeAttackCommand.TribeId == unit->r_TribeId
                        ? "commandSeq=" + activeAttackCommand.Sequence + ",command=" + activeAttackCommand.Command +
                          ",buildingOrUnit=" + activeAttackCommand.TargetValue1 + "/" + activeAttackCommand.TargetValue2
                        : "orderContext=outside-attack-command");
                return report.Token == null ? null : report;
            }
            catch (Exception error) { TemporaryGateRouteAcceptanceBridge.ReportFailure("route-begin", error); return null; }
        }
        private void EndTemporaryRouteReport(IntPtr manager, object token, bool completed, int result)
        {
            if (!(token is TemporaryRouteReport report)) return;
            string status = "unavailable";
            try
            {
                if (!completed) status = "exception";
                else if (result <= 0) status = "no-route-output";
                else if (manager == nativePathManager && nativeUnitManager != null && report.Unit > 0 && report.Unit <= MaximumUnitCount &&
                    GameUnitManagerAPI.Instance.TryGetUnitById(report.Unit, out GameUnit* unit) && unit != null &&
                    unit->r_GlobalId == report.Global && (unit->r_ControllableForPlayerId | ((int)unit->N00000569 << 8)) == report.Player)
                {
                    byte* context = (byte*)manager.ToPointer();
                    byte* path = *(byte**)(context + PathManagerOutputBufferOffset);
                    if (path == nativeUnitManager + NativeUnitPathBufferOffset + report.Unit * NativeUnitPathBufferStride &&
                        result <= WeightedMoatRoutePlanner.MaximumRouteEdges && *(int*)(context + PathManagerOutputLengthOffset) == result &&
                        *(int*)(context + 0x08) == report.X && *(int*)(context + 0x0C) == report.Y &&
                        *(int*)(context + 0x10) == report.TX && *(int*)(context + 0x14) == report.TY)
                    {
                        int x = report.X, y = report.Y;
                        status = Shared.TemporaryPackedRouteInspection.Decode(new ReadOnlySpan<byte>(path, (result + 1) / 2), result,
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
                }
            }
            catch (Exception error) { status = "diagnostic-exception"; TemporaryGateRouteAcceptanceBridge.ReportFailure("route-read", error); }
            finally
            {
                try { report.Observer.EndRoute(report.Token, status, result); }
                catch (Exception error) { TemporaryGateRouteAcceptanceBridge.ReportFailure("route-end", error); }
            }
        }
    }
}
