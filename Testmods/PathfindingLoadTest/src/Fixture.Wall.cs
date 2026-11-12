using System;
using System.Runtime.InteropServices;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace PathfindingLoadTest
{
    internal sealed partial class Fixture
    {
        private void LogWallOutcome(string segment, int x, int firstY, int lastY)
        {
            // Read-only, immediately after each already hash-gated ordinary CreateWall.
            // These offsets are the complete current-hash wall-placement audit contract.
            var tiles = GameTileManagerAPI.Instance;
            IntPtr manager = tiles.GetTileManager();
            int invalid = Marshal.ReadInt32(manager, 0x204E6E4);
            int queryBlocked = Marshal.ReadInt32(manager, 0x204E6FC);
            int reason = Marshal.ReadInt32(manager, 0x204E704);
            int stone = GamePlayerManagerAPI.Instance.GetGoodAmount(owner, eGoods.STORED_STONE_BLOCKS);
            int built = 0, expected = lastY - firstY + 1;
            int firstTile = tiles.GetTileId(x, firstY);
            var firstLogic = tiles.GetTilePropertyFlag(firstTile);
            int firstHeight = tiles.GetTileHeight(firstTile);
            for (int y = firstY; y <= lastY; y++)
                if ((tiles.GetTilePropertyFlag(tiles.GetTileId(x, y)) & TilePropertyFlag.IsWall) != 0) built++;
            log("wall outcome segment=" + segment + " x=" + x + " firstY=" + firstY + " lastY=" + lastY +
                " invalid=" + invalid + " queryBlocked=" + queryBlocked + " reason=" + reason +
                " reasonMayBeStale=true stoneAfter=" + stone + " expectedCells=" + expected + " wallFlagCells=" + built +
                " firstLogic=" + firstLogic + " firstHeight=" + firstHeight);
        }
    }
}
