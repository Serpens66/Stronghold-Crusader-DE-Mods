using System;

namespace BugfixesAndQoL
{
    internal static class Program
    {
        private static int failures;

        private static int Main()
        {
            Check("human is protected", false, RandomOpponentLobbyPolicy.IsRemovableAi(true, true));
            Check("non-skirmish member is protected", false, RandomOpponentLobbyPolicy.IsRemovableAi(false, false));
            Check("AI is removable", true, RandomOpponentLobbyPolicy.IsRemovableAi(true, false));

            CheckRandomCount("one random opponent", true, 1, "-1");
            CheckRandomCount("seven random opponents", true, 7, "-7");
            CheckRandomCount("eight random opponents rejected", false, 0, "-8");
            CheckRandomCount("positive random count rejected", false, 0, "7");
            CheckRandomCount("invalid random count rejected", false, 0, "invalid");
            CheckTemporaryStateRestore("false state", false);
            CheckTemporaryStateRestore("true state", true);
            CheckTemporaryStateExceptionPreservation();

            CheckCount("singleplayer capacity", 7, 8, 8, 8, false, true, true, 1);
            CheckCount("singleplayer ignores full-AI setting", 7, 8, 8, 8, false, true, false, 1);
            CheckCount("multiplayer fills final AI seat", 7, 8, 8, 8, false, false, true, 1);
            CheckCount("disabled feature reserves final AI seat", 6, 8, 8, 8, false, false, false, 1);
            CheckCount("multiplayer host smaller lobby cap", 4, 8, 5, 8, false, false, true, 1);
            CheckCount("disabled feature with smaller cap", 3, 8, 5, 8, false, false, false, 1);
            CheckCount("multiplayer with two humans", 6, 8, 8, 8, false, false, true, 2);
            CheckCount("disabled feature with two humans", 6, 8, 8, 8, false, false, false, 2);
            CheckCount("multiplayer lobby cap wins", 3, 8, 5, 8, false, false, true, 2);
            CheckCount("multiplayer map cap wins", 3, 8, 8, 5, false, false, true, 2);
            CheckCount("custom coop map cap", 2, 8, 8, 4, true, false, true, 2);
            CheckCount("custom coop reserves partner", 2, 8, 8, 4, true, false, true, 1);
            CheckCount("custom coop ignores full-AI setting", 2, 8, 8, 4, true, false, false, 1);
            CheckCount("full custom coop", 0, 8, 8, 4, true, false, true, 4);
            CheckInvalidCount("invalid capacity fails closed", 0, 8, 8, false, false, true, 1);
            CheckInvalidCount("oversized player cap fails closed", 9, 8, 8, false, false, true, 1);
            CheckInvalidCount("oversized lobby cap fails closed", 8, 9, 8, false, false, true, 1);
            CheckInvalidCount("missing map cap fails closed", 8, 8, 0, false, false, true, 1);
            CheckInvalidCount("oversized custom coop cap fails closed", 8, 8, 9, true, false, true, 1);
            CheckInvalidCount("negative human count fails closed", 8, 8, 8, false, false, true, -1);
            CheckInvalidCount("zero humans fail closed", 8, 8, 8, false, false, true, 0);
            CheckInvalidCount("oversized human count fails closed", 8, 8, 8, false, false, true, 9);
            CheckInvalidCount("humans above capacity fail closed", 4, 4, 4, false, false, true, 5);

            CheckRelease("host releases final seat", true, true, true, true, false, false, false, 8, 8, 8, 7, 1, 6);
            CheckRelease("smaller lobby cap releases final seat", true, true, true, true, false, false, false, 8, 5, 8, 4, 1, 3);
            CheckRelease("smaller map cap releases final seat", true, true, true, true, false, false, false, 8, 8, 5, 4, 1, 3);
            CheckRelease("disabled mod keeps reservation", false, false, true, true, false, false, false, 8, 8, 8, 7, 1, 6);
            CheckRelease("disabled feature keeps reservation", false, true, false, true, false, false, false, 8, 8, 8, 7, 1, 6);
            CheckRelease("client keeps reservation", false, true, true, false, false, false, false, 8, 8, 8, 7, 1, 6);
            CheckRelease("two humans need no bypass", false, true, true, true, false, false, false, 8, 8, 8, 7, 2, 5);
            CheckRelease("non-AI member blocks bypass", false, true, true, true, false, false, false, 8, 8, 8, 7, 1, 5);
            CheckRelease("not yet final seat", false, true, true, true, false, false, false, 8, 8, 8, 6, 1, 5);
            CheckRelease("full lobby cannot overfill", false, true, true, true, false, false, false, 8, 8, 8, 8, 1, 7);
            CheckRelease("singleplayer needs no bypass", false, true, true, true, true, false, false, 8, 8, 8, 7, 1, 6);
            CheckRelease("coop keeps partner seat", false, true, true, true, false, true, false, 8, 8, 8, 7, 1, 6);
            CheckRelease("custom coop keeps partner seat", false, true, true, true, false, false, true, 8, 8, 8, 7, 1, 6);
            CheckRelease("invalid lobby cap fails closed", false, true, true, true, false, false, false, 8, 0, 8, 7, 1, 6);
            CheckRelease("invalid map cap fails closed", false, true, true, true, false, false, false, 8, 8, 0, 7, 1, 6);
            CheckRelease("oversized cap fails closed", false, true, true, true, false, false, false, 9, 8, 8, 7, 1, 6);
            CheckRelease("zero human count fails closed", false, true, true, true, false, false, false, 8, 8, 8, 7, 0, 7);
            CheckRelease("negative AI count fails closed", false, true, true, true, false, false, false, 8, 8, 8, 7, 1, -1);

            Console.WriteLine(failures == 0
                ? "BugfixesAndQoL random AI policy tests passed."
                : $"BugfixesAndQoL random AI policy tests failed: {failures}.");
            return failures == 0 ? 0 : 1;
        }

