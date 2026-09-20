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
                LogDebug("Raw AI AddStartTroops:", current.AddStartTroopsAI);
                LogDebug("Raw Human AddStartTroops:", current.AddStartTroopsHuman);
                Dictionary<eChimps, int> aiTroops = ParseEnumAmounts<eChimps>(current.AddStartTroopsAI, 0, 1000);
                Dictionary<eChimps, int> humanTroops = ParseEnumAmounts<eChimps>(current.AddStartTroopsHuman, 0, 1000);
                LogConfiguredTroops("AI AddStartTroops", aiTroops);
                LogConfiguredTroops("Human AddStartTroops", humanTroops);

                StartTroopPlan plan = new StartTroopPlan(aiTroops, humanTroops);
                ForEachActivePlayer(playerId =>
                {
                    bool isAI = GamePlayerManagerAPI.Instance.IsAIPlayer(playerId);
                    int multiplier = isAI ? current.MultiplyStartTroopsAI : current.MultiplyStartTroopsHuman;

                    if (multiplier == 0 || multiplier > 1)
                    {
                        plan.PendingPlayers.Add(new PendingStartTroopPlayer(playerId, multiplier));
                        LogDebug("Scheduling start troop processing for player", playerId, "multiplier", multiplier);
                    }
                });

                if (!plan.HasWork)
                {
                    LogDebug("No start-troop changes are configured.");
                    return;
                }

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
                LogDebug("AddStartTroops failed:", ex);
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
            LogDebug("Waiting for Vanilla to finish spawning and initializing all start troops.");
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
            {
                LogDebug(
                    "Vanilla reported complete start-troop spawning at game tick",
                    gameTick,
                    "waiting one additional simulation tick for unit initialization.");
                return;
            }

            StopWaitingForVanillaStartTroopCompletion();
            ExecuteStartTroopPlan(plan, "Vanilla completion signal");
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
            LogDebug(
                "Vanilla peace time is active; start-troop processing will begin",
                DelayedStartTroopCountSeconds,
                "seconds after it ends.");
        }

        private void OnPeaceTimeWaitTick(int gameTick)
        {
            try
            {
                ProcessPeaceTimeWaitTick(gameTick);
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

        private void ProcessPeaceTimeWaitTick(int gameTick)
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
            LogDebug("Vanilla peace time ended at game tick", gameTick);
            ScheduleDelayedStartTroopProcessing(plan, "peace-time end");
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
            LogDebug("Resuming start-troop plan with legacy timing because", reason);
            if (plan.PendingPlayers.Count > 0)
                ScheduleDelayedStartTroopProcessing(plan, "legacy fallback");
            else
            {
                pendingStartTroopPlan = null;
                SpawnConfiguredStartTroops(plan.AiTroops, plan.HumanTroops);
            }
        }

        private void StartLegacyStartTroopTiming(StartTroopPlan plan, string reason)
        {
            LogDebug("Using legacy start-troop timing because", reason);
            if (vanillaPeaceTimeState.TryGetIsActive(out bool peaceTimeActive) && peaceTimeActive)
                WaitForPeaceTimeEnd(plan);
            else
                ResumeStartTroopPlanWithLegacyTiming(plan, reason);
        }

        private void ScheduleDelayedStartTroopProcessing(StartTroopPlan plan, string origin)
        {
            pendingStartTroopPlan = plan;
            pendingStartTroopTimerHandle = GameTimeManagerAPI.Instance.GetTimerEngine().AddDelayedAction(
                DelayedStartTroopCountMilliseconds,
                RunDelayedStartTroopProcessing,
                string.Empty);

            LogDebug(
                "Scheduled delayed start troop processing in",
                DelayedStartTroopCountMilliseconds,
                "ms after",
                origin,
                "for",
                plan.PendingPlayers.Count,
                "players. Timer is not save/load persistent.");
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
                    LogDebug("Vanilla peace time became active again before start-troop processing.");
                    WaitForPeaceTimeEnd(plan);
                    return;
                }

                ExecuteStartTroopPlan(plan, "legacy delay");
            }
            catch (Exception ex)
            {
                LogDebug("RunDelayedStartTroopProcessing failed:", ex);
            }
        }

        private void ExecuteStartTroopPlan(StartTroopPlan plan, string origin)
        {
            pendingStartTroopPlan = null;
            LogDebug(
                "Running start troop processing after",
                origin,
                "for",
                plan.PendingPlayers.Count,
                "players");

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
                LogDebug("Delayed start troop processing skipped; player no longer valid:", pending.PlayerId);
                return;
            }

            if (pending.Multiplier == 0)
            {
                DeleteSoldiersForPlayer(pending.PlayerId);
                return;
            }

            if (!Shared.ActivePlayerKeepReadiness.TryGetReadyKeep(pending.PlayerId, out _))
            {
                LogDebug("Delayed start troop multiply skipped; player has no keep:", pending.PlayerId);
                return;
            }

            if (troopCounts.TryGetValue(pending.PlayerId, out Dictionary<eChimps, int> playerCounts))
                SpawnMultipliedStartTroops(pending.PlayerId, playerCounts, pending.Multiplier, "completed start-troop count");
            else
                LogDebug("No start troop counts available for player", pending.PlayerId, "multiplier skipped.");
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

                try
                {
                    if (GameUnitManagerAPI.Instance.DeleteUnitSafe(unitId))
                        deleted++;
                    else
                        failed++;
                }
                catch (Exception ex)
                {
                    failed++;
                    LogError("Start Conditions could not delete one start soldier; remaining units continue:", unitId, ex);
                }
            }

            LogDebug("Deleted start soldiers for player", playerId, "deleted", deleted, "failed", failed);
        }

        private void SpawnConfiguredStartTroops(Dictionary<eChimps, int> aiTroops, Dictionary<eChimps, int> humanTroops)
        {
            LogDebug("Applying configured AddStartTroops after multiplier phase");
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
