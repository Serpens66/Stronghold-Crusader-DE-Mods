using System;
using System.Runtime.CompilerServices;
using System.Threading;
using APIShared;
using APIShared.Statistics;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.Interop.Enums;

namespace CastlePlanner
{
    internal sealed partial class BlueprintRuntimeController
    {
        private readonly BlueprintSpectatorCaptureState spectatorCapture = new BlueprintSpectatorCaptureState();
        private Func<long, bool> isSpectatorSessionReady;
        private EngineReadbackRegistration spectatorReadback;
        private SpectatorReadRequest spectatorPending;
        private SpectatorReadRequest spectatorInbox;
        private BlueprintSpectatorKeep[] spectatorKeeps;
        private BlueprintLivePlan[] spectatorPlans;
        private BlueprintLayout[] spectatorLayouts;
        private int spectatorPlayer = -1;
        private int spectatorKeepsToCheck;
        private int spectatorLostKeeps;
        private int spectatorUnverifiedKeeps;
        private bool spectatorKeepReadFailed;
        private Noesis.SolidColorBrush spectatorBrush;
        private int spectatorBrushPlayer = -1;

        private void InitializeSpectatorBlueprints()
        {
            var client = ApiShared.ForMod(CastlePlannerPlugin.PluginGuid);
            subscriptions.Add(PlayerPerspectiveAPI.SubscribeChanges(CastlePlannerPlugin.PluginGuid, "blueprint-view", change =>
                client.Ui.TryPost(() =>
                {
                    if (HasSpectatorIdentity)
                        RefreshSpectatorIdentity();
                    RequestFrame();
                })));

            try
            {
                BindSpectatorReadiness(client);
            }
            catch (Exception error)
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "SpectatorBlueprintReadiness",
                    "AI blueprint mission readiness unavailable: " + error.GetBaseException().Message);
                return;
            }

