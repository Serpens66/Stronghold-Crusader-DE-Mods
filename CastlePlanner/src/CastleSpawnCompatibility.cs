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

    internal static class CastleSpawnCompatibility
    {
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
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(manifest))
                return result;

            string[] lines = manifest.Split(
                new[] { '\n' },
                StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0 || !string.Equals(lines[0].Trim(), "v1", StringComparison.Ordinal))
                return result;

            foreach (string line in lines.Skip(1))
            {
                int separator = line.LastIndexOf('|');
                if (separator <= 0 || separator == line.Length - 1)
                    continue;

                try
                {
                    string name = Encoding.UTF8.GetString(
                        Convert.FromBase64String(line.Substring(0, separator)));
                    string hash = line.Substring(separator + 1).Trim();
                    if (!string.IsNullOrWhiteSpace(name) && IsSha256(hash))
                        result[name] = hash;
                }
                catch (FormatException)
                {
                    // Malformed remote entries are omitted and therefore fail compatibility.
                }
            }

            return result;
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
