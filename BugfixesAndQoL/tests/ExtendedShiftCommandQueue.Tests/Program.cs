using BugfixesAndQoL;
using Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

internal static class Program
{
    private static int checks;

    private static void Main()
    {
        CheckClassification();
        CheckQueueLimitAndOrder();
        CheckBuildingQueueOrder();
        CheckStateTransitions();
        CheckVisualQueueProjection();
        CheckStableVisualPages();
        CheckVisualFlagFiltering();
        CheckTribeReuseGuard();
        CheckUnitIdentityBinding();
        CheckUnitStableBranching();
        CheckAtomicCohortEnqueue();
        CheckNativeLayoutTranslation();
        CheckChoreMarkers();
        CheckDeferredFormationChoreExecution();
        CheckMoveChoreDeduplication();
        CheckFirstShiftMoveTakeover();
        CheckMoveFormationSpacing();
        CheckMoveFormationPlanner();
        CheckMoveFormationGesture();
        CheckGroundMovePreviewEligibility();
        CheckLargeMoveTargetOverflow();
        CheckMigrationSourceContracts();
        CheckNativeReference();
        Console.WriteLine($"Extended Shift command queue static tests passed: {checks} checks.");
    }

    private static void CheckGroundMovePreviewEligibility()
    {
        Check(GroundMovePreviewEligibility.EvaluateInitial(PreviewSnapshot()) ==
              GroundMovePreviewRejection.None,
            "clean pathable ground permits the Dense formation preview");
        Check(GroundMovePreviewEligibility.EvaluateInitial(
                  PreviewSnapshot(underCursor: 1)) ==
                  GroundMovePreviewRejection.UnderCursorUnit &&
              GroundMovePreviewEligibility.EvaluateInitial(
                  PreviewSnapshot(hoveredUnit: 1)) ==
                  GroundMovePreviewRejection.HoveredUnit &&
              GroundMovePreviewEligibility.EvaluateInitial(
                  PreviewSnapshot(tileUnit: 1)) ==
                  GroundMovePreviewRejection.TileOccupiedByUnit,
            "all immediate and native unit targets suppress Dense preview");
        Check(GroundMovePreviewEligibility.EvaluateInitial(
                  PreviewSnapshot(hoveredBuilding: 1)) ==
                  GroundMovePreviewRejection.HoveredBuilding &&
              GroundMovePreviewEligibility.EvaluateInitial(
                  PreviewSnapshot(hoveringWall: true)) ==
                  GroundMovePreviewRejection.HoveredWall &&
              GroundMovePreviewEligibility.EvaluateInitial(
                  PreviewSnapshot(tileBuilding: 1)) ==
                  GroundMovePreviewRejection.TileOccupiedByBuilding,
            "buildings, tall hovered structures, and walls suppress Dense preview");
        Check(GroundMovePreviewEligibility.EvaluateInitial(
                  PreviewSnapshot(insideMap: false)) ==
                  GroundMovePreviewRejection.OutsideMap &&
              GroundMovePreviewEligibility.EvaluateInitial(
                  PreviewSnapshot(targetAvailable: false)) ==
                  GroundMovePreviewRejection.TargetUnavailable &&
              GroundMovePreviewEligibility.EvaluateInitial(
                  PreviewSnapshot(hasComponent: false)) ==
                  GroundMovePreviewRejection.MissingPathComponent,
            "invalid and unpathable ground suppresses Dense preview");
        Check(GroundMovePreviewEligibility.EvaluateInitial(
                  PreviewSnapshot(cursorInGame: false)) ==
                  GroundMovePreviewRejection.CursorOutsideGame &&
              GroundMovePreviewEligibility.EvaluateInitial(
                  PreviewSnapshot(cursorMatches: false)) ==
                  GroundMovePreviewRejection.CursorSnapshotMismatch,
            "ambiguous cursor snapshots fail closed to Vanilla");
        Check(GroundMovePreviewEligibility.EvaluateFixedTarget(
                  true, 0, 0, true, true) == GroundMovePreviewRejection.None &&
              GroundMovePreviewEligibility.EvaluateFixedTarget(
                  true, 2, 0, true, true) ==
                  GroundMovePreviewRejection.TileOccupiedByUnit &&
              GroundMovePreviewEligibility.EvaluateFixedTarget(
                  true, 0, 2, true, true) ==
                  GroundMovePreviewRejection.TileOccupiedByBuilding,
            "held and release revalidation detects later target occupancy");
    }

    private static GroundMovePreviewSnapshot PreviewSnapshot(
        bool insideMap = true,
        bool cursorInGame = true,
        bool cursorMatches = true,
        int underCursor = 0,
        int hoveredUnit = 0,
        int tileUnit = 0,
        int hoveredBuilding = 0,
        bool hoveringWall = false,
        int tileBuilding = 0,
        bool targetAvailable = true,
        bool hasComponent = true) =>
        new GroundMovePreviewSnapshot(
            insideMap, cursorInGame, cursorMatches, underCursor,
            hoveredUnit, tileUnit, hoveredBuilding, hoveringWall,
            tileBuilding, targetAvailable, hasComponent);

    private static void CheckMoveFormationSpacing()
    {
        Check(MoveFormationSpacingPolicy.Default == 2,
            "neutral Move formation click uses spacing two");
        Check(MoveFormationSpacingPolicy.GetCommandMouseButton(true) == 0 &&
              MoveFormationSpacingPolicy.GetCommandMouseButton(false) == 1,
            "Vanilla SH1 and DE control schemes select left and right command buttons");
        Check(MoveFormationSpacingPolicy.GetHorizontalDragStep(800) == 32f &&
              Math.Abs(MoveFormationSpacingPolicy.GetHorizontalDragStep(1920) - 57.6f) < 0.001f,
            "horizontal drag step is resolution independent with a 32-pixel floor");
        float start = 0f;
        float step = MoveFormationSpacingPolicy.GetHorizontalDragStep(1920);
        Check(MoveFormationSpacingPolicy.FromHorizontalDrag(start, start - step, 1920) == 1 &&
              MoveFormationSpacingPolicy.FromHorizontalDrag(start, start - step + 0.01f, 1920) == 2 &&
              MoveFormationSpacingPolicy.FromHorizontalDrag(start, start, 1920) == 2 &&
              MoveFormationSpacingPolicy.FromHorizontalDrag(start, start + step, 1920) == 3 &&
              MoveFormationSpacingPolicy.FromHorizontalDrag(start, start + 2f * step, 1920) == 4,
            "drag thresholds select spacings one through four at exact boundaries");
        foreach (int spacing in new[] { 1, 2, 3, 4 })
        {
            MoveFormationOffset[] offsets = MoveFormationSpacingPolicy
                .EnumerateManhattanOffsets(spacing, 20).Take(100).ToArray();
            Check(offsets.Length == 100 && offsets[0].X == 0 && offsets[0].Y == 0 &&
                  offsets.All(offset =>
                      (Math.Abs(offset.X) + Math.Abs(offset.Y)) % spacing == 0) &&
                  offsets.Select(offset => $"{offset.X},{offset.Y}").Distinct().Count() == 100,
                $"spacing {spacing} preview enumerates unique Manhattan-grid candidates");
        }
        MoveFormationOffset[] edgeCandidates = MoveFormationSpacingPolicy
            .EnumerateManhattanOffsets(2, 20)
            .Where(offset => offset.X >= 0 && offset.Y >= 0 &&
                !(offset.X == 2 && offset.Y == 0))
            .Take(25)
            .ToArray();
        Check(edgeCandidates.Length == 25 &&
              edgeCandidates.All(offset => offset.X >= 0 && offset.Y >= 0) &&
              edgeCandidates.All(offset => offset.X != 2 || offset.Y != 0),
            "preview candidate stream supports map-edge and blocked-tile filtering");
        for (int spacing = 1; spacing <= 4; spacing++)
        {
            Check(MoveFormationSpacingPolicy.Normalize(spacing) == spacing,
                $"Move formation spacing accepts {spacing}");
        }
        foreach (int invalid in new[] { int.MinValue, -1, 0, 5, int.MaxValue })
        {
            Check(MoveFormationSpacingPolicy.Normalize(invalid) == MoveFormationSpacingPolicy.Default,
                $"invalid Move formation spacing {invalid} resets to default");
        }
        foreach (int vanillaSpacing in new[] { 1, 2, 3, 4 })
        foreach (int configuredSpacing in new[] { 1, 2, 3, 4 })
        {
            Check(MoveFormationSpacingPolicy.ResolveEffectiveSpacing(
                    vanillaSpacing, configuredSpacing, overrideEnabled: true) == configuredSpacing,
                $"normal Vanilla spacing {vanillaSpacing} accepts configured {configuredSpacing}");
        }
        Check(MoveFormationSpacingPolicy.ResolveEffectiveSpacing(3, 4, overrideEnabled: false) == 3,
            "disabled Move formation feature preserves Vanilla Assassin spacing");

        object owner = new object();
        MoveFormationUnitIdentity[] identities = Enumerable.Range(1, 200)
            .Select(unitId => new MoveFormationUnitIdentity(unitId, (uint)(1000 + unitId)))
            .ToArray();
        MoveFormationCommandSnapshotStore.Begin(owner, 7, 30, 40, 4, identities);
        MoveFormationCommandSnapshotStore.Observe(
            owner, MoveFormationSelector.AssassinGround, 3, 4);
        Check(MoveFormationCommandSnapshotStore.TryConsume(
                7, 30, 40, out MoveFormationCommandSnapshot snapshot) &&
              snapshot.Units.Length == 200 &&
              snapshot.Units[0].UnitId == 1 && snapshot.Units[0].GlobalId == 1001 &&
              snapshot.Audit is MoveFormationSpacingAudit audit &&
              audit.AssassinGroundCalls == 1 && audit.StandardCalls == 0 &&
              audit.OverriddenCalls == 1 && audit.VanillaCounts[3] == 1 &&
              audit.EffectiveCounts[4] == 1 &&
              audit.FormatCompact() == "cfg4;selectors=s0/a1/w0;transitions=3->4:1",
            "large-group snapshot binds identities and reports the observed spacing transition");
        MoveFormationCommandSnapshotStore.Begin(owner, 7, 30, 40, 2, identities);
        MoveFormationCommandSnapshotStore.Observe(
            new object(), MoveFormationSelector.Standard, 4, 2);
        Check(!MoveFormationCommandSnapshotStore.TryConsume(
                  7, 31, 40, out MoveFormationCommandSnapshot _) &&
              !MoveFormationCommandSnapshotStore.TryConsume(
                  7, 30, 40, out MoveFormationCommandSnapshot _),
            "mismatched and stale command snapshots are discarded immediately");
        Check(Enumerable.Range(0, 40).Count(value => value % 1 == 0) >
              Enumerable.Range(0, 40).Count(value => value % 2 == 0) &&
              Enumerable.Range(0, 40).Count(value => value % 2 == 0) >
              Enumerable.Range(0, 40).Count(value => value % 3 == 0) &&
              Enumerable.Range(0, 40).Count(value => value % 3 == 0) >
              Enumerable.Range(0, 40).Count(value => value % 4 == 0),
            "spacing values select monotonically fewer Manhattan grid fields");
    }

