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
        // reliably affect live start troops, so this mod counts them after Vanilla finishes spawning.

        private void AddStartTroops()
        {
            try
            {
                IStartConditionsSettings current = EffectiveSettings;
                CancelPendingStartTroopProcessing();
                Dictionary<eChimps, int> aiTroops = ParseEnumAmounts<eChimps>(current.AddStartTroopsAI, 0, 1000);
                Dictionary<eChimps, int> humanTroops = ParseEnumAmounts<eChimps>(current.AddStartTroopsHuman, 0, 1000);

                StartTroopPlan plan = new StartTroopPlan(aiTroops, humanTroops);
                ForEachActivePlayer(playerId =>
                {
                    bool isAI = GamePlayerManagerAPI.Instance.IsAIPlayer(playerId);
                    int multiplier = isAI ? current.MultiplyStartTroopsAI : current.MultiplyStartTroopsHuman;

                    if (multiplier == 0 || multiplier > 1)
                    {
                        plan.PendingPlayers.Add(new PendingStartTroopPlayer(playerId, multiplier));
                    }
                });

                if (!plan.HasWork)
                    return;

                if (plan.PendingPlayers.Count > 0)
                {
                    if (vanillaStartTroopSpawnState.TryGetIsComplete(out _))
                        WaitForVanillaStartTroopCompletion(plan);
                    else
                        StartLegacyStartTroopTiming(plan, "Vanilla's completion state is unavailable");
                }
                else if (vanillaPeaceTimeState.TryGetIsActive(out bool peaceTimeActive) && peaceTimeActive)
                {
                    WaitForPeaceTimeEnd(plan);
                }
                else
                {
                    SpawnConfiguredStartTroops(aiTroops, humanTroops);
                }
            }
            catch (Exception ex)
            {
                LogError("AddStartTroops failed:", ex);
            }
        }

        private void WaitForVanillaStartTroopCompletion(StartTroopPlan plan)
        {
            pendingStartTroopPlan = plan;
            startTroopCompletionWaitState.Reset();
            if (waitingForVanillaStartTroopCompletion)
                return;

            GameTimeManagerAPI.Instance.OnTick += OnVanillaStartTroopCompletionTick;
            waitingForVanillaStartTroopCompletion = true;
        }

        private void OnVanillaStartTroopCompletionTick(int gameTick)
        {
            try
            {
                ProcessVanillaStartTroopCompletionTick(gameTick);
            }
            catch (Exception ex)
            {
                StartTroopPlan plan = pendingStartTroopPlan;
                StopWaitingForVanillaStartTroopCompletion();
                if (plan != null)
                    StartLegacyStartTroopTiming(plan, $"the completion tick handler failed: {ex.Message}");
                else
                    LogError("Vanilla start-troop completion processing failed after consuming its plan:", ex);
            }
        }

        private void ProcessVanillaStartTroopCompletionTick(int gameTick)
        {
            StartTroopPlan plan = pendingStartTroopPlan;
            if (plan == null)
            {
                StopWaitingForVanillaStartTroopCompletion();
                return;
            }

            if (!vanillaStartTroopSpawnState.TryGetIsComplete(out bool complete))
            {
                StopWaitingForVanillaStartTroopCompletion();
                StartLegacyStartTroopTiming(plan, "Vanilla's completion state became unavailable");
                return;
            }

            StartTroopCompletionWaitResult result =
                startTroopCompletionWaitState.Observe(complete, gameTick);
            if (result == StartTroopCompletionWaitResult.Waiting)
                return;

            if (result == StartTroopCompletionWaitResult.Settling)
                return;

            StopWaitingForVanillaStartTroopCompletion();
            ExecuteStartTroopPlan(plan);
        }

        private void StopWaitingForVanillaStartTroopCompletion()
        {
            if (waitingForVanillaStartTroopCompletion)
                GameTimeManagerAPI.Instance.OnTick -= OnVanillaStartTroopCompletionTick;

            waitingForVanillaStartTroopCompletion = false;
            startTroopCompletionWaitState.Reset();
        }

        private void WaitForPeaceTimeEnd(StartTroopPlan plan)
        {
            pendingStartTroopPlan = plan;
            if (waitingForPeaceTimeEnd)
                return;

            GameTimeManagerAPI.Instance.OnTick += OnPeaceTimeWaitTick;
            waitingForPeaceTimeEnd = true;
        }

        private void OnPeaceTimeWaitTick(int gameTick)
        {
            try
            {
                ProcessPeaceTimeWaitTick();
            }
            catch (Exception ex)
            {
                LogError("Start Conditions peace-time wait tick failed; attempting legacy timing:", ex);
                StartTroopPlan plan = pendingStartTroopPlan;
                try
                {
                    StopWaitingForPeaceTimeEnd();
                    if (plan != null)
                        ResumeStartTroopPlanWithLegacyTiming(plan, "the peace-time tick handler failed");
                }
                catch (Exception fallbackException)
                {
                    pendingStartTroopTimerHandle = null;
                    pendingStartTroopPlan = null;
                    waitingForPeaceTimeEnd = false;
                    LogError("Start Conditions could not recover its start-troop plan:", fallbackException);
                }
            }
        }

        private void ProcessPeaceTimeWaitTick()
        {
            StartTroopPlan plan = pendingStartTroopPlan;
            if (plan == null)
            {
                StopWaitingForPeaceTimeEnd();
                return;
            }

            if (!vanillaPeaceTimeState.TryGetIsActive(out bool peaceTimeActive))
            {
                StopWaitingForPeaceTimeEnd();
                ResumeStartTroopPlanWithLegacyTiming(plan, "peace-time state became unavailable");
                return;
            }

            if (peaceTimeActive)
                return;

            StopWaitingForPeaceTimeEnd();
            ScheduleDelayedStartTroopProcessing(plan);
        }

        private void StopWaitingForPeaceTimeEnd()
        {
            if (!waitingForPeaceTimeEnd)
                return;

            GameTimeManagerAPI.Instance.OnTick -= OnPeaceTimeWaitTick;
            waitingForPeaceTimeEnd = false;
        }

        private void ResumeStartTroopPlanWithLegacyTiming(StartTroopPlan plan, string reason)
        {
            if (plan.PendingPlayers.Count > 0)
                ScheduleDelayedStartTroopProcessing(plan);
            else
            {
                pendingStartTroopPlan = null;
                SpawnConfiguredStartTroops(plan.AiTroops, plan.HumanTroops);
            }
        }

        private void StartLegacyStartTroopTiming(StartTroopPlan plan, string reason)
        {
            if (vanillaPeaceTimeState.TryGetIsActive(out bool peaceTimeActive) && peaceTimeActive)
                WaitForPeaceTimeEnd(plan);
            else
                ResumeStartTroopPlanWithLegacyTiming(plan, reason);
        }

        private void ScheduleDelayedStartTroopProcessing(StartTroopPlan plan)
        {
            pendingStartTroopPlan = plan;
            pendingStartTroopTimerHandle = GameTimeManagerAPI.Instance.GetTimerEngine().AddDelayedAction(
                DelayedStartTroopCountMilliseconds,
                RunDelayedStartTroopProcessing,
                string.Empty);

        }

        private void RunDelayedStartTroopProcessing()
        {
            StartTroopPlan plan = pendingStartTroopPlan;
            pendingStartTroopTimerHandle = null;

            if (plan == null)
                return;

            try
            {
                if (vanillaPeaceTimeState.TryGetIsActive(out bool peaceTimeActive) && peaceTimeActive)
                {
                    WaitForPeaceTimeEnd(plan);
                    return;
                }

                ExecuteStartTroopPlan(plan);
            }
            catch (Exception ex)
            {
                LogError("RunDelayedStartTroopProcessing failed:", ex);
            }
        }

        private void ExecuteStartTroopPlan(StartTroopPlan plan)
        {
            pendingStartTroopPlan = null;

            var troopCounts = new Dictionary<int, Dictionary<eChimps, int>>();
            if (plan.PendingPlayers.Count > 0)
                TryRunFeature("start troop counting", () => troopCounts = CountSoldiersForPlayers());
            foreach (PendingStartTroopPlayer pending in plan.PendingPlayers)
            {
                TryRunFeature(
                    $"start troops for player {pending.PlayerId}",
                    () => ProcessDelayedStartTroopsForPlayer(pending, troopCounts));
            }

            SpawnConfiguredStartTroops(plan.AiTroops, plan.HumanTroops);
        }

        private void ProcessDelayedStartTroopsForPlayer(
            PendingStartTroopPlayer pending,
            Dictionary<int, Dictionary<eChimps, int>> troopCounts)
        {
            if (!GamePlayerManagerAPI.Instance.IsPlayerIdValid(pending.PlayerId))
            {
                LogWarning("Start-troop processing skipped because the player is no longer valid:", pending.PlayerId);
                return;
            }

            if (pending.Multiplier == 0)
            {
                DeleteSoldiersForPlayer(pending.PlayerId);
                return;
            }

            if (!Shared.ActivePlayerKeepReadiness.TryGetReadyKeep(pending.PlayerId, out _))
            {
                LogWarning("Start-troop multiplication skipped because the player has no ready Keep:", pending.PlayerId);
                return;
            }

            if (troopCounts.TryGetValue(pending.PlayerId, out Dictionary<eChimps, int> playerCounts))
                SpawnMultipliedStartTroops(pending.PlayerId, playerCounts, pending.Multiplier);
            else
                LogWarning("Start-troop multiplication skipped because no troop counts are available for player", pending.PlayerId);
        }

        private void CancelPendingStartTroopProcessing()
        {
            StopWaitingForVanillaStartTroopCompletion();
            StopWaitingForPeaceTimeEnd();
            if (!string.IsNullOrEmpty(pendingStartTroopTimerHandle))
            {
                try
                {
                    GameTimeManagerAPI.Instance.GetTimerEngine().RemoveAction(pendingStartTroopTimerHandle);
                }
                catch (Exception ex)
                {
                    LogWarning("Could not cancel pending start-troop timer:", ex.Message);
                }
            }

            pendingStartTroopTimerHandle = null;
            pendingStartTroopPlan = null;
        }

        private void SpawnMultipliedStartTroops(int playerId, Dictionary<eChimps, int> playerCounts, int multiplier)
        {
            foreach (KeyValuePair<eChimps, int> entry in playerCounts)
            {
                int amount = entry.Value * (multiplier - 1);
                if (amount > 0)
                {
                    TryRunFeature(
                        $"multiplied {entry.Key} start troops for player {playerId}",
                        () => SpawnUnitsNearKeep(playerId, entry.Key, amount));
                }
            }
        }

        private void DeleteSoldiersForPlayer(int playerId)
        {
            List<int> unitIds = new List<int>();
            GameUnitManagerAPI.Instance.GetAllUnits(unitIds, AliveState.IsAlive);
            int rejected = 0;

            foreach (int unitId in unitIds)
            {
                if (unitId <= 0)
                    continue;

                if (GameUnitManagerAPI.Instance.GetOwner(unitId) != playerId)
                    continue;

                eChimps unitType = GameUnitManagerAPI.Instance.GetType(unitId);
                if (!SoldierChimps.Contains(unitType))
                    continue;

                try
                {
                    if (!GameUnitManagerAPI.Instance.DeleteUnitSafe(unitId))
                        rejected++;
                }
                catch (Exception ex)
                {
                    LogError("Start Conditions could not delete one start soldier; remaining units continue:", unitId, ex);
                }
            }

            if (rejected > 0)
                LogWarning("Start Conditions could not mark", rejected, "start soldiers for deletion for player", playerId);
        }

        private void SpawnConfiguredStartTroops(Dictionary<eChimps, int> aiTroops, Dictionary<eChimps, int> humanTroops)
        {
            ForEachActivePlayer(playerId =>
            {
                bool isAI = GamePlayerManagerAPI.Instance.IsAIPlayer(playerId);
                Dictionary<eChimps, int> configuredTroops = isAI ? aiTroops : humanTroops;
                foreach (KeyValuePair<eChimps, int> entry in configuredTroops)
                {
                    if (entry.Value > 0)
                    {
                        TryRunFeature(
                            $"configured {entry.Key} start troops for player {playerId}",
                            () => SpawnUnitsNearKeep(playerId, entry.Key, entry.Value));
                    }
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
                LogWarning("Start-troop counting skipped invalid unit IDs:", skippedInvalidUnitIds);

            return troopCounts;
        }

        private void SpawnUnitsNearKeep(int playerId, eChimps unitType, int amount)
        {
            if (!TryGetTileNearKeep(playerId, out int x, out int y, out int height))
            {
                LogWarning("Could not find a spawn tile near the Keep for player", playerId, "unit", unitType, "amount", amount);
                return;
            }

            bool isolateFromAI = GamePlayerManagerAPI.Instance.IsAIPlayer(playerId);
            for (int i = 0; i < amount; i++)
            {
                long createdId = GameUnitManagerAPI.Instance.CreateUnitLocal(
                    playerId,
                    playerId,
                    x,
                    y,
                    height,
                    unitType);

                if (isolateFromAI)
                    TryProtectSpawnedAIStartTroop(createdId, playerId, unitType);
            }
        }

        private bool TryGetTileNearKeep(int playerId, out int x, out int y, out int height)
        {
            x = 0;
            y = 0;
            height = 0;

            if (!GamePlayerManagerAPI.Instance.IsPlayerIdValid(playerId) ||
                !Shared.ActivePlayerKeepReadiness.TryGetReadyKeep(playerId, out _))
                return false;

            var door = GamePlayerManagerAPI.Instance.GetPlayerKeepDoorPosition(playerId);
            var position = GameTileManagerAPI.Instance.GetNearestUnoccupiedTile(door.X, door.Y, 12);
            int tileId = GameTileManagerAPI.Instance.GetTileId(position.X, position.Y);

            if (!GameTileManagerAPI.Instance.IsValidTileId(tileId) || !GameTileManagerAPI.Instance.IsTileWalkableAndUnoccupied(tileId))
                return false;

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

            public bool HasWork =>
                PendingPlayers.Count > 0 ||
                HasPositiveAmount(AiTroops) ||
                HasPositiveAmount(HumanTroops);

            public StartTroopPlan(Dictionary<eChimps, int> aiTroops, Dictionary<eChimps, int> humanTroops)
            {
                AiTroops = aiTroops;
                HumanTroops = humanTroops;
            }

            private static bool HasPositiveAmount(Dictionary<eChimps, int> troops)
            {
                foreach (int amount in troops.Values)
                {
                    if (amount > 0)
                        return true;
                }

                return false;
            }
        }

        private sealed class PendingStartTroopPlayer
        {
            public readonly int PlayerId;
            public readonly int Multiplier;

            public PendingStartTroopPlayer(int playerId, int multiplier)
            {
                PlayerId = playerId;
                Multiplier = multiplier;
            }
        }
    }
}
