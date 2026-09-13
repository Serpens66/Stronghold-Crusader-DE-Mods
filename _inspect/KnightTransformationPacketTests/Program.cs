using MessagePack;
using System;
using System.Linq;

namespace ExtraFeatures
{
    internal static class Program
    {
        private const int MaximumPlayers = 8;
        private static int assertions;

        private static void Main()
        {
            TestValidPackets();
            TestFormatterBounds();
            TestReceiverValidation();
            TestProtocolActions();
            TestTooltipMetadata();
            TestStableHorseAccounting();
            Console.WriteLine($"PASS: Knight transformation packet tests ({assertions} assertions).");
        }

        private static void TestValidPackets()
        {
            RoundTrip(CreatePacket(new[] { 101 }));
            int maximumFittingCount = FindMaximumFittingSequentialTargetCount();
            int[] maximumIds = Enumerable.Range(1, maximumFittingCount).ToArray();
            RoundTrip(CreatePacket(maximumIds));

            byte[] maximumBody = MessagePackSerializer.Serialize(CreatePacket(maximumIds));
            Assert(KnightTransformationPacketValidation.DoesSerializedBodyFitChore(maximumBody.Length), "largest tested selection must fit the Chore transport limit");
            byte[] firstOversizedBody = MessagePackSerializer.Serialize(
                CreatePacket(Enumerable.Range(1, maximumFittingCount + 1).ToArray()));
            Assert(!KnightTransformationPacketValidation.DoesSerializedBodyFitChore(firstOversizedBody.Length), "the next selection size must exceed the actual byte budget");
        }

        private static void TestFormatterBounds()
        {
            ExpectFailure(() => MessagePackSerializer.Deserialize<KnightTransformationPacket>(new byte[] { 0x94, 1, 1, 1, 1 }), "four fields must fail");
            KnightTransformationPacket extended = MessagePackSerializer.Deserialize<KnightTransformationPacket>(
                new byte[] { 0x96, 1, 1, 1, 1, 0x91, 1, 0xC0 });
            Assert(extended.UnitGlobalIds.SequenceEqual(new[] { 1 }), "an additive sixth field must be skipped");
            ExpectFailure(() => MessagePackSerializer.Deserialize<KnightTransformationPacket>(new byte[] { 0x95, 1, 1, 1, 1, 0x90 }), "zero targets must fail");

            byte[] overMaximum = new byte[8 + KnightTransformationPacket.MaximumEncodedTargetCount + 1];
            overMaximum[0] = 0x95;
            overMaximum[1] = 1;
            overMaximum[2] = 1;
            overMaximum[3] = 1;
            overMaximum[4] = 1;
            overMaximum[5] = 0xDC;
            int overMaximumCount = KnightTransformationPacket.MaximumEncodedTargetCount + 1;
            overMaximum[6] = (byte)(overMaximumCount >> 8);
            overMaximum[7] = (byte)overMaximumCount;
            for (int index = 8; index < overMaximum.Length; index++)
                overMaximum[index] = 1;
            ExpectFailure(() => MessagePackSerializer.Deserialize<KnightTransformationPacket>(overMaximum), "maximum plus one must fail before allocation");

            ExpectFailure(
                () => MessagePackSerializer.Deserialize<KnightTransformationPacket>(new byte[] { 0x95, 1, 1, 1, 1, 0xDD, 0x7F, 0xFF, 0xFF, 0xFF }),
                "int.MaxValue array header without payload must fail before allocation");
            ExpectFailure(() => MessagePackSerializer.Serialize(CreatePacket(Array.Empty<int>())), "serializer must reject zero targets");
            RoundTrip(CreatePacket(Array.Empty<int>(), KnightTransformationPacket.CancelAllAction));
            ExpectFailure(
                () => MessagePackSerializer.Serialize(CreatePacket(new[] { 1 }, KnightTransformationPacket.CancelAllAction)),
                "cancel-all serializer must reject targets");
            ExpectFailure(
                () => MessagePackSerializer.Serialize(CreatePacket(new int[KnightTransformationPacket.MaximumEncodedTargetCount + 1])),
                "serializer must reject maximum plus one targets");
        }

