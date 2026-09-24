using CrusaderDE;
using System;
using System.Reflection;

namespace APIShared
{
    /// <summary>Process-lifetime, single-owner preparation of a newly opened skirmish lobby.</summary>
    public static class LobbyPreparationOverride
    {
        private static string owner;
        private static Func<FRONT_Multiplayer, bool> prepare;
        private static Action<FRONT_Multiplayer> apply;
        private static Platform_Multiplayer.MPLobby currentLobby;
        private static bool prepared;
        private static bool applyAttempted;
        private static bool active;
        private static readonly FieldInfo selectedMapField = typeof(FRONT_Multiplayer).GetField(
            "selectedMPHeader", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo mapListField = typeof(FRONT_Multiplayer).GetField(
            "RefFileLists", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>Registers rooted callbacks before the lobby is opened.</summary>
        public static void Register(string ownerId, Func<FRONT_Multiplayer, bool> prepareCallback,
            Action<FRONT_Multiplayer> applyCallback)
        {
            if (string.IsNullOrWhiteSpace(ownerId) || prepareCallback == null || applyCallback == null)
                throw new ArgumentException("A lobby preparation owner and both callbacks are required.");
            if (owner != null && owner != ownerId)
                throw new InvalidOperationException("Lobby preparation is already owned by " + owner + ".");
            owner = ownerId;
            prepare = prepareCallback;
            apply = applyCallback;
        }

        /// <summary>Observes the ready lobby from a process-lifetime frontend callback.</summary>
        public static void Tick(FRONT_Multiplayer view)
        {
            // FRONT_Multiplayer inherits Noesis.BaseComponent, whose == null checks
            // the native handle rather than the managed reference.
            if (ReferenceEquals(view, null) || !FRONT_Multiplayer.skirmishGame ||
                !MainViewModel.viewModelLoaded ||
                MainViewModel.Instance?.Show_MPGameCreation != true)
            {
                if (view?.currentLobby == null)
                    ResetLobby(null);
                return;
            }

            Begin(view);
            Apply(view);
        }

        private static void ResetLobby(Platform_Multiplayer.MPLobby lobby)
        {
            currentLobby = lobby;
            prepared = false;
            applyAttempted = false;
            active = false;
        }

        /// <summary>Reads and validates the preset once for each newly opened lobby.</summary>
        public static bool Begin(FRONT_Multiplayer lobby)
        {
            Platform_Multiplayer.MPLobby session = lobby?.currentLobby;
            if (!ReferenceEquals(currentLobby, session))
                ResetLobby(session);
            if (prepare == null || session == null)
                return false;
            if (prepared)
                return active;

            // A setup callback can run before Vanilla has finished opening the panel.
            // A rejected early preparation gets one more chance on the ready panel.
            prepared = lobby.panelActive;
            try { active = prepare(lobby); }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogError("Lobby preparation " + owner + " rejected: " + exception);
            }
            return active;
        }

        /// <summary>Whether an owner has accepted the current lobby opening.</summary>
        public static bool IsActive => active;

        /// <summary>Applies the prepared preset after Vanilla has opened its controls.</summary>
        public static void Apply(FRONT_Multiplayer lobby)
        {
            if (!active || apply == null || applyAttempted || ReferenceEquals(lobby, null) ||
                !ReferenceEquals(currentLobby, lobby.currentLobby) ||
                !lobby.panelActive)
                return;
            try
            {
                if (selectedMapField == null || mapListField == null)
                    throw new MissingFieldException("FRONT_Multiplayer lobby map fields are unavailable.");
                if (selectedMapField.GetValue(lobby) == null ||
                    (mapListField.GetValue(lobby) as Noesis.ListView)?.ItemsSource == null)
                    return;
                // Applying a map can synchronously invoke other lobby hooks.
                applyAttempted = true;
                apply(lobby);
            }
            catch (Exception exception)
            {
                applyAttempted = true;
                // Keep the owner's memory suppression active: Apply may already have
                // changed part of the lobby, so saving it as the user's preference is unsafe.
                UnityEngine.Debug.LogError("Lobby preparation " + owner + " incomplete: " + exception);
            }
        }
    }
}
