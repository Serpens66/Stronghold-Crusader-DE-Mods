using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace PathfindingSurfaceTest
{
    internal sealed partial class Fixture
    {
        private const string NativeWallHash = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        private void SupplyWallStone()
        {
            // Native CF640 still reads actual Stone when the SE multiplier is zero.
            // Use ordinary costs plus enough real stored Stone for the entire fixture.
            string file = Path.Combine(BepInEx.Paths.GameRootPath,
                "Stronghold Crusader Definitive Edition_Data", "Plugins", "x86_64", "CrusaderDE.dll");
            using (var input = File.OpenRead(file)) using (var sha = SHA256.Create())
                Require(BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "") == NativeWallHash,
                    "Unknown DLL: wall-status fields may not be inspected");
            var buildings = GameBuildingManagerAPI.Instance;
            buildings.SetLowWallCostMultiplier(0.25f); buildings.SetHighWallCostMultiplier(0.5f);
            EnsureWallStone(80);
        }
        private void EnsureWallStone(int required)
        {
            var players = GamePlayerManagerAPI.Instance;
            int before = players.GetGoodAmount(owner, eGoods.STORED_STONE_BLOCKS);
            int missing = Math.Max(0, required - before);
            bool accepted = missing == 0 || players.TryAddGood(owner, eGoods.STORED_STONE_BLOCKS, missing);
            int after = players.GetGoodAmount(owner, eGoods.STORED_STONE_BLOCKS);
            log("wall-stock owner=" + owner + " before=" + before + " requested=" + missing + " accepted=" + accepted + " after=" + after + " required=" + required);
            Require(after >= required, "Insufficient actual stored Stone for ordinary wall fixture");
        }
        private bool TryBuildBarrier()
        {
            BuildObserved("barrier-candidate", cx, cy - 12, cx, cy + 12, eMappers.MAPPER_WALL);
            for (int y = cy - 12; y <= cy + 12; y++) if (!Wall(cx, y)) return false;
            return true;
        }
        private void BuildObserved(string label, int x1, int y1, int x2, int y2, eMappers mapper)
        {
            int cells = Math.Max(Math.Abs(x2 - x1), Math.Abs(y2 - y1)) + 1;
            EnsureWallStone(Math.Max(8, (cells + 1) / 2 + 2)); // Native remainder and endpoint revisit margin.
            int before = GamePlayerManagerAPI.Instance.GetGoodAmount(owner, eGoods.STORED_STONE_BLOCKS);
            GameBuildingManagerAPI.Instance.CreateWall(owner, x1, y1, x2, y2, mapper);
            // Read immediately after the authorized normal call, before another call can
            // overwrite its validation scratch. Never reset fields or call validators as probes.
            IntPtr manager = GameTileManagerAPI.Instance.GetTileManager();
            int invalid = Marshal.ReadInt32(manager, 0x204E6E4);
            int blocked = Marshal.ReadInt32(manager, 0x204E6FC);
            int reason = Marshal.ReadInt32(manager, 0x204E704);
            int after = GamePlayerManagerAPI.Instance.GetGoodAmount(owner, eGoods.STORED_STONE_BLOCKS);
            int dx = Math.Sign(x2 - x1), dy = Math.Sign(y2 - y1);
            int walls = 0, stairs = 0;
            string detail = "";
            var tiles = GameTileManagerAPI.Instance;
            for (int i = 0; i < cells; i++)
            {
                int x = x1 + dx * i, y = y1 + dy * i, tile = tiles.GetTileId(x, y);
                var flags = tiles.GetTilePropertyFlag(tile);
                if ((flags & TilePropertyFlag.IsWall) != 0) walls++;
                if ((flags & TilePropertyFlag.IsStairs) != 0) stairs++;
                detail += (i == 0 ? "" : ";") + x + "," + y + ":f=" + ((uint)flags).ToString("X") + ":h=" + tiles.GetTileHeight(tile) +
                    ":b=" + tiles.GetTileBuildingId(tile) + ":u=" + tiles.GetChimpLayer()[tile];
            }
            var keep = GamePlayerManagerAPI.Instance.GetPlayerKeepPosition(owner);
            int range = GameBuildingManagerAPI.Instance.GetKeepProximityRange(tiles.GetCurrentMapSize());
            log("wall-outcome segment=" + label + " mapper=" + mapper + " begin=" + x1 + "," + y1 + " end=" + x2 + "," + y2 +
                " invalid=" + invalid + " queryBlocked=" + blocked + " reason=" + reason + " reasonMayBeStale=true stone=" + before + "->" + after +
                " wallCells=" + walls + "/" + cells + " stairCells=" + stairs + " keep=" + keep.X + "," + keep.Y + " guideRange=" + range + " cells=[" + detail + "]");
        }
    }
}
