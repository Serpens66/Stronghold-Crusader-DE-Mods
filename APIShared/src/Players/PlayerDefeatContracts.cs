using System;

namespace APIShared
{
    /// <summary>Immutable notification that a previously confirmed living player lord disappeared or died.</summary>
    public sealed class PlayerLordDeathNotification
    {
        /// <summary>Creates a lord-death notification.</summary>
        public PlayerLordDeathNotification(
            long sessionId,
            int playerId,
            int lordUnitId,
            int lordGlobalId,
            int simulationTick)
        {
            SessionId = sessionId;
            PlayerId = playerId;
            LordUnitId = lordUnitId;
            LordGlobalId = lordGlobalId;
            SimulationTick = simulationTick;
        }

        /// <summary>Gets the mission-lifecycle session identifier.</summary>
        public long SessionId { get; }
        /// <summary>Gets the one-based player identifier.</summary>
        public int PlayerId { get; }
        /// <summary>Gets the one-based unit identifier of the last confirmed living lord.</summary>
        public int LordUnitId { get; }
        /// <summary>Gets the stable global identity of the last confirmed living lord.</summary>
        public int LordGlobalId { get; }
        /// <summary>Gets the simulation tick on which the transition was observed.</summary>
        public int SimulationTick { get; }
    }

    /// <summary>Immutable notification that a player entered Vanilla's official loss state.</summary>
    public sealed class PlayerDefeatNotification
    {
        /// <summary>Creates an official player-defeat notification.</summary>
        public PlayerDefeatNotification(long sessionId, int playerId, int simulationTick)
        {
            SessionId = sessionId;
            PlayerId = playerId;
            SimulationTick = simulationTick;
        }

        /// <summary>Gets the mission-lifecycle session identifier.</summary>
        public long SessionId { get; }
        /// <summary>Gets the one-based player identifier.</summary>
        public int PlayerId { get; }
        /// <summary>Gets the simulation tick on which the transition was observed.</summary>
        public int SimulationTick { get; }
    }

    /// <summary>Owner-bound observer for process-wide player defeat transitions.</summary>
    public interface IPlayerDefeatCapability
    {
        /// <summary>
        /// Registers one process-lifetime observer under an owner-local stable ID. Either callback
        /// may be null, but at least one callback is required. Existing state is never replayed.
        /// </summary>
        bool TryRegisterObserver(
            string registrationId,
            Action<PlayerLordDeathNotification> onLordDied,
            Action<PlayerDefeatNotification> onDefeated,
            out NativeCapabilityDiagnostic diagnostic);
    }
}
