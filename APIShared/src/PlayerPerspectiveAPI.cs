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
        private static readonly object viewChangeGate = new object();
        private static ManualLogSource transitionLog;
        private static int identityOverrideActive;
        private static int selectedSpectatorPlayerId;
        private static int transitionViewPlayerId;
        private static int transitionActive;
        private static int viewUncertain;

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
                transitionLog = log;
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
            // The hot hook never touches Unity state. Session teardown clears this flag,
            // and inactive calls retain the original Script Extender behavior.
            if (Volatile.Read(ref identityOverrideActive) != 0) return -1;
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
            if (Volatile.Read(ref transitionActive) != 0)
                return Volatile.Read(ref transitionViewPlayerId);
            if (Volatile.Read(ref viewUncertain) != 0) return -1;
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

            bool changed = TryChangeView(playerId, GetRawNativeViewPlayerId,
                EngineInterface.SetEditorPlayer, out string failure);
            if (!changed) transitionLog?.LogError("PLAYER_PERSPECTIVE_SWITCH_FAILED: " + failure);
            return changed;
        }

        // The injected delegates also let the transaction be tested without a running game.
        internal static bool TryChangeView(int playerId, Func<int> readNativeView,
            Action<int> setNativeView, out string failure)
        {
            failure = null;
            if (playerId < 1 || playerId > 8 || readNativeView == null || setNativeView == null)
            {
                failure = "Invalid view transition request.";
                return false;
            }

            lock (viewChangeGate)
            {
                int previousRaw;
                try { previousRaw = readNativeView(); }
                catch (Exception error)
                {
                    failure = "Could not read the previous native view: " + error;
                    return false;
                }
                if (previousRaw < 0 || previousRaw > 8)
                {
                    failure = "Previous native view cannot be restored: " + previousRaw + ".";
                    return false;
                }

                int previousPublished = Volatile.Read(ref selectedSpectatorPlayerId);
                if (previousPublished != previousRaw) previousPublished = 0;
                int previousOverride = Volatile.Read(ref identityOverrideActive);
                int previousUncertain = Volatile.Read(ref viewUncertain);
                // Publish protection before Vanilla can write its native viewpoint.
                Volatile.Write(ref identityOverrideActive, 1);
                Volatile.Write(ref transitionViewPlayerId, previousUncertain != 0 ? -1 :
                    previousPublished != 0 ? previousPublished : previousRaw);
                Volatile.Write(ref transitionActive, 1);
                Exception changeError = null;
                try
                {
                    setNativeView(playerId);
                    if (readNativeView() == playerId)
                    {
                        Volatile.Write(ref selectedSpectatorPlayerId, playerId);
                        Volatile.Write(ref viewUncertain, 0);
                        Volatile.Write(ref transitionActive, 0);
                        return true;
                    }
                }
                catch (Exception error) { changeError = error; }

                bool restored = false;
                Exception restoreError = null;
                try
                {
                    setNativeView(previousRaw);
                    restored = readNativeView() == previousRaw;
                }
                catch (Exception error) { restoreError = error; }
                Volatile.Write(ref selectedSpectatorPlayerId, restored ? previousPublished : 0);
                Volatile.Write(ref viewUncertain, restored ? previousUncertain : 1);
                if (restored) Volatile.Write(ref identityOverrideActive, previousOverride);
                Volatile.Write(ref transitionActive, 0);
                failure = "Native view " + playerId + " was not confirmed; rollback=" +
                    (restored ? "verified" : "failed") +
                    (changeError == null ? string.Empty : ", changeError=" + changeError) +
                    (restoreError == null ? string.Empty : ", rollbackError=" + restoreError) + ".";
                return false;
            }
        }

        /// <summary>Clears the identity override when the spectator session ends.</summary>
        public static void ClearSpectatorView()
        {
            lock (viewChangeGate)
            {
                Volatile.Write(ref selectedSpectatorPlayerId, 0);
                Volatile.Write(ref viewUncertain, 0);
                Volatile.Write(ref transitionActive, 0);
                Volatile.Write(ref identityOverrideActive, 0);
            }
        }
    }
}
