using AIVParser.Core;
using AIVPlacement.Core;
using AIVPlacementLobby.Core;

internal static class Program
{
    private static int Main()
    {
        var tests = new (string Name, Action Run)[]
        {
            ("maps player to keep slot", MapsPlayerToKeepSlot),
            ("creates eight default candidates", CreatesDefaultCandidates),
            ("marks prebuild as not evaluable", MarksPrebuildNotEvaluable),
            ("rejects missing map", RejectsMissingMap),
            ("rejects ambiguous keep", RejectsAmbiguousKeep),
            ("resolves custom AIV", ResolvesCustomAiv),
            ("preserves multiple custom candidates", PreservesMultipleCustomCandidates),
            ("resolves embedded custom candidate", ResolvesEmbeddedCustomCandidate),
            ("uses Script Extender override", UsesAssetOverride),
            ("loads AIVJSON from the shared core", LoadsAivJsonFromSharedCore),
            ("copies mutable inputs", CopiesInputs),
            ("rejects stale generation", RejectsStaleGeneration)
        };

        int failures = 0;
        foreach ((string name, Action run) in tests)
        {
            try
            {
                run();
                Console.WriteLine($"PASS {name}");
            }
            catch (Exception ex)
            {
                failures++;
                Console.Error.WriteLine($"FAIL {name}: {ex.Message}");
            }
        }
        Console.WriteLine($"{tests.Length - failures}/{tests.Length} tests passed.");
        return failures == 0 ? 0 : 1;
    }

    private static void MapsPlayerToKeepSlot()
    {
        using Fixture fixture = new();
        AivPlacementCheckRequest request = fixture.Build(keepOrder: [-1, -1, 1, -1, -1, -1, -1, -1]);
        Equal(2, request.KeepSlotIndex);
        Equal(AivRotation.Degrees90, request.InitialRotation);
        Assert(request.IsReady, request.FailureKind.ToString());
    }

    private static void CreatesDefaultCandidates()
    {
        using Fixture fixture = new();
        AivPlacementCheckRequest request = fixture.Build();
        Equal(8, request.Candidates.Count);
        Assert(request.Candidates.All(value => value.IsAvailable), "default candidates unavailable");
        Assert(request.Candidates[0].Source.EndsWith("rat1.aivjson"), request.Candidates[0].Source);
    }

    private static void MarksPrebuildNotEvaluable()
    {
        using Fixture fixture = new();
        AivPlacementCheckRequest request = fixture.Build(preBuild: 1);
        Equal(LobbyRequestFailureKind.PreBuildSequenceUnsupported, request.FailureKind);
        Equal(AivPlacementStatus.NotEvaluable, request.ImmediateResultStatus.Value);
    }

    private static void RejectsMissingMap()
    {
        using Fixture fixture = new();
        AivPlacementCheckRequest request = fixture.Build(mapPath: Path.Combine(fixture.Root, "missing.map"));
        Equal(LobbyRequestFailureKind.MapUnavailable, request.FailureKind);
    }

    private static void RejectsAmbiguousKeep()
    {
        using Fixture fixture = new();
        AivPlacementCheckRequest request = fixture.Build(keepOrder: [1, 1, -1, -1, -1, -1, -1, -1]);
        Equal(LobbyRequestFailureKind.KeepAssignmentAmbiguous, request.FailureKind);
    }

    private static void ResolvesCustomAiv()
    {
        using Fixture fixture = new();
        string customDirectory = Directory.CreateDirectory(Path.Combine(fixture.Root, "custom")).FullName;
        File.WriteAllText(Path.Combine(customDirectory, "castle.aivjson"), "{}");
        LobbyAiSlotInput slot = Slot(
            LobbyAivMode.Custom,
            [new LobbyAivCandidateInput("castle", customDirectory, 42, false, "SK_RAT")]);
        AivPlacementCheckRequest request = fixture.Build(slot: slot);
        Assert(request.IsReady, request.FailureKind.ToString());
        Equal((ulong)42, request.Candidates[0].Checksum);
    }

    private static void PreservesMultipleCustomCandidates()
    {
        using Fixture fixture = new();
        string customDirectory = Directory.CreateDirectory(Path.Combine(fixture.Root, "custom-list")).FullName;
        File.WriteAllText(Path.Combine(customDirectory, "rat-one.aivjson"), "{}");
        File.WriteAllText(Path.Combine(customDirectory, "rat-two.aivjson"), "{}");
        File.WriteAllText(Path.Combine(customDirectory, "rat-three.aivjson"), "{}");
        LobbyAiSlotInput slot = Slot(
            LobbyAivMode.Custom,
            [
                new LobbyAivCandidateInput("rat-one", customDirectory, 11, false, "SK_RAT"),
                new LobbyAivCandidateInput("rat-two", customDirectory, 22, false, "SK_RAT"),
                new LobbyAivCandidateInput("rat-three", customDirectory, 33, false, "SK_RAT")
            ]);

        AivPlacementCheckRequest request = fixture.Build(slot: slot);

        Assert(request.IsReady, request.FailureKind.ToString());
        Equal(LobbyCandidateSelectionPolicy.NativeBestFit, request.CandidateSelectionPolicy);
        Equal(3, request.Candidates.Count);
        Equal("rat-one", request.Candidates[0].Name);
        Equal("rat-two", request.Candidates[1].Name);
        Equal("rat-three", request.Candidates[2].Name);
        Equal(2, request.Candidates[2].CandidateId);
    }

