using System;
using SHCDESE.API;
using SHCDESE.Interop;

namespace BugfixesAndQoL.UnitCommands
{
    internal sealed unsafe partial class UnitCommandPathRuntime
    {
        private readonly CursorRegionRejectPool cursorRejectPool = new CursorRegionRejectPool();
        private bool cursorRejectContractReady;
        [ThreadStatic] private static CursorRegionRejectPool.Batch cursorRejectBatch;
        [ThreadStatic] private static CursorRejectObservation cursorRejectObservation;
        private struct CursorRejectObservation
        {
            internal CursorRegionRejectPool.Batch Batch;
            internal CursorRegionRejectKey Key;
            internal int Calls, Result;
            internal bool Matches;
        }
        private struct CursorRejectScope : IDisposable
        {
            internal CursorRegionRejectPool Pool;
            internal CursorRegionRejectPool.Batch Value, Parent;
            public void Dispose() { cursorRejectBatch = Parent; Pool?.Return(Value); }
        }
        private void InitializeCursorRejectContract(ReadOnlySpan<byte> memory)
        {
            try
            {
                // FBCB9319, 18E391..18E3C6: ordinary mode, signed mode/player loads,
                // E2610(player, source PCL, target PCL, mode), immediate failure on zero.
                ValidateExactBytes(memory, 0x18E391, new byte[] {
                    0x83,0x3D,0x50,0xF3,0xF1,0x05,0x00,0x75,0x49,0x0F,0xBF,0x83,0xB8,0x09,0x00,0x00,
                    0x48,0x8D,0x0D,0xB8,0xF2,0xF1,0x05,0x0F,0xBF,0x93,0xEE,0x06,0x00,0x00,
                    0x44,0x8B,0xCF,0x45,0x8B,0xC6,0x89,0x44,0x24,0x20,0xE8,0x52,0x42,0xF5,0xFF,
                    0x85,0xC0,0x0F,0x84,0xA2,0x00,0x00,0x00 }, "cursor mandatory region rejection");
                ValidateGameUnitFieldOffset(nameof(GameUnit.r_CurrentTilePositionX), 0xC0);
                ValidateGameUnitFieldOffset(nameof(GameUnit.r_CurrentTilePositionY), 0xC2);
                ValidateGameUnitFieldOffset(nameof(GameUnit.r_CurrentPositionTileId), 0xD0);
                ValidateGameUnitFieldOffset(nameof(GameUnit.r_ControllableForPlayerId), 0x92);
                ValidateGameUnitFieldOffset(nameof(GameUnit.r_PathConnectionMode), 0x35C);
                cursorRejectContractReady = true;
            }
            catch (Exception exception)
            {
                cursorRejectContractReady = false;
                Shared.DebugLogHelper.LogWarningLimited(log, "cursor-region-reject-unavailable",
                    "Cursor region rejection reuse unavailable; full native probes retained: " + exception);
            }
        }
        private CursorRejectScope BeginCursorRejectBatch(bool enabled, int selectionCount = 0)
        {
            var scope = new CursorRejectScope { Parent = cursorRejectBatch, Pool = cursorRejectPool };
            cursorRejectBatch = null; // A nested building/native operation cannot inherit a proof.
            try
            {
                if (enabled && cursorRejectContractReady && (cursorGroundContractReady ||
                    (regionPairReachabilityDetour != null && originalRegionPairReachability != null)))
                    scope.Value = cursorRejectPool.Rent(this, mapEpoch, CaptureCurrentGameTick(), placementRevision, cursorDiplomacy);
                if (scope.Value != null) scope.Value.SelectionCount = selectionCount;
            }
            catch (Exception) { /* Optional acceleration must leave the full probe available. */ }
            cursorRejectBatch = scope.Value;
            return scope;
        }
        private bool CursorRejectContextMatches(CursorRegionRejectPool.Batch batch, int tick)
        {
            if (batch == null || batch.Disabled || !ReferenceEquals(batch.Owner, this)) return false;
            if (batch.Epoch != mapEpoch || batch.Tick != tick || batch.Revision != placementRevision || batch.Diplomacy != cursorDiplomacy)
            { batch.Disabled = true; batch.Rejections.Clear(); batch.ClearGround(); return false; }
            // These contexts survive the existing native probe's isolation and can change
            // E2610's effective result. Never transfer their answer to another member.
            return activeDirectCursorMove == null && activeLadderAttackProbe == null && activeBuildingCursorConnectivity == null;
        }
        private bool CursorOrdinaryProbeContext(GameUnit* unit, int x, int y, int tick)
        {
            if (!cursorRejectContractReady || disposed || nativeManualProbe || nativeProbeBatchDepth <= 0 ||
                !CursorRejectContextMatches(cursorRejectBatch, tick) || !APIShared.UnitAccess.IsReallyAlive(unit) ||
                (uint)x >= MapWidth || (uint)y >= MapWidth ||
                (uint)unit->r_CurrentTilePositionX >= MapWidth || (uint)unit->r_CurrentTilePositionY >= MapWidth ||
                !IsValidTileId(unchecked((int)unit->r_CurrentPositionTileId)) ||
                IsCompletedMoatTile(unchecked((int)unit->r_CurrentPositionTileId)) ||
                (unit->r_CurrentTilePositionX == x && unit->r_CurrentTilePositionY == y)) return false;
            return true;
        }
        private bool TryGetCursorRejectKey(GameUnit* unit, int x, int y, int tick, out CursorRegionRejectKey key)
        {
            key = default;
            if (!CursorOrdinaryProbeContext(unit, x, y, tick) || regionPairReachabilityDetour == null || originalRegionPairReachability == null) return false;
            int start = GameTileManagerAPI.Instance.GetTileId(unit->r_CurrentTilePositionX, unit->r_CurrentTilePositionY);
            int target = GameTileManagerAPI.Instance.GetTileId(x, y);
            if (!IsValidTileId(start) || !IsValidTileId(target)) return false;
            int sourcePcl = unchecked((short)pathRegionGrid[start]), targetPcl = unchecked((short)pathRegionGrid[target]);
            DiagCursorGround(sourcePcl == targetPcl ? 7 : 8, 0, sourcePcl, targetPcl);
            if (sourcePcl == targetPcl) return false;
            key = new CursorRegionRejectKey(unchecked((short)unit->r_ControllableForPlayerId), sourcePcl, targetPcl,
                unchecked((short)unit->r_PathConnectionMode));
            return true;
        }
        private bool ProbeCursorWithRegionReject(int id, GameUnit* unit, int x, int y, int tick)
        {
            CursorRegionRejectPool.Batch batch = cursorRejectBatch;
            bool eligible = false;
            bool groundEligible = false;
            CursorGroundObservation ground = default;
            CursorRegionRejectKey key = default;
            try
            {
                if (TryRejectCursorNativeTarget(unit, x, y)) return false;
                eligible = TryGetCursorRejectKey(unit, x, y, tick, out key);
                groundEligible = TryGetCursorGroundInputs(unit, x, y, tick, out ground);
            }
            catch (Exception) { if (batch != null) batch.Disabled = true; eligible = groundEligible = false; }
            DiagCursorReject(0);
            if (eligible && batch.Rejections.ContainsKey(key)) { DiagCursorReject(2); return false; }
            try { if (groundEligible && TryUseCursorGroundReject(ground)) return false; }
            catch (Exception) { if (batch != null) batch.Disabled = true; groundEligible = false; }
            CursorRejectObservation previous = cursorRejectObservation;
            CursorGroundObservation previousGround = cursorGroundObservation;
            cursorRejectObservation = eligible ? new CursorRejectObservation { Batch = batch, Key = key } : default;
            cursorGroundObservation = groundEligible ? ground : default;
            bool completed = false, reachable = false;
            try { reachable = ProbeNativeManualPath(id, x, y); completed = true; return reachable; }
            finally
            {
                CursorRejectObservation observed = cursorRejectObservation;
                CursorGroundObservation observedGround = cursorGroundObservation;
                cursorRejectObservation = previous;
                cursorGroundObservation = previousGround;
                try
                {
                    if (eligible && completed && !reachable && observed.Calls == 1 && observed.Matches && observed.Result == 0 &&
                        CursorRejectContextMatches(batch, CaptureCurrentGameTick()) && batch.Rejections.Count < CursorRegionRejectPool.KeysPerBatch)
                    { batch.Rejections[key] = 0; DiagCursorReject(1); }
                    if (groundEligible && completed && !reachable && observedGround.BuilderCalls == 1 && observedGround.Pending != null &&
                        CursorRejectContextMatches(batch, CaptureCurrentGameTick()) &&
                        (observedGround.Pending.Policy == null || observedGround.Pending.Policy.IsCurrent))
                    { batch.GroundCount++; DiagCursorGround(1, 0, 0, 0); }
                }
                catch (Exception) { if (batch != null) batch.Disabled = true; }
            }
        }
        private void ObserveCursorRegionReject(IntPtr manager, int player, int source, int target, int mode, int result)
        {
            if (cursorRejectObservation.Batch == null) return;
            cursorRejectObservation.Calls++;
            cursorRejectObservation.Matches = cursorRejectObservation.Calls == 1 && nativeManualProbe && manager == nativePathManager &&
                ReferenceEquals(cursorRejectObservation.Batch.Owner, this) &&
                ReferenceEquals(cursorRejectBatch, cursorRejectObservation.Batch) &&
                cursorRejectObservation.Key.Equals(new CursorRegionRejectKey(player, source, target, mode));
            cursorRejectObservation.Result = result;
            DiagCursorGround(cursorRejectObservation.Matches ? result == 0 ? 10 : 9 : 11, 0, source, target);
        }
        partial void DiagCursorReject(int kind);
    }
}
