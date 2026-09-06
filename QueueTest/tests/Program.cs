using QueueTest;
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
        CheckMultiplayerChoreMarkers();
        CheckMoveChoreDeduplication();
        CheckFirstShiftMoveTakeover();
        CheckMigrationSourceContracts();
        CheckNativeReference();
        Console.WriteLine($"QueueTest static tests passed: {checks} checks.");
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
        Check(QueueNativeContract.ChoreMoveTypeRva == 0x86C133C, "Chore Move type global RVA");
    }

    private static void CheckMultiplayerChoreMarkers()
    {
        int[] producerMoveTypes = { 0, 1, 0x81 };
        foreach (int moveType in producerMoveTypes)
        {
            Check(QueueNativeContract.TryMarkMoveTypeForQueue(moveType, out int marked),
                $"Chore 17 producer value 0x{moveType:X} accepts queue marker");
            Check((marked & QueueNativeContract.MoveQueueMarker) != 0,
                $"Chore 17 producer value 0x{moveType:X} carries bit 0x40");
            int unpackedMoveType = marked & ~0x80;
            Check(QueueNativeContract.TryDecodeQueuedMoveType(unpackedMoveType, out int decoded) &&
                decoded == (moveType & ~0x80),
                $"Chore 17 producer value 0x{moveType:X} survives Vanilla bit-7 unpacking");
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

    }

    private static void CheckMigrationSourceContracts()
    {
        string workspace = FindWorkspace();
        string queuePlugin = Read(workspace, "QueueTest", "src", "QueueTestPlugin.cs");
        string queueRuntime = Read(workspace, "QueueTest", "src", "QueueRuntime.cs");
        string queueProject = Read(workspace, "QueueTest", "QueueTest.csproj");
        string queueContract = Read(workspace, "QueueTest", "NATIVE_CONTRACT.md");
        string bugfixesPlugin = Read(workspace, "BugfixesAndQoL", "src", "BugfixesAndQoLPlugin.cs");
        string bugfixesRuntime = string.Join(
            "\n",
            Directory.GetFiles(Path.Combine(workspace, "BugfixesAndQoL", "src"), "*.cs")
                .Select(File.ReadAllText));
        string bugfixesProject = Read(workspace, "BugfixesAndQoL", "BugfixesAndQoL.csproj");

        Check(queuePlugin.Contains("BepInDependency(ScriptExtenderGuid, \"2.2.0\")"),
            "QueueTest pins Script Extender 2.2.0");
        Check(queuePlugin.Contains("OnCrusaderLibraryLoaded(CrusaderLibraryLoadContext context)"),
            "QueueTest consumes the 2.2.0 load context");
        Check(queueRuntime.Contains("SelectedUnitInfo[] selectedUnits"),
            "QueueTest projects the 2.2.0 selected-unit contract");
        Check(CountText(queueRuntime, "new DetourHandle<") == 5,
            "QueueTest owns five typed RedBird detour handles");
        Check(CountText(queueRuntime, "HookTarget.FromAddress(") == 5,
            "QueueTest registers five explicit native targets");
        Check(CountText(queueRuntime, ".Original(") == 10,
            "QueueTest preserves every original-call path through typed handles");
        Check(CountText(queueRuntime, ".IsCompleteSuccess") == 3,
            "QueueTest checks all three transaction commits");
        Check(queueRuntime.Contains("OwnsHooks = false"),
            "QueueTest declares process-lifetime hook ownership");
        Check(!queueRuntime.Contains("Zhuqiaomon") && !queueRuntime.Contains("HookRef<") &&
            !queueRuntime.Contains(".Hook.Trampoline"), "QueueTest has no legacy hook API");
        Check(queueProject.Contains("RedBird.Abstractions.dll") &&
            queueProject.Contains("RedBird.Core.dll") && queueProject.Contains("RedBird.X64.dll"),
            "QueueTest references the RedBird assemblies shipped with 2.2.0");
        Check(!queueProject.Contains("Zhuqiaomon.dll") && !queueProject.Contains("PolyHook2.NET.dll") &&
            !queueProject.Contains("Iced.dll"), "QueueTest project has no legacy hook dependency");
        Check(queueContract.Contains("Script Extender 2.0.2") &&
            queueContract.Contains("Script Extender 2.2.0") &&
            queueRuntime.Contains("GameTribeManagerAPI.Instance.UnassignUnit(tribeId, member.UnitId)") &&
            !queueRuntime.Contains("RemoveUnitFromTribeRva") &&
            !queueRuntime.Contains("removeUnitFromTribe("),
            "QueueTest uses the corrected 2.2.0 public UnassignUnit wrapper");

        Check(bugfixesPlugin.Contains("BepInDependency(ScriptExtenderGuid, \"2.2.0\")") &&
            bugfixesPlugin.Contains("BepInIncompatibility(LegacyMoveMoatGuid)"),
            "BugfixesAndQoL owns the migrated moat runtime on Script Extender 2.2.0");
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

        foreach (string mod in new[] { "OxTetherIdleFixTest", "QueueTest", "StockpileAccessFixTest" })
        {
            string manifest = Read(workspace, mod, "info.json");
            Check(manifest.Contains("\"NetworkMode\": 1"), mod + " remains gameplay synchronized");
        }

        foreach (string mod in new[] { "OxTetherIdleFixTest", "StockpileAccessFixTest" })
        {
            string plugin = Read(workspace, mod, "src", mod + "Plugin.cs");
            string runtime = Read(workspace, mod, "src", mod + "Runtime.cs");
            string project = Read(workspace, mod, mod + ".csproj");
            Check(plugin.Contains("BepInDependency(ScriptExtenderGuid, \"2.2.0\")") &&
                plugin.Contains("CrusaderLibraryLoadContext context"), mod + " consumes exact 2.2.0");
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
            if (Directory.Exists(Path.Combine(current.FullName, "QueueTest")) &&
                Directory.Exists(Path.Combine(current.FullName, "BugfixesAndQoL")))
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

    private static void Check(bool condition, string name)
    {
        checks++;
        if (!condition)
            throw new InvalidOperationException($"Check failed: {name}");
    }
}
