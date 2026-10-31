using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CrusaderDE;
using ExtendedData;
using ExtendedData.Core;

// Regressions of the Custom Trail fixes (F1-F8 in CustomTrailFixes.md) that need no game process.
internal static class CustomTrailFixTests
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static T Reject<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T error) { return error; }
        throw new Exception("Expected " + typeof(T).Name);
    }

    private static string TempRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "ExtendedDataTrailFix", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void Delete(string root)
    {
        string parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ExtendedDataTrailFix"));
        Check(Path.GetDirectoryName(Path.GetFullPath(root)) == parent, "unsafe test cleanup path");
        Directory.Delete(root, true);
    }

    private static TrailLordSlot Slot(int playerId, string checksum = "11", int? type = -1) => new TrailLordSlot
    {
        PlayerId = playerId, LordType = type, LordName = "Phoenix", ConfigName = "Phoenix",
        ConfigChecksum = checksum, AivChecksums = new[] { "21", "22" },
    };

    // F5/F6: stored requirements keep values only for the same authored identity. New writes include complete metadata;
    // legacy v2/v3 remain readable without inventing complete Fixes source captures.
    internal static void RequirementsBindIdentityAndDecision()
    {
        string root = TempRoot();
        try
        {
            string trail = Path.Combine(root, "Trail_Mission_01.trail");
            File.WriteAllText(trail, "trail bytes");
            TrailLordSlot valued = Slot(3);
            valued.ModLordJson = "{\"test.mod\":{\"value\":1}}";
            TrailLordRequirements.Create(trail, new[] { valued }).Write(trail);
            string current = File.ReadAllText(TrailLordRequirements.SidecarPath(trail));
            Check(current.Contains("\"version\": " + TrailLordRequirements.CurrentVersion), "complete source format was not written");
            File.WriteAllText(TrailLordRequirements.SidecarPath(trail), current.Replace("\"version\": " + TrailLordRequirements.CurrentVersion, "\"version\": 2"));
            Check(TrailLordRequirements.Read(trail).Slots.Single().FixesSources == null,
                "legacy requirements invented a complete source capture");

            File.AppendAllText(trail, " re-saved");
            TrailLordRequirements stale = TrailLordRequirements.ReadUnbound(trail, out bool bound);
            Check(!bound && stale.Slots.Single().ModLordJson == valued.ModLordJson,
                "a re-saved Trail lost its stored values or was reported as bound");
            Reject<InvalidDataException>(() => TrailLordRequirements.Read(trail));

            Check(TrailLordRequirements.HasSameAuthoredIdentity(stale.Slots[0], Slot(3)), "same identity rejected");
            Check(TrailLordRequirements.HasSameAuthoredIdentity(Slot(3, type: null), Slot(3)),
                "legacy requirements without a Lord type lost their identity");
            TrailLordSlot reordered = Slot(3);
            reordered.AivChecksums = new[] { "22", "21" };
            Check(!TrailLordRequirements.HasSameAuthoredIdentity(stale.Slots[0], reordered) &&
                !TrailLordRequirements.HasSameAuthoredIdentity(stale.Slots[0], Slot(3, "99")) &&
                !TrailLordRequirements.HasSameAuthoredIdentity(stale.Slots[0], Slot(4)),
                "changed AIV order, configuration or slot kept the stored values");

            TrailLordSlot embeddedOnly = Slot(3);
            embeddedOnly.EmbeddedValuesOnly = true;
            TrailLordRequirements.Create(trail, new[] { embeddedOnly, Slot(5) }).Write(trail);
            string json = File.ReadAllText(TrailLordRequirements.SidecarPath(trail));
            TrailLordRequirements read = TrailLordRequirements.Read(trail);
            Check(json.Contains("\"version\": " + TrailLordRequirements.CurrentVersion) && read.Slots.Single(slot => slot.PlayerId == 3).EmbeddedValuesOnly &&
                !read.Slots.Single(slot => slot.PlayerId == 5).EmbeddedValuesOnly,
                "the embedded-only export decision was not stored per slot");
            embeddedOnly.FixesJson = "{}";
            Reject<InvalidDataException>(() => TrailLordRequirements.Create(trail, new[] { embeddedOnly }));
        }
        finally { Delete(root); }
    }

    // F6: a different configuration under the same Lord name is a replacement; a media alias is not.
    internal static void ConfigurationIdentitySelectsEmbeddedSlots()
    {
        TrailLordSlot slot = Slot(3);
        Check(TrailLordSelectionPolicy.UsesEmbeddedLord(slot, false, "Phoenix", null, null, -1, "Phoenix", "11"),
            "the embedded configuration was not recognized");
        Check(!TrailLordSelectionPolicy.UsesEmbeddedLord(slot, false, "Phoenix", null, null, -1, "Phoenix_cheat", "12"),
            "another configuration under the same Lord name kept the embedded values");
        Check(!TrailLordSelectionPolicy.UsesEmbeddedLord(slot, false, "Phoenix", null, null, -1, "Phoenix", "12"),
            "a changed checksum kept the embedded values");
        Check(TrailLordSelectionPolicy.UsesEmbeddedLord(slot, false, "3693412090\\Phoenix", "3693412090\\Phoenix",
            null, -1, "Phoenix", "11"), "a media alias of the same configuration was treated as replaced");
        var unnamed = new TrailLordSlot { PlayerId = 3, LordType = 3, LordName = "Wolf", ConfigName = "aic", ConfigChecksum = "5" };
        Check(TrailLordSelectionPolicy.UsesEmbeddedLord(unnamed, false, "", null, 3, 3, "aic", "5") &&
            !TrailLordSelectionPolicy.UsesEmbeddedLord(unnamed, false, "", null, 3, 2, "aic", "5"),
            "an unnamed standard Lord with a custom AIC was not bound by configuration type");
    }

    internal static void CoopRemapLeavesMakerSlotsUnchanged()
    {
        TrailLordSlot maker = Slot(6);
        IReadOnlyList<TrailLordSlot> coop = CoopTrailSlotSelection.RemapForCoop(new[] { maker }, new List<int> { 1, 3, 5 });
        Check(maker.PlayerId == 6 && coop.Single().PlayerId == 3 && !ReferenceEquals(maker, coop[0]),
            "Coop numbering mutated the Maker slot");
    }

    // P2/P3: schema 5 references the bundled Trail slot; schema 4 keeps its meaning and reader.
    internal static void SchemaFiveEmbeddedReferences()
    {
        using Fixture fixture = Fixture.Create();
        File.WriteAllText(Path.Combine(fixture.Root, "mission.trail"), "trail");
        CoopMissionDefinition definition = MissionDefinitionJson.Parse(File.ReadAllText(fixture.JsonPath));
        PlayerDefinition ai = definition.Players[2];
        ai.Lord = new LordReference { Source = MissionLoader.EmbeddedTrailSource, File = "mission.trail", SourcePlayerId = 6, BaseLordId = 4 };
        ai.Aivs = new List<AivReference>();
        ai.PreferredAiv = -1;
        definition.Map.OriginalSource = "workshop";
        definition.Map.OriginalName = "Original Map";

        void Write(Action<CoopMissionDefinition, PlayerDefinition> change, int schema = MissionLoader.CurrentSchemaVersion)
        {
            CoopMissionDefinition copy = MissionDefinitionJson.Parse(MissionDefinitionJson.Serialize(definition));
            copy.SchemaVersion = schema;
            change?.Invoke(copy, copy.Players[2]);
            File.WriteAllText(fixture.JsonPath, MissionDefinitionJson.Serialize(copy), new UTF8Encoding(false));
        }

        Write(null);
        LoadedMission loaded = new MissionLoader().Load(fixture.JsonPath, 1, 1);
        Check(loaded.Definition.Players[2].Lord.SourcePlayerId == 6 &&
            loaded.Definition.Map.OriginalName == "Original Map" &&
            loaded.BundledFiles.Any(file => file.EndsWith("mission.trail", StringComparison.OrdinalIgnoreCase)),
            "the embedded Trail reference or provenance was lost or not fingerprinted");

        foreach (var invalid in new (string Name, Action<CoopMissionDefinition, PlayerDefinition> Change, int Schema)[]
        {
            ("schema 4 embedded", (d, _) => { d.Map.OriginalSource = null; d.Map.OriginalName = null; }, MissionLoader.LegacySchemaVersion),
            ("explicit AIV beside embedded", (_, p) => p.Aivs.Add(new AivReference { Source = "bundled", File = "castle.aivjson", Rotation = 90 }), MissionLoader.CurrentSchemaVersion),
            ("missing source slot", (_, p) => p.Lord.SourcePlayerId = null, MissionLoader.CurrentSchemaVersion),
            ("source slot out of range", (_, p) => p.Lord.SourcePlayerId = 9, MissionLoader.CurrentSchemaVersion),
            ("wrong extension", (_, p) => p.Lord.File = "mission.map", MissionLoader.CurrentSchemaVersion),
            ("preferred explicit AIV", (_, p) => p.PreferredAiv = 0, MissionLoader.CurrentSchemaVersion),
            ("source slot on a bundled Lord", (_, p) => { p.Lord.Source = "bundled"; p.Lord.File = "lord.lordjson"; }, MissionLoader.CurrentSchemaVersion),
            ("schema 4 provenance", (d, p) => { p.Lord = new LordReference { Source = "bundled", File = "lord.lordjson" }; p.Lord.SourcePlayerId = null; }, MissionLoader.LegacySchemaVersion),
        })
        {
            Write(invalid.Change, invalid.Schema);
            Reject<InvalidDataException>(() => new MissionLoader().Load(fixture.JsonPath, 1, 1));
        }

        Write((d, p) =>
        {
            d.Map.OriginalSource = null;
            d.Map.OriginalName = null;
            p.Lord = new LordReference { Source = "bundled", File = "lord.lordjson", BaseLordId = 0 };
            p.Aivs = new List<AivReference> { new AivReference { Source = "bundled", File = "castle.aivjson", Rotation = 90 } };
        }, MissionLoader.LegacySchemaVersion);
        Check(new MissionLoader().Load(fixture.JsonPath, 1, 1).Definition.SchemaVersion == MissionLoader.LegacySchemaVersion,
            "a schema 4 mission is no longer readable");
    }

    // P5: the Coop missions and the editable Maker sources are replaced together or not at all.
    internal static void PublicationReplacesMakerSourcesAtomically()
    {
        string root = TempRoot();
        try
        {
            string target = Path.Combine(root, "target");
            string oldMaker = Path.Combine(target, CoopTrailPackagePublication.TrailMakerSourceDirectory);
            Directory.CreateDirectory(Path.Combine(target, CoopTrailPackagePublication.MissionsDirectory));
            Directory.CreateDirectory(oldMaker);
            File.WriteAllText(Path.Combine(oldMaker, "Trail_Mission_01.trail"), "old source");
            string stage = Path.Combine(root, "stage");
            void Stage()
            {
                Directory.CreateDirectory(Path.Combine(stage, CoopTrailPackagePublication.MissionsDirectory));
                Directory.CreateDirectory(Path.Combine(stage, CoopTrailPackagePublication.TrailMakerSourceDirectory));
                File.WriteAllText(Path.Combine(stage, CoopTrailPackagePublication.TrailMakerSourceDirectory, "Trail_Mission_01.trail"), "new source");
            }
            Stage();
            var failing = new FailingManifestFiles();
            Reject<IOException>(() => CoopTrailPackagePublication.Publish(stage, target, Manifest(), failing));
            Check(File.ReadAllText(Path.Combine(oldMaker, "Trail_Mission_01.trail")) == "old source" &&
                Directory.Exists(Path.Combine(target, CoopTrailPackagePublication.MissionsDirectory)),
                "a failed publication left new Maker sources behind or lost the old missions");
            Stage();
            CoopTrailPackagePublication.Publish(stage, target, Manifest());
            Check(File.ReadAllText(Path.Combine(oldMaker, "Trail_Mission_01.trail")) == "new source" &&
                Directory.GetDirectories(target).All(path => !Path.GetFileName(path).StartsWith(".", StringComparison.Ordinal)),
                "Maker sources were not published with the package or a backup remained");
        }
        finally { Delete(root); }
    }

    private static CoopTrailPackageManifest Manifest() => new CoopTrailPackageManifest
    {
        SchemaVersion = CoopTrailPackageManifestJson.CurrentSchemaVersion,
        PackageId = Guid.NewGuid().ToString("D"), DisplayName = "Test", MissionCount = 1,
        ContentFingerprint = new string('C', 64),
    };

    private sealed class FailingManifestFiles : ICoopTrailPackageFiles
    {
        public bool DirectoryExists(string path) => Directory.Exists(path);
        public void CreateDirectory(string path) => Directory.CreateDirectory(path);
        public void MoveDirectory(string source, string destination) => Directory.Move(source, destination);
        public void DeleteDirectory(string path) => Directory.Delete(path, true);
        public void WriteManifest(string path, CoopTrailPackageManifest manifest) => throw new IOException("injected manifest");
    }

    // P2: the original map reference survives without the original installation.
    internal static void RestartMapReferenceIsReadWithoutTheOriginalMap()
    {
        var bytes = new List<byte> { 60 };
        bytes.AddRange(BitConverter.GetBytes(2));
        bytes.AddRange(new byte[24]);
        bytes.Add(2);
        byte[] name = Encoding.UTF8.GetBytes("Two Against the Marsh");
        bytes.AddRange(BitConverter.GetBytes(name.Length));
        bytes.AddRange(name);
        bytes.AddRange(new byte[] { 9, 9 });
        TrailRestartMapReference reference = TrailRestartMapReference.Read(bytes.ToArray());
        Check(reference.Source == "workshop" && reference.Name == "Two Against the Marsh", "the original map reference was misread");
        Reject<InvalidDataException>(() => TrailRestartMapReference.Read(bytes.Take(20).ToArray()));
        byte[] hugeCount = bytes.ToArray();
        BitConverter.GetBytes(int.MaxValue).CopyTo(hugeCount, 1);
        Reject<InvalidDataException>(() => TrailRestartMapReference.Read(hugeCount));
    }
}