        private static void TestReceiverValidation()
        {
            Assert(IsValid(CreatePacket(new[] { 11 })), "one valid target");
            Assert(IsValid(CreatePacket(Enumerable.Range(1, KnightTransformationPacket.MaximumEncodedTargetCount).ToArray())), "absolute maximum valid targets");
            Assert(!IsValid(CreatePacket(Array.Empty<int>())), "zero targets");
            Assert(!IsValid(CreatePacket(new int[KnightTransformationPacket.MaximumEncodedTargetCount + 1])), "too many targets");
            Assert(!IsValid(CreatePacket(new[] { 0 })), "zero global ID");
            Assert(!IsValid(CreatePacket(new[] { -1 })), "negative global ID");
            Assert(!IsValid(CreatePacket(new[] { 7, 7 })), "duplicate global IDs");

            KnightTransformationPacket packet = CreatePacket(new[] { 1 });
            packet.PlayerId = 0;
            Assert(!IsValid(packet), "player zero");
            packet.PlayerId = MaximumPlayers + 1;
            Assert(!IsValid(packet), "player above maximum");
            packet.PlayerId = -1;
            Assert(!IsValid(packet), "negative player");
            packet.PlayerId = 1;
            packet.OperationId = 0;
            Assert(!IsValid(packet), "operation zero");
        }

        private static void TestProtocolActions()
        {
            Assert(IsValid(CreatePacket(new[] { 1 }, KnightTransformationPacket.StartMountAction)), "start mount action");
            Assert(IsValid(CreatePacket(new[] { 1 }, KnightTransformationPacket.StartDismountAction)), "start dismount action");
            Assert(IsValid(CreatePacket(new[] { 1 }, KnightTransformationPacket.CancelSelectedAction)), "cancel selected action");
            Assert(IsValid(CreatePacket(Array.Empty<int>(), KnightTransformationPacket.CancelAllAction)), "cancel all action");
            Assert(!IsValid(CreatePacket(new[] { 1 }, KnightTransformationPacket.CancelAllAction)), "cancel all rejects targets");
            Assert(!IsValid(CreatePacket(Array.Empty<int>(), KnightTransformationPacket.StartMountAction)), "start mount requires targets");
            Assert(!IsValid(CreatePacket(new[] { 1 }, 99)), "unknown action");
        }

        private static void TestStableHorseAccounting()
        {
            int total = 4;
            const int staleUsed = 4;
            int occupied = 4;
            for (int expectedTotal = 3; expectedTotal >= 0; expectedTotal--)
            {
                Assert(
                    StableHorseConsumptionPolicy.TryGetTotalAfterConsumption(
                        total, staleUsed, occupied, true, false, out int totalAfter),
                    $"sequential dismount from total {total} must succeed without recount");
                Assert(totalAfter == expectedTotal, $"sequential total must become {expectedTotal}");
                total = totalAfter;
                occupied--;
            }

            Assert(total == 0 && occupied == 0 && staleUsed == 4, "used remains stale until Vanilla recount");
            int recountedUsed = occupied;
            Assert(total == 0 && recountedUsed == 0, "Vanilla recount normalizes used to zero");

            Assert(StableHorseConsumptionPolicy.TryGetTotalAfterConsumption(4, 2, 2, true, false, out int freeHorseAfter) && freeHorseAfter == 3,
                "stable with free horses consumes exactly one total horse");
            Assert(StableHorseConsumptionPolicy.TryGetTotalAfterConsumption(1, 0, 1, true, false, out int freshReservationAfter) && freshReservationAfter == 0,
                "fresh reservation with stale used zero is valid");
            Assert(StableHorseConsumptionPolicy.TryGetTotalAfterConsumption(4, 4, 4, true, true, out int instantAfter) && instantAfter == 4,
                "instant horse preserves total");
            Assert(!StableHorseConsumptionPolicy.TryGetTotalAfterConsumption(0, 0, 0, true, false, out _), "total zero rejected");
            Assert(!StableHorseConsumptionPolicy.TryGetTotalAfterConsumption(5, 4, 4, true, false, out _), "total above cap rejected");
            Assert(!StableHorseConsumptionPolicy.TryGetTotalAfterConsumption(1, -1, 1, true, false, out _), "negative used rejected");
            Assert(!StableHorseConsumptionPolicy.TryGetTotalAfterConsumption(4, 5, 4, true, false, out _), "used above cap rejected");
            Assert(!StableHorseConsumptionPolicy.TryGetTotalAfterConsumption(2, 4, 3, true, false, out _), "occupied slots above total rejected");
            Assert(!StableHorseConsumptionPolicy.TryGetTotalAfterConsumption(4, 4, 4, false, false, out _), "partial or contradictory slots rejected");

            int completed = 0;
            for (int stable = 0; stable < 25; stable++)
            {
                int stableTotal = 4;
                for (int stableOccupied = 4; stableOccupied > 0; stableOccupied--)
                {
                    Assert(StableHorseConsumptionPolicy.TryGetTotalAfterConsumption(
                            stableTotal, 4, stableOccupied, true, false, out int stableAfter),
                        "large same-tick selection must not wait for an intermediate recount");
                    stableTotal = stableAfter;
                    completed++;
                }
            }
            Assert(completed == 100, "100 dismounts complete in one synchronous pass");
        }

