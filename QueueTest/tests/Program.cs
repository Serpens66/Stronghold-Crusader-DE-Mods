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
        CheckNativeLayoutTranslation();
        CheckMoveChoreDeduplication();
        CheckFirstShiftMoveTakeover();
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
        Check(state.ShouldLogWaitDiagnostic(10, 100), "first wait diagnostic");
        Check(!state.ShouldLogWaitDiagnostic(50, 100), "wait diagnostic throttle");
        Check(state.ShouldLogWaitDiagnostic(110, 100), "wait diagnostic interval");
        state.CompleteActive();
        Check(state.ShouldLogWaitDiagnostic(111, 100), "completion resets wait diagnostic");
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
        TribeQueueState state = new TribeQueueState(0xAABBCCDD, 1);
        Check(state.MatchesTribe(0xAABBCCDD), "same tribe global ID");
        Check(!state.MatchesTribe(0xAABBCCDE), "reused tribe slot rejected");
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
        Check(paged.CurrentVisualPageIndex == 1 &&
            paged.VisualPages.Skip(paged.CurrentVisualPageIndex).Count() == 1,
            "completed pages are excluded from subsequent projection");

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
        TribeQueueState state = new TribeQueueState(3, 1, new[] { first });
        Check(state.Members.Count == 1 && state.Members[0].Equals(first), "unit identity captured");
        Check(state.SharesMemberWith(new[] { first }), "same unit identity matches queue");
        Check(!state.SharesMemberWith(new[] { reused }), "reused unit slot rejected");
        state.RebindTribe(4);
        Check(state.MatchesTribe(4), "queue tribe binding migrated");
        Check(!state.MatchesTribe(3), "old tribe binding rejected after migration");
        Check(!state.ActiveNeedsRedispatch, "idle migration needs no redispatch");
        Check(state.TryEnqueue(new QueueCommand(QueueCommandKind.AttackUnit, 2, 20)), "migration enqueue");
        Check(state.TryActivateNext(out _), "migration activate");
        state.RebindTribe(5);
        Check(state.ActiveNeedsRedispatch, "active command marked for redispatch");
        state.MarkActiveRedispatched();
        Check(!state.ActiveNeedsRedispatch, "active redispatch acknowledged");
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
