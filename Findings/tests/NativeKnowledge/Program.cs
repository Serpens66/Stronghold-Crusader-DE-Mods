using APIShared.NativeKnowledge;
using System;
using System.Collections.Generic;
using System.Linq;

internal static class Program
{
    private static int assertions;
    private static readonly string Hash = NativeFieldCatalog.CapturedNativeHash;
    private static NativeFieldCatalog Catalog => NativeFieldCatalog.Default;

    private static void Main()
    {
        CatalogIntegrity(); RawReads(); Failures(); UncertaintyAndRejection(); Evolution(); Arrays(); ScalarEncodings();
        Console.WriteLine("PASS: " + assertions + " assertions against production catalog/reader. Synthetic buffers only; no gameplay acceptance.");
    }

    private static void Check(bool condition, string reason)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException(reason);
    }

    private static NativeFieldDescriptor Field(string qualifiedName)
    {
        var matches = Catalog.FindByName(qualifiedName);
        Check(matches.Count == 1, "Expected unambiguous discovery: " + qualifiedName);
        return matches[0];
    }

    private static NativeFieldKey<T> Key<T>(NativeFieldCatalog catalog, string id) where T : struct
    {
        Check(catalog.TryGetKey<T>(id, out var key), "Expected exact typed key " + id);
        return key;
    }

    private static void CatalogIntegrity()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in Catalog.Fields)
        {
            Check(ids.Add(field.Id), "Duplicate storage identity");
            Check(Catalog.TryGet(field.Id, out var found) && ReferenceEquals(field, found), "Identity lookup");
            Check(field.Id.StartsWith("nkf-", StringComparison.Ordinal), "Neutral field identity");
            Check(field.Meanings.Count > 0, "Explicit interpretation confidence");
            foreach (var relation in field.RelatedFieldIds) Check(Catalog.TryGet(relation, out _), "Unresolved relationship");
            foreach (var binding in field.Bindings)
            {
                Check(binding.Evidence.Count > 0, "Binding evidence");
                foreach (var evidence in binding.Evidence)
                {
                    Check(NativeScalarEncoding.IsHash(evidence.SourceSha256), "Captured source hash");
                    Check(evidence.FirstLine > 0 && evidence.LastLine >= evidence.FirstLine, "One-based evidence range");
                    Check(!evidence.Source.Contains(":\\"), "Portable provenance");
                }
            }
            Check(field.Aliases is System.Collections.ObjectModel.ReadOnlyCollection<string>, "Immutable aliases");
        }
        Check(!Catalog.TryGet(null, out _), "Null identity");
        Check(Catalog.FindByName(null).Count == 0, "Null search");
        Check(Catalog.FindByName("missing").Count == 0, "Missing search");
        Check(!Catalog.TryGetKey<bool>(Field("GameUnit.DeathMarker").Id, out _), "No implicit Boolean semantics");
        Check(!Catalog.TryGetKey<GameLikeStruct>(Field("GameUnit.DeathMarker").Id, out _), "No struct payloads");
        Check(Catalog.FindByName("CurrentHealthRaw").Count >= 2, "Name ambiguity retained across structures");
        Check(Catalog.FindByName("gameunit.deathmarker").Count == 1, "Case-insensitive discovery");
    }

    private struct GameLikeStruct { }

    private static void RawReads()
    {
        var field = Field("GameUnit.DeathMarker");
        var key = Key<ushort>(Catalog, field.Id);
        var bytes = new byte[0x490];
        bytes[0x29C] = 0x01; bytes[0x29D] = 0x80; bytes[0x29E] = 0xFF; bytes[0x29F] = 0xFF;
        var before = (byte[])bytes.Clone();
        var reader = new NativeFieldReader(Catalog, Hash.ToLowerInvariant());
        Check(reader.TryRead(new NativeRecordView(field.RecordTypeId, bytes), key, out ushort value, out var failure)
            && value == 0x8001 && failure == NativeFieldReadFailure.None, "Read only low WORD, unsigned and little-endian");
        Check(before.SequenceEqual(bytes), "Reader never changes memory");
        Check(Catalog.FindByName("r_IsKilledByProjectile")[0].Id == key.Id, "Source rename alias identity");
        Check(!Catalog.TryGetKey<uint>(field.Id, out _), "No widening into adjacent unrelated WORD");

        var gold = Field("GamePlayerResources.GoldRaw");
        var player = new byte[0x583C];
        Array.Copy(BitConverter.GetBytes(-123456), 0, player, 0x50C, 4);
        Check(reader.TryRead(new NativeRecordView(gold.RecordTypeId, player), Key<int>(Catalog, gold.Id), out int balance, out _)
            && balance == -123456, "Gold signedness is preserved without clamping");

        var hp = Field("GameBuilding.CurrentHealthRaw");
        var building = new byte[0x32C]; building[0x10C] = 0xFE; building[0x10D] = 0xFF;
        Check(reader.TryRead(new NativeRecordView(hp.RecordTypeId, building), Key<short>(Catalog, hp.Id), out short health, out _)
            && health == -2, "Building current HP signed word");
    }

    private static void Failures()
    {
        var field = Field("GameUnit.DeathMarker");
        var key = Key<ushort>(Catalog, field.Id);
        var reader = new NativeFieldReader(Catalog, Hash);
        var record = new NativeRecordView(field.RecordTypeId, new byte[0x490]);
        Check(!reader.TryRead(record, default(NativeFieldKey<ushort>), out ushort value, out var failure)
            && failure == NativeFieldReadFailure.InvalidKey && value == 0, "Default key fails cleanly");
        Check(!reader.TryRead(new NativeRecordView(NativeRecordTypes.GameBuilding, record.Bytes), key, out value, out failure)
            && failure == NativeFieldReadFailure.WrongRecordType, "Reject wrong record even when enough bytes are present");
        Check(!reader.TryRead(new NativeRecordView(field.RecordTypeId, new byte[0x29E]), key, out value, out failure)
            && failure == NativeFieldReadFailure.RecordTooShort, "Complete record contract, not just field end");
        Check(!reader.TryRead(record, key, out value, out failure, (NativeFieldReadPolicy)999)
            && failure == NativeFieldReadFailure.InvalidPolicy, "Unknown policy does not opt in");
        var unknown = new NativeFieldReader(Catalog, new string('0', 64));
        Check(!unknown.TryRead(record, key, out value, out failure, NativeFieldReadPolicy.AllowUncertainMeaning)
            && failure == NativeFieldReadFailure.UnsupportedImage, "Opt-in cannot bypass unknown image");
        var foreign = Fixture("Foreign", NativeMeaningConfidence.Confirmed);
        Check(!reader.TryRead(record, Key<ushort>(foreign, "fixture"), out value, out failure)
            && failure == NativeFieldReadFailure.InvalidKey, "Foreign catalog key rejected even with valid scalar");
        bool threw = false;
        try { _ = new NativeFieldReader(Catalog, "2.14.3"); } catch (ArgumentException) { threw = true; }
        Check(threw, "Version text is not an image identity");
    }

    private static void UncertaintyAndRejection()
    {
        var field = Field("GameUnit.CaptureEligibilityWord");
        var key = Key<ushort>(Catalog, field.Id);
        var bytes = new byte[0x490]; bytes[0x2A0] = 3;
        var record = new NativeRecordView(field.RecordTypeId, bytes);
        var reader = new NativeFieldReader(Catalog, Hash);
        Check(!reader.TryRead(record, key, out ushort value, out var failure)
            && failure == NativeFieldReadFailure.UncertainMeaning && value == 0, "Default denies uncertain semantics");
        Check(reader.TryRead(record, key, out value, out failure, NativeFieldReadPolicy.AllowUncertainMeaning)
            && value == 3, "Explicit opt-in allows verified raw storage");
        var sourceOnly = Field("GamePlayerResources.r_AILastStandPhase");
        Check(!reader.TryRead(new NativeRecordView(sourceOnly.RecordTypeId, new byte[0x583C]),
            Key<uint>(Catalog, sourceOnly.Id), out uint phase, out failure, NativeFieldReadPolicy.AllowUncertainMeaning)
            && failure == NativeFieldReadFailure.UnverifiedStorage && phase == 0, "Source-only layout stays blocked with opt-in");
        var rejected = Fixture("WrongMeaning", NativeMeaningConfidence.Rejected);
        var rejectedReader = new NativeFieldReader(rejected, Hash);
        Check(!rejectedReader.TryRead(new NativeRecordView("fixture-record", new byte[8]), Key<ushort>(rejected,"fixture"),
            out value, out failure, NativeFieldReadPolicy.AllowUncertainMeaning)
            && failure == NativeFieldReadFailure.RejectedMeaning, "Opt-in never revives a rejected interpretation");
        var death = Field("GameUnit.DeathMarker");
        Check(death.Meanings.Any(m => m.Confidence == NativeMeaningConfidence.Rejected), "Counter-evidence remains visible");

        var wide = Field("GameTribe.r_LastRangedAttackerGlobalId1");
        Check(wide.RelatedFieldIds.Count >= 2, "New wide field relates to former halves");
        Check(!wide.Aliases.Any(a => a.StartsWith("2.14.2:", StringComparison.Ordinal)), "Former halfwords are not exact aliases");
        Check(!Catalog.TryGetKey<ushort>(wide.Id, out _), "New UInt32 identity never narrows to old upper half");
    }

    // Same compiled public call path consumes both snapshots; renaming/promotion must not
    // change the storage selected by a permanent ID. No source-symbol substitution occurs.
    private static ushort Consumer(NativeFieldCatalog catalog, NativeFieldReader reader)
    {
        var key = Key<ushort>(catalog, "fixture");
        var bytes = new byte[8]; bytes[2] = 0x34; bytes[3] = 0x12;
        Check(reader.TryRead(new NativeRecordView("fixture-record", bytes), key, out ushort value, out _,
            NativeFieldReadPolicy.AllowUncertainMeaning), "Unchanged compiled consumer read");
        return value;
    }

    private static void Evolution()
    {
        var oldCatalog = Fixture("OldHypothesis", NativeMeaningConfidence.Probable);
        var newCatalog = Fixture("ImprovedName", NativeMeaningConfidence.Confirmed, "OldHypothesis");
        Check(Consumer(oldCatalog, new NativeFieldReader(oldCatalog,Hash)) == Consumer(newCatalog,new NativeFieldReader(newCatalog,Hash)),
            "Renaming and confirmation preserve raw behavior");
        Check(newCatalog.FindByName("OldHypothesis")[0].Id == "fixture", "Old exact-storage alias retained");
        var binding = new NativeFieldBinding(Hash, 8, 7, NativeScalarType.UInt16, 1, 2, true, "invalid", Evidence());
        bool threw = false;
        try { _ = new NativeFieldCatalog(new[] { Descriptor("Overflow",NativeMeaningConfidence.Confirmed,binding) }); }
        catch (ArgumentException) { threw = true; }
        Check(threw, "Invalid authoring contract is rejected before any read");
        threw = false;
        try { _ = new NativeFieldCatalog(new[] { oldCatalog.Fields[0], oldCatalog.Fields[0] }); }
        catch (ArgumentException) { threw = true; }
        Check(threw, "Duplicate identity cannot override storage");
    }

    private static void Arrays()
    {
        var field = Field("GamePlayerResources.RaidCandidateBuildingIds");
        var key = Key<ushort>(Catalog,field.Id);
        var bytes = new byte[0x583C];
        bytes[0x35BC] = 42; bytes[0x35BC+99*2] = 91;
        var reader = new NativeFieldReader(Catalog,Hash);
        var record = new NativeRecordView(field.RecordTypeId,bytes);
        Check(reader.TryRead(record,key,out ushort first,out _,elementIndex:0) && first == 42, "Public-pointer corrected array base");
        Check(reader.TryRead(record,key,out ushort last,out _,elementIndex:99) && last == 91, "Last bounded element");
        foreach (int index in new[] { -1,100,int.MaxValue })
            Check(!reader.TryRead(record,key,out ushort value,out var failure,elementIndex:index)
                && value == 0 && failure == NativeFieldReadFailure.ElementOutOfRange, "No index wrap or array spill");
    }

    private static void ScalarEncodings()
    {
        Scalar<byte>(NativeScalarType.UInt8, 0xFF);
        Scalar<sbyte>(NativeScalarType.Int8, -1);
        Scalar<ushort>(NativeScalarType.UInt16, 0xFFFF);
        Scalar<short>(NativeScalarType.Int16, -1);
        Scalar<uint>(NativeScalarType.UInt32, uint.MaxValue);
        Scalar<int>(NativeScalarType.Int32, -1);
        Scalar<ulong>(NativeScalarType.UInt64, ulong.MaxValue);
        Scalar<long>(NativeScalarType.Int64, -1);
        Scalar<float>(NativeScalarType.Float32, float.NaN);
        Scalar<double>(NativeScalarType.Float64, double.NaN);
    }

    private static void Scalar<T>(NativeScalarType raw, T expected) where T:struct
    {
        var binding = new NativeFieldBinding(Hash,16,0,raw,1,NativeScalarEncoding.Width(raw),true,"fixture",Evidence());
        var catalog = new NativeFieldCatalog(new[] { Descriptor("Scalar",NativeMeaningConfidence.Confirmed,binding) });
        var bytes = Enumerable.Repeat((byte)0xFF,16).ToArray();
        Check(new NativeFieldReader(catalog,Hash).TryRead(new NativeRecordView("fixture-record",bytes),Key<T>(catalog,"fixture"),
            out T value,out _) && EqualityComparer<T>.Default.Equals(value,expected),"Exact raw encoding " + typeof(T).Name);
    }

    private static NativeFieldEvidence Evidence() => new NativeFieldEvidence(NativeEvidenceKind.NativeAudit,"test-spec",Hash,1,1,"Synthetic contract, never game evidence.");
    private static NativeFieldDescriptor Descriptor(string name,NativeMeaningConfidence confidence,NativeFieldBinding binding,params string[] aliases) =>
        new NativeFieldDescriptor("fixture","fixture-record",name,"synthetic",aliases,Array.Empty<string>(),
            new[] { new NativeFieldMeaning(name,confidence,"synthetic",Evidence()) },new[] { binding });
    private static NativeFieldCatalog Fixture(string name,NativeMeaningConfidence confidence,params string[] aliases) =>
        new NativeFieldCatalog(new[] { Descriptor(name,confidence,new NativeFieldBinding(Hash,8,2,NativeScalarType.UInt16,1,2,true,"synthetic",Evidence()),aliases) });
}
