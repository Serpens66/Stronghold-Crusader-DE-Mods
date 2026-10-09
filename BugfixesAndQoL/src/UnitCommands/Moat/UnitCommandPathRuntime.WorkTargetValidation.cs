using RedBird.X64.Hooks.Transaction;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BugfixesAndQoL.UnitCommands
{
    internal sealed unsafe partial class UnitCommandPathRuntime
    {
        internal bool ValidatePendingDigTarget(PendingDigMoatTarget pending)
        {
            if (pending == null || pending.MapEpoch != mapEpoch ||
                pending.TileManager != GameTileManagerAPI.Instance.GetTileManager() ||
                !APIShared.UnitAccess.TryGetById(
                    pending.UnitId, out GameUnit* unit, out _) ||
                unit == null || !APIShared.UnitAccess.IsReallyAlive(unit) ||
                unit->r_ControllableForPlayerId != pending.PlayerId || !CanDigMoat(unit) ||
                unit->r_CurrentTilePositionX != pending.StartX ||
                unit->r_CurrentTilePositionY != pending.StartY ||
                !TryReadMoatRecordTile(
                    pending.TileManager, pending.MoatId,
                    out int tileId, out int x, out int y) ||
                tileId != pending.TileId || x != pending.X || y != pending.Y ||
                pathRegionGrid[tileId] != pending.TargetRegion)
            {
                return false;
            }

            return TryGetMoatWorkRoute(pending.SearchScope, pending.X, pending.Y, out _);
        }

        internal bool TryReadMoatRecordTile(
            IntPtr tileManager, int moatId, out int tileId, out int x, out int y)
        {
            tileId = -1;
            x = -1;
            y = -1;
            if (tileManager == IntPtr.Zero)
                return false;
            int count = *(int*)((byte*)tileManager.ToPointer() + MoatRecordCountOffset);
            if (!IsValidMoatRecordId(moatId, count))
                return false;
            byte* record = (byte*)tileManager.ToPointer() +
                MoatRecordArrayOffset + moatId * MoatRecordSize;
            tileId = *(int*)(record + MoatRecordTileIdOffset);
            x = *(short*)(record + MoatRecordXOffset);
            y = *(short*)(record + MoatRecordYOffset);
            return IsValidTileId(tileId) && x >= 0 && x < MapWidth &&
                y >= 0 && y < MapWidth &&
                GameTileManagerAPI.Instance.GetTileId(x, y) == tileId;
        }

        internal static bool HasDownstreamMovementBlockingFlags(uint flags) =>
            (flags & MovementBlockedLowTileFlagMask) != 0 ||
            (flags & MovementBlockedStructureTileFlagMask) != 0;

        internal bool IsOccupiedByOtherLivingUnit(int tileId, int currentUnitId)
        {
            int occupantUnitId = GameTileManagerAPI.Instance.GetTileUnitId(tileId);
            if (occupantUnitId == 0 || occupantUnitId == currentUnitId)
                return false;
            return APIShared.UnitAccess.TryGetById(
                    occupantUnitId, out GameUnit* occupant, out _) &&
                occupant != null && APIShared.UnitAccess.IsReallyAlive(occupant);
        }

        internal static bool TryReadMoatRecord(
            IntPtr tileManager,
            int moatId,
            out byte* record,
            out int tileId,
            out int x,
            out int y)
        {
            record = null;
            tileId = -1;
            x = -1;
            y = -1;
            if (tileManager == IntPtr.Zero)
                return false;
            byte* manager = (byte*)tileManager.ToPointer();
            int count = *(int*)(manager + MoatRecordCountOffset);
            if (!IsValidMoatRecordId(moatId, count))
                return false;
            record = manager + MoatRecordArrayOffset + moatId * MoatRecordSize;
            tileId = *(int*)(record + MoatRecordTileIdOffset);
            x = *(short*)(record + MoatRecordXOffset);
            y = *(short*)(record + MoatRecordYOffset);
            return IsValidTileId(tileId) && x >= 0 && x < MapWidth && y >= 0 && y < MapWidth &&
                GameTileManagerAPI.Instance.GetTileId(x, y) == tileId;
        }

        internal void ResetMoatWorkTargetSelection()
        {
            activeMoatWorkSelection = null;
            pendingFillMoatApproach = null;
            pendingDigMoatTarget = null;
            lastMoatWorkSelectionByUnit.Clear();
            lastMoatWorkApproachByUnit.Clear();
        }

        internal sealed class PendingDigMoatTarget
        {
            public MoatWorkSelectionScope SearchScope { get; set; }
            public PendingDigMoatTarget(
                int mapEpoch,
                IntPtr tileManager,
                int unitId,
                int playerId,
                int moatId,
                int startX,
                int startY,
                int tileId,
                int x,
                int y,
                int targetRegion,
                RouteProbeSummary summary)
            {
                MapEpoch = mapEpoch;
                TileManager = tileManager;
                UnitId = unitId;
                PlayerId = playerId;
                MoatId = moatId;
                StartX = startX;
                StartY = startY;
                TileId = tileId;
                X = x;
                Y = y;
                TargetRegion = targetRegion;
                Summary = summary;
            }

            public int MapEpoch { get; }
            public IntPtr TileManager { get; }
            public int UnitId { get; }
            public int PlayerId { get; }
            public int MoatId { get; }
            public int StartX { get; }
            public int StartY { get; }
            public int TileId { get; }
            public int X { get; }
            public int Y { get; }
            public int TargetRegion { get; }
            public RouteProbeSummary Summary { get; }

            public bool Matches(
                int mapEpoch,
                IntPtr tileManager,
                int moatId,
                uint sourceX,
                uint sourceY) =>
                MapEpoch == mapEpoch && TileManager == tileManager && MoatId == moatId &&
                sourceX == unchecked((uint)StartX) && sourceY == unchecked((uint)StartY);
        }
    }
}
