using System;
using CrusaderDE;
using System.Globalization;
using System.Linq;
using SHCDESE.API;
using SHCDESE.Interop;

namespace PathfindingLoadTest
{
    internal sealed partial class Fixture
    {
        private bool lobbyPinned;
        private int pinnedOwner, pinnedRat, pinnedEast, pinnedWest, pinnedHumanRadarX, pinnedHumanRadarY, pinnedRatRadarX, pinnedRatRadarY;

        private void TryPinLobbyKeeps(FRONT_Multiplayer lobby, EngineInterface.MultiplayerSetupData setup)
        {
            var room = lobby.currentLobby;
            var members = room.members.Where(member => member.SkirmishMember && !member.dummyToBeKicked).ToArray();
            var humans = members.Where(member => member.SkirmishHumanMember && member.IsSelf()).ToArray();
            var rats = members.Where(member => !member.SkirmishHumanMember && member.GetLordType() == (int)AILords.SK_RAT).ToArray();
            if (humans.Length == 0 || rats.Length == 0) return; // Starter has not added the opponent yet.
            Require(humans.Length == 1 && rats.Length == 1 && members.Length == 2, "Expected exactly the local human and one Rat");
            int human = room.getThisPlayerFromSteamID(humans[0].GetSteamID());
            int rat = room.getThisPlayerFromSteamID(rats[0].GetSteamID());
            Require(human >= 1 && human <= GamePlayerManagerAPI.MAX_PLAYERS && rat >= 1 &&
                rat <= GamePlayerManagerAPI.MAX_PLAYERS && human != rat, "Invalid lobby participant mapping");
            int[,] locations = GameData.Instance.Keep_Locations;
            ChooseKeepExtremes(locations, out int east, out int west);
            Action<int, int> swap = (source, target) => {
                // Normal frontend commands only. OffClick clears any previous radar selection;
                // the next pair selects the source keep and swaps it with the target keep.
                lobby.SkirmishRadar_OffClick(null, null);
                lobby.ButtonClicked("Radar" + (source + 1).ToString(CultureInfo.InvariantCulture));
                lobby.ButtonClicked("Radar" + (target + 1).ToString(CultureInfo.InvariantCulture));
            };
            PlaceLobbyPlayer(setup.start_keep_location_order, human, east, swap);
            // Re-read this same live order after the first swap: it may have displaced the Rat.
            PlaceLobbyPlayer(setup.start_keep_location_order, rat, west, swap);
            Require(setup.start_keep_location_order[east] == human - 1 && setup.start_keep_location_order[west] == rat - 1,
                "Lobby keep placement changed during radar commands");
            pinnedOwner = human; pinnedRat = rat; pinnedEast = east; pinnedWest = west;
            pinnedHumanRadarX = locations[east, 0]; pinnedHumanRadarY = locations[east, 1];
            pinnedRatRadarX = locations[west, 0]; pinnedRatRadarY = locations[west, 1];
            lobbyPinned = true;
            log("lobby keeps pinned human=" + human + " keepIndex=" + east + " radar=" + pinnedHumanRadarX + "," + pinnedHumanRadarY +
                " rat=" + rat + " keepIndex=" + west + " radar=" + pinnedRatRadarX + "," + pinnedRatRadarY +
                " order=[" + string.Join(",", setup.start_keep_location_order) + "] viaNormalRadar=true");
        }

        private static void ChooseKeepExtremes(int[,] locations, out int east, out int west)
        {
            Require(locations != null && locations.GetLength(1) >= 2, "Keep coordinates unavailable");
            var valid = Enumerable.Range(0, Math.Min(locations.GetLength(0), GamePlayerManagerAPI.MAX_PLAYERS))
                // These are radar marker coordinates, not world tiles. Vanilla tests their sign.
                .Where(index => locations[index, 0] >= 0 && locations[index, 1] >= 0)
                .OrderBy(index => locations[index, 0]).ThenBy(index => locations[index, 1]).ThenBy(index => index).ToArray();
            Require(valid.Length >= 2, "At least two valid keep locations are required");
            west = valid[0]; east = valid[valid.Length - 1];
            Require(locations[east, 0] != locations[west, 0] || locations[east, 1] != locations[west, 1],
                "Keep extrema do not have distinct coordinates");
        }

