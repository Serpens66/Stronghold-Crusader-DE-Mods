using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.EventAPI.Player;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;

namespace StartConditions
{
    public sealed partial class StartConditionsRuntime
    {
        // SetPlayerSkirmishDefaultUnitsAmount and the old InternalAIC table patch do not
        // reliably affect live start troops, so this mod only uses delayed unit counting.

        private void AddStartTroops()
        {
            try
            {
                CancelPendingStartTroopProcessing();
                LogDebug("Raw AI AddStartTroops:", settings.AddStartTroopsAI);
                LogDebug("Raw Human AddStartTroops:", settings.AddStartTroopsHuman);
                Dictionary<eChimps, int> aiTroops = ParseEnumAmounts<eChimps>(settings.AddStartTroopsAI);
                Dictionary<eChimps, int> humanTroops = ParseEnumAmounts<eChimps>(settings.AddStartTroopsHuman);
                LogConfiguredTroops("AI AddStartTroops", aiTroops);
                LogConfiguredTroops("Human AddStartTroops", humanTroops);

                StartTroopPlan plan = new StartTroopPlan(aiTroops, humanTroops);
                ForEachAlivePlayer(playerId =>
                {
                    bool isAI = GamePlayerManagerAPI.Instance.IsAIPlayer(playerId);
                    int multiplier = isAI ? settings.MultiplyStartTroopsAI : settings.MultiplyStartTroopsHuman;

                    if (multiplier == 0 || multiplier > 1)
                    {
                        plan.PendingPlayers.Add(new PendingStartTroopPlayer(playerId, isAI, multiplier));
                        LogDebug("Scheduling delayed start troop processing for player", playerId, "multiplier", multiplier);
                    }
                });

                if (plan.PendingPlayers.Count > 0)
                    ScheduleDelayedStartTroopProcessing(plan);
                else
                    SpawnConfiguredStartTroops(aiTroops, humanTroops);
            }
            catch (Exception ex)
            {
                LogDebug("AddStartTroops failed:", ex);
            }
        }

        private void ScheduleDelayedStartTroopProcessing(StartTroopPlan plan)
        {
            pendingStartTroopPlan = plan;
            pendingStartTroopTimerHandle = GameTimeManagerAPI.Instance.GetTimerEngine().AddDelayedAction(
                DelayedStartTroopCountMilliseconds,
                RunDelayedStartTroopProcessing,
                string.Empty);

            LogDebug("Scheduled delayed start troop processing in", DelayedStartTroopCountMilliseconds, "ms for", plan.PendingPlayers.Count, "players. Timer is not save/load persistent.");
        }

        private void RunDelayedStartTroopProcessing()
        {
            StartTroopPlan plan = pendingStartTroopPlan;
            pendingStartTroopPlan = null;
            pendingStartTroopTimerHandle = null;

            if (plan == null)
                return;

            try
            {
                LogDebug("Running delayed start troop processing for", plan.PendingPlayers.Count, "players");
                Dictionary<int, Dictionary<eChimps, int>> troopCounts = CountSoldiersForPlayers();
                foreach (PendingStartTroopPlayer pending in plan.PendingPlayers)
                {
                    if (!GamePlayerManagerAPI.Instance.IsPlayerIdValid(pending.PlayerId))
                    {
                        LogDebug("Delayed start troop processing skipped; player no longer valid:", pending.PlayerId);
                        continue;
                    }

                    if (pending.Multiplier == 0)
                    {
                        DeleteSoldiersForPlayer(pending.PlayerId);
                        continue;
                    }

                    if (!HasKeep(pending.PlayerId))
                    {
                        LogDebug("Delayed start troop multiply skipped; player has no keep:", pending.PlayerId);
                        continue;
                    }

                    if (troopCounts.TryGetValue(pending.PlayerId, out Dictionary<eChimps, int> playerCounts))
                        SpawnMultipliedStartTroops(pending.PlayerId, playerCounts, pending.Multiplier, "delayed count");
                    else
                        LogDebug("No start troop counts available for player", pending.PlayerId, "multiplier skipped.");
                }

                SpawnConfiguredStartTroops(plan.AiTroops, plan.HumanTroops);
            }
            catch (Exception ex)
            {
                LogDebug("RunDelayedStartTroopProcessing failed:", ex);
            }
        }

        private void CancelPendingStartTroopProcessing()
        {
            if (!string.IsNullOrEmpty(pendingStartTroopTimerHandle))
            {
                try
                {
                    GameTimeManagerAPI.Instance.GetTimerEngine().RemoveAction(pendingStartTroopTimerHandle);
                    LogDebug("Cancelled pending start troop timer", pendingStartTroopTimerHandle);
                }
                catch (Exception ex)
                {
                    LogDebug("Could not cancel pending start troop timer:", ex.Message);
                }
            }

            pendingStartTroopTimerHandle = null;
            pendingStartTroopPlan = null;
        }

        private void SpawnMultipliedStartTroops(int playerId, Dictionary<eChimps, int> playerCounts, int multiplier, string source)
        {
            foreach (KeyValuePair<eChimps, int> entry in playerCounts)
            {
                int amount = entry.Value * (multiplier - 1);
                if (amount > 0)
                {
                    LogDebug("Spawning multiplied start troops from", source, "for player", playerId, entry.Key, entry.Value, "x", multiplier, "=> add", amount);
                    SpawnUnitsNearKeep(playerId, entry.Key, amount);
                }
            }
        }

