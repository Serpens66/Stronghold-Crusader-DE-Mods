// Feature: Versioned save/map payload for manual-only gatehouses.
using MessagePack;
using MessagePack.Formatters;

namespace ExtraFeatures
{
    [MessagePackObject]
    [MessagePackFormatter(typeof(GatehouseAutomationSaveStateFormatter))]
    internal sealed class GatehouseAutomationSaveState
    {
        public const int CurrentVersion = 2;

        [Key(0)] public int Version;
        [Key(1)] public int[] ManualOnlyGateGlobalIds;
        [Key(2)] public GatehouseMapLocator[] ManualOnlyGateLocators;
    }

    [MessagePackObject]
    [MessagePackFormatter(typeof(GatehouseMapLocatorFormatter))]
    internal sealed class GatehouseMapLocator
    {
        [Key(0)] public int OwnerPlayerId;
        [Key(1)] public int BuildingType;
        [Key(2)] public int TileXBegin;
        [Key(3)] public int TileYBegin;
        [Key(4)] public int TileXEnd;
        [Key(5)] public int TileYEnd;

        internal bool HasValidShape =>
            OwnerPlayerId >= 1 && OwnerPlayerId <= 8 && BuildingType > 0 &&
            TileXBegin >= 0 && TileYBegin >= 0 &&
            TileXEnd >= TileXBegin && TileYEnd >= TileYBegin;

        internal string IdentityKey =>
            $"{OwnerPlayerId}:{BuildingType}:{TileXBegin},{TileYBegin}-{TileXEnd},{TileYEnd}";
    }

    internal sealed class GatehouseAutomationSaveStateFormatter : IMessagePackFormatter<GatehouseAutomationSaveState>
    {
        private const int FieldCount = 3;

        public void Serialize(ref MessagePackWriter writer, GatehouseAutomationSaveState value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteArrayHeader(FieldCount);
            writer.Write(value.Version);
            WriteIntArray(ref writer, value.ManualOnlyGateGlobalIds);

            GatehouseMapLocator[] locators = value.ManualOnlyGateLocators;
            if (locators == null)
            {
                writer.WriteNil();
            }
            else
            {
                writer.WriteArrayHeader(locators.Length);
                IMessagePackFormatter<GatehouseMapLocator> formatter = options.Resolver.GetFormatterWithVerify<GatehouseMapLocator>();
                for (int index = 0; index < locators.Length; index++)
                    formatter.Serialize(ref writer, locators[index], options);
            }
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
                        state.ManualOnlyGateGlobalIds = ReadIntArray(ref reader);
                        break;
                    case 2:
                        if (reader.TryReadNil())
                        {
                            state.ManualOnlyGateLocators = null;
                            break;
                        }

                        int locatorCount = reader.ReadArrayHeader();
                        state.ManualOnlyGateLocators = new GatehouseMapLocator[locatorCount];
                        IMessagePackFormatter<GatehouseMapLocator> locatorFormatter = options.Resolver.GetFormatterWithVerify<GatehouseMapLocator>();
                        for (int item = 0; item < locatorCount; item++)
                            state.ManualOnlyGateLocators[item] = locatorFormatter.Deserialize(ref reader, options);
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            return state;
        }

        private static void WriteIntArray(ref MessagePackWriter writer, int[] values)
        {
            if (values == null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteArrayHeader(values.Length);
            for (int index = 0; index < values.Length; index++)
                writer.Write(values[index]);
        }

        private static int[] ReadIntArray(ref MessagePackReader reader)
        {
            if (reader.TryReadNil())
                return null;

            int count = reader.ReadArrayHeader();
            int[] values = new int[count];
            for (int index = 0; index < count; index++)
                values[index] = reader.ReadInt32();
            return values;
        }
    }

    internal sealed class GatehouseMapLocatorFormatter : IMessagePackFormatter<GatehouseMapLocator>
    {
        private const int FieldCount = 6;

        public void Serialize(ref MessagePackWriter writer, GatehouseMapLocator value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteArrayHeader(FieldCount);
            writer.Write(value.OwnerPlayerId);
            writer.Write(value.BuildingType);
            writer.Write(value.TileXBegin);
            writer.Write(value.TileYBegin);
            writer.Write(value.TileXEnd);
            writer.Write(value.TileYEnd);
        }

        public GatehouseMapLocator Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return null;

            int fieldCount = reader.ReadArrayHeader();
            var locator = new GatehouseMapLocator();
            for (int index = 0; index < fieldCount; index++)
            {
                switch (index)
                {
                    case 0: locator.OwnerPlayerId = reader.ReadInt32(); break;
                    case 1: locator.BuildingType = reader.ReadInt32(); break;
                    case 2: locator.TileXBegin = reader.ReadInt32(); break;
                    case 3: locator.TileYBegin = reader.ReadInt32(); break;
                    case 4: locator.TileXEnd = reader.ReadInt32(); break;
                    case 5: locator.TileYEnd = reader.ReadInt32(); break;
                    default: reader.Skip(); break;
                }
            }
            return locator;
        }
    }
}
