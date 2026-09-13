using System;
using System.Collections.Generic;
using System.IO;

namespace MoatMove
{
    internal readonly struct FastUnitIdentity : IEquatable<FastUnitIdentity>, IComparable<FastUnitIdentity>
    {
        internal FastUnitIdentity(int id, uint global) { Id = id; Global = global; }
        internal int Id { get; }
        internal uint Global { get; }
        public bool Equals(FastUnitIdentity other) => Id == other.Id && Global == other.Global;
        public override bool Equals(object other) => other is FastUnitIdentity identity && Equals(identity);
        public override int GetHashCode() => unchecked(Id * 397 ^ (int)Global);
        public int CompareTo(FastUnitIdentity other)
        { int order = Id.CompareTo(other.Id); return order != 0 ? order : Global.CompareTo(other.Global); }
    }

    internal sealed class FastPendingCommand
    {
        internal long Sequence;
        internal int Player, TribeId, X, Y, Patrol, MoveFlags, EnqueuedTick;
        internal uint TribeGlobal;
        internal bool IsWaypoint;
        internal readonly List<FastUnitIdentity> Members = new List<FastUnitIdentity>();
        // Managed leases are deliberately absent from the save representation.
        internal object SearchState;
    }

    // Deterministic command ownership. Native pointers, input polling and wall clocks
    // are not part of this model. Removing one cohort member never drops the others.
    internal sealed class FastCommandQueue
    {
        private const uint Magic = 0x31464D4D; // MMF1
        private const int MaximumUnits = 10000;
        private readonly List<FastPendingCommand> commands = new List<FastPendingCommand>();
        private long nextSequence = 1;
        internal IReadOnlyList<FastPendingCommand> Commands => commands;
        internal event Action<FastPendingCommand> Removed;

        internal FastPendingCommand Enqueue(int player, int tribeId, uint tribeGlobal,
            int x, int y, int patrol, int moveFlags, int tick, IEnumerable<FastUnitIdentity> members)
        {
            var command = new FastPendingCommand { Sequence = nextSequence++, Player = player,
                TribeId = tribeId, TribeGlobal = tribeGlobal, X = x, Y = y,
                Patrol = patrol, MoveFlags = moveFlags, EnqueuedTick = tick };
            var unique = new SortedSet<FastUnitIdentity>(members);
            if (unique.Count == 0) return null;
            if (unique.Count > MaximumUnits) throw new ArgumentOutOfRangeException(nameof(members));
            Supersede(unique);
            command.Members.AddRange(unique);
            commands.Add(command);
            return command;
        }

        internal void Supersede(IEnumerable<FastUnitIdentity> members)
        {
            var replaced = new HashSet<FastUnitIdentity>(members);
            for (int i = commands.Count - 1; i >= 0; i--)
            {
                FastPendingCommand command = commands[i];
                command.Members.RemoveAll(replaced.Contains);
                if (command.Members.Count == 0) Remove(command);
            }
        }

        internal bool HasPredecessor(IEnumerable<FastUnitIdentity> members, long before = long.MaxValue)
        {
            var identities = new HashSet<FastUnitIdentity>(members);
            foreach (FastPendingCommand command in commands)
                if (command.Sequence < before && command.Members.Exists(identities.Contains)) return true;
            return false;
        }

        internal void AppendWaypoint(int player, int tribeId, uint global, int x, int y,
            int index, short mode, int tick, IEnumerable<FastUnitIdentity> members)
        {
            var identities = new SortedSet<FastUnitIdentity>(members);
            // Vanilla repeatedly overwrites its last slot after its ten-point limit.
            // Apply that replacement per member instead of growing an unbounded queue.
            for (int i = commands.Count - 1; i >= 0; i--)
            {
                FastPendingCommand old = commands[i];
                if (!old.IsWaypoint || old.MoveFlags != index) continue;
                old.Members.RemoveAll(identities.Contains);
                if (old.Members.Count == 0) Remove(old);
            }
            var command = new FastPendingCommand { Sequence = nextSequence++, Player = player,
                TribeId = tribeId, TribeGlobal = global, X = x, Y = y, Patrol = mode,
                MoveFlags = index, EnqueuedTick = tick, IsWaypoint = true };
            command.Members.AddRange(identities);
            if (command.Members.Count != 0) commands.Add(command);
        }

