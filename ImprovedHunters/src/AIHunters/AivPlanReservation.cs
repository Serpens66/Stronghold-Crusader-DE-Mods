using System;
using System.Collections.Generic;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace ImprovedHunters
{
    // Reuse the clipped mask between rare searches; its anchor and contents belong to one search only.
    internal sealed class AivPlanReservation
    {
        private int minimumX, minimumY, width;
        private bool[] reserved = Array.Empty<bool>();
        // Initialized with the builder at native readiness, never reflected per frame or placement.
        private static readonly Dictionary<eMappers, int> mapperScales;

        static AivPlanReservation() { mapperScales = CreateMapperScales(); }

        internal AivPlanReservation() { }
        internal AivPlanReservation(int x, int y, int radius, int hutSize)
        {
            Reset(x, y, radius, hutSize);
        }

        internal void Reset(int x, int y, int radius, int hutSize)
        {
            if (radius < 0 || hutSize < 1) throw new ArgumentOutOfRangeException();
            minimumX = checked(x - radius); minimumY = checked(y - radius);
            width = checked(radius * 2 + hutSize);
            int length = checked(width * width);
            if (reserved.Length != length) reserved = new bool[length];
            else Array.Clear(reserved, 0, reserved.Length);
        }

        internal unsafe bool TryCapture(int player, int x, int y, int radius, int hutSize,
            out string reason)
        {
            Reset(x, y, radius, hutSize);
            reason = "aiv-plan-unavailable";
            var api = GameAIVManagerAPI.Instance;
            if (!api.TryGetVillageSlotByPlayerId(player, out int slot) ||
                !api.TryGetVillageBySlot(slot, out AivVillageState* village) || village->OwnerPlayerId != player ||
                (village->LayoutSelectionState != AivLayoutSelectionState.Usable && village->LayoutSelectionState != AivLayoutSelectionState.Perfect) ||
                !ValidRotation(village->Rotation)) return false;
            var steps = api.GetBuildSteps(slot);
            int maximum = village->MaximumBuildStep;
            if (maximum < 0 || maximum >= steps.Length) return false;
            int orderedCount = api.GetOrderedMapTiles(slot).Length;
            int packedCapacity = GamePathingManagerAPI.Instance.GetPathComponentGrid().Length;
            var tileApi = GameTileManagerAPI.Instance;
            for (int frame = 0; frame <= maximum; frame++)
            {
                ref readonly AivBuildStep step = ref steps[frame];
                // Do not filter by progress: built and abandoned sites must remain free for rebuilding.
                if (step.BuildingType == eMappers.MAPPER_NULL || step.TileCount == 0) continue;
                bool grouped = GameAIVManagerAPI.UsesOrderedMapTileBuffer(step.BuildingType);
                if (grouped)
                {
                    if (step.MapTileIdOrBufferIndex < 0 || step.MapTileIdOrBufferIndex > orderedCount - step.TileCount) return false;
                }
                var tiles = api.GetBuildStepMapTiles(slot, frame);
                if (tiles.Length != (grouped ? step.TileCount : 1)) return false;
                foreach (int tile in tiles)
                {
                    if (tile <= 0 || tile >= packedCapacity) return false;
                    var point = tileApi.GetTileVectorFromId(tile);
                    if (!ReservePlacement(step.BuildingType, point.X, point.Y, village->Rotation, grouped))
                    { reason = "aiv-plan-unknown-footprint"; return false; }
                }
            }
            reason = ""; return true;
        }

        internal static bool ValidRotation(AivRotation rotation) => rotation == AivRotation.South || rotation == AivRotation.East ||
            rotation == AivRotation.North || rotation == AivRotation.West;

        internal bool ReservePlacement(eMappers mapper, int x, int y, AivRotation rotation, bool grouped)
        {
            if (!ValidRotation(rotation)) return false;
            if (grouped) { ReserveSquare(x, y, 1); return true; }
            if (!mapperScales.TryGetValue(mapper, out int size)) return false;
            ReserveSquare(x, y, size);
            // These are prepared-frame minimum corners, not serialized AIV anchors. Only the
            // additional native components rotate; never transform the base position again.
            if (mapper == eMappers.MAPPER_BEDOUIN_STOCKADE || mapper == eMappers.MAPPER_BARRACKS_WOOD || mapper == eMappers.MAPPER_BARRACKS_STONE ||
                mapper == eMappers.MAPPER_ENGINEERS_GUILD || mapper == eMappers.MAPPER_TUNNELERS_GUILD)
            {
                int dx = 0, dy = 0;
                switch (rotation)
                {
                    case AivRotation.South: dx = 5; dy = 5; break;
                    case AivRotation.East: dx = 5; dy = -5; break;
                    case AivRotation.North: dx = -5; dy = -5; break;
                    case AivRotation.West: dx = -5; dy = 5; break;
                }
                if (mapper == eMappers.MAPPER_ENGINEERS_GUILD || mapper == eMappers.MAPPER_TUNNELERS_GUILD)
                {
                    // The second extra is vertical at South/North, horizontal at East/West.
                    if (rotation == AivRotation.South || rotation == AivRotation.North) ReserveSquare(x, y + dy, 5);
                    else ReserveSquare(x + dx, y, 5);
                }
                else { ReserveSquare(x, y + dy, 5); ReserveSquare(x + dx, y, 5); ReserveSquare(x + dx, y + dy, 5); }
            }
            else if (mapper == eMappers.MAPPER_OIL_SMELTER)
            {
                switch (rotation)
                {
                    case AivRotation.South: ReserveSquare(x, y + 4, 4); break;
                    case AivRotation.East: ReserveSquare(x + 4, y, 4); break;
                    case AivRotation.North: ReserveSquare(x, y - 4, 4); break;
                    case AivRotation.West: ReserveSquare(x - 4, y, 4); break;
                }
            }
            return true;
        }

        private static Dictionary<eMappers, int> CreateMapperScales()
        {
            var scales = new Dictionary<eMappers, int>();
            foreach (eMappers mapper in Enum.GetValues(typeof(eMappers)))
                // Native AIV import clamps defined markers and special commands to one tile.
                scales[mapper] = Math.Max(1, BuildingScales.GetScale(mapper));
            return scales;
        }

        private void ReserveSquare(int x, int y, int size)
        {
            int firstX = Math.Max(x - minimumX, 0), firstY = Math.Max(y - minimumY, 0);
            int lastX = Math.Min(x - minimumX + size, width), lastY = Math.Min(y - minimumY + size, width);
            for (int row = firstY; row < lastY; row++)
                for (int column = firstX; column < lastX; column++) reserved[row * width + column] = true;
        }

        internal bool Overlaps(int x, int y, int size)
        {
            x -= minimumX; y -= minimumY;
            if (size < 1 || x < 0 || y < 0 || x > width - size || y > width - size) return true;
            for (int row = y; row < y + size; row++)
                for (int column = x; column < x + size; column++) if (reserved[row * width + column]) return true;
            return false;
        }

        // Only the plan can justify suppressing later searches. Current obstacles, pathing
        // and resources can change. Include the entire search raster, beyond the attempt budget.
        internal bool CoversSearchArea(int centerX, int centerY, int radius, int step, int hutSize)
        {
            if (radius <= 0 || step <= 0 || radius % step != 0 || hutSize <= 0 ||
                minimumX != centerX - radius || minimumY != centerY - radius || width != radius * 2 + hutSize)
                return false;
            for (int dx = -radius; dx <= radius; dx += step)
                for (int dy = -radius; dy <= radius; dy += step)
                    if ((dx != 0 || dy != 0) && !Overlaps(centerX + dx, centerY + dy, hutSize)) return false;
            return true;
        }

    }
}
