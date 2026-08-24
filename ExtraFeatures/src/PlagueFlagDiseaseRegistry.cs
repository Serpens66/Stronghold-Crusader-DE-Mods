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

        private readonly Dictionary<int, uint> globalIdsBySlot = new Dictionary<int, uint>();
        private readonly HashSet<uint> globalIds = new HashSet<uint>();

        public int Count => globalIdsBySlot.Count;

        public void Track(int slotId, uint globalId)
        {
            if (!IsValid(slotId, globalId))
                throw new ArgumentOutOfRangeException(nameof(slotId), "The projectile identity is invalid.");

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

        public void RemoveSlot(int slotId)
        {
            if (!globalIdsBySlot.TryGetValue(slotId, out uint globalId))
                return;

            globalIdsBySlot.Remove(slotId);
            globalIds.Remove(globalId);
        }

        public bool ContainsGlobalId(uint globalId) => globalId != 0 && globalIds.Contains(globalId);

        public PlagueFlagDiseaseIdentity[] Snapshot()
        {
            var result = new PlagueFlagDiseaseIdentity[globalIdsBySlot.Count];
            int index = 0;
            foreach (KeyValuePair<int, uint> entry in globalIdsBySlot)
                result[index++] = new PlagueFlagDiseaseIdentity(entry.Key, entry.Value);
            Array.Sort(result, (left, right) => left.SlotId.CompareTo(right.SlotId));
            return result;
        }

        public void Restore(PlagueFlagDiseaseIdentity[] identities)
        {
            identities = identities ?? Array.Empty<PlagueFlagDiseaseIdentity>();
            if (identities.Length > NativeProjectileSlotCount)
                throw new InvalidOperationException("The AI flag disease state contains too many projectiles.");

            Clear();
            for (int index = 0; index < identities.Length; index++)
            {
                PlagueFlagDiseaseIdentity identity = identities[index];
                if (!IsValid(identity.SlotId, identity.GlobalId) ||
                    globalIdsBySlot.ContainsKey(identity.SlotId) ||
                    globalIds.Contains(identity.GlobalId))
                {
                    Clear();
                    throw new InvalidOperationException("The AI flag disease state contains an invalid or duplicate identity.");
                }
                Track(identity.SlotId, identity.GlobalId);
            }
        }

        public void Clear()
        {
            globalIdsBySlot.Clear();
            globalIds.Clear();
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
