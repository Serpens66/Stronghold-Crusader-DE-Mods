using APIShared;
using APIShared.Networking;
using APIShared.Savegames;
using BepInEx.Logging;
using MessagePack;
using MessagePack.Formatters;
using R3;
using SHCDESE.API;
using SHCDESE.Interop;
using System;
using System.Threading;

namespace ImprovedHunters
{
    /// <summary>
    /// Keeps the Tannery table identical on all peers and in saves: a slider change is a Chore that
    /// runs in the same tick everywhere, and the table is a simulation record of the save.
    /// </summary>
    internal sealed class TannerRatioSync
    {
        private const string SaveEntry = "tanneries";
        private const int ProtocolVersion = 1;

        private readonly ManualLogSource log;
        private readonly TannerRunTable table;
        private readonly AuthenticatedChoreChannel<TannerRatioPacket> channel;
        private bool archiveLoadedForMission;
        private int lastSentSerial;
        private int awaitedSerial;

        internal TannerRatioSync(ManualLogSource log, TannerRunTable table, Func<bool> isEnabled)
        {
            this.log = log;
            this.table = table;

            // Registered on every peer, whatever the settings say: packet IDs follow the registration order.
            channel = ApiShared.ForMod(ImprovedHuntersPlugin.PluginGuid).Network.RegisterAuthenticatedChore<TannerRatioPacket>(
                "tanner-ratio",
                message =>
                {
                    table.SetMeatRuns(message.Packet.BuildingId, message.Packet.GlobalId, message.Packet.MeatRuns);
                    // Presentation only: tells this peer's panel that its last change has arrived.
                    // Every peer counts its own serials, so another player's may carry the same number.
                    if (message.SenderPlayerId == PlayerPerspectiveAPI.GetControlledPlayerId())
                        Interlocked.CompareExchange(ref awaitedSerial, 0, message.Packet.Serial);
                },
                new AuthenticatedChoreChannelOptions<TannerRatioPacket>
                {
                    MaximumPayloadBytes = 64,
                    IsEnabled = isEnabled,
                    ValidatePayload = IsValidPayload,
                    ValidateReceive = IsOwnTannery,
                    Diagnostic = failure => Shared.DebugLogHelper.LogWarning(log, $"Improved Hunters Tannery Chore: {failure.Reason}")
                });

            var registration = new VersionedSaveDataRegistration(
                ImprovedHuntersPlugin.PluginGuid,
                "ImprovedHunters.Tanneries",
                1,
                1,
                new[] { SaveEntry },
                writer =>
                {
                    writer.Write(SaveEntry, table.Encode());
                    return true;
                },
                OnArchiveLoaded)
            {
                SimulationRelevant = true
            };
            if (!VersionedSaveData.TryRegister(registration, out _, out string reason))
                throw new InvalidOperationException(reason);

            // A mission without an archive load must not inherit the table of an earlier one.
            Shared.MissionEvents.Started.Subscribe(_ =>
            {
                if (!archiveLoadedForMission)
                    table.Clear();

                archiveLoadedForMission = false;
            });
        }

        /// <summary>Whether this peer's last slider change has not reached the table yet.</summary>
        internal bool HasPendingChange => Volatile.Read(ref awaitedSerial) != 0;

        /// <summary>For a closed panel: a change that never arrives must not hold the next opening back.</summary>
        internal void ForgetPendingChange() => Volatile.Write(ref awaitedSerial, 0);

        internal bool Send(int buildingId, uint globalId, int meatRuns)
        {
            int serial = ++lastSentSerial;
            NetworkSendResult result = channel.Send(new TannerRatioPacket
            {
                Protocol = ProtocolVersion,
                BuildingId = buildingId,
                GlobalId = globalId,
                MeatRuns = meatRuns,
                Serial = serial
            });
            if (!result.Submitted)
            {
                Shared.DebugLogHelper.LogWarning(log, $"Improved Hunters could not send the Tannery ratio: {result.Reason}");
                return false;
            }

            Volatile.Write(ref awaitedSerial, serial);
            return true;
        }

        private void OnArchiveLoaded(VersionedSaveLoad load)
        {
            archiveLoadedForMission = true;
            if (load.Status != VersionedSaveLoadStatus.Restored)
            {
                table.Clear();
                return;
            }

            if (!load.Entries.TryGetValue(SaveEntry, out byte[] data) || !table.TryRestore(data))
            {
                table.Clear();
                Shared.DebugLogHelper.LogError(log, "Improved Hunters found a damaged Tannery record; Tanneries start on their own good.");
            }
        }

        private static bool IsValidPayload(TannerRatioPacket packet) =>
            packet.Protocol == ProtocolVersion &&
            TannerRunTable.IsValidId(packet.BuildingId) &&
            TannerMeatPolicy.IsValidMeatRuns(packet.MeatRuns);

        private static unsafe bool IsOwnTannery(AuthenticatedChoreMessage<TannerRatioPacket> message)
        {
            return global::APIShared.BuildingAccess.TryGetById(message.Packet.BuildingId, out GameBuilding* building, out _) &&
                building != null &&
                building->r_GlobalId == message.Packet.GlobalId &&
                building->r_BuildingType == eStructs.STRUCT_TANNERS_WORKSHOP &&
                building->r_PlayerIdOwner == message.SenderPlayerId;
        }
    }

    [MessagePackObject]
    [MessagePackFormatter(typeof(TannerRatioPacketFormatter))]
    public sealed class TannerRatioPacket
    {
        [Key(0)] public int Protocol;
        [Key(1)] public int BuildingId;
        [Key(2)] public uint GlobalId;
        [Key(3)] public int MeatRuns;
        [Key(4)] public int Serial;
    }

    public sealed class TannerRatioPacketFormatter : IMessagePackFormatter<TannerRatioPacket>
    {
        public void Serialize(ref MessagePackWriter writer, TannerRatioPacket packet, MessagePackSerializerOptions options)
        {
            if (packet == null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteArrayHeader(5);
            writer.Write(packet.Protocol);
            writer.Write(packet.BuildingId);
            writer.Write(packet.GlobalId);
            writer.Write(packet.MeatRuns);
            writer.Write(packet.Serial);
        }

        public TannerRatioPacket Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return null;

            int count = reader.ReadArrayHeader();
            var packet = new TannerRatioPacket();
            for (int index = 0; index < count; index++)
            {
                switch (index)
                {
                    case 0: packet.Protocol = reader.ReadInt32(); break;
                    case 1: packet.BuildingId = reader.ReadInt32(); break;
                    case 2: packet.GlobalId = reader.ReadUInt32(); break;
                    case 3: packet.MeatRuns = reader.ReadInt32(); break;
                    case 4: packet.Serial = reader.ReadInt32(); break;
                    default: reader.Skip(); break;
                }
            }

            return packet;
        }
    }
}