        private static void PlaceLobbyPlayer(int[] order, int player, int target, Action<int, int> swap)
        {
            Require(player >= 1 && player <= GamePlayerManagerAPI.MAX_PLAYERS && order != null &&
                (uint)target < (uint)order.Length && order.Count(value => value == player - 1) == 1,
                "Lobby participant must occupy exactly one keep");
            int source = Array.IndexOf(order, player - 1);
            if (source != target) swap(source, target);
            Require(order[target] == player - 1 && order.Count(value => value == player - 1) == 1,
                "Normal radar commands did not confirm the requested keep assignment");
        }

        private static bool ValidWorldKeep(int x, int y)
        {
            var tiles = GameTileManagerAPI.Instance;
            return tiles.IsTileInsideMapBounds(x, y) && tiles.IsValidTileId(tiles.GetTileId(x, y));
        }

        private void VerifyPinnedKeeps()
        {
            Require(lobbyPinned && owner == pinnedOwner, "Local native owner does not match the pinned lobby participant");
            var data = GameData.Instance;
            int[] order = data.start_keep_location_order;
            int[,] radar = data.Keep_Locations;
            Require(order != null && (uint)pinnedEast < (uint)order.Length && (uint)pinnedWest < (uint)order.Length &&
                radar != null && radar.GetLength(1) >= 2 && (uint)pinnedEast < (uint)radar.GetLength(0) &&
                (uint)pinnedWest < (uint)radar.GetLength(0), "Loaded keep mapping unavailable");
            var players = GamePlayerManagerAPI.Instance;
            int humanKeep = players.GetPlayerKeepId(pinnedOwner), ratKeep = players.GetPlayerKeepId(pinnedRat);
            var human = players.GetPlayerKeepPosition(pinnedOwner);
            var rat = players.GetPlayerKeepPosition(pinnedRat);
            log("native pinned keep check humanId=" + humanKeep + " world=" + human.X + "," + human.Y +
                " keepIndex=" + pinnedEast + " returnedPlayerIndex=" + order[pinnedEast] +
                " lobbyRadar=" + pinnedHumanRadarX + "," + pinnedHumanRadarY + " loadedRadar=" + radar[pinnedEast, 0] + "," + radar[pinnedEast, 1] +
                " ratId=" + ratKeep + " world=" + rat.X + "," + rat.Y +
                " keepIndex=" + pinnedWest + " returnedPlayerIndex=" + order[pinnedWest] +
                " lobbyRadar=" + pinnedRatRadarX + "," + pinnedRatRadarY + " loadedRadar=" + radar[pinnedWest, 0] + "," + radar[pinnedWest, 1]);
            Require(order[pinnedEast] == pinnedOwner - 1 && order[pinnedWest] == pinnedRat - 1 &&
                order.Count(value => value == pinnedOwner - 1) == 1 && order.Count(value => value == pinnedRat - 1) == 1,
                "Loaded keep order differs from pinned lobby participant mapping");
            Require(humanKeep > 0 && ratKeep > 0 && humanKeep != ratKeep && ValidWorldKeep(human.X, human.Y) &&
                ValidWorldKeep(rat.X, rat.Y) && (human.X != rat.X || human.Y != rat.Y),
                "Native keep references are missing, outside the playable map or not distinct");
            // InitGameInfo copies native returned order and radar positions independently.
            // Never equate radar pixels with public resource-record world tile coordinates.
            // The coordinator compares actual WORLD coordinates across measured runs.
        }
    }
}
