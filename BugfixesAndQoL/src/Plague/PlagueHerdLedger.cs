using System;
using System.Collections.Generic;

namespace BugfixesAndQoL
{
    internal readonly struct PlagueProjectileIdentity
    {
        internal PlagueProjectileIdentity(int slotId, uint globalId)
        {
            SlotId = slotId;
            GlobalId = globalId;
        }
        internal int SlotId { get; }
        internal uint GlobalId { get; }
    }

    // Simulation/save callbacks own this ledger under the runtime's state lock.
    // Linked nodes preserve save order and allow deletion without a global herd search.
    internal sealed class PlagueHerdLedger
    {
        private sealed class Herd
        {
            internal int PlayerId;
            internal List<PlagueProjectileIdentity> Members;
            internal LinkedListNode<Herd> AllNode;
            internal LinkedListNode<Herd> PlayerNode;
        }

        private readonly LinkedList<Herd> all = new LinkedList<Herd>();
        private readonly Dictionary<int, LinkedList<Herd>> players = new Dictionary<int, LinkedList<Herd>>();
        private readonly Dictionary<int, Herd> slots = new Dictionary<int, Herd>();

        internal int Count => all.Count;
        internal int CountHerds(int playerId) => players.TryGetValue(playerId, out var herds) ? herds.Count : 0;
        internal int CountProjectiles(int playerId)
        {
            int count = 0;
            if (players.TryGetValue(playerId, out var herds))
                foreach (var herd in herds) count += herd.Members.Count;
            return count;
        }

        internal void Clear()
        {
            all.Clear();
            players.Clear();
            slots.Clear();
        }

        internal void Add(int playerId, List<PlagueProjectileIdentity> members)
        {
            if (members == null || members.Count == 0) throw new ArgumentException("An empty plague herd cannot be tracked.");
            // A newly spawned identity supersedes an earlier occupant of the same slot.
            foreach (var member in members)
                if (slots.TryGetValue(member.SlotId, out var previous)) RemoveSlot(previous, member.SlotId);
            var herd = new Herd { PlayerId = playerId, Members = new List<PlagueProjectileIdentity>(members) };
            if (!players.TryGetValue(playerId, out var playerHerds))
                players.Add(playerId, playerHerds = new LinkedList<Herd>());
            herd.AllNode = all.AddLast(herd);
            herd.PlayerNode = playerHerds.AddLast(herd);
            foreach (var member in herd.Members) slots.Add(member.SlotId, herd);
        }

        internal void ReconcilePlayer(int playerId, Func<PlagueProjectileIdentity, bool> isLiving)
        {
            if (!players.TryGetValue(playerId, out var herds)) return;
            for (var node = herds.First; node != null;)
            {
                var next = node.Next;
                ReconcileHerd(node.Value, isLiving);
                node = next;
            }
        }

        internal void ReconcileAll(Func<PlagueProjectileIdentity, bool> isLiving)
        {
            for (var node = all.First; node != null;)
            {
                var next = node.Next;
                ReconcileHerd(node.Value, isLiving);
                node = next;
            }
        }

        // Post may report the original slot although a Pre subscriber changed the target.
        // Keep that slot if it still contains the same living identity.
        internal void ReconcileDeletedSlot(int slotId, Func<PlagueProjectileIdentity, bool> isLiving)
        {
            if (!slots.TryGetValue(slotId, out var herd)) return;
            foreach (var member in herd.Members)
            {
                if (member.SlotId != slotId) continue;
                if (!isLiving(member)) RemoveSlot(herd, slotId);
                return;
            }
        }

        private void ReconcileHerd(Herd herd, Func<PlagueProjectileIdentity, bool> isLiving)
        {
            for (int index = herd.Members.Count - 1; index >= 0; index--)
            {
                var member = herd.Members[index];
                if (isLiving(member)) continue;
                slots.Remove(member.SlotId);
                herd.Members.RemoveAt(index);
            }
            if (herd.Members.Count == 0) RemoveHerd(herd);
        }

        private void RemoveSlot(Herd herd, int slotId)
        {
            slots.Remove(slotId);
            for (int index = herd.Members.Count - 1; index >= 0; index--)
                if (herd.Members[index].SlotId == slotId) herd.Members.RemoveAt(index);
            if (herd.Members.Count == 0) RemoveHerd(herd);
        }

        private void RemoveHerd(Herd herd)
        {
            all.Remove(herd.AllNode);
            var playerHerds = players[herd.PlayerId];
            playerHerds.Remove(herd.PlayerNode);
            if (playerHerds.Count == 0) players.Remove(herd.PlayerId);
        }

        internal PlagueHerdSaveRecord[] ToSaveRecords()
        {
            var records = new PlagueHerdSaveRecord[all.Count];
            int herdIndex = 0;
            foreach (var herd in all)
            {
                var record = new PlagueHerdSaveRecord
                {
                    PlayerId = herd.PlayerId,
                    ProjectileSlotIds = new int[herd.Members.Count],
                    ProjectileGlobalIds = new uint[herd.Members.Count]
                };
                for (int index = 0; index < herd.Members.Count; index++)
                {
                    record.ProjectileSlotIds[index] = herd.Members[index].SlotId;
                    record.ProjectileGlobalIds[index] = herd.Members[index].GlobalId;
                }
                records[herdIndex++] = record;
            }
            return records;
        }

        internal void Load(PlagueHerdSaveRecord[] records)
        {
            Clear();
            foreach (var record in records)
            {
                var members = new List<PlagueProjectileIdentity>(record.ProjectileSlotIds.Length);
                for (int index = 0; index < record.ProjectileSlotIds.Length; index++)
                    members.Add(new PlagueProjectileIdentity(record.ProjectileSlotIds[index], record.ProjectileGlobalIds[index]));
                Add(record.PlayerId, members);
            }
        }
    }
}
