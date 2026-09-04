using BepInEx.Logging;
using SHCDESE.API;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SerpsModsHost
{
    internal sealed class LobbyModHashWarning
    {
        private const string LobbyModHashToken = "_SE_MODHASH_";
        private static readonly MethodInfo ComputeActiveModHashMethod =
            typeof(GameNetworkAPI).GetMethod(
                "ComputeActiveModHash",
                BindingFlags.Static | BindingFlags.NonPublic);

        private readonly ManualLogSource log;

        internal LobbyModHashWarning(ManualLogSource log)
        {
            this.log = log;
        }

        internal void CheckAfterJoin(Platform_Multiplayer.MPLobby lobby)
        {
            if (lobby == null || lobby.isHost)
                return;

            try
            {
                if (ComputeActiveModHashMethod == null)
                    throw new MissingMethodException(typeof(GameNetworkAPI).FullName, "ComputeActiveModHash");

                string localHash = ComputeActiveModHashMethod.Invoke(null, null) as string;
                string hostHash = SteamMatchmaking.GetLobbyData(lobby.id, LobbyModHashToken);
                if (string.IsNullOrWhiteSpace(localHash) || string.IsNullOrWhiteSpace(hostHash))
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"[Serps Mods] Lobby mod hashes could not be compared: " +
                        $"localHash={FormatHash(localHash)}, hostHash={FormatHash(hostHash)}.");
                    return;
                }

                CSteamID hostId = SteamMatchmaking.GetLobbyOwner(lobby.id);
                string localName = SteamFriends.GetPersonaName();
                string hostName = SteamFriends.GetFriendPersonaName(hostId);
                string template = SerpLocalization.Get(SerpLocalization.SerpsModsLobbyHashMismatch);
                if (!ModHashCompatibility.TryCreateMismatchMessage(
                    localHash,
                    hostHash,
                    localName,
                    hostName,
                    template,
                    out string message))
                {
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"[Serps Mods] Lobby mod hashes match: local={localHash}, host={hostHash}.");
                    return;
                }

                string messageDetails = BuildInventoryDetails(lobby.id, localName);
                message += messageDetails + " " + SerpLocalization.Get(SerpLocalization.SerpsModsLobbyHashFolders);

                Platform_Multiplayer.Instance.SendLobbyChatMessage(message);
                Shared.DebugLogHelper.LogError(
                    log,
                    $"[Serps Mods] Lobby mod hash mismatch announced: " +
                    $"player={localName}, host={hostName}, localHash={localHash}, hostHash={hostHash}.");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"[Serps Mods] Lobby mod hash comparison failed: {Unwrap(ex)}");
            }
        }

        private static string FormatHash(string value) =>
            string.IsNullOrWhiteSpace(value) ? "missing" : value;

        private string BuildInventoryDetails(CSteamID lobbyId, string localName)
        {
            string encoded = SteamMatchmaking.GetLobbyData(
                lobbyId,
                LobbyModInventoryPublisher.LobbyInventoryToken);
            if (!ModInventoryCompatibility.TryDecode(encoded, out List<ModInventoryEntry> hostEntries))
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    "Exact lobby mod inventory is unavailable or invalid; using the folder guidance fallback.");
                return " " + SerpLocalization.Get(SerpLocalization.SerpsModsLobbyInventoryUnavailable);
            }

            List<ModInventoryEntry> localEntries = LobbyModInventoryPublisher.Capture();
            ModInventoryDifference difference = ModInventoryCompatibility.Compare(hostEntries, localEntries);
            if (difference.Count == 0)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    "Lobby mod hashes differ although the published GUID/version inventories match.");
                return " " + SerpLocalization.Get(SerpLocalization.SerpsModsLobbyInventoryUnavailable);
            }

            Shared.DebugLogHelper.LogInfo(
                log,
                "Full lobby mod inventory difference: hostOnly=[" + string.Join("; ", difference.HostOnly) +
                "], clientOnly=[" + string.Join("; ", difference.ClientOnly) +
                "], versions=[" + string.Join("; ", difference.VersionMismatches) + "].");

            int remaining = 4;
            var sections = new List<string>();
            AddSection(
                sections,
                SerpLocalization.Get(SerpLocalization.SerpsModsLobbyHostOnly),
                difference.HostOnly,
                ref remaining);
            AddSection(
                sections,
                SerpLocalization.Get(
                    SerpLocalization.SerpsModsLobbyClientOnly,
                    "Player", localName),
                difference.ClientOnly,
                ref remaining);
            AddSection(
                sections,
                SerpLocalization.Get(SerpLocalization.SerpsModsLobbyVersions),
                difference.VersionMismatches,
                ref remaining);

            int shown = 4 - remaining;
            int omitted = difference.Count - shown;
            string result = " " + string.Join(" ", sections);
            if (omitted > 0)
            {
                result += " " + SerpLocalization.Get(
                    SerpLocalization.SerpsModsLobbyMoreDifferences,
                    "Count", omitted.ToString());
            }
            return result;
        }

        private static void AddSection(
            ICollection<string> sections,
            string label,
            IReadOnlyCollection<string> values,
            ref int remaining)
        {
            if (remaining <= 0 || values.Count == 0)
                return;
            string[] shown = values.Take(remaining).ToArray();
            remaining -= shown.Length;
            sections.Add(label + ": " + string.Join(", ", shown) + ".");
        }

        private static Exception Unwrap(Exception ex) =>
            ex is TargetInvocationException invocation && invocation.InnerException != null
                ? invocation.InnerException
                : ex;
    }
}
