using APIShared;
using APIShared.GameModes;
using APIShared.Presentation;
using APIShared.Commands;
using BepInEx.Logging;
using CrusaderDE;
using System;
using System.Threading;
using Steamworks;

namespace Spectator
{
    // The host setting grants a local view, never a replacement network/player identity.
    internal sealed class SpectatorDefeat
    {
        private readonly ManualLogSource log;
        private readonly SpectatorViewModel settings;
        private readonly object stateGate = new object();
        private readonly PlayerDefeatSnapshot[] observed = new PlayerDefeatSnapshot[9];
        private long activeSessionId;
        private int localPlayerId;
        private GameModeSnapshot mode;
        private bool initialized, promotionRequested, promotionActivated, promotionConfirmed, promotionIdentityError;
        private int promotionPlayerId = -1;
        private static SpectatorDefeat instance;

        internal SpectatorDefeat(ManualLogSource log, SpectatorViewModel settings)
        { this.log = log; this.settings = settings; }
        // Constructed on Unity's startup thread; local UI input arrives on the same thread.
        private readonly int mainThreadId = Thread.CurrentThread.ManagedThreadId;
        private readonly System.Collections.Generic.HashSet<Enums.GameActionCommand> reportedVetoes =
            new System.Collections.Generic.HashSet<Enums.GameActionCommand>();

        internal void Initialize()
        {
            instance = this;
            if (!ApiShared.Current.TryGetMissionLifecycle(SpectatorPlugin.PluginGuid, out var lifecycle, out var diagnostic) ||
                !lifecycle.TryRegisterObserver("defeat-spectator", BeginSession, _ => Reset(0), null, out diagnostic))
                throw new InvalidOperationException("Defeat lifecycle unavailable: " + diagnostic?.Reason);
            foreach (var operation in new[] { MissionOverOperation.State, MissionOverOperation.Text })
                if (!MissionOverEvents.TryRegister(operation, SpectatorPlugin.PluginGuid, "eliminated-player-result",
                    args => { if (initialized) args.Result = SpectatorDefeatPolicy.ResolvePresentedGameOverState(args.Result,
                        Volatile.Read(ref promotionRequested) || Volatile.Read(ref promotionActivated)); }, null, out string reason))
                    throw new InvalidOperationException(reason);
            if (!ApiShared.Current.TryGetPlayerDefeat(SpectatorPlugin.PluginGuid,
                    out IPlayerDefeatCapability capability, out diagnostic) ||
                !(capability is IPlayerDefeatStateCapability states))
                throw new InvalidOperationException("Player defeat state capability unavailable: " + diagnostic?.Reason);
            // Run after the existing offline power selector, which consumes its command without
            // executing Vanilla. Remaining ordinary gameplay commands are denied for the observer.
            if (!GameActionEvents.TryRegister(SpectatorPlugin.PluginGuid, "eliminated-player-command-safety",
                    OnGameAction, null, out string actionReason, order: int.MaxValue))
                throw new InvalidOperationException("Eliminated-player command safety unavailable: " + actionReason);
            initialized = true; // The state observer may replay the current immutable baseline immediately.
            if (!states.TryRegisterStateObserver("eliminated-player-view", OnObservedState, out diagnostic))
            {
                initialized = false;
                throw new InvalidOperationException("Player defeat state observer unavailable: " + diagnostic?.Reason);
            }
        }

        private void BeginSession(MissionLifecycleNotification notification)
        {
            lock (stateGate)
            {
                Reset(0);
                mode = notification.Context.Mode;
                Volatile.Write(ref localPlayerId, PlayerPerspectiveAPI.GetControlledPlayerId());
                Interlocked.Exchange(ref activeSessionId, notification.Context.SessionId);
            }
        }

        private void Reset(long session)
        {
            lock (stateGate)
            {
                Interlocked.Exchange(ref activeSessionId, 0);
                for (int player = 1; player <= 8; player++) Volatile.Write(ref observed[player], null);
                Volatile.Write(ref localPlayerId, -1);
                promotionRequested = promotionActivated = promotionConfirmed = promotionIdentityError = false;
                promotionPlayerId = -1;
                lock (reportedVetoes) reportedVetoes.Clear();
                Interlocked.Exchange(ref activeSessionId, session);
            }
        }

