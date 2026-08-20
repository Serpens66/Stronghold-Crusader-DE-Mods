using MessagePack;
using SHCDESE.API;
using SHCDESE.API.Components.Network;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace RandomEvents
{
    internal static class RandomEventsDiagnostics
    {
        private const int EventKindCount = 15;

        public static byte[] SerializeAndVerify(RandomEventsChorePacket packet)
        {
            byte[] body = GameNetworkAPI.Serialize(packet);
            RandomEventsChorePacket roundTrip = GameNetworkAPI.Deserialize<RandomEventsChorePacket>(body);
            if (!PacketsEqual(packet, roundTrip))
                throw new InvalidDataException("The local RandomEvents Chore serializer roundtrip changed packet fields.");
            return body;
        }

        public static byte[] SerializeAndVerify(RandomEventsInitializationAckPacket packet)
        {
            byte[] body = GameNetworkAPI.Serialize(packet);
            RandomEventsInitializationAckPacket roundTrip =
                GameNetworkAPI.Deserialize<RandomEventsInitializationAckPacket>(body);
            if (packet == null || roundTrip == null ||
                packet.ProtocolVersion != roundTrip.ProtocolVersion ||
                packet.OperationId != roundTrip.OperationId ||
                packet.PlayerId != roundTrip.PlayerId ||
                !string.Equals(packet.StateDigest, roundTrip.StateDigest, StringComparison.Ordinal))
            {
                throw new InvalidDataException("The local RandomEvents initialization-ACK serializer roundtrip changed packet fields.");
            }
            return body;
        }

        public static string RunSerializerSelfTests(int protocolVersion)
        {
            int playerSlots = GamePlayerManagerAPI.MAX_PLAYERS + 1;
            var initialization = new RandomEventsChorePacket
            {
                ProtocolVersion = protocolVersion,
                CommandType = 1,
                OperationId = 1,
                EffectiveEnabled = true,
                IntervalMonths = 1,
                CooldownMonths = 90,
                MultiplayerMode = 1,
                Chances = Enumerable.Range(0, EventKindCount).Select(index => index * 7 % 101).ToArray(),
                StrengthMinimums = new[] { 1, 2, 3, 4, 5, 6 },
                StrengthMaximums = new[] { 11, 12, 13, 14, 15, 16 },
                PrngState0 = 0x0123456789ABCDEFUL,
                PrngState1 = 0xFEDCBA9876543210UL,
                NextDueAbsoluteMonth = 12346,
                StartAbsoluteMonth = 12345,
                SharedCooldownUntilAbsoluteMonths = Enumerable.Range(0, EventKindCount).ToArray(),
                IndividualCooldownUntilAbsoluteMonths = Enumerable.Range(0, playerSlots * EventKindCount).ToArray(),
                BatchPrepared = false,
                SignpostsInitialized = false,
                SignpostBuildingIds = new[] { -1, 101, 202, 303 }
            };

            var emptyBatch = new RandomEventsChorePacket
            {
                ProtocolVersion = protocolVersion,
                CommandType = 2,
                OperationId = 2,
                PrngState0 = initialization.PrngState0,
                PrngState1 = initialization.PrngState1,
                NextDueAbsoluteMonth = initialization.NextDueAbsoluteMonth
            };

            int maximumActions = playerSlots * EventKindCount;
            var maximumBatch = new RandomEventsChorePacket
            {
                ProtocolVersion = protocolVersion,
                CommandType = 2,
                OperationId = 3,
                PrngState0 = initialization.PrngState1,
                PrngState1 = initialization.PrngState0,
                NextDueAbsoluteMonth = initialization.NextDueAbsoluteMonth,
                EventKinds = Enumerable.Range(0, maximumActions).Select(index => index % EventKindCount).ToArray(),
                EventStrengths = Enumerable.Range(0, maximumActions).Select(index => index * 1000 - 50000).ToArray(),
                TargetPlayerIds = Enumerable.Range(0, maximumActions).Select(index => index % GamePlayerManagerAPI.MAX_PLAYERS + 1).ToArray()
            };

            var results = new List<string>();
            RunSerializerSelfTest("initialization", initialization, results);
            RunSerializerSelfTest("empty-batch", emptyBatch, results);
            RunSerializerSelfTest("maximum-batch", maximumBatch, results);
            var ack = new RandomEventsInitializationAckPacket
            {
                ProtocolVersion = protocolVersion,
                OperationId = 4,
                PlayerId = 2,
                StateDigest = new string('A', 64)
            };
            byte[] ackBody = SerializeAndVerify(ack);
            results.Add($"initialization-ack(bytes={ackBody.Length},sha256={HashBytes(ackBody)})");
            return string.Join(", ", results);
        }

        public static string HashBytes(byte[] bytes)
        {
            using (SHA256 sha256 = SHA256.Create())
                return ToHex(sha256.ComputeHash(bytes ?? Array.Empty<byte>()));
        }

        public static string GetStateDigest(RandomEventsSaveStateV2 state)
        {
            if (state == null)
                return "null";
            return HashBytes(MessagePackSerializer.Serialize(state));
        }

        public static string GetActionDigest(int[] kinds, int[] strengths, int[] targetPlayerIds)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                WriteIntArray(writer, kinds);
                WriteIntArray(writer, strengths);
                WriteIntArray(writer, targetPlayerIds);
                writer.Flush();
                return HashBytes(stream.ToArray());
            }
        }

        public static string DescribeActions(int[] kinds, int[] strengths, int[] targetPlayerIds)
        {
            int count = kinds?.Length ?? 0;
            if (count == 0)
                return "[]";

            var entries = new string[count];
            for (int index = 0; index < count; index++)
            {
                int strength = strengths != null && index < strengths.Length ? strengths[index] : int.MinValue;
                int target = targetPlayerIds != null && index < targetPlayerIds.Length ? targetPlayerIds[index] : -1;
                entries[index] = $"{index}:{kinds[index]}@P{target}={strength}";
            }
            return "[" + string.Join(",", entries) + "]";
        }

        public static string FormatPrng(ulong state0, ulong state1) =>
            $"{state0:X16}:{state1:X16}";

        public static string DescribeScriptExtenderBinary()
        {
            string path = typeof(GameNetworkAPI).Assembly.Location;
            var file = new FileInfo(path);
            FileVersionInfo version = FileVersionInfo.GetVersionInfo(path);
            string hash;
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 sha256 = SHA256.Create())
                hash = ToHex(sha256.ComputeHash(stream));
            return $"path={path}, version={version.FileVersion}, size={file.Length}, sha256={hash}";
        }

        private static void RunSerializerSelfTest(
            string name,
            RandomEventsChorePacket packet,
            ICollection<string> results)
        {
            byte[] body = SerializeAndVerify(packet);
            results.Add($"{name}(bytes={body.Length},sha256={HashBytes(body)})");
        }

        private static bool PacketsEqual(RandomEventsChorePacket left, RandomEventsChorePacket right)
        {
            return left != null && right != null &&
                left.ProtocolVersion == right.ProtocolVersion &&
                left.CommandType == right.CommandType &&
                left.OperationId == right.OperationId &&
                left.EffectiveEnabled == right.EffectiveEnabled &&
                left.IntervalMonths == right.IntervalMonths &&
                left.CooldownMonths == right.CooldownMonths &&
                left.MultiplayerMode == right.MultiplayerMode &&
                ArraysEqual(left.Chances, right.Chances) &&
                ArraysEqual(left.StrengthMinimums, right.StrengthMinimums) &&
                ArraysEqual(left.StrengthMaximums, right.StrengthMaximums) &&
                left.PrngState0 == right.PrngState0 &&
                left.PrngState1 == right.PrngState1 &&
                left.NextDueAbsoluteMonth == right.NextDueAbsoluteMonth &&
                left.StartAbsoluteMonth == right.StartAbsoluteMonth &&
                ArraysEqual(left.SharedCooldownUntilAbsoluteMonths, right.SharedCooldownUntilAbsoluteMonths) &&
                ArraysEqual(left.IndividualCooldownUntilAbsoluteMonths, right.IndividualCooldownUntilAbsoluteMonths) &&
                left.BatchPrepared == right.BatchPrepared &&
                ArraysEqual(left.EventKinds, right.EventKinds) &&
                ArraysEqual(left.EventStrengths, right.EventStrengths) &&
                ArraysEqual(left.TargetPlayerIds, right.TargetPlayerIds) &&
                left.SignpostsInitialized == right.SignpostsInitialized &&
                ArraysEqual(left.SignpostBuildingIds, right.SignpostBuildingIds);
        }

        private static bool ArraysEqual(int[] left, int[] right) =>
            ReferenceEquals(left, right) ||
            (left != null && right != null && left.SequenceEqual(right));

        private static void WriteIntArray(BinaryWriter writer, int[] values)
        {
            int[] safeValues = values ?? Array.Empty<int>();
            writer.Write(safeValues.Length);
            for (int index = 0; index < safeValues.Length; index++)
                writer.Write(safeValues[index]);
        }

        private static string ToHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            for (int index = 0; index < bytes.Length; index++)
                builder.Append(bytes[index].ToString("X2"));
            return builder.ToString();
        }
    }
}