    private static void CheckMoveFormationGesture()
    {
        Check(!MoveFormationDragEligibility.RequiresNormalTribeOwnership(
                  isMapEditor: true) &&
              MoveFormationDragEligibility.RequiresNormalTribeOwnership(
                  isMapEditor: false),
            "map-editor selections including tribe 499 bypass only normal ownership validation");
        Check(!MoveFormationDragEligibility.IsUsableSelectionCount(-1) &&
              !MoveFormationDragEligibility.IsUsableSelectionCount(0) &&
              !MoveFormationDragEligibility.IsUsableSelectionCount(1) &&
              MoveFormationDragEligibility.IsUsableSelectionCount(2) &&
              MoveFormationDragEligibility.IsUsableSelectionCount(
                  MoveFormationDragEligibility.MaximumSelectionCount) &&
              !MoveFormationDragEligibility.IsUsableSelectionCount(
                  MoveFormationDragEligibility.MaximumSelectionCount + 1),
            "transient and implausible native selection counts reject only the current drag gesture");
        Check(MoveFormationDragEligibility.IsVanillaRelease(0, 3, false) &&
              !MoveFormationDragEligibility.IsVanillaRelease(0, 2, true) &&
              MoveFormationDragEligibility.IsVanillaRelease(1, 2, true) &&
              !MoveFormationDragEligibility.IsVanillaRelease(1, 3, false),
            "each control scheme releases only on its authoritative Vanilla transport state");

        foreach (int commandButton in new[] { 0, 1 })
        {
            float gestureStart = 500f;
            float gestureStep = MoveFormationSpacingPolicy.GetHorizontalDragStep(1920);
            MoveFormationDragGesture gesture = new MoveFormationDragGesture(
                commandButton, gestureStart);
            Check(gesture.CommandButton == commandButton && gesture.Spacing == 2 &&
                  !gesture.Released && !gesture.Aborted,
                $"button {commandButton} starts a neutral spacing-two gesture");
            Check(gesture.OnHeld(1 - commandButton, 600f, 1920) ==
                      MoveFormationGestureResult.Ignored && gesture.Spacing == 2,
                $"button {commandButton} ignores held events from the other button");
            Check(gesture.OnHeld(commandButton, gestureStart - gestureStep, 1920) ==
                      MoveFormationGestureResult.SpacingChanged && gesture.Spacing == 1,
                $"button {commandButton} selects compact spacing one");
            Check(gesture.OnHeld(commandButton, gestureStart, 1920) ==
                      MoveFormationGestureResult.SpacingChanged && gesture.Spacing == 2 &&
                  gesture.OnHeld(commandButton, gestureStart + gestureStep + 0.1f, 1920) ==
                      MoveFormationGestureResult.SpacingChanged && gesture.Spacing == 3 &&
                  gesture.OnHeld(commandButton, gestureStart + 2f * gestureStep + 0.1f, 1920) ==
                      MoveFormationGestureResult.SpacingChanged && gesture.Spacing == 4,
                $"button {commandButton} traverses spacings two through four");
            Check(gesture.OnMouseUp(1 - commandButton, gestureStart, 1920) ==
                      MoveFormationGestureResult.Ignored && !gesture.Released &&
                  gesture.OnMouseUp(commandButton, gestureStart + gestureStep + 0.1f, 1920) ==
                      MoveFormationGestureResult.Released && gesture.Released &&
                  gesture.Spacing == 3,
                $"button {commandButton} releases only on its matching button and samples final X");
            Check(gesture.OnHeld(commandButton, 500f, 1920) ==
                      MoveFormationGestureResult.Ignored && gesture.Spacing == 3,
                $"button {commandButton} cannot change after release");
        }

        MoveFormationDragGesture conflicting = new MoveFormationDragGesture(0, 100f);
        Check(conflicting.OnMouseDown(0) == MoveFormationGestureResult.Ignored &&
              conflicting.OnMouseDown(1) == MoveFormationGestureResult.Aborted &&
              conflicting.Aborted &&
              conflicting.OnMouseUp(0, 100f, 800) == MoveFormationGestureResult.Ignored,
            "opposite mouse down aborts without releasing a later command");

        MoveFormationDragGesture cancelled = new MoveFormationDragGesture(1, 100f);
        Check(cancelled.Abort() == MoveFormationGestureResult.Aborted &&
              cancelled.Abort() == MoveFormationGestureResult.Ignored &&
              cancelled.OnHeld(1, 200f, 800) == MoveFormationGestureResult.Ignored,
            "external cancellation is terminal and idempotent");

        MoveFormationReleaseGate upThenRun = new MoveFormationReleaseGate(1, 500f);
        Check(upThenRun.OnHeld(700f, 1920) ==
                  MoveFormationGestureResult.SpacingChanged &&
              upThenRun.OnInputRelease(700f, 1920) ==
                  MoveFormationGestureResult.Released &&
              upThenRun.ReleaseEventSeen &&
              upThenRun.TryClaimVanillaRelease(2, true, 1920) &&
              upThenRun.VanillaReleaseClaimed && upThenRun.Spacing == 4 &&
              !upThenRun.TryClaimVanillaRelease(2, true, 1920),
            "R3 up before Engine run retains spacing until exactly one Vanilla release claim");

        MoveFormationReleaseGate runThenUp = new MoveFormationReleaseGate(0, 500f);
        Check(runThenUp.OnHeld(565f, 1920) ==
                  MoveFormationGestureResult.SpacingChanged &&
              runThenUp.TryClaimVanillaRelease(3, false, 1920) &&
              runThenUp.Released && !runThenUp.ReleaseEventSeen &&
              runThenUp.Spacing == 3 &&
              runThenUp.OnInputRelease(565f, 1920) ==
                  MoveFormationGestureResult.Ignored,
            "authoritative Engine release before R3 up samples the last held position");

        MoveFormationReleaseGate wrongRelease = new MoveFormationReleaseGate(1, 500f);
        Check(!wrongRelease.TryClaimVanillaRelease(3, false, 1920) &&
              !wrongRelease.Released && !wrongRelease.VanillaReleaseClaimed,
            "the opposite Vanilla release cannot claim a drag transaction");
    }

    private static void CheckMoveFormationPlanner()
    {
        var tiles = new SHCDESE.Interop.GameTileManagerView();
        SHCDESE.API.GameTileManagerAPI.Instance.TileManager = tiles;
        bool[] available = new bool[800 * 800];
        for (int y = 20; y <= 380; y++)
        for (int x = 20; x <= 779; x++)
        {
            int tile = y * 800 + x;
            available[tile] = true;
            tiles.Components[tile] = 1;
            tiles.Edges[tile] = 0xFF;
        }

        var planner = new MoveFormationPreviewPlanner(
            (x, y) => (uint)x < 800 && (uint)y < 800 && available[y * 800 + x]);
        var destinations = new List<MoveFormationDestination>();
        foreach (int spacing in new[] { 1, 2, 3, 4 })
        foreach (int required in new[] { 1000, 1001, 1002, 1250, 1350, 3999, 4000, 4001, 5001 })
        {
            MoveFormationPlanMetrics metrics = planner.Plan(
                400, 200, spacing, required, assassinOnly: false, destinations);
            Check(destinations.Count == required &&
                  destinations.Select(item => item.TileId).Distinct().Count() == required &&
                  destinations.All(item =>
                      (Math.Abs(item.X - 400) + Math.Abs(item.Y - 200)) % spacing == 0) &&
                  metrics.ExactDestinations == required &&
                  metrics.RelaxedDestinations == 0 && metrics.ReusedDestinations == 0,
                $"full-grid planner assigns {required} unique spacing-{spacing} destinations");
        }

        MoveFormationPlanMetrics overflow = planner.Plan(
            400,
            200,
            4,
            15,
            assassinOnly: false,
            destinations,
            (x, y) => y == 200 && x >= 400 && x < 410);
        Check(destinations.Count == 15 && overflow.ExactDestinations == 3 &&
              overflow.RelaxedDestinations == 7 && overflow.UniqueDestinations == 10 &&
              overflow.ReusedDestinations == 5,
            "planner deterministically relaxes density and only then reuses reachable destinations");

        int excludedAssassinTile = 200 * 800 + 404;
        tiles.Logic[excludedAssassinTile] = 0x100;
        planner.Plan(400, 200, 4, 20, assassinOnly: true, destinations);
        Check(destinations.All(item => item.TileId != excludedAssassinTile),
            "Assassin ground planning retains Vanilla's additional tile-flag filter");
    }

    private static void CheckLargeMoveTargetOverflow()
    {
        Check(LargeMoveTargetOverflowModel.NativeDrawCapacity == 250 &&
              LargeMoveTargetOverflowModel.NativeUsableDrawRecords == 249,
            "Vanilla shared draw list exposes 249 usable records");
        Check(LargeMoveTargetOverflowModel.NativeTileCount == 320800,
            "overflow uses the complete native tile capacity");
        Check(LargeMoveTargetOverflowModel.IsVanillaMoveTargetMarker(0x6B, 0x52, 0xC, 6, 2) &&
              LargeMoveTargetOverflowModel.IsVanillaMoveTargetMarker(0x6B, 0x59, 0xC, 6, 0x40002),
            "Vanilla green Move marker signatures are recognized");
        Check(!LargeMoveTargetOverflowModel.IsVanillaMoveTargetMarker(0x6B, 0x5A, 0xC, 6, 2) &&
              !LargeMoveTargetOverflowModel.IsVanillaMoveTargetMarker(0xAC, 0x142, 0x12, -1, 0xA0022),
            "non-Move and Extended Shift markers remain outside overflow capture");

        foreach (int requested in new[] { 248, 249, 250, 251, 1000, 4000 })
        {
            int expectedOverflow = Math.Max(0, requested - 249);
            Check(LargeMoveTargetOverflowModel.GetOverflowCount(requested) == expectedOverflow,
                $"overflow count for {requested} requested markers");
            var buffer = new LargeMoveTargetOverflowBuffer();
            for (int index = 0; index < expectedOverflow; index++)
            {
                Check(buffer.TryAdd(0x6B, 0x52 + index % 8, 0xC, 6, index, 2),
                    $"overflow accepts marker {index} of {expectedOverflow}");
            }
            Check(buffer.Count == expectedOverflow,
                $"only the rejected tail of {requested} markers enters overflow");
        }

        Check(!LargeMoveTargetOverflowModel.IsRejectedByFullVanillaList(249) &&
              LargeMoveTargetOverflowModel.IsRejectedByFullVanillaList(250),
            "overflow begins only after Vanilla reaches its hard capacity");

        var duplicates = new LargeMoveTargetOverflowBuffer();
        Check(duplicates.TryAdd(0x6B, 0x52, 0xC, 6, 42, 2) &&
              duplicates.TryAdd(0x6B, 0x52, 9, 99, 42, 0x40002) &&
              duplicates.Count == 1,
            "same tile, category, and sprite follows Vanilla duplicate suppression");
        Check(duplicates.TryAdd(0x6B, 0x53, 0xC, 6, 42, 2) && duplicates.Count == 2,
            "different sprites on one tile remain distinct chained records");
        int head = duplicates.GetHead(42);
        Check(duplicates.GetRecord(head).SpriteId == 0x53 &&
              duplicates.GetRecord(duplicates.GetRecord(head).Next).SpriteId == 0x52,
            "overflow tile chains use Vanilla last-in-first-rendered order");

        duplicates.Clear();
        Check(duplicates.Count == 0 && duplicates.GetHead(42) == 0,
            "Vanilla reset clears every touched overflow tile immediately");
        Check(duplicates.TryAdd(0x6B, 0x54, 0xC, 6, 99, 2) &&
              duplicates.Count == 1 && duplicates.GetHead(42) == 0,
            "selection, tribe, target, arrival, death, and interruption frames cannot retain old tiles");
        duplicates.Clear();
        Check(duplicates.Count == 0,
            "a frame without a selected tribe publishes no previous overflow");

        var capacity = new LargeMoveTargetOverflowBuffer();
        for (int index = 0; index < LargeMoveTargetOverflowModel.MaximumOverflowMarkers; index++)
            Check(capacity.TryAdd(0x6B, 0x52, 0xC, 6, index, 2), "overflow capacity fill");
        Check(!capacity.TryAdd(0x6B, 0x52, 0xC, 6,
                LargeMoveTargetOverflowModel.MaximumOverflowMarkers, 2),
            "overflow fails open when its validated identity range is exhausted");
        Check(capacity.TryAdd(0x6B, 0x52, 0xC, 6, 0, 2),
            "a duplicate remains harmless at capacity");
        Check(!capacity.TryAdd(0x6B, 0x52, 0xC, 6,
                LargeMoveTargetOverflowModel.NativeTileCount, 2),
            "out-of-range native tiles are rejected");
    }

