using System;
using System.Collections.Generic;

namespace WaterboyTargetReservationTest
{
    internal enum ReservationClaimResult { Claimed, Suppressed, Conflict }
    internal enum ReservationReconcileResult { Missing, Kept, Released, Stalled }

    internal readonly struct NativeIdentity : IEquatable<NativeIdentity>
    {
        internal NativeIdentity(int slot, uint globalId) { Slot = slot; GlobalId = globalId; }
        internal int Slot { get; }
        internal uint GlobalId { get; }
        internal bool IsValid => Slot > 0 && GlobalId != 0;
        public bool Equals(NativeIdentity other) => Slot == other.Slot && GlobalId == other.GlobalId;
        public override bool Equals(object obj) => obj is NativeIdentity other && Equals(other);
        public override int GetHashCode() => (Slot * 397) ^ unchecked((int)GlobalId);
        public override string ToString() => $"{Slot}:{GlobalId}";
    }

    internal sealed class FireReservation
    {
        internal FireReservation(int playerId, NativeIdentity owner, NativeIdentity target,
            uint compoundKey, ushort x, ushort y, int progressTick)
        {
            PlayerId = playerId;
            Owner = owner;
            Target = target;
            CompoundKey = compoundKey;
            LastX = x;
            LastY = y;
            LastProgressTick = progressTick;
        }

        internal int PlayerId { get; }
        internal NativeIdentity Owner { get; }
        internal NativeIdentity Target { get; }
        internal uint CompoundKey { get; }
        internal ushort LastX { get; set; }
        internal ushort LastY { get; set; }
        internal int LastProgressTick { get; set; }
    }

    internal static class WaterboyTargetPolicy
    {
        internal static int ManhattanDistance(ushort fromX, ushort fromY, ushort toX, ushort toY) =>
            Math.Abs((int)fromX - toX) + Math.Abs((int)fromY - toY);

        internal static bool CanTakeOver(int requesterState, int ownerState,
            int requesterDistance, int ownerDistance) =>
            (requesterState == 1 || requesterState == 2) &&
            ownerState == ReservationLedger.WalkingState &&
            requesterDistance < ownerDistance;
    }

    internal static class WaterboyModeOperationPolicy
    {
        internal static bool TryAccept(int lastOperationId, int incomingOperationId,
            out int acceptedOperationId)
        {
            acceptedOperationId = lastOperationId;
            if (incomingOperationId <= 0 || incomingOperationId <= lastOperationId)
                return false;
            acceptedOperationId = incomingOperationId;
            return true;
        }
    }

    internal sealed class PerPlayerModeState
    {
        private readonly bool[] data =
            { true, true, true, true, true, true, true, true, true };
        private bool localValue = true;
        private int localPlayerId;

        internal bool LocalValue => localValue;
        internal bool[] Data => data;

        internal bool SetLocalValue(bool value)
        {
            if (localValue == value)
                return false;
            localValue = value;
            if (IsValidPlayerId(localPlayerId))
                data[localPlayerId] = value;
            return true;
        }

        internal void SetPlayerValue(int playerId, bool value, bool isLocalPlayer)
        {
            if (!IsValidPlayerId(playerId))
                return;
            data[playerId] = value;
            if (isLocalPlayer)
                localValue = value;
        }

        internal void ResolveLocalPlayer(int playerId)
        {
            if (!IsValidPlayerId(playerId))
                return;
            localPlayerId = playerId;
            data[playerId] = localValue;
        }

        private static bool IsValidPlayerId(int playerId) => playerId >= 1 && playerId <= 8;
    }

    internal sealed class ReservationLedger
    {
        internal const int WalkingState = 3;
        internal const int ExtinguishingState = 4;
        internal const int StationaryTimeoutTicks = 600;

        private readonly Dictionary<uint, FireReservation> reservationsByOwner =
            new Dictionary<uint, FireReservation>();
        private readonly Dictionary<uint, SuppressedAssignment> suppressedByOwner =
            new Dictionary<uint, SuppressedAssignment>();

