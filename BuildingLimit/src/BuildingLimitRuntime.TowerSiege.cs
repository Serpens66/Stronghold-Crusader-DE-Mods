using CrusaderDE;
using MonoMod.RuntimeDetour;
using SHCDESE.API;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace BuildingLimit
{
    public sealed partial class BuildingLimitRuntime
    {
        private static readonly TimeSpan TowerSiegeReservationLifetime = TimeSpan.FromSeconds(3);
        private readonly object towerSiegeReservationSync = new object();
        private readonly Dictionary<TowerSiegeReservationKey, List<TowerSiegeReservation>> towerSiegeReservations =
            new Dictionary<TowerSiegeReservationKey, List<TowerSiegeReservation>>();

        private delegate void PlaceMapperItemDelegate(
            int item, int x, int y, int size, int player,
            bool inGameNotEditor, bool constructingOnly, int mouseState);

        private void InstallPlaceMapperItemHook()
        {
            MethodInfo target = typeof(EngineInterface).GetMethod(
                "PlaceMapperItem",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(int), typeof(int), typeof(int), typeof(int), typeof(int),
                    typeof(bool), typeof(bool), typeof(int) },
                null);
            if (target == null || target.ReturnType != typeof(void))
                throw new MissingMethodException(typeof(EngineInterface).FullName, "PlaceMapperItem");

            Hook candidate = new Hook(target, new PlaceMapperItemDelegate(PlaceMapperItemHookImpl));
            try
            {
                PlaceMapperItemDelegate trampoline = candidate.GenerateTrampoline<PlaceMapperItemDelegate>();
                placeMapperItemTrampoline = trampoline;
                placeMapperItemHook = candidate;
            }
            catch
            {
                candidate.Dispose(); // Roll back only the unpublished initialization candidate.
                throw;
            }

            LogDebug("EngineInterface.PlaceMapperItem tower-siege hook installed");
        }

        private void PlaceMapperItemHookImpl(
            int item, int x, int y, int size, int player,
            bool inGameNotEditor, bool constructingOnly, int mouseState)
        {
            TowerSiegeReservation reservation = null;
            TowerSiegeReservationKey reservationKey = default(TowerSiegeReservationKey);
            BuildingLimitRule blockedRule = null;

            try
            {
                if (EffectsEnabled && IsBuildingLimitModeAllowed() && towerSiegeCacheAvailable &&
                    inGameNotEditor && !constructingOnly && mouseState == 1 &&
                    activeBuildingLimitRules.TryGetValue((eMappers)item, out BuildingLimitRule rule) &&
                    rule.Definition.CountedUnitType.HasValue && rule.Limit >= 0 &&
                    GamePlayerManagerAPI.Instance.IsPlayerIdValid(player) &&
                    !GamePlayerManagerAPI.Instance.IsAIPlayer(player))
                {
                    eChimps unitType = rule.Definition.CountedUnitType.Value;
                    int liveCount = towerSiegeUnitCache.GetAliveCount(player, unitType);
                    reservationKey = new TowerSiegeReservationKey(player, unitType);
                    lock (towerSiegeReservationSync)
                    {
                        List<TowerSiegeReservation> pending = GetPendingReservations(reservationKey, DateTime.UtcNow);
                        if (liveCount + pending.Count >= rule.Limit)
                        {
                            blockedRule = rule;
                        }
                        else
                        {
                            reservation = new TowerSiegeReservation(DateTime.UtcNow + TowerSiegeReservationLifetime);
                            pending.Add(reservation);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogDebug("Tower-siege placement check failed; forwarding Vanilla:", ex);
            }

            if (blockedRule != null)
            {
                try { ShowBuildingLimitMessageForLocalPlayer(player, blockedRule.Definition, blockedRule.DisplayLimit); }
                catch (Exception ex) { LogDebug("Tower-siege limit message failed:", ex); }
                return;
            }

            try
            {
                placeMapperItemTrampoline(item, x, y, size, player, inGameNotEditor, constructingOnly, mouseState);
            }
            catch
            {
                if (reservation != null)
                    RemoveTowerSiegeReservation(reservationKey, reservation);
                throw;
            }
        }

        private void OnTowerSiegeUnitCreated(UnitCreateEventArgs args)
        {
            if (args.ReturnValue <= 0 || args.ReturnValue > int.MaxValue ||
                !towerSiegeUnitCache.TryReadActiveTower((int)args.ReturnValue, out int ownerId, out eChimps unitType))
                return;

            TowerSiegeReservationKey key = new TowerSiegeReservationKey(ownerId, unitType);
            lock (towerSiegeReservationSync)
            {
                List<TowerSiegeReservation> pending = GetPendingReservations(key, DateTime.UtcNow);
                if (pending.Count > 0)
                    pending.RemoveAt(0);
            }
        }

        private List<TowerSiegeReservation> GetPendingReservations(TowerSiegeReservationKey key, DateTime now)
        {
            if (!towerSiegeReservations.TryGetValue(key, out List<TowerSiegeReservation> pending))
            {
                pending = new List<TowerSiegeReservation>();
                towerSiegeReservations.Add(key, pending);
            }

            pending.RemoveAll(entry => entry.ExpiresUtc <= now);
            return pending;
        }

        private void RemoveTowerSiegeReservation(TowerSiegeReservationKey key, TowerSiegeReservation reservation)
        {
            lock (towerSiegeReservationSync)
                if (towerSiegeReservations.TryGetValue(key, out List<TowerSiegeReservation> pending))
                    pending.Remove(reservation);
        }

        private void ClearTowerSiegeReservations()
        {
            lock (towerSiegeReservationSync)
                towerSiegeReservations.Clear();
        }

        private sealed class TowerSiegeReservation
        {
            public readonly DateTime ExpiresUtc;

            public TowerSiegeReservation(DateTime expiresUtc)
            {
                ExpiresUtc = expiresUtc;
            }
        }

        private struct TowerSiegeReservationKey
        {
            public readonly int PlayerId;
            public readonly eChimps UnitType;

            public TowerSiegeReservationKey(int playerId, eChimps unitType)
            {
                PlayerId = playerId;
                UnitType = unitType;
            }

            public override bool Equals(object obj) =>
                obj is TowerSiegeReservationKey other &&
                PlayerId == other.PlayerId && UnitType == other.UnitType;

            public override int GetHashCode()
            {
                unchecked { return (PlayerId * 397) ^ (int)UnitType; }
            }
        }
    }
}
