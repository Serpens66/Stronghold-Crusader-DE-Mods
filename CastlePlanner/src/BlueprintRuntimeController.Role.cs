using APIShared;

namespace CastlePlanner
{
    internal sealed partial class BlueprintRuntimeController
    {
        private readonly BlueprintSpectatorRole observerRole = new BlueprintSpectatorRole();
        private volatile IPlayerDefeatStateCapability defeatStates;

        private void InitializeBlueprintRole()
        {
            ApiShared.ForMod(CastlePlannerPlugin.PluginGuid).WhenReady(client =>
            {
                if (!client.TryGetPlayerDefeat(out var defeats, out var diagnostic) ||
                    !(defeats is IPlayerDefeatStateCapability states))
                {
                    Shared.DebugLogHelper.LogErrorLimited(log, "BlueprintDefeatState",
                        "Blueprint participant-loss observation unavailable: " + diagnostic?.Reason);
                    return;
                }
                defeatStates = states;
                if (!states.TryRegisterStateObserver("blueprint-observer-role", OnBlueprintRoleChanged, out diagnostic))
                    Shared.DebugLogHelper.LogErrorLimited(log, "BlueprintDefeatState", "Blueprint participant-loss observation failed: " + diagnostic?.Reason);
                observerRole.Invalidate();
                RequestFrame();
            });
        }

        private void OnBlueprintRoleChanged(PlayerDefeatSnapshot snapshot)
        {
            // The publisher may run on a simulation/lifecycle thread. Never apply
            // its queued snapshot to UI; the existing frame rechecks the live session.
            observerRole.Invalidate();
            RequestFrame();
        }

        private void RefreshBlueprintRoleEvidence()
        {
            if (!mapActive || editorSessionActive || observerRole.SessionId <= 0 || observerRole.HasConfirmedLoss || defeatStates == null) return;
            // GetLocalPlayerId can follow a foreign view/input actor. This API keeps
            // the real positive participant identity, including after elimination.
            int actual = PlayerPerspectiveAPI.GetControlledPlayerId();
            if (actual > 0 && defeatStates.TryGetObservedState(observerRole.SessionId, actual, out var snapshot))
                observerRole.Observe(snapshot.SessionId, snapshot.PlayerId, actual, snapshot.Readable, snapshot.IsEliminated);
        }

        private bool CanUseManualBlueprintControls()
        {
            // A retained command can run before the requested presentation frame.
            // This is a managed snapshot lookup at input time, never a native poll.
            RefreshBlueprintRoleEvidence();
            return !HasSpectatorIdentity;
        }
    }
}
