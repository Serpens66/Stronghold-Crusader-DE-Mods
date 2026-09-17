using BepInEx;
using BepInEx.Logging;
using BepInEx.Bootstrap;
using SHCDESE.API;
using SHCDESE.API.Components.ModManager;
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

                if (TryBuildNetworkedDifference(lobby.id, out ModInventoryDifference difference) &&
                    difference.Count == 0)
                {
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"[Serps Mods] Lobby mod hashes differ only because of client-side-only mod differences: " +
                        $"local={localHash}, host={hostHash}. No warning was sent.");
                    return;
                }

                string messageDetails = difference == null
                    ? " " + SerpLocalization.Get(SerpLocalization.SerpsModsLobbyInventoryUnavailable)
                    : BuildInventoryDetails(difference, localName);
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

        private bool TryBuildNetworkedDifference(
            CSteamID lobbyId,
            out ModInventoryDifference difference)
        {
            difference = null;
            string json = SteamMatchmaking.GetLobbyData(
                lobbyId,
                ModInventoryCompatibility.ScriptExtenderLobbyModListToken);
            if (!ModInventoryCompatibility.TryDecodeScriptExtenderMetadata(
                json,
                out List<ModInventoryEntry> hostEntries))
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    "Script Extender lobby mod metadata is unavailable or invalid; " +
                    "using the conservative legacy mod-hash warning.");
                return false;
            }

            difference = ModInventoryCompatibility.Compare(hostEntries, CaptureLocalInventory());
            return true;
        }

        private static List<ModInventoryEntry> CaptureLocalInventory()
        {
            var assets = new List<ModInventoryEntry>();
            foreach (KeyValuePair<ModInfo, string> asset in
                GameAssetModManager.Instance.GetRegisteredAssetDirectories())
            {
                ModInfo info = asset.Key;
                assets.Add(new ModInventoryEntry(
                    info.GUID,
                    info.Name,
                    info.Version,
                    info.NetworkMode == ModNetworkMode.Clientside));
            }

            var plugins = new List<ModInventoryEntry>();
            foreach (KeyValuePair<string, PluginInfo> plugin in Chainloader.PluginInfos)
            {
                plugins.Add(new ModInventoryEntry(
                    plugin.Value.Metadata.GUID,
                    plugin.Value.Metadata.Name,
                    plugin.Value.Metadata.Version?.ToString() ?? string.Empty,
                    false));
            }

            return ModInventoryCompatibility.BuildCanonicalLocalInventory(assets, plugins);
        }

        private string BuildInventoryDetails(
            ModInventoryDifference difference,
            string localName)
        {
            Shared.DebugLogHelper.LogInfo(
                log,
                "Gameplay-relevant lobby mod inventory difference: hostOnly=[" + string.Join("; ", difference.HostOnly) +
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
