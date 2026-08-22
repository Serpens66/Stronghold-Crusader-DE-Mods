using BugfixesAndQoL;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

internal static class Program
{
    private static int failures;

    private static int Main()
    {
        Run("preserves slot and candidate order", PreservesOrder);
        Run("canonicalizes AI slot order", CanonicalizesSlotOrder);
        Run("appends visible selections in order without duplicates", AppendsSelectionInOrder);
        Run("limits active selections to 50", LimitsSelection);
        Run("deduplicates identical AIV data", DeduplicatesData);
        Run("round-trips compression and chunks", RoundTripsCompressionAndChunks);
        Run("rejects a corrupt blob hash", RejectsCorruptHash);
        Run("rejects more than 50 candidates", RejectsCandidateLimit);
        Run("rejects an oversized manifest", RejectsSizeLimit);
        Run("accepts packets only from the lobby host", ValidatesSender);
        Run("invalidates responses after roster changes", InvalidatesRosterChanges);
        Console.WriteLine(failures == 0
            ? "BugfixesAndQoL AIV sync protocol tests passed."
            : $"BugfixesAndQoL AIV sync protocol tests failed: {failures}.");
        return failures == 0 ? 0 : 1;
    }

    private static void Run(string name, Action test)
    {
        try
        {
            test();
            Console.WriteLine("PASS " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("FAIL " + name + ": " + ex.Message);
        }
    }

    private static void PreservesOrder()
    {
        MultiplayerAivManifest decoded = RoundTrip(BuildManifest(3, 4, distinct: true));
        Equal(new[] { 2, 3, 4 }, decoded.Slots.Select(slot => slot.PlayerId).ToArray());
        Equal(new ulong[] { 200, 201, 202, 203 },
            decoded.Slots[0].Candidates.Select(candidate => candidate.Checksum).ToArray());
    }

    private static void CanonicalizesSlotOrder()
    {
        MultiplayerAivManifest manifest = BuildManifest(3, 2, distinct: true);
        manifest.Slots.Reverse();
        MultiplayerAivManifest decoded = RoundTrip(manifest);
        Equal(new[] { 2, 3, 4 }, decoded.Slots.Select(slot => slot.PlayerId).ToArray());
        Equal(new ulong[] { 400, 401 },
            decoded.Slots[2].Candidates.Select(candidate => candidate.Checksum).ToArray());
    }

    private static void AppendsSelectionInOrder()
    {
        var available = Enumerable.Range(0, 8).Select(index => new Candidate((ulong)index)).ToList();
        var selected = new List<Candidate> { available[5] };
        int added = AivCandidateSelectionPolicy.AppendDistinct(
            selected, available, new[] { 7, 2, 5, 3 }, value => value.Checksum, 50);
        Assert(added == 3, "unexpected added count");
        Equal(new ulong[] { 5, 2, 3, 7 }, selected.Select(value => value.Checksum).ToArray());
    }

    private static void LimitsSelection()
    {
        var available = Enumerable.Range(0, 80).Select(index => new Candidate((ulong)index)).ToList();
        var selected = new List<Candidate>();
        AivCandidateSelectionPolicy.AppendDistinct(
            selected, available, Enumerable.Range(0, available.Count).Reverse(), value => value.Checksum, 50);
        Assert(selected.Count == 50, "selection was not limited to 50");
        Equal(Enumerable.Range(0, 50).Select(value => (ulong)value).ToArray(),
            selected.Select(value => value.Checksum).ToArray());
    }

    private static void DeduplicatesData()
    {
        MultiplayerAivManifest manifest = BuildManifest(2, 3, distinct: false);
        byte[] oneBlob = MultiplayerAivSyncProtocol.Encode(manifest);
        MultiplayerAivManifest distinct = BuildManifest(2, 3, distinct: true);
        byte[] sixBlobs = MultiplayerAivSyncProtocol.Encode(distinct);
        Assert(oneBlob.Length < sixBlobs.Length, "identical AIV data was not deduplicated");
        MultiplayerAivManifest decoded = MultiplayerAivSyncProtocol.Decode(oneBlob);
        Assert(ReferenceEquals(decoded.Slots[0].Candidates[0].Data, decoded.Slots[1].Candidates[2].Data),
            "decoded references do not share the canonical blob");
    }

    private static void RoundTripsCompressionAndChunks()
    {
        byte[] encoded = MultiplayerAivSyncProtocol.Encode(BuildManifest(7, 50, distinct: true));
        byte[] compressed = MultiplayerAivSyncProtocol.Compress(encoded);
        List<byte[]> chunks = MultiplayerAivSyncProtocol.Split(compressed);
        byte[] assembled = chunks.SelectMany(chunk => chunk).ToArray();
        byte[] decoded = MultiplayerAivSyncProtocol.Decompress(assembled, encoded.Length);
        Assert(MultiplayerAivSyncProtocol.FixedEquals(encoded, decoded), "chunk reassembly changed data");
        Assert(chunks.All(chunk => chunk.Length <= MultiplayerAivSyncProtocol.MaximumChunkBytes),
            "chunk exceeds 192 KiB");
    }

    private static void RejectsCorruptHash()
    {
        byte[] encoded = MultiplayerAivSyncProtocol.Encode(BuildManifest(1, 2, distinct: true));
        encoded[encoded.Length - 1] ^= 0x5A;
        Throws<InvalidDataException>(() => MultiplayerAivSyncProtocol.Decode(encoded));
    }

    private static void RejectsCandidateLimit()
    {
        Throws<InvalidDataException>(() =>
            MultiplayerAivSyncProtocol.Encode(BuildManifest(1, 51, distinct: true)));
    }

    private static void RejectsSizeLimit()
    {
        var manifest = new MultiplayerAivManifest { LobbyId = 1, VanillaChecksum = "x" };
        var slot = new MultiplayerAivSlot { PlayerId = 2 };
        for (int index = 0; index < 50; index++)
        {
            slot.Candidates.Add(new MultiplayerAivCandidate
            {
                Checksum = (ulong)index,
                Data = new short[MultiplayerAivSyncProtocol.MaximumUncompressedBytes / 100 + index]
            });
        }
        manifest.Slots.Add(slot);
        Throws<InvalidDataException>(() => MultiplayerAivSyncProtocol.Encode(manifest));
    }

    private static void ValidatesSender()
    {
        Assert(MultiplayerAivSyncPolicy.CanAcceptHostPacket(false, true, 123, 123),
            "lobby host packet was rejected");
        Assert(!MultiplayerAivSyncPolicy.CanAcceptHostPacket(false, true, 456, 123),
            "non-host sender was accepted");
        Assert(!MultiplayerAivSyncPolicy.CanAcceptHostPacket(true, true, 123, 123),
            "host accepted a client data packet");
    }

    private static void InvalidatesRosterChanges()
    {
        Assert(MultiplayerAivSyncPolicy.IsCurrentResponse(
                true, true, true, 7, 7, "ABC", "abc", "10,20", "10,20"),
            "current response was rejected");
        Assert(!MultiplayerAivSyncPolicy.IsCurrentResponse(
                true, true, true, 7, 7, "ABC", "abc", "10,20", "10,30"),
            "response from an obsolete roster was accepted");
        Assert(MultiplayerAivSyncPolicy.HasContextChanged("10,20", "10,30", "a", "a"),
            "roster change did not invalidate the transfer");
    }

    private static MultiplayerAivManifest RoundTrip(MultiplayerAivManifest manifest) =>
        MultiplayerAivSyncProtocol.Decode(MultiplayerAivSyncProtocol.Encode(manifest));

    private static MultiplayerAivManifest BuildManifest(int slots, int candidates, bool distinct)
    {
        var manifest = new MultiplayerAivManifest { LobbyId = 123, VanillaChecksum = "vanilla" };
        for (int slotIndex = 0; slotIndex < slots; slotIndex++)
        {
            var slot = new MultiplayerAivSlot { PlayerId = slotIndex + 2 };
            for (int candidateIndex = 0; candidateIndex < candidates; candidateIndex++)
            {
                int seed = distinct ? slotIndex * 100 + candidateIndex : 7;
                slot.Candidates.Add(new MultiplayerAivCandidate
                {
                    Checksum = (ulong)((slotIndex + 2) * 100 + candidateIndex),
                    Data = Enumerable.Range(0, 256).Select(value => (short)(value + seed)).ToArray()
                });
            }
            manifest.Slots.Add(slot);
        }
        manifest.Slots.Sort((left, right) => left.PlayerId.CompareTo(right.PlayerId));
        return manifest;
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new Exception("expected " + typeof(T).Name);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }

    private static void Equal<T>(T[] expected, T[] actual)
    {
        Assert(expected.SequenceEqual(actual),
            "expected [" + string.Join(",", expected) + "] but got [" + string.Join(",", actual) + "]");
    }

    private sealed class Candidate
    {
        public Candidate(ulong checksum) { Checksum = checksum; }
        public ulong Checksum { get; }
    }
}
