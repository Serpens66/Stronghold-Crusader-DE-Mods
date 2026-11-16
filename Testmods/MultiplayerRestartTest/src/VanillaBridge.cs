using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using CrusaderDE;
using SHCDESE.API;
using Steamworks;

namespace MultiplayerRestartTest
{
    // All private game access is validated against the actual loaded assembly. No publicized reference.
    internal static class VanillaBridge
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static FieldInfo chat, pendingExit, ready, saving, resync;
        private static MethodInfo raw;
        internal static void Validate()
        {
            var members = typeof(Platform_Multiplayer).GetField("gameMembers", BindingFlags.Public | BindingFlags.Instance);
            var active = typeof(Platform_Multiplayer).GetProperty("MPGameActive", BindingFlags.Public | BindingFlags.Static);
            if (members == null || members.FieldType != typeof(List<Platform_Multiplayer.MPGameMember>) ||
                active?.PropertyType != typeof(bool) || active.GetSetMethod() == null || !active.GetSetMethod().IsStatic)
                throw new MissingMemberException("Public multiplayer transport-detachment contract changed.");
            chat = Field(typeof(Platform_Multiplayer), "LobbyChatDelegate", typeof(Action<string,string,int>));
            saving = Field(typeof(Platform_Multiplayer), "resyncingOrSaving", typeof(bool));
            resync = Field(typeof(Platform_Multiplayer), "resyncing", typeof(bool));
            pendingExit = Field(typeof(EditorDirector), "pendingMPExit", typeof(bool));
            ready = Field(typeof(FRONT_Multiplayer), "MPLocalReady", typeof(bool));
            raw = typeof(GameNetworkAPI).GetMethod("HandleRawPacket", AnyInstance, null,
                new[] { typeof(short), typeof(byte[]), typeof(CSteamID?) }, null);
            if (raw == null || raw.ReturnType != typeof(bool)) throw new MissingMethodException("GameNetworkAPI.HandleRawPacket contract");
            foreach (string method in new[] { "updateSteamIDMappings", "UpdateRadarShieldPositions", "ShowSetupScreen" })
                FrontMethod(method, Type.EmptyTypes);
            FrontMethod("UpdateHostInfo", new[] { typeof(bool) });
            FrontMethod("populateMapList", new[] { typeof(FileHeader), typeof(bool) });
        }
        private static FieldInfo Field(Type type, string name, Type expected)
        {
            var field = type.GetField(name, AnyInstance);
            if (field == null || field.IsStatic || field.FieldType != expected) throw new MissingFieldException(type.FullName, name);
            return field;
        }
        private static MethodInfo FrontMethod(string name, Type[] arguments) => typeof(FRONT_Multiplayer)
            .GetMethod(name, AnyInstance, null, arguments, null) ?? throw new MissingMethodException("FRONT_Multiplayer", name);
        private static object Invoke(FRONT_Multiplayer front, string name, Type[] types, params object[] args) => FrontMethod(name, types).Invoke(front, args);
        internal static Action<string,string,int> Chat() => (Action<string,string,int>)chat.GetValue(Platform_Multiplayer.Instance)
            ?? throw new InvalidOperationException("Original lobby chat callback is unavailable.");
        internal static bool Busy() => (bool)saving.GetValue(Platform_Multiplayer.Instance) || (bool)resync.GetValue(Platform_Multiplayer.Instance);
        internal static bool IsReady() => (bool)ready.GetValue(MainViewModel.Instance.FRONTMultiplayer);
        internal static void ClearPendingExit()
        {
            // No ExitMP coroutine may have been scheduled: EndMultiplayer preceded stopGameSim.
            if ((bool)pendingExit.GetValue(EditorDirector.instance)) throw new InvalidOperationException("Unexpected delayed multiplayer exit.");
        }
        internal static void DetachTransport()
        {
            if (!StopBarrier.Quiescent) throw new InvalidOperationException("Transport detachment requires the drained stop barrier.");
            DetachTransport(Platform_Multiplayer.Instance);
        }
        internal static void DetachTransport(Platform_Multiplayer multiplayer)
        {
            if (multiplayer == null) throw new ArgumentNullException(nameof(multiplayer));
            // initFastFollowOn also closes every old Steam session. Keep those sessions:
            // submitted reliable handoff messages still need them on both sides.
            // The caller retains peer identities and owns terminal-failure cleanup.
            Platform_Multiplayer.MPGameActive = false;
            multiplayer.gameMembers = null;
        }
        internal static void ClearOldManagedMessages()
        {
            if (!StopBarrier.Quiescent) throw new InvalidOperationException("Old message cleanup requires the drained stop barrier.");
            ClearOldManagedMessages(Platform_Multiplayer.Instance);
        }
        internal static void ClearOldManagedMessages(Platform_Multiplayer multiplayer)
        {
            if (multiplayer == null) throw new ArgumentNullException(nameof(multiplayer));
            // Call only after the protocol has validated every old-stream stop fence.
            // Detaching alone is deliberately not permission to erase queued messages.
            multiplayer.clearMPMessages();
        }
        internal static void CleanupRetainedPeers(ulong[] peers) => CleanupRetainedPeers(peers, peer => {
            var identity = default(SteamNetworkingIdentity);
            identity.SetSteamID(new CSteamID(peer));
            SteamNetworkingMessages.CloseSessionWithUser(ref identity);
        });
        internal static void CleanupRetainedPeers(ulong[] peers, Action<ulong> close)
        {
            if (peers == null) throw new ArgumentNullException(nameof(peers));
            if (close == null) throw new ArgumentNullException(nameof(close));
            // Terminal abandonment only, after the caller has finished its Abort sends.
            // A successful restart transfers these sessions to the new gameMembers instead.
            foreach (ulong peer in peers.Where(value => value > 1000UL).Distinct()) close(peer);
        }
        internal static void RestoreMode(LaunchSnapshot snapshot)
        {
            bool coop = snapshot.CoopTrailId > 0;
            FRONT_Multiplayer.coopGame = coop;
            FRONT_Multiplayer.customCoopGame = snapshot.GameTypeCoop == 1 && !coop;
            if (coop) FrontendMenus.CurrentSelectedTrail = 20 + snapshot.CoopTrailId;
            var front = MainViewModel.Instance.FRONTMultiplayer;
            Field(typeof(FRONT_Multiplayer), "selectedCoopMissionID", typeof(int)).SetValue(front, snapshot.CoopMissionId);
        }
        internal static void OpenHost(LaunchSnapshot snapshot, Platform_Multiplayer.MPLobby lobby)
        {
            if (lobby == null || !lobby.isHost) throw new InvalidOperationException("Replacement host lobby missing.");
            var view = MainViewModel.Instance; var mp = Platform_Multiplayer.Instance;
            view.Show_HUD_MissionOver = false;
            FatControler.instance.NewScene(Enums.SceneIDS.FrontEnd);
            var front = view.FRONTMultiplayer;
            front.currentLobby = lobby; mp.SetActiveLobby(lobby);
            front.doOpen(false, false, null, snapshot.CoopTrailId > 0);
            front.currentLobby = lobby; mp.SetActiveLobby(lobby);
            // Keep the peer transport through both sides' lobby initialization. SE's regular
            // lobby receiver becomes the sole consumer without dropping submitted handoffs.
            DetachTransport(); RestoreMode(snapshot);
            Invoke(front, "updateSteamIDMappings", Type.EmptyTypes);
            Invoke(front, "UpdateRadarShieldPositions", Type.EmptyTypes);
            Invoke(front, "UpdateHostInfo", new[] { typeof(bool) }, false);
            Invoke(front, "ShowSetupScreen", Type.EmptyTypes);
            Invoke(front, "populateMapList", new[] { typeof(FileHeader), typeof(bool) }, snapshot.Map, false);
            Field(typeof(FRONT_Multiplayer), "MPGameLoading", typeof(bool)).SetValue(front, false);
            Field(typeof(FRONT_Multiplayer), "MPLocalReady", typeof(bool)).SetValue(front, false);
            Field(typeof(FRONT_Multiplayer), "MPLocalReadyLocked", typeof(bool)).SetValue(front, false);
            view.Show_FrontMenus_Background_Main = false; view.Show_Frontend_MainMenu = false;
        }
        internal static void RefreshHost()
        {
            var front = MainViewModel.Instance.FRONTMultiplayer;
            Invoke(front, "UpdateRadarShieldPositions", Type.EmptyTypes);
            Invoke(front, "UpdateHostInfo", new[] { typeof(bool) }, false);
        }
        internal static void DrainRestart(short packetId)
        {
            // This narrow bridge runs only after all old readers drained and before any active
            // lobby exists. It never forwards old gameplay/seed/start packets to a new world.
            if (!StopBarrier.Quiescent || Platform_Multiplayer.Instance.activeLobby != null) return;
            var pointers = new IntPtr[64];
            int count = SteamNetworkingMessages.ReceiveMessagesOnChannel(2, pointers, pointers.Length);
            try
            {
                for (int i = 0; i < count; i++)
                {
                    var message = Marshal.PtrToStructure<SteamNetworkingMessage_t>(pointers[i]);
                    if (message.m_cbSize < 6 || message.m_cbSize > 1024) continue;
                    var bytes = new byte[message.m_cbSize]; Marshal.Copy(message.m_pData, bytes, 0, bytes.Length);
                    byte[] payload = ExtractRestart(bytes, packetId);
                    if (payload != null)
                        raw.Invoke(GameNetworkAPI.Instance, new object[] { packetId, payload,
                            (CSteamID?)message.m_identityPeer.GetSteamID() });
                }
            }
            finally { for (int i = 0; i < count; i++) SteamNetworkingMessage_t.Release(pointers[i]); }
        }
        internal static byte[] ExtractRestart(byte[] bytes, short packetId)
        {
            // MPData.FromBytes trusts the declared signed length. Validate its full framing
            // before allocating or forwarding anything from this narrow transitional reader.
            if (bytes == null || bytes.Length < 6 || bytes.Length > 1024 || BitConverter.ToInt16(bytes, 0) != packetId) return null;
            int length = BitConverter.ToInt32(bytes, 2);
            if (length < 1 || length != bytes.Length - 6 || length > 512) return null;
            var payload = new byte[length]; Buffer.BlockCopy(bytes, 6, payload, 0, length); return payload;
        }
    }
}
