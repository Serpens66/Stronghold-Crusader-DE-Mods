using Shared;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

static class Program
{
    static int checks;
    static void Check(bool value, string description)
    { checks++; if (!value) throw new Exception(description); }
    static StoragePlacement Part(int index, eMappers mapper, int x = 20) =>
        new StoragePlacement(index, mapper, x, 30, mapper == eMappers.MAPPER_STORES ? 5 : 4);
    static void Main()
    {
        foreach (eMappers mapper in new[] { eMappers.MAPPER_STORES, eMappers.MAPPER_GRANARY, eMappers.MAPPER_ARMOURY })
        {
            var one = Part(0, mapper); var two = Part(1, mapper, 25);
            var backend = new Fake();
            var queue = new DeferredStorageQueue(2, new[] { one, one, two });
            Check(queue.Count == 2, "duplicate source placements are not separate requests");
            Check(queue.Process(10, backend, out _) == StorageQueueResult.Waiting && backend.Creates == 0,
                "no initialized seed means no native call");
            backend.ReadySeed = true;
            Check(queue.Process(11, backend, out _) == StorageQueueResult.Waiting && backend.Creates == 1,
                "first extension uses a live seed, including the Keep stockpile");
            queue.Process(11, backend, out _);
            Check(backend.Creates == 1, "duplicate tick cannot place twice");
            queue.Process(12, backend, out _);
            Check(backend.Creates == 1, "NeedsInit predecessor cannot permit a second section");
            backend.States[0] = StorageSectionState.IsAlive;
            queue.Process(13, backend, out _);
            Check(backend.Creates == 2 && backend.Ready.SequenceEqual(new[] { (0, true) }),
                "initialized predecessor unlocks one next section with creation bookkeeping");
            backend.States[1] = StorageSectionState.IsAlive;
            Check(queue.Process(14, backend, out _) == StorageQueueResult.Completed,
                "last section must initialize before completing");
            queue.Process(15, backend, out _);
            Check(backend.Creates == 2, "finished startup never becomes a rebuild loop");

            backend = new Fake { ReadySeed = true };
            backend.States[0] = StorageSectionState.IsAlive;
            queue = new DeferredStorageQueue(2, new[] { one, two });
            queue.Process(0, backend, out _);
            Check(backend.Creates == 1 && backend.Ready.SequenceEqual(new[] { (0, false) }),
                "already built Vanilla section is skipped before missing extension");
            backend.States[1] = StorageSectionState.Lost;
            Check(queue.Process(1, backend, out _) == StorageQueueResult.Aborted && backend.Creates == 1,
                "destroyed or replaced observed section cannot be recreated");

            foreach (bool uncertain in new[] { false, true })
            {
                backend = new Fake { ReadySeed = true, Blocked = !uncertain, Unconfirmed = uncertain };
                queue = new DeferredStorageQueue(2, new[] { one });
                Check(queue.Process(0, backend, out _) == StorageQueueResult.Aborted, "unsafe placement stops queue");
                queue.Process(1, backend, out _);
                Check(backend.Creates == (uncertain ? 1 : 0), "blocked or uncertain side effect is not retried");
            }

            backend = new Fake { ReadySeed = true, Reenter = true };
            queue = new DeferredStorageQueue(2, new[] { one, two }); backend.Queue = queue;
            queue.Process(0, backend, out _);
            Check(backend.Creates == 1, "nested processing cannot create another section");

            backend = new Fake { ReadySeed = true, ThrowOnCreate = true };
            queue = new DeferredStorageQueue(2, new[] { one });
            try { queue.Process(0, backend, out _); throw new Exception("native exception expected"); }
            catch (InvalidOperationException) { }
            Check(queue.Process(1, backend, out _) == StorageQueueResult.Aborted && backend.Creates == 1,
                "exception after uncertain mutation ends queue permanently");

            backend = new Fake { Current = false };
            queue = new DeferredStorageQueue(2, new[] { one });
            Check(queue.Process(0, backend, out _) == StorageQueueResult.Aborted, "changed mission/plan rejected");

            backend = new Fake(); queue = new DeferredStorageQueue(2, new[] { one });
            queue.Process(int.MaxValue - 20, backend, out _);
            Check(queue.Process(unchecked(int.MaxValue - 20 + 599), backend, out _) == StorageQueueResult.Waiting,
                "native signed tick wrap remains a short wait");
            Check(queue.Process(unchecked(int.MaxValue - 20 + 600), backend, out _) == StorageQueueResult.TimedOut,
                "deadline ends at 600 native ticks");
        }
        foreach (TilePropertyFlag flags in new[] { TilePropertyFlag.Sea, TilePropertyFlag.IsFarm,
            TilePropertyFlag.MapBorder, TilePropertyFlag.RealityEdge, TilePropertyFlag.IsWall,
            TilePropertyFlag.IsBuilding, TilePropertyFlag.IsElevated, TilePropertyFlag.River,
            TilePropertyFlag.Ford, TilePropertyFlag.IsWheat, TilePropertyFlag.IsHops,
            TilePropertyFlag.IsAppleFarm, TilePropertyFlag.IsFarmFence, TilePropertyFlag.IsSwamp,
            TilePropertyFlag.IsMoat, TilePropertyFlag.ImpassableEdge })
            Check(StoragePlacementBackend.IsBlockedTerrain(flags | TilePropertyFlag.IsLand), "blocked physical terrain " + flags);
        Check(!StoragePlacementBackend.IsBlockedTerrain(TilePropertyFlag.IsLand), "ordinary land remains available");
        for (int pending = 0; pending < 4; pending++)
        {
            var states = Enumerable.Repeat(StorageSectionState.IsAlive, 4).ToArray();
            states[pending] = StorageSectionState.NeedsInit;
            Check(StoragePlacementBackend.CombineYardStates(states[0], states[1], states[2], states[3]) ==
                StorageSectionState.NeedsInit, "each of four stockpile records must initialize");
            states[pending] = StorageSectionState.Lost;
            Check(StoragePlacementBackend.CombineYardStates(states[0], states[1], states[2], states[3]) ==
                StorageSectionState.Lost, "a lost stockpile subrecord forbids reconstruction");
        }
        Check(StoragePlacementBackend.CombineYardStates(StorageSectionState.IsAlive, StorageSectionState.IsAlive,
            StorageSectionState.IsAlive, StorageSectionState.IsAlive) == StorageSectionState.IsAlive, "complete yard ready");
        CheckAiCompletion();
        CheckStartupPlanGroups();
        CheckPlacementDiagnostics();
        CheckPlacementRejection();
        Console.WriteLine($"PASS: {checks} deferred storage checks");
    }

