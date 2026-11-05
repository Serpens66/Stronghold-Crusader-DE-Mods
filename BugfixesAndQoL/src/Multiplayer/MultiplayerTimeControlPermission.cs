using MessagePack;
using MessagePack.Formatters;

namespace BugfixesAndQoL
{
    [MessagePackFormatter(typeof(MultiplayerTimeControlPermissionFormatter))]
    public enum MultiplayerTimeControlPermission
    {
        Disabled = 0,
        OnlyHost = 1,
        Everyone = 2
    }

    public sealed class MultiplayerTimeControlPermissionFormatter : IMessagePackFormatter<MultiplayerTimeControlPermission>
    {
        public void Serialize(
            ref MessagePackWriter writer,
            MultiplayerTimeControlPermission value,
            MessagePackSerializerOptions options)
        {
            if (!MultiplayerTimeControlPolicy.IsDefinedPermission(value))
                throw new MessagePackSerializationException($"Unknown multiplayer time-control permission [{(int)value}].");

            writer.Write((int)value);
        }

        public MultiplayerTimeControlPermission Deserialize(
            ref MessagePackReader reader,
            MessagePackSerializerOptions options)
        {
            if (reader.NextMessagePackType == MessagePackType.Boolean)
            {
                // Version 1.0.105 stored this setting as a Boolean: enabled meant everyone.
                return reader.ReadBoolean()
                    ? MultiplayerTimeControlPermission.Everyone
                    : MultiplayerTimeControlPermission.Disabled;
            }

            if (reader.NextMessagePackType != MessagePackType.Integer)
                throw new MessagePackSerializationException("Multiplayer time-control permission must be a Boolean or integer.");

            int rawValue = reader.ReadInt32();
            var value = (MultiplayerTimeControlPermission)rawValue;
            if (!MultiplayerTimeControlPolicy.IsDefinedPermission(value))
                throw new MessagePackSerializationException($"Unknown multiplayer time-control permission [{rawValue}].");

            return value;
        }
    }

    public static class MultiplayerTimeControlPolicy
    {
        public static bool IsDefinedPermission(MultiplayerTimeControlPermission permission) =>
            permission == MultiplayerTimeControlPermission.Disabled ||
            permission == MultiplayerTimeControlPermission.OnlyHost ||
            permission == MultiplayerTimeControlPermission.Everyone;

        public static bool CanRequest(MultiplayerTimeControlPermission permission, bool isLocalHost) =>
            permission == MultiplayerTimeControlPermission.Everyone ||
            permission == MultiplayerTimeControlPermission.OnlyHost && isLocalHost;
    }
}
