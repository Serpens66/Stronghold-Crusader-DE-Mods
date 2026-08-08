using BepInEx.Logging;
using CrusaderDE;
using MessagePack;
using R3;
using SHCDESE.API;
using SHCDESE.API.Components.SaveData;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
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
        private const string SaveDataIdentifier = "serp-randomevents-state-v1";
        private const short TagMagic = unchecked((short)0x5245);
        private const short TagVersion = 1;
        private const short TagGuard = unchecked((short)0x6E7A);
        private const int TagStartIndex = 36;
        private const int VanillaMonthsPerYear = 12;

        private readonly ManualLogSource log;
        private readonly RandomEventsSettingsViewModel settings;
        private readonly ScenarioSignpostRegistry signpostRegistry;
        private readonly SignpostPlacementService signpostPlacement;
        private readonly RandomEventsMainThreadPump mainThreadPump;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private bool initialized;
        private bool disposed;
        private bool mapStartPending;
        private bool mapActive;
        private bool loadedStateAvailable;
        private bool multiplayerDisableLogged;
        private bool mapStartedFromMultiplayerSave;
        private bool timelineRestoredForLoadedState;
        private int lastSignpostAttemptTick = int.MinValue;
        private int lastLoggedAbsoluteMonth = int.MinValue;
        private long lastClockLogTimestamp;
        private int pendingTimelineAuditDue = int.MinValue;
        private int[] pendingTimelineAuditKinds = Array.Empty<int>();
        private int pendingRabbitAuditTick = int.MinValue;
        private int pendingRabbitAuditTargetPlayerId = -1;
        private int pendingRabbitAuditSignpostBuildingId = -1;
        private int pendingRabbitAuditBaselineCount;
        private bool calendarApiFallbackLogged;
        private int lastObservedGameTick;
        private int mapGeneration;
        private int timelineSynchronizationSerial;
        private RandomEventsSaveStateV1 state;

        public RandomEventsRuntime(ManualLogSource log, RandomEventsSettingsViewModel settings)
        {
            this.log = log;
            this.settings = settings;
            signpostRegistry = new ScenarioSignpostRegistry(log);
            signpostPlacement = new SignpostPlacementService(log, signpostRegistry);
            mainThreadPump = RandomEventsMainThreadPump.Create();
        }

        public void InitializeNative(IntPtr libraryHandle, ReadOnlySpan<byte> memory, bool referenceHashMatches) =>
            signpostRegistry.InitializeNative(libraryHandle, memory, referenceHashMatches);

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
            LogInfo("Runtime hooks and versioned save-data handler registered.");
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
            mapGeneration++;
            timelineSynchronizationSerial++;
            mapStartPending = true;
            mapActive = false;
            multiplayerDisableLogged = false;
            mapStartedFromMultiplayerSave = args.bMultiplayerSave != 0;
            timelineRestoredForLoadedState = false;
            lastSignpostAttemptTick = int.MinValue;
            lastLoggedAbsoluteMonth = int.MinValue;
            lastClockLogTimestamp = 0;
            pendingTimelineAuditDue = int.MinValue;
            pendingTimelineAuditKinds = Array.Empty<int>();
            pendingRabbitAuditTick = int.MinValue;
            pendingRabbitAuditTargetPlayerId = -1;
            pendingRabbitAuditSignpostBuildingId = -1;
            pendingRabbitAuditBaselineCount = 0;
            calendarApiFallbackLogged = false;
            LogInfo(
                $"Map start received; initialization deferred to persistent game tick: " +
                $"loadedState={loadedStateAvailable}, multiplayerSave={mapStartedFromMultiplayerSave}.");
        }

        private void OnUnloadMap(MapUnloadEventArgs args)
        {
            LogInfo("Map unload received; in-memory Random Events state cleared.");
            ResetMapState();
        }

        private void ResetMapState()
        {
            mapGeneration++;
            timelineSynchronizationSerial++;
            mapStartPending = false;
            mapActive = false;
            loadedStateAvailable = false;
            multiplayerDisableLogged = false;
            mapStartedFromMultiplayerSave = false;
            timelineRestoredForLoadedState = false;
            lastLoggedAbsoluteMonth = int.MinValue;
            lastClockLogTimestamp = 0;
            pendingTimelineAuditDue = int.MinValue;
            pendingTimelineAuditKinds = Array.Empty<int>();
            pendingRabbitAuditTick = int.MinValue;
            pendingRabbitAuditTargetPlayerId = -1;
            pendingRabbitAuditSignpostBuildingId = -1;
            pendingRabbitAuditBaselineCount = 0;
            calendarApiFallbackLogged = false;
            state = null;
        }

        private void OnGameTick(int tick)
        {
            try
            {
                lastObservedGameTick = tick;
                if (mapStartPending)
                {
                    if (GameTimeManagerAPI.Instance.GetElapsedMapTicks() <= 0)
                        return;
                    InitializeCurrentMap();
                }

                if (!mapActive || state == null)
                    return;

                if (Shared.GameModeHelper.IsRealMultiplayer(mapStartedFromMultiplayerSave))
                {
                    Shared.GameModeSnapshot networkMode = Shared.GameModeHelper.Capture(mapStartedFromMultiplayerSave);
                    DisableForNetwork("real network game became active during the match", networkMode.ToDiagnosticString());
                    return;
                }

                int currentAbsoluteMonth = GetCurrentAbsoluteMonth();
                LogClockHeartbeat(tick, currentAbsoluteMonth);
                AuditPendingRabbitOutcome(tick);
                AuditPendingTimelineEntries(currentAbsoluteMonth);
                if (!timelineRestoredForLoadedState && loadedStateAvailable && currentAbsoluteMonth < state.NextDueAbsoluteMonth)
                {
                    RequestTimelineSynchronization(state.PreparedTimelineKinds);
                    timelineRestoredForLoadedState = true;
                }

                if (state.BatchPrepared && currentAbsoluteMonth >= state.NextDueAbsoluteMonth)
                {
                    ExecuteDueBatch(currentAbsoluteMonth);
                    return; // Give Vanilla one complete tick to consume the due Timeline records before reuse.
                }

                if (!state.BatchPrepared)
                    PrepareBatch();

                if (!state.SignpostsInitialized && RandomEventDefinitions.RequiresSignposts(state.Chances) &&
                    (lastSignpostAttemptTick == int.MinValue || tick - lastSignpostAttemptTick >= 30))
                {
                    lastSignpostAttemptTick = tick;
                    signpostPlacement.TryInitialize(state);
                }
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
            LogInfo($"Game-mode diagnostics: {gameModeDetails}.");
            if (gameMode.IsRealMultiplayer)
            {
                DisableForNetwork("map started as a real network game", gameModeDetails);
                return;
            }

            if (gameMode.IsMapEditor)
            {
                LogInfo("Random Events disabled for map editor session.");
                state = null;
                return;
            }

            if (!gameMode.IsSingleplayerSkirmishMode)
            {
                LogInfo("Random Events disabled because the map is neither a singleplayer skirmish nor a singleplayer Trail mission.");
                state = null;
                return;
            }

            if (loadedStateAvailable && ValidateLoadedState(state))
            {
                mapActive = state.EffectiveEnabled;
                LogInfo(
                    $"Restored Random Events state: enabled={state.EffectiveEnabled}, interval={state.IntervalMonths}, " +
                    $"nextDueAbsoluteMonth={state.NextDueAbsoluteMonth}, batchPrepared={state.BatchPrepared}, " +
                    $"signpostsInitialized={state.SignpostsInitialized}.");
                return;
            }

            loadedStateAvailable = false;
            if (!settings.EnableMod)
            {
                LogInfo("Random Events disabled by the effective map setting.");
                state = null;
                return;
            }

            state = CreateFreshState();
            mapActive = true;
            PrepareBatch();
            LogInfo(
                $"Fresh Random Events state initialized: interval={state.IntervalMonths}, " +
                $"firstDueAbsoluteMonth={state.NextDueAbsoluteMonth}, multiplayerMode={(MultiplayerEventMode)state.MultiplayerMode} (reserved)." );
        }

        private RandomEventsSaveStateV1 CreateFreshState()
        {
            byte[] seed = new byte[16];
            using (RandomNumberGenerator generator = RandomNumberGenerator.Create())
                generator.GetBytes(seed);

            int[] minimums = new int[6];
            int[] maximums = new int[6];
            for (int index = 0; index < 6; index++)
            {
                settings.GetStrengthRange((RandomEventStrengthKind)(index + 1), out minimums[index], out maximums[index]);
            }

            int interval = Math.Max(1, Math.Min(90, settings.IntervalMonths));
            return new RandomEventsSaveStateV1
            {
                EffectiveEnabled = true,
                IntervalMonths = interval,
                MultiplayerMode = Math.Max(0, Math.Min(1, settings.MultiplayerEventModeIndex)),
                Chances = settings.SnapshotChances(),
                StrengthMinimums = minimums,
                StrengthMaximums = maximums,
                PrngState0 = BitConverter.ToUInt64(seed, 0),
                PrngState1 = BitConverter.ToUInt64(seed, 8),
                NextDueAbsoluteMonth = checked(GetCurrentAbsoluteMonth() + interval),
                SignpostsInitialized = !RandomEventDefinitions.RequiresSignposts(settings.SnapshotChances())
            };
        }

        private void PrepareBatch()
        {
            SavedPrng prng = new SavedPrng(state.PrngState0, state.PrngState1);
            List<int> directKinds = new List<int>();
            List<int> directStrengths = new List<int>();
            List<int> directTargetPlayerIds = new List<int>();
            List<int> timelineKinds = new List<int>();
            List<int> timelineStrengths = new List<int>();

            foreach (RandomEventDefinition definition in RandomEventDefinitions.All)
            {
                int chance = state.Chances[(int)definition.Kind];
                int roll = prng.Next(100);
                bool success = roll < chance;
                int strength = success ? RollStrength(definition.StrengthKind, ref prng) : 0;
                LogInfo(
                    $"Event roll: event={definition.Name}, kind={definition.Kind}, roll={roll}, chance={chance}, " +
                    $"success={success}, strength={strength}, dueAbsoluteMonth={state.NextDueAbsoluteMonth}.");

                if (!success)
                    continue;
                if (definition.IsDirect)
                {
                    directKinds.Add((int)definition.Kind);
                    directStrengths.Add(strength);
                    // Singleplayer currently targets its human explicitly. A future synchronized
                    // multiplayer packet can carry another stable player ID through the same path.
                    directTargetPlayerIds.Add(GamePlayerManagerAPI.Instance.GetLocalPlayerId());
                }
                else
                {
                    timelineKinds.Add((int)definition.Kind);
                    timelineStrengths.Add(strength);
                }
            }

            state.PrngState0 = prng.State0;
            state.PrngState1 = prng.State1;
            state.PreparedDirectKinds = directKinds.ToArray();
            state.PreparedDirectStrengths = directStrengths.ToArray();
            state.PreparedDirectTargetPlayerIds = directTargetPlayerIds.ToArray();
            state.PreparedTimelineKinds = timelineKinds.ToArray();
            state.PreparedTimelineStrengths = timelineStrengths.ToArray();
            state.BatchPrepared = true;
            RequestTimelineSynchronization(state.PreparedTimelineKinds);
            LogInfo(
                $"Event batch prepared: dueAbsoluteMonth={state.NextDueAbsoluteMonth}, " +
                $"directHits={directKinds.Count}, timelineHits={timelineKinds.Count}.");
        }

        private int RollStrength(RandomEventStrengthKind kind, ref SavedPrng prng)
        {
            if (kind == RandomEventStrengthKind.None)
                return 0;
            int index = (int)kind - 1;
            return prng.NextInclusive(state.StrengthMinimums[index], state.StrengthMaximums[index]);
        }

        private void ExecuteDueBatch(int currentAbsoluteMonth)
        {
            int due = state.NextDueAbsoluteMonth;
            int[] directKinds = state.PreparedDirectKinds ?? Array.Empty<int>();
            int[] strengths = state.PreparedDirectStrengths ?? Array.Empty<int>();
            int[] targetPlayerIds = state.PreparedDirectTargetPlayerIds ?? Array.Empty<int>();
            int[] timelineKinds = state.PreparedTimelineKinds ?? Array.Empty<int>();
            int snapshotTargetPlayerId = GetBatchTargetPlayerId(targetPlayerIds);

            LogInfo($"Prerequisite snapshot before due batch: dueAbsoluteMonth={due}, {CapturePrerequisiteSnapshot(snapshotTargetPlayerId)}.");
            AuditTimelineEntries("at-due", due, currentAbsoluteMonth, timelineKinds);

            // Clear first so a save callback during a Vanilla action cannot persist an executable duplicate.
            state.BatchPrepared = false;
            state.PreparedDirectKinds = Array.Empty<int>();
            state.PreparedDirectStrengths = Array.Empty<int>();
            state.PreparedDirectTargetPlayerIds = Array.Empty<int>();
            state.PreparedTimelineKinds = Array.Empty<int>();
            state.PreparedTimelineStrengths = Array.Empty<int>();

            LogInfo($"Event date reached: dueAbsoluteMonth={due}, currentAbsoluteMonth={currentAbsoluteMonth}, directCount={directKinds.Length}.");
            for (int index = 0; index < directKinds.Length; index++)
            {
                RandomEventDefinition definition = RandomEventDefinitions.Get((RandomEventKind)directKinds[index]);
                int strength = index < strengths.Length ? strengths[index] : 0;
                int targetPlayerId = index < targetPlayerIds.Length
                    ? targetPlayerIds[index]
                    : GamePlayerManagerAPI.Instance.GetLocalPlayerId();
                DispatchDirectEvent(definition, strength, targetPlayerId);
            }

            LogInfo($"Prerequisite snapshot after direct batch: dueAbsoluteMonth={due}, {CapturePrerequisiteSnapshot(snapshotTargetPlayerId)}.");
            pendingTimelineAuditDue = due;
            pendingTimelineAuditKinds = timelineKinds;
            LogInfo("Prepared Timeline events reached their Vanilla execution date; their done flags will be audited on the following game tick before the reusable records are changed.");
            state.NextDueAbsoluteMonth = checked(due + state.IntervalMonths);
            LogInfo($"Next event batch will be prepared on the following game tick: nextDueAbsoluteMonth={state.NextDueAbsoluteMonth}.");
        }

        private void RequestTimelineSynchronization(int[] successfulKinds)
        {
            int[] kinds = (int[])(successfulKinds ?? Array.Empty<int>()).Clone();
            int[] strengths = (int[])(state.PreparedTimelineStrengths ?? Array.Empty<int>()).Clone();
            int dueAbsoluteMonth = state.NextDueAbsoluteMonth;
            int requestMapGeneration = mapGeneration;
            int requestSerial = ++timelineSynchronizationSerial;
            LogInfo(
                $"Timeline synchronization queued for safe LateUpdate: dueAbsoluteMonth={dueAbsoluteMonth}, " +
                $"activeKinds={kinds.Length}, mapGeneration={requestMapGeneration}, requestSerial={requestSerial}.");

            mainThreadPump.Enqueue(() =>
            {
                if (!mapActive || state == null || requestMapGeneration != mapGeneration ||
                    requestSerial != timelineSynchronizationSerial)
                {
                    LogInfo(
                        $"Queued Timeline synchronization discarded as stale: mapGeneration={requestMapGeneration}, " +
                        $"requestSerial={requestSerial}, currentMapGeneration={mapGeneration}, " +
                        $"currentRequestSerial={timelineSynchronizationSerial}.");
                    return;
                }

                try
                {
                    LogInfo(
                        $"Timeline synchronization executing in safe LateUpdate: dueAbsoluteMonth={dueAbsoluteMonth}, " +
                        $"activeKinds={kinds.Length}, requestSerial={requestSerial}.");
                    SynchronizeTimelineEntries(kinds, strengths, dueAbsoluteMonth);
                }
                catch (Exception ex)
                {
                    LogError(
                        "Timeline synchronization failed outside the native pre-tick hook; " +
                        $"Timeline-only events are disabled for this prepared batch: {ex}");
                }
            });
        }

        private void SynchronizeTimelineEntries(int[] successfulKinds, int[] successfulStrengths, int dueAbsoluteMonth)
        {
            HashSet<int> successes = new HashSet<int>(successfulKinds ?? Array.Empty<int>());
            foreach (RandomEventDefinition definition in RandomEventDefinitions.TimelineOnly)
            {
                bool active = successes.Contains((int)definition.Kind);
                int eventId = FindOwnedTimelineEntry(definition.Kind);
                if (eventId < 0 && !active)
                    continue;

                EngineInterface.tl_event timelineEvent;
                if (eventId < 0)
                {
                    timelineEvent = EngineInterface.CreateNewScenarioEvent(ref eventId);
                    if (timelineEvent == null || eventId < 0)
                    {
                        LogWarning($"Timeline event creation failed: event={definition.Name}, action={definition.TimelineActionId}.");
                        continue;
                    }
                    LogInfo($"Created reusable Random Events timeline entry: event={definition.Name}, initialId={eventId}.");
                }
                else
                {
                    timelineEvent = EngineInterface.GetScenarioEvent(eventId);
                }

                ConfigureTimelineEvent(
                    timelineEvent,
                    definition,
                    active,
                    dueAbsoluteMonth,
                    successfulKinds,
                    successfulStrengths);
                EngineInterface.ApplyScenarioEvent(eventId, timelineEvent);
                int sortedId = FindOwnedTimelineEntry(definition.Kind);
                LogInfo(
                    $"Timeline event synchronized: event={definition.Name}, action={definition.TimelineActionId}, " +
                    $"active={active}, dueAbsoluteMonth={dueAbsoluteMonth}, sortedId={sortedId}.");
            }
            RefreshTimelineIds();
        }

        private void ConfigureTimelineEvent(
            EngineInterface.tl_event timelineEvent,
            RandomEventDefinition definition,
            bool active,
            int dueAbsoluteMonth,
            int[] successfulKinds,
            int[] successfulStrengths)
        {
            ToDate(dueAbsoluteMonth, out int year, out int month);
            timelineEvent.month = month;
            timelineEvent.year = year;
            timelineEvent.tl_type = 3;
            timelineEvent.done = active ? (short)0 : (short)1;
            timelineEvent.pre_done = 0;
            timelineEvent.action = definition.TimelineActionId;
            timelineEvent.action_data = definition.StrengthKind == RandomEventStrengthKind.GranaryTheft && active
                ? GetPreparedTimelineStrength(definition.Kind, successfulKinds, successfulStrengths)
                : 0;
            timelineEvent.and_or = 0;
            timelineEvent.repeat = 0;
            timelineEvent.repeat_count = 0;
            EnsureConditions(timelineEvent);
            SetTag(timelineEvent, definition.Kind);
        }

        private static int GetPreparedTimelineStrength(RandomEventKind kind, int[] kinds, int[] strengths)
        {
            for (int index = 0; index < kinds.Length; index++)
            {
                if (kinds[index] == (int)kind)
                    return index < strengths.Length ? strengths[index] : 0;
            }
            return 0;
        }

        private int FindOwnedTimelineEntry(RandomEventKind kind)
        {
            EngineInterface.ScenarioOverview overview = EngineInterface.GetScenarioOverview();
            if (overview?.entries == null)
                return -1;
            for (int eventId = 0; eventId < overview.entries.Count; eventId++)
            {
                if (overview.entries[eventId].entryType != 3)
                    continue;
                try
                {
                    EngineInterface.tl_event timelineEvent = EngineInterface.GetScenarioEvent(eventId);
                    if (HasTag(timelineEvent, kind))
                        return eventId;
                }
                catch (Exception ex)
                {
                    LogWarning($"Timeline entry scan skipped unreadable eventId={eventId}: {ex.Message}");
                }
            }
            return -1;
        }

        private void AuditPendingTimelineEntries(int currentAbsoluteMonth)
        {
            if (pendingTimelineAuditDue == int.MinValue)
                return;

            // Read Vanilla's result before PrepareBatch reuses and resets these five records.
            AuditTimelineEntries(
                "following-tick",
                pendingTimelineAuditDue,
                currentAbsoluteMonth,
                pendingTimelineAuditKinds);
            pendingTimelineAuditDue = int.MinValue;
            pendingTimelineAuditKinds = Array.Empty<int>();
        }

        private void AuditTimelineEntries(string phase, int due, int currentAbsoluteMonth, int[] kinds)
        {
            foreach (int kindValue in kinds ?? Array.Empty<int>())
            {
                RandomEventKind kind = (RandomEventKind)kindValue;
                RandomEventDefinition definition = RandomEventDefinitions.Get(kind);
                int eventId = FindOwnedTimelineEntry(kind);
                if (eventId < 0)
                {
                    LogInfo(
                        $"Timeline execution audit: phase={phase}, event={definition.Name}, dueAbsoluteMonth={due}, " +
                        $"currentAbsoluteMonth={currentAbsoluteMonth}, status=missing-owned-entry.");
                    continue;
                }

                try
                {
                    EngineInterface.tl_event timelineEvent = EngineInterface.GetScenarioEvent(eventId);
                    string status = timelineEvent.done != 0 ? "consumed-by-vanilla" : "not-consumed";
                    LogInfo(
                        $"Timeline execution audit: phase={phase}, event={definition.Name}, eventId={eventId}, " +
                        $"dueAbsoluteMonth={due}, currentAbsoluteMonth={currentAbsoluteMonth}, status={status}, " +
                        $"done={timelineEvent.done}, preDone={timelineEvent.pre_done}, type={timelineEvent.tl_type}, " +
                        $"action={timelineEvent.action}, actionData={timelineEvent.action_data}, " +
                        $"eventDate={timelineEvent.year}/{timelineEvent.month}.");
                }
                catch (Exception ex)
                {
                    LogInfo(
                        $"Timeline execution audit: phase={phase}, event={definition.Name}, eventId={eventId}, " +
                        $"dueAbsoluteMonth={due}, currentAbsoluteMonth={currentAbsoluteMonth}, " +
                        $"status=unreadable, reason={ex.Message}.");
                }
            }
        }

        private void RefreshTimelineIds()
        {
            int[] ids = new int[RandomEventDefinitions.TimelineOnly.Length];
            for (int index = 0; index < ids.Length; index++)
                ids[index] = FindOwnedTimelineEntry(RandomEventDefinitions.TimelineOnly[index].Kind);
            state.TimelineEntryIds = ids;
        }

        private static void EnsureConditions(EngineInterface.tl_event timelineEvent)
        {
            if (timelineEvent.event_value == null || timelineEvent.event_value.Length != 40)
                timelineEvent.event_value = new EngineInterface.ev[40];
            for (int index = 0; index < timelineEvent.event_value.Length; index++)
            {
                if (timelineEvent.event_value[index] == null)
                    timelineEvent.event_value[index] = new EngineInterface.ev();
            }
        }

        private static void SetTag(EngineInterface.tl_event timelineEvent, RandomEventKind kind)
        {
            short[] values = { TagMagic, TagVersion, (short)((int)kind + 1), TagGuard };
            for (int offset = 0; offset < values.Length; offset++)
            {
                EngineInterface.ev condition = timelineEvent.event_value[TagStartIndex + offset];
                condition.value = values[offset];
                condition.type = 0;
                condition.onoff = 0; // Vanilla skips type/value completely while onoff is zero.
            }
        }

        private static bool HasTag(EngineInterface.tl_event timelineEvent, RandomEventKind kind)
        {
            if (timelineEvent?.event_value == null || timelineEvent.event_value.Length < 40)
                return false;
            short[] values = { TagMagic, TagVersion, (short)((int)kind + 1), TagGuard };
            for (int offset = 0; offset < values.Length; offset++)
            {
                EngineInterface.ev condition = timelineEvent.event_value[TagStartIndex + offset];
                if (condition == null || condition.onoff != 0 || condition.value != values[offset])
                    return false;
            }
            return true;
        }

        private byte[] SaveState(SaveContext context)
        {
            if (!context.IsSaveFile || !mapActive || state == null ||
                Shared.GameModeHelper.IsRealMultiplayer(mapStartedFromMultiplayerSave))
                return null;
            byte[] bytes = MessagePackSerializer.Serialize(state);
            LogInfo($"Saved Random Events state: bytes={bytes.Length}, nextDueAbsoluteMonth={state.NextDueAbsoluteMonth}, batchPrepared={state.BatchPrepared}.");
            return bytes;
        }

        private void LoadState(byte[] bytes, LoadContext context)
        {
            if (!context.IsSaveFile)
                return;
            try
            {
                state = MessagePackSerializer.Deserialize<RandomEventsSaveStateV1>(bytes);
                loadedStateAvailable = state != null;
                mapStartPending = true;
                LogInfo($"Loaded Random Events state bytes: bytes={bytes.Length}, version={state?.Version}.");
            }
            catch (Exception ex)
            {
                state = null;
                loadedStateAvailable = false;
                LogError($"Random Events state could not be deserialized and will be initialized fresh: {ex}");
            }
        }

        private bool ValidateLoadedState(RandomEventsSaveStateV1 loaded)
        {
            bool valid = loaded != null && loaded.Version == RandomEventsSaveStateV1.CurrentVersion &&
                loaded.IntervalMonths >= 1 && loaded.IntervalMonths <= 90 &&
                loaded.Chances?.Length == RandomEventDefinitions.All.Length &&
                loaded.StrengthMinimums?.Length == 6 && loaded.StrengthMaximums?.Length == 6 &&
                loaded.PreparedDirectKinds != null && loaded.PreparedDirectStrengths != null &&
                loaded.PreparedDirectKinds.Length == loaded.PreparedDirectStrengths.Length &&
                loaded.PreparedDirectTargetPlayerIds != null &&
                loaded.PreparedDirectKinds.Length == loaded.PreparedDirectTargetPlayerIds.Length &&
                loaded.PreparedTimelineKinds != null && loaded.PreparedTimelineStrengths != null &&
                loaded.PreparedTimelineKinds.Length == loaded.PreparedTimelineStrengths.Length &&
                (loaded.PrngState0 | loaded.PrngState1) != 0;
            if (valid)
            {
                int currentAbsoluteMonth = GetCurrentAbsoluteMonth();
                // Reject states written with an incompatible calendar basis instead of waiting centuries.
                valid = loaded.NextDueAbsoluteMonth >= currentAbsoluteMonth &&
                    loaded.NextDueAbsoluteMonth <= checked(currentAbsoluteMonth + loaded.IntervalMonths);
                if (!valid)
                {
                    LogWarning(
                        $"Loaded Random Events state uses an implausible event date and will be initialized fresh: " +
                        $"currentAbsoluteMonth={currentAbsoluteMonth}, loadedNextDueAbsoluteMonth={loaded.NextDueAbsoluteMonth}, " +
                        $"interval={loaded.IntervalMonths}, effectiveMonthsPerYear={VanillaMonthsPerYear}.");
                }
            }
            if (!valid)
                LogWarning("Loaded Random Events V1 state failed validation and will not be used.");
            return valid;
        }

        private void DispatchDirectEvent(
            RandomEventDefinition definition,
            int strength,
            int targetPlayerId)
        {
            if (!GamePlayerManagerAPI.Instance.IsPlayerIdValid(targetPlayerId))
            {
                LogError(
                    $"Vanilla direct event skipped: event={definition.Name}, targetPlayerId={targetPlayerId}, " +
                    "reason=invalid target player.");
                return;
            }

            IDisposable signpostScope = null;
            int signpostBuildingId = -1;
            double signpostDistance = 0;
            if (definition.RequiresSignpost &&
                !signpostRegistry.TryBeginTargetedEvent(
                    targetPlayerId,
                    definition.Kind == RandomEventKind.Rabbits,
                    out signpostScope,
                    out signpostBuildingId,
                    out signpostDistance,
                    out string signpostFailure))
            {
                LogError(
                    $"Vanilla direct event skipped: event={definition.Name}, targetPlayerId={targetPlayerId}, " +
                    $"reason=required signpost unavailable or could not be prioritized ({signpostFailure}).");
                return;
            }

            int originalLocalPlayerId = GamePlayerManagerAPI.Instance.GetLocalPlayerId();
            IntPtr localPlayerAddress = new IntPtr(unchecked((long)GameGlobalsManager.Instance.LocalPlayerIdVA));
            bool playerChanged = targetPlayerId != originalLocalPlayerId;
            int rabbitsAliveBefore = 0;
            int rabbitsNeedsInitBefore = 0;
            try
            {
                if (playerChanged)
                {
                    if (localPlayerAddress == IntPtr.Zero)
                    {
                        LogError(
                            $"Vanilla direct event skipped: event={definition.Name}, targetPlayerId={targetPlayerId}, " +
                            "reason=native local-player address unavailable for explicit targeting.");
                        return;
                    }

                    Marshal.WriteInt32(localPlayerAddress, targetPlayerId);
                    if (Marshal.ReadInt32(localPlayerAddress) != targetPlayerId)
                    {
                        LogError(
                            $"Vanilla direct event skipped: event={definition.Name}, targetPlayerId={targetPlayerId}, " +
                            "reason=native target-player switch verification failed.");
                        return;
                    }
                }

                if (definition.Kind == RandomEventKind.Rabbits)
                    CountRabbitUnits(out rabbitsAliveBefore, out rabbitsNeedsInitBefore);

                EngineInterface.GameAction(Enums.GameActionCommand.FreeBuild_Event, definition.TextId, strength);
                if (definition.Kind == RandomEventKind.Rabbits)
                {
                    pendingRabbitAuditTick = checked(lastObservedGameTick + 30);
                    pendingRabbitAuditTargetPlayerId = targetPlayerId;
                    pendingRabbitAuditSignpostBuildingId = signpostBuildingId;
                    pendingRabbitAuditBaselineCount = rabbitsAliveBefore + rabbitsNeedsInitBefore;
                    LogInfo(
                        $"Rabbit outcome audit scheduled: auditTick={pendingRabbitAuditTick}, " +
                        $"targetPlayerId={targetPlayerId}, signpostBuildingId={signpostBuildingId}, " +
                        $"baselineRabbitsAlive={rabbitsAliveBefore}, baselineRabbitsNeedsInit={rabbitsNeedsInitBefore}.");
                }
                LogInfo(
                    $"Vanilla direct event dispatched: event={definition.Name}, textId={definition.TextId}, " +
                    $"strength={strength}, targetPlayerId={targetPlayerId}, signpostBuildingId={signpostBuildingId}, " +
                    $"signpostDistanceToTargetKeep={signpostDistance:0.00}. " +
                    "Vanilla assigns its own event-specific movement, attack, or aggression behavior; " +
                    "the managed wrapper cannot distinguish a visible effect " +
                    "from a prerequisite-driven native no-op.");
            }
            finally
            {
                if (playerChanged && localPlayerAddress != IntPtr.Zero)
                    Marshal.WriteInt32(localPlayerAddress, originalLocalPlayerId);
                signpostScope?.Dispose();
            }
        }

        private int GetCurrentAbsoluteMonth()
        {
            int currentYear = GameTimeManagerAPI.Instance.GetCurrentYear();
            int currentMonth = GameTimeManagerAPI.Instance.GetCurrentMonth();
            ValidateCalendarApi(currentYear, currentMonth);
            return checked(currentYear * VanillaMonthsPerYear + currentMonth);
        }

        private void LogClockHeartbeat(int tick, int currentAbsoluteMonth)
        {
            long now = Stopwatch.GetTimestamp();
            bool monthChanged = currentAbsoluteMonth != lastLoggedAbsoluteMonth;
            // Periodic output distinguishes a frozen calendar API from a stopped persistent tick hook.
            bool periodic = lastClockLogTimestamp == 0 ||
                (now - lastClockLogTimestamp) >= Stopwatch.Frequency * 30L;
            if (!monthChanged && !periodic)
                return;

            int currentYear = GameTimeManagerAPI.Instance.GetCurrentYear();
            int currentMonth = GameTimeManagerAPI.Instance.GetCurrentMonth();
            int apiMonthsPerYear = GameTimeManagerAPI.Instance.GetMonthsInYear();
            LogInfo(
                $"Game-time heartbeat: reason={(monthChanged ? "month-changed" : "periodic")}, tick={tick}, " +
                $"year={currentYear}, month={currentMonth}, apiMonthsPerYear={apiMonthsPerYear}, " +
                $"effectiveMonthsPerYear={VanillaMonthsPerYear}, " +
                $"absoluteMonth={currentAbsoluteMonth}, nextDueAbsoluteMonth={state.NextDueAbsoluteMonth}, " +
                $"batchPrepared={state.BatchPrepared}.");
            lastLoggedAbsoluteMonth = currentAbsoluteMonth;
            lastClockLogTimestamp = now;
        }

        private int GetBatchTargetPlayerId(int[] targetPlayerIds)
        {
            if (targetPlayerIds != null)
            {
                foreach (int playerId in targetPlayerIds)
                {
                    if (GamePlayerManagerAPI.Instance.IsPlayerIdValid(playerId))
                        return playerId;
                }
            }

            return GamePlayerManagerAPI.Instance.GetLocalPlayerId();
        }

        private unsafe string CapturePrerequisiteSnapshot(int targetPlayerId)
        {
            int wheatAlive = 0;
            int hopsAlive = 0;
            int appleAlive = 0;
            int cattleAlive = 0;
            int granaryAlive = 0;
            int relevantNeedsInit = 0;

            Span<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            for (int index = 0; index < buildings.Length; index++)
            {
                ref GameBuilding building = ref buildings[index];
                if (building.r_PlayerIdOwner != targetPlayerId)
                    continue;

                bool relevant = building.r_BuildingType == eStructs.STRUCT_WHEATFARM ||
                    building.r_BuildingType == eStructs.STRUCT_HOPSFARM ||
                    building.r_BuildingType == eStructs.STRUCT_APPLEFARM ||
                    building.r_BuildingType == eStructs.STRUCT_CATTLEFARM ||
                    building.r_BuildingType == eStructs.STRUCT_GRANARY;
                if (!relevant)
                    continue;
                if (building.r_AliveState == AliveState.NeedsInit)
                {
                    relevantNeedsInit++;
                    continue;
                }
                // Vanilla's event prerequisite searches only accept fully alive structures.
                if (building.r_AliveState != AliveState.IsAlive)
                    continue;

                switch (building.r_BuildingType)
                {
                    case eStructs.STRUCT_WHEATFARM: wheatAlive++; break;
                    case eStructs.STRUCT_HOPSFARM: hopsAlive++; break;
                    case eStructs.STRUCT_APPLEFARM: appleAlive++; break;
                    case eStructs.STRUCT_CATTLEFARM: cattleAlive++; break;
                    case eStructs.STRUCT_GRANARY: granaryAlive++; break;
                }
            }

            CountRabbitUnits(out int rabbitsAlive, out int rabbitsNeedsInit);

            int registeredSignposts = 0;
            foreach (int buildingId in signpostRegistry.ReadRegisteredBuildingIds())
            {
                if (buildingId > 0)
                    registeredSignposts++;
            }

            if (!GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(targetPlayerId, out GamePlayerResources* resources) ||
                resources == null)
            {
                return
                    $"targetPlayerId={targetPlayerId}, playerResources=unavailable, " +
                    $"farmsAlive[wheat={wheatAlive},hops={hopsAlive},apple={appleAlive},cattle={cattleAlive}], " +
                    $"granariesAlive={granaryAlive}, relevantBuildingsNeedsInit={relevantNeedsInit}, " +
                    $"rabbitsAlive={rabbitsAlive}, rabbitsNeedsInit={rabbitsNeedsInit}, " +
                    $"registeredSignposts={registeredSignposts}, " +
                    "nativeRabbitActiveState=not-exposed-by-stable-api";
            }

            return
                $"targetPlayerId={targetPlayerId}, population={resources->r_TotalPopulation}, " +
                $"plagueEligibilityField0x02B8={resources->N00003A4E}, firstGranaryId={resources->r_FirstGranaryId}, " +
                $"food[bread={resources->r_FoodStockBread},cheese={resources->r_FoodStockCheese}," +
                $"meat={resources->r_FoodStockMeat},fruit={resources->r_FoodStockFruit},total={resources->r_FoodStockTotal}], " +
                $"farmsAlive[wheat={wheatAlive},hops={hopsAlive},apple={appleAlive},cattle={cattleAlive}], " +
                $"granariesAlive={granaryAlive}, relevantBuildingsNeedsInit={relevantNeedsInit}, " +
                $"rabbitsAlive={rabbitsAlive}, rabbitsNeedsInit={rabbitsNeedsInit}, " +
                $"registeredSignposts={registeredSignposts}, " +
                "nativeRabbitActiveState=not-exposed-by-stable-api";
        }

        private void AuditPendingRabbitOutcome(int tick)
        {
            if (pendingRabbitAuditTick == int.MinValue || tick < pendingRabbitAuditTick)
                return;

            int targetPlayerId = pendingRabbitAuditTargetPlayerId;
            int signpostBuildingId = pendingRabbitAuditSignpostBuildingId;
            int baselineCount = pendingRabbitAuditBaselineCount;
            pendingRabbitAuditTick = int.MinValue;
            pendingRabbitAuditTargetPlayerId = -1;
            pendingRabbitAuditSignpostBuildingId = -1;
            pendingRabbitAuditBaselineCount = 0;

            CountRabbitUnits(out int alive, out int needsInit);
            string details =
                $"targetPlayerId={targetPlayerId}, signpostBuildingId={signpostBuildingId}, " +
                $"baselineRabbitCount={baselineCount}, rabbitsAlive={alive}, rabbitsNeedsInit={needsInit}, " +
                $"rabbitCountDelta={alive + needsInit - baselineCount}";
            if (alive + needsInit <= baselineCount)
            {
                LogError(
                    $"Rabbit outcome audit found no spawned rabbit units after the Vanilla event call: {details}. " +
                    "Check the preceding prerequisite snapshot and native source-prioritization log.");
                return;
            }

            LogInfo($"Rabbit outcome audit confirmed Vanilla rabbit units: {details}.");
        }

        private static void CountRabbitUnits(out int alive, out int needsInit)
        {
            alive = 0;
            needsInit = 0;
            Span<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
            for (int index = 0; index < units.Length; index++)
            {
                ref GameUnit unit = ref units[index];
                if (unit.r_UnitChimp != eChimps.CHIMP_TYPE_RABBIT)
                    continue;
                if (unit.r_AliveState == AliveState.IsAlive)
                    alive++;
                else if (unit.r_AliveState == AliveState.NeedsInit)
                    needsInit++;
            }
        }

        private void ValidateCalendarApi(int currentYear, int currentMonth)
        {
            int apiMonthsPerYear = GameTimeManagerAPI.Instance.GetMonthsInYear();
            if (currentYear < 0 || currentMonth < 0 || currentMonth >= VanillaMonthsPerYear)
            {
                mapActive = false;
                state = null;
                throw new InvalidOperationException(
                    $"Unsupported Vanilla calendar values year={currentYear}, month={currentMonth}; " +
                    "Random Events was disabled for this map to prevent incorrectly dated events.");
            }

            if (apiMonthsPerYear == VanillaMonthsPerYear || calendarApiFallbackLogged)
                return;

            calendarApiFallbackLogged = true;
            LogWarning(
                $"GameTimeManagerAPI.GetMonthsInYear() returned {apiMonthsPerYear}; " +
                $"Random Events uses the validated Vanilla calendar constant {VanillaMonthsPerYear} instead.");
        }

        private static void ToDate(int absoluteMonth, out int year, out int month)
        {
            year = absoluteMonth / VanillaMonthsPerYear;
            month = absoluteMonth % VanillaMonthsPerYear;
        }

        private void DisableForNetwork(string reason, string details)
        {
            mapStartPending = false;
            mapActive = false;
            state = null;
            if (multiplayerDisableLogged) return;
            multiplayerDisableLogged = true;
            LogInfo(
                $"Random Events fully disabled: {reason}; no rolls, timeline entries, save data, or signposts will be created. " +
                $"Network details: {details}.");
        }

        private void LogInfo(string message) => Shared.DebugLogHelper.LogInfo(log, message);
        private void LogWarning(string message) => Shared.DebugLogHelper.LogWarning(log, message);
        private void LogError(string message) => Shared.DebugLogHelper.LogError(log, message);
    }
}