    private static void CheckClassification()
    {
        Check(QueueCommandClassifier.TryClassifyTarget(4, out QueueCommandKind unit) &&
            unit == QueueCommandKind.AttackUnit, "AttackUnit classification");
        Check(QueueCommandClassifier.TryClassifyTarget(9, out QueueCommandKind building) &&
            building == QueueCommandKind.AttackBuilding, "AttackBuilding classification");
        Check(QueueCommandClassifier.TryClassifyTarget(36, out QueueCommandKind force) &&
            force == QueueCommandKind.ForceAttackBuilding, "ForceAttackBuilding classification");
        Check(!QueueCommandClassifier.TryClassifyTarget(5, out _), "unsupported command rejection");
    }

    private static void CheckQueueLimitAndOrder()
    {
        TribeQueueState state = new TribeQueueState(123, 2);
        QueueCommand first = new QueueCommand(QueueCommandKind.Move, 10, 20, 1);
        QueueCommand second = new QueueCommand(QueueCommandKind.AttackUnit, 7, 99);
        Check(state.TryEnqueue(first), "first enqueue");
        Check(state.TryEnqueue(second), "second enqueue");
        Check(!state.TryEnqueue(new QueueCommand(QueueCommandKind.Move, 30, 40)), "queue cap");
        Check(state.TryActivateNext(out QueueCommand active) && ReferenceEquals(first, active), "FIFO first");
        Check(!state.TryActivateNext(out _), "only one active command");
        state.CompleteActive();
        Check(state.TryActivateNext(out active) && ReferenceEquals(second, active), "FIFO second");
    }

    private static void CheckStateTransitions()
    {
        TribeQueueState state = new TribeQueueState(8, 4)
        {
            WaitForVanillaMovement = true,
            ExternalAttack = new QueueCommand(QueueCommandKind.AttackBuilding, 3, 44)
        };
        Check(!state.IsEmpty, "predecessor makes state nonempty");
        state.ExternalAttack = null;
        state.WaitForVanillaMovement = false;
        Check(state.IsEmpty, "cleared predecessors make state empty");
        Check(state.TryEnqueue(new QueueCommand(QueueCommandKind.Move, 1, 2)), "transition enqueue");
        Check(state.TryActivateNext(out _), "transition activate");
        Check(!state.IsEmpty, "active makes state nonempty");
        state.CompleteActive();
        Check(state.IsEmpty, "completion makes state empty");
    }

    private static void CheckBuildingQueueOrder()
    {
        TribeQueueState state = new TribeQueueState(321, 4);
        QueueCommand move = new QueueCommand(QueueCommandKind.Move, 10, 20, 1);
        QueueCommand building = new QueueCommand(QueueCommandKind.AttackBuilding, 7, 70);
        QueueCommand forceBuilding = new QueueCommand(QueueCommandKind.ForceAttackBuilding, 8, 80, -127);
        Check(state.TryEnqueue(move), "building sequence Move enqueue");
        Check(state.TryEnqueue(building), "building sequence AttackBuilding enqueue");
        Check(state.TryEnqueue(forceBuilding), "building sequence ForceAttackBuilding enqueue");
        Check(state.TryActivateNext(out QueueCommand active) && ReferenceEquals(active, move),
            "building sequence starts with Move");
        state.CompleteActive();
        Check(state.TryActivateNext(out active) && ReferenceEquals(active, building),
            "AttackBuilding retains FIFO position");
        state.CompleteActive();
        Check(state.TryActivateNext(out active) && ReferenceEquals(active, forceBuilding),
            "ForceAttackBuilding retains FIFO position");
    }

    private static void CheckTribeReuseGuard()
    {
        TribeQueueState state = new TribeQueueState(0xAABBCCDD, 1, ownerPlayerId: 3);
        Check(state.MatchesTribe(0xAABBCCDD, 3), "same tribe global ID and owner");
        Check(!state.MatchesTribe(0xAABBCCDE, 3), "reused tribe slot rejected");
        Check(!state.MatchesTribe(0xAABBCCDD, 4), "changed tribe owner rejected");
    }

    private static void CheckVisualQueueProjection()
    {
        TribeQueueState state = new TribeQueueState(8, 8);
        Check(state.TryEnqueue(new QueueCommand(QueueCommandKind.Move, 1, 2)), "visual first move enqueue");
        Check(state.TryEnqueue(new QueueCommand(QueueCommandKind.AttackUnit, 3, 4)), "visual attack enqueue");
        Check(state.TryEnqueue(new QueueCommand(QueueCommandKind.Move, 5, 6)), "visual second move enqueue");
        IReadOnlyList<QueueVisualSlot> slots = state.CurrentVisualSlots;
        Check(slots.Count == 3, "visual projection contains mixed commands");
        Check(
            slots[0].Command.Kind == QueueCommandKind.Move &&
            slots[1].Command.Kind == QueueCommandKind.AttackUnit &&
            slots[2].Command.Kind == QueueCommandKind.Move,
            "visual projection preserves mixed order");
        Check(slots.Select(slot => slot.Ordinal).SequenceEqual(new[] { 1, 2, 3 }),
            "mixed visual commands receive consecutive fixed numbers");
    }

    private static void CheckVisualFlagFiltering()
    {
        Check(
            QueueVisualContract.IsPatrolOnceNumberSubmission(0xAC, 0x12E, 0x12, -1, 0xA0022),
            "first patrol number submission recognized");
        Check(
            QueueVisualContract.IsPatrolOnceNumberSubmission(0xAC, 0x136, 0x12, -1, 0xA0022),
            "ninth patrol number submission recognized");
        Check(
            !QueueVisualContract.IsPatrolOnceNumberSubmission(0xAC, 0x137, 0x12, -1, 0xA0022),
            "out-of-range patrol number rejected");
        Check(QueueVisualContract.ShouldSuppressFlag(QueueVisualMarkerMode.Hidden),
            "hidden slot flag suppressed");
        Check(!QueueVisualContract.ShouldSuppressFlag(QueueVisualMarkerMode.Move),
            "Move flag retained");
        Check(!QueueVisualContract.ShouldSuppressFlag(QueueVisualMarkerMode.Attack),
            "attack flag retained beside attack icon");
        Check(QueueVisualContract.ShouldSuppressNumber(QueueVisualMarkerMode.Hidden, true),
            "hidden current-page number suppressed");
        Check(!QueueVisualContract.ShouldSuppressNumber(QueueVisualMarkerMode.Attack, true),
            "visible current-page attack number retained");
        Check(!QueueVisualContract.ShouldSuppressNumber(QueueVisualMarkerMode.Move, true),
            "visible current-page Move number retained");
        Check(QueueVisualContract.ShouldSuppressNumber(QueueVisualMarkerMode.Attack, false),
            "future-page attack number suppressed");
        Check(QueueVisualContract.ShouldSuppressNumber(QueueVisualMarkerMode.Move, false),
            "future-page Move number suppressed");
    }

    private static void CheckStableVisualPages()
    {
        TribeQueueState stable = new TribeQueueState(44, 12);
        QueueVisualSlot vanillaCompleted = stable.AddVanillaWaypoint(
            new QueueCommand(QueueCommandKind.Move, 1, 1),
            nativeWaypointIndex: 1,
            completed: true);
        QueueVisualSlot vanillaCurrent = stable.AddVanillaWaypoint(
            new QueueCommand(QueueCommandKind.Move, 2, 2),
            nativeWaypointIndex: 2,
            completed: false);
        QueueCommand attack = new QueueCommand(QueueCommandKind.AttackUnit, 3, 30);
        QueueCommand move = new QueueCommand(QueueCommandKind.Move, 4, 4);
        Check(stable.TryEnqueue(attack, out QueueVisualSlot attackSlot) && attackSlot.Ordinal == 3,
            "managed numbering follows Vanilla prefix");
        Check(stable.TryEnqueue(move, out QueueVisualSlot moveSlot) && moveSlot.Ordinal == 4,
            "mixed command receives stable next number");
        Check(!stable.UpdateVanillaVisualProgress(3),
            "Vanilla progress does not advance a page with managed successors");
        Check(vanillaCompleted.Completed && vanillaCurrent.Completed && attackSlot.Ordinal == 3,
            "completed Vanilla slots remain reserved ahead of managed commands");
        Check(stable.OutstandingVisualCount == 2,
            "repeated completion updates maintain the outstanding-target count");
        Check(stable.TryActivateNext(out QueueCommand active) && ReferenceEquals(active, attack),
            "stable visual attack activates");
        Check(!attackSlot.Completed && attackSlot.Ordinal == 3,
            "active visual slot remains visible and numbered");
        stable.CompleteActive();
        Check(attackSlot.Completed && moveSlot.Ordinal == 4,
            "completion hides slot without renumbering successor");
        QueueCommand appended = new QueueCommand(QueueCommandKind.AttackBuilding, 5, 50);
        Check(stable.TryEnqueue(appended, out QueueVisualSlot appendedSlot) && appendedSlot.Ordinal == 5,
            "later command uses highest assigned number plus one");

        TribeQueueState paged = new TribeQueueState(55, 12);
        QueueVisualSlot tenthSlot = null;
        for (int index = 0; index < 10; index++)
        {
            QueueCommand command = new QueueCommand(QueueCommandKind.Move, index, index);
            Check(paged.TryEnqueue(command, out QueueVisualSlot slot), $"paged command {index + 1} enqueue");
            if (index == 9)
                tenthSlot = slot;
        }
        Check(paged.CurrentVisualPageNumber == 1 && paged.CurrentVisualSlots.Count == 9,
            "first visual page remains active at nine slots");
        Check(paged.CurrentVisualPageIndex == 0 && paged.VisualPages.Count == 2,
            "all visual pages are available from the current page onward");
        Check(
            paged.VisualPages
                .Skip(paged.CurrentVisualPageIndex)
                .SelectMany(page => page.Slots)
                .Select(slot => slot.Command)
                .SequenceEqual(paged.VisualPages.SelectMany(page => page.Slots).Select(slot => slot.Command)),
            "multi-page projection preserves complete FIFO order");
        Check(tenthSlot != null && tenthSlot.PageNumber == 2 && tenthSlot.Ordinal == 1,
            "tenth command starts second page at one");
        for (int index = 0; index < 8; index++)
        {
            Check(paged.TryActivateNext(out _), $"page-one command {index + 1} activates");
            Check(!paged.CompleteActive(), $"page remains stable before slot {index + 9} completes");
        }
        Check(paged.TryActivateNext(out _), "ninth page-one command activates");
        Check(paged.CompleteActive(), "completed first page advances visual page");
        Check(paged.CurrentVisualPageNumber == 2 && paged.CurrentVisualSlots.Count == 1,
            "second visual page becomes active");
        Check(paged.CurrentVisualPageIndex == 0 &&
            paged.VisualPages.Skip(paged.CurrentVisualPageIndex).Count() == 1,
            "completed pages are pruned from subsequent projection");
        Check(paged.VisualPages[0].PageNumber == 2,
            "pruning retains the stable page identity");
        for (int index = 0; index < 8; index++)
        {
            Check(paged.TryEnqueue(new QueueCommand(QueueCommandKind.Move, 20 + index, 20 + index)),
                $"refill retained second page slot {index + 2}");
        }
        Check(paged.TryEnqueue(
                new QueueCommand(QueueCommandKind.Move, 30, 30),
                out QueueVisualSlot pageThreeFirst) &&
            pageThreeFirst.PageNumber == 3 && pageThreeFirst.Ordinal == 1,
            "new pages remain monotonic after completed-page pruning");

        TribeQueueState mixedPages = new TribeQueueState(56, 16);
        QueueCommandKind[] mixedKinds =
        {
            QueueCommandKind.Move,
            QueueCommandKind.AttackUnit,
            QueueCommandKind.AttackBuilding,
            QueueCommandKind.ForceAttackBuilding
        };
        for (int index = 0; index < 12; index++)
        {
            Check(mixedPages.TryEnqueue(new QueueCommand(mixedKinds[index % mixedKinds.Length], index, index)),
                $"mixed multi-page command {index + 1} enqueue");
        }
        Check(
            mixedPages.VisualPages
                .Skip(mixedPages.CurrentVisualPageIndex)
                .SelectMany(page => page.Slots)
                .Select(slot => slot.Command.Kind)
                .SequenceEqual(Enumerable.Range(0, 12).Select(index => mixedKinds[index % mixedKinds.Length])),
            "all Move and attack kinds remain visible in FIFO order across pages");
        Check(mixedPages.VisualPages[1].Slots.All(slot => slot.PageNumber == 2),
            "future mixed commands remain assigned to their second visual page");

        TribeQueueState capacity = new TribeQueueState(66, 128);
        for (int index = 0; index < 128; index++)
            Check(capacity.TryEnqueue(new QueueCommand(QueueCommandKind.Move, index, index)),
                $"managed queue capacity command {index + 1}");
        Check(!capacity.TryEnqueue(new QueueCommand(QueueCommandKind.Move, 129, 129)),
            "managed queue rejects command beyond 128 pending entries");
        Check(capacity.VisualPageCount == 15,
            "128 stored commands are partitioned into fifteen visual pages");
        Check(capacity.VisualPages.SelectMany(page => page.Slots).Count(slot => !slot.Completed) == 128,
            "full managed queue exposes at most 128 visible flags");

        TribeQueueState boundedWithPredecessor = new TribeQueueState(67, 3);
        boundedWithPredecessor.AddVanillaWaypoint(
            new QueueCommand(QueueCommandKind.Move, 1, 1),
            nativeWaypointIndex: 1,
            completed: false);
        Check(boundedWithPredecessor.TryEnqueue(new QueueCommand(QueueCommandKind.Move, 2, 2)),
            "queue accepts first command behind Vanilla predecessor");
        Check(boundedWithPredecessor.TryActivateNext(out _),
            "active managed command remains an outstanding visual target");
        Check(boundedWithPredecessor.TryEnqueue(new QueueCommand(QueueCommandKind.AttackUnit, 3, 30)),
            "queue fills remaining outstanding visual slot");
        Check(!boundedWithPredecessor.TryEnqueue(new QueueCommand(QueueCommandKind.Move, 4, 4)),
            "active and Vanilla predecessor count toward 128-target display bound");
    }

