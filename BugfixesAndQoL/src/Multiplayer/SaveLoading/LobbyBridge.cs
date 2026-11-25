using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using CrusaderDE;
using SHCDESE.API;
using Steamworks;

namespace BugfixesAndQoL.Multiplayer.SaveLoading
{
    internal enum LocalReadyResult { NotCurrent, Waiting, Ready, Rejected }

    // Calls the same Vanilla frontend/transport primitives as the ordinary lobby return.
    // Save eligibility and transport remain Vanilla-owned; no original-map or Lord rules.
    internal static class LobbyBridge
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        // This is the audited CreateLobby UI protocol value, not ELobbyType's numeric value.
        private const int VanillaPrivateLobbyMode = 4;
        private static FieldInfo chat, pendingExit, loading, ready, locked, setup, selectedHeader, startTime, mapValid, closing;
        private static MethodInfo raw;
        private static Action<Platform_Multiplayer, string, int> sendSaveCrc;
        internal static MethodInfo CreationResultMethod { get; private set; }

        internal static void Validate()
        {
            chat = Field(typeof(Platform_Multiplayer), "LobbyChatDelegate", typeof(Action<string, string, int>));
            pendingExit = Field(typeof(EditorDirector), "pendingMPExit", typeof(bool));
            loading = Field(typeof(FRONT_Multiplayer), "MPGameLoading", typeof(bool));
            ready = Field(typeof(FRONT_Multiplayer), "MPLocalReady", typeof(bool));
            locked = Field(typeof(FRONT_Multiplayer), "MPLocalReadyLocked", typeof(bool));
            mapValid = Field(typeof(FRONT_Multiplayer), "MPMapValid", typeof(bool));
            closing = Field(typeof(FRONT_Multiplayer), "closePanelDisplayed", typeof(bool));
            setup = Field(typeof(FRONT_Multiplayer), "MPsetupData", typeof(EngineInterface.MultiplayerSetupData));
            selectedHeader = Field(typeof(FRONT_Multiplayer), "selectedMPHeader", typeof(FileHeader));
            startTime = Field(typeof(FRONT_Multiplayer), "startGameTime", typeof(DateTime));
            raw = typeof(GameNetworkAPI).GetMethod("HandleRawPacket", Instance, null,
                new[] { typeof(short), typeof(byte[]), typeof(CSteamID?) }, null);
            if (raw == null || raw.ReturnType != typeof(bool)) throw new MissingMethodException("GameNetworkAPI.HandleRawPacket contract");
            var saveCrc = typeof(Platform_Multiplayer).GetMethod("SendSaveCRC", Instance, null,
                new[] { typeof(string), typeof(int) }, null);
            if (saveCrc == null || saveCrc.IsStatic || saveCrc.ReturnType != typeof(void))
                throw new MissingMethodException("Platform_Multiplayer.SendSaveCRC contract");
            sendSaveCrc = (Action<Platform_Multiplayer, string, int>)Delegate.CreateDelegate(
                typeof(Action<Platform_Multiplayer, string, int>), saveCrc);
            foreach (string method in new[] { "updateSteamIDMappings", "UpdateRadarShieldPositions", "ShowSetupScreen" })
                FrontMethod(method, Type.EmptyTypes);
            FrontMethod("UpdateHostInfo", new[] { typeof(bool) });
            FrontMethod("updateRadarTexture", new[] { typeof(FileHeader) });
            var button = FrontMethod(nameof(FRONT_Multiplayer.ButtonClicked), new[] { typeof(string) });
            if (!button.IsPublic || button.IsStatic || button.ReturnType != typeof(void))
                throw new MissingMethodException("Public FRONT_Multiplayer.ButtonClicked contract");
            foreach (string propertyName in new[] { "Show_HUD_Confirmation", "Show_HUD_ConfirmationMP",
                "Show_HUD_ConfirmationSands", "Show_HUD_ConfirmationSM", "Show_HUD_LoadSaveRequester",
                "Show_HUD_LoadSaveRequesterMP", "Show_HUD_LoadSaveRequesterTrails" })
            {
                var property = typeof(MainViewModel).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                if (property?.PropertyType != typeof(bool) || property.GetGetMethod() == null || property.GetGetMethod().IsStatic)
                    throw new MissingMemberException("Public MainViewModel." + propertyName + " modal visibility contract");
            }
            CreationResultMethod = typeof(Platform_Multiplayer).GetMethod("CreateLobbyResult", Instance, null,
                new[] { typeof(LobbyCreated_t), typeof(bool) }, null);
            if (CreationResultMethod == null || CreationResultMethod.ReturnType != typeof(void))
                throw new MissingMethodException("Platform_Multiplayer.CreateLobbyResult contract");
            var active = typeof(Platform_Multiplayer).GetProperty("MPGameActive", BindingFlags.Public | BindingFlags.Static);
            if (active?.PropertyType != typeof(bool) || active.GetSetMethod() == null || !active.GetSetMethod().IsStatic)
                throw new MissingMemberException("Public multiplayer detachment contract changed.");
        }

