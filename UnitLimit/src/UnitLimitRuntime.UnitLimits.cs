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

        private void OnGameActionMakeTroop(UnitGameActionMakeTroopEventArgs args)
        {
            try
            {
                bool block = ShouldBlockLocalUnitRecruitmentRequest(args.Amount, args.UnitType, args.RawState);
                if (block)
                {
                    args.SkipOriginalFunction = true;
                    args.ReturnValue = 0;
                }
            }
            catch (Exception ex)
            {
                LogInfo("Unit limit game action event failed:", ex.Message);
            }
        }

        private bool ShouldBlockLocalUnitRecruitmentRequest(int amount, eChimps unitType, int rawUnitType)
        {
            if (amount <= 0)
                return false;

            if (!SoldierChimps.Contains(unitType))
                return false;

            if (!activeUnitLimits.TryGetValue(unitType, out int limit) || limit < 0)
                return false;

            int playerId = GetLocalHumanPlayerId();
            if (playerId <= 0)
                return false;

            RemoveExpiredPendingRecruitments();
            int liveCount = CountAliveUnits(playerId, unitType);
            int pendingCount = GetPendingRecruitmentCount(playerId, unitType);
            int effectiveCount = liveCount + pendingCount;
            if (effectiveCount + amount <= limit)
            {
                ReservePendingRecruitment(playerId, unitType, amount);
                return false;
            }

            LogInfo(
                "MakeTroop block: unit limit exceeded",
                unitType,
                "player", playerId,
                "live", liveCount,
                "pending", pendingCount,
                "effective", effectiveCount,
                "amount", amount,
                "limit", limit,
                "rawUnitType", rawUnitType);
            ShowUnitLimitMessageForLocalPlayer(playerId, unitType, limit);
            RefreshLocalUnitRecruitableStates("MakeTroopBlock", false);
            return true;
        }

        private int CountAliveUnits(int playerId, eChimps unitType)
        {
            matchingUnitIds.Clear();
            GameUnitManagerAPI.Instance.GetAllUnits(
                matchingUnitIds,
                AliveState.IsAlive,
                unitType,
                PlayerRelationship.Self,
                playerId);
            return matchingUnitIds.Count;
        }

        private int GetPendingRecruitmentCount(int playerId, eChimps unitType)
        {
            PendingRecruitmentKey key = new PendingRecruitmentKey(playerId, unitType);
            if (!pendingRecruitments.TryGetValue(key, out List<DateTime> expiresAt))
                return 0;

            return expiresAt.Count;
        }

        private void ReservePendingRecruitment(int playerId, eChimps unitType, int amount)
        {
            if (amount <= 0)
                return;

            PendingRecruitmentKey key = new PendingRecruitmentKey(playerId, unitType);
            if (!pendingRecruitments.TryGetValue(key, out List<DateTime> expiresAt))
            {
                expiresAt = new List<DateTime>();
                pendingRecruitments[key] = expiresAt;
            }

            DateTime expiration = DateTime.UtcNow + PendingRecruitmentLifetime;
            for (int i = 0; i < amount; i++)
                expiresAt.Add(expiration);
        }

        private bool ConsumePendingRecruitment(int playerId, eChimps unitType)
        {
            RemoveExpiredPendingRecruitments();
            PendingRecruitmentKey key = new PendingRecruitmentKey(playerId, unitType);
            if (!pendingRecruitments.TryGetValue(key, out List<DateTime> expiresAt) || expiresAt.Count == 0)
                return false;

            expiresAt.RemoveAt(0);
            int remaining = expiresAt.Count;
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
            foreach (KeyValuePair<PendingRecruitmentKey, List<DateTime>> entry in pendingRecruitments)
            {
                int before = entry.Value.Count;
                entry.Value.RemoveAll(expiration => expiration <= now);
                int expired = before - entry.Value.Count;
                if (expired > 0)
                    LogInfo("Pending recruit expired:", entry.Key.UnitType, "player", entry.Key.PlayerId, "expired", expired, "remaining", entry.Value.Count);

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
                LogInfo("Clearing pending recruitments:", reason, "keys", pendingRecruitments.Count);

            pendingRecruitments.Clear();
        }

        private void OnUnitCreate(UnitCreateEventArgs args)
        {
            if (args.Phase != EventHookPhase.Post)
                return;

            if (SoldierChimps.Contains(args.UnitType) &&
                IsLocalPlayer(args.PlayerOwnerId))
            {
                ConsumePendingRecruitment(args.PlayerOwnerId, args.UnitType);
            }

            RefreshLocalUnitRecruitableStates("OnUnitCreate");
        }

        private void OnUnitTransition(UnitTransitionEventArgs args)
        {
            if (args.Phase != EventHookPhase.Pre ||
                !SoldierChimps.Contains(args.NextUnitType) ||
                !IsLocalPlayer(args.PlayerOwnerId))
                return;

            bool consumed = ConsumePendingRecruitment(args.PlayerOwnerId, args.NextUnitType);
            if (consumed)
                LogInfo("Unit transition consumed pending recruitment:", "unitId", args.UnitId, "player", args.PlayerOwnerId, "source", args.Source, "nextUnitType", args.NextUnitType);

            RefreshLocalUnitRecruitableStates("OnUnitTransition");
        }

        private void OnUnitDelete(UnitDeleteEventArgs args)
        {
            if (args.UnitId <= int.MaxValue)
            {
                int unitId = (int)args.UnitId;
                if (unitId > 0 && args.Phase == EventHookPhase.Pre)
                {
                    try
                    {
                        int playerId = GameUnitManagerAPI.Instance.GetOwner(unitId);
                        if (IsLocalPlayer(playerId))
                            LogInfo("Unit delete event:", "phase", args.Phase, "unitId", unitId, "player", playerId);
                    }
                    catch (Exception ex)
                    {
                        LogInfo("OnUnitDelete owner lookup failed:", "unitId", unitId, ex.Message);
                    }
                }
            }

            RefreshLocalUnitRecruitableStates("OnUnitDelete");
        }

        private void ShowUnitLimitMessageForLocalPlayer(int playerId, eChimps unitType, int limit)
        {
            if (!IsLocalPlayer(playerId))
                return;

            ShowUnitLimitMessage(unitType, "Max " + limit + " " + GetLocalizedUnitName(unitType));
        }

        private void ShowUnitLimitMessage(eChimps unitType, string message)
        {
            DateTime now = DateTime.UtcNow;
            if (unitLimitMessageCooldowns.TryGetValue(unitType, out DateTime cooldownStart) &&
                now - cooldownStart < UnitLimitMessageCooldown)
            {
                if (loggedUnitLimitCooldownSuppressions.Add(unitType))
                    LogInfo("Unit limit notification suppressed by cooldown:", unitType);

                return;
            }

            unitLimitMessageCooldowns[unitType] = now;
            loggedUnitLimitCooldownSuppressions.Remove(unitType);
            LogInfo("Unit limit notification shown:", unitType, message);
            DisplayLimitNotification(message);
        }

        private void ApplyUnitLimits(bool showNotifications = true)
        {
            activeUnitLimits.Clear();
            Dictionary<eChimps, int> parsedLimits = ParseEnumAmounts<eChimps>(settings.UnitLimits);
            foreach (KeyValuePair<eChimps, int> entry in parsedLimits)
            {
                if (!SoldierChimps.Contains(entry.Key))
                {
                    LogInfo("Unit limit type is not a supported recruitable soldier:", entry.Key);
                    continue;
                }

                activeUnitLimits[entry.Key] = entry.Value;
                if (entry.Value >= 0)
                    LogInfo("Active unit limit:", entry.Key, "=", entry.Value);
            }

            LogInfo("Applied active unit limit rules:", activeUnitLimits.Count);
            RefreshLocalUnitRecruitableStates("ApplyUnitLimits", showNotifications);
        }

        private void RefreshLocalUnitRecruitableStates(string source)
        {
            RefreshLocalUnitRecruitableStates(source, true);
        }

        private void RefreshLocalUnitRecruitableStates(string source, bool showNotifications)
        {
            try
            {
                int playerId = GetLocalHumanPlayerId();
                if (playerId <= 0)
                    return;
                RemoveExpiredPendingRecruitments();
                CaptureOriginalUnitRecruitableStates();
                foreach (eChimps unitType in SoldierChimps)
                {
                    bool mapAllowsUnit = GetOriginalUnitRecruitableState(unitType);
                    int limit = -1;
                    int count = 0;
                    bool shouldAllowRecruitment = mapAllowsUnit && ShouldAllowLocalUnitRecruitment(playerId, unitType, out limit, out count);
                    bool currentlyAllowed = GamePlayerManagerAPI.Instance.IsUnitAllowed(unitType);

                    if (currentlyAllowed != shouldAllowRecruitment)
                        GamePlayerManagerAPI.Instance.SetIsUnitAllowed(unitType, shouldAllowRecruitment);

                    bool disabledByLimit = mapAllowsUnit && !shouldAllowRecruitment;
                    if (disabledByLimit)
                    {
                        if (locallyDisabledUnitRecruitment.Add(unitType))
                        {
                            LogInfo("Unit recruitment disabled by limit:", unitType, "player", playerId, "count", count, "limit", limit, "source", source);
                            if (showNotifications)
                                ShowUnitLimitMessageForLocalPlayer(playerId, unitType, limit);
                        }
                    }
                    else if (locallyDisabledUnitRecruitment.Remove(unitType))
                    {
                        LogInfo("Unit recruitment enabled again:", unitType, "player", playerId, "count", count, "limit", limit, "source", source);
                    }
                }
            }
            catch (Exception ex)
            {
                LogInfo("RefreshLocalUnitRecruitableStates failed:", "source", source, ex.Message);
            }
        }

        private bool ShouldAllowLocalUnitRecruitment(int playerId, eChimps unitType, out int limit, out int count)
        {
            limit = -1;
            count = 0;

            if (!activeUnitLimits.TryGetValue(unitType, out limit) || limit < 0)
                return true;

            count = CountAliveUnits(playerId, unitType) + GetPendingRecruitmentCount(playerId, unitType);
            return count < limit;
        }

        private void CaptureOriginalUnitRecruitableStates()
        {
            foreach (eChimps unitType in SoldierChimps)
            {
                if (originalUnitRecruitableStates.ContainsKey(unitType))
                    continue;

                originalUnitRecruitableStates[unitType] = GamePlayerManagerAPI.Instance.IsUnitAllowed(unitType);
            }
        }

        private bool GetOriginalUnitRecruitableState(eChimps unitType)
        {
            if (originalUnitRecruitableStates.TryGetValue(unitType, out bool allowed))
                return allowed;

            allowed = GamePlayerManagerAPI.Instance.IsUnitAllowed(unitType);
            originalUnitRecruitableStates[unitType] = allowed;
            return allowed;
        }

        private void RestoreOriginalUnitRecruitableStates()
        {
            if (originalUnitRecruitableStates.Count == 0)
                return;

            try
            {
                foreach (KeyValuePair<eChimps, bool> entry in originalUnitRecruitableStates)
                    GamePlayerManagerAPI.Instance.SetIsUnitAllowed(entry.Key, entry.Value);

                LogInfo("Restored original unit recruitable states:", originalUnitRecruitableStates.Count);
            }
            catch (Exception ex)
            {
                LogInfo("RestoreOriginalUnitRecruitableStates failed:", ex.Message);
            }
        }

        private void ResetUnitRecruitableTracking()
        {
            locallyDisabledUnitRecruitment.Clear();
            ClearPendingRecruitments("ResetUnitRecruitableTracking");
            originalUnitRecruitableStates.Clear();
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

        private void StartUnitLimitRecruitableRefresh()
        {
            CancelUnitLimitRecruitableRefresh();
            try
            {
                unitLimitRecruitableRefreshTimerHandle = GameTimeManagerAPI.Instance.GetTimerEngine().AddRepeatedAction(
                    UnitLimitRecruitableRefreshMilliseconds,
                    OnUnitLimitRecruitableRefreshTimerElapsed,
                    null);
            }
            catch (Exception ex)
            {
                LogInfo("Could not start unit limit recruitable refresh timer:", ex.Message);
            }
        }

        private void OnUnitLimitRecruitableRefreshTimerElapsed()
        {
            RefreshLocalUnitRecruitableStates("UnitLimitRecruitableRefreshTimer");
        }

        private void CancelUnitLimitRecruitableRefresh()
        {
            if (string.IsNullOrEmpty(unitLimitRecruitableRefreshTimerHandle))
                return;

            try
            {
                GameTimeManagerAPI.Instance.GetTimerEngine().RemoveAction(unitLimitRecruitableRefreshTimerHandle);
            }
            catch (Exception ex)
            {
                LogInfo("Could not cancel unit limit recruitable refresh timer:", ex.Message);
            }

            unitLimitRecruitableRefreshTimerHandle = null;
        }
    }
}

