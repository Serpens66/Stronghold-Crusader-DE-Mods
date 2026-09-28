using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using SHCDESE.API;
using SHCDESE.GameGlobals;
using SHCDESE.Interop.Enums;
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace APIShared
{
    /// <summary>Separates the locally controlled player from Vanilla's native report viewpoint.</summary>
    public static class PlayerPerspectiveAPI
    {
        private delegate int LocalPlayerIdCall(GamePlayerManagerAPI self);

        private static Hook localPlayerIdHook;
        private static LocalPlayerIdCall originalLocalPlayerId;
        private static int selectedSpectatorPlayerId;

        internal static void Initialize(ManualLogSource log)
        {
            if (localPlayerIdHook != null) return;
            Hook candidate = null;
            try
            {
                MethodInfo method = typeof(GamePlayerManagerAPI).GetMethod(
                    nameof(GamePlayerManagerAPI.GetLocalPlayerId),
                    BindingFlags.Public | BindingFlags.Instance,
                    null, Type.EmptyTypes, null);
                if (method == null || method.ReturnType != typeof(int))
                    throw new MissingMethodException("Installed GamePlayerManagerAPI.GetLocalPlayerId signature changed.");

                candidate = new Hook(method, (LocalPlayerIdCall)OnGetLocalPlayerId,
                    new HookConfig { ManualApply = true, ID = "APIShared.PlayerPerspective.LocalIdentity" });
                LocalPlayerIdCall original = candidate.GenerateTrampoline<LocalPlayerIdCall>();
                originalLocalPlayerId = original;
                candidate.Apply();
                localPlayerIdHook = candidate;
            }
            catch (Exception error)
            {
                // A failed, unpublished candidate is the only hook we ever undo.
                try { candidate?.Undo(); candidate?.Dispose(); } catch { }
                log.LogError("PLAYER_PERSPECTIVE_HOOK_FAILED: " + error);
            }
        }

        private static int OnGetLocalPlayerId(GamePlayerManagerAPI self)
        {
            // Session changes publish zero before the next map starts. The hot hook never
            // touches Unity state and leaves every inactive call to the original method.
            if (Volatile.Read(ref selectedSpectatorPlayerId) != 0) return -1;
            return originalLocalPlayerId(self);
        }

        internal static bool ShouldOverrideLocalPlayerId(
            int selectedPlayerId, int gameType, int spectatorMode, int controlledPlayerId) =>
            selectedPlayerId >= 1 && selectedPlayerId <= 8 &&
            gameType == (int)eGameTypeModes.GAMETYPE_MULTIPLAYER &&
            spectatorMode != 0 && controlledPlayerId <= 0;

        /// <summary>Gets the player actually controlled by this client, or -1 for a spectator.</summary>
        public static int GetControlledPlayerId()
        {
            if (GameData.Instance?.lastGameState == null || EditorDirector.instance == null) return -1;
            int playerId = EditorDirector.instance.ActivePlayerID;
            return playerId >= 1 && playerId <= 8 ? playerId : -1;
        }

        /// <summary>Gets the selected report viewpoint; outside spectator mode this is the native view.</summary>
        public static int GetViewedPlayerId()
        {
            int selected = Volatile.Read(ref selectedSpectatorPlayerId);
            if (selected != 0) return selected;
            return GetRawNativeViewPlayerId();
        }

        /// <summary>Reads the native index without applying the spectator identity correction.</summary>
        public static int GetRawNativeViewPlayerId()
        {
            try
            {
                ulong address = GameGlobalsManager.Instance.LocalPlayerIdVA;
                return address == 0 ? -1 : Marshal.ReadInt32(new IntPtr(unchecked((long)address)));
            }
            catch { return -1; }
        }

        /// <summary>Changes Vanilla's view and publishes it for an original spectator only.</summary>
        public static bool TrySetSpectatorView(int playerId)
        {
            if (localPlayerIdHook == null || playerId < 1 || playerId > 8) return false;
            EngineInterface.PlayState state = GameData.Instance?.lastGameState;
            EditorDirector director = EditorDirector.instance;
            if (state == null || director == null || !ShouldOverrideLocalPlayerId(
                    playerId, state.game_type, state.spectatorMode, director.ActivePlayerID) ||
                !state.is_human_or_skirmish_player(playerId))
                return false;

            EngineInterface.SetEditorPlayer(playerId);
            if (GetRawNativeViewPlayerId() != playerId) return false;
            Volatile.Write(ref selectedSpectatorPlayerId, playerId);
            return true;
        }

        /// <summary>Clears the identity override when the spectator session ends.</summary>
        public static void ClearSpectatorView() => Volatile.Write(ref selectedSpectatorPlayerId, 0);
    }
}