        private static FieldInfo Field(Type type, string name, Type expected)
        {
            var field = type.GetField(name, Instance);
            if (field == null || field.IsStatic || field.FieldType != expected) throw new MissingFieldException(type.FullName, name);
            return field;
        }
        private static MethodInfo FrontMethod(string name, Type[] arguments) => typeof(FRONT_Multiplayer)
            .GetMethod(name, Instance, null, arguments, null) ?? throw new MissingMethodException("FRONT_Multiplayer", name);
        private static void Invoke(FRONT_Multiplayer front, string name, Type[] arguments, params object[] values) =>
            FrontMethod(name, arguments).Invoke(front, values);
        private static FRONT_Multiplayer Front()
        {
            if (!MainViewModel.viewModelLoaded) throw new InvalidOperationException("The multiplayer frontend is unavailable.");
            return MainViewModel.Instance.FRONTMultiplayer ?? throw new InvalidOperationException("The multiplayer frontend is unavailable.");
        }
        private static void RequireParked()
        {
            if (!LoadBarrier.Quiescent) throw new InvalidOperationException("Lobby transition requires the drained worker barrier.");
        }

        internal static Action<string, string, int> CaptureChat(Platform_Multiplayer multiplayer) =>
            (Action<string, string, int>)chat.GetValue(multiplayer ?? throw new ArgumentNullException(nameof(multiplayer)))
                ?? throw new InvalidOperationException("The multiplayer chat callback is unavailable.");

        internal static void ClearOldMessages(Platform_Multiplayer multiplayer)
        {
            RequireParked();
            if (multiplayer == null) throw new ArgumentNullException(nameof(multiplayer));
            // Runtime calls only after every peer has consumed every old-stream fence.
            multiplayer.clearMPMessages();
        }

        internal static void CreatePrivateLobby(string name, int humanCount, Action created, Action<string, string, int> callback)
        {
            RequireParked();
            if (Director.instance.MultiplayerGame || Director.instance.SimRunning)
                throw new InvalidOperationException("Retire the old mission before creating its loading lobby.");
            if (created == null || callback == null) throw new ArgumentNullException(created == null ? nameof(created) : nameof(callback));
            // doOpen(fromNew:false) deliberately does not run native initialization. Obtain
            // normal fresh lobby defaults after retirement, never from the old live mission.
            var defaults = EngineInterface.initMultiplayerGame(false);
            Platform_Multiplayer.Instance.CreateLobby(name ?? string.Empty, string.Empty, string.Empty,
                humanCount, 0, VanillaPrivateLobbyMode, defaults.ToString(), 0, created, callback, -1,
                clearGameMembers: false);
        }

        private static void Detach(Platform_Multiplayer multiplayer)
        {
            // Do not use initFastFollowOn here: it closes retained reliable-control sessions.
            // The caller retires this handoff only after the all-peer lobby receipt.
            Platform_Multiplayer.MPGameActive = false;
            multiplayer.gameMembers = null;
        }

        internal static void OpenHost(Platform_Multiplayer.MPLobby lobby)
        {
            RequireParked();
            if (lobby == null || !lobby.isHost || lobby.identifier == 0)
                throw new InvalidOperationException("The replacement host lobby is unavailable.");
            // doOpen(fromNew:false) preserves this static flag. The new lobby is generic;
            // retaining a previous Custom-Coop choice would republish type=1/maxPlayers=2.
            FRONT_Multiplayer.customCoopGame = false;
            var multiplayer = Platform_Multiplayer.Instance;
            var view = MainViewModel.Instance;
            view.Show_HUD_MissionOver = false;
            FatControler.instance.NewScene(Enums.SceneIDS.FrontEnd);
            var front = Front();
            Detach(multiplayer);
            var defaults = new EngineInterface.MultiplayerSetupData();
            defaults.FromString(lobby.settings);
            setup.SetValue(front, defaults);
            // This is a fresh generic loading lobby; no previous AI/Coop choices are restored.
            front.AIVs = new FRONT_Multiplayer.MPAIVInfo[8];
            for (int index = 0; index < front.AIVs.Length; index++) front.AIVs[index] = new FRONT_Multiplayer.MPAIVInfo();
            view.Show_CoopHostInvitePane = false;
            view.Show_CoopHostJoinedPane = false;
            view.Show_CoopClientPane = false;
            view.Show_CoopConnectedChatVisible = false;
            front.currentLobby = lobby;
            multiplayer.SetActiveLobby(lobby);
            front.doOpen(false, false, null, false);
            front.currentLobby = lobby;
            multiplayer.SetActiveLobby(lobby);
            Invoke(front, "updateSteamIDMappings", Type.EmptyTypes);
            Invoke(front, "UpdateRadarShieldPositions", Type.EmptyTypes);
            Invoke(front, "UpdateHostInfo", new[] { typeof(bool) }, false);
            Invoke(front, "ShowSetupScreen", Type.EmptyTypes);
            loading.SetValue(front, false);
            ready.SetValue(front, false);
            locked.SetValue(front, false);
            view.Show_FrontMenus_Background_Main = false;
            view.Show_Frontend_MainMenu = false;
        }

