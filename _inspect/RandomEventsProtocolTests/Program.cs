using MessagePack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace RandomEvents
{
    internal static class Program
    {
        private static int assertions;
        private static void Main()
        {
            TestConfigurationDigest();
            TestCooldowns();
            TestPackets();
            TestSaveState();
            TestPresentationTargeting();
            TestSignpostSelection();
            TestArcherSourceTargetingScope();
            Console.WriteLine($"PASS: RandomEvents protocol tests ({assertions} assertions).");
        }

        private static void TestConfigurationDigest()
        {
            RandomEventsConfigurationSnapshot first = CreateConfiguration();
            RandomEventsConfigurationSnapshot second = CreateConfiguration();
            Assert(first.GetDigest().SequenceEqual(second.GetDigest()), "equal configurations must have equal digests");
            second.Chances[14]++;
            Assert(!first.GetDigest().SequenceEqual(second.GetDigest()), "a changed setting must change the digest");
        }

        private static void TestCooldowns()
        {
            RandomEventsRuntimeState shared = CreateState(0);
            Assert(RandomEventsCooldownCodec.CreateCandidates(shared)[0].Encoding == RandomEventsCooldownEncoding.None, "zero shared cooldowns use None");
            shared.SharedCooldownUntilAbsoluteMonths[3] = 42;
            RoundTripCooldown(shared);

            RandomEventsRuntimeState individual = CreateState(1);
            Assert(RandomEventsCooldownCodec.CreateCandidates(individual)[0].Encoding == RandomEventsCooldownEncoding.None, "zero individual cooldowns use None");
            individual.IndividualCooldownUntilAbsoluteMonths[15] = 21;
            individual.IndividualCooldownUntilAbsoluteMonths[134] = 99;
            foreach (RandomEventsCooldownPayload payload in RandomEventsCooldownCodec.CreateCandidates(individual))
                RoundTripCooldown(individual, payload);

            ExpectFailure(() => RandomEventsCooldownCodec.Decode(1, (int)RandomEventsCooldownEncoding.IndividualSparse, new[] { 0, 4 }, out _, out _), "slot zero must fail");
            ExpectFailure(() => RandomEventsCooldownCodec.Decode(1, (int)RandomEventsCooldownEncoding.IndividualSparse, new[] { 15, 4, 15, 5 }, out _, out _), "duplicates must fail");
            ExpectFailure(() => RandomEventsCooldownCodec.Decode(0, (int)RandomEventsCooldownEncoding.SharedDense, new[] { -1 }.Concat(new int[14]).ToArray(), out _, out _), "negative cooldown must fail");
        }

        private static void TestPackets()
        {
            byte[] digest = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();
            var initialization = new RandomEventsInitializationChorePacket
            {
                ProtocolVersion = 2, OperationId = 7, ConfigurationDigest = digest,
                PrngState0 = 1, PrngState1 = 2, NextDueAbsoluteMonth = 20, StartAbsoluteMonth = 10,
                CooldownEncoding = 0
            };
            byte[] first = MessagePackSerializer.Serialize(initialization);
            byte[] retry = MessagePackSerializer.Serialize(initialization);
            Assert(first.SequenceEqual(retry), "retry bytes must be identical");
            Assert(first.Length + 2 < 1200, "initialization must fit Chore limit");
            Assert(MessagePackSerializer.Deserialize<RandomEventsInitializationChorePacket>(first).ConfigurationDigest.SequenceEqual(digest), "initialization roundtrip");

            var batch = new RandomEventsBatchChorePacket { ProtocolVersion = 2, OperationId = 8, PrngState0 = 3, PrngState1 = 4, DueAbsoluteMonth = 20 };
            Assert(MessagePackSerializer.Serialize(batch).Length + 2 < 1200, "empty batch must fit Chore limit");
            var signpost = new RandomEventsSignpostChorePacket { ProtocolVersion = 2, OperationId = 9 };
            Assert(MessagePackSerializer.Deserialize<RandomEventsSignpostChorePacket>(MessagePackSerializer.Serialize(signpost)).OperationId == 9, "signpost roundtrip");
            var ack = new RandomEventsInitializationAckPacket { ProtocolVersion = 2, OperationId = 7, PlayerId = 2, StateDigest = digest };
            Assert(MessagePackSerializer.Deserialize<RandomEventsInitializationAckPacket>(MessagePackSerializer.Serialize(ack)).StateDigest.SequenceEqual(digest), "ACK roundtrip");
        }

        private static void TestSaveState()
        {
            var saved = new RandomEventsSaveState
            {
                PrngState0 = 11, PrngState1 = 12, NextDueAbsoluteMonth = 22, StartAbsoluteMonth = 10,
                SharedCooldownUntilAbsoluteMonths = new int[15], IndividualCooldownUntilAbsoluteMonths = new int[135],
                BatchPrepared = true, PreparedDirectKinds = new[] { 1 }, PreparedDirectStrengths = new[] { 9 },
                PreparedDirectTargetPlayerIds = new[] { 2 }, SignpostsInitialized = true,
                SignpostBuildingIds = new[] { 1, 2, 3, 4 }
            };
            RandomEventsSaveState restored = MessagePackSerializer.Deserialize<RandomEventsSaveState>(MessagePackSerializer.Serialize(saved));
            Assert(restored.SchemaVersion == RandomEventsSaveState.CurrentSchemaVersion, "save schema version");
            Assert(restored.BatchPrepared && restored.PreparedDirectTargetPlayerIds.SequenceEqual(new[] { 2 }), "prepared batch save roundtrip");
            Assert(typeof(RandomEventsSaveState).GetField("Chances") == null, "save schema must not persist configuration");
        }

        private static void TestPresentationTargeting()
        {
            Assert(!RandomEventsPresentationScope.ShouldSuppress(1, 1), "the affected local player must see the event");
            Assert(RandomEventsPresentationScope.ShouldSuppress(2, 1), "a foreign target must be hidden locally");
            Assert(!RandomEventsPresentationScope.IsSuppressed, "presentation starts enabled");
            using (RandomEventsPresentationScope.Begin(2, 1))
            {
                Assert(RandomEventsPresentationScope.IsSuppressed, "foreign-target presentation is suppressed");
                RandomEventsPresentationScope.RecordSuppressedPresentation();
                RandomEventsPresentationScope.RecordSuppressedActionPoint();
                using (RandomEventsPresentationScope.Begin(1, 1))
                    Assert(RandomEventsPresentationScope.IsSuppressed, "nested local scope cannot cancel foreign suppression");
                Assert(RandomEventsPresentationScope.IsSuppressed, "outer suppression survives nested scope disposal");
            }
            Assert(!RandomEventsPresentationScope.IsSuppressed, "presentation suppression is restored after dispatch");
            RandomEventsPresentationScope.GetSuppressedCallCounts(out int presentations, out int actionPoints);
            Assert(presentations == 1 && actionPoints == 1, "suppressed native UI calls are counted for diagnostics");
        }

        private static void TestSignpostSelection()
        {
            var candidates = new[]
            {
                new SignpostTarget(9, 100, 100, 50.0, "keep"),
                new SignpostTarget(7, 200, 200, 25.0, "keep"),
                new SignpostTarget(3, 300, 300, 25.0, "keep")
            };
            Assert(SignpostTargetSelection.TrySelectClosest(candidates, out SignpostTarget selected), "a closest signpost must be selected");
            Assert(selected.BuildingId == 3, "equal-distance signposts use the lower building ID");
            Assert(selected.TileX == 300 && selected.TileY == 300 && selected.DistanceReference == "keep", "selected signpost metadata is preserved");
            Assert(!SignpostTargetSelection.TrySelectClosest(Array.Empty<SignpostTarget>(), out _), "an empty candidate list must fail closed");

            var lordFallback = new[] { new SignpostTarget(11, 50, 60, 10.0, "living-lord") };
            Assert(SignpostTargetSelection.TrySelectClosest(lordFallback, out SignpostTarget lordSelected) &&
                lordSelected.DistanceReference == "living-lord", "the living-Lord anchor is preserved for diagnostics");
        }

        private static void TestArcherSourceTargetingScope()
        {
            IntPtr slots = Marshal.AllocHGlobal(ArcherSourceTargetingScope.SlotCount * sizeof(int));
            IntPtr source = Marshal.AllocHGlobal(2 * sizeof(int));
            try
            {
                int[] originals = Enumerable.Range(20, ArcherSourceTargetingScope.SlotCount).ToArray();
                for (int index = 0; index < originals.Length; index++)
                    Marshal.WriteInt32(slots, index * sizeof(int), originals[index]);
                Marshal.WriteInt32(source, 42);
                Marshal.WriteInt32(source, sizeof(int), 371);

                var target = new SignpostTarget(944, 581, 185, 112.09, "keep");
                Assert(ArcherSourceTargetingScope.TryBegin(
                    slots, source, target, out IDisposable scope, out int originalX, out int originalY, out string failure),
                    "valid archer targeting scope must begin: " + failure);
                Assert(originalX == 42 && originalY == 371, "original archer source is captured for diagnostics");
                Assert(Marshal.ReadInt32(slots) == 944, "selected signpost is exposed in slot zero");
                for (int index = 1; index < ArcherSourceTargetingScope.SlotCount; index++)
                    Assert(Marshal.ReadInt32(slots, index * sizeof(int)) == 0, $"signpost slot {index} is hidden");
                Assert(Marshal.ReadInt32(source) == 581 && Marshal.ReadInt32(source, sizeof(int)) == 185, "target source coordinates are injected");

                try
                {
                    using (scope)
                        throw new InvalidOperationException("simulated event failure");
                }
                catch (InvalidOperationException)
                {
                }
                scope.Dispose();
                for (int index = 0; index < originals.Length; index++)
                    Assert(Marshal.ReadInt32(slots, index * sizeof(int)) == originals[index], $"signpost slot {index} is restored");
                Assert(Marshal.ReadInt32(source) == 42 && Marshal.ReadInt32(source, sizeof(int)) == 371, "source coordinates are restored after an exception");

                Assert(!ArcherSourceTargetingScope.TryBegin(
                    slots, source, new SignpostTarget(0, 581, 185, 0, "keep"),
                    out _, out _, out _, out _), "invalid building IDs fail closed");
            }
            finally
            {
                Marshal.FreeHGlobal(source);
                Marshal.FreeHGlobal(slots);
            }
        }

        private static RandomEventsConfigurationSnapshot CreateConfiguration() => new RandomEventsConfigurationSnapshot
        {
            Enabled = true, IntervalMonths = 3, CooldownMonths = 6, MultiplayerMode = 0,
            Chances = Enumerable.Range(0, 15).ToArray(), StrengthMinimums = Enumerable.Range(1, 6).ToArray(), StrengthMaximums = Enumerable.Range(11, 6).ToArray()
        };
        private static RandomEventsRuntimeState CreateState(int mode)
        {
            var state = new RandomEventsRuntimeState { MultiplayerMode = mode, SharedCooldownUntilAbsoluteMonths = new int[15], IndividualCooldownUntilAbsoluteMonths = new int[135] };
            CreateConfiguration().ApplyTo(state); state.MultiplayerMode = mode; return state;
        }
        private static void RoundTripCooldown(RandomEventsRuntimeState state) => RoundTripCooldown(state, RandomEventsCooldownCodec.CreateCandidates(state)[0]);
        private static void RoundTripCooldown(RandomEventsRuntimeState state, RandomEventsCooldownPayload payload)
        {
            RandomEventsCooldownCodec.Decode(state.MultiplayerMode, (int)payload.Encoding, payload.Data, out int[] shared, out int[] individual);
            Assert(shared.SequenceEqual(state.SharedCooldownUntilAbsoluteMonths), $"{payload.Encoding} shared roundtrip");
            Assert(individual.SequenceEqual(state.IndividualCooldownUntilAbsoluteMonths), $"{payload.Encoding} individual roundtrip");
        }
        private static void ExpectFailure(Action action, string message)
        {
            try { action(); } catch { assertions++; return; }
            throw new InvalidOperationException(message);
        }
        private static void Assert(bool condition, string message)
        {
            assertions++; if (!condition) throw new InvalidOperationException(message);
        }
    }
}