    static void CheckPlacementRejection()
    {
        var stores = new StoragePlacement(2, eMappers.MAPPER_STORES, 410, 528, 5);
        var armoury = new StoragePlacement(133, eMappers.MAPPER_ARMOURY, 399, 521, 4);
        var backend = new Fake { ReadySeed = true, RejectedFrame = stores.SourceIndex };
        var skipped = new List<(int Tick, int Frame, string Reason)>();
        var queue = new DeferredStorageQueue(3, new[] { stores, armoury },
            (tick, placement, reason) => skipped.Add((tick, placement.SourceIndex, reason)));
        Check(queue.Process(8, backend, out _) == StorageQueueResult.Waiting &&
            skipped.SequenceEqual(new[] { (8, 2, "placement-validation-rejected") }) &&
            queue.SkippedCount == 1 && backend.Creates == 1 && !backend.States.ContainsKey(2),
            "confirmed validator rejection skips only the failed frame and makes no completion writes");
        queue.Process(8, backend, out _);
        Check(backend.Creates == 1, "skipping a rejected frame still consumes the tick's attempt");
        Check(queue.Process(9, backend, out _) == StorageQueueResult.Waiting && backend.Creates == 2 &&
            backend.States[133] == StorageSectionState.NeedsInit,
            "a later eligible armoury can be placed after the rejected stores");
        backend.States[133] = StorageSectionState.IsAlive;
        Check(queue.Process(10, backend, out _) == StorageQueueResult.Completed &&
            backend.Ready.SequenceEqual(new[] { (133, true) }) && queue.SkippedCount == 1,
            "only the armoury receives completion bookkeeping");

        backend = new Fake { ReadySeed = true, RejectedFrame = stores.SourceIndex };
        queue = new DeferredStorageQueue(3, new[] { stores, armoury });
        Check(queue.Process(8, backend, out _) == StorageQueueResult.Aborted &&
            queue.SkippedCount == 0 && queue.CurrentPlacement?.SourceIndex == stores.SourceIndex,
            "CastlePlanner's default policy still stops at the rejected placement");

        backend = new Fake { ReadySeed = true, RejectedFrame = stores.SourceIndex };
        queue = new DeferredStorageQueue(3, new[] { stores }, (_, _, _) => { });
        Check(queue.Process(8, backend, out _) == StorageQueueResult.Completed &&
            queue.SkippedCount == 1 && backend.Ready.Count == 0,
            "skipping the final placement completes the queue without marking the frame built");

        foreach (bool occupied in new[] { false, true })
        {
            backend = new Fake { ReadySeed = true, Blocked = occupied, Unconfirmed = !occupied };
            queue = new DeferredStorageQueue(3, new[] { stores, armoury },
                (_, _, _) => throw new Exception("only a confirmed validator rejection may be skipped"));
            Check(queue.Process(8, backend, out _) == StorageQueueResult.Aborted && queue.SkippedCount == 0 &&
                queue.CurrentPlacement?.SourceIndex == stores.SourceIndex && !backend.States.ContainsKey(133),
                "precheck failure or uncertain native mutation still stops the AI queue");
        }
    }

