using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System.Collections.Generic;

namespace UnitLimit
{
    public sealed partial class UnitLimitRuntime
    {
        private readonly object externalReservationSyncRoot = new object();
        private readonly Dictionary<long, ExternalUnitReservation> externalReservations =
            new Dictionary<long, ExternalUnitReservation>();
        private long nextExternalReservationId;
        private long armedExternalReservationId;

        internal bool TryGetExternalCapacity(int playerId, int rawUnitType, out int count, out int limit)
        {
            count = 0;
            limit = -1;
            if (!TryGetFiniteExternalLimit(playerId, rawUnitType, out eChimps unitType, out limit))
                return false;

            RemoveExpiredPendingRecruitments();
            count = GetEffectiveUnitCount(playerId, unitType);
            return true;
        }

        internal bool TryReserveExternalUnit(
            int playerId,
            int rawUnitType,
            out long reservationId,
            out int count,
            out int limit)
        {
            reservationId = 0;
            count = 0;
            limit = -1;
            if (!TryGetFiniteExternalLimit(playerId, rawUnitType, out eChimps unitType, out limit))
                return true;

            RemoveExpiredPendingRecruitments();
            bool limitReached;
            lock (externalReservationSyncRoot)
            {
                count = GetEffectiveUnitCountLocked(playerId, unitType);
                limitReached = count >= limit;
                if (limitReached)
                    reservationId = 0;
                else
                    reservationId = CreateExternalReservationLocked(playerId, unitType);

                if (!limitReached)
                    count++;
            }

            if (limitReached)
            {
                ShowUnitLimitReachedMessageForLocalPlayer(playerId, unitType, limit);
                return false;
            }

            UnitLimitIntegration.NotifyStateChanged();
            RefreshCurrentUnitLimitTooltip();
            return true;
        }

        private long CreateExternalReservationLocked(int playerId, eChimps unitType)
        {
            do
            {
                nextExternalReservationId++;
                if (nextExternalReservationId <= 0)
                    nextExternalReservationId = 1;
            }
            while (externalReservations.ContainsKey(nextExternalReservationId));

            long reservationId = nextExternalReservationId;
            externalReservations.Add(
                reservationId,
                new ExternalUnitReservation(playerId, unitType));
            return reservationId;
        }

        internal bool TryArmExternalReservation(long reservationId)
        {
            lock (externalReservationSyncRoot)
            {
                if (!externalReservations.ContainsKey(reservationId))
                    return false;

                if (armedExternalReservationId == reservationId)
                    return true;
                if (armedExternalReservationId != 0)
                    return false;

                armedExternalReservationId = reservationId;
                return true;
            }
        }

        internal void ReleaseExternalReservation(long reservationId)
        {
            bool changed;
            lock (externalReservationSyncRoot)
            {
                if (armedExternalReservationId == reservationId)
                    armedExternalReservationId = 0;
                changed = externalReservations.Remove(reservationId);
            }

            if (!changed)
                return;

            UnitLimitIntegration.NotifyStateChanged();
            RefreshCurrentUnitLimitTooltip();
        }

        private bool TryConsumeArmedExternalReservation(int playerId, eChimps unitType)
        {
            bool consumed = false;
            lock (externalReservationSyncRoot)
            {
                long reservationId = armedExternalReservationId;
                if (reservationId != 0 &&
                    externalReservations.TryGetValue(reservationId, out ExternalUnitReservation reservation) &&
                    reservation.PlayerId == playerId &&
                    reservation.UnitType == unitType)
                {
                    externalReservations.Remove(reservationId);
                    armedExternalReservationId = 0;
                    consumed = true;
                }
            }

            return consumed;
        }

        private int GetEffectiveUnitCount(int playerId, eChimps unitType)
        {
            lock (externalReservationSyncRoot)
                return GetEffectiveUnitCountLocked(playerId, unitType);
        }

        private int GetEffectiveUnitCountLocked(int playerId, eChimps unitType)
        {
            return CountAliveUnits(playerId, unitType) +
                GetPendingRecruitmentCount(playerId, unitType) +
                GetExternalReservationCountLocked(playerId, unitType);
        }

        private int GetExternalReservationCountLocked(int playerId, eChimps unitType)
        {
            int count = 0;
            foreach (ExternalUnitReservation reservation in externalReservations.Values)
            {
                if (reservation.PlayerId == playerId && reservation.UnitType == unitType)
                    count++;
            }

            return count;
        }

        private bool TryGetFiniteExternalLimit(
            int playerId,
            int rawUnitType,
            out eChimps unitType,
            out int limit)
        {
            unitType = (eChimps)rawUnitType;
            limit = -1;
            return EffectsEnabled &&
                activeUnitCacheAvailable &&
                IsUnitLimitModeAllowed() &&
                IsUsableHumanPlayerId(playerId) &&
                SoldierChimps.Contains(unitType) &&
                activeUnitLimits.TryGetValue(unitType, out limit) &&
                limit >= 0;
        }

        private void ClearExternalReservations(string reason)
        {
            int count;
            lock (externalReservationSyncRoot)
            {
                count = externalReservations.Count;
                externalReservations.Clear();
                armedExternalReservationId = 0;
            }

            if (count == 0)
                return;

            LogDebug("Clearing external unit reservations:", reason, "count", count);
            UnitLimitIntegration.NotifyStateChanged();
        }

        private readonly struct ExternalUnitReservation
        {
            internal ExternalUnitReservation(int playerId, eChimps unitType)
            {
                PlayerId = playerId;
                UnitType = unitType;
            }

            internal int PlayerId { get; }
            internal eChimps UnitType { get; }
        }
    }
}
