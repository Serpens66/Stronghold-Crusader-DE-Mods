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
        private void ApplyStartTroopDefaultPatches()
        {
            PatchAiStartTroopDefaults();
            LogDebug("Human start troop default patching is disabled because the extender API can crash the game.");
        }

        private void RestoreStartTroopDefaultPatches()
        {
            RestoreAiStartTroopDefaults();
            RestoreHumanStartTroopDefaults();
        }

        private unsafe void PatchAiStartTroopDefaults()
        {
            int factor = settings.MultiplyStartTroopsAI;
            InternalAIC* aicArray = GetAicArrayPointer(out int lordCount);
            if (aicArray == null || lordCount <= 1)
                return;

            EnsureOriginalAiStartTroops(aicArray, lordCount);

            for (int lordIndex = 1; lordIndex < lordCount; lordIndex++)
            {
                int* values = GetAiStartTroopsPointer(&aicArray[lordIndex]);
                for (int modeIndex = 0; modeIndex < AiStartTroopModeCount; modeIndex++)
                    PatchAiStartTroopMode(values, lordIndex, modeIndex, factor);
            }

            LogDebug("Patched AI lord start troop defaults with factor", factor);
        }

        private unsafe void PatchAiStartTroopMode(int* values, int lordIndex, int modeIndex, int factor)
        {
            int offset = modeIndex * AiStartTroopFieldCountPerMode;
            if (factor <= 0)
            {
                for (int i = 0; i < AiStartTroopFieldCountPerMode; i++)
                    values[offset + i] = originalAiStartTroops[lordIndex, offset + i];
                return;
            }

            List<int> originalTroops = new List<int>(AiStartTroopFieldCountPerMode);
            for (int i = 0; i < AiStartTroopFieldCountPerMode; i++)
            {
                int troop = originalAiStartTroops[lordIndex, offset + i];
                if (troop > 0)
                    originalTroops.Add(troop);
            }

            for (int i = 0; i < AiStartTroopFieldCountPerMode; i++)
                values[offset + i] = 0;

            if (originalTroops.Count == 0)
                return;

            int targetCount = Math.Min(AiStartTroopFieldCountPerMode, originalTroops.Count * factor);
            for (int i = 0; i < targetCount; i++)
                values[offset + i] = originalTroops[i % originalTroops.Count];
        }

        private unsafe void RestoreAiStartTroopDefaults()
        {
            if (originalAiStartTroops == null)
                return;

            InternalAIC* aicArray = GetAicArrayPointer(out int lordCount);
            if (aicArray == null || lordCount <= 1)
                return;

            int storedLordCount = originalAiStartTroops.GetLength(0);
            int limit = Math.Min(lordCount, storedLordCount);
            for (int lordIndex = 1; lordIndex < limit; lordIndex++)
            {
                int* values = GetAiStartTroopsPointer(&aicArray[lordIndex]);
                for (int fieldIndex = 0; fieldIndex < AiStartTroopFieldCount; fieldIndex++)
                    values[fieldIndex] = originalAiStartTroops[lordIndex, fieldIndex];
            }
        }

        private unsafe void EnsureOriginalAiStartTroops(InternalAIC* aicArray, int lordCount)
        {
            if (originalAiStartTroops != null && originalAiStartTroops.GetLength(0) == lordCount)
                return;

            originalAiStartTroops = new int[lordCount, AiStartTroopFieldCount];
            for (int lordIndex = 1; lordIndex < lordCount; lordIndex++)
            {
                int* values = GetAiStartTroopsPointer(&aicArray[lordIndex]);
                for (int fieldIndex = 0; fieldIndex < AiStartTroopFieldCount; fieldIndex++)
                    originalAiStartTroops[lordIndex, fieldIndex] = values[fieldIndex];
            }
        }

        private unsafe InternalAIC* GetAicArrayPointer(out int lordCount)
        {
            lordCount = Enum.GetValues(typeof(AILords)).Length - 1;
            ulong address = (ulong)GameAIManagerAPI.Instance.GetAICArray().GetArrayAddress();
            if (address == 0)
            {
                LogDebug("AIC array address is null; cannot patch AI start troops.");
                return null;
            }

            return (InternalAIC*)address;
        }

        private unsafe int* GetAiStartTroopsPointer(InternalAIC* aic)
        {
            return &aic->starting_troops_normal1;
        }

        private void PatchHumanStartTroopDefaults()
        {
            LogDebug("Skipping human start troop default patching; GetPlayerSkirmishDefaultUnitsAmount is unsafe.");
        }

        private void RestoreHumanStartTroopDefaults()
        {
            GamePlayerManagerAPI player = GamePlayerManagerAPI.Instance;
            foreach (KeyValuePair<eChimps, uint> entry in originalHumanStartTroops)
                player.SetPlayerSkirmishDefaultUnitsAmount(entry.Key, entry.Value);
        }

        private static uint MultiplyClamped(uint value, int factor)
        {
            if (factor <= 0)
                return value;

            ulong result = (ulong)value * (uint)factor;
            return result > uint.MaxValue ? uint.MaxValue : (uint)result;
        }

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

                    if (multiplier > 1)
                    {
                        Dictionary<eChimps, int> playerCounts = ReadDefaultStartTroops(playerId, isAI);
                        if (playerCounts == null)
                        {
                            plan.PendingPlayers.Add(new PendingStartTroopPlayer(playerId, isAI, multiplier));
                            LogDebug("Scheduling delayed start troop count for player", playerId, "multiplier", multiplier);
                            return;
                        }

                        SpawnMultipliedStartTroops(playerId, playerCounts, multiplier, "defaults");
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
                LogDebug("Running delayed start troop count for", plan.PendingPlayers.Count, "players");
                Dictionary<int, Dictionary<eChimps, int>> troopCounts = CountSoldiersForPlayers();
                foreach (PendingStartTroopPlayer pending in plan.PendingPlayers)
                {
                    if (!GamePlayerManagerAPI.Instance.IsPlayerIdValid(pending.PlayerId) || !HasKeep(pending.PlayerId))
                    {
                        LogDebug("Delayed start troop count skipped; player no longer valid or has no keep:", pending.PlayerId);
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

        private Dictionary<eChimps, int> ReadDefaultStartTroops(int playerId, bool isAI)
        {
            try
            {
                if (!isAI)
                {
                    LogDebug("Human start troop defaults are not read because GetPlayerSkirmishDefaultUnitsAmount is unsafe.");
                    return null;
                }

                Dictionary<eChimps, int> troops = ReadAiDefaultStartTroops(playerId);
                if (troops == null || troops.Count == 0)
                    return null;

                LogStartTroopDefaults(playerId, isAI, troops);
                return troops;
            }
            catch (Exception ex)
            {
                LogDebug("Could not read start troop defaults for player", playerId, ":", ex.Message);
                return null;
            }
        }

        private unsafe Dictionary<eChimps, int> ReadAiDefaultStartTroops(int playerId)
        {
            if (!TryResolveAiLord(playerId, out AILords lord, out string lordName))
                return null;

            InternalAIC* aicArray = GetAicArrayPointer(out int lordCount);
            int lordIndex = (int)lord;
            if (aicArray == null || lordIndex <= 0 || lordIndex >= lordCount)
            {
                LogDebug("AI player", playerId, "lord index is outside AIC array:", lordName, lordIndex, "count", lordCount);
                return null;
            }

            int* values = GetAiStartTroopsPointer(&aicArray[lordIndex]);
            int offset = GetAiStartTroopModeOffset();
            LogAiStartTroopModes(playerId, lordName, values);
            Dictionary<eChimps, int> troops = CountStartTroopMode(values, offset);
            LogDebug("Read AI start troop defaults for player", playerId, "lord", lordName, "modeOffset", offset);
            if (troops.Count == 0)
            {
                LogDebug("AI start troop defaults are empty for selected mode; delayed count fallback required for player", playerId, "lord", lordName);
                DumpAllAiStartTroopDefaultsOnce();
            }

            return troops;
        }

        private bool TryResolveAiLord(int playerId, out AILords lord, out string lordName)
        {
            if (TryResolveAiLordFromPlayerManager(playerId, out lord, out lordName))
                return true;

            lordName = GameAIManagerAPI.Instance.GetCustomAILordNameByPlayerId(playerId);
            if (string.IsNullOrWhiteSpace(lordName))
            {
                LogDebug("AI player", playerId, "has no readable lord name.");
                lord = default(AILords);
                return false;
            }

            if (!Enum.TryParse(lordName, true, out lord))
            {
                LogDebug("AI player", playerId, "lord name cannot be mapped to AILords:", lordName);
                return false;
            }

            LogDebug("Resolved AI lord via custom slot name for player", playerId, lordName, lord);
            return true;
        }

        private bool TryResolveAiLordFromPlayerManager(int playerId, out AILords lord, out string lordName)
        {
            int[] candidates = { playerId - 1, playerId };
            AILords resolvedLord = default(AILords);
            string resolvedLordName = string.Empty;
            foreach (int candidatePlayerId in candidates)
            {
                try
                {
                    var gameLord = GamePlayerManagerAPI.Instance.GetAILord(candidatePlayerId);
                    lordName = gameLord.ToString();
                    LogDebug("GetAILord candidate for player", playerId, "arg", candidatePlayerId, "=>", lordName, Convert.ToInt32(gameLord));

                    if (Enum.TryParse(lordName, true, out lord) && (int)lord > 0 && IsDefinedEnumValue<AILords>(lord))
                    {
                        if (string.IsNullOrEmpty(resolvedLordName))
                        {
                            resolvedLord = lord;
                            resolvedLordName = lordName;
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogDebug("GetAILord failed for player", playerId, "arg", candidatePlayerId, ":", ex.Message);
                }
            }

            lord = resolvedLord;
            lordName = resolvedLordName;
            return !string.IsNullOrEmpty(lordName);
        }

        private unsafe Dictionary<eChimps, int> CountStartTroopMode(int* values, int offset)
        {
            Dictionary<eChimps, int> troops = new Dictionary<eChimps, int>();
            for (int i = 0; i < AiStartTroopFieldCountPerMode; i++)
            {
                int rawTroop = values[offset + i];
                if (rawTroop <= 0 || !IsDefinedEnumValue<eChimps>(rawTroop))
                    continue;

                eChimps unit = (eChimps)rawTroop;
                if (!SoldierChimps.Contains(unit))
                    continue;

                troops.TryGetValue(unit, out int count);
                troops[unit] = count + 1;
            }

            return troops;
        }

        private int GetAiStartTroopModeOffset()
        {
            // The installed Script Extender build exposes the troop tables, but not the skirmish mode accessor.
            // Keep this read-only and log all modes so we can map the correct block from real game logs.
            return 0;
        }

        private unsafe void LogAiStartTroopModes(int playerId, string lordName, int* values)
        {
            for (int modeIndex = 0; modeIndex < AiStartTroopModeCount; modeIndex++)
            {
                Dictionary<eChimps, int> troops = CountStartTroopMode(values, modeIndex * AiStartTroopFieldCountPerMode);
                LogDebug("AI start troop mode", modeIndex, "player", playerId, "lord", lordName, FormatTroopCounts(troops));
            }
        }

        private unsafe void DumpAllAiStartTroopDefaultsOnce()
        {
            if (dumpedAllAiStartTroopDefaults)
                return;

            dumpedAllAiStartTroopDefaults = true;

            InternalAIC* aicArray = GetAicArrayPointer(out int lordCount);
            if (aicArray == null || lordCount <= 1)
            {
                LogDebug("AI start troop dump skipped; AIC array is unavailable.");
                return;
            }

            int nonEmptyModes = 0;
            int emptyModes = 0;
            LogDebug("AI start troop full dump begin; lords", lordCount - 1, "modesPerLord", AiStartTroopModeCount);

            for (int lordIndex = 1; lordIndex < lordCount; lordIndex++)
            {
                AILords lord = (AILords)lordIndex;
                int* values = GetAiStartTroopsPointer(&aicArray[lordIndex]);
                for (int modeIndex = 0; modeIndex < AiStartTroopModeCount; modeIndex++)
                {
                    Dictionary<eChimps, int> troops = CountStartTroopMode(values, modeIndex * AiStartTroopFieldCountPerMode);
                    if (troops.Count == 0)
                    {
                        emptyModes++;
                        continue;
                    }

                    nonEmptyModes++;
                    LogDebug("AI start troop full dump", lord, "mode", modeIndex, FormatTroopCounts(troops));
                }
            }

            LogDebug("AI start troop full dump complete; nonEmptyModes", nonEmptyModes, "emptyModes", emptyModes);
        }

        private void LogStartTroopDefaults(int playerId, bool isAI, Dictionary<eChimps, int> troops)
        {
            LogDebug(isAI ? "AI start troop defaults" : "Human start troop defaults", "player", playerId, FormatTroopCounts(troops));
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
