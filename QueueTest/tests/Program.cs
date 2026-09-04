using QueueTest;
using System;
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
        CheckStateTransitions();
        CheckVisualQueueProjection();
        CheckVisualFlagFiltering();
        CheckTribeReuseGuard();
        CheckUnitIdentityBinding();
        CheckNativeLayoutTranslation();
        CheckMoveChoreDeduplication();
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

    private static void CheckTribeReuseGuard()
    {
        TribeQueueState state = new TribeQueueState(0xAABBCCDD, 1);
        Check(state.MatchesTribe(0xAABBCCDD), "same tribe global ID");
        Check(!state.MatchesTribe(0xAABBCCDE), "reused tribe slot rejected");
    }

    private static void CheckVisualQueueProjection()
    {
        TribeQueueState state = new TribeQueueState(8, 8);
        Check(!state.ContainsAttack, "move visualization starts without attack");
        Check(state.TryEnqueue(new QueueCommand(QueueCommandKind.Move, 1, 2)), "visual first move enqueue");
        Check(state.TryEnqueue(new QueueCommand(QueueCommandKind.AttackUnit, 3, 4)), "visual attack enqueue");
        Check(state.TryEnqueue(new QueueCommand(QueueCommandKind.Move, 5, 6)), "visual second move enqueue");
        Check(state.ContainsAttack, "mixed queue reports attack");
        QueueCommand[] commands = state.GetPendingCommands(9).ToArray();
        Check(commands.Length == 3, "visual projection contains mixed commands");
        Check(
            commands[0].Kind == QueueCommandKind.Move &&
            commands[1].Kind == QueueCommandKind.AttackUnit &&
            commands[2].Kind == QueueCommandKind.Move,
            "visual projection preserves mixed order");
        Check(state.GetPendingCommands(1).Count == 1, "visual projection respects native limit");
        Check(state.GetPendingCommands(0).Count == 0, "empty visual projection limit");

        TribeQueueState compact = new TribeQueueState(9, 8);
        QueueCommand invalid = new QueueCommand(QueueCommandKind.AttackUnit, 99, 100);
        QueueCommand valid1 = new QueueCommand(QueueCommandKind.Move, 7, 8);
        QueueCommand valid2 = new QueueCommand(QueueCommandKind.AttackBuilding, 4, 5);
        QueueCommand valid3 = new QueueCommand(QueueCommandKind.Move, 9, 10);
        compact.TryEnqueue(invalid);
        compact.TryEnqueue(valid1);
        compact.TryEnqueue(valid2);
        compact.TryEnqueue(valid3);
        QueueCommand[] visible = compact.GetPendingCommands(2, command => !ReferenceEquals(command, invalid)).ToArray();
        Check(visible.Length == 2, "invalid visual target compacts sequence");
        Check(ReferenceEquals(visible[0], valid1) && ReferenceEquals(visible[1], valid2),
            "visual limit applies after invalid target filtering");
        Check(compact.TryActivateNext(out QueueCommand active) && ReferenceEquals(active, invalid),
            "active visual command selected");
        Check(!compact.GetPendingCommands(3).Contains(active), "active command excluded from visual queue");

        TribeQueueState limited = new TribeQueueState(10, 12);
        for (int index = 0; index < 12; index++)
            limited.TryEnqueue(new QueueCommand(QueueCommandKind.Move, index, index));
        QueueCommand[] firstNine = limited.GetPendingCommands(9).ToArray();
        Check(firstNine.Length == 9, "visual queue is limited to nine future commands");
        Check(firstNine[8].Argument1 == 8, "visual queue limit retains FIFO prefix");
    }

    private static void CheckVisualFlagFiltering()
    {
        Check(
            QueueVisualContract.ShouldSuppressFlag(
                QueueCommandKind.AttackUnit, 0xAC, 0x138, 0x12, -1, 0xA0022),
            "unit attack Move flag suppressed");
        Check(
            QueueVisualContract.ShouldSuppressFlag(
                QueueCommandKind.ForceAttackBuilding, 0xAC, 0x141, 0x12, -1, 0xA0022),
            "building force-attack Move flag suppressed");
        Check(
            !QueueVisualContract.ShouldSuppressFlag(
                QueueCommandKind.Move, 0xAC, 0x138, 0x12, -1, 0xA0022),
            "real Move flag retained");
        Check(
            !QueueVisualContract.ShouldSuppressFlag(
                QueueCommandKind.AttackBuilding, 0xAC, 0x12E, 0x12, -1, 0xA0022),
            "attack number sprite retained");
        Check(
            !QueueVisualContract.ShouldSuppressFlag(
                QueueCommandKind.AttackBuilding, 0x6B, 0x138, 0x12, -1, 0xA0022),
            "unrelated draw submission retained");
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
        Check(QueueNativeContract.GameBuildingGlobalIdOffset == 0xD6, "GameBuilding global ID offset");
        Check(QueueNativeContract.GameBuildingAttackMarkerOffset == 0xC0, "GameBuilding attack marker offset");
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