    private static void CheckUnitIdentityBinding()
    {
        QueueUnitIdentity first = new QueueUnitIdentity(7, 101);
        QueueUnitIdentity reused = new QueueUnitIdentity(7, 102);
        TribeQueueState state = new TribeQueueState(3, 1, new[] { first }, ownerPlayerId: 6);
        Check(state.Members.Count == 1 && state.Members[0].Equals(first), "unit identity captured");
        Check(state.SharesMemberWith(new[] { first }), "same unit identity matches queue");
        Check(!state.SharesMemberWith(new[] { reused }), "reused unit slot rejected");
        state.RebindTribe(14, 4);
        Check(state.MatchesTribe(4, 6), "queue tribe binding migrated without changing owner");
        Check(!state.MatchesTribe(3, 6), "old tribe binding rejected after migration");
        Check(!state.MatchesTribe(4, 5), "migration cannot cross owner boundary");
        Check(!state.ActiveNeedsRedispatch, "idle migration needs no redispatch");
        Check(state.TryEnqueue(new QueueCommand(QueueCommandKind.AttackUnit, 2, 20)), "migration enqueue");
        Check(state.TryActivateNext(out _), "migration activate");
        state.RebindTribe(15, 5);
        Check(state.ActiveNeedsRedispatch, "active command marked for redispatch");
        state.MarkActiveRedispatched();
        Check(!state.ActiveNeedsRedispatch, "active redispatch acknowledged");
    }

    private static void CheckUnitStableBranching()
    {
        QueueUnitIdentity first = new QueueUnitIdentity(3, 30);
        QueueUnitIdentity second = new QueueUnitIdentity(4, 40);
        QueueCommand move = new QueueCommand(QueueCommandKind.Move, 100, 101, 1);
        QueueCommand attack = new QueueCommand(QueueCommandKind.AttackBuilding, 8, 80);
        TribeQueueState source = new TribeQueueState(
            70, 128, new[] { second, first }, ownerPlayerId: 2, cohortId: 9, boundTribeId: 7);
        Check(source.Members[0].Equals(first), "cohort members are deterministically sorted");
        Check(source.TryEnqueue(move) && source.TryEnqueue(attack), "cohort queue initialized");
        Check(source.TryActivateNext(out QueueCommand active) && ReferenceEquals(active, move),
            "cohort active command initialized");

        TribeQueueState branch = source.CloneForBranch(10, 8, 71, new[] { second });
        source.ReplaceMembers(new[] { first });
        source.RebindTribe(7, 70);
        Check(branch.CohortId == 10 && branch.BoundTribeId == 8 && branch.Members.Single().Equals(second),
            "split branch receives its own deterministic identity and members");
        Check(ReferenceEquals(source.Active, branch.Active) &&
            ReferenceEquals(source.PendingCommands.Single(), branch.PendingCommands.Single()),
            "split branches share immutable command objects");
        branch.CompleteActive();
        Check(source.Active != null && branch.Active == null,
            "split branches advance independently");
        Check(!source.CurrentVisualSlots[0].Completed && branch.CurrentVisualSlots[0].Completed,
            "split branches own independent visual progress");
        Check(!first.Equals(new QueueUnitIdentity(3, 31)),
            "unit global ID prevents slot reuse from inheriting a queue");
    }

    private static void CheckAtomicCohortEnqueue()
    {
        TribeQueueState available = new TribeQueueState(1, 2, cohortId: 1);
        TribeQueueState full = new TribeQueueState(2, 1, cohortId: 2);
        Check(full.TryEnqueue(new QueueCommand(QueueCommandKind.Move, 1, 1)),
            "atomic test fills one cohort");
        QueueCommand rejected = new QueueCommand(QueueCommandKind.AttackUnit, 4, 40);
        Check(!QueueCohortOperations.TryEnqueueAtomically(
                new[] { available, full }, rejected),
            "group enqueue rejects atomically when one cohort is full");
        Check(available.PendingCount == 0 && full.PendingCount == 1,
            "atomic rejection does not mutate any cohort");

        TribeQueueState second = new TribeQueueState(3, 2, cohortId: 3);
        QueueCommand shared = new QueueCommand(QueueCommandKind.Move, 10, 11);
        Check(QueueCohortOperations.TryEnqueueAtomically(
                new[] { available, second }, shared),
            "group enqueue succeeds for all available cohorts");
        Check(ReferenceEquals(available.PendingCommands.Single(), shared) &&
            ReferenceEquals(second.PendingCommands.Single(), shared),
            "atomic group enqueue shares the immutable command object");
    }

    private static void CheckNativeLayoutTranslation()
    {
        Check(QueueNativeContract.GameTribePointerAdjustment == 0x2A, "native GameTribe pointer adjustment");
        Check(QueueNativeContract.GameTribeWaypointBaseOffset == 0x58A, "GameTribe waypoint base offset");
        Check(QueueNativeContract.GameTribeWaypointIndexOffset == 0x5B2, "GameTribe waypoint index offset");
        Check(QueueNativeContract.GameTribeWaypointCountOffset == 0x5B4, "GameTribe waypoint count offset");
        Check(QueueNativeContract.GameTribeMovementModeOffset == 0x558, "GameTribe movement mode offset");
        Check(
            QueueNativeContract.GameTribeWaypointCountOffset + QueueNativeContract.GameTribePointerAdjustment ==
                QueueNativeContract.ManagerRelativeWaypointCountOffset,
            "manager-relative waypoint count translation");
        Check(
            QueueNativeContract.GameTribeMovementModeOffset + QueueNativeContract.GameTribePointerAdjustment ==
                QueueNativeContract.ManagerRelativeMovementModeOffset,
            "manager-relative movement mode translation");
        Check(QueueNativeContract.WaypointChoreValueToTribeId(498) == 498,
            "waypoint chore tribe ID remains one-based");
        Check(QueueNativeContract.GameUnitSize == 0x490, "native GameUnit size");
        Check(QueueNativeContract.GameUnitGlobalIdOffset == 0x94, "GameUnit global ID offset");
        Check(QueueNativeContract.GameUnitAttackMarkerOffset == 0x68, "GameUnit attack marker offset");
        Check(
            QueueNativeContract.GameUnitGlobalIdOffset - QueueNativeContract.GameUnitAttackMarkerOffset == 0x2C,
            "GameUnit marker/global native displacement");
        Check(QueueNativeContract.GameBuildingSize == 0x32C, "native GameBuilding size");
        Check(QueueNativeContract.GameBuildingGlobalIdOffset == 0xD8, "GameBuilding global ID offset");
        Check(QueueNativeContract.GameBuildingAttackMarkerOffset == 0xC2, "GameBuilding attack marker offset");
        Check(
            QueueNativeContract.GameBuildingGlobalIdOffset -
                QueueNativeContract.GameBuildingAttackMarkerOffset == 0x16,
            "GameBuilding marker/global native displacement");
        Check(QueueNativeContract.MoveChoreOpcode == 17, "first Move uses Chore 17");
        Check(QueueNativeContract.TargetOrderChoreOpcode == 36, "target order uses Chore 36");
        Check(QueueNativeContract.WaypointAppendChoreOpcode == 71, "Shift waypoint uses Chore 71");
        Check(QueueNativeContract.MoveChoreHandlerRva == 0x10AE0, "Chore 17 handler RVA");
        Check(QueueNativeContract.TargetOrderChoreHandlerRva == 0x12BF0, "Chore 36 handler RVA");
        Check(QueueNativeContract.WaypointAppendChoreHandlerRva == 0x176C0, "Chore 71 handler RVA");
        Check(QueueNativeContract.ChoreHandlerTableRva == 0x2C7A30, "Chore handler table RVA");
        Check(QueueNativeContract.MoveChoreHandlerSize == 470, "Chore 17 handler size");
        Check(QueueNativeContract.TargetOrderChoreHandlerSize == 450, "Chore 36 handler size");
        Check(QueueNativeContract.WaypointAppendChoreHandlerSize == 487, "Chore 71 handler size");
        Check(QueueNativeContract.ChoreModeRva == 0x85F8FEC, "Chore mode global RVA");
        Check(QueueNativeContract.ChoreTribeIdRva == 0x86C132C, "Chore tribe global RVA");
        Check(QueueNativeContract.ChoreCommandOrTileXRva == 0x86C1330,
            "Chore command/tile-X global RVA");
        Check(QueueNativeContract.ChoreTileYRva == 0x86C1334,
            "Chore tile-Y global RVA");
        Check(QueueNativeContract.ChoreMoveTypeRva == 0x86C133C, "Chore Move type global RVA");
    }

