using System;
using System.Collections.Generic;

namespace WaterboyTargetReservationTest
{
    internal enum ReservationClaimResult
    {
        Claimed,
        Suppressed,
        Conflict
    }

    internal enum ReservationReconcileResult
    {
        Missing,
        Kept,
        Released,
        Stalled
    }

    internal readonly struct NativeIdentity : IEquatable<NativeIdentity>
    {
        internal NativeIdentity(int slot, uint globalId)
        {
            Slot = slot;
            GlobalId = globalId;
        }

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
        internal FireReservation(
            NativeIdentity owner,
            NativeIdentity target,
            uint compoundKey,
            ushort x,
            ushort y,
            int progressTick)
        {
            Owner = owner;
            Target = target;
            CompoundKey = compoundKey;
            LastX = x;
            LastY = y;
            LastProgressTick = progressTick;
        }

        internal NativeIdentity Owner { get; }
        internal NativeIdentity Target { get; }
        internal uint CompoundKey { get; }
        internal ushort LastX { get; set; }
        internal ushort LastY { get; set; }
        internal int LastProgressTick { get; set; }
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

        internal ReservationClaimResult Claim(
            NativeIdentity owner,
            NativeIdentity target,
            uint compoundKey,
            ushort x,
            ushort y,
            int currentTick)
        {
            if (!owner.IsValid || !target.IsValid)
                throw new ArgumentException("Reservation identities must be valid.");

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

            foreach (FireReservation reservation in reservationsByOwner.Values)
            {
                if (reservation.Owner.GlobalId == owner.GlobalId)
                    continue;
                if (Covers(reservation, target, compoundKey))
                    return ReservationClaimResult.Conflict;
            }

            reservationsByOwner[owner.GlobalId] = new FireReservation(
                owner,
                target,
                compoundKey,
                x,
                y,
                currentTick);
            return ReservationClaimResult.Claimed;
        }

        internal bool TryGetSuppressedCoverage(
            NativeIdentity owner,
            ushort x,
            ushort y,
            out NativeIdentity target,
            out uint compoundKey)
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

        internal static bool CoversSuppression(
            NativeIdentity candidate,
            uint candidateCompoundKey,
            NativeIdentity suppressedTarget,
            uint suppressedCompoundKey) =>
            candidate.Equals(suppressedTarget) ||
            (suppressedCompoundKey != 0 && candidateCompoundKey == suppressedCompoundKey);

        internal bool IsCoveredByForeignOwner(
            NativeIdentity target,
            uint compoundKey,
            uint requesterGlobalId)
        {
            foreach (FireReservation reservation in reservationsByOwner.Values)
            {
                if (reservation.Owner.GlobalId != requesterGlobalId && Covers(reservation, target, compoundKey))
                    return true;
            }
            return false;
        }

        internal ReservationReconcileResult Reconcile(
            FireReservation reservation,
            bool ownerIdentityValid,
            int ownerState,
            NativeIdentity currentTarget,
            bool targetBurning,
            uint currentCompoundKey,
            ushort x,
            ushort y,
            int currentTick)
        {
            if (reservation == null ||
                !reservationsByOwner.TryGetValue(reservation.Owner.GlobalId, out FireReservation current) ||
                !ReferenceEquals(reservation, current))
            {
                return ReservationReconcileResult.Missing;
            }

            if (!ownerIdentityValid ||
                (ownerState != WalkingState && ownerState != ExtinguishingState) ||
                !currentTarget.Equals(reservation.Target) ||
                !targetBurning ||
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
                reservation.Owner,
                reservation.Target,
                reservation.CompoundKey,
                x,
                y);
            return ReservationReconcileResult.Stalled;
        }

        internal bool Release(uint ownerGlobalId, bool clearSuppression)
        {
            if (clearSuppression)
                suppressedByOwner.Remove(ownerGlobalId);
            return reservationsByOwner.Remove(ownerGlobalId);
        }

        internal void Clear()
        {
            reservationsByOwner.Clear();
            suppressedByOwner.Clear();
        }

        private static bool Covers(FireReservation reservation, NativeIdentity target, uint compoundKey)
        {
            if (reservation.Target.Equals(target))
                return true;
            return reservation.CompoundKey != 0 && compoundKey == reservation.CompoundKey;
        }

        private readonly struct SuppressedAssignment
        {
            internal SuppressedAssignment(
                NativeIdentity owner,
                NativeIdentity target,
                uint compoundKey,
                ushort x,
                ushort y)
            {
                Owner = owner;
                Target = target;
                CompoundKey = compoundKey;
                X = x;
                Y = y;
            }

            internal NativeIdentity Owner { get; }
            internal NativeIdentity Target { get; }
            internal uint CompoundKey { get; }
            internal ushort X { get; }
            internal ushort Y { get; }
        }
    }
}
