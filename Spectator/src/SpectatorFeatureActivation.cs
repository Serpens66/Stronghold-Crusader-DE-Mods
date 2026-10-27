namespace Spectator
{
    internal sealed class SpectatorFeatureActivation
    {
        internal SpectatorFeatureActivation(bool host, bool client, bool perspective,
            bool statistics, bool defeat, bool tools)
        {
            Perspective = host && perspective;
            Statistics = host && statistics;
            Defeat = host && defeat;
            TestTools = client && tools;
        }

        internal bool Perspective { get; }
        internal bool Statistics { get; }
        internal bool Defeat { get; }
        internal bool TestTools { get; }
        // Information rights are independent from temporarily unfrozen placement controls.
        internal static bool ForceArmyReport(bool vanillaFrozen, bool originalSpectator, bool perspectiveAllowed) =>
            originalSpectator ? !perspectiveAllowed : vanillaFrozen;
        internal static bool HasToolbar(bool tools, bool perspective, bool statistics) =>
            tools || perspective || statistics;
    }
}
