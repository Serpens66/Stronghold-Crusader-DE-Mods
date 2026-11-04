// Feature: Track exact projectile identities created by Vanilla's AI flag routine.
using System;
using System.Collections.Generic;

namespace ExtraFeatures
{
    internal sealed class PlagueFlagDiseaseRegistry
    {
        // The Script Extender exposes a fixed native array with 10,000 projectile slots.
        // This is input validation, not an artificial limit on simultaneously tracked clouds.
        internal const int NativeProjectileSlotCount = 10000;

        // Read on every cloud tick and changed by projectile events on the simulation thread, but
        // also cleared and restored from lifecycle and load callbacks on other threads.
        private readonly object sync = new object();
        private readonly Dictionary<int, uint> globalIdsBySlot = new Dictionary<int, uint>();
        private readonly HashSet<uint> globalIds = new HashSet<uint>();

        public int Count
        {
            get
            {
                lock (sync)
                    return globalIdsBySlot.Count;
            }
        }

        public void Track(int slotId, uint globalId)
        {
            if (!IsValid(slotId, globalId))
                throw new ArgumentOutOfRangeException(nameof(slotId), "The projectile identity is invalid.");

            lock (sync)
            {
                if (globalIdsBySlot.TryGetValue(slotId, out uint previousGlobalId))
                {
                    if (previousGlobalId == globalId)
                        return;
                    if (globalIds.Contains(globalId))
                        throw new InvalidOperationException("The projectile global ID is already tracked in another slot.");
                    globalIds.Remove(previousGlobalId);
                }
                else if (globalIds.Contains(globalId))
                    throw new InvalidOperationException("The projectile global ID is already tracked in another slot.");

                globalIdsBySlot[slotId] = globalId;
                globalIds.Add(globalId);
            }
        }

        public void RemoveSlot(int slotId)
        {
            lock (sync)
            {
                if (!globalIdsBySlot.TryGetValue(slotId, out uint globalId))
                    return;

                globalIdsBySlot.Remove(slotId);
                globalIds.Remove(globalId);
            }
        }

        public bool ContainsGlobalId(uint globalId)
        {
            if (globalId == 0)
                return false;
            lock (sync)
                return globalIds.Contains(globalId);
        }

        public PlagueFlagDiseaseIdentity[] Snapshot()
        {
            PlagueFlagDiseaseIdentity[] result;
            lock (sync)
            {
                result = new PlagueFlagDiseaseIdentity[globalIdsBySlot.Count];
                int index = 0;
                foreach (KeyValuePair<int, uint> entry in globalIdsBySlot)
                    result[index++] = new PlagueFlagDiseaseIdentity(entry.Key, entry.Value);
            }
            Array.Sort(result, (left, right) => left.SlotId.CompareTo(right.SlotId));
            return result;
        }

        // Validates the whole state first and publishes it in one step; invalid input leaves the
        // registry empty, as before.
        public void Restore(PlagueFlagDiseaseIdentity[] identities)
        {
            identities = identities ?? Array.Empty<PlagueFlagDiseaseIdentity>();
            if (identities.Length > NativeProjectileSlotCount)
                throw new InvalidOperationException("The AI flag disease state contains too many projectiles.");

            var restoredBySlot = new Dictionary<int, uint>(identities.Length);
            var restoredGlobalIds = new HashSet<uint>();
            for (int index = 0; index < identities.Length; index++)
            {
                PlagueFlagDiseaseIdentity identity = identities[index];
                if (!IsValid(identity.SlotId, identity.GlobalId) ||
                    restoredBySlot.ContainsKey(identity.SlotId) ||
                    !restoredGlobalIds.Add(identity.GlobalId))
                {
                    Clear();
                    throw new InvalidOperationException("The AI flag disease state contains an invalid or duplicate identity.");
                }
                restoredBySlot.Add(identity.SlotId, identity.GlobalId);
            }

            lock (sync)
            {
                globalIdsBySlot.Clear();
                globalIds.Clear();
                foreach (KeyValuePair<int, uint> entry in restoredBySlot)
                {
                    globalIdsBySlot.Add(entry.Key, entry.Value);
                    globalIds.Add(entry.Value);
                }
            }
        }

        public void Clear()
        {
            lock (sync)
            {
                globalIdsBySlot.Clear();
                globalIds.Clear();
            }
        }

        private static bool IsValid(int slotId, uint globalId) =>
            slotId >= 1 && slotId <= NativeProjectileSlotCount && globalId != 0;
    }

    internal readonly struct PlagueFlagDiseaseIdentity
    {
        public PlagueFlagDiseaseIdentity(int slotId, uint globalId)
        {
            SlotId = slotId;
            GlobalId = globalId;
        }

        public int SlotId { get; }
        public uint GlobalId { get; }
    }
}