        private void DeleteSoldiersForPlayer(int playerId)
        {
            List<int> unitIds = new List<int>();
            GameUnitManagerAPI.Instance.GetAllUnits(unitIds, AliveState.IsAlive);
            int deleted = 0;
            int failed = 0;

            foreach (int unitId in unitIds)
            {
                if (unitId <= 0)
                    continue;

                if (GameUnitManagerAPI.Instance.GetOwner(unitId) != playerId)
                    continue;

                eChimps unitType = GameUnitManagerAPI.Instance.GetType(unitId);
                if (!SoldierChimps.Contains(unitType))
                    continue;

                if (GameUnitManagerAPI.Instance.DeleteUnitSafe(unitId))
                    deleted++;
                else
                    failed++;
            }

            LogDebug("Deleted start soldiers for player", playerId, "deleted", deleted, "failed", failed);
        }

        private void SpawnConfiguredStartTroops(Dictionary<eChimps, int> aiTroops, Dictionary<eChimps, int> humanTroops)
        {
            LogDebug("Applying configured AddStartTroops after multiplier phase");
            ForEachAlivePlayer(playerId =>
            {
                bool isAI = GamePlayerManagerAPI.Instance.IsAIPlayer(playerId);
                Dictionary<eChimps, int> configuredTroops = isAI ? aiTroops : humanTroops;
                foreach (KeyValuePair<eChimps, int> entry in configuredTroops)
                {
                    if (entry.Value > 0)
                        SpawnUnitsNearKeep(playerId, entry.Key, entry.Value);
                }
            });
        }

        private Dictionary<int, Dictionary<eChimps, int>> CountSoldiersForPlayers()
        {
            Dictionary<int, Dictionary<eChimps, int>> troopCounts = new Dictionary<int, Dictionary<eChimps, int>>();
            List<int> unitIds = new List<int>();
            GameUnitManagerAPI.Instance.GetAllUnits(unitIds, AliveState.IsAlive);
            int skippedInvalidUnitIds = 0;

            foreach (int unitId in unitIds)
            {
                if (unitId <= 0)
                {
                    skippedInvalidUnitIds++;
                    continue;
                }

                int playerId = GameUnitManagerAPI.Instance.GetOwner(unitId);
                eChimps unitType = GameUnitManagerAPI.Instance.GetType(unitId);
                if (!SoldierChimps.Contains(unitType))
                    continue;

                if (!troopCounts.TryGetValue(playerId, out Dictionary<eChimps, int> playerCounts))
                {
                    playerCounts = new Dictionary<eChimps, int>();
                    troopCounts[playerId] = playerCounts;
                }

                playerCounts.TryGetValue(unitType, out int count);
                playerCounts[unitType] = count + 1;
            }

            if (skippedInvalidUnitIds > 0)
                LogDebug("CountSoldiersForPlayers skipped invalid unit ids:", skippedInvalidUnitIds);

            return troopCounts;
        }

        private void SpawnUnitsNearKeep(int playerId, eChimps unitType, int amount)
        {
            if (!TryGetTileNearKeep(playerId, out int x, out int y, out int height))
            {
                LogDebug("Could not find spawn tile near keep for player", playerId, "unit", unitType, "amount", amount);
                return;
            }

            LogDebug("CreateLocal", amount, unitType, "for player", playerId, "at", x, y, height);
            for (int i = 0; i < amount; i++)
                GameUnitManagerAPI.Instance.CreateUnitLocal(playerId, playerId, x, y, height, unitType);
        }

        private bool TryGetTileNearKeep(int playerId, out int x, out int y, out int height)
        {
            x = 0;
            y = 0;
            height = 0;

            if (!GamePlayerManagerAPI.Instance.IsPlayerIdValid(playerId) || !HasKeep(playerId))
            {
                LogDebug("Cannot find keep spawn tile; player is invalid or has no keep:", playerId);
                return false;
            }

            var door = GamePlayerManagerAPI.Instance.GetPlayerKeepDoorPosition(playerId);
            var position = GameTileManagerAPI.Instance.GetNearestUnoccupiedTile(door.X, door.Y, 12);
            int tileId = GameTileManagerAPI.Instance.GetTileId(position.X, position.Y);

            if (!GameTileManagerAPI.Instance.IsValidTileId(tileId) || !GameTileManagerAPI.Instance.IsTileWalkableAndUnoccupied(tileId))
            {
                LogDebug("Nearest keep tile is not valid/walkable/unoccupied for player", playerId, "door", door.X, door.Y, "candidate", position.X, position.Y, "tile", tileId);
                return false;
            }

            x = position.X;
            y = position.Y;
            height = GameTileManagerAPI.Instance.GetTileHeight(tileId);
            return true;
        }


        private sealed class StartTroopPlan
        {
            public readonly Dictionary<eChimps, int> AiTroops;
            public readonly Dictionary<eChimps, int> HumanTroops;
            public readonly List<PendingStartTroopPlayer> PendingPlayers = new List<PendingStartTroopPlayer>();

            public StartTroopPlan(Dictionary<eChimps, int> aiTroops, Dictionary<eChimps, int> humanTroops)
            {
                AiTroops = aiTroops;
                HumanTroops = humanTroops;
            }
        }

        private sealed class PendingStartTroopPlayer
        {
            public readonly int PlayerId;
            public readonly bool IsAI;
            public readonly int Multiplier;

            public PendingStartTroopPlayer(int playerId, bool isAI, int multiplier)
            {
                PlayerId = playerId;
                IsAI = isAI;
                Multiplier = multiplier;
            }
        }
    }
}