        internal static void OpenClient(ulong lobbyId)
        {
            RequireParked();
            if (lobbyId == 0) throw new ArgumentOutOfRangeException(nameof(lobbyId));
            var multiplayer = Platform_Multiplayer.Instance;
            Detach(multiplayer);
            multiplayer.SetInviteLobbyID(lobbyId);
            multiplayer.ResumeInvite();
        }

        internal static bool IsLobbyReady(ulong lobbyId, ulong hostId, ulong[] expectedPeers, ulong self)
        {
            if (!MainViewModel.viewModelLoaded) return false;
            var view = MainViewModel.Instance;
            var front = view.FRONTMultiplayer;
            var lobby = Platform_Multiplayer.Instance?.activeLobby;
            return lobby != null && lobby.identifier == lobbyId && front != null &&
                ReferenceEquals(front.currentLobby, lobby) && view.Show_MultiplayerSetup &&
                lobby.isHost == (self == hostId) &&
                SteamMatchmaking.GetLobbyOwner(lobby.id).m_SteamID == hostId &&
                MatchesHumans(lobby, expectedPeers, self) && !(bool)loading.GetValue(front);
        }

        internal static LocalReadyResult TryReady(ulong lobbyId, ulong hostId, ulong[] expectedPeers, ulong self)
        {
            // The singleton getter constructs UI; never touch it before Vanilla loaded it.
            if (!MainViewModel.viewModelLoaded) return LocalReadyResult.NotCurrent;
            if (!IsLobbyReady(lobbyId, hostId, expectedPeers, self)) return LocalReadyResult.NotCurrent;
            var front = MainViewModel.Instance.FRONTMultiplayer;
            if (Director.instance == null || Director.instance.MultiplayerGame || Director.instance.SimRunning ||
                !string.IsNullOrEmpty(front.currentLobby.startGame) || (bool)closing.GetValue(front))
                return LocalReadyResult.NotCurrent;
            // Ready is a toggle. Never unready a player who already accepted this lobby.
            if ((bool)ready.GetValue(front)) return LocalReadyResult.Ready;
            // A normal launch guard can open a confirmation and replace its callbacks.
            // Never run that autonomous command over an existing confirmation/requester.
            if (HasModal()) return LocalReadyResult.Waiting;
            if (!front.currentLobby.isHost && !(bool)mapValid.GetValue(front)) return LocalReadyResult.Waiting;
            // Keep the real UI entry: APIShared, ExtendedData and other registered guards
            // must see the attempt. A normal return does not prove that Ready was accepted.
            front.ButtonClicked("Ready");
            return (bool)ready.GetValue(front) ? LocalReadyResult.Ready : LocalReadyResult.Rejected;
        }

        internal static bool HasSaveRequester()
        {
            if (!MainViewModel.viewModelLoaded) return false;
            var view = MainViewModel.Instance;
            return view.Show_HUD_LoadSaveRequester || view.Show_HUD_LoadSaveRequesterMP ||
                view.Show_HUD_LoadSaveRequesterTrails;
        }

        internal static bool HasModal()
        {
            if (!MainViewModel.viewModelLoaded) return false;
            var view = MainViewModel.Instance;
            return view.Show_HUD_Confirmation || view.Show_HUD_ConfirmationMP || view.Show_HUD_ConfirmationSands ||
                view.Show_HUD_ConfirmationSM || HasSaveRequester();
        }

