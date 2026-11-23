using System;
using System.Collections.Generic;
using System.IO;
using APIShared.Multiplayer;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace RandomEvents
{
    internal sealed partial class RandomEventsRuntime
    {
        // On the audited game image there are at most 9999 live unit IDs. After dropping
        // dead/replaced members, each pending group owns at least one distinct live ID.
        private const int MaximumRepairUnits = 9999;
        private bool resyncRegistered;
        private int repairedTimelineBeforeMonth;

        private void RegisterResync()
        {
            int playerCount = GamePlayerManagerAPI.MAX_PLAYERS;
            int rowHeader = sizeof(int) * 9 + playerCount * sizeof(int);
            int maximum = 13 + sizeof(int) * (RandomEventsCooldownCodec.EventCount +
                RandomEventsCooldownCodec.FullIndividualLength) + MaximumRepairUnits * (rowHeader + sizeof(ulong));
            resyncRegistered = MultiplayerResyncData.TryRegister<PreparedEventsRepair>(RandomEventsPlugin.PluginGuid,
                "event-continuation", 1, maximum, CaptureEventContinuation, PrepareEventContinuation,
                CommitEventContinuation, out string reason);
            if (!resyncRegistered) LogError("Random Events repair registration failed: " + reason);
        }

        private byte[] CaptureEventContinuation()
        {
            if (state == null || !multiplayerInitializationConfirmed) return Array.Empty<byte>();
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write(state.StartAbsoluteMonth);
            writer.Write(state.NextDueAbsoluteMonth);
            writer.Write(banditEventsEnabled);
            foreach (int month in state.SharedCooldownUntilAbsoluteMonths) writer.Write(month);
            foreach (int month in state.IndividualCooldownUntilAbsoluteMonths) writer.Write(month);

            long countOffset = stream.Position;
            writer.Write(0);
            int count = 0;
            foreach (PendingBanditGroup group in pendingBanditGroups)
            {
                var living = new List<BanditUnitReference>();
                foreach (BanditUnitReference member in group.Units)
                    if (MatchesRepairMember(member, group.OwnerPlayerId)) living.Add(member);
                if (living.Count == 0) continue;

                writer.Write(group.OwnerPlayerId);
                writer.Write(group.Team);
                writer.Write(group.TargetPlayerId);
                writer.Write(group.ExecuteAtMapTick);
                writer.Write(group.Target.BuildingId);
                writer.Write(group.Target.BuildingGlobalId);
                writer.Write(group.Target.TileX);
                writer.Write(group.Target.TileY);
                // Counts fit one byte/ushort under the producer's player and unit bounds.
                writer.Write((byte)group.ParticipantPlayerIds.Length);
                foreach (int player in group.ParticipantPlayerIds) writer.Write(player);
                writer.Write((ushort)living.Count);
                foreach (BanditUnitReference member in living)
                {
                    writer.Write(member.UnitId);
                    writer.Write(member.GlobalId);
                }
                count++;
            }
            stream.Position = countOffset;
            writer.Write(count);
            return stream.ToArray();
        }

        private PreparedEventsRepair PrepareEventContinuation(ReadOnlyMemory<byte> bytes)
        {
            // A repair can happen during the startup handshake. The existing initialization
            // Chore still owns the session and there is no executed batch state to import yet.
            if (bytes.IsEmpty) return null;
            using var stream = new MemoryStream(bytes.ToArray(), writable: false);
            using var reader = new BinaryReader(stream);
            int start = reader.ReadInt32();
            int next = reader.ReadInt32();
            byte bandits = reader.ReadByte();
            if (state == null || !multiplayerInitializationReceived || start != state.StartAbsoluteMonth ||
                state.IntervalMonths <= 0 || next < start || bandits > 1)
                throw new InvalidDataException("Invalid Random Events repair timeline.");

            int[] shared = ReadRepairCooldowns(reader, RandomEventsCooldownCodec.EventCount);
            int[] individual = ReadRepairCooldowns(reader, RandomEventsCooldownCodec.FullIndividualLength);
            int count = reader.ReadInt32();
            if (count < 0 || count > MaximumRepairUnits)
                throw new InvalidDataException("Invalid Random Events repair group count.");

            var groups = new List<PendingBanditGroup>(count);
            var usedUnits = new HashSet<int>();
            for (int index = 0; index < count; index++)
            {
                int owner = ReadRepairPlayer(reader);
                int team = reader.ReadInt32();
                int targetPlayer = ReadRepairPlayer(reader);
                int tick = reader.ReadInt32();
                var target = new BanditMoveTarget(reader.ReadInt32(), reader.ReadUInt32(), reader.ReadInt32(), reader.ReadInt32());
                int participantCount = reader.ReadByte();
                if (owner == targetPlayer || tick < 0 || target.BuildingId <= 0 || target.BuildingGlobalId == 0 ||
                    participantCount > GamePlayerManagerAPI.MAX_PLAYERS)
                    throw new InvalidDataException("Invalid Random Events repair group identity.");

                var participants = new int[participantCount];
                for (int participant = 0; participant < participantCount; participant++)
                    participants[participant] = ReadRepairPlayer(reader);
                int memberCount = reader.ReadUInt16();
                if (memberCount == 0 || memberCount > MaximumRepairUnits - usedUnits.Count)
                    throw new InvalidDataException("Invalid Random Events repair membership count.");
                var members = new List<BanditUnitReference>(memberCount);
                for (int member = 0; member < memberCount; member++)
                {
                    var entry = new BanditUnitReference(reader.ReadInt32(), reader.ReadUInt32());
                    if (entry.UnitId < 1 || entry.UnitId > MaximumRepairUnits || entry.GlobalId == 0 || !usedUnits.Add(entry.UnitId))
                        throw new InvalidDataException("Invalid Random Events repair member identity.");
                    if (MatchesRepairMember(entry, owner)) members.Add(entry);
                }
                if (members.Count != 0 && MatchesRepairTarget(target, targetPlayer))
                    groups.Add(new PendingBanditGroup(owner, team, participants, targetPlayer, members.ToArray(), target, tick));
            }
            if (stream.Position != stream.Length) throw new InvalidDataException("Trailing Random Events repair data.");
            return new PreparedEventsRepair(next, bandits != 0, shared, individual, groups);
        }

        private static int[] ReadRepairCooldowns(BinaryReader reader, int count)
        {
            var values = new int[count];
            for (int index = 0; index < values.Length; index++)
            {
                values[index] = reader.ReadInt32();
                if (values[index] < 0) throw new InvalidDataException("Invalid Random Events repair cooldown.");
            }
            return values;
        }

        private static int ReadRepairPlayer(BinaryReader reader)
        {
            int player = reader.ReadInt32();
            if (player < 1 || player > GamePlayerManagerAPI.MAX_PLAYERS)
                throw new InvalidDataException("Invalid Random Events repair player.");
            return player;
        }

        private static unsafe bool MatchesRepairMember(BanditUnitReference member, int owner)
        {
            if (!APIShared.UnitAccess.TryGetById(member.UnitId, out GameUnit* unit, out _) ||
                unit->r_GlobalId != member.GlobalId || unit->r_ControllableForPlayerId != owner)
                return false;
            // Placement can return NeedsInit before the first unit update. The later
            // scheduled activation still performs the real liveness and tribe checks.
            return unit->r_AliveState == AliveState.NeedsInit || APIShared.UnitAccess.IsReallyAlive(unit) &&
                unit->r_TribeId == 0 && unit->r_TribeLeaderUnitId == 0;
        }

        private static unsafe bool MatchesRepairTarget(BanditMoveTarget target, int player) =>
            APIShared.BuildingAccess.TryGetById(target.BuildingId, out GameBuilding* building, out _) &&
            building->r_GlobalId == target.BuildingGlobalId && building->r_AliveState == AliveState.IsAlive &&
            building->r_PlayerIdOwner == player && BanditTargetEligibility.IsEligibleStructureType(building->r_BuildingType);

        private void CommitEventContinuation(PreparedEventsRepair repair)
        {
            if (repair == null) return;
            state.NextDueAbsoluteMonth = repair.Next;
            repairedTimelineBeforeMonth = repair.Next;
            state.SharedCooldownUntilAbsoluteMonths = repair.Shared;
            state.IndividualCooldownUntilAbsoluteMonths = repair.Individual;
            banditEventsEnabled = repair.Bandits;
            pendingBanditGroups = repair.Groups;
        }

        private sealed class PreparedEventsRepair
        {
            internal PreparedEventsRepair(int next, bool bandits, int[] shared, int[] individual, List<PendingBanditGroup> groups)
            { Next = next; Bandits = bandits; Shared = shared; Individual = individual; Groups = groups; }
            internal readonly int Next;
            internal readonly bool Bandits;
            internal readonly int[] Shared, Individual;
            internal readonly List<PendingBanditGroup> Groups;
        }
    }
}
