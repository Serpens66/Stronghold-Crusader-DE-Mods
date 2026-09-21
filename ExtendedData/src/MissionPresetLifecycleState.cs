namespace ExtendedData
{
    internal enum MissionPresetLaunchKind
    {
        None,
        CustomTrail,
        TrailMakerTest,
        CoopTrail,
    }

    internal enum MissionPresetEndKind
    {
        Replaced,
        Unloaded,
        SceneChanged,
        Failed,
        Exception,
        ApplicationExit,
    }

    internal enum MissionPresetEndAction
    {
        Exit,
        Preserve,
        SuspendTrailMaker,
    }

    /// <summary>
    /// Dependency-free state machine for Map/Trail preset ownership across Vanilla's
    /// nested unload, replacement, restart and Trail Maker return paths.
    /// </summary>
    internal sealed class MissionPresetLifecycleState
    {
        internal MissionPresetLaunchKind PendingLaunch { get; private set; }
        internal MissionPresetLaunchKind ActiveMission { get; private set; }
        internal bool AwaitingTrailMakerReturn { get; private set; }

        internal void Prepare(MissionPresetLaunchKind kind)
        {
            if (kind == MissionPresetLaunchKind.None)
                return;
            PendingLaunch = kind;
            if (kind == MissionPresetLaunchKind.TrailMakerTest)
                AwaitingTrailMakerReturn = false;
        }

        internal bool ConfirmStarted(MissionPresetLaunchKind kind)
        {
            if (kind == MissionPresetLaunchKind.None || PendingLaunch != kind)
            {
                Reset();
                return false;
            }

            ActiveMission = kind;
            PendingLaunch = MissionPresetLaunchKind.None;
            AwaitingTrailMakerReturn = false;
            return true;
        }

        internal MissionPresetEndAction End(MissionPresetEndKind kind)
        {
            if (kind == MissionPresetEndKind.Replaced && PendingLaunch != MissionPresetLaunchKind.None)
                return MissionPresetEndAction.Preserve;

            bool trailMakerTransition =
                PendingLaunch == MissionPresetLaunchKind.TrailMakerTest ||
                ActiveMission == MissionPresetLaunchKind.TrailMakerTest;
            PendingLaunch = MissionPresetLaunchKind.None;
            ActiveMission = MissionPresetLaunchKind.None;

            if (trailMakerTransition && kind != MissionPresetEndKind.ApplicationExit)
            {
                AwaitingTrailMakerReturn = true;
                return MissionPresetEndAction.SuspendTrailMaker;
            }

            AwaitingTrailMakerReturn = false;
            return MissionPresetEndAction.Exit;
        }

        internal void AwaitTrailMakerReturn()
        {
            PendingLaunch = MissionPresetLaunchKind.None;
            ActiveMission = MissionPresetLaunchKind.None;
            AwaitingTrailMakerReturn = true;
        }

        internal void CompleteTrailMakerReturn()
        {
            PendingLaunch = MissionPresetLaunchKind.None;
            ActiveMission = MissionPresetLaunchKind.None;
            AwaitingTrailMakerReturn = false;
        }

        internal void Reset()
        {
            PendingLaunch = MissionPresetLaunchKind.None;
            ActiveMission = MissionPresetLaunchKind.None;
            AwaitingTrailMakerReturn = false;
        }
    }
}
