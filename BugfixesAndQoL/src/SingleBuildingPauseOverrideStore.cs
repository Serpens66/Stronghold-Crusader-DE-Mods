using SHCDESE.Interop;
using System;
using System.Collections.Generic;

namespace BugfixesAndQoL
{
    internal sealed class SingleBuildingPauseOverrideStore
    {
        private readonly object sync = new object();
        private readonly Dictionary<int, SingleBuildingPauseOverride> overridesByBuildingId =
            new Dictionary<int, SingleBuildingPauseOverride>();
        private readonly Dictionary<IntPtr, int> buildingIdsBySleepingAddress =
            new Dictionary<IntPtr, int>();

        internal int Count
        {
            get
            {
                lock (sync)
                    return overridesByBuildingId.Count;
            }
        }

        internal bool Set(SingleBuildingPauseOverride entry)
        {
            lock (sync)
            {
                bool becameNonEmpty = overridesByBuildingId.Count == 0;
                if (overridesByBuildingId.TryGetValue(entry.BuildingId, out SingleBuildingPauseOverride oldEntry) &&
                    buildingIdsBySleepingAddress.TryGetValue(oldEntry.SleepingAddress, out int oldBuildingId) &&
                    oldBuildingId == entry.BuildingId)
                {
                    buildingIdsBySleepingAddress.Remove(oldEntry.SleepingAddress);
                }

                overridesByBuildingId[entry.BuildingId] = entry;
                buildingIdsBySleepingAddress[entry.SleepingAddress] = entry.BuildingId;
                return becameNonEmpty;
            }
        }

        internal bool TryGet(int buildingId, out SingleBuildingPauseOverride entry)
        {
            lock (sync)
                return overridesByBuildingId.TryGetValue(buildingId, out entry);
        }

        internal bool TryGetBySleepingAddress(IntPtr sleepingAddress, out SingleBuildingPauseOverride entry)
        {
            entry = default;
            if (sleepingAddress == IntPtr.Zero)
                return false;

            lock (sync)
            {
                if (!buildingIdsBySleepingAddress.TryGetValue(sleepingAddress, out int buildingId) ||
                    !overridesByBuildingId.TryGetValue(buildingId, out entry) ||
                    entry.SleepingAddress != sleepingAddress)
                {
                    buildingIdsBySleepingAddress.Remove(sleepingAddress);
                    entry = default;
                    return false;
                }

                return true;
            }
        }

        internal bool Remove(int buildingId)
        {
            lock (sync)
            {
                if (!RemoveUnsafe(buildingId))
                    return false;

                return overridesByBuildingId.Count == 0;
            }
        }

        internal OverrideRemovalResult RemoveForBuildingType(int owner, eStructs buildingType)
        {
            lock (sync)
            {
                List<int> idsToRemove = null;
                foreach (SingleBuildingPauseOverride entry in overridesByBuildingId.Values)
                {
                    if (entry.Owner != owner || entry.BuildingType != buildingType)
                        continue;

                    if (idsToRemove == null)
                        idsToRemove = new List<int>();
                    idsToRemove.Add(entry.BuildingId);
                }

                if (idsToRemove == null)
                    return default;

                foreach (int buildingId in idsToRemove)
                    RemoveUnsafe(buildingId);

                return new OverrideRemovalResult(idsToRemove.Count, overridesByBuildingId.Count == 0);
            }
        }

        internal int Clear()
        {
            lock (sync)
            {
                int count = overridesByBuildingId.Count;
                overridesByBuildingId.Clear();
                buildingIdsBySleepingAddress.Clear();
                return count;
            }
        }

        private bool RemoveUnsafe(int buildingId)
        {
            if (!overridesByBuildingId.TryGetValue(buildingId, out SingleBuildingPauseOverride entry))
                return false;

            overridesByBuildingId.Remove(buildingId);
            if (buildingIdsBySleepingAddress.TryGetValue(entry.SleepingAddress, out int indexedBuildingId) &&
                indexedBuildingId == buildingId)
            {
                buildingIdsBySleepingAddress.Remove(entry.SleepingAddress);
            }

            return true;
        }
    }

    internal readonly struct OverrideRemovalResult
    {
        internal OverrideRemovalResult(int count, bool becameEmpty)
        {
            Count = count;
            BecameEmpty = becameEmpty;
        }

        internal int Count { get; }
        internal bool BecameEmpty { get; }
    }

    internal readonly struct SingleBuildingPauseOverride
    {
        internal SingleBuildingPauseOverride(
            int buildingId,
            bool isSleeping,
            IntPtr sleepingAddress,
            eStructs buildingType,
            int owner,
            int globalId)
        {
            BuildingId = buildingId;
            IsSleeping = isSleeping;
            SleepingAddress = sleepingAddress;
            BuildingType = buildingType;
            Owner = owner;
            GlobalId = globalId;
        }

        internal int BuildingId { get; }
        internal bool IsSleeping { get; }
        internal IntPtr SleepingAddress { get; }
        internal eStructs BuildingType { get; }
        internal int Owner { get; }
        internal int GlobalId { get; }
    }
}
