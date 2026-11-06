using APIShared.Simulation;

namespace ImprovedHunters
{
    /// <summary>
    /// Time base of every Hunter decision: the native map tick published by APIShared. It is the same
    /// on all multiplayer peers, is preserved in saves and stands still while the game is paused.
    /// A new mission can reuse earlier values, so state holding a tick is cleared at mission start.
    /// </summary>
    internal static class HunterSimulationTime
    {
        /// <summary>Map ticks per second at the default game speed; converts durations written in seconds.</summary>
        public const int MapTicksPerSecond = 40;

        private static long lastKnownTick;

        /// <summary>The current map tick; call on the simulation thread.</summary>
        /// <remarks>
        /// Multiplayer simulation starts after the mission start, so the source is available there. The last
        /// value only covers Hunter events of a local game that arrive before the mission counts as started,
        /// when the features using this time are still inactive. Signed wrap would need 621 days of simulation.
        /// </remarks>
        public static long Now
        {
            get
            {
                if (SimulationTickEvents.TryGetCurrentTick(out int mapTick))
                    lastKnownTick = mapTick;
                return lastKnownTick;
            }
        }
    }
}
