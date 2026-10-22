using System;
using System.Collections.Generic;

namespace APIShared.NativeKnowledge
{
    /// <summary>Immutable process-independent field knowledge. Catalog queries never access the game.</summary>
    public sealed partial class NativeFieldCatalog
    {
        private readonly Dictionary<string, NativeFieldDescriptor> byId;
        private readonly Dictionary<string, List<NativeFieldDescriptor>> byName;

        internal NativeFieldCatalog(IEnumerable<NativeFieldDescriptor> fields)
        {
            byId = new Dictionary<string, NativeFieldDescriptor>(StringComparer.Ordinal);
            byName = new Dictionary<string, List<NativeFieldDescriptor>>(StringComparer.OrdinalIgnoreCase);
            var ordered = new List<NativeFieldDescriptor>();
            foreach (NativeFieldDescriptor field in fields)
            {
                Validate(field);
                byId.Add(field.Id, field); ordered.Add(field);
                AddName(field.Name, field);
                AddName(field.RecordTypeId + "." + field.Name, field);
                foreach (string alias in field.Aliases) AddName(alias, field);
                foreach (NativeFieldMeaning meaning in field.Meanings) AddName(meaning.Name, field);
            }
            foreach (NativeFieldDescriptor field in ordered)
                foreach (string id in field.RelatedFieldIds)
                    if (!byId.ContainsKey(id) || id == field.Id)
                        throw new ArgumentException("Unresolved/self-related field identity: " + id);
            Fields = ordered.AsReadOnly();
        }

        /// <summary>Captured built-in knowledge. Publication is CLR-static, without plugin callbacks or mutable registrations.</summary>
        public static NativeFieldCatalog Default { get; } = new NativeFieldCatalog(CreateFields());

        /// <summary>All entries, including unverified and rejected knowledge. Order has no semantic meaning.</summary>
        public IReadOnlyList<NativeFieldDescriptor> Fields { get; }

        /// <summary>Looks up a permanent identity using ordinal comparison. Null/unknown IDs return false.</summary>
        /// <param name="id">Permanent catalog identity, not an offset or display name.</param>
        /// <param name="field">Entry on success, otherwise null.</param>
        /// <returns>Whether the identity exists.</returns>
        public bool TryGet(string id, out NativeFieldDescriptor field)
        {
            field = null;
            return id != null && byId.TryGetValue(id, out field);
        }

        /// <summary>Exact case-insensitive discovery by current, historical or proposed name. Ambiguity is preserved.</summary>
        /// <param name="name">A name, optionally qualified by its recorded source type.</param>
        /// <returns>An independent read-only result; never an arbitrarily chosen first match.</returns>
        public IReadOnlyList<NativeFieldDescriptor> FindByName(string name) =>
            name != null && byName.TryGetValue(name, out var matches)
                ? NativeFieldMeaning.Freeze(matches)
                : NativeFieldMeaning.Freeze(Array.Empty<NativeFieldDescriptor>());

        /// <summary>Creates an exact scalar handle. This does not grant support for the running image or uncertain meanings.</summary>
        /// <typeparam name="T">An exact supported primitive encoding.</typeparam>
        /// <param name="id">Permanent identity.</param>
        /// <param name="key">Handle on success, otherwise default.</param>
        /// <returns>False if absent, not scalar, or no binding has this CLR encoding.</returns>
        public bool TryGetKey<T>(string id, out NativeFieldKey<T> key) where T : struct
        {
            key = default;
            NativeScalarType scalar = NativeScalarEncoding.ForType(typeof(T));
            if (scalar == NativeScalarType.Unknown || !TryGet(id, out var field)) return false;
            foreach (NativeFieldBinding binding in field.Bindings)
                if (binding.ScalarType == scalar)
                {
                    key = new NativeFieldKey<T>(field); return true;
                }
            return false;
        }

        internal bool Owns(NativeFieldDescriptor field) => field != null &&
            byId.TryGetValue(field.Id, out var found) && ReferenceEquals(field, found);

        private void AddName(string name, NativeFieldDescriptor field)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Empty field name/alias.");
            if (!byName.TryGetValue(name, out var entries)) byName.Add(name, entries = new List<NativeFieldDescriptor>());
            if (!entries.Contains(field)) entries.Add(field);
        }

        private static void Validate(NativeFieldDescriptor field)
        {
            if (field == null || string.IsNullOrWhiteSpace(field.Id) || string.IsNullOrWhiteSpace(field.RecordTypeId)
                || string.IsNullOrWhiteSpace(field.Name) || field.Meanings.Count == 0)
                throw new ArgumentException("Incomplete field descriptor.");
            var hashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (NativeFieldBinding binding in field.Bindings)
            {
                if (!NativeScalarEncoding.IsHash(binding.NativeHash) || !hashes.Add(binding.NativeHash))
                    throw new ArgumentException("Invalid/duplicate binding hash: " + field.Id);
                if (!binding.ReadVerified) continue;
                int width = NativeScalarEncoding.Width(binding.ScalarType);
                long end = (long)binding.Offset + (long)(binding.ElementCount - 1) * binding.ElementStride + width;
                if (binding.RecordSize <= 0 || binding.Offset < 0 || width == 0 || binding.ElementCount <= 0
                    || binding.ElementStride < width || end > binding.RecordSize || binding.Evidence.Count == 0)
                    throw new ArgumentException("Invalid verified storage contract: " + field.Id);
            }
        }
    }

    internal static class NativeScalarEncoding
    {
        internal static NativeScalarType ForType(Type type)
        {
            if (type == typeof(byte)) return NativeScalarType.UInt8;
            if (type == typeof(sbyte)) return NativeScalarType.Int8;
            if (type == typeof(ushort)) return NativeScalarType.UInt16;
            if (type == typeof(short)) return NativeScalarType.Int16;
            if (type == typeof(uint)) return NativeScalarType.UInt32;
            if (type == typeof(int)) return NativeScalarType.Int32;
            if (type == typeof(ulong)) return NativeScalarType.UInt64;
            if (type == typeof(long)) return NativeScalarType.Int64;
            if (type == typeof(float)) return NativeScalarType.Float32;
            if (type == typeof(double)) return NativeScalarType.Float64;
            return NativeScalarType.Unknown;
        }

        internal static int Width(NativeScalarType scalar)
        {
            switch (scalar)
            {
                case NativeScalarType.UInt8: case NativeScalarType.Int8: return 1;
                case NativeScalarType.UInt16: case NativeScalarType.Int16: return 2;
                case NativeScalarType.UInt32: case NativeScalarType.Int32: case NativeScalarType.Float32: return 4;
                case NativeScalarType.UInt64: case NativeScalarType.Int64: case NativeScalarType.Float64: return 8;
                default: return 0;
            }
        }

        internal static bool IsHash(string hash)
        {
            if (hash == null || hash.Length != 64) return false;
            foreach (char character in hash)
                if (!(character >= '0' && character <= '9') && !(character >= 'A' && character <= 'F')
                    && !(character >= 'a' && character <= 'f')) return false;
            return true;
        }
    }
}
