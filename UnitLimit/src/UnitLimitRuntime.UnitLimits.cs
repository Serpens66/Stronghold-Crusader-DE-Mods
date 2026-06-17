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

namespace UnitLimit
{
    public sealed partial class UnitLimitRuntime
    {
        private struct PendingRecruitmentKey
        {
            public readonly int PlayerId;
            public readonly eChimps UnitType;

            public PendingRecruitmentKey(int playerId, eChimps unitType)
            {
                PlayerId = playerId;
                UnitType = unitType;
            }

            public override bool Equals(object obj)
            {
                if (!(obj is PendingRecruitmentKey other))
                    return false;

                return PlayerId == other.PlayerId && UnitType == other.UnitType;
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (PlayerId * 397) ^ (int)UnitType;
                }
            }
        }

        private sealed class PendingRecruitmentQueue
        {
            private readonly Queue<DateTime> expirations = new Queue<DateTime>();

            public int Count => expirations.Count;

            public void Add(DateTime expiration, int amount)
            {
                for (int i = 0; i < amount; i++)
                    expirations.Enqueue(expiration);
            }

            public bool ConsumeOne()
            {
                if (expirations.Count == 0)
                    return false;

                expirations.Dequeue();
                return true;
            }

            public int RemoveExpired(DateTime now)
            {
                int before = expirations.Count;
                while (expirations.Count > 0 && expirations.Peek() <= now)
                    expirations.Dequeue();

                return before - expirations.Count;
            }
        }

        internal MakeTroopGameActionDecision DecideMakeTroopGameAction(int amount, eChimps unitType, int rawUnitType)
        {
            try
            {
                return DecideLocalUnitRecruitmentRequest(amount, unitType, rawUnitType);
            }
            catch (Exception ex)
            {
                LogDebug("Unit limit game action event failed:", ex.Message);
                return MakeTroopGameActionDecision.AllowOriginal();
            }
        }

        private MakeTroopGameActionDecision DecideLocalUnitRecruitmentRequest(int amount, eChimps unitType, int rawUnitType)
        {
            if (amount <= 0)
                return MakeTroopGameActionDecision.AllowOriginal();

            if (!SoldierChimps.Contains(unitType))
                return MakeTroopGameActionDecision.AllowOriginal();

            if (!activeUnitLimits.TryGetValue(unitType, out int limit) || limit < 0)
                return MakeTroopGameActionDecision.AllowOriginal();

            int playerId = GetLocalHumanPlayerId();
            if (playerId <= 0)
                return MakeTroopGameActionDecision.AllowOriginal();

            RemoveExpiredPendingRecruitments();
            int liveCount = CountAliveUnits(playerId, unitType);
            int pendingCount = GetPendingRecruitmentCount(playerId, unitType);
            int effectiveCount = liveCount + pendingCount;
            int remaining = limit - effectiveCount;
            int readyPeasants = -1;
            int peasantLimitedRemaining = remaining;
            if (!IsEngineerSiegeUnit(unitType) && TryGetReadyPeasantCount(playerId, out readyPeasants))
                peasantLimitedRemaining = Math.Min(peasantLimitedRemaining, readyPeasants);

            int allowedAmount = peasantLimitedRemaining <= 0
                ? 0
                : amount == 1000
                    ? peasantLimitedRemaining
                    : Math.Min(amount, peasantLimitedRemaining);
            LogDebug(
                log,
                "MakeTroop decision:",
                "unit", unitType,
                "player", playerId,
                "live", liveCount,
                "pending", pendingCount,
                "effective", effectiveCount,
                "remaining", remaining,
                "readyPeasants", readyPeasants,
                "peasantLimitedRemaining", peasantLimitedRemaining,
                "requestedAmount", amount,
                "allowedAmount", allowedAmount,
                "limit", limit,
                "rawUnitType", rawUnitType);

            if (allowedAmount > 0)
            {
                ReservePendingRecruitment(playerId, unitType, allowedAmount);
                RefreshCurrentUnitLimitTooltip();
                return MakeTroopGameActionDecision.ForwardAmount(allowedAmount);
            }

            LogDebug(
                log,
                "MakeTroop block: unit limit exceeded",
                unitType,
                "player", playerId,
                "live", liveCount,
                "pending", pendingCount,
                "effective", effectiveCount,
                "remaining", remaining,
                "readyPeasants", readyPeasants,
                "peasantLimitedRemaining", peasantLimitedRemaining,
                "requestedAmount", amount,
                "allowedAmount", allowedAmount,
                "limit", limit,
                "rawUnitType", rawUnitType);

            if (remaining <= 0)
                ShowUnitLimitReachedMessageForLocalPlayer(playerId, unitType, limit);
            else if (readyPeasants == 0 && peasantLimitedRemaining <= 0)
                PlayRecruitsNeededSpeech();

            RefreshCurrentUnitLimitTooltip();
            return MakeTroopGameActionDecision.BlockAction();
        }

