using BepInEx.Logging;
using CrusaderDE;
using MessagePack;
using R3;
using SHCDESE.API;
using SHCDESE.API.Components.SaveData;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using System;
using System.Collections.Generic;
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

        private readonly ManualLogSource log;
        private readonly RandomEventsSettingsViewModel settings;
        private readonly ScenarioSignpostRegistry signpostRegistry;
        private readonly SignpostPlacementService signpostPlacement;
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
        private RandomEventsSaveStateV1 state;

        public RandomEventsRuntime(ManualLogSource log, RandomEventsSettingsViewModel settings)
        {
            this.log = log;
            this.settings = settings;
            signpostRegistry = new ScenarioSignpostRegistry(log);
            signpostPlacement = new SignpostPlacementService(log, signpostRegistry);
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
            mapStartPending = true;
            mapActive = false;
            multiplayerDisableLogged = false;
            mapStartedFromMultiplayerSave = args.bMultiplayerSave != 0;
            timelineRestoredForLoadedState = false;
            lastSignpostAttemptTick = int.MinValue;
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
            mapStartPending = false;
            mapActive = false;
            loadedStateAvailable = false;
            multiplayerDisableLogged = false;
            mapStartedFromMultiplayerSave = false;
            timelineRestoredForLoadedState = false;
            state = null;
        }

        private void OnGameTick(int tick)
        {
            try
            {
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
                if (!timelineRestoredForLoadedState && loadedStateAvailable && currentAbsoluteMonth < state.NextDueAbsoluteMonth)
                {
                    SynchronizeTimelineEntries(state.PreparedTimelineKinds);
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
            state.PreparedTimelineKinds = timelineKinds.ToArray();
            state.PreparedTimelineStrengths = timelineStrengths.ToArray();
            state.BatchPrepared = true;
            SynchronizeTimelineEntries(state.PreparedTimelineKinds);
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

            // Clear first so a save callback during a Vanilla action cannot persist an executable duplicate.
            state.BatchPrepared = false;
            state.PreparedDirectKinds = Array.Empty<int>();
            state.PreparedDirectStrengths = Array.Empty<int>();
            state.PreparedTimelineKinds = Array.Empty<int>();
            state.PreparedTimelineStrengths = Array.Empty<int>();

            LogInfo($"Event date reached: dueAbsoluteMonth={due}, currentAbsoluteMonth={currentAbsoluteMonth}, directCount={directKinds.Length}.");
            for (int index = 0; index < directKinds.Length; index++)
            {
                RandomEventDefinition definition = RandomEventDefinitions.Get((RandomEventKind)directKinds[index]);
                int strength = index < strengths.Length ? strengths[index] : 0;
                EngineInterface.GameAction(Enums.GameActionCommand.FreeBuild_Event, definition.TextId, strength);
                LogInfo(
                    $"Vanilla direct event dispatched: event={definition.Name}, textId={definition.TextId}, strength={strength}. " +
                    "The managed wrapper cannot distinguish a successful native effect from a prerequisite-driven native no-op.");
            }

            LogInfo("Prepared Timeline events reached their Vanilla execution date; missing native prerequisites are handled as native no-ops without replacement logic.");
            state.NextDueAbsoluteMonth = checked(due + state.IntervalMonths);
            LogInfo($"Next event batch will be prepared on the following game tick: nextDueAbsoluteMonth={state.NextDueAbsoluteMonth}.");
        }

        private void SynchronizeTimelineEntries(int[] successfulKinds)
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

                ConfigureTimelineEvent(timelineEvent, definition, active);
                EngineInterface.ApplyScenarioEvent(eventId, timelineEvent);
                int sortedId = FindOwnedTimelineEntry(definition.Kind);
                LogInfo(
                    $"Timeline event synchronized: event={definition.Name}, action={definition.TimelineActionId}, " +
                    $"active={active}, dueAbsoluteMonth={state.NextDueAbsoluteMonth}, sortedId={sortedId}.");
            }
            RefreshTimelineIds();
        }

        private void ConfigureTimelineEvent(EngineInterface.tl_event timelineEvent, RandomEventDefinition definition, bool active)
        {
            ToDate(state.NextDueAbsoluteMonth, out int year, out int month);
            timelineEvent.month = month;
            timelineEvent.year = year;
            timelineEvent.tl_type = 3;
            timelineEvent.done = active ? (short)0 : (short)1;
            timelineEvent.pre_done = 0;
            timelineEvent.action = definition.TimelineActionId;
            timelineEvent.action_data = definition.StrengthKind == RandomEventStrengthKind.GranaryTheft && active
                ? GetPreparedTimelineStrength(definition.Kind)
                : 0;
            timelineEvent.and_or = 0;
            timelineEvent.repeat = 0;
            timelineEvent.repeat_count = 0;
            EnsureConditions(timelineEvent);
            SetTag(timelineEvent, definition.Kind);
        }

        private int GetPreparedTimelineStrength(RandomEventKind kind)
        {
            int[] kinds = state.PreparedTimelineKinds ?? Array.Empty<int>();
            int[] strengths = state.PreparedTimelineStrengths ?? Array.Empty<int>();
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
                loaded.PreparedTimelineKinds != null && loaded.PreparedTimelineStrengths != null &&
                loaded.PreparedTimelineKinds.Length == loaded.PreparedTimelineStrengths.Length &&
                (loaded.PrngState0 | loaded.PrngState1) != 0;
            if (!valid)
                LogWarning("Loaded Random Events V1 state failed validation and will not be used.");
            return valid;
        }

        private int GetCurrentAbsoluteMonth()
        {
            int monthsPerYear = Math.Max(1, GameTimeManagerAPI.Instance.GetMonthsInYear());
            return checked(GameTimeManagerAPI.Instance.GetCurrentYear() * monthsPerYear + GameTimeManagerAPI.Instance.GetCurrentMonth());
        }

        private static void ToDate(int absoluteMonth, out int year, out int month)
        {
            int monthsPerYear = Math.Max(1, GameTimeManagerAPI.Instance.GetMonthsInYear());
            year = absoluteMonth / monthsPerYear;
            month = absoluteMonth % monthsPerYear;
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