            if (!EngineReadbacks.TryRegister(CastlePlannerPlugin.PluginGuid, "blueprint-spectator",
                CopySpectatorReadback, out spectatorReadback, out string reason))
                Shared.DebugLogHelper.LogErrorLimited(log, "SpectatorBlueprintReadback",
                    "AI blueprint engine readback unavailable: " + reason);
        }

        // An older SDK can lack this capability. Keep the dependency out of the
        // ordinary blueprint controller's startup path when resolving that SDK.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void BindSpectatorReadiness(ModApiClient client)
        {
            if (!client.TryGetMissionPlayStateReadiness(out var readiness, out var diagnostic) ||
                !readiness.TryRegisterObserver("blueprint-spectator", _ => RequestFrame(), out diagnostic))
                throw new InvalidOperationException(diagnostic?.Reason);

            isSpectatorSessionReady = session => readiness.Current?.SessionId == session;
        }

        private void ResetSpectatorBlueprints()
        {
            CancelSpectatorReadback();
            Volatile.Write(ref spectatorKeeps, null);
            spectatorKeepsToCheck = 0;
            spectatorLostKeeps = 0;
            spectatorUnverifiedKeeps = 0;
            spectatorKeepReadFailed = false;
            spectatorPlans = null;
            spectatorLayouts = null;
            spectatorCapture.Reset();
            spectatorPlayer = -1;
            spectatorBrush = null;
            spectatorBrushPlayer = -1;
            spectatorAivNames.Clear();
            spectatorAivFileName = null;
            spectatorNameErrorLogged = false;
        }

        private void RefreshSpectatorIdentity()
        {
            int player = PlayerPerspectiveAPI.GetViewedPlayerId();
            if (player == spectatorPlayer)
                return;

            spectatorPlayer = player;
            PresentCachedSpectatorPlan();
        }

        private void SetSpectatorRequested(bool requested)
        {
            spectatorCapture.SetRequested(requested);
            if (requested)
                spectatorKeepsToCheck |= spectatorUnverifiedKeeps;
            else if (spectatorPending?.Kind == SpectatorReadKind.Plans)
                CancelSpectatorReadback();
            PresentCachedSpectatorPlan();
        }

        private void PresentCachedSpectatorPlan()
        {
            int player = spectatorPlayer;
            var cached = player > 0 && player <= GamePlayerManagerAPI.MAX_PLAYERS
                ? spectatorLayouts?[player] : null;
            var plan = cached != null ? spectatorPlans[player] : null;
            RefreshSpectatorAivName(plan);
            controlledKeepAvailable = plan != null;

            int pendingChecks = spectatorKeepsToCheck | spectatorUnverifiedKeeps;
            if (spectatorPending?.Kind == SpectatorReadKind.CheckKeeps)
                pendingChecks |= spectatorPending.KeepMask;
            bool checkingKeep = player > 0 && (pendingChecks & (1 << player)) != 0;
            BlueprintLayout next = spectatorCapture.Requested && EffectiveBlueprintMode && !checkingKeep ? cached : null;
            if (!ReferenceEquals(layout, next))
            {
                if (next == null && cached != null && ReferenceEquals(layout, cached))
                {
                    // A simple toggle can show the renderer's existing geometry again.
                    SetBlueprintVisible(false, "spectator toggle");
                    RefreshHud();
                    return;
                }

                renderer.Clear();
                blueprintVisible = false;
                layout = next;
                if (next != null)
                    renderer.PreloadDepthCaptures(next);
            }

            if (next != null)
                SetBlueprintVisible(true, "spectator AI plan");
            RefreshHud();
        }

        private void RebuildSpectatorLayouts()
        {
            if (spectatorPlans == null)
                return;

            var options = settings.GetBlueprintDisplayOptions();
            for (int player = 1; player < spectatorPlans.Length; player++)
            {
                var plan = spectatorPlans[player];
                spectatorLayouts[player] = null;
                if (plan == null)
                    continue;

                try { spectatorLayouts[player] = plan.Build(options); }
                catch (Exception error)
                {
                    Shared.DebugLogHelper.LogErrorLimited(log, "SpectatorBlueprintLayout",
                        $"AI blueprint layout unavailable for player {player}: {error}");
                }
            }
        }

        private void ObserveSpectatorKeepDeletion(BuildingDeleteEventArgs args)
        {
            var keeps = Volatile.Read(ref spectatorKeeps);
            if (keeps == null || args.BuildingId < 1)
                return;

            for (int player = 1; player < keeps.Length; player++)
            {
                if (keeps[player].Id != args.BuildingId)
                    continue;

                int removedPlayer = player;
                ApiShared.ForMod(CastlePlannerPlugin.PluginGuid).Ui.TryPost(() =>
                {
                    if (!ReferenceEquals(keeps, spectatorKeeps) ||
                        (spectatorLostKeeps & (1 << removedPlayer)) != 0)
                        return;

                    spectatorKeepsToCheck |= 1 << removedPlayer;
                    if (HasSpectatorIdentity && spectatorPlayer == removedPlayer && blueprintVisible)
                        SetBlueprintVisible(false, "spectator Keep verification");
                    RequestFrame();
                });
                return;
            }
        }

        private void UpdateSpectatorReadback()
        {
            bool needsKeeps = spectatorKeeps == null && !spectatorKeepReadFailed;
            if (spectatorPending == null && !needsKeeps && spectatorKeepsToCheck == 0 &&
                !spectatorCapture.NeedsCapture)
                return;

            long session = observerRole.SessionId;
            if (spectatorReadback == null || !mapActive || editorSessionActive || !EffectiveBlueprintMode ||
                session <= 0 || !isSpectatorSessionReady(session) ||
                GameData.Instance?.lastGameState?.game_type != (int)eGameTypeModes.GAMETYPE_MULTIPLAYER)
            {
                CancelSpectatorReadback();
                return;
            }

            var completed = spectatorPending;
            if (completed != null)
            {
                if (completed.Kind == SpectatorReadKind.Plans &&
                    (!HasSpectatorIdentity || !spectatorCapture.Requested))
                {
                    CancelSpectatorReadback();
                    return;
                }
                if (!completed.Completed)
                {
                    RequestSpectatorReadbackFrame();
                    return;
                }

                spectatorPending = null;
                if (!completed.Cancelled)
                    ApplySpectatorReadback(completed);
            }

            if (spectatorKeeps == null && !spectatorKeepReadFailed)
                QueueSpectatorReadback(new SpectatorReadRequest(session, SpectatorReadKind.Keeps));
            else if (spectatorKeepsToCheck != 0)
            {
                QueueSpectatorReadback(new SpectatorReadRequest(session, SpectatorReadKind.CheckKeeps,
                    spectatorKeeps, spectatorKeepsToCheck));
                spectatorKeepsToCheck = 0;
            }
            else if (HasSpectatorIdentity && spectatorCapture.NeedsCapture && spectatorKeeps != null)
                QueueSpectatorReadback(new SpectatorReadRequest(session, SpectatorReadKind.Plans,
                    spectatorKeeps, spectatorLostKeeps, GetSpectatorOrderedTileCapacity()));
        }

        private void ApplySpectatorReadback(SpectatorReadRequest completed)
        {
            if (completed.Error != null)
            {
                if (completed.Kind == SpectatorReadKind.Keeps)
                    spectatorKeepReadFailed = true;
                if (completed.Kind == SpectatorReadKind.Plans)
                    spectatorCapture.Finish(false);
                Shared.DebugLogHelper.LogErrorLimited(log, "SpectatorBlueprintCopy",
                    "AI blueprint readback failed: " + completed.Error);
                if (completed.Kind == SpectatorReadKind.CheckKeeps)
                    spectatorUnverifiedKeeps |= completed.KeepMask;
                RefreshHud();
                return;
            }

            switch (completed.Kind)
            {
                case SpectatorReadKind.Keeps:
                    Volatile.Write(ref spectatorKeeps, completed.Keeps);
                    break;

                case SpectatorReadKind.CheckKeeps:
                    spectatorUnverifiedKeeps &= ~completed.KeepMask;
                    DiscardSpectatorPlans(completed.LostKeeps);
                    break;

                case SpectatorReadKind.Plans:
                    spectatorPlayer = PlayerPerspectiveAPI.GetViewedPlayerId();
                    spectatorLostKeeps |= completed.LostKeeps;
                    spectatorPlans = completed.MaterializePlans();
                    spectatorLayouts = new BlueprintLayout[spectatorPlans.Length];
                    spectatorCapture.Finish(true);
                    RebuildSpectatorLayouts();
                    PresentCachedSpectatorPlan();
                    break;
            }
        }

        private void DiscardSpectatorPlans(int lostKeeps)
        {
            spectatorLostKeeps |= lostKeeps;
            if (spectatorPlans == null)
                return;

            for (int player = 1; player < spectatorPlans.Length; player++)
            {
                if ((lostKeeps & (1 << player)) == 0)
                    continue;

                spectatorPlans[player] = null;
                spectatorLayouts[player] = null;
            }
            if (HasSpectatorIdentity)
                PresentCachedSpectatorPlan();
        }

        private void QueueSpectatorReadback(SpectatorReadRequest request)
        {
            spectatorPending = request;
            Volatile.Write(ref spectatorInbox, request);
            spectatorReadback.Request();
            RequestSpectatorReadbackFrame();
        }

        private void RequestSpectatorReadbackFrame() => frameWork.RequestAfter(TimeSpan.FromMilliseconds(30));

        private void CancelSpectatorReadback()
        {
            var request = spectatorPending;
            if (request == null)
                return;

            request.Cancelled = true;
            Interlocked.CompareExchange(ref spectatorInbox, null, request);
            spectatorReadback.Cancel();
            spectatorPending = null;
            if (request.Kind == SpectatorReadKind.CheckKeeps)
                spectatorKeepsToCheck |= request.KeepMask;
        }

        private void CopySpectatorReadback()
        {
            var request = Interlocked.Exchange(ref spectatorInbox, null);
            if (request == null)
                return;

            try
            {
                // The readiness service takes its own state lock. Inside the engine
                // monitor use only the mission adapter's published context.
                if (!request.Cancelled && Shared.MissionEvents.CurrentContext?.SessionId == request.Session)
                    request.CopyFromSimulation();
                else
                    request.Cancelled = true;
            }
            catch (Exception error) { request.Error = error; }
            finally { request.Completed = true; }
        }

        private BlueprintHudDisplayState? SpectatorCaptureStatus =>
            spectatorCapture.GetStatus(spectatorReadback != null && !spectatorKeepReadFailed);
    }
}