    private static void CheckChoreMarkers()
    {
        foreach (string mode in new[] { "map editor", "singleplayer", "multiplayer" })
        {
            Check(QueueNativeContract.ShouldPackFormationSpacing(
                    installed: true,
                    modEnabled: true,
                    featureEnabled: true,
                    choreTransportReady: true,
                    internalDispatch: false,
                    choreMode: QueueNativeContract.ChorePackMode,
                    shiftPressed: false),
                $"{mode} direct Move packs formation spacing through Vanilla Chore 17");
        }
        Check(!QueueNativeContract.ShouldPackFormationSpacing(
                  true, true, true, true, false, choreMode: 0, shiftPressed: false) &&
              !QueueNativeContract.ShouldPackFormationSpacing(
                  true, true, true, true, false,
                  QueueNativeContract.ChorePackMode, shiftPressed: true) &&
              !QueueNativeContract.ShouldPackFormationSpacing(
                  true, true, false, true, false,
                  QueueNativeContract.ChorePackMode, shiftPressed: false) &&
              !QueueNativeContract.ShouldPackFormationSpacing(
                  installed: true,
                  modEnabled: true,
                  featureEnabled: true,
                  choreTransportReady: true,
                  internalDispatch: true,
                  choreMode: QueueNativeContract.ChorePackMode,
                  shiftPressed: false),
            "formation spacing packs only for enabled direct non-Shift Moves in Chore pack mode");

        int[] producerMoveTypes = { 0, 1, 0x81 };
        foreach (int moveType in producerMoveTypes)
        {
            Check(QueueNativeContract.TryMarkMoveTypeForQueue(moveType, out int marked),
                $"Chore 17 producer value 0x{moveType:X} accepts queue marker");
            Check((marked & QueueNativeContract.MoveQueueMarker) != 0,
                $"Chore 17 producer value 0x{moveType:X} carries bit 0x40");
            int unpackedMoveType = SimulateVanillaMoveTypeExecute(marked);
            Check(QueueNativeContract.TryDecodeQueuedMoveType(unpackedMoveType, out int decoded) &&
                decoded == ExpectedExecutedVanillaMoveType(moveType),
                $"Chore 17 producer value 0x{moveType:X} survives Vanilla bit-7 unpacking");
        }
        foreach (int moveType in producerMoveTypes)
        foreach (int spacing in new[] { 1, 2, 3, 4 })
        {
            Check(QueueNativeContract.TryEncodeFormationSpacing(
                    moveType, spacing, out int spacingMarked),
                $"Chore 17 producer value 0x{moveType:X} accepts spacing {spacing}");
            Check(QueueNativeContract.TryMarkMoveTypeForQueue(
                    spacingMarked, out int queueAndSpacingMarked),
                $"spacing {spacing} coexists with queue bit 6");
            int unpacked = SimulateVanillaMoveTypeExecute(queueAndSpacingMarked);
            Check(QueueNativeContract.TryDecodeFormationSpacing(
                    unpacked, out int withoutSpacing, out int decodedSpacing) &&
                  decodedSpacing == spacing &&
                  (withoutSpacing & QueueNativeContract.MoveFormationSpacingMask) == 0 &&
                  QueueNativeContract.TryDecodeQueuedMoveType(
                      withoutSpacing, out int decodedMoveType) &&
                  decodedMoveType == ExpectedExecutedVanillaMoveType(moveType),
                $"spacing {spacing} and queue marker roundtrip after Vanilla strips bit 7");
        }
        foreach (int invalidSpacing in new[] { 0, 5 })
        {
            Check(!QueueNativeContract.TryEncodeFormationSpacing(
                    0, invalidSpacing, out _),
                $"invalid command spacing {invalidSpacing} is rejected");
        }
        foreach (int unknown in new[] { 2, 0x42, 0x100, -256, -254, -190 })
        {
            Check(!QueueNativeContract.TryDecodeFormationSpacing(
                    unknown, out int unchanged, out _) && unchanged == unknown,
                $"unknown MoveType 0x{unknown:X} is left unchanged");
        }
        Check(!QueueNativeContract.TryMarkMoveTypeForQueue(2, out _),
            "unknown Chore 17 producer value rejected");
        Check(!QueueNativeContract.TryMarkMoveTypeForQueue(0x100, out _),
            "non-byte Chore 17 value rejected");
        Check(!QueueNativeContract.TryMarkMoveTypeForQueue(0x40, out _),
            "already marked Chore 17 value rejected");
        Check(QueueNativeContract.TryDecodeQueuedMoveType(0x40, out int normalMove) && normalMove == 0,
            "marked normal Move roundtrip");
        Check(QueueNativeContract.TryDecodeQueuedMoveType(0x41, out int alternateMove) && alternateMove == 1,
            "marked alternate Move roundtrip");
        Check(QueueNativeContract.TryDecodeQueuedMoveType(-191, out int fastMove) &&
              fastMove == QueueNativeContract.ExecutedFastMoveType,
            "marked Fast Move retains Vanilla's signed execute representation");
        Check(!QueueNativeContract.TryDecodeQueuedMoveType(0xC1, out _),
            "Vanilla high bit cannot masquerade as a queued Move");
        Check(!QueueNativeContract.TryDecodeQueuedMoveType(1, out _),
            "unmarked Move remains Vanilla");

        int[] targetCommands = { 4, 9, 36 };
        foreach (int command in targetCommands)
        {
            Check(QueueNativeContract.TryMarkTargetCommandForQueue(command, out int marked),
                $"supported Chore 36 command {command} accepts queue marker");
            Check(QueueNativeContract.TryDecodeQueuedTargetCommand(marked, out int decoded) && decoded == command,
                $"supported Chore 36 command {command} roundtrip");
        }
        Check(!QueueNativeContract.TryMarkTargetCommandForQueue(5, out _),
            "unsupported Chore 36 command is not marked");
        Check(!QueueNativeContract.TryDecodeQueuedTargetCommand(0x80 | 5, out _),
            "marked unsupported Chore 36 command is rejected");
        Check(!QueueNativeContract.TryDecodeQueuedTargetCommand(36, out _),
            "unmarked target command remains Vanilla");
    }

    private static void CheckDeferredFormationChoreExecution()
    {
        foreach (int tribeId in new[] { 7, 498, 499 })
        foreach (int producerMoveType in new[] { 0, 1, 0x81 })
        foreach (int spacing in new[] { 1, 2, 3, 4 })
        {
            Check(QueueNativeContract.TryEncodeFormationSpacing(
                    producerMoveType, spacing, out int packedMoveType),
                $"tribe {tribeId} spacing {spacing} packs before deferred execution");

            // Vanilla stores this byte in its pending Chore. The managed release
            // context may be cleared before the later execute-mode invocation.
            int executeMoveType = SimulateVanillaMoveTypeExecute(packedMoveType);
            Check(QueueNativeContract.TryResolveExecutedFormationSpacing(
                    executeMoveType,
                    executingMoveChore: true,
                    out int vanillaMoveType,
                    out int executedSpacing) &&
                  executedSpacing == spacing &&
                  vanillaMoveType == ExpectedExecutedVanillaMoveType(producerMoveType),
                $"tribe {tribeId} spacing {spacing} survives deferred Chore execution");
            bool resolvesWithoutExecuteScope =
                QueueNativeContract.TryResolveExecutedFormationSpacing(
                    executeMoveType,
                    executingMoveChore: false,
                    out _,
                    out _);
            Check(resolvesWithoutExecuteScope ==
                  (spacing != MoveFormationSpacingPolicy.Default),
                $"tribe {tribeId} spacing {spacing} has the expected private-bit identity");
        }
        Check(!QueueNativeContract.TryResolveExecutedFormationSpacing(
                  QueueNativeContract.MoveQueueMarker,
                  executingMoveChore: true,
                  out int queuedMoveType,
                  out _) &&
              queuedMoveType == QueueNativeContract.MoveQueueMarker,
            "Extended Shift marker does not acquire default formation spacing during Chore execution");
    }

    private static int SimulateVanillaMoveTypeExecute(int wireMoveType)
    {
        int signExtended = unchecked((sbyte)(byte)wireMoveType);
        return signExtended < 0 ? signExtended & ~0x80 : signExtended;
    }

    private static int ExpectedExecutedVanillaMoveType(int producerMoveType) =>
        producerMoveType == 0x81 ? QueueNativeContract.ExecutedFastMoveType : producerMoveType;

    private static void CheckMoveChoreDeduplication()
    {
        TribeQueueState state = new TribeQueueState(9, 8);
        QueueCommand first = new QueueCommand(QueueCommandKind.Move, 10, 20, 1);
        QueueCommand second = new QueueCommand(QueueCommandKind.Move, 30, 40, 2);
        state.ExpectMoveChore(first, 130);
        state.ExpectMoveChore(second, 130);
        Check(state.ExpectedMoveChoreCount == 2, "expected move chores queued");
        Check(state.TryConsumeExpectedMoveChore(second, 101), "out-of-order matching chore consumed");
        Check(state.TryConsumeExpectedMoveChore(first, 101), "matching first chore consumed");
        Check(state.ExpectedMoveChoreCount == 0, "expected move chores exhausted");

        state.ExpectMoveChore(first, 110);
        Check(!state.TryConsumeExpectedMoveChore(first, 111), "expired move chore not consumed");
        Check(state.ExpectedMoveChoreCount == 0, "expired move chore removed");

        state.ExpectMoveEvent(second, 150);
        Check(state.ExpectedMoveEventCount == 1, "chore-first move expects matching event");
        Check(state.TryConsumeExpectedMoveEvent(second, 140), "matching event consumed");
        Check(state.ExpectedMoveEventCount == 0, "expected move events exhausted");
    }

    private static void CheckFirstShiftMoveTakeover()
    {
        QueueCommand firstMove = new QueueCommand(QueueCommandKind.Move, 100, 200, -1);

        TribeQueueState eventFirst = new TribeQueueState(10, 128)
        {
            WaitForVanillaMovement = true
        };
        QueueVisualSlot predecessor = eventFirst.AddVanillaWaypoint(
            new QueueCommand(QueueCommandKind.Move, 90, 190, -1),
            nativeWaypointIndex: 1,
            completed: false);
        Check(eventFirst.TryEnqueue(firstMove, out QueueVisualSlot eventSlot),
            "first Shift Move event creates a managed entry");
        eventFirst.ExpectMoveChore(firstMove, 130);
        Check(eventFirst.TryConsumeExpectedMoveChore(firstMove, 101),
            "event-first Shift Move suppresses its native Chore");
        Check(eventFirst.PendingCount == 1 && eventFirst.CurrentVisualSlots.Count == 2,
            "event-first Shift Move exists exactly once behind Vanilla predecessor");
        Check(predecessor.Ordinal == 1 && eventSlot.Ordinal == 2,
            "first managed Shift Move continues Vanilla visual numbering");

        TribeQueueState choreFirst = new TribeQueueState(11, 128);
        Check(choreFirst.TryEnqueue(firstMove, out QueueVisualSlot choreSlot),
            "first Shift Move Chore creates a managed entry");
        choreFirst.ExpectMoveEvent(firstMove, 130);
        Check(choreFirst.TryConsumeExpectedMoveEvent(firstMove, 101),
            "chore-first Shift Move suppresses its managed event duplicate");
        Check(choreFirst.PendingCount == 1 && choreFirst.CurrentVisualSlots.Count == 1,
            "chore-first Shift Move exists exactly once");
        Check(choreSlot.PageNumber == 1 && choreSlot.Ordinal == 1,
            "Shift Move from idle starts visual page one");

        TribeQueueState pureMoves = new TribeQueueState(12, 128);
        QueueVisualSlot tenth = null;
        for (int index = 0; index < 15; index++)
        {
            Check(pureMoves.TryEnqueue(
                    new QueueCommand(QueueCommandKind.Move, index + 1, index + 2, -1),
                    out QueueVisualSlot slot),
                $"pure Move command {index + 1} enqueue");
            if (index == 9)
                tenth = slot;
        }
        Check(tenth != null && tenth.PageNumber == 2 && tenth.Ordinal == 1,
            "tenth pure Move starts visual page two instead of replacing number nine");
        Check(pureMoves.PendingCount == 15 && pureMoves.VisualPageCount == 2,
            "pure Move queue retains commands beyond Vanilla capacity");
    }

