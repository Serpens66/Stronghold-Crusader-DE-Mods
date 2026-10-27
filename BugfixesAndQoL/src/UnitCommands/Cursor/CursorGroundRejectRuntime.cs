using System;
using APIShared;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace BugfixesAndQoL.UnitCommands
{
    internal sealed unsafe partial class UnitCommandPathRuntime
    {
        private bool cursorGroundContractReady;
        private int* cursorGroundNeighborOffsets;
        private ushort* cursorGroundRows;
        [ThreadStatic] private static CursorGroundObservation cursorGroundObservation;
        private struct CursorGroundObservation
        {
            internal CursorRegionRejectPool.Batch Batch;
            internal CursorGroundRejectProof Pending;
            internal int Player, Start, Target, Mode, Profile, BuilderCalls;
        }

        private void InitializeCursorGroundContract(ReadOnlySpan<byte> memory, ulong libraryBase)
        {
            try
            {
                // All fields/tables below are additionally bound to the constructor's
                // complete FBCB9319 native hash gate. No hook is installed here.
                ValidateExactBytes(memory, PathBuilderRva, new byte[] { 0x48,0x89,0x5C,0x24,0x08,0x48,0x89,0x6C,0x24,0x10 }, "cursor ground builder");
                ValidateExactBytes(memory, 0xDA783, new byte[] { 0x44,0x0F,0xBE,0x9C,0x17,0xD0,0x90,0x18,0x05,0x66,0x44,0x39,0x84,0x7A,0x4E,0x25,0x2C,0x05 }, "cursor ground connections/stamps");
                ValidateGameUnitFieldOffset(nameof(GameUnit.r_UnitChimp), 0x8A);
                ValidateGameUnitFieldOffset(nameof(GameUnit.r_CurrentTilePositionX), 0xC0);
                ValidateGameUnitFieldOffset(nameof(GameUnit.r_CurrentTilePositionY), 0xC2);
                cursorGroundNeighborOffsets = (int*)(libraryBase + 0x405EDB0);
                cursorGroundRows = GameTileManagerAPI.Instance.MapColumnLookupTable;
                // Profile views are checked when used; APIShared may finish publishing
                // its tables later in this same LibraryLoaded notification.
                cursorGroundContractReady = cursorGroundRows != null;
                DiagCursorGroundReady(cursorGroundContractReady);
            }
            catch (Exception exception)
            {
                cursorGroundContractReady = false;
                Shared.DebugLogHelper.LogWarningLimited(log, "cursor-ground-reject-unavailable",
                    "Cursor ground rejection reuse unavailable; full native probes retained: " + exception);
            }
        }

        private bool TryGetCursorGroundInputs(GameUnit* unit, int x, int y, int tick,
            out CursorGroundObservation value)
        {
            value = default;
            if (!cursorGroundContractReady || !CursorOrdinaryProbeContext(unit, x, y, tick)) return false;
            var profiles = APIShared.ScriptExtenderReplacements.GamePathingManagerAPI.Instance.GetUnitTypePathfindingProfiles();
            int type = unchecked((short)unit->r_UnitChimp);
            // Both F32B0 (Large) and F3060 (Default), plus DAAC0/DA590/DB650,
            // use subsets of the same directed connection grid in ordinary mode.
            if ((uint)type >= (uint)profiles.Length ||
                (profiles[type] != PathfindingProfile.Large && profiles[type] != PathfindingProfile.Default)) return false;
            int start = GameTileManagerAPI.Instance.GetTileId(unit->r_CurrentTilePositionX, unit->r_CurrentTilePositionY);
            int target = GameTileManagerAPI.Instance.GetTileId(x, y);
            if ((uint)start >= CursorGroundRejectProof.TileCount || (uint)target >= CursorGroundRejectProof.TileCount) return false;
            value = new CursorGroundObservation { Batch = cursorRejectBatch, Player = unchecked((short)unit->r_ControllableForPlayerId),
                Start = start, Target = target, Mode = unchecked((short)unit->r_PathConnectionMode), Profile = (int)profiles[type] };
            return true;
        }

        private bool TryUseCursorGroundReject(CursorGroundObservation request)
        {
            var batch = request.Batch;
            for (int index = 0; index < batch.GroundCount; index++)
            {
                CursorGroundRejectProof proof = batch.Ground[index];
                if (proof.Player != request.Player || proof.Target != request.Target || proof.Mode != request.Mode ||
                    proof.Profile != request.Profile || !proof.Contains(request.Start)) continue;
                if (proof.Policy == null) { DiagCursorGround(2, 0, 0, 0); return true; }
                // A fresh, successful scope must expose precisely the captured policy.
                // A failed mask scope must not turn a filtered proof into an unfiltered one.
                IEnemyGatePathPolicy gate = BeginEnemyGateSearch(request.Player, EnemyGateSearchKind.Builder, out object scope);
                try
                {
                    if (scope != null && ReferenceEquals(gate, proof.Provider) && proof.Policy.IsCurrent &&
                        gate is IEnemyGateRoutePolicyProvider provider && provider.TryCaptureRoutePolicy(request.Player, out var policy) &&
                        ReferenceEquals(policy, proof.Policy))
                    { DiagCursorGround(2, 0, 0, 0); return true; }
                }
                finally { EndEnemyGateSearch(gate, scope, EnemyGateSearchKind.Builder, false, false); }
            }
            return false;
        }

        private void ObserveCursorGroundBuilder(IntPtr manager, int player, int profile, int result)
        {
            ref CursorGroundObservation observed = ref cursorGroundObservation;
            CursorRegionRejectPool.Batch batch = observed.Batch;
            if (batch == null) return;
            observed.BuilderCalls++;
            if (observed.BuilderCalls != 1 || result > 0 || !nativeManualProbe || manager != nativePathManager ||
                player != observed.Player || profile != observed.Profile || batch.Disabled ||
                batch.SelectionCount < CursorRegionRejectPool.GroundMinimumSelection ||
                batch.GroundAttempts >= CursorRegionRejectPool.GroundProofsPerBatch) return;
            byte* state = (byte*)manager;
            if ((uint)*(int*)(state + 8) >= MapWidth || (uint)*(int*)(state + 12) >= MapWidth ||
                (uint)*(int*)(state + 16) >= MapWidth || (uint)*(int*)(state + 20) >= MapWidth) return;
            if (*(int*)(state + 0x84) != 0 || *(int*)(state + 0x88) != 0 ||
                GameTileManagerAPI.Instance.GetTileId(*(int*)(state + 8), *(int*)(state + 12)) != observed.Start ||
                GameTileManagerAPI.Instance.GetTileId(*(int*)(state + 16), *(int*)(state + 20)) != observed.Target) return;
            batch.GroundAttempts++;
            CursorGroundRejectProof proof = batch.Ground[batch.GroundCount];
            proof.Reset();
            IEnemyGatePathPolicy gate = EnemyGatePathPolicyBridge.Current;
            IEnemyGateRoutePolicySnapshot policy = null;
            if (gate != null && gate.HasPublishedMask && gate is IEnemyGateRoutePolicyProvider provider &&
                provider.TryCaptureRoutePolicy(player, out var captured) && captured != null && captured.PlayerId == player && captured.IsCurrent)
                policy = captured;
            long measured = 0;
            Shared.DevDiag.Start(ref measured);
            bool valid = false;
            try
            {
                valid = proof.Capture((short*)(nativeProbeGrid + NativeProbeGridBytes / 2), *(int*)(state + 4),
                    observed.Start, observed.Target, nativeMovementMasks, nativeDirectionMasks,
                    cursorGroundNeighborOffsets, cursorGroundRows, policy);
                if (!valid) return;
                proof.Player = player; proof.Target = observed.Target; proof.Mode = observed.Mode; proof.Profile = profile;
                proof.Policy = policy; proof.Provider = policy == null ? null : gate;
                observed.Pending = proof;
            }
            finally
            {
                DiagCursorGround(valid ? 0 : 3, measured, 0, 0);
            }
        }

        private bool TryRejectCursorNativeTarget(GameUnit* unit, int x, int y)
        {
            if (!cursorGroundContractReady || disposed || nativeManualProbe || nativePathManager == IntPtr.Zero ||
                originalCentralMovementPlan == null || !APIShared.UnitAccess.IsReallyAlive(unit) || (uint)x >= MapWidth || (uint)y >= MapWidth) return false;
            // 18E1E0 checks availability before arrival, but target flags only AFTER arrival.
            if (movementTargetAvailability[y * MapWidth + x] == 0) { DiagCursorGround(4, 0, 0, 0); return true; }
            if (unit->r_CurrentTilePositionX == x && unit->r_CurrentTilePositionY == y) return false;
            int target = GameTileManagerAPI.Instance.GetTileId(x, y);
            if ((uint)target >= CursorGroundRejectProof.TileCount) return false;
            if ((tileFlags[target] & 0x30u) != 0) { DiagCursorGround(5, 0, 0, 0); return true; }
            if ((tileFlags[target] & 0x10000100u) == 0) return false;
            var profiles = APIShared.ScriptExtenderReplacements.GamePathingManagerAPI.Instance.GetUnitTypePathfindingProfiles();
            int type = unchecked((short)unit->r_UnitChimp);
            if ((uint)type < (uint)profiles.Length && profiles[type] == PathfindingProfile.Large)
            { DiagCursorGround(6, 0, 0, 0); return true; }
            return false;
        }

        partial void DiagCursorGround(int kind, long started, int sourceRegion, int targetRegion);
        partial void DiagCursorGroundReady(bool ready);
    }
}
