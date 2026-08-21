// Feature: Versioned save-file payload for manual-only gatehouses.
using MessagePack;
using MessagePack.Formatters;

namespace ExtraFeatures
{
    [MessagePackObject]
    [MessagePackFormatter(typeof(GatehouseAutomationSaveStateFormatter))]
    internal sealed class GatehouseAutomationSaveState
    {
        public const int CurrentVersion = 1;

        [Key(0)] public int Version;
        [Key(1)] public int[] ManualOnlyGateGlobalIds;
    }

    internal sealed class GatehouseAutomationSaveStateFormatter : IMessagePackFormatter<GatehouseAutomationSaveState>
    {
        private const int FieldCount = 2;

        public void Serialize(ref MessagePackWriter writer, GatehouseAutomationSaveState value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteArrayHeader(FieldCount);
            writer.Write(value.Version);
            int[] ids = value.ManualOnlyGateGlobalIds;
            if (ids == null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteArrayHeader(ids.Length);
            for (int index = 0; index < ids.Length; index++)
                writer.Write(ids[index]);
        }

        public GatehouseAutomationSaveState Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return null;

            int fieldCount = reader.ReadArrayHeader();
            var state = new GatehouseAutomationSaveState();
            for (int index = 0; index < fieldCount; index++)
            {
                switch (index)
                {
                    case 0:
                        state.Version = reader.ReadInt32();
                        break;
                    case 1:
                        if (reader.TryReadNil())
                        {
                            state.ManualOnlyGateGlobalIds = null;
                            break;
                        }

                        int count = reader.ReadArrayHeader();
                        state.ManualOnlyGateGlobalIds = new int[count];
                        for (int item = 0; item < count; item++)
                            state.ManualOnlyGateGlobalIds[item] = reader.ReadInt32();
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            return state;
        }
    }
}