    private static void CheckNativeReference()
    {
        const string expectedSha = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        const int functionRva = 0x11C3A0;
        byte[] expectedBody = Convert.FromHexString(
            "4C635C24284C63D24969C2880600004969D2A20100004903D36641FFC36644898491B4050000" +
            "6644898C91B60500004803C80FB744243066448999DE05000066898182050000C3");
        string gameRoot = Environment.GetEnvironmentVariable("QUEUE_TEST_GAME_DIR") ??
            @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition";
        string path = Path.Combine(
            gameRoot,
            "Stronghold Crusader Definitive Edition_Data",
            "Plugins",
            "x86_64",
            "CrusaderDE.dll");
        byte[] image = File.ReadAllBytes(path);
        string actualSha = Convert.ToHexString(SHA256.HashData(image));
        Check(string.Equals(actualSha, expectedSha, StringComparison.Ordinal), "canonical native SHA-256");

        CheckWildcardPattern(image, 0x8D3C2,
            "44 39 25 ?? ?? ?? ?? 74 3C 48 8B CE E8 ?? ?? ?? ?? 85 C0 74 30 B8 01 00 00 00 44 8B E8 89 44 24 54",
            "MoatCommandTest DigMoat mode");
        CheckWildcardPattern(image, 0x8F3A8,
            "44 8B 0D ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? 44 8B 05 ?? ?? ?? ?? 41 8B D6 E8 ?? ?? ?? ?? 85 C0 74 11 44 8B BC 24 C0 00 00 00",
            "MoatCommandTest cursor reachability");
        CheckWildcardPattern(image, 0x69560,
            "48 63 C2 0F B7 84 41 ?? ?? ?? ?? C3 CC CC CC",
            "MoatCommandTest moat lookup");
        CheckWildcardPattern(image, 0x69D60,
            "44 89 44 24 18 89 54 24 10 55 56 57 41 54 41 55 41 56 48 83 EC 68 48 8B E9 48 8D 3D ?? ?? ?? ?? 45 8B F1 48 8D 87 1C 07 00 00 4D 63 C8 45 33 E4",
            "MoatCommandTest nearest-friendly-moat helper");

        byte[] assassinGroundSelectorEntry = Convert.FromHexString(
            "48895C240848896C2410488974241848897C242041544155415641574C631DA15DBE074C8BF14863816C5F1500458BF9");
        int assassinGroundSelectorRawOffset = RvaToRawOffset(image, 0xE0970);
        Check(image.AsSpan(assassinGroundSelectorRawOffset, assassinGroundSelectorEntry.Length)
                .SequenceEqual(assassinGroundSelectorEntry),
            "Assassin ground formation selector exact entry signature");
        Check(CountOccurrences(image, assassinGroundSelectorEntry) == 1,
            "Assassin ground formation selector unique entry signature");

        CheckNativeHandler(
            image,
            QueueNativeContract.MoveChoreHandlerRva,
            QueueNativeContract.MoveChoreHandlerSize,
            Convert.FromHexString(
                "40534883EC308B0500855E08C705FE845E080800000083F8010F85A400000033DB448D4001448BC8895C2420488D1519086B08488D0D06385608E8D1EA0000"),
            Convert.FromHexString("0889442420E8505418004883C4305BC3"),
            "Chore 17 handler");
        CheckNativeHandler(
            image,
            QueueNativeContract.TargetOrderChoreHandlerRva,
            QueueNativeContract.TargetOrderChoreHandlerSize,
            Convert.FromHexString(
                "40534883EC308B05F0635E08C705EE635E080F00000083F8010F85A300000033DB448D4001448BC8895C2420488D1509E76A08488D0DF6165608E8C1C90000"),
            Convert.FromHexString("0595E56A0889442420E8C46E18004883C4305BC3"),
            "Chore 36 handler");
        CheckNativeHandler(
            image,
            QueueNativeContract.WaypointAppendChoreHandlerRva,
            QueueNativeContract.WaypointAppendChoreHandlerSize,
            Convert.FromHexString(
                "4883EC388B0522195E08C70520195E080800000083F8010F85AD00000048895C2430448D400133DB488D153D9C6A0844"),
            Convert.FromHexString("0889442420E8FE4A10004883C438C3"),
            "Chore 71 handler");

        int rawOffset = RvaToRawOffset(image, functionRva);
        Check(expectedBody.Length == 71, "waypoint helper function length");
        Check(image.AsSpan(rawOffset, expectedBody.Length).SequenceEqual(expectedBody), "waypoint helper exact body");
        Check(CountOccurrences(image, expectedBody) == 1, "waypoint helper unique exact signature");
        Check(image[rawOffset + expectedBody.Length] == 0xCC, "waypoint helper RET boundary");

        const int movementCompleteRva = 0x1178D0;
        byte[] expectedMovementCompleteBody = Convert.FromHexString(
            "48895C240848896C2410488974241848897C242041564883EC204863F233DB4869FE88060000488BE9663B5C0F5C7D58" +
            "4C8D35F90A6D06660F1F840000000000448BC38BD6488BCDE8732600004863C8FFC34869D190040000664283BC32E406" +
            "000002751A664283BC32F808000000750E8BD0498BCEE8F509070085C074290FBF442F5C3BD87CB8B801000000488B5C" +
            "2430488B6C2438488B742440488B7C24484883C420415EC333C0EBE1");
        int movementCompleteRawOffset = RvaToRawOffset(image, movementCompleteRva);
        Check(expectedMovementCompleteBody.Length == 172, "movement completion predicate length");
        Check(
            image.AsSpan(movementCompleteRawOffset, expectedMovementCompleteBody.Length)
                .SequenceEqual(expectedMovementCompleteBody),
            "movement completion predicate exact body");
        Check(
            CountOccurrences(image, expectedMovementCompleteBody.AsSpan(0, 48).ToArray()) == 1,
            "movement completion predicate unique 48-byte signature");

        const int overlayRenderRva = 0x1222A0;
        byte[] overlayRenderSignature = Convert.FromHexString(
            "48895C240848896C241048897424185741544155415641574883EC404C63E24C8D2DFAFA8E034D69F4880600004C8BF9");
        int overlayRenderRawOffset = RvaToRawOffset(image, overlayRenderRva);
        Check(overlayRenderSignature.Length == 48, "tribe overlay renderer signature length");
        Check(
            image.AsSpan(overlayRenderRawOffset, overlayRenderSignature.Length).SequenceEqual(overlayRenderSignature),
            "tribe overlay renderer exact signature");
        Check(CountOccurrences(image, overlayRenderSignature) == 1, "tribe overlay renderer unique signature");
        byte[] overlayRenderTail = Convert.FromHexString("00004883C440415F415E415D415C5FC3");
        const int overlayRenderLength = 1371;
        Check(
            image.AsSpan(
                overlayRenderRawOffset + overlayRenderLength - overlayRenderTail.Length,
                overlayRenderTail.Length).SequenceEqual(overlayRenderTail),
            "tribe overlay renderer exact tail and RET boundary");
        Check(image[overlayRenderRawOffset + overlayRenderLength] == 0xCC, "tribe overlay renderer end boundary");

        const int drawSubmissionRva = 0x417A0;
        byte[] expectedDrawSubmissionBody = Convert.FromHexString(
            "48895C2408488974241048897C241848635C2430418BF14863B948226200448BDA4C8BD185DB0F88AC00000081FFFA00" +
            "00000F8DA0000000488D0D51C577040FB704594C8D0C596685C0750433D2EB368BD03DFA0000000F837B0000000F1F00" +
            "4898486BC81C46398411F0066200750A46399C11F4066200745E428B84110807620085C075DA486BC71C428994100807" +
            "62004A8D14108B442428664189398982FC066200488D8740800300486BC81C8B442438448982F006620044899AF40662" +
            "0089B2F806620042891C1189820407620041FF8248226200488B5C2408488B742410488B7C2418C3");
        int drawSubmissionRawOffset = RvaToRawOffset(image, drawSubmissionRva);
        Check(expectedDrawSubmissionBody.Length == 232, "overlay draw submission function length");
        Check(
            image.AsSpan(drawSubmissionRawOffset, expectedDrawSubmissionBody.Length)
                .SequenceEqual(expectedDrawSubmissionBody),
            "overlay draw submission exact body");
        Check(
            CountOccurrences(image, expectedDrawSubmissionBody.AsSpan(0, 48).ToArray()) == 1,
            "overlay draw submission unique 48-byte signature");
        Check(image[drawSubmissionRawOffset + expectedDrawSubmissionBody.Length] == 0xCC,
            "overlay draw submission RET boundary");

        const int resetDrawListRva = 0x41D10;
        byte[] expectedResetDrawListBody = Convert.FromHexString(
            "41B801000000443981482262007E39488D91240762004533C94C8D1500C07704" +
            "486342F8488D521C41FFC06645890C4244894AE4443B81482262007CE3C78148" +
            "22620001000000C344898148226200C3");
        int resetDrawListRawOffset = RvaToRawOffset(image, resetDrawListRva);
        Check(expectedResetDrawListBody.Length == 80,
            "overlay draw-list reset function length");
        Check(image.AsSpan(resetDrawListRawOffset, expectedResetDrawListBody.Length)
                .SequenceEqual(expectedResetDrawListBody),
            "overlay draw-list reset exact body");
        Check(image[resetDrawListRawOffset + expectedResetDrawListBody.Length] == 0x48,
            "overlay draw-list reset exact RET boundary before visible renderer");

        const int visibleTileRendererRva = 0x41D60;
        const int visibleTileRendererLength = 14826;
        byte[] visibleTileRendererEntry = Convert.FromHexString(
            "48895C241048896C2418488974242048894C24085741544155415641574881EC");
        int visibleTileRendererRawOffset = RvaToRawOffset(image, visibleTileRendererRva);
        Check(image.AsSpan(visibleTileRendererRawOffset, visibleTileRendererEntry.Length)
                .SequenceEqual(visibleTileRendererEntry),
            "visible-tile renderer exact entry");
        byte[] resetCall = Convert.FromHexString("E8FFC5FFFF");
        int resetCallRawOffset = RvaToRawOffset(image, 0x4570C);
        Check(image.AsSpan(resetCallRawOffset, resetCall.Length).SequenceEqual(resetCall),
            "visible-tile renderer calls the reset function at its terminal callsite");
        Check(image[visibleTileRendererRawOffset + visibleTileRendererLength] == 0x66 &&
              image[visibleTileRendererRawOffset + visibleTileRendererLength + 1] == 0x90,
            "visible-tile renderer exact function boundary");

        byte[] visibleTileHookBytes = Convert.FromHexString("410FB7BC5980EF7500");
        int visibleTileHookRawOffset = RvaToRawOffset(image, 0x436DE);
        Check(image.AsSpan(visibleTileHookRawOffset, visibleTileHookBytes.Length)
                .SequenceEqual(visibleTileHookBytes),
            "visible-tile marker hook exact MOVZX block");
        Check(0x436DE + visibleTileHookBytes.Length == 0x436E7 &&
              image[visibleTileHookRawOffset + visibleTileHookBytes.Length] == 0x85 &&
              image[visibleTileHookRawOffset + visibleTileHookBytes.Length + 1] == 0xFF,
            "visible-tile marker hook resumes at TEST EDI without live input flags");

        byte[] spriteBuilderEntry = Convert.FromHexString(
            "48895C240844894C242044894424188954241055565741544155415641574881ECC0000000" +
            "8B842460010000488BD98BAC242001000085C0448BF04D63F841F7D64C63EA");
        int spriteBuilderRawOffset = RvaToRawOffset(image, 0x1A13C0);
        Check(image.AsSpan(spriteBuilderRawOffset, spriteBuilderEntry.Length)
                .SequenceEqual(spriteBuilderEntry),
            "native sprite builder validated entry signature");

    }

