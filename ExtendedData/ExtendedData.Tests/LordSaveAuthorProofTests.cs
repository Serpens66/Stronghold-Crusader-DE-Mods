using ExtendedData;
using System;
using System.IO;

internal static class LordSaveAuthorProofTests
{
    private static void Require(bool value, string text) { if (!value) throw new Exception(text); }
    private static LordDataSlot Slot() => new LordDataSlot { PlayerId = 2, LordType = 3, LordName = "Wolf",
        ConfigName = "authored", ConfigChecksum = "42", FixesJson = "{\"Value\":9}",
        FixesSources = new[] { new FixesPreferenceSource { CustomName = "RAT", Json = null },
            new FixesPreferenceSource { LordType = 0, Json = "{\"Value\":7}" } } };
    private static LordDataSnapshot Record(LordDataSlot slot, string session = "original") =>
        LordDataSnapshot.Create(session, true, new[] { slot });

    internal static void Run()
    {
        ProvenanceRead();
        Ownership();
        var author = Record(Slot());
        string wire = author.WireJson, digest = author.Digest;
        Require(LordSaveAuthorProof.Matches(author, Record(Slot(), "new-lobby"), value => value),
            "Session rebasing changed immutable author values.");
        foreach (Action<LordDataSlot> change in new Action<LordDataSlot>[] {
            slot => slot.LordType = -1, slot => slot.LordName = "Other", slot => slot.ConfigName = "other",
            slot => slot.ConfigChecksum = "43", slot => slot.FixesJson = "{\"Value\":10}",
            slot => slot.FixesSources = new[] { new FixesPreferenceSource { LordType = 0, Json = "{\"Value\":8}" } },
            slot => slot.Presentation = new LordPresentationData { DisplayName = "Different" },
        })
        {
            var changed = Slot(); change(changed);
            Require(!LordSaveAuthorProof.Matches(author, Record(changed), value => value),
                "A changed authored value was admitted as a migration.");
        }
        var legacySlot = Slot(); legacySlot.FixesSources = null;
        var legacy = Record(legacySlot);
        string legacyWire = legacy.WireJson, legacyDigest = legacy.Digest;
        Require(LordSaveAuthorProof.Matches(legacy, Record(Slot(), "new-lobby"), value => value),
            "Host-authorized completion of legacy missing sources was rejected.");
        Require(!LordSaveAuthorProof.Matches(legacy, Record(legacySlot, "new-lobby"), value => value),
            "Legacy missing sources were treated as a complete current publication.");
        var changedLegacy = Slot(); changedLegacy.FixesJson = "{\"Value\":123}";
        Require(!LordSaveAuthorProof.Matches(legacy, Record(changedLegacy), value => value),
            "Source completion changed previously authored own preferences.");
        Require(author.WireJson == wire && author.Digest == digest && legacy.WireJson == legacyWire &&
            legacy.Digest == legacyDigest && legacy.Slots[0].FixesSources == null,
            "Readiness proof rewrote the original persisted envelope or legacy history.");
        foreach (var invalid in new[] { new FixesPreferenceSource { LordType = -1 },
            new FixesPreferenceSource { CustomName = "" }, new FixesPreferenceSource { CustomName = "RAT", LordType = 0 } })
        {
            bool rejected = false;
            try { FixesPreferenceSources.Identity(invalid); } catch (InvalidDataException) { rejected = true; }
            Require(rejected, "Fixes source validation was weakened for a selected Lord's separate -1 type domain.");
        }
        Require(TrailLordSourceSelector.ResolveLordName("", 0, new[] { "Rat" }) == "Rat",
            "Vanilla unnamed Extended Lord did not resolve through its actual type.");
        bool emptyCustomRejected = false;
        try { TrailLordSourceSelector.ResolveLordName("", -1, new[] { "Rat" }); }
        catch (InvalidDataException) { emptyCustomRejected = true; }
        Require(emptyCustomRejected, "Unnamed Custom Lord was guessed as an Extended Lord.");
        Require(LordSaveAuthorProof.AllowsInheritedDigest(null, "host-completed-sources"),
            "Legacy local-values source completion was treated as an exact authored null digest.");
        Require(LordSaveAuthorProof.AllowsInheritedDigest("original", "original") &&
            !LordSaveAuthorProof.AllowsInheritedDigest("original", "replacement") &&
            !LordSaveAuthorProof.AllowsInheritedDigest("original", null),
            "Existing authored local-values source history was changed or erased.");
    }

    private static void Ownership()
    {
        var ownership = new LordSaveOwnership();
        ownership.Initializing(17);
        Require(!ownership.MissionOwned && ownership.FailedBeforeStarted(17),
            "Initialization was incorrectly treated as committed mission ownership.");
        Require(!ownership.Started(18) && !ownership.FailedBeforeStarted(18),
            "An unrelated mission changed the provisional save owner.");
        Require(ownership.Started(17) && ownership.MissionOwned && !ownership.FailedBeforeStarted(17),
            "A started save remained eligible for failed-start cleanup.");
        Require(LordSaveOwnership.IsCurrentObservation("A", 1, "A", 1) &&
            !LordSaveOwnership.IsCurrentObservation("A", 1, "B", 2) &&
            !LordSaveOwnership.IsCurrentObservation("A", 1, "A", 2) &&
            !LordSaveOwnership.IsCurrentObservation("A", 1, null, -1),
            "A stale or reentrant preparation observation retained permission to take a newer candidate.");
    }

    private static void ProvenanceRead()
    {
        Require(LordSaveFileRead.MatchesSource("C:\\Saves\\A.sav", "ABC123", "c:\\saves\\a.sav", "abc123"),
            "Canonical Windows source identity became case sensitive.");
        Require(!LordSaveFileRead.MatchesSource("C:\\Saves\\A.sav", "ABC123", "C:\\Saves\\A.sav", "DEF456") &&
            !LordSaveFileRead.MatchesSource("C:\\Saves\\A.sav", "ABC123", "C:\\Saves\\B.sav", "ABC123") &&
            !LordSaveFileRead.MatchesSource(null, "ABC123", null, "ABC123") &&
            !LordSaveFileRead.MatchesSource("C:\\Saves\\A.sav", null, "C:\\Saves\\A.sav", null),
            "A changed or missing candidate source could be treated as a fresh direct-load request.");
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "original author");
            string before;
            using (var sha = System.Security.Cryptography.SHA256.Create())
                before = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "");
            string captured = LordSaveFileRead.Read(path, originalHash =>
            {
                Require(originalHash == before && File.ReadAllText(path) == "original author",
                    "The author callback was not bound to the pre-parse file hash.");
                bool writerRejected = false;
                try { File.WriteAllText(path, "replacement author"); } catch (IOException) { writerRejected = true; }
                Require(writerRejected, "A writer could replace the file while its author record was being parsed.");
                return originalHash;
            });
            Require(captured == before && File.ReadAllText(path) == "original author",
                "A captured author record was paired with a replacement file hash.");
            bool propagated = false;
            try { LordSaveFileRead.Read<string>(path, _ => throw new InvalidDataException("malformed archive")); }
            catch (InvalidDataException) { propagated = true; }
            Require(propagated, "Malformed archive parsing became an admitted empty author record.");
        }
        finally { File.Delete(path); }
    }
}
