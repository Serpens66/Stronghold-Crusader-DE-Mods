using AIVParser.Core;
using AIVPlacement.Core;
using CastlePlanner.AIVPlacement.Core;
using MapParser.Core;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System.Collections.Concurrent;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length == 3 && string.Equals(args[0], "--integration", StringComparison.Ordinal))
            return RunLocalIntegration(args[1], args[2]);
        if (args.Length >= 2 && string.Equals(args[0], "--validate-spawn-aiv", StringComparison.Ordinal))
            return ValidateSpawnAivFiles(args.Skip(1));

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
            ("randomizes every highest partial score tie", FindsEveryHighestPartialScoreTie),
            ("roundtrips strict native AIV spawn data", RoundtripsStrictNativeSpawnData),
            ("roundtrips Vanilla no-op AIV frames", RoundtripsVanillaNoOpFrames),
            ("accepts native Int16 AIV frame counts", AcceptsNativeInt16FrameCounts),
            ("resolves LordJSON flag types deterministically", ResolvesLordJsonFlagTypesDeterministically),
            ("rejects malformed AIVJSON spawn data", RejectsMalformedAivJsonSpawnData),
            ("roundtrips known working AIVJSON files", RoundtripsKnownWorkingAivJsonFiles),
            ("rejects malformed native AIV spawn data", RejectsMalformedNativeSpawnData),
            ("filters every AIV spawn frame category", FiltersEverySpawnFrameCategory),
            ("filters Blueprint castle choices case-insensitively", FiltersBlueprintCastleChoices),
            ("formats compact AIVJSON display names", FormatsCompactAivDisplayNames),
            ("disambiguates compact AIVJSON display names", DisambiguatesCompactAivDisplayNames),
            ("filters troops and maps only siege engines", FiltersTroopsAndMapsOnlySiegeEngines),
            ("maps braziers and owner-specific flags", MapsBraziersAndOwnerSpecificFlags),
            ("projects supplemental items for every rotation", ProjectsSupplementalItemsForEveryRotation),
            ("keeps supplemental items on the native reference anchor", KeepsSupplementalItemsOnNativeReferenceAnchor),
            ("converts decoration tiles to projectile coordinates", ConvertsDecorationTilesToProjectileCoordinates),
            ("aligns native rotation to the live Keep footprint", AlignsNativeRotationToLiveKeepFootprint),
            ("resolves rotated BuildStructure origins", ResolvesRotatedBuildStructureOrigins),
            ("preserves compound storage placement order", PreservesCompoundStoragePlacementOrder),
            ("pins CastlePlanner to RedBird for Script Extender 2.2.0", PinsCastlePlannerToRedBird220)
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

    private static int ValidateSpawnAivFiles(IEnumerable<string> paths)
    {
        try
        {
            foreach (string path in paths)
            {
                CastlePlanner.AivJsonDocument document = CastlePlanner.AivJsonReader.Parse(
                    File.ReadAllText(path));
                short[] raw = CastlePlanner.AivRawDataEncoder.Encode(document);
                CastlePlanner.AivJsonDocument decoded = CastlePlanner.AivSpawnPlan.Decode(raw);
                ushort flagType = CastlePlanner.AivLordJsonResolver.ResolveFlagProjectileType(
                    path,
                    out string lordPath,
                    out string warning);
                Assert(raw.SequenceEqual(CastlePlanner.AivRawDataEncoder.Encode(decoded)),
                    $"native roundtrip changed '{path}'");
                Console.WriteLine(
                    $"PASS spawn AIV '{path}': frames={document.frames.Count}, " +
                    $"emptyFrames={document.frames.Count(frame => frame != null && (frame.tilePositionOfsets == null || frame.tilePositionOfsets.Count == 0))}, " +
                    $"rawShorts={raw.Length}, flagType={flagType}, lord='{lordPath}', warning='{warning}'");
            }
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"FAIL spawn AIV validation: {ex}");
            return 1;
        }
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

    private static void PinsCastlePlannerToRedBird220()
    {
        string root = FindCastlePlannerRoot();
        string plugin = File.ReadAllText(Path.Combine(root, "src", "CastlePlannerPlugin.cs"));
        string runtime = File.ReadAllText(Path.Combine(root, "src", "CastlePlannerRuntime.cs"));
        string project = File.ReadAllText(Path.Combine(root, "CastlePlanner.csproj"));
        string manifest = File.ReadAllText(Path.Combine(
            root, "BepInEx", "plugins", "CastlePlanner_Serp", "info.json"));
        string settingsXaml = File.ReadAllText(Path.Combine(
            root, "BepInEx", "plugins", "CastlePlanner_Serp", "Override",
            "ScriptExtenderUI", "CastlePlannerSettings.xaml"));

        Assert(plugin.Contains("BepInDependency(ScriptExtenderGuid, \"2.2.0\")", StringComparison.Ordinal),
            "Script Extender dependency is not exact 2.2.0");
        Assert(plugin.Contains("OnCrusaderLibraryLoaded(CrusaderLibraryLoadContext context)", StringComparison.Ordinal) &&
            plugin.Contains("runtime.Install(context, currentNativeLayout)", StringComparison.Ordinal),
            "CastlePlanner does not propagate the 2.2.0 load context");
        Assert(runtime.Contains("new HookHandle<X64InlineHook>()", StringComparison.Ordinal) &&
            runtime.Contains("HookTarget.FromAddress(", StringComparison.Ordinal) &&
            runtime.Contains("new ContextHookOptions", StringComparison.Ordinal),
            "CastlePlanner hook is not registered through a typed explicit RedBird target");
        Assert(runtime.Contains("commitResult.IsCompleteSuccess", StringComparison.Ordinal) &&
            runtime.Contains("humanKeepCoordinateLoadHook.Success", StringComparison.Ordinal),
            "CastlePlanner does not check transaction and handle success");
        Assert(runtime.Contains("OwnsHooks = false", StringComparison.Ordinal),
            "CastlePlanner process-lifetime hook ownership is not explicit");
        Assert(!runtime.Contains("Zhuqiaomon", StringComparison.Ordinal) &&
            !runtime.Contains("HookRef<", StringComparison.Ordinal),
            "CastlePlanner runtime retains a legacy hook API");
        Assert(project.Contains("RedBird.Abstractions.dll", StringComparison.Ordinal) &&
            project.Contains("RedBird.Core.dll", StringComparison.Ordinal) &&
            project.Contains("RedBird.X64.dll", StringComparison.Ordinal) &&
            !project.Contains("Zhuqiaomon.dll", StringComparison.Ordinal) &&
            !project.Contains("PolyHook2.NET.dll", StringComparison.Ordinal) &&
            !project.Contains("Iced.dll", StringComparison.Ordinal),
            "CastlePlanner project references do not match RedBird 2.2.0");
        Assert(manifest.Contains("\"NetworkMode\": 1", StringComparison.Ordinal),
            "CastlePlanner is not classified as gameplay-synchronized");
        Assert(settingsXaml.Contains("HorizontalScrollBarVisibility=\"Auto\"", StringComparison.Ordinal) &&
            settingsXaml.Contains("VerticalScrollBarVisibility=\"Auto\"", StringComparison.Ordinal),
            "CastlePlanner settings are not reachable in both dimensions");

        const int hookRva = 0x95B3C;
        byte[] signature = Convert.FromHexString(
            "4863BCCD540D0000448BA4CD500D0000448BCF458BC46689442420");
        string gameRoot = Environment.GetEnvironmentVariable("CASTLE_PLANNER_GAME_DIR") ??
            @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition";
        byte[] image = File.ReadAllBytes(Path.Combine(
            gameRoot, "Stronghold Crusader Definitive Edition_Data", "Plugins", "x86_64", "CrusaderDE.dll"));
        int rawOffset = RvaToRawOffset(image, hookRva);
        Assert(image.AsSpan(rawOffset, signature.Length).SequenceEqual(signature),
            "human Keep hook signature differs at audited RVA");
        int matches = 0;
        for (int offset = 0; offset <= image.Length - signature.Length; offset++)
        {
            if (image.AsSpan(offset, signature.Length).SequenceEqual(signature))
                matches++;
        }
        Equal(1, matches);
    }

    private static int RvaToRawOffset(byte[] image, int rva)
    {
        int peOffset = BitConverter.ToInt32(image, 0x3C);
        int sectionCount = BitConverter.ToUInt16(image, peOffset + 6);
        int optionalHeaderSize = BitConverter.ToUInt16(image, peOffset + 20);
        int sectionTable = peOffset + 24 + optionalHeaderSize;
        for (int index = 0; index < sectionCount; index++)
        {
            int header = sectionTable + index * 40;
            int virtualSize = BitConverter.ToInt32(image, header + 8);
            int virtualAddress = BitConverter.ToInt32(image, header + 12);
            int rawSize = BitConverter.ToInt32(image, header + 16);
            int rawAddress = BitConverter.ToInt32(image, header + 20);
            int length = Math.Max(virtualSize, rawSize);
            if (rva >= virtualAddress && rva < virtualAddress + length)
                return checked(rawAddress + rva - virtualAddress);
        }
        throw new InvalidOperationException($"RVA 0x{rva:X} is not in a PE section.");
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

    private static void RoundtripsStrictNativeSpawnData()
    {
        var document = new CastlePlanner.AivJsonDocument
        {
            pauseDelayAmount = 7,
            frames =
            [
                new CastlePlanner.AivJsonFrame { itemType = 61, tilePositionOfsets = [5643], shouldPause = false },
                new CastlePlanner.AivJsonFrame { itemType = 0, tilePositionOfsets = [], shouldPause = true },
                new CastlePlanner.AivJsonFrame { itemType = 80, tilePositionOfsets = [], shouldPause = false },
                new CastlePlanner.AivJsonFrame { itemType = 25, tilePositionOfsets = [5543, 5544], shouldPause = true },
                new CastlePlanner.AivJsonFrame { itemType = 80, tilePositionOfsets = [5443], shouldPause = false }
            ],
            miscItems =
            [
                new CastlePlanner.AivJsonMiscItem { itemType = 9006, positionOfset = 5343, number = 7 },
                new CastlePlanner.AivJsonMiscItem { itemType = 9006, positionOfset = 5343, number = 3 }
            ]
        };

        short[] encoded = CastlePlanner.AivRawDataEncoder.Encode(document);
        CastlePlanner.AivJsonDocument decoded = CastlePlanner.AivSpawnPlan.Decode(encoded);
        Assert(encoded.SequenceEqual(CastlePlanner.AivRawDataEncoder.Encode(decoded)), "native AIV roundtrip changed data");
        Assert(decoded.frames[1].shouldPause, "no-op pause frame was not decoded");
        Equal(0, decoded.frames[1].itemType);
        Equal(0, decoded.frames[1].tilePositionOfsets.Count);
        Equal(80, decoded.frames[2].itemType);
        Equal(0, decoded.frames[2].tilePositionOfsets.Count);
        Equal(2, decoded.miscItems.Count);
        Equal(6, decoded.miscItems[0].itemType);
        Equal(7, decoded.miscItems[0].number);
        Equal(3, decoded.miscItems[1].number);
    }

    private static void RoundtripsVanillaNoOpFrames()
    {
        CastlePlanner.AivJsonDocument document = CreateStrictSpawnDocument();
        document.frames.Insert(1, new CastlePlanner.AivJsonFrame
        {
            itemType = 0,
            tilePositionOfsets = null,
            shouldPause = false
        });
        document.frames.Insert(2, new CastlePlanner.AivJsonFrame
        {
            itemType = 80,
            tilePositionOfsets = [],
            shouldPause = true
        });
        document.frames.Insert(3, new CastlePlanner.AivJsonFrame
        {
            itemType = 0,
            tilePositionOfsets = [],
            shouldPause = true
        });

        short[] raw = CastlePlanner.AivRawDataEncoder.Encode(document);
        CastlePlanner.AivJsonDocument decoded = CastlePlanner.AivSpawnPlan.Decode(raw);
        Equal(0, decoded.frames[1].itemType);
        Equal(0, decoded.frames[1].tilePositionOfsets.Count);
        Assert(!decoded.frames[1].shouldPause, "unpaused no-op changed");
        Equal(80, decoded.frames[2].itemType);
        Equal(0, decoded.frames[2].tilePositionOfsets.Count);
        Assert(decoded.frames[2].shouldPause, "typed empty pause was lost");
        Equal(0, decoded.frames[3].itemType);
        Equal(0, decoded.frames[3].tilePositionOfsets.Count);
        Assert(decoded.frames[3].shouldPause, "no-op pause was lost");
        Assert(raw.SequenceEqual(CastlePlanner.AivRawDataEncoder.Encode(decoded)),
            "Vanilla no-op native roundtrip changed data");

        CastlePlanner.AivJsonDocument filtered = CastlePlanner.AivSpawnPlan.Filter(
            decoded,
            new CastlePlanner.AivSpawnOptions
            {
                SpawnFortifications = true,
                SpawnBuildings = false
            });
        Assert(filtered.frames.Any(frame => frame.itemType == 0 && frame.shouldPause),
            "content filter removed a no-op frame");
    }

    private static void AcceptsNativeInt16FrameCounts()
    {
        foreach (int frameCount in new[] { 1000, 1025, short.MaxValue })
        {
            CastlePlanner.AivJsonDocument document = CreateStrictSpawnDocument();
            while (document.frames.Count < frameCount)
                document.frames.Add(new CastlePlanner.AivJsonFrame { itemType = 0, tilePositionOfsets = [] });

            short[] raw = CastlePlanner.AivRawDataEncoder.Encode(document);
            Equal(frameCount, CastlePlanner.AivSpawnPlan.Decode(raw).frames.Count);
        }

        CastlePlanner.AivJsonDocument excessive = CreateStrictSpawnDocument();
        while (excessive.frames.Count <= short.MaxValue)
            excessive.frames.Add(new CastlePlanner.AivJsonFrame { itemType = 0, tilePositionOfsets = [] });
        AssertAivJsonRejected(excessive, "positive Int16 range");

        CastlePlanner.AivJsonDocument unrepresentablePause = CreateStrictSpawnDocument();
        while (unrepresentablePause.frames.Count < short.MaxValue)
            unrepresentablePause.frames.Add(new CastlePlanner.AivJsonFrame { itemType = 0, tilePositionOfsets = [] });
        unrepresentablePause.frames[short.MaxValue - 1].shouldPause = true;
        AssertAivJsonRejected(unrepresentablePause, "pause index");
    }

    private static void ResolvesLordJsonFlagTypesDeterministically()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "CastlePlannerLordResolverTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string prefixDirectory = Path.Combine(root, "prefix");
            Directory.CreateDirectory(prefixDirectory);
            string plagueAiv = Path.Combine(prefixDirectory, "Plague Doctor3.aivjson");
            File.WriteAllText(plagueAiv, "{}");
            File.WriteAllText(Path.Combine(prefixDirectory, "Plague.lordjson"), LordJson(9));
            string plagueLord = Path.Combine(prefixDirectory, "Plague Doctor.lordjson");
            File.WriteAllText(plagueLord, LordJson(22));
            Equal((ushort)22, CastlePlanner.AivLordJsonResolver.ResolveFlagProjectileType(
                plagueAiv, out string resolvedLord, out string warning));
            Equal(plagueLord, resolvedLord);
            Equal(string.Empty, warning);

            string singleDirectory = Path.Combine(root, "single");
            Directory.CreateDirectory(singleDirectory);
            string noxAiv = Path.Combine(singleDirectory, "Unrelated.aivjson");
            File.WriteAllText(noxAiv, "{}");
            File.WriteAllText(Path.Combine(singleDirectory, "Nox.lordjson"), LordJson(11));
            Equal((ushort)11, CastlePlanner.AivLordJsonResolver.ResolveFlagProjectileType(
                noxAiv, out _, out warning));
            Equal(string.Empty, warning);

            string ambiguousDirectory = Path.Combine(root, "ambiguous");
            Directory.CreateDirectory(ambiguousDirectory);
            string ambiguousAiv = Path.Combine(ambiguousDirectory, "Castle.aivjson");
            File.WriteAllText(ambiguousAiv, "{}");
            File.WriteAllText(Path.Combine(ambiguousDirectory, "Alpha.lordjson"), LordJson(9));
            File.WriteAllText(Path.Combine(ambiguousDirectory, "Beta.lordjson"), LordJson(22));
            Equal((ushort)13, CastlePlanner.AivLordJsonResolver.ResolveFlagProjectileType(
                ambiguousAiv, out _, out warning));
            Assert(warning.Contains("multiple", StringComparison.OrdinalIgnoreCase),
                "ambiguous LordJSON fallback was not diagnosed");

            string missingDirectory = Path.Combine(root, "missing");
            Directory.CreateDirectory(missingDirectory);
            string missingAiv = Path.Combine(missingDirectory, "Missing.aivjson");
            File.WriteAllText(missingAiv, "{}");
            Equal((ushort)13, CastlePlanner.AivLordJsonResolver.ResolveFlagProjectileType(
                missingAiv, out _, out warning));
            Assert(warning.Contains("no LordJSON", StringComparison.OrdinalIgnoreCase),
                "missing LordJSON fallback was not diagnosed");

            foreach ((string json, ushort expected) in new[]
            {
                (LordJson(0), (ushort)0),
                (LordJson(ushort.MaxValue), ushort.MaxValue),
                (LordJson(-1), (ushort)13),
                (LordJson(ushort.MaxValue + 1), (ushort)13),
                ("{broken", (ushort)13)
            })
            {
                string valueDirectory = Path.Combine(root, Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(valueDirectory);
                string aiv = Path.Combine(valueDirectory, "Value.aivjson");
                File.WriteAllText(aiv, "{}");
                File.WriteAllText(Path.Combine(valueDirectory, "Value.lordjson"), json);
                Equal(expected, CastlePlanner.AivLordJsonResolver.ResolveFlagProjectileType(
                    aiv, out _, out _));
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string LordJson(int flagType) =>
        $"{{\"lord\":{{\"flag_type\":{flagType}}}}}";

    private static void RejectsMalformedAivJsonSpawnData()
    {
        CastlePlanner.AivJsonDocument emptyFrames = CreateStrictSpawnDocument();
        emptyFrames.frames.Clear();
        AssertAivJsonRejected(emptyFrames, "frames array is empty");

        CastlePlanner.AivJsonDocument missingPositions = CreateStrictSpawnDocument();
        missingPositions.frames[1].tilePositionOfsets = null;
        AssertAivJsonRejected(missingPositions, "frames[1].tilePositionOfsets is missing");

        CastlePlanner.AivJsonDocument typedEmptyPositions = CreateStrictSpawnDocument();
        typedEmptyPositions.frames[1].tilePositionOfsets.Clear();
        CastlePlanner.AivRawDataEncoder.Encode(typedEmptyPositions);

        CastlePlanner.AivJsonDocument noOpWithPosition = CreateStrictSpawnDocument();
        noOpWithPosition.frames[1].itemType = 0;
        AssertAivJsonRejected(noOpWithPosition, "itemType 0 must not contain positions");

        CastlePlanner.AivJsonDocument emptyKeep = CreateStrictSpawnDocument();
        emptyKeep.frames[0].tilePositionOfsets.Clear();
        AssertAivJsonRejected(emptyKeep, "must contain exactly one Keep position; found 0");

        CastlePlanner.AivJsonDocument compoundKeep = CreateStrictSpawnDocument();
        compoundKeep.frames[0].tilePositionOfsets.Add(5144);
        AssertAivJsonRejected(compoundKeep, "frames[0].tilePositionOfsets must contain exactly one Keep position");

        CastlePlanner.AivJsonDocument missingKeep = CreateStrictSpawnDocument();
        missingKeep.frames.RemoveAt(0);
        AssertAivJsonRejected(missingKeep, "contains no keep frame");

        CastlePlanner.AivJsonDocument duplicateKeep = CreateStrictSpawnDocument();
        duplicateKeep.frames.Add(new CastlePlanner.AivJsonFrame
        {
            itemType = (int)eMappers.MAPPER_KEEP3,
            tilePositionOfsets = [5144]
        });
        AssertAivJsonRejected(duplicateKeep, "must contain exactly one Keep position; found 2");

        CastlePlanner.AivJsonDocument belowGrid = CreateStrictSpawnDocument();
        belowGrid.frames[1].tilePositionOfsets[0] = -1;
        AssertAivJsonRejected(belowGrid, "frames[1].tilePositionOfsets[0]");

        CastlePlanner.AivJsonDocument aboveGrid = CreateStrictSpawnDocument();
        aboveGrid.frames[1].tilePositionOfsets[0] = 10000;
        AssertAivJsonRejected(aboveGrid, "frames[1].tilePositionOfsets[0]");

        CastlePlanner.AivJsonDocument gridBoundaries = CreateStrictSpawnDocument();
        gridBoundaries.frames[0].tilePositionOfsets[0] = 0;
        gridBoundaries.frames[1].tilePositionOfsets[0] = 9999;
        CastlePlanner.AivRawDataEncoder.Encode(gridBoundaries);

        CastlePlanner.AivJsonDocument tooManyPositions = CreateStrictSpawnDocument();
        tooManyPositions.frames[1].tilePositionOfsets =
            Enumerable.Repeat(4040, short.MaxValue + 1).ToList();
        AssertAivJsonRejected(tooManyPositions, "frames[1].tilePositionOfsets count");
    }

    private static void RoundtripsKnownWorkingAivJsonFiles()
    {
        string vanillaDirectory = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "BepInEx", "plugins", "CastlePlanner_Serp", "VanillaAIV"));
        foreach (string fileName in new[] { "rat1.aivjson", "wolf1.aivjson", "Nizar8.aivjson" })
        {
            string path = Path.Combine(vanillaDirectory, fileName);
            AivJsonLoadResult loaded = AivJsonFileLoader.Load(path);
            Assert(loaded.Document != null, $"known AIVJSON did not load: {path}");
            Assert(
                !loaded.Diagnostics.Any(d => d.Severity == AivDiagnosticSeverity.Error),
                $"known AIVJSON has parser errors: {path}");

            var document = new CastlePlanner.AivJsonDocument
            {
                pauseDelayAmount = loaded.Document.pauseDelayAmount,
                frames = loaded.Document.frames.Select(frame => new CastlePlanner.AivJsonFrame
                {
                    itemType = frame.itemType,
                    tilePositionOfsets = new List<int>(frame.tilePositionOfsets),
                    shouldPause = frame.shouldPause
                }).ToList(),
                miscItems = loaded.Document.miscItems.Select(item => new CastlePlanner.AivJsonMiscItem
                {
                    itemType = item.itemType,
                    positionOfset = item.positionOfset,
                    number = item.number
                }).ToList()
            };

            short[] encoded = CastlePlanner.AivRawDataEncoder.Encode(document);
            CastlePlanner.AivJsonDocument decoded = CastlePlanner.AivSpawnPlan.Decode(encoded);
            Assert(
                encoded.SequenceEqual(CastlePlanner.AivRawDataEncoder.Encode(decoded)),
                $"known AIVJSON native roundtrip changed data: {path}");
        }
    }

    private static CastlePlanner.AivJsonDocument CreateStrictSpawnDocument() => new()
    {
        pauseDelayAmount = 7,
        frames =
        [
            new CastlePlanner.AivJsonFrame
            {
                itemType = (int)eMappers.MAPPER_KEEP2,
                tilePositionOfsets = [5044]
            },
            new CastlePlanner.AivJsonFrame
            {
                itemType = (int)eMappers.MAPPER_HOVEL,
                tilePositionOfsets = [4040]
            }
        ],
        miscItems = []
    };

    private static void AssertAivJsonRejected(
        CastlePlanner.AivJsonDocument document,
        string expectedMessagePart)
    {
        try
        {
            CastlePlanner.AivRawDataEncoder.Encode(document);
        }
        catch (InvalidDataException ex)
        {
            Assert(
                ex.Message.Contains(expectedMessagePart, StringComparison.Ordinal),
                $"rejection did not identify '{expectedMessagePart}': {ex.Message}");
            return;
        }

        throw new InvalidOperationException(
            $"malformed AIVJSON was accepted; expected '{expectedMessagePart}'");
    }

    private static void RejectsMalformedNativeSpawnData()
    {
        bool rejectedTrailing = false;
        try
        {
            CastlePlanner.AivSpawnPlan.Decode([0, 1, 0, 1, 61, 5643, 0, 99]);
        }
        catch (InvalidDataException)
        {
            rejectedTrailing = true;
        }
        Assert(rejectedTrailing, "trailing native data was accepted");

        bool rejectedPause = false;
        try
        {
            CastlePlanner.AivSpawnPlan.Decode([0, 1, 3, 1, 61, 5643, 0]);
        }
        catch (InvalidDataException)
        {
            rejectedPause = true;
        }
        Assert(rejectedPause, "invalid native pause index was accepted");

        bool rejectedNoOpPositions = false;
        try
        {
            CastlePlanner.AivSpawnPlan.Decode([0, 1, 0, 2, 61, 5044, 0, 1, 4040, 0]);
        }
        catch (InvalidDataException)
        {
            rejectedNoOpPositions = true;
        }
        Assert(rejectedNoOpPositions, "native item type 0 with positions was accepted");

        bool rejectedCompoundKeep = false;
        try
        {
            CastlePlanner.AivSpawnPlan.Decode([0, 1, 0, 1, -61, 2, 5044, 5144, 0]);
        }
        catch (InvalidDataException)
        {
            rejectedCompoundKeep = true;
        }
        Assert(rejectedCompoundKeep, "native Keep with multiple placements was accepted");
    }

    private static void FiltersEverySpawnFrameCategory()
    {
        int[] fortifications = [61, 25, 26, 35, 46, 105, 110, 114, 144, 147, 181, 186];
        foreach (int mapper in fortifications)
            Equal(CastlePlanner.AivFrameSpawnCategory.Fortification, CastlePlanner.AivSpawnPlan.ClassifyFrame(mapper));
        foreach (int mapper in new[] { 98, 99, 106, 312 })
            Equal(CastlePlanner.AivFrameSpawnCategory.DefensiveGroundFeature, CastlePlanner.AivSpawnPlan.ClassifyFrame(mapper));
        foreach (int mapper in new[] { 160, 166, 169, 175, 176, 177, 301, 305, 306, 307, 308, 310, 311, 313, 318, 324, 325, 327 })
            Equal(CastlePlanner.AivFrameSpawnCategory.FearFactor, CastlePlanner.AivSpawnPlan.ClassifyFrame(mapper));
        foreach (int mapper in new[] { 52, 53, 80, 178, 179 })
            Equal(CastlePlanner.AivFrameSpawnCategory.Building, CastlePlanner.AivSpawnPlan.ClassifyFrame(mapper));

        var source = new CastlePlanner.AivJsonDocument
        {
            frames =
            [
                new CastlePlanner.AivJsonFrame { itemType = 61, tilePositionOfsets = [5643] },
                new CastlePlanner.AivJsonFrame { itemType = 25, tilePositionOfsets = [5543] },
                new CastlePlanner.AivJsonFrame { itemType = 80, tilePositionOfsets = [5443] },
                new CastlePlanner.AivJsonFrame { itemType = 98, tilePositionOfsets = [5343] },
                new CastlePlanner.AivJsonFrame { itemType = 312, tilePositionOfsets = [5293] },
                new CastlePlanner.AivJsonFrame { itemType = 160, tilePositionOfsets = [5243], shouldPause = true }
            ],
            miscItems = []
        };
        CastlePlanner.AivJsonDocument fortOnly = CastlePlanner.AivSpawnPlan.Filter(source, new CastlePlanner.AivSpawnOptions());
        Assert(fortOnly.frames.Select(frame => frame.itemType).SequenceEqual([61, 25]), "fortification-only filter retained optional frames");
        var all = new CastlePlanner.AivSpawnOptions
        {
            SpawnBuildings = true,
            SpawnDefensiveGroundFeatures = true,
            SpawnFearFactorBuildings = true
        };
        CastlePlanner.AivJsonDocument complete = CastlePlanner.AivSpawnPlan.Filter(source, all);
        Equal(6, complete.frames.Count);
        Assert(complete.frames[5].shouldPause, "frame pause was not preserved before reindexing");

        var withoutFortifications = new CastlePlanner.AivSpawnOptions
        {
            SpawnFortifications = false,
            SpawnBuildings = true,
            SpawnDefensiveGroundFeatures = true,
            SpawnFearFactorBuildings = true
        };
        CastlePlanner.AivJsonDocument optionalOnly =
            CastlePlanner.AivSpawnPlan.Filter(source, withoutFortifications);
        Assert(
            optionalOnly.frames.Select(frame => frame.itemType)
                .SequenceEqual([61, 80, 98, 312, 160]),
            "fortification filter removed the Keep anchor or retained a wall");

        var buildingsAndFearOnly = new CastlePlanner.AivSpawnOptions
        {
            SpawnFortifications = false,
            SpawnBuildings = true,
            SpawnFearFactorBuildings = true
        };
        CastlePlanner.AivJsonDocument combined =
            CastlePlanner.AivSpawnPlan.Filter(source, buildingsAndFearOnly);
        Assert(
            combined.frames.Select(frame => frame.itemType)
                .SequenceEqual([61, 80, 160]),
            "combined Blueprint category filter retained an unwanted category");

        CastlePlanner.AivJsonDocument defensesOnly =
            CastlePlanner.AivSpawnPlan.Filter(
                source,
                new CastlePlanner.AivSpawnOptions
                {
                    SpawnFortifications = false,
                    SpawnDefensiveGroundFeatures = true
                });
        Assert(
            defensesOnly.frames.Select(frame => frame.itemType)
                .SequenceEqual([61, 98, 312]),
            "defensive-ground filter did not retain the Dog Cage trap");

        var stockpileSource = new CastlePlanner.AivJsonDocument
        {
            frames =
            [
                new CastlePlanner.AivJsonFrame { itemType = 61, tilePositionOfsets = [5643] },
                new CastlePlanner.AivJsonFrame { itemType = 52, tilePositionOfsets = [5543] }
            ],
            miscItems = []
        };
        CastlePlanner.AivJsonDocument stockpileDisabled = CastlePlanner.AivSpawnPlan.Filter(stockpileSource, new CastlePlanner.AivSpawnOptions());
        Equal(1, stockpileDisabled.frames.Count);
        CastlePlanner.AivJsonDocument stockpileEnabled = CastlePlanner.AivSpawnPlan.Filter(
            stockpileSource,
            new CastlePlanner.AivSpawnOptions { SpawnBuildings = true });
        Equal(2, stockpileEnabled.frames.Count);
        CastlePlanner.AivJsonDocument stockpilePreventedByFixes = CastlePlanner.AivSpawnPlan.Filter(
            stockpileSource,
            new CastlePlanner.AivSpawnOptions
            {
                SpawnBuildings = true,
                SpawnStockpile = false
            });
        Equal(1, stockpilePreventedByFixes.frames.Count);
    }

    private static void FiltersBlueprintCastleChoices()
    {
        Assert(CastlePlanner.BlueprintSearchPolicy.Matches(
            "VanillaAIV/Saladin1.aivjson",
            "saladin"), "case-insensitive castle search did not match");
        Assert(CastlePlanner.BlueprintSearchPolicy.Matches(
            "Workshop/My Castle.aivjson",
            "  my castle  "), "trimmed castle search did not match");
        Assert(CastlePlanner.BlueprintSearchPolicy.Matches(
            "Any.aivjson",
            " "), "blank castle search did not retain all choices");
        Assert(!CastlePlanner.BlueprintSearchPolicy.Matches(
            "Rat1.aivjson",
            "Snake"), "unrelated castle search unexpectedly matched");
    }

    private static void FormatsCompactAivDisplayNames()
    {
        string[] options =
        [
            @"[Vanilla] Saladin1.aivjson",
            @"[Mod] Packs\My Castle.aivjson",
            @"[CustomLords] Lord\My Keep.aivjson",
            @"[ExtendedLords] Lord\Extended Keep.aivjson",
            @"[Editor] Villages\Editor Keep.aivjson",
            @"[Steam Workshop 1234567890] Nested\Steam Keep.aivjson"
        ];

        IReadOnlyDictionary<string, string> names =
            CastlePlanner.AivOptionDisplayNames.Build(options);

        Equal("[Vanilla] Saladin1", names[options[0]]);
        Equal("[Mod] My Castle", names[options[1]]);
        Equal("[CustomLords] My Keep", names[options[2]]);
        Equal("[ExtendedLords] Extended Keep", names[options[3]]);
        Equal("[Editor] Editor Keep", names[options[4]]);
        Equal("[Steam] Steam Keep (1234567890)", names[options[5]]);
        Assert(options.All(names.ContainsKey),
            "formatting changed a stable catalog option key");
        Assert(names.Values.All(name =>
            !name.EndsWith(".aivjson", StringComparison.OrdinalIgnoreCase)),
            "an AIVJSON extension remained visible");
    }

    private static void DisambiguatesCompactAivDisplayNames()
    {
        string[] options =
        [
            @"[Mod] packA\common\Castle.aivjson",
            @"[Mod] packB\common\Castle.aivjson",
            @"[Steam Workshop 111] one\Castle.aivjson",
            @"[Steam Workshop 222] two\Castle.aivjson"
        ];

        IReadOnlyDictionary<string, string> names =
            CastlePlanner.AivOptionDisplayNames.Build(options);

        Equal("[Mod] Castle — packA/common", names[options[0]]);
        Equal("[Mod] Castle — packB/common", names[options[1]]);
        Equal("[Steam] Castle (111)", names[options[2]]);
        Equal("[Steam] Castle (222)", names[options[3]]);
        Equal(names.Count, names.Values.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    private static void FiltersTroopsAndMapsOnlySiegeEngines()
    {
        var expected = new Dictionary<int, eChimps>
        {
            [2] = eChimps.CHIMP_TYPE_MANGONEL,
            [3] = eChimps.CHIMP_TYPE_BALLISTA,
            [4] = eChimps.CHIMP_TYPE_TREBUCHET,
            [5] = eChimps.CHIMP_TYPE_ARAB_BALLISTA
        };
        foreach ((int miscType, eChimps expectedChimp) in expected)
        {
            Assert(CastlePlanner.AivSpawnPlan.TryMapSiegeEngine(9000 + miscType, out eChimps actual), $"siege type {miscType} was not mapped");
            Equal(expectedChimp, actual);
        }
        Assert(!CastlePlanner.AivSpawnPlan.TryMapSiegeEngine(1, out _), "engineer was mapped as a siege engine");
        Assert(!CastlePlanner.AivSpawnPlan.TryMapSiegeEngine(6, out _), "troop was mapped as a siege engine");
        Equal(CastlePlanner.AivMiscSpawnCategory.Troop, CastlePlanner.AivSpawnPlan.ClassifyMisc(1));
        Equal(CastlePlanner.AivMiscSpawnCategory.Troop, CastlePlanner.AivSpawnPlan.ClassifyMisc(9006));
        Equal(CastlePlanner.AivMiscSpawnCategory.Decoration, CastlePlanner.AivSpawnPlan.ClassifyMisc(20));
        Equal(CastlePlanner.AivMiscSpawnCategory.Decoration, CastlePlanner.AivSpawnPlan.ClassifyMisc(9021));
        Equal(CastlePlanner.AivMiscSpawnCategory.Unknown, CastlePlanner.AivSpawnPlan.ClassifyMisc(22));
        var source = new CastlePlanner.AivJsonDocument
        {
            frames = [new CastlePlanner.AivJsonFrame { itemType = 61, tilePositionOfsets = [5643] }],
            miscItems =
            [
                new CastlePlanner.AivJsonMiscItem { itemType = 1, positionOfset = 5543, number = 0 },
                new CastlePlanner.AivJsonMiscItem { itemType = 6, positionOfset = 5443, number = 0 },
                new CastlePlanner.AivJsonMiscItem { itemType = 2, positionOfset = 5343, number = 0 }
            ]
        };
        CastlePlanner.AivJsonDocument filtered = CastlePlanner.AivSpawnPlan.Filter(
            source,
            new CastlePlanner.AivSpawnOptions { SpawnSiegeEngines = true });
        Equal(1, filtered.miscItems.Count);
        Equal(2, filtered.miscItems[0].itemType);
    }

    private static void ProjectsSupplementalItemsForEveryRotation()
    {
        var point = new AivGridPoint(55, 44);
        var expected = new Dictionary<AivRotation, (int X, int Y)>
        {
            [AivRotation.Degrees0] = (201, 201),
            [AivRotation.Degrees90] = (201, 212),
            [AivRotation.Degrees180] = (212, 212),
            [AivRotation.Degrees270] = (212, 201)
        };
        foreach ((AivRotation rotation, (int X, int Y) target) in expected)
        {
            AivWorldTile projected = AivWorldTransform.ProjectNativeFit(point, 200, 200, rotation);
            Equal(target.X, projected.X);
            Equal(target.Y, projected.Y);
        }
    }

    private static void MapsBraziersAndOwnerSpecificFlags()
    {
        Assert(CastlePlanner.AivSpawnPlan.TryMapDecoration(20, 1, 22, out eMappers brazier, out ProjectileType brazierType), "brazier was not mapped");
        Equal(eMappers.MAPPER_BRAZIER, brazier);
        Equal(ProjectileType.Brazier, brazierType);
        Assert(CastlePlanner.AivSpawnPlan.TryMapDecoration(9020, 8, 22, out eMappers encodedBrazier, out ProjectileType encodedBrazierType), "encoded brazier was not mapped");
        Equal(eMappers.MAPPER_BRAZIER, encodedBrazier);
        Equal(ProjectileType.Brazier, encodedBrazierType);

        for (int playerId = 0; playerId <= 8; playerId++)
        {
            Assert(CastlePlanner.AivSpawnPlan.TryMapDecoration(21, playerId, 22, out eMappers flag, out ProjectileType flagType), $"flag for player {playerId} was not mapped");
            Equal((eMappers)((int)eMappers.MAPPER_FLAG_TYPE0 + playerId), flag);
            Equal(ProjectileType.Disease, flagType);
        }

        foreach (ushort value in new ushort[] { 0, 9, 10, 11, 12, 13, 22, 42, ushort.MaxValue })
        {
            Assert(CastlePlanner.AivSpawnPlan.TryMapDecoration(21, 1, value, out _, out ProjectileType type),
                $"UInt16 flag type {value} was rejected");
            Equal(value, (ushort)type);
        }

        Assert(!CastlePlanner.AivSpawnPlan.TryMapDecoration(21, -1, 22, out _, out _), "negative player id mapped a flag");
        Assert(!CastlePlanner.AivSpawnPlan.TryMapDecoration(21, 9, 22, out _, out _), "out-of-range player id mapped a flag");
        Assert(!CastlePlanner.AivSpawnPlan.TryMapDecoration(22, 1, 22, out _, out _), "unknown decoration type was mapped");
    }

    private static void KeepsSupplementalItemsOnNativeReferenceAnchor()
    {
        var point = new AivGridPoint(55, 44);
        AivWorldTile correctlyAnchored = AivWorldTransform.ProjectNativeFit(
            point,
            525,
            274,
            AivRotation.Degrees90);
        AivWorldTile incorrectlyAnchoredToLiveKeep = AivWorldTransform.ProjectNativeFit(
            point,
            525,
            281,
            AivRotation.Degrees90);

        Equal(correctlyAnchored.X, incorrectlyAnchoredToLiveKeep.X);
        Equal(correctlyAnchored.Y + 7, incorrectlyAnchoredToLiveKeep.Y);
    }

    private static void ConvertsDecorationTilesToProjectileCoordinates()
    {
        Equal(0, CastlePlanner.AivProjectileTransform.ToProjectileCoordinate(0));
        Equal(4040, CastlePlanner.AivProjectileTransform.ToProjectileCoordinate(505));
        Equal(6392, CastlePlanner.AivProjectileTransform.ToProjectileCoordinate(799));
    }

    private static void AlignsNativeRotationToLiveKeepFootprint()
    {
        var keepAnchor = new AivGridPoint(48, 53);
        const int footprintSize = 7;
        const int liveKeepX = 523;
        const int liveKeepY = 278;
        foreach (AivRotation rotation in Enum.GetValues<AivRotation>())
        {
            AivWorldTile nativeReference = CastlePlanner.AivNativeKeepAlignment.ResolveNativeReference(
                keepAnchor,
                footprintSize,
                liveKeepX,
                liveKeepY,
                rotation);
            AivFootprint footprint = AivGridTransform.GetFootprint(
                keepAnchor,
                footprintSize,
                AivRotation.Degrees0);
            int minimumX = int.MaxValue;
            int minimumY = int.MaxValue;
            for (int row = footprint.Minimum.Row; row <= footprint.Maximum.Row; row++)
            {
                for (int column = footprint.Minimum.Column;
                     column <= footprint.Maximum.Column;
                     column++)
                {
                    AivWorldTile projected = AivWorldTransform.ProjectNativeFit(
                        new AivGridPoint(row, column),
                        nativeReference.X,
                        nativeReference.Y,
                        rotation);
                    minimumX = Math.Min(minimumX, projected.X);
                    minimumY = Math.Min(minimumY, projected.Y);
                }
            }

            Equal(liveKeepX, minimumX);
            Equal(liveKeepY, minimumY);
        }
    }

    private static void ResolvesRotatedBuildStructureOrigins()
    {
        var anchor = new AivGridPoint(49, 50);
        var expected = new Dictionary<AivRotation, (int X, int Y)>
        {
            [AivRotation.Degrees0] = (532, 281),
            [AivRotation.Degrees90] = (532, 276),
            [AivRotation.Degrees180] = (527, 276),
            [AivRotation.Degrees270] = (527, 281)
        };

        foreach ((AivRotation rotation, (int X, int Y) target) in expected)
        {
            AivWorldTile origin = CastlePlanner.AivNativeBuildingPlacement.ResolveBuildStructureOrigin(
                anchor,
                5,
                525,
                274,
                rotation);
            Equal(target.X, origin.X);
            Equal(target.Y, origin.Y);
        }

        AivWorldTile nativeGranary = CastlePlanner.AivNativeBuildingPlacement.ResolveBuildStructureOrigin(
            new AivGridPoint(3552),
            4,
            525,
            274,
            AivRotation.Degrees270);
        Equal(514, nativeGranary.X);
        Equal(283, nativeGranary.Y);

        AivWorldTile rotatedGarden10 = CastlePlanner.AivNativeBuildingPlacement.ResolveBuildStructureOrigin(
            new AivGridPoint(5338),
            4,
            525,
            274,
            AivRotation.Degrees270);
        Equal(532, rotatedGarden10.X);
        Equal(269, rotatedGarden10.Y);

        AivWorldTile rotatedGarden7 = CastlePlanner.AivNativeBuildingPlacement.ResolveBuildStructureOrigin(
            new AivGridPoint(3948),
            3,
            525,
            274,
            AivRotation.Degrees270);
        Equal(519, rotatedGarden7.X);
        Equal(279, rotatedGarden7.Y);
    }

    private static void PreservesCompoundStoragePlacementOrder()
    {
        var document = new CastlePlanner.AivJsonDocument
        {
            frames =
            [
                new CastlePlanner.AivJsonFrame
                {
                    itemType = (int)eMappers.MAPPER_GRANARY,
                    tilePositionOfsets = [5338, 4938]
                },
                new CastlePlanner.AivJsonFrame
                {
                    itemType = (int)eMappers.MAPPER_HOVEL,
                    tilePositionOfsets = [5040]
                },
                new CastlePlanner.AivJsonFrame
                {
                    itemType = (int)eMappers.MAPPER_ARMOURY,
                    tilePositionOfsets = [5542, 5546]
                }
            ],
            miscItems = []
        };

        List<CastlePlanner.AivCompoundBuildingPlacement> placements =
            CastlePlanner.AivCompoundBuildingPlan.Create(
                document,
                525,
                274,
                AivRotation.Degrees90);

        Equal(4, placements.Count);
        Equal(eMappers.MAPPER_GRANARY, placements[0].Mapper);
        Equal(5338, placements[0].EncodedPosition);
        Equal(528, placements[0].BuildOrigin.X);
        Equal(289, placements[0].BuildOrigin.Y);
        Equal(eMappers.MAPPER_GRANARY, placements[1].Mapper);
        Equal(4938, placements[1].EncodedPosition);
        Equal(532, placements[1].BuildOrigin.X);
        Equal(289, placements[1].BuildOrigin.Y);
        Equal(eMappers.MAPPER_ARMOURY, placements[2].Mapper);
        Equal(eMappers.MAPPER_ARMOURY, placements[3].Mapper);
        Equal(0, placements[0].SourceOrdinal);
        Equal(3, placements[3].SourceOrdinal);
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