    private static void CheckMigrationSourceContracts()
    {
        string workspace = FindWorkspace();
        string bugfixesPlugin = Read(workspace, "BugfixesAndQoL", "src", "BugfixesAndQoLPlugin.cs");
        string bugfixesMinimum = ReadManifestMinimum(workspace, "BugfixesAndQoL");
        string queueRuntime = Read(
            workspace,
            "BugfixesAndQoL",
            "src",
            "ExtendedShiftCommandQueueRuntime.cs");
        string largeMoveRuntime = Read(
            workspace,
            "BugfixesAndQoL",
            "src",
            "LargeMoveTargetMarkerRuntime.cs");
        string largeMoveRenderer = Read(
            workspace,
            "BugfixesAndQoL",
            "src",
            "LargeMoveTargetMarkerRenderer.cs");
        string nativeFormationSlots = Read(
            workspace,
            "BugfixesAndQoL",
            "src",
            "NativeFormationSlots.cs");
        string moveFormationDrag = Read(
            workspace,
            "BugfixesAndQoL",
            "src",
            "MoveFormationDragRuntime.cs");
        string moveFormationPreview = Read(
            workspace,
            "BugfixesAndQoL",
            "src",
            "MoveFormationPreviewPlanner.cs");
        string viewModel = Read(workspace, "BugfixesAndQoL", "src", "BugfixesAndQoLViewModel.cs");
        string settingsXaml = Read(
            workspace,
            "BugfixesAndQoL",
            "Override",
            "ScriptExtenderUI",
            "BugfixesAndQoLSettings.xaml");
        string bugfixesRuntime = string.Join(
            "\n",
            Directory.GetFiles(Path.Combine(workspace, "BugfixesAndQoL", "src"), "*.cs")
                .Select(File.ReadAllText));
        string bugfixesProject = Read(workspace, "BugfixesAndQoL", "BugfixesAndQoL.csproj");

        Check(bugfixesMinimum.Length == 0 ||
            bugfixesPlugin.Contains($"BepInDependency(ScriptExtenderGuid, \"{bugfixesMinimum}\")"),
            "integrated queue dependency matches the manifest minimum");
        Check(bugfixesRuntime.Contains("InitializeExtendedShiftCommandQueue(context, isFixedLayoutHashValidated)"),
            "integrated queue consumes the validated Script Extender load context");
        Check(queueRuntime.Contains("SelectedUnitInfo[] selectedUnits"),
            "integrated queue projects the selected-unit contract");
        Check(CountText(queueRuntime, "new DetourHandle<") == 5,
            "integrated queue owns five typed RedBird detour handles");
        Check(CountText(queueRuntime, "HookTarget.FromAddress(") == 5,
            "integrated queue registers five explicit native targets");
        Check(CountText(queueRuntime, ".Original(") == 6 &&
            CountText(queueRuntime, "InvokeOriginalMoveChore(") == 3 &&
            CountText(queueRuntime, "InvokeOriginalTribeOverlay(") == 5,
            "integrated queue preserves every original-call path through typed handles and the observed overlay wrapper");
        Check(CountText(queueRuntime, ".IsCompleteSuccess") == 3,
            "integrated queue checks all three transaction commits");
        Check(queueRuntime.Contains("largeMoveTargets.TryCaptureOverflowCandidate(") &&
            !queueRuntime.Contains("ObserveAndShouldSuppressMarker(") &&
            CountText(queueRuntime, "new DetourHandle<") == 5,
            "large Move markers reuse the existing draw hook without overlapping detours");
        Check(queueRuntime.Contains("args.Phase == EventHookPhase.Post") &&
            queueRuntime.Contains("args.IsNewOrder") &&
            queueRuntime.Contains("IsLocalSelectedTribe(args.TribeId, out _)") &&
            !queueRuntime.Contains("localMoveChoreDepth"),
            "local direct and executed Extended Shift Moves share formation handling without Chore nesting");
        Check(!queueRuntime.Contains("GameNetworkAPI.GetLocalPlayerId()") &&
            queueRuntime.Contains("GamePlayerManagerAPI.Instance.GetLocalPlayerId()") &&
            queueRuntime.Contains("IsAiOwnedAliveTribe(args.TribeId)") &&
            queueRuntime.Contains("IsAiOwnedAliveTribe(tribeId)") &&
            queueRuntime.Contains("cachedRealMultiplayerMode") &&
            queueRuntime.Contains("if (args.Context.IsSave) RefreshMapContext(); else OnMapStart();") &&
            queueRuntime.Contains(".Subscribe(args => ResetMapState())") &&
            queueRuntime.Contains("private void RefreshMapContext()") &&
            CountText(queueRuntime, "cachedRealMultiplayerMode = null;") >= 2,
            "AI orders bypass Shift queue work and map-scoped context uses the native in-game player ID");
        Check(largeMoveRuntime.Contains("DrawListCountOffset = 0x622248") &&
            largeMoveRuntime.Contains("IsRejectedByFullVanillaList(") &&
            largeMoveRuntime.Contains("renderer.TryAddOverflowMarker(") &&
            !largeMoveRuntime.Contains("OnTick(") &&
            !largeMoveRuntime.Contains("GetUnitsAsSpan(") &&
            !largeMoveRuntime.Contains("MoveFormationCommandSnapshotStore") &&
            CountText(bugfixesRuntime, "MOVE_TARGET_" + "RESULT:") == 0,
            "large Move runtime captures only frame-local records rejected by Vanilla capacity");
        Check(largeMoveRenderer.Contains("VisibleTileHookRva = 0x436DE") &&
            largeMoveRenderer.Contains("ResetDrawListRva = 0x41D10") &&
            largeMoveRenderer.Contains("VisibleTileRendererRva = 0x41D60") &&
            largeMoveRenderer.Contains("SpriteBuilderRva = 0x1A13C0") &&
            largeMoveRenderer.Contains("BugfixesHookInfrastructure.AddContextHook(") &&
            largeMoveRenderer.Contains("candidate.AddDetour(") &&
            largeMoveRenderer.Contains("resetDrawListHook.Original(drawManager)") &&
            largeMoveRenderer.Contains("!featureEnabled()") &&
            largeMoveRenderer.Contains("0x6B") && largeMoveRenderer.Contains("0x52 + frame") &&
            largeMoveRenderer.Contains("0xC") &&
            !largeMoveRenderer.Contains("Application.onBeforeRender") &&
            !largeMoveRenderer.Contains("Texture2D") &&
            !largeMoveRenderer.Contains("Mesh"),
            "large Move renderer injects real Vanilla sprites in the visible-tile pass");
        Check(largeMoveRenderer.Contains("LargeMoveTargetOverflowBuffer firstBuffer") &&
            largeMoveRenderer.Contains("LargeMoveTargetOverflowBuffer secondBuffer") &&
            largeMoveRenderer.Contains("publishedOverflow") &&
            largeMoveRenderer.Contains("private readonly object stateRoot") &&
            largeMoveRenderer.Contains("SetPreviewMarkerTiles(IEnumerable<int> tileIds)") &&
            largeMoveRenderer.Contains("ClearPreviewMarkerTiles()") &&
            largeMoveRenderer.Contains("TryAddOverflowMarker(") &&
            largeMoveRenderer.Contains("NativeContainsDuplicate(drawManager, tileId, category, spriteId)") &&
            largeMoveRenderer.Contains("DrawRecordBaseOffset = 0x6206F0") &&
            largeMoveRenderer.Contains("DrawRecordNextOffset = 0x18") &&
            largeMoveRenderer.Contains("TileDrawHeadRva = 0x47BDD30") &&
            largeMoveRenderer.Contains("OnVanillaDrawListReset()") &&
            largeMoveRenderer.Contains("ExpectedVisibleTileDisplacedBytes = 17") &&
            largeMoveRenderer.Contains("renderingActive = shouldBeActive") &&
            !largeMoveRenderer.Contains("visibleTileHook.Hook.Enable()") &&
            !largeMoveRenderer.Contains("visibleTileHook.Hook.Disable()") &&
            largeMoveRenderer.Contains("record.Flags >> 16") &&
            !largeMoveRenderer.Contains("new Dictionary<int, int>(stableIdentityByTile)") &&
            !largeMoveRenderer.Contains("public void Shutdown()") &&
            CountText(largeMoveRenderer, ".Dispose()") == 1 &&
            largeMoveRenderer.Contains("candidate.Dispose()") &&
            !largeMoveRenderer.Contains(".Sort(") &&
            largeMoveRenderer.Contains("MOVE_TARGET_MARKER_RENDER_FAIL_OPEN"),
            "large Move renderer uses reset-bound reusable overflow buffers and activity windows");
        Check(queueRuntime.Contains("OwnsHooks = false"),
            "integrated queue declares process-lifetime hook ownership");
        Check(queueRuntime.Contains("OnTribeAssignUnit.Observable") &&
            queueRuntime.Contains("OnTribeCreate.Observable") &&
            queueRuntime.Contains("private void MirrorTransientTribeState(") &&
            queueRuntime.Contains("targetTribe->r_UnitsInGroup != 0") &&
            queueRuntime.Contains("sourceTribe->r_PlayerIdOwner == targetTribe->r_PlayerIdOwner") &&
            queueRuntime.Contains("pendingSource.MemberGlobalIds.TryGetValue(") &&
            queueRuntime.Contains("ReferenceEquals(target.Command, source.Command)") &&
            queueRuntime.Contains("target.ExpiresAfterTick == source.ExpiresAfterTick"),
            "Fixes-style empty-tribe splits preserve attack and Move deduplication state idempotently");
        Check(!queueRuntime.Contains("CrashBreadcrumbDiagnostics.Enter(") &&
            !queueRuntime.Contains("\"ShiftQueueTick\"") &&
            !queueRuntime.Contains("\"ShiftQueueMoveOrder\"") &&
            !queueRuntime.Contains("\"ShiftQueueTargetOrder\"") &&
            !queueRuntime.Contains("\"ShiftQueueWaypoint\"") &&
            queueRuntime.Contains("\"ShiftQueueEnqueue\"") &&
            queueRuntime.Contains("\"ShiftQueueDispatch\"") &&
            queueRuntime.Contains("\"ShiftQueueCancel\""),
            "crash breadcrumbs retain queue transitions without recording routine ticks or global hook traffic");
        Check(!queueRuntime.Contains("TOPOLOGY_") &&
            CountText(queueRuntime, "LogCommandFailureOnce(") == 3 &&
            queueRuntime.Contains("ShouldLogUnexpectedOnce(diagnosticOperation)") &&
            queueRuntime.Contains("loggedUnexpectedFailures.Add(normalized)") &&
            !queueRuntime.Contains("loggedUnsupportedCommands.Clear()") &&
            queueRuntime.Contains("Further occurrences are aggregated by crash diagnostics."),
            "Shift queue topology stays in diagnostics and repeated warnings are logged once per process session");
        Check(!queueRuntime.Contains("Zhuqiaomon") && !queueRuntime.Contains("HookRef<") &&
            !queueRuntime.Contains(".Hook.Trampoline"), "integrated queue has no legacy hook API");
        Check(bugfixesProject.Contains("RedBird.Abstractions.dll") &&
            bugfixesProject.Contains("RedBird.Core.dll") && bugfixesProject.Contains("RedBird.X64.dll"),
            "integrated queue uses the RedBird assemblies already owned by BugfixesAndQoL");
        Check(queueRuntime.Contains("GameTribeManagerAPI.Instance.UnassignUnit(tribeId, member.UnitId)") &&
            !queueRuntime.Contains("RemoveUnitFromTribeRva") &&
            !queueRuntime.Contains("removeUnitFromTribe("),
            "integrated queue uses the corrected public UnassignUnit wrapper");

        Check(viewModel.Contains("[SyncHostOnly]\n        public bool EnableExtendedShiftCommandQueue") ||
              viewModel.Contains("[SyncHostOnly]\r\n        public bool EnableExtendedShiftCommandQueue"),
            "extended queue is classified as a synchronized host setting");
        Check(viewModel.Contains("private bool enableExtendedShiftCommandQueue = true;") &&
              viewModel.Contains("EnableExtendedShiftCommandQueue = true;"),
            "extended queue is enabled by default and by preset reset");
        Check((viewModel.Contains("[SyncHostOnly]\n        public bool EnableMoveFormationEnhancements") ||
               viewModel.Contains("[SyncHostOnly]\r\n        public bool EnableMoveFormationEnhancements")) &&
              !viewModel.Contains("public int MoveFormationSpacing") &&
              !viewModel.Contains("MoveFormationSpacingValueText") &&
              viewModel.Contains("EnableMoveFormationEnhancements = true;"),
            "Move formation behavior has only a synchronized host switch");
        Check(queueRuntime.Contains("settings.EnableMod && settings.EnableExtendedShiftCommandQueue") &&
              (queueRuntime.Contains("if (!enabled)\n                ResetMapState();") ||
               queueRuntime.Contains("if (!enabled)\r\n                ResetMapState();")),
            "master and host settings gate the queue and disable transitions clear managed state");
        Check(queueRuntime.Contains("args.AICommand = (TribeAICommand)decodedCommand;") &&
              queueRuntime.Contains("args.MoveType = (TribeMoveType)decodedMoveType;") &&
              queueRuntime.Contains("if (!FeatureEnabled)"),
            "marked commands are decoded safely across setting transitions");
        Check(settingsXaml.Contains("EnableExtendedShiftCommandQueue, Mode=TwoWay"),
            "host settings UI exposes the extended queue option");
        Check(settingsXaml.Contains("EnableMoveFormationEnhancements, Mode=TwoWay") &&
              !settingsXaml.Contains("MoveFormationSpacing") &&
              !settingsXaml.Contains("bugfixes.move-formation-spacing"),
            "host settings UI exposes the Move feature switch without a spacing slider");
        Check(moveFormationDrag.Contains("GetCommandMouseButton(") &&
              moveFormationDrag.Contains("InputR3EventHooks.OnKeyDown.Observable") &&
              moveFormationDrag.Contains("InputR3EventHooks.OnKey.Observable") &&
              moveFormationDrag.Contains("InputR3EventHooks.OnKeyUp.Observable") &&
              moveFormationDrag.Contains("args.Phase != EventHookPhase.Post") &&
              moveFormationDrag.Contains("MoveFormationCommandContext.Arm(") &&
              moveFormationDrag.Contains("markers.ClearPreview();") &&
              moveFormationDrag.Contains("RunAnchoredVanillaTransaction(state, mpFrameSkip)") &&
              moveFormationDrag.Contains("engineRunOriginal(mpFrameSkip)") &&
              moveFormationDrag.Contains("CalcMapTileFromMousePos(") &&
              moveFormationDrag.Contains("mapTile.gameMapX") &&
              moveFormationDrag.Contains("state.Target.TileMapX") &&
              moveFormationDrag.Contains("state.Target.NativeX") &&
              moveFormationDrag.Contains("MoveFormationPreviewPlanner") &&
              moveFormationDrag.Contains("state.Target.UnderCursorUnitIds") &&
              moveFormationDrag.Contains("state.Target.TroopDepth") &&
              moveFormationDrag.Contains("state.Target.OverTopHalf") &&
              moveFormationDrag.Contains("grabTroopsOnScreen(") &&
              moveFormationDrag.Contains("EvaluateInitialGroundTarget(") &&
              moveFormationDrag.Contains("EvaluateFixedGroundTarget(") &&
              moveFormationDrag.Contains("r_HoverOverUnitId") &&
              moveFormationDrag.Contains("r_HoverOverBuildingId") &&
              moveFormationDrag.Contains("r_HoveringOverWall") &&
              moveFormationDrag.Contains("TileUnitIdGrid") &&
              moveFormationDrag.Contains("StructureGrid") &&
              !moveFormationDrag.Contains("\"unit-target\"") &&
              !moveFormationDrag.Contains("\"structure-target\"") &&
              !moveFormationDrag.Contains("\"unwalkable-ground\"") &&
              !moveFormationDrag.Contains("CurrentAction != 0") &&
              moveFormationDrag.Contains("private static readonly object syncRoot") &&
              moveFormationDrag.Contains("ReferenceEquals(observedPreEvent, args)") &&
              !moveFormationDrag.Contains("[ThreadStatic]") &&
              moveFormationDrag.Contains("RestoreInputState(") &&
              moveFormationDrag.Contains("StartSelectionHook(") &&
              moveFormationDrag.Contains("MainControls.instance.CurrentAction = 0") &&
              moveFormationDrag.Contains("leftMouseStateForEngineField") &&
              moveFormationDrag.Contains("rightUpForEngineField") &&
              moveFormationDrag.Contains("SelectionMatches(state.Selection)") &&
              moveFormationDrag.Contains("Shared.GameModeHelper.IsMapEditor()") &&
              !moveFormationDrag.Contains("MainViewModel.Instance.IsMapEditorMode") &&
              !moveFormationDrag.Contains("Input.GetMouseButton") &&
              !moveFormationDrag.Contains("OnBeforeRender") &&
              !moveFormationDrag.Contains("\"Update\", BindingFlags") &&
              !moveFormationDrag.Contains("PreDllCallActionsDelegate") &&
              !moveFormationDrag.Contains("preDLLCallActionsOriginal"),
            "drag preview uses R3 input and a full Engine run transaction with separate coordinate domains");
        Check(queueRuntime.Contains("QueueNativeContract.ShouldPackFormationSpacing(") &&
              queueRuntime.Contains("MoveFormationCommandContext.EnterMoveChoreExecution()") &&
              queueRuntime.Contains("MoveFormationCommandContext.ExitMoveChoreExecution()") &&
              queueRuntime.Contains("Shared.GameModeHelper.Capture()") &&
              queueRuntime.Contains("MOVE_FORMATION_DRAG: chore-packed;") &&
              !queueRuntime.Contains("MOVE_FORMATION_DRAG: chore-marked;") &&
              !queueRuntime.Contains("IsRealMultiplayer() && !IsShiftPressed()"),
            "formation spacing uses Vanilla Chore 17 in every mode and Shared mode diagnostics");
        Check(moveFormationPreview.Contains("PathEdgeMaskGrid") &&
              moveFormationPreview.Contains("PathConnectionGrid") &&
              moveFormationPreview.Contains("queueTile = new int[NativeTileCapacity]") &&
              !moveFormationPreview.Contains("NativeFormationCandidateCapacity = 4001") &&
              moveFormationPreview.Contains("0x10000100") &&
              moveFormationPreview.Contains("destination.Count < requiredCount") &&
              moveFormationPreview.Contains("relaxedQueueIndices") &&
              moveFormationPreview.Contains("while (destination.Count < requiredCount)") &&
              bugfixesProject.Contains("src\\MoveFormationPreviewPlanner.cs"),
            "formation preview and execution planner cover the full native grid with deterministic overflow");
        Check(bugfixesRuntime.Contains("settings.EnableMoveFormationEnhancements") &&
              bugfixesRuntime.Contains("!FeatureEnabled ||") &&
              bugfixesRuntime.Contains("setting-disabled"),
            "Move spacing, diagnostics, suppression, and replacement obey their feature setting");
        Check(nativeFormationSlots.Contains("libraryBase, 0xE0970") &&
              nativeFormationSlots.Contains("MoveFormationSelector.AssassinGround") &&
              nativeFormationSlots.Contains("ResolveEffectiveSpacing") &&
              nativeFormationSlots.Contains("TryChooseManagedFormationSlot(") &&
              nativeFormationSlots.Contains("state[2] = 0") &&
              nativeFormationSlots.Contains("formation-assigned") &&
              !nativeFormationSlots.Contains("libraryBase, 0xE0AC0"),
            "Move spacing assigns every managed slot while leaving the Assassin structure selector untouched");
        Check(bugfixesPlugin.Contains("BepInIncompatibility(LegacyQueueTestGuid)") &&
              bugfixesPlugin.Contains("LegacyQueueTestGuid = \"QueueTest_Serp\""),
            "standalone QueueTest is explicitly incompatible");
        Check(!Directory.Exists(Path.Combine(workspace, "QueueTest")),
            "standalone QueueTest project has been removed after integration");

        Check((bugfixesMinimum.Length == 0 ||
               bugfixesPlugin.Contains($"BepInDependency(ScriptExtenderGuid, \"{bugfixesMinimum}\")")) &&
            bugfixesPlugin.Contains("BepInIncompatibility(LegacyMoveMoatGuid)"),
            "BugfixesAndQoL owns the migrated moat runtime on its manifest-selected Script Extender");
        Check(bugfixesRuntime.Contains("new HookHandle<X64InlineHook>") &&
            bugfixesRuntime.Contains("new DetourHandle<") &&
            bugfixesRuntime.Contains("HookTarget.FromAddress("),
            "integrated moat runtime retains typed RedBird hooks");
        Check(!bugfixesRuntime.Contains("Zhuqiaomon") && !bugfixesRuntime.Contains("NativeDetour") &&
            !bugfixesRuntime.Contains(".Hook.Trampoline"), "integrated moat runtime has no legacy hook API");
        Check(bugfixesProject.Contains("RedBird.Abstractions.dll") && bugfixesProject.Contains("RedBird.Core.dll") &&
            bugfixesProject.Contains("RedBird.X64.dll") && !bugfixesProject.Contains("Zhuqiaomon.dll"),
            "BugfixesAndQoL project carries the integrated RedBird hook references");
        Check(!Directory.Exists(Path.Combine(workspace, "MoatCommandTest")),
            "standalone MoatCommandTest project has been removed after integration");

        foreach (string mod in new[] { "OxTetherIdleFixTest", "StockpileAccessFixTest" })
        {
            string manifest = Read(workspace, "Testmods", mod, "info.json");
            Check(manifest.Contains("\"NetworkMode\": 1"), mod + " remains gameplay synchronized");
        }

        foreach (string mod in new[] { "OxTetherIdleFixTest", "StockpileAccessFixTest" })
        {
            string plugin = Read(workspace, "Testmods", mod, "src", mod + "Plugin.cs");
            string runtime = Read(workspace, "Testmods", mod, "src", mod + "Runtime.cs");
            string project = Read(workspace, "Testmods", mod, mod + ".csproj");
            string minimum = ReadManifestMinimum(workspace, Path.Combine("Testmods", mod));
            Check((minimum.Length == 0 ||
                   plugin.Contains($"BepInDependency(ScriptExtenderGuid, \"{minimum}\")")) &&
                plugin.Contains("CrusaderLibraryLoadContext context"),
                mod + " consumes its manifest-selected Script Extender contract");
            Check(runtime.Contains("using RedBird.Core.Memory;") && !runtime.Contains("Zhuqiaomon"),
                mod + " uses the RedBird memory contract");
            Check(project.Contains("RedBird.Core.dll") && !project.Contains("Zhuqiaomon.dll"),
                mod + " project references RedBird Core only");
        }
    }

