// Feature: Applies the lobby-selected Lord health multipliers after Vanilla initialization.
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;

namespace ExtraFeatures
{
    internal sealed unsafe class LordHealthRuntime : IDisposable
    {
        private const int FirstPlayerId = 1;
        private const int LastPlayerId = 8;
        private const int ScanTickInterval = 10;

        private readonly ManualLogSource log;
        private readonly ExtraFeaturesViewModel settings;
        private readonly Dictionary<int, uint> appliedLordGlobalIds = new Dictionary<int, uint>();
        private readonly HashSet<int> warnedPlayers = new HashSet<int>();

        private bool initialized;
        private bool mapActive;
        private int humanPercent = LordHealthMultiplierPolicy.DefaultPercent;
        private int aiPercent = LordHealthMultiplierPolicy.DefaultPercent;

        public LordHealthRuntime(ManualLogSource log, ExtraFeaturesViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public void Initialize()
        {
            if (initialized)
                return;

            GameTimeManagerAPI.Instance.OnTick += OnGameTick;
            initialized = true;
        }

        public void BeginMap()
        {
            humanPercent = LordHealthMultiplierPolicy.NormalizePercent(settings.HumanLordHealthPercent);
            aiPercent = LordHealthMultiplierPolicy.NormalizePercent(settings.AILordHealthPercent);
            appliedLordGlobalIds.Clear();
            warnedPlayers.Clear();
            mapActive = true;

            Shared.DebugLogHelper.LogDebug(
                log,
                $"Extra Features Lord health initialized for this map: humans={humanPercent}%, AI={aiPercent}%.");
            ApplyAvailableLords();
        }

        public void ResetMapState()
        {
            mapActive = false;
            appliedLordGlobalIds.Clear();
            warnedPlayers.Clear();
        }

        public void Dispose()
        {
            ResetMapState();
            if (!initialized)
                return;

            GameTimeManagerAPI.Instance.OnTick -= OnGameTick;
            initialized = false;
        }

        private void OnGameTick(int tick)
        {
            if (!mapActive || tick % ScanTickInterval != 0)
                return;

            try
            {
                ApplyAvailableLords();
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, $"Extra Features Lord health scan failed: {ex}");
            }
        }

        private void ApplyAvailableLords()
        {
            for (int playerId = FirstPlayerId; playerId <= LastPlayerId; playerId++)
                TryApplyPlayerLord(playerId);
        }

        private void TryApplyPlayerLord(int playerId)
        {
            GamePlayerManagerAPI players = GamePlayerManagerAPI.Instance;
            int unitId = players.GetLordUnitId(playerId);
            int expectedGlobalId = players.GetLordUnitGlobalId(playerId);
            if (unitId <= 0 || expectedGlobalId <= 0)
                return;

            uint globalId = (uint)expectedGlobalId;
            if (appliedLordGlobalIds.TryGetValue(playerId, out uint appliedGlobalId) &&
                appliedGlobalId == globalId)
            {
                return;
            }

            if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* lord) ||
                lord == null ||
                lord->r_GlobalId != globalId ||
                lord->r_UnitChimp != eChimps.CHIMP_TYPE_LORD ||
                lord->r_ControllableForPlayerId != playerId ||
                lord->r_AliveState != AliveState.IsAlive)
            {
                return;
            }

            bool isAI = players.IsAIPlayer(playerId);
            int selectedPercent = isAI ? aiPercent : humanPercent;
            uint baseLordHealth = GameUnitManagerAPI.Instance.GetDefaultHealth(eChimps.CHIMP_TYPE_LORD);
            if (baseLordHealth == 0)
            {
                LogPlayerWarningOnce(playerId, "the Vanilla Lord health table returned zero");
                return;
            }

            int aiHealthPercent = isAI ? ResolveAIHealthPercent(playerId) : 100;
            if (aiHealthPercent <= 0)
                return;

            int enemyHealthPercent = isAI ? ResolveEnemyHealthPercent(players.GetEnemyHealthModifier()) : 100;
            uint vanillaMaximum = LordHealthMultiplierPolicy.CalculateVanillaMaximum(
                baseLordHealth,
                aiHealthPercent,
                enemyHealthPercent);
            uint oldMaximum = lord->r_MaxHealth;
            uint oldCurrent = lord->r_CurrentHealth;
            uint newMaximum = LordHealthMultiplierPolicy.CalculateMaximum(vanillaMaximum, selectedPercent);
            uint newCurrent = LordHealthMultiplierPolicy.CalculateCurrent(oldCurrent, oldMaximum, newMaximum);
            ushort newHealthPercent = LordHealthMultiplierPolicy.CalculateHealthPercent(newCurrent, newMaximum);

            lord->r_MaxHealth = newMaximum;
            lord->r_CurrentHealth = newCurrent;
            lord->r_CurrentHealthPercentage = newHealthPercent;
            lord->r_HealthBarBlocks = (uint)(newHealthPercent / 10);
            appliedLordGlobalIds[playerId] = globalId;

            Shared.DebugLogHelper.LogDebug(
                log,
                $"Extra Features applied Lord health: player={playerId}, globalId={globalId}, " +
                $"controller={(isAI ? "AI" : "human")}, multiplier={selectedPercent}%, " +
                $"health={oldCurrent}/{oldMaximum}->{newCurrent}/{newMaximum}, " +
                $"vanillaMax={vanillaMaximum}, aiBasePercent={aiHealthPercent}%, " +
                $"enemyHealthPercent={enemyHealthPercent}%.");
        }

        private static int ResolveEnemyHealthPercent(EnemyHPModifier modifier)
        {
            switch (modifier)
            {
                case EnemyHPModifier.Weak:
                    return 66;
                case EnemyHPModifier.Strong:
                    return 125;
                case EnemyHPModifier.VeryStrong:
                    return 150;
                default:
                    return 100;
            }
        }

        private int ResolveAIHealthPercent(int playerId)
        {
            int aiLordId = (int)GamePlayerManagerAPI.Instance.GetAILord(playerId);
            try
            {
                var aics = GameAIManagerAPI.Instance.GetAICArray();
                if (aiLordId <= 0 || aiLordId >= aics.Length)
                {
                    LogPlayerWarningOnce(playerId, $"AI Lord index {aiLordId} is outside the AIC array");
                    return 0;
                }

                int percent = aics.GetValue(aiLordId).lord_hps_percent;
                if (percent <= 0)
                {
                    LogPlayerWarningOnce(playerId, $"AI Lord index {aiLordId} has invalid lord_hps_percent={percent}");
                    return 0;
                }

                return percent;
            }
            catch (Exception ex)
            {
                LogPlayerWarningOnce(playerId, $"AI Lord health data could not be read: {ex.Message}");
                return 0;
            }
        }

        private void LogPlayerWarningOnce(int playerId, string reason)
        {
            if (!warnedPlayers.Add(playerId))
                return;

            Shared.DebugLogHelper.LogWarning(
                log,
                $"Extra Features skipped Lord health for player {playerId}: {reason}.");
        }
    }
}
