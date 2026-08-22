using BepInEx.Logging;
using SHCDESE.API;
using Steamworks;
using System;
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

        private static Exception Unwrap(Exception ex) =>
            ex is TargetInvocationException invocation && invocation.InnerException != null
                ? invocation.InnerException
                : ex;
    }
}
