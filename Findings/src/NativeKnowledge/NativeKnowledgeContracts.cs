using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace APIShared.NativeKnowledge
{
    /// <summary>Confidence in a meaning, independent of memory layout and runtime acceptance.</summary>
    public enum NativeMeaningConfidence
    {
        /// <summary>A discovery hint, not an established meaning.</summary>
        Candidate,
        /// <summary>A supported inference with remaining semantic uncertainty.</summary>
        Probable,
        /// <summary>The stated, scoped meaning is established by the cited audit.</summary>
        Confirmed,
        /// <summary>A historical interpretation contradicted by evidence.</summary>
        Rejected
    }

    /// <summary>Authority of an evidence item; source declarations do not prove native behavior.</summary>
    public enum NativeEvidenceKind
    {
        /// <summary>A feature-specific native data/control-flow audit.</summary>
        NativeAudit,
        /// <summary>A declaration in a captured Script Extender source file.</summary>
        ExtenderSource,
        /// <summary>A reference in a Knowledge document; not an independent field audit.</summary>
        KnowledgeReference,
        /// <summary>Reconstructed memory or layout observations; not an audited access contract.</summary>
        Reconstruction,
        /// <summary>A scoped gameplay observation, not a universal contract.</summary>
        RuntimeObservation
    }

    /// <summary>Supported scalar encodings. Values are little-endian; no implicit Boolean or enum conversion occurs.</summary>
    public enum NativeScalarType
    {
        /// <summary>No audited scalar encoding is known.</summary>
        Unknown,
        /// <summary>An unsigned byte.</summary>
        UInt8,
        /// <summary>A signed byte.</summary>
        Int8,
        /// <summary>An unsigned 16-bit integer.</summary>
        UInt16,
        /// <summary>A signed 16-bit integer.</summary>
        Int16,
        /// <summary>An unsigned 32-bit integer.</summary>
        UInt32,
        /// <summary>A signed 32-bit integer.</summary>
        Int32,
        /// <summary>An unsigned 64-bit integer.</summary>
        UInt64,
        /// <summary>A signed 64-bit integer.</summary>
        Int64,
        /// <summary>An IEEE single-precision value.</summary>
        Float32,
        /// <summary>An IEEE double-precision value.</summary>
        Float64
    }

    /// <summary>Permission for reading a field whose preferred meaning remains uncertain.</summary>
    public enum NativeFieldReadPolicy
    {
        /// <summary>Require a confirmed preferred meaning as well as a verified storage binding.</summary>
        ConfirmedMeaningOnly,
        /// <summary>Also admit Candidate/Probable meanings. Does not relax any storage or bounds check.</summary>
        AllowUncertainMeaning
    }

    /// <summary>Why a read failed. A failed read always returns the default scalar, never a partial value.</summary>
    public enum NativeFieldReadFailure
    {
        /// <summary>The read succeeded.</summary>
        None,
        /// <summary>A default key or a key from another catalog was supplied.</summary>
        InvalidKey,
        /// <summary>No binding exists for the reader's verified native image hash.</summary>
        UnsupportedImage,
        /// <summary>The binding records knowledge only, not an audited read contract.</summary>
        UnverifiedStorage,
        /// <summary>The view is not the declared record family.</summary>
        WrongRecordType,
        /// <summary>The CLR scalar type differs from the exact raw encoding.</summary>
        WrongScalarType,
        /// <summary>The meaning is uncertain and was not explicitly admitted.</summary>
        UncertainMeaning,
        /// <summary>No non-rejected preferred interpretation exists.</summary>
        RejectedMeaning,
        /// <summary>The complete declared record is not available.</summary>
        RecordTooShort,
        /// <summary>The array element index is outside the catalog's audited extent.</summary>
        ElementOutOfRange,
        /// <summary>The caller supplied an undefined read policy.</summary>
        InvalidPolicy,
        /// <summary>The platform is not little-endian.</summary>
        UnsupportedByteOrder
    }

    /// <summary>Portable evidence coordinates captured at authoring time; no files are read at runtime.</summary>
    public sealed class NativeFieldEvidence
    {
        internal NativeFieldEvidence(NativeEvidenceKind kind, string source, string sha256,
            int firstLine, int lastLine, string summary)
        {
            Kind = kind; Source = source; SourceSha256 = sha256;
            FirstLine = firstLine; LastLine = lastLine; Summary = summary;
        }
        /// <summary>The evidence's authority, not an inferred confidence promotion.</summary>
        public NativeEvidenceKind Kind { get; }
        /// <summary>A portable source identifier relative to the captured baseline or source root.</summary>
        public string Source { get; }
        /// <summary>SHA-256 of the exact captured source bytes.</summary>
        public string SourceSha256 { get; }
        /// <summary>First one-based source line.</summary>
        public int FirstLine { get; }
        /// <summary>Last one-based source line.</summary>
        public int LastLine { get; }
        /// <summary>Scope and limitations of this particular evidence.</summary>
        public string Summary { get; }
    }

    /// <summary>One independently qualified interpretation; several may describe the same storage.</summary>
    public sealed class NativeFieldMeaning
    {
        internal NativeFieldMeaning(string name, NativeMeaningConfidence confidence, string description,
            params NativeFieldEvidence[] evidence)
        {
            Name = name; Confidence = confidence; Description = description;
            Evidence = Freeze(evidence);
        }
        /// <summary>A human-readable interpretation, never the field identity.</summary>
        public string Name { get; }
        /// <summary>Confidence only in the stated interpretation.</summary>
        public NativeMeaningConfidence Confidence { get; }
        /// <summary>Semantic scope, units, ID basis and counter-evidence where relevant.</summary>
        public string Description { get; }
        /// <summary>Immutable supporting and contradicting evidence.</summary>
        public IReadOnlyList<NativeFieldEvidence> Evidence { get; }

        internal static ReadOnlyCollection<T> Freeze<T>(IEnumerable<T> values) =>
            new List<T>(values ?? throw new ArgumentNullException(nameof(values))).AsReadOnly();
    }

    /// <summary>A hash-bound, record-relative layout. Never an absolute address or automatic migration.</summary>
    public sealed class NativeFieldBinding
    {
        internal NativeFieldBinding(string nativeHash, int recordSize, int offset, NativeScalarType scalarType,
            int elementCount, int elementStride, bool readVerified, string limitations,
            params NativeFieldEvidence[] evidence)
        {
            NativeHash = nativeHash; RecordSize = recordSize; Offset = offset; ScalarType = scalarType;
            ElementCount = elementCount; ElementStride = elementStride; ReadVerified = readVerified;
            Limitations = limitations; Evidence = NativeFieldMeaning.Freeze(evidence);
        }
        /// <summary>The exact native image SHA-256; metadata for other hashes is not applicable.</summary>
        public string NativeHash { get; }
        /// <summary>Full extent of the declared view, or zero when not established.</summary>
        public int RecordSize { get; }
        /// <summary>Offset from the view's declared origin, never silently manager-relative.</summary>
        public int Offset { get; }
        /// <summary>Raw encoding; signedness is part of this binding.</summary>
        public NativeScalarType ScalarType { get; }
        /// <summary>Bounded scalar count, or zero when the extent remains unverified.</summary>
        public int ElementCount { get; }
        /// <summary>Byte spacing between array elements, zero for an unknown layout.</summary>
        public int ElementStride { get; }
        /// <summary>Whether the full storage contract has been audited for bounded reads.</summary>
        public bool ReadVerified { get; }
        /// <summary>Remaining limitations, including source-only layouts and historical basis corrections.</summary>
        public string Limitations { get; }
        /// <summary>Immutable binding-specific evidence.</summary>
        public IReadOnlyList<NativeFieldEvidence> Evidence { get; }
    }

    /// <summary>Immutable knowledge about one stable storage identity; names are discovery metadata.</summary>
    public sealed class NativeFieldDescriptor
    {
        internal NativeFieldDescriptor(string id, string recordTypeId, string name, string description,
            string[] aliases, string[] relatedFieldIds, NativeFieldMeaning[] meanings, NativeFieldBinding[] bindings)
        {
            Id = id; RecordTypeId = recordTypeId; Name = name; Description = description;
            Aliases = NativeFieldMeaning.Freeze(aliases); RelatedFieldIds = NativeFieldMeaning.Freeze(relatedFieldIds);
            Meanings = NativeFieldMeaning.Freeze(meanings); Bindings = NativeFieldMeaning.Freeze(bindings);
        }
        /// <summary>Permanent, case-sensitive neutral identifier; never reused for another raw value.</summary>
        public string Id { get; }
        /// <summary>Stable record-family identity. The integration guide defines each view origin.</summary>
        public string RecordTypeId { get; }
        /// <summary>Current display name. May improve without changing Id.</summary>
        public string Name { get; }
        /// <summary>Scope, origin and any historical ambiguity.</summary>
        public string Description { get; }
        /// <summary>Exact-storage discovery aliases. A former wider/narrower field is a relation instead.</summary>
        public IReadOnlyList<string> Aliases { get; }
        /// <summary>Related storage identities; this is not alias equivalence or conversion permission.</summary>
        public IReadOnlyList<string> RelatedFieldIds { get; }
        /// <summary>Preferred interpretation first; rejected historical interpretations remain queryable.</summary>
        public IReadOnlyList<NativeFieldMeaning> Meanings { get; }
        /// <summary>Bindings qualified by exact native image hash.</summary>
        public IReadOnlyList<NativeFieldBinding> Bindings { get; }
    }

    /// <summary>A catalog-issued handle with an exact CLR raw type. Default handles are invalid.</summary>
    /// <typeparam name="T">A supported primitive scalar, not a game struct, enum or Boolean.</typeparam>
    public readonly struct NativeFieldKey<T> where T : struct
    {
        internal NativeFieldKey(NativeFieldDescriptor descriptor) { Descriptor = descriptor; }
        internal NativeFieldDescriptor Descriptor { get; }
        /// <summary>Permanent identity, or null for a default handle.</summary>
        public string Id => Descriptor?.Id;
    }

    /// <summary>A scoped caller-owned byte view, starting exactly at the documented record origin.</summary>
    /// <remarks>No pointer validity, object identity, thread affinity or synchronization is inferred.
    /// Live-memory callers must acquire a valid immediate view on the owning game thread and keep it
    /// valid through the read. A Span backed by native memory can still fault if that promise is false.
    /// Offline callers may supply an independently owned snapshot. No game structure is copied or boxed.</remarks>
    public readonly ref struct NativeRecordView
    {
        /// <summary>Labels caller-owned bytes with their audited record family; does not validate the label.</summary>
        /// <param name="recordTypeId">The exact family from the field descriptor.</param>
        /// <param name="bytes">A complete valid record, not manager-prefix bytes or an arbitrary field slice.</param>
        public NativeRecordView(string recordTypeId, ReadOnlySpan<byte> bytes)
        {
            RecordTypeId = recordTypeId; Bytes = bytes;
        }
        /// <summary>Caller-attested record family.</summary>
        public string RecordTypeId { get; }
        /// <summary>Immediate read-only view; not a retained pointer or a synchronization mechanism.</summary>
        public ReadOnlySpan<byte> Bytes { get; }
    }
}
