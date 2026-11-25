using APIShared.Multiplayer;
using SHCDESE.API;
using System;
using System.Collections.Generic;
using System.IO;

namespace BugfixesAndQoL
{
    internal sealed unsafe partial class PlaguePopularityFix
    {
        private const string ResyncId = "plague-popularity";
        // Chore 62 has 6,000 projectile slots; the herd ledger excludes slot zero.
        private const int ResyncProjectileSlots = 6000;
        private const int ResyncHeaderBytes = sizeof(byte) + sizeof(ushort);
        private const int ResyncHerdBytes = sizeof(byte) + sizeof(ushort);
        private const int ResyncMemberBytes = sizeof(ushort) + sizeof(uint);
        private const int ResyncMaximumBytes = ResyncHeaderBytes +
            (ResyncProjectileSlots - 1) * (ResyncHerdBytes + ResyncMemberBytes);

        private volatile bool repairing;
        private bool resyncReady;
        private ResyncPopulation pendingResync;

        private sealed class ResyncPopulation
        {
            internal readonly PlagueHerdLedger Herds = new PlagueHerdLedger();
            internal readonly HashSet<int> Managed = new HashSet<int>();
        }

        private void RegisterResync()
        {
            resyncReady = MultiplayerResyncEvents.TryRegister(BugfixesAndQoLPlugin.PluginGuid, ResyncId,
                BeginResync, CompleteResync, out string reason) &&
                MultiplayerResyncData.TryRegister<ResyncPopulation>(BugfixesAndQoLPlugin.PluginGuid, ResyncId, 1,
                    ResyncMaximumBytes, CaptureResync, PrepareResync, CommitResync, out reason);
            if (!resyncReady)
                Shared.DebugLogHelper.LogError(log, "Plague popularity multiplayer data registration failed: " + reason);
        }

        private void BeginResync()
        {
            if (!resyncReady) return;
            lock (stateGate)
            {
                pendingResync = null;
                repairing = true;
            }
        }

        private byte[] CaptureResync()
        {
            lock (stateGate)
            {
                if (!mapActive || !correctionAvailable) return Array.Empty<byte>();

                byte players = 0;
                foreach (int player in managedPlayerIds)
                    players |= (byte)(1 << (player - 1));

                var groups = new List<(int Player, List<PlagueProjectileIdentity> Members)>();
                foreach (var record in herds.ToSaveRecords())
                {
                    var members = new List<PlagueProjectileIdentity>(record.ProjectileSlotIds.Length);
                    for (int i = 0; i < record.ProjectileSlotIds.Length; i++)
                    {
                        var member = new PlagueProjectileIdentity(record.ProjectileSlotIds[i], record.ProjectileGlobalIds[i]);
                        if (member.SlotId > 0 && member.SlotId < ResyncProjectileSlots && isLivingProjectile(member))
                            members.Add(member);
                    }
                    if (members.Count == 0) continue;
                    members.Sort((a, b) => a.SlotId.CompareTo(b.SlotId));
                    groups.Add((record.PlayerId, members));
                }
                groups.Sort((a, b) => a.Members[0].SlotId.CompareTo(b.Members[0].SlotId));

                using (var stream = new MemoryStream())
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write(players);
                    writer.Write((ushort)groups.Count);
                    foreach (var group in groups)
                    {
                        writer.Write((byte)group.Player);
                        writer.Write((ushort)group.Members.Count);
                        foreach (var member in group.Members)
                        {
                            writer.Write((ushort)member.SlotId);
                            writer.Write(member.GlobalId);
                        }
                    }
                    writer.Flush();
                    return stream.ToArray();
                }
            }
        }

        private ResyncPopulation PrepareResync(ReadOnlyMemory<byte> record)
        {
            lock (stateGate)
            {
                if (!repairing || record.IsEmpty == (mapActive && correctionAvailable))
                    throw new InvalidDataException("Plague popularity availability differs during repair.");

                var result = new ResyncPopulation();
                if (record.IsEmpty) return result;
                var occupiedSlots = new bool[ResyncProjectileSlots];
                using (var stream = new MemoryStream(record.ToArray(), writable: false))
                using (var reader = new BinaryReader(stream))
                {
                    byte players = reader.ReadByte();
                    for (int player = 1; player <= GamePlayerManagerAPI.MAX_PLAYERS; player++)
                        if ((players & (1 << (player - 1))) != 0) result.Managed.Add(player);
                    int groups = reader.ReadUInt16();
                    if (groups >= ResyncProjectileSlots) throw new InvalidDataException("Too many plague groups.");

                    int previousFirst = 0;
                    for (int group = 0; group < groups; group++)
                    {
                        int owner = reader.ReadByte();
                        int count = reader.ReadUInt16();
                        if (!result.Managed.Contains(owner) || count <= 0 || count >= ResyncProjectileSlots ||
                            count * ResyncMemberBytes > stream.Length - stream.Position)
                            throw new InvalidDataException("Invalid plague group in repair.");
                        var members = new List<PlagueProjectileIdentity>(count);
                        int previousSlot = 0;
                        for (int i = 0; i < count; i++)
                        {
                            int slot = reader.ReadUInt16();
                            uint global = reader.ReadUInt32();
                            if (slot <= previousSlot || slot >= ResyncProjectileSlots || global == 0 || occupiedSlots[slot] ||
                                (i == 0 && slot <= previousFirst))
                                throw new InvalidDataException("Invalid plague projectile identity in repair.");
                            previousSlot = slot;
                            if (i == 0) previousFirst = slot;
                            occupiedSlots[slot] = true;
                            var member = new PlagueProjectileIdentity(slot, global);
                            if (isLivingProjectile(member)) members.Add(member);
                        }
                        if (members.Count != 0) result.Herds.Add(owner, members);
                    }
                    if (stream.Position != stream.Length) throw new InvalidDataException("Trailing plague repair bytes.");
                }
                return result;
            }
        }

        private void CommitResync(ResyncPopulation state) => pendingResync = state;

        private void CompleteResync()
        {
            lock (stateGate)
            {
                if (!repairing) return;
                if (pendingResync == null)
                    throw new InvalidOperationException("Plague popularity repair completed without the host state.");
                herds = pendingResync.Herds;
                managedPlayerIds = pendingResync.Managed;
                pendingResync = null;
                repairing = false;
            }
        }
    }
}
