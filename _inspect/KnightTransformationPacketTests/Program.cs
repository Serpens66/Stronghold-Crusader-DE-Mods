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

        private static KnightTransformationPacket CreatePacket(int[] ids)
        {
            return new KnightTransformationPacket
            {
                ProtocolVersion = 1,
                PlayerId = 1,
                OperationId = 1,
                Action = 1,
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