    static void CheckPlacementDiagnostics()
    {
        var placement = Part(5, eMappers.MAPPER_STORES);
        BuildingPlacementValidationEventArgs Event(EventHookPhase phase, int player = 2) =>
            new BuildingPlacementValidationEventArgs(phase, player, placement.X, placement.Y,
                placement.Mapper, placement.Size, 0);

        foreach (int state in new[] { 0, 1, 2 })
        {
            var raw = (State: 0, Reason: 3);
            var observation = new StoragePlacementBackend.ValidationObservation(2, placement, () => raw);
            observation.Observe(Event(EventHookPhase.Pre));
            raw = (state, state == 1 ? 7 : 3);
            observation.Observe(Event(EventHookPhase.Post));
            string description = observation.Describe();
            Check(state == 0 ? description == "validation=allowed" :
                description.StartsWith("validation=blocked") && description.Contains($"blockState={state}"),
                $"raw validator state {state} stays distinguishable");
            Check(description.Contains("possibly stale") == (state == 2),
                "an unchanged reason on a blocked attempt may be stale");
            Check(observation.IsRejectedWithoutSpawn(false) == (state == 1) &&
                !observation.IsRejectedWithoutSpawn(true),
                "only the ordinary blocked state with no spawn activity confirms rejection");
        }

        var rawStore = (State: 0, Reason: 7);
        var adjacent = new StoragePlacementBackend.ValidationObservation(2, placement, () => rawStore);
        adjacent.Observe(Event(EventHookPhase.Pre));
        rawStore = (1, 3);
        adjacent.Observe(Event(EventHookPhase.Post));
        Check(adjacent.Describe().Contains("nativeReasonHint=no-shared-edge-with-live-stockpile"),
            "a fresh Stores reason 3 identifies the live edge requirement");
        var custom = new StoragePlacementBackend.ValidationObservation(2, placement, () => rawStore);
        var customPre = Event(EventHookPhase.Pre);
        custom.Observe(customPre);
        customPre.CustomValidationRules = true;
        custom.Observe(Event(EventHookPhase.Post));
        Check(!custom.IsRejectedWithoutSpawn(false), "a custom validator override remains diagnostically uncertain");

        var uncertain = new StoragePlacementBackend.ValidationObservation(2, placement, () => (0, 0));
        uncertain.Observe(Event(EventHookPhase.Pre, 3));
        uncertain.Observe(Event(EventHookPhase.Post, 3));
        uncertain.Observe(Event(EventHookPhase.Post));
        Check(uncertain.Describe() == "validation=not-observed", "foreign and unpaired events cannot confirm validation");

        uncertain = new StoragePlacementBackend.ValidationObservation(2, placement, () => (1, 3));
        var pre = Event(EventHookPhase.Pre);
        uncertain.Observe(pre);
        pre.TileX++;
        uncertain.Observe(Event(EventHookPhase.Post));
        Check(uncertain.Describe() == "validation=not-observed", "changed effective arguments invalidate the observation");

        var repeated = new StoragePlacementBackend.ValidationObservation(2, placement, () => (1, 3));
        for (int i = 0; i < 2; i++)
        {
            repeated.Observe(Event(EventHookPhase.Pre));
            repeated.Observe(Event(EventHookPhase.Post));
        }
        Check(repeated.Describe() == "validation=not-observed" && !repeated.IsRejectedWithoutSpawn(false),
            "multiple matching validator calls are ambiguous");

        var unavailable = new StoragePlacementBackend.ValidationObservation(2, placement,
            () => throw new InvalidOperationException("read unavailable"));
        unavailable.Observe(Event(EventHookPhase.Pre));
        unavailable.Observe(Event(EventHookPhase.Post));
        Check(unavailable.Describe() == "validation=not-observed", "read failure stays diagnostic-only");

        Check(StoragePlacementBackend.InvalidSpawnReturn(0) == "return=0" &&
            StoragePlacementBackend.InvalidSpawnReturn((long)int.MaxValue + 1) != null &&
            StoragePlacementBackend.InvalidSpawnReturn(3) == null, "unusable return values are distinguished from game IDs");
        var building = new GameBuilding { r_PlayerIdOwner = 2, r_BuildingType = eStructs.STRUCT_GOODS_YARD,
            r_TilePositionXBegin = 20, r_TilePositionYBegin = 30, r_AliveState = AliveState.NeedsInit };
        Check(StoragePlacementBackend.SpawnMismatch(in building, 2, eStructs.STRUCT_GOODS_YARD, 20, 30) == null &&
            StoragePlacementBackend.SpawnMismatch(in building, 3, eStructs.STRUCT_GOODS_YARD, 20, 30) == "owner-mismatch" &&
            StoragePlacementBackend.SpawnMismatch(in building, 2, eStructs.STRUCT_GOODS_YARD, 21, 30) == "tile-mismatch",
            "NeedsInit is valid but a different owner or origin is reported");
    }