        internal int Count => reservationsByOwner.Count;
        internal int SuppressionCount => suppressedByOwner.Count;

        internal void CopyReservationsTo(List<FireReservation> destination)
        {
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));
            destination.Clear();
            foreach (FireReservation reservation in reservationsByOwner.Values)
                destination.Add(reservation);
        }

        internal ReservationClaimResult Claim(int playerId, NativeIdentity owner,
            NativeIdentity target, uint compoundKey, ushort x, ushort y, int currentTick)
        {
            if (!IsValidPlayerId(playerId) || !owner.IsValid || !target.IsValid)
                throw new ArgumentException("Reservation player and identities must be valid.");

            if (suppressedByOwner.TryGetValue(owner.GlobalId, out SuppressedAssignment suppressed))
            {
                if (suppressed.Owner.Equals(owner) && suppressed.Target.Equals(target) &&
                    suppressed.CompoundKey == compoundKey && suppressed.X == x && suppressed.Y == y)
                {
                    reservationsByOwner.Remove(owner.GlobalId);
                    return ReservationClaimResult.Suppressed;
                }
                suppressedByOwner.Remove(owner.GlobalId);
            }

            if (TryGetCoveringReservation(playerId, target, compoundKey, owner.GlobalId, out _))
                return ReservationClaimResult.Conflict;

            reservationsByOwner[owner.GlobalId] = new FireReservation(
                playerId, owner, target, compoundKey, x, y, currentTick);
            return ReservationClaimResult.Claimed;
        }

        internal bool TryGetCoveringReservation(int playerId, NativeIdentity target,
            uint compoundKey, uint requesterGlobalId, out FireReservation covering)
        {
            foreach (FireReservation reservation in reservationsByOwner.Values)
            {
                if (reservation.PlayerId == playerId &&
                    reservation.Owner.GlobalId != requesterGlobalId &&
                    Covers(reservation, target, compoundKey))
                {
                    covering = reservation;
                    return true;
                }
            }
            covering = null;
            return false;
        }

        internal bool TryTransfer(FireReservation previous, int playerId,
            NativeIdentity newOwner, NativeIdentity target, uint compoundKey,
            ushort x, ushort y, int currentTick)
        {
            if (previous == null || previous.PlayerId != playerId ||
                !reservationsByOwner.TryGetValue(previous.Owner.GlobalId, out FireReservation current) ||
                !ReferenceEquals(previous, current) || !Covers(previous, target, compoundKey))
                return false;

            reservationsByOwner.Remove(previous.Owner.GlobalId);
            suppressedByOwner.Remove(newOwner.GlobalId);
            reservationsByOwner[newOwner.GlobalId] = new FireReservation(
                playerId, newOwner, target, compoundKey, x, y, currentTick);
            return true;
        }

        internal bool TryGetSuppressedCoverage(NativeIdentity owner, ushort x, ushort y,
            out NativeIdentity target, out uint compoundKey)
        {
            target = default;
            compoundKey = 0;
            if (!suppressedByOwner.TryGetValue(owner.GlobalId, out SuppressedAssignment suppressed))
                return false;
            if (!suppressed.Owner.Equals(owner) || suppressed.X != x || suppressed.Y != y)
            {
                suppressedByOwner.Remove(owner.GlobalId);
                return false;
            }
            target = suppressed.Target;
            compoundKey = suppressed.CompoundKey;
            return true;
        }

        internal void ClearSuppression(uint ownerGlobalId) => suppressedByOwner.Remove(ownerGlobalId);

        internal static bool CoversSuppression(NativeIdentity candidate, uint candidateCompoundKey,
            NativeIdentity suppressedTarget, uint suppressedCompoundKey) =>
            candidate.Equals(suppressedTarget) ||
            (suppressedCompoundKey != 0 && candidateCompoundKey == suppressedCompoundKey);

        internal ReservationReconcileResult Reconcile(FireReservation reservation,
            bool ownerIdentityValid, int ownerPlayerId, int ownerState,
            NativeIdentity currentTarget, bool targetBurning, uint currentCompoundKey,
            ushort x, ushort y, int currentTick)
        {
            if (reservation == null ||
                !reservationsByOwner.TryGetValue(reservation.Owner.GlobalId, out FireReservation current) ||
                !ReferenceEquals(reservation, current))
                return ReservationReconcileResult.Missing;

            if (!ownerIdentityValid || ownerPlayerId != reservation.PlayerId ||
                (ownerState != WalkingState && ownerState != ExtinguishingState) ||
                !currentTarget.Equals(reservation.Target) || !targetBurning ||
                currentCompoundKey != reservation.CompoundKey)
            {
                Release(reservation.Owner.GlobalId, clearSuppression: true);
                return ReservationReconcileResult.Released;
            }
            if (ownerState != WalkingState)
                return ReservationReconcileResult.Kept;
            if (x != reservation.LastX || y != reservation.LastY)
            {
                reservation.LastX = x;
                reservation.LastY = y;
                reservation.LastProgressTick = currentTick;
                suppressedByOwner.Remove(reservation.Owner.GlobalId);
                return ReservationReconcileResult.Kept;
            }
            if (currentTick < reservation.LastProgressTick)
            {
                reservation.LastProgressTick = currentTick;
                return ReservationReconcileResult.Kept;
            }
            if (currentTick - reservation.LastProgressTick < StationaryTimeoutTicks)
                return ReservationReconcileResult.Kept;

            reservationsByOwner.Remove(reservation.Owner.GlobalId);
            suppressedByOwner[reservation.Owner.GlobalId] = new SuppressedAssignment(
                reservation.PlayerId, reservation.Owner, reservation.Target,
                reservation.CompoundKey, x, y);
            return ReservationReconcileResult.Stalled;
        }

        internal bool Release(uint ownerGlobalId, bool clearSuppression)
        {
            if (clearSuppression)
                suppressedByOwner.Remove(ownerGlobalId);
            return reservationsByOwner.Remove(ownerGlobalId);
        }

        internal void ClearPlayer(int playerId)
        {
            var owners = new List<uint>();
            foreach (FireReservation reservation in reservationsByOwner.Values)
                if (reservation.PlayerId == playerId)
                    owners.Add(reservation.Owner.GlobalId);
            foreach (uint owner in owners)
            {
                reservationsByOwner.Remove(owner);
                suppressedByOwner.Remove(owner);
            }
            owners.Clear();
            foreach (KeyValuePair<uint, SuppressedAssignment> suppressed in suppressedByOwner)
                if (suppressed.Value.PlayerId == playerId)
                    owners.Add(suppressed.Key);
            foreach (uint owner in owners)
                suppressedByOwner.Remove(owner);
        }

        internal void Clear()
        {
            reservationsByOwner.Clear();
            suppressedByOwner.Clear();
        }

        private static bool Covers(FireReservation reservation, NativeIdentity target, uint compoundKey) =>
            reservation.Target.Equals(target) ||
            (reservation.CompoundKey != 0 && compoundKey == reservation.CompoundKey);

        private static bool IsValidPlayerId(int playerId) => playerId >= 1 && playerId <= 8;

        private readonly struct SuppressedAssignment
        {
            internal SuppressedAssignment(int playerId, NativeIdentity owner, NativeIdentity target,
                uint compoundKey, ushort x, ushort y)
            {
                PlayerId = playerId;
                Owner = owner;
                Target = target;
                CompoundKey = compoundKey;
                X = x;
                Y = y;
            }
            internal int PlayerId { get; }
            internal NativeIdentity Owner { get; }
            internal NativeIdentity Target { get; }
            internal uint CompoundKey { get; }
            internal ushort X { get; }
            internal ushort Y { get; }
        }
    }
}
