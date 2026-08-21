using BepInEx.Logging;
using CrusaderDE;
using MessagePack;
using R3;
using SHCDESE.API;
using SHCDESE.API.Components.Network;
using SHCDESE.API.Components.SaveData;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.EventAPI.Network;
using SHCDESE.GameGlobals;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace RandomEvents
{
    internal sealed class RandomEventsRuntime : IDisposable
    {
        private const string SaveDataIdentifier = "serp-randomevents-state";
        private const int VanillaMonthsPerYear = 12;
        private const int RabbitSpawnRadius = 12;
        private const int LionSpawnRadius = 12;
        private const int BanditVisualPlayerId = 0;
        private const int MaximumBanditGroups = 5;
        private const int ScaledStrengthTenthsPerUnit = 10;
        private const int ScaledStrengthMonthsPerPeriod = 3;
        private const int EventKindCount = 15;
        private const int BanditGroupActivationDelayTicks = 20;
        private const int ChoreProtocolVersion = 2;
        private const int MultiplayerStartupDelayMilliseconds = 5000;
        private const int MultiplayerStartupMinimumTicks = 30;
        private const int MaximumChorePayloadBytes = 1200;
        private const int MaximumChoreActions = EventKindCount * (GamePlayerManagerAPI.MAX_PLAYERS + 1);

        private readonly ManualLogSource log;
        private readonly RandomEventsSettingsViewModel settings;
        private readonly ScenarioSignpostRegistry signpostRegistry;
        private readonly SignpostPlacementService signpostPlacement;
        private readonly NativeVanillaEventDispatcher nativeEventDispatcher;
        private readonly NativeWildlifeEventDispatcher nativeWildlifeDispatcher;
        private readonly NativeBanditEventSupport nativeBanditSupport;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private readonly List<PendingBanditGroup> pendingBanditGroups = new List<PendingBanditGroup>();
        private readonly HashSet<int> initializationAcknowledgedPlayerIds = new HashSet<int>();
        private bool initialized;
        private bool disposed;
        private bool mapStartPending;
        private bool mapActive;
        private bool loadedStateAvailable;
        private bool mapStartedFromMultiplayerSave;
        private int lastSignpostAttemptTick = int.MinValue;
        private bool banditEventsEnabled = true;
        private bool networkInitialized;
        private bool isRealMultiplayer;
        private bool isLocalHost;
        private bool initializationChoreQueued;
        private bool multiplayerInitializationReceived;
        private bool batchChoreQueued;
        private bool signpostChoreQueued;
        private bool multiplayerInitializationConfirmed;
        private bool startupDelayLogged;
        private int initializationAttemptCount;
        private int nextOperationId;
        private int initializationOperationId;
        private int lastRandomEventsChoreQueuedTick = int.MinValue;
        private long mapStartTimestamp;
        private long lastInitializationSendTimestamp;
        private byte[] initializationStateDigest = Array.Empty<byte>();
        private byte[] cachedInitializationBody = Array.Empty<byte>();
        private string cachedInitializationBodyHash = string.Empty;
        private RandomEventsCooldownEncoding cachedInitializationCooldownEncoding;
        private int acceptedInitializationOperationId;
        private string acceptedInitializationBodyHash = string.Empty;
        private R3PacketEventHook<RandomEventsInitializationChorePacket> initializationChorePacketHook;
        private IDisposable initializationChorePacketSubscription;
        private R3PacketEventHook<RandomEventsBatchChorePacket> batchChorePacketHook;
        private IDisposable batchChorePacketSubscription;
        private R3PacketEventHook<RandomEventsSignpostChorePacket> signpostChorePacketHook;
        private IDisposable signpostChorePacketSubscription;
        private R3PacketEventHook<RandomEventsInitializationAckPacket> initializationAckPacketHook;
        private IDisposable initializationAckPacketSubscription;
        private RandomEventsRuntimeState state;
        private RandomEventsSaveState loadedSaveState;
        private RandomEventsRuntimeState deferredPreparedState;

        public RandomEventsRuntime(ManualLogSource log, RandomEventsSettingsViewModel settings)
        {
            this.log = log;
            this.settings = settings;
            signpostRegistry = new ScenarioSignpostRegistry(log);
            signpostPlacement = new SignpostPlacementService(log, signpostRegistry);
            nativeEventDispatcher = new NativeVanillaEventDispatcher(log);
            nativeWildlifeDispatcher = new NativeWildlifeEventDispatcher(log, nativeEventDispatcher);
            nativeBanditSupport = new NativeBanditEventSupport(log);
        }

        public void InitializeNative(IntPtr libraryHandle, ReadOnlySpan<byte> memory, bool referenceHashMatches)
        {
            signpostRegistry.InitializeNative(libraryHandle, memory, referenceHashMatches);
            nativeEventDispatcher.InitializeNative(libraryHandle, memory, referenceHashMatches);
            nativeWildlifeDispatcher.InitializeNative(libraryHandle, memory, referenceHashMatches);
            nativeBanditSupport.InitializeNative(memory, referenceHashMatches);
        }

        public void InitializeNetwork()
        {
            if (networkInitialized)
                return;

            initializationChorePacketHook = GameNetworkAPI.Instance.GetPacketEventFor<RandomEventsInitializationChorePacket>();
            initializationChorePacketSubscription = initializationChorePacketHook.GetBaseHook().Observable.Subscribe(OnInitializationChorePacketReceived);
            batchChorePacketHook = GameNetworkAPI.Instance.GetPacketEventFor<RandomEventsBatchChorePacket>();
            batchChorePacketSubscription = batchChorePacketHook.GetBaseHook().Observable.Subscribe(OnBatchChorePacketReceived);
            signpostChorePacketHook = GameNetworkAPI.Instance.GetPacketEventFor<RandomEventsSignpostChorePacket>();
            signpostChorePacketSubscription = signpostChorePacketHook.GetBaseHook().Observable.Subscribe(OnSignpostChorePacketReceived);
            initializationAckPacketHook = GameNetworkAPI.Instance.GetPacketEventFor<RandomEventsInitializationAckPacket>();
            initializationAckPacketSubscription = initializationAckPacketHook.GetBaseHook().Observable
                .Subscribe(OnInitializationAckPacketReceived);
            networkInitialized = true;
            LogDebug($"Random Events Chore packets registered eagerly: initialization={initializationChorePacketHook.GetPacketId()}, batch={batchChorePacketHook.GetPacketId()}, signpost={signpostChorePacketHook.GetPacketId()}, protocolVersion={ChoreProtocolVersion}.");
            LogDebug($"Random Events initialization-ACK packet registered eagerly: packetId={initializationAckPacketHook.GetPacketId()}, protocolVersion={ChoreProtocolVersion}.");
            LogDebug($"Random Events Script Extender binary: {RandomEventsDiagnostics.DescribeScriptExtenderBinary()}.");
            string serializerTests = RandomEventsDiagnostics.RunSerializerSelfTests(ChoreProtocolVersion);
            LogDebug($"Random Events Chore serializer self-tests passed: {serializerTests}.");
        }

        public void Initialize()
        {
            if (initialized)
                return;

            subscriptions.Add(MapLoaderR3EventHooks.OnStartMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(OnStartMap));
            subscriptions.Add(MapLoaderR3EventHooks.OnUnloadMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(OnUnloadMap));
            GameTimeManagerAPI.Instance.OnTick += OnGameTick;

            if (!ModSaveDataAPI.Instance.RegisterModDataHandler(
                    SaveDataIdentifier,
                    SaveState,
                    LoadState,
                    ResetMapState))
            {
                throw new InvalidOperationException("Random Events save-data handler registration failed.");
            }

            initialized = true;
        }

        public void Dispose()
        {
            if (disposed) return;
            GameTimeManagerAPI.Instance.OnTick -= OnGameTick;
            foreach (IDisposable subscription in subscriptions)
                subscription.Dispose();
            subscriptions.Clear();
            signpostPlacement.Dispose();
            ModSaveDataAPI.Instance.UnregisterModDataHandler(SaveDataIdentifier);
            disposed = true;
        }

        private void OnStartMap(MapStartEventArgs args)
        {
            mapStartPending = true;
            mapActive = false;
            mapStartedFromMultiplayerSave = args.bMultiplayerSave != 0;
            lastSignpostAttemptTick = int.MinValue;
            mapStartTimestamp = Stopwatch.GetTimestamp();
            startupDelayLogged = false;
        }

        private void OnUnloadMap(MapUnloadEventArgs args)
        {
            ResetMapState();
        }

        private void ResetMapState()
        {
            mapStartPending = false;
            mapActive = false;
            loadedStateAvailable = false;
            mapStartedFromMultiplayerSave = false;
            banditEventsEnabled = true;
            isRealMultiplayer = false;
            isLocalHost = false;
            initializationChoreQueued = false;
            multiplayerInitializationReceived = false;
            multiplayerInitializationConfirmed = false;
            batchChoreQueued = false;
            signpostChoreQueued = false;
            startupDelayLogged = false;
            initializationOperationId = 0;
            initializationAttemptCount = 0;
            acceptedInitializationOperationId = 0;
            lastRandomEventsChoreQueuedTick = int.MinValue;
            mapStartTimestamp = 0;
            lastInitializationSendTimestamp = 0;
            initializationStateDigest = Array.Empty<byte>();
            cachedInitializationBody = Array.Empty<byte>();
            cachedInitializationBodyHash = string.Empty;
            cachedInitializationCooldownEncoding = RandomEventsCooldownEncoding.None;
            acceptedInitializationBodyHash = string.Empty;
            initializationAcknowledgedPlayerIds.Clear();
            pendingBanditGroups.Clear();
            signpostPlacement.ResetMapState();
            state = null;
            loadedSaveState = null;
            deferredPreparedState = null;
        }

        private void OnGameTick(int tick)
        {
            try
            {
                if (mapStartPending)
                {
                    // OnStartMap(Post) can precede the running simulation. The first positive map tick is late
                    // enough for native map/GameData setup, while still preceding all Random Events work.
                    if (GameTimeManagerAPI.Instance.GetElapsedMapTicks() <= 0)
                        return;
                    InitializeCurrentMap();
                }

                if (!mapActive || state == null)
                    return;

                ProcessPendingBanditGroups();

                int currentAbsoluteMonth = GetCurrentAbsoluteMonth();
                if (isRealMultiplayer)
                {
                    if (!multiplayerInitializationConfirmed)
                    {
                        if (isLocalHost)
                            ProcessInitializationHandshake(tick);
                        return;
                    }

                    if (isLocalHost && state.BatchPrepared && !batchChoreQueued)
                    {
                        TryQueueBatchChore();
                        return;
                    }

                    RetrySignpostInitialization(tick);
                    if (!isLocalHost || initializationChoreQueued || batchChoreQueued || signpostChoreQueued ||
                        (RandomEventDefinitions.RequiresSignposts(state.Chances) && !state.SignpostsInitialized))
                        return;

                    if (currentAbsoluteMonth >= state.NextDueAbsoluteMonth)
                    {
                        if (!state.BatchPrepared && !PrepareBatch())
                            return;
                        TryQueueBatchChore();
                    }
                    return;
                }

                if (state.BatchPrepared && currentAbsoluteMonth >= state.NextDueAbsoluteMonth)
                {
                    ExecuteDueBatch();
                    return;
                }

                if (!state.BatchPrepared)
                    PrepareBatch();

                RetrySignpostInitialization(tick);
            }
            catch (Exception ex)
            {
                LogError($"Persistent game-tick processing failed: {ex}");
            }
        }

        private void InitializeCurrentMap()
        {
            mapStartPending = false;

            Shared.GameModeSnapshot gameMode = Shared.GameModeHelper.Capture(mapStartedFromMultiplayerSave);
            string gameModeDetails = gameMode.ToDiagnosticString();
            if (gameMode.IsRealMultiplayer)
            {
                InitializeMultiplayerMap(gameModeDetails);
                return;
            }

            if (gameMode.IsMapEditor)
            {
                LogDebug("Random Events disabled for map editor session.");
                state = null;
                return;
            }

            if (!gameMode.IsSingleplayerSkirmishMode)
            {
                LogDebug("Random Events disabled because the map is neither a singleplayer skirmish nor a singleplayer Trail mission.");
                state = null;
                return;
            }

            RandomEventsConfigurationSnapshot configuration = CaptureConfiguration();
            if (loadedStateAvailable && TryCreateLoadedRuntimeState(configuration, out state))
            {
                // Native path components can change with the restored map, so signposts must be revalidated.
                state.SignpostsInitialized = !RandomEventDefinitions.RequiresSignposts(state.Chances);
                state.SignpostBuildingIds = new[] { -1, -1, -1, -1 };
                mapActive = configuration.Enabled;
                return;
            }

            loadedStateAvailable = false;
            if (!settings.EnableMod)
            {
                LogDebug("Random Events disabled by the effective map setting.");
                state = null;
                return;
            }

            state = CreateFreshState(configuration);
            mapActive = true;
            PrepareBatch();
        }

        private void InitializeMultiplayerMap(string gameModeDetails)
        {
            isRealMultiplayer = true;
            isLocalHost = GameNetworkAPI.IsLocalHost();
            if (!networkInitialized || initializationChorePacketHook == null || batchChorePacketHook == null ||
                signpostChorePacketHook == null || initializationAckPacketHook == null ||
                !ChoreNetworkTransport.IsAvailable)
            {
                DisableForNetwork("tick-aligned Chore transport is unavailable", gameModeDetails);
                return;
            }

            if (!settings.EnableMod)
            {
                DisableForNetwork("disabled by the effective host setting", gameModeDetails);
                return;
            }

            if (!isLocalHost)
            {
                if (multiplayerInitializationReceived && state != null)
                {
                    mapActive = state.EffectiveEnabled;
                    return;
                }

                state = null;
                loadedStateAvailable = false;
                mapActive = false;
                LogDebug($"Random Events multiplayer client waiting for host initialization Chore. Network details: {gameModeDetails}.");
                return;
            }

            RandomEventsConfigurationSnapshot configuration = CaptureConfiguration();
            if (!(loadedStateAvailable && TryCreateLoadedRuntimeState(configuration, out state)))
            {
                loadedStateAvailable = false;
                state = CreateFreshState(configuration);
            }
            else
            {
                CaptureDeferredPreparedBatch();
                // Revalidate restored native signposts on every peer after the host snapshot arrives.
                state.SignpostsInitialized = !RandomEventDefinitions.RequiresSignposts(state.Chances);
                state.SignpostBuildingIds = new[] { -1, -1, -1, -1 };
            }

            mapActive = state.EffectiveEnabled;
            LogDebug(
                $"Random Events multiplayer host prepared initialization and will wait for startup stability: " +
                $"minimumElapsedMilliseconds={MultiplayerStartupDelayMilliseconds}, minimumElapsedTicks={MultiplayerStartupMinimumTicks}, " +
                $"networkDetails={gameModeDetails}.");
        }

        private void RetrySignpostInitialization(int tick)
        {
            if (state == null || state.SignpostsInitialized || !RandomEventDefinitions.RequiresSignposts(state.Chances) ||
                (lastSignpostAttemptTick != int.MinValue && tick - lastSignpostAttemptTick < 30))
            {
                return;
            }

            lastSignpostAttemptTick = tick;
            if (isRealMultiplayer)
            {
                if (isLocalHost && multiplayerInitializationConfirmed && !signpostChoreQueued)
                    TryQueueSignpostInitializationChore();
                return;
            }

            signpostPlacement.TryInitialize(state);
        }

        private RandomEventsRuntimeState CreateFreshState(RandomEventsConfigurationSnapshot configuration)
        {
            byte[] seed = new byte[16];
            using (RandomNumberGenerator generator = RandomNumberGenerator.Create())
                generator.GetBytes(seed);

            // The native calendar has no elapsed-month counter, so persist this absolute baseline with the map.
            int startAbsoluteMonth = GetCurrentAbsoluteMonth();
            var result = new RandomEventsRuntimeState
            {
                PrngState0 = BitConverter.ToUInt64(seed, 0),
                PrngState1 = BitConverter.ToUInt64(seed, 8),
                NextDueAbsoluteMonth = checked(startAbsoluteMonth + configuration.IntervalMonths),
                StartAbsoluteMonth = startAbsoluteMonth,
                SharedCooldownUntilAbsoluteMonths = new int[EventKindCount],
                IndividualCooldownUntilAbsoluteMonths = new int[(GamePlayerManagerAPI.MAX_PLAYERS + 1) * EventKindCount],
                SignpostsInitialized = !RandomEventDefinitions.RequiresSignposts(configuration.Chances)
            };
            configuration.ApplyTo(result);
            return result;
        }

        private RandomEventsConfigurationSnapshot CaptureConfiguration()
        {
            int[] minimums = new int[6];
            int[] maximums = new int[6];
            for (int index = 0; index < 6; index++)
                settings.GetStrengthRange((RandomEventStrengthKind)(index + 1), out minimums[index], out maximums[index]);
            int[] chances = settings.SnapshotChances();
            for (int index = 0; index < chances.Length; index++)
                chances[index] = Math.Max(0, Math.Min(100, chances[index]));
            return new RandomEventsConfigurationSnapshot
            {
                Enabled = settings.EnableMod,
                IntervalMonths = Math.Max(1, Math.Min(90, settings.IntervalMonths)),
                CooldownMonths = Math.Max(0, Math.Min(90, settings.CooldownMonths)),
                MultiplayerMode = Math.Max(0, Math.Min(1, settings.MultiplayerEventModeIndex)),
                Chances = chances,
                StrengthMinimums = minimums,
                StrengthMaximums = maximums
            };
        }

        private bool PrepareBatch()
        {
            int[] humanTargetPlayerIds = GetLivingHumanPlayerIds();
            if (humanTargetPlayerIds.Length == 0)
                return false;
            Array.Sort(humanTargetPlayerIds);

            string prngBefore = RandomEventsDiagnostics.FormatPrng(state.PrngState0, state.PrngState1);
            SavedPrng prng = new SavedPrng(state.PrngState0, state.PrngState1);
            List<int> directKinds = new List<int>();
            List<int> directStrengths = new List<int>();
            List<int> directTargetPlayerIds = new List<int>();

            if (state.MultiplayerMode == (int)MultiplayerEventMode.SharedEvents)
            {
                // One roll and one strength are shared by every living human player.
                foreach (RandomEventDefinition definition in RandomEventDefinitions.All)
                {
                    if (!IsEventOffCooldown(definition.Kind, -1, state.NextDueAbsoluteMonth))
                        continue;

                    int roll = prng.Next(100);
                    if (roll >= state.Chances[(int)definition.Kind])
                        continue;

                    int strength = RollStrength(definition.StrengthKind, ref prng);
                    foreach (int targetPlayerId in humanTargetPlayerIds)
                    {
                        directKinds.Add((int)definition.Kind);
                        directStrengths.Add(strength);
                        directTargetPlayerIds.Add(targetPlayerId);
                    }
                }
            }
            else
            {
                // Every player consumes a separate chance and strength roll, but the host records
                // the resulting global action order so no peer may advance either PRNG differently.
                foreach (int targetPlayerId in humanTargetPlayerIds)
                {
                    foreach (RandomEventDefinition definition in RandomEventDefinitions.All)
                    {
                        if (!IsEventOffCooldown(definition.Kind, targetPlayerId, state.NextDueAbsoluteMonth))
                            continue;

                        int roll = prng.Next(100);
                        if (roll >= state.Chances[(int)definition.Kind])
                            continue;

                        directKinds.Add((int)definition.Kind);
                        directStrengths.Add(RollStrength(definition.StrengthKind, ref prng));
                        directTargetPlayerIds.Add(targetPlayerId);
                    }
                }
            }

            state.PrngState0 = prng.State0;
            state.PrngState1 = prng.State1;
            state.PreparedDirectKinds = directKinds.ToArray();
            state.PreparedDirectStrengths = directStrengths.ToArray();
            state.PreparedDirectTargetPlayerIds = directTargetPlayerIds.ToArray();
            state.BatchPrepared = true;
            string actionDigest = RandomEventsDiagnostics.GetActionDigest(
                state.PreparedDirectKinds,
                state.PreparedDirectStrengths,
                state.PreparedDirectTargetPlayerIds);
            LogDebug(
                $"Random Events batch prepared: mode={(MultiplayerEventMode)state.MultiplayerMode}, " +
                $"dueAbsoluteMonth={state.NextDueAbsoluteMonth}, actions={directKinds.Count}, humanPlayers=[{string.Join(",", humanTargetPlayerIds)}], " +
                $"prngBefore={prngBefore}, prngAfter={RandomEventsDiagnostics.FormatPrng(state.PrngState0, state.PrngState1)}, " +
                $"actionDigest={actionDigest}, actionOrder={RandomEventsDiagnostics.DescribeActions(state.PreparedDirectKinds, state.PreparedDirectStrengths, state.PreparedDirectTargetPlayerIds)}, " +
                $"stateDigest={RandomEventsDiagnostics.GetStateDigest(state)}.");
            return true;
        }

        private int RollStrength(RandomEventStrengthKind kind, ref SavedPrng prng)
        {
            if (kind == RandomEventStrengthKind.None)
                return 0;
            int index = (int)kind - 1;
            return prng.NextInclusive(state.StrengthMinimums[index], state.StrengthMaximums[index]);
        }

        private void ProcessInitializationHandshake(int tick)
        {
            if (multiplayerInitializationConfirmed || state == null)
                return;

            int elapsedTicks = GameTimeManagerAPI.Instance.GetElapsedMapTicks();
            if (elapsedTicks < MultiplayerStartupMinimumTicks ||
                !HasElapsedMilliseconds(mapStartTimestamp, MultiplayerStartupDelayMilliseconds))
            {
                if (!startupDelayLogged)
                {
                    startupDelayLogged = true;
                    LogDebug(
                        $"Random Events multiplayer initialization delayed until the map startup is stable: " +
                        $"requiredMilliseconds={MultiplayerStartupDelayMilliseconds}, requiredTicks={MultiplayerStartupMinimumTicks}, " +
                        $"currentTicks={elapsedTicks}.");
                }
                return;
            }

            int retryMilliseconds = GetInitializationRetryMilliseconds(initializationAttemptCount);
            if (lastInitializationSendTimestamp != 0 && !HasElapsedMilliseconds(lastInitializationSendTimestamp, retryMilliseconds))
                return;

            if (!TryQueueInitializationChore())
            {
                lastInitializationSendTimestamp = Stopwatch.GetTimestamp();
                LogWarning(
                    $"Random Events initialization handshake could not queue its Chore and will retry: tick={tick}, " +
                    $"retryMilliseconds={retryMilliseconds}.");
            }
        }

        private bool TryQueueInitializationChore()
        {
            if (state == null)
                return false;
            if (cachedInitializationBody.Length == 0 && !CreateInitializationAttempt())
                return false;
            if (!TrySendRawChore(initializationChorePacketHook.GetPacketId(), cachedInitializationBody, initializationOperationId, "initialization"))
            {
                initializationChoreQueued = false;
                return false;
            }
            initializationAttemptCount++;
            initializationChoreQueued = true;
            lastInitializationSendTimestamp = Stopwatch.GetTimestamp();
            int[] expected = GetLivingHumanPlayerIds();
            var missing = new List<int>();
            foreach (int playerId in expected) if (!initializationAcknowledgedPlayerIds.Contains(playerId)) missing.Add(playerId);
            LogDebug(
                $"Random Events initialization Chore queued: attempt={initializationAttemptCount}, operationId={initializationOperationId}, " +
                $"bodyBytes={cachedInitializationBody.Length}, bodySha256={cachedInitializationBodyHash}, cooldownEncoding={cachedInitializationCooldownEncoding}, " +
                $"configurationDigest={RandomEventsDiagnostics.ToHex(state.ConfigurationDigest)}, stateDigest={RandomEventsDiagnostics.ToHex(initializationStateDigest)}, " +
                $"expectedPlayers=[{string.Join(",", expected)}], missingPlayers=[{string.Join(",", missing)}].");
            return true;
        }

        private bool CreateInitializationAttempt()
        {
            initializationOperationId = NextOperationId();
            initializationStateDigest = RandomEventsDiagnostics.GetStateDigestBytes(state);
            RandomEventsCooldownPayload[] candidates = RandomEventsCooldownCodec.CreateCandidates(state);
            byte[] smallestBody = null;
            foreach (RandomEventsCooldownPayload cooldown in candidates)
            {
                var packet = new RandomEventsInitializationChorePacket
                {
                    ProtocolVersion = ChoreProtocolVersion, OperationId = initializationOperationId,
                    ConfigurationDigest = (byte[])state.ConfigurationDigest.Clone(),
                    PrngState0 = state.PrngState0, PrngState1 = state.PrngState1,
                    NextDueAbsoluteMonth = state.NextDueAbsoluteMonth, StartAbsoluteMonth = state.StartAbsoluteMonth,
                    CooldownEncoding = (int)cooldown.Encoding, CooldownData = (int[])cooldown.Data.Clone()
                };
                byte[] body = RandomEventsDiagnostics.SerializeAndVerify(packet);
                if (smallestBody == null || body.Length < smallestBody.Length)
                {
                    smallestBody = body;
                    cachedInitializationCooldownEncoding = cooldown.Encoding;
                }
            }
            cachedInitializationBody = smallestBody ?? Array.Empty<byte>();
            cachedInitializationBodyHash = RandomEventsDiagnostics.HashBytes(cachedInitializationBody);
            initializationAcknowledgedPlayerIds.Clear();
            return cachedInitializationBody.Length > 0;
        }

        private static int GetInitializationRetryMilliseconds(int attempts)
        {
            if (attempts <= 0) return 0;
            if (attempts == 1) return 5000;
            if (attempts == 2) return 10000;
            if (attempts == 3) return 20000;
            return 30000;
        }

        private bool TryQueueBatchChore()
        {
            if (state == null || !state.BatchPrepared)
                return false;

            var packet = new RandomEventsBatchChorePacket
            {
                ProtocolVersion = ChoreProtocolVersion,
                OperationId = NextOperationId(),
                PrngState0 = state.PrngState0,
                PrngState1 = state.PrngState1,
                DueAbsoluteMonth = state.NextDueAbsoluteMonth,
                EventKinds = (int[])(state.PreparedDirectKinds ?? Array.Empty<int>()).Clone(),
                EventStrengths = (int[])(state.PreparedDirectStrengths ?? Array.Empty<int>()).Clone(),
                TargetPlayerIds = (int[])(state.PreparedDirectTargetPlayerIds ?? Array.Empty<int>()).Clone()
            };

            if (!ValidateActionArrays(packet))
            {
                LogError($"Random Events event batch failed local validation and was not queued: operationId={packet.OperationId}.");
                return false;
            }

            byte[] body = RandomEventsDiagnostics.SerializeAndVerify(packet);
            if (!TrySendRawChore(batchChorePacketHook.GetPacketId(), body, packet.OperationId, "event batch"))
                return false;

            batchChoreQueued = true;
            return true;
        }

        private bool TryQueueSignpostInitializationChore()
        {
            var packet = new RandomEventsSignpostChorePacket
            {
                ProtocolVersion = ChoreProtocolVersion,
                OperationId = NextOperationId()
            };
            byte[] body = RandomEventsDiagnostics.SerializeAndVerify(packet);
            if (!TrySendRawChore(signpostChorePacketHook.GetPacketId(), body, packet.OperationId, "signpost initialization"))
                return false;

            signpostChoreQueued = true;
            return true;
        }

        private bool TrySendRawChore(short packetId, byte[] body, int operationId, string label)
        {
            if (!networkInitialized || !ChoreNetworkTransport.IsAvailable)
            {
                LogError($"Random Events {label} refused because the Chore transport is unavailable.");
                return false;
            }

            int queueTick = GameTimeManagerAPI.Instance.GetElapsedMapTicks();
            if (isRealMultiplayer && queueTick == lastRandomEventsChoreQueuedTick)
            {
                LogWarning(
                    $"Random Events {label} Chore deferred because another Random Events Chore was already queued this tick: " +
                    $"tick={queueTick}, operationId={operationId}.");
                return false;
            }

            byte[] blob = new byte[sizeof(short) + body.Length];
            if (blob.Length > MaximumChorePayloadBytes)
            {
                LogError(
                    $"Random Events {label} refused because its serialized Chore exceeds the Script Extender limit: " +
                    $"operationId={operationId}, payloadBytes={blob.Length}, limit={MaximumChorePayloadBytes}.");
                return false;
            }
            BitConverter.GetBytes(packetId).CopyTo(blob, 0);
            Buffer.BlockCopy(body, 0, blob, sizeof(short), body.Length);
            Func<byte[], bool> sendRawBlob = ChoreNetworkTransport.SendRawBlob;
            bool queued = sendRawBlob != null && sendRawBlob(blob);
            if (!queued)
            {
                LogError($"Random Events {label} Chore was not queued; no local simulation action was applied: operationId={operationId}, payloadBytes={blob.Length}.");
                return false;
            }

            lastRandomEventsChoreQueuedTick = queueTick;
            LogDebug($"Random Events {label} Chore queued: packetId={packetId}, operationId={operationId}, bodyBytes={body.Length}, payloadBytes={blob.Length}, bodySha256={RandomEventsDiagnostics.HashBytes(body)}.");
            return true;
        }

        private void OnInitializationChorePacketReceived(ReceiveCustomPacketEventArgs<RandomEventsInitializationChorePacket> args)
        {
            RandomEventsInitializationChorePacket packet = args?.Packet;
            if (packet == null || packet.ProtocolVersion != ChoreProtocolVersion)
            {
                LogError("Random Events rejected an initialization Chore with an unsupported payload.");
                return;
            }
            try
            {
                byte[] receivedBody = RandomEventsDiagnostics.SerializeAndVerify(packet);
                ApplyInitializationChore(packet, RandomEventsDiagnostics.HashBytes(receivedBody));
            }
            catch (Exception ex)
            {
                mapActive = false;
                LogError($"Random Events initialization Chore execution failed: operationId={packet.OperationId}, exception={ex}");
            }
        }

        private void OnBatchChorePacketReceived(ReceiveCustomPacketEventArgs<RandomEventsBatchChorePacket> args)
        {
            try { ApplyBatchChore(args?.Packet); }
            catch (Exception ex) { mapActive = false; LogError($"Random Events batch Chore execution failed: {ex}"); }
        }

        private void OnSignpostChorePacketReceived(ReceiveCustomPacketEventArgs<RandomEventsSignpostChorePacket> args)
        {
            try { ApplySignpostInitializationChore(args?.Packet); }
            catch (Exception ex) { mapActive = false; LogError($"Random Events signpost Chore execution failed: {ex}"); }
        }

        private void OnInitializationAckPacketReceived(
            ReceiveCustomPacketEventArgs<RandomEventsInitializationAckPacket> args)
        {
            if (!isRealMultiplayer || !isLocalHost || multiplayerInitializationConfirmed)
                return;

            try
            {
                RandomEventsInitializationAckPacket packet = args?.Packet;
                if (packet == null || packet.ProtocolVersion != ChoreProtocolVersion ||
                    packet.OperationId != initializationOperationId ||
                    packet.PlayerId < 1 || packet.PlayerId > GamePlayerManagerAPI.MAX_PLAYERS ||
                    !RandomEventsDiagnostics.BytesEqual(packet.StateDigest, initializationStateDigest))
                {
                    LogWarning(
                        $"Random Events rejected an invalid initialization ACK: " +
                        $"protocolVersion={packet?.ProtocolVersion}, operationId={packet?.OperationId}, " +
                        $"expectedOperationId={initializationOperationId}, playerId={packet?.PlayerId}, " +
                        $"stateDigest={RandomEventsDiagnostics.ToHex(packet?.StateDigest)}, expectedStateDigest={RandomEventsDiagnostics.ToHex(initializationStateDigest)}.");
                    return;
                }

                int[] expectedPlayers = GetLivingHumanPlayerIds();
                if (Array.IndexOf(expectedPlayers, packet.PlayerId) < 0)
                {
                    LogWarning(
                        $"Random Events rejected an initialization ACK from a non-participating player: " +
                        $"playerId={packet.PlayerId}, expectedPlayers=[{string.Join(",", expectedPlayers)}].");
                    return;
                }

                // The sender supplied by the current Script Extender may be unavailable in-game.
                // PlayerId is used only as a readiness receipt; it never authorizes a simulation action.
                initializationAcknowledgedPlayerIds.Add(packet.PlayerId);
                LogDebug(
                    $"Random Events initialization ACK accepted: operationId={packet.OperationId}, " +
                    $"playerId={packet.PlayerId}, stateDigest={RandomEventsDiagnostics.ToHex(packet.StateDigest)}.");
                TryCompleteInitializationHandshake();
            }
            catch (Exception ex)
            {
                mapActive = false;
                LogError($"Random Events initialization ACK processing failed; the handshake remains locked: {ex}");
            }
        }

        private void ApplyInitializationChore(RandomEventsInitializationChorePacket packet, string bodyHash)
        {
            RandomEventsConfigurationSnapshot configuration = CaptureConfiguration();
            byte[] localConfigurationDigest = configuration.GetDigest();
            if (packet == null || packet.OperationId <= 0 || (packet.PrngState0 | packet.PrngState1) == 0 ||
                packet.NextDueAbsoluteMonth < packet.StartAbsoluteMonth ||
                !RandomEventsDiagnostics.BytesEqual(packet.ConfigurationDigest, localConfigurationDigest))
            {
                mapActive = false;
                LogError($"Random Events rejected initialization: operationId={packet?.OperationId}, hostConfigurationDigest={RandomEventsDiagnostics.ToHex(packet?.ConfigurationDigest)}, localConfigurationDigest={RandomEventsDiagnostics.ToHex(localConfigurationDigest)}.");
                return;
            }

            if (acceptedInitializationOperationId != 0)
            {
                if (packet.OperationId == acceptedInitializationOperationId && string.Equals(bodyHash, acceptedInitializationBodyHash, StringComparison.Ordinal))
                {
                    initializationChoreQueued = false;
                    SendInitializationAck();
                    LogDebug($"Random Events duplicate initialization acknowledged without state replay: operationId={packet.OperationId}, bodySha256={bodyHash}.");
                    return;
                }
                if (packet.OperationId < acceptedInitializationOperationId)
                {
                    LogWarning($"Random Events ignored stale initialization: operationId={packet.OperationId}, acceptedOperationId={acceptedInitializationOperationId}.");
                    return;
                }
                mapActive = false;
                LogError($"Random Events initialization protocol conflict: operationId={packet.OperationId}, acceptedOperationId={acceptedInitializationOperationId}, bodySha256={bodyHash}, acceptedBodySha256={acceptedInitializationBodyHash}.");
                return;
            }

            RandomEventsCooldownCodec.Decode(configuration.MultiplayerMode, packet.CooldownEncoding, packet.CooldownData, out int[] shared, out int[] individual);
            if (!isLocalHost)
            {
                state = new RandomEventsRuntimeState
                {
                    PrngState0 = packet.PrngState0, PrngState1 = packet.PrngState1,
                    NextDueAbsoluteMonth = packet.NextDueAbsoluteMonth, StartAbsoluteMonth = packet.StartAbsoluteMonth,
                    SharedCooldownUntilAbsoluteMonths = shared, IndividualCooldownUntilAbsoluteMonths = individual,
                    SignpostsInitialized = !RandomEventDefinitions.RequiresSignposts(configuration.Chances),
                    SignpostBuildingIds = new[] { -1, -1, -1, -1 }
                };
                configuration.ApplyTo(state);
            }
            mapActive = state.EffectiveEnabled;
            isRealMultiplayer = true;
            multiplayerInitializationReceived = true;
            loadedStateAvailable = false;
            initializationChoreQueued = false;
            batchChoreQueued = false;
            signpostChoreQueued = false;
            lastSignpostAttemptTick = int.MinValue;
            acceptedInitializationOperationId = packet.OperationId;
            acceptedInitializationBodyHash = bodyHash;

            byte[] stateDigest = RandomEventsDiagnostics.GetStateDigestBytes(state);
            if (isLocalHost)
            {
                if (packet.OperationId != initializationOperationId ||
                    !string.Equals(bodyHash, cachedInitializationBodyHash, StringComparison.Ordinal) ||
                    !RandomEventsDiagnostics.BytesEqual(stateDigest, initializationStateDigest))
                {
                    mapActive = false;
                    LogError(
                        $"Random Events host initialization state did not match its queued handshake and was disabled: " +
                        $"packetOperationId={packet.OperationId}, expectedOperationId={initializationOperationId}, " +
                        $"stateDigest={RandomEventsDiagnostics.ToHex(stateDigest)}, expectedStateDigest={RandomEventsDiagnostics.ToHex(initializationStateDigest)}.");
                    return;
                }

                initializationAcknowledgedPlayerIds.Add(GamePlayerManagerAPI.Instance.GetLocalPlayerId());
                TryCompleteInitializationHandshake();
            }
            else
            {
                multiplayerInitializationConfirmed = true;
                initializationOperationId = packet.OperationId;
                initializationStateDigest = stateDigest;
                SendInitializationAck();
            }

            // Every peer starts from the host's private PRNG state. Signpost placement derives a
            // separate deterministic stream and therefore never perturbs Vanilla's synchronized RNG.
            LogDebug(
                $"Random Events initialization Chore executed: operationId={packet.OperationId}, mode={(MultiplayerEventMode)state.MultiplayerMode}, " +
                $"nextDueAbsoluteMonth={state.NextDueAbsoluteMonth}, prng={RandomEventsDiagnostics.FormatPrng(state.PrngState0, state.PrngState1)}, " +
                $"actionDigest={RandomEventsDiagnostics.GetActionDigest(state.PreparedDirectKinds, state.PreparedDirectStrengths, state.PreparedDirectTargetPlayerIds)}, " +
                $"stateDigest={RandomEventsDiagnostics.ToHex(stateDigest)}, bodySha256={bodyHash}, localHandshakeReady={multiplayerInitializationConfirmed}.");
        }

        private void SendInitializationAck()
        {
            if (initializationAckPacketHook == null || state == null)
            {
                mapActive = false;
                LogError("Random Events client could not acknowledge initialization because its ACK packet hook or state is unavailable.");
                return;
            }

            var packet = new RandomEventsInitializationAckPacket
            {
                ProtocolVersion = ChoreProtocolVersion,
                OperationId = initializationOperationId,
                PlayerId = GamePlayerManagerAPI.Instance.GetLocalPlayerId(),
                StateDigest = initializationStateDigest
            };

            try
            {
                if (packet.PlayerId < 1 || packet.PlayerId > GamePlayerManagerAPI.MAX_PLAYERS)
                    throw new InvalidOperationException($"local player ID {packet.PlayerId} is invalid");

                byte[] body = RandomEventsDiagnostics.SerializeAndVerify(packet);
                // ACKs are control-plane receipts. They deliberately use the ordinary packet path so
                // they cannot consume another synchronized simulation Chore in the same game tick.
                GameNetworkAPI.SendPacketToAll(packet, initializationAckPacketHook.GetPacketId());
                LogDebug(
                    $"Random Events initialization ACK sent: packetId={initializationAckPacketHook.GetPacketId()}, " +
                    $"operationId={packet.OperationId}, playerId={packet.PlayerId}, bodyBytes={body.Length}, " +
                    $"bodySha256={RandomEventsDiagnostics.HashBytes(body)}, stateDigest={RandomEventsDiagnostics.ToHex(packet.StateDigest)}.");
            }
            catch (Exception ex)
            {
                mapActive = false;
                multiplayerInitializationConfirmed = false;
                LogError(
                    $"Random Events client initialization ACK failed; local event processing was disabled: " +
                    $"operationId={packet.OperationId}, playerId={packet.PlayerId}, error={ex}");
            }
        }

        private void TryCompleteInitializationHandshake()
        {
            if (!isLocalHost || multiplayerInitializationConfirmed || initializationOperationId <= 0)
                return;

            int[] expectedPlayers = GetLivingHumanPlayerIds();
            Array.Sort(expectedPlayers);
            var missingPlayers = new List<int>();
            foreach (int playerId in expectedPlayers)
            {
                if (!initializationAcknowledgedPlayerIds.Contains(playerId))
                    missingPlayers.Add(playerId);
            }

            if (missingPlayers.Count != 0)
            {
                LogDebug(
                    $"Random Events initialization handshake waiting: operationId={initializationOperationId}, " +
                    $"acknowledgedPlayers=[{string.Join(",", initializationAcknowledgedPlayerIds)}], " +
                    $"missingPlayers=[{string.Join(",", missingPlayers)}], stateDigest={RandomEventsDiagnostics.ToHex(initializationStateDigest)}.");
                return;
            }

            multiplayerInitializationConfirmed = true;
            initializationChoreQueued = false;
            lastSignpostAttemptTick = int.MinValue;
            RestoreDeferredPreparedBatch();
            LogDebug(
                $"Random Events initialization handshake completed: operationId={initializationOperationId}, " +
                $"players=[{string.Join(",", expectedPlayers)}], stateDigest={RandomEventsDiagnostics.ToHex(initializationStateDigest)}. " +
                "Signpost and event Chores are now enabled.");
        }

        private void CaptureDeferredPreparedBatch()
        {
            if (state == null || !state.BatchPrepared) return;
            deferredPreparedState = new RandomEventsRuntimeState
            {
                PreparedDirectKinds = (int[])state.PreparedDirectKinds.Clone(),
                PreparedDirectStrengths = (int[])state.PreparedDirectStrengths.Clone(),
                PreparedDirectTargetPlayerIds = (int[])state.PreparedDirectTargetPlayerIds.Clone()
            };
            state.BatchPrepared = false;
            state.PreparedDirectKinds = Array.Empty<int>();
            state.PreparedDirectStrengths = Array.Empty<int>();
            state.PreparedDirectTargetPlayerIds = Array.Empty<int>();
        }

        private void RestoreDeferredPreparedBatch()
        {
            if (state == null || deferredPreparedState == null) return;
            state.BatchPrepared = true;
            state.PreparedDirectKinds = (int[])deferredPreparedState.PreparedDirectKinds.Clone();
            state.PreparedDirectStrengths = (int[])deferredPreparedState.PreparedDirectStrengths.Clone();
            state.PreparedDirectTargetPlayerIds = (int[])deferredPreparedState.PreparedDirectTargetPlayerIds.Clone();
            deferredPreparedState = null;
            LogDebug("Random Events restored one prepared save-game batch after initialization handshake completion.");
        }

        private void ApplySignpostInitializationChore(RandomEventsSignpostChorePacket packet)
        {
            if (packet == null || packet.ProtocolVersion != ChoreProtocolVersion || packet.OperationId <= 0)
            {
                mapActive = false;
                LogError("Random Events rejected an invalid signpost Chore.");
                return;
            }
            signpostChoreQueued = false;
            if (isRealMultiplayer && (!multiplayerInitializationReceived || !multiplayerInitializationConfirmed))
            {
                mapActive = false;
                LogError(
                    $"Random Events rejected signpost initialization before the local initialization handshake completed: " +
                    $"operationId={packet.OperationId}, received={multiplayerInitializationReceived}, confirmed={multiplayerInitializationConfirmed}.");
                return;
            }

            if (state == null || state.SignpostsInitialized || !RandomEventDefinitions.RequiresSignposts(state.Chances))
                return;

            bool completed = signpostPlacement.TryInitialize(state);
            LogDebug($"Random Events signpost initialization Chore executed: operationId={packet.OperationId}, completed={completed}, initialized={state.SignpostsInitialized}.");
        }

        private void ApplyBatchChore(RandomEventsBatchChorePacket packet)
        {
            if (!ValidateActionArrays(packet))
            {
                mapActive = false;
                LogError("Random Events rejected an invalid batch Chore.");
                return;
            }
            if (isRealMultiplayer && (!multiplayerInitializationReceived || !multiplayerInitializationConfirmed))
            {
                mapActive = false;
                LogError(
                    $"Random Events rejected an event batch before the local initialization handshake completed: " +
                    $"operationId={packet.OperationId}, received={multiplayerInitializationReceived}, confirmed={multiplayerInitializationConfirmed}.");
                return;
            }

            if (state == null ||
                packet.DueAbsoluteMonth != state.NextDueAbsoluteMonth ||
                (packet.PrngState0 | packet.PrngState1) == 0)
            {
                mapActive = false;
                LogError($"Random Events rejected an invalid or out-of-sequence batch Chore: operationId={packet?.OperationId}, packetDue={packet?.DueAbsoluteMonth}, localDue={state?.NextDueAbsoluteMonth.ToString() ?? "null"}.");
                return;
            }

            string stateDigestBefore = RandomEventsDiagnostics.GetStateDigest(state);
            string prngBefore = RandomEventsDiagnostics.FormatPrng(state.PrngState0, state.PrngState1);
            state.PrngState0 = packet.PrngState0;
            state.PrngState1 = packet.PrngState1;
            state.PreparedDirectKinds = (int[])packet.EventKinds.Clone();
            state.PreparedDirectStrengths = (int[])packet.EventStrengths.Clone();
            state.PreparedDirectTargetPlayerIds = (int[])packet.TargetPlayerIds.Clone();
            state.BatchPrepared = true;
            batchChoreQueued = false;

            // All direct native mutations now run in one Chore callback and in the host-recorded
            // order. Vanilla GameAction events are queued only by the host below, because they are
            // already native Chores and would otherwise be duplicated once per peer.
            ExecuteDueBatch();
            LogDebug(
                $"Random Events batch Chore executed: operationId={packet.OperationId}, actions={packet.EventKinds.Length}, " +
                $"executedDueAbsoluteMonth={packet.DueAbsoluteMonth}, nextDueAbsoluteMonth={state.NextDueAbsoluteMonth}, " +
                $"prngBeforePacket={prngBefore}, prngFromPacket={RandomEventsDiagnostics.FormatPrng(packet.PrngState0, packet.PrngState1)}, " +
                $"prngAfter={RandomEventsDiagnostics.FormatPrng(state.PrngState0, state.PrngState1)}, actionDigest={RandomEventsDiagnostics.GetActionDigest(packet.EventKinds, packet.EventStrengths, packet.TargetPlayerIds)}, " +
                $"stateDigestBefore={stateDigestBefore}, stateDigestAfter={RandomEventsDiagnostics.GetStateDigest(state)}.");
        }

        private static bool ValidateActionArrays(RandomEventsBatchChorePacket packet)
        {
            if (packet == null || packet.ProtocolVersion != ChoreProtocolVersion || packet.OperationId <= 0 ||
                packet.DueAbsoluteMonth < 0 || (packet.PrngState0 | packet.PrngState1) == 0)
                return false;
            int count = packet.EventKinds?.Length ?? -1;
            if (count < 0 || count > MaximumChoreActions ||
                packet.EventStrengths?.Length != count || packet.TargetPlayerIds?.Length != count)
                return false;

            for (int index = 0; index < count; index++)
            {
                if (packet.EventKinds[index] < 0 || packet.EventKinds[index] >= EventKindCount ||
                    packet.TargetPlayerIds[index] < 1 || packet.TargetPlayerIds[index] > GamePlayerManagerAPI.MAX_PLAYERS)
                    return false;
            }
            return true;
        }

        private int NextOperationId()
        {
            if (nextOperationId == int.MaxValue)
                nextOperationId = 0;
            return ++nextOperationId;
        }

        private void ExecuteDueBatch()
        {
            int due = state.NextDueAbsoluteMonth;
            int[] directKinds = state.PreparedDirectKinds ?? Array.Empty<int>();
            int[] strengths = state.PreparedDirectStrengths ?? Array.Empty<int>();
            int[] targetPlayerIds = state.PreparedDirectTargetPlayerIds ?? Array.Empty<int>();

            // Clear first so a save callback during a Vanilla action cannot persist an executable duplicate.
            state.BatchPrepared = false;
            state.PreparedDirectKinds = Array.Empty<int>();
            state.PreparedDirectStrengths = Array.Empty<int>();
            state.PreparedDirectTargetPlayerIds = Array.Empty<int>();

            for (int index = 0; index < directKinds.Length; index++)
            {
                RandomEventDefinition definition = RandomEventDefinitions.Get((RandomEventKind)directKinds[index]);
                int strength = index < strengths.Length ? strengths[index] : 0;
                int targetPlayerId = index < targetPlayerIds.Length
                    ? targetPlayerIds[index]
                    : -1;
                string prngBefore = RandomEventsDiagnostics.FormatPrng(state.PrngState0, state.PrngState1);
                string stateDigestBefore = RandomEventsDiagnostics.GetStateDigest(state);
                LogDebug(
                    $"Random Events action begin: dueAbsoluteMonth={due}, actionIndex={index}, event={definition.Name}, " +
                    $"dispatchKind={definition.DispatchKind}, targetPlayerId={targetPlayerId}, strength={strength}, " +
                    $"prng={prngBefore}, stateDigest={stateDigestBefore}.");
                // GameAction has no result signal, so its successful roll is the inexpensive success boundary.
                bool cooldownStartedFromRoll = definition.DispatchKind == RandomEventDispatchKind.GameAction;
                if (cooldownStartedFromRoll)
                    StartEventCooldown(definition.Kind, targetPlayerId, due);

                bool effectApplied = DispatchDirectEvent(definition, strength, targetPlayerId);
                if (!cooldownStartedFromRoll && effectApplied)
                    StartEventCooldown(definition.Kind, targetPlayerId, due);
                LogDebug(
                    $"Random Events action end: dueAbsoluteMonth={due}, actionIndex={index}, event={definition.Name}, " +
                    $"dispatchKind={definition.DispatchKind}, targetPlayerId={targetPlayerId}, strength={strength}, effectApplied={effectApplied}, " +
                    $"cooldownStartedFromRoll={cooldownStartedFromRoll}, prngBefore={prngBefore}, " +
                    $"prngAfter={RandomEventsDiagnostics.FormatPrng(state.PrngState0, state.PrngState1)}, " +
                    $"stateDigestBefore={stateDigestBefore}, stateDigestAfter={RandomEventsDiagnostics.GetStateDigest(state)}.");
            }

            state.NextDueAbsoluteMonth = checked(due + state.IntervalMonths);
        }

        private bool IsEventOffCooldown(
            RandomEventKind kind,
            int targetPlayerId,
            int scheduledAbsoluteMonth)
        {
            if (state.CooldownMonths == 0)
                return true;

            int kindIndex = (int)kind;
            if (state.MultiplayerMode == (int)MultiplayerEventMode.SharedEvents)
                return scheduledAbsoluteMonth >= state.SharedCooldownUntilAbsoluteMonths[kindIndex];

            if (targetPlayerId < 1 || targetPlayerId > GamePlayerManagerAPI.MAX_PLAYERS)
                return false;

            int playerEventIndex = targetPlayerId * EventKindCount + kindIndex;
            return scheduledAbsoluteMonth >= state.IndividualCooldownUntilAbsoluteMonths[playerEventIndex];
        }

        private void StartEventCooldown(
            RandomEventKind kind,
            int targetPlayerId,
            int triggeredAbsoluteMonth)
        {
            if (state.CooldownMonths == 0)
                return;

            int cooldownUntil = checked(triggeredAbsoluteMonth + state.CooldownMonths);
            int kindIndex = (int)kind;
            if (state.MultiplayerMode == (int)MultiplayerEventMode.SharedEvents)
            {
                state.SharedCooldownUntilAbsoluteMonths[kindIndex] = cooldownUntil;
                LogDebug(
                    $"Shared event cooldown started: event={kind}, triggeredAbsoluteMonth={triggeredAbsoluteMonth}, " +
                    $"cooldownMonths={state.CooldownMonths}, eligibleAbsoluteMonth={cooldownUntil}.");
                return;
            }

            if (targetPlayerId < 1 || targetPlayerId > GamePlayerManagerAPI.MAX_PLAYERS)
            {
                LogError(
                    $"Individual event cooldown could not start: event={kind}, targetPlayerId={targetPlayerId}, " +
                    "reason=invalid target player.");
                return;
            }

            int playerEventIndex = targetPlayerId * EventKindCount + kindIndex;
            state.IndividualCooldownUntilAbsoluteMonths[playerEventIndex] = cooldownUntil;
            LogDebug(
                $"Individual event cooldown started: event={kind}, targetPlayerId={targetPlayerId}, " +
                $"triggeredAbsoluteMonth={triggeredAbsoluteMonth}, cooldownMonths={state.CooldownMonths}, " +
                $"eligibleAbsoluteMonth={cooldownUntil}.");
        }

        private byte[] SaveState(SaveContext context)
        {
            if (!context.IsSaveFile || !mapActive || state == null)
                return null;
            RandomEventsSaveState saved = CreateSaveState(state);
            if (deferredPreparedState != null)
            {
                saved.BatchPrepared = true;
                saved.PreparedDirectKinds = (int[])deferredPreparedState.PreparedDirectKinds.Clone();
                saved.PreparedDirectStrengths = (int[])deferredPreparedState.PreparedDirectStrengths.Clone();
                saved.PreparedDirectTargetPlayerIds = (int[])deferredPreparedState.PreparedDirectTargetPlayerIds.Clone();
            }
            return MessagePackSerializer.Serialize(saved);
        }

        private void LoadState(byte[] bytes, LoadContext context)
        {
            if (!context.IsSaveFile)
                return;
            try
            {
                loadedSaveState = MessagePackSerializer.Deserialize<RandomEventsSaveState>(bytes);
                loadedStateAvailable = loadedSaveState != null;
                mapStartPending = true;
            }
            catch (Exception ex)
            {
                loadedSaveState = null;
                loadedStateAvailable = false;
                LogError($"Random Events state could not be deserialized and will be initialized fresh: {ex}");
            }
        }

        private bool TryCreateLoadedRuntimeState(RandomEventsConfigurationSnapshot configuration, out RandomEventsRuntimeState restored)
        {
            restored = null;
            RandomEventsSaveState loaded = loadedSaveState;
            bool valid = loaded != null && loaded.SchemaVersion == RandomEventsSaveState.CurrentSchemaVersion &&
                loaded.PreparedDirectKinds != null && loaded.PreparedDirectStrengths != null &&
                loaded.PreparedDirectKinds.Length == loaded.PreparedDirectStrengths.Length &&
                loaded.PreparedDirectTargetPlayerIds != null &&
                loaded.PreparedDirectKinds.Length == loaded.PreparedDirectTargetPlayerIds.Length &&
                loaded.SharedCooldownUntilAbsoluteMonths?.Length == EventKindCount &&
                loaded.IndividualCooldownUntilAbsoluteMonths?.Length ==
                    (GamePlayerManagerAPI.MAX_PLAYERS + 1) * EventKindCount &&
                Array.TrueForAll(loaded.SharedCooldownUntilAbsoluteMonths, month => month >= 0) &&
                Array.TrueForAll(loaded.IndividualCooldownUntilAbsoluteMonths, month => month >= 0) &&
                (loaded.PrngState0 | loaded.PrngState1) != 0;
            if (valid)
            {
                int currentAbsoluteMonth = GetCurrentAbsoluteMonth();
                // Reject states written with an incompatible calendar basis instead of waiting centuries.
                valid = loaded.StartAbsoluteMonth >= 0 &&
                    loaded.StartAbsoluteMonth <= currentAbsoluteMonth &&
                    loaded.NextDueAbsoluteMonth >= currentAbsoluteMonth &&
                    loaded.NextDueAbsoluteMonth <= checked(currentAbsoluteMonth + 90);
                if (!valid)
                {
                    LogWarning(
                        $"Loaded Random Events state uses an implausible event date and will be initialized fresh: " +
                        $"currentAbsoluteMonth={currentAbsoluteMonth}, startAbsoluteMonth={loaded.StartAbsoluteMonth}, " +
                        $"loadedNextDueAbsoluteMonth={loaded.NextDueAbsoluteMonth}, effectiveMonthsPerYear={VanillaMonthsPerYear}.");
                }
            }
            if (!valid)
                LogWarning("Loaded Random Events state failed validation and will not be used.");
            if (!valid) return false;

            restored = new RandomEventsRuntimeState
            {
                PrngState0 = loaded.PrngState0, PrngState1 = loaded.PrngState1,
                NextDueAbsoluteMonth = loaded.NextDueAbsoluteMonth, StartAbsoluteMonth = loaded.StartAbsoluteMonth,
                SharedCooldownUntilAbsoluteMonths = (int[])loaded.SharedCooldownUntilAbsoluteMonths.Clone(),
                IndividualCooldownUntilAbsoluteMonths = (int[])loaded.IndividualCooldownUntilAbsoluteMonths.Clone(),
                BatchPrepared = loaded.BatchPrepared,
                PreparedDirectKinds = (int[])loaded.PreparedDirectKinds.Clone(),
                PreparedDirectStrengths = (int[])loaded.PreparedDirectStrengths.Clone(),
                PreparedDirectTargetPlayerIds = (int[])loaded.PreparedDirectTargetPlayerIds.Clone(),
                SignpostsInitialized = loaded.SignpostsInitialized,
                SignpostBuildingIds = (int[])loaded.SignpostBuildingIds.Clone()
            };
            configuration.ApplyTo(restored);
            if (configuration.MultiplayerMode == (int)MultiplayerEventMode.SharedEvents)
                restored.IndividualCooldownUntilAbsoluteMonths = new int[(GamePlayerManagerAPI.MAX_PLAYERS + 1) * EventKindCount];
            else
                restored.SharedCooldownUntilAbsoluteMonths = new int[EventKindCount];
            loadedSaveState = null;
            return true;
        }

        private static RandomEventsSaveState CreateSaveState(RandomEventsRuntimeState source) => new RandomEventsSaveState
        {
            PrngState0 = source.PrngState0, PrngState1 = source.PrngState1,
            NextDueAbsoluteMonth = source.NextDueAbsoluteMonth, StartAbsoluteMonth = source.StartAbsoluteMonth,
            SharedCooldownUntilAbsoluteMonths = (int[])source.SharedCooldownUntilAbsoluteMonths.Clone(),
            IndividualCooldownUntilAbsoluteMonths = (int[])source.IndividualCooldownUntilAbsoluteMonths.Clone(),
            BatchPrepared = source.BatchPrepared,
            PreparedDirectKinds = (int[])source.PreparedDirectKinds.Clone(),
            PreparedDirectStrengths = (int[])source.PreparedDirectStrengths.Clone(),
            PreparedDirectTargetPlayerIds = (int[])source.PreparedDirectTargetPlayerIds.Clone(),
            SignpostsInitialized = source.SignpostsInitialized,
            SignpostBuildingIds = (int[])source.SignpostBuildingIds.Clone()
        };

        private bool DispatchDirectEvent(
            RandomEventDefinition definition,
            int strength,
            int targetPlayerId)
        {
            if (!GamePlayerManagerAPI.Instance.IsPlayerIdValid(targetPlayerId))
            {
                LogError(
                    $"Vanilla direct event skipped: event={definition.Name}, targetPlayerId={targetPlayerId}, " +
                    "reason=invalid target player.");
                return false;
            }

            if (GamePlayerManagerAPI.Instance.IsAIPlayer(targetPlayerId))
            {
                LogDebug(
                    $"Random event skipped: event={definition.Name}, targetPlayerId={targetPlayerId}, " +
                    "reason=target player is controlled by AI.");
                return false;
            }

            if (!TryGetLivingLord(targetPlayerId, out string lordFailure))
            {
                LogDebug(
                    $"Random event skipped: event={definition.Name}, targetPlayerId={targetPlayerId}, " +
                    $"reason=target player has no living Lord ({lordFailure}).");
                return false;
            }

            if (definition.Kind == RandomEventKind.Bandits || definition.Kind == RandomEventKind.Archers)
            {
                int factorTenths = strength;
                int elapsedMonths = GetElapsedMonthsSinceStart();
                strength = CalculateElapsedScaledUnitCount(elapsedMonths, factorTenths);
                LogDebug(
                    $"Elapsed-time strength calculated: event={definition.Name}, elapsedMonths={elapsedMonths}, " +
                    $"rolledUnitsPerThreeMonths={factorTenths / 10.0:0.0}, totalUnits={strength}.");
                if (strength == 0)
                {
                    LogDebug(
                        $"Random event had no effect: event={definition.Name}, targetPlayerId={targetPlayerId}, " +
                        "reason=elapsed-time strength rounded down to zero units.");
                    return false;
                }
            }

            if (definition.DispatchKind == RandomEventDispatchKind.NativeWildlife)
            {
                if (definition.Kind == RandomEventKind.Rabbits)
                    return SpawnRabbitInfestation(targetPlayerId);
                if (definition.Kind == RandomEventKind.LionAttack)
                    return SpawnLionAttack(targetPlayerId, strength);

                LogError($"Native wildlife event skipped: unsupported event kind {definition.Kind}.");
                return false;
            }

            if (definition.DispatchKind == RandomEventDispatchKind.NativeVanilla)
            {
                try
                {
                    NativeEventDispatchStatus status = nativeEventDispatcher.Dispatch(
                        definition.Kind,
                        strength,
                        targetPlayerId,
                        out string detail);
                    if (status == NativeEventDispatchStatus.Applied)
                    {
                        LogDebug(
                            $"Native Vanilla event dispatched: event={definition.Name}, actionId={definition.VanillaActionId}, " +
                            $"strength={strength}, targetPlayerId={targetPlayerId}, detail={detail}");
                    }
                    else if (status == NativeEventDispatchStatus.PrerequisiteNotMet)
                    {
                        LogDebug(
                            $"Native Vanilla event had no effect: event={definition.Name}, actionId={definition.VanillaActionId}, " +
                            $"targetPlayerId={targetPlayerId}, detail={detail}");
                    }
                    else
                    {
                        LogError(
                            $"Native Vanilla event skipped: event={definition.Name}, actionId={definition.VanillaActionId}, " +
                            $"targetPlayerId={targetPlayerId}, reason={detail}");
                    }
                    return status == NativeEventDispatchStatus.Applied;
                }
                catch (Exception ex)
                {
                    LogError(
                        $"Native Vanilla event failed: event={definition.Name}, actionId={definition.VanillaActionId}, " +
                        $"targetPlayerId={targetPlayerId}, error={ex}");
                    return false;
                }
            }

            if (definition.DispatchKind == RandomEventDispatchKind.ManualBandits)
                return SpawnBanditAttack(targetPlayerId, strength);

            if (isRealMultiplayer && !isLocalHost)
            {
                // GameAction queues a native Vanilla Chore. Only the host may enqueue it; every
                // peer will execute that native Chore later through the game's normal lockstep path.
                LogDebug($"Vanilla GameAction delegated to multiplayer host: event={definition.Name}, targetPlayerId={targetPlayerId}.");
                return true;
            }

            IDisposable signpostScope = null;
            int signpostBuildingId = -1;

            if (definition.RequiresSignpost &&
                !signpostRegistry.TryBeginTargetedEvent(
                    targetPlayerId,
                    out signpostScope,
                    out signpostBuildingId,
                    out _,
                    out string signpostFailure))
            {
                LogError(
                    $"Vanilla direct event skipped: event={definition.Name}, targetPlayerId={targetPlayerId}, " +
                    $"reason=required signpost unavailable or could not be prioritized ({signpostFailure}).");
                return false;
            }

            int originalLocalPlayerId = GamePlayerManagerAPI.Instance.GetLocalPlayerId();
            IntPtr localPlayerAddress = new IntPtr(unchecked((long)GameGlobalsManager.Instance.LocalPlayerIdVA));
            bool playerChanged = targetPlayerId != originalLocalPlayerId;
            try
            {
                if (playerChanged)
                {
                    if (localPlayerAddress == IntPtr.Zero)
                    {
                        LogError(
                            $"Vanilla direct event skipped: event={definition.Name}, targetPlayerId={targetPlayerId}, " +
                            "reason=native local-player address unavailable for explicit targeting.");
                        return false;
                    }

                    Marshal.WriteInt32(localPlayerAddress, targetPlayerId);
                    if (Marshal.ReadInt32(localPlayerAddress) != targetPlayerId)
                    {
                        LogError(
                            $"Vanilla direct event skipped: event={definition.Name}, targetPlayerId={targetPlayerId}, " +
                            "reason=native target-player switch verification failed.");
                        return false;
                    }
                }

                EngineInterface.GameAction(Enums.GameActionCommand.FreeBuild_Event, definition.TextId, strength);
                LogDebug(
                    $"Vanilla direct event dispatched: event={definition.Name}, textId={definition.TextId}, " +
                    $"strength={strength}, targetPlayerId={targetPlayerId}, signpostBuildingId={signpostBuildingId}.");
            }
            finally
            {
                if (playerChanged && localPlayerAddress != IntPtr.Zero)
                    Marshal.WriteInt32(localPlayerAddress, originalLocalPlayerId);
                signpostScope?.Dispose();
            }
            return true;
        }

        private unsafe bool SpawnBanditAttack(int targetPlayerId, int strength)
        {
            if (!banditEventsEnabled)
            {
                LogError(
                    $"Bandit event skipped: targetPlayerId={targetPlayerId}, " +
                    "reason=manual bandit support was disabled after an earlier compatibility failure.");
                return false;
            }

            try
            {
                if (!TryReserveBanditPlayerSlot(
                        targetPlayerId,
                        out int banditOwnerPlayerId,
                        out int banditTeam,
                        out int[] humanPlayerIds,
                        out string slotFailure))
                {
                    LogDebug(
                        $"Bandit event ignored without spawning units: targetPlayerId={targetPlayerId}, " +
                        $"reason={slotFailure}.");
                    return false;
                }

                if (!signpostRegistry.TryGetClosestSignpostToPlayer(
                        targetPlayerId,
                        out int signpostBuildingId,
                        out int spawnTileX,
                        out int spawnTileY,
                        out _,
                        out _,
                        out string signpostFailure))
                {
                    LogError(
                        $"Bandit event skipped: targetPlayerId={targetPlayerId}, " +
                        $"reason=no usable targeted signpost ({signpostFailure}).");
                    return false;
                }

                if (!TryResolveBanditSpawnTile(
                        signpostBuildingId,
                        targetPlayerId,
                        spawnTileX,
                        spawnTileY,
                        out spawnTileX,
                        out spawnTileY,
                        out int spawnTileId,
                        out ushort sourcePathComponent,
                        out string spawnFailure))
                {
                    LogError(
                        $"Bandit event skipped: targetPlayerId={targetPlayerId}, " +
                        $"reason=no usable tile adjacent to signpost {signpostBuildingId} ({spawnFailure}).");
                    return false;
                }

                GameTileManagerAPI tiles = GameTileManagerAPI.Instance;

                GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
                int requestedUnits = strength;
                int spawnHeight = tiles.GetTileHeight(spawnTileId);
                List<BanditMoveTarget> moveTargets = FindBanditMoveTargets(targetPlayerId, sourcePathComponent);
                if (moveTargets.Count == 0)
                {
                    LogDebug(
                        $"Bandit event ignored without spawning units: targetPlayerId={targetPlayerId}, " +
                        $"reason=no living target building has a free approach tile in path component {sourcePathComponent}.");
                    return false;
                }

                List<BanditUnitReference> spawnedUnits = new List<BanditUnitReference>(requestedUnits);

                for (int index = 0; index < requestedUnits; index++)
                {
                    // Script Extender 1.41 exposes the native owner/color order; names keep that contract explicit.
                    int unitId = checked((int)unitApi.CreateUnitLocal(
                        playerOwnerId: banditOwnerPlayerId,
                        playerColorId: BanditVisualPlayerId,
                        localTileX: spawnTileX,
                        localTileY: spawnTileY,
                        heightElevation: spawnHeight,
                        chimp: eChimps.CHIMP_TYPE_MACEMAN));
                    if (unitId <= 0 ||
                        !unitApi.TryGetUnitById(unitId, out GameUnit* unit) ||
                        unit == null ||
                        unit->r_GlobalId == 0)
                    {
                        LogWarning(
                            $"Bandit maceman creation stopped without modifying the returned unit: " +
                            $"unitId={unitId}, createdUnits={spawnedUnits.Count}/{requestedUnits}.");
                        break;
                    }

                    if (unit->r_ControllableForPlayerId != banditOwnerPlayerId)
                    {
                        throw new InvalidOperationException(
                            $"Native bandit spawn returned owner {unit->r_ControllableForPlayerId} instead of reserved player {banditOwnerPlayerId}.");
                    }
                    if (unit->r_SpritePlayerColorId != BanditVisualPlayerId)
                    {
                        throw new InvalidOperationException(
                            $"Native bandit spawn returned sprite color {unit->r_SpritePlayerColorId} instead of {BanditVisualPlayerId}.");
                    }

                    spawnedUnits.Add(new BanditUnitReference(unitId, unit->r_GlobalId));
                }

                if (spawnedUnits.Count == 0)
                {
                    LogDebug(
                        $"Bandit event had no effect: targetPlayerId={targetPlayerId}, " +
                        "reason=no reserved-player-owned maceman could be created.");
                    return false;
                }

                SavedPrng movePrng = new SavedPrng(state.PrngState0, state.PrngState1);
                int executeAtMapTick = checked(
                    GameTimeManagerAPI.Instance.GetElapsedMapTicks() + BanditGroupActivationDelayTicks);
                int scheduledGroups = Math.Min(MaximumBanditGroups, spawnedUnits.Count);
                // Spread the total as evenly as possible while never creating more than five independent orders.
                int baseGroupSize = spawnedUnits.Count / scheduledGroups;
                int largerGroups = spawnedUnits.Count % scheduledGroups;
                int unitOffset = 0;
                for (int groupIndex = 0; groupIndex < scheduledGroups; groupIndex++)
                {
                    int groupCount = baseGroupSize + (groupIndex < largerGroups ? 1 : 0);
                    BanditUnitReference[] groupUnits = new BanditUnitReference[groupCount];
                    spawnedUnits.CopyTo(unitOffset, groupUnits, 0, groupCount);
                    unitOffset += groupCount;
                    BanditMoveTarget target = moveTargets[movePrng.Next(moveTargets.Count)];
                    pendingBanditGroups.Add(new PendingBanditGroup(
                        banditOwnerPlayerId,
                        banditTeam,
                        humanPlayerIds,
                        targetPlayerId,
                        groupUnits,
                        target,
                        executeAtMapTick));
                }
                state.PrngState0 = movePrng.State0;
                state.PrngState1 = movePrng.State1;

                if (!nativeWildlifeDispatcher.TryQueueActionPoint(
                        spawnTileX,
                        spawnTileY,
                        out string actionPointFailure))
                {
                    LogError(
                        "Bandit minimap action point is disabled while spawned bandits remain active: " +
                        actionPointFailure);
                }

                if (!nativeEventDispatcher.TryQueuePresentation(
                        201,
                        9,
                        "action_bandits.bik",
                        "Random_Events9.wav",
                        out string presentationFailure))
                {
                    LogError(
                        "Bandit presentation is disabled while spawned bandits remain active: " +
                        presentationFailure);
                }

                if (!nativeBanditSupport.TryApplyPopularityPenalty(targetPlayerId, out string penaltyDetail))
                {
                    LogError(
                        "Bandit popularity penalty could not be activated; spawned bandits remain active: " +
                        penaltyDetail);
                }

                LogDebug(
                    $"Manual bandit event spawned: requestedUnits={requestedUnits}, createdUnits={spawnedUnits.Count}, " +
                    $"ownerPlayerId={banditOwnerPlayerId}, targetPlayerId={targetPlayerId}, " +
                    $"groups={scheduledGroups}, signpostBuildingId={signpostBuildingId}.");
                return true;
            }
            catch (Exception ex)
            {
                banditEventsEnabled = false;
                LogError(
                    "Manual bandit spawning failed and further bandit events are disabled for this map; " +
                    $"unrelated events remain active: {ex}");
                return false;
            }
        }


        private unsafe bool TryReserveBanditPlayerSlot(
            int targetPlayerId,
            out int banditPlayerId,
            out int banditTeam,
            out int[] humanPlayerIds,
            out string failure)
        {
            banditPlayerId = -1;
            banditTeam = -1;
            failure = string.Empty;
            GamePlayerManagerAPI players = GamePlayerManagerAPI.Instance;
            int[] activePlayerIds = Shared.ActivePlayerHelper.GetActivePlayerIds();
            HashSet<int> activePlayers = new HashSet<int>(activePlayerIds);
            List<int> humans = new List<int>();
            foreach (int playerId in activePlayerIds)
            {
                if (players.IsPlayerIdValid(playerId) && !players.IsAIPlayer(playerId))
                    humans.Add(playerId);
            }
            if (!humans.Contains(targetPlayerId))
                humans.Add(targetPlayerId);
            humans.Sort();
            humanPlayerIds = humans.ToArray();

            for (int playerId = 1; playerId <= GamePlayerManagerAPI.MAX_PLAYERS; playerId++)
            {
                if (activePlayers.Contains(playerId) ||
                    !players.TryGetPlayerResourcesById(playerId, out GamePlayerResources* resources) ||
                    resources == null ||
                    resources->r_LordUnitId != 0)
                {
                    continue;
                }

                banditPlayerId = playerId;
                break;
            }

            if (banditPlayerId < 1)
            {
                failure =
                    $"no unused regular player slot has r_LordUnitId=0; activePlayers=[{string.Join(",", activePlayerIds)}]";
                return false;
            }

            bool[] humanTeams = new bool[GamePlayerManagerAPI.MAX_PLAYERS + 1];
            foreach (int humanPlayerId in humanPlayerIds)
            {
                int team = players.GetPlayerTeam(humanPlayerId);
                if (team >= 0 && team < humanTeams.Length)
                    humanTeams[team] = true;
            }
            for (int team = 0; team <= GamePlayerManagerAPI.MAX_PLAYERS; team++)
            {
                if (!humanTeams[team])
                {
                    banditTeam = team;
                    break;
                }
            }
            if (banditTeam < 0)
            {
                failure = $"no team number remains distinct from human players [{string.Join(",", humanPlayerIds)}]";
                return false;
            }

            players.SetPlayerTeam(banditPlayerId, banditTeam);
            if (players.GetPlayerTeam(banditPlayerId) != banditTeam)
            {
                failure = $"team assignment for reserved player {banditPlayerId} did not persist";
                return false;
            }
            foreach (int humanPlayerId in humanPlayerIds)
            {
                if (players.IsPlayerAlliedTo(banditPlayerId, humanPlayerId))
                {
                    failure =
                        $"reserved player {banditPlayerId} remains allied to human player {humanPlayerId} on team {banditTeam}";
                    return false;
                }
            }

            return true;
        }

        private unsafe void ProcessPendingBanditGroups()
        {
            if (pendingBanditGroups.Count == 0)
                return;

            int currentMapTick = GameTimeManagerAPI.Instance.GetElapsedMapTicks();
            for (int index = pendingBanditGroups.Count - 1; index >= 0; index--)
            {
                PendingBanditGroup pending = pendingBanditGroups[index];
                if (currentMapTick < pending.ExecuteAtMapTick)
                    continue;

                // Remove first so a rejected native order cannot be repeated on every persistent tick.
                pendingBanditGroups.RemoveAt(index);
                try
                {
                    ActivatePendingBanditGroup(pending);
                }
                catch (Exception ex)
                {
                    banditEventsEnabled = false;
                    pendingBanditGroups.Clear();
                    LogError(
                        "Bandit group activation failed; remaining queued groups and future bandit events " +
                        $"are disabled for this map while unrelated events remain active: {ex}");
                    return;
                }
            }
        }

        private unsafe void ActivatePendingBanditGroup(PendingBanditGroup pending)
        {
            GamePlayerManagerAPI players = GamePlayerManagerAPI.Instance;
            if (!players.TryGetPlayerResourcesById(pending.OwnerPlayerId, out GamePlayerResources* resources) ||
                resources == null ||
                resources->r_LordUnitId != 0)
            {
                throw new InvalidOperationException(
                    $"Reserved bandit player {pending.OwnerPlayerId} is no longer unused.");
            }
            if (players.GetPlayerTeam(pending.OwnerPlayerId) != pending.Team)
            {
                throw new InvalidOperationException(
                    $"Reserved bandit player {pending.OwnerPlayerId} changed from team {pending.Team}.");
            }
            foreach (int humanPlayerId in pending.HumanPlayerIds)
            {
                if (players.IsPlayerAlliedTo(pending.OwnerPlayerId, humanPlayerId))
                {
                    throw new InvalidOperationException(
                        $"Reserved bandit player {pending.OwnerPlayerId} became allied to human player {humanPlayerId}.");
                }
            }

            BanditMoveTarget target = pending.Target;
            GameBuildingManagerAPI buildings = GameBuildingManagerAPI.Instance;
            if (!buildings.TryGetBuildingById(target.BuildingId, out GameBuilding* building) ||
                building == null ||
                building->r_GlobalId != target.BuildingGlobalId ||
                building->r_AliveState != AliveState.IsAlive ||
                building->r_PlayerIdOwner != pending.TargetPlayerId ||
                IsKeepType(building->r_BuildingType))
            {
                LogWarning(
                    $"Bandit group activation skipped: ownerPlayerId={pending.OwnerPlayerId}, " +
                    $"targetBuildingId={target.BuildingId}, reason=target building is no longer valid.");
                return;
            }

            GameUnitManagerAPI units = GameUnitManagerAPI.Instance;
            List<int> livingUnitIds = new List<int>(pending.Units.Length);
            foreach (BanditUnitReference unitReference in pending.Units)
            {
                if (!units.TryGetUnitById(unitReference.UnitId, out GameUnit* unit) ||
                    unit == null ||
                    unit->r_GlobalId != unitReference.GlobalId ||
                    unit->r_AliveState != AliveState.IsAlive)
                {
                    LogWarning(
                        $"Bandit omitted from delayed group activation: unitId={unitReference.UnitId}, " +
                        $"expectedGlobalId={unitReference.GlobalId}, reason=unit is no longer the expected living spawn.");
                    continue;
                }
                if (unit->r_ControllableForPlayerId != pending.OwnerPlayerId ||
                    unit->r_TribeId != 0 ||
                    unit->r_TribeLeaderUnitId != 0)
                {
                    throw new InvalidOperationException(
                        $"Bandit unit {unitReference.UnitId} changed owner or acquired a tribe before activation: " +
                        $"owner={unit->r_ControllableForPlayerId}, tribeId={unit->r_TribeId}, " +
                        $"tribeLeaderUnitId={unit->r_TribeLeaderUnitId}.");
                }
                livingUnitIds.Add(unitReference.UnitId);
            }
            if (livingUnitIds.Count == 0)
            {
                LogWarning(
                    $"Bandit group activation had no effect: ownerPlayerId={pending.OwnerPlayerId}, " +
                    "reason=no scheduled unit remained alive.");
                return;
            }

            GameTribeManagerAPI tribes = GameTribeManagerAPI.Instance;
            int tribeId = checked((int)tribes.Create(pending.OwnerPlayerId));
            if (tribeId <= 0 ||
                !tribes.TryGetTribeById(tribeId, out GameTribe* tribe) ||
                tribe == null ||
                tribe->r_GlobalId == 0 ||
                tribe->r_PlayerIdOwner != pending.OwnerPlayerId)
            {
                throw new InvalidOperationException(
                    $"Vanilla could not create a tribe for reserved bandit player {pending.OwnerPlayerId}.");
            }

            foreach (int unitId in livingUnitIds)
            {
                if (!tribes.AssignUnit(tribeId, unitId) ||
                    !units.TryGetUnitById(unitId, out GameUnit* assignedUnit) ||
                    assignedUnit == null ||
                    assignedUnit->r_TribeId != tribeId)
                {
                    throw new InvalidOperationException(
                        $"Vanilla could not assign bandit unit {unitId} to tribe {tribeId}.");
                }
            }

            if (!tribes.SetStance(tribeId, TribeStance.Aggressive) ||
                tribe->r_TribeStance != TribeStance.Aggressive)
            {
                throw new InvalidOperationException(
                    $"Vanilla could not set bandit tribe {tribeId} to Aggressive.");
            }
            if (!tribes.IssueMoveHereCommand(
                    tribeId,
                    target.TileX,
                    target.TileY,
                    isPatrolPath: false,
                    bIsNewOrder: 1,
                    tribeMoveType: TribeMoveType.DefaultInSync))
            {
                throw new InvalidOperationException(
                    $"Vanilla rejected MoveHere for bandit tribe {tribeId}.");
            }

        }

        private static unsafe List<BanditMoveTarget> FindBanditMoveTargets(
            int targetPlayerId,
            ushort sourcePathComponent)
        {
            List<BanditMoveTarget> targets = new List<BanditMoveTarget>();
            Span<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            for (int buildingIndex = 0; buildingIndex < buildings.Length; buildingIndex++)
            {
                ref GameBuilding building = ref buildings[buildingIndex];
                if (building.r_PlayerIdOwner != targetPlayerId ||
                    building.r_AliveState != AliveState.IsAlive ||
                    building.r_GlobalId == 0 ||
                    IsKeepType(building.r_BuildingType) ||
                    !TryFindBanditApproachTile(in building, sourcePathComponent, out int tileX, out int tileY))
                {
                    continue;
                }

                // Script Extender entity IDs are one-based while spans are indexed from zero.
                targets.Add(new BanditMoveTarget(
                    buildingIndex + 1,
                    building.r_GlobalId,
                    tileX,
                    tileY));
            }
            return targets;
        }

        private static bool IsKeepType(eStructs buildingType) =>
            buildingType == eStructs.STRUCT_KEEP_ONE ||
            buildingType == eStructs.STRUCT_KEEP_TWO ||
            buildingType == eStructs.STRUCT_KEEP_THREE ||
            buildingType == eStructs.STRUCT_KEEP_FOUR ||
            buildingType == eStructs.STRUCT_KEEP_FIVE;

        private static bool TryFindBanditApproachTile(
            in GameBuilding building,
            ushort sourcePathComponent,
            out int targetTileX,
            out int targetTileY)
        {
            targetTileX = 0;
            targetTileY = 0;
            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            Span<ushort> pathConnections = tiles.TileManager.PathConnectionGrid;
            long bestDistanceSquared = long.MaxValue;
            int centerX = (building.r_TilePositionXBegin + building.r_TilePositionXEnd) / 2;
            int centerY = (building.r_TilePositionYBegin + building.r_TilePositionYEnd) / 2;

            for (int y = building.r_TilePositionYBegin - 1; y <= building.r_TilePositionYEnd + 1; y++)
            {
                for (int x = building.r_TilePositionXBegin - 1; x <= building.r_TilePositionXEnd + 1; x++)
                {
                    if ((x >= building.r_TilePositionXBegin && x <= building.r_TilePositionXEnd &&
                         y >= building.r_TilePositionYBegin && y <= building.r_TilePositionYEnd) ||
                        !tiles.IsTileInsideMapBounds(x, y))
                    {
                        continue;
                    }

                    int tileId = tiles.GetTileId(x, y);
                    if (!tiles.IsTileWalkableAndUnoccupied(tileId) ||
                        pathConnections[tileId] != sourcePathComponent)
                    {
                        continue;
                    }

                    long deltaX = x - centerX;
                    long deltaY = y - centerY;
                    long distanceSquared = deltaX * deltaX + deltaY * deltaY;
                    if (distanceSquared >= bestDistanceSquared)
                        continue;

                    bestDistanceSquared = distanceSquared;
                    targetTileX = x;
                    targetTileY = y;
                }
            }

            return bestDistanceSquared != long.MaxValue;
        }

        private static bool HasReachableBanditApproach(
            in GameBuilding building,
            ushort sourcePathComponent)
        {
            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            Span<ushort> pathConnections = tiles.TileManager.PathConnectionGrid;
            for (int y = building.r_TilePositionYBegin - 1; y <= building.r_TilePositionYEnd + 1; y++)
            {
                for (int x = building.r_TilePositionXBegin - 1; x <= building.r_TilePositionXEnd + 1; x++)
                {
                    if ((x >= building.r_TilePositionXBegin && x <= building.r_TilePositionXEnd &&
                         y >= building.r_TilePositionYBegin && y <= building.r_TilePositionYEnd) ||
                        !tiles.IsTileInsideMapBounds(x, y))
                    {
                        continue;
                    }

                    int tileId = tiles.GetTileId(x, y);
                    if (tiles.IsTileWalkableAndUnoccupied(tileId) &&
                        pathConnections[tileId] == sourcePathComponent)
                    {
                        return true;
                    }
                }
            }
            return false;
        }


        private unsafe bool TryResolveBanditSpawnTile(
            int signpostBuildingId,
            int targetPlayerId,
            int preferredTileX,
            int preferredTileY,
            out int spawnTileX,
            out int spawnTileY,
            out int spawnTileId,
            out ushort sourcePathComponent,
            out string failure)
        {
            spawnTileX = 0;
            spawnTileY = 0;
            spawnTileId = -1;
            sourcePathComponent = 0;
            failure = string.Empty;

            if (signpostBuildingId <= 0 ||
                !GameBuildingManagerAPI.Instance.TryGetBuildingById(signpostBuildingId, out GameBuilding* signpost) ||
                signpost == null ||
                signpost->r_GlobalId == 0 ||
                signpost->r_AliveState != AliveState.IsAlive ||
                signpost->r_BuildingType != eStructs.STRUCT_SIGNPOST)
            {
                failure = "registered signpost is no longer a living STRUCT_SIGNPOST.";
                return false;
            }

            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            Span<ushort> pathConnections = tiles.TileManager.PathConnectionGrid;
            Dictionary<ushort, bool> reachableComponents = new Dictionary<ushort, bool>();
            long bestDistanceSquared = long.MaxValue;

            // The registry returns the building center, whose path component may be zero.
            // Spawn on the nearest free perimeter tile so Vanilla can initialize movement normally.
            for (int y = signpost->r_TilePositionYBegin - 1; y <= signpost->r_TilePositionYEnd + 1; y++)
            {
                for (int x = signpost->r_TilePositionXBegin - 1; x <= signpost->r_TilePositionXEnd + 1; x++)
                {
                    bool insideFootprint =
                        x >= signpost->r_TilePositionXBegin && x <= signpost->r_TilePositionXEnd &&
                        y >= signpost->r_TilePositionYBegin && y <= signpost->r_TilePositionYEnd;
                    if (insideFootprint || !tiles.IsTileInsideMapBounds(x, y))
                        continue;

                    int candidateTileId = tiles.GetTileId(x, y);
                    if (!tiles.IsValidTileId(candidateTileId) ||
                        !tiles.IsTileWalkableAndUnoccupied(candidateTileId))
                    {
                        continue;
                    }

                    ushort candidateComponent = pathConnections[candidateTileId];
                    if (candidateComponent == 0)
                        continue;

                    if (!reachableComponents.TryGetValue(candidateComponent, out bool reachesTarget))
                    {
                        reachesTarget = HasAnyBanditTargetInComponent(targetPlayerId, candidateComponent);
                        reachableComponents.Add(candidateComponent, reachesTarget);
                    }
                    if (!reachesTarget)
                        continue;

                    long deltaX = x - preferredTileX;
                    long deltaY = y - preferredTileY;
                    long distanceSquared = deltaX * deltaX + deltaY * deltaY;
                    if (distanceSquared > bestDistanceSquared ||
                        (distanceSquared == bestDistanceSquared && candidateTileId >= spawnTileId))
                    {
                        continue;
                    }

                    bestDistanceSquared = distanceSquared;
                    spawnTileX = x;
                    spawnTileY = y;
                    spawnTileId = candidateTileId;
                    sourcePathComponent = candidateComponent;
                }
            }

            if (spawnTileId < 0)
            {
                failure =
                    $"footprint=({signpost->r_TilePositionXBegin},{signpost->r_TilePositionYBegin})-" +
                    $"({signpost->r_TilePositionXEnd},{signpost->r_TilePositionYEnd}) has no free, " +
                    $"walkable perimeter tile connected to a living building or owned wall of player {targetPlayerId}";
                return false;
            }

            return true;
        }

        private unsafe bool HasAnyBanditTargetInComponent(int targetPlayerId, ushort sourcePathComponent)
        {
            Span<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            for (int index = 0; index < buildings.Length; index++)
            {
                ref GameBuilding building = ref buildings[index];
                if (building.r_PlayerIdOwner == targetPlayerId &&
                    building.r_AliveState == AliveState.IsAlive &&
                    building.r_GlobalId != 0 &&
                    !IsKeepType(building.r_BuildingType) &&
                    HasReachableBanditApproach(in building, sourcePathComponent))
                {
                    return true;
                }
            }
            return false;
        }

        private int[] GetLivingHumanPlayerIds()
        {
            List<int> result = new List<int>();
            GamePlayerManagerAPI playerApi = GamePlayerManagerAPI.Instance;
            foreach (int playerId in Shared.ActivePlayerHelper.GetActivePlayerIds())
            {
                if (!playerApi.IsPlayerIdValid(playerId) ||
                    playerApi.IsAIPlayer(playerId) ||
                    !TryGetLivingLord(playerId, out _))
                {
                    continue;
                }
                result.Add(playerId);
            }
            return result.ToArray();
        }

        private bool SpawnRabbitInfestation(int targetPlayerId)
        {
            if (!nativeWildlifeDispatcher.TryGetRabbitTileMask(out uint rabbitTileMask, out string compatibilityFailure))
            {
                LogError(
                    $"Native rabbit event skipped: targetPlayerId={targetPlayerId}, " +
                    $"reason={compatibilityFailure}");
                return false;
            }

            List<RabbitFarm> farms = new List<RabbitFarm>();
            Span<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            for (int buildingId = 0; buildingId < buildings.Length; buildingId++)
            {
                ref GameBuilding building = ref buildings[buildingId];
                if (building.r_PlayerIdOwner != targetPlayerId ||
                    building.r_AliveState != AliveState.IsAlive ||
                    (building.r_BuildingType != eStructs.STRUCT_WHEATFARM &&
                     building.r_BuildingType != eStructs.STRUCT_HOPSFARM))
                {
                    continue;
                }

                farms.Add(new RabbitFarm(
                    buildingId,
                    building.r_BuildingType,
                    (building.r_TilePositionXBegin + building.r_TilePositionXEnd) / 2,
                    (building.r_TilePositionYBegin + building.r_TilePositionYEnd) / 2));
            }

            if (farms.Count == 0)
            {
                LogDebug(
                    $"Rabbit infestation skipped: targetPlayerId={targetPlayerId}, " +
                    "reason=no alive wheat or hops farm owned by the target player.");
                return false;
            }

            SavedPrng prng = new SavedPrng(state.PrngState0, state.PrngState1);
            RabbitFarm farm = farms[prng.Next(farms.Count)];
            List<RabbitSpawnTile> spawnTiles = FindWildlifeSpawnTiles(
                farm.TileX,
                farm.TileY,
                RabbitSpawnRadius,
                rabbitTileMask);
            if (spawnTiles.Count == 0)
            {
                state.PrngState0 = prng.State0;
                state.PrngState1 = prng.State1;
                LogWarning(
                    $"Rabbit infestation skipped: targetPlayerId={targetPlayerId}, farmBuildingId={farm.BuildingId}, " +
                    $"farmType={farm.BuildingType}, farmTile=({farm.TileX},{farm.TileY}), " +
                    $"reason=no Vanilla-compatible tile exists within radius {RabbitSpawnRadius}.");
                return false;
            }

            RabbitSpawnTile spawnTile = spawnTiles[prng.Next(spawnTiles.Count)];
            state.PrngState0 = prng.State0;
            state.PrngState1 = prng.State1;
            NativeEventDispatchStatus status = nativeWildlifeDispatcher.DispatchRabbits(
                spawnTile.X, spawnTile.Y, spawnTile.Height, out string detail);
            LogWildlifeDispatchResult(
                "Rabbit infestation",
                status,
                targetPlayerId,
                $"farmBuildingId={farm.BuildingId}, farmType={farm.BuildingType}, " +
                $"farmTile=({farm.TileX},{farm.TileY}), radius={RabbitSpawnRadius}, " +
                $"spawnTile=({spawnTile.X},{spawnTile.Y}), candidateTiles={spawnTiles.Count}, detail={detail}");
            return status == NativeEventDispatchStatus.Applied;
        }

        private bool SpawnLionAttack(int targetPlayerId, int strength)
        {
            if (!nativeWildlifeDispatcher.TryGetLionTileMask(out uint lionTileMask, out string compatibilityFailure))
            {
                LogError(
                    $"Native lion event skipped: targetPlayerId={targetPlayerId}, " +
                    $"reason={compatibilityFailure}");
                return false;
            }

            if (!signpostRegistry.TryGetClosestSignpostToPlayer(
                    targetPlayerId,
                    out int signpostBuildingId,
                    out int signpostX,
                    out int signpostY,
                    out double signpostDistance,
                    out string distanceReference,
                    out string signpostFailure))
            {
                LogError(
                    $"Native lion event skipped: targetPlayerId={targetPlayerId}, " +
                    $"reason=closest registered signpost unavailable ({signpostFailure}).");
                return false;
            }

            List<RabbitSpawnTile> spawnTiles = FindWildlifeSpawnTiles(
                signpostX,
                signpostY,
                LionSpawnRadius,
                lionTileMask);
            if (spawnTiles.Count == 0)
            {
                LogError(
                    $"Native lion event skipped: targetPlayerId={targetPlayerId}, signpostBuildingId={signpostBuildingId}, " +
                    $"signpostTile=({signpostX},{signpostY}), reason=no Vanilla-compatible tile exists within radius {LionSpawnRadius}.");
                return false;
            }

            // The first candidate is the nearest valid tile to the selected signpost.
            RabbitSpawnTile spawnTile = spawnTiles[0];
            NativeEventDispatchStatus status = nativeWildlifeDispatcher.DispatchLions(
                spawnTile.X, spawnTile.Y, spawnTile.Height, strength, out string detail);
            LogWildlifeDispatchResult(
                "Lion attack",
                status,
                targetPlayerId,
                $"strength={strength}, signpostBuildingId={signpostBuildingId}, " +
                $"signpostTile=({signpostX},{signpostY}), distanceReference={distanceReference}, " +
                $"signpostDistance={signpostDistance:0.00}, " +
                $"spawnTile=({spawnTile.X},{spawnTile.Y}), spawnRadius={LionSpawnRadius}, detail={detail}");
            return status == NativeEventDispatchStatus.Applied;
        }

        private static List<RabbitSpawnTile> FindWildlifeSpawnTiles(
            int centerX,
            int centerY,
            int radius,
            uint rejectedTileMask)
        {
            List<RabbitSpawnTile> result = new List<RabbitSpawnTile>();
            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            if (rejectedTileMask == 0)
                return result;

            int radiusSquared = radius * radius;
            for (int y = centerY - radius; y <= centerY + radius; y++)
            {
                for (int x = centerX - radius; x <= centerX + radius; x++)
                {
                    int deltaX = x - centerX;
                    int deltaY = y - centerY;
                    if (deltaX * deltaX + deltaY * deltaY > radiusSquared ||
                        !tiles.IsTileInsideMapBounds(x, y))
                    {
                        continue;
                    }

                    int tileId = tiles.GetTileId(x, y);
                    uint flags = unchecked((uint)tiles.GetTilePropertyFlag(tileId));
                    if ((flags & rejectedTileMask) == 0)
                        result.Add(new RabbitSpawnTile(x, y, tiles.GetTileHeight(tileId), deltaX * deltaX + deltaY * deltaY));
                }
            }
            result.Sort((left, right) => left.DistanceSquared.CompareTo(right.DistanceSquared));
            return result;
        }

        private void LogWildlifeDispatchResult(
            string eventName,
            NativeEventDispatchStatus status,
            int targetPlayerId,
            string details)
        {
            if (status == NativeEventDispatchStatus.Applied)
            {
                LogDebug($"Native Vanilla wildlife event dispatched: event={eventName}, targetPlayerId={targetPlayerId}, {details}.");
                return;
            }
            if (status == NativeEventDispatchStatus.PrerequisiteNotMet)
            {
                LogDebug($"Native Vanilla wildlife event had no effect: event={eventName}, targetPlayerId={targetPlayerId}, {details}.");
                return;
            }
            LogError($"Native Vanilla wildlife event disabled or failed: event={eventName}, targetPlayerId={targetPlayerId}, {details}.");
        }

        private static unsafe bool TryGetLivingLord(int playerId, out string failure)
        {
            failure = string.Empty;
            if (!GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(
                    playerId,
                    out GamePlayerResources* resources) ||
                resources == null)
            {
                failure = "player resources unavailable";
                return false;
            }

            uint lordUnitId = resources->r_LordUnitId;
            if (lordUnitId == 0 || lordUnitId > int.MaxValue)
            {
                failure = "no valid Lord unit is registered";
                return false;
            }
            if (!GameUnitManagerAPI.Instance.TryGetUnitById((int)lordUnitId, out GameUnit* lord) || lord == null)
            {
                failure = "registered Lord unit cannot be resolved";
                return false;
            }
            if (lord->r_AliveState != AliveState.IsAlive)
            {
                failure = $"registered Lord unit state is {lord->r_AliveState}";
                return false;
            }
            if (lord->r_UnitChimp != eChimps.CHIMP_TYPE_LORD || lord->r_ControllableForPlayerId != playerId)
            {
                failure = "registered unit is not the target player's Lord";
                return false;
            }

            return true;
        }

        private int GetCurrentAbsoluteMonth()
        {
            int currentYear = GameTimeManagerAPI.Instance.GetCurrentYear();
            int currentMonth = GameTimeManagerAPI.Instance.GetCurrentMonth();
            ValidateCalendarApi(currentYear, currentMonth);
            return checked(currentYear * VanillaMonthsPerYear + currentMonth);
        }

        private int GetElapsedMonthsSinceStart() =>
            Math.Max(0, checked(GetCurrentAbsoluteMonth() - state.StartAbsoluteMonth));

        private static int CalculateElapsedScaledUnitCount(int elapsedMonths, int factorTenths)
        {
            // Fixed-point tenths keep rolls and save data deterministic; integer division intentionally floors.
            long numerator = checked((long)elapsedMonths * factorTenths);
            return checked((int)(numerator / (ScaledStrengthMonthsPerPeriod * ScaledStrengthTenthsPerUnit)));
        }

        private void ValidateCalendarApi(int currentYear, int currentMonth)
        {
            if (currentYear < 0 || currentMonth < 0 || currentMonth >= VanillaMonthsPerYear)
            {
                mapActive = false;
                state = null;
                throw new InvalidOperationException(
                    $"Unsupported Vanilla calendar values year={currentYear}, month={currentMonth}; " +
                    "Random Events was disabled for this map to prevent incorrectly dated events.");
            }

        }

        private static bool HasElapsedMilliseconds(long startTimestamp, int milliseconds)
        {
            if (startTimestamp <= 0)
                return false;
            long elapsedTimestamp = Stopwatch.GetTimestamp() - startTimestamp;
            return elapsedTimestamp >= (long)Math.Ceiling(milliseconds * (double)Stopwatch.Frequency / 1000.0);
        }

        private void DisableForNetwork(string reason, string details)
        {
            mapStartPending = false;
            mapActive = false;
            pendingBanditGroups.Clear();
            state = null;
            LogDebug(
                $"Random Events fully disabled: {reason}; no rolls, events, save data, or signposts will be created. " +
                $"Network details: {details}.");
        }

        private void LogDebug(string message) => Shared.DebugLogHelper.LogDebug(log, message);
        private void LogWarning(string message) => Shared.DebugLogHelper.LogWarning(log, message);
        private void LogError(string message) => Shared.DebugLogHelper.LogError(log, message);


        private readonly struct BanditUnitReference
        {
            public BanditUnitReference(int unitId, uint globalId)
            {
                UnitId = unitId;
                GlobalId = globalId;
            }

            public int UnitId { get; }
            public uint GlobalId { get; }
        }

        private readonly struct PendingBanditGroup
        {
            public PendingBanditGroup(
                int ownerPlayerId,
                int team,
                int[] humanPlayerIds,
                int targetPlayerId,
                BanditUnitReference[] units,
                BanditMoveTarget target,
                int executeAtMapTick)
            {
                OwnerPlayerId = ownerPlayerId;
                Team = team;
                HumanPlayerIds = humanPlayerIds;
                TargetPlayerId = targetPlayerId;
                Units = units;
                Target = target;
                ExecuteAtMapTick = executeAtMapTick;
            }

            public int OwnerPlayerId { get; }
            public int Team { get; }
            public int[] HumanPlayerIds { get; }
            public int TargetPlayerId { get; }
            public BanditUnitReference[] Units { get; }
            public BanditMoveTarget Target { get; }
            public int ExecuteAtMapTick { get; }
        }

        private readonly struct BanditMoveTarget
        {
            public BanditMoveTarget(
                int buildingId,
                uint buildingGlobalId,
                int tileX,
                int tileY)
            {
                BuildingId = buildingId;
                BuildingGlobalId = buildingGlobalId;
                TileX = tileX;
                TileY = tileY;
            }

            public int BuildingId { get; }
            public uint BuildingGlobalId { get; }
            public int TileX { get; }
            public int TileY { get; }
        }

        private readonly struct RabbitFarm
        {
            public RabbitFarm(int buildingId, eStructs buildingType, int tileX, int tileY)
            {
                BuildingId = buildingId;
                BuildingType = buildingType;
                TileX = tileX;
                TileY = tileY;
            }

            public int BuildingId { get; }
            public eStructs BuildingType { get; }
            public int TileX { get; }
            public int TileY { get; }
        }

        private readonly struct RabbitSpawnTile
        {
            public RabbitSpawnTile(int x, int y, int height, int distanceSquared)
            {
                X = x;
                Y = y;
                Height = height;
                DistanceSquared = distanceSquared;
            }

            public int X { get; }
            public int Y { get; }
            public int Height { get; }
            public int DistanceSquared { get; }
        }
    }
}
