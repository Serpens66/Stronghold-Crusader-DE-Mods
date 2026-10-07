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
            internal int Unit, Player, X, Y, TX, TY, Epoch, Tribe;
            internal uint Global, TribeGlobal;
            internal UnitMoveFrame Frame;
        }
        private static object UnavailableTemporaryRoute(ITemporaryGateRouteAcceptanceObserver observer, string reason)
        { observer.BeginRoute(0, 0, 0, 0, 0, -1, -1, -1, -1, -1, reason); return null; }
        private object BeginTemporaryRouteReport(IntPtr manager)
        {
            var observer = TemporaryGateRouteAcceptanceBridge.Current;
            if (observer == null) return null;
            try
            {
                if (manager != nativePathManager || manager == IntPtr.Zero || nativeUnitManager == null)
                    return UnavailableTemporaryRoute(observer, "unknown-path-manager");
                byte* context = (byte*)manager.ToPointer();
                byte* path = *(byte**)(context + PathManagerOutputBufferOffset);
                // Read the existing frame directly. Its accessor can abandon frames; no such mutation belongs in diagnosis.
                UnitMoveFrame frame = unitMoveFrame;
                if (frame != null && (frame.Args.SkipOriginalFunction || frame.MapEpoch != mapEpoch ||
                    frame.Tick != CaptureCurrentGameTick() || !ReferenceEquals(frame.Command, activeMoveCommand)))
                    return UnavailableTemporaryRoute(observer, "stale-or-skipped-unit-frame");
                if (frame != null && !GameUnitManagerAPI.Instance.IsValidId(frame.Args.UnitId))
                    return UnavailableTemporaryRoute(observer, "invalid-unit-frame");
                if (!Shared.TemporaryPackedRouteInspection.TryResolveUnit(path - (nativeUnitManager + NativeUnitPathBufferOffset),
                    NativeUnitPathBufferStride, MaximumUnitCount, frame?.Args.UnitId ?? 0, out int unitId))
                    return UnavailableTemporaryRoute(observer, "unit-buffer-mismatch");
                var units = GameUnitManagerAPI.Instance;
                if (!units.IsValidId(unitId) || !units.TryGetUnitById(unitId, out GameUnit* unit) || unit == null || unit->r_GlobalId == 0)
                    return UnavailableTemporaryRoute(observer, "missing-unit-identity");
                GetNativeMovementStart(unit, out int x, out int y);
                int tx = *(int*)(context + 0x10), ty = *(int*)(context + 0x14);
                if ((uint)x >= MapWidth || (uint)y >= MapWidth || (uint)tx >= MapWidth || (uint)ty >= MapWidth ||
                    *(int*)(context + 0x08) != x || *(int*)(context + 0x0C) != y)
                    return UnavailableTemporaryRoute(observer, "start-or-target-mismatch");
                int player = unit->r_ControllableForPlayerId | ((int)unit->N00000569 << 8);
                uint tribeGlobal = 0;
                var tribes = GameTribeManagerAPI.Instance;
                if (tribes.IsValidId(unit->r_TribeId) && tribes.TryGetTribeById(unit->r_TribeId, out GameTribe* tribe) && tribe != null && tribe->r_PlayerIdOwner == player)
                    tribeGlobal = tribe->r_GlobalId;
                var report = new TemporaryRouteReport { Observer = observer, Unit = unitId, Global = unit->r_GlobalId,
                    Player = player, X = x, Y = y, TX = tx, TY = ty, Epoch = mapEpoch, Frame = frame,
                    Tribe = unit->r_TribeId, TribeGlobal = tribeGlobal };
                report.Token = observer.BeginRoute(player, unit->r_TribeId, tribeGlobal, unitId, unit->r_GlobalId,
                    (int)unit->r_UnitChimp, x, y, tx, ty,
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
                else if (result <= 0) status = "search-negative";
                else if (manager == nativePathManager && nativeUnitManager != null && report.Unit > 0 && report.Unit <= MaximumUnitCount &&
                    report.Epoch == mapEpoch && ReferenceEquals(report.Frame, unitMoveFrame) &&
                    GameUnitManagerAPI.Instance.IsValidId(report.Unit) && GameUnitManagerAPI.Instance.TryGetUnitById(report.Unit, out GameUnit* unit) && unit != null &&
                    unit->r_GlobalId == report.Global && unit->r_TribeId == report.Tribe &&
                    (unit->r_ControllableForPlayerId | ((int)unit->N00000569 << 8)) == report.Player)
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
                    else status = "output-buffer-or-endpoints-mismatch";
                }
                else status = "unit-or-call-identity-changed";
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
