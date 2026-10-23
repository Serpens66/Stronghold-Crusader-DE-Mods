using APIShared;
using ExtendedData;
using SHCDESE.API;
using SHCDESE.API.Components.SaveData;

internal static class MissionOriginLifecycleTests
{
    internal static void Run()
    {
        ExtendedDataLaunchOriginApi.Initialize(null);
        var saveApi = ModSaveDataAPI.Instance;
        Action[] origins = {
            () => ExtendedDataLaunchOriginApi.SetCustomizedCustomTrail(90, 3),
            () => ExtendedDataLaunchOriginApi.SetCustomizedCoopTrail(3, 10),
            () => ExtendedDataLaunchOriginApi.SetCustomizedVanillaTrail(1, 1, 0),
            () => ExtendedDataLaunchOriginApi.SetCustomizedSandsOfTime(11, 11, 4)
        };
        foreach (var establish in origins)
        {
            foreach (bool restored in new[] { false, true })
            {
                establish();
                if (restored)
                {
                    byte[] bytes = saveApi.SaveCallback(new SaveContext(true, false));
                    saveApi.LoadCallback(bytes, new LoadContext(true));
                }
                long session = 1;
                ExtendedDataLaunchOriginApi.MarkMapStarted(session);
                var before = Snapshot();
                for (int restart = 0; restart < 3; restart++)
                {
                    ExtendedDataLaunchOriginApi.OnMissionEnded(session, MissionEndReason.Replaced, true, session + 1, restarting: true);
                    session++;
                    if (ExtendedDataLaunchOriginApi.Origin == ExtendedDataLaunchOriginKind.CustomizedCoopTrail)
                        ExtendedDataLaunchOriginApi.PrepareCoopLaunch(3, 10, restarting: true);
                    else ExtendedDataLaunchOriginApi.MarkRestartPending();
                    saveApi.OnUnloadCallback();
                    saveApi.OnUnloadCallback();
                    Check(ExtendedDataLaunchOriginApi.LaunchPending && before == Snapshot(), "Restart unload changed origin/provenance.");
                    ExtendedDataLaunchOriginApi.MarkMapStarted(session);
                    Check(!ExtendedDataLaunchOriginApi.LaunchPending && before == Snapshot(), "Restart completion changed origin/provenance.");
                }
                // The destination is prepared before the old restarted session ends.
                ExtendedDataLaunchOriginApi.SetCustomizedCustomTrail(91, 4);
                var destination = Snapshot();
                ExtendedDataLaunchOriginApi.OnMissionEnded(session, MissionEndReason.Replaced, true, session + 1, restarting: false);
                saveApi.OnUnloadCallback();
                Check(ExtendedDataLaunchOriginApi.LaunchPending && destination == Snapshot(), "Old End removed the next mission's origin.");
                ExtendedDataLaunchOriginApi.MarkMapStarted(++session);
                ExtendedDataLaunchOriginApi.OnMissionEnded(session - 1, MissionEndReason.Unloaded, true, null, false);
                Check(destination == Snapshot(), "Stale source End removed the ready destination origin.");
                ExtendedDataLaunchOriginApi.OnMissionEnded(session, MissionEndReason.SceneChanged, true, null, false);
                Check(ExtendedDataLaunchOriginApi.Origin == ExtendedDataLaunchOriginKind.None, "Real departure retained origin.");
            }
            foreach (var reason in new[] { MissionEndReason.Failed, MissionEndReason.Exception, MissionEndReason.ApplicationExit,
                MissionEndReason.Unloaded, MissionEndReason.SceneChanged })
            {
                establish();
                ExtendedDataLaunchOriginApi.MarkMapStarted(10);
                ExtendedDataLaunchOriginApi.OnMissionEnded(10, MissionEndReason.Replaced, true, 11, true);
                // A delayed client never becomes ready; abort must release its transferred origin.
                ExtendedDataLaunchOriginApi.OnMissionEnded(11, reason, false, null, false);
                Check(ExtendedDataLaunchOriginApi.Origin == ExtendedDataLaunchOriginKind.None &&
                    !ExtendedDataLaunchOriginApi.LaunchPending, "Failed/abandoned launch retained origin.");
            }
        }
        ExtendedDataLaunchOriginApi.SetCustomizedCoopTrail(3, 10);
        ExtendedDataLaunchOriginApi.MarkMapStarted(20);
        ExtendedDataLaunchOriginApi.OnMissionEnded(20, MissionEndReason.Replaced, true, 21, true);
        // Replace an abandoned delayed restart with an ordinary map lacking Customize.
        ExtendedDataLaunchOriginApi.OnMissionEnded(21, MissionEndReason.Replaced, false, 22, false);
        Check(ExtendedDataLaunchOriginApi.Origin == ExtendedDataLaunchOriginKind.None,
            "Abandoned restart origin leaked into a different map.");
        ExtendedDataLaunchOriginApi.SetCustomizedCustomTrail(91, 4);
        var preRootDestination = Snapshot();
        ExtendedDataLaunchOriginApi.OnMissionEnded(22, MissionEndReason.SceneChanged, true, null, false);
        Check(preRootDestination == Snapshot() && ExtendedDataLaunchOriginApi.LaunchPending,
            "Scene preparation removed the next launch before its root entry.");
        ExtendedDataLaunchOriginApi.OnMissionEnded(23, MissionEndReason.Failed, false, null, false);
        ExtendedDataLaunchOriginApi.PrepareCoopLaunch(3, 10, restarting: true);
        Check(ExtendedDataLaunchOriginApi.Origin == ExtendedDataLaunchOriginKind.None, "Restart invented a Customize origin.");
        ExtendedDataLaunchOriginApi.PrepareCoopLaunch(2, 5, restarting: false);
        Check(ExtendedDataLaunchOriginApi.Origin == ExtendedDataLaunchOriginKind.CustomizedCoopTrail &&
            ExtendedDataLaunchOriginApi.TrailId == 2 && ExtendedDataLaunchOriginApi.MissionId == 5 &&
            !ExtendedDataLaunchOriginApi.RestoredFromSave, "Ordinary Coop launch did not establish its own origin.");
        ExtendedDataLaunchOriginApi.Clear();
    }

    private static (ExtendedDataLaunchOriginKind, int, int, int, bool) Snapshot() =>
        (ExtendedDataLaunchOriginApi.Origin, ExtendedDataLaunchOriginApi.TrailType,
         ExtendedDataLaunchOriginApi.TrailId, ExtendedDataLaunchOriginApi.MissionId, ExtendedDataLaunchOriginApi.RestoredFromSave);
    private static void Check(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
}