    static void CheckStartupPlanGroups()
    {
        foreach (eMappers mapper in new[] { eMappers.MAPPER_STORES, eMappers.MAPPER_GRANARY, eMappers.MAPPER_ARMOURY })
        {
            var first = Part(0, mapper);
            var duplicate = Part(1, mapper);
            var otherPosition = Part(2, mapper, 40);
            eMappers otherMapper = mapper == eMappers.MAPPER_STORES ? eMappers.MAPPER_GRANARY : eMappers.MAPPER_STORES;
            var otherFamily = Part(3, otherMapper);
            foreach (bool reverse in new[] { false, true })
            {
                var source = reverse ? new[] { duplicate, otherPosition, first, otherFamily } :
                    new[] { first, otherPosition, duplicate, otherFamily };
                AivBuildStepState State(int id) => id == 0 ? AivBuildStepState.Built : AivBuildStepState.RetryPending;
                StorageSectionState Missing(StoragePlacement placement) => StorageSectionState.Missing;
                var actual = BugfixesAndQoL.PrebuiltAiStorageCompletionPolicy.FilterStartupPlan(source, Missing, State);
                Check(actual.Select(p => p.SourceIndex).SequenceEqual(new[] { 2, 3 }),
                    "missing completed coordinate excludes every duplicate, independent of order, without excluding another position/family");
                var backend = new Fake { ReadySeed = true };
                var queue = new DeferredStorageQueue(2, actual);
                queue.Process(0, backend, out _);
                Check(!backend.States.ContainsKey(0) && !backend.States.ContainsKey(1),
                    "a retry twin cannot recreate a completed but missing section");
                foreach (StorageSectionState presence in new[] { StorageSectionState.IsAlive, StorageSectionState.NeedsInit })
                {
                    actual = BugfixesAndQoL.PrebuiltAiStorageCompletionPolicy.FilterStartupPlan(source, _ => presence, State);
                    Check(actual.Select(p => p.SourceIndex).SequenceEqual(source.Select(p => p.SourceIndex)),
                        "present completed sections retain all duplicate sources for completion bookkeeping");
                }
                actual = BugfixesAndQoL.PrebuiltAiStorageCompletionPolicy.FilterStartupPlan(source, Missing, _ => AivBuildStepState.RetryPending);
                Check(actual.Select(p => p.SourceIndex).SequenceEqual(source.Select(p => p.SourceIndex)),
                    "genuinely missing unbuilt duplicate sections remain eligible in source order");
                actual = BugfixesAndQoL.PrebuiltAiStorageCompletionPolicy.FilterStartupPlan(source,
                    _ => StorageSectionState.Lost, State);
                Check(actual.Count == source.Length, "observed identity loss reaches terminal queue rejection rather than reconstruction");
            }
        }
    }

