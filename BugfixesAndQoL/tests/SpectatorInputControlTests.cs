using System;

namespace BugfixesAndQoL
{
    // A spectator who controls a player's input must get that player's Vanilla troop HUD and Lord
    // controls; an ordinary spectator keeps the foreign HUD and no Lord controls.
    internal static class SpectatorInputControlTests
    {
        internal static void Run(Action<bool, string> check)
        {
            check(!SpectatorInputControlPolicy.IsObservingSpectator(0, 0),
                "Spectator input control: a normal player is not an observing spectator");
            check(SpectatorInputControlPolicy.IsObservingSpectator(1, 0) &&
                    SpectatorInputControlPolicy.IsObservingSpectator(1, -1) &&
                    SpectatorInputControlPolicy.IsObservingSpectator(1, 9),
                "Spectator input control: a spectator without a valid controlled player keeps observing");
            check(!SpectatorInputControlPolicy.IsObservingSpectator(1, 1) &&
                    !SpectatorInputControlPolicy.IsObservingSpectator(1, 8),
                "Spectator input control: a spectator controlling a player acts as that player");

            check(SpectatorInputControlPolicy.ResolveOwnPlayerId(0, 0, 3) == 3 &&
                    SpectatorInputControlPolicy.ResolveOwnPlayerId(0, 0, -1) == 0 &&
                    SpectatorInputControlPolicy.ResolveOwnPlayerId(0, 5, 3) == 3,
                "Spectator input control: outside spectator mode the ordinary player owns the HUD");
            check(SpectatorInputControlPolicy.ResolveOwnPlayerId(1, 0, 3) == 0 &&
                    SpectatorInputControlPolicy.ResolveOwnPlayerId(1, 9, 3) == 0,
                "Spectator input control: an observing spectator has no own player, so every unit is foreign");
            check(SpectatorInputControlPolicy.ResolveOwnPlayerId(1, 4, -1) == 4,
                "Spectator input control: the controlled player owns the HUD although EditorDirector reports no player");

            var lord = new SurrenderLordSnapshot(4, 77, 1077, 4, true);
            bool controlledSpectator = SpectatorInputControlPolicy.IsObservingSpectator(1, 4);
            bool observingSpectator = SpectatorInputControlPolicy.IsObservingSpectator(1, 0);
            check(LordUnitControlsPolicy.CanActivate(true, true, true, false, controlledSpectator, 1, 77, 4, lord),
                "Spectator input control: Lord controls work for the controlled player's Lord");
            check(!LordUnitControlsPolicy.CanActivate(true, true, true, false, observingSpectator, 1, 77, 4, lord),
                "Spectator input control: an observing spectator still gets no Lord controls");
        }
    }
}
