using AIVParser.Core;
using AIVPlacement.Core;
using CastlePlanner.AIVPlacement.Core;
using MapParser.Core;
using System.Collections.Concurrent;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length == 3 && string.Equals(args[0], "--integration", StringComparison.Ordinal))
            return RunLocalIntegration(args[1], args[2]);

        var tests = new (string Name, Action Run)[]
        {
            ("maps player to keep slot", MapsPlayerToKeepSlot),
            ("maps lobby rotation values to native degrees", MapsLobbyRotationValues),
            ("resolves Vanilla map-facing start rotations", ResolvesMapFacingRotations),
            ("tracks retained starts in native player order", TracksRetainedStartsInNativePlayerOrder),
            ("evaluates AI starts sequentially", EvaluatesAiStartsSequentially),
            ("reuses multiplayer tie choice for sequential starts", ReusesTieChoiceForSequentialStarts),
            ("creates eight default candidates", CreatesDefaultCandidates),
            ("maps current lord enum names to bundled Vanilla files", MapsCurrentLordNamesToVanillaFiles),
            ("marks prebuild as not evaluable", MarksPrebuildNotEvaluable),
            ("keeps client evaluation host-only", KeepsClientEvaluationHostOnly),
            ("rejects missing map", RejectsMissingMap),
            ("rejects ambiguous keep", RejectsAmbiguousKeep),
            ("resolves custom AIV", ResolvesCustomAiv),
            ("preserves multiple custom candidates", PreservesMultipleCustomCandidates),
            ("leaves multiplayer AIV selection to Vanilla", LeavesMultiplayerSelectionToVanilla),
            ("publishes optional status without a hard Bugfix dependency", PublishesStatusWithoutDependency),
            ("resolves embedded custom candidate", ResolvesEmbeddedCustomCandidate),
            ("uses Script Extender override", UsesAssetOverride),
            ("loads AIVJSON from the shared core", LoadsAivJsonFromSharedCore),
            ("loads in-memory AIVJSON from the shared core", LoadsInMemoryAivJsonFromSharedCore),
            ("copies mutable inputs", CopiesInputs),
            ("rejects stale generation", RejectsStaleGeneration),
            ("throttles unchanged lobby captures", ThrottlesUnchangedLobbyCaptures),
            ("cancels superseded generation work", CancelsSupersededGenerationWork),
            ("classifies expected and unexpected evaluation logs", ClassifiesEvaluationLogs),
            ("caches not-evaluable placement results", CachesNotEvaluableResults),
            ("coalesces concurrent placement requests", CoalescesConcurrentRequests),
            ("invalidates cache after AIV and map changes", InvalidatesChangedFiles),
            ("invalidates cache after retained starts change", InvalidatesRetainedStarts),
            ("bounds the placement result cache", BoundsResultCache),
            ("fingerprints source file changes", FingerprintsSourceChanges),
            ("keeps prebuild out of the worker", KeepsPrebuildOutOfWorker),
            ("aggregates every candidate in import order", AggregatesEveryCandidate),
            ("preserves import order for complete ties", PreservesCompleteTieOrder),
            ("selects the best sequential partial", SelectsBestSequentialPartial),
            ("randomizes every complete tie", FindsEveryCompleteTie),
            ("randomizes every highest partial score tie", FindsEveryHighestPartialScoreTie)
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

    private static int RunLocalIntegration(string mapPath, string aivPath)
    {
        try
        {
            MapDocument document = MapFileReader.Parse(mapPath);
            MapKeepAnchorResult keep = MapKeepAnchors.Create(document).Slots.First(
                value => value.Status == MapKeepAnchorStatus.Exact);
            int[] keepOrder = Enumerable.Repeat(-1, MapKeepAnchors.SlotCount).ToArray();
            keepOrder[keep.SlotIndex] = 1;
            var capture = new LobbyStateCapture(
                mapPath,
                Path.GetFileNameWithoutExtension(mapPath),
                "LocalIntegration",
                true,
                0,
                keepOrder,
                [new LobbyAiSlotInput(
                    2,
                    0,
                    "SK_RAT",
                    string.Empty,
                    LobbyAivMode.Custom,
                    0,
                    [new LobbyAivCandidateInput(
                        Path.GetFileNameWithoutExtension(aivPath),
                        Path.GetDirectoryName(aivPath),
                        0,
                        false,
                        "SK_RAT")])],
                Array.Empty<string>());
            AivPlacementCheckRequest request = new LobbyRequestBuilder()
                .Build(1, capture, Path.GetDirectoryName(aivPath))
                .Requests.Single();
            var service = new AivPlacementEvaluationService();
            AivPlacementCheckResult first = service.EvaluateAsync(request).GetAwaiter().GetResult();
            AivPlacementCheckResult second = service.EvaluateAsync(request).GetAwaiter().GetResult();
            Console.WriteLine(
                $"INTEGRATION status={first.Status}, candidate={first.SelectedCandidate?.CandidateId}, " +
                $"rotation={first.SelectedVariant?.Rotation}, elapsedMs={first.Elapsed.TotalMilliseconds:F3}");
            foreach (AivPlacementCandidateEvaluation candidate in first.Candidates)
            {
                Console.WriteLine(
                    $"PHASE candidate={candidate.CandidateId}, cache={candidate.CacheDisposition}, " +
                    $"mapParseMs={candidate.Timings.MapParse.TotalMilliseconds:F3}, " +
                    $"snapshotMs={candidate.Timings.Snapshot.TotalMilliseconds:F3}, " +
                    $"aivParseMs={candidate.Timings.AivParse.TotalMilliseconds:F3}, " +
                    $"projectionMs={candidate.Timings.Projection.TotalMilliseconds:F3}, " +
                    $"ruleMs={candidate.Timings.RuleEvaluation.TotalMilliseconds:F3}");
            }
            Assert(first.Status != AivPlacementStatus.NotEvaluable,
                $"production worker returned {first.FailureKind}: {first.FailureMessage}");
            Equal(LobbyEvaluationCacheDisposition.ResultCacheHit,
                second.Candidates[0].CacheDisposition);
            Console.WriteLine("PASS local production-worker integration and result-cache hit");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"FAIL local integration: {ex}");
            return 1;
        }
    }

    private static void MapsPlayerToKeepSlot()
    {
        using Fixture fixture = new();
        AivPlacementCheckRequest request = fixture.Build(keepOrder: [-1, -1, 1, -1, -1, -1, -1, -1]);
        Equal(2, request.KeepSlotIndex);
        Equal(AivRotation.Degrees0, request.InitialRotation);
        Assert(!request.UsesMapFacingRotation, "explicit South rotation treated as map-facing");
        Assert(request.IsReady, request.FailureKind.ToString());
    }

    private static void MapsLobbyRotationValues()
    {
        using Fixture fixture = new();
        AivPlacementCheckRequest mapFacing = fixture.Build(slot: Slot(rotationIndex: 0));
        Assert(mapFacing.UsesMapFacingRotation, "NoRot was not preserved as map-facing");
        Equal(AivRotation.Degrees0, fixture.Build(slot: Slot(rotationIndex: 1)).InitialRotation);
        Equal(AivRotation.Degrees90, fixture.Build(slot: Slot(rotationIndex: 2)).InitialRotation);
        Equal(AivRotation.Degrees180, fixture.Build(slot: Slot(rotationIndex: 3)).InitialRotation);
        Equal(AivRotation.Degrees270, fixture.Build(slot: Slot(rotationIndex: 4)).InitialRotation);
    }

    private static void ResolvesMapFacingRotations()
    {
        Equal(AivRotation.Degrees270,
            AivInitialRotationResolver.ResolveMapFacing(new MapCoordinate(338, 419)));
        Equal(AivRotation.Degrees180,
            AivInitialRotationResolver.ResolveMapFacing(new MapCoordinate(433, 373)));
        Equal(AivRotation.Degrees0,
            AivInitialRotationResolver.ResolveMapFacing(new MapCoordinate(386, 467)));
        Equal(AivRotation.Degrees90,
            AivInitialRotationResolver.ResolveMapFacing(new MapCoordinate(481, 369)));
    }

    private static void TracksRetainedStartsInNativePlayerOrder()
    {
        using Fixture fixture = new();
        LobbyStateCapture capture = fixture.Capture(
            keepOrder: [1, 0, 2, -1, -1, -1, -1, -1],
            slots: [Slot(playerId: 2), Slot(playerId: 3)],
            humanPlayerIds: [1]);

        AivPlacementRequestBatch batch = new LobbyRequestBuilder()
            .Build(1, capture, fixture.VanillaDirectory);

        Equal(2, batch.Requests.Count);
        Equal(1, batch.Requests[0].RetainedStartSlotIndexes.Count);
        Equal(1, batch.Requests[0].RetainedStartSlotIndexes[0]);
        Equal(1 << 1, batch.Requests[0].RetainedStartSlotMask);
        Equal(2, batch.Requests[1].RetainedStartSlotIndexes.Count);
        Equal(0, batch.Requests[1].RetainedStartSlotIndexes[0]);
        Equal(1, batch.Requests[1].RetainedStartSlotIndexes[1]);
        Equal((1 << 0) | (1 << 1), batch.Requests[1].RetainedStartSlotMask);
    }

    private static void EvaluatesAiStartsSequentially()
    {
        using Fixture fixture = new();
        File.WriteAllText(Path.Combine(fixture.CustomDirectory, "first.aivjson"), "{}");
        File.WriteAllText(Path.Combine(fixture.CustomDirectory, "second.aivjson"), "{}");
        LobbyStateCapture capture = fixture.Capture(
            keepOrder: [1, 2, -1, -1, -1, -1, -1, -1],
            slots:
            [
                Slot(LobbyAivMode.Custom,
                    [new LobbyAivCandidateInput("first", fixture.CustomDirectory, 0, false, "SK_RAT")],
                    playerId: 2),
                Slot(LobbyAivMode.Custom,
                    [new LobbyAivCandidateInput("second", fixture.CustomDirectory, 0, false, "SK_RAT")],
                    playerId: 3)
            ]);
        AivPlacementRequestBatch batch = new LobbyRequestBuilder()
            .Build(1, capture, fixture.VanillaDirectory);
        var worker = new SequentialStateWorker();
        var service = new AivPlacementEvaluationService(worker, 32, 2);

        AivPlacementBatchResult batchResult = service
            .EvaluateBatchAsync(batch)
            .GetAwaiter()
            .GetResult();
        IReadOnlyList<AivPlacementCheckResult> results = batchResult.Results;

        Equal(2, results.Count);
        Equal(0, worker.RebuiltStates[0].Count);
        Equal(1, worker.RebuiltStates[1].Count);
        Equal(AivRotation.Degrees0, worker.RebuiltStates[1][0]);
    }

    private static void ReusesTieChoiceForSequentialStarts()
    {
        using Fixture fixture = new();
        foreach (string name in new[] { "first", "rotated", "second" })
            File.WriteAllText(Path.Combine(fixture.CustomDirectory, name + ".aivjson"), "{}");
        LobbyStateCapture capture = fixture.Capture(
            keepOrder: [1, 2, -1, -1, -1, -1, -1, -1],
            slots:
            [
                Slot(LobbyAivMode.Custom,
                    [
                        new LobbyAivCandidateInput("first", fixture.CustomDirectory, 0, false, "SK_RAT"),
                        new LobbyAivCandidateInput("rotated", fixture.CustomDirectory, 0, false, "SK_RAT")
                    ],
                    playerId: 2),
                Slot(LobbyAivMode.Custom,
                    [new LobbyAivCandidateInput("second", fixture.CustomDirectory, 0, false, "SK_RAT")],
                    playerId: 3)
            ]);
        AivPlacementRequestBatch batch = new LobbyRequestBuilder()
            .Build(1, capture, fixture.VanillaDirectory);
        var worker = new SequentialStateWorker();
        var service = new AivPlacementEvaluationService(worker, 32, 2);

        AivPlacementBatchResult result = service
            .EvaluateBatchAsync(batch, null, check => check.PlayerId == 2 ? 1 : null)
            .GetAwaiter()
            .GetResult();

        Equal(1, result.SelectedCandidateIdsByPlayer[2]);
        Equal(AivRotation.Degrees90, worker.StatesByCandidate["second"][0]);
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

    private static void MapsCurrentLordNamesToVanillaFiles()
    {
        using Fixture fixture = new();
        foreach ((string lord, string stem) in new[]
        {
            ("SK_CROCODILE", "croc"),
            ("SK_DLC4A", "surgeon"),
            ("SK_DLC4B", "baibars")
        })
        {
            for (int index = 1; index <= 8; index++)
                File.WriteAllText(Path.Combine(fixture.VanillaDirectory, stem + index + ".aivjson"), "{}");

            AivPlacementCheckRequest request = fixture.Build(
                slot: new LobbyAiSlotInput(
                    2,
                    0,
                    lord,
                    string.Empty,
                    LobbyAivMode.Default,
                    1,
                    Array.Empty<LobbyAivCandidateInput>()));
            Assert(request.IsReady, lord + ": " + request.FailureKind);
            Assert(request.Candidates[0].Source.EndsWith(stem + "1.aivjson"), request.Candidates[0].Source);
        }
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

    private static void LeavesMultiplayerSelectionToVanilla()
    {
        string root = FindCastlePlannerRoot();
        string source = File.ReadAllText(Path.Combine(
            root, "src", "AIVPlacement", "AivPlacementRuntime.cs"));
        string[] forbidden =
        {
            "SelectNetworkStartAivs",
            "NetworkAivSnapshot",
            "selectedNetworkCandidateIds",
            "info.aivs.Clear()",
            "info.aivs.Add(selected)"
        };
        foreach (string symbol in forbidden)
            Assert(!source.Contains(symbol, StringComparison.Ordinal),
                $"runtime still contains multiplayer selection mutation '{symbol}'");
    }

    private static void PublishesStatusWithoutDependency()
    {
        string root = FindCastlePlannerRoot();
        string project = File.ReadAllText(Path.Combine(root, "CastlePlanner.csproj"));
        string bridge = File.ReadAllText(Path.Combine(
            root, "src", "AIVPlacement", "BugfixAivStatusBridge.cs"));
        Assert(!project.Contains("Reference Include=\"BugfixesAndQoL\"", StringComparison.Ordinal),
            "CastlePlanner has a hard BugfixesAndQoL assembly reference");
        Assert(bridge.Contains("GetAssemblies()", StringComparison.Ordinal) &&
            bridge.Contains("ReplaceStatuses", StringComparison.Ordinal),
            "optional reflection status bridge is missing");
    }

    private static string FindCastlePlannerRoot()
    {
        DirectoryInfo current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "CastlePlanner.csproj")))
                return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("CastlePlanner project root was not found.");
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

    private static void LoadsInMemoryAivJsonFromSharedCore()
    {
        AivJsonLoadResult loaded = AivJsonFileLoader.LoadText(
            "{/*asset*/\"pauseDelayAmount\":100,\"frames\":[" +
            "{\"itemType\":61,\"tilePositionOfsets\":[5044,],\"shouldPause\":false,}," +
            "],\"miscItems\":[],}",
            "AIV/SK_RAT_0.aivjson");

        Assert(loaded.Document != null, "in-memory Core loader returned no document");
        Equal(0, loaded.Diagnostics.Count);
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

    private static void ThrottlesUnchangedLobbyCaptures()
    {
        var gate = new LobbyCapturePollGate(10);

        Assert(gate.ShouldCapture(100, false), "initial capture was throttled");
        Assert(!gate.ShouldCapture(101, false), "unchanged frame was captured");
        Assert(!gate.ShouldCapture(109, false), "poll interval ended too early");
        Assert(gate.ShouldCapture(110, false), "safety poll did not run");
        gate.Invalidate();
        Assert(gate.ShouldCapture(111, false), "known lobby mutation was delayed");
        Assert(gate.ShouldCapture(112, true), "forced start capture was throttled");
    }

    private static void CancelsSupersededGenerationWork()
    {
        using Fixture fixture = new();
        LobbyStateCapture capture = fixture.Capture();
        var builder = new LobbyRequestBuilder();
        AivPlacementRequestBatch firstBatch = builder.Build(1, capture, fixture.VanillaDirectory);
        AivPlacementRequestBatch secondBatch = builder.Build(2, capture, fixture.VanillaDirectory);
        var worker = new GenerationCancellationWorker();
        var service = new AivPlacementEvaluationService(worker, 16, 2);
        using var firstCancellation = new CancellationTokenSource();
        using var secondCancellation = new CancellationTokenSource();

        Task<AivPlacementBatchResult> first = service.EvaluateBatchAsync(
            firstBatch,
            null,
            null,
            firstCancellation.Token);
        Assert(worker.FirstGenerationStarted.Wait(TimeSpan.FromSeconds(2)),
            "first generation did not start");
        firstCancellation.Cancel();
        AivPlacementBatchResult second = service.EvaluateBatchAsync(
                secondBatch,
                null,
                null,
                secondCancellation.Token)
            .GetAwaiter()
            .GetResult();

        bool canceled = false;
        try
        {
            first.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            canceled = true;
        }

        Assert(canceled, "superseded generation completed instead of cancelling");
        Equal(2L, second.Results.Single().Generation);
        Assert(worker.FirstGenerationCallCount >= 1 && worker.FirstGenerationCallCount < 8,
            "superseded generation did not stop queued candidate work");
        Equal(8, worker.SecondGenerationCallCount);
    }

    private static void ClassifiesEvaluationLogs()
    {
        using Fixture fixture = new();
        var service = new AivPlacementEvaluationService(new CountingWorker(), 16, 1);
        AivPlacementCheckResult preBuild = service.EvaluateAsync(
                fixture.Build(preBuild: 1))
            .GetAwaiter()
            .GetResult();
        AivPlacementCheckResult incompleteLobby = service.EvaluateAsync(
                fixture.Build(keepOrder: Enumerable.Repeat(-1, 8).ToArray()))
            .GetAwaiter()
            .GetResult();
        AivPlacementCheckResult parseFailure = service.EvaluateAsync(
                fixture.BuildSingleCustom("parse-failure"))
            .GetAwaiter()
            .GetResult();

        Equal(LobbyEvaluationLogSeverity.None, LobbyEvaluationLogPolicy.Classify(preBuild));
        Equal(LobbyEvaluationLogSeverity.None, LobbyEvaluationLogPolicy.Classify(incompleteLobby));
        Equal(LobbyEvaluationLogSeverity.Warning, LobbyEvaluationLogPolicy.Classify(parseFailure));

        AivPlacementCheckRequest request = fixture.BuildSingleCustom("throwing-worker");
        var failingService = new AivPlacementEvaluationService(new ThrowingWorker(), 16, 1);
        AivPlacementCheckResult workerFailure = failingService.EvaluateAsync(request)
            .GetAwaiter()
            .GetResult();
        Equal(LobbyEvaluationLogSeverity.Error, LobbyEvaluationLogPolicy.Classify(workerFailure));
    }

    private static void CachesNotEvaluableResults()
    {
        using Fixture fixture = new();
        AivPlacementCheckRequest request = fixture.BuildSingleCustom("cached");
        var worker = new CountingWorker();
        var service = new AivPlacementEvaluationService(worker, 16, 1);

        AivPlacementCheckResult first = service.EvaluateAsync(request).GetAwaiter().GetResult();
        AivPlacementCheckResult second = service.EvaluateAsync(request).GetAwaiter().GetResult();

        Equal(1, worker.CallCount);
        Equal(AivPlacementStatus.NotEvaluable, first.Status);
        Equal(LobbyEvaluationCacheDisposition.Computed, first.Candidates[0].CacheDisposition);
        Equal(LobbyEvaluationCacheDisposition.ResultCacheHit, second.Candidates[0].CacheDisposition);
        Assert(worker.ThreadIds.All(value => value != Environment.CurrentManagedThreadId),
            "worker ran on the calling thread");
    }

    private static void CoalescesConcurrentRequests()
    {
        using Fixture fixture = new();
        AivPlacementCheckRequest request = fixture.BuildSingleCustom("shared");
        var worker = new CountingWorker(150);
        var service = new AivPlacementEvaluationService(worker, 16, 2);

        Task<AivPlacementCheckResult> first = service.EvaluateAsync(request);
        Task<AivPlacementCheckResult> second = service.EvaluateAsync(request);
        Assert(worker.Started.Wait(TimeSpan.FromSeconds(2)), "worker did not start");
        Assert(!first.IsCompleted || !second.IsCompleted,
            "both asynchronous requests completed before the delayed worker was released");
        Task.WaitAll(first, second);

        Equal(1, worker.CallCount);
        var dispositions = new[]
        {
            first.Result.Candidates[0].CacheDisposition,
            second.Result.Candidates[0].CacheDisposition
        };
        Assert(dispositions.Contains(LobbyEvaluationCacheDisposition.Computed),
            "no request owned the computation");
        Assert(dispositions.Contains(LobbyEvaluationCacheDisposition.SharedInFlight),
            "duplicate request did not share the in-flight computation");
    }

    private static void InvalidatesChangedFiles()
    {
        using Fixture fixture = new();
        AivPlacementCheckRequest request = fixture.BuildSingleCustom("changed");
        var worker = new CountingWorker();
        var service = new AivPlacementEvaluationService(worker, 16, 1);

        service.EvaluateAsync(request).GetAwaiter().GetResult();
        File.AppendAllText(request.Candidates[0].Source, "a");
        service.EvaluateAsync(request).GetAwaiter().GetResult();
        File.AppendAllText(request.MapPath, "m");
        service.EvaluateAsync(request).GetAwaiter().GetResult();

        Equal(3, worker.CallCount);
    }

    private static void InvalidatesRetainedStarts()
    {
        using Fixture fixture = new();
        File.WriteAllText(Path.Combine(fixture.CustomDirectory, "retained.aivjson"), "{}");
        LobbyAiSlotInput slot = Slot(
            LobbyAivMode.Custom,
            [new LobbyAivCandidateInput(
                "retained",
                fixture.CustomDirectory,
                0,
                false,
                "SK_RAT")]);
        int[] keepOrder = [-1, 0, 1, -1, -1, -1, -1, -1];
        LobbyStateCapture withoutHuman = fixture.Capture(
            keepOrder: keepOrder,
            slots: [slot]);
        LobbyStateCapture withHuman = fixture.Capture(
            keepOrder: keepOrder,
            slots: [slot],
            humanPlayerIds: [1]);
        AivPlacementCheckRequest first = new LobbyRequestBuilder()
            .Build(1, withoutHuman, fixture.VanillaDirectory).Requests.Single();
        AivPlacementCheckRequest second = new LobbyRequestBuilder()
            .Build(2, withHuman, fixture.VanillaDirectory).Requests.Single();
        var worker = new CountingWorker();
        var service = new AivPlacementEvaluationService(worker, 16, 1);

        service.EvaluateAsync(first).GetAwaiter().GetResult();
        service.EvaluateAsync(second).GetAwaiter().GetResult();

        Equal(2, worker.CallCount);
    }

    private static void BoundsResultCache()
    {
        using Fixture fixture = new();
        var worker = new CountingWorker();
        var service = new AivPlacementEvaluationService(worker, 2, 1);
        AivPlacementCheckRequest first = fixture.BuildSingleCustom("one");
        AivPlacementCheckRequest second = fixture.BuildSingleCustom("two");
        AivPlacementCheckRequest third = fixture.BuildSingleCustom("three");

        service.EvaluateAsync(first).GetAwaiter().GetResult();
        service.EvaluateAsync(second).GetAwaiter().GetResult();
        service.EvaluateAsync(third).GetAwaiter().GetResult();
        service.EvaluateAsync(first).GetAwaiter().GetResult();

        Equal(4, worker.CallCount);
    }

    private static void FingerprintsSourceChanges()
    {
        using Fixture fixture = new();
        AivPlacementCheckRequest request = fixture.BuildSingleCustom("fingerprint");
        AivPlacementRequestBatch batch = new LobbyRequestBuilder().Build(
            1,
            fixture.Capture(slots: [Slot(
                LobbyAivMode.Custom,
                [new LobbyAivCandidateInput(
                    "fingerprint",
                    fixture.CustomDirectory,
                    0,
                    false,
                    "SK_RAT")])]),
            fixture.VanillaDirectory);
        string first = AivPlacementEvaluationService.BuildSourceFingerprint(batch);
        File.AppendAllText(request.Candidates[0].Source, "a");
        string second = AivPlacementEvaluationService.BuildSourceFingerprint(batch);
        File.AppendAllText(request.MapPath, "m");
        string third = AivPlacementEvaluationService.BuildSourceFingerprint(batch);

        Assert(!string.Equals(first, second, StringComparison.Ordinal),
            "AIV modification did not change the source fingerprint");
        Assert(!string.Equals(second, third, StringComparison.Ordinal),
            "map modification did not change the source fingerprint");
    }

    private static void KeepsPrebuildOutOfWorker()
    {
        using Fixture fixture = new();
        AivPlacementCheckRequest request = fixture.Build(preBuild: 1);
        var worker = new CountingWorker();
        var service = new AivPlacementEvaluationService(worker, 16, 1);

        AivPlacementCheckResult result = service.EvaluateAsync(request).GetAwaiter().GetResult();

        Equal(0, worker.CallCount);
        Equal(AivPlacementStatus.NotEvaluable, result.Status);
        Equal(LobbyEvaluationFailureKind.RequestNotReady, result.FailureKind);
    }

    private static void KeepsClientEvaluationHostOnly()
    {
        using Fixture fixture = new();
        LobbyStateCapture capture = fixture.Capture(isHost: false);
        AivPlacementCheckRequest request = new LobbyRequestBuilder()
            .Build(1, capture, fixture.VanillaDirectory).Requests.Single();

        Equal(LobbyRequestFailureKind.ClientEvaluationNotRequired, request.FailureKind);
        Equal(AivPlacementStatus.NotEvaluable, request.ImmediateResultStatus);
    }

    private static void AggregatesEveryCandidate()
    {
        using Fixture fixture = new();
        AivPlacementCheckRequest request = fixture.BuildCustomList(
            "impossible",
            "partial",
            "complete");
        var worker = new SelectionWorker();
        var service = new AivPlacementEvaluationService(worker, 16, 2);

        AivPlacementCheckResult result = service.EvaluateAsync(request).GetAwaiter().GetResult();

        Equal(3, worker.CallCount);
        Equal(AivPlacementStatus.Complete, result.Status);
        Equal(2, result.SelectedCandidate.CandidateId);
        Equal(3, result.Candidates.Count);
    }

    private static void PreservesCompleteTieOrder()
    {
        using Fixture fixture = new();
        AivPlacementCheckRequest request = fixture.BuildCustomList(
            "complete-first",
            "complete-second");
        var service = new AivPlacementEvaluationService(new SelectionWorker(), 16, 2);

        AivPlacementCheckResult result = service.EvaluateAsync(request).GetAwaiter().GetResult();

        Equal(AivPlacementStatus.Complete, result.Status);
        Equal(0, result.SelectedCandidate.CandidateId);
    }

    private static void SelectsBestSequentialPartial()
    {
        using Fixture fixture = new();
        AivPlacementCheckRequest request = fixture.BuildCustomList(
            "partial-low",
            "partial-high");
        var service = new AivPlacementEvaluationService(new SelectionWorker(), 16, 2);

        AivPlacementCheckResult result = service.EvaluateAsync(request).GetAwaiter().GetResult();

        Equal(AivPlacementStatus.Partial, result.Status);
        Equal(1, result.SelectedCandidate.CandidateId);
        Equal(8, result.SelectedVariant.Score.SequentialBuildScore);
    }

    private static void FindsEveryCompleteTie()
    {
        using Fixture fixture = new();
        AivPlacementCheckRequest request = fixture.BuildCustomList(
            "complete-first",
            "complete-second");
        var service = new AivPlacementEvaluationService(new SelectionWorker(), 16, 2);

        AivPlacementCheckResult result = service.EvaluateAsync(request).GetAwaiter().GetResult();
        IReadOnlyList<int> eligible = BestFitCandidateSelector.GetEligibleCandidateIds(result);

        Equal(2, eligible.Count);
        Equal(0, eligible[0]);
        Equal(1, eligible[1]);
    }

    private static void FindsEveryHighestPartialScoreTie()
    {
        using Fixture fixture = new();
        AivPlacementCheckRequest request = fixture.BuildCustomList(
            "partial-low",
            "partial-high",
            "partial-copy-high",
            "partial-high-long");
        var service = new AivPlacementEvaluationService(new SelectionWorker(), 16, 2);

        AivPlacementCheckResult result = service.EvaluateAsync(request).GetAwaiter().GetResult();
        IReadOnlyList<int> eligible = BestFitCandidateSelector.GetEligibleCandidateIds(result);

        Equal(3, eligible.Count);
        Equal(1, eligible[0]);
        Equal(2, eligible[1]);
        Equal(3, eligible[2]);
    }

    private static LobbyAiSlotInput Slot(
        LobbyAivMode mode = LobbyAivMode.Default,
        IEnumerable<LobbyAivCandidateInput> candidates = null,
        int playerId = 2,
        int rotationIndex = 1) =>
        new(playerId, 0, "SK_RAT", "", mode, rotationIndex, candidates ?? []);

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
            Root = Path.Combine(Path.GetTempPath(), "CastlePlannerAIVPlacementTests", Guid.NewGuid().ToString("N"));
            VanillaDirectory = Directory.CreateDirectory(Path.Combine(Root, "VanillaAIV")).FullName;
            CustomDirectory = Directory.CreateDirectory(Path.Combine(Root, "CustomAIV")).FullName;
            MapPath = Path.Combine(Root, "test.map");
            File.WriteAllText(MapPath, "synthetic");
            for (int index = 1; index <= 8; index++)
                File.WriteAllText(Path.Combine(VanillaDirectory, $"rat{index}.aivjson"), "{}");
        }

        public string Root { get; }
        public string VanillaDirectory { get; }
        public string CustomDirectory { get; }
        public string MapPath { get; }

        public LobbyStateCapture Capture(
            int preBuild = 0,
            bool isHost = true,
            string mapPath = null,
            int[] keepOrder = null,
            IList<LobbyAiSlotInput> slots = null,
            IEnumerable<string> assets = null,
            IEnumerable<int> humanPlayerIds = null) =>
            new(
                mapPath ?? MapPath,
                "Synthetic",
                "Synthetic",
                isHost,
                preBuild,
                keepOrder ?? [-1, -1, 1, -1, -1, -1, -1, -1],
                slots ?? [Slot()],
                assets ?? [],
                humanPlayerIds ?? []);

        public AivPlacementCheckRequest Build(
            int preBuild = 0,
            string mapPath = null,
            int[] keepOrder = null,
            LobbyAiSlotInput slot = null,
            IEnumerable<string> assets = null) =>
            new LobbyRequestBuilder()
                .Build(1, Capture(preBuild, true, mapPath, keepOrder,
                    new List<LobbyAiSlotInput> { slot ?? Slot() }, assets), VanillaDirectory)
                .Requests.Single();

        public AivPlacementCheckRequest BuildSingleCustom(string name)
        {
            File.WriteAllText(Path.Combine(CustomDirectory, name + ".aivjson"), "{}");
            return Build(slot: Slot(
                LobbyAivMode.Custom,
                [new LobbyAivCandidateInput(name, CustomDirectory, 0, false, "SK_RAT")]));
        }

        public AivPlacementCheckRequest BuildCustomList(params string[] names)
        {
            var candidates = new List<LobbyAivCandidateInput>();
            foreach (string name in names)
            {
                File.WriteAllText(Path.Combine(CustomDirectory, name + ".aivjson"), "{}");
                candidates.Add(new LobbyAivCandidateInput(
                    name,
                    CustomDirectory,
                    0,
                    false,
                    "SK_RAT"));
            }
            return Build(slot: Slot(LobbyAivMode.Custom, candidates));
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, true);
        }
    }

    private sealed class CountingWorker : ILobbyPlacementCandidateWorker
    {
        private readonly int delayMilliseconds;
        private int callCount;

        public CountingWorker(int delayMilliseconds = 0)
        {
            this.delayMilliseconds = delayMilliseconds;
        }

        public int CallCount => Volatile.Read(ref callCount);
        public ConcurrentBag<int> ThreadIds { get; } = new();
        public ManualResetEventSlim Started { get; } = new(false);

        public LobbyPlacementWorkerResult Evaluate(
            AivPlacementCandidateWorkItem workItem,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref callCount);
            ThreadIds.Add(Environment.CurrentManagedThreadId);
            Started.Set();
            if (delayMilliseconds > 0)
                Thread.Sleep(delayMilliseconds);
            return LobbyPlacementWorkerResult.NotEvaluable(
                LobbyEvaluationFailureKind.AivParseFailed,
                "synthetic worker result");
        }
    }

    private sealed class SelectionWorker : ILobbyPlacementCandidateWorker
    {
        private int callCount;

        public int CallCount => Volatile.Read(ref callCount);

        public LobbyPlacementWorkerResult Evaluate(
            AivPlacementCandidateWorkItem workItem,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref callCount);
            bool partial = workItem.Candidate.Name.StartsWith(
                "partial",
                StringComparison.Ordinal);
            bool impossible = workItem.Candidate.Name.StartsWith(
                "impossible",
                StringComparison.Ordinal);
            int frameCount = partial && workItem.Candidate.Name.EndsWith(
                "long",
                StringComparison.Ordinal)
                    ? 20
                    : partial ? 10 : 1;
            var frames = new List<AivBuildFrame>();
            for (int index = 0; index < frameCount; index++)
            {
                frames.Add(new AivBuildFrame(
                    index,
                    25,
                    AivMapperCatalog.Resolve(25),
                    false,
                    [new AivGridPoint(40, 60 + index)]));
            }

            var blueprint = new AivBlueprint(
                workItem.Candidate.Name,
                5,
                frames,
                Array.Empty<AivMiscPlacement>(),
                new AivGridPoint(50, 50));
            var map = new SparsePlacementMap();
            if (partial || impossible)
            {
                var projector = new AivCastleProjector();
                foreach (AivRotation rotation in new[]
                {
                    AivRotation.Degrees0,
                    AivRotation.Degrees90,
                    AivRotation.Degrees180,
                    AivRotation.Degrees270
                })
                {
                    AivProjectedCastle castle = projector.Project(
                        blueprint,
                        new MapCoordinate(400, 400),
                        rotation);
                    int blockedIndex = impossible
                        ? 0
                        : workItem.Candidate.Name.Contains("high", StringComparison.Ordinal)
                            ? 8
                            : 2;
                    map.Set(
                        castle.Elements[blockedIndex].MapCoordinate,
                        new AivPlacementTileEvidence(0, 0, 0, 0, 0, 1, 0, 0));
                }
            }

            AivPlacementRotationSelection selection = new AivPlacementEvaluator()
                .EvaluateAllRotations(
                    map,
                    blueprint,
                    new MapCoordinate(400, 400),
                    AivRotation.Degrees90);
            return new LobbyPlacementWorkerResult(
                selection,
                LobbyEvaluationFailureKind.None,
                string.Empty,
                LobbyPlacementPhaseTimings.Empty);
        }
    }

    private sealed class SequentialStateWorker : ILobbyPlacementCandidateWorker
    {
        private readonly object sync = new();

        public List<Dictionary<int, AivRotation>> RebuiltStates { get; } = new();
        public Dictionary<string, Dictionary<int, AivRotation>> StatesByCandidate { get; } = new();

        public LobbyPlacementWorkerResult Evaluate(
            AivPlacementCandidateWorkItem workItem,
            CancellationToken cancellationToken)
        {
            lock (sync)
            {
                var state = new Dictionary<int, AivRotation>(
                    workItem.RebuiltStartRotationsBySlot);
                RebuiltStates.Add(state);
                StatesByCandidate[workItem.Candidate.Name] = state;
            }

            var blueprint = new AivBlueprint(
                workItem.Candidate.Name,
                5,
                [new AivBuildFrame(
                    0,
                    25,
                    AivMapperCatalog.Resolve(25),
                    false,
                    [new AivGridPoint(50, 50)])],
                Array.Empty<AivMiscPlacement>(),
                new AivGridPoint(50, 50));
            AivPlacementRotationSelection selection = new AivPlacementEvaluator()
                .EvaluateAllRotations(
                    new SparsePlacementMap(),
                    blueprint,
                    new MapCoordinate(400, 400),
                    workItem.Candidate.Name == "rotated"
                        ? AivRotation.Degrees90
                        : AivRotation.Degrees0);
            return new LobbyPlacementWorkerResult(
                selection,
                LobbyEvaluationFailureKind.None,
                string.Empty,
                LobbyPlacementPhaseTimings.Empty);
        }
    }

    private sealed class GenerationCancellationWorker : ILobbyPlacementCandidateWorker
    {
        private int firstGenerationCallCount;
        private int secondGenerationCallCount;

        public int FirstGenerationCallCount => Volatile.Read(ref firstGenerationCallCount);
        public int SecondGenerationCallCount => Volatile.Read(ref secondGenerationCallCount);
        public ManualResetEventSlim FirstGenerationStarted { get; } = new(false);

        public LobbyPlacementWorkerResult Evaluate(
            AivPlacementCandidateWorkItem workItem,
            CancellationToken cancellationToken)
        {
            if (workItem.Request.Generation == 1)
            {
                Interlocked.Increment(ref firstGenerationCallCount);
                FirstGenerationStarted.Set();
                cancellationToken.WaitHandle.WaitOne();
                cancellationToken.ThrowIfCancellationRequested();
            }
            else
            {
                Interlocked.Increment(ref secondGenerationCallCount);
            }

            return LobbyPlacementWorkerResult.NotEvaluable(
                LobbyEvaluationFailureKind.AivParseFailed,
                "synthetic current-generation result");
        }
    }

    private sealed class ThrowingWorker : ILobbyPlacementCandidateWorker
    {
        public LobbyPlacementWorkerResult Evaluate(
            AivPlacementCandidateWorkItem workItem,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("synthetic unexpected worker failure");
    }

    private sealed class SparsePlacementMap : IAivPlacementTileSource
    {
        private readonly Dictionary<int, AivPlacementTileEvidence> evidence = new();

        public MapTileGeometry Geometry { get; } =
            new(MapTileGeometry.FixedTileCount, 400);

        public AivPlacementTileEvidence GetTileEvidence(int tileId) =>
            evidence.TryGetValue(tileId, out AivPlacementTileEvidence value)
                ? value
                : default;

        public void Set(MapCoordinate coordinate, AivPlacementTileEvidence value)
        {
            evidence[Geometry.GetTileId(coordinate.X, coordinate.Y)] = value;
        }
    }
}
