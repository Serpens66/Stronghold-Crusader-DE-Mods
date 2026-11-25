using System;
using System.Collections.Concurrent;
using System.IO;
using System.Diagnostics;
using System.Threading;
using APIShared;
using APIShared.Economy;
using APIShared.Networking;
using APIShared.Multiplayer;
using APIShared.Presentation;
using APIShared.Savegames;
using APIShared.Simulation;
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace BugfixesAndQoL.AutoSend
{
    // The engine owns simulation access; this lock only protects our save/presentation
    // snapshots. Unity never calls the engine or submits a Chore while holding it.
    internal sealed class AutoSendRuntime : IAutoSendWorld
    {
        private readonly object sync = new object();
        private readonly ModApiClient api;
        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private bool localFeatureCaptured, localFeatureEnabled;
        private readonly ConcurrentQueue<string> messages = new ConcurrentQueue<string>();
        private AutoSendState state = AutoSendGoods.NewState(), loadedState;
        private AutoSendPlanner planner;
        private RepairedRules preparedRepair, repairedRules;
        private bool resyncRulesReady;
        private AuthenticatedChoreChannel<AutoSendPacket> channel;
        private IMissionLifecycleCapability missions;
        private IUiFrameWorkRegistration presentation;
        private AutoSendView view;
        private MissionContext mission;
        private readonly AutoSendEpochs epochs = new AutoSendEpochs();
        private readonly AutoSendEditQueue edits = new AutoSendEditQueue();
        private readonly AutoSendInitialRoster initialRoster = new AutoSendInitialRoster();
        private readonly ulong[] humanIdentities = new ulong[9];
        private uint[] loadedIdentities;
        private ulong[] loadedHumanIdentities;
        private readonly int[,] stocks = new int[9, AutoSendGoods.Values.Length];
        private readonly int[,] capacities = new int[9, AutoSendGoods.Values.Length];
        private readonly bool[,] allies = new bool[9, 9];
        private readonly AutoSendRecipientReadiness[,] recipientReadiness = new AutoSendRecipientReadiness[9, 9];
        private long localEpoch;
        private long initialAttempt;
        private bool hasInitialAttempt;
        private int initialMapTick;
        private bool hasInitialMapTick;
        private int localPlayer;
        private int expectedLocalPlayer;
        private int expectedMask;
        private readonly long[] readyEpoch = new long[9], readyProof = new long[9];
        private readonly int[] readyRoster = new int[9];
        private bool initializedMap, identityMismatch, transferUncertain, firstTick, loadFailure, helloSubmitted, readySubmitted;
        private AllyGoodsPanelContext selectedContext;
        private int helloAttempts;
        private long nextHelloAttempt;
        private string feedback = string.Empty;
        internal AutoSendRuntime(ModApiClient api, ManualLogSource log, BugfixesAndQoLViewModel settings)
        { this.api = api; this.log = log; this.settings = settings; }
        internal bool LocalFeatureEnabled
        { get { lock (sync) return mission != null && localFeatureCaptured && localFeatureEnabled; } }
        private bool IsSenderEnabled(int sender) => sender >= 1 && sender <= 8 && (epochs.EnabledMask & (1 << sender)) != 0;

        internal void Register()
        {
            channel = api.Network.RegisterAuthenticatedChore<AutoSendPacket>("auto-send/edit-rule", Receive,
                new AuthenticatedChoreChannelOptions<AutoSendPacket> { ValidatePayload = ValidPacket,
                    Diagnostic = e => Report(e.Reason) });
            if (!api.Ui.TryRegisterBeforeRender("auto-send/presentation", Present, out presentation, out string reason))
                throw new InvalidOperationException(reason);
            view = new AutoSendView(this);
            if (!AllyGoodsPanelExtensions.TryRegisterSection(BugfixesAndQoLPlugin.PluginGuid, "auto-send", view.Create, AllyGoodsSectionPlacement.GoodsDialogBottom,
                out var section, out reason, 100)) throw new InvalidOperationException(reason);
            view.RegisterDisclosure(section);
            if (!AllyGoodsPanelExtensions.TryRegisterGoodsAdornment(BugfixesAndQoLPlugin.PluginGuid, "auto-send-active-goods", view.CreateAdornment,
                out _, out reason, 100)) throw new InvalidOperationException(reason);
            if (!AllyGoodsPanelExtensions.TrySubscribeContext(BugfixesAndQoLPlugin.PluginGuid, "auto-send-selection", SelectionChanged,
                out reason)) throw new InvalidOperationException(reason);
            var save = new VersionedSaveDataRegistration(BugfixesAndQoLPlugin.PluginGuid, BugfixesAndQoLPlugin.PluginGuid + ".AutoSend.rules", 4, 4,
                new[] { "rules", "players" }, Capture, Loaded) { SimulationRelevant = true };
            if (!VersionedSaveData.TryRegister(save, out _, out reason)) throw new InvalidOperationException(reason);
            if (!api.TryGetMissionLifecycle(out missions, out var diagnostic) ||
                !missions.TryRegisterObserver("auto-send/mission", Start, End, Initialization, out diagnostic))
                throw new InvalidOperationException(diagnostic.Reason);
            // The source is initialized after the global WhenReady publication. We register
            // from LibraryLoaded after APIShared's entire handler has returned.
            if (!SimulationTickEvents.TryRegister(BugfixesAndQoLPlugin.PluginGuid, "auto-send/supply", Tick, out reason))
                throw new InvalidOperationException(reason);
            int maximum = AutoSendSaveCodec.Encode(state).Length;
            resyncRulesReady = MultiplayerResyncEvents.TryRegister(BugfixesAndQoLPlugin.PluginGuid, "auto-send/repair-rules", BeginRepair, EndRepair, out reason) &&
                MultiplayerResyncData.TryRegister<RepairedRules>(BugfixesAndQoLPlugin.PluginGuid, "auto-send-rules", 1, maximum,
                    CaptureRulesForRepair, PrepareRulesForRepair, CommitRulesForRepair, out reason);
            if (!resyncRulesReady) Report("Automatic gifts unavailable in multiplayer: " + reason);
        }

        private static bool ValidPacket(AutoSendPacket p)
        {
            if (p == null || p.Protocol != 4 || p.Epoch == 0 || p.Revision < 0 || p.Amount < 0 ||
                p.Minimum < 0 || p.Maximum < 0 || p.Recipient < 0 || p.Recipient > 8) return false;
            var operation = (AutoSendEdit)p.Operation;
            if (operation == AutoSendEdit.ChatMessages)
                return p.Recipient == 0 && p.Good == 0 && p.Amount == 0 && p.Minimum == 0 && p.Maximum == 0;
            return operation == AutoSendEdit.Hello || operation == AutoSendEdit.Ready || operation == AutoSendEdit.Paused ||
                operation == AutoSendEdit.Reserve && AutoSendGoods.Contains(p.Good) ||
                operation == AutoSendEdit.SetRule && AutoSendGoods.Contains(p.Good) &&
                    p.Amount >= AutoSendState.MinimumIntervalSeconds && p.Amount <= AutoSendState.MaximumIntervalSeconds;
        }

        private void Start(MissionLifecycleNotification note)
        {
            lock (sync)
            {
                mission = note.Context;
                preparedRepair = repairedRules = null;
                state = loadedState ?? AutoSendGoods.NewState(); loadedState = null;
                planner = new AutoSendPlanner(state);
                epochs.Reset(); expectedLocalPlayer = expectedMask = 0;
                Array.Clear(readyEpoch, 0, readyEpoch.Length); Array.Clear(readyProof, 0, readyProof.Length); Array.Clear(readyRoster, 0, readyRoster.Length);
                initializedMap = firstTick = helloSubmitted = readySubmitted = false;
                localFeatureCaptured = localFeatureEnabled = false;
                edits.Reset();
                helloAttempts = 0; nextHelloAttempt = 0;
                identityMismatch = loadFailure || mission.Mode.IsRealMultiplayer && !resyncRulesReady;
                transferUncertain = false;
                Array.Clear(stocks, 0, stocks.Length); Array.Clear(capacities, 0, capacities.Length); Array.Clear(allies, 0, allies.Length);
                Array.Clear(recipientReadiness, 0, recipientReadiness.Length);
                Array.Clear(humanIdentities, 0, humanIdentities.Length);
                // A multiplayer save still has temporary lobby slots here. Vanilla
                // remaps them before starting simulation; bind its final slots once
                // in our first simulation callback, keeping NativeLoaded loss state.
                localPlayer = note.Context.LocalPlayerId.GetValueOrDefault();
                localEpoch = BitConverter.ToInt64(Guid.NewGuid().ToByteArray(), 0);
                if (localEpoch == 0) localEpoch = 1;
                feedback = string.Empty;
                if (hasInitialMapTick && initialAttempt == note.Context.SessionId) state.InitializeMissingSchedules(initialMapTick);
                else if (note.Context.Mode.IsRealMultiplayer)
                { identityMismatch = true; Report("Initial native schedule clock unavailable; automatic gifts remain stopped."); }
                else if (SimulationClock.TryGetCurrentTick(note.Context, out int fallbackTick)) state.InitializeMissingSchedules(fallbackTick);
                if (SimulationClock.TryGetCurrentTick(note.Context, out int startingTick)) state.MarkProcessedTick(startingTick);
            }
            long session = note.Context.SessionId;
            api.Ui.TryPost(() => Announce(session));
            Report("Mission started: " + note.Context.StartKind + "; " + note.Context.Mode.ToDiagnosticString());
        }
        private unsafe void Initialization(MissionLifecycleNotification note)
        {
            lock (sync)
            {
                if (!hasInitialAttempt || initialAttempt != note.Context.SessionId || note.Phase == MissionInitializationPhase.BeforeLoad)
                { initialAttempt = note.Context.SessionId; hasInitialAttempt = true; hasInitialMapTick = false; initialRoster.Reset(initialAttempt); }
                if (note.Phase != MissionInitializationPhase.NativeLoaded) return;
                hasInitialMapTick = SimulationClock.TryGetCurrentTick(note.Context, out initialMapTick);
                if (!AllyTransfers.IsAvailable) return;
                // This phase precedes running simulation. A first-published-tick snapshot
                // could differ between peers if a Lord died while another peer started.
                int defeated = 0;
                for (int player = 1; player <= 8; player++)
                {
                    if (!GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(player, out GamePlayerResources* data) || data == null) return;
                    if (data->r_WinLossState == WinLossState.Loss) defeated |= 1 << player;
                }
                initialRoster.Capture(note.Context.SessionId, defeated);
            }
        }
        private void End(MissionLifecycleNotification ignored)
        {
            lock (sync)
            {
                mission = null; initializedMap = false; edits.Reset(); localEpoch = 0;
                preparedRepair = repairedRules = null;
                transferUncertain = false;
                loadedState = null; loadedIdentities = null; loadedHumanIdentities = null; loadFailure = false;
            }
            RequestPresentation();
        }
        private void Announce(long session)
        {
            // IsAvailable only describes transport readiness. A spectator cannot announce
            // a sender, and a failed submission must not self-schedule every rendered frame.
            int participant = PlayerPerspectiveAPI.GetControlledPlayerId();
            if (participant < 1 || participant > 8) return;
            AutoSendPacket hello;
            lock (sync)
            {
                if (mission == null || mission.SessionId != session || !initializedMap || identityMismatch || helloSubmitted || epochs.IsReady(expectedMask)) return;
                expectedLocalPlayer = participant;
                // Only this main-thread presentation path reads the settings model.
                // Bind once per mission; every peer receives the choice in the existing
                // authenticated Hello, including peers whose own controls are disabled.
                if (!localFeatureCaptured)
                {
                    localFeatureEnabled = settings.EnableClientFeatures && settings.EnableAutoSend;
                    localFeatureCaptured = true;
                }
                long now = Stopwatch.GetTimestamp();
                if (now < nextHelloAttempt) return;
                if (helloAttempts == 0) helloAttempts = 1; else helloAttempts = 2;
                nextHelloAttempt = now + Stopwatch.Frequency;
                hello = new AutoSendPacket { Protocol = 4, Epoch = localEpoch, Operation = (int)AutoSendEdit.Hello, Enabled = localFeatureEnabled };
            }
            // Authenticated submission rejects spectators and foreign viewpoints. It is
            // safe to try again on the next actual UI edit; never retry a submitted edit.
            var sent = channel.Send(hello);
            if (sent.Submitted) lock (sync) { if (mission?.SessionId == session) helloSubmitted = true; }
            if (!sent.Submitted)
            {
                if (helloAttempts == 1) Report("Session announcement unavailable: " + sent.Reason);
                presentation?.RequestAfter(TimeSpan.FromSeconds(1));
            }
        }

        private void AnnounceReady()
        {
            AutoSendPacket packet;
            lock (sync)
            {
                if (mission == null || !initializedMap || identityMismatch || readySubmitted || !epochs.AllKnown(expectedMask) || localPlayer < 1 || localPlayer > 8 ||
                    epochs.Get(localPlayer) != localEpoch || (expectedMask & (1 << localPlayer)) == 0) return;
                packet = new AutoSendPacket { Protocol = 4, Epoch = localEpoch, Operation = (int)AutoSendEdit.Ready,
                    Good = expectedMask, Revision = epochs.Fingerprint(expectedMask) };
            }
            var result = channel.Send(packet);
            if (result.Submitted) lock (sync) readySubmitted = true;
            else presentation?.RequestAfter(TimeSpan.FromSeconds(1));
        }
        private void PromoteReadiness()
        {
            if (expectedMask == 0) return;
            for (int player = 1; player <= 8; player++) if (readyRoster[player] == expectedMask && readyEpoch[player] != 0)
                epochs.AcknowledgeReady(player, readyEpoch[player], expectedMask, readyProof[player]);
        }

        private void Receive(AuthenticatedChoreMessage<AutoSendPacket> message)
        {
            lock (sync)
            {
                if (mission == null || !initializedMap || mission.Mode.IsRealMultiplayer && !resyncRulesReady) return;
                ApplyRepairedRules();
                if (!AllyTransfers.IsAvailable) return;
                int sender = message.SenderPlayerId;
                var p = message.Packet;
                if ((AutoSendEdit)p.Operation == AutoSendEdit.Hello)
                {
                    // The native queue is retired at map load. Within this map, each
                    // authenticated player may bind exactly one fresh command epoch.
                    if (epochs.Bind(sender, p.Epoch, expectedLocalPlayer, localEpoch, p.Enabled, out bool localAcknowledged) && localAcknowledged)
                    {
                        localPlayer = sender; helloSubmitted = false;
                        if (expectedMask == 0 && !mission.Mode.IsRealMultiplayer) expectedMask = 1 << sender;
                    }
                    PromoteReadiness();
                    RequestPresentation(); return;
                }
                if ((AutoSendEdit)p.Operation == AutoSendEdit.Ready)
                {
                    // A peer may see this before its missing Hello is repeated. Keep the
                    // bounded proof and promote it only against the complete roster.
                    readyEpoch[sender] = p.Epoch; readyProof[sender] = p.Revision; readyRoster[sender] = p.Good;
                    PromoteReadiness(); RequestPresentation(); return;
                }
                if (!epochs.IsReady(expectedMask)) return;
                if (epochs.Get(sender) != p.Epoch) return;
                if (!IsSenderEnabled(sender)) return;
                string reason = string.Empty;
                bool accepted = false;
                var operation = (AutoSendEdit)p.Operation;
                if (state.GetRevision(sender) == long.MaxValue || p.Revision != state.GetRevision(sender) + 1)
                    reason = "The rule revision changed before this command was applied.";
                else if (operation == AutoSendEdit.Paused) accepted = state.TrySetPaused(sender, p.Enabled, out reason);
                else if (operation == AutoSendEdit.ChatMessages) accepted = state.TrySetShowVanillaChatMessages(sender, p.Enabled, out reason);
                else if (operation == AutoSendEdit.Reserve) accepted = state.TrySetReserve(sender, p.Good, p.Amount, out reason);
                else if (operation == AutoSendEdit.SetRule && p.Recipient != sender && IsAlliedLiving(sender, p.Recipient))
                {
                    if (SimulationClock.TryGetCurrentTick(mission, out int commandTick))
                        accepted = state.TrySetRule(sender, p.Recipient, p.Good, p.Minimum, p.Maximum, p.Enabled, p.Amount, commandTick, out reason);
                    else reason = "The current native map tick is unavailable.";
                }
                else reason = "The selected recipient is no longer a living ally.";
                if (sender == localPlayer && p.Epoch == localEpoch && edits.Acknowledge(mission.SessionId, sender, p.Epoch, p.Revision))
                    feedback = accepted ? string.Empty : reason;
                RequestPresentation();
            }
        }

        private unsafe void Tick(int tick)
        {
            lock (sync)
            {
                if (mission == null) return;
                ApplyRepairedRules();
                if (!firstTick) { firstTick = true; Report("First native mission tick reached; startup cleanup survived."); }
                if (!AllyTransfers.IsAvailable) return;
                if (!initializedMap)
                {
                    var roster = Platform_Multiplayer.Instance?.gameMembers;
                    if (roster != null) foreach (var member in roster)
                        if (member != null && !member.kicked && !member.skirmishAI && member.playerID >= 1 && member.playerID <= 8)
                        {
                            humanIdentities[member.playerID] = member.steamID;
                            if (mission.Mode.IsRealMultiplayer && member.steamID == 0) identityMismatch = true;
                        }
                    if (!initialRoster.TryResolveFinal(mission.SessionId, humanIdentities, loadedHumanIdentities,
                        mission.Mode.IsRealMultiplayer, out expectedMask, out string rosterFailure))
                    { identityMismatch = true; Report(rosterFailure + " Automatic gifts remain stopped."); }
                    var players = GamePlayerManagerAPI.Instance;
                    for (int player = 1; player <= 8; player++)
                    {
                        uint currentIdentity = players.TryGetPlayerResourcesById(player, out GamePlayerResources* data) && data != null ? data->r_LordUnitGlobalId : 0;
                        if (loadedIdentities != null && loadedIdentities[player] != currentIdentity) identityMismatch = true;
                    }
                    loadedIdentities = null; loadedHumanIdentities = null;
                    initializedMap = true;
                    if (expectedMask == 0 && !mission.Mode.IsRealMultiplayer && localPlayer >= 1 && localPlayer <= 8) expectedMask = 1 << localPlayer;
                    PromoteReadiness();
                    // Start on a future deterministic boundary; a loaded boundary is never repeated.
                    if (!state.HasProcessedTick) state.MarkProcessedTick(tick);
                    // This tick need not be a supply boundary. Wake Unity so the
                    // first final-slot Hello is not dependent on logging or input.
                    RequestPresentation();
                }
                if (!SimulationClock.TryGetElapsedTicks(state.LastProcessedTick, tick, out int elapsed) || elapsed == 0) return;
                state.MarkProcessedTick(tick);
                if (!identityMismatch && epochs.IsReady(expectedMask))
                {
                    var result = planner.Run(this, tick, epochs.EnabledMask & expectedMask);
                    if (result.UncertainTransfers > 0)
                    {
                        transferUncertain = identityMismatch = true;
                        Report("Supply stopped: a transfer had unequal or unknown stock changes. No retry was issued.");
                    }
                }
                // Copy bounded values under the simulation lock; Unity only sees these snapshots.
                if (unchecked((uint)tick) % AutoSendState.TicksPerSecond != 0) return;
                var selection = selectedContext;
                if (selection != null && selection.IsSendMode && IsSenderEnabled(selection.LocalPlayerId))
                {
                    int source = selection.LocalPlayerId, recipient = selection.RecipientPlayerId;
                    if (source >= 1 && source <= 8 && recipient >= 1 && recipient <= 8)
                    {
                        recipientReadiness[source, recipient] = ReadRecipientReadiness(source, recipient);
                        allies[source, recipient] = recipientReadiness[source, recipient] == AutoSendRecipientReadiness.AlliedLiving;
                        for (int g = 0; selection.Good.HasValue && g < AutoSendGoods.Values.Length; g++) if (AutoSendGoods.Values[g] == selection.Good.Value)
                        {
                            int good = (int)selection.Good.Value;
                            stocks[source, g] = GetStock(source, good); stocks[recipient, g] = GetStock(recipient, good);
                            capacities[recipient, g] = GetFreeCapacity(recipient, good);
                            break;
                        }
                    }
                }
            }
            RequestPresentation();
        }

        public int GetStock(int player, int good) => GamePlayerManagerAPI.Instance.GetGoodAmount(player, (eGoods)good);
        public int GetFreeCapacity(int player, int good) => AllyTransfers.TryGetCapacity(player, (eGoods)good,
            out var capacity, out _) ? capacity.FreeAmount : 0;
        public bool IsAlliedLiving(int sender, int recipient) => ReadRecipientReadiness(sender, recipient) == AutoSendRecipientReadiness.AlliedLiving;
        private unsafe AutoSendRecipientReadiness ReadRecipientReadiness(int sender, int recipient)
        {
            if (sender < 1 || sender > 8 || recipient < 1 || recipient > 8 || sender == recipient || epochs.Get(sender) == 0)
                return AutoSendRecipientReadiness.Unknown;
            var players = GamePlayerManagerAPI.Instance;
            if (!players.TryGetPlayerResourcesById(sender, out GamePlayerResources* source) || source == null ||
                !players.TryGetPlayerResourcesById(recipient, out GamePlayerResources* target) || target == null)
                return AutoSendRecipientReadiness.Unknown;
            if (source->r_LordUnitId == 0 || target->r_LordUnitId == 0 ||
                source->r_WinLossState == WinLossState.Loss || target->r_WinLossState == WinLossState.Loss)
                return AutoSendRecipientReadiness.NotLiving;
            return players.IsPlayerAlliedTo(sender, recipient) ? AutoSendRecipientReadiness.AlliedLiving : AutoSendRecipientReadiness.NotAllied;
        }
        public AutoSendTransferResult Transfer(int sender, int recipient, int good, int amount)
        {
            if (!IsSenderEnabled(sender)) return new AutoSendTransferResult(0, 0, true);
            bool showChat = state.ShowVanillaChatMessages(sender);
            // A missing suppression capability must never turn a silent gift into
            // an ordinary noisy gift. A known zero is safe and does not stop edits.
            if (!showChat && !AllyTransfers.CanSuppressVanillaChatMessages) return new AutoSendTransferResult(0, 0, true);
            var rule = state.GetRule(sender, recipient, good);
            AllyTransfers.TryExecute(new AllyTransferRequest(sender, recipient, (eGoods)good, amount,
                state.GetReserve(sender, good), rule.Maximum, showChat), out var result, out _);
            if (result == null || !result.OriginalWasCalled) return new AutoSendTransferResult(0, 0, true);
            if (result.Status == AllyTransferStatus.Faulted || result.Status == AllyTransferStatus.Uncertain)
                return new AutoSendTransferResult(0, 0, false);
            return result.SenderDebit.HasValue && result.RecipientCredit.HasValue &&
                result.SenderDebit >= 0 && result.SenderDebit <= int.MaxValue && result.RecipientCredit >= 0 && result.RecipientCredit <= int.MaxValue
                ? new AutoSendTransferResult((int)result.SenderDebit.Value, (int)result.RecipientCredit.Value, true)
                : new AutoSendTransferResult(0, 0, false);
        }

        internal void Edit(AutoSendEdit operation, int recipient, int good, int amount, bool enabled)
        {
            long session;
            lock (sync) session = mission?.SessionId ?? 0;
            EditCaptured(session, operation, recipient, good, amount, enabled);
        }
        internal void EditCaptured(long session, AutoSendEdit operation, int recipient, int good, int amount, bool enabled)
        {
            lock (sync)
            {
                if (mission == null || mission.SessionId != session || localPlayer < 1 || localPlayer > 8 || identityMismatch ||
                    !initializedMap || !epochs.IsReady(expectedMask) || !IsSenderEnabled(localPlayer) || !AllyTransfers.IsAvailable) return;
                if (state.GetRevision(localPlayer) == long.MaxValue)
                { feedback = "Revision range exhausted."; RequestPresentation(); return; }
                bool field = operation == AutoSendEdit.Reserve || operation == AutoSendEdit.Minimum || operation == AutoSendEdit.Maximum || operation == AutoSendEdit.Interval;
                bool senderWide = operation == AutoSendEdit.Paused || operation == AutoSendEdit.ChatMessages;
                bool toggle = operation == AutoSendEdit.Enabled || senderWide;
                if ((!field && !toggle) || amount < 0 || !senderWide && !AutoSendGoods.Contains(good)) return;
                if (operation == AutoSendEdit.ChatMessages &&
                    (selectedContext == null || selectedContext.LocalPlayerId != localPlayer || selectedContext.ViewedPlayerId != localPlayer)) return;
                if (operation == AutoSendEdit.Interval && (amount < AutoSendState.MinimumIntervalSeconds || amount > AutoSendState.MaximumIntervalSeconds)) return;
                if (toggle && edits.Pending) return;
                if (!senderWide && (recipient < 1 || recipient > 8 || recipient == localPlayer)) return;
                var intent = new AutoSendEditIntent(session, localPlayer, operation, recipient, good, amount, enabled);
                if (operation == AutoSendEdit.Enabled && enabled)
                {
                    int reserve = state.GetReserve(localPlayer, good); var rule = state.GetRule(localPlayer, recipient, good);
                    edits.Project(session, localPlayer, recipient, good, ref reserve, ref rule);
                    if (!AutoSendRuleEdits.TryApply(rule, operation, amount, enabled, out _, out string reason))
                    { feedback = reason; RequestPresentation(); return; }
                }
                edits.Enqueue(intent); feedback = string.Empty;
            }
            RequestPresentation();
        }

        private void SendNextEdit()
        {
            AutoSendPacket packet = null; AutoSendEditIntent intent;
            lock (sync)
            {
                if (mission == null || identityMismatch || !initializedMap || MultiplayerResyncEvents.IsInProgress ||
                    Volatile.Read(ref preparedRepair) != null || Volatile.Read(ref repairedRules) != null ||
                    !epochs.IsReady(expectedMask) ||
                    localPlayer < 1 || localPlayer > 8 || !IsSenderEnabled(localPlayer) || state.GetRevision(localPlayer) == long.MaxValue) return;
                if (!edits.TryBegin(mission.SessionId, localPlayer, localEpoch, state.GetRevision(localPlayer) + 1, out intent)) return;
                packet = new AutoSendPacket { Protocol = 4, Epoch = localEpoch, Revision = state.GetRevision(localPlayer) + 1,
                    Operation = (int)intent.Operation, Recipient = intent.Recipient, Good = intent.Good,
                    Amount = intent.Amount, Enabled = intent.Enabled };
                if (intent.Operation == AutoSendEdit.ChatMessages) packet.Amount = 0;
                else if (intent.Operation != AutoSendEdit.Reserve && intent.Operation != AutoSendEdit.Paused)
                {
                    var old = state.GetRule(intent.Player, intent.Recipient, intent.Good);
                    if (!AutoSendRuleEdits.TryApply(old, intent.Operation, intent.Amount, intent.Enabled, out var rule, out string reason))
                    { edits.Failed(intent); feedback = reason; RequestPresentation(); return; }
                    packet.Operation = (int)AutoSendEdit.SetRule;
                    packet.Minimum = rule.Minimum; packet.Maximum = rule.Maximum; packet.Enabled = rule.Enabled;
                    packet.Amount = rule.IntervalSeconds;
                }
            }
            // The native queue can deliver synchronously. The flight is already bound;
            // no engine/transport operation is made while holding our snapshot lock.
            var sent = channel.Send(packet);
            if (!sent.Submitted) lock (sync)
            {
                if (mission?.SessionId == intent.Session)
                { edits.Failed(intent); feedback = sent.Reason; }
            }
            RequestPresentation();
        }

        internal AutoSendDisplay ReadDisplay(AllyGoodsPanelContext context)
            => ReadDisplayCore(context, context?.Good.HasValue == true ? (int)context.Good.Value : 0, null);

        // The caller owns this UI-only buffer. Fill all markers under the same lock
        // as the numeric snapshot, including before Vanilla has selected a ware.
        internal AutoSendDisplay ReadDisplay(AllyGoodsPanelContext context, bool[] activeGoods)
        {
            if (activeGoods == null || activeGoods.Length != AutoSendGoods.Values.Length)
                throw new ArgumentException("A buffer for all supported goods is required.", nameof(activeGoods));
            return ReadDisplayCore(context, context?.Good.HasValue == true ? (int)context.Good.Value : 0, activeGoods);
        }

        private AutoSendDisplay ReadDisplayCore(AllyGoodsPanelContext context, int good, bool[] activeGoods)
        {
            lock (sync)
            {
                int sender = context?.LocalPlayerId ?? 0, recipient = context?.RecipientPlayerId ?? 0;
                bool pairSelected = sender >= 1 && sender <= 8 && recipient >= 1 && recipient <= 8;
                bool selected = pairSelected && AutoSendGoods.Contains(good);
                var result = new AutoSendDisplay { Selected = selected, Pending = edits.Pending, Feedback = feedback,
                    SessionId = mission?.SessionId ?? 0, SenderPlayerId = localPlayer,
                    TransferUnavailable = !AllyTransfers.IsAvailable,
                    ChatSuppressionUnavailable = !AllyTransfers.CanSuppressVanillaChatMessages,
                    ChatMessages = sender >= 1 && sender <= 8 && state.ShowVanillaChatMessages(sender),
                    RecipientReadiness = sender >= 1 && sender <= 8 && recipient >= 1 && recipient <= 8
                        ? recipientReadiness[sender, recipient] : AutoSendRecipientReadiness.Unknown,
                    SenderReady = IsSenderEnabled(sender) && mission != null && initializedMap && epochs.IsReady(expectedMask) && AllyTransfers.IsAvailable && !identityMismatch &&
                        sender == localPlayer && context.ViewedPlayerId == sender,
                    Ready = IsSenderEnabled(sender) && mission != null && initializedMap && epochs.IsReady(expectedMask) && AllyTransfers.IsAvailable && !identityMismatch && selected && sender == localPlayer &&
                        context.ViewedPlayerId == sender && allies[sender, recipient], IdentityMismatch = identityMismatch };
                if (activeGoods != null) Array.Clear(activeGoods, 0, activeGoods.Length);
                if (pairSelected && (selected || activeGoods != null))
                    for (int index = 0; index < state.GoodCount; index++)
                    {
                        int id = state.GetGood(index);
                        bool active = state.GetRule(sender, recipient, id).Enabled;
                        if (active) result.ActiveRules++;
                        if (activeGoods != null) activeGoods[index] = active;
                        if (selected && id == good)
                        { result.SourceStock = stocks[sender, index]; result.RecipientStock = stocks[recipient, index]; result.Capacity = capacities[recipient, index]; }
                    }
                if (!selected) return result;
                result.Reserve = state.GetReserve(sender, good); result.Rule = state.GetRule(sender, recipient, good);
                int desiredReserve = result.Reserve; var desiredRule = result.Rule;
                result.HasDesiredValues = edits.Project(result.SessionId, sender, recipient, good, ref desiredReserve, ref desiredRule);
                result.DesiredReserve = desiredReserve; result.DesiredRule = desiredRule;
                result.Paused = state.IsPaused(sender);
                return result;
            }
        }

        private unsafe bool Capture(VersionedSaveWriter writer)
        {
            lock (sync)
            {
                if (mission == null || !initializedMap || identityMismatch) return false;
                // Native Lords can spawn after our first tick. The native save writer
                // owns the stopped/locked world here; snapshot the identities it saves.
                var currentIdentities = new uint[9];
                var players = GamePlayerManagerAPI.Instance;
                for (int player = 1; player <= 8; player++)
                {
                    if (!players.TryGetPlayerResourcesById(player, out GamePlayerResources* data) || data == null) return false;
                    currentIdentities[player] = data->r_LordUnitGlobalId;
                }
                writer.Write("rules", AutoSendSaveCodec.Encode(Volatile.Read(ref repairedRules)?.State ?? state));
                writer.Write("players", AutoSendSavedPlayers.EncodePlayers(currentIdentities, humanIdentities)); return true;
            }
        }

        private void BeginRepair() { }

        private void EndRepair()
        {
            RepairedRules completed = Volatile.Read(ref preparedRepair);
            if (completed != null) Volatile.Write(ref repairedRules, completed);
            Volatile.Write(ref preparedRepair, null);
        }

        private byte[] CaptureRulesForRepair()
        {
            lock (sync)
            {
                if (mission?.Mode.IsRealMultiplayer != true || !initializedMap) return Array.Empty<byte>();
                return AutoSendSaveCodec.Encode(Volatile.Read(ref repairedRules)?.State ?? state);
            }
        }

        private RepairedRules PrepareRulesForRepair(ReadOnlyMemory<byte> bytes)
        {
            // The rule snapshot does not prove the outcome of a partially observed
            // native transaction. Prevent a shared resume if either peer saw one.
            if (transferUncertain) throw new InvalidDataException("AutoSend observed an uncertain native transfer before repair.");
            if (bytes.IsEmpty) return null;
            if (!AutoSendSaveCodec.TryDecode(bytes.ToArray(), AutoSendGoods.Ids(), AutoSendGoods.Limits(),
                out AutoSendState replacement, out string reason)) throw new InvalidDataException(reason);
            return new RepairedRules(replacement);
        }

        private void CommitRulesForRepair(RepairedRules replacement) => Volatile.Write(ref preparedRepair, replacement);

        // Called with sync held at the first simulation entry or ordinary save after repair.
        // The transport epochs and any edits still awaiting a Chore reply remain local.
        private void ApplyRepairedRules()
        {
            RepairedRules replacement = Interlocked.Exchange(ref repairedRules, null);
            if (replacement == null) return;
            state = replacement.State;
            planner = replacement.Planner;
        }

        private sealed class RepairedRules
        {
            internal RepairedRules(AutoSendState state) { State = state; Planner = new AutoSendPlanner(state); }
            internal readonly AutoSendState State;
            internal readonly AutoSendPlanner Planner;
        }
        private void Loaded(VersionedSaveLoad load)
        {
            lock (sync)
            {
                loadedState = null; loadedIdentities = null; loadedHumanIdentities = null; loadFailure = false;
                if (load.Status == VersionedSaveLoadStatus.Restored)
                {
                    loadFailure = true;
                    load.Entries.TryGetValue("rules", out byte[] data); load.Entries.TryGetValue("players", out byte[] ids);
                    if (!AutoSendSavedPlayers.TryRead(data, ids, AutoSendGoods.Ids(), AutoSendGoods.Limits(),
                        out var candidate, out var lords, out var humans, out string reason)) throw new InvalidDataException(reason);
                    loadedState = candidate; loadedIdentities = lords; loadedHumanIdentities = humans; loadFailure = false;
                }
                else if (load.Status != VersionedSaveLoadStatus.NoRecord && load.Status != VersionedSaveLoadStatus.OutOfScope) loadFailure = true;
            }
        }
        private void Present()
        {
            long currentSession;
            lock (sync) currentSession = mission?.SessionId ?? 0;
            settings.SetAutoSendMissionLocked(currentSession != 0);
            if (currentSession != 0 && channel.IsAvailable) Announce(currentSession);
            if (currentSession != 0 && channel.IsAvailable) AnnounceReady();
            if (currentSession != 0 && channel.IsAvailable) SendNextEdit();
            while (messages.TryDequeue(out string message)) log.LogInfo("[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "] AutoSend: " + message);
            view?.Refresh();
        }
        private void Report(string message) { if (messages.Count < 32) messages.Enqueue(message); RequestPresentation(); }
        private void SelectionChanged(AllyGoodsPanelContext context)
        {
            lock (sync)
            {
                selectedContext = context;
                if (!helloSubmitted) { helloAttempts = 0; nextHelloAttempt = 0; }
            }
            RequestPresentation();
        }
        private void RequestPresentation() => presentation?.Request();
        internal void RequestViewRefresh() => RequestPresentation();
    }
}
