namespace Spectator
{
    internal sealed class SpectatorFeatureActivation
    {
        internal SpectatorFeatureActivation(bool host, bool client, bool perspective,
            bool statistics, bool defeat, bool tools, bool singleplayerTest = false, bool overview = true, bool control = false)
        {
            Perspective = client && perspective;
            // Control follows the portrait selection, so it needs the perspective feature.
            Control = Perspective && control;
            Statistics = client && statistics;
            Defeat = host && defeat;
            Overview = client && overview;
            TestTools = client && tools;
            SingleplayerTest = client && singleplayerTest;
        }

        internal bool Overview { get; }
        internal bool Perspective { get; }
        internal bool Control { get; }
        internal bool Statistics { get; }
        internal bool Defeat { get; }
        internal bool TestTools { get; }
        internal bool SingleplayerTest { get; }
        // Information rights are independent from temporarily unfrozen placement controls.
        internal static bool ForceArmyReport(bool vanillaFrozen, bool spectatorObserver, bool perspectiveAllowed) =>
            spectatorObserver ? !perspectiveAllowed : vanillaFrozen;
        // Returns whether the original freeze method should execute. Explicit unfreezing is only
        // allowed inside an authorized placement session; Vanilla's repeated freeze requests wait.
        internal static bool ResolveControlFreeze(bool requested, bool confirmedEliminatedParticipant,
            bool authorizedPlacement, out bool effective)
        {
            effective = requested;
            if (authorizedPlacement) return !requested;
            effective = requested || confirmedEliminatedParticipant;
            return true;
        }
        internal static bool CanOpenReports(bool spectatorObserver, bool viewUncertain) =>
            !spectatorObserver || !viewUncertain;
        internal static bool HasToolbar(bool tools, bool perspective, bool statistics, bool editor = false) =>
            !editor && (tools || perspective || statistics);
    }
}
