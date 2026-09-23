using APIShared;
using BepInEx;
using BepInEx.Logging;
using CrusaderDE;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using UnityEngine;

namespace AivLobbyPresetTest
{
    [BepInDependency("000shcdese", "2.9.0")]
    [BepInDependency("APIShared_Serp", "0.4.0")]
    [BepInDependency("BugfixesAndQoL_Serp")]
    [BepInPlugin("AivLobbyPresetTest_Serp", "AIV Lobby Preset Test", "0.1.0")]
    public sealed class AivLobbyPresetTestPlugin : BaseUnityPlugin
    {
        private static ManualLogSource log;
        private static LobbyPreset pending;
        private static FileHeader pendingMap;
        private static Dictionary<int, CustomisationFileManager.CustomAIV> pendingAivs;
        private static readonly string PresetPath = Path.Combine(Paths.ConfigPath, "AivLobbyPresetTest.json");
        private static Platform_Multiplayer.MPLobby observedLobby;
        private static bool renderCallbackObserved;
        private static bool observerFailureLogged;
        private static bool readyStateObserved;

        private void Awake()
        {
            if (log != null)
                return;
            log = Logger;
            string samplePath = Path.Combine(Paths.PluginPath, "AivLobbyPresetTest_Serp", "CraterLakePreset.json");
            try
            {
                if (!File.Exists(PresetPath) && File.Exists(samplePath))
                    File.Copy(samplePath, PresetPath);
                LobbyPreparationOverride.Register("AivLobbyPresetTest_Serp", Prepare, Apply);
                Application.onBeforeRender += ObserveLobbyBeforeRender;
                Logger.LogInfo("AIV lobby preset registered; config=" + PresetPath);
                Logger.LogInfo("Lobby bridge after registration: " + DescribeBridgeState(null));
            }
            catch (Exception exception)
            {
                Logger.LogError("Preset registration failed: " + exception);
            }
        }

        private static void ObserveLobbyBeforeRender()
        {
            if (log == null)
                return;
            try
            {
                if (!renderCallbackObserved)
                {
                    renderCallbackObserved = true;
                    log.LogInfo("Persistent lobby render observer active.");
                }
                MainViewModel model = MainViewModel.Instance;
                FRONT_Multiplayer view = model?.FRONTMultiplayer;
                Platform_Multiplayer.MPLobby lobby = view?.currentLobby;
                bool changedLobby = !ReferenceEquals(observedLobby, lobby);
                if (changedLobby)
                {
                    observedLobby = lobby;
                    readyStateObserved = false;
                    if (lobby != null)
                        log.LogInfo("Lobby observed: skirmish=" + FRONT_Multiplayer.skirmishGame +
                            ", host=" + lobby.isHost + ", visible=" +
                            (model.Show_MPGameCreation == true) + ", panelActive=" + view.panelActive +
                            ", managedViewNull=" + ReferenceEquals(view, null) +
                            ", noesisViewNull=" + (view == null));
                }

                bool ready = lobby != null && view.panelActive &&
                    model.Show_MPGameCreation == true;
                bool traceBridge = lobby != null &&
                    (changedLobby || (ready && !readyStateObserved));
                if (traceBridge)
                    log.LogInfo("Lobby bridge before Tick: " + DescribeBridgeState(lobby));
                LobbyPreparationOverride.Tick(view);
                if (traceBridge)
                    log.LogInfo("Lobby bridge after Tick: " + DescribeBridgeState(lobby));
                if (ready)
                    readyStateObserved = true;
            }
            catch (Exception exception)
            {
                if (observerFailureLogged)
                    return;
                observerFailureLogged = true;
                log.LogError("Lobby observation failed: " + exception);
            }
        }

        private static string DescribeBridgeState(Platform_Multiplayer.MPLobby lobby)
        {
            Type bridge = typeof(LobbyPreparationOverride);
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
            object Read(string name) => bridge.GetField(name, flags)?.GetValue(null);
            var callback = Read("prepare") as Delegate;
            return "assembly=" + bridge.Assembly.Location +
                ", owner=" + (Read("owner") ?? "<null>") +
                ", callback=" + (callback?.Method.DeclaringType?.FullName ?? "<null>") +
                ", sameLobby=" + ReferenceEquals(Read("currentLobby"), lobby) +
                ", prepared=" + Read("prepared") +
                ", active=" + Read("active") +
                ", applyAttempted=" + Read("applyAttempted");
        }

