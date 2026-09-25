using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ExtendedData
{
    internal static class LordDataSyncDiagnostics
    {
        internal static string DescribeSnapshot(LordDataSnapshot snapshot)
        {
            if (snapshot == null)
                return "snapshot=absent";
            return "session=" + SafeLabel(snapshot.SessionId) + ",digest=" + snapshot.Digest +
                ",wireBytes=" + Encoding.UTF8.GetByteCount(snapshot.WireJson) +
                ",fixesInstalled=" + snapshot.FixesInstalled + ",slots=" + snapshot.Slots.Count +
                ",selected=[" + string.Join(";", snapshot.Slots.Select(DescribeSlot)) + "]";
        }

        internal static string DescribeSlot(LordDataSlot slot) =>
            "player=" + slot.PlayerId + ",lord=" + SafeLabel(slot.LordName) +
            ",config=" + SafeLabel(slot.ConfigName) + ",configChecksum=" + SafeLabel(slot.ConfigChecksum) +
            ",modlord=" + DescribeJson(slot.ModLordJson, false) +
            ",fixes=" + DescribeJson(slot.FixesJson, true);

        internal static bool MatchesSelectedSlots(LordDataSnapshot snapshot,
            IEnumerable<int> selectedCustomPlayerIds) =>
            snapshot != null && snapshot.Slots.Select(slot => slot.PlayerId).OrderBy(id => id)
                .SequenceEqual((selectedCustomPlayerIds ?? Enumerable.Empty<int>()).OrderBy(id => id));

        internal static string DescribeJson(string json, bool countProperties)
        {
            if (json == null)
                return "absent";
            string fields = string.Empty;
            if (countProperties)
            {
                try
                {
                    var document = Shared.DependencyFreeJson.Parse(json) as Dictionary<string, object>;
                    fields = ",fields=" + (document == null ? "invalid" : document.Count.ToString());
                }
                catch (Exception exception) when (exception is InvalidDataException || exception is FormatException)
                {
                    fields = ",fields=invalid";
                }
            }
            return "bytes=" + Encoding.UTF8.GetByteCount(json) + ",sha256=" + Hash(json) + fields;
        }

        internal static string DescribeStatus(string status, string expectedDigest)
        {
            if (string.IsNullOrEmpty(status))
                return "missing";
            if (status.StartsWith("READY|", StringComparison.Ordinal))
            {
                string digest = status.Substring("READY|".Length);
                return string.Equals(digest, expectedDigest, StringComparison.Ordinal)
                    ? "ready:match" : "ready:stale:" +
                    (digest.Length == 64 && digest.All(Uri.IsHexDigit) ? digest : "invalid-digest");
            }
            if (status.StartsWith("ERROR|", StringComparison.Ordinal))
                return "error";
            return "invalid:bytes=" + Encoding.UTF8.GetByteCount(status) + ",sha256=" + Hash(status);
        }

        internal static bool IsHostLobby(bool hasLobby, bool isHost, bool singlePlayerCoop,
            bool activeLobbyMatches, bool observedLobbyMatches, bool hasRealLobbyMember) =>
            hasLobby && isHost && !singlePlayerCoop && activeLobbyMatches &&
            observedLobbyMatches && hasRealLobbyMember;

        internal static string DescribeHostGate(bool hasLobby, bool isHost, bool singlePlayerCoop,
            bool activeLobbyMatches, bool observedLobbyMatches, bool hasRealLobbyMember,
            bool realMultiplayer)
        {
            var reasons = new List<string>();
            if (!hasLobby) reasons.Add("no-lobby");
            if (!isHost) reasons.Add("not-host");
            if (singlePlayerCoop) reasons.Add("single-player-coop");
            if (!activeLobbyMatches) reasons.Add("active-lobby-mismatch");
            if (!observedLobbyMatches) reasons.Add("observed-lobby-mismatch");
            if (!hasRealLobbyMember) reasons.Add("no-real-lobby-member");
            return "eligible=" + IsHostLobby(hasLobby, isHost, singlePlayerCoop,
                activeLobbyMatches, observedLobbyMatches, hasRealLobbyMember) + ",reasons=" +
                (reasons.Count == 0 ? "none" : string.Join("+", reasons)) +
                ",hasLobby=" + hasLobby + ",isHost=" + isHost +
                ",singlePlayerCoop=" + singlePlayerCoop + ",activeLobbyMatches=" +
                activeLobbyMatches + ",observedLobbyMatches=" + observedLobbyMatches +
                ",hasRealLobbyMember=" + hasRealLobbyMember +
                ",mapModeRealMultiplayer=" + realMultiplayer;
        }

        internal static string Hash(string value)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty))).Replace("-", "");
        }

        internal static string SafeLabel(string value)
        {
            if (value == null)
                return "null";
            string safe = new string(value.Select(character => char.IsControl(character) ||
                character == '[' || character == ']' || character == ';' || character == ',' ||
                character == '=' ? '_' : character).Take(80).ToArray());
            return value.Length > 80 ? safe + "..." : safe;
        }
    }
}