        internal void Remove(FastPendingCommand command)
        { if (commands.Remove(command)) Removed?.Invoke(command); }

        internal void Clear()
        {
            while (commands.Count != 0) Remove(commands[commands.Count - 1]);
            nextSequence = 1;
        }

        // Always return an explicit empty record too: returning null would leave an
        // earlier archive entry present in the installed ModSaveDataAPI implementation.
        internal byte[] Save()
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Magic); writer.Write(1); writer.Write(nextSequence);
                writer.Write(commands.Count);
                foreach (FastPendingCommand c in commands)
                {
                    writer.Write(c.IsWaypoint);
                    writer.Write(c.Sequence); writer.Write(c.Player); writer.Write(c.TribeId);
                    writer.Write(c.TribeGlobal); writer.Write(c.X); writer.Write(c.Y);
                    writer.Write(c.Patrol); writer.Write(c.MoveFlags); writer.Write(c.EnqueuedTick);
                    writer.Write(c.Members.Count);
                    foreach (FastUnitIdentity member in c.Members)
                    { writer.Write(member.Id); writer.Write(member.Global); }
                }
                writer.Flush(); return stream.ToArray();
            }
        }

        internal void Load(byte[] bytes)
        {
            // Validate completely before replacing live ownership. No partial import.
            if (bytes == null || bytes.Length > 8 * 1024 * 1024) throw new InvalidDataException("Fast save size.");
            var loaded = new List<FastPendingCommand>();
            var identities = new HashSet<FastUnitIdentity>();
            long next;
            using (var stream = new MemoryStream(bytes, false))
            using (var reader = new BinaryReader(stream))
            {
                if (reader.ReadUInt32() != Magic || reader.ReadInt32() != 1)
                    throw new InvalidDataException("Fast save version.");
                next = reader.ReadInt64(); int count = reader.ReadInt32();
                if (next < 1 || count < 0 || count > MaximumUnits * 11) throw new InvalidDataException("Fast save count.");
                long previous = 0;
                for (int i = 0; i < count; i++)
                {
                    var c = new FastPendingCommand { IsWaypoint = reader.ReadBoolean(), Sequence = reader.ReadInt64(), Player = reader.ReadInt32(),
                        TribeId = reader.ReadInt32(), TribeGlobal = reader.ReadUInt32(),
                        X = reader.ReadInt32(), Y = reader.ReadInt32(), Patrol = reader.ReadInt32(),
                        MoveFlags = reader.ReadInt32(), EnqueuedTick = reader.ReadInt32() };
                    int members = reader.ReadInt32();
                    if (c.Sequence <= previous || c.Sequence >= next || c.Player < 1 || c.Player > 8 ||
                        c.TribeId < 1 || c.TribeId >= 4500 || c.TribeGlobal == 0 ||
                        (uint)c.X >= 800 || (uint)c.Y >= 800 || members < 1 || members > MaximumUnits ||
                        (c.IsWaypoint && ((uint)c.MoveFlags >= 10 || c.Patrol < short.MinValue || c.Patrol > short.MaxValue)))
                        throw new InvalidDataException("Fast save command.");
                    previous = c.Sequence;
                    for (int j = 0; j < members; j++)
                    {
                        var member = new FastUnitIdentity(reader.ReadInt32(), reader.ReadUInt32());
                        if (member.Id < 1 || member.Id > MaximumUnits || member.Global == 0 ||
                            (!c.IsWaypoint && !identities.Add(member)) || (j > 0 && c.Members[j - 1].CompareTo(member) >= 0))
                            throw new InvalidDataException("Fast save member.");
                        c.Members.Add(member);
                    }
                    loaded.Add(c);
                }
                if (stream.Position != stream.Length) throw new InvalidDataException("Fast save trailing data.");
            }
            Clear(); nextSequence = next; commands.AddRange(loaded);
        }
    }
}