        private static void CheckCount(
            string name,
            int expected,
            int playerCap,
            int lobbyMax,
            int mapMax,
            bool customCoop,
            bool singleplayer,
            bool allowFullAiMultiplayerLobby,
            int humans)
        {
            bool valid = RandomOpponentLobbyPolicy.TryGetMaximumAiCount(
                playerCap,
                lobbyMax,
                mapMax,
                customCoop,
                singleplayer,
                allowFullAiMultiplayerLobby,
                humans,
                out int actual);
            Check(name + " validity", true, valid);
            Check(name, expected, actual);
        }

        private static void CheckInvalidCount(
            string name,
            int playerCap,
            int lobbyMax,
            int mapMax,
            bool customCoop,
            bool singleplayer,
            bool allowFullAiMultiplayerLobby,
            int humans)
        {
            bool valid = RandomOpponentLobbyPolicy.TryGetMaximumAiCount(
                playerCap,
                lobbyMax,
                mapMax,
                customCoop,
                singleplayer,
                allowFullAiMultiplayerLobby,
                humans,
                out int actual);
            Check(name + " validity", false, valid);
            Check(name + " count", 0, actual);
        }

        private static void CheckRelease(
            string name,
            bool expected,
            bool modEnabled,
            bool featureEnabled,
            bool isHost,
            bool singleplayer,
            bool coop,
            bool customCoop,
            int playerCap,
            int lobbyMax,
            int mapMax,
            int members,
            int humans,
            int ais)
        {
            bool actual = RandomOpponentLobbyPolicy.ShouldReleaseFinalAiSeat(
                modEnabled && featureEnabled,
                isHost,
                singleplayer,
                coop,
                customCoop,
                playerCap,
                lobbyMax,
                mapMax,
                members,
                humans,
                ais);
            Check(name, expected, actual);
        }

        private static void CheckRandomCount(string name, bool expectedValid, int expectedCount, string value)
        {
            bool actualValid = RandomOpponentLobbyPolicy.TryGetRandomOpponentCount(value, out int actualCount);
            Check(name + " validity", expectedValid, actualValid);
            Check(name + " count", expectedCount, actualCount);
        }

        private static void CheckTemporaryStateRestore(string name, bool originalState)
        {
            bool state = originalState;
            bool temporaryObserved = false;
            bool restoredObserved = false;
            int cleanupFailures = 0;
            TemporaryBooleanStateGuard.Execute(
                () => state,
                value => state = value,
                !originalState,
                () => temporaryObserved = state == !originalState,
                () => restoredObserved = state == originalState,
                _ => cleanupFailures++);

            Check(name + " temporary state", true, temporaryObserved);
            Check(name + " restored state", originalState, state);
            Check(name + " after-restore ordering", true, restoredObserved);
            Check(name + " cleanup failures", 0, cleanupFailures);
        }

        private static void CheckTemporaryStateExceptionPreservation()
        {
            bool state = false;
            bool afterRestoreRan = false;
            int cleanupFailures = 0;
            Exception observed = null;
            var expected = new InvalidOperationException("action failure");
            try
            {
                TemporaryBooleanStateGuard.Execute(
                    () => state,
                    value => state = value,
                    true,
                    () => throw expected,
                    () =>
                    {
                        afterRestoreRan = state == false;
                        throw new ApplicationException("cleanup failure");
                    },
                    _ => cleanupFailures++);
            }
            catch (Exception ex)
            {
                observed = ex;
            }

            Check("throwing action restored state", false, state);
            Check("throwing action ran post-restore cleanup", true, afterRestoreRan);
            Check("cleanup failure was reported", 1, cleanupFailures);
            Check("cleanup did not replace action exception", expected, observed);
        }

        private static void Check<T>(string name, T expected, T actual)
        {
            if (Equals(expected, actual))
                return;

            failures++;
            Console.Error.WriteLine($"FAIL {name}: expected={expected}, actual={actual}");
        }
    }
}