    static void CheckAiCompletion()
    {
        foreach (eMappers mapper in new[] { eMappers.MAPPER_STORES, eMappers.MAPPER_GRANARY, eMappers.MAPPER_ARMOURY })
            foreach (AivBuildStepState state in Enum.GetValues<AivBuildStepState>())
            {
                var step = new AivBuildStep { State = state, RebuildDelay = 16, BuildingType = mapper,
                    TileCount = 1, MapTileIdOrBufferIndex = 8050, Unknown06 = 123 };
                bool eligible = state == AivBuildStepState.Pending || state == AivBuildStepState.Built ||
                    state == AivBuildStepState.RetryPending;
                Check(BugfixesAndQoL.PrebuiltAiStorageCompletionPolicy.IsMatchingStep(in step, mapper, 8050) == eligible,
                    "AI completion admits only active matching native states");
                Check(!BugfixesAndQoL.PrebuiltAiStorageCompletionPolicy.IsMatchingStep(in step, mapper, 8051), "changed AI plan tile rejected");
                var changed = step; changed.TileCount = 2;
                Check(!BugfixesAndQoL.PrebuiltAiStorageCompletionPolicy.IsMatchingStep(in changed, mapper, 8050), "grouped payload rejected");
                changed = step; changed.BuildingType = eMappers.MAPPER_HOVEL;
                Check(!BugfixesAndQoL.PrebuiltAiStorageCompletionPolicy.IsMatchingStep(in changed, mapper, 8050), "changed AI type rejected");
                BugfixesAndQoL.PrebuiltAiStorageCompletionPolicy.MarkBuilt(ref step);
                Check(step.State == AivBuildStepState.Built && step.RebuildDelay == 10 &&
                    step.BuildingType == mapper && step.TileCount == 1 && step.MapTileIdOrBufferIndex == 8050 && step.Unknown06 == 123,
                    "AI success has Vanilla's exact state/delay writes and preserves payload");
            }
        var building = new GameBuilding { r_AliveState = AliveState.NeedsInit, r_GlobalId = 456,
            r_PlayerIdOwner = 2, r_BuildingType = eStructs.STRUCT_GOODS_YARD };
        BugfixesAndQoL.PrebuiltAiStorageCompletionPolicy.MarkCreatedBuilding(ref building);
        Check(building.p_UnknownRelevant17 == 1 && building.r_AliveState == AliveState.NeedsInit &&
            building.r_GlobalId == 456 && building.r_PlayerIdOwner == 2 && building.r_BuildingType == eStructs.STRUCT_GOODS_YARD,
            "AIV marker never initializes a freshly created building");
    }

    sealed class Fake : IDeferredStorageBackend
    {
        internal readonly Dictionary<int, StorageSectionState> States = new();
        internal readonly List<(int, bool)> Ready = new();
        internal bool ReadySeed, Blocked, Unconfirmed, Reenter, ThrowOnCreate, Current = true;
        internal int RejectedFrame = -1;
        internal int Creates;
        internal DeferredStorageQueue Queue;
        public bool IsCurrent(StoragePlacement p, out string reason) { reason = "changed"; return Current; }
        public StorageSectionState Inspect(StoragePlacement p) => States.GetValueOrDefault(p.SourceIndex);
        public bool HasInitializedSeed(eMappers mapper) => ReadySeed;
        public bool CanPlace(StoragePlacement p, out string reason) { reason = "blocked"; return !Blocked; }
        public StorageCreateResult TryCreate(StoragePlacement p, out string reason)
        {
            Creates++; reason = "uncertain";
            if (Reenter) Queue.Process(0, this, out _);
            if (ThrowOnCreate) throw new InvalidOperationException();
            if (p.SourceIndex == RejectedFrame)
            {
                reason = "placement-validation-rejected";
                return StorageCreateResult.RejectedByValidation;
            }
            States[p.SourceIndex] = StorageSectionState.NeedsInit;
            return Unconfirmed ? StorageCreateResult.Unconfirmed : StorageCreateResult.Created;
        }
        public void OnReady(StoragePlacement p, bool created) => Ready.Add((p.SourceIndex, created));
    }
}
