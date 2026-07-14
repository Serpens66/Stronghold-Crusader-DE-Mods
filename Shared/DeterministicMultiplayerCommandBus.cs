using BepInEx.Logging;
using MessagePack;
using MessagePack.Formatters;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.EventAPI.Network;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace Shared
{
    /// <summary>
    /// Wire-level messages used by <see cref="DeterministicMultiplayerCommandBus"/>.
    /// A single explicitly formatted envelope keeps packet registration stable and lets
    /// any mod multiplex multiple deterministic command channels over one packet type.
    /// </summary>
    public enum DeterministicCommandMessageKind
    {
        Request = 1,
        Prepare = 2,
        Acknowledge = 3,
        Commit = 4,
        Cancel = 5,
        StateFingerprint = 6
    }

    [MessagePackObject]
    [MessagePackFormatter(typeof(DeterministicCommandEnvelopeFormatter))]
    public sealed class DeterministicCommandEnvelope
    {
        [Key(0)] public string ScopeId { get; set; }
        [Key(1)] public int Generation { get; set; }
        [Key(2)] public DeterministicCommandMessageKind Kind { get; set; }
        [Key(3)] public string ChannelId { get; set; }
        [Key(4)] public int SourcePlayerId { get; set; }
        [Key(5)] public int RequestId { get; set; }
        [Key(6)] public long Sequence { get; set; }
        [Key(7)] public int ActorPlayerId { get; set; }
        [Key(8)] public int ExecutionTick { get; set; }
        [Key(9)] public byte[] Payload { get; set; }
        [Key(10)] public string Reason { get; set; }
    }

    public sealed class DeterministicCommandEnvelopeFormatter : IMessagePackFormatter<DeterministicCommandEnvelope>
    {
        private const int FieldCount = 11;

        public void Serialize(ref MessagePackWriter writer, DeterministicCommandEnvelope value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteArrayHeader(FieldCount);
            writer.Write(value.ScopeId);
            writer.Write(value.Generation);
            writer.Write((int)value.Kind);
            writer.Write(value.ChannelId);
            writer.Write(value.SourcePlayerId);
            writer.Write(value.RequestId);
            writer.Write(value.Sequence);
            writer.Write(value.ActorPlayerId);
            writer.Write(value.ExecutionTick);
            writer.Write(value.Payload);
            writer.Write(value.Reason);
        }

        public DeterministicCommandEnvelope Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return null;

            int count = reader.ReadArrayHeader();
            DeterministicCommandEnvelope value = new DeterministicCommandEnvelope();
            for (int i = 0; i < count; i++)
            {
                switch (i)
                {
                    case 0:
                        value.ScopeId = reader.ReadString();
                        break;
                    case 1:
                        value.Generation = reader.ReadInt32();
                        break;
                    case 2:
                        value.Kind = (DeterministicCommandMessageKind)reader.ReadInt32();
                        break;
                    case 3:
                        value.ChannelId = reader.ReadString();
                        break;
                    case 4:
                        value.SourcePlayerId = reader.ReadInt32();
                        break;
                    case 5:
                        value.RequestId = reader.ReadInt32();
                        break;
                    case 6:
                        value.Sequence = reader.ReadInt64();
                        break;
                    case 7:
                        value.ActorPlayerId = reader.ReadInt32();
                        break;
                    case 8:
                        value.ExecutionTick = reader.ReadInt32();
                        break;
                    case 9:
                    {
                        ReadOnlySequence<byte>? payload = reader.ReadBytes();
                        value.Payload = payload.HasValue ? payload.Value.ToArray() : null;
                        break;
                    }
                    case 10:
                        value.Reason = reader.ReadString();
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            return value;
        }
    }

    public readonly struct DeterministicCommandContext
    {
        public DeterministicCommandContext(
            string channelId,
            int sourcePlayerId,
            int requestId,
            long sequence,
            int executionTick,
            bool isLocalSource)
        {
            ChannelId = channelId;
            SourcePlayerId = sourcePlayerId;
            RequestId = requestId;
            Sequence = sequence;
            ExecutionTick = executionTick;
            IsLocalSource = isLocalSource;
        }

        public string ChannelId { get; }
        public int SourcePlayerId { get; }
        public int RequestId { get; }
        public long Sequence { get; }
        public int ExecutionTick { get; }
        public bool IsLocalSource { get; }
    }

    /// <summary>
    /// Synchronizes mod commands without mutating simulation state in packet callbacks.
    /// The host assigns a total sequence, waits until every connected human peer has
    /// prepared the command, then commits it for the same future simulation tick.
    /// </summary>
    internal sealed class DeterministicMultiplayerCommandBus : IDisposable
    {
        private const int DefaultExecutionLeadTicks = 16;
        private const int DefaultPrepareTimeoutTicks = 1200;
        private const int MaximumFingerprintPayloadBytes = 16 * 1024;

        private readonly ManualLogSource log;
        private readonly string scopeId;
        private readonly int executionLeadTicks;
        private readonly int prepareTimeoutTicks;
        private readonly object stateLock = new object();
        private readonly Dictionary<string, CommandChannel> channels = new Dictionary<string, CommandChannel>(StringComparer.Ordinal);
        private readonly Dictionary<long, PreparedCommand> preparedCommands = new Dictionary<long, PreparedCommand>();
        private readonly Dictionary<long, HostPendingCommand> hostPendingCommands = new Dictionary<long, HostPendingCommand>();
        private readonly Dictionary<RequestKey, long> hostRequestSequences = new Dictionary<RequestKey, long>();
        private readonly Dictionary<StateFingerprintKey, HostStateFingerprintSet> hostStateFingerprints = new Dictionary<StateFingerprintKey, HostStateFingerprintSet>();

        private R3PacketEventHook<DeterministicCommandEnvelope> packetHook;
        private IDisposable packetSubscription;
        private IDisposable mapUnloadSubscription;
        private int generation;
        private int nextRequestId;
        private long nextHostSequence;
        private int lastProcessedTick = int.MinValue;
        private bool initialized;
        private bool disposed;

        public DeterministicMultiplayerCommandBus(
            ManualLogSource log,
            string scopeId,
            int executionLeadTicks = DefaultExecutionLeadTicks,
            int prepareTimeoutTicks = DefaultPrepareTimeoutTicks)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            if (string.IsNullOrWhiteSpace(scopeId))
                throw new ArgumentException("A non-empty synchronization scope is required.", nameof(scopeId));
            if (executionLeadTicks < 1)
                throw new ArgumentOutOfRangeException(nameof(executionLeadTicks));
            if (prepareTimeoutTicks <= executionLeadTicks)
                throw new ArgumentOutOfRangeException(nameof(prepareTimeoutTicks));

            this.scopeId = scopeId;
            this.executionLeadTicks = executionLeadTicks;
            this.prepareTimeoutTicks = prepareTimeoutTicks;
        }

        public void RegisterChannel<TCommand>(
            string channelId,
            Func<TCommand, string> validate,
            Func<TCommand, DeterministicCommandContext, bool> execute)
            where TCommand : class
        {
            if (string.IsNullOrWhiteSpace(channelId))
                throw new ArgumentException("A non-empty command channel is required.", nameof(channelId));
            if (execute == null)
                throw new ArgumentNullException(nameof(execute));

            lock (stateLock)
            {
                if (channels.ContainsKey(channelId))
                    throw new InvalidOperationException($"The deterministic command channel '{channelId}' is already registered.");

                channels.Add(channelId, new CommandChannel<TCommand>(channelId, validate, execute));
            }

            DebugLogHelper.LogInfo(
                log,
                $"Deterministic channel registered: scope={scopeId}, channel={channelId}, commandType={typeof(TCommand).FullName}.");
        }

        public void UnregisterChannel(string channelId)
        {
            if (string.IsNullOrEmpty(channelId))
                return;

            bool removed;
            lock (stateLock)
                removed = channels.Remove(channelId);

            DebugLogHelper.LogInfo(
                log,
                $"Deterministic channel unregistered: scope={scopeId}, channel={channelId}, removed={removed}.");
        }

        public void Initialize()
        {
            if (initialized)
                return;

            disposed = false;
            if (UnityMainThreadDispatcher.Instance == null)
                throw new InvalidOperationException("The Script Extender main-thread dispatcher could not be initialized.");

            packetHook = GameNetworkAPI.Instance.GetPacketEventFor<DeterministicCommandEnvelope>();
            packetSubscription = packetHook.GetBaseHook().Observable.Subscribe(OnPacketReceived);
            mapUnloadSubscription = MapLoaderR3EventHooks.OnUnloadMap.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(_ => ResetMapState("map unload"));
            GameTimeManagerAPI.Instance.GetFrameProvider().OnGameTick += OnGameTick;
            initialized = true;
            DebugLogHelper.LogInfo(
                log,
                $"Deterministic command bus initialized: scope={scopeId}, packetId={packetHook.GetPacketId()}, leadTicks={executionLeadTicks}, prepareTimeoutTicks={prepareTimeoutTicks}, generation={generation}, currentTick={GetCurrentGameTick()}, networkApiEnvironment={GameNetworkAPI.IsNetworkedEnvironment()}, activeMultiplayerGame={IsActiveMultiplayerGame()}, localHost={GameNetworkAPI.IsLocalHost()}, localPlayer={GameNetworkAPI.GetLocalPlayerId()}, members={BuildMemberSummary()}.");
        }

        public int ReserveRequestId()
        {
            lock (stateLock)
            {
                if (nextRequestId == int.MaxValue)
                    nextRequestId = 0;

                return ++nextRequestId;
            }
        }

        public bool Submit<TCommand>(string channelId, int requestId, TCommand command)
            where TCommand : class
        {
            bool activeMultiplayerGame = IsActiveMultiplayerGame();
            DebugLogHelper.LogInfo(
                log,
                $"Deterministic command submission started: scope={scopeId}, channel={channelId}, request={requestId}, commandType={typeof(TCommand).FullName}, initialized={initialized}, disposed={disposed}, currentTick={GetCurrentGameTick()}, networkApiEnvironment={GameNetworkAPI.IsNetworkedEnvironment()}, activeMultiplayerGame={activeMultiplayerGame}, localHost={GameNetworkAPI.IsLocalHost()}, localPlayer={GameNetworkAPI.GetLocalPlayerId()}, members={BuildMemberSummary()}.");

            if (!initialized || disposed)
            {
                DebugLogHelper.LogError(log, $"Deterministic command submission rejected because scope '{scopeId}' is not active.");
                return false;
            }
            if (requestId <= 0)
            {
                DebugLogHelper.LogError(log, $"Deterministic command submission rejected: channel={channelId}, invalid requestId={requestId}.");
                return false;
            }

            int sourcePlayerId = GameNetworkAPI.GetLocalPlayerId();
            if (sourcePlayerId <= 0)
            {
                DebugLogHelper.LogError(log, $"Deterministic command submission rejected: channel={channelId}, local player id is unavailable.");
                return false;
            }

            CommandChannel channel;
            int currentGeneration;
            lock (stateLock)
            {
                if (!channels.TryGetValue(channelId, out channel))
                {
                    DebugLogHelper.LogError(log, $"Deterministic command submission rejected: unregistered channel={channelId}.");
                    return false;
                }

                currentGeneration = generation;
            }

            if (channel.CommandType != typeof(TCommand))
            {
                DebugLogHelper.LogError(
                    log,
                    $"Deterministic command submission rejected: channel={channelId}, expectedType={channel.CommandType.FullName}, actualType={typeof(TCommand).FullName}.");
                return false;
            }

            string validationFailure = channel.Validate(command);
            if (validationFailure != null)
            {
                DebugLogHelper.LogWarning(
                    log,
                    $"Deterministic command submission rejected: channel={channelId}, source={sourcePlayerId}, request={requestId}, reason={validationFailure}.");
                return false;
            }

            byte[] payload;
            try
            {
                payload = channel.Serialize(command);
            }
            catch (Exception ex)
            {
                DebugLogHelper.LogError(
                    log,
                    $"Deterministic command serialization failed: channel={channelId}, source={sourcePlayerId}, request={requestId}, error={ex}.");
                return false;
            }

            DeterministicCommandEnvelope request = new DeterministicCommandEnvelope
            {
                ScopeId = scopeId,
                Generation = currentGeneration,
                Kind = DeterministicCommandMessageKind.Request,
                ChannelId = channelId,
                SourcePlayerId = sourcePlayerId,
                RequestId = requestId,
                Payload = payload
            };

            DebugLogHelper.LogInfo(
                log,
                $"Deterministic command payload prepared: scope={scopeId}, generation={currentGeneration}, channel={channelId}, source={sourcePlayerId}, request={requestId}, payloadBytes={payload.Length}.");

            if (!activeMultiplayerGame)
            {
                DebugLogHelper.LogInfo(
                    log,
                    $"Deterministic command uses the singleplayer path because no active in-game multiplayer membership was found: scope={scopeId}, generation={currentGeneration}, channel={channelId}, source={sourcePlayerId}, request={requestId}, networkApiEnvironment={GameNetworkAPI.IsNetworkedEnvironment()}, members={BuildMemberSummary()}.");
                ScheduleSingleplayerCommand(request, channel, command);
                return true;
            }

            if (GameNetworkAPI.IsLocalHost())
            {
                DebugLogHelper.LogInfo(
                    log,
                    $"Deterministic command routed directly to local host sequencer: scope={scopeId}, generation={currentGeneration}, channel={channelId}, source={sourcePlayerId}, request={requestId}.");
                HandleHostRequest(request);
                return true;
            }

            int hostPlayerId = FindHostPlayerId();
            if (hostPlayerId <= 0)
            {
                DebugLogHelper.LogError(
                    log,
                    $"Deterministic command submission rejected: channel={channelId}, source={sourcePlayerId}, request={requestId}, host player id was not found.");
                return false;
            }

            return TrySendPacketToPlayer(hostPlayerId, request, "request-to-host");
        }

        /// <summary>
        /// Reports a deterministic state snapshot for a command checkpoint. In multiplayer,
        /// every peer sends its snapshot to the host, which compares all reports automatically.
        /// The report is diagnostic only and never mutates simulation state.
        /// </summary>
        public bool ReportStateFingerprint(
            DeterministicCommandContext context,
            string checkpoint,
            string fingerprint)
        {
            if (!initialized || disposed)
            {
                DebugLogHelper.LogWarning(
                    log,
                    $"Deterministic state fingerprint ignored because scope '{scopeId}' is not active: channel={context.ChannelId}, sequence={context.Sequence}, checkpoint={checkpoint ?? "null"}.");
                return false;
            }
            if (string.IsNullOrWhiteSpace(context.ChannelId) || context.RequestId <= 0 || context.Sequence <= 0 ||
                string.IsNullOrWhiteSpace(checkpoint) || fingerprint == null)
            {
                DebugLogHelper.LogWarning(
                    log,
                    $"Invalid deterministic state fingerprint ignored: scope={scopeId}, channel={context.ChannelId ?? "null"}, request={context.RequestId}, sequence={context.Sequence}, checkpoint={checkpoint ?? "null"}.");
                return false;
            }

            byte[] payload = Encoding.UTF8.GetBytes(fingerprint);
            if (payload.Length > MaximumFingerprintPayloadBytes)
            {
                DebugLogHelper.LogError(
                    log,
                    $"Deterministic state fingerprint exceeds the payload limit: scope={scopeId}, channel={context.ChannelId}, request={context.RequestId}, sequence={context.Sequence}, checkpoint={checkpoint}, payloadBytes={payload.Length}, limit={MaximumFingerprintPayloadBytes}.");
                return false;
            }

            int localPlayerId = GameNetworkAPI.GetLocalPlayerId();
            int currentGeneration;
            lock (stateLock)
                currentGeneration = generation;

            DebugLogHelper.LogInfo(
                log,
                $"Deterministic state fingerprint captured: scope={scopeId}, generation={currentGeneration}, channel={context.ChannelId}, source={context.SourcePlayerId}, request={context.RequestId}, sequence={context.Sequence}, executeTick={context.ExecutionTick}, checkpoint={checkpoint}, reportingPlayer={localPlayerId}, currentTick={GetCurrentGameTick()}, payloadBytes={payload.Length}, digest={ComputeFingerprintDigest(fingerprint)}, fingerprint={fingerprint}.");

            if (!IsActiveMultiplayerGame())
                return true;
            if (localPlayerId <= 0)
            {
                DebugLogHelper.LogError(
                    log,
                    $"Deterministic state fingerprint could not be reported because the local player id is unavailable: scope={scopeId}, channel={context.ChannelId}, sequence={context.Sequence}, checkpoint={checkpoint}.");
                return false;
            }

            DeterministicCommandEnvelope report = new DeterministicCommandEnvelope
            {
                ScopeId = scopeId,
                Generation = currentGeneration,
                Kind = DeterministicCommandMessageKind.StateFingerprint,
                ChannelId = context.ChannelId,
                SourcePlayerId = context.SourcePlayerId,
                RequestId = context.RequestId,
                Sequence = context.Sequence,
                ActorPlayerId = localPlayerId,
                ExecutionTick = context.ExecutionTick,
                Payload = payload,
                Reason = checkpoint
            };

            if (GameNetworkAPI.IsLocalHost())
            {
                HandleStateFingerprint(report);
                return true;
            }

            int hostPlayerId = FindHostPlayerId();
            if (hostPlayerId <= 0)
            {
                DebugLogHelper.LogError(
                    log,
                    $"Deterministic state fingerprint could not be reported because the host player id was not found: scope={scopeId}, channel={context.ChannelId}, sequence={context.Sequence}, checkpoint={checkpoint}.");
                return false;
            }

            return TrySendPacketToPlayer(hostPlayerId, report, "state-fingerprint-to-host");
        }

        public void ResetMapState(string reason)
        {
            int oldGeneration;
            int newGeneration;
            int preparedCount;
            int pendingCount;
            int fingerprintCount;
            lock (stateLock)
            {
                oldGeneration = generation;
                generation = generation == int.MaxValue ? 0 : generation + 1;
                newGeneration = generation;
                preparedCount = preparedCommands.Count;
                pendingCount = hostPendingCommands.Count;
                fingerprintCount = hostStateFingerprints.Count;
                preparedCommands.Clear();
                hostPendingCommands.Clear();
                hostRequestSequences.Clear();
                hostStateFingerprints.Clear();
                nextRequestId = 0;
                nextHostSequence = 0;
                lastProcessedTick = int.MinValue;
            }

            DebugLogHelper.LogInfo(
                log,
                $"Deterministic command state reset: scope={scopeId}, reason={reason ?? "unspecified"}, generation={oldGeneration}->{newGeneration}, prepared={preparedCount}, hostPending={pendingCount}, fingerprintSets={fingerprintCount}.");
        }

        private void AdoptHostGeneration(int hostGeneration, long sequence, int previousGeneration)
        {
            int preparedCount;
            int pendingCount;
            int fingerprintCount;
            lock (stateLock)
            {
                preparedCount = preparedCommands.Count;
                pendingCount = hostPendingCommands.Count;
                fingerprintCount = hostStateFingerprints.Count;
                preparedCommands.Clear();
                hostPendingCommands.Clear();
                hostRequestSequences.Clear();
                hostStateFingerprints.Clear();
                nextHostSequence = 0;
                lastProcessedTick = int.MinValue;
                generation = hostGeneration;
            }

            DebugLogHelper.LogWarning(
                log,
                $"Deterministic client adopted authoritative host generation: scope={scopeId}, generation={previousGeneration}->{hostGeneration}, triggeringPrepareSequence={sequence}, clearedPrepared={preparedCount}, clearedHostPending={pendingCount}, clearedFingerprintSets={fingerprintCount}, currentTick={GetCurrentGameTick()}.");
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            initialized = false;
            GameTimeManagerAPI.Instance.GetFrameProvider().OnGameTick -= OnGameTick;
            packetSubscription?.Dispose();
            mapUnloadSubscription?.Dispose();
            packetSubscription = null;
            mapUnloadSubscription = null;
            packetHook = null;
            lock (stateLock)
            {
                preparedCommands.Clear();
                hostPendingCommands.Clear();
                hostRequestSequences.Clear();
                hostStateFingerprints.Clear();
            }
        }

        private void OnPacketReceived(ReceiveCustomPacketEventArgs<DeterministicCommandEnvelope> args)
        {
            DeterministicCommandEnvelope packet = args.Packet;
            if (packet == null || !string.Equals(packet.ScopeId, scopeId, StringComparison.Ordinal))
                return;

            DebugLogHelper.LogInfo(
                log,
                $"Deterministic packet callback received and queued: scope={scopeId}, packetId={args.PacketId}, phase={args.Phase}, kind={packet.Kind}, generation={packet.Generation}, channel={packet.ChannelId}, source={packet.SourcePlayerId}, request={packet.RequestId}, sequence={packet.Sequence}, actor={packet.ActorPlayerId}, executeTick={packet.ExecutionTick}, payloadBytes={packet.Payload?.Length ?? 0}.");

            // Packet callbacks can run from the game's receive path. They only enqueue;
            // all protocol decisions and network sends happen on Unity's main thread.
            UnityMainThreadDispatcher.EnqueueStatic(() => ProcessPacketOnMainThread(packet));
        }

        private void ProcessPacketOnMainThread(DeterministicCommandEnvelope packet)
        {
            if (disposed || !initialized)
                return;

            int currentGeneration;
            lock (stateLock)
                currentGeneration = generation;

            bool localHost = GameNetworkAPI.IsLocalHost();
            DebugLogHelper.LogInfo(
                log,
                $"Deterministic packet processing started on main thread: scope={scopeId}, kind={packet.Kind}, packetGeneration={packet.Generation}, currentGeneration={currentGeneration}, channel={packet.ChannelId}, source={packet.SourcePlayerId}, request={packet.RequestId}, sequence={packet.Sequence}, localHost={localHost}, localPlayer={GameNetworkAPI.GetLocalPlayerId()}, currentTick={GetCurrentGameTick()}.");

            if (packet.Kind == DeterministicCommandMessageKind.Request)
            {
                if (!localHost)
                {
                    DebugLogHelper.LogWarning(
                        log,
                        $"Deterministic request ignored on non-host: scope={scopeId}, channel={packet.ChannelId}, source={packet.SourcePlayerId}, request={packet.RequestId}, packetGeneration={packet.Generation}, localGeneration={currentGeneration}.");
                    return;
                }

                if (packet.Generation != currentGeneration)
                {
                    DebugLogHelper.LogInfo(
                        log,
                        $"Deterministic request generation replaced by host authority: scope={scopeId}, channel={packet.ChannelId}, source={packet.SourcePlayerId}, request={packet.RequestId}, clientGeneration={packet.Generation}, hostGeneration={currentGeneration}.");
                    packet.Generation = currentGeneration;
                }
            }
            else if (packet.Kind == DeterministicCommandMessageKind.Prepare && !localHost)
            {
                if (packet.Generation != currentGeneration)
                    AdoptHostGeneration(packet.Generation, packet.Sequence, currentGeneration);
            }
            else if (packet.Generation != currentGeneration)
            {
                DebugLogHelper.LogWarning(
                    log,
                    $"Deterministic packet rejected because its host generation does not match: scope={scopeId}, kind={packet.Kind}, packetGeneration={packet.Generation}, currentGeneration={currentGeneration}, channel={packet.ChannelId}, source={packet.SourcePlayerId}, request={packet.RequestId}, sequence={packet.Sequence}.");
                return;
            }

            try
            {
                switch (packet.Kind)
                {
                    case DeterministicCommandMessageKind.Request:
                        HandleHostRequest(packet);
                        break;
                    case DeterministicCommandMessageKind.Prepare:
                        HandlePrepare(packet);
                        break;
                    case DeterministicCommandMessageKind.Acknowledge:
                        if (GameNetworkAPI.IsLocalHost())
                            HandleAcknowledge(packet);
                        break;
                    case DeterministicCommandMessageKind.Commit:
                        HandleCommit(packet);
                        break;
                    case DeterministicCommandMessageKind.Cancel:
                        HandleCancel(packet);
                        break;
                    case DeterministicCommandMessageKind.StateFingerprint:
                        if (GameNetworkAPI.IsLocalHost())
                            HandleStateFingerprint(packet);
                        else
                            DebugLogHelper.LogWarning(log, $"Deterministic state fingerprint ignored on non-host: scope={scopeId}, channel={packet.ChannelId}, sequence={packet.Sequence}, checkpoint={packet.Reason ?? "null"}, reportingPlayer={packet.ActorPlayerId}.");
                        break;
                    default:
                        DebugLogHelper.LogWarning(log, $"Unknown deterministic packet kind ignored: scope={scopeId}, kind={(int)packet.Kind}.");
                        break;
                }
            }
            catch (Exception ex)
            {
                DebugLogHelper.LogError(
                    log,
                    $"Deterministic protocol processing failed: scope={scopeId}, kind={packet.Kind}, channel={packet.ChannelId}, source={packet.SourcePlayerId}, request={packet.RequestId}, sequence={packet.Sequence}, error={ex}.");
            }
        }

        private void ScheduleSingleplayerCommand(DeterministicCommandEnvelope request, CommandChannel channel, object command)
        {
            long sequence;
            int executionTick = GetCurrentGameTick() + 1;
            lock (stateLock)
            {
                sequence = ++nextHostSequence;
                preparedCommands[sequence] = new PreparedCommand(
                    request.Generation,
                    request.ChannelId,
                    request.SourcePlayerId,
                    request.RequestId,
                    sequence,
                    executionTick,
                    GetCurrentGameTick(),
                    channel,
                    command);
            }

            DebugLogHelper.LogInfo(
                log,
                $"Singleplayer deterministic command scheduled: scope={scopeId}, generation={request.Generation}, channel={request.ChannelId}, source={request.SourcePlayerId}, request={request.RequestId}, sequence={sequence}, executeTick={executionTick}.");
        }

        private void HandleHostRequest(DeterministicCommandEnvelope request)
        {
            int incomingGeneration = request?.Generation ?? -1;
            int hostGeneration;
            lock (stateLock)
                hostGeneration = generation;

            if (request != null)
                request.Generation = hostGeneration;

            HashSet<int> activeHumanPlayers = GetActiveHumanPlayerIds();
            DebugLogHelper.LogInfo(
                log,
                $"Host sequencer received request: scope={scopeId}, channel={request?.ChannelId ?? "null"}, source={request?.SourcePlayerId ?? 0}, request={request?.RequestId ?? 0}, incomingGeneration={incomingGeneration}, hostGeneration={hostGeneration}, currentTick={GetCurrentGameTick()}, activeHumans={BuildPlayerIdSummary(activeHumanPlayers)}, members={BuildMemberSummary()}.");

            if (IsActiveMultiplayerGame() &&
                (request == null || !activeHumanPlayers.Contains(request.SourcePlayerId)))
            {
                DebugLogHelper.LogWarning(
                    log,
                    $"Deterministic request rejected by host: scope={scopeId}, source={request?.SourcePlayerId ?? 0}, reason=source player is not an active human member.");
                return;
            }

            if (!ValidateRequestEnvelope(request, out CommandChannel channel, out object command, out string failureReason))
            {
                DebugLogHelper.LogWarning(
                    log,
                    $"Deterministic request rejected by host: scope={scopeId}, channel={request.ChannelId}, source={request.SourcePlayerId}, request={request.RequestId}, reason={failureReason}.");
                return;
            }

            RequestKey requestKey = new RequestKey(request.Generation, request.ChannelId, request.SourcePlayerId, request.RequestId);
            DeterministicCommandEnvelope prepare;
            lock (stateLock)
            {
                if (hostRequestSequences.TryGetValue(requestKey, out long existingSequence))
                {
                    if (!preparedCommands.TryGetValue(existingSequence, out PreparedCommand existing))
                        return;

                    prepare = CreatePrepareEnvelope(existing, request.Payload);
                }
                else
                {
                    long sequence = ++nextHostSequence;
                    int preparedAtTick = GetCurrentGameTick();
                    PreparedCommand prepared = new PreparedCommand(
                        request.Generation,
                        request.ChannelId,
                        request.SourcePlayerId,
                        request.RequestId,
                        sequence,
                        null,
                        preparedAtTick,
                        channel,
                        command);
                    HashSet<int> requiredPlayers = new HashSet<int>(activeHumanPlayers);
                    int localPlayerId = GameNetworkAPI.GetLocalPlayerId();
                    requiredPlayers.Add(localPlayerId);
                    HostPendingCommand pending = new HostPendingCommand(preparedAtTick, requiredPlayers);
                    pending.AcknowledgedPlayerIds.Add(localPlayerId);

                    preparedCommands.Add(sequence, prepared);
                    hostPendingCommands.Add(sequence, pending);
                    hostRequestSequences.Add(requestKey, sequence);
                    prepare = CreatePrepareEnvelope(prepared, request.Payload);
                }
            }

            DebugLogHelper.LogInfo(
                log,
                $"Host sequencer prepared command: scope={scopeId}, generation={prepare.Generation}, channel={prepare.ChannelId}, source={prepare.SourcePlayerId}, request={prepare.RequestId}, sequence={prepare.Sequence}, preparedTick={GetCurrentGameTick()}, {BuildPendingStateSummary(prepare.Sequence)}.");
            TrySendPacketToAll(prepare, "prepare-to-peers");
            TryCommitHostCommand(prepare.Sequence);
        }

        private void HandlePrepare(DeterministicCommandEnvelope prepare)
        {
            DebugLogHelper.LogInfo(
                log,
                $"Client handling host Prepare: scope={scopeId}, generation={prepare.Generation}, channel={prepare.ChannelId}, source={prepare.SourcePlayerId}, request={prepare.RequestId}, sequence={prepare.Sequence}, payloadBytes={prepare.Payload?.Length ?? 0}, localPlayer={GameNetworkAPI.GetLocalPlayerId()}, hostPlayer={FindHostPlayerId()}, currentTick={GetCurrentGameTick()}.");

            CommandChannel channel = null;
            object command = null;
            string failureReason = prepare.Sequence <= 0 ? "invalid sequence" : null;
            if (failureReason != null || !ValidateRequestEnvelope(prepare, out channel, out command, out failureReason))
            {
                DebugLogHelper.LogWarning(
                    log,
                    $"Deterministic prepare rejected: scope={scopeId}, channel={prepare.ChannelId}, source={prepare.SourcePlayerId}, request={prepare.RequestId}, sequence={prepare.Sequence}, reason={failureReason}.");
                return;
            }

            bool newlyStored = false;
            lock (stateLock)
            {
                if (!preparedCommands.ContainsKey(prepare.Sequence))
                {
                    preparedCommands.Add(
                        prepare.Sequence,
                        new PreparedCommand(
                            prepare.Generation,
                            prepare.ChannelId,
                            prepare.SourcePlayerId,
                            prepare.RequestId,
                            prepare.Sequence,
                            null,
                            GetCurrentGameTick(),
                            channel,
                            command));
                    newlyStored = true;
                }
            }

            DebugLogHelper.LogInfo(
                log,
                $"Client prepared deterministic payload: scope={scopeId}, generation={prepare.Generation}, channel={prepare.ChannelId}, source={prepare.SourcePlayerId}, request={prepare.RequestId}, sequence={prepare.Sequence}, newlyStored={newlyStored}, currentTick={GetCurrentGameTick()}.");

            int localPlayerId = GameNetworkAPI.GetLocalPlayerId();
            int hostPlayerId = FindHostPlayerId();
            if (localPlayerId <= 0 || hostPlayerId <= 0)
            {
                DebugLogHelper.LogError(
                    log,
                    $"Deterministic prepare could not be acknowledged: scope={scopeId}, sequence={prepare.Sequence}, localPlayer={localPlayerId}, hostPlayer={hostPlayerId}.");
                return;
            }

            DeterministicCommandEnvelope acknowledgement = new DeterministicCommandEnvelope
            {
                ScopeId = scopeId,
                Generation = prepare.Generation,
                Kind = DeterministicCommandMessageKind.Acknowledge,
                ChannelId = prepare.ChannelId,
                SourcePlayerId = prepare.SourcePlayerId,
                RequestId = prepare.RequestId,
                Sequence = prepare.Sequence,
                ActorPlayerId = localPlayerId
            };
            TrySendPacketToPlayer(hostPlayerId, acknowledgement, "acknowledge-to-host");
        }

        private void HandleAcknowledge(DeterministicCommandEnvelope acknowledgement)
        {
            DebugLogHelper.LogInfo(
                log,
                $"Host received deterministic acknowledgement: scope={scopeId}, generation={acknowledgement.Generation}, channel={acknowledgement.ChannelId}, source={acknowledgement.SourcePlayerId}, request={acknowledgement.RequestId}, sequence={acknowledgement.Sequence}, acknowledgingPlayer={acknowledgement.ActorPlayerId}, currentTick={GetCurrentGameTick()}.");

            lock (stateLock)
            {
                if (!hostPendingCommands.TryGetValue(acknowledgement.Sequence, out HostPendingCommand pending))
                {
                    DebugLogHelper.LogWarning(
                        log,
                        $"Deterministic acknowledgement ignored because no host-pending command exists: scope={scopeId}, sequence={acknowledgement.Sequence}, acknowledgingPlayer={acknowledgement.ActorPlayerId}.");
                    return;
                }
                if (!pending.RequiredPlayerIds.Contains(acknowledgement.ActorPlayerId))
                {
                    DebugLogHelper.LogWarning(
                        log,
                        $"Unexpected deterministic acknowledgement ignored: scope={scopeId}, sequence={acknowledgement.Sequence}, player={acknowledgement.ActorPlayerId}.");
                    return;
                }

                pending.AcknowledgedPlayerIds.Add(acknowledgement.ActorPlayerId);
            }

            DebugLogHelper.LogInfo(
                log,
                $"Host recorded deterministic acknowledgement: scope={scopeId}, sequence={acknowledgement.Sequence}, {BuildPendingStateSummary(acknowledgement.Sequence)}.");

            TryCommitHostCommand(acknowledgement.Sequence);
        }

        private void TryCommitHostCommand(long sequence)
        {
            DeterministicCommandEnvelope commit = null;
            lock (stateLock)
            {
                if (!hostPendingCommands.TryGetValue(sequence, out HostPendingCommand pending))
                {
                    DebugLogHelper.LogInfo(
                        log,
                        $"Host commit check found no pending command: scope={scopeId}, sequence={sequence}. The command may already be committed or cancelled.");
                    return;
                }
                if (!pending.HasAllAcknowledgements)
                {
                    DebugLogHelper.LogInfo(
                        log,
                        $"Host commit waiting for peer acknowledgements: scope={scopeId}, sequence={sequence}, {BuildPendingStateSummary(sequence)}.");
                    return;
                }
                if (!preparedCommands.TryGetValue(sequence, out PreparedCommand prepared))
                {
                    DebugLogHelper.LogError(
                        log,
                        $"Host commit failed because the prepared command is missing: scope={scopeId}, sequence={sequence}.");
                    return;
                }
                if (prepared.ExecutionTick.HasValue)
                {
                    DebugLogHelper.LogInfo(
                        log,
                        $"Host commit skipped because the command already has an execution tick: scope={scopeId}, sequence={sequence}, executeTick={prepared.ExecutionTick.Value}.");
                    return;
                }

                int executionTick = GetCurrentGameTick() + executionLeadTicks;
                prepared.ExecutionTick = executionTick;
                hostPendingCommands.Remove(sequence);
                commit = new DeterministicCommandEnvelope
                {
                    ScopeId = scopeId,
                    Generation = prepared.Generation,
                    Kind = DeterministicCommandMessageKind.Commit,
                    ChannelId = prepared.ChannelId,
                    SourcePlayerId = prepared.SourcePlayerId,
                    RequestId = prepared.RequestId,
                    Sequence = prepared.Sequence,
                    ExecutionTick = executionTick
                };
            }

            bool sent = TrySendPacketToAll(commit, "commit-to-peers");
            DebugLogHelper.LogInfo(
                log,
                $"Deterministic command committed by host: scope={scopeId}, generation={commit.Generation}, channel={commit.ChannelId}, source={commit.SourcePlayerId}, request={commit.RequestId}, sequence={commit.Sequence}, executeTick={commit.ExecutionTick}, currentTick={GetCurrentGameTick()}, broadcastSent={sent}.");
        }

        private void HandleCommit(DeterministicCommandEnvelope commit)
        {
            DebugLogHelper.LogInfo(
                log,
                $"Client handling host Commit: scope={scopeId}, generation={commit.Generation}, channel={commit.ChannelId}, source={commit.SourcePlayerId}, request={commit.RequestId}, sequence={commit.Sequence}, executeTick={commit.ExecutionTick}, currentTick={GetCurrentGameTick()}, ticksUntilExecution={commit.ExecutionTick - GetCurrentGameTick()}.");

            if (commit.Sequence <= 0 || commit.ExecutionTick <= 0)
            {
                DebugLogHelper.LogWarning(log, $"Invalid deterministic commit ignored: scope={scopeId}, sequence={commit.Sequence}, executeTick={commit.ExecutionTick}.");
                return;
            }

            lock (stateLock)
            {
                if (!preparedCommands.TryGetValue(commit.Sequence, out PreparedCommand prepared))
                {
                    DebugLogHelper.LogError(
                        log,
                        $"Deterministic commit arrived without prepared payload: scope={scopeId}, sequence={commit.Sequence}, executeTick={commit.ExecutionTick}.");
                    return;
                }

                prepared.ExecutionTick = commit.ExecutionTick;
            }

            DebugLogHelper.LogInfo(
                log,
                $"Deterministic command armed on client: scope={scopeId}, generation={commit.Generation}, channel={commit.ChannelId}, source={commit.SourcePlayerId}, request={commit.RequestId}, sequence={commit.Sequence}, executeTick={commit.ExecutionTick}, currentTick={GetCurrentGameTick()}.");
        }

        private void HandleCancel(DeterministicCommandEnvelope cancel)
        {
            lock (stateLock)
            {
                preparedCommands.Remove(cancel.Sequence);
                hostPendingCommands.Remove(cancel.Sequence);
            }

            DebugLogHelper.LogWarning(
                log,
                $"Deterministic command cancelled: scope={scopeId}, sequence={cancel.Sequence}, reason={cancel.Reason ?? "unspecified"}.");
        }

        private void HandleStateFingerprint(DeterministicCommandEnvelope report)
        {
            if (report == null || report.Sequence <= 0 || report.RequestId <= 0 ||
                report.ActorPlayerId <= 0 || string.IsNullOrWhiteSpace(report.ChannelId) ||
                string.IsNullOrWhiteSpace(report.Reason) || report.Payload == null)
            {
                DebugLogHelper.LogWarning(
                    log,
                    $"Invalid deterministic state fingerprint report ignored: scope={scopeId}, channel={report?.ChannelId ?? "null"}, request={report?.RequestId ?? 0}, sequence={report?.Sequence ?? 0}, checkpoint={report?.Reason ?? "null"}, reportingPlayer={report?.ActorPlayerId ?? 0}, payloadBytes={report?.Payload?.Length ?? 0}.");
                return;
            }
            if (report.Payload.Length > MaximumFingerprintPayloadBytes)
            {
                DebugLogHelper.LogWarning(
                    log,
                    $"Oversized deterministic state fingerprint report ignored: scope={scopeId}, channel={report.ChannelId}, sequence={report.Sequence}, checkpoint={report.Reason}, reportingPlayer={report.ActorPlayerId}, payloadBytes={report.Payload.Length}, limit={MaximumFingerprintPayloadBytes}.");
                return;
            }

            string fingerprint;
            try
            {
                fingerprint = Encoding.UTF8.GetString(report.Payload);
            }
            catch (Exception ex)
            {
                DebugLogHelper.LogError(
                    log,
                    $"Deterministic state fingerprint decoding failed: scope={scopeId}, channel={report.ChannelId}, sequence={report.Sequence}, checkpoint={report.Reason}, reportingPlayer={report.ActorPlayerId}, error={ex}.");
                return;
            }

            StateFingerprintKey key = new StateFingerprintKey(
                report.Generation,
                report.ChannelId,
                report.SourcePlayerId,
                report.RequestId,
                report.Sequence,
                report.ExecutionTick,
                report.Reason);
            HostStateFingerprintSet set;
            string previousFingerprint = null;
            bool duplicate;
            bool complete;
            lock (stateLock)
            {
                if (!hostStateFingerprints.TryGetValue(key, out set))
                {
                    HashSet<int> requiredPlayers = GetActiveHumanPlayerIds();
                    int localPlayerId = GameNetworkAPI.GetLocalPlayerId();
                    if (localPlayerId > 0)
                        requiredPlayers.Add(localPlayerId);
                    set = new HostStateFingerprintSet(requiredPlayers);
                    hostStateFingerprints.Add(key, set);
                }

                if (!set.RequiredPlayerIds.Contains(report.ActorPlayerId))
                {
                    DebugLogHelper.LogWarning(
                        log,
                        $"Deterministic state fingerprint from an unexpected player was ignored: scope={scopeId}, channel={report.ChannelId}, sequence={report.Sequence}, checkpoint={report.Reason}, reportingPlayer={report.ActorPlayerId}, requiredPlayers={BuildPlayerIdSummary(set.RequiredPlayerIds)}.");
                    return;
                }

                duplicate = set.ReportsByPlayerId.TryGetValue(report.ActorPlayerId, out previousFingerprint);
                if (!duplicate)
                    set.ReportsByPlayerId.Add(report.ActorPlayerId, fingerprint);
                complete = set.HasAllReports;
            }

            if (duplicate)
            {
                if (!string.Equals(previousFingerprint, fingerprint, StringComparison.Ordinal))
                {
                    DebugLogHelper.LogError(
                        log,
                        $"Deterministic state fingerprint changed in a duplicate report: scope={scopeId}, channel={report.ChannelId}, request={report.RequestId}, sequence={report.Sequence}, executeTick={report.ExecutionTick}, checkpoint={report.Reason}, reportingPlayer={report.ActorPlayerId}, previousDigest={ComputeFingerprintDigest(previousFingerprint)}, newDigest={ComputeFingerprintDigest(fingerprint)}, previous={previousFingerprint}, current={fingerprint}.");
                }
                else
                {
                    DebugLogHelper.LogInfo(
                        log,
                        $"Duplicate deterministic state fingerprint report matched: scope={scopeId}, channel={report.ChannelId}, sequence={report.Sequence}, checkpoint={report.Reason}, reportingPlayer={report.ActorPlayerId}, digest={ComputeFingerprintDigest(fingerprint)}.");
                }
                return;
            }

            DebugLogHelper.LogInfo(
                log,
                $"Host recorded deterministic state fingerprint: scope={scopeId}, generation={report.Generation}, channel={report.ChannelId}, source={report.SourcePlayerId}, request={report.RequestId}, sequence={report.Sequence}, executeTick={report.ExecutionTick}, checkpoint={report.Reason}, reportingPlayer={report.ActorPlayerId}, digest={ComputeFingerprintDigest(fingerprint)}, receivedPlayers={BuildPlayerIdSummary(set.ReportedPlayerIds)}, requiredPlayers={BuildPlayerIdSummary(set.RequiredPlayerIds)}, complete={complete}.");

            if (!complete)
                return;

            List<int> sortedPlayers = new List<int>(set.RequiredPlayerIds);
            sortedPlayers.Sort();
            int baselinePlayerId = sortedPlayers[0];
            string baseline = set.ReportsByPlayerId[baselinePlayerId];
            bool identical = true;
            for (int i = 1; i < sortedPlayers.Count; i++)
            {
                if (!string.Equals(baseline, set.ReportsByPlayerId[sortedPlayers[i]], StringComparison.Ordinal))
                {
                    identical = false;
                    break;
                }
            }

            lock (stateLock)
                hostStateFingerprints.Remove(key);

            if (identical)
            {
                DebugLogHelper.LogInfo(
                    log,
                    $"Deterministic state fingerprints MATCH: scope={scopeId}, generation={report.Generation}, channel={report.ChannelId}, request={report.RequestId}, sequence={report.Sequence}, executeTick={report.ExecutionTick}, checkpoint={report.Reason}, players={BuildPlayerIdSummary(set.RequiredPlayerIds)}, digest={ComputeFingerprintDigest(baseline)}, payloadChars={baseline.Length}.");
                return;
            }

            List<string> reports = new List<string>(sortedPlayers.Count);
            for (int i = 0; i < sortedPlayers.Count; i++)
            {
                int playerId = sortedPlayers[i];
                string playerFingerprint = set.ReportsByPlayerId[playerId];
                reports.Add($"player={playerId}:digest={ComputeFingerprintDigest(playerFingerprint)}:fingerprint={playerFingerprint}");
            }
            DebugLogHelper.LogError(
                log,
                $"Deterministic state fingerprints MISMATCH: scope={scopeId}, generation={report.Generation}, channel={report.ChannelId}, request={report.RequestId}, sequence={report.Sequence}, executeTick={report.ExecutionTick}, checkpoint={report.Reason}, reports=[{string.Join(" || ", reports)}].");
        }

        private void OnGameTick(int tick)
        {
            List<PreparedCommand> dueCommands = null;
            List<PreparedCommand> missedCommands = null;
            List<PreparedCommand> timedOutCommands = null;
            lock (stateLock)
            {
                if (tick == lastProcessedTick)
                    return;

                lastProcessedTick = tick;
                foreach (KeyValuePair<long, PreparedCommand> entry in preparedCommands)
                {
                    PreparedCommand prepared = entry.Value;
                    if (prepared.ExecutionTick.HasValue)
                    {
                        if (prepared.ExecutionTick.Value == tick)
                            AddCommand(ref dueCommands, prepared);
                        else if (prepared.ExecutionTick.Value < tick)
                            AddCommand(ref missedCommands, prepared);
                    }
                    else if (tick - prepared.PreparedAtTick > prepareTimeoutTicks)
                    {
                        prepared.TimeoutDetails = BuildPendingStateSummary(prepared.Sequence);
                        AddCommand(ref timedOutCommands, prepared);
                    }
                }

                RemoveCommands(dueCommands);
                RemoveCommands(missedCommands);
                RemoveCommands(timedOutCommands);
            }

            ExecuteDueCommands(dueCommands, tick);
            LogMissedCommands(missedCommands, tick);
            CancelTimedOutCommands(timedOutCommands, tick);
        }

        private void ExecuteDueCommands(List<PreparedCommand> commands, int tick)
        {
            if (commands == null)
                return;

            commands.Sort((left, right) => left.Sequence.CompareTo(right.Sequence));
            int localPlayerId = GameNetworkAPI.GetLocalPlayerId();
            DebugLogHelper.LogInfo(
                log,
                $"Deterministic tick execution batch started: scope={scopeId}, tick={tick}, commandCount={commands.Count}, localPlayer={localPlayerId}, localHost={GameNetworkAPI.IsLocalHost()}.");
            for (int i = 0; i < commands.Count; i++)
            {
                PreparedCommand command = commands[i];
                DeterministicCommandContext context = new DeterministicCommandContext(
                    command.ChannelId,
                    command.SourcePlayerId,
                    command.RequestId,
                    command.Sequence,
                    tick,
                    command.SourcePlayerId == localPlayerId);

                try
                {
                    DebugLogHelper.LogInfo(
                        log,
                        $"Deterministic command execution entering feature handler: scope={scopeId}, generation={command.Generation}, channel={command.ChannelId}, source={command.SourcePlayerId}, request={command.RequestId}, sequence={command.Sequence}, tick={tick}, localSource={context.IsLocalSource}.");
                    bool applied = command.Channel.Execute(command.Command, context);
                    DebugLogHelper.LogInfo(
                        log,
                        $"Deterministic command executed: scope={scopeId}, generation={command.Generation}, channel={command.ChannelId}, source={command.SourcePlayerId}, request={command.RequestId}, sequence={command.Sequence}, tick={tick}, applied={applied}.");
                }
                catch (Exception ex)
                {
                    DebugLogHelper.LogError(
                        log,
                        $"Deterministic command execution failed: scope={scopeId}, generation={command.Generation}, channel={command.ChannelId}, source={command.SourcePlayerId}, request={command.RequestId}, sequence={command.Sequence}, tick={tick}, error={ex}.");
                }
            }
        }

        private void LogMissedCommands(List<PreparedCommand> commands, int tick)
        {
            if (commands == null)
                return;

            for (int i = 0; i < commands.Count; i++)
            {
                PreparedCommand command = commands[i];
                DebugLogHelper.LogError(
                    log,
                    $"Deterministic command missed its execution tick and was not applied: scope={scopeId}, channel={command.ChannelId}, sequence={command.Sequence}, expectedTick={command.ExecutionTick}, currentTick={tick}.");
            }
        }

        private void CancelTimedOutCommands(List<PreparedCommand> commands, int tick)
        {
            if (commands == null)
                return;

            bool localHost = GameNetworkAPI.IsLocalHost();
            for (int i = 0; i < commands.Count; i++)
            {
                PreparedCommand command = commands[i];
                DebugLogHelper.LogWarning(
                    log,
                    $"Deterministic command preparation timed out: scope={scopeId}, generation={command.Generation}, channel={command.ChannelId}, source={command.SourcePlayerId}, request={command.RequestId}, sequence={command.Sequence}, preparedTick={command.PreparedAtTick}, currentTick={tick}, waitedTicks={tick - command.PreparedAtTick}, {command.TimeoutDetails ?? "pendingState=unavailable"}, localHost={localHost}, localPlayer={GameNetworkAPI.GetLocalPlayerId()}, members={BuildMemberSummary()}.");

                if (!localHost || !IsActiveMultiplayerGame())
                    continue;

                DeterministicCommandEnvelope cancel = new DeterministicCommandEnvelope
                {
                    ScopeId = scopeId,
                    Generation = command.Generation,
                    Kind = DeterministicCommandMessageKind.Cancel,
                    ChannelId = command.ChannelId,
                    SourcePlayerId = command.SourcePlayerId,
                    RequestId = command.RequestId,
                    Sequence = command.Sequence,
                    Reason = "prepare-timeout"
                };
                UnityMainThreadDispatcher.EnqueueStatic(() =>
                {
                    if (!disposed && packetHook != null)
                        TrySendPacketToAll(cancel, "prepare-timeout-cancel");
                });
            }
        }

        private bool ValidateRequestEnvelope(
            DeterministicCommandEnvelope request,
            out CommandChannel channel,
            out object command,
            out string failureReason)
        {
            channel = null;
            command = null;
            failureReason = null;
            if (request == null)
            {
                failureReason = "packet is null";
                return false;
            }
            if (request.SourcePlayerId <= 0)
            {
                failureReason = "invalid source player id";
                return false;
            }
            if (request.RequestId <= 0)
            {
                failureReason = "invalid request id";
                return false;
            }
            if (string.IsNullOrEmpty(request.ChannelId))
            {
                failureReason = "missing channel id";
                return false;
            }
            if (request.Payload == null || request.Payload.Length == 0)
            {
                failureReason = "missing payload";
                return false;
            }

            lock (stateLock)
            {
                if (!channels.TryGetValue(request.ChannelId, out channel))
                {
                    failureReason = "unregistered channel";
                    return false;
                }
            }

            try
            {
                command = channel.Deserialize(request.Payload);
            }
            catch (Exception ex)
            {
                failureReason = "payload deserialization failed: " + ex.Message;
                return false;
            }

            failureReason = channel.Validate(command);
            return failureReason == null;
        }

        private DeterministicCommandEnvelope CreatePrepareEnvelope(PreparedCommand prepared, byte[] payload)
        {
            return new DeterministicCommandEnvelope
            {
                ScopeId = scopeId,
                Generation = prepared.Generation,
                Kind = DeterministicCommandMessageKind.Prepare,
                ChannelId = prepared.ChannelId,
                SourcePlayerId = prepared.SourcePlayerId,
                RequestId = prepared.RequestId,
                Sequence = prepared.Sequence,
                Payload = payload
            };
        }

        private bool TrySendPacketToAll(DeterministicCommandEnvelope packet, string stage)
        {
            if (packetHook == null)
            {
                DebugLogHelper.LogError(
                    log,
                    $"Deterministic packet broadcast failed because the packet hook is unavailable: scope={scopeId}, stage={stage}, kind={packet?.Kind}, sequence={packet?.Sequence ?? 0}.");
                return false;
            }

            try
            {
                DebugLogHelper.LogInfo(
                    log,
                    $"Deterministic packet broadcast started: scope={scopeId}, stage={stage}, packetId={packetHook.GetPacketId()}, kind={packet.Kind}, generation={packet.Generation}, channel={packet.ChannelId}, source={packet.SourcePlayerId}, request={packet.RequestId}, sequence={packet.Sequence}, actor={packet.ActorPlayerId}, executeTick={packet.ExecutionTick}, payloadBytes={packet.Payload?.Length ?? 0}, members={BuildMemberSummary()}.");
                GameNetworkAPI.SendPacketToAll(packet, packetHook.GetPacketId());
                DebugLogHelper.LogInfo(
                    log,
                    $"Deterministic packet broadcast returned successfully: scope={scopeId}, stage={stage}, kind={packet.Kind}, sequence={packet.Sequence}.");
                return true;
            }
            catch (Exception ex)
            {
                DebugLogHelper.LogError(
                    log,
                    $"Deterministic packet broadcast threw an exception: scope={scopeId}, stage={stage}, kind={packet?.Kind}, sequence={packet?.Sequence ?? 0}, error={ex}.");
                return false;
            }
        }

        private bool TrySendPacketToPlayer(int playerId, DeterministicCommandEnvelope packet, string stage)
        {
            if (packetHook == null)
            {
                DebugLogHelper.LogError(
                    log,
                    $"Deterministic directed packet failed because the packet hook is unavailable: scope={scopeId}, stage={stage}, targetPlayer={playerId}, kind={packet?.Kind}, sequence={packet?.Sequence ?? 0}.");
                return false;
            }

            try
            {
                DebugLogHelper.LogInfo(
                    log,
                    $"Deterministic directed packet send started: scope={scopeId}, stage={stage}, packetId={packetHook.GetPacketId()}, targetPlayer={playerId}, kind={packet.Kind}, generation={packet.Generation}, channel={packet.ChannelId}, source={packet.SourcePlayerId}, request={packet.RequestId}, sequence={packet.Sequence}, actor={packet.ActorPlayerId}, executeTick={packet.ExecutionTick}, payloadBytes={packet.Payload?.Length ?? 0}.");
                GameNetworkAPI.SendPacketToPlayerId(playerId, packet, packetHook.GetPacketId());
                DebugLogHelper.LogInfo(
                    log,
                    $"Deterministic directed packet send returned successfully: scope={scopeId}, stage={stage}, targetPlayer={playerId}, kind={packet.Kind}, sequence={packet.Sequence}.");
                return true;
            }
            catch (Exception ex)
            {
                DebugLogHelper.LogError(
                    log,
                    $"Deterministic directed packet threw an exception: scope={scopeId}, stage={stage}, targetPlayer={playerId}, kind={packet?.Kind}, sequence={packet?.Sequence ?? 0}, error={ex}.");
                return false;
            }
        }

        private string BuildPendingStateSummary(long sequence)
        {
            lock (stateLock)
            {
                if (!hostPendingCommands.TryGetValue(sequence, out HostPendingCommand pending))
                    return "pendingState=not-found";

                HashSet<int> missingPlayerIds = new HashSet<int>(pending.RequiredPlayerIds);
                missingPlayerIds.ExceptWith(pending.AcknowledgedPlayerIds);
                return $"requiredPlayers={BuildPlayerIdSummary(pending.RequiredPlayerIds)}, acknowledgedPlayers={BuildPlayerIdSummary(pending.AcknowledgedPlayerIds)}, missingPlayers={BuildPlayerIdSummary(missingPlayerIds)}";
            }
        }

        private static HashSet<int> GetActiveHumanPlayerIds()
        {
            HashSet<int> playerIds = new HashSet<int>();
            List<Platform_Multiplayer.MPGameMember> players = GameNetworkAPI.GetPlayers();
            if (players == null)
                return playerIds;

            for (int i = 0; i < players.Count; i++)
            {
                Platform_Multiplayer.MPGameMember player = players[i];
                if (player == null || player.playerID <= 0 || player.skirmishAI || player.kicked ||
                    !player.stillWithSteamConnection)
                    continue;

                playerIds.Add(player.playerID);
            }

            return playerIds;
        }

        /// <summary>
        /// Distinguishes an active in-game multiplayer session from stale gameMembers data.
        /// The base game can retain a synthetic singleplayer/skirmish member list after leaving
        /// multiplayer. Real multiplayer members are populated from Steam and always contain
        /// both a local <c>isSelf</c> member and an <c>isHost</c> member.
        /// </summary>
        private static bool IsActiveMultiplayerGame()
        {
            List<Platform_Multiplayer.MPGameMember> players = GameNetworkAPI.GetPlayers();
            if (players == null)
                return false;

            int activeHumanCount = 0;
            bool hasSelf = false;
            bool hasHost = false;
            for (int i = 0; i < players.Count; i++)
            {
                Platform_Multiplayer.MPGameMember player = players[i];
                if (player == null || player.playerID <= 0 || player.skirmishAI || player.kicked ||
                    !player.stillWithSteamConnection)
                    continue;

                activeHumanCount++;
                hasSelf |= player.isSelf;
                hasHost |= player.isHost;
            }

            return activeHumanCount > 1 && hasSelf && hasHost;
        }

        private static int FindHostPlayerId()
        {
            List<Platform_Multiplayer.MPGameMember> players = GameNetworkAPI.GetPlayers();
            if (players == null)
                return -1;

            for (int i = 0; i < players.Count; i++)
            {
                Platform_Multiplayer.MPGameMember player = players[i];
                if (player != null && player.playerID > 0 && player.isHost && !player.kicked)
                    return player.playerID;
            }

            return -1;
        }

        private static string BuildMemberSummary()
        {
            List<Platform_Multiplayer.MPGameMember> players = GameNetworkAPI.GetPlayers();
            if (players == null)
                return "none";

            List<string> summaries = new List<string>(players.Count);
            for (int i = 0; i < players.Count; i++)
            {
                Platform_Multiplayer.MPGameMember player = players[i];
                if (player == null)
                {
                    summaries.Add("null");
                    continue;
                }

                summaries.Add(
                    $"id={player.playerID}:host={player.isHost}:self={player.isSelf}:ai={player.skirmishAI}:kicked={player.kicked}:connected={player.stillWithSteamConnection}");
            }

            return summaries.Count == 0 ? "empty" : string.Join("|", summaries);
        }

        private int GetCurrentGameTick()
        {
            return GameTimeManagerAPI.Instance.GetFrameProvider().CurrentGameTick;
        }

        private static string BuildPlayerIdSummary(HashSet<int> playerIds)
        {
            if (playerIds == null || playerIds.Count == 0)
                return "none";

            List<int> sorted = new List<int>(playerIds);
            sorted.Sort();
            return string.Join(",", sorted);
        }

        private static string ComputeFingerprintDigest(string value)
        {
            if (value == null)
                return "null";

            const ulong offsetBasis = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offsetBasis;
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            for (int i = 0; i < bytes.Length; i++)
            {
                hash ^= bytes[i];
                hash *= prime;
            }

            return hash.ToString("X16");
        }

        private static void AddCommand(ref List<PreparedCommand> commands, PreparedCommand command)
        {
            if (commands == null)
                commands = new List<PreparedCommand>();

            commands.Add(command);
        }

        private void RemoveCommands(List<PreparedCommand> commands)
        {
            if (commands == null)
                return;

            for (int i = 0; i < commands.Count; i++)
            {
                PreparedCommand command = commands[i];
                preparedCommands.Remove(command.Sequence);
                hostPendingCommands.Remove(command.Sequence);
            }
        }

        private abstract class CommandChannel
        {
            protected CommandChannel(string channelId, Type commandType)
            {
                ChannelId = channelId;
                CommandType = commandType;
            }

            public string ChannelId { get; }
            public Type CommandType { get; }
            public abstract byte[] Serialize(object command);
            public abstract object Deserialize(byte[] payload);
            public abstract string Validate(object command);
            public abstract bool Execute(object command, DeterministicCommandContext context);
        }

        private sealed class CommandChannel<TCommand> : CommandChannel
            where TCommand : class
        {
            private readonly Func<TCommand, string> validate;
            private readonly Func<TCommand, DeterministicCommandContext, bool> execute;

            public CommandChannel(
                string channelId,
                Func<TCommand, string> validate,
                Func<TCommand, DeterministicCommandContext, bool> execute)
                : base(channelId, typeof(TCommand))
            {
                this.validate = validate;
                this.execute = execute;
            }

            public override byte[] Serialize(object command)
            {
                return MessagePackSerializer.Serialize((TCommand)command);
            }

            public override object Deserialize(byte[] payload)
            {
                return MessagePackSerializer.Deserialize<TCommand>(payload);
            }

            public override string Validate(object command)
            {
                if (!(command is TCommand typedCommand))
                    return "payload type mismatch";

                return validate?.Invoke(typedCommand);
            }

            public override bool Execute(object command, DeterministicCommandContext context)
            {
                return execute((TCommand)command, context);
            }
        }

        private sealed class PreparedCommand
        {
            public PreparedCommand(
                int generation,
                string channelId,
                int sourcePlayerId,
                int requestId,
                long sequence,
                int? executionTick,
                int preparedAtTick,
                CommandChannel channel,
                object command)
            {
                Generation = generation;
                ChannelId = channelId;
                SourcePlayerId = sourcePlayerId;
                RequestId = requestId;
                Sequence = sequence;
                ExecutionTick = executionTick;
                PreparedAtTick = preparedAtTick;
                Channel = channel;
                Command = command;
            }

            public int Generation { get; }
            public string ChannelId { get; }
            public int SourcePlayerId { get; }
            public int RequestId { get; }
            public long Sequence { get; }
            public int? ExecutionTick { get; set; }
            public int PreparedAtTick { get; }
            public CommandChannel Channel { get; }
            public object Command { get; }
            public string TimeoutDetails { get; set; }
        }

        private sealed class HostPendingCommand
        {
            public HostPendingCommand(int preparedAtTick, HashSet<int> requiredPlayerIds)
            {
                PreparedAtTick = preparedAtTick;
                RequiredPlayerIds = requiredPlayerIds;
            }

            public int PreparedAtTick { get; }
            public HashSet<int> RequiredPlayerIds { get; }
            public HashSet<int> AcknowledgedPlayerIds { get; } = new HashSet<int>();

            public bool HasAllAcknowledgements
            {
                get
                {
                    foreach (int playerId in RequiredPlayerIds)
                    {
                        if (!AcknowledgedPlayerIds.Contains(playerId))
                            return false;
                    }

                    return true;
                }
            }
        }

        private sealed class HostStateFingerprintSet
        {
            public HostStateFingerprintSet(HashSet<int> requiredPlayerIds)
            {
                RequiredPlayerIds = requiredPlayerIds ?? new HashSet<int>();
            }

            public HashSet<int> RequiredPlayerIds { get; }
            public Dictionary<int, string> ReportsByPlayerId { get; } = new Dictionary<int, string>();

            public HashSet<int> ReportedPlayerIds => new HashSet<int>(ReportsByPlayerId.Keys);

            public bool HasAllReports
            {
                get
                {
                    if (RequiredPlayerIds.Count == 0)
                        return false;

                    foreach (int playerId in RequiredPlayerIds)
                    {
                        if (!ReportsByPlayerId.ContainsKey(playerId))
                            return false;
                    }

                    return true;
                }
            }
        }

        private readonly struct StateFingerprintKey : IEquatable<StateFingerprintKey>
        {
            public StateFingerprintKey(
                int generation,
                string channelId,
                int sourcePlayerId,
                int requestId,
                long sequence,
                int executionTick,
                string checkpoint)
            {
                Generation = generation;
                ChannelId = channelId;
                SourcePlayerId = sourcePlayerId;
                RequestId = requestId;
                Sequence = sequence;
                ExecutionTick = executionTick;
                Checkpoint = checkpoint;
            }

            public int Generation { get; }
            public string ChannelId { get; }
            public int SourcePlayerId { get; }
            public int RequestId { get; }
            public long Sequence { get; }
            public int ExecutionTick { get; }
            public string Checkpoint { get; }

            public bool Equals(StateFingerprintKey other)
            {
                return Generation == other.Generation &&
                    SourcePlayerId == other.SourcePlayerId &&
                    RequestId == other.RequestId &&
                    Sequence == other.Sequence &&
                    ExecutionTick == other.ExecutionTick &&
                    string.Equals(ChannelId, other.ChannelId, StringComparison.Ordinal) &&
                    string.Equals(Checkpoint, other.Checkpoint, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is StateFingerprintKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode = Generation;
                    hashCode = (hashCode * 397) ^ SourcePlayerId;
                    hashCode = (hashCode * 397) ^ RequestId;
                    hashCode = (hashCode * 397) ^ Sequence.GetHashCode();
                    hashCode = (hashCode * 397) ^ ExecutionTick;
                    hashCode = (hashCode * 397) ^ (ChannelId != null ? StringComparer.Ordinal.GetHashCode(ChannelId) : 0);
                    hashCode = (hashCode * 397) ^ (Checkpoint != null ? StringComparer.Ordinal.GetHashCode(Checkpoint) : 0);
                    return hashCode;
                }
            }
        }

        private readonly struct RequestKey : IEquatable<RequestKey>
        {
            public RequestKey(int generation, string channelId, int sourcePlayerId, int requestId)
            {
                Generation = generation;
                ChannelId = channelId;
                SourcePlayerId = sourcePlayerId;
                RequestId = requestId;
            }

            public int Generation { get; }
            public string ChannelId { get; }
            public int SourcePlayerId { get; }
            public int RequestId { get; }

            public bool Equals(RequestKey other)
            {
                return Generation == other.Generation &&
                    SourcePlayerId == other.SourcePlayerId &&
                    RequestId == other.RequestId &&
                    string.Equals(ChannelId, other.ChannelId, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is RequestKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode = Generation;
                    hashCode = (hashCode * 397) ^ SourcePlayerId;
                    hashCode = (hashCode * 397) ^ RequestId;
                    hashCode = (hashCode * 397) ^ (ChannelId != null ? StringComparer.Ordinal.GetHashCode(ChannelId) : 0);
                    return hashCode;
                }
            }
        }
    }
}