        // This verifies the requested group's transport handoff only, not save eligibility.
        internal static bool MatchesHumans(Platform_Multiplayer.MPLobby lobby, ulong[] expectedPeers, ulong self)
        {
            if (lobby?.members == null || expectedPeers == null || expectedPeers.Length == 0 ||
                !expectedPeers.Contains(self) || expectedPeers.Any(peer => peer == 0) ||
                expectedPeers.Distinct().Count() != expectedPeers.Length) return false;
            var current = new HashSet<ulong>();
            foreach (var member in lobby.members)
            {
                if (member == null || member.dummyToBeKicked || member.SkirmishMember || !current.Add(member.id.m_SteamID)) return false;
            }
            return current.SetEquals(expectedPeers);
        }

        internal static void RequestAllSaveChecks(Platform_Multiplayer multiplayer)
        {
            var saves = MapFileManager.Instance.GetMPSaves(0, true, false);
            // Reset once before any send. Calling SendSaveCRCs twice would reset the
            // other category's acknowledgements. Keep Vanilla's wire and reply checks.
            foreach (var save in saves) save.retrieveCRCChecks = 0;
            foreach (var save in saves)
                sendSaveCrc(multiplayer, save.fileName, save.xPlaySaveChecksum ^ save.xPlaySaveTime);
        }

        internal static void OpenLoadDialog()
        {
            var front = Front();
            var multiplayer = Platform_Multiplayer.Instance;
            if (front.currentLobby == null || !front.currentLobby.isHost || !ReferenceEquals(front.currentLobby, multiplayer.activeLobby))
                throw new InvalidOperationException("Only the current loading-lobby host can open its save requester.");
            MainViewModel.Instance.FrontEndMenu.UpdateFrontMenuPopupScale();
            // The normal MP requester lists both kinds; its rows retain Vanilla's
            // peer-response requirement. Only this explicit caller requests both kinds.
            RequestAllSaveChecks(multiplayer);
            HUD_LoadSaveRequester.OpenLoadSaveRequester(Enums.RequesterTypes.LoadMultiplayerGame, (name, header) => {
                multiplayer.HostLoadGame(header.fileName);
                startTime.SetValue(front, DateTime.UtcNow.AddMilliseconds(500));
            }, () => Invoke(front, "updateRadarTexture", new[] { typeof(FileHeader) }, selectedHeader.GetValue(front)),
                front.currentLobby.CountHumanPlayers() - 1);
        }

        internal static byte[] ExtractControl(byte[] bytes, short packetId)
        {
            if (packetId <= 0 || bytes == null || bytes.Length < 7 || bytes.Length > 1024 ||
                BitConverter.ToInt16(bytes, 0) != packetId) return null;
            int length = BitConverter.ToInt32(bytes, 2);
            if (length <= 0 || length != bytes.Length - 6) return null;
            var body = new byte[length];
            Buffer.BlockCopy(bytes, 6, body, 0, length);
            return body;
        }

        internal static void DrainControl(short packetId)
        {
            RequireParked();
            if (Platform_Multiplayer.Instance.activeLobby != null) return;
            var pointers = new IntPtr[64];
            int count = SteamNetworkingMessages.ReceiveMessagesOnChannel(2, pointers, pointers.Length);
            try
            {
                for (int index = 0; index < count; index++)
                {
                    var message = Marshal.PtrToStructure<SteamNetworkingMessage_t>(pointers[index]);
                    if (message.m_cbSize < 7 || message.m_cbSize > 1024) continue;
                    var bytes = new byte[message.m_cbSize];
                    Marshal.Copy(message.m_pData, bytes, 0, bytes.Length);
                    byte[] body = ExtractControl(bytes, packetId);
                    if (body != null) raw.Invoke(GameNetworkAPI.Instance, new object[] {
                        packetId, body, (CSteamID?)message.m_identityPeer.GetSteamID() });
                }
            }
            finally
            {
                for (int index = 0; index < count; index++) SteamNetworkingMessage_t.Release(pointers[index]);
            }
        }

        internal static void RecoverAfterCommitted(ulong[] peers)
        {
            RequireParked();
            var multiplayer = Platform_Multiplayer.Instance;
            Director.instance.EndMultiplayer();
            EditorDirector.instance.stopGameSim();
            // Runtime must finish/defer an outstanding CreateLobby callback before invoking
            // recovery: Vanilla writes activeLobby before calling its success continuation.
            if (multiplayer.activeLobby != null) multiplayer.LeaveLobby();
            foreach (ulong peer in (peers ?? Array.Empty<ulong>()).Where(peer => peer > 1000).Distinct())
            {
                var identity = default(SteamNetworkingIdentity);
                identity.SetSteamID(new CSteamID(peer));
                SteamNetworkingMessages.CloseSessionWithUser(ref identity);
            }
            multiplayer.exitMP();
            pendingExit.SetValue(EditorDirector.instance, false);
            FatControler.instance.NewScene(Enums.SceneIDS.FrontEnd);
        }
    }
}
