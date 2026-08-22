using System;
using System.Collections.Generic;

namespace BugfixesAndQoL
{
    internal static class Program
    {
        private static int failures;

        private static int Main()
        {
            CheckOrder(
                "first click groups Workshop, local, Vanilla",
                false,
                "Workshop A", "Workshop Z", "Local A", "Local Z", "Vanilla A", "Vanilla Z", "Unknown");
            CheckOrder(
                "second click groups Vanilla, local, Workshop",
                true,
                "Vanilla A", "Vanilla Z", "Local A", "Local Z", "Workshop A", "Workshop Z", "Unknown");

            MapOriginSortKey contradictory = Key("Contradictory", true, true, true);
            Check(
                "contradictory flags follow displayed Vanilla icon",
                0,
                MapOriginSortPolicy.GetOriginRank(contradictory));
            Check(
                "Workshop has priority over local when Vanilla is false",
                2,
                MapOriginSortPolicy.GetOriginRank(Key("Mixed", false, true, true)));
            Check(
                "missing flags are unknown",
                3,
                MapOriginSortPolicy.GetOriginRank(Key("Unknown", false, false, false)));

            Console.WriteLine(failures == 0
                ? "BugfixesAndQoL map-origin sort policy tests passed."
                : $"BugfixesAndQoL map-origin sort policy tests failed: {failures}.");
            return failures == 0 ? 0 : 1;
        }

        private static void CheckOrder(string name, bool ascending, params string[] expected)
        {
            List<MapOriginSortKey> keys = new List<MapOriginSortKey>
            {
                Key("Local Z", false, true, false),
                Key("Workshop Z", false, false, true),
                Key("Vanilla Z", true, false, false),
                Key("Unknown", false, false, false),
                Key("Local A", false, true, false),
                Key("Workshop A", false, false, true),
                Key("Vanilla A", true, false, false),
            };
            keys.Sort((left, right) => MapOriginSortPolicy.Compare(left, right, ascending));

            for (int index = 0; index < expected.Length; index++)
                Check($"{name} row {index}", expected[index], keys[index].DisplayName);
        }

        private static MapOriginSortKey Key(
            string name,
            bool builtIn,
            bool user,
            bool workshop) =>
            new MapOriginSortKey(builtIn, user, workshop, name);

        private static void Check<T>(string name, T expected, T actual)
        {
            if (Equals(expected, actual))
                return;

            failures++;
            Console.Error.WriteLine($"FAIL {name}: expected={expected}, actual={actual}");
        }
    }
}
