using System;
using System.Collections.Concurrent;
using System.IO;
using System.Diagnostics;
using APIShared;
using APIShared.Economy;
using APIShared.Networking;
using APIShared.Presentation;
using APIShared.Savegames;
using APIShared.Simulation;
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace AutoSendTest
{
    // The engine owns simulation access; this lock only protects our save/presentation
    // snapshots. Unity never calls the engine or submits a Chore while holding it.
    internal sealed partial class AutoSendRuntime : IAutoSendWorld
    {
        private readonly object sync = new object();
        private readonly ModApiClient api;
        private readonly ManualLogSource log;
        private readonly ConcurrentQueue<string> messages = new ConcurrentQueue<string>();
        private AutoSendState state = AutoSendGoods.NewState(), loadedState;
        private AutoSendPlanner planner;
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
        private long localEpoch;
        private long initialAttempt;
        private bool hasInitialAttempt;
        private int localPlayer;
        private int expectedLocalPlayer;
        private int expectedMask;
        private readonly long[] readyEpoch = new long[9], readyProof = new long[9];
        private readonly int[] readyRoster = new int[9];
        private bool initializedMap, identityMismatch, firstTick, loadFailure, helloSubmitted, readySubmitted;
        private AllyGoodsPanelContext selectedContext;
        private int helloAttempts;
        private long nextHelloAttempt;
        private string feedback = string.Empty;
        internal AutoSendRuntime(ModApiClient api, ManualLogSource log) { this.api = api; this.log = log; }

        internal void Register()
        {
            channel = api.Network.RegisterAuthenticatedChore<AutoSendPacket>("edit-rule", Receive,
                new AuthenticatedChoreChannelOptions<AutoSendPacket> { ValidatePayload = ValidPacket,
                    Diagnostic = e => Report(e.Reason) });
            if (!api.Ui.TryRegisterBeforeRender("presentation", Present, out presentation, out string reason))
                throw new InvalidOperationException(reason);
            InitializeDiagnostics();
            view = new AutoSendView(this);
            if (!AllyGoodsPanelExtensions.TryRegisterSection(AutoSendPlugin.Guid, "auto-send", view.Create,
                out _, out reason, 100)) throw new InvalidOperationException(reason);
            if (!AllyGoodsPanelExtensions.TryRegisterGoodsAdornment(AutoSendPlugin.Guid, "active-goods", view.CreateAdornment,
                out _, out reason, 100)) throw new InvalidOperationException(reason);
            if (!AllyGoodsPanelExtensions.TrySubscribeContext(AutoSendPlugin.Guid, "selection", SelectionChanged,
                out reason)) throw new InvalidOperationException(reason);
            var save = new VersionedSaveDataRegistration(AutoSendPlugin.Guid, AutoSendPlugin.Guid + ".rules", 2, 2,
                new[] { "rules", "players" }, Capture, Loaded) { SimulationRelevant = true };
            if (!VersionedSaveData.TryRegister(save, out _, out reason)) throw new InvalidOperationException(reason);
            if (!api.TryGetMissionLifecycle(out missions, out var diagnostic) ||
                !missions.TryRegisterObserver("mission", Start, End, Initialization, out diagnostic))
                throw new InvalidOperationException(diagnostic.Reason);
            // The source is initialized after the global WhenReady publication. We register
            // from LibraryLoaded after APIShared's entire handler has returned.
            if (!SimulationTickEvents.TryRegister(AutoSendPlugin.Guid, "supply", Tick, out reason))
                throw new InvalidOperationException(reason);
        }

        private static bool ValidPacket(AutoSendPacket p)
        {
            if (p == null || p.Protocol != 2 || p.Epoch == 0 || p.Revision < 0 || p.Amount < 0 ||
                p.Minimum < 0 || p.Maximum < 0 || p.Recipient < 0 || p.Recipient > 8) return false;
            var operation = (AutoSendEdit)p.Operation;
            return operation == AutoSendEdit.Hello || operation == AutoSendEdit.Ready || operation == AutoSendEdit.Paused ||
                (operation == AutoSendEdit.Reserve || operation == AutoSendEdit.SetRule) && AutoSendGoods.Contains(p.Good);
        }

        private void Start(MissionLifecycleNotification note)
        {
            lock (sync)
            {
                mission = note.Context;
                state = loadedState ?? AutoSendGoods.NewState(); loadedState = null;
                planner = new AutoSendPlanner(state);
                epochs.Reset(); expectedLocalPlayer = expectedMask = 0;
                Array.Clear(readyEpoch, 0, readyEpoch.Length); Array.Clear(readyProof, 0, readyProof.Length); Array.Clear(readyRoster, 0, readyRoster.Length);
                initializedMap = firstTick = helloSubmitted = readySubmitted = false;
                edits.Reset();
                helloAttempts = 0; nextHelloAttempt = 0;
                identityMismatch = loadFailure;
                Array.Clear(stocks, 0, stocks.Length); Array.Clear(capacities, 0, capacities.Length); Array.Clear(allies, 0, allies.Length);
                Array.Clear(humanIdentities, 0, humanIdentities.Length);
                var roster = Platform_Multiplayer.Instance?.gameMembers;
                if (roster != null) foreach (var member in roster)
                    if (member != null && !member.kicked && !member.skirmishAI && member.playerID >= 1 && member.playerID <= 8)
                    {
                        humanIdentities[member.playerID] = member.steamID;
                        if (note.Context.Mode.IsRealMultiplayer && member.steamID == 0) identityMismatch = true;
                    }
                if (!initialRoster.TryGet(note.Context.SessionId, humanIdentities, out expectedMask) && note.Context.Mode.IsRealMultiplayer)
                { identityMismatch = true; Report("Initial participant roster unavailable; automatic gifts remain stopped."); }
                localPlayer = note.Context.LocalPlayerId.GetValueOrDefault();
                localEpoch = BitConverter.ToInt64(Guid.NewGuid().ToByteArray(), 0);
                if (localEpoch == 0) localEpoch = 1;
                feedback = string.Empty;
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
                { initialAttempt = note.Context.SessionId; hasInitialAttempt = true; initialRoster.Reset(initialAttempt); }
                if (note.Phase != MissionInitializationPhase.NativeLoaded) return;
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
                loadedState = null; loadedIdentities = null; loadedHumanIdentities = null; loadFailure = false;
                EndDiagnostics();
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
                if (mission == null || mission.SessionId != session || identityMismatch || helloSubmitted || epochs.IsReady(expectedMask)) return;
                expectedLocalPlayer = participant;
                long now = Stopwatch.GetTimestamp();
                if (now < nextHelloAttempt) return;
                if (helloAttempts == 0) helloAttempts = 1; else helloAttempts = 2;
                nextHelloAttempt = now + Stopwatch.Frequency;
                hello = new AutoSendPacket { Protocol = 2, Epoch = localEpoch, Operation = (int)AutoSendEdit.Hello };
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
                if (mission == null || readySubmitted || !epochs.AllKnown(expectedMask) || localPlayer < 1 || localPlayer > 8 ||
                    epochs.Get(localPlayer) != localEpoch || (expectedMask & (1 << localPlayer)) == 0) return;
                packet = new AutoSendPacket { Protocol = 2, Epoch = localEpoch, Operation = (int)AutoSendEdit.Ready,
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
                if (mission == null) return;
                if (!AllyTransfers.IsAvailable) return;
                int sender = message.SenderPlayerId;
                var p = message.Packet;
                if ((AutoSendEdit)p.Operation == AutoSendEdit.Hello)
                {
                    // The native queue is retired at map load. Within this map, each
                    // authenticated player may bind exactly one fresh command epoch.
                    if (epochs.Bind(sender, p.Epoch, expectedLocalPlayer, localEpoch, out bool localAcknowledged) && localAcknowledged)
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
                string reason = string.Empty;
                bool accepted = false;
                var operation = (AutoSendEdit)p.Operation;
                if (state.GetRevision(sender) == long.MaxValue || p.Revision != state.GetRevision(sender) + 1)
                    reason = "The rule revision changed before this command was applied.";
                else if (operation == AutoSendEdit.Paused) accepted = state.TrySetPaused(sender, p.Enabled, out reason);
                else if (operation == AutoSendEdit.Reserve) accepted = state.TrySetReserve(sender, p.Good, p.Amount, out reason);
                else if (operation == AutoSendEdit.SetRule && p.Recipient != sender && IsAlliedLiving(sender, p.Recipient))
                {
                    accepted = state.TrySetRule(sender, p.Recipient, p.Good, p.Minimum, p.Maximum, p.Enabled, out reason);
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
                if (!firstTick) { firstTick = true; Report("First native mission tick reached; startup cleanup survived."); }
                if (!AllyTransfers.IsAvailable) return;
                TickDiagnostics(tick);
                if (!initializedMap)
                {
                    var players = GamePlayerManagerAPI.Instance;
                    for (int player = 1; player <= 8; player++)
                    {
                        uint currentIdentity = players.TryGetPlayerResourcesById(player, out GamePlayerResources* data) && data != null ? data->r_LordUnitGlobalId : 0;
                        if (loadedIdentities != null && loadedIdentities[player] != currentIdentity) identityMismatch = true;
                        if (loadedHumanIdentities != null && loadedHumanIdentities[player] != humanIdentities[player]) identityMismatch = true;
                    }
                    loadedIdentities = null; loadedHumanIdentities = null;
                    initializedMap = true;
                    if (expectedMask == 0 && !mission.Mode.IsRealMultiplayer && localPlayer >= 1 && localPlayer <= 8) expectedMask = 1 << localPlayer;
                    PromoteReadiness();
                    // Start on a future deterministic boundary; a loaded boundary is never repeated.
                    if (!state.HasProcessedTick) state.MarkProcessedTick(tick);
                }
                if (!SimulationClock.TryGetElapsedTicks(state.LastProcessedTick, tick, out int elapsed) || elapsed == 0 || unchecked((uint)tick) % 40 != 0) return;
                state.MarkProcessedTick(tick);
                if (!identityMismatch && epochs.IsReady(expectedMask))
                {
                    BeforeDiagnosticRound(tick);
                    var result = planner.Run(this);
                    AfterDiagnosticRound();
                    if (result.UncertainTransfers > 0)
                    { identityMismatch = true; Report("Supply stopped: a transfer had unequal or unknown stock changes. No retry was issued."); }
                }
                // Copy bounded values under the simulation lock; Unity only sees these snapshots.
                var selection = selectedContext;
                if (selection != null && selection.IsSendMode && selection.Good.HasValue)
                {
                    int source = selection.LocalPlayerId, recipient = selection.RecipientPlayerId;
                    if (source >= 1 && source <= 8 && recipient >= 1 && recipient <= 8)
                    {
                        for (int g = 0; g < AutoSendGoods.Values.Length; g++) if (AutoSendGoods.Values[g] == selection.Good.Value)
                        {
                            int good = (int)selection.Good.Value;
                            stocks[source, g] = GetStock(source, good); stocks[recipient, g] = GetStock(recipient, good);
                            capacities[recipient, g] = GetFreeCapacity(recipient, good);
                            allies[source, recipient] = IsAlliedLiving(source, recipient);
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
        public unsafe bool IsAlliedLiving(int sender, int recipient)
        {
            if (sender < 1 || sender > 8 || recipient < 1 || recipient > 8 || sender == recipient || epochs.Get(sender) == 0) return false;
            var players = GamePlayerManagerAPI.Instance;
            return players.TryGetPlayerResourcesById(sender, out GamePlayerResources* source) && source != null &&
                players.TryGetPlayerResourcesById(recipient, out GamePlayerResources* target) && target != null &&
                source->r_LordUnitId != 0 && target->r_LordUnitId != 0 &&
                source->r_WinLossState != WinLossState.Loss && target->r_WinLossState != WinLossState.Loss &&
                players.IsPlayerAlliedTo(sender, recipient);
        }
        public AutoSendTransferResult Transfer(int sender, int recipient, int good, int amount)
        {
            var rule = state.GetRule(sender, recipient, good);
            BeforeDiagnosticTransfer(sender, recipient, good, amount);
            AllyTransfers.TryExecute(new AllyTransferRequest(sender, recipient, (eGoods)good, amount,
                state.GetReserve(sender, good), rule.Maximum), out var result, out _);
            AfterDiagnosticTransfer(sender, recipient, good, amount, result);
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
                    !initializedMap || !epochs.IsReady(expectedMask) || !AllyTransfers.IsAvailable) return;
                if (state.GetRevision(localPlayer) == long.MaxValue)
                { feedback = "Revision range exhausted."; RequestPresentation(); return; }
                bool field = operation == AutoSendEdit.Reserve || operation == AutoSendEdit.Minimum || operation == AutoSendEdit.Maximum;
                bool toggle = operation == AutoSendEdit.Enabled || operation == AutoSendEdit.Paused;
                if ((!field && !toggle) || amount < 0 || operation != AutoSendEdit.Paused && !AutoSendGoods.Contains(good)) return;
                if (toggle && edits.Pending) return;
                if (operation != AutoSendEdit.Paused && (recipient < 1 || recipient > 8 || recipient == localPlayer)) return;
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
                if (mission == null || identityMismatch || !initializedMap || !epochs.IsReady(expectedMask) ||
                    localPlayer < 1 || localPlayer > 8 || state.GetRevision(localPlayer) == long.MaxValue) return;
                if (!edits.TryBegin(mission.SessionId, localPlayer, localEpoch, state.GetRevision(localPlayer) + 1, out intent)) return;
                packet = new AutoSendPacket { Protocol = 2, Epoch = localEpoch, Revision = state.GetRevision(localPlayer) + 1,
                    Operation = (int)intent.Operation, Recipient = intent.Recipient, Good = intent.Good,
                    Amount = intent.Amount, Enabled = intent.Enabled };
                if (intent.Operation != AutoSendEdit.Reserve && intent.Operation != AutoSendEdit.Paused)
                {
                    var old = state.GetRule(intent.Player, intent.Recipient, intent.Good);
                    if (!AutoSendRuleEdits.TryApply(old, intent.Operation, intent.Amount, intent.Enabled, out var rule, out string reason))
                    { edits.Failed(intent); feedback = reason; RequestPresentation(); return; }
                    packet.Operation = (int)AutoSendEdit.SetRule;
                    packet.Minimum = rule.Minimum; packet.Maximum = rule.Maximum; packet.Enabled = rule.Enabled;
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
            => ReadDisplayCore(context, context?.Good.HasValue == true ? (int)context.Good.Value : 0);

        internal AutoSendDisplay ReadDisplayForGood(AllyGoodsPanelContext context, eGoods good)
            => ReadDisplayCore(context, (int)good);

        private AutoSendDisplay ReadDisplayCore(AllyGoodsPanelContext context, int good)
        {
            lock (sync)
            {
                int sender = context?.LocalPlayerId ?? 0, recipient = context?.RecipientPlayerId ?? 0;
                bool selected = sender >= 1 && sender <= 8 && recipient >= 1 && recipient <= 8 && AutoSendGoods.Contains(good);
                var result = new AutoSendDisplay { Selected = selected, Pending = edits.Pending, Feedback = feedback,
                    SessionId = mission?.SessionId ?? 0, SenderPlayerId = localPlayer,
                    TransferUnavailable = !AllyTransfers.IsAvailable,
                    Ready = mission != null && initializedMap && epochs.IsReady(expectedMask) && AllyTransfers.IsAvailable && !identityMismatch && selected && sender == localPlayer &&
                        context.ViewedPlayerId == sender && allies[sender, recipient], IdentityMismatch = identityMismatch };
                if (!selected) return result;
                result.Reserve = state.GetReserve(sender, good); result.Rule = state.GetRule(sender, recipient, good);
                int desiredReserve = result.Reserve; var desiredRule = result.Rule;
                result.HasDesiredValues = edits.Project(result.SessionId, sender, recipient, good, ref desiredReserve, ref desiredRule);
                result.DesiredReserve = desiredReserve; result.DesiredRule = desiredRule;
                result.Paused = state.IsPaused(sender);
                for (int g = 0; g < state.GoodCount; g++)
                {
                    int id = state.GetGood(g);
                    if (state.GetRule(sender, recipient, id).Enabled) result.ActiveRules++;
                    if (id == good)
                    { result.SourceStock = stocks[sender, g]; result.RecipientStock = stocks[recipient, g]; result.Capacity = capacities[recipient, g]; }
                }
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
                writer.Write("rules", AutoSendSaveCodec.Encode(state));
                writer.Write("players", AutoSendSavedPlayers.EncodePlayers(currentIdentities, humanIdentities)); return true;
            }
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
            PresentDiagnostics();
            long currentSession;
            lock (sync) currentSession = mission?.SessionId ?? 0;
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
        partial void InitializeDiagnostics();
        partial void EndDiagnostics();
        partial void TickDiagnostics(int tick);
        partial void BeforeDiagnosticRound(int tick);
        partial void AfterDiagnosticRound();
        partial void BeforeDiagnosticTransfer(int sender, int recipient, int good, int amount);
        partial void AfterDiagnosticTransfer(int sender, int recipient, int good, int amount, AllyTransferResult result);
        partial void PresentDiagnostics();
    }
    internal sealed class AutoSendDisplay
    {
        internal long SessionId;
        internal int SenderPlayerId, DesiredReserve;
        internal AutoSendRule DesiredRule;
        internal bool HasDesiredValues;
        internal bool Selected, Ready, Pending, Paused, IdentityMismatch, TransferUnavailable;
        internal int Reserve, SourceStock, RecipientStock, Capacity, ActiveRules;
        internal AutoSendRule Rule;
        internal string Feedback;
    }
}
