using MessagePack;
using MessagePack.Formatters;
namespace Spectator
{
    [MessagePackObject]
    [MessagePackFormatter(typeof(EliminatedPlayerSpectatorPacketFormatter))]
    internal sealed class EliminatedPlayerSpectatorPacket
    {
        [Key(0)] public int PlayerId;
    }

    internal sealed class EliminatedPlayerSpectatorPacketFormatter : IMessagePackFormatter<EliminatedPlayerSpectatorPacket>
    {
        public void Serialize(ref MessagePackWriter writer, EliminatedPlayerSpectatorPacket value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            // Keep the Chore body to one byte for player slots 1-8. Session validity is
            // checked against the process-local APIShared lord-death latch on every peer.
            writer.Write(value.PlayerId);
        }

        public EliminatedPlayerSpectatorPacket Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return null;

            return new EliminatedPlayerSpectatorPacket
            {
                PlayerId = reader.ReadInt32()
            };
        }
    }
}
