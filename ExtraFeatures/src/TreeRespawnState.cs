using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.IO;

namespace ExtraFeatures
{
    internal sealed class TreeRespawnState
    {
        internal const int Off = -1;
        internal const int MaximumMinutes = 120;
        internal const int TicksPerMinute = 60 * 40;
        internal const int VegetationSlots = 4000;
        private const int AttemptsPerTick = 16;
        private const int HeaderSize = 8;
        private const int RecordSize = 11;

        internal struct Site
        {
            internal readonly ushort X, Y;
            internal readonly VegetationType Species;
            internal int Key => KeyOf(X, Y);
            internal int MissingSince;
            internal bool Missing, Due, Queued;

            internal Site(ushort x, ushort y, VegetationType species)
            {
                X = x;
                Y = y;
                Species = species;
                MissingSince = 0;
                Missing = Due = Queued = false;
            }

            internal void Present()
            {
                Missing = Due = false;
                MissingSince = 0;
            }
        }

        private readonly Site[] sites;
        private readonly Dictionary<int, int> indices;
        private readonly Queue<int> dueSites;
        private int nextCheck;

        internal TreeRespawnState(List<Site> captured, int tick)
            : this(Sort(captured), unchecked(tick + TicksPerMinute)) { }

        private TreeRespawnState(Site[] ordered, int nextCheck)
        {
            this.nextCheck = nextCheck;
            sites = ordered;
            indices = new Dictionary<int, int>(sites.Length);
            dueSites = new Queue<int>(sites.Length);
            for (int index = 0; index < sites.Length; index++)
            {
                indices.Add(sites[index].Key, index);
                if (sites[index].Queued)
                    dueSites.Enqueue(index);
            }
        }

        internal int Count => sites.Length;
        internal static int KeyOf(ushort x, ushort y) => (y << 16) | x;
        internal static bool IsWoodland(VegetationType type) =>
            type == VegetationType.ChestnutTree || type == VegetationType.OakTree ||
            type == VegetationType.PineTree || type == VegetationType.BirchTree;
        internal bool Contains(int key) => indices.ContainsKey(key);

        private static Site[] Sort(List<Site> captured)
        {
            captured.Sort((left, right) => left.Key.CompareTo(right.Key));
            return captured.ToArray();
        }

        internal void Removed(int key, int tick)
        {
            if (!indices.TryGetValue(key, out int index))
                return;
            ref Site site = ref sites[index];
            site.Missing = true;
            site.Due = false;
            site.MissingSince = tick;
        }

        internal bool HasWork(int tick) => dueSites.Count != 0 || unchecked(tick - nextCheck) >= 0;

        internal void ReconcileSettings(int tick, int minutes)
        {
            if (minutes == Off)
                dueSites.Clear();
            foreach (ref Site site in sites.AsSpan())
            {
                if (minutes == Off)
                {
                    site.Present();
                    site.Queued = false;
                }
                else if (site.Due)
                {
                    // A save can be loaded with different host settings than it was written with.
                    site.Due = unchecked((uint)(tick - site.MissingSince)) >= (uint)(minutes * TicksPerMinute);
                }
            }
        }

        // Returns whether the game needs a path component rebuild after this batch.
        internal bool Advance(int tick, int minutes, Func<Site, bool> hasTree, Func<Site, bool> tryPlant)
        {
            int delay = minutes * TicksPerMinute;
            if (dueSites.Count == 0 && unchecked(tick - nextCheck) >= 0)
            {
                nextCheck = unchecked(tick + TicksPerMinute);
                for (int index = 0; index < sites.Length; index++)
                {
                    ref Site site = ref sites[index];
                    if (hasTree(site))
                        site.Present();
                    else
                    {
                        if (!site.Missing)
                        {
                            site.Missing = true;
                            site.MissingSince = tick;
                        }
                        if (IsDue(site, tick, delay))
                        {
                            site.Due = true;
                            if (!site.Queued)
                            {
                                site.Queued = true;
                                dueSites.Enqueue(index);
                            }
                        }
                    }
                }
            }

            bool planted = false;
            for (int attempt = 0; attempt < AttemptsPerTick && dueSites.Count != 0; attempt++)
            {
                ref Site site = ref sites[dueSites.Dequeue()];
                site.Queued = false;
                // A deletion can start a new timer while the previous attempt is queued.
                if (!site.Due)
                    continue;
                if (hasTree(site))
                    site.Present();
                else if (tryPlant(site))
                {
                    site.Present();
                    planted = true;
                }
            }
            return planted;
        }

        private static bool IsDue(Site site, int tick, int delay) =>
            site.Due || unchecked((uint)(tick - site.MissingSince)) >= (uint)delay;

        // Persist the cadence and any unfinished batch along with the original coordinates.
        internal byte[] Encode()
        {
            using (var stream = new MemoryStream(HeaderSize + RecordSize * sites.Length))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(nextCheck);
                writer.Write(sites.Length);
                foreach (Site site in sites)
                {
                    writer.Write(site.X);
                    writer.Write(site.Y);
                    writer.Write((ushort)site.Species);
                    writer.Write(site.MissingSince);
                    writer.Write((byte)((site.Missing ? 1 : 0) | (site.Due ? 2 : 0) | (site.Queued ? 4 : 0)));
                }
                return stream.ToArray();
            }
        }

        internal static TreeRespawnState Decode(byte[] data, int tick, Func<Site, bool> validTile)
        {
            if (data == null || data.Length < HeaderSize)
                return null;
            using (var stream = new MemoryStream(data, writable: false))
            using (var reader = new BinaryReader(stream))
            {
                int nextCheck = reader.ReadInt32();
                int count = reader.ReadInt32();
                if (unchecked(nextCheck - tick) > TicksPerMinute || count < 0 || count >= VegetationSlots ||
                    data.Length != HeaderSize + RecordSize * count)
                    return null;
                var restored = new Site[count];
                for (int index = 0; index < count; index++)
                {
                    var site = new Site(reader.ReadUInt16(), reader.ReadUInt16(), (VegetationType)reader.ReadUInt16());
                    site.MissingSince = reader.ReadInt32();
                    byte flags = reader.ReadByte();
                    if (flags > 7 || !IsWoodland(site.Species) || !validTile(site) ||
                        (index > 0 && site.Key <= restored[index - 1].Key))
                        return null;
                    site.Missing = (flags & 1) != 0;
                    site.Due = (flags & 2) != 0;
                    site.Queued = (flags & 4) != 0;
                    if ((!site.Missing && (site.Due || site.MissingSince != 0)) ||
                        (site.Missing && !site.Due && unchecked(tick - site.MissingSince) < 0))
                        return null;
                    restored[index] = site;
                }
                return new TreeRespawnState(restored, nextCheck);
            }
        }
    }
}
