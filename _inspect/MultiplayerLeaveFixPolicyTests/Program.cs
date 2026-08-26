using System;
using SHCDESE.API;

namespace MultiplayerLeaveFix
{
    internal static class Program
    {
        private const string RemovingPrefix = "Removing Player :";
        private const string ConnectionPrefix = "Player Connection Issue :";
        private static int assertions;
        private static long now;

        private static void Main()
        {
            TestIntentionalLeaveBurst();
            TestUnintentionalDisconnect();
            TestThreadToMainThreadSemantics();
            TestRejoinAndSlotReuse();
            TestExpirationAndValidation();
            Console.WriteLine($"PASS: Multiplayer leave policy tests ({assertions} assertions).");
        }

        private static void TestIntentionalLeaveBurst()
        {
            MultiplayerLeaveMessagePolicy policy = CreatePolicy();
            Assert(policy.RecordProcessedLeave(3, "Alice", 76561198000000001UL), "processed leave is recorded");
            AssertDisposition(policy, 3, "Alice", RemovingPrefix, LeaveMessageDisposition.SuppressDuplicate, "Vanilla removal emitted by packet processing is already the first message");
            AssertDisposition(policy, 3, "Alice", ConnectionPrefix, LeaveMessageDisposition.AllowFirst, "first related connection message remains visible");
            AssertDisposition(policy, 3, "Alice", ConnectionPrefix, LeaveMessageDisposition.SuppressDuplicate, "immediate connection duplicate is suppressed");

            now += 6;
            AssertDisposition(policy, 3, "Alice", ConnectionPrefix, LeaveMessageDisposition.AllowFirst, "same message is visible after the duplicate burst window");
        }

        private static void TestUnintentionalDisconnect()
        {
            MultiplayerLeaveMessagePolicy policy = CreatePolicy();
            AssertDisposition(policy, 4, "Bob", ConnectionPrefix, LeaveMessageDisposition.NotLimited, "disconnect without a processed leave is never limited");
        }

        private static void TestThreadToMainThreadSemantics()
        {
            MultiplayerLeaveMessagePolicy policy = CreatePolicy();
            AssertDisposition(policy, 5, "Carol", RemovingPrefix, LeaveMessageDisposition.NotLimited, "queued thread pass does not create an intention");
            Assert(policy.RecordProcessedLeave(5, "Carol", 76561198000000002UL), "successful main-thread pass records once");
            AssertDisposition(policy, 5, "Carol", RemovingPrefix, LeaveMessageDisposition.SuppressDuplicate, "main-thread processing seeds the emitted removal message");

            now += 1;
            Assert(policy.RecordProcessedLeave(5, "Carol", 76561198000000002UL), "a later distinct processed leave starts a new generation");
            AssertDisposition(policy, 5, "Carol", ConnectionPrefix, LeaveMessageDisposition.AllowFirst, "old duplicate keys do not leak into a new leave generation");
        }

        private static void TestRejoinAndSlotReuse()
        {
            MultiplayerLeaveMessagePolicy policy = CreatePolicy();
            policy.RecordProcessedLeave(6, "Dana", 76561198000000003UL);
            policy.DiscardForActiveMember(6, "Dana", 76561198000000003UL);
            AssertDisposition(policy, 6, "Dana", ConnectionPrefix, LeaveMessageDisposition.NotLimited, "same player rejoin clears leave state");

            policy.RecordProcessedLeave(6, "Dana", 76561198000000003UL);
            policy.DiscardForActiveMember(6, "Eve", 76561198000000004UL);
            AssertDisposition(policy, 6, "Dana", ConnectionPrefix, LeaveMessageDisposition.NotLimited, "slot reuse by another player clears leave state");
        }

        private static void TestExpirationAndValidation()
        {
            MultiplayerLeaveMessagePolicy policy = CreatePolicy();
            policy.RecordProcessedLeave(7, "Frank", 76561198000000005UL);
            now += 16;
            AssertDisposition(policy, 7, "Frank", ConnectionPrefix, LeaveMessageDisposition.NotLimited, "leave association expires by monotonic TTL");

            Assert(!policy.RecordProcessedLeave(0, "  ", 0), "invalid player without fallback name is rejected");
            int invalidPlayerId = GamePlayerManagerAPI.MAX_PLAYERS + 1;
            Assert(policy.RecordProcessedLeave(invalidPlayerId, "Grace", 76561198000000006UL), "invalid player ID may use a short-lived normalized name fallback");
            AssertDisposition(policy, invalidPlayerId, " Grace ", ConnectionPrefix, LeaveMessageDisposition.AllowFirst, "name fallback matches normalized name without accepting invalid ID");
        }

        private static MultiplayerLeaveMessagePolicy CreatePolicy()
        {
            now = 0;
            return new MultiplayerLeaveMessagePolicy(
                () => now,
                1,
                playerId => playerId > 0 && playerId <= GamePlayerManagerAPI.MAX_PLAYERS);
        }

        private static void AssertDisposition(
            MultiplayerLeaveMessagePolicy policy,
            int playerId,
            string playerName,
            string prefix,
            LeaveMessageDisposition expected,
            string message)
        {
            LeaveMessageDisposition actual = policy.Classify(playerId, playerName, prefix);
            Assert(actual == expected, $"{message}: expected={expected}, actual={actual}");
        }

        private static void Assert(bool condition, string message)
        {
            assertions++;
            if (!condition)
                throw new InvalidOperationException("Assertion failed: " + message);
        }
    }
}