        private static T LobbyField<T>(FRONT_Multiplayer view, string name)
        {
            FieldInfo field = typeof(FRONT_Multiplayer).GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null || !typeof(T).IsAssignableFrom(field.FieldType))
                throw new MissingFieldException("FRONT_Multiplayer." + name +
                    " does not match the installed game assembly.");
            return (T)field.GetValue(view);
        }

        private static void InvokeLobbyMethod(FRONT_Multiplayer view, string name,
            Type[] parameterTypes, params object[] arguments)
        {
            MethodInfo method = typeof(FRONT_Multiplayer).GetMethod(name,
                BindingFlags.Instance | BindingFlags.NonPublic, null, parameterTypes, null);
            if (method == null)
                throw new MissingMethodException("FRONT_Multiplayer." + name +
                    " does not match the installed game assembly.");
            try { method.Invoke(view, arguments); }
            catch (TargetInvocationException exception)
            {
                throw new InvalidOperationException("Vanilla lobby method " + name + " failed.",
                    exception.InnerException ?? exception);
            }
        }

        private static bool Prepare(FRONT_Multiplayer view)
        {
            log.LogInfo("Preset Prepare entered; config=" + PresetPath +
                ", lobby=" + (view?.currentLobby != null));
            pending = null;
            pendingMap = null;
            pendingAivs = null;
            LobbyPreset preset;
            try { preset = LobbyPreset.Read(PresetPath); }
            catch (Exception exception)
            {
                log.LogError("Preset invalid; Vanilla lobby remains available: " + exception);
                return false;
            }
            if (!preset.Enabled)
            {
                log.LogInfo("Preset disabled in config; no lobby changes.");
                return false;
            }
            if (!FRONT_Multiplayer.skirmishGame || FRONT_Multiplayer.coopGame ||
                FRONT_Multiplayer.customCoopGame || view.trailMakerMode ||
                view.currentLobby == null || !view.currentLobby.isHost ||
                view.currentLobby.CountHumanPlayers() != 1 ||
                view.currentLobby.members.Any(member => !member.SkirmishMember ||
                    (!member.SkirmishHumanMember && member.GetLordType() < 0)))
            {
                log.LogWarning("Preset skipped: this is not a one-human local skirmish lobby.");
                return false;
            }
            var host = view.currentLobby.members.FirstOrDefault(member => member.SkirmishHumanMember);
            if (host == null || view.currentLobby.getThisPlayerFromSteamID(host.id.m_SteamID) != preset.Players[0].Id)
            {
                log.LogWarning("Preset skipped: human player ID differs from the config.");
                return false;
            }
            try
            {
                // The map list may not yet be populated before ShowSetupScreen. Read the
                // installed map catalogue now, then verify the exact displayed row at Apply.
                var maps = MapFileManager.Instance.GetMultiplayerMaps(0, true, 1, true, true, true);
                var matches = maps.Where(header => string.Equals(
                    Path.GetFileName(header.filePath), preset.MapFileName,
                    StringComparison.OrdinalIgnoreCase)).ToList();
                if (matches.Count != 1)
                    throw new InvalidDataException("Expected exactly one map named " + preset.MapFileName);
                FileHeader map = matches[0];
                if (map.maxPlayers < preset.Players.Count ||
                    LobbyField<int>(view, "PlayerCap") < preset.Players.Count)
                    throw new InvalidDataException("The selected map or lobby player cap is too small.");
                using (var stream = File.OpenRead(map.filePath))
                using (var sha = SHA256.Create())
                {
                    string hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
                    if (!string.Equals(hash, preset.MapSha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Map SHA-256 mismatch: " + hash);
                }
                foreach (var player in preset.Players)
                {
                    if (map.keep_locations[player.KeepSlot, 0] != player.RadarX ||
                        map.keep_locations[player.KeepSlot, 1] != player.RadarY)
                        throw new InvalidDataException("Keep-slot coordinates differ for player " + player.Id);
                }
                var aivs = new Dictionary<int, CustomisationFileManager.CustomAIV>();
                foreach (var player in preset.Players.Where(player => !player.Human))
                {
                    var candidates = CustomisationFileManager.Instance.getLordAIVList(player.LordType);
                    var selection = candidates.Where(aiv => aiv.builtIn &&
                        aiv.checksum == (ulong)player.AivDefault &&
                        aiv.lordType == player.LordType).ToList();
                    if (selection.Count != 1 || selection[0].data == null)
                        throw new InvalidDataException("Built-in Default " + player.AivDefault +
                            " unavailable for lord " + player.LordType);
                    aivs.Add(player.Id, selection[0]);
                }
                pending = preset;
                pendingMap = map;
                pendingAivs = aivs;
                log.LogInfo("Validated lobby preset: " + preset.MapFileName +
                    ", players=" + preset.Players.Count + ", mapSha256=" + preset.MapSha256);
                return true;
            }
            catch (Exception exception)
            {
                log.LogError("Preset validation failed; no lobby changes: " + exception);
                return false;
            }
        }

        private static void Apply(FRONT_Multiplayer view)
        {
            log.LogInfo("Preset Apply entered; map=" + pendingMap?.filePath +
                ", players=" + pending?.Players.Count);
            Noesis.ListView mapList = LobbyField<Noesis.ListView>(view, "RefFileLists");
            var rows = mapList?.ItemsSource as IEnumerable;
            if (rows == null)
                throw new InvalidOperationException("Vanilla map list is not ready.");
            FileRow targetRow = null;
            foreach (object item in rows)
            {
                if (item is FileRow row && row.fileHeader != null &&
                    string.Equals(row.fileHeader.filePath, pendingMap.filePath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    targetRow = row;
                    break;
                }
            }
            if (targetRow == null)
                throw new InvalidOperationException("The validated map is not visible in the lobby list.");
            mapList.SelectedItem = targetRow;
            if (!string.Equals(LobbyField<FileHeader>(view, "selectedMPHeader")?.filePath,
                    pendingMap.filePath,
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Vanilla did not select the requested map.");

            // Vanilla's skirmish remove path uses the same lobby operation. Refresh
            // mappings after each removal before adding players in the requested order.
            for (int id = 8; id >= 2; id--)
            {
                var member = view.currentLobby.GetLobbyMemberFromThis_PlayerID(id);
                if (member == null)
                    continue;
                if (member.SkirmishHumanMember)
                    throw new InvalidOperationException("Unexpected human opponent; preset aborted.");
                Platform_Multiplayer.Instance.kickSkirmishPlayer(member.id.m_SteamID);
                view.currentLobby.validateTeams();
                InvokeLobbyMethod(view, "updateSteamIDMappings", Type.EmptyTypes);
            }
            InvokeLobbyMethod(view, "ReSortTeamInfo", Type.EmptyTypes);
            foreach (var player in pending.Players.Where(player => !player.Human))
            {
                int before = view.currentLobby.members.Count;
                view.SkirmishAIAddClick(player.LordType.ToString());
                if (view.currentLobby.members.Count != before + 1)
                    throw new InvalidOperationException("Vanilla did not add player " + player.Id);
                var member = view.currentLobby.GetLobbyMemberFromThis_PlayerID(player.Id);
                if (member == null || member.SkirmishHumanMember || member.GetLordType() != player.LordType)
                    throw new InvalidOperationException("Added player ID or lord differs from the preset.");
                var selection = view.AIVs[player.Id - 1];
                selection.Init(player.LordType, string.Empty);
                selection.builtIn = false;
                selection.rotation = 0;
                selection.aivs.Add(pendingAivs[player.Id]);
            }
            if (view.currentLobby.members.Count != pending.Players.Count)
                throw new InvalidOperationException("Unexpected lobby member count after AI addition.");
            EngineInterface.MultiplayerSetupData setup =
                LobbyField<EngineInterface.MultiplayerSetupData>(view, "MPsetupData");
            for (int slot = 0; slot < 8; slot++)
                setup.start_keep_location_order[slot] = -10;
            foreach (var player in pending.Players)
                setup.start_keep_location_order[player.KeepSlot] = player.Id - 1;
            InvokeLobbyMethod(view, "UpdateHostInfo", new[] { typeof(bool) }, false);
            InvokeLobbyMethod(view, "UpdateRadarShieldPositions", Type.EmptyTypes);
            foreach (var player in pending.Players)
            {
                if (setup.start_keep_location_order[player.KeepSlot] != player.Id - 1)
                    throw new InvalidOperationException("Keep-slot validation failed for player " + player.Id);
                log.LogInfo("Preset player=" + player.Id +
                    " lord=" + (player.Human ? "human" : player.LordType.ToString()) +
                    " keepSlot=" + player.KeepSlot + " keep=" + player.KeepX + "," + player.KeepY +
                    (player.Human ? "" : " AIV=Default " + player.AivDefault + " rotation=auto" +
                        " aivDataSha256=" + AivHash(pendingAivs[player.Id])));
            }
            log.LogInfo("Lobby preset applied; Completed Castles remains user-controlled.");
        }

        private static string AivHash(CustomisationFileManager.CustomAIV aiv)
        {
            var bytes = new byte[aiv.data.Length * sizeof(short)];
            Buffer.BlockCopy(aiv.data, 0, bytes, 0, bytes.Length);
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
        }
    }
}
