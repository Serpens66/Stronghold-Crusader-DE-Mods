// Feature: Versioned save/map payload for manual-only gatehouses.
using MessagePack;
using MessagePack.Formatters;
using System;
using System.Collections.Generic;

namespace ExtraFeatures
{
    public sealed class GatehouseAutomationSaveLimits
    {
        public GatehouseAutomationSaveLimits(int maximumSavedGatehouses)
        {
            if (maximumSavedGatehouses < 1)
                throw new ArgumentOutOfRangeException(nameof(maximumSavedGatehouses));
            MaximumSavedGatehouses = maximumSavedGatehouses;
        }

        public int MaximumSavedGatehouses { get; }
    }

    public static class GatehouseAutomationSaveLimitPolicy
    {
        private const int DefaultMaximumSavedGatehouses = 10000;
        private static readonly object Sync = new object();
        private static readonly Dictionary<string, Func<GatehouseAutomationSaveLimits>> Providers =
            new Dictionary<string, Func<GatehouseAutomationSaveLimits>>(StringComparer.Ordinal);

        // Optional feature mods can advertise their effective capacity without weakening the default limit.
        public static IDisposable Register(string featureId, Func<GatehouseAutomationSaveLimits> provider)
        {
            if (string.IsNullOrWhiteSpace(featureId))
                throw new ArgumentException("A stable feature ID is required.", nameof(featureId));
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));

            lock (Sync)
            {
                if (Providers.ContainsKey(featureId))
                    throw new InvalidOperationException($"A gatehouse save-limit provider is already registered for '{featureId}'.");
                Providers.Add(featureId, provider);
            }
            return new Registration(featureId);
        }

        public static GatehouseAutomationSaveLimits GetCurrent()
        {
            Func<GatehouseAutomationSaveLimits>[] providers;
            lock (Sync)
            {
                providers = new Func<GatehouseAutomationSaveLimits>[Providers.Count];
                Providers.Values.CopyTo(providers, 0);
            }

            int maximumSavedGatehouses = DefaultMaximumSavedGatehouses;
            for (int index = 0; index < providers.Length; index++)
            {
                GatehouseAutomationSaveLimits limits = providers[index]();
                if (limits == null)
                    throw new InvalidOperationException("A gatehouse save-limit provider returned null.");
                maximumSavedGatehouses = Math.Max(maximumSavedGatehouses, limits.MaximumSavedGatehouses);
            }
            return new GatehouseAutomationSaveLimits(maximumSavedGatehouses);
        }

        private sealed class Registration : IDisposable
        {
            private string featureId;

            public Registration(string featureId) => this.featureId = featureId;

            public void Dispose()
            {
                string registeredFeatureId = featureId;
                if (registeredFeatureId == null)
                    return;
                lock (Sync)
                    Providers.Remove(registeredFeatureId);
                featureId = null;
            }
        }
    }

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

            GatehouseAutomationSaveLimits limits = GatehouseAutomationSaveLimitPolicy.GetCurrent();
            if ((value.ManualOnlyGateGlobalIds?.Length ?? 0) > limits.MaximumSavedGatehouses ||
                (value.ManualOnlyGateLocators?.Length ?? 0) > limits.MaximumSavedGatehouses)
            {
                throw new MessagePackSerializationException("Gatehouse automation save state exceeds the registered capacity.");
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

            GatehouseAutomationSaveLimits limits = GatehouseAutomationSaveLimitPolicy.GetCurrent();
            int fieldCount = reader.ReadArrayHeader();
            if (fieldCount != 2 && fieldCount != FieldCount)
                throw new MessagePackSerializationException($"Gatehouse automation save state has {fieldCount} fields; expected 2 or {FieldCount}.");

            var state = new GatehouseAutomationSaveState { Version = reader.ReadInt32() };
            int expectedFieldCount = state.Version == 1 ? 2 :
                state.Version == GatehouseAutomationSaveState.CurrentVersion ? FieldCount : 0;
            if (expectedFieldCount == 0 || fieldCount != expectedFieldCount)
                throw new MessagePackSerializationException("Gatehouse automation save state has an unsupported version or field count.");

            state.ManualOnlyGateGlobalIds = ReadIntArray(
                ref reader, limits.MaximumSavedGatehouses, "global-ID array");
            if (fieldCount == FieldCount)
                state.ManualOnlyGateLocators = ReadLocatorArray(ref reader, options, limits.MaximumSavedGatehouses);
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

        private static int[] ReadIntArray(ref MessagePackReader reader, int maximumLength, string label)
        {
            if (reader.TryReadNil())
                return null;

            int count = reader.ReadArrayHeader();
            if (count > maximumLength)
                throw new MessagePackSerializationException($"Gatehouse automation {label} length {count} exceeds {maximumLength}.");
            int[] values = new int[count];
            for (int index = 0; index < count; index++)
                values[index] = reader.ReadInt32();
            return values;
        }

        private static GatehouseMapLocator[] ReadLocatorArray(
            ref MessagePackReader reader,
            MessagePackSerializerOptions options,
            int maximumLength)
        {
            if (reader.TryReadNil())
                return null;

            int count = reader.ReadArrayHeader();
            if (count > maximumLength)
                throw new MessagePackSerializationException($"Gatehouse automation locator-array length {count} exceeds {maximumLength}.");

            var values = new GatehouseMapLocator[count];
            IMessagePackFormatter<GatehouseMapLocator> formatter =
                options.Resolver.GetFormatterWithVerify<GatehouseMapLocator>();
            for (int index = 0; index < count; index++)
            {
                values[index] = formatter.Deserialize(ref reader, options);
                if (values[index] == null)
                    throw new MessagePackSerializationException("Gatehouse automation locator array contains null.");
            }
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
            if (fieldCount != FieldCount)
                throw new MessagePackSerializationException($"Gatehouse map locator has {fieldCount} fields; expected exactly {FieldCount}.");
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
