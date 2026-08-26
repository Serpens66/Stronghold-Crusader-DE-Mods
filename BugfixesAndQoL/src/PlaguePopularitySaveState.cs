using MessagePack;
using MessagePack.Formatters;
using System;
using System.Collections.Generic;

namespace BugfixesAndQoL
{
    public sealed class PlaguePopularitySaveLimits
    {
        public PlaguePopularitySaveLimits(
            int maximumManagedPlayers,
            int maximumHerds,
            int maximumProjectilesPerHerd,
            int maximumTotalProjectiles,
            int maximumProjectileSlotId)
        {
            if (maximumManagedPlayers < 1) throw new ArgumentOutOfRangeException(nameof(maximumManagedPlayers));
            if (maximumHerds < 1) throw new ArgumentOutOfRangeException(nameof(maximumHerds));
            if (maximumProjectilesPerHerd < 1) throw new ArgumentOutOfRangeException(nameof(maximumProjectilesPerHerd));
            if (maximumTotalProjectiles < 1) throw new ArgumentOutOfRangeException(nameof(maximumTotalProjectiles));
            if (maximumProjectileSlotId < 1) throw new ArgumentOutOfRangeException(nameof(maximumProjectileSlotId));

            MaximumManagedPlayers = maximumManagedPlayers;
            MaximumHerds = maximumHerds;
            MaximumProjectilesPerHerd = maximumProjectilesPerHerd;
            MaximumTotalProjectiles = maximumTotalProjectiles;
            MaximumProjectileSlotId = maximumProjectileSlotId;
        }

        public int MaximumManagedPlayers { get; }
        public int MaximumHerds { get; }
        public int MaximumProjectilesPerHerd { get; }
        public int MaximumTotalProjectiles { get; }
        public int MaximumProjectileSlotId { get; }
    }

    public static class PlaguePopularitySaveLimitPolicy
    {
        private static readonly PlaguePopularitySaveLimits Defaults =
            new PlaguePopularitySaveLimits(8, 4096, 10, 10000, 10000);
        private static readonly object Sync = new object();
        private static readonly Dictionary<string, Func<PlaguePopularitySaveLimits>> Providers =
            new Dictionary<string, Func<PlaguePopularitySaveLimits>>(StringComparer.Ordinal);

        // Providers report total supported capacities. Their maxima are merged with the safe defaults.
        public static IDisposable Register(string featureId, Func<PlaguePopularitySaveLimits> provider)
        {
            if (string.IsNullOrWhiteSpace(featureId))
                throw new ArgumentException("A stable feature ID is required.", nameof(featureId));
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));

