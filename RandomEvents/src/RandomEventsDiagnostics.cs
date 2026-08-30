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
        public static byte[] SerializeAndVerify(RandomEventsInitializationChorePacket packet) =>
            Verify(packet, SameInitialization, "initialization Chore");
        public static byte[] SerializeAndVerify(RandomEventsBatchChorePacket packet) =>
            Verify(packet, SameBatch, "batch Chore");
        public static byte[] SerializeAndVerify(RandomEventsSignpostChorePacket packet) =>
            Verify(packet, (a, b) => a != null && b != null && a.ProtocolVersion == b.ProtocolVersion && a.OperationId == b.OperationId, "signpost Chore");
        public static byte[] SerializeAndVerify(RandomEventsInitializationAckPacket packet) =>
            Verify(packet, (a, b) => a != null && b != null && a.ProtocolVersion == b.ProtocolVersion && a.OperationId == b.OperationId &&
                a.PlayerId == b.PlayerId && BytesEqual(a.StateDigest, b.StateDigest), "initialization ACK");

        public static string RunSerializerSelfTests(int protocolVersion)
        {
            byte[] digest = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();
            var initialization = new RandomEventsInitializationChorePacket
            {
                ProtocolVersion = protocolVersion, OperationId = 1, ConfigurationDigest = digest,
                PrngState0 = 0x0123456789ABCDEFUL, PrngState1 = 0xFEDCBA9876543210UL,
                NextDueAbsoluteMonth = 12346, StartAbsoluteMonth = 12345,
                CooldownEncoding = (int)RandomEventsCooldownEncoding.None
            };
            var shared = CloneInitialization(initialization);
            shared.CooldownEncoding = (int)RandomEventsCooldownEncoding.SharedDense;
            shared.CooldownData = Enumerable.Range(0, RandomEventDefinitions.All.Length).ToArray();
            var dense = CloneInitialization(initialization);
            dense.CooldownEncoding = (int)RandomEventsCooldownEncoding.IndividualDense;
            dense.CooldownData = Enumerable.Range(
                0,
                GamePlayerManagerAPI.MAX_PLAYERS * RandomEventDefinitions.All.Length).ToArray();
            var emptyBatch = new RandomEventsBatchChorePacket
            {
                ProtocolVersion = protocolVersion, OperationId = 2,
                PrngState0 = initialization.PrngState0, PrngState1 = initialization.PrngState1,
                DueAbsoluteMonth = initialization.NextDueAbsoluteMonth
            };
            RandomEventsRuntimeState validationState = CreateValidationState();
            RandomEventsBatchChorePacket maximumBatch = CreateMaximumBatch(protocolVersion, initialization, validationState);
            RunBatchValidationSelfTests(protocolVersion, initialization, validationState, maximumBatch);
            RunConfigurationDigestSelfTest();
            var results = new List<string>();
            AddResult("initialization-none", SerializeAndVerify(initialization), results);
            AddResult("initialization-shared", SerializeAndVerify(shared), results);
            AddResult("initialization-individual-dense", SerializeAndVerify(dense), results);
            AddResult("empty-batch", SerializeAndVerify(emptyBatch), results);
            AddResult("maximum-batch", SerializeAndVerify(maximumBatch), results);
            AddResult("signpost", SerializeAndVerify(new RandomEventsSignpostChorePacket { ProtocolVersion = protocolVersion, OperationId = 4 }), results);
            AddResult("initialization-ack", SerializeAndVerify(new RandomEventsInitializationAckPacket
            {
                ProtocolVersion = protocolVersion, OperationId = 1, PlayerId = 2, StateDigest = digest
            }), results);
            return string.Join(", ", results);
        }

        private static RandomEventsRuntimeState CreateValidationState()
        {
            int strengthKindCount = Enum.GetValues(typeof(RandomEventStrengthKind)).Length - 1;
            int[] minimums = new int[strengthKindCount];
            int[] maximums = new int[strengthKindCount];
            for (int index = 0; index < strengthKindCount; index++)
                RandomEventDefinitions.GetEncodedStrengthLimits((RandomEventStrengthKind)(index + 1), out minimums[index], out maximums[index]);
            return new RandomEventsRuntimeState
            {
                MultiplayerMode = (int)MultiplayerEventMode.SharedEvents,
                StrengthMinimums = minimums,
                StrengthMaximums = maximums
            };
        }

        private static RandomEventsBatchChorePacket CreateMaximumBatch(
            int protocolVersion,
            RandomEventsInitializationChorePacket initialization,
            RandomEventsRuntimeState validationState)
        {
            var kinds = new List<int>();
            var strengths = new List<int>();
            var targets = new List<int>();
            foreach (RandomEventDefinition definition in RandomEventDefinitions.All)
            {
                int strength = definition.StrengthKind == RandomEventStrengthKind.None
                    ? 0
                    : validationState.StrengthMinimums[(int)definition.StrengthKind - 1];
                for (int playerId = 1; playerId <= GamePlayerManagerAPI.MAX_PLAYERS; playerId++)
                {
                    kinds.Add((int)definition.Kind);
                    strengths.Add(strength);
                    targets.Add(playerId);
                }
            }

            return new RandomEventsBatchChorePacket
            {
                ProtocolVersion = protocolVersion, OperationId = 3,
                PrngState0 = initialization.PrngState1, PrngState1 = initialization.PrngState0,
                DueAbsoluteMonth = initialization.NextDueAbsoluteMonth,
                EventKinds = kinds.ToArray(), EventStrengths = strengths.ToArray(), TargetPlayerIds = targets.ToArray()
            };
        }

        private static void RunBatchValidationSelfTests(
            int protocolVersion,
            RandomEventsInitializationChorePacket initialization,
            RandomEventsRuntimeState validationState,
            RandomEventsBatchChorePacket maximumBatch)
        {
            RequireBatchValidation(maximumBatch, protocolVersion, validationState, true, "maximum valid batch");

            RandomEventsBatchChorePacket oversized = CloneBatch(maximumBatch);
            Array.Resize(ref oversized.EventKinds, oversized.EventKinds.Length + 1);
            Array.Resize(ref oversized.EventStrengths, oversized.EventStrengths.Length + 1);
            Array.Resize(ref oversized.TargetPlayerIds, oversized.TargetPlayerIds.Length + 1);
            RequireBatchValidation(oversized, protocolVersion, validationState, false, "oversized batch");

            RandomEventsBatchChorePacket duplicate = CloneBatch(maximumBatch);
            int last = duplicate.EventKinds.Length - 1;
            duplicate.EventKinds[last] = duplicate.EventKinds[0];
            duplicate.EventStrengths[last] = duplicate.EventStrengths[0];
            duplicate.TargetPlayerIds[last] = duplicate.TargetPlayerIds[0];
            RequireBatchValidation(duplicate, protocolVersion, validationState, false, "duplicate action");

            RandomEventDefinition strengthDefinition = RandomEventDefinitions.All.First(
                definition => definition.StrengthKind != RandomEventStrengthKind.None);
            int strengthIndex = (int)strengthDefinition.StrengthKind - 1;
            var invalidStrength = new RandomEventsBatchChorePacket
            {
                ProtocolVersion = protocolVersion, OperationId = 5,
                PrngState0 = initialization.PrngState0, PrngState1 = initialization.PrngState1,
                DueAbsoluteMonth = initialization.NextDueAbsoluteMonth,
                EventKinds = new[] { (int)strengthDefinition.Kind },
                EventStrengths = new[] { validationState.StrengthMaximums[strengthIndex] + 1 },
                TargetPlayerIds = new[] { 1 }
            };
            RequireBatchValidation(invalidStrength, protocolVersion, validationState, false, "invalid strength");

            RandomEventsBatchChorePacket sharedMismatch = CloneBatch(invalidStrength);
            sharedMismatch.EventKinds = new[] { (int)strengthDefinition.Kind, (int)strengthDefinition.Kind };
            sharedMismatch.EventStrengths = new[]
            {
                validationState.StrengthMinimums[strengthIndex],
                validationState.StrengthMinimums[strengthIndex] + 1
            };
            sharedMismatch.TargetPlayerIds = new[] { 1, 2 };
            RequireBatchValidation(sharedMismatch, protocolVersion, validationState, false, "shared strength mismatch");
        }

        private static void RunConfigurationDigestSelfTest()
        {
            int eventCount = RandomEventDefinitions.All.Length;
            int strengthKindCount = Enum.GetValues(typeof(RandomEventStrengthKind)).Length - 1;
            var withoutAI = new RandomEventsConfigurationSnapshot
            {
                Enabled = true, IntervalMonths = 1, CooldownMonths = 0,
                MultiplayerMode = (int)MultiplayerEventMode.SharedEvents, IncludeAIPlayers = false,
                Chances = new int[eventCount], StrengthMinimums = new int[strengthKindCount],
                StrengthMaximums = new int[strengthKindCount]
            };
            var withAI = new RandomEventsConfigurationSnapshot
            {
                Enabled = withoutAI.Enabled, IntervalMonths = withoutAI.IntervalMonths,
                CooldownMonths = withoutAI.CooldownMonths, MultiplayerMode = withoutAI.MultiplayerMode,
                IncludeAIPlayers = true, Chances = (int[])withoutAI.Chances.Clone(),
                StrengthMinimums = (int[])withoutAI.StrengthMinimums.Clone(),
                StrengthMaximums = (int[])withoutAI.StrengthMaximums.Clone()
            };
            if (BytesEqual(withoutAI.GetDigest(), withAI.GetDigest()))
                throw new InvalidDataException("The IncludeAIPlayers setting does not affect the configuration digest.");
        }

        private static void RequireBatchValidation(
            RandomEventsBatchChorePacket packet,
            int protocolVersion,
            RandomEventsRuntimeState state,
            bool expected,
            string label)
        {
            bool actual = RandomEventsBatchValidator.Validate(packet, protocolVersion, state, out string failure);
            if (actual != expected)
                throw new InvalidDataException($"RandomEvents {label} validation returned {actual}: {failure}");
        }

        private static RandomEventsBatchChorePacket CloneBatch(RandomEventsBatchChorePacket value) =>
            new RandomEventsBatchChorePacket
            {
                ProtocolVersion = value.ProtocolVersion, OperationId = value.OperationId,
                PrngState0 = value.PrngState0, PrngState1 = value.PrngState1,
                DueAbsoluteMonth = value.DueAbsoluteMonth,
                EventKinds = (int[])value.EventKinds.Clone(),
                EventStrengths = (int[])value.EventStrengths.Clone(),
                TargetPlayerIds = (int[])value.TargetPlayerIds.Clone()
            };

        public static byte[] GetStateDigestBytes(RandomEventsRuntimeState state)
        {
            if (state == null) return Array.Empty<byte>();
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            using (SHA256 sha256 = SHA256.Create())
            {
                WriteByteArray(writer, state.ConfigurationDigest);
                writer.Write(state.PrngState0); writer.Write(state.PrngState1);
                writer.Write(state.NextDueAbsoluteMonth); writer.Write(state.StartAbsoluteMonth);
                WriteIntArray(writer, state.SharedCooldownUntilAbsoluteMonths);
                WriteIntArray(writer, state.IndividualCooldownUntilAbsoluteMonths);
                writer.Write(state.BatchPrepared);
                WriteIntArray(writer, state.PreparedDirectKinds);
                WriteIntArray(writer, state.PreparedDirectStrengths);
                WriteIntArray(writer, state.PreparedDirectTargetPlayerIds);
                writer.Write(state.SignpostsInitialized);
                WriteIntArray(writer, state.SignpostBuildingIds);
                writer.Flush();
                return sha256.ComputeHash(stream.ToArray());
            }
        }

        public static string GetStateDigest(RandomEventsRuntimeState state) => ToHex(GetStateDigestBytes(state));
        public static string HashBytes(byte[] bytes)
        {
            using (SHA256 sha256 = SHA256.Create()) return ToHex(sha256.ComputeHash(bytes ?? Array.Empty<byte>()));
        }
        public static string ToHex(byte[] bytes)
        {
            if (bytes == null) return "null";
            var builder = new StringBuilder(bytes.Length * 2);
            for (int index = 0; index < bytes.Length; index++) builder.Append(bytes[index].ToString("X2"));
            return builder.ToString();
        }
        public static bool BytesEqual(byte[] left, byte[] right) =>
            ReferenceEquals(left, right) || (left != null && right != null && left.SequenceEqual(right));

        public static string GetActionDigest(int[] kinds, int[] strengths, int[] targetPlayerIds)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                WriteIntArray(writer, kinds); WriteIntArray(writer, strengths); WriteIntArray(writer, targetPlayerIds);
                writer.Flush(); return HashBytes(stream.ToArray());
            }
        }

        public static string DescribeActions(int[] kinds, int[] strengths, int[] targetPlayerIds)
        {
            int count = kinds?.Length ?? 0;
            if (count == 0) return "[]";
            var entries = new string[count];
            for (int index = 0; index < count; index++)
                entries[index] = $"{index}:{kinds[index]}@P{targetPlayerIds[index]}={strengths[index]}";
            return "[" + string.Join(",", entries) + "]";
        }

        public static string FormatPrng(ulong state0, ulong state1) => $"{state0:X16}:{state1:X16}";
        public static string DescribeScriptExtenderBinary()
        {
            string path = typeof(GameNetworkAPI).Assembly.Location;
            var file = new FileInfo(path); FileVersionInfo version = FileVersionInfo.GetVersionInfo(path);
            string hash;
            using (FileStream stream = File.OpenRead(path)) using (SHA256 sha256 = SHA256.Create()) hash = ToHex(sha256.ComputeHash(stream));
            return $"path={path}, version={version.FileVersion}, size={file.Length}, sha256={hash}";
        }

        private static byte[] Verify<T>(T packet, Func<T, T, bool> equals, string label)
        {
            byte[] body = GameNetworkAPI.Serialize(packet);
            T roundTrip = GameNetworkAPI.Deserialize<T>(body);
            if (!equals(packet, roundTrip)) throw new InvalidDataException($"The local RandomEvents {label} serializer roundtrip changed fields.");
            return body;
        }
        private static bool SameInitialization(RandomEventsInitializationChorePacket a, RandomEventsInitializationChorePacket b) =>
            a != null && b != null && a.ProtocolVersion == b.ProtocolVersion && a.OperationId == b.OperationId &&
            BytesEqual(a.ConfigurationDigest, b.ConfigurationDigest) && a.PrngState0 == b.PrngState0 && a.PrngState1 == b.PrngState1 &&
            a.NextDueAbsoluteMonth == b.NextDueAbsoluteMonth && a.StartAbsoluteMonth == b.StartAbsoluteMonth &&
            a.CooldownEncoding == b.CooldownEncoding && ArraysEqual(a.CooldownData, b.CooldownData);
        private static bool SameBatch(RandomEventsBatchChorePacket a, RandomEventsBatchChorePacket b) =>
            a != null && b != null && a.ProtocolVersion == b.ProtocolVersion && a.OperationId == b.OperationId &&
            a.PrngState0 == b.PrngState0 && a.PrngState1 == b.PrngState1 && a.DueAbsoluteMonth == b.DueAbsoluteMonth &&
            ArraysEqual(a.EventKinds, b.EventKinds) && ArraysEqual(a.EventStrengths, b.EventStrengths) && ArraysEqual(a.TargetPlayerIds, b.TargetPlayerIds);
        private static bool ArraysEqual(int[] left, int[] right) => ReferenceEquals(left, right) || (left != null && right != null && left.SequenceEqual(right));
        private static RandomEventsInitializationChorePacket CloneInitialization(RandomEventsInitializationChorePacket value) => new RandomEventsInitializationChorePacket
        {
            ProtocolVersion = value.ProtocolVersion, OperationId = value.OperationId, ConfigurationDigest = (byte[])value.ConfigurationDigest.Clone(),
            PrngState0 = value.PrngState0, PrngState1 = value.PrngState1, NextDueAbsoluteMonth = value.NextDueAbsoluteMonth,
            StartAbsoluteMonth = value.StartAbsoluteMonth, CooldownEncoding = value.CooldownEncoding, CooldownData = (int[])value.CooldownData.Clone()
        };
        private static void AddResult(string name, byte[] body, ICollection<string> results)
        {
            if (body.Length + sizeof(short) > 1200)
                throw new InvalidDataException($"RandomEvents {name} self-test exceeded the 1200-byte Chore limit.");
            results.Add($"{name}(bytes={body.Length},sha256={HashBytes(body)})");
        }
        private static void WriteIntArray(BinaryWriter writer, int[] values)
        {
            int[] safe = values ?? Array.Empty<int>(); writer.Write(safe.Length);
            for (int index = 0; index < safe.Length; index++) writer.Write(safe[index]);
        }
        private static void WriteByteArray(BinaryWriter writer, byte[] values)
        {
            byte[] safe = values ?? Array.Empty<byte>(); writer.Write(safe.Length); writer.Write(safe);
        }
    }
}
