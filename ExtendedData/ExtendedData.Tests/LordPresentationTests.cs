using ExtendedData;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

internal static class LordPresentationTests
{
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    internal static void Roundtrip()
    {
        const string json = "{\n\"LocalizedDisplayName\":{\"de-DE\":\"Doktor\"},\"LocalizedTitles\":{\"de-DE\":[\", der Weise\"],\"en-US\":[\", the Wise\"]},\"Future\":{\"a\":[1,2,null,true]},\"Messages\":{\"Taunt1\":[{\"LocalizedText\":{\"ja-JP\":\"声\"}}]}}";
        var data = new LordPresentationData { DisplayName = "Doctor", MetadataJson = json, SourceRoot = "C:\\author-only" };
        var slot = new LordDataSlot { PlayerId = 3, LordType = -1, LordName = "123\\Doctor", ConfigName = "aic", ConfigChecksum = "11", Presentation = data };
        var snapshot = LordDataSnapshot.CreateTrail("trail:test", false, new[] { slot });
        Check(!snapshot.WireJson.Contains("author-only"), "author path leaked to network/save");
        Check(LordDataSnapshot.Parse(snapshot.WireJson).Slots.Single().Presentation.MetadataJson == json, "snapshot lost original metadata");
        LordDataSaveRecord.Read(LordDataSaveRecord.Write(snapshot, null), out var saved, out _);
        Check(saved.Slots.Single().Presentation.MetadataJson == json, "save lost original metadata");
        Check(LordPresentationData.FallbackName("123\\Doctor") == "Doctor", "Workshop prefix remains in fallback name");
        string root = TestWorkDirectory.New("lordmeta-data");
        string mission = Path.Combine(root, "01.trail"); File.WriteAllText(mission, "test mission");
        var requirements = TrailLordRequirements.Create(mission, new[] { new TrailLordSlot { PlayerId = 3, LordType = -1,
            LordName = slot.LordName, ConfigName = "aic", ConfigChecksum = "11", AivChecksums = Array.Empty<string>(), Presentation = data } });
        requirements.Write(mission);
        Check(TrailLordRequirements.Read(mission).Slots.Single().Presentation.MetadataJson == json, "requirements lost metadata");
        var maker = TrailLordRequirements.Read(mission).Slots.Single(); maker.PlayerId = 6;
        var coop = ExtendedData.Core.CoopTrailSlotSelection.RemapForCoop(new[] { maker }, new List<int> { 0, 1, 5 }).Single();
        Check(coop.PlayerId == 3 && maker.PlayerId == 6 && coop.Presentation.MetadataJson == json,
            "Coop remapping changed authoring slots or lost full presentation");
        string nextMission = Path.Combine(root, "02.trail"); File.WriteAllText(nextMission, "next mission");
        TrailLordRequirements.Create(nextMission, new[] { coop }).Write(nextMission);
        var next = TrailLordRequirements.Read(nextMission).Slots.Single();
        var nextSnapshot = LordDataSnapshot.CreateTrail("trail:next", false, new[] { new LordDataSlot {
            PlayerId = next.PlayerId, LordType = next.LordType, LordName = next.LordName,
            ConfigName = next.ConfigName, ConfigChecksum = next.ConfigChecksum, Presentation = next.Presentation } });
        LordDataSaveRecord.Read(LordDataSaveRecord.Write(nextSnapshot, null), out var nextSave, out _);
        Check(nextSave.Slots.Single().LordName == slot.LordName && nextSave.Slots.Single().Presentation.MetadataJson == json,
            "mission transition/save lost identity, titles, locale or unknown metadata fields");

        // Missing metadata must stay distinct from an authored empty object: runtime title fallback depends on it.
        string missingSource = Path.Combine(root, "no-meta"); Directory.CreateDirectory(missingSource);
        var missing = TrailLordMediaStore.CaptureOrFallback("123\\Untitled", missingSource, out string problem);
        Check(problem == null && missing.MetadataJson == null, "absent metadata became an authored empty document");
        var missingSnapshot = LordDataSnapshot.CreateTrail("trail:no-meta", false, new[] { new LordDataSlot {
            PlayerId = 4, LordType = -1, LordName = "123\\Untitled", ConfigName = "aic", ConfigChecksum = "22", Presentation = missing } });
        missingSnapshot = LordDataSnapshot.Parse(missingSnapshot.WireJson);
        Check(missingSnapshot.Slots.Single().Presentation.MetadataJson == null, "snapshot synthesized absent metadata");
        for (int pass = 0; pass < 2; pass++)
        {
            LordDataSaveRecord.Read(LordDataSaveRecord.Write(missingSnapshot, null), out missingSnapshot, out _);
            Check(missingSnapshot.Slots.Single().Presentation.MetadataJson == null &&
                missingSnapshot.Slots.Single().Presentation.DisplayName == "Untitled", "repeated save synthesized metadata or lost the authored name");
        }
        string missingMission = Path.Combine(root, "03.trail"); File.WriteAllText(missingMission, "mission without metadata");
        TrailLordRequirements.Create(missingMission, new[] { new TrailLordSlot { PlayerId = 4, LordType = -1,
            LordName = "123\\Untitled", ConfigName = "aic", ConfigChecksum = "22", AivChecksums = Array.Empty<string>(),
            Presentation = missingSnapshot.Slots.Single().Presentation } }).Write(missingMission);
        Check(TrailLordRequirements.Read(missingMission).Slots.Single().Presentation.MetadataJson == null,
            "Trail requirements synthesized absent metadata");
    }
    // Presentation is optional: malformed lordmeta.json must not block capturing the Lord itself.
    internal static void InvalidMetadataFallsBack()
    {
        string root = TestWorkDirectory.New("lordmeta-invalid");
        foreach (byte[] content in new[] { System.Text.Encoding.UTF8.GetBytes("[1,2]"), System.Text.Encoding.UTF8.GetBytes("{broken"), new byte[] { 0x7B, 0xFF, 0x7D } })
        {
            string source = Path.Combine(root, Guid.NewGuid().ToString("N").Substring(0, 8)); Directory.CreateDirectory(source);
            File.WriteAllBytes(Path.Combine(source, "lordmeta.json"), content);
            bool strictFailed = false;
            try { TrailLordMediaStore.Capture("123\\Doctor", source); } catch (Exception) { strictFailed = true; }
            var fallback = TrailLordMediaStore.CaptureOrFallback("123\\Doctor", source, out string problem);
            Check(strictFailed && problem != null, "invalid metadata was not reported");
            Check(fallback.DisplayName == "Doctor" && fallback.MetadataJson == null && fallback.MediaId == null,
                "fallback must keep only the display name");
        }
        string valid = Path.Combine(root, "valid"); Directory.CreateDirectory(valid);
        File.WriteAllText(Path.Combine(valid, "lordmeta.json"), "{\"Unknown\":1}");
        Check(TrailLordMediaStore.CaptureOrFallback("Doctor", valid, out string none).MetadataJson == "{\"Unknown\":1}" && none == null,
            "valid metadata must be captured unchanged");
    }
    internal static void Media()
    {
        string root = TestWorkDirectory.New("lordmeta-media");
        string source = Path.Combine(root, "source"), target = Path.Combine(root, "package"); Directory.CreateDirectory(source);
        const string json = "{\"FacePath\":\"face\",\"Messages\":{\"Taunt1\":[{\"AudioPath\":\"fx/voice\"}]},\"Unknown\":{\"kept\":true}}";
        File.WriteAllText(Path.Combine(source, "lordmeta.json"), json);
        Directory.CreateDirectory(Path.Combine(source, "Override", "fx"));
        Directory.CreateDirectory(Path.Combine(source, "Override", "Locales", "de-DE", "fx"));
        File.WriteAllBytes(Path.Combine(source, "Override", "face.png"), new byte[] { 1,2,3 });
        File.WriteAllBytes(Path.Combine(source, "Override", "fx", "voice.ogg"), new byte[] { 4,5 });
        File.WriteAllBytes(Path.Combine(source, "Override", "Locales", "de-DE", "fx", "voice.ogg"), new byte[] { 6,7 });
        File.WriteAllText(Path.Combine(source, "Override", "ignored.lua"), "not presentation");
        var original = TrailLordMediaStore.Capture("123\\Doctor", source);
        var off = TrailLordMediaStore.SelectMedia(original, false);
        Check(off.MetadataJson == json && off.MediaId == null, "default-off export omitted metadata or included media");
        var on = TrailLordMediaStore.SelectMedia(original, true);
        Check(on.MediaHashes.Count == 3, "language variant missing or unrelated content copied");
        foreach (var exported in new[] { off, on })
        {
            var snapshot = LordDataSnapshot.CreateTrail("media-save", false, new[] { new LordDataSlot {
                PlayerId = 3, LordType = -1, LordName = "123\\Doctor", ConfigName = "aic", ConfigChecksum = "11", Presentation = exported } });
            LordDataSaveRecord.Read(LordDataSaveRecord.Write(snapshot, null), out var save, out _);
            var presentation = save.Slots.Single().Presentation;
            Check(presentation.SourceRoot == null && presentation.MetadataJson == json && presentation.MediaId == exported.MediaId &&
                (presentation.MediaHashes?.Count ?? 0) == (exported.MediaHashes?.Count ?? 0),
                "media-off/on save requires the author's installation or lost authored presentation");
        }
        Check(TrailLordMediaStore.ByteCount(new[] { on, on }) == 7, "media counted per mission instead of per unique payload");
        TrailLordMediaStore.Stage(new[] { on, on }, target);
        Check(Directory.GetFiles(target, "*", SearchOption.AllDirectories).Length == 3, "duplicate files stored");
        var restored = LordPresentationData.Decode(Shared.DependencyFreeJson.Parse(Shared.DependencyFreeJson.Serialize(LordPresentationData.Encode(on))));
        TrailLordMediaStore.BindAndVerify(new[] { restored }, target);
        string target2 = Path.Combine(root, "reexport"); TrailLordMediaStore.Stage(new[] { restored }, target2);
        Check(Directory.GetFiles(target2, "*", SearchOption.AllDirectories).Length == 3, "media lost on reexport without installed Lord");
        string coopRoot = Path.Combine(root, "coop"), makerSource = Path.Combine(coopRoot, "TrailMakerSource");
        Directory.CreateDirectory(makerSource);
        TrailLordMediaStore.Stage(new[] { restored }, Path.Combine(coopRoot, "CoopMissions"));
        var makerPresentation = LordPresentationData.Decode(Shared.DependencyFreeJson.Parse(Shared.DependencyFreeJson.Serialize(LordPresentationData.Encode(on))));
        TrailLordMediaStore.BindAndVerify(new[] { makerPresentation }, makerSource);
        Check(Directory.GetFiles(coopRoot, "*", SearchOption.AllDirectories).Length == 3 && makerPresentation.SourceRoot.Contains("CoopMissions"),
            "Coop authoring and runtime duplicated media or could not share the package store");
        TrailLordMediaStore.Publish(Array.Empty<LordPresentationData>(), target2);
        Check(Directory.GetFiles(target2, "*", SearchOption.AllDirectories).Length == 0, "disabling media retained a previous export's files");
        File.WriteAllBytes(Path.Combine(target, "LordMedia", on.MediaId, "Override", "face.png"), new byte[] { 9 });
        bool rejected = false; try { TrailLordMediaStore.BindAndVerify(new[] { restored }, target); } catch (InvalidDataException) { rejected = true; }
        Check(rejected, "tampered media accepted");
        rejected = false; try { TrailLordMediaStore.Publish(Array.Empty<LordPresentationData>(), target); } catch (InvalidDataException) { rejected = true; }
        Check(rejected && File.ReadAllBytes(Path.Combine(target, "LordMedia", on.MediaId, "Override", "face.png"))[0] == 9,
            "publication discarded modified files from the preceding export");
        File.Delete(Path.Combine(source, "Override", "face.png"));
        rejected = false; try { TrailLordMediaStore.SelectMedia(original, true); } catch (InvalidDataException) { rejected = true; }
        Check(rejected && TrailLordMediaStore.SelectMedia(original, false).MetadataJson == json, "missing-media error or metadata-only recovery failed");
    }
}