            lock (Sync)
            {
                if (Providers.ContainsKey(featureId))
                    throw new InvalidOperationException($"A plague save-limit provider is already registered for '{featureId}'.");
                Providers.Add(featureId, provider);
            }
            return new Registration(featureId);
        }

        public static PlaguePopularitySaveLimits GetCurrent()
        {
            Func<PlaguePopularitySaveLimits>[] providers;
            lock (Sync)
            {
                providers = new Func<PlaguePopularitySaveLimits>[Providers.Count];
                Providers.Values.CopyTo(providers, 0);
            }

            int maximumManagedPlayers = Defaults.MaximumManagedPlayers;
            int maximumHerds = Defaults.MaximumHerds;
            int maximumProjectilesPerHerd = Defaults.MaximumProjectilesPerHerd;
            int maximumTotalProjectiles = Defaults.MaximumTotalProjectiles;
            int maximumProjectileSlotId = Defaults.MaximumProjectileSlotId;
            for (int index = 0; index < providers.Length; index++)
            {
                PlaguePopularitySaveLimits limits = providers[index]();
                if (limits == null)
                    throw new InvalidOperationException("A plague save-limit provider returned null.");
                maximumManagedPlayers = Math.Max(maximumManagedPlayers, limits.MaximumManagedPlayers);
                maximumHerds = Math.Max(maximumHerds, limits.MaximumHerds);
                maximumProjectilesPerHerd = Math.Max(maximumProjectilesPerHerd, limits.MaximumProjectilesPerHerd);
                maximumTotalProjectiles = Math.Max(maximumTotalProjectiles, limits.MaximumTotalProjectiles);
                maximumProjectileSlotId = Math.Max(maximumProjectileSlotId, limits.MaximumProjectileSlotId);
            }
            return new PlaguePopularitySaveLimits(
                maximumManagedPlayers,
                maximumHerds,
                maximumProjectilesPerHerd,
                maximumTotalProjectiles,
                maximumProjectileSlotId);
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
    [MessagePackFormatter(typeof(PlaguePopularitySaveStateFormatter))]
    public sealed class PlaguePopularitySaveState
    {
        public const int CurrentVersion = 1;

        [Key(0)] public int Version = CurrentVersion;
        [Key(1)] public int[] ManagedPlayerIds = Array.Empty<int>();
        [Key(2)] public PlagueHerdSaveRecord[] Herds = Array.Empty<PlagueHerdSaveRecord>();
    }

    [MessagePackObject]
    [MessagePackFormatter(typeof(PlagueHerdSaveRecordFormatter))]
    public sealed class PlagueHerdSaveRecord
    {
        [Key(0)] public int PlayerId;
        [Key(1)] public int[] ProjectileSlotIds = Array.Empty<int>();
        [Key(2)] public uint[] ProjectileGlobalIds = Array.Empty<uint>();
    }

    public sealed class PlaguePopularitySaveStateFormatter : IMessagePackFormatter<PlaguePopularitySaveState>
    {
        private const int FieldCount = 3;
        private static readonly PlagueHerdSaveRecordFormatter HerdFormatter =
            new PlagueHerdSaveRecordFormatter();

        public void Serialize(ref MessagePackWriter writer, PlaguePopularitySaveState value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            PlaguePopularitySaveLimits limits = PlaguePopularitySaveLimitPolicy.GetCurrent();
            ValidateState(value, limits);
            writer.WriteArrayHeader(FieldCount);
            writer.Write(value.Version);
            WriteIntArray(ref writer, value.ManagedPlayerIds);
            PlagueHerdSaveRecord[] herds = value.Herds ?? Array.Empty<PlagueHerdSaveRecord>();
            writer.WriteArrayHeader(herds.Length);
            for (int index = 0; index < herds.Length; index++)
                HerdFormatter.Serialize(ref writer, herds[index], options, limits);
        }

        public PlaguePopularitySaveState Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return null;

            RequireFieldCount(ref reader, FieldCount, "plague save state");
            PlaguePopularitySaveLimits limits = PlaguePopularitySaveLimitPolicy.GetCurrent();
            var value = new PlaguePopularitySaveState
            {
                Version = reader.ReadInt32(),
                ManagedPlayerIds = ReadIntArray(ref reader, limits.MaximumManagedPlayers, "managed-player array")
            };
            if (value.Version != PlaguePopularitySaveState.CurrentVersion)
                throw new MessagePackSerializationException("Plague save state has an unsupported version.");

            value.Herds = ReadHerdArray(ref reader, options, limits);
            ValidateState(value, limits);
            return value;
        }

        private static void WriteIntArray(ref MessagePackWriter writer, int[] values)
        {
            values = values ?? Array.Empty<int>();
            writer.WriteArrayHeader(values.Length);
            for (int index = 0; index < values.Length; index++)
                writer.Write(values[index]);
        }

        private static int[] ReadIntArray(ref MessagePackReader reader, int maximumLength, string label)
        {
            if (reader.TryReadNil())
                return Array.Empty<int>();
            int length = reader.ReadArrayHeader();
            RequireLength(length, maximumLength, label);
            int[] values = new int[length];
            for (int index = 0; index < length; index++)
                values[index] = reader.ReadInt32();
            return values;
        }

        private static PlagueHerdSaveRecord[] ReadHerdArray(
            ref MessagePackReader reader,
            MessagePackSerializerOptions options,
            PlaguePopularitySaveLimits limits)
        {
            if (reader.TryReadNil())
                return Array.Empty<PlagueHerdSaveRecord>();
            int length = reader.ReadArrayHeader();
            RequireLength(length, limits.MaximumHerds, "herd array");
            PlagueHerdSaveRecord[] values = new PlagueHerdSaveRecord[length];
            int totalProjectiles = 0;
            for (int index = 0; index < length; index++)
                values[index] = HerdFormatter.Deserialize(ref reader, options, limits, ref totalProjectiles);
            return values;
        }

        internal static void RequireFieldCount(ref MessagePackReader reader, int expected, string label)
        {
            int count = reader.ReadArrayHeader();
            if (count != expected)
                throw new MessagePackSerializationException($"{label} has {count} fields; expected exactly {expected}.");
        }

        internal static void RequireLength(int length, int maximumLength, string label)
        {
            if (length > maximumLength)
                throw new MessagePackSerializationException($"Plague {label} length {length} exceeds {maximumLength}.");
        }

        private static void ValidateState(PlaguePopularitySaveState value, PlaguePopularitySaveLimits limits)
        {
            if (value.Version != PlaguePopularitySaveState.CurrentVersion ||
                value.ManagedPlayerIds == null || value.Herds == null)
            {
                throw new MessagePackSerializationException("Plague save state header is invalid.");
            }
            RequireLength(value.ManagedPlayerIds.Length, limits.MaximumManagedPlayers, "managed-player array");
            RequireLength(value.Herds.Length, limits.MaximumHerds, "herd array");

            var players = new HashSet<int>();
            for (int index = 0; index < value.ManagedPlayerIds.Length; index++)
            {
                int playerId = value.ManagedPlayerIds[index];
                if (playerId < 1 || playerId > limits.MaximumManagedPlayers || !players.Add(playerId))
                    throw new MessagePackSerializationException("Plague save state contains an invalid or duplicate managed-player ID.");
            }

            int totalProjectiles = 0;
            var projectileSlots = new HashSet<int>();
            var projectileGlobalIds = new HashSet<uint>();
            for (int herdIndex = 0; herdIndex < value.Herds.Length; herdIndex++)
            {
                PlagueHerdSaveRecord record = value.Herds[herdIndex];
                PlagueHerdSaveRecordFormatter.ValidateRecord(record, limits);
                totalProjectiles = checked(totalProjectiles + record.ProjectileSlotIds.Length);
                if (totalProjectiles > limits.MaximumTotalProjectiles)
                    throw new MessagePackSerializationException("Plague save state exceeds the registered total projectile capacity.");

                for (int projectileIndex = 0; projectileIndex < record.ProjectileSlotIds.Length; projectileIndex++)
                {
                    if (!projectileSlots.Add(record.ProjectileSlotIds[projectileIndex]) ||
                        !projectileGlobalIds.Add(record.ProjectileGlobalIds[projectileIndex]))
                    {
                        throw new MessagePackSerializationException("Plague save state contains a duplicate projectile identity.");
                    }
                }
            }
        }
    }

    public sealed class PlagueHerdSaveRecordFormatter : IMessagePackFormatter<PlagueHerdSaveRecord>
    {
        private const int FieldCount = 3;

        public void Serialize(ref MessagePackWriter writer, PlagueHerdSaveRecord value, MessagePackSerializerOptions options)
        {
            Serialize(ref writer, value, options, PlaguePopularitySaveLimitPolicy.GetCurrent());
        }

        internal void Serialize(
            ref MessagePackWriter writer,
            PlagueHerdSaveRecord value,
            MessagePackSerializerOptions options,
            PlaguePopularitySaveLimits limits)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            ValidateRecord(value, limits);
            writer.WriteArrayHeader(FieldCount);
            writer.Write(value.PlayerId);
            WriteIntArray(ref writer, value.ProjectileSlotIds);
            WriteUIntArray(ref writer, value.ProjectileGlobalIds);
        }

        public PlagueHerdSaveRecord Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return null;

            PlaguePopularitySaveLimits limits = PlaguePopularitySaveLimitPolicy.GetCurrent();
            int totalProjectiles = 0;
            return Deserialize(ref reader, options, limits, ref totalProjectiles);
        }

        internal PlagueHerdSaveRecord Deserialize(
            ref MessagePackReader reader,
            MessagePackSerializerOptions options,
            PlaguePopularitySaveLimits limits,
            ref int totalProjectiles)
        {
            if (reader.TryReadNil())
                throw new MessagePackSerializationException("Plague herd array contains null.");

            PlaguePopularitySaveStateFormatter.RequireFieldCount(ref reader, FieldCount, "plague herd record");
            var value = new PlagueHerdSaveRecord { PlayerId = reader.ReadInt32() };
            int remainingTotal = limits.MaximumTotalProjectiles - totalProjectiles;
            value.ProjectileSlotIds = ReadIntArray(
                ref reader,
                Math.Min(limits.MaximumProjectilesPerHerd, remainingTotal),
                "projectile-slot array");

            int projectedTotal = checked(totalProjectiles + value.ProjectileSlotIds.Length);
            if (projectedTotal > limits.MaximumTotalProjectiles)
                throw new MessagePackSerializationException("Plague save state exceeds the registered total projectile capacity.");

            value.ProjectileGlobalIds = ReadUIntArray(
                ref reader,
                limits.MaximumProjectilesPerHerd,
                value.ProjectileSlotIds.Length,
                "projectile-global-ID array");
            ValidateRecord(value, limits);
            totalProjectiles = projectedTotal;
            return value;
        }

        private static void WriteIntArray(ref MessagePackWriter writer, int[] values)
        {
            values = values ?? Array.Empty<int>();
            writer.WriteArrayHeader(values.Length);
            for (int index = 0; index < values.Length; index++)
                writer.Write(values[index]);
        }

        private static int[] ReadIntArray(ref MessagePackReader reader, int maximumLength, string label)
        {
            if (reader.TryReadNil())
                return Array.Empty<int>();
            int length = reader.ReadArrayHeader();
            PlaguePopularitySaveStateFormatter.RequireLength(length, maximumLength, label);
            int[] values = new int[length];
            for (int index = 0; index < length; index++)
                values[index] = reader.ReadInt32();
            return values;
        }

        private static void WriteUIntArray(ref MessagePackWriter writer, uint[] values)
        {
            values = values ?? Array.Empty<uint>();
            writer.WriteArrayHeader(values.Length);
            for (int index = 0; index < values.Length; index++)
                writer.Write(values[index]);
        }

        private static uint[] ReadUIntArray(
            ref MessagePackReader reader,
            int maximumLength,
            int expectedLength,
            string label)
        {
            if (reader.TryReadNil())
            {
                if (expectedLength != 0)
                    throw new MessagePackSerializationException("Plague projectile identity arrays have different lengths.");
                return Array.Empty<uint>();
            }
            int length = reader.ReadArrayHeader();
            PlaguePopularitySaveStateFormatter.RequireLength(length, maximumLength, label);
            if (length != expectedLength)
                throw new MessagePackSerializationException("Plague projectile identity arrays have different lengths.");
            uint[] values = new uint[length];
            for (int index = 0; index < length; index++)
                values[index] = reader.ReadUInt32();
            return values;
        }

        internal static void ValidateRecord(PlagueHerdSaveRecord value, PlaguePopularitySaveLimits limits)
        {
            if (value == null || value.PlayerId < 1 || value.PlayerId > limits.MaximumManagedPlayers ||
                value.ProjectileSlotIds == null || value.ProjectileGlobalIds == null ||
                value.ProjectileSlotIds.Length != value.ProjectileGlobalIds.Length ||
                value.ProjectileSlotIds.Length < 1)
            {
                throw new MessagePackSerializationException("Plague herd record is invalid.");
            }
            PlaguePopularitySaveStateFormatter.RequireLength(
                value.ProjectileSlotIds.Length, limits.MaximumProjectilesPerHerd, "projectiles-per-herd array");

            var slots = new HashSet<int>();
            var globalIds = new HashSet<uint>();
            for (int index = 0; index < value.ProjectileSlotIds.Length; index++)
            {
                int slotId = value.ProjectileSlotIds[index];
                uint globalId = value.ProjectileGlobalIds[index];
                if (slotId < 1 || slotId > limits.MaximumProjectileSlotId || globalId == 0 ||
                    !slots.Add(slotId) || !globalIds.Add(globalId))
                {
                    throw new MessagePackSerializationException("Plague herd contains an invalid or duplicate projectile identity.");
                }
            }
        }
    }
}
