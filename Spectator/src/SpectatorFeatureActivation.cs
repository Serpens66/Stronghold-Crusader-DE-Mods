namespace Spectator
{
    internal sealed class SpectatorFeatureActivation
    {
        internal SpectatorFeatureActivation(bool host, bool client, bool perspective,
            bool statistics, bool defeat, bool tools, bool singleplayerTest = false)
        {
            Perspective = host && perspective;
            Statistics = host && statistics;
            Defeat = host && defeat;
            TestTools = client && tools;
            SingleplayerTest = client && singleplayerTest;
        }

        internal bool Perspective { get; }
        internal bool Statistics { get; }
        internal bool Defeat { get; }
        internal bool TestTools { get; }
        internal bool SingleplayerTest { get; }
        // Information rights are independent from temporarily unfrozen placement controls.
        internal static bool ForceArmyReport(bool vanillaFrozen, bool originalSpectator, bool perspectiveAllowed) =>
            originalSpectator ? !perspectiveAllowed : vanillaFrozen;
        internal static bool HasToolbar(bool tools, bool perspective, bool statistics, bool editor = false) =>
            !editor && (tools || perspective || statistics);
    }
}
