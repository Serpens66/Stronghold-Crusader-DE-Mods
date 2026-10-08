using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace APIShared
{
    /// <summary>Immutable process-wide observation of the current multiplayer lobby.</summary>
    public sealed class LobbyStateSnapshot
    {
        /// <summary>Creates an immutable lobby-state snapshot.</summary>
        public LobbyStateSnapshot(
            ulong? lobbyId,
            IReadOnlyDictionary<int, ulong> players,
            bool hasUnresolvedPlayers,
            int localPlayerId,
            bool preserveForMapTransition,
            string error,
            string diagnostic)
        {
            LobbyId = lobbyId;
            var copy = new Dictionary<int, ulong>();
            if (players != null)
            {
                foreach (KeyValuePair<int, ulong> player in players)
                    copy[player.Key] = player.Value;
            }
            Players = new ReadOnlyDictionary<int, ulong>(copy);
            HasUnresolvedPlayers = hasUnresolvedPlayers;
            LocalPlayerId = localPlayerId;
            PreserveForMapTransition = preserveForMapTransition;
            Error = error ?? string.Empty;
            Diagnostic = diagnostic ?? string.Empty;
        }

        /// <summary>Steam lobby identifier, or null when no lobby is active.</summary>
        public ulong? LobbyId { get; }
        /// <summary>Resolved one-based player slots mapped to Steam identities.</summary>
        public IReadOnlyDictionary<int, ulong> Players { get; }
        /// <summary>Whether at least one required lobby identity is not resolved yet.</summary>
        public bool HasUnresolvedPlayers { get; }
        /// <summary>Resolved one-based local player slot, or zero while unresolved.</summary>
        public int LocalPlayerId { get; }
        /// <summary>Whether a missing lobby must be preserved for an ongoing map transition.</summary>
        public bool PreserveForMapTransition { get; }
        /// <summary>Observation failure that consumers must treat as not ready.</summary>
        public string Error { get; }
        /// <summary>Non-fatal identity-source diagnostic.</summary>
        public string Diagnostic { get; }
    }

    /// <summary>Owner-bound observer for the single process-wide lobby-state source.</summary>
    public interface ILobbyStateCapability
    {
        /// <summary>Registers one process-lifetime observer under an owner-local stable ID.</summary>
        bool TryRegisterObserver(
            string registrationId,
            Action<LobbyStateSnapshot> observer,
            out NativeCapabilityDiagnostic diagnostic);
    }
}
