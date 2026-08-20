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

            CheckCount("singleplayer capacity", 7, 8, 8, 0, false, true, 1);
            CheckCount("multiplayer reserves second human", 6, 8, 8, 0, false, false, 1);
            CheckCount("multiplayer with two humans", 6, 8, 8, 0, false, false, 2);
            CheckCount("multiplayer lobby cap wins", 3, 8, 5, 0, false, false, 2);
            CheckCount("custom coop map cap", 2, 8, 8, 4, true, false, 2);
            CheckCount("custom coop reserves partner", 2, 8, 8, 4, true, false, 1);
            CheckCount("full custom coop", 0, 8, 8, 4, true, false, 4);
            CheckCount("invalid capacity fails closed", 0, 0, 8, 0, false, false, 1);
            CheckCount("invalid human count fails closed", 0, 8, 8, 0, false, false, -1);

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
            int humans)
        {
            int actual = RandomOpponentLobbyPolicy.GetMaximumAiCount(
                playerCap, lobbyMax, mapMax, customCoop, singleplayer, humans);
            Check(name, expected, actual);
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