        private void OnObservedState(PlayerDefeatSnapshot snapshot)
        {
            if (!initialized || snapshot == null || snapshot.PlayerId < 1 || snapshot.PlayerId > 8 ||
                snapshot.SessionId != Interlocked.Read(ref activeSessionId)) return;
            lock (stateGate)
            {
                if (snapshot.SessionId != Interlocked.Read(ref activeSessionId)) return;
                Volatile.Write(ref observed[snapshot.PlayerId], snapshot);
                if (SpectatorDefeatPolicy.IsLocalElimination(activeSessionId, snapshot.SessionId, snapshot.PlayerId,
                        Volatile.Read(ref localPlayerId), snapshot.Readable, snapshot.IsEliminated) &&
                    settings.Activation.Defeat && (mode.IsRealMultiplayer || mode.IsSingleplayerSkirmishMode))
                    Volatile.Write(ref promotionRequested, true);
            }
            // The publisher can be the simulation thread. Invalidate bound UI only on the presentation path.
            long session = snapshot.SessionId;
            SpectatorRuntime.PostPresentation(() => {
                if (session != Interlocked.Read(ref activeSessionId)) return;
                SpectatorRuntime.Statistics?.InvalidateMenu();
                SpectatorRuntime.InvalidateToolbar();
                SpectatorPerspectiveRuntime.OnDefeatStateChanged();
            });
        }

        // Real defeat of any slot from APIShared's observed state (official loss or confirmed lord
        // death). No snapshot yet, or a not yet spawned lord, never counts as defeated.
        internal static bool IsPlayerEliminated(int player)
        {
            var current = instance;
            if (current == null || player < 1 || player > 8) return false;
            var snapshot = Volatile.Read(ref current.observed[player]);
            return snapshot != null && SpectatorDefeatPolicy.IsRosterEliminated(Interlocked.Read(ref current.activeSessionId),
                snapshot.SessionId, snapshot.Readable, snapshot.IsEliminated);
        }

        internal static bool IsEliminatedSpectator => instance != null && instance.IsConfirmedParticipant();
        internal static bool IsEliminatedParticipant
        {
            get
            {
                var current = instance;
                int player = PlayerPerspectiveAPI.GetControlledPlayerId();
                if (current == null || player < 1 || player > 8) return false;
                var snapshot = Volatile.Read(ref current.observed[player]);
                return snapshot != null && SpectatorDefeatPolicy.IsLocalElimination(Interlocked.Read(ref current.activeSessionId),
                    snapshot.SessionId, snapshot.PlayerId, player, snapshot.Readable, snapshot.IsEliminated);
            }
        }

        private bool IsConfirmedParticipant()
        {
            long session = Interlocked.Read(ref activeSessionId);
            int player = promotionPlayerId;
            if (!promotionConfirmed || promotionIdentityError || session <= 0 || player < 1 || player > 8 ||
                GameData.Instance?.lastGameState == null || GameData.Instance.lastGameState.spectatorMode == 0 ||
                PlayerPerspectiveAPI.GetControlledPlayerId() != player) return false;
            // Confirmation is a terminal role for this participant/session. A transient unreadable
            // resource snapshot must not turn a foreign observer view back into active-player input.
            return session == Interlocked.Read(ref activeSessionId);
        }

        // Called by the existing durable presentation callback, including when the perspective option is off.
        internal static void ObservePresentedState()
        {
            var current = instance;
            if (current == null || !current.initialized || current.promotionIdentityError ||
                Interlocked.Read(ref current.activeSessionId) <= 0) return;
            long session = Interlocked.Read(ref current.activeSessionId);
            if (!current.promotionActivated) current.TryActivateLocalView();
            var state = GameData.Instance?.lastGameState;
            if (!current.promotionActivated || current.promotionConfirmed || state == null || state.spectatorMode == 0) return;
            int native = PlayerPerspectiveAPI.GetRawNativeViewPlayerId();
            int managed = PlayerPerspectiveAPI.GetControlledPlayerId();
            if (session != Interlocked.Read(ref current.activeSessionId)) return;
            if (!SpectatorDefeatPolicy.ConfirmIdentity(current.promotionPlayerId, native, managed))
            {
                current.promotionIdentityError = true;
                Shared.DebugLogHelper.LogErrorLimited(current.log, "spectator-promotion-identity",
                    $"Eliminated-player view identity mismatch: expected={current.promotionPlayerId}, native={native}, managed={managed}.");
                return;
            }
            SpectatorRuntime.PrepareEliminatedObserverHud();
            current.promotionConfirmed = true;
            SpectatorPerspectiveRuntime.OnEliminatedSpectatorConfirmed();
            SpectatorRuntime.InvalidateToolbar();
            Shared.DebugLogHelper.LogDebugLimited(current.log, "spectator-promotion-confirmed",
                $"Eliminated-player view confirmed: player={managed}; participant identity preserved.");
        }

