using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;

namespace Shared
{
    public static unsafe class ActivePlayerHelper
    {
        /// <summary>
        /// Returns the 1-based IDs of players that are still relevant to the current game.
        /// Skirmish, multiplayer, and trail games use the native participant roster. Other
        /// singleplayer missions always retain the local human and infer additional players
        /// from currently active owned buildings and units.
        /// </summary>
        /// <remarks>
        /// In story, economic, and siege missions, a scripted non-local player with no current
        /// buildings or units cannot be detected until one of its entities exists. The ownership
        /// scans describe the current simulation only and cannot predict players used by future
        /// mission events. This method scans the complete building and unit arrays in those modes
        /// and should therefore not be called every tick.
        /// </remarks>
        public static int[] GetActivePlayerIds()
        {
            EngineInterface.PlayState playState = GameData.Instance?.lastGameState;
            if (playState == null)
                return Array.Empty<int>();

            GamePlayerManagerAPI playerApi = GamePlayerManagerAPI.Instance;
            if (UsesNativeParticipantRoster())
                return GetRosterActivePlayerIds(playState, playerApi);

            HashSet<int> activePlayerIds = new HashSet<int>();

            // A running non-skirmish singleplayer mission keeps its human relevant before keep placement.
            int localPlayerId = playerApi.GetLocalPlayerId();
            AddPlayerIfActive(activePlayerIds, localPlayerId, playerApi);

            Span<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            for (int i = 0; i < buildings.Length; i++)
            {
                ref GameBuilding building = ref buildings[i];
                if (IsRelevantEntityState(building.r_AliveState))
                    AddPlayerIfActive(activePlayerIds, building.r_PlayerIdOwner, playerApi);
            }

            Span<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
            for (int i = 0; i < units.Length; i++)
            {
                ref GameUnit unit = ref units[i];
                if (IsRelevantEntityState(unit.r_AliveState))
                    AddPlayerIfActive(activePlayerIds, unit.r_ControllableForPlayerId, playerApi);
            }

            int[] results = new int[activePlayerIds.Count];
            activePlayerIds.CopyTo(results);
            Array.Sort(results);
            return results;
        }

        /// <summary>
        /// Returns whether the player currently owns a keep building.
        /// </summary>
        public static bool HasKeep(int playerId)
        {
            return GetPlayerIdsWithKeeps().Contains(playerId);
        }

        private static HashSet<int> GetPlayerIdsWithKeeps()
        {
            HashSet<int> playerIds = new HashSet<int>();
            Span<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            for (int i = 0; i < buildings.Length; i++)
            {
                GameBuilding building = buildings[i];
                int owner = building.r_PlayerIdOwner;
                if (!GamePlayerManagerAPI.Instance.IsPlayerIdValid(owner) ||
                    !IsKeepType(building.r_BuildingType))
                {
                    continue;
                }

                // At map start the keep can establish ownership before reaching IsAlive.
                playerIds.Add(owner);
            }

            return playerIds;
        }

        private static bool IsKeepType(eStructs buildingType)
        {
            return buildingType == eStructs.STRUCT_KEEP_ONE ||
                   buildingType == eStructs.STRUCT_KEEP_TWO ||
                   buildingType == eStructs.STRUCT_KEEP_THREE ||
                   buildingType == eStructs.STRUCT_KEEP_FOUR ||
                   buildingType == eStructs.STRUCT_KEEP_FIVE;
        }

        private static int[] GetRosterActivePlayerIds(
            EngineInterface.PlayState playState,
            GamePlayerManagerAPI playerApi)
        {
            List<int> results = new List<int>(GamePlayerManagerAPI.MAX_PLAYERS);

            for (int playerId = 1; playerId <= GamePlayerManagerAPI.MAX_PLAYERS; playerId++)
            {
                int mpStatsValid = ReadByte(playState.mp_stats_valid, playerId);
                int humanRegister = ReadInt16(playState.player_register, playerId);
                int aiRegister = ReadInt16(playState.computer_register, playerId);

                bool registeredHuman = humanRegister != int.MinValue && humanRegister != -1;
                bool registeredAI = aiRegister != int.MinValue && aiRegister != -1;
                bool participated = mpStatsValid > 0 || registeredHuman || registeredAI;

                if (participated && !HasPlayerLost(playerId, playerApi))
                    results.Add(playerId);
            }

            return results.ToArray();
        }

        private static bool UsesNativeParticipantRoster()
        {
            if (Director.instance != null &&
                (Director.instance.SkirmishModeGame || Director.instance.MultiplayerGame))
            {
                return true;
            }

            // Coop trails use the multiplayer-style roster even if the managed flag is late.
            return GameData.Instance != null && GameData.Instance.coopTrailID > 0;
        }

        private static void AddPlayerIfActive(
            HashSet<int> activePlayerIds,
            int playerId,
            GamePlayerManagerAPI playerApi)
        {
            if (playerApi.IsPlayerIdValid(playerId) && !HasPlayerLost(playerId, playerApi))
                activePlayerIds.Add(playerId);
        }

        private static bool HasPlayerLost(int playerId, GamePlayerManagerAPI playerApi)
        {
            return !playerApi.TryGetPlayerResourcesById(playerId, out GamePlayerResources* resources) ||
                   resources->r_WinLossState == WinLossState.Loss;
        }

        private static bool IsRelevantEntityState(AliveState state)
        {
            // NeedsInit already establishes ownership; paused entities remain in the simulation.
            return state == AliveState.NeedsInit ||
                   state == AliveState.IsAlive ||
                   state == AliveState.Paused;
        }

        private static int ReadByte(byte[] values, int playerId)
        {
            return values != null && playerId >= 0 && playerId < values.Length
                ? values[playerId]
                : int.MinValue;
        }

        private static int ReadInt16(short[] values, int playerId)
        {
            return values != null && playerId >= 0 && playerId < values.Length
                ? values[playerId]
                : int.MinValue;
        }
    }
}
