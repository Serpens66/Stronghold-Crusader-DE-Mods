using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using SHCDESE.API.Components.ModManager;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SerpsModsHost
{
    internal sealed class LobbyModInventoryPublisher
    {
        internal const string LobbyInventoryToken = "_SERPS_MODLIST_V1_";
        private readonly ManualLogSource log;
        private ulong publishedLobbyId;
        private bool started;

        internal LobbyModInventoryPublisher(ManualLogSource log)
        {
            this.log = log;
        }

        internal void Start()
        {
            if (started)
                return;
            started = true;
            Application.onBeforeRender += OnBeforeRender;
        }

        internal void Stop()
        {
            if (!started)
                return;
            Application.onBeforeRender -= OnBeforeRender;
            started = false;
            publishedLobbyId = 0;
        }

        internal static List<ModInventoryEntry> Capture()
        {
            var entries = new List<ModInventoryEntry>();
            foreach (KeyValuePair<string, PluginInfo> plugin in Chainloader.PluginInfos)
            {
                entries.Add(new ModInventoryEntry(
                    "plugin",
                    plugin.Value.Metadata.GUID,
                    plugin.Value.Metadata.Version?.ToString() ?? string.Empty));
            }
            foreach (KeyValuePair<ModInfo, string> asset in GameAssetModManager.Instance.GetRegisteredAssetDirectories())
                entries.Add(new ModInventoryEntry("asset", asset.Key.GUID, asset.Key.Version));
            return entries;
        }

        private void OnBeforeRender()
        {
            try
            {
                Platform_Multiplayer.MPLobby lobby = Platform_Multiplayer.Instance?.activeLobby;
                ulong lobbyId = lobby?.id.m_SteamID ?? 0;
                if (lobby == null || !lobby.isHost || lobbyId == 0)
                {
                    publishedLobbyId = 0;
                    return;
                }
                if (publishedLobbyId == lobbyId)
                    return;

                List<ModInventoryEntry> entries = Capture();
                string encoded = ModInventoryCompatibility.Encode(entries);
                int byteCount = Encoding.UTF8.GetByteCount(encoded);
                if (byteCount >= Constants.k_cubChatMetadataMax)
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"Lobby mod inventory was not published because it needs {byteCount} bytes; " +
                        $"Steam permits fewer than {Constants.k_cubChatMetadataMax} bytes.");
                    publishedLobbyId = lobbyId;
                    return;
                }

                if (!SteamMatchmaking.SetLobbyData(lobby.id, LobbyInventoryToken, encoded))
                {
                    Shared.DebugLogHelper.LogWarning(log, "Steam rejected the lobby mod inventory metadata.");
                    // Do not retry on every rendered frame. Clients retain the normal hash/folder fallback.
                    publishedLobbyId = lobbyId;
                    return;
                }

                publishedLobbyId = lobbyId;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Published lobby mod inventory: lobby={lobbyId}, entries={entries.Count}, bytes={byteCount}.");
            }
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogWarning(log, "Could not publish lobby mod inventory: " + exception.Message);
                Platform_Multiplayer.MPLobby lobby = Platform_Multiplayer.Instance?.activeLobby;
                if (lobby != null && lobby.isHost)
                    publishedLobbyId = lobby.id.m_SteamID;
            }
        }
    }
}