        // Vetoes only LOCAL input (Unity thread) of a confirmed observer that would act as the viewed
        // player (SpectatorObserverActionPolicy). Synchronized simulation/Chore calls, e.g. other mods'
        // GameActions inside a Chore callback, run on every peer and are never touched.
        private static void OnGameAction(GameActionPreEventArgs args)
        {
            var current = instance;
            if (args.SkipOriginalFunction || current == null || !current.promotionConfirmed ||
                Thread.CurrentThread.ManagedThreadId != current.mainThreadId) return;
            var decision = SpectatorObserverActionPolicy.Decision.Veto;
            int viewed = -1;
            try
            {
                if (!IsEliminatedSpectator) return;
                decision = SpectatorObserverActionPolicy.Decide(true, true, args.Command, false);
                if (decision == SpectatorObserverActionPolicy.Decision.Allow) return;
                bool ally = args.Command >= Enums.GameActionCommand.Ally_Orders &&
                    args.Command <= Enums.GameActionCommand.Ally_CancelOrders;
                if (ally && !current.mode.IsRealMultiplayer && SpectatorPerspectiveRuntime.CanIssueAllyAction(args.Command, args.StructureId,
                        args.ActionState, args.Value2)) return;
                viewed = PlayerPerspectiveAPI.GetRawNativeViewPlayerId();
                if (ObserverCommandExceptions.IsAllowed(new ObserverCommandRequest(current.promotionPlayerId, viewed,
                        args.Command, args.StructureId, args.ActionState, args.Value2))) return;
            }
            catch (Exception error)
            {
                // A failing optional ally/readback path must not roll this veto back to Vanilla.
                ReportCommandValidationFailure(current, error);
            }
            args.SkipOriginalFunction = true;
            current.ReportVeto(args.Command, viewed, decision == SpectatorObserverActionPolicy.Decision.VetoUnaudited);
        }

        // Once per command and session, so it stays visible what an observer could not issue.
        private void ReportVeto(Enums.GameActionCommand command, int viewed, bool unaudited)
        {
            lock (reportedVetoes) if (!reportedVetoes.Add(command)) return;
            string message = (unaudited ? "SPECTATOR_OBSERVER_UNAUDITED_GAMEACTION_BLOCKED" : "SPECTATOR_OBSERVER_GAMEACTION_BLOCKED") +
                $": command={command} ({(int)command}), observer={promotionPlayerId}, viewed={viewed}.";
            if (unaudited) Shared.DebugLogHelper.LogWarning(log, message);
            else Shared.DebugLogHelper.LogInfo(log, message);
        }

        private static void ReportCommandValidationFailure(SpectatorDefeat current, Exception error)
            => SpectatorRuntime.PostPresentation(() => Shared.DebugLogHelper.LogErrorLimited(current.log,
                "spectator-command-safety", "Eliminated-player command validation failed closed: " + error));

        private void TryActivateLocalView()
        {
            int player = PlayerPerspectiveAPI.GetControlledPlayerId();
            if (player < 1 || player > 8) return;
            Volatile.Write(ref localPlayerId, player);
            var snapshot = Volatile.Read(ref observed[player]);
            bool confirmed = snapshot != null && SpectatorDefeatPolicy.IsLocalElimination(Interlocked.Read(ref activeSessionId),
                snapshot.SessionId, snapshot.PlayerId, player, snapshot.Readable, snapshot.IsEliminated);
            // Runs every frame: cheap conjuncts first, before the Steam/lobby participant lookup.
            if (!settings.Activation.Defeat || !confirmed) return;
            if (!SpectatorDefeatPolicy.CanPromoteEliminatedPlayerToSpectator(settings.Activation.Defeat,
                    IsActiveMatch(), GameModeHelper.IsMapEditor(), SpectatorPerspectiveRuntime.IsOriginalSpectator(),
                    mode.IsRealMultiplayer || mode.IsSingleplayerSkirmishMode,
                    IsValidParticipant(player, mode.IsRealMultiplayer), confirmed, player)) return;
            Volatile.Write(ref promotionRequested, true);
            // This audited local-only action returns zero and cannot enter a managed game-over/UI branch.
            if (GameData.Instance.lastGameState.spectatorMode == 0)
                EngineInterface.GameAction(Enums.GameActionCommand.SpectatorMode, 0, 0);
            if (snapshot.SessionId != Interlocked.Read(ref activeSessionId)) return;
            promotionPlayerId = player;
            Volatile.Write(ref promotionActivated, true);
            Shared.DebugLogHelper.LogDebugLimited(log, "spectator-local-view",
                $"Activated eliminated-player view: session={snapshot.SessionId}, player={player}, mode={mode.ToDiagnosticString()}.");
        }

        private static bool IsValidParticipant(int player, bool multiplayer)
        {
            if (EditorDirector.instance == null || EditorDirector.instance.ActivePlayerID != player) return false;
            if (!multiplayer) return true;
            var member = Platform_Multiplayer.Instance?.getPlayer(player);
            ulong steam = SteamUser.GetSteamID().m_SteamID;
            return member != null && !member.kicked && !member.skirmishAI &&
                member.playerID == player && steam > 1000 && member.steamID == steam;
        }

        private static bool IsActiveMatch() => FatControler.currentScene == Enums.SceneIDS.ActualMainGame &&
            Director.instance != null && Director.instance.SimRunning && GameData.Instance?.lastGameState != null;
    }
}