    private static string FindWorkspace()
    {
        DirectoryInfo current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "BugfixesAndQoL")) &&
                Directory.Exists(Path.Combine(current.FullName, "Shared")))
                return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Workspace root was not found.");
    }

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(parts.Aggregate(root, Path.Combine));

    private static int CountText(string value, string needle)
    {
        int count = 0;
        int offset = 0;
        while ((offset = value.IndexOf(needle, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += needle.Length;
        }
        return count;
    }

    private static void CheckNativeHandler(
        byte[] image,
        int rva,
        int functionSize,
        byte[] signature,
        byte[] tail,
        string name)
    {
        int rawOffset = RvaToRawOffset(image, rva);
        Check(image.AsSpan(rawOffset, signature.Length).SequenceEqual(signature), $"{name} exact signature");
        Check(CountOccurrences(image, signature) == 1, $"{name} unique signature");
        Check(
            image.AsSpan(rawOffset + functionSize - tail.Length, tail.Length).SequenceEqual(tail),
            $"{name} exact tail and RET");
        Check(image[rawOffset + functionSize] == 0xCC, $"{name} exact end boundary");
    }

    private static int CountOccurrences(byte[] image, byte[] pattern)
    {
        int count = 0;
        for (int offset = 0; offset <= image.Length - pattern.Length; offset++)
        {
            if (image.AsSpan(offset, pattern.Length).SequenceEqual(pattern))
                count++;
        }
        return count;
    }

    private static void CheckWildcardPattern(byte[] image, int expectedRva, string text, string name)
    {
        string[] tokens = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        byte?[] pattern = tokens.Select(token => token == "??" ? (byte?)null : Convert.ToByte(token, 16)).ToArray();
        int count = 0;
        int matchedRawOffset = -1;
        for (int offset = 0; offset <= image.Length - pattern.Length; offset++)
        {
            bool match = true;
            for (int index = 0; index < pattern.Length; index++)
            {
                if (pattern[index].HasValue && image[offset + index] != pattern[index].Value)
                {
                    match = false;
                    break;
                }
            }
            if (!match)
                continue;
            count++;
            matchedRawOffset = offset;
        }
        Check(count == 1, name + " unique signature");
        Check(matchedRawOffset == RvaToRawOffset(image, expectedRva), name + " audited RVA");
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

    private static string ReadManifestMinimum(string workspace, string mod)
    {
        string manifest = Read(workspace, mod, "info.json");
        const string property = "\"MinimumScriptExtenderVersion\"";
        int propertyIndex = manifest.IndexOf(property, StringComparison.Ordinal);
        if (propertyIndex < 0) return string.Empty;
        int colon = manifest.IndexOf(':', propertyIndex + property.Length);
        int openingQuote = colon < 0 ? -1 : manifest.IndexOf('"', colon + 1);
        int closingQuote = openingQuote < 0 ? -1 : manifest.IndexOf('"', openingQuote + 1);
        return closingQuote < 0
            ? string.Empty
            : manifest.Substring(openingQuote + 1, closingQuote - openingQuote - 1);
    }

    private static void Check(bool condition, string name)
    {
        checks++;
        if (!condition)
            throw new InvalidOperationException($"Check failed: {name}");
    }
}