        private static void TestTooltipMetadata()
        {
            AssertTooltipMetadata(0, 0, 0, 0, false, false, false, false);
            AssertTooltipMetadata(5, 0, 5, 0, true, false, false, true);
            AssertTooltipMetadata(0, 30, 0, 30, false, true, false, true);
            AssertTooltipMetadata(5, 30, 5, 30, true, true, true, true);
            AssertTooltipMetadata(1000, 120, 1000, 120, true, true, true, true);
            AssertTooltipMetadata(1001, 121, 1000, 120, true, true, true, true);
            AssertTooltipMetadata(-1, -1, 0, 0, false, false, false, false);
        }

        private static void AssertTooltipMetadata(
            int goldCost,
            int delaySeconds,
            int expectedGoldCost,
            int expectedDelaySeconds,
            bool showGold,
            bool showDelay,
            bool showSeparator,
            bool showHost)
        {
            KnightTransformationTooltipMetadata metadata =
                KnightTransformationTooltipPolicy.Create(goldCost, delaySeconds);
            Assert(metadata.GoldCost == expectedGoldCost, $"tooltip gold clamps to {expectedGoldCost}");
            Assert(metadata.DelaySeconds == expectedDelaySeconds, $"tooltip delay clamps to {expectedDelaySeconds}");
            Assert(metadata.ShowGold == showGold, $"tooltip gold visibility for {goldCost}/{delaySeconds}");
            Assert(metadata.ShowDelay == showDelay, $"tooltip delay visibility for {goldCost}/{delaySeconds}");
            Assert(metadata.ShowSeparator == showSeparator, $"tooltip separator visibility for {goldCost}/{delaySeconds}");
            Assert(metadata.ShowHost == showHost, $"tooltip host visibility for {goldCost}/{delaySeconds}");
        }

        private static int FindMaximumFittingSequentialTargetCount()
        {
            int maximum = 0;
            for (int count = 1; count <= KnightTransformationPacket.MaximumEncodedTargetCount; count++)
            {
                byte[] body = MessagePackSerializer.Serialize(CreatePacket(Enumerable.Range(1, count).ToArray()));
                if (!KnightTransformationPacketValidation.DoesSerializedBodyFitChore(body.Length))
                    break;
                maximum = count;
            }

            Assert(maximum > 1, "a variable multi-unit selection must fit");
            return maximum;
        }

        private static KnightTransformationPacket CreatePacket(
            int[] ids,
            int action = KnightTransformationPacket.StartMountAction)
        {
            return new KnightTransformationPacket
            {
                ProtocolVersion = 2,
                PlayerId = 1,
                OperationId = 1,
                Action = action,
                UnitGlobalIds = ids
            };
        }

        private static bool IsValid(KnightTransformationPacket packet)
        {
            return KnightTransformationPacketValidation.HasValidMetadataAndTargets(packet, MaximumPlayers);
        }

        private static void RoundTrip(KnightTransformationPacket packet)
        {
            KnightTransformationPacket restored = MessagePackSerializer.Deserialize<KnightTransformationPacket>(
                MessagePackSerializer.Serialize(packet));
            Assert(restored.ProtocolVersion == packet.ProtocolVersion, "protocol version roundtrip");
            Assert(restored.PlayerId == packet.PlayerId, "player roundtrip");
            Assert(restored.OperationId == packet.OperationId, "operation roundtrip");
            Assert(restored.Action == packet.Action, "action roundtrip");
            Assert(restored.UnitGlobalIds.SequenceEqual(packet.UnitGlobalIds), "target IDs roundtrip");
        }

        private static void ExpectFailure(Action action, string message)
        {
            try
            {
                action();
            }
            catch (Exception)
            {
                assertions++;
                return;
            }

            throw new InvalidOperationException("Expected failure: " + message);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException("Assertion failed: " + message);
            assertions++;
        }
    }
}
