using System;
using System.Runtime.InteropServices;

namespace APIShared.NativeKnowledge
{
    /// <summary>Bounded raw reads against one host-verified native image and one immutable catalog.</summary>
    /// <remarks>The host supplies the hash computed from the actual image using its normal initialization.
    /// This class neither loads nor hashes game files, finds managers, validates native pointers, nor
    /// subscribes to callbacks. It is safe across startup cleanup because no Unity component owns it.
    /// Catalog access is thread-safe; live-memory views require the caller's owning-thread/lifetime contract.</remarks>
    public sealed class NativeFieldReader
    {
        private readonly NativeFieldCatalog catalog;

        /// <summary>Binds the immutable reader to the host's independently verified image identity.</summary>
        /// <param name="catalog">The catalog that issues this reader's keys.</param>
        /// <param name="verifiedNativeHash">Actual image SHA-256, not a catalog constant copied as a claim of support.</param>
        /// <exception cref="ArgumentNullException">The catalog is null.</exception>
        /// <exception cref="ArgumentException">The hash is not a SHA-256 hexadecimal string.</exception>
        public NativeFieldReader(NativeFieldCatalog catalog, string verifiedNativeHash)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            if (!NativeScalarEncoding.IsHash(verifiedNativeHash)) throw new ArgumentException("Expected verified SHA-256.", nameof(verifiedNativeHash));
            NativeHash = verifiedNativeHash.ToUpperInvariant();
        }

        /// <summary>Host-attested identity; unknown but well-formed hashes are accepted and reads fail closed.</summary>
        public string NativeHash { get; }

        /// <summary>Reads one exact little-endian scalar without modifying, retaining or interpreting the record.</summary>
        /// <typeparam name="T">Exact primitive raw encoding from the catalog key.</typeparam>
        /// <param name="record">Valid complete caller-owned record view.</param>
        /// <param name="key">A handle issued by this reader's catalog.</param>
        /// <param name="value">Raw value on success; default on every failure.</param>
        /// <param name="failure">Specific failure reason; None on success.</param>
        /// <param name="policy">Uncertain semantics require an explicit opt-in on each call.</param>
        /// <param name="elementIndex">Zero-based element within the field's audited array, not a game ID.</param>
        /// <returns>Whether all catalog, policy and storage checks succeeded.</returns>
        public bool TryRead<T>(NativeRecordView record, NativeFieldKey<T> key, out T value,
            out NativeFieldReadFailure failure, NativeFieldReadPolicy policy = NativeFieldReadPolicy.ConfirmedMeaningOnly,
            int elementIndex = 0) where T : struct
        {
            value = default;
            failure = NativeFieldReadFailure.InvalidKey;
            var field = key.Descriptor;
            if (!catalog.Owns(field)) return false;
            if (policy != NativeFieldReadPolicy.ConfirmedMeaningOnly && policy != NativeFieldReadPolicy.AllowUncertainMeaning)
                return Fail(NativeFieldReadFailure.InvalidPolicy, out failure);
            if (!BitConverter.IsLittleEndian) return Fail(NativeFieldReadFailure.UnsupportedByteOrder, out failure);
            if (!string.Equals(record.RecordTypeId, field.RecordTypeId, StringComparison.Ordinal))
                return Fail(NativeFieldReadFailure.WrongRecordType, out failure);

            NativeFieldBinding selected = null;
            for (int index = 0; index < field.Bindings.Count; index++)
            {
                NativeFieldBinding binding = field.Bindings[index];
                if (string.Equals(binding.NativeHash, NativeHash, StringComparison.OrdinalIgnoreCase)) { selected = binding; break; }
            }
            if (selected == null) return Fail(NativeFieldReadFailure.UnsupportedImage, out failure);
            if (!selected.ReadVerified) return Fail(NativeFieldReadFailure.UnverifiedStorage, out failure);
            if (selected.ScalarType != NativeScalarEncoding.ForType(typeof(T)))
                return Fail(NativeFieldReadFailure.WrongScalarType, out failure);
            NativeMeaningConfidence confidence = field.Meanings[0].Confidence;
            if (confidence == NativeMeaningConfidence.Rejected) return Fail(NativeFieldReadFailure.RejectedMeaning, out failure);
            if (confidence != NativeMeaningConfidence.Confirmed && policy != NativeFieldReadPolicy.AllowUncertainMeaning)
                return Fail(NativeFieldReadFailure.UncertainMeaning, out failure);
            if (record.Bytes.Length < selected.RecordSize) return Fail(NativeFieldReadFailure.RecordTooShort, out failure);
            if (elementIndex < 0 || elementIndex >= selected.ElementCount)
                return Fail(NativeFieldReadFailure.ElementOutOfRange, out failure);

            // Catalog validation proves the full extent fits RecordSize. Use wide arithmetic anyway:
            // large externally requested element indices must never wrap into another field.
            long offset = (long)selected.Offset + (long)elementIndex * selected.ElementStride;
            int width = NativeScalarEncoding.Width(selected.ScalarType);
            if (offset < 0 || offset > record.Bytes.Length - width)
                return Fail(NativeFieldReadFailure.RecordTooShort, out failure);
            value = MemoryMarshal.Read<T>(record.Bytes.Slice((int)offset, width));
            failure = NativeFieldReadFailure.None;
            return true;
        }

        private static bool Fail(NativeFieldReadFailure reason, out NativeFieldReadFailure failure)
        {
            failure = reason; return false;
        }
    }
}
