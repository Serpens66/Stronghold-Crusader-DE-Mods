using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CastlePlanner
{
    internal sealed class CastleSpawnRequest
    {
        public CastleSpawnRequest(int playerId, string castle, string hash, string filePath)
        {
            PlayerId = playerId;
            Castle = castle;
            Hash = hash;
            FilePath = filePath;
        }

        public int PlayerId { get; }
        public string Castle { get; }
        public string Hash { get; }
        public string FilePath { get; }
    }

    internal sealed class CastleSpawnPlayerReport
    {
        public CastleSpawnPlayerReport(int playerId, string selection, string manifest)
        {
            PlayerId = playerId;
            Selection = selection;
            Manifest = manifest;
        }

        public int PlayerId { get; }
        public string Selection { get; }
        public string Manifest { get; }
    }

    internal static class CastleSpawnCompatibility
    {
        public static bool TryResolvePlayerReports(
            bool isRealMultiplayer,
            IEnumerable<int> humanPlayerIds,
            string localSelection,
            string localManifest,
            string[] synchronizedSelections,
            string[] synchronizedManifests,
            out Dictionary<int, CastleSpawnPlayerReport> reports,
            out string error)
        {
            reports = new Dictionary<int, CastleSpawnPlayerReport>();
            error = string.Empty;
            int[] playerIds = (humanPlayerIds ?? Enumerable.Empty<int>())
                .Distinct()
                .OrderBy(id => id)
                .ToArray();
            if (playerIds.Length == 0)
            {
                error = "No human player IDs were available for castle spawning.";
                return false;
            }
            if (playerIds.Any(id => id <= 0 || id > 8))
            {
                error = "At least one supplied human player ID is outside slots 1 through 8.";
                return false;
            }
            if (!isRealMultiplayer && playerIds.Length != 1)
            {
                error = "Singleplayer castle spawning requires exactly one active human player.";
                return false;
            }

            foreach (int playerId in playerIds)
            {
                // Local skirmishes have no stable Steam roster; real multiplayer must
                // always consume the synchronized per-player companion slots.
                string selection = isRealMultiplayer
                    ? synchronizedSelections != null && playerId < synchronizedSelections.Length
                        ? synchronizedSelections[playerId]
                        : null
                    : localSelection;
                string manifest = isRealMultiplayer
                    ? synchronizedManifests != null && playerId < synchronizedManifests.Length
                        ? synchronizedManifests[playerId]
                        : null
                    : localManifest;
                if (string.IsNullOrEmpty(manifest))
                {
                    error = isRealMultiplayer
                        ? $"Player {playerId} has not reported an AIVJSON inventory."
                        : "The local AIVJSON inventory is unavailable.";
                    return false;
                }
                if (selection == null)
                {
                    error = isRealMultiplayer
                        ? $"Player {playerId} has not reported a personal spawn selection."
                        : "The local castle selection is unavailable.";
                    return false;
                }

                reports.Add(
                    playerId,
                    new CastleSpawnPlayerReport(playerId, selection, manifest));
            }

            return true;
        }

        public static string NormalizeSelection(
            string value,
            IEnumerable<string> availableCastles,
            bool catalogComplete)
        {
            string candidate = value?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(candidate))
                return string.Empty;
            string available = (availableCastles ?? Enumerable.Empty<string>())
                .FirstOrDefault(option =>
                    string.Equals(option, candidate, StringComparison.OrdinalIgnoreCase));
            if (available != null)
                return available;
            return catalogComplete ? string.Empty : candidate;
        }

        public static string EncodeManifest(
            IReadOnlyDictionary<string, string> hashes)
        {
            if (hashes == null || hashes.Count == 0)
                return "v1";

            return "v1\n" + string.Join("\n", hashes
                .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
                .Select(entry =>
                    Convert.ToBase64String(Encoding.UTF8.GetBytes(entry.Key)) +
                    "|" + entry.Value));
        }

        public static Dictionary<string, string> DecodeManifest(string manifest)
        {
            return TryDecodeManifest(manifest, out Dictionary<string, string> result)
                ? result
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public static bool TryDecodeManifest(
            string manifest,
            out Dictionary<string, string> result)
        {
            result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(manifest))
                return false;

            string[] lines = manifest.Split(
                new[] { '\n' },
                StringSplitOptions.None);
            if (lines.Length == 0 ||
                !string.Equals(lines[0].TrimEnd('\r'), "v1", StringComparison.Ordinal))
                return false;

            var strictUtf8 = new UTF8Encoding(false, true);

            foreach (string line in lines.Skip(1))
            {
                string normalizedLine = line.TrimEnd('\r');
                int separator = normalizedLine.LastIndexOf('|');
                if (separator <= 0 || separator == normalizedLine.Length - 1)
                    return false;

                try
                {
                    string name = strictUtf8.GetString(
                        Convert.FromBase64String(normalizedLine.Substring(0, separator)));
                    string hash = normalizedLine.Substring(separator + 1);
                    if (string.IsNullOrWhiteSpace(name) || !IsSha256(hash) ||
                        result.ContainsKey(name))
                    {
                        return false;
                    }
                    result.Add(name, hash);
                }
                catch (Exception exception) when (
                    exception is FormatException ||
                    exception is DecoderFallbackException)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsSha256(string value)
        {
            if (value == null || value.Length != 64)
                return false;
            foreach (char character in value)
            {
                bool hexadecimal =
                    (character >= '0' && character <= '9') ||
                    (character >= 'a' && character <= 'f') ||
                    (character >= 'A' && character <= 'F');
                if (!hexadecimal)
                    return false;
            }
            return true;
        }

        public static bool IsAvailableToAll(
            string castle,
            IEnumerable<int> humanPlayerIds,
            string[] manifests,
            out string expectedHash)
        {
            int[] playerIds = (humanPlayerIds ?? Enumerable.Empty<int>()).ToArray();
            var inventories = new Dictionary<int, IReadOnlyDictionary<string, string>>();
            foreach (int playerId in playerIds)
            {
                if (manifests == null || playerId <= 0 || playerId >= manifests.Length)
                    continue;
                inventories[playerId] = DecodeManifest(manifests[playerId]);
            }

            return IsAvailableToAll(
                castle,
                playerIds,
                inventories,
                out expectedHash);
        }

        public static bool IsAvailableToAll(
            string castle,
            IEnumerable<int> humanPlayerIds,
            IReadOnlyDictionary<int, IReadOnlyDictionary<string, string>> inventories,
            out string expectedHash)
        {
            expectedHash = string.Empty;
            if (string.IsNullOrEmpty(castle))
                return true;

            bool foundPlayer = false;
            foreach (int playerId in humanPlayerIds ?? Enumerable.Empty<int>())
            {
                foundPlayer = true;
                if (inventories == null ||
                    !inventories.TryGetValue(playerId, out IReadOnlyDictionary<string, string> inventory))
                    return false;

                if (!inventory.TryGetValue(castle, out string hash))
                    return false;
                if (string.IsNullOrEmpty(expectedHash))
                    expectedHash = hash;
                else if (!string.Equals(expectedHash, hash, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return foundPlayer;
        }
    }
}
