using System;
using System.Collections.Generic;
using System.Globalization;

namespace MultiplayerSaveLoadTest
{
    // Shared localization owns game-language selection. Embedded defaults also cover
    // missing assets; Vanilla confirmation headings have a single-line width budget.
    internal static class LoadText
    {
        internal const int MaximumTitleElements = 14;
        private static readonly Dictionary<string, string> english = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Load.ConfirmTitle"] = "End match?",
            ["Load.CategoryTitle"] = "Coop saves?",
            ["Load.RecoveryTitle"] = "Main menu",
            ["Load.WaitPeers"] = "Waiting for all players to enable multiplayer loading.",
            ["Load.Transition"] = "Moving the players to the multiplayer load lobby.",
            ["Load.Failed"] = "The multiplayer load lobby could not be opened. See the log.",
            ["Load.NotReady"] = "Multiplayer loading is not available yet."
        };

        internal static IEnumerable<string> Keys => english.Keys;
        internal static string Get(string key) => Resolve(key, SerpLocalization.Get);

        internal static string Resolve(string key, Func<string, string> lookup)
        {
            if (!english.TryGetValue(key, out string fallback)) throw new ArgumentOutOfRangeException(nameof(key));
            string value = lookup(key);
            if (string.IsNullOrWhiteSpace(value) || string.Equals(value, key, StringComparison.Ordinal)) return fallback;
            if (key.EndsWith("Title", StringComparison.Ordinal) &&
                (value.IndexOfAny(new[] { '\r', '\n' }) >= 0 || new StringInfo(value).LengthInTextElements > MaximumTitleElements))
                return fallback;
            return value;
        }
    }
}
