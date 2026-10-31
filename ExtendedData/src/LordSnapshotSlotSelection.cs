using System;
using System.Collections.Generic;
using System.Linq;
using CrusaderDE;

namespace ExtendedData
{
    // Snapshot membership is gameplay identity, not availability of an installed media name.
    internal sealed class LordSnapshotSlotSelection
    {
        internal int[] ExpectedPlayers { get; private set; }
        internal int[] SnapshotPlayers { get; private set; }
        internal int[] MissingPlayers { get; private set; }
        internal int[] ExtraPlayers { get; private set; }
        internal int[] InvalidPlayers { get; private set; }
        internal int[] AffectedPlayers => MissingPlayers.Concat(ExtraPlayers).Concat(InvalidPlayers)
            .Distinct().OrderBy(id => id).ToArray();
        internal bool Matches { get; private set; }
        internal string Diagnostic { get; private set; }

        internal static LordSnapshotSlotSelection Inspect(LordDataSnapshot snapshot,
            FRONT_Multiplayer.MPAIVInfo[] infos, Func<int, bool> isActiveAi,
            ISet<int> embeddedPlayers, LordDataSnapshot preparedTrail,
            IReadOnlyList<string> extendedNames = null)
        {
            var expected = new List<int>();
            var invalid = new List<int>();
            var details = new List<string>();
            for (int index = 0; index < 8; index++)
            {
                int playerId = index + 1;
                FRONT_Multiplayer.MPAIVInfo info = infos != null && index < infos.Length ? infos[index] : null;
                bool activeAi = isActiveAi(playerId);
                bool embedded = embeddedPlayers?.Contains(playerId) == true;
                LordDataSlot prepared = preparedTrail?.GetSlot(playerId);
                LordDataSlot published = snapshot?.GetSlot(playerId);
                bool bound = embedded && MatchesConfiguration(prepared, info);
                bool extendedIdentity = !embedded && published != null && info?.lordConfig != null &&
                    MatchesConfiguration(published, info) && info.lordConfig.lordType >= 0 &&
                    extendedNames != null && info.lordConfig.lordType < extendedNames.Count &&
                    !string.IsNullOrWhiteSpace(extendedNames[info.lordConfig.lordType]) &&
                    string.Equals(published.LordName, extendedNames[info.lordConfig.lordType], StringComparison.OrdinalIgnoreCase);
                if (activeAi && (info == null || embedded && info.builtInLord)) invalid.Add(playerId);
                if (activeAi && info?.builtInLord == false)
                {
                    expected.Add(playerId);
                    // Empty names require an exact embedded binding or a published Extended identity.
                    // Claimed embedded slots must retain that binding even if media supplies a name.
                    if (info.lordConfig == null || embedded && !bound ||
                        published != null && !MatchesConfiguration(published, info) ||
                        string.IsNullOrWhiteSpace(info.lordName) && !bound && !extendedIdentity) invalid.Add(playerId);
                }
                details.Add("player=" + playerId + ",activeAi=" + activeAi +
                    ",selectionPresent=" + (info != null) + ",builtInLord=" + info?.builtInLord +
                    ",embedded=" + embedded + ",preparedBinding=" + bound + ",extendedIdentity=" + extendedIdentity +
                    ",lord=" + Label(info?.lordName) +
                    ",lordType=" + info?.lordType + ",configType=" + info?.lordConfig?.lordType +
                    ",config=" + Label(info?.lordConfig?.name) +
                    ",configChecksum=" + info?.lordConfig?.checksum +
                    ",preparedConfig=" + Label(prepared?.ConfigName) +
                    ",preparedChecksum=" + Label(prepared?.ConfigChecksum) +
                    ",snapshotConfig=" + Label(published?.ConfigName) +
                    ",snapshotChecksum=" + Label(published?.ConfigChecksum));
            }
            var result = new LordSnapshotSlotSelection
            {
                ExpectedPlayers = expected.ToArray(),
                SnapshotPlayers = snapshot?.Slots.Select(slot => slot.PlayerId).OrderBy(id => id).ToArray()
                    ?? Array.Empty<int>(),
                InvalidPlayers = invalid.ToArray(),
            };
            result.MissingPlayers = result.ExpectedPlayers.Except(result.SnapshotPlayers).ToArray();
            result.ExtraPlayers = result.SnapshotPlayers.Except(result.ExpectedPlayers).ToArray();
            result.Matches = infos != null && result.InvalidPlayers.Length == 0 &&
                LordDataSyncDiagnostics.MatchesSelectedSlots(snapshot, result.ExpectedPlayers);
            result.Diagnostic = "selectionAvailable=" + (infos != null) +
                "; expected=[" + string.Join(",", result.ExpectedPlayers) +
                "]; snapshot=[" + string.Join(",", result.SnapshotPlayers) +
                "]; missing=[" + string.Join(",", result.MissingPlayers) +
                "]; extra=[" + string.Join(",", result.ExtraPlayers) +
                "]; invalidBindings=[" + string.Join(",", result.InvalidPlayers) +
                "]; current=[" + string.Join("; ", details) + "]";
            return result;
        }

        // Names in Vanilla's MP restart block are optional media identities. The two
        // caller-supplied envelopes must already have passed their digest/schema checks.
        internal static bool MatchesSavedIdentity(string savedName, LordDataSlot slot,
            LordPackageSlot package, IReadOnlyList<string> extendedNames)
        {
            if (slot == null) return false;
            if (!string.IsNullOrWhiteSpace(savedName))
                return string.Equals(savedName, slot.LordName, StringComparison.OrdinalIgnoreCase);
            if (package?.EmbeddedTrail == true && package.PlayerId == slot.PlayerId &&
                package.LordType == slot.LordType &&
                string.Equals(package.LordName, slot.LordName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(package.ConfigName, slot.ConfigName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(package.ConfigChecksum, slot.ConfigChecksum, StringComparison.Ordinal)) return true;
            return slot.LordType.HasValue && slot.LordType.Value >= 0 && extendedNames != null &&
                slot.LordType.Value < extendedNames.Count && !string.IsNullOrWhiteSpace(extendedNames[slot.LordType.Value]) &&
                string.Equals(extendedNames[slot.LordType.Value], slot.LordName, StringComparison.OrdinalIgnoreCase);
        }

        private static bool MatchesConfiguration(LordDataSlot slot, FRONT_Multiplayer.MPAIVInfo info) =>
            slot != null && info?.lordConfig != null && slot.LordType == info.lordConfig.lordType &&
            string.Equals(slot.ConfigName, info.lordConfig.name, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(slot.ConfigChecksum, info.lordConfig.checksum.ToString(), StringComparison.Ordinal);

        // The support reporter splits long diagnostics into bounded log records, so names
        // need no truncation. Only delimiters/control characters are escaped for readable fields.
        private static string Label(string value) => value == null ? "null" :
            new string(value.Select(character => char.IsControl(character) ||
                character == '[' || character == ']' || character == ';' || character == ',' ||
                character == '=' ? '_' : character).ToArray());
    }
}
