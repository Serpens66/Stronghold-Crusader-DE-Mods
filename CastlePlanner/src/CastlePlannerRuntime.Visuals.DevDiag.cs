using System;
using System.Collections.Generic;
using SHCDESE.EventAPI.Buildings;
using R3;

namespace CastlePlanner
{
    internal sealed unsafe partial class CastlePlannerRuntime
    {
        private readonly Dictionary<int, string> diagnosticCastleNames = new Dictionary<int, string>();
        partial void DiagInstall()
        {
            CastleVisualDiagnostics.Initialize(log);
            subscriptions.Add(Shared.MissionEvents.Started.Subscribe(args =>
                CastleVisualDiagnostics.MissionStarted(args.Context)));
        }
        partial void DiagLoadSave(APIShared.MissionLifecycleNotification args)
        {
            diagnosticCastleNames.Clear();
            CastleVisualDiagnostics.LoadedSave(args.Context, CaptureHumanPlayerIds);
        }
        partial void DiagStart(APIShared.MissionLifecycleNotification args)
        {
            diagnosticCastleNames.Clear();
            CastleVisualDiagnostics.Start(args.Context);
        }
        partial void DiagEnd(string reason)
        {
            diagnosticCastleNames.Clear();
            CastleVisualDiagnostics.End(reason);
        }
        partial void DiagRemember(PendingAivImport prepared)
        { diagnosticCastleNames[prepared.PlayerId] = prepared.DisplayName + ", sha256=" + prepared.ContentHash; }
        partial void DiagBeforeCastle(PreparedAivCastle castle)
        {
            diagnosticCastleNames.TryGetValue(castle.PlayerId, out string name);
            CastleVisualDiagnostics.BeginCastle(castle.PlayerId, name ?? "unknown", castle.RequestedKeepX,
                castle.RequestedKeepY, ToAivRotation(castle.Orientation), castle.PreparedKeepX, castle.PreparedKeepY);
        }
        partial void DiagSnapshot(int playerId, string phase) { CastleVisualDiagnostics.Snapshot(playerId, phase); }
        partial void DiagSpawn(BuildingSpawnEventArgs args) { CastleVisualDiagnostics.Spawn(args); }
        partial void DiagTick(int tick) { CastleVisualDiagnostics.Tick(tick); }
    }

    internal sealed partial class BlueprintRuntimeController
    {
        partial void DiagVisualFrame()
        {
            if (mapActive) CastleVisualDiagnostics.VisualFrame(blueprintVisible);
        }
    }
}