    private static void ResolvesEmbeddedCustomCandidate()
    {
        using Fixture fixture = new();
        LobbyAiSlotInput slot = Slot(
            LobbyAivMode.Custom,
            [new LobbyAivCandidateInput("Default 2", "", 2, true, "SK_RAT")]);
        AivPlacementCheckRequest request = fixture.Build(slot: slot);
        Assert(request.Candidates[0].Source.EndsWith("rat2.aivjson"), request.Candidates[0].Source);
        Assert(request.IsReady, request.FailureKind.ToString());
    }

    private static void UsesAssetOverride()
    {
        using Fixture fixture = new();
        AivPlacementCheckRequest request = fixture.Build(assets: ["AIV/SK_RAT_0.aivjson"]);
        Equal(LobbyCandidateSourceKind.ScriptExtenderAsset, request.Candidates[0].SourceKind);
    }

    private static void LoadsAivJsonFromSharedCore()
    {
        using Fixture fixture = new();
        string path = Path.Combine(fixture.Root, "package-free.aivjson");
        File.WriteAllText(
            path,
            "{/*comment*/\"pauseDelayAmount\":100,\"frames\":[" +
            "{\"itemType\":61,\"tilePositionOfsets\":[5044,],\"shouldPause\":false,}," +
            "],\"miscItems\":[],}");

        AivJsonLoadResult loaded = AivJsonFileLoader.Load(path);

        Assert(loaded.Document != null, "shared Core loader returned no document");
        Equal(0, loaded.Diagnostics.Count);
        Equal(100, loaded.Document.pauseDelayAmount);
        Equal(5044, loaded.Document.frames[0].tilePositionOfsets[0]);
    }

    private static void CopiesInputs()
    {
        using Fixture fixture = new();
        int[] keepOrder = [-1, -1, 1, -1, -1, -1, -1, -1];
        var slots = new List<LobbyAiSlotInput> { Slot() };
        LobbyStateCapture capture = fixture.Capture(keepOrder: keepOrder, slots: slots);
        keepOrder[2] = -1;
        slots.Clear();
        AivPlacementCheckRequest request = new LobbyRequestBuilder()
            .Build(1, capture, fixture.VanillaDirectory).Requests.Single();
        Equal(2, request.KeepSlotIndex);
    }

    private static void RejectsStaleGeneration()
    {
        var gate = new LobbyRequestGenerationGate();
        long first = gate.Advance();
        long second = gate.Advance();
        Assert(!gate.IsCurrent(first), "old generation accepted");
        Assert(gate.IsCurrent(second), "current generation rejected");
    }

    private static LobbyAiSlotInput Slot(
        LobbyAivMode mode = LobbyAivMode.Default,
        IEnumerable<LobbyAivCandidateInput> candidates = null) =>
        new(2, 0, "SK_RAT", "", mode, 1, candidates ?? []);

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"expected {expected}, got {actual}");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class Fixture : IDisposable
    {
        public Fixture()
        {
            Root = Path.Combine(Path.GetTempPath(), "AIVPlacementLobbyTests", Guid.NewGuid().ToString("N"));
            VanillaDirectory = Directory.CreateDirectory(Path.Combine(Root, "VanillaAIV")).FullName;
            MapPath = Path.Combine(Root, "test.map");
            File.WriteAllText(MapPath, "synthetic");
            for (int index = 1; index <= 8; index++)
                File.WriteAllText(Path.Combine(VanillaDirectory, $"rat{index}.aivjson"), "{}");
        }

        public string Root { get; }
        public string VanillaDirectory { get; }
        public string MapPath { get; }

        public LobbyStateCapture Capture(
            int preBuild = 0,
            string mapPath = null,
            int[] keepOrder = null,
            IList<LobbyAiSlotInput> slots = null,
            IEnumerable<string> assets = null) =>
            new(
                mapPath ?? MapPath,
                "Synthetic",
                "Synthetic",
                true,
                preBuild,
                keepOrder ?? [-1, -1, 1, -1, -1, -1, -1, -1],
                slots ?? [Slot()],
                assets ?? []);

        public AivPlacementCheckRequest Build(
            int preBuild = 0,
            string mapPath = null,
            int[] keepOrder = null,
            LobbyAiSlotInput slot = null,
            IEnumerable<string> assets = null) =>
            new LobbyRequestBuilder()
                .Build(1, Capture(preBuild, mapPath, keepOrder,
                    new List<LobbyAiSlotInput> { slot ?? Slot() }, assets), VanillaDirectory)
                .Requests.Single();

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, true);
        }
    }
}
