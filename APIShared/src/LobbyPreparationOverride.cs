using CrusaderDE;
using System;

namespace APIShared
{
    /// <summary>Process-lifetime, single-owner preparation of a newly opened skirmish lobby.</summary>
    public static class LobbyPreparationOverride
    {
        private static string owner;
        private static Func<FRONT_Multiplayer, bool> prepare;
        private static Action<FRONT_Multiplayer> apply;
        private static bool active;

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

        /// <summary>Reads and validates the preset once before Vanilla opens a lobby.</summary>
        public static bool Begin(FRONT_Multiplayer lobby)
        {
            active = false;
            if (prepare == null || lobby == null)
                return false;
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
            if (!active || apply == null)
                return;
            try { apply(lobby); }
            catch (Exception exception)
            {
                active = false;
                UnityEngine.Debug.LogError("Lobby preparation " + owner + " failed: " + exception);
            }
        }
    }
}