        // private int CountAliveUnits(int playerId, eChimps unitType)
        // {
        //     matchingUnitIds.Clear();
        //     GameUnitManagerAPI.Instance.GetAllUnits(
        //         matchingUnitIds,
        //         AliveState.IsAlive,
        //         unitType,
        //         PlayerRelationship.Self,
        //         playerId);
        //     return matchingUnitIds.Count;
        // }

        private int CountAliveUnits(int playerId, eChimps unitType)
        {
            if (IsEngineerSiegeUnit(unitType))
            {
                return activeUnitCache.GetActiveUnitCount(playerId, unitType) +
                    CountActiveSiegeTentBuildings(playerId, unitType);
            }

            return activeUnitCache.GetActiveUnitCount(playerId, unitType);
        }

        private int CountActiveSiegeTentBuildings(int playerId, eChimps unitType)
        {
            return activeSiegeTentCache.GetActiveSiegeTentCount(playerId, unitType);
        }

        private static bool IsEngineerSiegeUnit(eChimps unitType)
        {
            switch (unitType)
            {
                case eChimps.CHIMP_TYPE_CATAPULT:
                case eChimps.CHIMP_TYPE_TREBUCHET:
                case eChimps.CHIMP_TYPE_BATTERING_RAM:
                case eChimps.CHIMP_TYPE_SIEGE_TOWER:
                case eChimps.CHIMP_TYPE_PORTABLE_SHIELD:
                case eChimps.CHIMP_TYPE_ARAB_BALLISTA:
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryGetReadyPeasantCount(int playerId, out int readyPeasants)
        {
            readyPeasants = 0;
            unsafe
            {
                if (!GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(playerId, out GamePlayerResources* resources) ||
                    resources == null)
                {
                    return false;
                }

                uint readyPeasantValue = resources->r_ReadyPeasants;
                readyPeasants = readyPeasantValue > (uint)int.MaxValue ? int.MaxValue : (int)readyPeasantValue;
                return true;
            }
        }

        private static bool TryGetSiegeUnitFromMapper(eMappers mapper, out eChimps unitType)
        {
            switch (mapper)
            {
                case eMappers.MAPPER_CATAPULT:
                    unitType = eChimps.CHIMP_TYPE_CATAPULT;
                    return true;
                case eMappers.MAPPER_TREBUCHET:
                    unitType = eChimps.CHIMP_TYPE_TREBUCHET;
                    return true;
                case eMappers.MAPPER_BATTERING_RAM:
                    unitType = eChimps.CHIMP_TYPE_BATTERING_RAM;
                    return true;
                case eMappers.MAPPER_SIEGE_TOWER:
                    unitType = eChimps.CHIMP_TYPE_SIEGE_TOWER;
                    return true;
                case eMappers.MAPPER_PORTABLE_SHIELD:
                    unitType = eChimps.CHIMP_TYPE_PORTABLE_SHIELD;
                    return true;
                case eMappers.MAPPER_PEOPLE_ARAB_BALLISTA:
                case eMappers.MAPPER_ARAB_BALLISTA:
                    unitType = eChimps.CHIMP_TYPE_ARAB_BALLISTA;
                    return true;
                default:
                    unitType = eChimps.CHIMP_TYPE_NULL;
                    return false;
            }
        }

        private void OnBuildingPlacementValidation(BuildingPlacementValidationEventArgs args)
        {
            try
            {
                ValidateSiegeTentPlacement(args);
            }
            catch (Exception ex)
            {
                LogDebug("Unit limit siege placement validation failed:", ex.Message);
            }
        }

        private void ValidateSiegeTentPlacement(BuildingPlacementValidationEventArgs args)
        {
            if (GamePlayerManagerAPI.Instance.IsAIPlayer(args.PlayerId))
                return;

            if (!TryGetSiegeUnitFromMapper(args.Mappers, out eChimps unitType))
                return;

            if (!activeUnitLimits.TryGetValue(unitType, out int limit) || limit < 0)
                return;

            int count = CountAliveUnits(args.PlayerId, unitType);
            if (count < limit)
                return;

            args.CustomValidationRules = true;
            args.ForceBlockPlacementState = true;
            LogDebug(
                "Siege tent placement blocked by unit limit:",
                "player", args.PlayerId,
                "mapper", args.Mappers,
                "unit", unitType,
                "count", count,
                "limit", limit);
            ShowUnitLimitReachedMessage(unitType, limit);
        }

        private int GetPendingRecruitmentCount(int playerId, eChimps unitType)
        {
            PendingRecruitmentKey key = new PendingRecruitmentKey(playerId, unitType);
            if (!pendingRecruitments.TryGetValue(key, out PendingRecruitmentQueue pending))
                return 0;

            return pending.Count;
        }

        private void ReservePendingRecruitment(int playerId, eChimps unitType, int amount)
        {
            if (amount <= 0)
                return;

            PendingRecruitmentKey key = new PendingRecruitmentKey(playerId, unitType);
            if (!pendingRecruitments.TryGetValue(key, out PendingRecruitmentQueue pending))
            {
                pending = new PendingRecruitmentQueue();
                pendingRecruitments[key] = pending;
            }

            DateTime expiration = DateTime.UtcNow + PendingRecruitmentLifetime;
            pending.Add(expiration, amount);
        }

        private bool ConsumePendingRecruitment(int playerId, eChimps unitType)
        {
            RemoveExpiredPendingRecruitments();
            PendingRecruitmentKey key = new PendingRecruitmentKey(playerId, unitType);
            if (!pendingRecruitments.TryGetValue(key, out PendingRecruitmentQueue pending) || !pending.ConsumeOne())
                return false;

            int remaining = pending.Count;
            if (remaining == 0)
                pendingRecruitments.Remove(key);

            return true;
        }

        private void RemoveExpiredPendingRecruitments()
        {
            if (pendingRecruitments.Count == 0)
                return;

            DateTime now = DateTime.UtcNow;
            List<PendingRecruitmentKey> emptyKeys = null;
            foreach (KeyValuePair<PendingRecruitmentKey, PendingRecruitmentQueue> entry in pendingRecruitments)
            {
                int expired = entry.Value.RemoveExpired(now);
                if (expired > 0)
                {
                    if (ShouldLogHumanPlayer(entry.Key.PlayerId))
                        LogDebug("Pending recruit expired:", entry.Key.UnitType, "player", entry.Key.PlayerId, "expired", expired, "remaining", entry.Value.Count);
                }

                if (entry.Value.Count == 0)
                {
                    if (emptyKeys == null)
                        emptyKeys = new List<PendingRecruitmentKey>();

                    emptyKeys.Add(entry.Key);
                }
            }

            if (emptyKeys == null)
                return;

            foreach (PendingRecruitmentKey key in emptyKeys)
                pendingRecruitments.Remove(key);
        }

        private void ClearPendingRecruitments(string reason)
        {
            if (pendingRecruitments.Count > 0)
                LogDebug("Clearing pending recruitments:", reason, "keys", pendingRecruitments.Count);

            pendingRecruitments.Clear();
        }

        private void OnActiveUnitChanged(ActiveUnitCache.ActiveUnitChangedEventArgs args)
        {
            bool oldSnapshotRelevant = IsLocalSoldierSnapshot(args.OldSnapshot);
            bool newSnapshotRelevant = IsLocalSoldierSnapshot(args.NewSnapshot);
            if (!oldSnapshotRelevant && !newSnapshotRelevant)
                return;

            if (newSnapshotRelevant &&
                (args.Reason == ActiveUnitCache.ActiveUnitChangeReason.Created ||
                    args.Reason == ActiveUnitCache.ActiveUnitChangeReason.TypeChanged))
            {
                eChimps pendingUnitType = GetPendingRecruitmentUnitType(args.NewSnapshot);
                bool consumed = ConsumePendingRecruitment(args.NewSnapshot.OwnerId, pendingUnitType);
                if (consumed && ShouldLogHumanPlayer(args.NewSnapshot.OwnerId))
                    LogDebug("Active unit change consumed pending recruitment:", "unitId", args.UnitId, "player", args.NewSnapshot.OwnerId, "reason", args.Reason, "unitType", args.NewSnapshot.UnitType, "pendingUnitType", pendingUnitType);
            }

            RefreshCurrentUnitLimitTooltip();
        }

        private void OnActiveSiegeTentChanged(ActiveSiegeTentCache.ActiveSiegeTentChangedEventArgs args)
        {
            if (!IsLocalPlayer(args.PlayerId))
                return;

            RefreshCurrentUnitLimitTooltip();
        }

        private bool IsLocalSoldierSnapshot(ActiveUnitCache.UnitSnapshot snapshot)
        {
            return IsActiveUnitState(snapshot.AliveState) &&
                (SoldierChimps.Contains(snapshot.UnitType) ||
                    (snapshot.UnitType == eChimps.CHIMP_SIEGE_TENT &&
                        IsEngineerSiegeUnit(snapshot.TransformIntoUnitOfType))) &&
                IsLocalPlayer(snapshot.OwnerId);
        }

        private static eChimps GetPendingRecruitmentUnitType(ActiveUnitCache.UnitSnapshot snapshot)
        {
            if (snapshot.UnitType == eChimps.CHIMP_SIEGE_TENT &&
                IsEngineerSiegeUnit(snapshot.TransformIntoUnitOfType))
            {
                return snapshot.TransformIntoUnitOfType;
            }

            return snapshot.UnitType;
        }

        private static bool IsActiveUnitState(AliveState aliveState)
        {
            return aliveState == AliveState.IsAlive ||
                aliveState == AliveState.NeedsInit;
        }

        private void ShowUnitLimitReachedMessageForLocalPlayer(int playerId, eChimps unitType, int limit)
        {
            if (!IsLocalPlayer(playerId))
                return;

            ShowUnitLimitReachedMessage(unitType, limit);
        }

        private void ShowUnitLimitReachedMessage(eChimps unitType, int limit)
        {
            string message = SerpLocalization.Get(SerpLocalization.Max) + " " + limit + " " + GetLocalizedUnitName(unitType);
            LogDebug("Unit limit notification shown:", unitType, message);
            if (IsEngineerSiegeUnit(unitType))
                DisplaySiegeLimitNotification(message);
            else
                DisplayLimitNotification(message);
        }

        private void ApplyUnitLimits()
        {
            activeUnitLimits.Clear();
            Dictionary<eChimps, int> parsedLimits = ParseEnumAmounts<eChimps>(settings.UnitLimits);
            foreach (KeyValuePair<eChimps, int> entry in parsedLimits)
            {
                if (!SoldierChimps.Contains(entry.Key))
                {
                    LogDebug("Unit limit type is not a supported recruitable soldier:", entry.Key);
                    continue;
                }

                activeUnitLimits[entry.Key] = entry.Value;
                if (entry.Value >= 0)
                    LogDebug("Active unit limit:", entry.Key, "=", entry.Value);
            }

            LogDebug("Applied active unit limit rules:", activeUnitLimits.Count);
            RefreshCurrentUnitLimitTooltip();
        }

        private void ResetUnitRecruitableTracking()
        {
            ClearPendingRecruitments("ResetUnitRecruitableTracking");
        }

        private int GetLocalHumanPlayerId()
        {
            int playerId = GamePlayerManagerAPI.Instance.GetLocalPlayerId();
            if (IsUsableHumanPlayerId(playerId, true))
                return playerId;

            if (IsUsableHumanPlayerId(playerId, false))
                return playerId;

            return -1;
        }

        private bool IsUsableHumanPlayerId(int playerId, bool requireKeep)
        {
            try
            {
                if (!GamePlayerManagerAPI.Instance.IsPlayerIdValid(playerId) ||
                    GamePlayerManagerAPI.Instance.IsAIPlayer(playerId))
                    return false;

                return !requireKeep || HasKeep(playerId);
            }
            catch
            {
                return false;
            }
        }

        private bool ShouldLogHumanPlayer(int playerId)
        {
            return IsUsableHumanPlayerId(playerId, false);
        }

    }
}

