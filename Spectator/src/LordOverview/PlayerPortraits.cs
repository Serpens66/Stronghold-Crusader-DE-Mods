using System;

namespace Spectator.LordOverview
{
    internal enum PortraitKind { Missing, Ai, Local, Steam }

    internal readonly struct PortraitIdentity
    {
        internal readonly PortraitKind Kind;
        internal readonly int Lord;
        internal readonly ulong SteamId;
        private PortraitIdentity(PortraitKind kind, int lord = 0, ulong steamId = 0)
        { Kind = kind; Lord = lord; SteamId = steamId; }
        internal static PortraitIdentity Choose(bool ai, int lord, bool knownMode, bool multiplayer, ulong validSteamId)
        {
            if (ai) return new PortraitIdentity(PortraitKind.Ai, lord);
            if (!knownMode) return default;
            if (!multiplayer) return new PortraitIdentity(PortraitKind.Local);
            return validSteamId == 0 ? default : new PortraitIdentity(PortraitKind.Steam, steamId: validSteamId);
        }
        internal bool Same(PortraitIdentity other) => Kind == other.Kind && Lord == other.Lord && SteamId == other.SteamId;
    }

    internal interface IPlayerPortraitSource<T> where T : class
    {
        PortraitIdentity Identify(int player);
        T Read(PortraitIdentity identity);
    }

    // Only retains sources already owned by Vanilla. No texture creation or private cache access.
    // Called synchronously on the presentation thread; a reset retires an in-flight read.
    internal sealed class PlayerPortraits<T> where T : class
    {
        private struct Entry
        {
            internal PortraitIdentity Identity;
            internal T Image;
            internal bool Checked;
            internal double NextRead;
        }
        private readonly IPlayerPortraitSource<T> source;
        private readonly Entry[] entries = new Entry[9]; // One-based player IDs 1..8.
        private int active, pending, revision;
        private double nextPendingRead;
        internal PlayerPortraits(IPlayerPortraitSource<T> source) { this.source = source; }
        internal bool HasPending => pending != 0;
        internal void SetPlayers(int mask)
        {
            mask &= (1 << entries.Length) - 2;
            if (mask == active) return;
            revision++;
            for (int player = 1; player < entries.Length; player++)
                if ((mask & (1 << player)) == 0) entries[player] = default;
            active = mask; pending &= mask;
            if (pending == 0) nextPendingRead = 0;
        }
        internal T Resolve(int player, double now)
        {
            if (player < 1 || player >= entries.Length || (active & (1 << player)) == 0) return null;
            int stamp = revision;
            var identity = source.Identify(player);
            if (stamp != revision) return null;
            ref Entry entry = ref entries[player];
            if (!entry.Identity.Same(identity)) entry = new Entry { Identity = identity };
            if (!entry.Checked || now >= entry.NextRead)
            {
                T image = identity.Kind == PortraitKind.Missing ? null : source.Read(identity);
                if (stamp != revision) return null;
                entry.Image = image; entry.Checked = true; entry.NextRead = now + 1;
            }
            int bit = 1 << player;
            if (entry.Image == null)
            {
                if (pending == 0) nextPendingRead = now + 1;
                pending |= bit;
            }
            else pending &= ~bit;
            return entry.Image;
        }
        internal int Poll(double now)
        {
            if (pending == 0 || now < nextPendingRead) return 0;
            nextPendingRead = now + 1;
            int mask = pending, stamp = revision, changed = 0;
            for (int player = 1; player < entries.Length; player++)
            {
                int bit = 1 << player;
                if ((mask & bit) == 0) continue;
                T previous = entries[player].Image, image = Resolve(player, now);
                if (stamp != revision) return 0;
                if (!ReferenceEquals(previous, image)) changed |= bit;
            }
            return changed;
        }
        internal T Cached(int player) => player > 0 && player < entries.Length ? entries[player].Image : null;
        internal void Reset()
        {
            revision++; active = pending = 0; nextPendingRead = 0;
            Array.Clear(entries, 0, entries.Length);
        }
    }
}
